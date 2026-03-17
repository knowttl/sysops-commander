using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

public sealed class DnsResolverServiceTests
{
    private readonly IDnsLookup _dnsLookup = Substitute.For<IDnsLookup>();
    private readonly DnsResolverService _service;

    public DnsResolverServiceTests()
    {
        _service = new DnsResolverService(_dnsLookup);
    }

    [Fact]
    public async Task ResolveAsync_ValidHostname_ReturnsResolvedResult()
    {
        _dnsLookup.GetHostAddressesAsync("server1.test.local", Arg.Any<CancellationToken>())
            .Returns([IPAddress.Parse("10.0.1.50")]);

        IpResolutionResult result = await _service.ResolveAsync("server1.test.local");

        result.Status.Should().Be(IpResolutionStatus.Resolved);
        result.PrimaryIPv4.Should().Be("10.0.1.50");
        result.AllAddresses.Should().HaveCount(1);
        result.Hostname.Should().Be("server1.test.local");
    }

    [Fact]
    public async Task ResolveAsync_UnresolvableHostname_ReturnsFailedWithError()
    {
        _dnsLookup.GetHostAddressesAsync("unknown.test.local", Arg.Any<CancellationToken>())
            .ThrowsAsync(new SocketException(11001));

        IpResolutionResult result = await _service.ResolveAsync("unknown.test.local");

        result.Status.Should().Be(IpResolutionStatus.Failed);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.PrimaryIPv4.Should().BeNull();
        result.Hostname.Should().Be("unknown.test.local");
    }

    [Fact]
    public async Task ResolveAsync_Timeout_ReturnsFailedWithTimeoutMessage()
    {
        _dnsLookup.GetHostAddressesAsync("slow.test.local", Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                CancellationToken ct = callInfo.ArgAt<CancellationToken>(1);
                await Task.Delay(10000, ct);
                return (IPAddress[])[];
            });

        IpResolutionResult result = await _service.ResolveAsync("slow.test.local");

        result.Status.Should().Be(IpResolutionStatus.Failed);
        result.ErrorMessage.Should().Be("DNS resolution timed out");
    }

    [Fact]
    public async Task ResolveAsync_EmptyHostname_ReturnsNotApplicable()
    {
        IpResolutionResult result = await _service.ResolveAsync("");

        result.Status.Should().Be(IpResolutionStatus.NotApplicable);
        await _dnsLookup.DidNotReceive().GetHostAddressesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_NullHostname_ReturnsNotApplicable()
    {
        IpResolutionResult result = await _service.ResolveAsync(null!);

        result.Status.Should().Be(IpResolutionStatus.NotApplicable);
    }

    [Fact]
    public async Task ResolveAsync_MultipleAddresses_PrimaryIsFirstIPv4()
    {
        _dnsLookup.GetHostAddressesAsync("multi.test.local", Arg.Any<CancellationToken>())
            .Returns([
                IPAddress.Parse("fe80::1"),
                IPAddress.Parse("10.0.1.50"),
                IPAddress.Parse("192.168.1.100")
            ]);

        IpResolutionResult result = await _service.ResolveAsync("multi.test.local");

        result.Status.Should().Be(IpResolutionStatus.Resolved);
        result.PrimaryIPv4.Should().Be("10.0.1.50");
        result.AllAddresses.Should().HaveCount(3);
    }

    [Fact]
    public async Task ResolveAsync_IPv6Only_PrimaryIsNull()
    {
        _dnsLookup.GetHostAddressesAsync("v6only.test.local", Arg.Any<CancellationToken>())
            .Returns([IPAddress.Parse("fe80::1"), IPAddress.Parse("::1")]);

        IpResolutionResult result = await _service.ResolveAsync("v6only.test.local");

        result.Status.Should().Be(IpResolutionStatus.Resolved);
        result.PrimaryIPv4.Should().BeNull();
        result.AllAddresses.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => _service.ResolveAsync("host.test.local", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ResolveAllAsync_CallbackFiredPerHostname()
    {
        _dnsLookup.GetHostAddressesAsync("host1.test.local", Arg.Any<CancellationToken>())
            .Returns([IPAddress.Parse("10.0.1.1")]);
        _dnsLookup.GetHostAddressesAsync("host2.test.local", Arg.Any<CancellationToken>())
            .Returns([IPAddress.Parse("10.0.1.2")]);

        List<string?> results = [];

        await _service.ResolveAllAsync(
        [
            ("host1.test.local", r => results.Add(r.PrimaryIPv4)),
            ("host2.test.local", r => results.Add(r.PrimaryIPv4))
        ]);

        results.Should().HaveCount(2);
        results.Should().Contain("10.0.1.1");
        results.Should().Contain("10.0.1.2");
    }

    [Fact]
    public async Task ResolveAllAsync_EmptyList_CompletesImmediately()
    {
        await _service.ResolveAllAsync([]);

        await _dnsLookup.DidNotReceive().GetHostAddressesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAllAsync_CancellationStopsPending()
    {
        using var cts = new CancellationTokenSource();

        _dnsLookup.GetHostAddressesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                CancellationToken ct = callInfo.ArgAt<CancellationToken>(1);
                await Task.Delay(5000, ct);
                return (IPAddress[])[IPAddress.Parse("10.0.0.1")];
            });

        await cts.CancelAsync();

        Func<Task> act = () => _service.ResolveAllAsync(
            [("host1.test.local", _ => { })],
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
