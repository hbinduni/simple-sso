using System.Text.RegularExpressions;

namespace Server.Common;

public static partial class Validation
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    public static bool IsValidEmail(string email) => EmailRegex().IsMatch(email);

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidPassword(string password) => password.Length >= 8;
}
