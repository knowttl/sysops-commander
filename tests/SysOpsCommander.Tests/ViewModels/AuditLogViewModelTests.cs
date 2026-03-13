using System.Collections.ObjectModel;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class AuditLogViewModelTests : IDisposable
{
    private readonly IAuditLogService _auditLogService = Substitute.For<IAuditLogService>();
    private readonly IExportService _exportService = Substitute.For<IExportService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly AuditLogViewModel _viewModel;

    public AuditLogViewModelTests()
    {
        _auditLogService.QueryAsync(Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _viewModel = new AuditLogViewModel(
            _auditLogService,
            _exportService,
            _dialogService,
            _logger);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task LoadAuditLog_PopulatesEntries()
    {
        List<AuditLogEntry> entries =
        [
            new() { Id = 1, ScriptName = "Get-InstalledSoftware", Timestamp = DateTime.UtcNow },
            new() { Id = 2, ScriptName = "Test-WinRM", Timestamp = DateTime.UtcNow }
        ];

        _auditLogService.QueryAsync(Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>())
            .Returns(entries);

        await _viewModel.LoadAuditLogCommand.ExecuteAsync(null);

        _viewModel.AuditEntries.Should().HaveCount(2);
        _viewModel.TotalEntries.Should().Be(2);
        _viewModel.StatusMessage.Should().Contain("2 entries");
    }

    [Fact]
    public async Task LoadAuditLog_PassesFilterCriteria()
    {
        _viewModel.FilterScriptName = "Get-InstalledSoftware";
        _viewModel.FilterHostname = "server01";
        _viewModel.FilterDomain = "corp.local";
        _viewModel.FilterAuthMethod = WinRmAuthMethod.Kerberos;
        _viewModel.FilterUserName = "admin";

        await _viewModel.LoadAuditLogCommand.ExecuteAsync(null);

        await _auditLogService.Received(1).QueryAsync(
            Arg.Is<AuditLogFilter>(f =>
                f.ScriptName == "Get-InstalledSoftware" &&
                f.Hostname == "server01" &&
                f.TargetDomain == "corp.local" &&
                f.AuthMethod == WinRmAuthMethod.Kerberos &&
                f.UserName == "admin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearFilters_ResetsAllFilterValues()
    {
        _viewModel.FilterScriptName = "Test";
        _viewModel.FilterHostname = "host";
        _viewModel.FilterDomain = "domain";
        _viewModel.FilterAuthMethod = WinRmAuthMethod.NTLM;
        _viewModel.FilterUserName = "user";
        _viewModel.FilterStartDate = DateTime.Today.AddDays(-30);
        _viewModel.FilterEndDate = DateTime.Today;

        await _viewModel.ClearFiltersCommand.ExecuteAsync(null);

        _viewModel.FilterScriptName.Should().BeEmpty();
        _viewModel.FilterHostname.Should().BeEmpty();
        _viewModel.FilterDomain.Should().BeEmpty();
        _viewModel.FilterAuthMethod.Should().BeNull();
        _viewModel.FilterUserName.Should().BeEmpty();
        _viewModel.FilterStartDate.Should().BeNull();
        _viewModel.FilterEndDate.Should().BeNull();
        _viewModel.CurrentPage.Should().Be(1);
    }

    [Fact]
    public async Task ExportAuditLog_CallsExportService()
    {
        _viewModel.AuditEntries = new ObservableCollection<AuditLogEntry>(
            [new() { Id = 1, ScriptName = "Test" }]);

        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\export\\audit.csv");

        await _viewModel.ExportAuditLogCommand.ExecuteAsync(null);

        await _exportService.Received(1).ExportAuditLogToCsvAsync(
            Arg.Any<IEnumerable<AuditLogEntry>>(),
            "C:\\export\\audit.csv",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAuditLog_ExcelExtension_CallsExcelExport()
    {
        _viewModel.AuditEntries = new ObservableCollection<AuditLogEntry>(
            [new() { Id = 1, ScriptName = "Test" }]);

        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\export\\audit.xlsx");

        await _viewModel.ExportAuditLogCommand.ExecuteAsync(null);

        await _exportService.Received(1).ExportAuditLogToExcelAsync(
            Arg.Any<IEnumerable<AuditLogEntry>>(),
            "C:\\export\\audit.xlsx",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAuditLog_WhenCancelled_DoesNotExport()
    {
        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null);

        await _viewModel.ExportAuditLogCommand.ExecuteAsync(null);

        await _exportService.DidNotReceive().ExportAuditLogToCsvAsync(
            Arg.Any<IEnumerable<AuditLogEntry>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeOldEntries_WithConfirmation_PurgesEntries()
    {
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        _auditLogService.PurgeOldEntriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(42);

        await _viewModel.PurgeOldEntriesCommand.ExecuteAsync(null);

        await _auditLogService.Received(1).PurgeOldEntriesAsync(
            AppConstants.AuditLogRetentionDays,
            Arg.Any<CancellationToken>());
        _viewModel.StatusMessage.Should().Contain("42");
    }

    [Fact]
    public async Task PurgeOldEntries_WithoutConfirmation_DoesNotPurge()
    {
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        await _viewModel.PurgeOldEntriesCommand.ExecuteAsync(null);

        await _auditLogService.DidNotReceive().PurgeOldEntriesAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectedEntry_SetsDetailText()
    {
        AuditLogEntry entry = new()
        {
            ScriptName = "Get-InstalledSoftware",
            Status = ExecutionStatus.Completed,
            TargetHosts = "HOST01,HOST02",
            TargetHostCount = 2,
            SuccessCount = 2,
            FailureCount = 0,
            Duration = TimeSpan.FromSeconds(8.2),
            AuthMethod = WinRmAuthMethod.Kerberos,
            Transport = WinRmTransport.HTTP,
            TargetDomain = "corp.local",
            UserName = "admin",
            MachineName = "WORKSTATION01",
            Timestamp = new DateTime(2026, 3, 10, 14, 30, 0, DateTimeKind.Utc)
        };

        _viewModel.SelectedEntry = entry;

        _viewModel.SelectedEntryDetail.Should().Contain("Get-InstalledSoftware");
        _viewModel.SelectedEntryDetail.Should().Contain("HOST01");
        _viewModel.SelectedEntryDetail.Should().Contain("2/2 succeeded");
        _viewModel.SelectedEntryDetail.Should().Contain("Kerberos");
    }

    [Fact]
    public void SelectedEntry_WhenNull_ClearsDetail()
    {
        _viewModel.SelectedEntry = new AuditLogEntry { ScriptName = "Test" };
        _viewModel.SelectedEntry = null;

        _viewModel.SelectedEntryDetail.Should().BeEmpty();
    }

    [Fact]
    public async Task NextPage_IncrementsAndReloads()
    {
        _viewModel.TotalPages = 5;
        _viewModel.CurrentPage = 2;

        await _viewModel.NextPageCommand.ExecuteAsync(null);

        _viewModel.CurrentPage.Should().Be(3);
    }

    [Fact]
    public async Task NextPage_AtLastPage_DoesNotIncrement()
    {
        _viewModel.TotalPages = 3;
        _viewModel.CurrentPage = 3;

        await _viewModel.NextPageCommand.ExecuteAsync(null);

        _viewModel.CurrentPage.Should().Be(3);
    }

    [Fact]
    public async Task PreviousPage_DecrementsAndReloads()
    {
        _viewModel.CurrentPage = 3;

        await _viewModel.PreviousPageCommand.ExecuteAsync(null);

        _viewModel.CurrentPage.Should().Be(2);
    }

    [Fact]
    public async Task PreviousPage_AtFirstPage_DoesNotDecrement()
    {
        _viewModel.CurrentPage = 1;

        await _viewModel.PreviousPageCommand.ExecuteAsync(null);

        _viewModel.CurrentPage.Should().Be(1);
    }
}
