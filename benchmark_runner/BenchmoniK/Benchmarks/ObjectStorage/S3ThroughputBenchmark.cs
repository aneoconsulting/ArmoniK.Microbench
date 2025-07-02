using System.Runtime.CompilerServices;
using System.Text;
using ArmoniK.Core.Base;
using ArmoniK.Core.Base.DataStructures;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmoniK.Benchmarks.ObjectStorage;
using BenchmoniK.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BenchmoniK.Benchmarks;

public class S3ThroughputBenchmark : BaseThroughputBenchmark
{
    // Parameters to test
    [Params(4)] 
    public int DegreeOfParallelism { get; set; }

    // Object sizes to test
    [Params(1024, 1048576, 20971520)] // 1KB, 1MB, 20MB
    public int ObjectSizeBytes { get; set; }

    IServiceProvider _serviceProvider;
    
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

        _serviceProvider = ConfigUtils.BuildServiceProvider(configuration, adapterPath);

        _objectStorageClients = new IObjectStorage[NumConcurrentRunners];
        for (int i = 0; i < NumConcurrentRunners; i++)
        {
            var objectStorage = _serviceProvider.GetRequiredService<IObjectStorage>();
            await objectStorage.Init(CancellationToken.None);
            _objectStorageClients[i] = objectStorage;
        }
        
        _testData = new byte[ObjectSizeBytes];
        new Random(42).NextBytes(_testData);
    }
    
}