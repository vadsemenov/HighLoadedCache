using System.Net.Sockets;

namespace HighLoadedCache.SocketTestClient;

public class SimpleTcpClient : IAsyncDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream? _stream;
    private readonly string _host;
    private readonly int _port;

    public SimpleTcpClient(string host, int port)
    {
        _host = host;
        _port = port;
        _client.NoDelay = true;
    }

    public async Task ConnectAsync()
    {
        await _client.ConnectAsync(_host, _port);
        _stream = _client.GetStream();
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data)
    {
        if (_stream != null) await _stream.WriteAsync(data);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null) await _stream.DisposeAsync();
        _client.Dispose();
    }
}