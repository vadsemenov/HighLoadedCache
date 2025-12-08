using BenchmarkDotNet.Running;

namespace HighLoadedCache.Benchmark;

class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<SerializationBenchmarks>();
    }
}