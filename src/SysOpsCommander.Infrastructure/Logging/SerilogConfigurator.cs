using Serilog;
using Serilog.Formatting.Compact;
using SysOpsCommander.Core.Constants;

namespace SysOpsCommander.Infrastructure.Logging;

/// <summary>
/// Configures Serilog with file and console sinks, custom enrichers, and the credential destructuring policy.
/// </summary>
public static class SerilogConfigurator
{
    /// <summary>
    /// Configures and returns a Serilog <see cref="ILogger"/> with file sink, enrichers, and credential destructuring.
    /// </summary>
    /// <param name="correlationIdEnricher">The session-level correlation ID enricher to use.</param>
    /// <returns>A configured Serilog <see cref="ILogger"/> instance.</returns>
    public static ILogger Configure(CorrelationIdEnricher correlationIdEnricher)
    {
        ArgumentNullException.ThrowIfNull(correlationIdEnricher);

        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppDataFolder,
            "Logs");

        Directory.CreateDirectory(logDirectory);

        string logFilePath = Path.Combine(logDirectory, "sysops-.log");

        LoggerConfiguration configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.With(correlationIdEnricher)
            .Destructure.With<CredentialDestructuringPolicy>()
            .WriteTo.File(
                new CompactJsonFormatter(),
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31);

#if DEBUG
        configuration = configuration.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
#endif

        Serilog.ILogger logger = configuration.CreateLogger();
        Log.Logger = logger;

        return logger;
    }
}
