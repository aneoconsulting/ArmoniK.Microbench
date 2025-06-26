using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace BenchmoniK.Utils;

public static class ConfigUtils
{
    
    /// <summary>
    /// Gets the path to an adapter DLL based on the ARMONIK_CORE_PATH environment variable
    /// </summary>
    /// <param name="adapterName">Name of the adapter (e.g., "S3", "Redis", "LocalStorage")</param>
    /// <param name="configuration">Release or Debug</param>
    /// <returns>Full path to the adapter DLL</returns>
    public static string GetAdapterDllPath(string adapterName, string configuration = "Release")
    {
        var armonikCorePath = Environment.GetEnvironmentVariable("ARMONIK_CORE_PATH");
        if (string.IsNullOrEmpty(armonikCorePath))
        {
            throw new InvalidOperationException("ARMONIK_CORE_PATH environment variable is not set. " +
                                                "Please set it to the path of your ArmoniK.Core repository root directory.");
        }

        return Path.Combine(armonikCorePath, "Adaptors", adapterName, "src", "bin", configuration, "net8.0", $"ArmoniK.Core.Adapters.{adapterName}.dll");
    }

    
    public static ConfigurationManager LoadConfig(string configPath)
    {
        var configuration = new ConfigurationManager();
        if (File.Exists(configPath))
            configuration.AddJsonFile(configPath);
        return configuration;
    }

    public static IServiceProvider BuildServiceProvider(ConfigurationManager configuration, String adapterPath)
    {
        var services = new ServiceCollection();
        
        // Create logger:
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Config");

        var adapterAssembly = Assembly.LoadFrom(adapterPath);
        
        // Get the ObjectBuilder : 
        var builderType = adapterAssembly.GetTypes().First(t => typeof(ArmoniK.Core.Base.IDependencyInjectionBuildable).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (builderType == null)
        {
            throw new InvalidOperationException($"No ObjectBuilder implementation found in {adapterPath}");
        }
        
        // If this fails then it just fails ¯\_(ツ)_/¯ too bad
        var objectBuilder = (ArmoniK.Core.Base.IDependencyInjectionBuildable)Activator.CreateInstance(builderType);
        objectBuilder.Build(services, configuration, logger);

        return services.BuildServiceProvider();
    }
}