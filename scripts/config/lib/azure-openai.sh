#!/usr/bin/env bash
# azure-openai.sh - Azure OpenAI resource discovery and configuration retrieval

# Source guard
[ -n "${_AZURE_OPENAI_SH_SOURCED:-}" ] && return 0
readonly _AZURE_OPENAI_SH_SOURCED=1

set -euo pipefail

# Source required libraries
_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./validation.sh
source "${_LIB_DIR}/validation.sh"
# shellcheck source=./interactive.sh
source "${_LIB_DIR}/interactive.sh"

# List all Azure OpenAI accounts in the current subscription
# Returns: JSON array of OpenAI accounts
list_openai_accounts() {
    print_info "Discovering Azure OpenAI accounts..."

    local accounts
    if ! accounts=$(az cognitiveservices account list \
        --query "[?kind=='OpenAI'].{name:name, location:location, resourceGroup:resourceGroup, endpoint:properties.endpoint}" \
        -o json 2>/dev/null); then
        print_error "Failed to list Azure OpenAI accounts"
        return 1
    fi

    local count
    count=$(echo "$accounts" | jq 'length')

    if [ "$count" -eq 0 ]; then
        print_warning "No Azure OpenAI accounts found in current subscription"
        echo ""
        echo "Please create an Azure OpenAI account first:"
        echo "  https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAI"
        return 1
    fi

    print_success "Found $count Azure OpenAI account(s)"
    echo "$accounts"
    return 0
}

# Interactive selection of an Azure OpenAI account
# Returns: JSON object with selected account details
select_openai_account() {
    local accounts="$1"

    print_section "Select Azure OpenAI Account"

    local count
    count=$(echo "$accounts" | jq 'length' 2>/dev/null)

    # If only one account, auto-select it
    if [ "$count" -eq 1 ]; then
        local account_name
        account_name=$(echo "$accounts" | jq -r '.[0].name' 2>/dev/null)
        print_info "Auto-selecting only available account: $account_name"
        echo "$accounts" | jq '.[0]' 2>/dev/null
        return 0
    fi

    # Format accounts for fzf display
    local formatted_list
    formatted_list=$(echo "$accounts" | jq -r '.[] | "\(.name)\t\(.location)\t\(.resourceGroup)"' 2>/dev/null)

    if [ -z "$formatted_list" ]; then
        print_error "Failed to format accounts list"
        return 1
    fi

    # Write accounts to temp file for preview command
    local temp_file
    temp_file=$(mktemp)
    echo "$accounts" > "$temp_file"

    # Create preview command to show account details
    local preview_cmd="echo {} | awk '{print \$1}' | xargs -I ACCT jq '.[] | select(.name == \"ACCT\")' $temp_file"

    local selection
    if ! selection=$(fzf_select "Select Azure OpenAI Account" "$formatted_list" "$preview_cmd"); then
        rm -f "$temp_file"
        return 1
    fi

    # Extract account name from selection
    local account_name
    account_name=$(echo "$selection" | awk '{print $1}')

    # Get full account details
    local account_details
    account_details=$(echo "$accounts" | jq ".[] | select(.name == \"$account_name\")" 2>/dev/null)

    # Clean up temp file
    rm -f "$temp_file"

    echo "$account_details"
    return 0
}

# List all model deployments for an Azure OpenAI account
# Usage: list_model_deployments "account_name" "resource_group"
list_model_deployments() {
    local account_name="$1"
    local resource_group="$2"

    print_info "Fetching model deployments for $account_name..."

    local deployments
    if ! deployments=$(az cognitiveservices account deployment list \
        --name "$account_name" \
        --resource-group "$resource_group" \
        --query "[].{name:name, model:properties.model.name, version:properties.model.version, capacity:sku.capacity}" \
        -o json 2>/dev/null); then
        print_error "Failed to list model deployments"
        return 1
    fi

    local count
    count=$(echo "$deployments" | jq 'length')

    if [ "$count" -eq 0 ]; then
        print_warning "No model deployments found for $account_name"
        echo ""
        echo "Please deploy models in Azure AI Studio:"
        echo "  https://oai.azure.com/"
        return 1
    fi

    print_success "Found $count model deployment(s)"
    echo "$deployments"
    return 0
}

