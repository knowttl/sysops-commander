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

    /// <summary>
    /// Minimal implementation of <see cref="ILogEventPropertyValueFactory"/> for unit testing destructuring policies.
    /// </summary>
    private sealed class FakePropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false) =>
            new ScalarValue(value);
    }
}
