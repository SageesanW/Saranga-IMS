namespace Saranga.Application.Common.Interfaces;

public interface IPhoneNormalizationService
{
    string DefaultRegion { get; }

    string? Normalize(string? phone, string? regionCode = null);

    string ExtractDigits(string? phone);
}
