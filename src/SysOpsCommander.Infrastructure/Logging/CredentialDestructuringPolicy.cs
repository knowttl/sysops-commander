using System.Net;
using System.Security;
using Serilog.Core;
using Serilog.Events;

namespace SysOpsCommander.Infrastructure.Logging;

/// <summary>
/// Prevents credential objects from being logged by replacing their values with "[REDACTED]".
/// </summary>
public sealed class CredentialDestructuringPolicy : IDestructuringPolicy
{
    private const string RedactedValue = "[REDACTED]";

    /// <summary>
    /// Attempts to destructure the given value, replacing credential types with a redacted placeholder.
    /// </summary>
    /// <param name="value">The object to destructure.</param>
    /// <param name="propertyValueFactory">Factory for creating log event property values.</param>
    /// <param name="result">The resulting redacted property value, if applicable.</param>
    /// <returns><see langword="true"/> if the value was a credential type and was redacted; otherwise, <see langword="false"/>.</returns>
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        if (value is SecureString or NetworkCredential)
        {
            result = new ScalarValue(RedactedValue);
            return true;
        }

        // PSCredential check by type name to avoid hard dependency on PowerShell SDK
        if (value?.GetType().Name is "PSCredential")
        {
            result = new ScalarValue(RedactedValue);
            return true;
        }

        result = null;
        return false;
    }
}
