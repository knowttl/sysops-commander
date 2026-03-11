using FluentAssertions;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.Tests.Validation;

public sealed class HostnameValidatorTests
{
    [Theory]
    [InlineData("SERVER01")]
    [InlineData("WS-PC-01")]
    [InlineData("DC1")]
    public void Validate_ValidNetBios_ReturnsSuccess(string hostname)
    {
        ValidationResult result = HostnameValidator.Validate(hostname);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("server01.corp.contoso.com")]
    [InlineData("dc1.ad.example.org")]
    [InlineData("a.b")]
    public void Validate_ValidFqdn_ReturnsSuccess(string hostname)
    {
        ValidationResult result = HostnameValidator.Validate(hostname);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("192.168.1.100")]
    [InlineData("10.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    public void Validate_ValidIpv4_ReturnsSuccess(string hostname)
    {
        ValidationResult result = HostnameValidator.Validate(hostname);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Validate_EmptyOrWhitespace_ReturnsFailure(string? hostname)
    {
        ValidationResult result = HostnameValidator.Validate(hostname!);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Theory]
    [InlineData("server;ls")]
    [InlineData("server|cmd")]
    [InlineData("server&calc")]
    [InlineData("$env:PATH")]
    [InlineData("host`id")]
    [InlineData("host(name)")]
    public void Validate_InjectionCharacters_ReturnsFailure(string hostname)
    {
        ValidationResult result = HostnameValidator.Validate(hostname);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("disallowed characters");
    }

    [Fact]
    public void Validate_NetBiosTooLong_ReturnsFailure()
    {
        ValidationResult result = HostnameValidator.Validate("ABCDEFGHIJKLMNOP");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("15 characters");
    }

    [Fact]
    public void Validate_NetBiosLeadingHyphen_ReturnsFailure()
    {
        ValidationResult result = HostnameValidator.Validate("-SERVER");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("hyphen");
    }

    [Fact]
    public void Validate_NetBiosAllDigits_ReturnsFailure()
    {
        ValidationResult result = HostnameValidator.ValidateNetBios("12345");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("all digits");
    }

    [Fact]
    public void Validate_FqdnLabelTooLong_ReturnsFailure()
    {
        string longLabel = new('a', 64);
        ValidationResult result = HostnameValidator.Validate($"{longLabel}.contoso.com");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("63 characters");
    }

    [Fact]
    public void Validate_Ipv4OctetOutOfRange_ReturnsFailure()
    {
        ValidationResult result = HostnameValidator.Validate("256.1.1.1");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("out of range");
    }

    [Fact]
    public void Validate_Ipv4LeadingZeros_ReturnsFailure()
    {
        ValidationResult result = HostnameValidator.Validate("01.02.03.04");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("leading zeros");
    }

    [Fact]
    public void ValidateMany_MixedInput_ReturnsCorrectResults()
    {
        string[] hostnames = ["SERVER01", "", "192.168.1.1", "bad;host"];
        IReadOnlyList<(string Hostname, ValidationResult Result)> results = HostnameValidator.ValidateMany(hostnames);

        results.Should().HaveCount(4);
        results[0].Result.IsValid.Should().BeTrue();
        results[1].Result.IsValid.Should().BeFalse();
        results[2].Result.IsValid.Should().BeTrue();
        results[3].Result.IsValid.Should().BeFalse();
    }
}
