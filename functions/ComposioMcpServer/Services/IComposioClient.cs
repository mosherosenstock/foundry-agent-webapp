using ComposioMcpServer.Models;

namespace ComposioMcpServer.Services;

/// <summary>
/// Interface for Composio API client
/// </summary>
public interface IComposioClient
{
    /// <summary>
    /// Create a new Composio session for a user
    /// </summary>
    Task<ComposioSessionResponse> CreateSessionAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a tool action via Composio
    /// </summary>
    Task<object> ExecuteToolAsync(
        string sessionUrl,
        Dictionary<string, string> sessionHeaders,
        string toolName,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check OAuth status for a tool
    /// </summary>
    Task<OAuthStatus> CheckOAuthStatusAsync(
        string userId,
        string provider,
        CancellationToken cancellationToken = default);
}
