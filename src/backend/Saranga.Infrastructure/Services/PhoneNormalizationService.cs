using Microsoft.Extensions.Configuration;
using Saranga.Application.Common.Interfaces;
using Saranga.Domain.Common;

namespace Saranga.Infrastructure.Services;

public class PhoneNormalizationService : IPhoneNormalizationService
{
    public string DefaultRegion { get; }

    public PhoneNormalizationService(IConfiguration configuration)
    {
        // Configurable default region with fallback to 'LK' (Sri Lanka)
        DefaultRegion = configuration["PhoneNormalization:DefaultRegion"]
                        ?? configuration["DEFAULT_PHONE_REGION"]
                        ?? "LK";
    }

    public string? Normalize(string? phone, string? regionCode = null)
    {
        var targetRegion = !string.IsNullOrWhiteSpace(regionCode) ? regionCode.Trim().ToUpperInvariant() : DefaultRegion;
        return PhoneNormalizer.Normalize(phone, targetRegion);
    }

    public string ExtractDigits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        return new string(phone.Where(char.IsDigit).ToArray());
    }
}
