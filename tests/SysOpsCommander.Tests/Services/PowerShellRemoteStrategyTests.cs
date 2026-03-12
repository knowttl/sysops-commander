using System.Management.Automation.Runspaces;
using FluentAssertions;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Services.Strategies;

namespace SysOpsCommander.Tests.Services;

/// <summary>
/// Tests for <see cref="PowerShellRemoteStrategy"/> connection configuration logic.
/// Full execution tests require WinRM connectivity — these verify setup via BuildConnectionInfo.
/// </summary>
public sealed class PowerShellRemoteStrategyTests
{
    [Fact]
    public void BuildConnectionInfo_KerberosHttp_CorrectConnectionInfo()
    {
        WinRmConnectionOptions options = new()
        {
            AuthMethod = WinRmAuthMethod.Kerberos,
            Transport = WinRmTransport.HTTP
        };

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "SERVER01", null, options, 60);

        result.AuthenticationMechanism.Should().Be(AuthenticationMechanism.Kerberos);
        result.Port.Should().Be(5985);
        result.Scheme.Should().Be("http");
    }

    [Fact]
    public void BuildConnectionInfo_NtlmHttps_CorrectConnectionInfo()
    {
        WinRmConnectionOptions options = new()
        {
            AuthMethod = WinRmAuthMethod.NTLM,
            Transport = WinRmTransport.HTTPS
        };

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "SERVER02", null, options, 60);

        result.AuthenticationMechanism.Should().Be(AuthenticationMechanism.Negotiate);
        result.Port.Should().Be(5986);
        result.Scheme.Should().Be("https");
    }

    [Fact]
    public void BuildConnectionInfo_CredSspHttp_CorrectConnectionInfo()
    {
        WinRmConnectionOptions options = new()
        {
            AuthMethod = WinRmAuthMethod.CredSSP,
            Transport = WinRmTransport.HTTP
        };

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "SERVER03", null, options, 60);

        result.AuthenticationMechanism.Should().Be(AuthenticationMechanism.Credssp);
        result.Port.Should().Be(5985);
    }

    [Fact]
    public void BuildConnectionInfo_CustomPort_UsesCustomPort()
    {
        WinRmConnectionOptions options = new()
        {
            AuthMethod = WinRmAuthMethod.Kerberos,
            Transport = WinRmTransport.HTTP,
            CustomPort = 9999
        };

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "SERVER04", null, options, 60);

        result.Port.Should().Be(9999);
    }

    [Fact]
    public void BuildConnectionInfo_Timeout_ConvertedToMilliseconds()
    {
        var options = WinRmConnectionOptions.CreateDefault();

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "SERVER05", null, options, 120);

        result.OperationTimeout.Should().Be(120_000);
        result.OpenTimeout.Should().Be(30_000);
    }

    [Fact]
    public void BuildConnectionInfo_DefaultShellUri_UsesDefaultPowerShellShell()
    {
        var options = WinRmConnectionOptions.CreateDefault();

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "SERVER06", null, options, 60);

        result.ShellUri.Should().Be("http://schemas.microsoft.com/powershell/Microsoft.PowerShell");
    }

    [Fact]
    public void BuildConnectionInfo_CustomShellUri_UsesCustomShell()
    {
        WinRmConnectionOptions options = new()
        {
            AuthMethod = WinRmAuthMethod.Kerberos,
            Transport = WinRmTransport.HTTP,
            ShellUri = "http://schemas.microsoft.com/powershell/Custom.Shell"
        };

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "SERVER07", null, options, 60);

        result.ShellUri.Should().Be("http://schemas.microsoft.com/powershell/Custom.Shell");
    }

    [Fact]
    public void BuildConnectionInfo_HostnamePassedThrough()
    {
        var options = WinRmConnectionOptions.CreateDefault();

        WSManConnectionInfo result = PowerShellRemoteStrategy.BuildConnectionInfo(
            "web-server.contoso.com", null, options, 60);

        result.ComputerName.Should().Be("web-server.contoso.com");
    }
}
