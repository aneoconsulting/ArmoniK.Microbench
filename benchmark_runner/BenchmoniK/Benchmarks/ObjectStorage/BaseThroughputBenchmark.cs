using ArmoniK.Core.Base;
using ArmoniK.Core.Base.DataStructures;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmoniK.Utils;
using System.Collections.Concurrent; 

namespace BenchmoniK.Benchmarks.ObjectStorage;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, RuntimeMoniker.Net80, launchCount: 1, warmupCount: 1, iterationCount: 12)]
[Config(typeof(BenchmarkDotNetConfig))]
public class BaseThroughputBenchmark
{
    protected IObjectStorage[] _objectStorageClients = null!;
    protected byte[] _testData = null!;
    protected ConcurrentBag<byte[]> _objectIdsForDeletion = new ConcurrentBag<byte[]>();
    protected List<Task> _tasks = new List<Task>();

    protected String adapterDllPath;
    
    // ---
    public static IEnumerable<(int, int)> TransferParameterSource => new[]
    {
        // (65536, 1048576),    // 64KB download, 1MB upload
        // (1048576, 5242880)     // 1MB download, 5MB upload
        (1048576, 5242880)
    };

    [ParamsSource(nameof(TransferParameterSource))]
    public (int ChunkDownloadSize, int ChunkUploadSize) TransferParameters { get; set; }
    
    // Found out about this really late, might be useful: https://github.com/timcassell/ProtoBenchmarkHelpers  
    [Params(5, 10)] 
    public int NumConcurrentRunners { get; set; }
    
    [Params(20)]  // Each runner will work with this many objects
    public int NumObjectsPerRunner { get; set; }
    
    [IterationSetup(Target = nameof(AddObject))]
    public void SetupAddObject()
    {
        _objectIdsForDeletion.Clear();
        _tasks.Clear();
        
        for (var runnerIndex = 0; runnerIndex < NumConcurrentRunners; runnerIndex++)
        {
            var clientIndex = runnerIndex; // Capture for closure
            _tasks.Add(new Task(async () =>
            {
                var client = _objectStorageClients[clientIndex];
                
                // Each runner works with multiple objects
                for (int objectIndex = 0; objectIndex < NumObjectsPerRunner; objectIndex++)
                {
                    var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
                    var result = await client.AddOrUpdateAsync(
                        new ObjectData { 
                            ResultId = Guid.NewGuid().ToString(), 
                            SessionId = $"throughput-session-runner-{clientIndex}" 
                        }, 
                        chunks);
                    _objectIdsForDeletion.Add(result.id);
                }
            }));
        }
    }
    
    [IterationSetup(Target = nameof(GetObject))]
    public void SetupGetObject()
    {
        _tasks.Clear();
        
        // Pre-create objects for each runner
        var objectIdsByRunner = new List<byte[]>[NumConcurrentRunners];
        
        for (int runnerIndex = 0; runnerIndex < NumConcurrentRunners; runnerIndex++)
        {
            objectIdsByRunner[runnerIndex] = new List<byte[]>();
            var client = _objectStorageClients[runnerIndex];
            
            for (int objectIndex = 0; objectIndex < NumObjectsPerRunner; objectIndex++)
            {
                var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
                var result = Task.Run(async () =>
                    await client.AddOrUpdateAsync(
                        new ObjectData { 
                            ResultId = Guid.NewGuid().ToString(), 
                            SessionId = $"throughput-session-runner-{runnerIndex}" 
                        },
                        chunks
                    )
                ).GetAwaiter().GetResult();
                
                objectIdsByRunner[runnerIndex].Add(result.id);
            }
        }
    
        // Create tasks that get multiple objects per runner
        for (var runnerIndex = 0; runnerIndex < NumConcurrentRunners; runnerIndex++)
        {
            var clientIndex = runnerIndex; // Capture for closure
            var objectIds = objectIdsByRunner[runnerIndex];
            
            _tasks.Add(new Task(async () =>
            {
                var client = _objectStorageClients[clientIndex];
                
                foreach (var objectId in objectIds)
                {
                    var bytes = new List<byte>();
                    await foreach (var chunk in client.GetValuesAsync(objectId, CancellationToken.None))
                    {
                        bytes.AddRange(chunk);
                    }
        
                    // Validate to ensure we got the right data
                    if (bytes.Count != _testData.Length)
                    {
                        throw new InvalidOperationException(
                            $"Retrieved data size {bytes.Count} doesn't match expected size {_testData.Length}");
                    }
                }
            }));
        }
    }
    
