using FluentAssertions;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.Tests.Validation;

public sealed class ManifestSchemaValidatorTests
{
    [Fact]
    public void Validate_CompleteValidManifest_ReturnsNoErrors()
    {
        ScriptManifest manifest = CreateValidManifest();

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        ScriptManifest manifest = CreateValidManifest();
        manifest.Name = "";

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Name"));
    }

    [Fact]
    public void Validate_InvalidVersion_ReturnsError()
    {
        ScriptManifest manifest = CreateValidManifest();
        manifest.Version = "1.0";

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("semver"));
    }

    [Fact]
    public void Validate_InvalidCategory_ReturnsError()
    {
        ScriptManifest manifest = CreateValidManifest();
        manifest.Category = "Unknown";

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not recognized"));
    }

    [Fact]
    public void Validate_DuplicateParameterNames_ReturnsError()
    {
        ScriptManifest manifest = CreateValidManifest();
        manifest.Parameters =
        [
            new ScriptParameter { Name = "Filter", Type = "string" },
            new ScriptParameter { Name = "Filter", Type = "string" }
        ];

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate"));
    }

    [Fact]
    public void Validate_ChoiceParamNoChoices_ReturnsError()
    {
        ScriptManifest manifest = CreateValidManifest();
        manifest.Parameters =
        [
            new ScriptParameter { Name = "Mode", Type = "choice", Choices = null }
        ];

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("no choices"));
    }

    [Fact]
    public void Validate_InvalidParameterType_ReturnsError()
    {
        ScriptManifest manifest = CreateValidManifest();
        manifest.Parameters =
        [
            new ScriptParameter { Name = "StartDate", Type = "datetime" }
        ];

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("invalid type"));
    }

    [Fact]
    public void Validate_ValidManifestWithParameters_ReturnsNoErrors()
    {
        ScriptManifest manifest = CreateValidManifest();
        manifest.Parameters =
        [
            new ScriptParameter { Name = "Filter", Type = "string" },
            new ScriptParameter { Name = "Mode", Type = "choice", Choices = ["Fast", "Thorough"] }
        ];

        ManifestValidationResult result = ManifestSchemaValidator.Validate(manifest);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static ScriptManifest CreateValidManifest() =>
        new()
        {
            Name = "Get-ServiceStatus",
            Description = "Retrieves the status of specified services.",
            Version = "1.0.0",
            Author = "Admin",
            Category = "Diagnostics",
            DangerLevel = ScriptDangerLevel.Safe,
            OutputFormat = OutputFormat.Table
        };
}
