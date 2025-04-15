using Microsoft.CodeAnalysis.Diagnostics;

namespace Envied.SourceGenerator.Common.Utils;

internal static class ProjectHelper
{
    public static bool CheckSupportsPartial(
        AnalyzerConfigOptionsProvider analyzerConfig,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();
        if (
            !analyzerConfig.GlobalOptions.TryGetValue(
                "build_property.TargetFramework",
                out var targetFramework
            )
        )
        {
            return true;
        }

        string versionString = targetFramework.Replace("net", "").Replace("coreapp", "");

        if (Version.TryParse(versionString, out var version))
            return version.Major >= 9;
        return true;
    }
}
