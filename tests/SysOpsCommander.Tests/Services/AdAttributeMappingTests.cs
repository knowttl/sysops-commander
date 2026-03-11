using System.Security.Principal;
using FluentAssertions;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class AdAttributeMappingTests
{
    [Fact]
    public void ConvertSid_ValidSidBytes_ReturnsSidString()
    {
        // Well-known SID: S-1-5-18 (Local System)
        var sid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        byte[] sidBytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(sidBytes, 0);

        string result = AdAttributeMapper.ConvertSid(sidBytes);

        result.Should().Be("S-1-5-18");
    }

    [Fact]
    public void ConvertGuid_ValidGuidBytes_ReturnsGuidString()
    {
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        byte[] guidBytes = guid.ToByteArray();

        string result = AdAttributeMapper.ConvertGuid(guidBytes);

        result.Should().Be("12345678-1234-1234-1234-123456789abc");
    }

    [Fact]
    public void ConvertFileTime_ValidFileTime_ReturnsIso8601()
    {
        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        long fileTime = dt.ToFileTimeUtc();

        string result = AdAttributeMapper.ConvertFileTime(fileTime);

        result.Should().Contain("2024-06-15");
    }

    [Fact]
    public void ConvertFileTime_Zero_ReturnsNever()
    {
        string result = AdAttributeMapper.ConvertFileTime(0);

        result.Should().Be("Never");
    }

    [Fact]
    public void ConvertFileTime_MaxValue_ReturnsNever()
    {
        string result = AdAttributeMapper.ConvertFileTime(long.MaxValue);

        result.Should().Be("Never");
    }

    [Fact]
    public void DecodeUacFlags_NormalAccount_ReturnsFlag()
    {
        // 0x0200 = NORMAL_ACCOUNT (512)
        IReadOnlyList<string> flags = AdAttributeMapper.DecodeUacFlags(0x0200);

        flags.Should().ContainSingle().Which.Should().Be("NORMAL_ACCOUNT");
    }

    [Fact]
    public void DecodeUacFlags_DisabledNormalAccount_ReturnsBothFlags()
    {
        // 0x0202 = ACCOUNTDISABLE | NORMAL_ACCOUNT
        IReadOnlyList<string> flags = AdAttributeMapper.DecodeUacFlags(0x0202);

        flags.Should().HaveCount(2);
        flags.Should().Contain("ACCOUNTDISABLE");
        flags.Should().Contain("NORMAL_ACCOUNT");
    }

    [Fact]
    public void DecodeUacFlags_DomainController_ReturnsServerTrust()
    {
        // 0x2000 = SERVER_TRUST_ACCOUNT (8192)
        IReadOnlyList<string> flags = AdAttributeMapper.DecodeUacFlags(0x2000);

        flags.Should().ContainSingle().Which.Should().Be("SERVER_TRUST_ACCOUNT");
    }

    [Fact]
    public void DecodeUacFlags_Zero_ReturnsEmpty()
    {
        IReadOnlyList<string> flags = AdAttributeMapper.DecodeUacFlags(0);

        flags.Should().BeEmpty();
    }

    [Fact]
    public void FormatValue_NullValue_ReturnsNull()
    {
        object? result = AdAttributeMapper.FormatValue("cn", null);

        result.Should().BeNull();
    }

    [Fact]
    public void FormatValue_UacAttribute_ReturnsDecodedFlags()
    {
        object? result = AdAttributeMapper.FormatValue("userAccountControl", 514);

        result.Should().BeOfType<List<string>>()
            .Which.Should().Contain("ACCOUNTDISABLE")
            .And.Contain("NORMAL_ACCOUNT");
    }

    [Fact]
    public void FormatValue_FileTimeAttribute_ReturnsFormattedString()
    {
        var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        long fileTime = dt.ToFileTimeUtc();

        object? result = AdAttributeMapper.FormatValue("lastLogonTimestamp", fileTime);

        result.Should().BeOfType<string>()
            .Which.Should().Contain("2024-01-01");
    }

    [Fact]
    public void FormatValue_RegularAttribute_ReturnsOriginalValue()
    {
        object? result = AdAttributeMapper.FormatValue("cn", "TestUser");

        result.Should().Be("TestUser");
    }

    [Fact]
    public void FormatValue_ObjectSid_ReturnsSidString()
    {
        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        byte[] sidBytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(sidBytes, 0);

        object? result = AdAttributeMapper.FormatValue("objectSid", sidBytes);

        result.Should().BeOfType<string>()
            .Which.Should().StartWith("S-1-5-32-544");
    }

    [Fact]
    public void FormatValue_ObjectGuid_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        byte[] guidBytes = guid.ToByteArray();

        object? result = AdAttributeMapper.FormatValue("objectGUID", guidBytes);

        result.Should().BeOfType<string>()
            .Which.Should().Be(guid.ToString());
    }

    [Fact]
    public void FormatValue_AccountExpires_MaxValue_ReturnsNever()
    {
        object? result = AdAttributeMapper.FormatValue("accountExpires", long.MaxValue);

        result.Should().Be("Never");
    }
}
