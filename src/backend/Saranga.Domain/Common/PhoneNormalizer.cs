using System.Text;
using PhoneNumbers;

namespace Saranga.Domain.Common;

public static class PhoneNormalizer
{
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();

    /// <summary>
    /// Normalizes phone numbers to canonical E.164 international format (e.g., +94779991111).
    /// Uses Google libphonenumber with default region 'LK' (Sri Lanka).
    /// If an international country code is explicitly provided (e.g., +1, +44), it is preserved.
    /// Returns null if input is null or whitespace.
    /// </summary>
    public static string? Normalize(string? phone, string defaultRegion = "LK")
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var raw = phone.Trim();

        try
        {
            // libphonenumber handles both local formats with defaultRegion and international formats starting with '+'
            var parsed = PhoneUtil.Parse(raw, defaultRegion);
            if (PhoneUtil.IsPossibleNumber(parsed))
            {
                return PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
            }
        }
        catch (NumberParseException)
        {
            // Fallback for non-standard or partial inputs
        }

        // Fallback: strip whitespace/punctuation, preserve leading '+' if present
        var sb = new StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (i == 0 && c == '+')
            {
                sb.Append(c);
            }
            else if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }

        var fallback = sb.ToString();
        return string.IsNullOrEmpty(fallback) ? null : fallback;
    }
}
