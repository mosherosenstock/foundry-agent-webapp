using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Mcp;
using Microsoft.Extensions.Logging;
using ComposioMcpServer.Services;

namespace ComposioMcpServer.Functions;

/// <summary>
/// MCP tool triggers for Composio integration
/// Implements 5 whitelisted tools with approval policies
/// </summary>
public class McpToolTriggers
{
    private readonly ComposioSessionManager _sessionManager;
    private readonly ILogger<McpToolTriggers> _logger;

    public McpToolTriggers(
        ComposioSessionManager sessionManager,
        ILogger<McpToolTriggers> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Helper to extract user ID from context
    /// </summary>
    private string GetUserIdFromContext(FunctionContext context)
    {
        // Try to get from X-User-Id header
        if (context.Items.TryGetValue("HttpRequestData", out var requestData))
        {
            var httpRequest = requestData as HttpRequestData;
            if (httpRequest != null && httpRequest.Headers.TryGetValues("X-User-Id", out var values))
            {
                var userId = values.FirstOrDefault();
                if (!string.IsNullOrEmpty(userId))
                {
                    return userId;
                }
            }
        }

        // Fallback: extract from Entra ID claims (if available)
        // TODO: Implement claim extraction from token
        
        throw new InvalidOperationException("X-User-Id header is required");
    }

    /// <summary>
    /// Helper to get correlation ID from context
    /// </summary>
    private string GetCorrelationId(FunctionContext context)
    {
        if (context.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            return correlationId?.ToString() ?? Guid.NewGuid().ToString();
        }
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Send SMS via Twilio
    /// Approval Policy: ALWAYS (write operation, triggers external charge)
    /// </summary>
    [Function("twilio_send_sms")]
    [McpToolTrigger("twilio_send_sms", 
        Description = "Send SMS message via Twilio. Requires approval.",
        RequiresApproval = true)]
    public async Task<HttpResponseData> SendSmsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var correlationId = GetCorrelationId(context);
        var userId = GetUserIdFromContext(context);

        _logger.LogInformation("Tool invoked: twilio_send_sms. UserId: {UserId}, CorrelationId: {CorrelationId}", 
            userId, correlationId);

        try
        {
            // Parse request body
            var requestBody = await req.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody ?? "{}")
                ?? throw new ArgumentException("Invalid request body");

            // Validate required parameters
            if (!parameters.ContainsKey("to") || !parameters.ContainsKey("message"))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "invalid_parameters",
                        message = "Missing required parameters: 'to' and 'message'",
                        correlation_id = correlationId
                    }
                });
                return errorResponse;
            }

            // Execute tool via Composio
            var result = await _sessionManager.ExecuteToolAsync(
                userId,
                "TWILIO_SEND_SMS",
                parameters,
                context.CancellationToken);

            _logger.LogInformation("Tool executed successfully: twilio_send_sms. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            // Return success response
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                result,
                correlation_id = correlationId
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: twilio_send_sms. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "tool_execution_failed",
                    message = ex.Message,
                    correlation_id = correlationId
                }
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Send WhatsApp message via Twilio
    /// Approval Policy: ALWAYS (write operation, triggers external charge)
    /// </summary>
    [Function("twilio_send_whatsapp")]
    [McpToolTrigger("twilio_send_whatsapp", 
        Description = "Send WhatsApp message via Twilio. Requires approval.",
        RequiresApproval = true)]
    public async Task<HttpResponseData> SendWhatsAppAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var correlationId = GetCorrelationId(context);
        var userId = GetUserIdFromContext(context);

        _logger.LogInformation("Tool invoked: twilio_send_whatsapp. UserId: {UserId}, CorrelationId: {CorrelationId}", 
            userId, correlationId);

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody ?? "{}")
                ?? throw new ArgumentException("Invalid request body");

            if (!parameters.ContainsKey("to") || !parameters.ContainsKey("message"))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "invalid_parameters",
                        message = "Missing required parameters: 'to' and 'message'",
                        correlation_id = correlationId
                    }
                });
                return errorResponse;
            }

            var result = await _sessionManager.ExecuteToolAsync(
                userId,
                "TWILIO_SEND_WHATSAPP_MESSAGE",
                parameters,
                context.CancellationToken);

            _logger.LogInformation("Tool executed successfully: twilio_send_whatsapp. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                result,
                correlation_id = correlationId
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: twilio_send_whatsapp. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "tool_execution_failed",
                    message = ex.Message,
                    correlation_id = correlationId
                }
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Search the web via Tavily
    /// Approval Policy: NEVER (read-only operation, no side effects)
    /// </summary>
    [Function("tavily_search")]
    [McpToolTrigger("tavily_search", 
        Description = "Search the web using Tavily API. No approval required.",
        RequiresApproval = false)]
    public async Task<HttpResponseData> TavilySearchAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var correlationId = GetCorrelationId(context);
        var userId = GetUserIdFromContext(context);

        _logger.LogInformation("Tool invoked: tavily_search. UserId: {UserId}, CorrelationId: {CorrelationId}", 
            userId, correlationId);

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody ?? "{}")
                ?? throw new ArgumentException("Invalid request body");

            if (!parameters.ContainsKey("query"))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "invalid_parameters",
                        message = "Missing required parameter: 'query'",
                        correlation_id = correlationId
                    }
                });
                return errorResponse;
            }

            var result = await _sessionManager.ExecuteToolAsync(
                userId,
                "TAVILY_SEARCH",
                parameters,
                context.CancellationToken);

            _logger.LogInformation("Tool executed successfully: tavily_search. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                result,
                correlation_id = correlationId
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: tavily_search. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "tool_execution_failed",
                    message = ex.Message,
                    correlation_id = correlationId
                }
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Send email via Gmail
    /// Approval Policy: ALWAYS (write operation, PII involved)
    /// </summary>
    [Function("gmail_send_email")]
    [McpToolTrigger("gmail_send_email", 
        Description = "Send email via Gmail. Requires approval.",
        RequiresApproval = true)]
    public async Task<HttpResponseData> SendEmailAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var correlationId = GetCorrelationId(context);
        var userId = GetUserIdFromContext(context);

        _logger.LogInformation("Tool invoked: gmail_send_email. UserId: {UserId}, CorrelationId: {CorrelationId}", 
            userId, correlationId);

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody ?? "{}")
                ?? throw new ArgumentException("Invalid request body");

            if (!parameters.ContainsKey("to") || !parameters.ContainsKey("subject") || !parameters.ContainsKey("body"))
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "invalid_parameters",
                        message = "Missing required parameters: 'to', 'subject', and 'body'",
                        correlation_id = correlationId
                    }
                });
                return errorResponse;
            }

            var result = await _sessionManager.ExecuteToolAsync(
                userId,
                "GMAIL_SEND_EMAIL",
                parameters,
                context.CancellationToken);

            _logger.LogInformation("Tool executed successfully: gmail_send_email. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                result,
                correlation_id = correlationId
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: gmail_send_email. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "tool_execution_failed",
                    message = ex.Message,
                    correlation_id = correlationId
                }
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// List Gmail messages (read-only)
    /// Approval Policy: NEVER (read-only operation, PII involved but no modification)
    /// </summary>
    [Function("gmail_list_messages")]
    [McpToolTrigger("gmail_list_messages", 
        Description = "List Gmail messages (read-only). No approval required.",
        RequiresApproval = false)]
    public async Task<HttpResponseData> ListMessagesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var correlationId = GetCorrelationId(context);
        var userId = GetUserIdFromContext(context);

        _logger.LogInformation("Tool invoked: gmail_list_messages. UserId: {UserId}, CorrelationId: {CorrelationId}", 
            userId, correlationId);

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBody ?? "{}") ?? new();

            // Optional parameters: max_results, query
            var result = await _sessionManager.ExecuteToolAsync(
                userId,
                "GMAIL_LIST_MESSAGES",
                parameters,
                context.CancellationToken);

            _logger.LogInformation("Tool executed successfully: gmail_list_messages. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                result,
                correlation_id = correlationId
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: gmail_list_messages. UserId: {UserId}, CorrelationId: {CorrelationId}", 
                userId, correlationId);

            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "tool_execution_failed",
                    message = ex.Message,
                    correlation_id = correlationId
                }
            });
            return errorResponse;
        }
    }

    /// <summary>
    /// Health check endpoint for MCP server
    /// </summary>
    [Function("health")]
    public HttpResponseData HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogDebug("Health check invoked");

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.WriteString("MCP Server is healthy");
        return response;
    }
}
