using System.Runtime.Versioning;
using System.Security.Principal;

namespace SysOpsCommander.Services;

/// <summary>
/// Provides static helpers for converting AD attribute values to human-readable formats.
/// Handles binary SIDs, GUIDs, FileTime integers, and UAC flag decoding.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class AdAttributeMapper
{
    private static readonly HashSet<string> FileTimeAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "lastLogonTimestamp", "pwdLastSet", "accountExpires", "lockoutTime", "badPasswordTime"
    };

    private static readonly HashSet<string> BinarySidAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "objectSid"
    };

    private static readonly HashSet<string> BinaryGuidAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "objectGUID"
    };

    /// <summary>
    /// Converts an AD attribute value to a display-friendly format based on the attribute name.
    /// </summary>
    internal static object? FormatValue(string attributeName, object? value)
    {
        return value is null
            ? null
            : BinarySidAttributes.Contains(attributeName) && value is byte[] sidBytes
                ? ConvertSid(sidBytes)
                : BinaryGuidAttributes.Contains(attributeName) && value is byte[] guidBytes
                    ? ConvertGuid(guidBytes)
                    : FileTimeAttributes.Contains(attributeName) && value is long fileTime
                        ? ConvertFileTime(fileTime)
                        : attributeName.Equals("userAccountControl", StringComparison.OrdinalIgnoreCase) && value is int uac
                            ? DecodeUacFlags(uac)
                            : value;
    }

    /// <summary>
    /// Converts a binary SID byte array to its string representation (e.g., "S-1-5-21-...").
    /// </summary>
    internal static string ConvertSid(byte[] sidBytes)
    {
        ArgumentNullException.ThrowIfNull(sidBytes);
        return new SecurityIdentifier(sidBytes, 0).Value;
    }

    /// <summary>
    /// Converts a binary GUID byte array to its string representation.
    /// </summary>
    internal static string ConvertGuid(byte[] guidBytes)
    {
        ArgumentNullException.ThrowIfNull(guidBytes);
        return new Guid(guidBytes).ToString();
    }

    /// <summary>
    /// Converts a Windows FileTime (100-nanosecond ticks since 1601-01-01) to an ISO 8601 UTC string.
    /// Returns "Never" for sentinel values (0 and <see cref="long.MaxValue"/>).
    /// </summary>
    internal static string ConvertFileTime(long fileTime)
    {
        return fileTime is 0 or long.MaxValue
            ? "Never"
            : DateTime.FromFileTimeUtc(fileTime).ToString("o");
    }

    /// <summary>
    /// Decodes a <c>userAccountControl</c> integer into a list of human-readable flag names.
    /// </summary>
    internal static IReadOnlyList<string> DecodeUacFlags(int uac)
    {
        var flags = new List<string>();

        if ((uac & 0x0002) != 0)
        {
            flags.Add("ACCOUNTDISABLE");
        }

        if ((uac & 0x0010) != 0)
        {
            flags.Add("LOCKOUT");
        }

        if ((uac & 0x0020) != 0)
        {
            flags.Add("PASSWD_NOTREQD");
        }

        if ((uac & 0x0040) != 0)
        {
            flags.Add("PASSWD_CANT_CHANGE");
        }

        if ((uac & 0x0200) != 0)
        {
            flags.Add("NORMAL_ACCOUNT");
        }

        if ((uac & 0x0800) != 0)
        {
            flags.Add("INTERDOMAIN_TRUST_ACCOUNT");
        }

        if ((uac & 0x1000) != 0)
        {
            flags.Add("WORKSTATION_TRUST_ACCOUNT");
        }

        if ((uac & 0x2000) != 0)
        {
            flags.Add("SERVER_TRUST_ACCOUNT");
        }

        if ((uac & 0x10000) != 0)
        {
            flags.Add("DONT_EXPIRE_PASSWD");
        }

        if ((uac & 0x40000) != 0)
        {
            flags.Add("SMARTCARD_REQUIRED");
        }

        if ((uac & 0x80000) != 0)
        {
            flags.Add("TRUSTED_FOR_DELEGATION");
        }

        if ((uac & 0x100000) != 0)
        {
            flags.Add("NOT_DELEGATED");
        }

        if ((uac & 0x200000) != 0)
        {
            flags.Add("USE_DES_KEY_ONLY");
        }

        if ((uac & 0x400000) != 0)
        {
            flags.Add("DONT_REQ_PREAUTH");
        }

        if ((uac & 0x800000) != 0)
        {
            flags.Add("PASSWORD_EXPIRED");
        }

        if ((uac & 0x1000000) != 0)
        {
            flags.Add("TRUSTED_TO_AUTH_FOR_DELEGATION");
        }

        return flags;
    }

    /// <summary>
    /// Resolves a SID byte array to an NT account name (e.g., "DOMAIN\GroupName").
    /// Returns the SID string if translation fails (orphan SID).
    /// </summary>
    internal static string ResolveSidToName(byte[] sidBytes)
    {
        ArgumentNullException.ThrowIfNull(sidBytes);

        var sid = new SecurityIdentifier(sidBytes, 0);
        try
        {
            var account = (NTAccount?)sid.Translate(typeof(NTAccount));
            return account?.Value ?? sid.Value;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }
}
