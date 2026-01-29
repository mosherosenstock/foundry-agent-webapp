using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ComposioMcpServer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ComposioMcpServer.Services;

/// <summary>
/// HTTP client for Composio API
/// </summary>
public class ComposioHttpClient : IComposioClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ComposioHttpClient> _logger;
    private readonly string _apiKey;

    public ComposioHttpClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ComposioHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        
        // Get Composio API key from configuration (Key Vault or environment)
        _apiKey = configuration["COMPOSIO_API_KEY"] 
            ?? throw new InvalidOperationException("COMPOSIO_API_KEY not configured");
    }

    public async Task<ComposioSessionResponse> CreateSessionAsync(
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("Composio");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var request = new CreateSessionRequest
        {
            UserId = userId,
            AllowedToolkits = new List<string> { "twilio", "gmail", "tavily", "google" }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Creating Composio session for user: {UserId}", userId);

        var response = await client.PostAsync("sessions", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create Composio session. Status: {Status}, Error: {Error}", 
                response.StatusCode, error);
            throw new HttpRequestException($"Composio session creation failed: {response.StatusCode}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var session = JsonSerializer.Deserialize<ComposioSessionResponse>(responseBody)
            ?? throw new InvalidOperationException("Failed to deserialize Composio session response");

        _logger.LogInformation("Successfully created Composio session: {SessionId} for user: {UserId}", 
            session.SessionId, userId);

        return session;
    }

    public async Task<object> ExecuteToolAsync(
        string sessionUrl,
        Dictionary<string, string> sessionHeaders,
        string toolName,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        // Create client for the session-specific MCP URL
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(sessionUrl);

        // Add session headers (provided by Composio)
        foreach (var header in sessionHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        var request = new
        {
            tool = toolName,
            parameters
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Executing tool: {Tool} with session: {SessionUrl}", 
            toolName, sessionUrl);

        var response = await client.PostAsync("", content, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Tool execution failed. Tool: {Tool}, Status: {Status}, Error: {Error}", 
                toolName, response.StatusCode, responseBody);

            // Try to parse error response for OAuth URL
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ComposioErrorResponse>(responseBody);
                if (errorResponse?.Error.OAuthUrl != null)
                {
                    _logger.LogInformation("OAuth authentication required. Provider: {Provider}, URL: {Url}", 
                        errorResponse.Error.Provider, errorResponse.Error.OAuthUrl);
                    
                    // Return error with OAuth URL
                    return new
                    {
                        error = true,
                        code = "oauth_required",
                        message = errorResponse.Error.Message,
                        oauth_url = errorResponse.Error.OAuthUrl,
                        provider = errorResponse.Error.Provider
                    };
                }
            }
            catch
            {
                // If not OAuth error, throw the original error
            }

            throw new HttpRequestException($"Tool execution failed: {response.StatusCode} - {responseBody}");
        }

        var result = JsonSerializer.Deserialize<object>(responseBody)
            ?? throw new InvalidOperationException("Failed to deserialize tool execution response");

        _logger.LogInformation("Successfully executed tool: {Tool}", toolName);

        return result;
    }

    public async Task<OAuthStatus> CheckOAuthStatusAsync(
        string userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("Composio");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        _logger.LogInformation("Checking OAuth status for user: {UserId}, provider: {Provider}", 
            userId, provider);

        var response = await client.GetAsync($"connections?user_id={userId}&provider={provider}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to check OAuth status. User: {UserId}, Provider: {Provider}", 
                userId, provider);
            
            return new OAuthStatus
            {
                IsAuthenticated = false,
                Provider = provider,
                LastChecked = DateTime.UtcNow
            };
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // TODO: Parse actual response format from Composio
        // For now, assume any 200 response means connected
        return new OAuthStatus
        {
            IsAuthenticated = true,
            Provider = provider,
            LastChecked = DateTime.UtcNow
        };
    }
}
