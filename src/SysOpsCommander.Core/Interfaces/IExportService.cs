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

    /// <summary>
    /// Exports Active Directory objects to a CSV file with user-selected columns.
    /// </summary>
    /// <param name="objects">The AD objects to export.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="columns">The ordered list of column names to include in the export.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportAdObjectsToCsvAsync(IEnumerable<AdObject> objects, string filePath, IReadOnlyList<string> columns, CancellationToken cancellationToken);

    /// <summary>
    /// Exports Active Directory objects to an Excel file with user-selected columns.
    /// </summary>
    /// <param name="objects">The AD objects to export.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="columns">The ordered list of column names to include in the export.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExportAdObjectsToExcelAsync(IEnumerable<AdObject> objects, string filePath, IReadOnlyList<string> columns, CancellationToken cancellationToken);
}
