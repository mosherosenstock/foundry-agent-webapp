# Composio Setup Guide

## Overview
This guide walks through setting up Composio for the MCP server integration.

## Step 1: Create Composio Account

1. Go to https://platform.composio.dev/
2. Sign up with your work email
3. Verify email and complete onboarding

## Step 2: Generate API Key

1. Navigate to Settings → API Keys
2. Click "Create New API Key"
3. Name it: "Azure AI Foundry MCP Server"
4. Copy the API key (format: `composio_***`)
5. Store securely - you'll need this for Azure Key Vault

## Step 3: Configure Auth Configs for Tools

### Twilio (SMS + WhatsApp)
1. Go to Composio Dashboard → Tools → Twilio
2. Click "Configure Auth"
3. Create new auth config:
   - Name: "Twilio Production"
   - Account SID: (from your Twilio account)
   - Auth Token: (from your Twilio account)
4. Copy the auth config ID (format: `ac_***`)

### Gmail
1. Go to Composio Dashboard → Tools → Gmail
2. Click "Configure Auth"
3. Select "OAuth 2.0" (default for Gmail)
4. Use Composio's OAuth flow (managed by them)
5. No additional configuration needed - Composio handles OAuth

### Google OAuth / Google Workspace
1. Go to Composio Dashboard → Tools → Google
2. Composio provides built-in Google OAuth
3. No manual configuration required

### Tavily Search
1. Go to https://tavily.com/ and create account
2. Get API key from Tavily dashboard
3. In Composio Dashboard → Tools → Tavily
4. Create auth config:
   - Name: "Tavily Production"
   - API Key: (from Tavily)
5. Copy auth config ID

## Step 4: Test Auth Configs

Use Composio CLI to test:
```bash
npm install -g composio-core
composio login
composio apps list
composio connections list
```

Or test via API:
```bash
curl -X POST https://backend.composio.dev/api/v3/sessions \
  -H "X-API-Key: composio_***" \
  -H "Content-Type: application/json" \
  -d '{"user_id": "test-user"}'
```

Expected response:
```json
{
  "session_id": "sess_***",
  "mcp": {
    "url": "https://mcp.composio.dev/...",
    "headers": {...}
  }
}
```

## Step 5: Document Configuration

Record the following for Azure deployment:
- Composio API Key: `composio_***`
- Twilio Auth Config ID: `ac_twilio_***`
- Tavily Auth Config ID: `ac_tavily_***`
- Gmail: Uses OAuth (no auth config ID needed)

## Troubleshooting

**Issue**: "API key invalid"
- Solution: Regenerate API key in Composio dashboard

**Issue**: "Tool not available"
- Solution: Check if tool is enabled in your Composio plan

**Issue**: "OAuth flow fails"
- Solution: Check redirect URLs in Composio settings

## Next Steps
Once Composio is configured, proceed to P0-002 (Azure Key Vault setup) to store the API key securely.
