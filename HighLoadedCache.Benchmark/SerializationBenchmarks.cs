using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using HighLoadedCache.Domain.Dto;
using Newtonsoft.Json;

namespace HighLoadedCache.Benchmark;

[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private readonly UserProfile _userProfile = UserProfile.Create();

    private readonly JsonSerializerOptions _stjOptions = new(JsonSerializerDefaults.General);

    private readonly JsonSerializerSettings _newtonsoftSettings = new();

    [Benchmark(Baseline = true)]
    public byte[] NewtonsoftJson()
    {
        var json = JsonConvert.SerializeObject(_userProfile, _newtonsoftSettings);
        return Encoding.UTF8.GetBytes(json);
    }

    [Benchmark]
    public byte[] SystemTextJson()
    {
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(_userProfile, _stjOptions);
    }

    [Benchmark]
    public void SourceGenerator()
    {
        using var ms = new MemoryStream();
        _userProfile.SerializeToBinary(ms);
    }
}