using MafDoctor.Commands;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Unit tests for the <c>migrate-scan</c> CLI argument parsing
/// (<see cref="MigrateScanCli.Parse"/> / <see cref="MigrateScanCli.IsSupportedSource"/>).
/// The detection itself is covered by <c>SemanticKernelDetectorToolTests</c>; these
/// stay process-free. Guards the two parsing bugs the review found: a <c>--source</c>
/// value being mistaken for the path, and the <c>--source=value</c> equals form.
/// </summary>
public class MigrateScanCliTests
{
    [Fact]
    public void Parse_NoFlags_DefaultsPathNullSourceSk_NoJson()
    {
        var (path, source, json) = MigrateScanCli.Parse(new[] { "migrate-scan" });
        Assert.Null(path); // caller substitutes the current directory
        Assert.Equal("semantic-kernel", source);
        Assert.False(json);
    }

    [Fact]
    public void Parse_PathOnly_ReadsPath()
    {
        var (path, _, _) = MigrateScanCli.Parse(new[] { "migrate-scan", "C:/repo" });
        Assert.Equal("C:/repo", path);
    }

    [Fact]
    public void Parse_SourceBeforePath_DoesNotEatThePath()
    {
        // Regression: `--source semantic-kernel C:/repo` must keep C:/repo as the path,
        // not treat "semantic-kernel" as the path.
        var (path, source, _) = MigrateScanCli.Parse(new[] { "migrate-scan", "--source", "semantic-kernel", "C:/repo" });
        Assert.Equal("C:/repo", path);
        Assert.Equal("semantic-kernel", source);
    }

    [Fact]
    public void Parse_SourceAsTrailingFlagWithNoValue_KeepsDefault()
    {
        var (path, source, _) = MigrateScanCli.Parse(new[] { "migrate-scan", "C:/repo", "--source" });
        Assert.Equal("C:/repo", path);
        Assert.Equal("semantic-kernel", source);
    }

    [Fact]
    public void Parse_SourceFollowedByFlag_DoesNotConsumeTheFlag()
    {
        // Regression: `--source` immediately followed by another flag must not eat the flag
        // as the value (`--source --json` would otherwise set source="--json" and drop --json).
        var (path, source, json) = MigrateScanCli.Parse(new[] { "migrate-scan", "--source", "--json", "C:/repo" });
        Assert.Equal("C:/repo", path);
        Assert.Equal("semantic-kernel", source); // default kept, not "--json"
        Assert.True(json);                        // --json still enabled
    }

    [Fact]
    public void Parse_JsonBeforePath_StillReadsPath()
    {
        var (path, _, json) = MigrateScanCli.Parse(new[] { "migrate-scan", "--json", "C:/repo" });
        Assert.Equal("C:/repo", path);
        Assert.True(json);
    }

    [Fact]
    public void Parse_SourceEqualsValue_IsParsed()
    {
        // Regression: the equals form was silently ignored, defeating source validation.
        var (path, source, _) = MigrateScanCli.Parse(new[] { "migrate-scan", "C:/repo", "--source=autogen" });
        Assert.Equal("C:/repo", path);
        Assert.Equal("autogen", source);
    }

    [Theory]
    [InlineData("semantic-kernel", true)]
    [InlineData("Semantic-Kernel", true)]
    [InlineData("sk", true)]
    [InlineData("autogen", false)]
    [InlineData("langchain", false)]
    [InlineData("", false)]
    public void IsSupportedSource_AcceptsOnlySkAndAlias(string source, bool expected)
        => Assert.Equal(expected, MigrateScanCli.IsSupportedSource(source));
}
