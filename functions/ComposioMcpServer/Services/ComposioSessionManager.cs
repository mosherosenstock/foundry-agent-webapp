using System.Collections.Concurrent;
using ComposioMcpServer.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ComposioMcpServer.Services;

/// <summary>
/// Manages Composio sessions per user with in-memory caching
/// </summary>
public class ComposioSessionManager
{
    private readonly IComposioClient _composioClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ComposioSessionManager> _logger;
    private const int SessionTtlMinutes = 60;

    // Rate limiting: max 50 tool invocations per user per minute
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _rateLimitTracker = new();
    private const int MaxRequestsPerMinute = 50;

    public ComposioSessionManager(
        IComposioClient composioClient,
        IMemoryCache cache,
        ILogger<ComposioSessionManager> logger)
    {
        _composioClient = composioClient;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get existing session or create new one for user
    /// </summary>
    public async Task<ComposioUserSession> GetOrCreateSessionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"composio-session:{userId}";

        // Try to get from cache
        if (_cache.TryGetValue<ComposioUserSession>(cacheKey, out var cachedSession))
        {
            // Check if expired
            if (cachedSession!.IsExpired)
            {
                _logger.LogInformation("Session expired for user: {UserId}. Creating new session.", userId);
                _cache.Remove(cacheKey);
            }
            else
            {
                _logger.LogDebug("Using cached session for user: {UserId}", userId);
                return cachedSession;
            }
        }

        // Create new session
        _logger.LogInformation("Creating new Composio session for user: {UserId}", userId);
        
        var sessionResponse = await _composioClient.CreateSessionAsync(userId, cancellationToken);

        var userSession = new ComposioUserSession
        {
            UserId = userId,
            SessionUrl = sessionResponse.Mcp.Url,
            Headers = sessionResponse.Mcp.Headers,
            SessionId = sessionResponse.SessionId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(SessionTtlMinutes)
        };

        // Cache with sliding expiration
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(SessionTtlMinutes),
            Priority = CacheItemPriority.High
        };

        _cache.Set(cacheKey, userSession, cacheOptions);

        _logger.LogInformation("Successfully created and cached session for user: {UserId}, SessionId: {SessionId}", 
            userId, userSession.SessionId);

        return userSession;
    }

    /// <summary>
    /// Invalidate session for user (e.g., on logout)
    /// </summary>
    public void InvalidateSession(string userId)
    {
        var cacheKey = $"composio-session:{userId}";
        _cache.Remove(cacheKey);
        _logger.LogInformation("Invalidated session for user: {UserId}", userId);
    }

    /// <summary>
    /// Execute tool with automatic session handling and retry logic
    /// </summary>
    public async Task<object> ExecuteToolAsync(
        string userId,
        string toolName,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        // Check rate limit
        if (!CheckRateLimit(userId))
        {
            _logger.LogWarning("Rate limit exceeded for user: {UserId}", userId);
            throw new InvalidOperationException("Rate limit exceeded. Maximum 50 requests per minute.");
        }

        // Get or create session
        var session = await GetOrCreateSessionAsync(userId, cancellationToken);

        try
        {
            // Execute tool via Composio
            var result = await _composioClient.ExecuteToolAsync(
                session.SessionUrl,
                session.Headers,
                toolName,
                parameters,
                cancellationToken);

            return result;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("403"))
        {
            // Session expired, retry once with new session
            _logger.LogWarning("Session expired during tool execution. Retrying with new session. User: {UserId}, Tool: {Tool}", 
                userId, toolName);

            InvalidateSession(userId);
            var newSession = await GetOrCreateSessionAsync(userId, cancellationToken);

            // Retry with new session
            return await _composioClient.ExecuteToolAsync(
                newSession.SessionUrl,
                newSession.Headers,
                toolName,
                parameters,
                cancellationToken);
        }
    }

    /// <summary>
    /// Check if user has exceeded rate limit
    /// </summary>
    private bool CheckRateLimit(string userId)
    {
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        var requestQueue = _rateLimitTracker.GetOrAdd(userId, _ => new Queue<DateTime>());

        lock (requestQueue)
        {
            // Remove requests older than 1 minute
            while (requestQueue.Count > 0 && requestQueue.Peek() < oneMinuteAgo)
            {
                requestQueue.Dequeue();
            }

            // Check if limit exceeded
            if (requestQueue.Count >= MaxRequestsPerMinute)
            {
                return false;
            }

            // Add current request
            requestQueue.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Get OAuth status for a tool
    /// </summary>
    public async Task<OAuthStatus> GetOAuthStatusAsync(
        string userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        return await _composioClient.CheckOAuthStatusAsync(userId, provider, cancellationToken);
    }
}
