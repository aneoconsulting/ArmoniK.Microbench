using ArmoniK.Core.Base;
using BenchmarkDotNet.Attributes;
using BenchmoniK.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace BenchmoniK.Benchmarks;

public class LocalStorageBenchmark: BaseBenchmark
{

    // Object sizes to test
    [Params(1024, 1048576, 2 * 10485760, 104857600)] // 1KB, 1MB, 20MB, 100MB
    public int ObjectSizeBytes { get; set; }
    
    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var configPath = Environment.GetEnvironmentVariable("BENCHMARK_CONFIG");
        var configuration = ConfigUtils.LoadConfig(configPath);
        
        string adapterPath = ConfigUtils.GetAdapterDllPath("LocalStorage");
        var serviceProvider = ConfigUtils.BuildServiceProvider(configuration,adapterPath);

        _objectStorage = serviceProvider.GetRequiredService<IObjectStorage>();
        await _objectStorage.Init(CancellationToken.None);
       
        _testData = new byte[ObjectSizeBytes];
        new Random(42).NextBytes(_testData);
        
    }
    
}