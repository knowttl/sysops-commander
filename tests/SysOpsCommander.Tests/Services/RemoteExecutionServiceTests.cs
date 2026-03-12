using System.IO;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

/// <summary>
/// Tests for <see cref="RemoteExecutionService"/> orchestration logic.
/// Uses a localhost TCP listener to simulate reachable hosts for pre-flight checks.
/// </summary>
public sealed class RemoteExecutionServiceTests : IDisposable
{
    private readonly IExecutionStrategy _mockStrategy = Substitute.For<IExecutionStrategy>();
    private readonly IAuditLogService _mockAuditLog = Substitute.For<IAuditLogService>();
    private readonly INotificationService _mockNotification = Substitute.For<INotificationService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly RemoteExecutionService _service;
    private readonly TcpListener _listener;
    private readonly int _port;

    public RemoteExecutionServiceTests()
    {
        _mockStrategy.Type.Returns(ExecutionType.PowerShell);
        _mockStrategy.ExecuteAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<System.Management.Automation.PSCredential?>(),
                Arg.Any<WinRmConnectionOptions>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(HostResult.Success(callInfo.ArgAt<string>(0), "OK", TimeSpan.Zero)));

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start(50);
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _service = new RemoteExecutionService(
            [_mockStrategy],
            _mockAuditLog,
            _mockNotification,
            _logger);
    }

    public void Dispose()
    {
        _listener.Stop();
        CleanupTempFiles();
    }

    [Fact]
    public async Task ExecuteAsync_AllHostsUnreachable_ReturnsUnreachableResults()
    {
        ExecutionJob job = CreateJob(["unreachable-host-01", "unreachable-host-02"]);
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        ExecutionJob result = await _service.ExecuteAsync(job, progress, CancellationToken.None);

        result.Status.Should().Be(ExecutionStatus.Failed);
        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_SelectsCorrectStrategy_ByExecutionType()
    {
        IExecutionStrategy wmiStrategy = Substitute.For<IExecutionStrategy>();
        wmiStrategy.Type.Returns(ExecutionType.WMI);
        wmiStrategy.ExecuteAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<System.Management.Automation.PSCredential?>(),
                Arg.Any<WinRmConnectionOptions>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(HostResult.Success(callInfo.ArgAt<string>(0), "WMI OK", TimeSpan.Zero)));

        RemoteExecutionService service = new(
            [_mockStrategy, wmiStrategy],
            _mockAuditLog,
            _mockNotification,
            _logger);

        ExecutionJob job = CreateReachableJob(1);
        job.ExecutionType = ExecutionType.WMI;
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        ExecutionJob result = await service.ExecuteAsync(job, progress, CancellationToken.None);

        await wmiStrategy.Received(1).ExecuteAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>?>(),
            Arg.Any<System.Management.Automation.PSCredential?>(),
            Arg.Any<WinRmConnectionOptions>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoStrategyRegistered_Throws()
    {
        RemoteExecutionService service = new(
            [],
            _mockAuditLog,
            _mockNotification,
            _logger);

        ExecutionJob job = CreateReachableJob(1);
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        Func<Task> act = () => service.ExecuteAsync(job, progress, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No execution strategy*");
    }

    [Fact]
    public async Task ExecuteAsync_ProgressReported_ForEachHost()
    {
        ExecutionJob job = CreateReachableJob(3);
        var reported = new List<HostResult>();
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();
        progress.When(p => p.Report(Arg.Any<HostResult>()))
            .Do(callInfo => reported.Add(callInfo.Arg<HostResult>()));

        await _service.ExecuteAsync(job, progress, CancellationToken.None);

        reported.Count.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_PerHostErrorIsolation_OtherHostsContinue()
    {
        int callCount = 0;
        _mockStrategy.ExecuteAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<System.Management.Automation.PSCredential?>(),
                Arg.Any<WinRmConnectionOptions>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                int current = Interlocked.Increment(ref callCount);
                return current == 1
                    ? Task.FromException<HostResult>(new InvalidOperationException("Simulated failure"))
                    : Task.FromResult(HostResult.Success(callInfo.ArgAt<string>(0), "OK", TimeSpan.Zero));
            });

        ExecutionJob job = CreateReachableJob(3);
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        ExecutionJob result = await _service.ExecuteAsync(job, progress, CancellationToken.None);

        result.Results.Should().HaveCount(3);
        result.Results.Count(r => r.Status == HostStatus.Success).Should().Be(2);
        result.Results.Count(r => r.Status == HostStatus.Failed).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_NotificationFired_AfterCompletion()
    {
        ExecutionJob job = CreateReachableJob(1);
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        await _service.ExecuteAsync(job, progress, CancellationToken.None);

        _mockNotification.Received(1).ShowExecutionComplete(Arg.Is<ExecutionJob>(j => j.Id == job.Id));
    }

    [Fact]
    public async Task ExecuteAsync_AuditLogged_AfterCompletion()
    {
        ExecutionJob job = CreateReachableJob(1);
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        await _service.ExecuteAsync(job, progress, CancellationToken.None);

        await _mockAuditLog.Received(1).LogExecutionAsync(
            Arg.Any<AuditLogEntry>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ThrottleEnforced_MaxConcurrentRespected()
    {
        int concurrent = 0;
        int maxConcurrent = 0;
        object lockObj = new();

        _mockStrategy.ExecuteAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>?>(),
                Arg.Any<System.Management.Automation.PSCredential?>(),
                Arg.Any<WinRmConnectionOptions>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                int c = Interlocked.Increment(ref concurrent);
                lock (lockObj)
                {
                    maxConcurrent = Math.Max(maxConcurrent, c);
                }

                await Task.Delay(50, callInfo.ArgAt<CancellationToken>(6));
                Interlocked.Decrement(ref concurrent);
                return HostResult.Success(callInfo.ArgAt<string>(0), "OK", TimeSpan.FromMilliseconds(50));
            });

        ExecutionJob job = CreateReachableJob(5, throttle: 2);
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        await _service.ExecuteAsync(job, progress, CancellationToken.None);

        maxConcurrent.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulExecution_CompletedStatus()
    {
        ExecutionJob job = CreateReachableJob(2);
        IProgress<HostResult> progress = Substitute.For<IProgress<HostResult>>();

        ExecutionJob result = await _service.ExecuteAsync(job, progress, CancellationToken.None);

        result.Status.Should().Be(ExecutionStatus.Completed);
        result.Results.Should().HaveCount(2);
        result.Results.Should().OnlyContain(r => r.Status == HostStatus.Success);
        result.EndTime.Should().NotBeNull();
    }

    [Fact]
    public void GetTempDirectory_ReturnsExpectedPath()
    {
        string tempDir = RemoteExecutionService.GetTempDirectory();

        tempDir.Should().Contain(AppConstants.AppDataFolder);
        tempDir.Should().EndWith("Temp");
    }

    private static ExecutionJob CreateJob(IEnumerable<string> hostnames)
    {
        var targets = hostnames
            .Select(h => new HostTarget { Hostname = h })
            .ToList();

        return new ExecutionJob
        {
            ScriptName = "Test-Script",
            ScriptContent = "Get-Process",
            TargetHosts = targets,
            WinRmConnectionOptions = WinRmConnectionOptions.CreateDefault()
        };
    }

    private ExecutionJob CreateReachableJob(int targetCount, int throttle = 5)
    {
        var targets = Enumerable.Range(1, targetCount)
            .Select(_ => new HostTarget { Hostname = "127.0.0.1" })
            .ToList();

        return new ExecutionJob
        {
            ScriptName = "Test-Script",
            ScriptContent = "Get-Process",
            TargetHosts = targets,
            ThrottleLimit = throttle,
            WinRmConnectionOptions = new WinRmConnectionOptions
            {
                AuthMethod = WinRmAuthMethod.Kerberos,
                Transport = WinRmTransport.HTTP,
                CustomPort = _port
            }
        };
    }

    private static void CleanupTempFiles()
    {
        string tempDir = RemoteExecutionService.GetTempDirectory();
        if (Directory.Exists(tempDir))
        {
            foreach (string file in Directory.GetFiles(tempDir, "*.txt"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Best-effort cleanup
                }
            }
        }
    }
}
