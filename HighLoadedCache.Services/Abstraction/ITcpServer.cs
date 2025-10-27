namespace HighLoadedCache.Services.Abstraction;

public interface ITcpServer : IDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
}