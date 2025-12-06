using System.Text.Json;
using HighLoadedCache.Domain.Dto;
using HighLoadedCache.Services.Abstraction;

namespace HighLoadedCache.Services.Store;

public class SimpleStore : ISimpleStore, IDisposable
{
    private readonly Dictionary<string, byte[]> _store = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private long _setCount;
    private long _getCount;
    private long _deleteCount;

    public void Set(string key, UserProfile userProfile)
    {
        Interlocked.Increment(ref _setCount);

        _lock.EnterWriteLock();
        try
        {
            _store[key] = JsonSerializer.SerializeToUtf8Bytes(userProfile);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public UserProfile? Get(string key)
    {
        Interlocked.Increment(ref _getCount);

        _lock.EnterReadLock();
        try
        {
            var bytes = _store.GetValueOrDefault(key);

            return JsonSerializer.Deserialize<UserProfile>(bytes);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Delete(ReadOnlySpan<char> key)
    {
        Interlocked.Increment(ref _deleteCount);

        _lock.EnterWriteLock();
        try
        {
            _store.Remove(key.ToString());
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public (long setCount, long getCount, long deleteCount) GetStatistics()
        => (Interlocked.Read(ref _setCount),
            Interlocked.Read(ref _getCount),
            Interlocked.Read(ref _deleteCount));

    public void Dispose() => _lock.Dispose();
}