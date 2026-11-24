using System.Text;

namespace Saranga.Domain.Common;

public static class PhoneNormalizer
{
    /// <summary>
    /// Normalizes phone numbers by stripping whitespace, hyphens, parentheses, and dots,
    /// while preserving optional leading '+' for international standard dialing.
    /// Does not force +94, allowing domestic and international phone numbers.
    /// </summary>
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var trimmed = phone.Trim();
        var sb = new StringBuilder();

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (i == 0 && c == '+')
            {
                sb.Append(c);
            }
            else if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
