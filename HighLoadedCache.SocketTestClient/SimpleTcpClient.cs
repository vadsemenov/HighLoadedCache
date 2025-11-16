using System.Buffers;
using System.Net.Sockets;
using System.Text;

namespace HighLoadedCache.SocketTestClient;

public class SimpleTcpClient : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly string _host;
    private readonly int _port;
    private NetworkStream _stream;


    public SimpleTcpClient(string? host = null, int? port = null)
    { 
        _client = new TcpClient();
        _host = host ?? "127.0.0.1";
        _port = port ?? 8081;
    }
    
    public async Task ConnectAsync()
    {
        await _client.ConnectAsync(_host, _port);
        
        _stream = _client.GetStream();
    }

    public async Task SetAsync(string key, string value)
    {
        var arrayPool = ArrayPool<byte>.Shared.Rent(1024);
        
        try
        {
            arrayPool = Encoding.UTF8.GetBytes(string.Concat("SET ", key, " ", value));
            await _stream.WriteAsync(arrayPool, 0, arrayPool.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(arrayPool);
        }
    }

    public async Task<string> GetAsync(string key)
    {
        var arrayPool = ArrayPool<byte>.Shared.Rent(1024);
        var responseBuffer = ArrayPool<byte>.Shared.Rent(1024);
        
        string? response = null;
        
        try
        {
            arrayPool = Encoding.UTF8.GetBytes(string.Concat("GET ", key));
            await _stream.WriteAsync(arrayPool, 0, arrayPool.Length);
            
            var byteCount = await _stream.ReadAsync(responseBuffer);
            response = Encoding.UTF8.GetString(responseBuffer, 0, byteCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(arrayPool);
            ArrayPool<byte>.Shared.Return(responseBuffer);
        }
        
        return response;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        _client.Dispose();
    }
}