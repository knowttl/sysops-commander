// SysOpsUpdater — Bootstrapper for applying staged updates.
// Usage: SysOpsUpdater.exe <stagedPath> <appDirectory> <waitForPid>

using System.Diagnostics;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: SysOpsUpdater.exe <stagedPath> <appDirectory> <waitForPid>");
    return 1;
}

string stagedPath = args[0];
string appDirectory = args[1];

if (!int.TryParse(args[2], out int waitForPid))
{
    Console.Error.WriteLine("Invalid PID: " + args[2]);
    return 1;
}

// Step 1: Wait for main app to exit
Console.WriteLine($"Waiting for process {waitForPid} to exit...");
try
{
    var process = Process.GetProcessById(waitForPid);
    _ = process.WaitForExit(TimeSpan.FromSeconds(30));
}
catch (ArgumentException)
{
    // Process already exited
}

// Step 2: Verify staged path exists
if (!Directory.Exists(stagedPath))
{
    Console.Error.WriteLine($"Staged path does not exist: {stagedPath}");
    return 1;
}

// Step 3: Copy staged files to app directory
Console.WriteLine("Applying update...");
try
{
    CopyDirectory(stagedPath, appDirectory);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to apply update: {ex.Message}");
    return 1;
}

// Step 4: Clean up staged files and pending marker
Console.WriteLine("Cleaning up...");
try
{
    Directory.Delete(stagedPath, true);

    string pendingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SysOpsCommander", "Updates", "pending-update.json");

    if (File.Exists(pendingPath))
    {
        File.Delete(pendingPath);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Cleanup partially failed: {ex.Message}");
}

// Step 5: Re-launch main app
Console.WriteLine("Launching SysOps Commander...");
string appExePath = Path.Combine(appDirectory, "SysOpsCommander.App.exe");
if (File.Exists(appExePath))
{
    _ = Process.Start(new ProcessStartInfo
    {
        FileName = appExePath,
        UseShellExecute = true
    });
}
else
{
    Console.Error.WriteLine($"Application executable not found: {appExePath}");
    return 1;
}

return 0;

static void CopyDirectory(string source, string destination)
{
    foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
    {
        string relativePath = Path.GetRelativePath(source, file);
        string destFile = Path.Combine(destination, relativePath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        File.Copy(file, destFile, overwrite: true);
    }
}
