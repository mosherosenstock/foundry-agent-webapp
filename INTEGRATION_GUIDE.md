# Integration Guide: Composio MCP Server + Azure AI Foundry

## Overview

This guide walks through the complete integration of Composio Tool Router with your Azure AI Foundry agent, enabling access to 800+ external tools through a secure, self-hosted MCP server.

## Architecture Summary

```
Usuario → Frontend → Backend API → Azure AI Foundry Agent → Azure Function (MCP Server) → Composio API → External APIs
```

## Prerequisites Checklist

Before starting, ensure you have:

- [ ] Azure CLI installed and authenticated (`az login`)
- [ ] Azure Developer CLI installed (`azd`)
- [ ] .NET 10 SDK installed
- [ ] Azure Functions Core Tools v4 installed
- [ ] PowerShell 7 installed (`pwsh`)
- [ ] Composio account created at https://platform.composio.dev/
- [ ] Active Azure subscription with permissions to create resources

## Step-by-Step Implementation

### Phase 1: Composio Setup (P0-001)

**Estimated Time**: 30-60 minutes

1. **Create Composio Account**:
   ```bash
   # Navigate to: https://platform.composio.dev/
   # Sign up with your work email
   # Verify email
   ```

2. **Generate API Key**:
   - Go to Settings → API Keys
   - Click "Create New API Key"
   - Name: "Azure AI Foundry MCP Server"
   - Copy the key (format: `composio_***`)
   - Save it securely (you'll need it for Azure Key Vault)

3. **Configure Auth Configs**:
   
   **Twilio** (for SMS + WhatsApp):
   - Dashboard → Tools → Twilio → Configure Auth
   - Enter your Twilio Account SID and Auth Token
   - Copy auth config ID: `ac_twilio_***`

   **Tavily** (for web search):
   - Get Tavily API key from https://tavily.com/
   - Dashboard → Tools → Tavily → Configure Auth
   - Enter Tavily API key
   - Copy auth config ID: `ac_tavily_***`

   **Gmail** (uses OAuth - managed by Composio):
   - Dashboard → Tools → Gmail
   - No manual configuration needed
   - Composio handles OAuth flow automatically

4. **Test Composio API**:
   ```bash
   # Test API key
   curl -X GET https://api.composio.dev/v1/apps \
     -H "X-API-Key: composio_your_key_here"
   
   # Should return list of available apps
   ```

**Deliverable**: Composio API key ready for Azure Key Vault

---

### Phase 2: Azure Infrastructure Setup (P0-002, P0-003)

**Estimated Time**: 45 minutes

#### P0-002: Create Azure Key Vault

```bash
cd /Users/mosherosenstock/Desktop/foundry-agent-webapp/functions/deployment-scripts
./setup-keyvault.sh
```

**The script will**:
1. Prompt for environment (dev/staging/prod)
2. Create Resource Group: `rg-{env}-composio-mcp`
3. Create Key Vault: `kv-{env}-composio-mcp`
4. Prompt for Composio API key
5. Store secret as `COMPOSIO-API-KEY`

**Verify**:
```bash
az keyvault secret show \
  --vault-name kv-dev-composio-mcp \
  --name COMPOSIO-API-KEY \
  --query value -o tsv
```

#### P0-003: Create Entra ID App Registration

```bash
./setup-entra-app.sh
```

**The script will**:
1. Create app registration: "Composio MCP Function"
2. Set App ID URI: `api://composio-mcp-function`
3. Add OAuth2 scope: `MCP.Execute`
4. Create service principal

**Output to save**:
- App ID (Client ID): `********-****-****-****-************`
- Tenant ID: `********-****-****-****-************`
- Scope: `api://composio-mcp-function/MCP.Execute`

**Deliverable**: Entra ID app configured, credentials noted

---

### Phase 3: Deploy Azure Function (P0-004 - P0-008)

**Estimated Time**: 1-2 hours

#### 3.1: Configure Local Development

```bash
cd /Users/mosherosenstock/Desktop/foundry-agent-webapp/functions/ComposioMcpServer
```

Edit `local.settings.json`:
```json
{
  "Values": {
    "COMPOSIO_API_KEY": "composio_your_key_here",
    "AZURE_TENANT_ID": "your-tenant-id",
    "AZURE_CLIENT_ID": "app-client-id-from-p0-003",
    "ENTRA_AUDIENCE": "api://composio-mcp-function"
  }
}
```

#### 3.2: Test Locally

```bash
# Start Function
func start

# In another terminal, test health endpoint
curl http://localhost:7071/api/health

# Expected: "MCP Server is healthy"
```

#### 3.3: Deploy to Azure

**Option A: Using azd (Recommended)**

First, update `azure.yaml` in project root to include the MCP server:

```yaml
services:
  web:
    project: ./backend/WebApp.Api
    language: dotnet
    host: containerapp
    
  mcp-server:
    project: ./functions/ComposioMcpServer
    language: dotnet
    host: function
```

Then deploy:
```bash
cd /Users/mosherosenstock/Desktop/foundry-agent-webapp
azd deploy mcp-server
```

**Option B: Manual Deployment**

```bash
# Create Function App
az functionapp create \
  --name func-dev-composio-mcp-eastus2 \
  --resource-group rg-dev-composio-mcp \
  --consumption-plan-location eastus2 \
  --runtime dotnet-isolated \
  --runtime-version 10 \
  --functions-version 4 \
  --os-type Linux

# Enable Managed Identity
az functionapp identity assign \
  --name func-dev-composio-mcp-eastus2 \
  --resource-group rg-dev-composio-mcp

# Get Managed Identity Object ID
IDENTITY_ID=$(az functionapp identity show \
  --name func-dev-composio-mcp-eastus2 \
  --resource-group rg-dev-composio-mcp \
  --query principalId -o tsv)

# Grant Key Vault access
az keyvault set-policy \
  --name kv-dev-composio-mcp \
  --object-id $IDENTITY_ID \
  --secret-permissions get

# Configure App Settings with Key Vault reference
az functionapp config appsettings set \
  --name func-dev-composio-mcp-eastus2 \
  --resource-group rg-dev-composio-mcp \
  --settings \
    "COMPOSIO_API_KEY=@Microsoft.KeyVault(SecretUri=https://kv-dev-composio-mcp.vault.azure.net/secrets/COMPOSIO-API-KEY)" \
    "AZURE_TENANT_ID=your-tenant-id" \
    "AZURE_CLIENT_ID=app-client-id" \
    "ENTRA_AUDIENCE=api://composio-mcp-function"

# Deploy code
cd functions/ComposioMcpServer
func azure functionapp publish func-dev-composio-mcp-eastus2
```

#### 3.4: Verify Deployment

```bash
# Get Function URL
FUNCTION_URL=$(az functionapp show \
  --name func-dev-composio-mcp-eastus2 \
  --resource-group rg-dev-composio-mcp \
  --query defaultHostName -o tsv)

echo "MCP Server URL: https://${FUNCTION_URL}/api/mcp"

# Test health endpoint
curl https://${FUNCTION_URL}/api/health
```

**Deliverable**: MCP Function deployed and accessible

---

### Phase 4: Backend API Integration (P0-009, P0-010)

**Estimated Time**: 1 hour

#### 4.1: Configure MCP Settings

Update `.env` in project root:
```bash
MCP_SERVER_URL="https://func-dev-composio-mcp-eastus2.azurewebsites.net/api/mcp"
MCP_SERVER_LABEL="composio-tool-router"
MCP_SERVER_AUDIENCE="api://composio-mcp-function/.default"
DEFAULT_USER_ID="anonymous-user"
```

#### 4.2: Grant Backend API Permission to Call MCP Function

Backend API needs permission to obtain tokens for MCP server:

```bash
# Get Backend API's Managed Identity
BACKEND_IDENTITY=$(az containerapp show \
  --name ca-web-*** \
  --resource-group rg-cocinas-prod \
  --query identity.principalId -o tsv)

# Grant API permission to MCP app
# This allows Backend to request tokens with scope: api://composio-mcp-function/MCP.Execute
az ad app permission add \
  --id <MCP_APP_CLIENT_ID> \
  --api <MCP_APP_CLIENT_ID> \
  --api-permissions <SCOPE_ID>=Scope

# Grant admin consent
az ad app permission admin-consent --id <MCP_APP_CLIENT_ID>
```

#### 4.3: Test Integration

```bash
# Restart backend to pick up new .env variables
cd backend/WebApp.Api
dotnet run

# In browser or curl, test that backend can reach MCP server
# (Full test requires Azure AI Foundry agent to invoke tools)
```

**Deliverable**: Backend API configured to call MCP server

---

### Phase 5: Agent Configuration in Azure AI Foundry (Manual)

**Estimated Time**: 15-30 minutes

Currently, the agent (`sdai-quote-checklist`) is configured in Azure AI Foundry Portal. To add MCP tools:

#### Option A: Via Azure AI Foundry Portal (Recommended)

1. Open https://ai.azure.com/
2. Navigate to your project: `next-sdai`
3. Go to Agents → `sdai-quote-checklist`
4. Edit Agent:
   - Click "Tools" section
   - Click "Add Tool" → "MCP Server"
   - Configure:
     - **Server Label**: `composio-tool-router`
     - **Server URL**: `https://func-dev-composio-mcp-eastus2.azurewebsites.net/api/mcp`
     - **Allowed Tools**: Leave empty for all tools, or specify:
       - `twilio_send_sms`
       - `twilio_send_whatsapp`
       - `tavily_search`
       - `gmail_send_email`
       - `gmail_list_messages`
   - Click "Save"
5. Save agent

#### Option B: Via SDK (Advanced)

If you need to programmatically create/update agents with MCP tools, use the code pattern in `AgentFrameworkService.cs` (see comments for `CreateAgentWithMcpAsync`).

**Deliverable**: Agent configured with MCP tool

---

### Phase 6: End-to-End Testing

**Estimated Time**: 30-45 minutes

#### Test 1: Tavily Search (No Approval)

1. Open webapp: https://ca-web-6uluhllxv7asm.grayhill-769056ba.eastus2.azurecontainerapps.io/
2. Send message: "Search the web for Azure AI Foundry pricing"
3. **Expected**: Agent invokes `tavily_search` automatically (no approval prompt)
4. **Expected**: Search results displayed in chat
5. **Expected**: No errors in Application Insights

#### Test 2: Send SMS (With Approval)

1. Send message: "Send SMS to +1234567890: 'Hello from Azure AI Foundry'"
2. **Expected**: McpApprovalCard appears in chat
3. **Expected**: Card shows:
   - Tool: `twilio_send_sms`
   - Parameters: `{"to": "+1234567890", "message": "Hello..."}`
4. Click "Approve"
5. **Expected**: SMS sent successfully (check Twilio logs)
6. **Expected**: Confirmation message in chat

#### Test 3: Send Email (OAuth Required)

1. Send message: "Send email to test@example.com with subject 'Test' and body 'Hello'"
2. If Gmail not connected:
   - **Expected**: McpApprovalCard with "Connect Gmail" button
   - Click button → OAuth flow → Redirect back
3. **Expected**: McpApprovalCard for email send (approval required)
4. Click "Approve"
5. **Expected**: Email sent (check Gmail)

#### Test 4: Check Observability

```bash
# Query Application Insights
az monitor app-insights query \
  --app <app-insights-id> \
  --analytics-query "traces | where message contains 'MCP tool invoked' | project timestamp, customDimensions | order by timestamp desc | take 10"

# Should show:
# - CorrelationId for each request
# - UserId: anonymous-user
# - ToolName: tavily_search, twilio_send_sms, etc.
```

**Deliverable**: All 8 test scenarios from "Definition of Done" pass successfully

---

## Troubleshooting

### Issue: "MCP server not configured, skipping MCP tool resources"

**Cause**: `MCP_SERVER_URL` not set in `.env`

**Solution**:
```bash
# Add to .env
MCP_SERVER_URL="https://func-dev-composio-mcp-eastus2.azurewebsites.net/api/mcp"

# Restart backend
dotnet run
```

### Issue: "Failed to obtain MCP access token"

**Cause**: Backend API doesn't have permission to request tokens for MCP server

**Solution**:
1. Verify Entra ID app registration completed (P0-003)
2. Grant Backend API permission to MCP scope (see Phase 4.2)
3. Run `az ad app permission admin-consent` if needed

### Issue: "X-User-Id header is required"

**Cause**: MCP Function requires user identity

**Solution**: Backend passes `DEFAULT_USER_ID` from `.env` (for public app without authentication)

### Issue: "401 Unauthorized" when calling MCP Function

**Cause**: Entra ID token validation failing

**Solutions**:
1. Check `host.json` has correct audience: `api://composio-mcp-function`
2. Verify Backend is requesting token with correct scope
3. Test token manually:
   ```bash
   az account get-access-token --resource api://composio-mcp-function
   ```

### Issue: "Rate limit exceeded"

**Cause**: User exceeded 50 requests per minute

**Solution**: Wait 60 seconds and retry. Consider increasing limit in `ComposioSessionManager.cs` if needed for your use case.

### Issue: OAuth flow fails for Gmail/Google

**Cause**: User hasn't authorized Composio to access their Gmail

**Solution**:
1. When McpApprovalCard shows OAuth URL, click to authorize
2. Complete Google OAuth consent screen
3. Redirect back to app
4. Retry tool invocation

---

## Configuration Reference

### Environment Variables

**Backend API (`.env`)**:
```bash
# Existing Azure AI Foundry config
AZURE_EXISTING_AGENT_ID="sdai-quote-checklist"
AZURE_EXISTING_AIPROJECT_ENDPOINT="https://..."

# New MCP Server config
MCP_SERVER_URL="https://func-dev-composio-mcp-eastus2.azurewebsites.net/api/mcp"
MCP_SERVER_LABEL="composio-tool-router"
MCP_SERVER_AUDIENCE="api://composio-mcp-function/.default"
DEFAULT_USER_ID="anonymous-user"
```

**Azure Function (`local.settings.json`)**:
```json
{
  "Values": {
    "COMPOSIO_API_KEY": "composio_***",
    "AZURE_TENANT_ID": "***",
    "AZURE_CLIENT_ID": "***",
    "ENTRA_AUDIENCE": "api://composio-mcp-function"
  }
}
```

**Azure Function (Azure Portal App Settings)**:
```bash
COMPOSIO_API_KEY=@Microsoft.KeyVault(SecretUri=https://kv-dev-composio-mcp.vault.azure.net/secrets/COMPOSIO-API-KEY)
AZURE_TENANT_ID=***
AZURE_CLIENT_ID=***
ENTRA_AUDIENCE=api://composio-mcp-function
```

### Tool Whitelist

Current configuration (5 tools):

| Tool | Approval | Use Case |
|------|----------|----------|
| `twilio_send_sms` | Always | Send SMS messages |
| `twilio_send_whatsapp` | Always | Send WhatsApp messages |
| `tavily_search` | Never | Web search (read-only) |
| `gmail_send_email` | Always | Send emails |
| `gmail_list_messages` | Never | List emails (read-only) |

To add more tools, modify `Functions/McpToolTriggers.cs` and deploy.

---

## Agent Instructions Template

When configuring your Azure AI Foundry agent, use this instruction template:

```
Eres el Agente Cotizador de Cocinas. Tu objetivo es ayudar a los clientes con cotizaciones personalizadas.

HERRAMIENTAS EXTERNAS DISPONIBLES (via Composio MCP):

1. tavily_search: Busca información en la web
   - Uso: "Busca precios de electrodomésticos para cocinas"
   - Sin aprobación requerida

2. gmail_list_messages: Lista emails recibidos
   - Uso: "Muestra mis últimos emails"
   - Sin aprobación requerida

3. gmail_send_email: Envía emails
   - Uso: "Envía cotización a cliente@example.com"
   - REQUIERE aprobación del usuario

4. twilio_send_sms: Envía SMS
   - Uso: "Envía SMS a +506XXXXXXXX"
   - REQUIERE aprobación del usuario

5. twilio_send_whatsapp: Envía WhatsApp
   - Uso: "Envía WhatsApp a +506XXXXXXXX"
   - REQUIERE aprobación del usuario

REGLAS:
- SIEMPRE confirmar con el usuario antes de usar herramientas que requieren aprobación
- Para búsquedas web, usa tavily_search para obtener información actualizada
- Al enviar cotizaciones por email/SMS/WhatsApp, formatea profesionalmente
- Si el usuario no ha conectado Gmail/Google, explícale cómo hacerlo cuando aparezca el prompt de OAuth
```

---

## Monitoring & Alerts

### Application Insights Queries

**Tool invocation count by tool**:
```kql
traces
| where message contains "Tool invoked"
| extend ToolName = tostring(customDimensions.Tool)
| summarize count() by ToolName
| render piechart
```

**Error rate over time**:
```kql
traces
| where severityLevel >= 3  // Warning or Error
| summarize ErrorCount = count() by bin(timestamp, 5m)
| render timechart
```

**Latency percentiles by tool**:
```kql
customMetrics
| where name == "ToolExecutionTime"
| extend ToolName = tostring(customDimensions.Tool)
| summarize p50=percentile(value, 50), p95=percentile(value, 95), p99=percentile(value, 99) by ToolName
```

### Recommended Alerts

Configure in Azure Portal → Function App → Monitoring → Alerts:

1. **High Error Rate**:
   - Condition: Error rate > 5% over 5 minutes
   - Action: Email to ops team

2. **High Latency**:
   - Condition: P95 latency > 10 seconds
   - Action: Email to ops team

3. **Function Availability**:
   - Condition: Availability < 99%
   - Action: PagerDuty/Opsgenie notification

---

## Cost Monitoring

Expected costs for MVP:

```bash
# Monthly cost breakdown
az consumption usage list \
  --start-date 2026-01-01 \
  --end-date 2026-01-31 \
  --query "[?contains(instanceName, 'composio')]"
```

**Typical usage**:
- Azure Function: ~10,000 requests/month = $5-10
- Application Insights: ~500 MB data/month = $2-5
- Key Vault: ~5,000 operations/month = $0.02
- **Total**: ~$7-15/month

---

## Roadmap

### Completed (MVP)
- [x] 5 whitelisted tools
- [x] Per-user session management
- [x] Entra ID authentication
- [x] Application Insights observability
- [x] Granular approval policies
- [x] Error handling with auto-retry

### Next Steps (Phase 2)
- [ ] Add 5 more tools (Drive, Sheets, Gemini)
- [ ] Implement Redis cache for multi-instance session sharing
- [ ] Add custom metrics dashboard
- [ ] Automated integration tests with Playwright

### Future (Production)
- [ ] Azure API Management (APIM) for governance
- [ ] Container Apps migration (for higher concurrency)
- [ ] Advanced rate limiting policies
- [ ] Compliance audit logs (90-day retention)
- [ ] Multi-environment deployment (dev/staging/prod)

---

## Support & Resources

- **Enterprise Plan**: `.cursor/plans/composio_mcp_integration_bd7e1dc9.plan.md`
- **Function README**: `functions/ComposioMcpServer/README.md`
- **Composio Docs**: https://docs.composio.dev/
- **Azure Functions MCP**: https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-mcp
- **Azure AI Foundry MCP**: https://learn.microsoft.com/en-us/azure/ai-foundry/agents/how-to/tools/model-context-protocol

## Questions?

- Check Application Insights logs for errors
- Review Function logs in Azure Portal
- Consult the enterprise plan for architecture details
- Test locally with `func start` before deploying

---

**Last Updated**: Jan 28, 2026
**Status**: Ready for MVP deployment
