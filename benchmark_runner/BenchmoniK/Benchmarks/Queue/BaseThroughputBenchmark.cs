namespace BenchmoniK.Benchmarks.Queue;
using ArmoniK.Core.Base;
using ArmoniK.Core.Base.DataStructures;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmoniK.Utils;
using System.Collections.Concurrent;
using BenchmarkDotNet.Diagnostics.dotTrace;
using BenchmarkDotNet.Diagnostics.dotMemory;

// [DotMemoryDiagnoser] // Can't have these both be active/attached to the same process at the same time
[DotTraceDiagnoser]
[MemoryDiagnoser]
[StopOnFirstError]
[SimpleJob(RunStrategy.Monitoring, RuntimeMoniker.Net80, launchCount: 1, warmupCount: 1, iterationCount: 2)] // TODO: Change this back to 5 !! Less iterations for dotMemory
[Config(typeof(BenchmarkDotNetConfig))]
public abstract class BaseThroughputBenchmark
{
    // Client collections - each runner gets its own client
    protected readonly List<IPullQueueStorage> _pullQueueClients = new();
    protected readonly List<IPushQueueStorage> _pushQueueClients = new();
    
    protected readonly ConcurrentBag<IQueueMessageHandler> _pulledMessages = new();
    protected readonly string _partitionName = "benchmonik";

    // Maximum messages to pull in a single operation
    [Params(1, 10)]
    // [Params(10)]
    public int MaxMessagesPerOperation { get; set; }

    [Params(100)]
    public int NumMessages { get; set; }
    
    [Params(1, 5, 25, 50, 75, 100)] //100, 125, 150, 175, 200)] 
    // [Params(10)] 
    public int NumConcurrentRunners { get; set; }

    [IterationSetup(Target = nameof(PullMessagesNack))]
    public void SetupPullMessagesNackAsync()
    {
        // Push messages for each runner using their dedicated client
        var pushTasks = new List<Task>();
        
        for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
        {
            var messages = CreateMessages(runnerIdx, NumMessages);
            var pushQueue = _pushQueueClients[runnerIdx];
            
            pushTasks.Add(pushQueue.PushMessagesAsync(messages, _partitionName));
        }
        
        Task.WhenAll(pushTasks).GetAwaiter().GetResult();
    }
    
    [Benchmark]
    public async Task PullMessagesNack()
    {
        var pullTasks = new List<Task>();
        
        for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
        {
            var pullQueue = _pullQueueClients[runnerIdx];
            var messagesToPull = NumMessages;
            
            pullTasks.Add(Task.Run(async () =>
            {
                await PullMessagesInChunks(_partitionName,pullQueue, messagesToPull, MaxMessagesPerOperation, QueueMessageStatus.Cancelled);
            }));
        }
        
        await Task.WhenAll(pullTasks);
    }
    
    [IterationSetup(Target = nameof(PullMessagesAck))]
    public void SetupPullMessagesAckAsync()
    {
        // Push messages for each runner using their dedicated client
        var pushTasks = new List<Task>();
        
        for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
        {
            var messages = CreateMessages(runnerIdx, NumMessages);
            var pushQueue = _pushQueueClients[runnerIdx];
            
            pushTasks.Add(pushQueue.PushMessagesAsync(messages, _partitionName));
        }
        
        Task.WhenAll(pushTasks).GetAwaiter().GetResult();
    }
    
    [Benchmark]
    public async Task PullMessagesAck()
    {
        var pullTasks = new List<Task>();
        
        for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
        {
            var pullQueue = _pullQueueClients[runnerIdx];
            var messagesToPull = NumMessages;
            
            pullTasks.Add(Task.Run(async () =>
            {
                await PullMessagesInChunks(_partitionName,pullQueue, messagesToPull, MaxMessagesPerOperation, QueueMessageStatus.Processed);
            }));
        }
        
        await Task.WhenAll(pullTasks);
    }

    [IterationSetup(Target = nameof(PushMessages))]
    public void SetupPushMessages()
    {
        // No setup needed for push benchmark
    }

    [Benchmark]
    public async Task PushMessages()
    {
        var pushTasks = new List<Task>();
        
        for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
        {
            var messages = CreateMessages(runnerIdx, NumMessages);
            var pushQueue = _pushQueueClients[runnerIdx];
            
            pushTasks.Add(Task.Run(async () =>
            {
                await PushMessagesInChunks(pushQueue, messages, _partitionName, MaxMessagesPerOperation);
            }));
        }
        
        await Task.WhenAll(pushTasks);
    }

    [IterationCleanup(Target = nameof(PushMessages))]
    public void CleanupPushMessagesAsync()
    {
        // Clean up messages using the first available pull queue
        var pullQueue = _pullQueueClients[0];
        int totalMessagesToDelete = NumConcurrentRunners * NumMessages;
        
        PullMessagesInChunks(_partitionName, pullQueue, totalMessagesToDelete, MaxMessagesPerOperation, QueueMessageStatus.Cancelled).GetAwaiter().GetResult();
    }

