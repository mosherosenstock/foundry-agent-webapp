---
name: Composio MCP Integration - Enterprise Architecture
overview: Production-ready integration of Composio Tool Router as a single master-tool into Azure AI Foundry Agents. Provides stable, secure, multi-user MCP server hosted in Azure with strict tool whitelisting, comprehensive observability, and enterprise security controls.
todos:
  - id: mvp-setup-composio
    content: "[MVP-P0] Setup Composio account, obtain API key, configure auth configs for Twilio/Gmail/Google"
    status: pending
  - id: mvp-create-keyvault
    content: "[MVP-P0] Create Azure Key Vault and store COMPOSIO_API_KEY secret"
    status: pending
  - id: mvp-create-function
    content: "[MVP-P0] Create Azure Function App (Flex Consumption) with MCP Extension enabled"
    status: pending
  - id: mvp-implement-session-manager
    content: "[MVP-P0] Implement ComposioSessionManager for per-user session lifecycle"
    status: pending
  - id: mvp-implement-tool-triggers
    content: "[MVP-P1] Implement 5 whitelisted MCP tool triggers (twilio_send_sms, twilio_send_whatsapp, tavily_search, gmail_send_email, gmail_list_messages)"
    status: pending
  - id: mvp-configure-entra-auth
    content: "[MVP-P0] Configure Entra ID authentication for MCP server endpoint"
    status: pending
  - id: mvp-add-app-insights
    content: "[MVP-P1] Configure Application Insights with correlation_id tracking"
    status: pending
  - id: mvp-update-agent-service
    content: "[MVP-P0] Update AgentFrameworkService.cs to add MCPToolDefinition with Functions URL"
    status: pending
  - id: mvp-implement-approval-logic
    content: "[MVP-P1] Implement granular approval logic (always for send actions, never for read)"
    status: pending
  - id: mvp-add-error-handling
    content: "[MVP-P1] Add comprehensive error handling (expired session, OAuth needed, rate limits)"
    status: pending
  - id: mvp-test-local
    content: "[MVP-P2] Test MCP server locally with Azure Functions Core Tools"
    status: pending
  - id: mvp-deploy-azure
    content: "[MVP-P2] Deploy to Azure and run end-to-end integration tests"
    status: pending
  - id: prod-create-apim
    content: "[PROD-P0] Create Azure API Management instance (Developer SKU for staging)"
    status: pending
  - id: prod-configure-apim-policy
    content: "[PROD-P0] Configure APIM inbound policies (auth validation, rate limiting, logging)"
    status: pending
  - id: prod-add-versioning
    content: "[PROD-P1] Add API versioning strategy (v1 path for MCP endpoints)"
    status: pending
  - id: prod-migrate-to-container-apps
    content: "[PROD-P2] Optional: Migrate MCP server from Functions to Container Apps for higher concurrency"
    status: pending
isProject: false
---

# Enterprise Integration Plan: Composio Tool Router + Azure AI Foundry

## Executive Summary

This plan outlines a production-grade architecture for integrating Composio Tool Router as a single "master tool" into Azure AI Foundry Agents. The solution provides:

