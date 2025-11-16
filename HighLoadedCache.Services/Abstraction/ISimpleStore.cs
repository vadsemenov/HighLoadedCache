using HighLoadedCache.Domain.Dto;

namespace HighLoadedCache.Services.Abstraction;

public interface ISimpleStore
{
    void Set(string key, UserProfile userProfile);
    UserProfile? Get(string key);
    void Delete(ReadOnlySpan<char> key);
    (long setCount, long getCount, long deleteCount) GetStatistics();
}