using System.IO;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

/// <summary>
/// Tests for <see cref="HostTargetingService"/> hostname validation, de-duplication, and CSV parsing.
/// </summary>
public sealed class HostTargetingServiceTests : IDisposable
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly HostTargetingService _service;
    private readonly string _tempDir;

    public HostTargetingServiceTests()
    {
        _service = new HostTargetingService(_logger);
        _tempDir = Path.Combine(Path.GetTempPath(), $"SysOpsTests_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void AddFromHostnames_ValidHostnames_AddsToTargets()
    {
        _service.AddFromHostnames(["SERVER01", "SERVER02", "SERVER03"]);

        _service.Targets.Should().HaveCount(3);
        _service.Targets.Select(t => t.Hostname).Should().BeEquivalentTo(["SERVER01", "SERVER02", "SERVER03"]);
    }

    [Fact]
    public void AddFromHostnames_InvalidHostname_SkipsAndLogs()
    {
        _service.AddFromHostnames(["SERVER01", "invalid hostname!!", "SERVER02"]);

        _service.Targets.Should().HaveCount(2);
        _service.Targets.Should().NotContain(t => t.Hostname == "invalid hostname!!");
    }

    [Fact]
    public void AddFromHostnames_Duplicates_DeDuplicated()
    {
        _service.AddFromHostnames(["SERVER01", "SERVER01"]);

        _service.Targets.Should().HaveCount(1);
    }

    [Fact]
    public void AddFromHostnames_CaseInsensitiveDeDup()
    {
        _service.AddFromHostnames(["SERVER01", "server01"]);

        _service.Targets.Should().HaveCount(1);
    }

    [Fact]
    public void AddFromHostnames_EmptyAndWhitespace_Skipped()
    {
        _service.AddFromHostnames(["SERVER01", "", "  ", "SERVER02"]);

        _service.Targets.Should().HaveCount(2);
    }

    [Fact]
    public void AddFromHostnames_ValidIpAddress_Added()
    {
        _service.AddFromHostnames(["192.168.1.100", "10.0.0.1"]);

        _service.Targets.Should().HaveCount(2);
    }

    [Fact]
    public void AddFromHostnames_ValidFqdn_Added()
    {
        _service.AddFromHostnames(["server01.contoso.com", "web-app.corp.local"]);

        _service.Targets.Should().HaveCount(2);
    }

    [Fact]
    public void AddFromHostnames_SetsIsValidated()
    {
        _service.AddFromHostnames(["SERVER01"]);

        _service.Targets[0].IsValidated.Should().BeTrue();
    }

    [Fact]
    public async Task AddFromCsvFile_ValidFile_ParsesAll()
    {
        string csvPath = CreateCsvFile("SERVER01\nSERVER02\nSERVER03\nSERVER04\nSERVER05");

        await _service.AddFromCsvFileAsync(csvPath, CancellationToken.None);

        _service.Targets.Should().HaveCount(5);
    }

    [Fact]
    public async Task AddFromCsvFile_SkipsComments()
    {
        string csvPath = CreateCsvFile("# This is a comment\nSERVER01\n# Another comment\nSERVER02");

        await _service.AddFromCsvFileAsync(csvPath, CancellationToken.None);

        _service.Targets.Should().HaveCount(2);
        _service.Targets.Should().NotContain(t => t.Hostname.StartsWith('#'));
    }

    [Fact]
    public async Task AddFromCsvFile_SkipsEmptyLines()
    {
        string csvPath = CreateCsvFile("SERVER01\n\n\nSERVER02\n  \n");

        await _service.AddFromCsvFileAsync(csvPath, CancellationToken.None);

        _service.Targets.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddFromCsvFile_ParsesFirstColumn()
    {
        string csvPath = CreateCsvFile("SERVER01,Production,Web\nSERVER02,Staging,DB");

        await _service.AddFromCsvFileAsync(csvPath, CancellationToken.None);

        _service.Targets.Should().HaveCount(2);
        _service.Targets[0].Hostname.Should().Be("SERVER01");
        _service.Targets[1].Hostname.Should().Be("SERVER02");
    }

    [Fact]
    public void ClearTargets_RemovesAll()
    {
        _service.AddFromHostnames(["SERVER01", "SERVER02", "SERVER03"]);
        _service.Targets.Should().HaveCount(3);

        _service.ClearTargets();

        _service.Targets.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTarget_ExistingHost_Removed()
    {
        _service.AddFromHostnames(["SERVER01", "SERVER02", "SERVER03"]);

        _service.RemoveTarget("SERVER01");

        _service.Targets.Should().HaveCount(2);
        _service.Targets.Should().NotContain(t => t.Hostname == "SERVER01");
    }

    [Fact]
    public void RemoveTarget_CaseInsensitive_MatchesAndRemoves()
    {
        _service.AddFromHostnames(["SERVER01"]);

        _service.RemoveTarget("server01");

        _service.Targets.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTarget_NonExistentHost_NoChange()
    {
        _service.AddFromHostnames(["SERVER01"]);

        _service.RemoveTarget("SERVER999");

        _service.Targets.Should().HaveCount(1);
    }

    [Fact]
    public void AddFromAdSearchResults_ComputerObjects_Added()
    {
        List<AdObject> computers =
        [
            new() { Name = "WEB01", ObjectClass = "computer", DistinguishedName = "CN=WEB01,OU=Servers,DC=corp,DC=local" },
            new() { Name = "DB01", ObjectClass = "computer", DistinguishedName = "CN=DB01,OU=Servers,DC=corp,DC=local" }
        ];

        _service.AddFromAdSearchResults(computers);

        _service.Targets.Should().HaveCount(2);
    }

    [Fact]
    public void AddFromAdSearchResults_MixedObjects_OnlyAddsComputers()
    {
        List<AdObject> objects =
        [
            new() { Name = "WEB01", ObjectClass = "computer", DistinguishedName = "CN=WEB01,OU=Servers,DC=corp,DC=local" },
            new() { Name = "user01", ObjectClass = "user", DistinguishedName = "CN=user01,OU=Users,DC=corp,DC=local" },
            new() { Name = "Server Group", ObjectClass = "group", DistinguishedName = "CN=Server Group,OU=Groups,DC=corp,DC=local" }
        ];

        _service.AddFromAdSearchResults(objects);

        _service.Targets.Should().HaveCount(1);
        _service.Targets[0].Hostname.Should().Be("WEB01");
    }

    private string CreateCsvFile(string content)
    {
        string filePath = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(filePath, content);
        return filePath;
    }
}
