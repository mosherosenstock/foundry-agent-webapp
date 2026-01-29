# Composio MCP Server - Azure Function

## Overview

This Azure Function implements a Model Context Protocol (MCP) server that integrates Composio Tool Router with Azure AI Foundry Agents. It provides 5 whitelisted tools for SMS, WhatsApp, web search, and Gmail operations.

## Architecture

```
Azure AI Foundry Agent → Azure Function (MCP Server) → Composio API → External APIs
```

## Features

- **Multi-User Session Management**: Per-user Composio sessions with 1-hour TTL
- **Distributed Tracing**: Correlation ID tracking across all requests
- **Granular Approval Policies**: Always for write operations, never for read-only
- **Rate Limiting**: 50 requests per user per minute
- **Error Handling**: Automatic retry on session expiration
- **Security**: Entra ID authentication, Azure Key Vault for secrets

## Prerequisites

- .NET 10 SDK or higher
- Azure Functions Core Tools v4 (4.0.7030+)
- Azure CLI
- Composio account with API key

## Tools Implemented

| Tool Name | Composio Action | Approval Required | Use Case |
|-----------|----------------|-------------------|----------|
| `twilio_send_sms` | TWILIO_SEND_SMS | ✅ Always | Send SMS messages |
| `twilio_send_whatsapp` | TWILIO_SEND_WHATSAPP_MESSAGE | ✅ Always | Send WhatsApp messages |
| `tavily_search` | TAVILY_SEARCH | ❌ Never | Web search |
| `gmail_send_email` | GMAIL_SEND_EMAIL | ✅ Always | Send emails |
| `gmail_list_messages` | GMAIL_LIST_MESSAGES | ❌ Never | List emails (read-only) |

## Local Development

### 1. Setup Composio

Follow instructions in `../COMPOSIO_SETUP.md` to configure Composio account and auth configs.

### 2. Configure Local Settings

Edit `local.settings.json`:
```json
{
  "Values": {
    "COMPOSIO_API_KEY": "composio_your_api_key_here",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "",
    "AZURE_TENANT_ID": "your-tenant-id",
    "AZURE_CLIENT_ID": "your-client-id",
    "ENTRA_AUDIENCE": "api://composio-mcp-function"
  }
}
```

### 3. Run Locally

```bash
cd /Users/mosherosenstock/Desktop/foundry-agent-webapp/functions/ComposioMcpServer
func start
```

Expected output:
```
Functions:
  health: [GET] http://localhost:7071/api/health
  twilio_send_sms: [POST] http://localhost:7071/api/mcp/twilio_send_sms
  twilio_send_whatsapp: [POST] http://localhost:7071/api/mcp/twilio_send_whatsapp
  tavily_search: [POST] http://localhost:7071/api/mcp/tavily_search
  gmail_send_email: [POST] http://localhost:7071/api/mcp/gmail_send_email
  gmail_list_messages: [POST] http://localhost:7071/api/mcp/gmail_list_messages
```

### 4. Test Locally

**Health check:**
```bash
curl http://localhost:7071/api/health
```

**Tavily search (no approval):**
```bash
curl -X POST http://localhost:7071/api/mcp/tavily_search \
  -H "Content-Type: application/json" \
  -H "X-User-Id: test-user@example.com" \
  -H "X-Correlation-Id: test-123" \
  -d '{"query": "Azure AI Foundry pricing"}'
```

**Gmail send (requires approval - will show OAuth URL if not authenticated):**
```bash
curl -X POST http://localhost:7071/api/mcp/gmail_send_email \
  -H "Content-Type: application/json" \
  -H "X-User-Id: test-user@example.com" \
  -H "X-Correlation-Id: test-456" \
  -d '{
    "to": "recipient@example.com",
    "subject": "Test from MCP Server",
    "body": "This is a test email."
  }'
```

## Deployment to Azure

### Prerequisites

1. **Create Azure Key Vault** (see `../deployment-scripts/setup-keyvault.sh`)
2. **Create Entra ID App Registration** (see `../deployment-scripts/setup-entra-app.sh`)
3. **Store Composio API Key in Key Vault**

