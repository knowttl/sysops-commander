using FluentAssertions;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class AdObjectRowTests
{
    private static AdObject CreateComputerObject(string name = "PC1", string? dnsHostName = "pc1.test.local") =>
        new()
        {
            Name = name,
            DistinguishedName = $"CN={name},OU=Computers,DC=test,DC=local",
            ObjectClass = "computer",
            Attributes = dnsHostName is not null
                ? new Dictionary<string, object?> { ["dNSHostName"] = dnsHostName }
                : []
        };

    private static AdObject CreateUserObject(string name = "User1") =>
        new()
        {
            Name = name,
            DistinguishedName = $"CN={name},OU=Users,DC=test,DC=local",
            ObjectClass = "user"
        };

    [Fact]
    public void Constructor_SetsAdObject()
    {
        AdObject adObject = CreateComputerObject();
        AdObjectRow row = new(adObject);

        row.AdObject.Should().BeSameAs(adObject);
    }

    [Fact]
    public void PassthroughProperties_ReturnAdObjectValues()
    {
        AdObject adObject = CreateComputerObject("Server1");
        AdObjectRow row = new(adObject);

        row.Name.Should().Be("Server1");
        row.ObjectClass.Should().Be("computer");
        row.DistinguishedName.Should().Be("CN=Server1,OU=Computers,DC=test,DC=local");
    }

    [Fact]
    public void IsComputer_ComputerClass_ReturnsTrue()
    {
        var row = new AdObjectRow(CreateComputerObject());
        row.IsComputer.Should().BeTrue();
    }

    [Fact]
    public void IsComputer_UserClass_ReturnsFalse()
    {
        var row = new AdObjectRow(CreateUserObject());
        row.IsComputer.Should().BeFalse();
    }

    [Fact]
    public void IsComputer_CaseInsensitive_ReturnsTrue()
    {
        var adObject = new AdObject
        {
            Name = "PC1",
            DistinguishedName = "CN=PC1,DC=test,DC=local",
            ObjectClass = "Computer"
        };
        var row = new AdObjectRow(adObject);
        row.IsComputer.Should().BeTrue();
    }

    [Fact]
    public void DnsHostName_ExtractsFromAttributes()
    {
        var row = new AdObjectRow(CreateComputerObject(dnsHostName: "server1.test.local"));
        row.DnsHostName.Should().Be("server1.test.local");
    }

    [Fact]
    public void DnsHostName_MissingAttribute_ReturnsNull()
    {
        var row = new AdObjectRow(CreateComputerObject(dnsHostName: null));
        row.DnsHostName.Should().BeNull();
    }

    [Fact]
    public void DnsHostName_UserObject_ReturnsNull()
    {
        var row = new AdObjectRow(CreateUserObject());
        row.DnsHostName.Should().BeNull();
    }

    [Fact]
    public void UpdateFromResolutionResult_Resolved_SetsIpAndTooltip()
    {
        var row = new AdObjectRow(CreateComputerObject());

        row.UpdateFromResolutionResult(new IpResolutionResult
        {
            Status = IpResolutionStatus.Resolved,
            PrimaryIPv4 = "10.0.1.50",
            AllAddresses = ["10.0.1.50", "fe80::1"],
            Hostname = "pc1.test.local"
        });

        row.IpAddress.Should().Be("10.0.1.50");
        row.IpTooltip.Should().Be("pc1.test.local");
        row.AllIpAddresses.Should().HaveCount(2);
        row.IpResolutionStatus.Should().Be(IpResolutionStatus.Resolved);
    }

    [Fact]
    public void UpdateFromResolutionResult_Failed_SetsNAAndErrorTooltip()
    {
        var row = new AdObjectRow(CreateComputerObject());

        row.UpdateFromResolutionResult(new IpResolutionResult
        {
            Status = IpResolutionStatus.Failed,
            ErrorMessage = "No such host is known",
            Hostname = "pc1.test.local"
        });

        row.IpAddress.Should().Be("N/A");
        row.IpTooltip.Should().Be("No such host is known");
        row.AllIpAddresses.Should().BeNull();
        row.IpResolutionStatus.Should().Be(IpResolutionStatus.Failed);
    }

    [Fact]
    public void UpdateFromResolutionResult_Resolving_SetsResolvingText()
    {
        var row = new AdObjectRow(CreateComputerObject());

        row.UpdateFromResolutionResult(new IpResolutionResult
        {
            Status = IpResolutionStatus.Resolving,
            Hostname = "pc1.test.local"
        });

        row.IpAddress.Should().Be("Resolving...");
        row.IpTooltip.Should().BeNull();
        row.AllIpAddresses.Should().BeNull();
        row.IpResolutionStatus.Should().Be(IpResolutionStatus.Resolving);
    }

    [Fact]
    public void UpdateFromResolutionResult_NotStarted_ClearsAll()
    {
        var row = new AdObjectRow(CreateComputerObject());

        row.UpdateFromResolutionResult(new IpResolutionResult
        {
            Status = IpResolutionStatus.NotStarted
        });

        row.IpAddress.Should().BeNull();
        row.IpTooltip.Should().BeNull();
        row.AllIpAddresses.Should().BeNull();
    }

    [Fact]
    public void UpdateFromResolutionResult_Resolved_NoPrimaryIPv4_FallsBackToFirstAddress()
    {
        var row = new AdObjectRow(CreateComputerObject());

        row.UpdateFromResolutionResult(new IpResolutionResult
        {
            Status = IpResolutionStatus.Resolved,
            PrimaryIPv4 = null,
            AllAddresses = ["fe80::1"],
            Hostname = "pc1.test.local"
        });

        row.IpAddress.Should().Be("fe80::1");
    }

    [Fact]
    public void PropertyChanged_FiredOnIpUpdate()
    {
        var row = new AdObjectRow(CreateComputerObject());
        var changedProperties = new List<string>();
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };

        row.UpdateFromResolutionResult(new IpResolutionResult
        {
            Status = IpResolutionStatus.Resolved,
            PrimaryIPv4 = "10.0.1.50",
            AllAddresses = ["10.0.1.50"],
            Hostname = "pc1.test.local"
        });

        changedProperties.Should().Contain("IpAddress");
        changedProperties.Should().Contain("IpTooltip");
        changedProperties.Should().Contain("IpResolutionStatus");
        changedProperties.Should().Contain("AllIpAddresses");
    }

    [Fact]
    public void Constructor_NullAdObject_ThrowsArgumentNull()
    {
        Action act = () => _ = new AdObjectRow(null!);
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("adObject");
    }

    [Fact]
    public void UpdateFromResolutionResult_NullResult_ThrowsArgumentNull()
    {
        var row = new AdObjectRow(CreateComputerObject());
        Action act = () => row.UpdateFromResolutionResult(null!);
        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("result");
    }
}
