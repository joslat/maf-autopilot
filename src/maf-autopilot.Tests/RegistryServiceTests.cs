using MafAutopilot.Data;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Tests for RegistryService — the single most regression-prone surface in the server.
///
/// The "AgentThread → SAFE" bug fixed in commit 393b7a2 would have been caught by
/// <see cref="SearchByApiName_AgentThread_FindsThreadEntry"/>. Future cross-field gaps are
/// guarded by parametric tests over Notes, FixDescription, ExampleBefore.
/// </summary>
public class RegistryServiceTests
{
    private readonly RegistryService _registry = new();

    // -------------------------------------------------------------------------
    // Load / metadata
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_FromEmbeddedResource_PopulatesEntries()
    {
        Assert.NotEmpty(_registry.AllIds);
        Assert.Equal("1.3.0", _registry.TargetVersion);
        Assert.False(string.IsNullOrWhiteSpace(_registry.LastUpdated));
    }

    [Fact]
    public void AllIds_AreSortedAndUnique()
    {
        var ids = _registry.AllIds.ToList();
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal), ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // -------------------------------------------------------------------------
    // FindById
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("MAF130-FAN-IN-001")]
    [InlineData("maf130-fan-in-001")]   // case-insensitive
    [InlineData("MAF130-Fan-In-001")]   // mixed case
    public void FindById_KnownEntry_AnyCasing_Returns(string id)
    {
        var entry = _registry.FindById(id);
        Assert.NotNull(entry);
        Assert.Equal("MAF130-FAN-IN-001", entry!.Id, ignoreCase: true);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MAF130-DOES-NOT-EXIST")]
    [InlineData("FAN-IN-001")]    // missing MAF130 prefix
    public void FindById_Unknown_ReturnsNull(string id)
    {
        Assert.Null(_registry.FindById(id));
    }

    // -------------------------------------------------------------------------
    // SearchByApiName — regression-prone surface
    // -------------------------------------------------------------------------

    /// <summary>
    /// Direct regression test for commit 393b7a2. Before the fix, searching "AgentThread"
    /// returned SAFE because the term only appeared in `notes` / `fix_description`,
    /// which were not in the OR-chain.
    /// </summary>
    [Fact]
    public void SearchByApiName_AgentThread_FindsThreadEntry()
    {
        var matches = _registry.SearchByApiName("AgentThread");
        Assert.NotEmpty(matches);
        Assert.Contains(matches, e => e.Id.Contains("THREAD", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("AddFanInBarrierEdge")]
    [InlineData("SerializeSession")]
    [InlineData("ChatClientAgent")]
    [InlineData("StreamAsync")]
    [InlineData("AgentRunUpdateEvent")]
    public void SearchByApiName_KnownSurface_FindsAtLeastOne(string term)
    {
        var matches = _registry.SearchByApiName(term);
        Assert.NotEmpty(matches);
    }

    [Fact]
    public void SearchByApiName_GibberishTerm_ReturnsEmpty()
    {
        var matches = _registry.SearchByApiName("zzzNotAnApiAnywhereInTheRegistry");
        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("agentthread")]   // all lowercase
    [InlineData("AGENTTHREAD")]   // all uppercase
    [InlineData("AgentTHREAD")]   // mixed
    public void SearchByApiName_IsCaseInsensitive(string term)
    {
        var matches = _registry.SearchByApiName(term);
        Assert.NotEmpty(matches);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SearchByApiName_NullOrEmpty_ReturnsEmpty(string? term)
    {
        var matches = _registry.SearchByApiName(term!);
        Assert.Empty(matches);
    }

    // -------------------------------------------------------------------------
    // Coverage of all string fields — guards the bug class fixed by the
    // 2026-05-11 inverted-haystack refactor (plan #37).
    //
    // Pre-refactor the OR-chain missed ExampleAfter, Package, Id, GuideSection,
    // CsWarning. Each test below targets a field that the original implementation
    // failed to search.
    // -------------------------------------------------------------------------

    /// <summary>"CreateSessionAsync" lives in `example_after` — pre-refactor: missed.</summary>
    [Fact]
    public void SearchByApiName_TermInExampleAfter_FindsEntry()
    {
        var matches = _registry.SearchByApiName("CreateSessionAsync");
        Assert.NotEmpty(matches);
    }

    /// <summary>Searching by package name should find entries from that package.</summary>
    [Theory]
    [InlineData("Microsoft.Agents.AI.Workflows")]
    [InlineData("Microsoft.Agents.AI")]
    public void SearchByApiName_TermInPackage_FindsEntry(string package)
    {
        var matches = _registry.SearchByApiName(package);
        Assert.NotEmpty(matches);
    }

    /// <summary>Searching by ID substring should find that entry.</summary>
    [Theory]
    [InlineData("FAN-IN")]
    [InlineData("THREAD")]
    [InlineData("MAF130")]
    public void SearchByApiName_TermInId_FindsEntry(string fragment)
    {
        var matches = _registry.SearchByApiName(fragment);
        Assert.NotEmpty(matches);
    }

    /// <summary>CS warning codes should be searchable so callers can ask "all CS0246 entries".</summary>
    [Theory]
    [InlineData("CS0618")]
    [InlineData("CS0246")]
    public void SearchByApiName_TermInCsWarning_FindsEntry(string warning)
    {
        var matches = _registry.SearchByApiName(warning);
        Assert.NotEmpty(matches);
    }
}
