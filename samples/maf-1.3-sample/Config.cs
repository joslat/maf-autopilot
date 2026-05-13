// SPDX-License-Identifier: MIT
// Copyright (c) 2026 maf-autopilot sample.

using Azure;

namespace MafSample.FraudClaims;

/// <summary>
/// Azure OpenAI configuration sourced from environment variables.
/// Only consulted when the sample is invoked with --run (default mode is
/// dry-run: build the workflow + print structure, no LLM calls).
/// </summary>
public static class Config
{
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"))   &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"));

    public static Uri Endpoint =>
        new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set."));

    public static AzureKeyCredential KeyCredential =>
        new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
            ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is not set."));

    public static string Model =>
        Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o";
}
