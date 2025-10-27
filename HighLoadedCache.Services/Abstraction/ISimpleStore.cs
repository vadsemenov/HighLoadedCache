namespace HighLoadedCache.Services.Abstraction;

public interface ISimpleStore
{
    void Set(ReadOnlySpan<char> key, ReadOnlySpan<char> value);
    byte[]? Get(ReadOnlySpan<char> key);
    void Delete(ReadOnlySpan<char> key);
    (long setCount, long getCount, long deleteCount) GetStatistics();
}