namespace VideoGateway.ConsoleApp;

public static class SpyPathResolver
{
    public static string Resolve(string[]? args, string? envVar)
    {
        string? spyPath = null;

        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.StartsWith("--rtiddsspy=", StringComparison.OrdinalIgnoreCase))
                {
                    spyPath = a.Substring(a.IndexOf('=') + 1).Trim('"');
                    break;
                }
                if (a.Equals("--rtiddsspy", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    spyPath = args[i + 1].Trim('"');
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(spyPath) && !string.IsNullOrWhiteSpace(envVar))
        {
            spyPath = envVar;
        }

        if (string.IsNullOrWhiteSpace(spyPath)) spyPath = "rtiddsspy";

        return spyPath;
    }
}
