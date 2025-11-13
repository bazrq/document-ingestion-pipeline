# Local Development Scripts

This directory contains scripts for local development and testing of the Document QA System.

## Available Scripts

### doc-qa-cli.sh

**Interactive CLI for Document QA System**

A menu-driven command-line interface that provides an intuitive way to interact with the Azure Functions API running locally via Azure Functions.

#### Prerequisites

1. **Azure Functions must be running** with the Functions API accessible at `http://localhost:7071`
   ```bash
   cd DocumentQA.AppHost
   dotnet run
   ```

2. **Required dependencies:**
   - `curl` - HTTP client (pre-installed on macOS)
   - `jq` - JSON parser
     ```bash
     brew install jq
     ```

#### Usage

```bash
./scripts/local/doc-qa-cli.sh
```

The script will:
1. Check if required dependencies are installed
2. Verify the API is reachable at `http://localhost:7071`
3. Display an interactive menu with the following options:

```
========================================
Document QA System - Local CLI
========================================
1. Upload PDF Document
2. Check Document Status
3. List All Documents
4. Query Documents (Ask Question)
5. Check API Health
6. Exit
========================================
```

#### Features

##### 1. Upload PDF Document

- Prompts for file path
- Validates the file:
  - File exists and is readable
  - Has `.pdf` extension
  - Size is under 100 MB
- Uploads to `/api/upload`
- Displays response with document ID
- Saves document ID for quick access in subsequent operations

**Example:**
```
Enter path to PDF file: ~/Documents/test.pdf
Validating file...
✓ File exists
✓ File is PDF
✓ File size: 2 MB (within 100 MB limit)

Uploading...

Response:
{
  "documentId": "abc-123-def",
  "fileName": "test.pdf",
  "message": "Document uploaded successfully. Processing will begin shortly.",
  "statusEndpoint": "/api/status/abc-123-def"
}

Document ID saved for quick access: abc-123-def
```

##### 2. Check Document Status

- Prompts for document ID (offers last uploaded ID as default)
- Option to poll status until complete:
  - **With polling**: Checks status every 3 seconds until "completed" or "failed"
  - **Without polling**: Single status check
- Calls `/api/status/{documentId}`
- Displays current status and processing details

**Example (with polling):**
```
Enter document ID (press Enter for: abc-123-def):
Using: abc-123-def

Poll until complete? (y/n): y

Polling status (checking every 3 seconds, press Ctrl+C to stop)...

Status: processing (14:23:01)
Status: processing (14:23:04)
Status: completed (14:23:07)

Final response:
{
  "documentId": "abc-123-def",
  "fileName": "test.pdf",
  "status": "completed",
  "uploadedAt": "2025-11-13T14:22:58Z",
  "processedAt": "2025-11-13T14:23:06Z",
  "pageCount": 5,
  "chunkCount": 12
}
```

##### 3. List All Documents

- Optional filtering by status (uploaded, processing, completed, failed)
- Optional limit on number of results
- Calls `/api/documents` with query parameters
- Displays all documents with their details

**Example:**
```
Filter by status?
  Options: uploaded, processing, completed, failed, all
Enter status (or press Enter for 'all'): completed

Limit results? (enter number or press Enter for all): 5

Fetching documents...

Response:
{
  "documents": [
    {
      "documentId": "abc-123-def",
      "fileName": "test.pdf",
      "status": "completed",
      "uploadedAt": "2025-11-13T14:22:58Z",
      "processedAt": "2025-11-13T14:23:06Z",
      "pageCount": 5,
      "chunkCount": 12
    },
    ...
  ],
  "totalCount": 5
}
```

##### 4. Query Documents (Ask Question)

- Prompts for question text
- Prompts for document IDs (comma-separated, offers last uploaded ID as default)
- Optional max chunks parameter (defaults to 7)
- Calls `/api/query` with JSON request
- Displays answer with confidence score and citations

