# Quick Start Checklist - Composio MCP Integration

Use this checklist to deploy the Composio MCP server to Azure and integrate with your AI Foundry agent.

## Pre-Flight Check

Before starting, verify you have:
- [ ] Azure CLI authenticated (`az account show`)
- [ ] .NET 10 SDK installed (`dotnet --version`)
- [ ] Azure Functions Core Tools (`func --version` shows 4.0.7030+)
- [ ] PowerShell 7 installed (`pwsh --version`)

---

## Step 1: Composio Setup (15 min)

- [ ] Go to https://platform.composio.dev/ and create account
- [ ] Generate API key (Settings → API Keys)
- [ ] Save API key: `composio_________________`
- [ ] Configure Twilio auth (Dashboard → Tools → Twilio)
  - [ ] Enter Account SID and Auth Token
  - [ ] Save auth config ID: `ac_twilio_____`
- [ ] Configure Tavily auth (Dashboard → Tools → Tavily)
  - [ ] Enter Tavily API key
  - [ ] Save auth config ID: `ac_tavily_____`
- [ ] Gmail: No action needed (OAuth managed by Composio)

**Verification**: Test API key works:
```bash
curl -X GET https://backend.composio.dev/api/v3/apps \
  -H "X-API-Key: composio_your_key"
```

---

## Step 2: Azure Key Vault (10 min)

- [ ] Run script:
  ```bash
  cd /Users/mosherosenstock/Desktop/foundry-agent-webapp/functions/deployment-scripts
  ./setup-keyvault.sh
  ```
- [ ] Select environment: **dev**
- [ ] Enter Composio API key when prompted
- [ ] Note Key Vault name: `kv-dev-composio-mcp`
- [ ] Note secret URI: `https://kv-dev-composio-mcp.vault.azure.net/secrets/COMPOSIO-API-KEY`

**Verification**: Check secret exists:
```bash
az keyvault secret show --vault-name kv-dev-composio-mcp --name COMPOSIO-API-KEY --query value -o tsv
```

---

## Step 3: Entra ID App Registration (10 min)

- [ ] Run script:
  ```bash
  ./setup-entra-app.sh
  ```
- [ ] Note App ID (Client ID): `________________________________`
- [ ] Note Tenant ID: `________________________________`
- [ ] Note App ID URI: `api://composio-mcp-function`
- [ ] Note Scope: `MCP.Execute`

**Verification**: Check app exists:
```bash
az ad app show --id <APP_ID> --query displayName -o tsv
```

---

## Step 4: Create Azure Function App (15 min)

- [ ] Create Function App:
  ```bash
  az functionapp create \
    --name func-dev-composio-mcp-eastus2 \
    --resource-group rg-dev-composio-mcp \
    --consumption-plan-location eastus2 \
    --runtime dotnet-isolated \
    --runtime-version 10 \
    --functions-version 4 \
    --os-type Linux
  ```
- [ ] Enable Managed Identity:
  ```bash
  az functionapp identity assign \
    --name func-dev-composio-mcp-eastus2 \
    --resource-group rg-dev-composio-mcp
  ```
- [ ] Note Managed Identity Object ID: `________________________________`

---

## Step 5: Grant Key Vault Access (5 min)

- [ ] Grant Function access to Key Vault:
  ```bash
  IDENTITY_ID=$(az functionapp identity show \
    --name func-dev-composio-mcp-eastus2 \
    --resource-group rg-dev-composio-mcp \
    --query principalId -o tsv)
  
  az keyvault set-policy \
    --name kv-dev-composio-mcp \
    --object-id $IDENTITY_ID \
    --secret-permissions get
  ```

**Verification**: Check policy exists:
```bash
az keyvault show --name kv-dev-composio-mcp --query properties.accessPolicies
```

---

## Step 6: Configure Function App Settings (10 min)

- [ ] Set app settings:
  ```bash
  az functionapp config appsettings set \
    --name func-dev-composio-mcp-eastus2 \
    --resource-group rg-dev-composio-mcp \
    --settings \
      "COMPOSIO_API_KEY=@Microsoft.KeyVault(SecretUri=https://kv-dev-composio-mcp.vault.azure.net/secrets/COMPOSIO-API-KEY)" \
      "AZURE_TENANT_ID=<YOUR_TENANT_ID>" \
      "AZURE_CLIENT_ID=<MCP_APP_CLIENT_ID>" \
      "ENTRA_AUDIENCE=api://composio-mcp-function"
  ```
- [ ] Verify settings:
  ```bash
  az functionapp config appsettings list \
    --name func-dev-composio-mcp-eastus2 \
    --resource-group rg-dev-composio-mcp
  ```

---

## Step 7: Deploy Function Code (10 min)

