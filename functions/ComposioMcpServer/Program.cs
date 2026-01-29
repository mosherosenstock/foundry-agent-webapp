using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ComposioMcpServer.Services;
using ComposioMcpServer.Middleware;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        // Add correlation ID middleware for distributed tracing
        builder.UseMiddleware<CorrelationIdMiddleware>();
    })
    .ConfigureServices(services =>
    {
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Composio services
        services.AddSingleton<IComposioClient, ComposioHttpClient>();
        services.AddSingleton<ComposioSessionManager>();
        
        // HTTP client for Composio API
        services.AddHttpClient("Composio", client =>
        {
            client.BaseAddress = new Uri("https://backend.composio.dev/api/v3/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Memory cache for session management
        services.AddMemoryCache();

        // Logging configuration
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddApplicationInsights();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    })
    .Build();

host.Run();
