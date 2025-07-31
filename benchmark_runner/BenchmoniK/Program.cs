using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmoniK.Benchmarks;
using BenchmoniK.Benchmarks.Queue;

namespace BenchmoniK;

class Program
{
    static void Main(string[] args)
    {
        
        var armonikCorePath = Environment.GetEnvironmentVariable("ARMONIK_CORE_PATH");
        if (string.IsNullOrEmpty(armonikCorePath))
        {
            Console.WriteLine("Warning: ARMONIK_CORE_PATH environment variable is not set.");
            Console.WriteLine("Please set it to the path of your ArmoniK.Core repository root directory.");
            Console.WriteLine("Example: export ARMONIK_CORE_PATH=/path/to/ArmoniK.Core");
            Console.WriteLine("Continuing with the assumption that DLLs are passed in through --armonik-core...");
        }
        else
        {
            Console.WriteLine($"Using ArmoniK.Core from: {armonikCorePath}");
            
            // Verify that key directories exist
            var adaptorsDir = Path.Combine(armonikCorePath, "Adaptors");
            if (!Directory.Exists(adaptorsDir))
            {
                Console.WriteLine($"Warning: Adaptors directory not found at {adaptorsDir}");
            }
        }
        
        try
        {
            // Parse command line arguments
            var configFiles = new List<string>();
            string? configDirectory = null;
            string? awsProfile = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--config":
                    case "-c":
                        // Ensure there's a value after the -c flag
                        if (i + 1 < args.Length)
                        {
                            string configPath = args[++i];
                            if (!configPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new ArgumentException($"Config file must have .json extension: {configPath}");
                            }
                            configFiles.Add(configPath);
                        }
                        else
                        {
                            throw new ArgumentException("Missing file path after -c option");
                        }
                        break;
                    
                    case "--directory":
                    case "-d":
                        // Ensure there's a value after the -d flag
                        if (i + 1 < args.Length)
                        {
                            // If -d is specified multiple times, use the last one
                            configDirectory = args[++i];
                            if (!Directory.Exists(configDirectory))
                            {
                                throw new DirectoryNotFoundException($"Directory not found: {configDirectory}");
                            }
                        }
                        else
                        {
                            throw new ArgumentException("Missing directory path after -d option");
                        }
                        break;
                    
                    case "--armonik-core":
                    case "-a":
                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ARMONIK_CORE_PATH")))
                        {
                            // Variable doesn't exist or is empty, so set it
                            Environment.SetEnvironmentVariable("ARMONIK_CORE_PATH", args[++i]);
                        }

                        break;
                    case "--attach-dottrace":
                        Environment.SetEnvironmentVariable("ENABLE_PERFORMANCE_PROFILING", "1");
                        break;
                    case "--attach-dotmemory":
                        Environment.SetEnvironmentVariable("ENABLE_MEMORY_PROFILING", "1");
                        break;
                    case "--profile":
                        awsProfile = args[++i];
                        break;
                        
                    default:
                        throw new ArgumentException($"Unknown option: {args[i]}");
                }
            }
            HandleAWSCredentials(awsProfile);
            // Process config files specified directly with -c
            foreach (var configFile in configFiles)
            {
                ExecuteBenchmark(configFile);
            }

            // Process config files from directory if specified
            if (configDirectory != null)
            {
                string[] jsonFiles = Directory.GetFiles(configDirectory, "*.json");
                foreach (var jsonFile in jsonFiles)
                {
                    ExecuteBenchmark(jsonFile);
                }
            }

