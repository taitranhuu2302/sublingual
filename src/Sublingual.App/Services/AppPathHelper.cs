namespace Sublingual.App.Services;

public static class AppPathHelper
{
    public static string GetDefaultAppRoot()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".sublingual");
    }

    public static string ResolveConfiguredPath(string? configuredPath, string defaultSubfolder)
    {
        var appRoot = GetDefaultAppRoot();
        Directory.CreateDirectory(appRoot);

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.Combine(appRoot, defaultSubfolder);
        }

        var trimmed = configuredPath.Trim();
        if (trimmed.StartsWith("~/", StringComparison.Ordinal) || trimmed.StartsWith("~\\", StringComparison.Ordinal))
        {
            var suffix = trimmed[2..].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), suffix);
        }

        if (Path.IsPathRooted(trimmed))
        {
            return trimmed;
        }

        return Path.Combine(appRoot, trimmed);
    }
}
