# Azure Configuration Scripts

This directory contains scripts for managing Azure infrastructure configuration for local development.

## Scripts

### 1. `pull-azure-config.sh` - Interactive Configuration Discovery

The `pull-azure-config.sh` script provides an **interactive, extensible** way to discover Azure resources and update local configuration files. It eliminates manual copy-pasting of endpoints, API keys, and deployment names.

### 2. `get-config.sh` - Retrieve Deployed Infrastructure Configuration

The `get-config.sh` script retrieves configuration values from your deployed Azure resources. Use this to pull the latest configuration after infrastructure deployment or to verify current settings.

**Usage:**
```bash
# Retrieve config from default resource group (rg-local-dev)
./scripts/config/get-config.sh

# Retrieve config from specific resource group
./scripts/config/get-config.sh rg-production
```

**Output:**
- Document Intelligence endpoint and API key
- AI Search endpoint, admin key, and index name
- Formatted JSON for `appsettings.Development.json`
- Environment variable export commands

**When to use:**
- After running `infra/deploy-local-dev.sh`
- When you need to retrieve credentials from deployed resources
- To verify current infrastructure configuration
- To regenerate configuration after key rotation

## Quick Reference

| Script | Purpose | Prerequisites | Interactive |
|--------|---------|---------------|-------------|
| `get-config.sh` | Retrieve config from deployed Azure resources | Azure CLI, jq | No |
| `pull-azure-config.sh` | Interactive Azure OpenAI discovery and configuration | Azure CLI, jq, fzf | Yes |

**Typical workflow:**
1. Deploy infrastructure: `infra/deploy-local-dev.sh`
2. Retrieve deployed config: `./scripts/config/get-config.sh`
3. Configure Azure OpenAI: `./scripts/config/pull-azure-config.sh`
4. Start Aspire: `cd DocumentQA.AppHost && dotnet run`

## Overview

### Current Features

- ✅ Azure OpenAI account discovery
- ✅ Interactive model selection (chat and embedding models) using fzf
- ✅ Automatic configuration of `appsettings.Development.json`
- ✅ Timestamped backups before modifications
- ✅ Comprehensive validation and error handling

### Future Extensions (Planned)

- ⏳ Document Intelligence resource discovery
- ⏳ AI Search resource discovery
- ⏳ Storage Account configuration
- ⏳ Support for `local.settings.json` (standalone Functions)
- ⏳ Support for `.env` file generation (Azure Developer CLI)
- ⏳ Multi-environment support (dev/staging/prod)

## Prerequisites

### Required Tools

1. **Azure CLI** - For querying Azure resources
   ```bash
   # macOS
   brew install azure-cli

   # Linux
   curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
   ```

2. **jq** - JSON processor for config manipulation
   ```bash
   # macOS
   brew install jq

   # Linux
   sudo apt-get install jq
   ```

3. **fzf** - Fuzzy finder for interactive selection
   ```bash
   # macOS
   brew install fzf

   # Linux
   sudo apt-get install fzf

   # Or install from source
   git clone --depth 1 https://github.com/junegunn/fzf.git ~/.fzf
   ~/.fzf/install
   ```

### Azure Resources

You need an **Azure OpenAI account** with deployed models:
- A **chat model** (e.g., gpt-4, gpt-4o, gpt-5-mini)
- An **embedding model** (e.g., text-embedding-3-large, text-embedding-3-small)

Create deployments at: https://oai.azure.com/

### Authentication

Authenticate with Azure CLI before running the script:

```bash
# Interactive login
az login

# Or service principal login
az login --service-principal \
  -u <app-id> \
  -p <password-or-cert> \
  --tenant <tenant-id>

# Select subscription (if you have multiple)
az account set --subscription <subscription-id>

# Verify authentication
az account show
```

## Usage

### Basic Usage

Run the script from anywhere in the repository:

```bash
./scripts/config/pull-azure-config.sh
```

