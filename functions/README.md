# Azure Functions - MCP Servers

This folder contains Azure Functions that implement MCP (Model Context Protocol) servers for integration with Azure AI Foundry Agents.

## Current Implementations

### Composio MCP Server

Location: `ComposioMcpServer/`

**Purpose**: Integrates Composio Tool Router as a single "master tool" into Azure AI Foundry Agents, providing access to 800+ external services through 5 whitelisted tools.

**Status**: ✅ MVP Implementation Complete

**Tools Available**:
1. `twilio_send_sms` - Send SMS messages (Approval: Always)
2. `twilio_send_whatsapp` - Send WhatsApp messages (Approval: Always)
3. `tavily_search` - Web search (Approval: Never)
4. `gmail_send_email` - Send emails (Approval: Always)
5. `gmail_list_messages` - List emails read-only (Approval: Never)

**Architecture**: Azure Function (Flex Consumption) → Composio API → External Providers

**Documentation**: See `ComposioMcpServer/README.md` for detailed setup and usage.

## Quick Start

### Prerequisites

1. .NET 10 SDK
2. Azure Functions Core Tools v4
3. Azure CLI
4. Composio account with API key

### Local Development

```bash
# 1. Setup Composio (follow COMPOSIO_SETUP.md)

# 2. Navigate to function project
cd ComposioMcpServer

# 3. Configure local.settings.json with your Composio API key

# 4. Run locally
func start

# 5. Test health endpoint
curl http://localhost:7071/api/health
```

### Deployment to Azure

```bash
# 1. Setup Azure infrastructure
cd deployment-scripts
./setup-keyvault.sh      # Create Key Vault and store secrets
./setup-entra-app.sh     # Create Entra ID app registration

# 2. Deploy function
cd ../ComposioMcpServer
func azure functionapp publish func-prod-composio-mcp-eastus2

# 3. Configure post-deployment
# - Enable Managed Identity
# - Grant Key Vault access
# - Configure app settings (see ComposioMcpServer/README.md)
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Azure AI Foundry Agent                    │
│  - Loads MCPToolDefinition                                  │
│  - serverUrl: https://func-*.azurewebsites.net/api/mcp    │
│  - serverLabel: "composio-tool-router"                      │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTPS + SSE Transport
                       │ Headers: Authorization, X-User-Id, X-Correlation-Id
                       ▼
┌─────────────────────────────────────────────────────────────┐
│           Azure Function App (MCP Server)                   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Entra ID Auth Middleware                              │  │
│  │  - Validates Bearer token                             │  │
│  │  - Extracts user identity                             │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Composio Session Manager                              │  │
│  │  - Per-user session cache (1 hour TTL)               │  │
│  │  - Rate limiting (50 req/min per user)               │  │
│  │  - Auto-retry on session expiration                   │  │
│  └───────────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ MCP Tool Triggers (5 whitelisted)                     │  │
│  │  - twilio_send_sms, twilio_send_whatsapp             │  │
│  │  - tavily_search                                       │  │
│  │  - gmail_send_email, gmail_list_messages              │  │
│  └───────────────────────────────────────────────────────┘  │
└──────────────────────┬──────────────────────────────────────┘
                       │ Composio SDK
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    Composio API (SaaS)                       │
│  - Tool execution engine                                    │
│  - OAuth token management                                   │
│  - Multi-provider integrations                              │
└──────────────────────┬──────────────────────────────────────┘
                       │ Provider APIs
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              External Service Providers                      │
│  - Twilio (SMS, WhatsApp)                                   │
│  - Tavily (Web Search)                                       │
│  - Gmail / Google Workspace                                  │
└─────────────────────────────────────────────────────────────┘
```

## Security Features

- **Authentication**: Entra ID OAuth 2.0 with token validation
- **Secrets Management**: Azure Key Vault for all sensitive data
- **Multi-User Isolation**: Per-user sessions, no credential sharing
- **Rate Limiting**: 50 requests per user per minute
- **Audit Logging**: All tool invocations logged with correlation IDs
- **Granular Approvals**: Write operations require explicit user consent

## Monitoring & Observability

- **Application Insights**: Structured logging with correlation IDs
- **Custom Metrics**: Tool invocation count, error rate, latency (p50, p95, p99)
- **Distributed Tracing**: End-to-end request tracking across components
- **Alerts**: Error rate, latency, availability thresholds

## Future MCP Servers

This folder is designed to support multiple MCP servers:

### Planned Additions

1. **Custom Enterprise MCP Server** (TBD)
   - Internal APIs and databases
   - Company-specific tools
   - Direct integration without Composio

2. **Azure Services MCP Server** (TBD)
   - Azure Storage operations
   - Azure Cosmos DB queries
   - Azure Cognitive Services

3. **Third-Party MCP Servers** (TBD)
   - GitHub MCP (code operations)
   - Notion MCP (knowledge base)
   - Slack MCP (team communication)

## Integration with Backend

The backend API (`../backend/WebApp.Api`) integrates with MCP servers via:

1. **AgentFrameworkService.cs**: Loads agents with MCPToolDefinition
2. **Approval Logic**: Implements granular approval policies per tool
3. **Correlation Tracking**: Propagates correlation IDs for distributed tracing

See `P0-009` and `P0-010` tasks in the implementation plan for details.

## Cost Estimates

**MVP (Composio MCP Server only)**:
- Azure Function (Flex Consumption): $5-20/month
- Application Insights: $2-10/month
- Key Vault: $0.03 per 10K operations
- **Total**: ~$7-30/month

**With APIM (Production)**:
- Add Azure API Management: $50-250/month (Developer/Standard SKU)
- **Total**: ~$65-350/month

## Support & Documentation

- **Setup Guide**: `COMPOSIO_SETUP.md`
- **Deployment Scripts**: `deployment-scripts/`
- **Enterprise Plan**: `../.cursor/plans/composio_mcp_integration_bd7e1dc9.plan.md`
- **Azure Docs**: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-mcp

## Contributing

When adding new MCP servers:

1. Create new folder: `YourMcpServer/`
2. Follow same project structure as Composio MCP Server
3. Implement required MCP interfaces
4. Add comprehensive README with:
   - Purpose and use cases
   - Tools available
   - Approval policies
   - Setup instructions
5. Add to this README's "Current Implementations" section
6. Update deployment scripts if needed

## License

Same as parent project.