- **Stable MCP Endpoint**: Azure-hosted MCP server with predictable URLs (not dependent on Composio's rotating session URLs)
- **Multi-User Isolation**: Per-user session management with secure credential separation
- **Tool Governance**: Strict whitelist of 5-15 tools with granular approval policies
- **Enterprise Security**: Entra ID authentication, Key Vault secrets, RBAC, audit logs
- **Full Observability**: Distributed tracing with correlation IDs across all components
- **Error Resilience**: Comprehensive error handling for OAuth, rate limits, provider failures

**Target Tools**: Twilio (SMS + WhatsApp), Tavily search, Gmail, Google OAuth, Gemini connector

---

## Architecture Options

### Option A: MVP Architecture (Azure Functions + MCP Extension)

**When to use**: Initial deployment, proof of concept, cost-sensitive scenarios, low-medium traffic (<1000 req/hour)

**Architecture Diagram (ASCII)**:

```
┌─────────────┐
│   Usuario   │
└──────┬──────┘
       │ HTTPS
       ▼
┌─────────────────────────┐
│  React Frontend (SPA)   │
│  - Built with Azure AI  │
│  - McpApprovalCard UI   │
└──────────┬──────────────┘
           │ POST /api/chat/stream
           ▼
┌─────────────────────────────────────┐
│  Backend API (.NET Container App)   │
│  - AgentFrameworkService.cs         │
│  - Streaming chat endpoint          │
└──────────┬──────────────────────────┘
           │ SSE Streaming
           ▼
┌──────────────────────────────────────────┐
│  Azure AI Foundry Agent Service          │
│  - Loads agent with MCPToolDefinition    │
│  - serverUrl → MCP Function endpoint     │
│  - serverLabel: "composio-tool-router"   │
└──────────┬───────────────────────────────┘
           │ HTTPS (SSE transport)
           │ Headers: 
           │   - Authorization: Bearer <Entra token>
           │   - X-User-Id: <user-identity>
           │   - X-Correlation-Id: <trace-id>
           ▼
┌──────────────────────────────────────────────────┐
│  Azure Function App (MCP Server)                 │
│  ┌────────────────────────────────────────────┐  │
│  │ Entra ID Auth Middleware                   │  │
│  │  - Validates Bearer token                  │  │
│  │  - Extracts user claims                    │  │
│  └────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────┐  │
│  │ ComposioSessionManager                     │  │
│  │  - Per-user session cache (in-memory)      │  │
│  │  - Session refresh logic                   │  │
│  │  - TTL: 1 hour, lazy refresh               │  │
│  └────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────┐  │
│  │ MCP Tool Triggers (5 whitelisted tools)    │  │
│  │  - twilio_send_sms                         │  │
│  │  - twilio_send_whatsapp                    │  │
│  │  - tavily_search                           │  │
│  │  - gmail_send_email                        │  │
│  │  - gmail_list_messages                     │  │
│  └────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────┐  │
│  │ Error Handler & Retry Logic                │  │
│  │  - Session expired → create new            │  │
│  │  - OAuth needed → return auth URL          │  │
│  │  - Rate limited → return 429 + Retry-After │  │
│  └────────────────────────────────────────────┘  │
└──────────┬───────────────────────────────────────┘
           │ Composio SDK calls
           ▼
┌────────────────────────────────────┐
│  Composio API (external SaaS)     │
│  - Tool execution engine           │
│  - OAuth token management          │
│  - Manages credentials per user    │
└──────────┬─────────────────────────┘
           │ API calls (REST)
           ▼
┌──────────────────────────────────────┐
│  External Provider APIs               │
│  - Twilio (SMS, WhatsApp)             │
│  - Tavily (Search)                    │
│  - Gmail API (Google Workspace)       │
│  - Google OAuth                       │
└───────────────────────────────────────┘

Supporting Services (all within same Azure subscription):
┌───────────────────────────┐
│  Azure Key Vault          │
│  - COMPOSIO_API_KEY       │
│  - Function app identity  │
└───────────────────────────┘

┌───────────────────────────┐
│  Application Insights     │
│  - Distributed tracing    │
│  - Custom metrics         │
│  - Structured logs        │
└───────────────────────────┘

┌───────────────────────────┐
│  Entra ID (AAD)           │
│  - Function app reg       │
│  - User authentication    │
│  - RBAC roles             │
└───────────────────────────┘
```

**Azure Services (Exact Names)**:

| Service | Azure Portal Name | SKU/Plan | Purpose | Monthly Cost (Est.) |

|---------|-------------------|----------|---------|---------------------|

| Azure Functions | "Function App" | Flex Consumption (Linux, .NET 10 Isolated) | MCP server hosting | $5-20 |

| Azure Key Vault | "Key Vault" | Standard | Secrets storage | $0.03 per 10K ops |

| Application Insights | "Application Insights" (part of Log Analytics Workspace) | Pay-as-you-go | Observability | $2-10 |

| Azure Container Apps | "Container App" (existing) | Consumption | Backend API (already deployed) | Included |

| Azure AI Foundry | "Azure AI Foundry Hub" + "Azure AI Foundry Project" | Standard | Agent runtime (existing) | Included |

| Entra ID | "Microsoft Entra ID" (formerly Azure AD) | Free tier | Authentication | Free |

**Total Additional Cost**: ~$7-30/month (mostly Functions + App Insights)

**Authentication Model (MVP)**:

1. **Entra ID OAuth 2.0 (Recommended)**

   - Backend API obtains token on behalf of user (OBO flow) or using Managed Identity
   - Token audience: `api://composio-mcp-function` (custom app registration)
   - Scopes: `api://composio-mcp-function/MCP.Execute`
   - Token passed in `Authorization: Bearer <token>` header
   - Function validates using Entra ID middleware

**Required Headers**:

   ```
   Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGc...
   X-User-Id: user-email@domain.com
   X-Correlation-Id: 550e8400-e29b-41d4-a716-446655440000
   Content-Type: application/json
   ```

2. **Alternative: Function Keys (NOT recommended for production)**

   - Use only for local testing
   - Header: `x-functions-key: <function-key>`
   - No user identity validation

**Session State Management (MVP)**:

**Data Model**:

```csharp
public class ComposioUserSession
{
    public string UserId { get; set; }  // From X-User-Id header or Entra claim
    public string SessionUrl { get; set; }  // session.mcp.url from Composio
    public Dictionary<string, string> Headers { get; set; }  // session.mcp.headers
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }  // TTL: 1 hour
    public Dictionary<string, OAuthStatus> ToolAuthStatus { get; set; }  // Per-tool OAuth state
}

public class OAuthStatus
{
    public bool IsAuthenticated { get; set; }
    public string? AuthUrl { get; set; }  // OAuth URL if not authenticated
    public DateTime? LastChecked { get; set; }
}
```

**Session Lifecycle**:

1. **Creation**:

   - Triggered on first tool invocation for a user
   - Call `composio.create(user_id=userId)` via Composio SDK
   - Store session in in-memory cache (ConcurrentDictionary)
   - Set TTL to 1 hour from creation

2. **Retrieval**:

   - Lookup by `userId` in cache
   - Check if expired: `DateTime.UtcNow > session.ExpiresAt`
   - If expired or missing → create new session
   - If valid → return cached session

3. **Refresh** (Lazy):

   - No proactive refresh
   - On tool execution error 401/403 → assume expired
   - Delete old session, create new session, retry tool

4. **Invalidation**:

   - Automatic on TTL expiry
   - Manual on user logout (call `/api/composio/logout` endpoint)
   - On critical error (e.g., Composio API key invalid)

**Storage**: In-memory cache (MemoryCache or ConcurrentDictionary) for MVP. No persistence needed as sessions are short-lived and recreated on demand.

**Whitelist Proposal (MVP - 5 Tools)**:

| Tool Name | Composio Action | Description | Safety Policy | Approval Required |

|-----------|----------------|-------------|---------------|-------------------|

| `twilio_send_sms` | `TWILIO_SEND_SMS` | Send SMS via Twilio | Write operation, triggers external charge | **always** |

| `twilio_send_whatsapp` | `TWILIO_SEND_WHATSAPP_MESSAGE` | Send WhatsApp via Twilio | Write operation, triggers external charge | **always** |

| `tavily_search` | `TAVILY_SEARCH` | Web search via Tavily API | Read-only, no side effects | **never** |

| `gmail_send_email` | `GMAIL_SEND_EMAIL` | Send email via Gmail | Write operation, PII involved | **always** |

| `gmail_list_messages` | `GMAIL_LIST_MESSAGES` | List emails (read-only) | Read-only, PII involved but no modification | **never** |

**Expansion (10 tools - Phase 2)**:

- `gmail_create_draft`, `gmail_search_emails`
- `google_auth_status` (check OAuth state)
- `gemini_generate_text` (Gemini LLM calls via Composio)
- `googledrive_create_file`, `googledrive_share_file`

**Safety Configuration**:

```csharp
private static readonly Dictionary<string, MCPApproval> ToolApprovalPolicy = new()
{
    ["twilio_send_sms"] = new MCPApproval("always"),
    ["twilio_send_whatsapp"] = new MCPApproval("always"),
    ["gmail_send_email"] = new MCPApproval("always"),
    ["tavily_search"] = new MCPApproval("never"),
    ["gmail_list_messages"] = new MCPApproval("never"),
};
```

**Error Handling Plan (MVP)**:

| Error Scenario | Detection | Response | User Experience | Retry Strategy |

|----------------|-----------|----------|-----------------|----------------|

| **Session Expired** | Composio SDK returns 401/403 | Delete cached session, create new, retry tool once | Transparent (auto-retry) | 1 retry with exponential backoff |

| **OAuth Not Connected** | Composio returns error with `oauth_url` | Return `McpToolCallApprovalRequestItem` with OAuth URL in arguments | Show McpApprovalCard with "Connect Gmail" button → OAuth flow | No retry until user completes OAuth |

| **Rate Limited (Composio)** | HTTP 429 from Composio API | Return error to agent with `Retry-After` header | Show error message: "Tool temporarily unavailable, try again in X seconds" | Agent can retry after delay |

| **Rate Limited (Provider, e.g., Twilio)** | Composio forwards provider 429 | Same as above | Same as above | Agent can retry after delay |

| **Tool Execution Failed** | Composio returns error (400, 500) | Log error with correlation_id, return error to agent | Show error message with details (sanitized) | No automatic retry, user can retry manually |

| **Invalid Parameters** | MCP tool trigger validation fails | Return 400 Bad Request | Show error: "Invalid parameters for tool X" | No retry, user fixes prompt |

| **Network Timeout** | Composio SDK timeout (30s default) | Catch TimeoutException, return 504 | Show error: "Tool timed out, try again" | 1 retry with longer timeout |

| **Composio API Key Invalid** | 401 Unauthorized from Composio | Log critical alert, return 500 | Show generic error (don't expose API key issue) | No retry, requires admin intervention |

**Error Response Format**:

```json
{
  "error": {
    "code": "session_expired",
    "message": "Composio session expired. Retrying...",
    "correlation_id": "550e8400-e29b-41d4-a716-446655440000",
    "retriable": true,
    "retry_after_seconds": 2
  }
}
```

**Security Checklist (MVP)**:

- [ ] **Authentication & Authorization**
  - [ ] Entra ID authentication enforced on MCP Function endpoint
  - [ ] Token validation configured (issuer, audience, signature)
  - [ ] User identity extracted from token claims
  - [ ] Function app has Managed Identity enabled
  - [ ] RBAC role assigned to backend API's Managed Identity to call Function

- [ ] **Secrets Management**
  - [ ] COMPOSIO_API_KEY stored in Azure Key Vault
  - [ ] Function app configured with Key Vault reference (not plain text in settings)
  - [ ] Key Vault access policy grants Function's Managed Identity "Get Secret" permission
  - [ ] Secrets rotated every 90 days (manual process for MVP)

- [ ] **Network Security**
  - [ ] Function app enforces HTTPS only (HTTP disabled)
  - [ ] CORS configured to allow only backend API origin
  - [ ] Function app has "Require authentication" enabled in Azure Portal
  - [ ] No public access to Function app without valid Entra token

- [ ] **Data Protection**
  - [ ] User sessions stored in-memory only (not persisted to disk/db)
  - [ ] No logging of OAuth tokens, API keys, or PII in plain text
  - [ ] Correlation IDs used instead of user IDs in logs where possible
  - [ ] Sensitive headers (Authorization) excluded from Application Insights logs

- [ ] **Input Validation**
  - [ ] X-User-Id header validated (format, length, character whitelist)
  - [ ] MCP tool parameters validated against schema before execution
  - [ ] Tool names validated against whitelist (reject unknown tools)
  - [ ] SQL injection / command injection risks N/A (no direct DB/OS access)

- [ ] **Audit & Compliance**
  - [ ] All tool invocations logged with user_id, tool_name, timestamp
  - [ ] Failed authentication attempts logged
  - [ ] Approval decisions (approve/reject) logged with user_id
  - [ ] Logs retained for 90 days minimum

- [ ] **Dependency Security**
  - [ ] NuGet packages up to date (check with `dotnet list package --vulnerable`)
  - [ ] Composio SDK from official source only
  - [ ] Function runtime (.NET 10) up to date

- [ ] **Rate Limiting & DoS Protection**
  - [ ] Azure Functions built-in rate limiting configured (100 concurrent requests)
  - [ ] Per-user rate limit: 50 tool invocations per minute (tracked in memory)
  - [ ] Composio SDK configured with timeout: 30 seconds

**Observability Checklist (MVP)**:

- [ ] **Distributed Tracing**
  - [ ] Application Insights enabled for Function app
  - [ ] correlation_id generated at Backend API and propagated to Function
  - [ ] correlation_id included in all logs and telemetry
  - [ ] OpenTelemetry or W3C Trace Context used for correlation

- [ ] **Structured Logging**
  - [ ] Use ILogger with structured log messages (not string concatenation)
  - [ ] Log level: Information for normal flow, Warning for retriable errors, Error for critical issues
  - [ ] Include context: user_id, tool_name, correlation_id, execution_time_ms

**Example**:

  ```csharp
  _logger.LogInformation(
      "MCP tool invoked. UserId={UserId}, Tool={Tool}, CorrelationId={CorrelationId}",
      userId, toolName, correlationId);
  ```

- [ ] **Custom Metrics**
  - [ ] Tool invocation count (by tool_name, status: success/failure)
  - [ ] Session creation count (by user_id)
  - [ ] OAuth connection attempts (by provider: gmail, google, twilio)
  - [ ] Error rate (by error_type: session_expired, oauth_needed, rate_limited)
  - [ ] Execution time (p50, p95, p99 latency for each tool)

- [ ] **Alerts**
  - [ ] Alert on error rate > 5% (5-minute window)
  - [ ] Alert on Composio API key expiration (proactive check)
  - [ ] Alert on high latency (p95 > 10 seconds)
  - [ ] Alert on Function app availability < 99%

- [ ] **Dashboards**
  - [ ] Application Insights dashboard with:
    - Request rate (requests/minute)
    - Tool usage breakdown (pie chart)
    - Error rate over time (line chart)
    - Top 10 users by tool invocations
    - Latency percentiles (p50, p95, p99)

**Definition of Done - End-to-End Tests (MVP)**:

- [ ] **Test 1: Happy Path - Tavily Search (Read-Only)**
  - [ ] User prompt: "Search the web for Azure AI Foundry pricing"
  - [ ] Expected: Agent invokes `tavily_search` without approval prompt
  - [ ] Expected: Search results returned in chat within 5 seconds
  - [ ] Expected: No errors logged

- [ ] **Test 2: Approval Required - SMS Send**
  - [ ] User prompt: "Send SMS to +1234567890: 'Test message'"
  - [ ] Expected: Agent invokes `twilio_send_sms` and pauses for approval
  - [ ] Expected: Frontend shows McpApprovalCard with tool details
  - [ ] User clicks "Approve"
  - [ ] Expected: SMS sent successfully
  - [ ] Expected: Confirmation message in chat

- [ ] **Test 3: OAuth Not Connected - Gmail**
  - [ ] User prompt: "Send email to test@example.com"
  - [ ] Expected: Composio returns OAuth URL
  - [ ] Expected: Frontend shows McpApprovalCard with "Connect Gmail" button
  - [ ] User clicks button → OAuth flow → Redirected back
  - [ ] Expected: Tool execution retried automatically
  - [ ] Expected: Email sent successfully

- [ ] **Test 4: Session Expired - Auto Retry**
  - [ ] Simulate expired session (manually delete from cache or wait 1 hour)
  - [ ] User prompt: "List my Gmail messages"
  - [ ] Expected: First attempt returns 401, session recreated automatically
  - [ ] Expected: Second attempt succeeds without user intervention
  - [ ] Expected: Messages listed in chat

- [ ] **Test 5: Rate Limit - Provider**
  - [ ] Simulate Twilio rate limit (send 100 SMS rapidly)
  - [ ] Expected: Function returns 429 with Retry-After
  - [ ] Expected: Frontend shows "Please wait X seconds and try again"

- [ ] **Test 6: Multi-User Isolation**
  - [ ] User A connects Gmail with account A@gmail.com
  - [ ] User B connects Gmail with account B@gmail.com
  - [ ] User A sends email → Expected: Sent from A@gmail.com
  - [ ] User B sends email → Expected: Sent from B@gmail.com
  - [ ] Expected: No credential leakage between users

- [ ] **Test 7: Error Handling - Invalid Parameters**
  - [ ] User prompt: "Send SMS to invalid-phone-number"
  - [ ] Expected: Twilio returns 400 error
  - [ ] Expected: Error message shown to user (sanitized)
  - [ ] Expected: Error logged with correlation_id

- [ ] **Test 8: Observability - Correlation Tracking**
  - [ ] User prompt: "Search Tavily for Azure pricing"
  - [ ] Expected: correlation_id generated at Backend API
  - [ ] Expected: Same correlation_id logged in Function app
  - [ ] Expected: Application Insights shows end-to-end trace
  - [ ] Verify: All spans (Backend → Foundry → Function → Composio) linked

---

### Option B: Production Architecture (APIM + Functions/Container Apps)

**When to use**: Production workloads, high traffic (>1000 req/hour), strict governance requirements, need for API versioning, multi-environment deployments

**Architecture Diagram (ASCII)**:

```
┌─────────────┐
│   Usuario   │
└──────┬──────┘
       │ HTTPS
       ▼
┌──────────────────────────┐
│  React Frontend (SPA)    │
└──────────┬───────────────┘
           │
           ▼
┌───────────────────────────────┐
│  Backend API (Container App)  │
└──────────┬────────────────────┘
           │
           ▼
┌─────────────────────────────────┐
│  Azure AI Foundry Agent Service │
│  MCPToolDefinition:             │
│    serverUrl: https://apim.     │
│      azure-api.net/mcp/v1       │
└──────────┬──────────────────────┘
           │ HTTPS
           │ Authorization: Bearer <token>
           ▼
┌──────────────────────────────────────────────────────┐
│  Azure API Management (APIM)                         │
│  ┌────────────────────────────────────────────────┐  │
│  │ Inbound Policy                                 │  │
│  │  1. Validate Entra ID JWT                     │  │
│  │  2. Rate limit: 1000 req/min per user         │  │
│  │  3. Add X-Correlation-Id if missing           │  │
│  │  4. Transform: Add backend auth header        │  │
│  │  5. Log request to App Insights               │  │
│  └────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────┐  │
│  │ Backend                                        │  │
│  │  - Route to Function App or Container Apps    │  │
│  │  - Timeout: 60 seconds                        │  │
│  └────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────┐  │
│  │ Outbound Policy                                │  │
│  │  1. Remove sensitive headers                   │  │
│  │  2. Add cache headers (for read-only ops)     │  │
│  │  3. Log response to App Insights              │  │
│  └────────────────────────────────────────────────┘  │
└──────────┬───────────────────────────────────────────┘
           │ HTTPS (backend)
           ▼
┌──────────────────────────────────────────────────┐
│  MCP Server Backend (2 options)                  │
│                                                   │
│  Option B1: Azure Functions (same as MVP)        │
│    - Flex Consumption plan                       │
│    - Scales 0-1000 instances                     │
│    - Good for bursty workload                    │
│                                                   │
│  Option B2: Azure Container Apps                 │
│    - Dedicated plan (0.25 vCPU, 0.5 GB RAM)     │
│    - Scales 1-30 replicas                        │
│    - Better for sustained load                   │
│    - Supports WebSocket (if needed)              │
│                                                   │
│  Implementation: Same code as MVP                │
└──────────┬───────────────────────────────────────┘
           │
           ▼
┌──────────────────────────┐
│  Composio API            │
└──────────────────────────┘

Supporting Services:
┌───────────────────────────┐
│  Azure Key Vault          │
│  - Production secrets     │
└───────────────────────────┘

┌───────────────────────────┐
│  Application Insights     │
│  - APIM integration       │
│  - Distributed tracing    │
└───────────────────────────┘

┌───────────────────────────┐
│  Azure Monitor            │
│  - Alerts & dashboards    │
└───────────────────────────┘
```

**Azure Services (Production)**:

| Service | Azure Portal Name | SKU/Plan | Purpose | Monthly Cost (Est.) |

|---------|-------------------|----------|---------|---------------------|

| Azure API Management | "API Management service" | Developer (for staging) or Standard (for prod) | API gateway, rate limiting, auth, logging | Developer: $50, Standard: $250 |

| Azure Functions | "Function App" | Flex Consumption OR Premium (EP1) | MCP server (Option B1) | Flex: $5-20, Premium: $200 |

| Azure Container Apps | "Container App" | Dedicated (0.25 vCPU) | MCP server (Option B2) | $15-50 |

| Azure Key Vault | "Key Vault" | Standard | Secrets | $0.03 per 10K ops |

| Application Insights | "Application Insights" | Pay-as-you-go | Observability | $10-50 |

| Azure Monitor | "Azure Monitor" | Pay-as-you-go | Alerts | $5-20 |

**Total Additional Cost (Production)**:

- With APIM Developer + Functions: ~$65-90/month
- With APIM Standard + Container Apps: ~$280-350/month

**Authentication Model (Production)**:

Same as MVP (Entra ID OAuth 2.0) but with additional APIM validation:

1. **APIM Inbound Policy**:
```xml
<policies>
    <inbound>
        <!-- Validate JWT issued by Entra ID -->
        <validate-jwt header-name="Authorization" 
                      failed-validation-httpcode="401" 
                      failed-validation-error-message="Unauthorized">
            <openid-config url="https://login.microsoftonline.com/{tenant-id}/v2.0/.well-known/openid-configuration" />
            <audiences>
                <audience>api://composio-mcp-function</audience>
            </audiences>
            <required-claims>
                <claim name="scp" match="any">
                    <value>MCP.Execute</value>
                </claim>
            </required-claims>
        </validate-jwt>
        
        <!-- Rate limiting per user -->
        <rate-limit-by-key calls="1000" 
                           renewal-period="60" 
                           counter-key="@(context.Request.Headers.GetValueOrDefault("X-User-Id","anonymous"))" />
        
        <!-- Add correlation ID if missing -->
        <choose>
            <when condition="@(!context.Request.Headers.ContainsKey("X-Correlation-Id"))">
                <set-header name="X-Correlation-Id" exists-action="override">
                    <value>@(Guid.NewGuid().ToString())</value>
                </set-header>
            </when>
        </choose>
        
        <!-- Add backend authentication (Managed Identity token) -->
        <authentication-managed-identity resource="https://management.azure.com/" />
        
        <!-- Log to Application Insights -->
        <log-to-eventhub logger-id="apim-logger">
            @{
                return new {
                    timestamp = DateTime.UtcNow,
                    correlation_id = context.Request.Headers.GetValueOrDefault("X-Correlation-Id"),
                    user_id = context.Request.Headers.GetValueOrDefault("X-User-Id"),
                    method = context.Request.Method,
                    url = context.Request.Url.Path
                };
            }
        </log-to-eventhub>
    </inbound>
    <backend>
        <forward-request timeout="60" />
    </backend>
    <outbound>
        <!-- Remove sensitive headers from response -->
        <set-header name="X-Internal-Error" exists-action="delete" />
        
        <!-- Add cache headers for read-only operations -->
        <choose>
            <when condition="@(context.Request.Url.Path.Contains("tavily_search") || context.Request.Url.Path.Contains("gmail_list"))">
                <cache-store duration="300" />
            </when>
        </choose>
    </outbound>
    <on-error>
        <log-to-eventhub logger-id="apim-logger">
            @{
                return new {
                    timestamp = DateTime.UtcNow,
                    correlation_id = context.Request.Headers.GetValueOrDefault("X-Correlation-Id"),
                    error = context.LastError.Message
                };
            }
        </log-to-eventhub>
    </on-error>
</policies>
```


**Session State Management (Production)**:

Same data model as MVP, but with enhanced storage options:

**Option 1: Redis Cache (Recommended for multi-instance)**

- Use Azure Cache for Redis (Basic, 250 MB)
- Cost: ~$15/month
- TTL handled by Redis
- Shared across all Function/Container App instances
- Example key: `composio-session:{userId}`

**Option 2: Azure Cosmos DB (For audit trail)**

- Use Cosmos DB (serverless mode)
- Cost: ~$1/month for low volume
- Stores session history for compliance
- Query sessions by user_id, timestamp

**Implementation** (Redis example):

```csharp
public class ComposioSessionManager
{
    private readonly IConnectionMultiplexer _redis;
    
    public async Task<ComposioUserSession> GetOrCreateSessionAsync(string userId)
    {
        var db = _redis.GetDatabase();
        var key = $"composio-session:{userId}";
        
        var cached = await db.StringGetAsync(key);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<ComposioUserSession>(cached);
        }
        
        // Create new session via Composio SDK
        var session = await _composioClient.CreateSessionAsync(userId);
        var userSession = new ComposioUserSession
        {
            UserId = userId,
            SessionUrl = session.Mcp.Url,
            Headers = session.Mcp.Headers,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        
        await db.StringSetAsync(key, JsonSerializer.Serialize(userSession), TimeSpan.FromHours(1));
        return userSession;
    }
}
```

**Whitelist (Production - 10 Tools)**:

Same as MVP + additional tools:

- `gmail_create_draft`, `gmail_search_emails`
- `google_drive_create_file`, `google_drive_share_file`
- `gemini_generate_text`

**Error Handling (Production)**:

Same as MVP, plus:

- APIM returns standardized error responses (RFC 7807 Problem Details)
- Circuit breaker pattern in APIM (fail fast if Composio API is down)
- Retry with exponential backoff (implemented in APIM policy)

**Security Checklist (Production)**:

All items from MVP, plus:

- [ ] APIM configured with TLS 1.3 minimum
- [ ] APIM IP whitelist (if internal only)
- [ ] APIM subscription keys rotated every 30 days
- [ ] Web Application Firewall (WAF) enabled on APIM (requires Premium SKU)
- [ ] DDoS protection enabled on APIM VNet (if using VNet)
- [ ] Penetration testing performed (annually)

**Observability (Production)**:

All items from MVP, plus:

- [ ] APIM logs sent to Log Analytics Workspace
- [ ] Custom alerting rules:
  - Alert on 5xx errors from APIM
  - Alert on circuit breaker open
  - Alert on unusual traffic patterns (spike detection)
- [ ] Azure Monitor Workbooks for dashboards
- [ ] Automated incident response (integrate with PagerDuty/Opsgenie)

---

## Implementation Backlog (Prioritized)

### Sprint 0: Foundation Setup (1-2 days)

**P0-001: Setup Composio Account & API Key**

- **Acceptance Criteria**:
  - [ ] Composio account created at platform.composio.dev
  - [ ] API key generated and tested with curl
  - [ ] Auth configs created for Twilio, Gmail, Google OAuth
  - [ ] Test auth flow for at least one provider (Gmail recommended)
- **Effort**: 2 hours
- **Dependencies**: None

**P0-002: Create Azure Key Vault & Store Secrets**

- **Acceptance Criteria**:
  - [ ] Key Vault created with naming convention: `kv-<env>-composio-mcp`
  - [ ] Secret `COMPOSIO-API-KEY` stored
  - [ ] Key Vault configured with access policy: "Get Secret" for Function Managed Identity
  - [ ] Test secret retrieval using Azure CLI
- **Effort**: 1 hour
- **Dependencies**: P0-001

**P0-003: Create Entra ID App Registration for MCP Server**

- **Acceptance Criteria**:
  - [ ] App registration created: "Composio MCP Function"
  - [ ] App ID URI: `api://composio-mcp-function`
  - [ ] Scope created: `MCP.Execute`
  - [ ] Backend API granted permission to this scope
  - [ ] Test token acquisition from Backend API
- **Effort**: 2 hours
- **Dependencies**: None

### Sprint 1: MVP Core Implementation (3-5 days)

**P0-004: Create Azure Function App with MCP Extension**

- **Acceptance Criteria**:
  - [ ] Function App created: Flex Consumption, .NET 10 Isolated, Linux
  - [ ] Naming: `func-<env>-composio-mcp-<region>`
  - [ ] Application Insights enabled
  - [ ] Managed Identity enabled
  - [ ] Key Vault reference configured in app settings
  - [ ] MCP extension added via host.json
  - [ ] Local project created and runs with `func start`
- **Effort**: 4 hours
- **Dependencies**: P0-002, P0-003

**P0-005: Implement ComposioSessionManager**

- **Acceptance Criteria**:
  - [ ] Class `ComposioSessionManager` created with GetOrCreateSessionAsync method
  - [ ] In-memory cache using ConcurrentDictionary
  - [ ] Session TTL: 1 hour
  - [ ] Unit tests: session creation, retrieval, expiration
  - [ ] Integration test with real Composio API
- **Effort**: 6 hours
- **Dependencies**: P0-004

**P0-006: Implement 5 Whitelisted MCP Tool Triggers**

- **Acceptance Criteria**:
  - [ ] 5 Function triggers created: twilio_send_sms, twilio_send_whatsapp, tavily_search, gmail_send_email, gmail_list_messages
  - [ ] Each trigger calls Composio SDK via SessionManager
  - [ ] Parameter validation implemented
  - [ ] Error handling for each tool (session expired, OAuth needed, rate limit)
  - [ ] Local testing: all 5 tools execute successfully with mock data
- **Effort**: 8 hours
- **Dependencies**: P0-005

**P0-007: Configure Entra ID Authentication on Function**

- **Acceptance Criteria**:
  - [ ] host.json configured with Entra ID auth
  - [ ] Token validation tested: valid token → 200, invalid token → 401
  - [ ] User identity extracted from claims
  - [ ] X-User-Id header validated
- **Effort**: 3 hours
- **Dependencies**: P0-006

**P0-008: Add Application Insights with Correlation Tracking**

- **Acceptance Criteria**:
  - [ ] ILogger used throughout code
  - [ ] correlation_id propagated from Backend API
  - [ ] All logs include: user_id, tool_name, correlation_id, execution_time_ms
  - [ ] Custom metrics tracked: tool invocation count, error rate
  - [ ] Application Insights dashboard created with key metrics
- **Effort**: 4 hours
- **Dependencies**: P0-007

**P0-009: Update AgentFrameworkService.cs for MCP Integration**

- **Acceptance Criteria**:
  - [ ] Method `CreateAgentWithMcpAsync` added
  - [ ] MCPToolDefinition configured with Function URL
  - [ ] AllowedTools set to whitelist (5 tools)
  - [ ] Existing agent re-created with MCP tool
  - [ ] Test: agent loads successfully and shows MCP tool in metadata
- **Effort**: 3 hours
- **Dependencies**: P0-008

**P0-010: Implement Granular Approval Logic in Backend**

- **Acceptance Criteria**:
  - [ ] StreamMessageAsync updated to include MCPToolResource with headers
  - [ ] Approval policy dictionary configured (always for send, never for read)
  - [ ] Entra token acquisition implemented
  - [ ] correlation_id generated and passed
  - [ ] Test: send action → approval prompt, read action → auto-execute
- **Effort**: 4 hours
- **Dependencies**: P0-009

### Sprint 2: Testing & Deployment (2-3 days)

**P0-011: Comprehensive Error Handling Implementation**

- **Acceptance Criteria**:
  - [ ] All 8 error scenarios from table implemented and tested
  - [ ] Error responses include correlation_id
  - [ ] Retry logic: session expired → auto-retry once
  - [ ] OAuth not connected → return auth URL
  - [ ] Rate limited → return 429 with Retry-After header
  - [ ] Unit tests for each error scenario
- **Effort**: 6 hours
- **Dependencies**: P0-010

**P0-012: Local End-to-End Testing**

- **Acceptance Criteria**:
  - [ ] All 8 "Definition of Done" test scenarios pass locally
  - [ ] Frontend McpApprovalCard tested with real OAuth flow
  - [ ] Multi-user isolation verified (2 different user accounts)
  - [ ] Performance test: 100 concurrent requests handled without errors
- **Effort**: 4 hours
- **Dependencies**: P0-011

**P0-013: Deploy to Azure and Integration Testing**

- **Acceptance Criteria**:
  - [ ] Function deployed to Azure using `azd deploy`
  - [ ] Backend API updated with production MCP URL
  - [ ] All 8 test scenarios re-run in Azure environment
  - [ ] Application Insights shows end-to-end traces
  - [ ] No errors in Function logs
- **Effort**: 3 hours
- **Dependencies**: P0-012

**P0-014: Security Validation & Checklist Completion**

- **Acceptance Criteria**:
  - [ ] All items in "Security Checklist (MVP)" verified
  - [ ] No secrets in code or logs
  - [ ] Entra ID auth working correctly
  - [ ] Key Vault access confirmed
  - [ ] Security scan run (Azure Defender or similar)
- **Effort**: 3 hours
- **Dependencies**: P0-013

**P0-015: Observability Validation & Alerting Setup**

- **Acceptance Criteria**:
  - [ ] All items in "Observability Checklist (MVP)" verified
  - [ ] Alerts configured: error rate, high latency, availability
  - [ ] Dashboard shows real data after deployment
  - [ ] Test alerts by simulating error conditions
- **Effort**: 2 hours
- **Dependencies**: P0-014

### Sprint 3: Production Hardening (APIM) - Optional (3-5 days)

**P1-016: Create Azure API Management Instance**

- **Acceptance Criteria**:
  - [ ] APIM created: Developer SKU for staging
  - [ ] Naming: `apim-<env>-composio-mcp`
  - [ ] API created: "Composio MCP API"
  - [ ] Backend configured to point to Function URL
  - [ ] Test: GET /health returns 200
- **Effort**: 2 hours
- **Dependencies**: P0-015

**P1-017: Configure APIM Inbound Policies**

- **Acceptance Criteria**:
  - [ ] JWT validation policy added (Entra ID)
  - [ ] Rate limiting policy: 1000 req/min per user
  - [ ] Correlation ID injection
  - [ ] Logging to Application Insights
  - [ ] Test: policies work end-to-end
- **Effort**: 4 hours
- **Dependencies**: P1-016

**P1-018: Add API Versioning Strategy**

- **Acceptance Criteria**:
  - [ ] API version: v1 (path-based: /mcp/v1)
  - [ ] Version documented in APIM portal
  - [ ] Backend API updated to use versioned URL
  - [ ] Test: v1 endpoint works, v2 (future) returns 404
- **Effort**: 2 hours
- **Dependencies**: P1-017

**P1-019: (Optional) Migrate to Azure Container Apps**

- **Acceptance Criteria**:
  - [ ] Container image built for MCP server (.NET app)
  - [ ] Container App created with 0.25 vCPU, 0.5 GB RAM
  - [ ] Scale rules: 1-30 replicas, CPU threshold 70%
  - [ ] APIM backend updated to Container App URL
  - [ ] Test: same functionality as Functions, better sustained load performance
- **Effort**: 6 hours
- **Dependencies**: P1-018

**P1-020: Production Deployment & Final Validation**

- **Acceptance Criteria**:
  - [ ] All production services deployed
  - [ ] Load testing: 1000 concurrent users
  - [ ] Security review completed (pen test if required)
  - [ ] Runbook documented for ops team
  - [ ] Sign-off from stakeholders
- **Effort**: 4 hours
- **Dependencies**: P1-019

---

## Summary & Next Steps

**MVP Timeline**: 7-10 days (1 developer)

**Production Timeline**: 14-18 days (1 developer)

**Recommended Path**:

1. Start with MVP (Azure Functions only)
2. Deploy to staging environment
3. Run for 2 weeks with real traffic
4. Collect metrics and user feedback
5. Decide if APIM is needed based on:

   - Traffic volume (>1000 req/hour → APIM recommended)
   - Governance requirements (versioning, rate limiting)
   - Budget approval ($50-250/month for APIM)

**Critical Success Factors**:

- Multi-user session isolation (MUST work correctly)
- Approval flow for "send" actions (MUST require explicit user consent)
- Observability with correlation IDs (MUST trace end-to-end)
- Security (MUST use Entra ID, Key Vault, no hardcoded secrets)

**Questions for Stakeholders**:

1. What is expected monthly traffic? (determines Functions vs Container Apps choice)
2. Is APIM required from day 1, or can we start with MVP?
3. Budget approval for APIM ($50-250/month)?
4. Do we need Redis for session management, or is in-memory sufficient? (depends on scale)
5. Compliance requirements (GDPR, SOC 2)? (may require audit logs, data residency)

---

End of Plan. Ready for implementation.