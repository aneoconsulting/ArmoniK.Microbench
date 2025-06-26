
using ArmoniK.Core.Base;
using BenchmarkDotNet.Attributes;
using BenchmoniK.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BenchmoniK.Benchmarks;

public class S3Benchmark : BaseBenchmark
{
    // Parameters to test
    [Params(1, 4, 8)] public int DegreeOfParallelism { get; set; }

    // Object sizes to test
    [Params(1024, 1048576, 2 * 10485760, 104857600)] // 1KB, 1MB, 20MB, 100MB
    public int ObjectSizeBytes { get; set; }


    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var configPath = Environment.GetEnvironmentVariable("BENCHMARK_CONFIG");
        var configuration = ConfigUtils.LoadConfig(configPath);

        var benchmarkParams = new Dictionary<string, string>
        {
            ["S3:DegreeOfParallelism"] = DegreeOfParallelism.ToString(),
            ["S3:ChunkDownloadSize"] = TransferParameters.ChunkDownloadSize.ToString(),
            ["S3:UseChecksum"] = "false",
            ["S3:UseChunkEncoding"] = "false",
        };
        configuration.AddInMemoryCollection(benchmarkParams!);

        string adapterPath = ConfigUtils.GetAdapterDllPath("S3");

        var serviceProvider = ConfigUtils.BuildServiceProvider(configuration, adapterPath);

        _objectStorage = serviceProvider.GetRequiredService<IObjectStorage>();
        await _objectStorage.Init(CancellationToken.None);

        _testData = new byte[ObjectSizeBytes];
        new Random(42).NextBytes(_testData);
    }
}