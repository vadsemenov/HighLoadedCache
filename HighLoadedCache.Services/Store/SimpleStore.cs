using System.Text;
using HighLoadedCache.Services.Abstraction;

namespace HighLoadedCache.Services.Store;

public class SimpleStore : ISimpleStore, IDisposable
{
    private readonly Dictionary<string, byte[]> _store = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private long _setCount;
    private long _getCount;
    private long _deleteCount;

    public void Set(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        Interlocked.Increment(ref _setCount);

        _lock.EnterWriteLock();
        try
        {
            _store[key.ToString()] = Encoding.UTF8.GetBytes(value.ToArray());
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public byte[]? Get(ReadOnlySpan<char> key)
    {
        Interlocked.Increment(ref _getCount);

        _lock.EnterReadLock();
        try
        {
            return _store.GetValueOrDefault(key.ToString());
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