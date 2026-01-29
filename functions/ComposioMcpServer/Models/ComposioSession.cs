using System.Text.Json.Serialization;

namespace ComposioMcpServer.Models;

/// <summary>
/// Response from Composio session creation API
/// </summary>
public class ComposioSessionResponse
{
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("mcp")]
    public required McpEndpoint Mcp { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// MCP endpoint details from Composio
/// </summary>
public class McpEndpoint
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("headers")]
    public required Dictionary<string, string> Headers { get; init; }

    [JsonPropertyName("transport")]
    public string Transport { get; init; } = "sse";
}

/// <summary>
/// Request to create a Composio session
/// </summary>
public class CreateSessionRequest
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("allowed_toolkits")]
    public List<string>? AllowedToolkits { get; init; }
}

/// <summary>
/// Composio API error response
/// </summary>
public class ComposioErrorResponse
{
    [JsonPropertyName("error")]
    public required ComposioError Error { get; init; }
}

public class ComposioError
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("oauth_url")]
    public string? OAuthUrl { get; init; }

    [JsonPropertyName("provider")]
    public string? Provider { get; init; }
}
