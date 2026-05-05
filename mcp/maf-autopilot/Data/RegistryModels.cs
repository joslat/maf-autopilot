using YamlDotNet.Serialization;

namespace MafAutopilot.Data;

/// <summary>
/// Root of the MAF obsolete-API registry YAML document.
/// Mirrors the structure of .github/skills/obsolete-api-registry/registry.yaml.
/// </summary>
public sealed class Registry
{
    [YamlMember(Alias = "schema_version")]
    public string SchemaVersion { get; set; } = "1.0";

    [YamlMember(Alias = "target_maf_version")]
    public string TargetMafVersion { get; set; } = "";

    [YamlMember(Alias = "last_updated")]
    public string LastUpdated { get; set; } = "";

    [YamlMember(Alias = "entries")]
    public List<RegistryEntry> Entries { get; set; } = [];
}

/// <summary>
/// A single obsolete-API entry in the registry.
/// Maps 1-to-1 with a YAML entry block under <c>entries:</c>.
/// </summary>
public sealed class RegistryEntry
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = "";

    [YamlMember(Alias = "package")]
    public string Package { get; set; } = "";

    [YamlMember(Alias = "version_introduced")]
    public string VersionIntroduced { get; set; } = "";

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "";

    [YamlMember(Alias = "method")]
    public string Method { get; set; } = "";

    [YamlMember(Alias = "obsolete_signature")]
    public string ObsoleteSignature { get; set; } = "";

    [YamlMember(Alias = "replacement_signature")]
    public string ReplacementSignature { get; set; } = "";

    [YamlMember(Alias = "argument_order_change")]
    public bool ArgumentOrderChange { get; set; }

    [YamlMember(Alias = "fix_description")]
    public string FixDescription { get; set; } = "";

    [YamlMember(Alias = "example_before")]
    public string ExampleBefore { get; set; } = "";

    [YamlMember(Alias = "example_after")]
    public string ExampleAfter { get; set; } = "";

    [YamlMember(Alias = "cs_warning")]
    public string CsWarning { get; set; } = "";

    [YamlMember(Alias = "guide_section")]
    public string GuideSection { get; set; } = "";

    [YamlMember(Alias = "dotnet_inspect_detectable")]
    public bool DotnetInspectDetectable { get; set; }

    [YamlMember(Alias = "notes")]
    public string Notes { get; set; } = "";
}