            // Validate that at least one option was provided
            if (configFiles.Count == 0 && configDirectory == null)
            {
                ShowHelp();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            ShowHelp();
            Environment.Exit(1);
        }
        
    }

    static void HandleAWSCredentials(string? profileName)
    {
        try
        {

            // Try to get credentials from instance metadata service
            var credentials = FallbackCredentialsFactory.GetCredentials();
            var credentialsAcquired = false;
            
            if (credentials != null)
            {
                ImmutableCredentials? credentialValues = null;
                try
                {
                    credentialValues = credentials.GetCredentials();
                } 
                catch
                {
                    Console.WriteLine("Failed to acquire credentials from IMDS, falling back to profile");
                }
                if (credentialValues != null)
                {
                    credentialsAcquired = true;
                    // I set these and then the different builds use them
                    Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", credentialValues.AccessKey);
                    Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", credentialValues.SecretKey);
                    
                    if (!string.IsNullOrEmpty(credentialValues.Token))
                    {
                        Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", credentialValues.Token);
                    }
                    
                    Console.WriteLine("Successfully obtained AWS credentials from instance profile");
                }
            }
            if (!credentialsAcquired)
            {
                // Fall back to profile-based credentials if not running on EC2
                string? awsProfile;
                if (string.IsNullOrEmpty(profileName))
                {
                    awsProfile = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default";
                }
                else
                {
                    awsProfile = profileName;
                }
                var sharedFile = new SharedCredentialsFile();
                if (sharedFile.TryGetProfile(awsProfile, out var credentialProfile)
                    && AWSCredentialsFactory.TryGetAWSCredentials(credentialProfile, sharedFile, out var awsCredentials))
                {
                    var credentialValues = awsCredentials.GetCredentials();
                    if (credentialValues != null)
                    {
                        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", credentialValues.AccessKey);
                        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", credentialValues.SecretKey);
                        Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", credentialValues.Token);
                        Console.WriteLine($"Successfully obtained AWS credentials from profile {awsProfile}");
                    }
                    else
                    {
                        throw new Exception($"Could not retrieve AWS credentials from profile {awsProfile}");
                    }
                }
                else
                {
                    throw new Exception($"AWS Profile {awsProfile} not found");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error obtaining AWS credentials: {ex.Message}");
            throw;
        }
    }

static void ExecuteBenchmark(string filePath)
{
    try
    {
        Console.WriteLine($"Reading benchmark configuration from: {filePath}");
        
        string jsonContent = File.ReadAllText(filePath);
        
        using var document = System.Text.Json.JsonDocument.Parse(jsonContent);
        var root = document.RootElement;
        
        if (!root.TryGetProperty("Component", out var componentElement))
        {
            throw new Exception($"Required 'Component' property not found in {filePath}");
        }
        
        // Get the component value
        string component = componentElement.GetString() ?? throw new Exception("Component value cannot be null");
        Console.WriteLine($"Running benchmark for component: {component}");
        
        // Set environment variable for config
        Environment.SetEnvironmentVariable("BENCHMARK_CONFIG", filePath);
        
        // Run the appropriate benchmark based on the component value
        switch (component.ToLowerInvariant())
        {
            case "redis":
                Console.WriteLine("Starting Redis Object Storage benchmark...");
                var redisBenchmark = BenchmarkConverter.TypeToBenchmarks(typeof(RedisBenchmark));
                var redisThroughputConfig =
                    BenchmarkConverter.TypeToBenchmarks(typeof(RedisThroughputBenchmark));
                // BenchmarkRunner.Run(redisBenchmark);
                BenchmarkRunner.Run(redisThroughputConfig);
                break;
                
            case "s3":
                Console.WriteLine("Starting S3 Object Storage benchmark...");
                var s3Benchmark = BenchmarkConverter.TypeToBenchmarks(typeof(S3Benchmark));
                var s3ThroughputBenchmark = BenchmarkConverter.TypeToBenchmarks(typeof(S3ThroughputBenchmark));
                // BenchmarkRunner.Run(s3Benchmark);
                BenchmarkRunner.Run(s3ThroughputBenchmark);
                break;
            
            case "localstorage":
                Console.WriteLine("Starting Local Storage Object Storage benchmark...");
                var localStorageBenchmark = BenchmarkConverter.TypeToBenchmarks(typeof(LocalStorageBenchmark));
                var localStorageThroughputBenchmark = BenchmarkConverter.TypeToBenchmarks(typeof(LocalStorageThroughputBenchmark));
                // BenchmarkRunner.Run(localStorageBenchmark);
                BenchmarkRunner.Run(localStorageThroughputBenchmark);
                break;
            
            case "sqs":
                Console.WriteLine("Starting SQS Queue benchmark...");
                var sqsThroughputBenchmark = BenchmarkConverter.TypeToBenchmarks(typeof(SqsThroughputBenchmark));
                BenchmarkRunner.Run(sqsThroughputBenchmark);
                break;
            
            case "activemq":
                Console.WriteLine("Starting ActiveMQ Queue benchmark");
                var activemqThroughputBenchmark =
                    BenchmarkConverter.TypeToBenchmarks(typeof(ActivemqThroughputBenchmark));
                BenchmarkRunner.Run(activemqThroughputBenchmark);
                break;
            case "rabbitmq":
                Console.WriteLine("Starting RabbitMQ Queue benchmark");
                var rabbitmqThroughputBenchmark =
                    BenchmarkConverter.TypeToBenchmarks(typeof(RabbitmqThroughputBenchmark));
                BenchmarkRunner.Run(rabbitmqThroughputBenchmark);
                break;
            default:
                throw new Exception($"Unknown component type: {component}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error executing benchmark for {filePath}: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}
    
    static void ShowHelp()
    {
        Console.WriteLine("BenchmoniK - ArmoniK Benchmarking Tool");
        Console.WriteLine("Usage:");
        Console.WriteLine("  --benchmarks, -b : Run all benchmarks");
        Console.WriteLine("  --s3       : Run S3 object storage benchmarks");
        Console.WriteLine("  --help, -h      : Show this help");
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  ARMONIK_CORE_PATH: Path to the ArmoniK.Core repository root");
        Console.WriteLine("  BENCHMARK_CONFIG_DIR: Path to the benchmark configuration files");
    }
    
}
