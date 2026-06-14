using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MafDoctor.Analyzers;
using Xunit;

namespace MafDoctor.Analyzers.Tests;

using DacVerify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
    MafDoctor.Analyzers.DefaultAzureCredentialAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

using SdVerify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<
    MafDoctor.Analyzers.SensitiveDataAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class DefaultAzureCredentialAnalyzerTests
{
    private const string AzureIdentityStub = """

        namespace Azure.Identity
        {
            public class DefaultAzureCredential { public DefaultAzureCredential() { } }
            public class ManagedIdentityCredential { public ManagedIdentityCredential() { } }
        }
        """;

    [Fact]
    public async Task ProductionFile_ReportsMAF002()
    {
        var source = """
            using Azure.Identity;
            public class Auth
            {
                public void Setup() { var c = {|MAF002:new DefaultAzureCredential()|}; }
            }
            """ + AzureIdentityStub;

        await new DacVerify { TestCode = source }.RunAsync();
    }

    [Fact]
    public async Task ManagedIdentityCredential_NoReport()
    {
        var source = """
            using Azure.Identity;
            public class Auth
            {
                public void Setup() { var c = new ManagedIdentityCredential(); }
            }
            """ + AzureIdentityStub;

        await new DacVerify { TestCode = source }.RunAsync();
    }
}

public class SensitiveDataAnalyzerTests
{
    [Fact]
    public async Task TrueLiteralAssignment_ReportsMAF003()
    {
        var source = """
            public class Options { public bool EnableSensitiveData { get; set; } }
            public class Setup
            {
                public void Configure()
                {
                    var o = new Options();
                    {|MAF003:o.EnableSensitiveData = true|};
                }
            }
            """;

        await new SdVerify { TestCode = source }.RunAsync();
    }

    [Fact]
    public async Task FalseLiteralAssignment_NoReport()
    {
        var source = """
            public class Options { public bool EnableSensitiveData { get; set; } }
            public class Setup
            {
                public void Configure()
                {
                    var o = new Options();
                    o.EnableSensitiveData = false;
                }
            }
            """;

        await new SdVerify { TestCode = source }.RunAsync();
    }

    [Fact]
    public async Task ObjectInitializerWithTrue_ReportsMAF003()
    {
        var source = """
            public class Options { public bool EnableSensitiveData { get; set; } }
            public class Setup
            {
                public void Configure()
                {
                    var o = new Options { {|MAF003:EnableSensitiveData = true|} };
                }
            }
            """;

        await new SdVerify { TestCode = source }.RunAsync();
    }

    // -------------------------------------------------------------------------
    // Phase 4.4 — dictionary-style assignment parity with rewriter.
    //
    // The EnableSensitiveDataRewriter has handled `["EnableSensitiveData"] = true`
    // since the initial rewriter pass; the analyzer was missing this shape.
    // Without the analyzer rule, the IDE didn't warn — only the build-time
    // `MafAutoFix` would silently remove the entry. Phase 4.4 closes the gap.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DictionaryInitializerStyle_ReportsMAF003()
    {
        var source = """
            using System.Collections.Generic;
            public class Setup
            {
                public void Configure()
                {
                    var o = new Dictionary<string, object> { {|MAF003:["EnableSensitiveData"] = true|} };
                }
            }
            """;

        await new SdVerify { TestCode = source }.RunAsync();
    }

    [Fact]
    public async Task DictionaryStandaloneAssignment_ReportsMAF003()
    {
        var source = """
            using System.Collections.Generic;
            public class Setup
            {
                public void Configure()
                {
                    var dict = new Dictionary<string, object>();
                    {|MAF003:dict["EnableSensitiveData"] = true|};
                }
            }
            """;

        await new SdVerify { TestCode = source }.RunAsync();
    }

    [Fact]
    public async Task DictionaryStyleWithFalse_NoReport()
    {
        var source = """
            using System.Collections.Generic;
            public class Setup
            {
                public void Configure()
                {
                    var o = new Dictionary<string, object> { ["EnableSensitiveData"] = false };
                }
            }
            """;

        await new SdVerify { TestCode = source }.RunAsync();
    }

    [Fact]
    public async Task DictionaryStyleWithUnrelatedKey_NoReport()
    {
        var source = """
            using System.Collections.Generic;
            public class Setup
            {
                public void Configure()
                {
                    var o = new Dictionary<string, object> { ["SomeOtherFlag"] = true };
                }
            }
            """;

        await new SdVerify { TestCode = source }.RunAsync();
    }
}
