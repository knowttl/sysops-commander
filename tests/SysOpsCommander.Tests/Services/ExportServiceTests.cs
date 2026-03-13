using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

/// <summary>
/// Tests for <see cref="ExportService"/> covering CSV and Excel export
/// for both host results and audit log entries.
/// </summary>
public sealed class ExportServiceTests : IDisposable
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ExportService _service;
    private readonly string _tempDir;

    public ExportServiceTests()
    {
        _service = new ExportService(_logger);
        _tempDir = Path.Combine(Path.GetTempPath(), $"SysOpsExportTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Action act = () => _ = new ExportService(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task ExportToCsvAsync_ValidResults_CreatesFile()
    {
        string filePath = GetTempFilePath("results.csv");
        List<HostResult> results = CreateSampleHostResults();

        await _service.ExportToCsvAsync(results, filePath, CancellationToken.None);

        File.Exists(filePath).Should().BeTrue();
        string[] lines = await File.ReadAllLinesAsync(filePath);
        lines.Should().HaveCountGreaterThanOrEqualTo(3); // header + 2 data rows
        lines[0].Should().Contain("Hostname");
        lines[0].Should().Contain("Status");
    }

    [Fact]
    public async Task ExportToCsvAsync_EmptyResults_CreatesHeaderOnly()
    {
        string filePath = GetTempFilePath("empty.csv");

        await _service.ExportToCsvAsync([], filePath, CancellationToken.None);

        File.Exists(filePath).Should().BeTrue();
        string content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("Hostname");
        string[] lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1); // header only
    }

    [Fact]
    public async Task ExportToExcelAsync_ValidResults_CreatesWorkbook()
    {
        string filePath = GetTempFilePath("results.xlsx");
        List<HostResult> results = CreateSampleHostResults();

        await _service.ExportToExcelAsync(results, filePath, CancellationToken.None);

        File.Exists(filePath).Should().BeTrue();
        using var workbook = new XLWorkbook(filePath);
        IXLWorksheet worksheet = workbook.Worksheet("Results");
        worksheet.Should().NotBeNull();
        worksheet.Cell(1, 1).Value.ToString(CultureInfo.InvariantCulture).Should().Be("Hostname");
        worksheet.Cell(2, 1).Value.ToString(CultureInfo.InvariantCulture).Should().Be("HOST-01");
        worksheet.Cell(3, 1).Value.ToString(CultureInfo.InvariantCulture).Should().Be("HOST-02");
    }

    [Fact]
    public async Task ExportToExcelAsync_HeadersAreBold()
    {
        string filePath = GetTempFilePath("bold.xlsx");
        List<HostResult> results = CreateSampleHostResults();

        await _service.ExportToExcelAsync(results, filePath, CancellationToken.None);

        using var workbook = new XLWorkbook(filePath);
        IXLWorksheet worksheet = workbook.Worksheet("Results");
        worksheet.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public async Task ExportAuditLogToCsvAsync_AllColumns_Included()
    {
        string filePath = GetTempFilePath("audit.csv");
        List<AuditLogEntry> entries = CreateSampleAuditEntries();

        await _service.ExportAuditLogToCsvAsync(entries, filePath, CancellationToken.None);

        File.Exists(filePath).Should().BeTrue();
        string header = (await File.ReadAllLinesAsync(filePath))[0];
        header.Should().Contain("Id");
        header.Should().Contain("User");
        header.Should().Contain("Script");
        header.Should().Contain("Status");
    }

    [Fact]
    public async Task ExportAuditLogToExcelAsync_CreatesWorkbook()
    {
        string filePath = GetTempFilePath("audit.xlsx");
        List<AuditLogEntry> entries = CreateSampleAuditEntries();

        await _service.ExportAuditLogToExcelAsync(entries, filePath, CancellationToken.None);

        File.Exists(filePath).Should().BeTrue();
        using var workbook = new XLWorkbook(filePath);
        IXLWorksheet worksheet = workbook.Worksheet("Audit Log");
        worksheet.Should().NotBeNull();
        worksheet.Cell(1, 1).Value.ToString(CultureInfo.InvariantCulture).Should().Be("Id");
        worksheet.Cell(2, 5).Value.ToString(CultureInfo.InvariantCulture).Should().Be("Get-Process");
    }

    [Fact]
    public async Task ExportToCsvAsync_NullResults_ThrowsArgumentNull()
    {
        string filePath = GetTempFilePath("null.csv");

        Func<Task> act = () => _service.ExportToCsvAsync(null!, filePath, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportToExcelAsync_NullResults_ThrowsArgumentNull()
    {
        string filePath = GetTempFilePath("null.xlsx");

        Func<Task> act = () => _service.ExportToExcelAsync(null!, filePath, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportToCsvAsync_SpecialCharacters_HandledCorrectly()
    {
        string filePath = GetTempFilePath("special.csv");
        List<HostResult> results =
        [
            new()
            {
                Hostname = "HOST-SPECIAL",
                Status = HostStatus.Success,
                Output = "Output with \"quotes\" and, commas",
                Duration = TimeSpan.FromSeconds(1),
                CompletedAt = DateTime.UtcNow
            }
        ];

        await _service.ExportToCsvAsync(results, filePath, CancellationToken.None);

        string content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("HOST-SPECIAL");
        content.Should().Contain("quotes");
    }

    [Fact]
    public async Task ExportAdObjectsToCsvAsync_WritesSelectedColumns()
    {
        string filePath = GetTempFilePath("ad_objects.csv");
        List<AdObject> objects = CreateSampleAdObjects();
        List<string> columns = ["Name", "ObjectClass", "DistinguishedName"];

        await _service.ExportAdObjectsToCsvAsync(objects, filePath, columns, CancellationToken.None);

        File.Exists(filePath).Should().BeTrue();
        string[] lines = await File.ReadAllLinesAsync(filePath);
        lines.Should().HaveCountGreaterThanOrEqualTo(3);
        lines[0].Should().Contain("Name");
        lines[0].Should().Contain("ObjectClass");
        lines[0].Should().Contain("DistinguishedName");
        lines[1].Should().Contain("User1");
    }

    [Fact]
    public async Task ExportAdObjectsToExcelAsync_WritesSelectedColumns()
    {
        string filePath = GetTempFilePath("ad_objects.xlsx");
        List<AdObject> objects = CreateSampleAdObjects();
        List<string> columns = ["Name", "ObjectClass", "mail"];

        await _service.ExportAdObjectsToExcelAsync(objects, filePath, columns, CancellationToken.None);

        File.Exists(filePath).Should().BeTrue();
        using var workbook = new XLWorkbook(filePath);
        IXLWorksheet worksheet = workbook.Worksheets.First();
        worksheet.Cell(1, 1).GetText().Should().Be("Name");
        worksheet.Cell(1, 2).GetText().Should().Be("ObjectClass");
        worksheet.Cell(1, 3).GetText().Should().Be("mail");
        worksheet.Cell(2, 1).GetText().Should().Be("User1");
        worksheet.Cell(2, 3).GetText().Should().Be("user1@test.local");
    }

    [Fact]
    public async Task ExportAdObjectsToCsvAsync_FallsBackToAttributesDictionary()
    {
        string filePath = GetTempFilePath("ad_attrs.csv");
        List<AdObject> objects = CreateSampleAdObjects();
        List<string> columns = ["Name", "mail", "department"];

        await _service.ExportAdObjectsToCsvAsync(objects, filePath, columns, CancellationToken.None);

        string content = await File.ReadAllTextAsync(filePath);
        content.Should().Contain("user1@test.local");
        content.Should().Contain("IT");
    }

    private string GetTempFilePath(string fileName) =>
        Path.Combine(_tempDir, fileName);

    private static List<HostResult> CreateSampleHostResults() =>
    [
        new()
        {
            Hostname = "HOST-01",
            Status = HostStatus.Success,
            Output = "ProcessId: 1234",
            Duration = TimeSpan.FromMilliseconds(500),
            CompletedAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        },
        new()
        {
            Hostname = "HOST-02",
            Status = HostStatus.Failed,
            Output = string.Empty,
            ErrorOutput = "Connection refused",
            Duration = TimeSpan.FromMilliseconds(5000),
            CompletedAt = new DateTime(2024, 1, 15, 10, 0, 5, DateTimeKind.Utc)
        }
    ];

    private static List<AuditLogEntry> CreateSampleAuditEntries() =>
    [
        new()
        {
            Id = 1,
            Timestamp = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            UserName = "admin@domain.local",
            MachineName = "WORKSTATION-01",
            ScriptName = "Get-Process",
            TargetHosts = "HOST-01,HOST-02",
            TargetHostCount = 2,
            SuccessCount = 1,
            FailureCount = 1,
            Status = ExecutionStatus.PartialFailure,
            Duration = TimeSpan.FromSeconds(5),
            ErrorSummary = "HOST-02: Connection refused"
        }
    ];

    private static List<AdObject> CreateSampleAdObjects() =>
    [
        new()
        {
            Name = "User1",
            DistinguishedName = "CN=User1,DC=test,DC=local",
            ObjectClass = "user",
            DisplayName = "User One",
            Attributes = new Dictionary<string, object?>
            {
                ["mail"] = "user1@test.local",
                ["department"] = "IT"
            }
        },
        new()
        {
            Name = "PC1",
            DistinguishedName = "CN=PC1,DC=test,DC=local",
            ObjectClass = "computer",
            Attributes = new Dictionary<string, object?>
            {
                ["operatingSystem"] = "Windows 11"
            }
        }
    ];
}
