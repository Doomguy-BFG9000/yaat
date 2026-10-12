namespace Yaat.Sim.Tests.Helpers;

/// <summary>The yaat checkout the running test assembly was built from.</summary>
internal static class RepositoryRoot
{
    /// <summary>The nearest directory above the test assembly that holds <c>yaat.slnx</c>.</summary>
    /// <exception cref="InvalidOperationException">No directory above the test assembly holds <c>yaat.slnx</c>.</exception>
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "yaat.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"No yaat.slnx above {AppContext.BaseDirectory}; the test needs the repository's sources.");
    }
}
