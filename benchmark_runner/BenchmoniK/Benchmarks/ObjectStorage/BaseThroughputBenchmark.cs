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
[SimpleJob(RunStrategy.Monitoring, RuntimeMoniker.Net80, launchCount: 1, warmupCount: 5, iterationCount: 12)]
[Config(typeof(BenchmarkDotNetConfig))]
public class BaseThroughputBenchmark
{
    protected IObjectStorage _objectStorage = null!;
    protected byte[] _testData = null!;
    protected ConcurrentBag<byte[]> _objectIdsForDeletion = new ConcurrentBag<byte[]>();
    protected List<Task> _tasks = new List<Task>();

    protected String adapterDllPath;
    // ---
    public static IEnumerable<(int, int)> TransferParameterSource => new[]
    {
        (65536, 1048576),    // 64KB download, 1MB upload
        (1048576, 5242880)     // 1MB download, 5MB upload
    };

    [ParamsSource(nameof(TransferParameterSource))]
    public (int ChunkDownloadSize, int ChunkUploadSize) TransferParameters { get; set; }
    
    // Found out about this really late, might be useful: https://github.com/timcassell/ProtoBenchmarkHelpers  
    [Params(5, 10, 20)] 
    public int NumConcurrentRunners { get; set; }
    
    [Params(100)]
    public int NumObjects { get; set; }
    
    [IterationSetup(Target = nameof(AddObject))]
    public void SetupAddObject()
    {
        _objectIdsForDeletion.Clear();
        _tasks.Clear();
        for (var i = 0; i < NumConcurrentRunners; i++)
        {
            var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
            _tasks.Add(new Task(async () =>
            {
                var result = await _objectStorage.AddOrUpdateAsync(
                    new ObjectData { ResultId = Guid.NewGuid().ToString(), SessionId = "throughput-session" }, chunks);
                _objectIdsForDeletion.Add(result.id);
            }));
        }
    }
    
    [IterationSetup(Target = nameof(GetObject))]
    public void SetupGetObject()
    {
        _tasks.Clear();
        var objectIds = Enumerable.Range(0, NumConcurrentRunners).AsParallel().Select(_ =>
        {
            var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
            return Task.Run(async () =>
                {
                    var result = await _objectStorage.AddOrUpdateAsync(
                        new ObjectData { ResultId = Guid.NewGuid().ToString(), SessionId = "throughput-session" },
                        chunks
                    );
                    return result.id;
                }
            ).GetAwaiter().GetResult();
        }).ToList();
    
        // Add tasks to get the different objects (There's probably an optimization that kicks in with the same object)
        foreach (var objectId in objectIds)
        {
            _tasks.Add(new Task(async () =>
            {
                var bytes = new List<byte>();
                await foreach (var chunk in _objectStorage.GetValuesAsync(objectId, CancellationToken.None))
                {
                    bytes.AddRange(chunk);
                }
    
                // Validate to ensure we got the right data
                if (bytes.Count != _testData.Length)
                {
                    throw new InvalidOperationException(
                        $"Retrieved data size {bytes.Count} doesn't match expected size {_testData.Length}");
                }
            }));
        }
    }
    
    [IterationSetup(Target = nameof(DeleteObject))]
    public void SetupDeleteObject()
    {
        _tasks.Clear();
        var objectIds = Enumerable.Range(0, NumConcurrentRunners).AsParallel().Select(_ =>
        {
            var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
            return Task.Run(async () =>
                {
                    var result = await _objectStorage.AddOrUpdateAsync(
                        new ObjectData { ResultId = Guid.NewGuid().ToString(), SessionId = "throughput-session" },
                        chunks
                    );
                    return result.id;
                }
            ).GetAwaiter().GetResult();
        }).ToList();
    
        // Add tasks to get the different objects (There's probably an optimization that kicks in with the same object)
        foreach (var objectId in objectIds)
        {
            _tasks.Add(new Task(async () => { await _objectStorage.TryDeleteAsync(new[] { objectId }); }));
        }
    }
    
    [IterationSetup(Target = nameof(GetObjectSize))]
    public void SetupGetObjectSize()
    {
        _tasks.Clear();
        var objectIds = Enumerable.Range(0, NumConcurrentRunners).AsParallel().Select(_ =>
        {
            var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
            return Task.Run(async () =>
                {
                    var result = await _objectStorage.AddOrUpdateAsync(
                        new ObjectData { ResultId = Guid.NewGuid().ToString(), SessionId = "throughput-session" },
                        chunks
                    );
                    return result.id;
                }
            ).GetAwaiter().GetResult();
        }).ToList();
    
        // Add tasks to get the different objects (There's probably an optimization that kicks in with the same object)
        foreach (var objectId in objectIds)
        {
            _tasks.Add(new Task(async () =>
            {
                var sizes = await _objectStorage.GetSizesAsync(new[] { objectId });
                // TODO: remove these after the first test
                if (!sizes.ContainsKey(objectId) || sizes[objectId] != _testData.Length)
                {
                    throw new InvalidOperationException(
                        $"Retrieved size {(sizes.ContainsKey(objectId) ? sizes[objectId]?.ToString() : "null")} " +
                        $"doesn't match expected size {_testData.Length}");
                }
            }));
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        Task.Run(async () =>
        {
            await _objectStorage
                .TryDeleteAsync(
                    _objectIdsForDeletion); // If this fails then this fails honestly what's the worst that can happen.. 
        }).GetAwaiter().GetResult();
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