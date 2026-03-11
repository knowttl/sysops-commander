using FluentAssertions;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Infrastructure.Database;

namespace SysOpsCommander.Tests.Infrastructure;

public sealed class SettingsRepositoryTests : IAsyncLifetime, IDisposable
{
    private readonly string _connectionString = $"Data Source=SettingsTests_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private Microsoft.Data.Sqlite.SqliteConnection _keepAliveConnection = null!;
    private SettingsRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _keepAliveConnection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await _keepAliveConnection.OpenAsync();

        var dbInitializer = new DatabaseInitializer(_connectionString);
        await dbInitializer.InitializeAsync();
        _repository = new SettingsRepository(dbInitializer);
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _keepAliveConnection?.Dispose();

    [Fact]
    public async Task GetValueAsync_ExistingKey_ReturnsValue()
    {
        await _repository.SetValueAsync("theme", "dark", CancellationToken.None);

        string? result = await _repository.GetValueAsync("theme", CancellationToken.None);

        result.Should().Be("dark");
    }

    [Fact]
    public async Task GetValueAsync_MissingKey_ReturnsNull()
    {
        string? result = await _repository.GetValueAsync("nonexistent", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetValueAsync_NewKey_InsertsValue()
    {
        await _repository.SetValueAsync("newkey", "newvalue", CancellationToken.None);

        string? result = await _repository.GetValueAsync("newkey", CancellationToken.None);
        result.Should().Be("newvalue");
    }

    [Fact]
    public async Task SetValueAsync_ExistingKey_UpdatesValue()
    {
        await _repository.SetValueAsync("key1", "original", CancellationToken.None);
        await _repository.SetValueAsync("key1", "updated", CancellationToken.None);

        string? result = await _repository.GetValueAsync("key1", CancellationToken.None);
        result.Should().Be("updated");
    }

    [Fact]
    public async Task GetAllAsync_MultipleSettings_ReturnsAll()
    {
        await _repository.SetValueAsync("key1", "val1", CancellationToken.None);
        await _repository.SetValueAsync("key2", "val2", CancellationToken.None);

        IReadOnlyList<UserSettings> all = await _repository.GetAllAsync(CancellationToken.None);

        all.Should().HaveCount(2);
    }
}
