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
public abstract class Notify : INotifyPropertyChanged { public event PropertyChangedEventHandler? PropertyChanged; protected bool Set<T>(ref T f, T v, [CallerMemberName] string? n = null) { if (EqualityComparer<T>.Default.Equals(f,v)) return false; f=v; PropertyChanged?.Invoke(this,new(n)); return true; } }
public sealed class ProjectDefinition : Notify { string name="", directory="", status="Stopped"; public string Name {get=>name;set=>Set(ref name,value);} public string WorkingDirectory {get=>directory;set=>Set(ref directory,value);} public string Status {get=>status;set=>Set(ref status,value);} public ObservableCollection<ServiceDefinition> Services {get;set;}=[]; public ObservableCollection<EnvironmentVariableDefinition> EnvironmentVariables {get;set;}=[]; }
public sealed class EnvironmentVariableDefinition : Notify { string name="", _value=""; public string Name {get=>name;set=>Set(ref name,value);} public string Value {get=>_value;set=>Set(ref _value,value);} }
public sealed class ManagedServiceRow : Notify
{
    public ProjectDefinition? Project { get; init; } public ServiceDefinition? Service { get; init; } public int ProcessId { get; init; } public bool IsExternal { get; init; } public bool CanRestart { get; init; }
    string status = "Stopped";
    public string ProjectName { get; init; } = "External"; public string ServiceName { get; init; } = "Node"; public string SourceLocation { get; init; } = "Unknown"; public string Command { get; init; } = "Unavailable"; public int Port { get; init; } public string Status { get => status; set => Set(ref status, value); }
    public string DisplayCommand => string.IsNullOrWhiteSpace(SourceLocation) || SourceLocation == "Unknown" ? Command : Command.Replace(SourceLocation, ".", StringComparison.OrdinalIgnoreCase);
    public static ManagedServiceRow FromManaged(ProjectDefinition project, ServiceDefinition service, int processId) => new() { Project = project, Service = service, ProcessId = processId, ProjectName = project.Name, ServiceName = service.Name, SourceLocation = project.WorkingDirectory, Command = service.Command, Port = service.Port, Status = service.Status, CanRestart = true };
}
public sealed class ServerSourceGroup(string sourceLocation, IEnumerable<ManagedServiceRow> servers) { public string SourceLocation { get; } = sourceLocation; public ObservableCollection<ManagedServiceRow> Servers { get; } = [.. servers]; public string Summary => $"{SourceLocation}  •  {Servers.Count} server(s)"; }
public sealed class ServiceDefinition : Notify { string name="", command="", url="", status="Stopped"; int port, order; bool enabled=true; ServiceTemplate template; public string Name {get=>name;set=>Set(ref name,value);} public string Command {get=>command;set=>Set(ref command,value);} public string BrowserUrl {get=>url;set=>Set(ref url,value);} public string Status {get=>status;set=>Set(ref status,value);} public int Port {get=>port;set=>Set(ref port,value);} public int StartOrder {get=>order;set=>Set(ref order,value);} public bool Enabled {get=>enabled;set=>Set(ref enabled,value);} public ServiceTemplate Template {get=>template;set=>Set(ref template,value);} public void ApplyTemplate(){Command=TemplateCatalog.Command(Template);if(TemplateCatalog.IsWeb(Template)&&string.IsNullOrWhiteSpace(BrowserUrl))BrowserUrl="http://localhost:{port}";} public static ServiceDefinition Web(int p)=>new(){Name="Web",Template=ServiceTemplate.NxWeb,Command=TemplateCatalog.Command(ServiceTemplate.NxWeb),Port=p,StartOrder=20,BrowserUrl="http://localhost:{port}"}; public static ServiceDefinition Api(int p)=>new(){Name="API",Template=ServiceTemplate.NxApi,Command=TemplateCatalog.Command(ServiceTemplate.NxApi),Port=p,StartOrder=10}; }
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
        while (reader.Read()) { var project = JsonSerializer.Deserialize<ProjectDefinition>(reader.GetString(0), options); if (project is not null) projects.Add(project); }
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
public sealed class ServerProcessManager { readonly Dictionary<ServiceDefinition,Process> processes=[]; readonly Dictionary<string,List<string>> logs=[]; readonly HashSet<ServiceDefinition> stopping=[]; public event EventHandler<LogEntry>? LogReceived; public event EventHandler? StatusChanged; public bool IsServiceRunning(ServiceDefinition s)=>processes.TryGetValue(s,out var p)&&!p.HasExited; public bool IsProjectRunning(ProjectDefinition p)=>p.Services.Any(IsServiceRunning); public IReadOnlyList<string> GetLog(string p)=>logs.TryGetValue(p,out var l)?l:[];
 public async Task StartProjectAsync(ProjectDefinition p){Validate(p);var services=p.Services.Where(x=>x.Enabled&&!IsServiceRunning(x)).OrderBy(x=>x.StartOrder).ToList();CheckPorts(services);p.Status="Starting";foreach(var s in services){Start(p,s);await Task.Delay(200);}p.Status="Running";StatusChanged?.Invoke(this,EventArgs.Empty);}
 public async Task RestartServiceAsync(ProjectDefinition p,ServiceDefinition s){processes.TryGetValue(s,out var process);Stop(p,s);if(process is not null)try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));}catch(TimeoutException){throw new InvalidOperationException($"{s.Name} did not stop within 10 seconds, so it was not restarted.");}Validate(p);CheckPorts([s]);Start(p,s);p.Status="Running";StatusChanged?.Invoke(this,EventArgs.Empty);}
 public Task StartServiceAsync(ProjectDefinition p,ServiceDefinition s){Validate(p);if(IsServiceRunning(s))return Task.CompletedTask;CheckPorts([s]);Start(p,s);p.Status="Running";StatusChanged?.Invoke(this,EventArgs.Empty);return Task.CompletedTask;}
 public void StopService(ProjectDefinition p,ServiceDefinition s){Stop(p,s);p.Status=processes.Keys.Any(x=>p.Services.Contains(x))?"Degraded":"Stopped";StatusChanged?.Invoke(this,EventArgs.Empty);}
 public void StopProject(ProjectDefinition p){foreach(var s in p.Services.ToList())Stop(p,s);p.Status="Stopped";StatusChanged?.Invoke(this,EventArgs.Empty);}
 public void StopAll(IEnumerable<ProjectDefinition> projects){foreach(var project in projects.ToList())StopProject(project);}
 void Start(ProjectDefinition p,ServiceDefinition s){var command=Expander.Expand(s.Command,p,s);var startInfo=new ProcessStartInfo("cmd.exe","/c "+command){WorkingDirectory=p.WorkingDirectory,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var variable in p.EnvironmentVariables.Where(x=>!string.IsNullOrWhiteSpace(x.Name)))startInfo.Environment[variable.Name]=variable.Value??"";var x=new Process{StartInfo=startInfo,EnableRaisingEvents=true};x.OutputDataReceived+=(_,e)=>{if(e.Data is not null)Log(p.Name,s.Name,e.Data);};x.ErrorDataReceived+=(_,e)=>{if(e.Data is not null)Log(p.Name,s.Name,"ERR "+e.Data);};x.Exited+=(_,_)=>{var wasStopped=stopping.Remove(s);processes.Remove(s);var code=x.ExitCode;s.Status=wasStopped?"Stopped":code==0?"Stopped":"Failed";p.Status=processes.Keys.Any(z=>p.Services.Contains(z))?"Degraded":s.Status=="Failed"?"Failed":"Stopped";Log(p.Name,s.Name,wasStopped?"Stopped.":$"Process exited with code {code}.");StatusChanged?.Invoke(this,EventArgs.Empty);};if(!x.Start())throw new InvalidOperationException($"Could not start {s.Name}.");processes[s]=x;s.Status="Running";x.BeginOutputReadLine();x.BeginErrorReadLine();Log(p.Name,s.Name,$"Started (PID {x.Id}): {command}");}
 void Stop(ProjectDefinition p,ServiceDefinition s){if(!processes.Remove(s,out var x))return;try{stopping.Add(s);if(!x.HasExited)x.Kill(true);Log(p.Name,s.Name,"Stop requested.");}catch(InvalidOperationException){}finally{s.Status="Stopped";}}
 static void Validate(ProjectDefinition p){if(string.IsNullOrWhiteSpace(p.Name))throw new InvalidOperationException("A project name is required.");if(string.IsNullOrWhiteSpace(p.WorkingDirectory)||!Directory.Exists(p.WorkingDirectory))throw new InvalidOperationException("Choose an existing working directory before starting.");foreach(var s in p.Services.Where(x=>x.Enabled))if(string.IsNullOrWhiteSpace(s.Name)||string.IsNullOrWhiteSpace(s.Command)||s.Port is <1 or >65535)throw new InvalidOperationException("Every enabled service needs a name, command, and valid port.");if(p.Services.Where(x=>x.Enabled).GroupBy(x=>x.Port).Any(x=>x.Count()>1))throw new InvalidOperationException("Enabled services cannot use the same port.");}
 static void CheckPorts(IEnumerable<ServiceDefinition> ss){var ports=IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(x=>x.Port).ToHashSet();var bad=ss.FirstOrDefault(x=>ports.Contains(x.Port));if(bad is not null)throw new InvalidOperationException($"Port {bad.Port} is already in use. Dev Server Manager will not stop an unknown process.");}
 void Log(string p,string s,string m){var text=$"[{DateTime.Now:HH:mm:ss}] {s}: {m}";if(!logs.TryGetValue(p,out var l))logs[p]=l=[];l.Add(text);if(l.Count>1000)l.RemoveAt(0);LogReceived?.Invoke(this,new(p,text));}}
