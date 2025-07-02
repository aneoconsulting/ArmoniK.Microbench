using ArmoniK.Core.Base;
using BenchmarkDotNet.Attributes;
using BenchmoniK.Benchmarks.ObjectStorage;
using BenchmoniK.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BenchmoniK.Benchmarks;

public class RedisThroughputBenchmark: BaseThroughputBenchmark
{
    // Object sizes to test
    [Params(1024, 1048576, 2 * 10485760, 104857600)] // 1KB, 1MB, 20MB, 100MB
    public int ObjectSizeBytes { get; set; }

    IServiceProvider _serviceProvider;
    
    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var configPath = Environment.GetEnvironmentVariable("BENCHMARK_CONFIG");
        var configuration = ConfigUtils.LoadConfig(configPath);
        
        string adapterPath = ConfigUtils.GetAdapterDllPath("Redis");
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