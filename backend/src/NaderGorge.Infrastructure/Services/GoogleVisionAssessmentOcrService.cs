using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.Admin.Ocr;

namespace NaderGorge.Infrastructure.Services;

public sealed class GoogleVisionAssessmentOcrService : IAssessmentOcrService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GoogleVisionAssessmentOcrService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<AssessmentOcrQuestionDto>> ExtractQuestionsAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ValidateSupportedContentType(contentType);
        var apiKey = ReadApiKey();
        var imageBytes = await ReadContentAsync(content, cancellationToken);
        var requestPayload = BuildVisionRequest(imageBytes, contentType);
        var endpoint = ResolveEndpoint(contentType);
        using var response = await SendVisionRequestAsync(apiKey, endpoint, requestPayload, cancellationToken);
        var payload = await ReadPayloadAsync(response, cancellationToken);
        return ParseVisionResponse(response, payload);
    }

    private static void ValidateSupportedContentType(string contentType)
    {
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        if (!isImage && !string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OCR supports JPG, PNG, WEBP, and PDF files.");
    }

    private string ReadApiKey()
    {
        var candidates = new[]
        {
            _configuration["GoogleCloud:VisionApiKey"],
            _configuration["GOOGLE_CLOUD_VISION_API_KEY"],
            Environment.GetEnvironmentVariable("GOOGLE_CLOUD_VISION_API_KEY")
        };
        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))
            ?? throw new InvalidOperationException("Google Cloud Vision is not configured. Set GOOGLE_CLOUD_VISION_API_KEY on the backend.");
    }

    private static async Task<byte[]> ReadContentAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static object BuildVisionRequest(byte[] fileBytes, string contentType)
    {
        var encodedContent = Convert.ToBase64String(fileBytes);
        var features = new[] { new { type = "DOCUMENT_TEXT_DETECTION" } };
        var imageContext = new { languageHints = new[] { "ar", "en" } };
        return string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            ? new { requests = new[] { new { inputConfig = new { content = encodedContent, mimeType = contentType }, features, imageContext } } }
            : new { requests = new[] { new { image = new { content = encodedContent }, features, imageContext } } };
    }

    private static string ResolveEndpoint(string contentType) =>
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            ? "https://vision.googleapis.com/v1/files:annotate"
            : "https://vision.googleapis.com/v1/images:annotate";

    private async Task<HttpResponseMessage> SendVisionRequestAsync(
        string apiKey,
        string endpoint,
        object requestPayload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestPayload)
        };
        request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<JsonElement> ReadPayloadAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

    private static IReadOnlyList<AssessmentOcrQuestionDto> ParseVisionResponse(HttpResponseMessage response, JsonElement payload)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(ReadGoogleError(payload) ?? "Cloud Vision request failed.");

        if (!payload.TryGetProperty("responses", out var responses) || responses.GetArrayLength() == 0)
            return [];

        var first = responses[0];
        if (first.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.TryGetProperty("message", out var message)
                ? message.GetString()
                : "Cloud Vision could not read this image.");

        var imageResponses = first.TryGetProperty("responses", out var fileResponses)
            ? fileResponses
            : responses;
        var extractedText = imageResponses.EnumerateArray()
            .Select(ReadExtractedText)
            .Where(text => !string.IsNullOrWhiteSpace(text));
        return AssessmentOcrQuestionParser.Parse(string.Join(Environment.NewLine, extractedText));
    }

    private static string? ReadExtractedText(JsonElement response) =>
        response.TryGetProperty("fullTextAnnotation", out var annotation)
        && annotation.TryGetProperty("text", out var extracted)
            ? extracted.GetString()
            : null;

    private static string? ReadGoogleError(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("error", out var error)
            && error.TryGetProperty("message", out var message))
            return message.GetString();

        return null;
    }
}
