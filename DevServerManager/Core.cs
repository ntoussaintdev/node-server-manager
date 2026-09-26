using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace DevServerManager;
public enum ServiceTemplate { NxWeb, NxApi, AngularCli, NpmDev, Custom }
public abstract class Notify : INotifyPropertyChanged { public event PropertyChangedEventHandler? PropertyChanged; protected bool Set<T>(ref T f, T v, [CallerMemberName] string? n = null) { if (EqualityComparer<T>.Default.Equals(f,v)) return false; f=v; Raise(n!); return true; } protected void Raise(string name) => PropertyChanged?.Invoke(this,new(name)); }
public sealed class ProjectDefinition : Notify
{
    string name="", directory="", status="Stopped";
    public string Name {get=>name;set=>Set(ref name,value);} public string WorkingDirectory {get=>directory;set=>Set(ref directory,value);} public string Status {get=>status;set=>Set(ref status,value);}
    public ObservableCollection<ServiceDefinition> Services {get;set;}=[]; public ObservableCollection<EnvironmentVariableDefinition> EnvironmentVariables {get;set;}=[];
    [JsonIgnore] public ObservableCollection<EnvironmentTarget> EnvironmentTargets { get; } = [new(Guid.Empty, "All services")];
    public void PrepareEnvironmentTargets()
    {
        foreach (var service in Services)
        {
            if (service.Id == Guid.Empty) service.Id = Guid.NewGuid();
            var target = EnvironmentTargets.FirstOrDefault(option => option.ServiceId == service.Id);
            if (target is null) EnvironmentTargets.Add(new EnvironmentTarget(service.Id, service.Name));
            else target.Name = service.Name;
        }
        foreach (var target in EnvironmentTargets.Where(option => option.ServiceId != Guid.Empty && Services.All(service => service.Id != option.ServiceId)).ToList()) EnvironmentTargets.Remove(target);
        foreach (var variable in EnvironmentVariables) variable.Targets = EnvironmentTargets;
    }
}
public sealed class EnvironmentVariableDefinition : Notify
{
    string name="", value=""; Guid serviceId; ObservableCollection<EnvironmentTarget> targets=[];
    public string Name {get=>name;set=>Set(ref name,value);}
    public string Value {get=>this.value;set=>Set(ref this.value,value);}
    public Guid ServiceId {get=>serviceId;set {if(Set(ref serviceId,value)) Raise(nameof(SelectedTarget));}}
    [JsonIgnore] public ObservableCollection<EnvironmentTarget> Targets {get=>targets;set {if(Set(ref targets,value)) Raise(nameof(SelectedTarget));}}
    [JsonIgnore] public EnvironmentTarget? SelectedTarget {get=>Targets.FirstOrDefault(target => target.ServiceId == ServiceId);set {if(value is not null) ServiceId=value.ServiceId;}}
}
public sealed class EnvironmentTarget(Guid serviceId, string name) : Notify { string targetName = name; public Guid ServiceId {get;} = serviceId; public string Name {get=>targetName;set=>Set(ref targetName,value);} }
public sealed class ManagedServiceRow : Notify
{
    public ProjectDefinition? Project { get; init; } public ServiceDefinition? Service { get; init; } public int ProcessId { get; init; } public bool IsExternal { get; init; } public bool IsScanned { get; init; } public bool CanRestart { get; init; }
    string status = "Stopped";
    public string ProjectName { get; init; } = "External"; public string ServiceName { get; init; } = "Node"; public string SourceLocation { get; init; } = "Unknown"; public string GroupLocation { get; init; } = ""; public string Command { get; init; } = "Unavailable"; public int Port { get; init; } public string Status { get => status; set => Set(ref status, value); }
    public string DisplayCommand => string.IsNullOrWhiteSpace(SourceLocation) || SourceLocation == "Unknown" ? Command : Command.Replace(SourceLocation, ".", StringComparison.OrdinalIgnoreCase);
    public static ManagedServiceRow FromManaged(ProjectDefinition project, ServiceDefinition service, int processId) => new() { Project = project, Service = service, ProcessId = processId, ProjectName = project.Name, ServiceName = service.Name, SourceLocation = project.WorkingDirectory, Command = service.Command, Port = service.Port, Status = service.Status, CanRestart = true };
}
public sealed class ServerSourceGroup(string sourceLocation, IEnumerable<ManagedServiceRow> servers, bool isExpanded = false) : Notify { bool expanded = isExpanded; public string SourceLocation { get; } = sourceLocation; public ObservableCollection<ManagedServiceRow> Servers { get; } = [.. servers]; public string Summary => SourceLocation == "Scanned port" ? $"Scanned port {Servers.FirstOrDefault()?.Port}  •  {Servers.Count(row => row.Status == "Running")} process(es)" : $"{SourceLocation}  •  {Servers.Count} server(s)"; public bool IsExpanded { get => expanded; set => Set(ref expanded, value); } }
public sealed class ServiceDefinition : Notify { string name="", command="", url="", status="Stopped", listenerPids=""; int port, order; bool enabled=true; ServiceTemplate template; public Guid Id {get;set;}=Guid.NewGuid(); public string Name {get=>name;set=>Set(ref name,value);} public string Command {get=>command;set=>Set(ref command,value);} public string BrowserUrl {get=>url;set=>Set(ref url,value);} public string Status {get=>status;set=>Set(ref status,value);} [JsonIgnore] public string ListenerPids {get=>listenerPids;set=>Set(ref listenerPids,value);} public int Port {get=>port;set=>Set(ref port,value);} public int StartOrder {get=>order;set=>Set(ref order,value);} public bool Enabled {get=>enabled;set=>Set(ref enabled,value);} public ServiceTemplate Template {get=>template;set=>Set(ref template,value);} public void ApplyTemplate(){Command=TemplateCatalog.Command(Template);if(TemplateCatalog.IsWeb(Template)&&string.IsNullOrWhiteSpace(BrowserUrl))BrowserUrl="http://localhost:{port}";} public static ServiceDefinition Web(int p)=>new(){Name="Web",Template=ServiceTemplate.NxWeb,Command=TemplateCatalog.Command(ServiceTemplate.NxWeb),Port=p,StartOrder=20,BrowserUrl="http://localhost:{port}"}; public static ServiceDefinition Api(int p)=>new(){Name="API",Template=ServiceTemplate.NxApi,Command=TemplateCatalog.Command(ServiceTemplate.NxApi),Port=p,StartOrder=10}; }
public static class TemplateCatalog { public static string Command(ServiceTemplate template)=>template switch { ServiceTemplate.NxWeb=>"npx nx serve web --port {port}", ServiceTemplate.NxApi=>"npx nx serve api --port {port}", ServiceTemplate.AngularCli=>"npx ng serve --port {port}", ServiceTemplate.NpmDev=>"npm run dev -- --port {port}", _=>"" }; public static bool IsWeb(ServiceTemplate t)=>t is ServiceTemplate.NxWeb or ServiceTemplate.AngularCli or ServiceTemplate.NpmDev; }
public static class PortableDatabase
{
    public static string DatabasePath { get; } = Path.Combine(AppContext.BaseDirectory, "dev-server-manager.db");
    public static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS projects (id INTEGER PRIMARY KEY, name TEXT NOT NULL, definition_json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            INSERT OR IGNORE INTO settings (key, value) VALUES ('theme', 'Device');
            """;
        command.ExecuteNonQuery();
        return connection;
    }
}

public sealed class ConfigurationStore
{
    readonly JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public IReadOnlyList<ProjectDefinition> Load()
    {
        using var connection = PortableDatabase.OpenConnection();
        using var command = connection.CreateCommand(); command.CommandText = "SELECT definition_json FROM projects ORDER BY name COLLATE NOCASE";
        using var reader = command.ExecuteReader(); var projects = new List<ProjectDefinition>();
        while (reader.Read()) { var project = JsonSerializer.Deserialize<ProjectDefinition>(reader.GetString(0), options); if (project is not null) { project.PrepareEnvironmentTargets(); projects.Add(project); } }
        return projects;
    }
    public void Save(IEnumerable<ProjectDefinition> projects)
    {
        using var connection = PortableDatabase.OpenConnection(); using var transaction = connection.BeginTransaction();
        using (var clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = "DELETE FROM projects"; clear.ExecuteNonQuery(); }
        foreach (var project in projects)
        {
            using var insert = connection.CreateCommand(); insert.Transaction = transaction; insert.CommandText = "INSERT INTO projects (name, definition_json) VALUES ($name, $json)";
            insert.Parameters.AddWithValue("$name", project.Name); insert.Parameters.AddWithValue("$json", JsonSerializer.Serialize(project, options)); insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
public sealed record LogEntry(string Project,string Text);
public static class Expander { public static string Expand(string text,ProjectDefinition p,ServiceDefinition s) {var web=p.Services.FirstOrDefault(x=>x.Name.Equals("Web",StringComparison.OrdinalIgnoreCase))?.Port??s.Port;var api=p.Services.FirstOrDefault(x=>x.Name.Equals("API",StringComparison.OrdinalIgnoreCase))?.Port??s.Port;return text.Replace("{projectFolder}",p.WorkingDirectory,StringComparison.OrdinalIgnoreCase).Replace("{webPort}",web.ToString(),StringComparison.OrdinalIgnoreCase).Replace("{apiPort}",api.ToString(),StringComparison.OrdinalIgnoreCase).Replace("{port}",s.Port.ToString(),StringComparison.OrdinalIgnoreCase);}}
public static class BatchLauncherImporter { public static ProjectDefinition Import(IEnumerable<string> files) { var all=files.ToList(); var folder=Path.GetDirectoryName(all.First())!; var project=new ProjectDefinition { Name=Path.GetFileName(folder), WorkingDirectory=folder }; foreach(var file in all){var lines=File.ReadAllLines(file);var port=int.TryParse(System.Text.RegularExpressions.Regex.Match(string.Join("\n",lines),@"(?im)^\s*set\s+PORT\s*=\s*(\d+)").Groups[1].Value,out var parsed)?parsed:4200+project.Services.Count;var command=lines.Select(x=>x.Trim()).LastOrDefault(x=>x.StartsWith("npx ",StringComparison.OrdinalIgnoreCase)||x.StartsWith("npm ",StringComparison.OrdinalIgnoreCase)||x.StartsWith("ng ",StringComparison.OrdinalIgnoreCase))??"";command=command.Replace("%PORT%","{port}",StringComparison.OrdinalIgnoreCase);if(!string.IsNullOrWhiteSpace(command)&&!command.Contains("--port",StringComparison.OrdinalIgnoreCase))command+=" --port {port}";var isWeb=Path.GetFileNameWithoutExtension(file).Contains("web",StringComparison.OrdinalIgnoreCase);project.Services.Add(new ServiceDefinition { Name=isWeb?"Web":"API", Template=ServiceTemplate.Custom, Command=command, Port=port, StartOrder=isWeb?20:10, BrowserUrl=isWeb?"http://localhost:{port}":"" });}return project;} }
public sealed class ServerProcessManager { readonly Dictionary<ServiceDefinition,Process> processes=[]; readonly Dictionary<string,List<string>> logs=[]; readonly HashSet<ServiceDefinition> stopping=[]; readonly object processGate = new(), logGate = new(); public event EventHandler<LogEntry>? LogReceived; public event EventHandler? StatusChanged; public bool IsServiceRunning(ServiceDefinition s) { lock (processGate) return processes.TryGetValue(s,out var p)&&!p.HasExited; } public bool IsProjectRunning(ProjectDefinition p)=>p.Services.Any(IsServiceRunning); public IReadOnlyList<string> GetLog(string p) { lock (logGate) return logs.TryGetValue(p,out var l)?l.ToArray():[]; }
 public async Task StartProjectAsync(ProjectDefinition p){Validate(p);var services=p.Services.Where(x=>x.Enabled&&!IsServiceRunning(x)).OrderBy(x=>x.StartOrder).ToList();CheckPorts(services);p.Status="Starting";foreach(var s in services){Start(p,s);await Task.Delay(200);}p.Status="Running";StatusChanged?.Invoke(this,EventArgs.Empty);}
 public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
 public async Task RestartServiceAsync(ProjectDefinition p,ServiceDefinition s){Process? process;lock(processGate)processes.TryGetValue(s,out process);Stop(p,s);if(process is not null)try{await process.WaitForExitAsync().WaitAsync(StopTimeout);}catch(TimeoutException){throw new InvalidOperationException($"{s.Name} did not stop within {StopTimeout.TotalSeconds:0} seconds, so it was not restarted.");}Validate(p);CheckPorts([s]);Start(p,s);p.Status="Running";StatusChanged?.Invoke(this,EventArgs.Empty);}
 public Task StartServiceAsync(ProjectDefinition p,ServiceDefinition s){Validate(p);if(IsServiceRunning(s))return Task.CompletedTask;CheckPorts([s]);Start(p,s);p.Status="Running";StatusChanged?.Invoke(this,EventArgs.Empty);return Task.CompletedTask;}
 public void StopService(ProjectDefinition p,ServiceDefinition s){Stop(p,s);lock(processGate)p.Status=processes.Keys.Any(x=>p.Services.Contains(x))?"Degraded":"Stopped";StatusChanged?.Invoke(this,EventArgs.Empty);}
 public async Task<bool> StopProjectAsync(ProjectDefinition p)
 {
     var ports = p.Services.Select(s => s.Port).Where(port => port is >= 1 and <= 65535).Distinct().ToHashSet();
     List<(ServiceDefinition Service, Process? Process)> tracked;
     lock (processGate) tracked = p.Services.Select(s => (Service: s, Process: processes.GetValueOrDefault(s))).Where(x => x.Process is not null).ToList();
     foreach (var (service, _) in tracked) { try { Stop(p, service); } catch (Exception ex) { Log(p.Name, service.Name, $"Stop failed: {ex.Message}"); } }
     var waits = tracked.Select(async item =>
     {
         try { await item.Process!.WaitForExitAsync().WaitAsync(StopTimeout); }
         catch (TimeoutException) { Log(p.Name, item.Service.Name, "Process still running after five seconds."); }
         catch (Exception ex) { Log(p.Name, item.Service.Name, $"Could not wait for process exit: {ex.Message}"); }
     });
     await Task.WhenAll(waits);
     var clean = ports.Count == 0 || await Task.Run(() => StopRemainingListeners(p.Name, ports));
     p.Status = clean ? "Stopped" : "Degraded";
     StatusChanged?.Invoke(this, EventArgs.Empty);
     return clean;
 }
 public async Task StopAllAsync(IEnumerable<ProjectDefinition> projects) { foreach (var project in projects.ToList()) try { await StopProjectAsync(project); } catch (Exception ex) { Log(project.Name, "Project", $"Stop failed: {ex.Message}"); } }
 bool StopRemainingListeners(string projectName, HashSet<int> ports)
 {
     IReadOnlyList<TcpPortOwner> listeners;
     try { listeners = TcpPortOwners.FindListeners(ports); }
     catch (Exception ex) { Log(projectName, "Ports", $"Could not inspect project ports: {ex.Message}"); return false; }
     foreach (var group in listeners.GroupBy(x => x.ProcessId))
     {
         var portList = string.Join(", ", group.Select(x => x.Port).Distinct().Order());
         try
         {
             using var process = Process.GetProcessById(group.Key);
             if (process.HasExited) { Log(projectName, "Ports", $"PID {group.Key} already exited (port {portList})."); continue; }
             Log(projectName, "Ports", $"Stopping remaining listener PID {group.Key} on port {portList}.");
             process.Kill(entireProcessTree: true);
         }
         catch (ArgumentException) { Log(projectName, "Ports", $"PID {group.Key} already exited (port {portList})."); }
         catch (Exception ex) { Log(projectName, "Ports", $"Could not stop PID {group.Key} on port {portList}: {ex.Message}"); }
     }
     try { var remaining = TcpPortOwners.FindListeners(ports); foreach (var listener in remaining) Log(projectName, "Ports", $"Warning: port {listener.Port} is still listening (PID {listener.ProcessId})."); return remaining.Count == 0; }
     catch (Exception ex) { Log(projectName, "Ports", $"Could not verify project ports: {ex.Message}"); return false; }
 }
 void Start(ProjectDefinition p,ServiceDefinition s){var command=Expander.Expand(s.Command,p,s);var startInfo=new ProcessStartInfo("cmd.exe","/c "+command){WorkingDirectory=p.WorkingDirectory,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var variable in p.EnvironmentVariables.Where(x=>!string.IsNullOrWhiteSpace(x.Name)&&(x.ServiceId==Guid.Empty||x.ServiceId==s.Id)).OrderBy(x=>x.ServiceId==s.Id?1:0))startInfo.Environment[variable.Name]=variable.Value??"";var x=new Process{StartInfo=startInfo,EnableRaisingEvents=true};x.OutputDataReceived+=(_,e)=>{if(e.Data is not null)Log(p.Name,s.Name,e.Data);};x.ErrorDataReceived+=(_,e)=>{if(e.Data is not null)Log(p.Name,s.Name,"ERR "+e.Data);};x.Exited+=(_,_)=>{bool wasStopped, hasOthers;lock(processGate){wasStopped=stopping.Remove(s);processes.Remove(s);hasOthers=processes.Keys.Any(z=>p.Services.Contains(z));}var code=x.ExitCode;s.Status=wasStopped?"Stopped":code==0?"Stopped":"Failed";p.Status=hasOthers?"Degraded":s.Status=="Failed"?"Failed":"Stopped";Log(p.Name,s.Name,wasStopped?"Stopped.":$"Process exited with code {code}.");StatusChanged?.Invoke(this,EventArgs.Empty);};if(!x.Start())throw new InvalidOperationException($"Could not start {s.Name}.");lock(processGate)processes[s]=x;s.Status="Running";x.BeginOutputReadLine();x.BeginErrorReadLine();Log(p.Name,s.Name,$"Started (PID {x.Id}): {command}");}
 void Stop(ProjectDefinition p,ServiceDefinition s){Process? x;lock(processGate){if(!processes.Remove(s,out x))return;stopping.Add(s);}try{if(!x.HasExited)x.Kill(true);Log(p.Name,s.Name,"Stop requested.");}catch(InvalidOperationException){}finally{s.Status="Stopped";}}
 static void Validate(ProjectDefinition p){if(string.IsNullOrWhiteSpace(p.Name))throw new InvalidOperationException("A project name is required.");if(string.IsNullOrWhiteSpace(p.WorkingDirectory)||!Directory.Exists(p.WorkingDirectory))throw new InvalidOperationException("Choose an existing working directory before starting.");foreach(var s in p.Services.Where(x=>x.Enabled))if(string.IsNullOrWhiteSpace(s.Name)||string.IsNullOrWhiteSpace(s.Command)||s.Port is <1 or >65535)throw new InvalidOperationException("Every enabled service needs a name, command, and valid port.");if(p.Services.Where(x=>x.Enabled).GroupBy(x=>x.Port).Any(x=>x.Count()>1))throw new InvalidOperationException("Enabled services cannot use the same port.");}
 static void CheckPorts(IEnumerable<ServiceDefinition> ss){var ports=IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x=>x.Port).ToHashSet();var bad=ss.FirstOrDefault(x=>ports.Contains(x.Port));if(bad is not null)throw new InvalidOperationException($"Port {bad.Port} is already in use. Dev Server Manager will not stop an unknown process.");}
 void Log(string p,string s,string m){var text=$"[{DateTime.Now:HH:mm:ss}] {s}: {m}";lock(logGate){if(!logs.TryGetValue(p,out var l))logs[p]=l=[];l.Add(text);if(l.Count>1000)l.RemoveAt(0);}LogReceived?.Invoke(this,new(p,text));}}
