using HighLoadedCache.Services.Store;

namespace HighLoadedCache.App.Tests;

public class SimpleStoreConcurrencyTests
{
    [Fact]
    public async Task Parallel_Set_and_Get_are_thread_safe()
    {
        const int keyCount = 50;
        const int writers = 8;
        const int writesPerWriter = 500;
        const int readers = 16;
        const int readsPerReader = 500;

        using var store = new SimpleStore();

        // Инициализация ключей
        var keys = Enumerable.Range(0, keyCount).Select(i => $"key-{i}").ToArray();
        foreach (var k in keys)
            store.Set(k, 0.ToString().AsSpan());

        var rnd = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        // Писатели
        var writerTasks = Enumerable.Range(0, writers).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < writesPerWriter; i++)
            {
                var k = keys[rnd.Value!.Next(keys.Length)];
                store.Set(k, i.ToString().AsSpan());
            }
        })).ToArray();

        // Читатели
        var readerTasks = Enumerable.Range(0, readers).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < readsPerReader; i++)
            {
                var k = keys[rnd.Value!.Next(keys.Length)];
                var val = store.Get(k.AsSpan());
                Assert.NotNull(val);
            }
        })).ToArray();

        await Task.WhenAll(writerTasks.Concat(readerTasks));

        // Проверка счетчиков
        var (setCount, getCount, deleteCount) = store.GetStatistics();
        Assert.Equal(writers * writesPerWriter + keyCount, setCount);
        Assert.Equal(readers * readsPerReader, getCount);
        Assert.Equal(0, deleteCount);
    }
}