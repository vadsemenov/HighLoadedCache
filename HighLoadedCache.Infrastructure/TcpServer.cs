using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HighLoadedCache.Services.Abstraction;
using HighLoadedCache.Services.Utils;

namespace HighLoadedCache.Infrastructure;

public class TcpServer : ITcpServer
{
    private readonly ISimpleStore _simpleStore;

    private Socket? _socket;

    private const string IpAddress = "127.0.0.1";
    private const int Port = 8081;

    public TcpServer(ISimpleStore simpleStore)
    {
        _simpleStore = simpleStore;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var localEndpoint = new IPEndPoint(IPAddress.Parse(IpAddress), Port);

            _socket.Bind(localEndpoint);
            _socket.Listen(100);

            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            while (!cancellationToken.IsCancellationRequested)
            {
                _ = Task.Run(async () =>
                {
                    Socket? clientSocket = null;

                    try
                    {
                        clientSocket = await _socket.AcceptAsync(cancellationToken);
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
                        clientSocket?.Dispose();
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
        var buffer = ArrayPool<byte>.Shared.Rent(200);

        try
        {
            while (clientSocket.Connected)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var byteCount = await clientSocket.ReceiveAsync(buffer, SocketFlags.None);

                if (byteCount == 0)
                {
                    break;
                }

                var receivedText = Encoding.UTF8.GetString(buffer, 0, byteCount);

                PrintCommandsToConsole(CommandParser.Parse(receivedText.AsSpan()));

                var commandParts = CommandParser.Parse(receivedText.AsSpan());

                var response = "OK\r\n";

                switch (commandParts.Command)
                {
                    case "SET":
                        _simpleStore.Set(commandParts.Key, commandParts.Value);
                        break;
                    case "GET":
                        var bytes = TryGetStoreValue(commandParts.Key);
                        response = bytes != null ? Encoding.UTF8.GetString(bytes) : "(nil)\r\n";
                        break;
                    case "DEL":
                        _simpleStore.Delete(commandParts.Key);
                        break;
                    case "STA":
                        var statistics = _simpleStore.GetStatistics();
                        response = $"Statistics, set count: {statistics.setCount}, get count: {statistics.getCount}, delete count: {statistics.deleteCount}\r\n";
                        break;
                    default:
                        response = "ERROR\r\n";
                        break;
                }

                await clientSocket.SendAsync(Encoding.UTF8.GetBytes(response), SocketFlags.None);
            }
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

    private byte[]? TryGetStoreValue(ReadOnlySpan<char> commandPartsKey)
    {
        return _simpleStore.Get(commandPartsKey);
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