This will:
1. Check prerequisites (Azure CLI, jq, fzf)
2. Verify Azure authentication
3. List Azure OpenAI accounts (interactive selection)
4. List model deployments (interactive selection for chat + embedding)
5. Retrieve API keys automatically
6. Create a timestamped backup of existing config
7. Update `DocumentQA.AppHost/appsettings.Development.json`
8. Display next steps

### Command Line Options

```bash
# Show help
./scripts/config/pull-azure-config.sh --help

# Skip backup creation (use with caution)
./scripts/config/pull-azure-config.sh --no-backup

# Output to custom file
./scripts/config/pull-azure-config.sh --output /path/to/config.json
```

### Interactive Workflow

The script provides a beautiful, user-friendly interface:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Azure OpenAI Configuration Discovery
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✓ All dependencies are installed
✓ Authenticated as: Your Azure Account
ℹ Using subscription: Your Subscription Name

▸ Select Azure OpenAI Account
> my-openai-account    eastus    my-resource-group
  another-account      westus2   another-rg

▸ Select Chat Model Deployment
> gpt-5-mini       gpt-5-mini    2024-07-01    100000 TPM
  gpt-4o          gpt-4o        2024-05-13    80000 TPM

▸ Select Embedding Model Deployment
> text-embedding-3-large    text-embedding-3-large    1    1000000 TPM
  text-embedding-3-small    text-embedding-3-small    1    1000000 TPM

┌─────────────────────────────────────────────────────────────────┐
│  Selected Configuration                                          │
├─────────────────────────────────────────────────────────────────┤
│  Account                       my-openai-account                │
│  Endpoint                      https://...openai.azure.com/     │
│  Chat Model                    gpt-5-mini                       │
│  Embedding Model               text-embedding-3-large           │
│  API Key                       sk-abc123...                     │
└─────────────────────────────────────────────────────────────────┘

? Apply this configuration to appsettings.Development.json? [Y/n]:
```

## Configuration Files

### Target: appsettings.Development.json

Default location: `DocumentQA.AppHost/appsettings.Development.json`

This file is used by .NET Aspire for local development orchestration.

**Before:**
```json
{
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://placeholder.openai.azure.com/",
      "ApiKey": "placeholder-key",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "ChatDeploymentName": "gpt-5-mini"
    }
  }
}
```

**After:**
```json
{
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://my-openai.openai.azure.com/",
      "ApiKey": "sk-abc123...",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "ChatDeploymentName": "gpt-5-mini"
    }
  }
}
```

### Backups

Backups are created automatically with timestamp:
```
appsettings.Development.json.bak.20251113-143022
```

To restore a backup:
```bash
cp appsettings.Development.json.bak.20251113-143022 appsettings.Development.json
```

## Architecture

### Modular Design

The script system is designed for extensibility:

```
scripts/config/
├── pull-azure-config.sh           # Main orchestrator
├── lib/
│   ├── validation.sh              # Dependency & auth checks
│   ├── interactive.sh             # fzf wrapper functions
│   ├── json-utils.sh              # Safe JSON manipulation
│   └── azure-openai.sh            # OpenAI resource discovery
└── README.md                       # This file
```

### Adding New Services

To add support for a new Azure service (e.g., Document Intelligence):

1. **Create library module**: `lib/azure-doc-intelligence.sh`

```bash
#!/usr/bin/env bash
source "$(dirname "${BASH_SOURCE[0]}")/validation.sh"
source "$(dirname "${BASH_SOURCE[0]}")/interactive.sh"

list_doc_intelligence_accounts() {
    az cognitiveservices account list \
        --query "[?kind=='FormRecognizer']" -o json
}

select_doc_intelligence_account() {
    # Interactive selection using fzf_select
}

