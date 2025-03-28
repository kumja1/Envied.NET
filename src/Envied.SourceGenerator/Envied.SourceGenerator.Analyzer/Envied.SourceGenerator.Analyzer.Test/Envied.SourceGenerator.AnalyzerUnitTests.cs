using System.Threading.Tasks;
using Xunit;
using Verify = Envied.SourceGenerator.Analyzer.Test.Utilities.CSharpVerifier<Envied.SourceGenerator.Analyzer.Envied.SourceGenerator.AnalyzerAnalyzer>;
    
namespace Envied.SourceGenerator.Analyzer.Test
{
    
    public class Envied.SourceGenerator.AnalyzerUnitTest
    {
        //No diagnostics expected to show up
        [Fact]
        public async Task TestMethod1()
        {
            var test = @"";

            await Verify.VerifyAnalyzerAsyncV2(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [Fact]
        public async Task TestMethod2()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class [|TypeName|]
        {   
        }
    }";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";

            var expected = Verify.Diagnostic(Envied.SourceGenerator.AnalyzerAnalyzer.DiagnosticId);
            await Verify.VerifyCodeFixAsyncV2(test, fixtest);
        }
    }
}
