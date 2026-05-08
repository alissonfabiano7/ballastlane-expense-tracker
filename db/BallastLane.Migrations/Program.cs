using System.Diagnostics;

const string DefaultConnectionString =
    "Server=tcp:127.0.0.1,1433;Database=BallastLane;User Id=sa;Password=BallastLane@2026!;TrustServerCertificate=True;Encrypt=True;Connect Timeout=60";

string connectionString = Environment.GetEnvironmentVariable("BALLASTLANE_DB_CONNECTION")
    ?? args.FirstOrDefault(a => a.StartsWith("--connection="))?["--connection=".Length..]
    ?? DefaultConnectionString;

string repoRoot = FindRepoRoot(AppContext.BaseDirectory);
string scriptsPath = Path.Combine(repoRoot, "db", "scripts");

if (!Directory.Exists(scriptsPath))
{
    Console.Error.WriteLine($"Scripts folder not found at {scriptsPath}");
    return 2;
}

Console.WriteLine($"Applying migrations from {scriptsPath}");
Console.WriteLine($"Target: {Redact(connectionString)}");

ProcessStartInfo psi = new()
{
    FileName = "dotnet",
    WorkingDirectory = repoRoot,
    RedirectStandardOutput = false,
    RedirectStandardError = false,
    UseShellExecute = false,
};
psi.ArgumentList.Add("grate");
psi.ArgumentList.Add("--connectionstring");
psi.ArgumentList.Add(connectionString);
psi.ArgumentList.Add("--files");
psi.ArgumentList.Add(scriptsPath);
psi.ArgumentList.Add("--silent");

using Process process = Process.Start(psi)
    ?? throw new InvalidOperationException("Failed to start grate process.");
await process.WaitForExitAsync();
return process.ExitCode;

static string FindRepoRoot(string start)
{
    DirectoryInfo? dir = new(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "BallastLane.slnx"))
            || File.Exists(Path.Combine(dir.FullName, "BallastLane.sln")))
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    throw new InvalidOperationException(
        "Could not locate repository root (no BallastLane.slnx found while walking up from binary path).");
}

static string Redact(string connectionString)
{
    return System.Text.RegularExpressions.Regex.Replace(
        connectionString,
        @"(Password|Pwd)=([^;]+)",
        "$1=***",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
