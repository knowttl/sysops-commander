using System.Management.Automation;
using System.Security;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

/// <summary>
/// Tests for <see cref="CredentialService"/> event raising, disposal, and null-guard behavior.
/// LDAP-bind validation requires a real domain controller and is not unit-tested here.
/// </summary>
public sealed class CredentialServiceTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly CredentialService _service;

    public CredentialServiceTests()
    {
        _service = new CredentialService(_logger);
    }

    [Fact]
    public void RequestCredentials_RaisesEvent()
    {
        bool eventRaised = false;
        _service.CredentialRequested += (_, _) => eventRaised = true;

        _service.RequestCredentials();

        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void RequestCredentials_NoSubscriber_DoesNotThrow()
    {
        Action act = _service.RequestCredentials;

        act.Should().NotThrow();
    }

    [Fact]
    public void DisposeCredentials_DisposesSecureString()
    {
        var password = new SecureString();
        password.AppendChar('p');
        var credential = new PSCredential("user@domain.local", password);

        _service.DisposeCredentials(credential);

        // SecureString.IsReadOnly() returns true after Dispose in some implementations,
        // but the primary way to verify is that accessing the disposed string throws.
        Action act = () => password.AppendChar('x');
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DisposeCredentials_NullCredential_ThrowsArgumentNull()
    {
        Action act = () => _service.DisposeCredentials(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateCredentials_NullCredential_ThrowsArgumentNull()
    {
        Func<Task> act = () => _service.ValidateCredentialsAsync(null!, "domain", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Action act = () => _ = new CredentialService(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
