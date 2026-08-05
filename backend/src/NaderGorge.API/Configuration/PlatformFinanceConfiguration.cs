using Microsoft.Extensions.Options;
using NaderGorge.Application.Common.Configuration;

namespace NaderGorge.API.Configuration;

public static class PlatformFinanceConfiguration
{
    public static IServiceCollection AddPlatformFinanceConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<PlatformFinanceOptions>()
            .Bind(configuration.GetSection(PlatformFinanceOptions.SectionName))
            .Validate(options => options.ReadOnlyCockpitEnabled || options.MutationsEnabled,
                "PlatformFinance must expose either the read-only cockpit or mutations.")
            .ValidateOnStart();
        return services;
    }
}
