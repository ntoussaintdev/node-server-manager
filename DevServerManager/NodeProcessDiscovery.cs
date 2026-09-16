using System.Diagnostics;
using System.IO;
using System.Management;

namespace DevServerManager;

public sealed record ExternalNodeProcess(int ProcessId, string Command, string SourceLocation, bool CanRestart, bool IsRunning);

public static class NodeProcessDiscovery
{
    public static IReadOnlyList<ExternalNodeProcess> Find(IEnumerable<ProjectDefinition> projects)
    {
        var results = new List<ExternalNodeProcess>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine, ExecutablePath FROM Win32_Process WHERE Name = 'node.exe'");
            foreach (ManagementObject process in searcher.Get())
            {
                var id = Convert.ToInt32(process["ProcessId"]);
                var command = process["CommandLine"]?.ToString() ?? "node.exe";
                var project = projects.OrderByDescending(x => x.WorkingDirectory.Length).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.WorkingDirectory) && command.Contains(x.WorkingDirectory, StringComparison.OrdinalIgnoreCase));
                var source = project?.WorkingDirectory ?? process["ExecutablePath"]?.ToString() ?? "Unknown";
                results.Add(new ExternalNodeProcess(id, command, source, project is not null, true));
            }
        }
        catch (Exception) { }
        return results;
    }

    public static void Stop(int processId) { using var process = Process.GetProcessById(processId); process.Kill(entireProcessTree: true); }
    public static void Start(string command, string workingDirectory)
    {
        if (!Directory.Exists(workingDirectory)) throw new InvalidOperationException("The source folder for this Node process no longer exists.");
        Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command) { WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true });
    }
}
