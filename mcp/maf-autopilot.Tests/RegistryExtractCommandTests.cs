using MafAutopilot;
using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class RegistryExtractCommandTests
{
    [Fact]
    public void ExtractDraftEntries_NoChangesDetected_ReturnsEmpty()
    {
        var parsed = new DiffParseResult([], [], NoChangesDetected: true);
        var result = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractDraftEntries_RemovedMember_EmitsYamlStub()
    {
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("AgentThread", "Type 'AgentThread' was removed", DiffEntryKind.Removed, false)],
            Additive: [],
            NoChangesDetected: false);

        var entries = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");

        var entry = Assert.Single(entries);
        Assert.Contains("id: MAF140-", entry);          // version-derived prefix
        Assert.Contains("package: Microsoft.Agents.AI", entry);
        Assert.Contains("type: AgentThread", entry);
        Assert.Contains("cs_warning: CS0246", entry);    // removed → CS0246
        Assert.Contains("TODO", entry);                  // human-fillable fields present
    }

    [Fact]
    public void ExtractDraftEntries_SignatureChange_ParsesObsoleteAndReplacement()
    {
        const string body = "Member 'AddResource' signature changed: `void AddResource(string)` -> `void AddResource(string, JsonSerializerOptions?)`";
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("AgentInlineSkill", body, DiffEntryKind.SignatureChanged, false)],
            Additive: [],
            NoChangesDetected: false);

        var entry = Assert.Single(RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0"));

        // Sig pair should be extracted from the backtick-delimited body. YamlDotNet
        // chooses quote style based on content; we only assert the values are present.
        Assert.Contains("void AddResource(string)", entry);
        Assert.Contains("void AddResource(string, JsonSerializerOptions?)", entry);
        Assert.Contains("method: AddResource", entry);
        Assert.Contains("cs_warning: CS0618", entry);    // sig change → CS0618 (caller may still get a warning)
    }

    [Fact]
    public void ExtractDraftEntries_ObsoleteAdditive_IsIncluded()
    {
        // An additive change with [Obsolete] annotation = newly-deprecated API.
        var parsed = new DiffParseResult(
            Breaking: [],
            Additive: [new DiffEntry("LegacyClient", "[Obsolete(\"use NewClient\")] public class LegacyClient", DiffEntryKind.Added, IsObsolete: true)],
            NoChangesDetected: false);

        var entries = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");

        Assert.Single(entries);
        Assert.Contains("type: LegacyClient", entries[0]);
        Assert.Contains("dotnet_inspect_detectable: true", entries[0]);
    }

    [Fact]
    public void ExtractDraftEntries_PureAdditive_Skipped()
    {
        // Plain additive (no Obsolete annotation) is NOT a candidate for the registry.
        var parsed = new DiffParseResult(
            Breaking: [],
            Additive: [new DiffEntry("ShinyNewType", "Type was added", DiffEntryKind.Added, IsObsolete: false)],
            NoChangesDetected: false);

        var entries = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");
        Assert.Empty(entries);
    }

    [Fact]
    public void ExtractDraftEntries_MultipleSameArea_GetSequentialIds()
    {
        var parsed = new DiffParseResult(
            Breaking: [
                new DiffEntry("FraudExecutor", "Type was removed", DiffEntryKind.Removed, false),
                new DiffEntry("PaymentExecutor", "Type was removed", DiffEntryKind.Removed, false),
                new DiffEntry("ReviewExecutor", "Type was removed", DiffEntryKind.Removed, false),
            ],
            Additive: [],
            NoChangesDetected: false);

        var entries = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");
        Assert.Equal(3, entries.Count);
        Assert.Contains("MAF140-EXECUTOR-001", entries[0]);
        Assert.Contains("MAF140-EXECUTOR-002", entries[1]);
        Assert.Contains("MAF140-EXECUTOR-003", entries[2]);
    }

    // -------------------------------------------------------------------------
    // VersionToIdSegment
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1.3.0", "130")]
    [InlineData("1.4.0", "140")]
    [InlineData("2.0.0", "200")]
    [InlineData("1.3.0-alpha-5", "130")]
    [InlineData("not-a-version", "XXX")]
    public void VersionToIdSegment_HandlesSemVerVariants(string version, string expected) =>
        Assert.Equal(expected, RegistryExtractCommand.VersionToIdSegment(version));

    // -------------------------------------------------------------------------
    // AreaCodeFromType
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("FraudExecutor", "EXECUTOR")]
    [InlineData("WorkflowBuilder", "BUILDER")]
    [InlineData("ChatClientAgentOptions", "OPTIONS")]
    [InlineData("StreamsMessageAttribute", "ATTRIBUT")]   // truncated at 8
    [InlineData("AgentSession", "AGENT")]                  // no recognised suffix → first PascalCase segment
    [InlineData("Microsoft.Agents.AI.AgentSession", "AGENT")]
    [InlineData("", "AREA")]
    public void AreaCodeFromType_ProducesExpected(string typeName, string expected) =>
        Assert.Equal(expected, RegistryExtractCommand.AreaCodeFromType(typeName));
}
