using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Exports execution results and audit log entries to CSV and Excel formats.
/// </summary>
public sealed class ExportService : IExportService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportService"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance.</param>
    public ExportService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExportToCsvAsync(IEnumerable<HostResult> results, string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(filePath);
        _logger.Information("Exporting host results to CSV: {FilePath}", filePath);

        await using var writer = new StreamWriter(filePath);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        csv.WriteHeader<HostResultCsvRecord>();
        await csv.NextRecordAsync().ConfigureAwait(false);

        foreach (HostResult result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            csv.WriteRecord(HostResultCsvRecord.FromHostResult(result));
            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await csv.FlushAsync().ConfigureAwait(false);
        _logger.Information("CSV export completed: {FilePath}", filePath);
    }

    /// <inheritdoc/>
    public Task ExportToExcelAsync(IEnumerable<HostResult> results, string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(filePath);
        _logger.Information("Exporting host results to Excel: {FilePath}", filePath);

        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            IXLWorksheet worksheet = workbook.Worksheets.Add("Results");

            worksheet.Cell(1, 1).Value = "Hostname";
            worksheet.Cell(1, 2).Value = "Status";
            worksheet.Cell(1, 3).Value = "Output";
            worksheet.Cell(1, 4).Value = "Errors";
            worksheet.Cell(1, 5).Value = "Duration (ms)";
            worksheet.Cell(1, 6).Value = "Completed At (UTC)";

            int row = 2;
            foreach (HostResult result in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                worksheet.Cell(row, 1).Value = result.Hostname;
                worksheet.Cell(row, 2).Value = result.Status.ToString();
                worksheet.Cell(row, 3).Value = result.Output;
                worksheet.Cell(row, 4).Value = result.ErrorOutput ?? string.Empty;
                worksheet.Cell(row, 5).Value = result.Duration.TotalMilliseconds;
                worksheet.Cell(row, 6).Value = result.CompletedAt.ToString("o", CultureInfo.InvariantCulture);
                row++;
            }

            IXLRange headerRange = worksheet.Range(1, 1, 1, 6);
            headerRange.Style.Font.Bold = true;
            _ = worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
            _logger.Information("Excel export completed: {FilePath}", filePath);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExportAuditLogToCsvAsync(IEnumerable<AuditLogEntry> entries, string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(filePath);
        _logger.Information("Exporting audit log to CSV: {FilePath}", filePath);

        await using var writer = new StreamWriter(filePath);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        csv.WriteHeader<AuditLogCsvRecord>();
        await csv.NextRecordAsync().ConfigureAwait(false);

        foreach (AuditLogEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            csv.WriteRecord(AuditLogCsvRecord.FromAuditLogEntry(entry));
            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await csv.FlushAsync().ConfigureAwait(false);
        _logger.Information("Audit log CSV export completed: {FilePath}", filePath);
    }

    /// <inheritdoc/>
    public async Task ExportAdObjectsToCsvAsync(IEnumerable<AdObject> objects, string filePath, IReadOnlyList<string> columns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(columns);
        _logger.Information("Exporting AD objects to CSV: {FilePath} with {ColumnCount} columns", filePath, columns.Count);

        await using var writer = new StreamWriter(filePath);
        await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        foreach (string column in columns)
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync().ConfigureAwait(false);

        foreach (AdObject obj in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (string column in columns)
            {
                csv.WriteField(GetAdObjectColumnValue(obj, column));
            }

            await csv.NextRecordAsync().ConfigureAwait(false);
        }

        await csv.FlushAsync().ConfigureAwait(false);
        _logger.Information("AD objects CSV export completed: {FilePath}", filePath);
    }

    /// <inheritdoc/>
    public Task ExportAdObjectsToExcelAsync(IEnumerable<AdObject> objects, string filePath, IReadOnlyList<string> columns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(columns);
        _logger.Information("Exporting AD objects to Excel: {FilePath} with {ColumnCount} columns", filePath, columns.Count);

        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            IXLWorksheet worksheet = workbook.Worksheets.Add("AD Objects");

            for (int col = 0; col < columns.Count; col++)
            {
                worksheet.Cell(1, col + 1).Value = columns[col];
            }

            int row = 2;
            foreach (AdObject obj in objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int col = 0; col < columns.Count; col++)
                {
                    worksheet.Cell(row, col + 1).Value = GetAdObjectColumnValue(obj, columns[col]);
                }

                row++;
            }

            IXLRange headerRange = worksheet.Range(1, 1, 1, columns.Count);
            headerRange.Style.Font.Bold = true;
            _ = worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
            _logger.Information("AD objects Excel export completed: {FilePath}", filePath);
        }, cancellationToken);
    }

    private static string GetAdObjectColumnValue(AdObject obj, string column) =>
        column.ToUpperInvariant() switch
        {
            "NAME" => obj.Name,
            "OBJECTCLASS" => obj.ObjectClass,
            "DISPLAYNAME" => obj.DisplayName ?? string.Empty,
            "DISTINGUISHEDNAME" => obj.DistinguishedName,
            _ => obj.Attributes.TryGetValue(column, out object? value)
                ? value?.ToString() ?? string.Empty
                : string.Empty
        };

    /// <inheritdoc/>
    public Task ExportAuditLogToExcelAsync(IEnumerable<AuditLogEntry> entries, string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(filePath);
        _logger.Information("Exporting audit log to Excel: {FilePath}", filePath);

        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            IXLWorksheet worksheet = workbook.Worksheets.Add("Audit Log");

            worksheet.Cell(1, 1).Value = "Id";
            worksheet.Cell(1, 2).Value = "Timestamp (UTC)";
            worksheet.Cell(1, 3).Value = "User";
            worksheet.Cell(1, 4).Value = "Machine";
            worksheet.Cell(1, 5).Value = "Script";
            worksheet.Cell(1, 6).Value = "Target Hosts";
            worksheet.Cell(1, 7).Value = "Host Count";
            worksheet.Cell(1, 8).Value = "Success";
            worksheet.Cell(1, 9).Value = "Failures";
            worksheet.Cell(1, 10).Value = "Status";
            worksheet.Cell(1, 11).Value = "Duration (ms)";
            worksheet.Cell(1, 12).Value = "Error Summary";

            int row = 2;
            foreach (AuditLogEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                worksheet.Cell(row, 1).Value = entry.Id;
                worksheet.Cell(row, 2).Value = entry.Timestamp.ToString("o", CultureInfo.InvariantCulture);
                worksheet.Cell(row, 3).Value = entry.UserName;
                worksheet.Cell(row, 4).Value = entry.MachineName;
                worksheet.Cell(row, 5).Value = entry.ScriptName;
                worksheet.Cell(row, 6).Value = entry.TargetHosts;
                worksheet.Cell(row, 7).Value = entry.TargetHostCount;
                worksheet.Cell(row, 8).Value = entry.SuccessCount;
                worksheet.Cell(row, 9).Value = entry.FailureCount;
                worksheet.Cell(row, 10).Value = entry.Status.ToString();
                worksheet.Cell(row, 11).Value = entry.Duration.TotalMilliseconds;
                worksheet.Cell(row, 12).Value = entry.ErrorSummary ?? string.Empty;
                row++;
            }

            IXLRange headerRange = worksheet.Range(1, 1, 1, 12);
            headerRange.Style.Font.Bold = true;
            _ = worksheet.Columns().AdjustToContents();

            workbook.SaveAs(filePath);
            _logger.Information("Audit log Excel export completed: {FilePath}", filePath);
        }, cancellationToken);
    }

    private sealed class HostResultCsvRecord
    {
        public string Hostname { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Output { get; init; } = string.Empty;
        public string Errors { get; init; } = string.Empty;
        public double DurationMs { get; init; }
        public string CompletedAtUtc { get; init; } = string.Empty;

        public static HostResultCsvRecord FromHostResult(HostResult result) =>
            new()
            {
                Hostname = result.Hostname,
                Status = result.Status.ToString(),
                Output = result.Output,
                Errors = result.ErrorOutput ?? string.Empty,
                DurationMs = result.Duration.TotalMilliseconds,
                CompletedAtUtc = result.CompletedAt.ToString("o", CultureInfo.InvariantCulture)
            };
    }

    private sealed class AuditLogCsvRecord
    {
        public long Id { get; init; }
        public string TimestampUtc { get; init; } = string.Empty;
        public string User { get; init; } = string.Empty;
        public string Machine { get; init; } = string.Empty;
        public string Script { get; init; } = string.Empty;
        public string TargetHosts { get; init; } = string.Empty;
        public int HostCount { get; init; }
        public int Success { get; init; }
        public int Failures { get; init; }
        public string Status { get; init; } = string.Empty;
        public double DurationMs { get; init; }
        public string ErrorSummary { get; init; } = string.Empty;

        public static AuditLogCsvRecord FromAuditLogEntry(AuditLogEntry entry) =>
            new()
            {
                Id = entry.Id,
                TimestampUtc = entry.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                User = entry.UserName,
                Machine = entry.MachineName,
                Script = entry.ScriptName,
                TargetHosts = entry.TargetHosts,
                HostCount = entry.TargetHostCount,
                Success = entry.SuccessCount,
                Failures = entry.FailureCount,
                Status = entry.Status.ToString(),
                DurationMs = entry.Duration.TotalMilliseconds,
                ErrorSummary = entry.ErrorSummary ?? string.Empty
            };
    }
}
