namespace BenchmoniK.Benchmarks.Queue;

using Microsoft.Extensions.Configuration;
using ArmoniK.Core.Base;
using BenchmarkDotNet.Attributes;
using BenchmoniK.Utils;
using Microsoft.Extensions.DependencyInjection;

public class ActivemqThroughputBenchmark : BaseThroughputBenchmark
{

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var configPath = Environment.GetEnvironmentVariable("BENCHMARK_CONFIG")!;
        var configuration = ConfigUtils.LoadConfig(configPath);
        
        var benchmarkParams = new Dictionary<string, string>
        {
            ["Amqp:MaxPriority"] = "1",
            ["Amqp:LinkCredit"] = "1",
            ["Amqp:PartitionId"] = "benchmonik",
            ["Amqp:AllowInsecureTls"] = "true",
            ["Amqp:Ssl"] = "false",
        };
        configuration.AddInMemoryCollection(benchmarkParams!);

        string adapterPath = ConfigUtils.GetAdapterDllPath("Amqp");

        // Create one client pair for each concurrent runner
        _pullQueueClients.Clear();
        _pushQueueClients.Clear();
        
        for (int i = 0; i < NumConcurrentRunners; i++)
        {
            var serviceProvider = ConfigUtils.BuildServiceProvider(configuration, adapterPath);
            
            var pullQueue = serviceProvider.GetRequiredService<IPullQueueStorage>();
            var pushQueue = serviceProvider.GetRequiredService<IPushQueueStorage>();
            
            await pullQueue.Init(CancellationToken.None);
            await pushQueue.Init(CancellationToken.None);
            
            _pullQueueClients.Add(pullQueue);
            _pushQueueClients.Add(pushQueue);
        }
    }
}
