// SPDX-License-Identifier: MIT
//
// ⚠️ DELIBERATE ANTI-PATTERN: instance-level mutable state on AIContextProvider.
// The provider is a SINGLETON per agent — instance fields are shared across
// concurrent sessions and corrupt one case's state with another's data.
//
// The fix: persist per-session state with ProviderSessionState<T>, which the
// MAF runtime scopes per active session.
//
// In a real customer codebase, `AIContextProvider` is the abstract base in
// `Microsoft.Agents.AI.MessageAIContextProvider` (internal) or accessed via
// the `Microsoft.Extensions.AI.AIContextProviderChatClientBuilderExtensions`
// surface. For the purposes of this sample we declare a local abstract base
// of the same name — the anti-pattern scanner matches on the SOURCE TEXT
// "AIContextProvider" (substring) so the rule still fires.
//
// Triggers:
//   - MAF-AP-CONC-001 (anti-pattern scanner)

namespace MafSample.FraudClaims.Providers;

/// <summary>
/// Local stand-in for the real <c>AIContextProvider</c> base class. A real
/// customer codebase would inherit from <c>MessageAIContextProvider</c> or
/// register a context-injector via <c>UseAIContextProvider(...)</c>.
/// </summary>
public abstract class AIContextProvider
{
    public abstract string ContributeInstructions();
}

public sealed class CaseContextProvider : AIContextProvider
{
    // ⚠️ MAF-AP-CONC-001 — mutable instance fields on an AIContextProvider derivative.
    // Two concurrent claims will trample each other's values. The fix is to lift these
    // into a per-session state object.
    private string _currentClaimId = "";
    private string _currentCustomerId = "";
    private int _invocationCount;

    public void SetCase(ClaimInput claim)
    {
        _currentClaimId = claim.ClaimId;
        _currentCustomerId = claim.CustomerId;
        _invocationCount++;
    }

    public override string ContributeInstructions()
        => $"Active claim: {_currentClaimId} for customer {_currentCustomerId} (call #{_invocationCount}).";
}
