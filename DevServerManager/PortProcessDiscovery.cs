using System.Diagnostics;
using System.IO;
using System.Management;

namespace DevServerManager;

public sealed record PortProcessInfo(int Port, int ProcessId, string Name, string Command, string WorkingDirectory);

public static class PortProcessDiscovery
{
    public static IReadOnlyList<PortProcessInfo> Find(int port, IReadOnlyList<ProjectDefinition> projects)
    {
        var owners = TcpPortOwners.FindListeners(new HashSet<int> { port });
        var matches = projects.SelectMany(project => project.Services.Where(service => service.Port == port).Select(service => project))
            .Where(project => Directory.Exists(project.WorkingDirectory)).ToList();
        var result = new List<PortProcessInfo>();
        foreach (var owner in owners.DistinctBy(x => x.ProcessId))
        {
            var name = $"PID {owner.ProcessId}";
            var command = "";
            var executable = "";
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT Name, CommandLine, ExecutablePath FROM Win32_Process WHERE ProcessId = {owner.ProcessId}");
                using var found = searcher.Get();
                foreach (ManagementObject process in found)
                {
                    name = process["Name"]?.ToString() ?? name;
                    command = process["CommandLine"]?.ToString() ?? "";
                    executable = process["ExecutablePath"]?.ToString() ?? "";
                    break;
                }
            }
            catch (Exception) { /* Some system processes do not expose their command line. */ }
            try
            {
                using var process = Process.GetProcessById(owner.ProcessId);
                if (name.StartsWith("PID ", StringComparison.Ordinal)) name = process.ProcessName;
                if (string.IsNullOrWhiteSpace(executable)) executable = process.MainModule?.FileName ?? "";
            }
            catch (Exception) { /* The process may have exited or may be inaccessible. */ }
            if (string.IsNullOrWhiteSpace(command) && !string.IsNullOrWhiteSpace(executable)) command = $"\"{executable}\"";
            var matchingProject = matches.FirstOrDefault(project => command.Contains(project.WorkingDirectory, StringComparison.OrdinalIgnoreCase));
            var directory = matchingProject?.WorkingDirectory ?? Path.GetDirectoryName(executable) ?? "";
            result.Add(new PortProcessInfo(port, owner.ProcessId, name, command, directory));
        }
        return result;
    }

    public static async Task StopAsync(int port, int processId)
    {
        if (!TcpPortOwners.FindListeners(new HashSet<int> { port }).Any(owner => owner.ProcessId == processId))
            throw new InvalidOperationException($"PID {processId} is no longer listening on port {port}. Refresh the scan.");
        using var process = Process.GetProcessById(processId);
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().WaitAsync(ServerProcessManager.StopTimeout);
    }

    public static void Start(int port, string command, string workingDirectory)
    {
        if (TcpPortOwners.FindListeners(new HashSet<int> { port }).Count > 0)
            throw new InvalidOperationException($"Port {port} already has a listener. Refresh the scan before starting another process.");
        if (string.IsNullOrWhiteSpace(command)) throw new InvalidOperationException("Enter a launch command for this port.");
        if (!Directory.Exists(workingDirectory)) throw new InvalidOperationException("Choose an existing working directory for this command.");
        _ = Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command)
        {
            WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true
        }) ?? throw new InvalidOperationException("The launch command could not be started.");
    }
}