# Filter deployments by model type (chat or embedding)
# Usage: filter_deployments_by_type "$deployments" "chat"
filter_deployments_by_type() {
    local deployments="$1"
    local model_type="$2" # "chat" or "embedding"

    case "$model_type" in
        chat)
            # Filter for chat/GPT models
            echo "$deployments" | jq '[.[] | select(.model | test("gpt|GPT"; "i"))]'
            ;;
        embedding)
            # Filter for embedding models
            echo "$deployments" | jq '[.[] | select(.model | test("embedding|ada"; "i"))]'
            ;;
        *)
            print_error "Invalid model type: $model_type (must be 'chat' or 'embedding')"
            return 1
            ;;
    esac
}

# Interactive selection of a chat model deployment
# Usage: select_chat_deployment "$deployments"
select_chat_deployment() {
    local deployments="$1"

    print_section "Select Chat Model Deployment"

    # Filter for chat models
    local chat_deployments
    chat_deployments=$(filter_deployments_by_type "$deployments" "chat")

    local count
    count=$(echo "$chat_deployments" | jq 'length' 2>/dev/null)

    if [ "$count" -eq 0 ]; then
        print_error "No chat model deployments found"
        echo ""
        echo "Expected models: gpt-4, gpt-4o, gpt-5-mini, etc."
        echo "Please deploy a chat model in Azure AI Studio."
        return 1
    fi

    # If only one deployment, auto-select it
    if [ "$count" -eq 1 ]; then
        local deployment_name
        deployment_name=$(echo "$chat_deployments" | jq -r '.[0].name' 2>/dev/null)
        print_info "Auto-selecting only available chat model: $deployment_name"
        echo "$deployment_name"
        return 0
    fi

    # Format for fzf display
    local formatted_list
    formatted_list=$(echo "$chat_deployments" | jq -r '.[] | "\(.name)\t\(.model)\t\(.version)\t\(.capacity) TPM"' 2>/dev/null)

    # Write deployments to temp file for preview command
    local temp_file
    temp_file=$(mktemp)
    echo "$chat_deployments" > "$temp_file"

    # Create preview command
    local preview_cmd="echo {} | awk '{print \$1}' | xargs -I DEPLOY jq '.[] | select(.name == \"DEPLOY\")' $temp_file"

    local selection
    if ! selection=$(fzf_select "Select Chat Model" "$formatted_list" "$preview_cmd"); then
        rm -f "$temp_file"
        return 1
    fi

    # Extract deployment name
    local deployment_name
    deployment_name=$(echo "$selection" | awk '{print $1}')

    # Clean up temp file
    rm -f "$temp_file"

    echo "$deployment_name"
    return 0
}

# Interactive selection of an embedding model deployment
# Usage: select_embedding_deployment "$deployments"
select_embedding_deployment() {
    local deployments="$1"

    print_section "Select Embedding Model Deployment"

    # Filter for embedding models
    local embedding_deployments
    embedding_deployments=$(filter_deployments_by_type "$deployments" "embedding")

    local count
    count=$(echo "$embedding_deployments" | jq 'length' 2>/dev/null)

    if [ "$count" -eq 0 ]; then
        print_error "No embedding model deployments found"
        echo ""
        echo "Expected models: text-embedding-3-large, text-embedding-3-small, text-embedding-ada-002, etc."
        echo "Please deploy an embedding model in Azure AI Studio."
        return 1
    fi

    # If only one deployment, auto-select it
    if [ "$count" -eq 1 ]; then
        local deployment_name
        deployment_name=$(echo "$embedding_deployments" | jq -r '.[0].name' 2>/dev/null)
        print_info "Auto-selecting only available embedding model: $deployment_name"
        echo "$deployment_name"
        return 0
    fi

    # Format for fzf display
    local formatted_list
    formatted_list=$(echo "$embedding_deployments" | jq -r '.[] | "\(.name)\t\(.model)\t\(.version)\t\(.capacity) TPM"' 2>/dev/null)

    # Write deployments to temp file for preview command
    local temp_file
    temp_file=$(mktemp)
    echo "$embedding_deployments" > "$temp_file"

    # Create preview command
    local preview_cmd="echo {} | awk '{print \$1}' | xargs -I DEPLOY jq '.[] | select(.name == \"DEPLOY\")' $temp_file"

    local selection
    if ! selection=$(fzf_select "Select Embedding Model" "$formatted_list" "$preview_cmd"); then
        rm -f "$temp_file"
        return 1
    fi

    # Extract deployment name
    local deployment_name
    deployment_name=$(echo "$selection" | awk '{print $1}')

    # Clean up temp file
    rm -f "$temp_file"

    echo "$deployment_name"
    return 0
}

