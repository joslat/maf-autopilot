using MafDoctor;
using Xunit;

namespace MafDoctor.Tests;

public sealed class UpdateAdvisorTests
{
    [Fact]
    public void ParseLatestStableVersion_IgnoresPrereleaseAndPicksHighestStable()
    {
        const string json = """
            {
              "versions": ["1.8.0", "1.9.0-beta.1", "1.10.0", "1.9.1"]
            }
            """;

        var latest = UpdateAdvisor.ParseLatestStableVersion(json);

        Assert.Equal("1.10.0", latest);
    }

    [Theory]
    [InlineData("1.10.0", "1.9.9", 1)]
    [InlineData("v1.8.0+abc123", "1.8.0", 0)]
    [InlineData("1.8", "1.8.0", 0)]
    [InlineData("1.7.9", "1.8.0", -1)]
    public void CompareSemanticVersions_HandlesCommonNuGetShapes(string left, string right, int expectedSign)
    {
        var comparison = UpdateAdvisor.CompareSemanticVersions(left, right);

        Assert.Equal(expectedSign, Math.Sign(comparison));
    }
}