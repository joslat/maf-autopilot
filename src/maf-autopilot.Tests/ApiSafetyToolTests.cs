using MafAutopilot.Data;
using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// End-to-end tests for the ApiSafetyTool — invokes the tool the way the MCP host would.
/// </summary>
public class ApiSafetyToolTests
{
    private readonly ApiSafetyTool _tool = new(new RegistryService());

    [Fact]
    public void MafApiSafety_EmptyInput_ReturnsErrorMessage()
    {
        var result = _tool.MafApiSafety("");
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafApiSafety_GibberishApi_ReturnsSafe()
    {
        var result = _tool.MafApiSafety("zzzNotAnApiAnywhere");
        Assert.Contains("SAFE", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Anchored regression test for commit 393b7a2.
    /// Pre-fix, this query returned SAFE — a silent false-negative on a removed type.
    /// </summary>
    [Fact]
    public void MafApiSafety_AgentThread_ReturnsUnsafe()
    {
        var result = _tool.MafApiSafety("AgentThread");
        Assert.DoesNotContain("✅ SAFE", result, StringComparison.Ordinal);
        // Either UNSAFE (single match) or MULTIPLE — both prove the term was found.
        Assert.True(
            result.Contains("UNSAFE", StringComparison.Ordinal)
            || result.Contains("MULTIPLE", StringComparison.Ordinal),
            $"Expected UNSAFE or MULTIPLE, got:\n{result}");
    }

    [Theory]
    [InlineData("AddFanInBarrierEdge")]
    [InlineData("SerializeSession")]
    [InlineData("StreamAsync")]
    public void MafApiSafety_KnownObsoleteApi_ReturnsNonSafe(string apiName)
    {
        var result = _tool.MafApiSafety(apiName);
        Assert.DoesNotContain("✅ SAFE", result, StringComparison.Ordinal);
    }
}
