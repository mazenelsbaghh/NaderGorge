using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Application.Services;

public sealed class WhatsAppCloudService
{
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
        string? ErrorCode);

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
                var (errorMessage, errorCode) = ParseMetaError(responseText);
                _logger.LogWarning(
                    "WhatsApp Cloud test message failed for {Recipient}. Status={StatusCode}, ErrorCode={ErrorCode}",
                    recipient,
                    statusCode,
                    errorCode);
                return new SendTestMessageResult(false, errorMessage, recipient, null, statusCode, errorCode);
            }

            var messageId = ParseMessageId(responseText);
            return new SendTestMessageResult(true, "WhatsApp test message sent successfully.", recipient, messageId, statusCode, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp Cloud test message failed for {Recipient}", recipient);
            return new SendTestMessageResult(false, "Failed to contact WhatsApp Cloud API.", recipient, null, 500, "WHATSAPP_CLOUD_REQUEST_FAILED");
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

    private static string? ParseMessageId(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var messages = document.RootElement.GetProperty("messages");
            if (messages.GetArrayLength() == 0)
            {
                return null;
            }

            return messages[0].TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static (string Message, string? Code) ParseMetaError(string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(responseText);
            var error = document.RootElement.GetProperty("error");
            var message = error.TryGetProperty("message", out var messageProperty)
                ? messageProperty.GetString()
                : "WhatsApp Cloud API rejected the request.";
            var code = error.TryGetProperty("code", out var codeProperty)
                ? codeProperty.GetRawText()
                : null;

            return (message ?? "WhatsApp Cloud API rejected the request.", code);
        }
        catch
        {
            return ("WhatsApp Cloud API rejected the request.", null);
        }
    }
}
