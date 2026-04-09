using System.Text.RegularExpressions;

namespace Squad.Sdk.PersonalSquad;

/// <summary>The result of a license detection pass on a LICENSE file.</summary>
public sealed record LicenseInfo(
    /// <summary>License classification: <c>permissive</c>, <c>copyleft</c>, or <c>unknown</c>.</summary>
    string Type,
    /// <summary>SPDX identifier (e.g. <c>MIT</c>, <c>Apache-2.0</c>, <c>GPL-3.0</c>), or null if unrecognised.</summary>
    string? SpdxId = null);

/// <summary>
/// Detects license type from LICENSE file content.
/// Type: "permissive" | "copyleft" | "unknown"
/// </summary>
public static class LicenseDetector
{
    /// <summary>Detect the license type and SPDX identifier from raw LICENSE file text.</summary>
    public static LicenseInfo Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return new("unknown");

        var upper = content.ToUpperInvariant();

        // Copyleft — check before permissive (GPL text is more specific)
        if (upper.Contains("GNU GENERAL PUBLIC LICENSE"))
        {
            var spdx = upper.Contains("VERSION 3") ? "GPL-3.0"
                     : upper.Contains("VERSION 2") ? "GPL-2.0"
                     : "GPL";
            return new("copyleft", spdx);
        }
        if (upper.Contains("GNU LESSER GENERAL PUBLIC LICENSE") || upper.Contains("LGPL"))
            return new("copyleft", "LGPL");
        if (upper.Contains("GNU AFFERO GENERAL PUBLIC LICENSE"))
            return new("copyleft", "AGPL-3.0");
        if (Regex.IsMatch(upper, @"\bMOZILLA PUBLIC LICENSE\b"))
            return new("copyleft", "MPL-2.0");

        // Permissive
        if (upper.Contains("MIT LICENSE") || Regex.IsMatch(content, @"\bMIT\b"))
            return new("permissive", "MIT");
        if (upper.Contains("APACHE LICENSE") && upper.Contains("2.0"))
            return new("permissive", "Apache-2.0");
        if (upper.Contains("BSD") && (upper.Contains("3-CLAUSE") || upper.Contains("2-CLAUSE")))
            return new("permissive", upper.Contains("3") ? "BSD-3-Clause" : "BSD-2-Clause");
        if (upper.Contains("ISC LICENSE") || upper.Contains("ISC"))
            return new("permissive", "ISC");

        return new("unknown");
    }
}
