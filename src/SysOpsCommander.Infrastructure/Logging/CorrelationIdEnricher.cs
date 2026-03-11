using Serilog.Core;
using Serilog.Events;

namespace SysOpsCommander.Infrastructure.Logging;

/// <summary>
/// Enriches every log event with a session-level correlation ID for tracing across a single application run.
/// </summary>
public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly LogEventProperty _correlationProperty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdEnricher"/> class with an auto-generated correlation ID.
    /// </summary>
    public CorrelationIdEnricher()
        : this(Guid.NewGuid().ToString("D"))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdEnricher"/> class with the specified correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID to attach to all log events.</param>
    public CorrelationIdEnricher(string correlationId)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        CorrelationId = correlationId;
        _correlationProperty = new LogEventProperty("CorrelationId", new ScalarValue(correlationId));
    }

    /// <summary>
    /// Gets the correlation ID assigned to this application session.
    /// </summary>
    public string CorrelationId { get; }

    /// <summary>
    /// Enriches the log event with the session correlation ID.
    /// </summary>
    /// <param name="logEvent">The log event to enrich.</param>
    /// <param name="propertyFactory">Factory for creating log event properties.</param>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        logEvent.AddPropertyIfAbsent(_correlationProperty);
    }
}
