using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Application.Services;

/// <summary>
/// Verifies phone numbers against WhatsApp via Evolution API.
/// </summary>
public class WhatsAppVerificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _instanceName;
    private readonly ILogger<WhatsAppVerificationService> _logger;

    public WhatsAppVerificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhatsAppVerificationService> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["EvolutionApi:BaseUrl"]
            ?? throw new InvalidOperationException("EvolutionApi:BaseUrl is not configured");
        _apiKey = configuration["EvolutionApi:ApiKey"]
            ?? throw new InvalidOperationException("EvolutionApi:ApiKey is not configured");
        _instanceName = configuration["EvolutionApi:InstanceName"]
            ?? throw new InvalidOperationException("EvolutionApi:InstanceName is not configured");
        _logger = logger;
    }

    public record WhatsAppCheckResult(bool? Exists, string Number);

    /// <summary>
    /// Check if a phone number is registered on WhatsApp.
    /// </summary>
    /// <param name="phoneNumber">Egyptian phone number (e.g. 01012345678)</param>
    public async Task<WhatsAppCheckResult> CheckWhatsAppAsync(string phoneNumber)
    {
        // Convert Egyptian format (01X...) to international (20X...)
        var internationalNumber = phoneNumber.StartsWith("0")
            ? "20" + phoneNumber[1..]
            : phoneNumber;

        var url = $"{_baseUrl}/chat/whatsappNumbers/{_instanceName}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("apikey", _apiKey);
            request.Content = JsonContent.Create(new
            {
                numbers = new[] { internationalNumber }
            });

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var results = await response.Content.ReadFromJsonAsync<List<EvolutionApiResponse>>();

            if (results is { Count: > 0 })
            {
                var result = results[0];
                _logger.LogInformation(
                    "WhatsApp check for {Number}: exists={Exists}",
                    internationalNumber, result.Exists);
                return new WhatsAppCheckResult(result.Exists, result.Number);
            }

            _logger.LogWarning("WhatsApp check returned empty results for {Number}", internationalNumber);
            return new WhatsAppCheckResult(null, internationalNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp verification failed for {Number}", internationalNumber);
            return new WhatsAppCheckResult(null, internationalNumber);
        }
    }

    private record EvolutionApiResponse(bool Exists, string Jid, string Number);
}