    private static IEnumerable<MessageData> CreateMessages(int runnerIdx, int messageCount)
    {
        return Enumerable.Range(0, messageCount).Select(msgIdx => new MessageData(
            $"runner-{runnerIdx}-task-{msgIdx}",
            "benchmark-session",
            new TaskOptions()
        ));
    }

    private static async Task PushMessagesInChunks(
        IPushQueueStorage pushQueue,
        IEnumerable<MessageData> messages,
        string partitionName,
        int maxMessagesPerPush)
    {
        var messagesList = messages.ToList();
        int messagesPushed = 0;
        int totalMessages = messagesList.Count;
        
        while (messagesPushed < totalMessages)
        {
            int messagesInThisChunk = Math.Min(maxMessagesPerPush, totalMessages - messagesPushed);
            var chunk = messagesList.Skip(messagesPushed).Take(messagesInThisChunk);
            
            await pushQueue.PushMessagesAsync(chunk, partitionName);
            messagesPushed += messagesInThisChunk;
        }
    }

    private static async Task PullMessagesInChunks(
        string partitionName,
        IPullQueueStorage pullQueue, 
        int totalMessagesToPull, 
        int MaxMessagesPerOperation,
        QueueMessageStatus status)
    {
        int messagesPulled = 0;
        var allHandlers = new List<IQueueMessageHandler>();
        
        while (messagesPulled < totalMessagesToPull)
        {
            int messagesInThisChunk = Math.Min(MaxMessagesPerOperation, totalMessagesToPull - messagesPulled);
            var chunkHandlers = new List<IQueueMessageHandler>();
            
            await foreach (var qmh in pullQueue.PullMessagesAsync(partitionName,messagesInThisChunk))
            {
                chunkHandlers.Add(qmh);
                messagesPulled++;
                
                if (chunkHandlers.Count >= messagesInThisChunk)
                    break;
            }
            
            allHandlers.AddRange(chunkHandlers);
            
            // If we got fewer messages than requested, we've reached the end of the queue
            if (chunkHandlers.Count < messagesInThisChunk)
                break;
        }
        
        // Process all message handlers in parallel
        var processingTasks = allHandlers.Select(async qmh =>
        {
            qmh.Status = status;
            await qmh.DisposeAsync();
        });
        
        await Task.WhenAll(processingTasks);
    }

[IterationSetup(Target = nameof(PushThenPull))]
public void SetupPushThenPull()
{
    // No setup needed - benchmark handles its own pushing
}

[Benchmark]
public async Task PushThenPull()
{
    // First, push all messages concurrently
    var pushTasks = new List<Task>();
    
    for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
    {
        var messages = CreateMessages(runnerIdx, NumMessages);
        var pushQueue = _pushQueueClients[runnerIdx];
        
        pushTasks.Add(Task.Run(async () =>
        {
            await PushMessagesInChunks(pushQueue, messages, _partitionName, MaxMessagesPerOperation);
        }));
    }
    
    await Task.WhenAll(pushTasks);
    
    // Then, pull all messages concurrently
    var pullTasks = new List<Task>();
    
    for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
    {
        var pullQueue = _pullQueueClients[runnerIdx];
        var messagesToPull = NumMessages;
        
        pullTasks.Add(Task.Run(async () =>
        {
            await PullMessagesInChunks(_partitionName, pullQueue, messagesToPull, MaxMessagesPerOperation, QueueMessageStatus.Processed);
        }));
    }
    
    await Task.WhenAll(pullTasks);
}

[IterationSetup(Target = nameof(PushThenPullPerRunner))]
public void SetupPushThenPullPerRunner()
{
    // No setup needed - benchmark handles its own pushing
}

[Benchmark]
public async Task PushThenPullPerRunner()
{
    var runnerTasks = new List<Task>();
    
    for (var runnerIdx = 0; runnerIdx < NumConcurrentRunners; runnerIdx++)
    {
        var messages = CreateMessages(runnerIdx, NumMessages);
        var pushQueue = _pushQueueClients[runnerIdx];
        var pullQueue = _pullQueueClients[runnerIdx];
        var messagesToPull = NumMessages;
        
        runnerTasks.Add(Task.Run(async () =>
        {
            // Push messages first
            await PushMessagesInChunks(pushQueue, messages, _partitionName, MaxMessagesPerOperation);
            
            // Then pull the same number of messages
            await PullMessagesInChunks(_partitionName, pullQueue, messagesToPull, MaxMessagesPerOperation, QueueMessageStatus.Processed);
        }));
    }
    
    await Task.WhenAll(runnerTasks);
}
}
