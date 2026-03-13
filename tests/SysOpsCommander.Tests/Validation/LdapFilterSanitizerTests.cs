using FluentAssertions;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.Tests.Validation;

public sealed class LdapFilterSanitizerTests
{
    [Fact]
    public void SanitizeInput_Asterisk_EscapedCorrectly()
    {
        string result = LdapFilterSanitizer.SanitizeInput("admin*");
        result.Should().Be(@"admin\2a");
    }

    [Fact]
    public void SanitizeInput_OpenParen_EscapedCorrectly()
    {
        string result = LdapFilterSanitizer.SanitizeInput("admin(");
        result.Should().Be(@"admin\28");
    }

    [Fact]
    public void SanitizeInput_CloseParen_EscapedCorrectly()
    {
        string result = LdapFilterSanitizer.SanitizeInput("admin)");
        result.Should().Be(@"admin\29");
    }

    [Fact]
    public void SanitizeInput_Backslash_EscapedCorrectly()
    {
        string result = LdapFilterSanitizer.SanitizeInput(@"admin\");
        result.Should().Be(@"admin\5c");
    }

    [Fact]
    public void SanitizeInput_NulChar_EscapedCorrectly()
    {
        string result = LdapFilterSanitizer.SanitizeInput("admin\0");
        result.Should().Be(@"admin\00");
    }

    [Fact]
    public void SanitizeInput_InjectionAttempt_FullyEscaped()
    {
        string result = LdapFilterSanitizer.SanitizeInput("admin)(objectClass=*");
        result.Should().Be(@"admin\29\28objectClass=\2a");
    }

    [Fact]
    public void SanitizeInput_EmptyString_ReturnsEmpty()
    {
        string result = LdapFilterSanitizer.SanitizeInput("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void SanitizeInput_CleanInput_ReturnsUnchanged()
    {
        string result = LdapFilterSanitizer.SanitizeInput("jsmith");
        result.Should().Be("jsmith");
    }

    [Fact]
    public void BuildSafeFilter_ValidInput_ReturnsFormattedFilter()
    {
        string result = LdapFilterSanitizer.BuildSafeFilter("cn", "jsmith");
        result.Should().Be("(cn=jsmith)");
    }

    [Fact]
    public void BuildSafeFilter_InputWithSpecial_EscapesValue()
    {
        string result = LdapFilterSanitizer.BuildSafeFilter("cn", "j*smith");
        result.Should().Be(@"(cn=j\2asmith)");
    }

    [Fact]
    public void SanitizePreservingWildcards_PreservesAsterisk()
    {
        string result = LdapFilterSanitizer.SanitizePreservingWildcards("admin*");
        result.Should().Be("admin*");
    }

    [Fact]
    public void SanitizePreservingWildcards_EscapesInjectionButKeepsWildcard()
    {
        string result = LdapFilterSanitizer.SanitizePreservingWildcards("admin*)(cn=*");
        result.Should().Be(@"admin*\29\28cn=*");
    }

    [Fact]
    public void SanitizePreservingWildcards_EmptyString_ReturnsEmpty()
    {
        string result = LdapFilterSanitizer.SanitizePreservingWildcards("");
        result.Should().BeEmpty();
    }
}