get_doc_intelligence_configuration() {
    # Complete workflow
}
```

2. **Source in main script**: `pull-azure-config.sh`

```bash
source "${SCRIPT_DIR}/lib/azure-doc-intelligence.sh"
```

3. **Add configuration function**:

```bash
configure_document_intelligence() {
    local output_file="$1"
    local config
    config=$(get_doc_intelligence_configuration)

    # Update config file
    json_update_value "$output_file" \
        ".Azure.DocumentIntelligence.Endpoint" \
        "$(echo "$config" | jq -r '.endpoint')"
}
```

4. **Call in main workflow**:

```bash
main() {
    # ... existing code ...
    configure_azure_openai "$OUTPUT_FILE"
    configure_document_intelligence "$OUTPUT_FILE"  # Add this
}
```

### Library Functions

#### validation.sh

- `check_dependencies()` - Verify required tools (az, jq, fzf)
- `check_azure_auth()` - Verify Azure CLI authentication
- `check_file_writable()` - Validate file permissions
- `validate_json_file()` - Validate JSON structure
- `create_backup()` - Create timestamped backup
- `print_error/success/warning/info()` - Colored output

#### interactive.sh

- `fzf_select()` - Interactive single selection
- `fzf_multiselect()` - Interactive multi-selection
- `confirm()` - Yes/no confirmation prompt
- `print_header/section()` - Formatted headers
- `print_summary()` - Display configuration summary
- `with_loading()` - Show loading indicator

#### json-utils.sh

- `json_update_value()` - Update single JSON value
- `json_update_multiple()` - Update multiple values atomically
- `json_read_value()` - Read JSON value
- `update_appsettings_openai()` - Update OpenAI config section
- `json_validate_schema()` - Validate JSON structure

#### azure-openai.sh

- `list_openai_accounts()` - List Azure OpenAI accounts
- `select_openai_account()` - Interactive account selection
- `list_model_deployments()` - List model deployments
- `select_chat_deployment()` - Select chat model
- `select_embedding_deployment()` - Select embedding model
- `get_openai_api_key()` - Retrieve API keys
- `get_openai_configuration()` - Complete workflow

## Troubleshooting

### Common Issues

#### 1. "azure-cli not found"

**Solution**: Install Azure CLI
```bash
# macOS
brew install azure-cli

# Linux
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

#### 2. "Not authenticated with Azure CLI"

**Solution**: Run `az login`
```bash
az login
az account show  # Verify authentication
```

#### 3. "No Azure OpenAI accounts found"

**Cause**: No OpenAI resources in current subscription

**Solution**:
- Create Azure OpenAI resource: https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAI
- Or switch to correct subscription: `az account set --subscription <id>`

#### 4. "No chat model deployments found"

**Cause**: No models deployed in selected OpenAI account

**Solution**: Deploy models in Azure AI Studio: https://oai.azure.com/

#### 5. "fzf: command not found"

**Solution**: Install fzf
```bash
# macOS
brew install fzf

# Linux
sudo apt-get install fzf

# From source
git clone --depth 1 https://github.com/junegunn/fzf.git ~/.fzf
~/.fzf/install
```

#### 6. "Failed to update JSON"

**Cause**: Invalid JSON or missing jq

**Solution**:
- Verify JSON is valid: `jq . appsettings.Development.json`
- Install jq: `brew install jq` (macOS) or `sudo apt-get install jq` (Linux)
- Restore from backup if corrupted

### Debug Mode

For verbose output, modify the script:

```bash
# Add at top of pull-azure-config.sh
set -x  # Enable debug mode
```

### Getting Help

If you encounter issues:

1. Check prerequisites are installed
2. Verify Azure authentication: `az account show`
3. Check Azure resources exist: `az cognitiveservices account list`
4. Review error messages carefully
5. Check backups if config was corrupted
6. See main project documentation: `docs/ASPIRE_SETUP.md`

## Examples

### Example 1: First-Time Setup with Infrastructure Deployment

```bash
# Install prerequisites
brew install azure-cli jq fzf

# Authenticate
az login
az account set --subscription "My Subscription"

# Deploy infrastructure (Document Intelligence + AI Search)
cd infra
./deploy-local-dev.sh

# Retrieve deployed configuration
cd ..
./scripts/config/get-config.sh

# Configure Azure OpenAI interactively
./scripts/config/pull-azure-config.sh

# Start application
cd DocumentQA.AppHost
dotnet run
```