### Deploy with azd

From project root:
```bash
azd deploy mcp-server
```

Or manually:
```bash
func azure functionapp publish func-prod-composio-mcp-eastus2
```

### Post-Deployment Configuration

1. **Enable Managed Identity** on Function App
2. **Grant Key Vault Access** to Function's Managed Identity
3. **Configure App Settings** to reference Key Vault:
   ```bash
   az functionapp config appsettings set \
     --name func-prod-composio-mcp-eastus2 \
     --settings "COMPOSIO_API_KEY=@Microsoft.KeyVault(SecretUri=https://kv-prod-composio.vault.azure.net/secrets/COMPOSIO-API-KEY)"
   ```

## Monitoring

### Application Insights

Logs are sent to Application Insights with structured data:
- **CorrelationId**: End-to-end tracing
- **UserId**: User making the request
- **ToolName**: Which tool was invoked
- **ExecutionTimeMs**: Performance metrics

### Dashboards

Access dashboards in Azure Portal:
- Function App → Monitor → Logs
- Application Insights → Logs → Custom queries

Example query:
```kql
traces
| where message contains "Tool invoked"
| extend CorrelationId = customDimensions.CorrelationId
| extend UserId = customDimensions.UserId
| extend ToolName = customDimensions.ToolName
| project timestamp, CorrelationId, UserId, ToolName, message
| order by timestamp desc
```

### Alerts

Configured alerts:
- Error rate > 5% (5-minute window)
- P95 latency > 10 seconds
- Function availability < 99%

## Security

### Authentication

Function uses Entra ID OAuth 2.0:
- Audience: `api://composio-mcp-function`
- Required scope: `MCP.Execute`
- Token validation via Entra ID middleware

### Secrets Management

All secrets stored in Azure Key Vault:
- `COMPOSIO-API-KEY`: Composio API key
- Function references via `@Microsoft.KeyVault(...)` syntax

### Multi-User Isolation

Each user gets their own Composio session:
- Sessions isolated by `X-User-Id` header
- No credential sharing between users
- Per-user rate limiting (50 req/min)

## Troubleshooting

### Issue: "COMPOSIO_API_KEY not configured"

**Solution**: Ensure Key Vault reference is configured correctly in Function App settings.

### Issue: "X-User-Id header is required"

**Solution**: Backend API must pass `X-User-Id` header in all MCP requests.

### Issue: "Rate limit exceeded"

**Solution**: User has exceeded 50 requests per minute. Wait 60 seconds and retry.

### Issue: OAuth authentication required

**Response includes**:
```json
{
  "error": true,
  "code": "oauth_required",
  "oauth_url": "https://composio.dev/oauth/...",
  "provider": "gmail"
}
```

**Solution**: User must complete OAuth flow at the provided URL.

## Development Roadmap

### Phase 1: MVP (Current)
- [x] 5 whitelisted tools
- [x] Entra ID authentication
- [x] Application Insights
- [x] Rate limiting
- [x] Session management

### Phase 2: Expansion
- [ ] Add 5 more tools (Gemini, Google Drive, etc.)
- [ ] Redis cache for multi-instance sessions
- [ ] Custom metrics dashboard
- [ ] Automated integration tests

### Phase 3: Production
- [ ] Azure API Management integration
- [ ] Migrate to Container Apps (optional)
- [ ] Advanced rate limiting policies
- [ ] Compliance audit logs (90-day retention)

## Contributing

When adding new tools:

1. Add function in `Functions/McpToolTriggers.cs`
2. Set `RequiresApproval` based on tool type (write = true, read = false)
3. Update whitelist in `ComposioSessionManager.cs`
4. Add tool documentation in this README
5. Test locally before deploying

## Resources

- [Azure Functions MCP Extension Docs](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-mcp)
- [Composio Documentation](https://docs.composio.dev/)
- [Azure AI Foundry MCP Integration](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/model-context-protocol)
- [Enterprise Integration Plan](/.cursor/plans/composio_mcp_integration_bd7e1dc9.plan.md)
