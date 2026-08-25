using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Application.Services;

public sealed class WhatsAppCloudService
{
    private const int MaxInboundMediaBytes = 10 * 1024 * 1024;
    private const int MaxOutboundImageBytes = 5 * 1024 * 1024;
    private const int MaxOutboundAudioBytes = 16 * 1024 * 1024;
    private const int MaxTemplatePages = 1_000;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppCloudService> _logger;

    public WhatsAppCloudService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhatsAppCloudService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public sealed record SendTestMessageRequest(
        string RecipientPhoneNumber,
        string MessageType,
        string? TextBody,
        string? TemplateName,
        string? TemplateLanguage,
        string? ParentName,
        string? StudentName,
        string? Score,
        string? TotalScore,
        string? Subject,
        string? Lecture);

    public sealed record StudentResultTemplateData(
        string ParentName,
        string StudentName,
        string Score,
        string TotalScore,
        string Subject,
        string Lecture);

    public sealed record SendTestMessageResult(
        bool Success,
        string Message,
        string RecipientPhoneNumber,
        string? MetaMessageId,
        int StatusCode,
        string? ErrorCode,
        bool IsRetryable = false);

    public sealed class WhatsAppCloudException : Exception
    {
        public WhatsAppCloudException(string errorCode, int statusCode, bool isRetryable)
            : base("WhatsApp Cloud API operation failed.")
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
            IsRetryable = isRetryable;
        }