**Example:**
```
Enter your question: What is the main topic of the document?

Enter document IDs (comma-separated) (press Enter for: abc-123-def):
Using: abc-123-def

Max chunks to use (press Enter for default 7):

Querying documents...

Response:
{
  "answer": "The main topic of the document is artificial intelligence...",
  "confidence": 0.87,
  "citations": [
    {
      "documentTitle": "test.pdf",
      "pageNumber": 2,
      "excerpt": "Artificial intelligence has become...",
      "sectionTitle": "Introduction"
    }
  ],
  "processingTimeMs": 1234
}
```

##### 5. Check API Health

- Verifies the Functions API is reachable
- Useful for troubleshooting connectivity issues
- Provides guidance if API is not accessible

##### 6. Exit

- Exits the CLI application

#### Error Handling

The script includes comprehensive error handling:

- **Dependency checks**: Verifies `curl` and `jq` are installed before starting
- **Health checks**: Verifies API is reachable before showing menu
- **File validation**: Checks file exists, is readable, is PDF, and under size limit
- **API errors**: Displays clear error messages when API calls fail
- **Invalid inputs**: Handles empty inputs and invalid menu choices gracefully

#### Troubleshooting

**"API is not reachable" error:**
- Ensure Azure Functions is running: `cd DocumentQA.AppHost && dotnet run`
- Check that Functions are listening on port 7071
- Verify no firewall blocking localhost connections

**"Missing required dependencies" error:**
- Install jq: `brew install jq`
- curl should be pre-installed on macOS

**"Upload failed" error:**
- Verify file path is correct and file exists
- Check file is a valid PDF
- Ensure file size is under 100 MB
- Check API logs in Functions console for details

**"Failed to query documents" error:**
- Ensure documents are in "completed" status (use option 2 to check)
- Verify document IDs are correct
- Check that Azure OpenAI and AI Search are properly configured

#### Tips

- **Quick access to last uploaded document**: After uploading, the document ID is saved and offered as a default in subsequent operations (status check, query). Just press Enter to use it.

- **Polling for completion**: When checking status, use polling mode (y) to automatically wait for processing to complete rather than manually checking multiple times.

- **Multiple document queries**: You can query multiple documents at once by entering comma-separated document IDs (e.g., `abc-123,def-456,ghi-789`).

- **Filtering lists**: Use the status filter to quickly find documents in specific states (especially useful to find failed uploads or completed documents).

#### Example Workflow

```bash
# 1. Start Azure Functions
cd DocumentQA.AppHost
dotnet run

# 2. In another terminal, start the CLI
./scripts/local/doc-qa-cli.sh

# 3. Upload a document (option 1)
#    - Enter file path
#    - Note the document ID

# 4. Check status with polling (option 2)
#    - Press Enter to use last document ID
#    - Choose 'y' to poll until complete

# 5. Query the document (option 4)
#    - Enter your question
#    - Press Enter to use last document ID
#    - Review the answer and citations

# 6. List all documents (option 3)
#    - Filter by 'completed' status
#    - Review all processed documents
```

## Other Scripts

### functions/run-application.sh

Convenience script to start the Azure Functions application from the repository root.

**Usage:**
```bash
./scripts/local/functions/run-application.sh
```

## Development

To add new scripts to this directory:

1. Create the script with a `.sh` extension
2. Add a shebang line: `#!/bin/bash`
3. Use `set -euo pipefail` for strict error handling
4. Make it executable: `chmod +x script-name.sh`
5. Document it in this README

## Additional Resources

- **Main README**: See [../../README.md](../../README.md) for project overview
- **Azure Functions Setup**: See [../../docs/ASPIRE_SETUP.md](../../docs/ASPIRE_SETUP.md) for Azure Functions configuration
- **CLAUDE.md**: See [../../CLAUDE.md](../../CLAUDE.md) for comprehensive development guide
