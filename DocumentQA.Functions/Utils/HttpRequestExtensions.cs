using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using System.Text;

namespace DocumentQA.Functions.Utils;

public static class HttpRequestExtensions
{
    public static async Task<FormData> ReadFormDataAsync(this HttpRequestData request)
    {
        var contentType = request.Headers.TryGetValues("Content-Type", out var values)
            ? values.FirstOrDefault()
            : null;

        if (contentType == null || !contentType.Contains("multipart/form-data"))
        {
            throw new InvalidOperationException("Request must be multipart/form-data");
        }

        // Extract boundary from content type
        var boundary = ExtractBoundary(contentType);
        if (string.IsNullOrEmpty(boundary))
        {
            throw new InvalidOperationException("No boundary found in multipart content");
        }

        var formData = new FormData();
        await ParseMultipartAsync(request.Body, boundary, formData);

        return formData;
    }

    private static string ExtractBoundary(string contentType)
    {
        var elements = contentType.Split(';');
        var boundaryElement = elements.FirstOrDefault(e => e.Trim().StartsWith("boundary="));

        if (boundaryElement == null)
            return string.Empty;

        var boundary = boundaryElement.Split('=')[1].Trim();
        // Remove quotes if present
        return boundary.Trim('"');
    }

    private static async Task ParseMultipartAsync(Stream stream, string boundary, FormData formData)
    {
        var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
        var reader = new StreamReader(stream);

        // Read until we hit the first boundary
        var line = await reader.ReadLineAsync();
        while (line != null && !line.Contains(boundary))
        {
            line = await reader.ReadLineAsync();
        }

        while (line != null && !line.Contains(boundary + "--"))
        {
            // Read headers for this part
            var headers = new Dictionary<string, string>();
            line = await reader.ReadLineAsync();

            while (!string.IsNullOrWhiteSpace(line))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var headerName = line.Substring(0, colonIndex).Trim();
                    var headerValue = line.Substring(colonIndex + 1).Trim();
                    headers[headerName] = headerValue;
                }
                line = await reader.ReadLineAsync();
            }

            // Parse Content-Disposition header
            if (headers.TryGetValue("Content-Disposition", out var contentDisposition))
            {
                var name = ExtractDispositionValue(contentDisposition, "name");
                var filename = ExtractDispositionValue(contentDisposition, "filename");

                if (!string.IsNullOrEmpty(filename))
                {
                    // This is a file upload
                    var contentType = headers.TryGetValue("Content-Type", out var ct) ? ct : "application/octet-stream";

                    // Read file content until next boundary
                    var fileContent = new MemoryStream();
                    var buffer = new byte[4096];
                    var nextLine = await reader.ReadLineAsync();

                    // Read content until we hit the next boundary
                    while (nextLine != null && !nextLine.Contains(boundary))
                    {
                        var lineBytes = Encoding.UTF8.GetBytes(nextLine + "\r\n");
                        await fileContent.WriteAsync(lineBytes, 0, lineBytes.Length);
                        nextLine = await reader.ReadLineAsync();
                    }

                    // Remove trailing CRLF
                    if (fileContent.Length > 2)
                    {
                        fileContent.SetLength(fileContent.Length - 2);
                    }

                    fileContent.Position = 0;
                    formData.Files.Add(new FormFile(name, filename, contentType, fileContent));

                    line = nextLine;
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    // This is a form field
                    var value = new StringBuilder();
                    var nextLine = await reader.ReadLineAsync();

                    while (nextLine != null && !nextLine.Contains(boundary))
                    {
                        if (value.Length > 0)
                            value.AppendLine();
                        value.Append(nextLine);
                        nextLine = await reader.ReadLineAsync();
                    }

                    formData.Fields[name] = value.ToString().TrimEnd();
                    line = nextLine;
                }
            }
            else
            {
                // Skip to next boundary
                line = await reader.ReadLineAsync();
                while (line != null && !line.Contains(boundary))
                {
                    line = await reader.ReadLineAsync();
                }
            }
        }
    }

    private static string ExtractDispositionValue(string disposition, string key)
    {
        var parts = disposition.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith(key + "="))
            {
                var value = trimmed.Substring(key.Length + 1);
                // Remove quotes
                return value.Trim('"');
            }
        }
        return string.Empty;
    }
}

public class FormData
{
    public Dictionary<string, string> Fields { get; set; } = new();
    public List<FormFile> Files { get; set; } = new();
}

public class FormFile
{
    public string Name { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public Stream Content { get; set; }
    public long Length => Content.Length;

    public FormFile(string name, string fileName, string contentType, Stream content)
    {
        Name = name;
        FileName = fileName;
        ContentType = contentType;
        Content = content;
    }

    public Stream OpenReadStream() => Content;
}
