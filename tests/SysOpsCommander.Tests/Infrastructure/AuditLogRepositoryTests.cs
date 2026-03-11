using FluentAssertions;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Infrastructure.Database;

namespace SysOpsCommander.Tests.Infrastructure;

public sealed class AuditLogRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly string _connectionString = $"Data Source=AuditLogTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private Microsoft.Data.Sqlite.SqliteConnection _keepAliveConnection = null!;
    private AuditLogRepository _repository = null!;

    public async Task InitializeAsync()
    {
        // Sentinel connection keeps the shared in-memory DB alive across multiple connections
        _keepAliveConnection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await _keepAliveConnection.OpenAsync();

        var dbInitializer = new DatabaseInitializer(_connectionString);
        await dbInitializer.InitializeAsync();
        _repository = new AuditLogRepository(dbInitializer);
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _keepAliveConnection?.Dispose();

    [Fact]
    public async Task InsertAsync_ValidEntry_Persists()
    {
        AuditLogEntry entry = CreateSampleEntry();

        await _repository.InsertAsync(entry, CancellationToken.None);
        IReadOnlyList<AuditLogEntry> results = await _repository.QueryAsync(new AuditLogFilter(), CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ScriptName.Should().Be("Test-Script");
        results[0].UserName.Should().Be("testuser");
        results[0].Status.Should().Be(ExecutionStatus.Completed);
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange_ReturnsMatchingEntries()
    {
        AuditLogEntry oldEntry = CreateSampleEntry();
        oldEntry.Timestamp = DateTime.UtcNow.AddDays(-10);
        oldEntry.ScriptName = "Old-Script";

        AuditLogEntry newEntry = CreateSampleEntry();
        newEntry.Timestamp = DateTime.UtcNow;
        newEntry.ScriptName = "New-Script";

        await _repository.InsertAsync(oldEntry, CancellationToken.None);
        await _repository.InsertAsync(newEntry, CancellationToken.None);

        AuditLogFilter filter = new()
        {
            StartDate = DateTime.UtcNow.AddDays(-5)
        };

        IReadOnlyList<AuditLogEntry> results = await _repository.QueryAsync(filter, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ScriptName.Should().Be("New-Script");
    }

    [Fact]
    public async Task QueryAsync_FilterByAuthMethod_ReturnsMatchingEntries()
    {
        AuditLogEntry kerberosEntry = CreateSampleEntry();
        kerberosEntry.AuthMethod = WinRmAuthMethod.Kerberos;

        AuditLogEntry ntlmEntry = CreateSampleEntry();
        ntlmEntry.AuthMethod = WinRmAuthMethod.NTLM;
        ntlmEntry.ScriptName = "NTLM-Script";

        await _repository.InsertAsync(kerberosEntry, CancellationToken.None);
        await _repository.InsertAsync(ntlmEntry, CancellationToken.None);

        AuditLogFilter filter = new()
        {
            AuthMethod = WinRmAuthMethod.NTLM
        };

        IReadOnlyList<AuditLogEntry> results = await _repository.QueryAsync(filter, CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].ScriptName.Should().Be("NTLM-Script");
    }

    [Fact]
    public async Task QueryAsync_NoFilters_ReturnsAllEntries()
    {
        await _repository.InsertAsync(CreateSampleEntry(), CancellationToken.None);
        await _repository.InsertAsync(CreateSampleEntry(), CancellationToken.None);
        await _repository.InsertAsync(CreateSampleEntry(), CancellationToken.None);

        IReadOnlyList<AuditLogEntry> results = await _repository.QueryAsync(new AuditLogFilter(), CancellationToken.None);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_OldEntries_RemovesCorrectCount()
    {
        AuditLogEntry oldEntry = CreateSampleEntry();
        oldEntry.Timestamp = DateTime.UtcNow.AddDays(-100);

        AuditLogEntry recentEntry = CreateSampleEntry();
        recentEntry.Timestamp = DateTime.UtcNow;

        await _repository.InsertAsync(oldEntry, CancellationToken.None);
        await _repository.InsertAsync(recentEntry, CancellationToken.None);

        int deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-50), CancellationToken.None);

        deleted.Should().Be(1);
        IReadOnlyList<AuditLogEntry> remaining = await _repository.QueryAsync(new AuditLogFilter(), CancellationToken.None);
        remaining.Should().HaveCount(1);
    }

    private static AuditLogEntry CreateSampleEntry() =>
        new()
        {
            Timestamp = DateTime.UtcNow,
            UserName = "testuser",
            MachineName = "WORKSTATION1",
            ScriptName = "Test-Script",
            TargetHosts = "host1,host2",
            TargetHostCount = 2,
            SuccessCount = 2,
            FailureCount = 0,
            Status = ExecutionStatus.Completed,
            Duration = TimeSpan.FromSeconds(30),
            AuthMethod = WinRmAuthMethod.Kerberos,
            Transport = WinRmTransport.HTTP,
            TargetDomain = "corp.contoso.com",
            CorrelationId = Guid.NewGuid()
        };
}
