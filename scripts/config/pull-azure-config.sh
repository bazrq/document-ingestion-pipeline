#!/usr/bin/env bash
# pull-azure-config.sh - Pull Azure infrastructure configuration for local development
#
# This script discovers Azure resources (OpenAI, Document Intelligence, AI Search)
# and updates local configuration files for running the Document QA system.
#
# Usage:
#   ./pull-azure-config.sh [options]
#
# Options:
#   --help, -h              Show this help message
#   --no-backup             Skip creating backup of existing config file
#
# Examples:
#   ./pull-azure-config.sh
#   ./pull-azure-config.sh --no-backup

set -euo pipefail

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# Default configuration
CREATE_BACKUP=true
CONFIG_FILE="${REPO_ROOT}/DocumentQA.Functions/local.settings.json"

# Source library modules
# shellcheck source=./lib/validation.sh
source "${SCRIPT_DIR}/lib/validation.sh"
# shellcheck source=./lib/interactive.sh
source "${SCRIPT_DIR}/lib/interactive.sh"
# shellcheck source=./lib/json-utils.sh
source "${SCRIPT_DIR}/lib/json-utils.sh"
# shellcheck source=./lib/azure-openai.sh
source "${SCRIPT_DIR}/lib/azure-openai.sh"

# Display help message
show_help() {
    cat << EOF
pull-azure-config.sh - Pull Azure infrastructure configuration

This script discovers your Azure OpenAI resources and updates the
DocumentQA.Functions/local.settings.json configuration file. It provides
an interactive interface for selecting Azure OpenAI deployments.

USAGE:
    ./pull-azure-config.sh [OPTIONS]

OPTIONS:
    -h, --help              Show this help message
    --no-backup             Skip creating backup of existing config

EXAMPLES:
    # Interactive configuration (default)
    ./pull-azure-config.sh

    # Configure without creating backup
    ./pull-azure-config.sh --no-backup

PREREQUISITES:
    - Azure CLI (az) installed and authenticated
    - jq (JSON processor) installed
    - fzf (fuzzy finder) installed
    - Azure OpenAI account with model deployments

AUTHENTICATION:
    Run 'az login' before executing this script
    Run 'az account set --subscription <id>' to select subscription

MORE INFO:
    See scripts/config/README.md for detailed documentation

EOF
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)
                show_help
                exit 0
                ;;
            --no-backup)
                CREATE_BACKUP=false
                shift
                ;;
            *)
                print_error "Unknown option: $1"
                echo "Run with --help for usage information"
                exit 1
                ;;
        esac
    done
}

# Initialize configuration file if it doesn't exist
initialize_config_file() {
    local file_path="$1"

    if [ ! -f "$file_path" ]; then
        print_warning "Configuration file does not exist: $file_path"

        if confirm "Create new configuration file?" "y"; then
            local default_config
            default_config=$(cat << 'EOF'
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Azure__OpenAI__Endpoint": "",
    "Azure__OpenAI__ApiKey": "",
    "Azure__OpenAI__EmbeddingDeploymentName": "",
    "Azure__OpenAI__ChatDeploymentName": "",
    "Azure__DocumentIntelligence__Endpoint": "",
    "Azure__DocumentIntelligence__ApiKey": "",
    "Azure__AISearch__Endpoint": "",
    "Azure__AISearch__AdminKey": "",
    "Azure__AISearch__IndexName": "document-chunks",
    "Azure__Storage__ConnectionString": "UseDevelopmentStorage=true"
  }
}
EOF
            )

            if echo "$default_config" | jq . > "$file_path" 2>/dev/null; then
                print_success "Created configuration file: $file_path"
            else
                print_error "Failed to create configuration file"
                return 1
            fi
        else
            print_error "Cannot proceed without configuration file"
            return 1
        fi
    fi

    return 0
}

