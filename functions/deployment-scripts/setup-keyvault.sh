#!/bin/bash

# Setup Azure Key Vault for Composio MCP Server
# P0-002: Create Azure Key Vault and store COMPOSIO_API_KEY

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Azure Key Vault Setup for MCP Server${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}Error: Azure CLI is not installed.${NC}"
    echo "Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if logged in
echo -e "${YELLOW}Checking Azure CLI login status...${NC}"
if ! az account show &> /dev/null; then
    echo -e "${RED}Error: Not logged in to Azure CLI.${NC}"
    echo "Run: az login"
    exit 1
fi

# Get current subscription
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
echo -e "${GREEN}✓ Using subscription: ${SUBSCRIPTION_NAME} (${SUBSCRIPTION_ID})${NC}"
echo ""

# Prompt for environment
echo -e "${YELLOW}Select environment:${NC}"
echo "1) dev"
echo "2) staging"
echo "3) prod"
read -p "Enter choice [1-3]: " ENV_CHOICE

case $ENV_CHOICE in
    1) ENV="dev" ;;
    2) ENV="staging" ;;
    3) ENV="prod" ;;
    *) echo -e "${RED}Invalid choice. Exiting.${NC}"; exit 1 ;;
esac

# Configuration
LOCATION="eastus2"
RESOURCE_GROUP="rg-${ENV}-composio-mcp"
KEY_VAULT_NAME="kv-${ENV}-composio-mcp"
FUNCTION_APP_NAME="func-${ENV}-composio-mcp-${LOCATION}"

echo ""
echo -e "${YELLOW}Configuration:${NC}"
echo "  Environment: ${ENV}"
echo "  Location: ${LOCATION}"
echo "  Resource Group: ${RESOURCE_GROUP}"
echo "  Key Vault: ${KEY_VAULT_NAME}"
echo "  Function App: ${FUNCTION_APP_NAME}"
echo ""

read -p "Continue? (y/n): " CONFIRM
if [ "$CONFIRM" != "y" ]; then
    echo "Aborted."
    exit 0
fi

# Create Resource Group
echo ""
echo -e "${YELLOW}Creating Resource Group...${NC}"
if az group show --name "$RESOURCE_GROUP" &> /dev/null; then
    echo -e "${GREEN}✓ Resource Group already exists.${NC}"
else
    az group create \
        --name "$RESOURCE_GROUP" \
        --location "$LOCATION" \
        --output none
    echo -e "${GREEN}✓ Created Resource Group: ${RESOURCE_GROUP}${NC}"
fi

# Create Key Vault
echo ""
echo -e "${YELLOW}Creating Key Vault...${NC}"
if az keyvault show --name "$KEY_VAULT_NAME" &> /dev/null; then
    echo -e "${GREEN}✓ Key Vault already exists.${NC}"
else
    az keyvault create \
        --name "$KEY_VAULT_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --location "$LOCATION" \
        --enable-rbac-authorization false \
        --output none
    echo -e "${GREEN}✓ Created Key Vault: ${KEY_VAULT_NAME}${NC}"
fi

# Prompt for Composio API Key
echo ""
echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}Composio API Key Setup${NC}"
echo -e "${YELLOW}========================================${NC}"
echo ""
echo "Before proceeding, you need a Composio API key."
echo "If you don't have one:"
echo "  1. Go to https://platform.composio.dev/"
echo "  2. Sign up / Log in"
echo "  3. Navigate to Settings → API Keys"
echo "  4. Create new API key named: 'Azure AI Foundry MCP Server'"
echo ""
read -p "Do you have your Composio API key ready? (y/n): " HAS_KEY

if [ "$HAS_KEY" != "y" ]; then
    echo ""
    echo -e "${YELLOW}Please obtain your Composio API key and run this script again.${NC}"
    echo "For instructions, see: ../COMPOSIO_SETUP.md"
    exit 0
fi

echo ""
read -sp "Enter Composio API Key (input hidden): " COMPOSIO_API_KEY
echo ""

if [ -z "$COMPOSIO_API_KEY" ]; then
    echo -e "${RED}Error: API key cannot be empty.${NC}"
    exit 1
fi

# Store secret in Key Vault
echo ""
echo -e "${YELLOW}Storing secret in Key Vault...${NC}"
az keyvault secret set \
    --vault-name "$KEY_VAULT_NAME" \
    --name "COMPOSIO-API-KEY" \
    --value "$COMPOSIO_API_KEY" \
    --output none

echo -e "${GREEN}✓ Secret stored: COMPOSIO-API-KEY${NC}"

# Verify secret
echo ""
echo -e "${YELLOW}Verifying secret...${NC}"
SECRET_ID=$(az keyvault secret show \
    --vault-name "$KEY_VAULT_NAME" \
    --name "COMPOSIO-API-KEY" \
    --query id -o tsv)

echo -e "${GREEN}✓ Secret verified.${NC}"
echo "  Secret URI: ${SECRET_ID}"

# Output summary
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Setup Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Key Vault Details:"
echo "  Name: ${KEY_VAULT_NAME}"
echo "  Resource Group: ${RESOURCE_GROUP}"
echo "  Secret Name: COMPOSIO-API-KEY"
echo "  Secret URI: ${SECRET_ID}"
echo ""
echo "Next Steps:"
echo "  1. Run setup-entra-app.sh to create Entra ID app registration"
echo "  2. Deploy Function App"
echo "  3. Grant Function's Managed Identity access to Key Vault:"
echo ""
echo "     az keyvault set-policy \\"
echo "       --name ${KEY_VAULT_NAME} \\"
echo "       --object-id <FUNCTION_MANAGED_IDENTITY_ID> \\"
echo "       --secret-permissions get"
echo ""
echo "  4. Configure Function App to use Key Vault reference:"
echo ""
echo "     az functionapp config appsettings set \\"
echo "       --name ${FUNCTION_APP_NAME} \\"
echo "       --resource-group ${RESOURCE_GROUP} \\"
echo "       --settings \"COMPOSIO_API_KEY=@Microsoft.KeyVault(SecretUri=${SECRET_ID})\""
echo ""