- [ ] Navigate to Function project:
  ```bash
  cd /Users/mosherosenstock/Desktop/foundry-agent-webapp/functions/ComposioMcpServer
  ```
- [ ] Deploy:
  ```bash
  func azure functionapp publish func-dev-composio-mcp-eastus2
  ```
- [ ] Wait for deployment (~2-3 minutes)
- [ ] Note Function URL: `https://func-dev-composio-mcp-eastus2.azurewebsites.net`

**Verification**: Test health endpoint:
```bash
curl https://func-dev-composio-mcp-eastus2.azurewebsites.net/api/health
```
Expected: "MCP Server is healthy"

---

## Step 8: Update Backend Configuration (5 min)

- [ ] Edit `.env` in project root:
  ```bash
  MCP_SERVER_URL="https://func-dev-composio-mcp-eastus2.azurewebsites.net/api/mcp"
  MCP_SERVER_LABEL="composio-tool-router"
  MCP_SERVER_AUDIENCE="api://composio-mcp-function/.default"
  DEFAULT_USER_ID="anonymous-user"
  ```
- [ ] Restart backend (if running):
  ```bash
  cd backend/WebApp.Api
  dotnet run
  ```

**Verification**: Check backend logs for:
```
[INF] Configuring MCP tool resources: Server=https://func-dev-composio-mcp-eastus2...
```

---

## Step 9: Configure Agent in Azure AI Foundry (15 min)

### Option A: Via Portal (Recommended)

- [ ] Open https://ai.azure.com/
- [ ] Navigate to project: `next-sdai`
- [ ] Go to: Agents → `sdai-quote-checklist`
- [ ] Click "Edit Agent"
- [ ] Go to "Tools" section
- [ ] Click "Add Tool" → "MCP Server"
- [ ] Configure:
  - Server Label: `composio-tool-router`
  - Server URL: `https://func-dev-composio-mcp-eastus2.azurewebsites.net/api/mcp`
  - Allowed Tools: (leave empty or list 5 tool names)
- [ ] Click "Save"
- [ ] Save agent

### Option B: Via Agent Instructions (Alternative)

If portal doesn't support MCP configuration yet, update agent instructions to mention available tools:

```
HERRAMIENTAS DISPONIBLES:
- tavily_search: Búsqueda web
- gmail_send_email: Enviar emails
- gmail_list_messages: Listar emails
- twilio_send_sms: Enviar SMS
- twilio_send_whatsapp: Enviar WhatsApp
```

---

## Step 10: Test End-to-End (30 min)

### Test 1: Web Search (No Approval)
- [ ] Open webapp
- [ ] Send: "Search the web for Azure AI Foundry latest features"
- [ ] Verify: Results appear without approval prompt
- [ ] Check: No errors in console

### Test 2: Send SMS (With Approval)
- [ ] Send: "Send SMS to +1234567890: 'Test message'"
- [ ] Verify: McpApprovalCard appears with tool details
- [ ] Click "Approve"
- [ ] Verify: SMS sent (check Twilio dashboard)

### Test 3: Send Email (OAuth + Approval)
- [ ] Send: "Send email to test@example.com with subject 'Test' and body 'Hello'"
- [ ] If not connected: Click OAuth button, authorize Gmail
- [ ] Verify: McpApprovalCard appears for email send
- [ ] Click "Approve"
- [ ] Verify: Email sent (check Gmail)

### Test 4: Application Insights
- [ ] Open Azure Portal → Function App → Application Insights
- [ ] Run query:
  ```kql
  traces | where message contains "Tool invoked" | take 10
  ```
- [ ] Verify: Logs show tool invocations with correlation IDs

---

## Deployment Complete! ✅

Once all checkboxes are checked, your Composio MCP integration is fully operational.

**Your agent can now**:
- Search the web via Tavily
- Send SMS and WhatsApp via Twilio
- Send and list emails via Gmail
- Access 800+ more tools (after adding more tool triggers)

---

## Quick Reference Commands

**View Function logs**:
```bash
func azure functionapp logstream func-dev-composio-mcp-eastus2
```

**Restart Function**:
```bash
az functionapp restart --name func-dev-composio-mcp-eastus2 --resource-group rg-dev-composio-mcp
```

**Update app settings**:
```bash
az functionapp config appsettings set \
  --name func-dev-composio-mcp-eastus2 \
  --resource-group rg-dev-composio-mcp \
  --settings "KEY=value"
```

**Check Function status**:
```bash
az functionapp show \
  --name func-dev-composio-mcp-eastus2 \
  --resource-group rg-dev-composio-mcp \
  --query state -o tsv
```

---

**Estimated Total Time**: 2-3 hours  
**Difficulty**: Intermediate (requires Azure + Composio knowledge)  
**Support**: See `INTEGRATION_GUIDE.md` for detailed troubleshooting

---

Good luck with your deployment! 🚀
