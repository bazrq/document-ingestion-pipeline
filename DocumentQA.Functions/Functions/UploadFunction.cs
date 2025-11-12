using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using DocumentQA.Functions.Services;
using DocumentQA.Functions.Models;
using DocumentQA.Functions.Utils;
using System.Net;

namespace DocumentQA.Functions.Functions;

public class UploadFunction
{
    private readonly BlobContainerClient _containerClient;
    private readonly DocumentStatusService _statusService;
    private readonly ILogger<UploadFunction> _logger;

    public UploadFunction(
        BlobContainerClient containerClient,
        DocumentStatusService statusService,
        ILogger<UploadFunction> loggerFactory)
    {
        _containerClient = containerClient;
        _statusService = statusService;
        _logger = loggerFactory;
    }

    [Function("UploadDocument")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "upload")] HttpRequestData req)
    {
        _logger.LogInformation("Processing document upload request");

        try
        {
            // Validate request has body
            if (!req.Body.CanRead)
            {
                _logger.LogWarning("Request body is not readable");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "No file provided" });
                return badResponse;
            }

            // Parse multipart form data
            FormData formData;
            try
            {
                formData = await req.ReadFormDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse multipart form data");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = $"Invalid multipart form data: {ex.Message}" });
                return badResponse;
            }

            var file = formData.Files.FirstOrDefault();

            if (file == null)
            {
                _logger.LogWarning("No file found in request");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "No file in request" });
                return badResponse;
            }

            // Validation: File size (100MB limit)
            if (file.Length > 100 * 1024 * 1024)
            {
                _logger.LogWarning("File too large: {FileSize} bytes", file.Length);
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "File too large. Max size: 100MB" });
                return badResponse;
            }

            // Validation: PDF only
            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid file type: {FileName}", file.FileName);
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Only PDF files are supported" });
                return badResponse;
            }

            // Generate document ID
            var documentId = Guid.NewGuid().ToString();
            var blobPath = $"{documentId}/{file.FileName}";

            _logger.LogInformation("Uploading document {FileName} with ID {DocumentId}", file.FileName, documentId);

            // Upload to blob storage
            var blobClient = _containerClient.GetBlobClient(blobPath);
            await blobClient.UploadAsync(file.OpenReadStream(), overwrite: true);

            _logger.LogInformation("Document uploaded to blob storage: {BlobPath}", blobPath);

            // Create status record
            await _statusService.CreateAsync(documentId, file.FileName, blobPath);

            _logger.LogInformation("Status record created for document {DocumentId}", documentId);

            // Return 202 Accepted with document ID
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new UploadResponse
            {
                DocumentId = documentId,
                FileName = file.FileName,
                Message = "Document uploaded successfully. Processing will begin shortly.",
                StatusEndpoint = $"/api/status/{documentId}"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = $"Internal server error: {ex.Message}" });
            return errorResponse;
        }
    }
}