    [IterationSetup(Target = nameof(DeleteObject))]
    public void SetupDeleteObject()
    {
        _tasks.Clear();
        
        // Pre-create objects for each runner
        var objectIdsByRunner = new List<byte[]>[NumConcurrentRunners];
        
        for (int runnerIndex = 0; runnerIndex < NumConcurrentRunners; runnerIndex++)
        {
            objectIdsByRunner[runnerIndex] = new List<byte[]>();
            var client = _objectStorageClients[runnerIndex];
            
            for (int objectIndex = 0; objectIndex < NumObjectsPerRunner; objectIndex++)
            {
                var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
                var result = Task.Run(async () =>
                    await client.AddOrUpdateAsync(
                        new ObjectData { 
                            ResultId = Guid.NewGuid().ToString(), 
                            SessionId = $"throughput-session-runner-{runnerIndex}" 
                        },
                        chunks
                    )
                ).GetAwaiter().GetResult();
                
                objectIdsByRunner[runnerIndex].Add(result.id);
            }
        }
    
        // Create tasks that delete multiple objects per runner
        for (var runnerIndex = 0; runnerIndex < NumConcurrentRunners; runnerIndex++)
        {
            var clientIndex = runnerIndex; // Capture for closure
            var objectIds = objectIdsByRunner[runnerIndex];
            
            _tasks.Add(new Task(async () => 
            { 
                var client = _objectStorageClients[clientIndex];
                await client.TryDeleteAsync(objectIds); 
            }));
        }
    }
    
    [IterationSetup(Target = nameof(GetObjectSize))]
    public void SetupGetObjectSize()
    {
        _tasks.Clear();
        
        // Pre-create objects for each runner
        var objectIdsByRunner = new List<byte[]>[NumConcurrentRunners];
        
        for (int runnerIndex = 0; runnerIndex < NumConcurrentRunners; runnerIndex++)
        {
            objectIdsByRunner[runnerIndex] = new List<byte[]>();
            var client = _objectStorageClients[runnerIndex];
            
            for (int objectIndex = 0; objectIndex < NumObjectsPerRunner; objectIndex++)
            {
                var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
                var result = Task.Run(async () =>
                    await client.AddOrUpdateAsync(
                        new ObjectData { 
                            ResultId = Guid.NewGuid().ToString(), 
                            SessionId = $"throughput-session-runner-{runnerIndex}" 
                        },
                        chunks
                    )
                ).GetAwaiter().GetResult();
                
                objectIdsByRunner[runnerIndex].Add(result.id);
            }
        }
    
        // Create tasks that get sizes of multiple objects per runner
        for (var runnerIndex = 0; runnerIndex < NumConcurrentRunners; runnerIndex++)
        {
            var clientIndex = runnerIndex; // Capture for closure
            var objectIds = objectIdsByRunner[runnerIndex];
            
            _tasks.Add(new Task(async () =>
            {
                var client = _objectStorageClients[clientIndex];
                var sizes = await client.GetSizesAsync(objectIds);
                
                // Validate all sizes
                foreach (var objectId in objectIds)
                {
                    if (!sizes.ContainsKey(objectId) || sizes[objectId] != _testData.Length)
                    {
                        throw new InvalidOperationException(
                            $"Retrieved size {(sizes.ContainsKey(objectId) ? sizes[objectId]?.ToString() : "null")} " +
                            $"doesn't match expected size {_testData.Length}");
                    }
                }
            }));
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        if (_objectIdsForDeletion.Any())
        {
            Task.Run(async () =>
            {
                // Use the first client for cleanup - could distribute this too if needed
                await _objectStorageClients[0]
                    .TryDeleteAsync(_objectIdsForDeletion);
            }).GetAwaiter().GetResult();
        }
    }

    [Benchmark]
    public async Task AddObject()
    {
        Parallel.ForEach(_tasks, task =>
            task.Start()
        );
        await Task.WhenAll(_tasks);
    }

    [Benchmark]
    public async Task GetObject()
    {
        Parallel.ForEach(_tasks, task => { task.Start(); });
        await Task.WhenAll(_tasks);
    }
    
    [Benchmark]
    public async Task DeleteObject()
    {
        Parallel.ForEach(_tasks,
            task => task.Start());
        await Task.WhenAll(_tasks);
    }
    
    [Benchmark]
    public async Task GetObjectSize()
    {
        Parallel.ForEach(_tasks, task => task.Start());
        await Task.WhenAll(_tasks);
    }

    // Helper method to create memory chunks from byte array
    protected static async IAsyncEnumerable<ReadOnlyMemory<byte>> CreateChunks(
        byte[] data,
        int chunkSize)
    {
        for (int i = 0; i < data.Length; i += chunkSize)
        {
            int remaining = Math.Min(chunkSize, data.Length - i);
            var chunk = new ReadOnlyMemory<byte>(data, i, remaining);

            yield return chunk;
        }
    }
}