#!/bin/bash

# Setup Entra ID App Registration for Composio MCP Server
# P0-003: Create Entra ID App Registration for MCP Server

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Entra ID App Registration Setup${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Check Azure CLI
if ! command -v az &> /dev/null; then
    echo -e "${RED}Error: Azure CLI is not installed.${NC}"
    exit 1
fi

# Check login
if ! az account show &> /dev/null; then
    echo -e "${RED}Error: Not logged in to Azure CLI.${NC}"
    echo "Run: az login"
    exit 1
fi

TENANT_ID=$(az account show --query tenantId -o tsv)
echo -e "${GREEN}✓ Tenant ID: ${TENANT_ID}${NC}"
echo ""

# Configuration
APP_NAME="Composio MCP Function"
APP_ID_URI="api://composio-mcp-function"
SCOPE_NAME="MCP.Execute"
SCOPE_DESCRIPTION="Execute MCP tools via Composio"

echo -e "${YELLOW}Configuration:${NC}"
echo "  App Name: ${APP_NAME}"
echo "  App ID URI: ${APP_ID_URI}"
echo "  Scope: ${SCOPE_NAME}"
echo ""

read -p "Continue? (y/n): " CONFIRM
if [ "$CONFIRM" != "y" ]; then
    echo "Aborted."
    exit 0
fi

# Check if app already exists
echo ""
echo -e "${YELLOW}Checking for existing app registration...${NC}"
EXISTING_APP_ID=$(az ad app list \
    --filter "displayName eq '${APP_NAME}'" \
    --query "[0].appId" -o tsv 2>/dev/null || echo "")

if [ -n "$EXISTING_APP_ID" ] && [ "$EXISTING_APP_ID" != "null" ]; then
    echo -e "${YELLOW}⚠ App registration already exists: ${EXISTING_APP_ID}${NC}"
    read -p "Delete and recreate? (y/n): " RECREATE
    
    if [ "$RECREATE" == "y" ]; then
        echo -e "${YELLOW}Deleting existing app...${NC}"
        az ad app delete --id "$EXISTING_APP_ID" --output none
        echo -e "${GREEN}✓ Deleted existing app.${NC}"
    else
        echo "Using existing app registration."
        APP_ID="$EXISTING_APP_ID"
        
        # Get object ID
        OBJECT_ID=$(az ad app show --id "$APP_ID" --query id -o tsv)
        
        echo ""
        echo -e "${GREEN}========================================${NC}"
        echo -e "${GREEN}App Registration Details${NC}"
        echo -e "${GREEN}========================================${NC}"
        echo ""
        echo "  App Name: ${APP_NAME}"
        echo "  App ID (Client ID): ${APP_ID}"
        echo "  Tenant ID: ${TENANT_ID}"
        echo "  App ID URI: ${APP_ID_URI}"
        echo "  Scope: ${SCOPE_NAME}"
        echo ""
        echo "Next Steps:"
        echo "  1. Configure Function App host.json with:"
        echo "       \"audience\": \"${APP_ID_URI}\""
        echo "  2. Configure Backend API to request token with scope:"
        echo "       \"${APP_ID_URI}/${SCOPE_NAME}\""
        echo ""
        exit 0
    fi
fi

# Create App Registration
echo ""
echo -e "${YELLOW}Creating App Registration...${NC}"

# Create manifest for app
MANIFEST=$(cat <<EOF
{
  "api": {
    "oauth2PermissionScopes": [
      {
        "adminConsentDescription": "${SCOPE_DESCRIPTION}",
        "adminConsentDisplayName": "${SCOPE_NAME}",
        "id": "$(uuidgen)",
        "isEnabled": true,
        "type": "User",
        "userConsentDescription": "${SCOPE_DESCRIPTION}",
        "userConsentDisplayName": "${SCOPE_NAME}",
        "value": "${SCOPE_NAME}"
      }
    ]
  },
  "identifierUris": ["${APP_ID_URI}"],
  "signInAudience": "AzureADMyOrg"
}
EOF
)

# Create app
APP_ID=$(az ad app create \
    --display-name "$APP_NAME" \
    --sign-in-audience "AzureADMyOrg" \
    --identifier-uris "$APP_ID_URI" \
    --query appId -o tsv)

echo -e "${GREEN}✓ Created App Registration: ${APP_ID}${NC}"

# Get object ID
OBJECT_ID=$(az ad app show --id "$APP_ID" --query id -o tsv)

# Add OAuth2 Permission Scope
echo ""
echo -e "${YELLOW}Adding OAuth2 permission scope...${NC}"

SCOPE_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')

az ad app update --id "$OBJECT_ID" --set api.oauth2PermissionScopes="[{
  \"adminConsentDescription\": \"${SCOPE_DESCRIPTION}\",
  \"adminConsentDisplayName\": \"${SCOPE_NAME}\",
  \"id\": \"${SCOPE_ID}\",
  \"isEnabled\": true,
  \"type\": \"User\",
  \"userConsentDescription\": \"${SCOPE_DESCRIPTION}\",
  \"userConsentDisplayName\": \"${SCOPE_NAME}\",
  \"value\": \"${SCOPE_NAME}\"
}]" --output none

echo -e "${GREEN}✓ Added scope: ${SCOPE_NAME}${NC}"

# Create Service Principal
echo ""
echo -e "${YELLOW}Creating Service Principal...${NC}"
SP_ID=$(az ad sp create --id "$APP_ID" --query id -o tsv)
echo -e "${GREEN}✓ Created Service Principal: ${SP_ID}${NC}"

# Output summary
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Setup Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "App Registration Details:"
echo "  App Name: ${APP_NAME}"
echo "  App ID (Client ID): ${APP_ID}"
echo "  Object ID: ${OBJECT_ID}"
echo "  Tenant ID: ${TENANT_ID}"
echo "  App ID URI: ${APP_ID_URI}"
echo "  Scope: ${SCOPE_NAME}"
echo "  Service Principal ID: ${SP_ID}"
echo ""
echo "Configuration for host.json:"
echo ""
echo "  \"authentication\": {"
echo "    \"type\": \"entra-id\","
echo "    \"audience\": \"${APP_ID_URI}\","
echo "    \"validateIssuer\": true"
echo "  }"
echo ""
echo "Configuration for Backend API (C#):"
echo ""
echo "  var credential = new ChainedTokenCredential("
echo "      new AzureCliCredential(),"
echo "      new ManagedIdentityCredential()"
echo "  );"
echo "  "
echo "  var token = await credential.GetTokenAsync("
echo "      new TokenRequestContext([\"${APP_ID_URI}/.default\"])"
echo "  );"
echo ""
echo "Next Steps:"
echo "  1. Update host.json in Function App with audience above"
echo "  2. Update Backend API to request token with scope above"
echo "  3. Test locally by getting token with Azure CLI:"
echo ""
echo "     az account get-access-token --resource ${APP_ID_URI}"
echo ""
