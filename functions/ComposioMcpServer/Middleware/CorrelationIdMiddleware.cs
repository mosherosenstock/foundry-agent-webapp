using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace ComposioMcpServer.Middleware;

/// <summary>
/// Middleware to handle correlation ID for distributed tracing
/// </summary>
public class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        string? correlationId = null;

        // Try to get correlation ID from request headers
        if (context.Items.TryGetValue("HttpRequestData", out var requestData))
        {
            var httpRequest = requestData as Microsoft.Azure.Functions.Worker.Http.HttpRequestData;
            if (httpRequest != null && httpRequest.Headers.TryGetValues("X-Correlation-Id", out var values))
            {
                correlationId = values.FirstOrDefault();
            }
        }

        // Generate new correlation ID if not present
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            _logger.LogDebug("Generated new correlation ID: {CorrelationId}", correlationId);
        }

        // Store correlation ID in context for access by functions
        context.Items["CorrelationId"] = correlationId;

        // Log with correlation ID
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["FunctionName"] = context.FunctionDefinition.Name
        }))
        {
            _logger.LogInformation("Function execution started. CorrelationId: {CorrelationId}", correlationId);

            try
            {
                await next(context);
                _logger.LogInformation("Function execution completed. CorrelationId: {CorrelationId}", correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Function execution failed. CorrelationId: {CorrelationId}", correlationId);
                throw;
            }
        }
    }
}
