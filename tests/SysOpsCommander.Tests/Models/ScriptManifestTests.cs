using System.Text.Json;
using FluentAssertions;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Tests.Models;

public sealed class ScriptManifestTests
{
    private static readonly string[] ExpectedChoices = ["Status", "Start", "Stop", "Restart"];
    private const string SampleManifestJson = """
        {
            "name": "Get-ServiceStatus",
            "description": "Returns the status of specified Windows services",
            "version": "1.2.0",
            "author": "IT Operations",
            "category": "Diagnostics",
            "dangerLevel": "Safe",
            "outputFormat": "Table",
            "parameters": [
                {
                    "name": "ServiceName",
                    "displayName": "Service Name",
                    "description": "The name of the service to check",
                    "type": "string",
                    "required": true,
                    "defaultValue": "Spooler"
                },
                {
                    "name": "Action",
                    "displayName": "Action",
                    "description": "The action to perform",
                    "type": "choice",
                    "required": false,
                    "choices": ["Status", "Start", "Stop", "Restart"]
                }
            ]
        }
        """;

    [Fact]
    public void Deserialize_ValidJson_MapsAllProperties()
    {
        ScriptManifest? manifest = JsonSerializer.Deserialize<ScriptManifest>(SampleManifestJson);

        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be("Get-ServiceStatus");
        manifest.Description.Should().Be("Returns the status of specified Windows services");
        manifest.Version.Should().Be("1.2.0");
        manifest.Author.Should().Be("IT Operations");
        manifest.Category.Should().Be("Diagnostics");
        manifest.DangerLevel.Should().Be(ScriptDangerLevel.Safe);
        manifest.OutputFormat.Should().Be(OutputFormat.Table);
    }

    [Fact]
    public void Deserialize_WithParameters_MapsParameterArray()
    {
        ScriptManifest? manifest = JsonSerializer.Deserialize<ScriptManifest>(SampleManifestJson);

        manifest.Should().NotBeNull();
        manifest!.Parameters.Should().HaveCount(2);

        ScriptParameter firstParam = manifest.Parameters[0];
        firstParam.Name.Should().Be("ServiceName");
        firstParam.DisplayName.Should().Be("Service Name");
        firstParam.Type.Should().Be("string");
        firstParam.Required.Should().BeTrue();
        firstParam.DefaultValue.Should().NotBeNull();
    }

    [Fact]
    public void Deserialize_ChoiceParameter_MapsChoicesArray()
    {
        ScriptManifest? manifest = JsonSerializer.Deserialize<ScriptManifest>(SampleManifestJson);

        manifest.Should().NotBeNull();
        ScriptParameter choiceParam = manifest!.Parameters[1];
        choiceParam.Type.Should().Be("choice");
        choiceParam.Required.Should().BeFalse();
        choiceParam.Choices.Should().NotBeNull();
        choiceParam.Choices.Should().BeEquivalentTo(ExpectedChoices);
    }

    [Fact]
    public void Deserialize_MinimalJson_DefaultsApplied()
    {
        string minimalJson = """{"name": "Test"}""";

        ScriptManifest? manifest = JsonSerializer.Deserialize<ScriptManifest>(minimalJson);

        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be("Test");
        manifest.DangerLevel.Should().Be(ScriptDangerLevel.Safe);
        manifest.OutputFormat.Should().Be(OutputFormat.Text);
        manifest.Parameters.Should().BeEmpty();
    }
}
