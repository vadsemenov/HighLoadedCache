using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace HighLoadedCache.SocketTestClient;

public class SimpleTcpClient(string? host = null, int? port = null) : IAsyncDisposable
{
    private readonly TcpClient _client = new();
    private readonly string _host = host ?? "127.0.0.1";
    private readonly int _port = port ?? 8081;
    private NetworkStream? _stream;


    public async Task ConnectAsync()
    {
        await _client.ConnectAsync(_host, _port);

        _stream = _client.GetStream();
    }

    public async Task SetAsync(string key, string value)
    {
        var arrayPool = ArrayPool<byte>.Shared;
        var rented = arrayPool.Rent(Encoding.UTF8.GetMaxByteCount(4 + key.Length + 1 + value.Length));

        try
        {
            var text = string.Concat("SET ", key, " ", value, '\n');
            var count = Encoding.UTF8.GetBytes(text.AsSpan(), rented);

            if (_stream == null)
                throw new InvalidOperationException("Stream is not initialized");

            await _stream.WriteAsync(rented.AsMemory(0, count)).ConfigureAwait(false);
        }
        finally
        {
            arrayPool.Return(rented, clearArray: false);
        }
    }

    public async Task<string> GetAsync(string key)
    {
        var arrayPool = ArrayPool<byte>.Shared;

        var command = string.Concat("GET ", key);
        var maxCmdBytes = Encoding.UTF8.GetMaxByteCount(command.Length);

        var cmdBuffer = arrayPool.Rent(maxCmdBytes);
        var responseBuffer = arrayPool.Rent(1024);

        try
        {
            var cmdLen = Encoding.UTF8.GetBytes(command.AsSpan(), cmdBuffer);

            if (_stream == null)
                throw new InvalidOperationException("Stream is not initialized");

            await _stream.WriteAsync(cmdBuffer.AsMemory(0, cmdLen)).ConfigureAwait(false);

            var byteCount = await _stream.ReadAsync(responseBuffer).ConfigureAwait(false);
            return Encoding.UTF8.GetString(responseBuffer, 0, byteCount);
        }
        finally
        {
            arrayPool.Return(cmdBuffer);
            arrayPool.Return(responseBuffer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
            await _stream.DisposeAsync();

        _client.Dispose();
    }
}