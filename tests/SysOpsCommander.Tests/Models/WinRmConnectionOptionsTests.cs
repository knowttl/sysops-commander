using FluentAssertions;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Tests.Models;

public sealed class WinRmConnectionOptionsTests
{
    [Fact]
    public void GetEffectivePort_HttpNoCustomPort_Returns5985()
    {
        WinRmConnectionOptions options = new()
        {
            Transport = WinRmTransport.HTTP,
            CustomPort = null
        };

        options.GetEffectivePort().Should().Be(AppConstants.WinRmHttpPort);
    }

    [Fact]
    public void GetEffectivePort_HttpsNoCustomPort_Returns5986()
    {
        WinRmConnectionOptions options = new()
        {
            Transport = WinRmTransport.HTTPS,
            CustomPort = null
        };

        options.GetEffectivePort().Should().Be(AppConstants.WinRmHttpsPort);
    }

    [Fact]
    public void GetEffectivePort_CustomPort_ReturnsCustom()
    {
        WinRmConnectionOptions options = new()
        {
            Transport = WinRmTransport.HTTP,
            CustomPort = 8080
        };

        options.GetEffectivePort().Should().Be(8080);
    }

    [Fact]
    public void CreateDefault_ReturnsKerberosHttp()
    {
        var defaults = WinRmConnectionOptions.CreateDefault();

        defaults.AuthMethod.Should().Be(WinRmAuthMethod.Kerberos);
        defaults.Transport.Should().Be(WinRmTransport.HTTP);
        defaults.CustomPort.Should().BeNull();
        defaults.ShellUri.Should().BeNull();
    }
}
