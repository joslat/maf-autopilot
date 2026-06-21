using MafDoctor;
using MafDoctor.Data;
using MafDoctor.Tools;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MafDoctor.Tests;

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

    // -------------------------------------------------------------------------
    // ADDITIVITY INVARIANT — engraved in test.
    //
    // The release-watcher workflow appends draft entries to the existing
    // registry.yaml via `>> $REGISTRY`. This test pins the contract that
    // (a) the emitted YAML is parseable as registry-entry list items, and
    // (b) appending them to an existing registry document preserves every
    // pre-existing entry. If a future refactor breaks this — e.g. by re-writing
    // the file from scratch, or emitting an invalid YAML shape — this test
    // fails loudly.
    //
    // Documented in `docs/architecture.md § "Auto-update — additivity"`.
    // -------------------------------------------------------------------------

    [Fact]
    public void Additivity_DraftEntries_AppendedToExistingRegistry_PreserveAllOriginalEntries()
    {
        // Arrange — minimal synthetic "existing" registry with one known entry.
        const string existingDoc = """
            schema_version: "1.0"
            target_maf_version: "1.3.0"
            last_updated: "2026-05-12"
            entries:
              - id: MAF130-EXISTING-001
                package: Microsoft.Agents.AI
                version_introduced: "1.3.0"
                type: Microsoft.Agents.AI.ExistingType
                method: DoStuff
                obsolete_signature: "void DoStuff()"
                replacement_signature: "void DoStuffAsync()"
                argument_order_change: false
                fix_description: "Use the async version."
                example_before: |
                  x.DoStuff();
                example_after: |
                  await x.DoStuffAsync();
                cs_warning: CS0618
                guide_section: "1"
                dotnet_inspect_detectable: false
                notes: "test"
            """;

        // Produce two synthetic draft entries from a fake MAF 1.4 diff.
        var parsed = new DiffParseResult(
            Breaking:
            [
                new DiffEntry("FuturisticTypeRemoved", "Type 'FuturisticTypeRemoved' was removed", DiffEntryKind.Removed, false),
                new DiffEntry("Microsoft.Agents.AI.FuturisticTypeChanged", "Member 'Microsoft.Agents.AI.FuturisticTypeChanged.Method()' signature changed", DiffEntryKind.SignatureChanged, false),
            ],
            Additive: [],
            NoChangesDetected: false);
        var drafts = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");
        Assert.NotEmpty(drafts);

        // Act — simulate the watcher's append flow (`>> $REGISTRY`) and parse the
        // combined document with the same YamlDotNet pipeline RegistryService uses.
        var combinedDoc = existingDoc + "\n" + string.Join("\n", drafts);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var loaded = deserializer.Deserialize<Registry>(combinedDoc);

        // Assert — original entry survived; new drafts added; total count = original + drafts.
        Assert.NotNull(loaded);
        Assert.Contains(loaded.Entries, e => e.Id == "MAF130-EXISTING-001");
        Assert.Contains(loaded.Entries, e => e.Id.StartsWith("MAF140-", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1 + drafts.Count, loaded.Entries.Count);
    }

    [Fact]
    public void Additivity_EmptyDraftSet_LeavesExistingRegistryUntouched()
    {
        // Arrange — no breaking changes detected.
        var parsed = new DiffParseResult([], [], NoChangesDetected: true);

        // Act
        var drafts = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");

        // Assert — when there's nothing to append, the watcher writes nothing.
        // This pins the "no-op when clean" invariant: the existing registry
        // is preserved bit-identical because there's no new text to append.
        Assert.Empty(drafts);
    }

    // -------------------------------------------------------------------------
    // Phase 2.6 — fence the dotnet-inspect diff body in the `notes:` field.
    //
    // entry.Body is attacker-controllable: a malicious upstream NuGet author
    // can put anything in the public-API surface that gets diffed, and the
    // resulting `notes:` field flows BOTH to the Coding Agent that fills the
    // registry AND to every downstream LLM that calls MafRegistryLookup.
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractDraftEntries_BodyContainsHtmlComment_StrippedFromNotes()
    {
        var body = "removed <!--SMUGGLED_NOTES_TOKEN--> a thing";
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("MyType", body, DiffEntryKind.Removed, false)],
            Additive: [],
            NoChangesDetected: false);

        var entries = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");
        var entry = Assert.Single(entries);

        Assert.DoesNotContain("<!--", entry);
        Assert.DoesNotContain("SMUGGLED_NOTES_TOKEN", entry);
    }

    [Fact]
    public void ExtractDraftEntries_NotesFieldUsesDataFence()
    {
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("MyType", "removed a method", DiffEntryKind.Removed, false)],
            Additive: [],
            NoChangesDetected: false);

        var entries = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");
        var entry = Assert.Single(entries);

        // The fence markers must be present so any LLM later reading the
        // `notes:` field sees the explicit "treat as data" framing.
        Assert.Contains("BEGIN_USER_DATA_", entry);
        Assert.Contains("END_USER_DATA_", entry);
        Assert.Contains("dotnet-inspect-diff", entry, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractDraftEntries_HugeBody_TruncatedInNotes()
    {
        var hugeBody = "removed a thing: " + new string('A', 16 * 1024);
        var parsed = new DiffParseResult(
            Breaking: [new DiffEntry("MyType", hugeBody, DiffEntryKind.Removed, false)],
            Additive: [],
            NoChangesDetected: false);

        var entries = RegistryExtractCommand.ExtractDraftEntries(parsed, "Microsoft.Agents.AI", "1.4.0");
        var entry = Assert.Single(entries);

        Assert.Contains("[TRUNCATED]", entry);
    }
}
