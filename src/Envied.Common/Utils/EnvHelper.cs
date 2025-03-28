using dotenv.net;

namespace Envied.Common.Utils;

public static class EnviromentHelper
{
    public static Dictionary<string, string>? LoadEnvironment(string path)
    {
        if (!File.Exists(path))
            return null;

        var env = DotEnv.Fluent().WithEnvFiles(path).WithTrimValues().Read();
        var envDict = env.ToDictionary(x => x.Key, x => x.Value);
        return envDict.Count > 0 ? envDict : null;
    }
}
