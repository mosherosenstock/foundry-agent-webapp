namespace ComposioMcpServer.Models;

/// <summary>
/// Represents a Composio session for a specific user.
/// Sessions are cached in-memory with a 1-hour TTL.
/// </summary>
public class ComposioUserSession
{
    /// <summary>
    /// User identifier (from X-User-Id header or Entra ID claim)
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Composio MCP server URL (session.mcp.url)
    /// This URL is managed by Composio and may rotate
    /// </summary>
    public required string SessionUrl { get; init; }

    /// <summary>
    /// Headers required for Composio API calls (session.mcp.headers)
    /// </summary>
    public required Dictionary<string, string> Headers { get; init; }

    /// <summary>
    /// Session ID from Composio (for reference)
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// When this session was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When this session expires (CreatedAt + 1 hour)
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Per-tool OAuth authentication status
    /// </summary>
    public Dictionary<string, OAuthStatus> ToolAuthStatus { get; init; } = new();

    /// <summary>
    /// Check if this session has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

/// <summary>
/// OAuth authentication status for a specific tool
/// </summary>
public class OAuthStatus
{
    /// <summary>
    /// Whether the user has authenticated with this tool
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// OAuth URL for user authentication (if not authenticated)
    /// </summary>
    public string? AuthUrl { get; set; }

    /// <summary>
    /// When we last checked the auth status
    /// </summary>
    public DateTime? LastChecked { get; set; }

    /// <summary>
    /// Provider name (e.g., "gmail", "twilio")
    /// </summary>
    public string? Provider { get; set; }
}
