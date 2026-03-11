using FluentAssertions;
using NSubstitute;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

public sealed class SettingsServiceTests
{
    private readonly ISettingsRepository _repository = Substitute.For<ISettingsRepository>();
    private readonly AppConfiguration _appConfig = new()
    {
        DefaultThrottle = 10,
        DefaultTimeoutSeconds = 120,
        DefaultWinRmTransport = "HTTPS",
        DefaultWinRmAuthMethod = "NTLM",
        SharedScriptRepositoryPath = @"\\server\scripts"
    };
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _service = new SettingsService(_repository, _appConfig);
    }

    [Fact]
    public async Task GetEffectiveAsync_PerUserOverrideExists_ReturnsOverride()
    {
        _repository.GetValueAsync("DefaultThrottle", Arg.Any<CancellationToken>())
            .Returns("25");

        string result = await _service.GetEffectiveAsync("DefaultThrottle", CancellationToken.None);

        result.Should().Be("25");
    }

    [Fact]
    public async Task GetEffectiveAsync_NoOverride_ReturnsOrgDefault()
    {
        _repository.GetValueAsync("DefaultThrottle", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        string result = await _service.GetEffectiveAsync("DefaultThrottle", CancellationToken.None);

        result.Should().Be("10");
    }

    [Fact]
    public async Task GetEffectiveAsync_NoOverrideNoOrgDefault_ReturnsHardDefault()
    {
        AppConfiguration emptyConfig = new()
        {
            SharedScriptRepositoryPath = string.Empty
        };
        SettingsService service = new(_repository, emptyConfig);
        _repository.GetValueAsync("SharedScriptRepositoryPath", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        string result = await service.GetEffectiveAsync("SharedScriptRepositoryPath", CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEffectiveAsync_EmptyOrgDefault_ReturnsHardDefault()
    {
        AppConfiguration config = new()
        {
            DefaultWinRmTransport = string.Empty
        };
        SettingsService service = new(_repository, config);
        _repository.GetValueAsync("DefaultWinRmTransport", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        string result = await service.GetEffectiveAsync("DefaultWinRmTransport", CancellationToken.None);

        result.Should().Be("HTTP");
    }

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsStoredValue()
    {
        _repository.GetValueAsync("mykey", Arg.Any<CancellationToken>())
            .Returns("myvalue");

        string result = await _service.GetAsync("mykey", "default", CancellationToken.None);

        result.Should().Be("myvalue");
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsDefault()
    {
        _repository.GetValueAsync("missing", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        string result = await _service.GetAsync("missing", "fallback", CancellationToken.None);

        result.Should().Be("fallback");
    }

    [Fact]
    public void GetOrgDefault_KnownKey_ReturnsConfigValue()
    {
        string result = _service.GetOrgDefault("DefaultWinRmTransport");

        result.Should().Be("HTTPS");
    }

    [Fact]
    public void GetOrgDefault_UnknownKey_ReturnsEmpty()
    {
        string result = _service.GetOrgDefault("NonExistentKey");

        result.Should().BeEmpty();
    }
}
