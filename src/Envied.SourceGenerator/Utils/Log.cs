using System.Text;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
public static class Log
{
    private static readonly StringBuilder _logBuilder = new();    
    public static void LogError(string message)
    {
        _logBuilder.AppendLine($"// [ERROR] {message}");
    }
    

    public static void LogInfo(string message)
    {
        _logBuilder.AppendLine($"// [INFO] {message}");
    }

    public static void Flush(SourceProductionContext context)
    {
        context.AddSource("log.g.cs", SourceText.From(_logBuilder.ToString(), Encoding.UTF8));
        _logBuilder.Clear();
    }
}