# Configure Azure OpenAI
configure_azure_openai() {
    print_header "Azure OpenAI Configuration"

    # Get OpenAI configuration through interactive workflow
    local openai_config
    if ! openai_config=$(get_openai_configuration); then
        print_error "Failed to get Azure OpenAI configuration"
        return 1
    fi

    # Display summary
    display_config_summary "$openai_config"

    # Confirm before applying
    echo ""
    if ! confirm "Apply this configuration to $CONFIG_FILE?" "y"; then
        print_warning "Configuration cancelled by user"
        return 1
    fi

    # Create backup if requested
    if [ "$CREATE_BACKUP" = true ]; then
        print_section "Creating Backup"
        if ! create_backup "$CONFIG_FILE"; then
            if ! confirm "Backup failed. Continue anyway?" "n"; then
                return 1
            fi
        fi
    fi

    # Apply configuration
    print_section "Applying Configuration"

    local endpoint
    endpoint=$(echo "$openai_config" | jq -r '.endpoint')
    local api_key
    api_key=$(echo "$openai_config" | jq -r '.apiKey')
    local chat_deployment
    chat_deployment=$(echo "$openai_config" | jq -r '.chatDeployment')
    local embedding_deployment
    embedding_deployment=$(echo "$openai_config" | jq -r '.embeddingDeployment')

    # Update Functions local.settings.json
    if update_local_settings_openai "$CONFIG_FILE" "$endpoint" "$api_key" "$embedding_deployment" "$chat_deployment"; then
        print_success "Updated Azure Functions configuration: $CONFIG_FILE"
    else
        print_error "Failed to apply configuration to $CONFIG_FILE"
        return 1
    fi

    echo ""
    print_success "Configuration applied successfully!"
    return 0
}

# Display next steps
show_next_steps() {
    print_header "Next Steps"

    echo "Your configuration has been updated. Here's what to do next:"
    echo ""
    echo "1. Review the configuration:"
    echo "   ${COLOR_BLUE}cat $CONFIG_FILE${COLOR_RESET}"
    echo ""
    echo "2. Start Azure Functions:"
    echo "   ${COLOR_BLUE}cd DocumentQA.Functions && func start${COLOR_RESET}"
    echo ""
    echo "3. Test the APIs:"
    echo "   Upload:  ${COLOR_BLUE}POST http://localhost:7071/api/upload${COLOR_RESET}"
    echo "   Query:   ${COLOR_BLUE}POST http://localhost:7071/api/query${COLOR_RESET}"
    echo ""
    print_info "For troubleshooting, see CLAUDE.md"
    echo ""
}

# Main execution flow
main() {
    # Parse arguments
    parse_args "$@"

    # Display banner
    clear
    print_header "Document QA System - Azure Configuration Tool"
    echo "This tool will help you configure Azure resources for local development."
    echo ""

    # Validate prerequisites
    print_section "Validating Prerequisites"
    if ! validate_all; then
        exit 2
    fi

    # Display current subscription
    local subscription_name
    subscription_name=$(az account show --query name -o tsv)
    print_info "Using subscription: $subscription_name"
    echo ""

    if ! confirm "Continue with this subscription?" "y"; then
        print_info "To change subscription, run: az account set --subscription <id>"
        exit 0
    fi

    # Initialize config file if needed
    if ! initialize_config_file "$CONFIG_FILE"; then
        exit 1
    fi

    # Validate config file
    if ! check_file_writable "$CONFIG_FILE"; then
        exit 1
    fi

    if ! validate_json_file "$CONFIG_FILE"; then
        exit 1
    fi

    # Configure Azure OpenAI
    if ! configure_azure_openai; then
        print_error "Configuration failed"
        exit 1
    fi

    # TODO: Future extensions
    # - configure_document_intelligence
    # - configure_ai_search
    # - configure_storage_account

    # Display next steps
    show_next_steps

    print_success "Configuration complete!"
    exit 0
}

# Handle script errors
trap 'print_error "An unexpected error occurred at line $LINENO. Exit code: $?"' ERR

# Run main function
main "$@"