        public string ErrorCode { get; }
        public int StatusCode { get; }
        public bool IsRetryable { get; }
    }

    public sealed record DownloadedMedia(byte[] Content, string ContentType, string FileName);

    public sealed record TemplateSnapshot(
        string Id,
        string Name,
        string Language,
        string Category,
        string Status,
        JsonElement Components);

    private sealed record TemplatePage(
        IReadOnlyList<TemplateSnapshot> Templates,
        string? NextCursor);

    public sealed record TemplateComponent(string Type, IReadOnlyList<string> Parameters);

    public sealed record TemplateMessageRequest(
        string RecipientPhoneNumber,
        string TemplateName,
        string Language,
        IReadOnlyList<TemplateComponent> Components);

    public sealed record MediaMessageRequest(
        string RecipientPhoneNumber,
        string MediaType,
        string FileName,
        string ContentType,
        byte[] Content,
        string? Caption);

    private sealed record MetaRequest(
        HttpRequestMessage Request,
        string Recipient,
        string FailureMessage,
        Func<string, string?> SuccessId);

    public async Task<SendTestMessageResult> SendTestMessageAsync(
        SendTestMessageRequest request,
        CancellationToken cancellationToken)
    {
        return await SendMessageAsync(request, cancellationToken);
    }

    public async Task<SendTestMessageResult> SendStudentResultTemplateAsync(
        string recipientPhoneNumber,
        StudentResultTemplateData data,
        CancellationToken cancellationToken)
    {
        var request = new SendTestMessageRequest(
            recipientPhoneNumber,
            "template",
            null,
            _configuration["WhatsAppCloudApi:DefaultTemplateName"] ?? "student_result_2",
            _configuration["WhatsAppCloudApi:DefaultTemplateLanguage"] ?? "ar_EG",
            data.ParentName,
            data.StudentName,
            data.Score,
            data.TotalScore,
            data.Subject,
            data.Lecture);

        return await SendMessageAsync(request, cancellationToken);
    }

    public Task<SendTestMessageResult> SendTextAsync(
        string recipientPhoneNumber,
        string text,
        CancellationToken cancellationToken) =>
        SendMessageAsync(new SendTestMessageRequest(
            recipientPhoneNumber,
            "text",
            text,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null), cancellationToken);

    public async Task<DownloadedMedia> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken)
    {
        var accessToken = _configuration["WhatsAppCloudApi:AccessToken"];
        var apiVersion = _configuration["WhatsAppCloudApi:ApiVersion"] ?? "v20.0";
        if (string.IsNullOrWhiteSpace(accessToken))
            throw Failure("WHATSAPP_CLOUD_NOT_CONFIGURED", 503);
        if (string.IsNullOrWhiteSpace(mediaId))
            throw Failure("WHATSAPP_MEDIA_ID_INVALID", 422, false);

        try
        {
            using var metadataRequest = AuthorizedRequest(HttpMethod.Get, $"https://graph.facebook.com/{apiVersion}/{Uri.EscapeDataString(mediaId)}", accessToken);
            using var metadataResponse = await _httpClient.SendAsync(metadataRequest, cancellationToken);
            if (!metadataResponse.IsSuccessStatusCode)
                throw ProviderFailure(metadataResponse.StatusCode, await metadataResponse.Content.ReadAsStringAsync(cancellationToken));

            JsonDocument metadata;
            try
            {
                metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (JsonException)
            {
                throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);
            }

            using (metadata)
            {
                if (metadata.RootElement.ValueKind != JsonValueKind.Object ||
                    !metadata.RootElement.TryGetProperty("url", out var urlProperty) ||
                    urlProperty.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(urlProperty.GetString()))
                    throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);

                var url = urlProperty.GetString()!;
                var contentType = metadata.RootElement.TryGetProperty("mime_type", out var mimeType) &&
                                  mimeType.ValueKind == JsonValueKind.String
                    ? mimeType.GetString() ?? "application/octet-stream"
                    : "application/octet-stream";
                using var mediaRequest = AuthorizedRequest(HttpMethod.Get, url, accessToken);
                using var mediaResponse = await _httpClient.SendAsync(mediaRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!mediaResponse.IsSuccessStatusCode)
                    throw ProviderFailure(
                        mediaResponse.StatusCode,
                        await mediaResponse.Content.ReadAsStringAsync(cancellationToken));
                if (mediaResponse.Content.Headers.ContentLength is > MaxInboundMediaBytes)
                    throw Failure("WHATSAPP_MEDIA_TOO_LARGE", 413, false);

                var content = await ReadBoundedAsync(
                    mediaResponse.Content,
                    MaxInboundMediaBytes,
                    cancellationToken);
                if (content.Length == 0)
                    throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);
                return new DownloadedMedia(content, contentType, $"whatsapp-{mediaId}{Extension(contentType)}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (WhatsAppCloudException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw Failure("WHATSAPP_CLOUD_REQUEST_FAILED", 503);
        }
    }

    public async Task<IReadOnlyList<TemplateSnapshot>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        var accessToken = _configuration["WhatsAppCloudApi:AccessToken"];
        var businessAccountId = _configuration["WhatsAppCloudApi:BusinessAccountId"];
        var apiVersion = _configuration["WhatsAppCloudApi:ApiVersion"] ?? "v20.0";
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(businessAccountId))
            throw Failure("WHATSAPP_CLOUD_NOT_CONFIGURED", 503);
        var baseUrl = $"https://graph.facebook.com/{apiVersion}/{businessAccountId}/message_templates?fields=id,name,language,category,status,components&limit=250";
        var templates = new List<TemplateSnapshot>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        try
        {
            for (var pageNumber = 0; pageNumber < MaxTemplatePages; pageNumber++)
            {
                var url = cursor is null
                    ? baseUrl
                    : $"{baseUrl}&after={Uri.EscapeDataString(cursor)}";
                using var request = AuthorizedRequest(HttpMethod.Get, url, accessToken);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw ProviderFailure(response.StatusCode, responseText);

                var page = ParseTemplatePage(responseText);
                templates.AddRange(page.Templates);
                if (page.NextCursor is null) return templates;
                if (!seenCursors.Add(page.NextCursor))
                    throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);
                cursor = page.NextCursor;
            }

            throw Failure("WHATSAPP_CLOUD_PAGINATION_LIMIT", 502);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (WhatsAppCloudException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw Failure("WHATSAPP_CLOUD_REQUEST_FAILED", 503);
        }
    }

    public async Task<SendTestMessageResult> SendTemplateAsync(
        TemplateMessageRequest message,
        CancellationToken cancellationToken)
    {
        var recipient = NormalizeRecipient(message.RecipientPhoneNumber);
        var payload = new
        {
            messaging_product = "whatsapp",
            to = recipient,
            type = "template",
            template = new
            {
                name = message.TemplateName,
                language = new { code = message.Language },
                components = message.Components.Select(component => new
                {
                    type = component.Type.ToLowerInvariant(),
                    parameters = component.Parameters.Select(value => new { type = "text", text = value })
                })
            }
        };
        return await PostMessageAsync(payload, recipient, cancellationToken);
    }

    public async Task<SendTestMessageResult> SendMediaAsync(
        MediaMessageRequest message,
        CancellationToken cancellationToken)
    {
        var recipient = NormalizeRecipient(message.RecipientPhoneNumber);
        var validationFailure = ValidateOutboundMedia(message, recipient);
        if (validationFailure is not null) return validationFailure;
        var uploaded = await UploadMediaAsync(message, cancellationToken);
        if (!uploaded.Success) return uploaded with { RecipientPhoneNumber = recipient };

        var media = new Dictionary<string, object?> { ["id"] = uploaded.MetaMessageId };
        if (message.MediaType == "image" && !string.IsNullOrWhiteSpace(message.Caption))
            media["caption"] = message.Caption;
        if (message.MediaType == "audio") media["voice"] = true;
        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = recipient,
            ["type"] = message.MediaType,
            [message.MediaType] = media
        };
        return await PostMessageAsync(payload, recipient, cancellationToken);
    }

    private static SendTestMessageResult? ValidateOutboundMedia(
        MediaMessageRequest message,
        string recipient)
    {
        if (message.Content.Length == 0)
            return InvalidOutboundMedia(recipient, "WHATSAPP_MEDIA_EMPTY", 422,
                "WhatsApp media is empty.");
        var expectedContentType = message.MediaType switch
        {
            "image" => "image/jpeg",
            "audio" => "audio/ogg",
            _ => null
        };
        if (expectedContentType is null ||
            !string.Equals(message.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            return InvalidOutboundMedia(recipient, "WHATSAPP_MEDIA_UNSUPPORTED", 422,
                "WhatsApp media type is not supported.");
        var maximumBytes = message.MediaType == "image"
            ? MaxOutboundImageBytes
            : MaxOutboundAudioBytes;
        if (message.Content.Length > maximumBytes)
            return InvalidOutboundMedia(recipient, "WHATSAPP_MEDIA_TOO_LARGE", 413,
                "WhatsApp media exceeds the supported size.");
        return null;
    }

    private static SendTestMessageResult InvalidOutboundMedia(
        string recipient,
        string errorCode,
        int statusCode,
        string message) =>
        new(false, message, recipient, null, statusCode, errorCode, false);

    private async Task<SendTestMessageResult> UploadMediaAsync(
        MediaMessageRequest message,
        CancellationToken cancellationToken)
    {
        var accessToken = _configuration["WhatsAppCloudApi:AccessToken"];
        var phoneNumberId = _configuration["WhatsAppCloudApi:PhoneNumberId"];
        var recipient = NormalizeRecipient(message.RecipientPhoneNumber);
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
            return NotConfigured(recipient);

        using var request = AuthorizedRequest(HttpMethod.Post, GraphUrl(phoneNumberId, "media"), accessToken);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        var content = new ByteArrayContent(message.Content);
        content.Headers.ContentType = new(message.ContentType);
        form.Add(content, "file", message.FileName);
        request.Content = form;
        return await SendMetaRequestAsync(
            new MetaRequest(request, recipient, "WhatsApp media upload failed.", ParseResourceId),
            cancellationToken);
    }

    private async Task<SendTestMessageResult> PostMessageAsync(
        object payload,
        string recipient,
        CancellationToken cancellationToken)
    {
        var accessToken = _configuration["WhatsAppCloudApi:AccessToken"];
        var phoneNumberId = _configuration["WhatsAppCloudApi:PhoneNumberId"];
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
            return NotConfigured(recipient);
        using var request = AuthorizedRequest(HttpMethod.Post, GraphUrl(phoneNumberId, "messages"), accessToken);
        request.Content = JsonContent.Create(payload);
        return await SendMetaRequestAsync(
            new MetaRequest(request, recipient, "WhatsApp message send failed.", ParseMessageId),
            cancellationToken);
    }

    private async Task<SendTestMessageResult> SendMetaRequestAsync(
        MetaRequest metaRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(metaRequest.Request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = ParseMetaError(responseText);
                return new(false, error.Message, metaRequest.Recipient, null, (int)response.StatusCode,
                    error.Code, IsRetryable(response.StatusCode, error.IsTransient));
            }
            var providerId = metaRequest.SuccessId(responseText);
            if (string.IsNullOrWhiteSpace(providerId))
                return new(false, "WhatsApp Cloud API returned an invalid response.", metaRequest.Recipient,
                    null, 502, "WHATSAPP_CLOUD_INVALID_RESPONSE", true);
            return new(true, "WhatsApp request completed.", metaRequest.Recipient,
                providerId, (int)response.StatusCode, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException)
        {
            return new(false, metaRequest.FailureMessage, metaRequest.Recipient, null, 503,
                "WHATSAPP_CLOUD_REQUEST_FAILED", true);
        }
    }

    private SendTestMessageResult NotConfigured(string recipient) =>
        new(false, "WhatsApp Cloud API is not configured.", recipient, null, 503, "WHATSAPP_CLOUD_NOT_CONFIGURED");

    private string GraphUrl(string resourceId, string edge)
    {
        var apiVersion = _configuration["WhatsAppCloudApi:ApiVersion"] ?? "v20.0";
        return $"https://graph.facebook.com/{apiVersion}/{resourceId}/{edge}";
    }

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static string Extension(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "audio/ogg" or "audio/opus" => ".ogg",
        "application/pdf" => ".pdf",
        _ => ".bin"
    };

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var chunk = new byte[81_920];
        while (true)
        {
            var readLimit = (int)Math.Min(chunk.Length, maxBytes - destination.Length + 1);
            var bytesRead = await source.ReadAsync(chunk.AsMemory(0, readLimit), cancellationToken);
            if (bytesRead == 0) return destination.ToArray();
            if (destination.Length + bytesRead > maxBytes)
                throw Failure("WHATSAPP_MEDIA_TOO_LARGE", 413, false);
            await destination.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private async Task<SendTestMessageResult> SendMessageAsync(
        SendTestMessageRequest request,
        CancellationToken cancellationToken)
    {
        var accessToken = _configuration["WhatsAppCloudApi:AccessToken"];
        var phoneNumberId = _configuration["WhatsAppCloudApi:PhoneNumberId"];
        var apiVersion = _configuration["WhatsAppCloudApi:ApiVersion"] ?? "v20.0";

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return new SendTestMessageResult(
                false,
                "WhatsApp Cloud API is not configured. Set WhatsAppCloudApi:AccessToken and WhatsAppCloudApi:PhoneNumberId.",
                NormalizeRecipient(request.RecipientPhoneNumber),
                null,
                (int)HttpStatusCode.ServiceUnavailable,
                "WHATSAPP_CLOUD_NOT_CONFIGURED");
        }

        var recipient = NormalizeRecipient(request.RecipientPhoneNumber);
        if (string.IsNullOrWhiteSpace(recipient) || recipient.Length < 10)
        {
            return new SendTestMessageResult(
                false,
                "Recipient phone number is invalid.",
                recipient,
                null,
                (int)HttpStatusCode.BadRequest,
                "INVALID_RECIPIENT_PHONE");
        }

        var messageType = string.Equals(request.MessageType, "text", StringComparison.OrdinalIgnoreCase)
            ? "text"
            : "template";

        object payload = messageType == "text"
            ? CreateTextPayload(recipient, request.TextBody)
            : CreateTemplatePayload(
                recipient,
                request.TemplateName ?? _configuration["WhatsAppCloudApi:DefaultTemplateName"] ?? "hello_world",
                request.TemplateLanguage ?? _configuration["WhatsAppCloudApi:DefaultTemplateLanguage"] ?? "en_US",
                request);

        var url = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Content = JsonContent.Create(payload);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                var error = ParseMetaError(responseText);
                _logger.LogWarning(
                    "WhatsApp Cloud test message failed. Status={StatusCode}, ErrorCode={ErrorCode}",
                    statusCode,
                    error.Code);
                return new SendTestMessageResult(false, error.Message, recipient, null, statusCode,
                    error.Code, IsRetryable(response.StatusCode, error.IsTransient));
            }

            var messageId = ParseMessageId(responseText);
            if (string.IsNullOrWhiteSpace(messageId))
                return new SendTestMessageResult(false, "WhatsApp Cloud API returned an invalid response.", recipient,
                    null, 502, "WHATSAPP_CLOUD_INVALID_RESPONSE", true);
            return new SendTestMessageResult(true, "WhatsApp test message sent successfully.", recipient, messageId, statusCode, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "WhatsApp Cloud test message request failed.");
            return new SendTestMessageResult(false, "Failed to contact WhatsApp Cloud API.", recipient,
                null, 503, "WHATSAPP_CLOUD_REQUEST_FAILED", true);
        }
    }

    private static object CreateTextPayload(string recipient, string? textBody) => new
    {
        messaging_product = "whatsapp",
        recipient_type = "individual",
        to = recipient,
        type = "text",
        text = new
        {
            preview_url = false,
            body = string.IsNullOrWhiteSpace(textBody)
                ? "رسالة اختبار من منصة مسار."
                : textBody.Trim()
        }
    };

    private static object CreateTemplatePayload(
        string recipient,
        string templateName,
        string templateLanguage,
        SendTestMessageRequest request)
    {
        var normalizedTemplateName = templateName.Trim();
        if (string.Equals(normalizedTemplateName, "student_result_2", StringComparison.OrdinalIgnoreCase))
        {
            var parentName = ValueOrDefault(request.ParentName, "ولي الأمر");
            var studentName = ValueOrDefault(request.StudentName, "الطالب");
            var score = ValueOrDefault(request.Score, "26");
            var totalScore = ValueOrDefault(request.TotalScore, "60");
            var subject = ValueOrDefault(request.Subject, "مادة التاريخ");
            var lecture = ValueOrDefault(request.Lecture, "المحاضرة السابعة");

            return new
            {
                messaging_product = "whatsapp",
                to = recipient,
                type = "template",
                template = new
                {
                    name = normalizedTemplateName,
                    language = new
                    {
                        code = templateLanguage.Trim()
                    },
                    components = new object[]
                    {
                        new
                        {
                            type = "header",
                            parameters = new object[]
                            {
                                new { type = "text", text = parentName }
                            }
                        },
                        new
                        {
                            type = "body",
                            parameters = new object[]
                            {
                                new { type = "text", text = studentName },
                                new { type = "text", text = score },
                                new { type = "text", text = totalScore },
                                new { type = "text", text = subject },
                                new { type = "text", text = lecture }
                            }
                        }
                    }
                }
            };
        }

        return new
        {
            messaging_product = "whatsapp",
            to = recipient,
            type = "template",
            template = new
            {
                name = normalizedTemplateName,
                language = new
                {
                    code = templateLanguage.Trim()
                }
            }
        };
    }

    private static string ValueOrDefault(string? value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static string NormalizeRecipient(string value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.StartsWith("01", StringComparison.Ordinal) && digits.Length == 11)
        {
            return $"20{digits[1..]}";
        }

        return digits;
    }

    private static string RequiredText(JsonElement value, string property)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(property, out var text) ||
            text.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(text.GetString()))
            throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);
        return text.GetString()!;
    }

    private static TemplatePage ParseTemplatePage(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("data", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
                throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);
            var templates = rows.EnumerateArray().Select(ParseTemplate).ToArray();
            return new TemplatePage(templates, TemplateNextCursor(document.RootElement));
        }
        catch (JsonException)
        {
            throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);
        }
    }

    private static TemplateSnapshot ParseTemplate(JsonElement row) => new(
        RequiredText(row, "id"),
        RequiredText(row, "name"),
        RequiredText(row, "language"),
        RequiredText(row, "category"),
        RequiredText(row, "status"),
        row.TryGetProperty("components", out var components)
            ? components.Clone()
            : JsonSerializer.SerializeToElement(Array.Empty<object>()));

    private static string? TemplateNextCursor(JsonElement root)
    {
        if (!root.TryGetProperty("paging", out var paging) ||
            paging.ValueKind != JsonValueKind.Object ||
            !paging.TryGetProperty("next", out var next) ||
            next.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(next.GetString()))
            return null;
        if (!paging.TryGetProperty("cursors", out var cursors))
            throw Failure("WHATSAPP_CLOUD_INVALID_RESPONSE", 502);
        return RequiredText(cursors, "after");
    }

    private static WhatsAppCloudException ProviderFailure(HttpStatusCode statusCode, string responseText)
    {
        var error = ParseMetaError(responseText);
        var safeProviderCode = string.IsNullOrWhiteSpace(error.Code)
            ? null
            : new string(error.Code
                .Where(character => char.IsAsciiLetterOrDigit(character) || character == '_')
                .Take(64)
                .ToArray());
        return Failure(
            string.IsNullOrWhiteSpace(safeProviderCode)
                ? "WHATSAPP_CLOUD_REQUEST_FAILED"
                : $"WHATSAPP_CLOUD_{safeProviderCode}",
            (int)statusCode,
            IsRetryable(statusCode, error.IsTransient));
    }

    private static WhatsAppCloudException Failure(string errorCode, int statusCode, bool? isRetryable = null) =>
        new(errorCode, statusCode,
            isRetryable ?? IsRetryable((HttpStatusCode)statusCode, false));

    private static bool IsRetryable(HttpStatusCode statusCode, bool isTransient) =>
        isTransient || statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static string? ParseMessageId(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array ||
                messages.GetArrayLength() == 0)
                return null;
            return messages[0].ValueKind == JsonValueKind.Object &&
                   messages[0].TryGetProperty("id", out var id) &&
                   id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ParseResourceId(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("id", out var id) &&
                   id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MetaError ParseMetaError(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("error", out var error) ||
                error.ValueKind != JsonValueKind.Object)
                return new("WhatsApp Cloud API rejected the request.", null, false);
            var message = error.TryGetProperty("message", out var messageProperty) &&
                          messageProperty.ValueKind == JsonValueKind.String
                ? messageProperty.GetString()
                : "WhatsApp Cloud API rejected the request.";
            var code = error.TryGetProperty("code", out var codeProperty)
                ? codeProperty.GetRawText()
                : null;
            var isTransient = error.TryGetProperty("is_transient", out var transientProperty) &&
                              transientProperty.ValueKind is JsonValueKind.True;

            return new(message ?? "WhatsApp Cloud API rejected the request.", code, isTransient);
        }
        catch (JsonException)
        {
            return new("WhatsApp Cloud API rejected the request.", null, false);
        }
    }

    private sealed record MetaError(string Message, string? Code, bool IsTransient);
}
