using ArmoniK.Core.Base;
using ArmoniK.Core.Base.DataStructures;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmoniK.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BenchmoniK.Benchmarks;

[MemoryDiagnoser]
// [DotTraceDiagnoser]
[SimpleJob(RunStrategy.Monitoring, RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 12 )]
[Config(typeof(BenchmarkDotNetConfig))]
public class BaseBenchmark
{
    
    protected IObjectStorage _objectStorage = null!;
    protected byte[] _testData = null!;
    protected byte[] _objectId = null!;
    protected List<byte[]> _objectIdsToCleanup = new List<byte[]>();
    protected byte[] _objectIdForDeletion = null!;

    protected String adapterDllPath;
    // ---
    public static IEnumerable<(int, int)> TransferParameterSource => new[]
    {
        (65536, 1048576),    // 64KB download, 1MB upload
        (1048576, 5242880)     // 1MB download, 5MB upload
    };

    [ParamsSource(nameof(TransferParameterSource))]
    public (int ChunkDownloadSize, int ChunkUploadSize) TransferParameters { get; set; }

    
    [IterationSetup]
    public void IterationSetup()
    {
        if (_objectId == null)
        {
            var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize);
            _objectId = Task.Run(async () =>
            {
                var result = await _objectStorage.AddOrUpdateAsync(
                    new ObjectData { ResultId = Guid.NewGuid().ToString(), SessionId = "benchmark-session" },
                    chunks);
                return result.id;
            }).GetAwaiter().GetResult();
        }
        
        // Create a separate object that will be used in the deletion benchmark
        var chunksForDeletion = CreateChunks(_testData, 1024 * 1024);
        _objectIdForDeletion = Task.Run(async () =>
        {
            var result = await _objectStorage.AddOrUpdateAsync(
                new ObjectData { ResultId = Guid.NewGuid().ToString(), SessionId = "benchmark-deletion" },
                chunksForDeletion,
                CancellationToken.None);
            return result.id;
        }).GetAwaiter().GetResult();
    }


    [IterationCleanup]
    public void IterationCleanup()
    {
        if (_objectId != null)
        {
            Task.Run(async () =>
            {
                await _objectStorage.TryDeleteAsync(new[] { _objectId });
                _objectId = null;
            }).GetAwaiter().GetResult();
        }
        
        // Clean up any objects created during AddObject benchmark
        if (_objectIdsToCleanup.Count > 0)
        {
            Task.Run(async () =>
            {
                // Delete all object IDs in a single call
                await _objectStorage.TryDeleteAsync(_objectIdsToCleanup);
                _objectIdsToCleanup.Clear();
            }).GetAwaiter().GetResult();
        }
        
        if (_objectIdForDeletion != null)
        {
            Task.Run(async () =>
            {
                await _objectStorage.TryDeleteAsync(new[] { _objectIdForDeletion });
                _objectIdForDeletion = null;
            }).GetAwaiter().GetResult();
        }

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
    
    
    [Benchmark]
    public async Task AddObject()
    {
        var chunks = CreateChunks(_testData, TransferParameters.ChunkUploadSize); // 1MB chunks
        var result = await _objectStorage.AddOrUpdateAsync(
            new ObjectData { ResultId = Guid.NewGuid().ToString(), SessionId = "benchmark-session" },
            chunks,
            CancellationToken.None);

        // Clean up the created object
        _objectIdsToCleanup.Add(result.id);
        
    }

    [Benchmark]
    public async Task GetObject()
    {
        if (_objectId == null)
        {
            throw new InvalidOperationException("Object ID is null. Make sure IterationSetup has been run.");
        }

        var bytes = new List<byte>();
        await foreach (var chunk in _objectStorage.GetValuesAsync(_objectId, CancellationToken.None))
        {
            bytes.AddRange(chunk);
        }
        
        // Validate to ensure we got the right data
        if (bytes.Count != _testData.Length)
        {
            throw new InvalidOperationException($"Retrieved data size {bytes.Count} doesn't match expected size {_testData.Length}");
        }
    }

    [Benchmark]
    public async Task GetObjectSize()
    {
        if (_objectId == null)
        {
            throw new InvalidOperationException("Object ID is null. Make sure IterationSetup has been run.");
        }

        var sizes = await _objectStorage.GetSizesAsync(new[] { _objectId }, CancellationToken.None);
        
        if (!sizes.ContainsKey(_objectId) || sizes[_objectId] != _testData.Length)
        {
            throw new InvalidOperationException(
                $"Retrieved size {(sizes.ContainsKey(_objectId) ? sizes[_objectId]?.ToString() : "null")} " +
                $"doesn't match expected size {_testData.Length}");
        }
    }

    [Benchmark]
    public async Task DeleteObject()
    {
        if (_objectIdForDeletion == null)
        {
            throw new InvalidOperationException("Object ID for deletion is null. Make sure IterationSetup has been run.");
        }
    
        await _objectStorage.TryDeleteAsync(new[] { _objectIdForDeletion }, CancellationToken.None);
    }
    
}
