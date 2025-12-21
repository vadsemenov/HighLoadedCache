using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
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

    private readonly Counter<long> _connectionsCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>("tcp.connections.total", "count", "Total accepted connections");

    private readonly UpDownCounter<long> _activeConnectionsCounter =
        DiagnosticsConfig.Meter.CreateUpDownCounter<long>("tcp.connections.active", "count", "Current active connections");

    private readonly Counter<long> _bytesReceivedCounter =
        DiagnosticsConfig.Meter.CreateCounter<long>("tcp.bytes_received", "bytes", "Total bytes received");

    private readonly Counter<long> _processedCommandsCounter = DiagnosticsConfig.Meter.CreateCounter<long>(
        "tcp.commands.processed.total",
        "count",
        "Total processed commands");
    private readonly Histogram<double> _commandProcessingTimeHistogram = DiagnosticsConfig.Meter.CreateHistogram<double>(
        "tcp.commands.duration",
        "ms",
        "Command processing duration");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var serverActivity = DiagnosticsConfig.ActivitySource.StartActivity(nameof(TcpServer)) ?? new Activity(nameof(TcpServer)).Start();

        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var localEndpoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);

            _socket.Bind(localEndpoint);
            _socket.Listen(100);

            Console.WriteLine("Сервер запущен. Ожидание подключений...");
            serverActivity.SetTag("server.port", _port.ToString());
            serverActivity.SetStatus(ActivityStatusCode.Ok);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _connectionsSemaphore.WaitAsync(cancellationToken);

                    Socket clientSocket = await _socket.AcceptAsync(cancellationToken);
                    Console.WriteLine("Создано новое подключение с клиентом.");

                    _connectionsCounter.Add(1);
                    _activeConnectionsCounter.Add(1);

                    _ = Task.Run(async () =>
                    {
                        using var activity = DiagnosticsConfig.ActivitySource.StartActivity("ClientSession") ?? new Activity("ClientSession").Start();

                        try
                        {
                            if (clientSocket.RemoteEndPoint is IPEndPoint ep)
                            {
                                activity?.AddTag("client.ip", ep.Address.ToString());
                            }

                            await ProcessAsync(clientSocket, cancellationToken);
                        }
                        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                        {
                            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                            Console.WriteLine("Подключение отменено.");
                        }
                        catch (Exception ex)
                        {
                            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                            Console.WriteLine("Ошибка подключения.");
                        }
                        finally
                        {
                            clientSocket.Dispose();
                            _connectionsSemaphore.Release();

                            _activeConnectionsCounter.Add(-1);

                            Console.WriteLine("Клиент отключен.");
                        }
                    }, cancellationToken);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Ошибка при принятии подключения: {exception}");
                }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            throw;
        }
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
                _bytesReceivedCounter.Add(bytesRead);

                int delimiterIndex = Array.IndexOf(buffer, (byte)'\n', 0, accumulatedBytes);

                if (delimiterIndex >= 0)
                {
                    var messageSpan = new ReadOnlySpan<byte>(buffer, 0, delimiterIndex);
                    var receivedText = Encoding.UTF8.GetString(messageSpan);

                    var response = ProcessCommandAsync(receivedText);

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

    private string ProcessCommandAsync(string receivedText)
    {
        var stopwatch = Stopwatch.StartNew();

        using var activity = DiagnosticsConfig.ActivitySource.StartActivity("ProcessCommand");

        PrintCommandsToConsole(CommandParser.Parse(receivedText.AsSpan()));
        var commandParts = CommandParser.Parse(receivedText.AsSpan());
        var response = "OK\r\n";

        activity?.AddTag("command.type", commandParts.Command.ToString());
        activity?.AddTag("command.key", commandParts.Key.ToString());

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

        stopwatch.Stop();
        var durationMs = stopwatch.Elapsed.TotalMilliseconds;

        _processedCommandsCounter.Add(1, new KeyValuePair<string, object?>("command.name", commandParts.Command.ToString()));
        _commandProcessingTimeHistogram.Record(durationMs, new KeyValuePair<string, object?>("command.name", commandParts.Command.ToString()));

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