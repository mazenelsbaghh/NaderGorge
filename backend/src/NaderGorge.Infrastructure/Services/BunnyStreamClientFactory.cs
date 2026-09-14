using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class BunnyStreamClientFactory : IBunnyStreamClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public BunnyStreamClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IBunnyStreamClient Create(long libraryId, string apiKey) =>
        new BunnyStreamClient(_httpClientFactory.CreateClient("BunnyStream"), libraryId, apiKey);
}