### Example 2: Quick Setup (Interactive Only)

```bash
# Install prerequisites
brew install azure-cli jq fzf

# Authenticate
az login
az account set --subscription "My Subscription"

# Run configuration script
./scripts/config/pull-azure-config.sh

# Start application
cd DocumentQA.AppHost
dotnet run
```

### Example 3: Retrieve Configuration After Deployment

After deploying infrastructure, retrieve the configuration:

```bash
# Deploy infrastructure
cd infra
./deploy-local-dev.sh

# Retrieve configuration from deployed resources
cd ..
./scripts/config/get-config.sh

# Copy the output JSON and manually update appsettings.Development.json
# Or use the environment variable exports for other purposes
```

### Example 4: Update Model Deployments

If you've deployed new models and want to update configuration:

```bash
# Re-run the script
./scripts/config/pull-azure-config.sh

# Select new models when prompted
# Backup will be created automatically
```

### Example 5: Multiple Environments

Configure different environments:

```bash
# Development - retrieve from dev resource group
./scripts/config/get-config.sh rg-local-dev

# Staging - retrieve from staging resource group
./scripts/config/get-config.sh rg-staging

# Production - retrieve from prod resource group
./scripts/config/get-config.sh rg-production

# Or use pull-azure-config.sh for OpenAI configuration
./scripts/config/pull-azure-config.sh \
  --output DocumentQA.AppHost/appsettings.Development.json

./scripts/config/pull-azure-config.sh \
  --output DocumentQA.AppHost/appsettings.Staging.json
```

### Example 6: CI/CD Integration

Use in automated pipelines with service principal:

```bash
# Authenticate with service principal
az login --service-principal \
  -u $AZURE_CLIENT_ID \
  -p $AZURE_CLIENT_SECRET \
  --tenant $AZURE_TENANT_ID

# Note: fzf requires interactive terminal
# For CI/CD, consider using environment variables directly
# or implementing a non-interactive mode
```

## Best Practices

1. **Always review configuration** after running the script
2. **Keep backups** - don't use `--no-backup` unless necessary
3. **Use version control** - commit working configurations
4. **Rotate API keys regularly** - re-run `get-config.sh` or `pull-azure-config.sh` after rotation
5. **Document custom deployments** - if using non-standard model names
6. **Test after configuration** - verify endpoints are accessible
7. **Use get-config.sh after infrastructure changes** - automatically pulls latest values from deployed resources

## Security Considerations

- **appsettings.Development.json is gitignored** - never commit secrets
- **API keys are masked** in summary output (shows first 8 chars only)
- **Backups contain secrets** - handle carefully
- **Use Azure Key Vault** for production deployments
- **Rotate keys regularly** via Azure Portal

## Next Steps

After configuring:

1. **Verify configuration**: Review `appsettings.Development.json`
2. **Start Aspire**: `cd DocumentQA.AppHost && dotnet run`
3. **Test endpoints**: Upload PDF and query via API
4. **Monitor**: Use Aspire Dashboard for logs and traces
5. **Extend**: Add support for Document Intelligence or AI Search

## Related Documentation

- [ASPIRE_SETUP.md](../../docs/ASPIRE_SETUP.md) - Aspire setup guide
- [CLAUDE.md](../../CLAUDE.md) - Development guide
- [README.md](../../README.md) - Project overview
- [DocumentQA.AppHost/README.md](../../DocumentQA.AppHost/README.md) - AppHost details

## Contributing

To add support for new Azure services:

1. Create library module in `lib/azure-<service>.sh`
2. Follow existing patterns (validation, interactive selection, etc.)
3. Add comprehensive error handling
4. Update main script to call new functions
5. Document in this README
6. Test thoroughly with multiple accounts/subscriptions

## License

This script is part of the Document QA System project (MIT License).
