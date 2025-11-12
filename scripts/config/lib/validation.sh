#!/usr/bin/env bash
# validation.sh - Validation and dependency checking utilities

# Source guard
[ -n "${_VALIDATION_SH_SOURCED:-}" ] && return 0
readonly _VALIDATION_SH_SOURCED=1

set -euo pipefail

# Colors for output (only declare if not already set)
if [ -z "${COLOR_RED:-}" ]; then
    readonly COLOR_RED='\033[0;31m'
    readonly COLOR_GREEN='\033[0;32m'
    readonly COLOR_YELLOW='\033[1;33m'
    readonly COLOR_BLUE='\033[0;34m'
    readonly COLOR_RESET='\033[0m'
fi

# Check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Print colored message
print_error() {
    echo -e "${COLOR_RED}✗ Error: $1${COLOR_RESET}" >&2
}

print_success() {
    echo -e "${COLOR_GREEN}✓ $1${COLOR_RESET}" >&2
}

print_warning() {
    echo -e "${COLOR_YELLOW}⚠ Warning: $1${COLOR_RESET}" >&2
}

print_info() {
    echo -e "${COLOR_BLUE}ℹ $1${COLOR_RESET}" >&2
}

# Check if all required dependencies are installed
check_dependencies() {
    local missing_deps=()

    print_info "Checking required dependencies..."

    if ! command_exists az; then
        missing_deps+=("azure-cli")
    fi

    if ! command_exists jq; then
        missing_deps+=("jq")
    fi

    if ! command_exists fzf; then
        missing_deps+=("fzf")
    fi

    if [ ${#missing_deps[@]} -gt 0 ]; then
        print_error "Missing required dependencies: ${missing_deps[*]}"
        echo ""
        echo "Please install the missing dependencies:"
        echo ""

        for dep in "${missing_deps[@]}"; do
            case "$dep" in
                azure-cli)
                    echo "  Azure CLI:"
                    echo "    macOS:   brew install azure-cli"
                    echo "    Linux:   curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash"
                    echo ""
                    ;;
                jq)
                    echo "  jq (JSON processor):"
                    echo "    macOS:   brew install jq"
                    echo "    Linux:   sudo apt-get install jq"
                    echo ""
                    ;;
                fzf)
                    echo "  fzf (fuzzy finder):"
                    echo "    macOS:   brew install fzf"
                    echo "    Linux:   sudo apt-get install fzf"
                    echo "    Or:      git clone --depth 1 https://github.com/junegunn/fzf.git ~/.fzf && ~/.fzf/install"
                    echo ""
                    ;;
            esac
        done

        return 1
    fi

    print_success "All dependencies are installed"
    return 0
}

# Check if user is authenticated with Azure CLI
check_azure_auth() {
    print_info "Checking Azure CLI authentication..."

    if ! az account show >/dev/null 2>&1; then
        print_error "Not authenticated with Azure CLI"
        echo ""
        echo "Please authenticate with Azure CLI:"
        echo "  az login"
        echo ""
        echo "Or for service principal authentication:"
        echo "  az login --service-principal -u <app-id> -p <password-or-cert> --tenant <tenant-id>"
        echo ""
        return 1
    fi

    local account_name
    account_name=$(az account show --query name -o tsv 2>/dev/null)
    local subscription_id
    subscription_id=$(az account show --query id -o tsv 2>/dev/null)

    print_success "Authenticated as: $account_name"
    print_info "Using subscription: $subscription_id"
    return 0
}

# Validate that a file path is writable
check_file_writable() {
    local file_path="$1"
    local dir_path
    dir_path=$(dirname "$file_path")

    if [ ! -d "$dir_path" ]; then
        print_error "Directory does not exist: $dir_path"
        return 1
    fi

    if [ -f "$file_path" ] && [ ! -w "$file_path" ]; then
        print_error "File is not writable: $file_path"
        return 1
    fi

    if [ ! -w "$dir_path" ]; then
        print_error "Directory is not writable: $dir_path"
        return 1
    fi

    return 0
}

# Validate JSON structure
validate_json_file() {
    local file_path="$1"

    if [ ! -f "$file_path" ]; then
        print_error "File does not exist: $file_path"
        return 1
    fi

    if ! jq empty "$file_path" 2>/dev/null; then
        print_error "Invalid JSON in file: $file_path"
        return 1
    fi

    return 0
}

# Validate URL format
validate_url() {
    local url="$1"

    if [[ ! "$url" =~ ^https?:// ]]; then
        print_error "Invalid URL format (must start with http:// or https://): $url"
        return 1
    fi

    return 0
}

# Create a timestamped backup of a file
create_backup() {
    local file_path="$1"

    if [ ! -f "$file_path" ]; then
        print_warning "No existing file to backup: $file_path"
        return 0
    fi

    local timestamp
    timestamp=$(date +"%Y%m%d-%H%M%S")
    local backup_path="${file_path}.bak.${timestamp}"

    if cp "$file_path" "$backup_path"; then
        print_success "Created backup: $backup_path"
        echo "$backup_path"
        return 0
    else
        print_error "Failed to create backup: $backup_path"
        return 1
    fi
}

# Validate all prerequisites
validate_all() {
    local errors=0

    if ! check_dependencies; then
        ((errors++))
    fi

    if ! check_azure_auth; then
        ((errors++))
    fi

    if [ $errors -gt 0 ]; then
        print_error "Validation failed with $errors error(s)"
        return 1
    fi

    print_success "All validations passed"
    return 0
}
