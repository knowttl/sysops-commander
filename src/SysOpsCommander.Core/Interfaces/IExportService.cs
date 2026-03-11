using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Exports execution results and audit log entries to Excel and CSV formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports host results to an Excel file.
    /// </summary>
    /// <param name="results">The host results to export.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportToExcelAsync(IEnumerable<HostResult> results, string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Exports host results to a CSV file.
    /// </summary>
    /// <param name="results">The host results to export.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportToCsvAsync(IEnumerable<HostResult> results, string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Exports audit log entries to an Excel file.
    /// </summary>
    /// <param name="entries">The audit log entries to export.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportAuditLogToExcelAsync(IEnumerable<AuditLogEntry> entries, string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Exports audit log entries to a CSV file.
    /// </summary>
    /// <param name="entries">The audit log entries to export.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportAuditLogToCsvAsync(IEnumerable<AuditLogEntry> entries, string filePath, CancellationToken cancellationToken);
}
