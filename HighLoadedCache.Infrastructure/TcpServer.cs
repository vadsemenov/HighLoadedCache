using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HighLoadedCache.Domain;
using HighLoadedCache.Domain.Dto;
using HighLoadedCache.Services.Abstraction;
using HighLoadedCache.Services.Utils;
using Microsoft.Extensions.Options;

namespace HighLoadedCache.Infrastructure;

public class TcpServer(ISimpleStore simpleStore, IOptions<TcpSettings> tcpSettings)
    : ITcpServer
{
    private Socket? _socket;

    private const int MaxMessageSize = 4096;

    private readonly string _ipAddress = tcpSettings.Value.IpAddress;
    private readonly int _port = tcpSettings.Value.Port;

    private readonly SemaphoreSlim _connectionsSemaphore = new(tcpSettings.Value.MaxConnections);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var localEndpoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);

            _socket.Bind(localEndpoint);
            _socket.Listen(100);

            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            while (!cancellationToken.IsCancellationRequested)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _connectionsSemaphore.WaitAsync(cancellationToken);

                        using Socket clientSocket = await _socket.AcceptAsync(cancellationToken);
                        Console.WriteLine("Создано новое подключение с клиентом.");

                        await ProcessAsync(clientSocket, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Подключение отменено.");
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"Ошибка при принятии подключения: {exception}");
                    }
                    finally
                    {
                        _connectionsSemaphore.Release();
                        Console.WriteLine("Клиент отключен.");
                    }
                }, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }

        return Task.CompletedTask;
    }

    private async Task ProcessAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        try
        {
            var welcomeMessage = "Подключение к серверу установлено\n";
            var welcomeBytes = Encoding.UTF8.GetBytes(welcomeMessage);
            await clientSocket.SendAsync(welcomeBytes, SocketFlags.None);

            await ReceiveDataAsync(clientSocket, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки клиента: {ex.Message}");
        }
    }

    private async Task ReceiveDataAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(MaxMessageSize);
        var accumulatedBytes = 0;

        try
        {
            while (clientSocket.Connected)
            {
                if (cancellationToken.IsCancellationRequested) return;

                if (accumulatedBytes >= MaxMessageSize)
                {
                    Console.WriteLine($"Клиент превысил лимит сообщения ({MaxMessageSize} байт). Разрыв соединения.");
                    break;
                }

                var bytesRead = await clientSocket.ReceiveAsync(
                    new Memory<byte>(buffer, accumulatedBytes, MaxMessageSize - accumulatedBytes),
                    SocketFlags.None, cancellationToken);

                if (bytesRead == 0) break;

                accumulatedBytes += bytesRead;

                int delimiterIndex = Array.IndexOf(buffer, (byte)'\n', 0, accumulatedBytes);

                if (delimiterIndex >= 0)
                {
                    var messageSpan = new ReadOnlySpan<byte>(buffer, 0, delimiterIndex);
                    var receivedText = Encoding.UTF8.GetString(messageSpan);

                    var response = ProcessCommandAsync(clientSocket, receivedText);

                    await clientSocket.SendAsync(Encoding.UTF8.GetBytes(response), SocketFlags.None);

                    accumulatedBytes = 0;
                }
                else
                {
                    if (accumulatedBytes < MaxMessageSize) continue;

                    Console.WriteLine($"Получена команда без завершающего символа длиной {accumulatedBytes} байт. Разрыв соединения.");
                    break;
                }
            }

            // while (clientSocket.Connected)
            // {
            //     if (cancellationToken.IsCancellationRequested)
            //     {
            //         return;
            //     }
            //
            //     var byteCount = await clientSocket.ReceiveAsync(buffer, SocketFlags.None);
            //
            //     if (byteCount == 0)
            //     {
            //         break;
            //     }
            //
            //     var receivedText = Encoding.UTF8.GetString(buffer, 0, byteCount);
            //
            //     PrintCommandsToConsole(CommandParser.Parse(receivedText.AsSpan()));
            //
            //     var commandParts = CommandParser.Parse(receivedText.AsSpan());
            //
            //     var response = "OK\r\n";
            //
            //     switch (commandParts.Command)
            //     {
            //         case "SET":
            //             simpleStore.Set(commandParts.Key.ToString(), JsonSerializer.Deserialize<UserProfile>(commandParts.Value)!);
            //             break;
            //         case "GET":
            //             var userProfile = TryGetStoreValue(commandParts.Key.ToString());
            //             response = userProfile != null ? JsonSerializer.Serialize(userProfile) : "(nil)\r\n";
            //             break;
            //         case "DEL":
            //             simpleStore.Delete(commandParts.Key);
            //             break;
            //         case "STA":
            //             var statistics = simpleStore.GetStatistics();
            //             response = $"Statistics, set count: {statistics.setCount}, get count: {statistics.getCount}, delete count: {statistics.deleteCount}\r\n";
            //             break;
            //         default:
            //             response = "ERROR\r\n";
            //             break;
            //     }
            //
            //     await clientSocket.SendAsync(Encoding.UTF8.GetBytes(response), SocketFlags.None);
            // }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
            Console.WriteLine("Клиент разорвал соединение");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при получении данных: {ex.Message}");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private string ProcessCommandAsync(Socket clientSocket, string receivedText)
    {
        PrintCommandsToConsole(CommandParser.Parse(receivedText.AsSpan()));
        var commandParts = CommandParser.Parse(receivedText.AsSpan());
        var response = "OK\r\n";

        switch (commandParts.Command)
        {
            case "SET":
                simpleStore.Set(commandParts.Key.ToString(), JsonSerializer.Deserialize<UserProfile>(commandParts.Value)!);
                break;
            case "GET":
                var userProfile = TryGetStoreValue(commandParts.Key.ToString());
                response = userProfile != null ? JsonSerializer.Serialize(userProfile) : "(nil)\r\n";
                break;
            case "DEL":
                simpleStore.Delete(commandParts.Key);
                break;
            case "STA":
                var statistics = simpleStore.GetStatistics();
                response = $"Statistics, set count: {statistics.setCount}, get count: {statistics.getCount}, delete count: {statistics.deleteCount}\r\n";
                break;
            default:
                response = "ERROR\r\n";
                break;
        }

        return response;
    }

    private UserProfile? TryGetStoreValue(string commandPartsKey)
    {
        return simpleStore.Get(commandPartsKey);
    }

    private void PrintCommandsToConsole(CommandParts commands)
    {
        Console.WriteLine($"Команда {commands.Command}, ключ {commands.Key}, значение {commands.Value}");
    }

    public void Dispose()
    {
        _socket?.Dispose();
    }
}