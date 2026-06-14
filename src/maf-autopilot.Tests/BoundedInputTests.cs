using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for <see cref="BoundedInput"/>. The cap table is the contract; the
/// validator must reject anything over the byte budget, accept anything at
/// or below it, and never echo the input in its error message.
///
/// Phase 5.1 of the v1.1 security-hardening release.
/// </summary>
public sealed class BoundedInputTests
{
    [Fact]
    public void Validate_NullValue_ReturnsNull()
    {
        Assert.Null(BoundedInput.Validate(null, BoundedInput.ShortTextBytes, "x"));
    }

    [Fact]
    public void Validate_EmptyValue_ReturnsEmpty()
    {
        Assert.Equal("", BoundedInput.Validate("", BoundedInput.ShortTextBytes, "x"));
    }

    [Fact]
    public void Validate_AtCap_Accepts()
    {
        var atCap = new string('A', BoundedInput.IdentifierBytes); // 256 ASCII bytes
        var result = BoundedInput.Validate(atCap, BoundedInput.IdentifierBytes, "x");
        Assert.Equal(atCap, result);
    }

    [Fact]
    public void Validate_OneByteOverCap_Throws()
    {
        var overCap = new string('A', BoundedInput.IdentifierBytes + 1);
        var ex = Assert.Throws<ArgumentException>(
            () => BoundedInput.Validate(overCap, BoundedInput.IdentifierBytes, "agentName"));
        Assert.Equal("agentName", ex.ParamName);
        Assert.Contains("exceeds the maximum allowed length", ex.Message);
        Assert.Contains($"{BoundedInput.IdentifierBytes} bytes", ex.Message);
    }

    [Fact]
    public void Validate_MultibyteUtf8_CountsBytesNotChars()
    {
        // 128 4-byte emoji = 512 bytes (over the 256-byte identifier cap).
        // Char count is 256 (each emoji is 2 UTF-16 code units / 1 codepoint
        // / 4 UTF-8 bytes).
        var multibyte = string.Concat(Enumerable.Repeat("\U0001F600", 128));
        Assert.Equal(512, System.Text.Encoding.UTF8.GetByteCount(multibyte));
        Assert.Throws<ArgumentException>(
            () => BoundedInput.Validate(multibyte, BoundedInput.IdentifierBytes, "x"));
    }

    [Fact]
    public void Validate_DoesNotEchoInput()
    {
        // Defense-in-depth: error messages must not echo the input
        // (matches PathGuard.ValidateContainment policy — telemetry must
        // not leak input data).
        var secret = "SECRET_TOKEN_" + new string('A', BoundedInput.IdentifierBytes);
        var ex = Assert.Throws<ArgumentException>(
            () => BoundedInput.Validate(secret, BoundedInput.IdentifierBytes, "x"));
        Assert.DoesNotContain("SECRET_TOKEN_", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveMaxBytes_Throws(int maxBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BoundedInput.Validate("x", maxBytes, "x"));
    }

    // -------------------------------------------------------------------------
    // Cap constants — lock the contract.
    // -------------------------------------------------------------------------

    [Fact]
    public void CapTable_LocksDocumentedValues()
    {
        Assert.Equal(  4 * 1024,        BoundedInput.PathBytes);
        Assert.Equal(        256,        BoundedInput.IdentifierBytes);
        Assert.Equal( 16 * 1024,        BoundedInput.ShortTextBytes);
        Assert.Equal(256 * 1024,        BoundedInput.SnippetBytes);
        Assert.Equal( 64 * 1024,        BoundedInput.InstructionsBytes);
        Assert.Equal(100 * 1024 * 1024, BoundedInput.AggregateBytes);
    }
}
