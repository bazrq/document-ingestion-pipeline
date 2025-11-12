using Azure.AI.OpenAI;

namespace DocumentQA.Functions.Utils;

/// <summary>
/// Factory for creating AzureOpenAIClientOptions with the appropriate API version.
/// </summary>
public static class AzureOpenAIClientOptionsFactory
{
    /// <summary>
    /// Creates AzureOpenAIClientOptions with the specified API version.
    /// </summary>
    /// <param name="apiVersion">The API version string (e.g., "2024-10-21", "2024-12-01-preview")</param>
    /// <returns>Configured AzureOpenAIClientOptions</returns>
    /// <exception cref="NotSupportedException">Thrown when the API version is not supported by the current SDK version</exception>
    public static AzureOpenAIClientOptions Create(string apiVersion)
    {
        var serviceVersion = ParseServiceVersion(apiVersion);

        return new AzureOpenAIClientOptions(serviceVersion)
        {
            NetworkTimeout = TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// Parses an API version string to the corresponding ServiceVersion enum value.
    /// </summary>
    /// <param name="apiVersion">The API version string</param>
    /// <returns>The corresponding ServiceVersion enum value</returns>
    /// <exception cref="NotSupportedException">Thrown when the API version is not supported</exception>
    private static AzureOpenAIClientOptions.ServiceVersion ParseServiceVersion(string apiVersion)
    {
        // Normalize the version string (remove hyphens, convert to lowercase)
        var normalizedVersion = apiVersion.Replace("-", "").ToLowerInvariant();

        return normalizedVersion switch
        {
            "20240601" => AzureOpenAIClientOptions.ServiceVersion.V2024_06_01,
            "20241021" => AzureOpenAIClientOptions.ServiceVersion.V2024_10_21,

            // Preview versions (require beta SDK package)
            "20241201preview" => throw new NotSupportedException(
                $"API version '{apiVersion}' requires Azure.AI.OpenAI 2.2.0-beta.4 or later. " +
                $"Current stable version (2.1.0) supports up to 2024-10-21. " +
                $"Using default version 2024-10-21 instead."),

            // Default to latest stable version
            _ => throw new NotSupportedException(
                $"API version '{apiVersion}' is not recognized. " +
                $"Supported versions: 2024-06-01, 2024-10-21. " +
                $"Using default version 2024-10-21 instead.")
        };
    }

    /// <summary>
    /// Gets the default ServiceVersion for the current SDK version.
    /// </summary>
    /// <returns>The default ServiceVersion</returns>
    public static AzureOpenAIClientOptions.ServiceVersion GetDefaultServiceVersion()
    {
        // Azure.AI.OpenAI 2.1.0 defaults to 2024-10-21
        return AzureOpenAIClientOptions.ServiceVersion.V2024_10_21;
    }

    /// <summary>
    /// Creates AzureOpenAIClientOptions with a fallback to the default version if parsing fails.
    /// </summary>
    /// <param name="apiVersion">The API version string</param>
    /// <returns>Configured AzureOpenAIClientOptions</returns>
    public static AzureOpenAIClientOptions CreateWithFallback(string apiVersion)
    {
        try
        {
            return Create(apiVersion);
        }
        catch (NotSupportedException ex)
        {
            // Log the warning (you might want to inject ILogger here)
            Console.WriteLine($"Warning: {ex.Message}");

            // Return default version
            return new AzureOpenAIClientOptions(GetDefaultServiceVersion())
            {
                NetworkTimeout = TimeSpan.FromMinutes(5)
            };
        }
    }
}