# Get API keys for an Azure OpenAI account
# Usage: get_openai_api_key "account_name" "resource_group"
get_openai_api_key() {
    local account_name="$1"
    local resource_group="$2"

    print_info "Retrieving API keys for $account_name..."

    local keys
    if ! keys=$(az cognitiveservices account keys list \
        --name "$account_name" \
        --resource-group "$resource_group" \
        --query "key1" \
        -o tsv 2>/dev/null); then
        print_error "Failed to retrieve API keys"
        return 1
    fi

    if [ -z "$keys" ]; then
        print_error "No API keys found"
        return 1
    fi

    print_success "Retrieved API key"
    echo "$keys"
    return 0
}

# Get endpoint URL for an Azure OpenAI account
# Usage: get_openai_endpoint "account_name" "resource_group"
get_openai_endpoint() {
    local account_name="$1"
    local resource_group="$2"

    print_info "Retrieving endpoint for $account_name..."

    local endpoint
    if ! endpoint=$(az cognitiveservices account show \
        --name "$account_name" \
        --resource-group "$resource_group" \
        --query "properties.endpoint" \
        -o tsv 2>/dev/null); then
        print_error "Failed to retrieve endpoint"
        return 1
    fi

    if [ -z "$endpoint" ]; then
        print_error "No endpoint found"
        return 1
    fi

    print_success "Retrieved endpoint: $endpoint"
    echo "$endpoint"
    return 0
}

# Complete workflow to get Azure OpenAI configuration
# Returns: JSON object with all configuration values
get_openai_configuration() {
    print_header "Azure OpenAI Configuration Discovery"

    # List and select account
    local accounts
    if ! accounts=$(list_openai_accounts); then
        return 1
    fi

    local account_details
    if ! account_details=$(select_openai_account "$accounts"); then
        return 1
    fi

    local account_name
    account_name=$(echo "$account_details" | jq -r '.name')
    local resource_group
    resource_group=$(echo "$account_details" | jq -r '.resourceGroup')
    local endpoint
    endpoint=$(echo "$account_details" | jq -r '.endpoint')

    # List deployments
    local deployments
    if ! deployments=$(list_model_deployments "$account_name" "$resource_group"); then
        return 1
    fi

    # Select chat model
    local chat_deployment
    if ! chat_deployment=$(select_chat_deployment "$deployments"); then
        return 1
    fi

    # Select embedding model
    local embedding_deployment
    if ! embedding_deployment=$(select_embedding_deployment "$deployments"); then
        return 1
    fi

    # Get API key
    local api_key
    if ! api_key=$(get_openai_api_key "$account_name" "$resource_group"); then
        return 1
    fi

    # Build configuration JSON
    local config
    config=$(jq -n \
        --arg endpoint "$endpoint" \
        --arg api_key "$api_key" \
        --arg chat_deployment "$chat_deployment" \
        --arg embedding_deployment "$embedding_deployment" \
        --arg account_name "$account_name" \
        --arg resource_group "$resource_group" \
        '{
            accountName: $account_name,
            resourceGroup: $resource_group,
            endpoint: $endpoint,
            apiKey: $api_key,
            chatDeployment: $chat_deployment,
            embeddingDeployment: $embedding_deployment
        }')

    echo "$config"
    return 0
}

# Display configuration summary
# Usage: display_config_summary "$config_json"
display_config_summary() {
    local config="$1"

    local account_name
    account_name=$(echo "$config" | jq -r '.accountName')
    local endpoint
    endpoint=$(echo "$config" | jq -r '.endpoint')
    local chat_deployment
    chat_deployment=$(echo "$config" | jq -r '.chatDeployment')
    local embedding_deployment
    embedding_deployment=$(echo "$config" | jq -r '.embeddingDeployment')
    local api_key_preview
    api_key_preview=$(echo "$config" | jq -r '.apiKey' | sed 's/\(.\{8\}\).*/\1.../')

    print_summary "Selected Configuration" \
        "Account:$account_name" \
        "Endpoint:$endpoint" \
        "Chat Model:$chat_deployment" \
        "Embedding Model:$embedding_deployment" \
        "API Key:$api_key_preview"
}
