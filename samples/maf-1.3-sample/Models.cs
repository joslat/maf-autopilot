// SPDX-License-Identifier: MIT

namespace MafSample.FraudClaims;

/// <summary>The raw claim text submitted by an insurance customer.</summary>
public sealed record ClaimInput(string ClaimId, string CustomerId, string RawText);

/// <summary>Output of one fan-out investigator agent.</summary>
public sealed record InvestigationFinding(string Investigator, double RiskScore, string Notes);

/// <summary>Aggregated investigation evidence — input to the decision agent.</summary>
public sealed record AggregatedEvidence(string ClaimId, IReadOnlyList<InvestigationFinding> Findings);

/// <summary>Final triage decision delivered downstream.</summary>
public sealed record TriageDecision(string ClaimId, string Action, double Confidence, string Reasoning);
