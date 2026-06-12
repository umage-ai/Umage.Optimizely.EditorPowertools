namespace UmageAI.Optimizely.EditorPowerTools.Forms.Services;

/// <summary>
/// Heuristics for spotting personal / GDPR-sensitive data in form fields.
/// Extracted into its own internal type so the (otherwise EPiServer-coupled)
/// detection logic can be unit-tested directly. Errs on the side of false
/// positives — every consumer treats the result as advisory, never destructive.
/// </summary>
internal static class FormsPiiHeuristics
{
    /// <summary>
    /// Curated keyword list (incl. a few da/no/sv terms common in this market)
    /// matched case-insensitively as a substring of an element's label.
    /// </summary>
    internal static readonly string[] LabelKeywords = new[]
    {
        "email", "e-mail", "mail", "phone", "tel", "mobile", "address", "street",
        "city", "zip", "postal", "country", "name", "first name", "last name",
        "surname", "fullname", "full name", "ssn", "social security",
        "dob", "birth", "birthday", "birthdate", "passport", "id number",
        "national id", "credit card", "iban", "ip", "ip address", "linkedin",
        "facebook", "twitter", "navn", "adresse", "telefon", "fødselsdag"
    };

    /// <summary>
    /// Element content-type names that are high-confidence PII regardless of label.
    /// </summary>
    internal static readonly string[] PiiElementTypeNames = new[]
    {
        "FileUploadElementBlock"
    };

    /// <summary>
    /// Returns true when an element (by its content-type name and/or label) looks
    /// like it captures personal data. <paramref name="hint"/> is a short reason
    /// ("file upload", or the matched keyword) suitable for display.
    /// </summary>
    internal static bool LooksLikePii(string? elementTypeName, string? label, out string hint)
    {
        if (!string.IsNullOrEmpty(elementTypeName))
        {
            foreach (var t in PiiElementTypeNames)
            {
                if (string.Equals(elementTypeName, t, StringComparison.OrdinalIgnoreCase))
                {
                    hint = "file upload";
                    return true;
                }
            }
        }

        var l = (label ?? string.Empty).ToLowerInvariant();
        if (l.Length > 0)
        {
            foreach (var kw in LabelKeywords)
            {
                if (l.Contains(kw)) { hint = kw; return true; }
            }
        }

        hint = string.Empty;
        return false;
    }
}
