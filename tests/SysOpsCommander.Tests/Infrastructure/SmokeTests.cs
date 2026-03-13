using System.Net;
using System.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;
using SysOpsCommander.App.DependencyInjection;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;
using SysOpsCommander.Infrastructure.Database;
using SysOpsCommander.Infrastructure.Logging;

namespace SysOpsCommander.Tests.Infrastructure;

/// <summary>
/// Smoke tests verifying Phase 0 foundation: DI container, Serilog destructuring, and configuration binding.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void DiContainer_Builds_WithoutErrors()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        ServiceCollection services = new();
        _ = services.AddSysOpsInfrastructure(configuration);
        _ = services.AddSysOpsServices();
        _ = services.AddSysOpsViewModels();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.Should().NotBeNull();
    }

    [Fact]
    public void CredentialDestructuringPolicy_SecureString_ReturnsRedacted()
    {
        CredentialDestructuringPolicy policy = new();
        SecureString secureString = new();
        secureString.AppendChar('x');

        bool handled = policy.TryDestructure(
            secureString,
            new FakePropertyValueFactory(),
            out LogEventPropertyValue? result);

        handled.Should().BeTrue();
        result.Should().NotBeNull();
        result!.ToString().Should().Contain("[REDACTED]");
    }

    [Fact]
    public void CredentialDestructuringPolicy_NetworkCredential_ReturnsRedacted()
    {
        CredentialDestructuringPolicy policy = new();
        NetworkCredential credential = new("user", "password", "domain");

        bool handled = policy.TryDestructure(
            credential,
            new FakePropertyValueFactory(),
            out LogEventPropertyValue? result);

        handled.Should().BeTrue();
        result.Should().NotBeNull();
        result!.ToString().Should().Contain("[REDACTED]");
    }

    [Fact]
    public void AppConfiguration_BindsFromJson_DefaultValues()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        AppConfiguration appConfig = new();
        configuration.GetSection("SysOpsCommander").Bind(appConfig);

        appConfig.DefaultThrottle.Should().Be(5);
        appConfig.DefaultWinRmTransport.Should().Be("HTTP");
        appConfig.DefaultWinRmAuthMethod.Should().Be("Kerberos");
        appConfig.DefaultTimeoutSeconds.Should().Be(60);
        appConfig.StaleComputerThresholdDays.Should().Be(90);
        appConfig.AuditLogRetentionDays.Should().Be(365);
    }

    [Fact]
    public void HostTargetingService_Registration_IsSingleton()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        ServiceCollection services = new();
        _ = services.AddSysOpsInfrastructure(configuration);
        _ = services.AddSysOpsServices();
        _ = services.AddSysOpsViewModels();

        ServiceProvider provider = services.BuildServiceProvider();

        IHostTargetingService first = provider.GetRequiredService<IHostTargetingService>();
        IHostTargetingService second = provider.GetRequiredService<IHostTargetingService>();

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void DI_AllCoreServicesResolvable()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        ServiceCollection services = new();
        _ = services.AddSysOpsInfrastructure(configuration);
        _ = services.AddSysOpsServices();
        _ = services.AddSysOpsViewModels();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHostTargetingService>().Should().NotBeNull();
        provider.GetRequiredService<ISettingsService>().Should().NotBeNull();
        provider.GetRequiredService<IScriptValidationService>().Should().NotBeNull();
        provider.GetRequiredService<IRemoteExecutionService>().Should().NotBeNull();
        provider.GetRequiredService<ICredentialService>().Should().NotBeNull();
        provider.GetRequiredService<IAuditLogService>().Should().NotBeNull();
        provider.GetRequiredService<IScriptLoaderService>().Should().NotBeNull();
        provider.GetRequiredService<IExportService>().Should().NotBeNull();
        provider.GetRequiredService<IAutoUpdateService>().Should().NotBeNull();
        provider.GetRequiredService<DatabaseInitializer>().Should().NotBeNull();
    }

    [Fact]
    public async Task DatabaseInitializer_CreatesSchema_WithIndexes()
    {
        string connStr = "Data Source=SmokeTest_Schema;Mode=Memory;Cache=Shared";
        DatabaseInitializer initializer = new(connStr);

        // Keep a connection open so the shared in-memory DB persists
        using Microsoft.Data.Sqlite.SqliteConnection keepAlive = new(connStr);
        await keepAlive.OpenAsync();

        await initializer.InitializeAsync();

        using Microsoft.Data.Sqlite.SqliteCommand cmd = keepAlive.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using System.Data.Common.DbDataReader reader = await cmd.ExecuteReaderAsync();

        List<string> tables = [];
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        tables.Should().Contain("AuditLog");
        tables.Should().Contain("UserSettings");
        tables.Should().Contain("SchemaVersion");

        // Verify indexes exist
        reader.Close();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'IX_%'";
        using System.Data.Common.DbDataReader indexReader = await cmd.ExecuteReaderAsync();

        List<string> indexes = [];
        while (await indexReader.ReadAsync())
        {
            indexes.Add(indexReader.GetString(0));
        }

        indexes.Should().Contain("IX_AuditLog_Timestamp");
        indexes.Should().Contain("IX_AuditLog_ScriptName");
        indexes.Should().Contain("IX_AuditLog_UserName");
        indexes.Should().Contain("IX_AuditLog_Status");
        indexes.Should().Contain("IX_AuditLog_CorrelationId");
    }

    [Fact]
    public async Task SettingsService_RoundTrip_PersistsAndRetrieves()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        string connStr = "Data Source=SmokeTest_Settings;Mode=Memory;Cache=Shared";
        DatabaseInitializer initializer = new(connStr);

        // Keep a connection open so the shared in-memory DB persists
        using Microsoft.Data.Sqlite.SqliteConnection keepAlive = new(connStr);
        await keepAlive.OpenAsync();

        await initializer.InitializeAsync();

        ServiceCollection services = new();
        _ = services.AddSysOpsInfrastructure(configuration);
        _ = services.AddSysOpsServices();
        _ = services.AddSingleton(initializer);

        ServiceProvider provider = services.BuildServiceProvider();
        ISettingsService settingsService = provider.GetRequiredService<ISettingsService>();

        await settingsService.SetAsync("TestKey", "TestValue", CancellationToken.None);
        string result = await settingsService.GetEffectiveAsync("TestKey", CancellationToken.None);

        result.Should().Be("TestValue");
    }

    [Theory]
    [InlineData("|whoami")]
    [InlineData(";rm -rf /")]
    [InlineData("&calc")]
    [InlineData("$env:SECRET")]
    [InlineData("`command`")]
    [InlineData("\"injected\"")]
    [InlineData("'injected'")]
    [InlineData("<script>")]
    [InlineData(">output")]
    public void HostnameValidator_SecurityMatrix_RejectsInjectionPatterns(string malicious)
    {
        ValidationResult result = HostnameValidator.Validate(malicious);
        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Minimal implementation of <see cref="ILogEventPropertyValueFactory"/> for unit testing destructuring policies.
    /// </summary>
    private sealed class FakePropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false) =>
            new ScalarValue(value);
    }
}
