using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace DevServerManager;
public partial class MainWindow : Window, INotifyPropertyChanged
{
    readonly ConfigurationStore store = new(); readonly UiSettingsStore uiSettings = new(); readonly ServerProcessManager manager = new(); readonly List<ExternalNodeProcess> stoppedExternalProcesses = []; readonly List<ManagedServiceRow> scannedRows = []; readonly Dictionary<string, bool> groupExpansion; System.Windows.Controls.Button? maximizeButton; ProjectDefinition? selectedProject; int? scannedPort; string footerMessage = "Ready", logFilter = ""; ThemeMode themeMode; bool initialized, shutdownStarted, shutdownComplete, refreshingServers, refreshingProject;
    public ObservableCollection<ProjectDefinition> Projects { get; } = []; public ObservableCollection<ManagedServiceRow> ManagedServices { get; } = []; public ObservableCollection<ServerSourceGroup> ServerGroups { get; } = []; public IReadOnlyList<ServiceTemplate> Templates { get; } = Enum.GetValues<ServiceTemplate>(); public IReadOnlyList<ThemeMode> ThemeOptions { get; } = Enum.GetValues<ThemeMode>();
    public ProjectDefinition? SelectedProject { get => selectedProject; set { selectedProject = value; Changed(nameof(SelectedProject)); RefreshEnvironmentTargets(); RefreshLog(); } } public string FooterMessage { get => footerMessage; set { footerMessage = value; Changed(nameof(FooterMessage)); } } public ThemeMode AppTheme { get => themeMode; set { themeMode = value; Changed(nameof(AppTheme)); } }
    public MainWindow() { themeMode = uiSettings.LoadTheme(); groupExpansion = uiSettings.LoadServerGroupExpansion(); ThemeManager.Apply(themeMode); InitializeComponent(); CreateCustomChrome(); DataContext = this; SetNavigationActive(0); KeyDown += MainWindow_KeyDown; ConfigureServiceMenu(); manager.LogReceived += (_, e) => Dispatcher.Invoke(() => { if (e.Project == SelectedProject?.Name) AppendLog(e.Text); }); manager.StatusChanged += (_, _) => Dispatcher.Invoke(() => { RefreshLog(); RefreshManagedServices(); }); foreach (var p in store.Load()) Projects.Add(p); SelectedProject = Projects.FirstOrDefault(); RefreshManagedServices(); initialized = true; FooterMessage = Projects.Count == 0 ? "Add a project to begin." : $"Loaded {Projects.Count} project(s)."; }
    void CreateCustomChrome() { WindowStyle = WindowStyle.None; var root = (Grid)Content; ((FrameworkElement)root.Children[0]).Margin = new Thickness(0, 42, 0, 0); MainTabs.Margin = new Thickness(30, 52, 30, 18); System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome { CaptionHeight = 42, ResizeBorderThickness = new Thickness(6) }); var bar = new Grid { Height = 42, Background = (System.Windows.Media.Brush)FindResource("Sidebar"), VerticalAlignment = VerticalAlignment.Top }; bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(138) }); var label = new TextBlock { Text = ">_  DEVFLOW  /  DEV SERVER MANAGER", Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) }; bar.Children.Add(label); var controls = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right }; foreach (var pair in new[] { ("—", new RoutedEventHandler(Minimize_Click)), ("□", new RoutedEventHandler(Maximize_Click)), ("×", new RoutedEventHandler(Close_Click)) }) { var button = new System.Windows.Controls.Button { Content = pair.Item1, Width = 46, Padding = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent, Foreground = System.Windows.Media.Brushes.White, BorderThickness = new Thickness(0) }; button.Click += pair.Item2; if (pair.Item1 == "□") maximizeButton = button; System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button, true); controls.Children.Add(button); } Grid.SetColumn(controls, 1); bar.Children.Add(controls); Grid.SetColumnSpan(bar, 2); Grid.SetZIndex(bar, 100); root.Children.Add(bar); StateChanged += (_, _) => UpdateMaximizeGlyph(); UpdateMaximizeGlyph(); }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(WindowMessage);
    }
    IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == MaximizeWorkArea.WmGetMinMaxInfo) handled = MaximizeWorkArea.Apply(window, lParam);
        return IntPtr.Zero;
    }
    void Save() { store.Save(Projects); FooterMessage = "Configuration saved locally."; }
    void AddProject_Click(object s, RoutedEventArgs e) { var p = new ProjectDefinition { Name = "New project" }; p.Services.Add(ServiceDefinition.Web(4201)); p.Services.Add(ServiceDefinition.Api(3001)); Projects.Add(p); SelectedProject = p; MainTabs.SelectedIndex = 0; SetNavigationActive(0); RefreshManagedServices(); Save(); }
    void RemoveProject_Click(object s, RoutedEventArgs e) { if (SelectedProject is null) return; if (manager.IsProjectRunning(SelectedProject)) { Warn("Stop this project before removing it."); return; } if (MessageBox.Show($"Remove '{SelectedProject.Name}' from the manager? Its files will not be deleted.", "Remove project", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; Projects.Remove(SelectedProject); SelectedProject = Projects.FirstOrDefault(); RefreshManagedServices(); Save(); }
    void BrowseFolder_Click(object s, RoutedEventArgs e) { if (SelectedProject is null) return; var d = new OpenFolderDialog(); if (d.ShowDialog() == true) SelectedProject.WorkingDirectory = d.FolderName; }
    void AddService_Click(object s, RoutedEventArgs e) { if (SelectedProject is null) return; var port = SelectedProject.Services.Any() ? SelectedProject.Services.Max(x => x.Port) + 1 : 4201; SelectedProject.Services.Add(new ServiceDefinition { Name = "Service", Port = port, Command = "npm run dev -- --port {port}", Template = ServiceTemplate.Custom, StartOrder = 30 }); RefreshEnvironmentTargets(); RefreshManagedServices(); Save(); }
    void AddEnvironmentVariable_Click(object s, RoutedEventArgs e) { if (SelectedProject is null) return; var variable = new EnvironmentVariableDefinition { Name = "VARIABLE_NAME", Value = "", Targets = SelectedProject.EnvironmentTargets }; SelectedProject.EnvironmentVariables.Add(variable); EnvironmentGrid.SelectedItem = variable; Save(); }
    void RemoveEnvironmentVariable_Click(object s, RoutedEventArgs e) { if (SelectedProject is null || EnvironmentGrid.SelectedItem is not EnvironmentVariableDefinition variable) return; SelectedProject.EnvironmentVariables.Remove(variable); Save(); }
    void RemoveService_Click(object s, RoutedEventArgs e) { if (SelectedProject is null || ServiceGrid.SelectedItem is not ServiceDefinition x) return; if (manager.IsServiceRunning(x)) { Warn("Stop this service before removing it."); return; } var targeted = SelectedProject.EnvironmentVariables.Where(variable => variable.ServiceId == x.Id).ToList(); if (targeted.Count > 0 && MessageBox.Show($"Remove {x.Name} and its {targeted.Count} service-specific environment variable(s)?", "Remove service", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; foreach (var variable in targeted) SelectedProject.EnvironmentVariables.Remove(variable); SelectedProject.Services.Remove(x); RefreshEnvironmentTargets(); RefreshManagedServices(); Save(); }
    async void StartProject_Click(object s, RoutedEventArgs e) { if (SelectedProject is null) return; Save(); try { await manager.StartProjectAsync(SelectedProject); FooterMessage = $"Started {SelectedProject.Name}."; } catch (Exception ex) { Warn(ex.Message); } }
    async void RefreshProject_Click(object s, RoutedEventArgs e)
    {
        if (SelectedProject is null || refreshingProject) return;
        var project = SelectedProject;
        refreshingProject = true;
        RefreshProjectButton.IsEnabled = false;
        FooterMessage = $"Checking {project.Name}'s service ports…";
        try
        {
            var ports = project.Services.Select(service => service.Port).Where(port => port is >= 1 and <= 65535).ToHashSet();
            var listeners = await Task.Run(() => TcpPortOwners.FindListeners(ports));
            ProjectStatusRefresher.Apply(project, listeners, manager.IsServiceRunning);
            var projects = Projects.ToList();
            var discovered = await Task.Run(() => NodeProcessDiscovery.Find(projects));
            RefreshManagedServices(discovered);
            FooterMessage = $"Refreshed {project.Name}: {listeners.Count} listener(s) on configured ports.";
        }
        catch (Exception ex) { Warn($"Could not refresh {project.Name}: {ex.Message}"); }
        finally { refreshingProject = false; RefreshProjectButton.IsEnabled = true; }
    }
    async void StopProject_Click(object s, RoutedEventArgs e) { if (SelectedProject is null || !StopProjectButton.IsEnabled) return; var project = SelectedProject; StopProjectButton.IsEnabled = false; FooterMessage = $"Stopping {project.Name}…"; try { var clean = await manager.StopProjectAsync(project); FooterMessage = clean ? $"Stopped {project.Name}." : $"Stopped {project.Name}, but a configured port could not be cleared. Check its log."; } catch (Exception ex) { Warn($"Could not stop {project.Name}: {ex.Message}"); } finally { StopProjectButton.IsEnabled = true; } }
    async void RestartService_Click(object s, RoutedEventArgs e) { if (SelectedProject is null || ServiceGrid.SelectedItem is not ServiceDefinition x) return; try { await manager.RestartServiceAsync(SelectedProject, x); } catch (Exception ex) { Warn(ex.Message); } }
    void OpenWeb_Click(object s, RoutedEventArgs e) { if (SelectedProject is null) return; var x = SelectedProject.Services.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.BrowserUrl)); if (x is null) { Warn("Configure a Browser URL on a service first."); return; } Process.Start(new ProcessStartInfo(Expander.Expand(x.BrowserUrl, SelectedProject, x)) { UseShellExecute = true }); }
    void ProjectList_SelectionChanged(object s, SelectionChangedEventArgs e) { RefreshLog(); if (!initialized || e.AddedItems.Count == 0) return; MainTabs.SelectedIndex = 0; SetNavigationActive(0); }
    void ProjectList_MouseLeftButtonUp(object s, MouseButtonEventArgs e) { if (s is ItemsControl list && e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(list, source) is ListBoxItem) { MainTabs.SelectedIndex = 0; SetNavigationActive(0); } }
    void ServiceGrid_CellEditEnding(object s, DataGridCellEditEndingEventArgs e) => Dispatcher.BeginInvoke(() => { if (e.Column.Header?.ToString() == "Template" && ServiceGrid.SelectedItem is ServiceDefinition service) service.ApplyTemplate(); RefreshEnvironmentTargets(); RefreshManagedServices(); Save(); }); void EnvironmentGrid_CellEditEnding(object s, DataGridCellEditEndingEventArgs e) => Dispatcher.BeginInvoke(Save);
    void EnvironmentTarget_DropDownClosed(object s, EventArgs e) { if (IsLoaded) Dispatcher.BeginInvoke(Save); }
    void RefreshEnvironmentTargets() => SelectedProject?.PrepareEnvironmentTargets();
    async void Window_Closing(object? s, CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App app && !app.IsExplicitExit) { e.Cancel = true; Hide(); app.ShowTrayNotification(); FooterMessage = "Manager is still running in the system tray."; return; }
        if (shutdownComplete) return;
        e.Cancel = true;
        if (shutdownStarted) return;
        shutdownStarted = true;
        FooterMessage = "Stopping project servers before exit…";
        try { await manager.StopAllAsync(Projects); }
        catch (Exception ex) { FooterMessage = $"Shutdown cleanup failed: {ex.Message}"; }
        finally { try { Save(); } catch (Exception ex) { FooterMessage = $"Could not save before exit: {ex.Message}"; } shutdownComplete = true; Close(); }
    }
    void ShowServers_Click(object s, RoutedEventArgs e) { MainTabs.SelectedIndex = 1; SetNavigationActive(1); RefreshManagedServices(); }
    void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Maximize_Click(object s, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void UpdateMaximizeGlyph() { if (maximizeButton is not null) maximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□"; }
    void Close_Click(object s, RoutedEventArgs e) => Close();
    void ThemeSelector_SelectionChanged(object s, SelectionChangedEventArgs e) { if (!IsLoaded) return; ThemeManager.Apply(AppTheme); uiSettings.SaveTheme(AppTheme); FooterMessage = AppTheme == DevServerManager.ThemeMode.Device ? "Theme follows your device preference." : $"{AppTheme} theme applied."; }
    void MainWindow_KeyDown(object s, System.Windows.Input.KeyEventArgs e) { if (System.Windows.Input.Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control) return; if (e.Key == System.Windows.Input.Key.I) ImportLaunchers(); if (e.Key == System.Windows.Input.Key.P) PreviewSelectedCommand(); if (e.Key == System.Windows.Input.Key.F) SetLogFilter(); if (e.Key == System.Windows.Input.Key.T && ServiceGrid.SelectedItem is ServiceDefinition service) { service.ApplyTemplate(); Save(); FooterMessage = "Template applied; the command remains editable."; } }
    void PreviewSelectedCommand() { if (SelectedProject is null || ServiceGrid.SelectedItem is not ServiceDefinition service) return; MessageBox.Show(Expander.Expand(service.Command, SelectedProject, service), "Final command preview", MessageBoxButton.OK, MessageBoxImage.Information); }
    void ImportLaunchers() { var dialog = new OpenFileDialog { Title = "Select .bat launcher files to import", Filter = "Batch launchers (*.bat)|*.bat", Multiselect = true }; if (dialog.ShowDialog() != true) return; var project = BatchLauncherImporter.Import(dialog.FileNames); Projects.Add(project); SelectedProject = project; Save(); FooterMessage = $"Imported {project.Services.Count} launcher(s). Review commands before starting."; }
    async void StartSelectedService() { if (SelectedProject is null || ServiceGrid.SelectedItem is not ServiceDefinition service) return; try { await manager.StartServiceAsync(SelectedProject, service); FooterMessage = $"Started {service.Name}."; } catch (Exception ex) { Warn(ex.Message); } }
    void StopSelectedService() { if (SelectedProject is null || ServiceGrid.SelectedItem is not ServiceDefinition service) return; manager.StopService(SelectedProject, service); FooterMessage = $"Stopped {service.Name}."; }
    void ConfigureServiceMenu() { var menu = new ContextMenu(); var start = new MenuItem { Header = "Start selected service" }; start.Click += (_, _) => StartSelectedService(); var stop = new MenuItem { Header = "Stop selected service" }; stop.Click += (_, _) => StopSelectedService(); var restart = new MenuItem { Header = "Restart selected service" }; restart.Click += (_, _) => RestartService_Click(this, new RoutedEventArgs()); var preview = new MenuItem { Header = "Preview final command" }; preview.Click += (_, _) => PreviewSelectedCommand(); menu.Items.Add(start); menu.Items.Add(stop); menu.Items.Add(restart); menu.Items.Add(new Separator()); menu.Items.Add(preview); ServiceGrid.ContextMenu = menu; }
    void SetLogFilter() { var value = PromptDialog.Show("Filter log", "Show only log lines containing (leave blank to clear):", logFilter); if (value is null) return; logFilter = value; FooterMessage = string.IsNullOrWhiteSpace(logFilter) ? "Log filter cleared." : $"Log filter: {logFilter}"; RefreshLog(); }
    async void StartServer_Click(object s, RoutedEventArgs e) { if ((s as FrameworkElement)?.Tag is not ManagedServiceRow row) return; if (row.IsScanned) { await StartScannedProcessAsync(row); return; } try { if (row.IsExternal) { if (row.Status == "Running") return; row.Status = "Starting"; await Task.Run(() => NodeProcessDiscovery.Start(row.Command, row.SourceLocation)); stoppedExternalProcesses.RemoveAll(x => x.ProcessId == row.ProcessId); row.Status = "Running"; } else if (row.Project is not null && row.Service is not null) { await manager.StartServiceAsync(row.Project, row.Service); RefreshManagedServices(); } FooterMessage = $"Started {row.ProjectName} / {row.ServiceName}."; } catch (Exception ex) { row.Status = "Stopped"; Warn(ex.Message); } }
    async void StopServer_Click(object s, RoutedEventArgs e) { if ((s as FrameworkElement)?.Tag is not ManagedServiceRow row) return; if (row.IsScanned) { await StopScannedProcessAsync(row); return; } if (row.IsExternal) { if (row.Status != "Running") return; if (MessageBox.Show($"Stop Node process {row.ProcessId}?\n\nSource: {row.SourceLocation}\n\nCommand: {row.Command}", "Stop external Node process", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { row.Status = "Stopping"; await Task.Run(() => NodeProcessDiscovery.Stop(row.ProcessId)); stoppedExternalProcesses.RemoveAll(x => x.ProcessId == row.ProcessId); stoppedExternalProcesses.Add(new ExternalNodeProcess(row.ProcessId, row.Command, row.SourceLocation, row.CanRestart, false)); row.Status = "Stopped"; } catch (Exception ex) { row.Status = "Running"; Warn(ex.Message); return; } } else if (row.Project is not null && row.Service is not null) { manager.StopService(row.Project, row.Service); RefreshManagedServices(); } FooterMessage = $"Stopped {row.ProjectName} / {row.ServiceName}."; }
    async void ScanPort_Click(object s, RoutedEventArgs e)
    {
        if (!int.TryParse(ScanPortInput.Text.Trim(), out var port) || port is < 1 or > 65535) { Warn("Enter a TCP port from 1 to 65535."); return; }
        if (scannedPort != port) scannedRows.Clear();
        scannedPort = port;
        ScanPortButton.IsEnabled = false;
        try { await RefreshScannedPortAsync(); FooterMessage = $"Scanned port {port}."; }
        catch (Exception ex) { Warn($"Could not scan port {port}: {ex.Message}"); }
        finally { ScanPortButton.IsEnabled = true; }
    }
    async Task RefreshScannedPortAsync()
    {
        if (scannedPort is not int port) return;
        var projects = Projects.ToList();
        var scan = await Task.Run(() => (Found: PortProcessDiscovery.Find(port, projects), Nodes: NodeProcessDiscovery.Find(projects)));
        if (scannedPort == port) { ApplyScannedPort(scan.Found); RefreshManagedServices(scan.Nodes); }
    }
    void ApplyScannedPort(IReadOnlyList<PortProcessInfo> found)
    {
        if (scannedPort is not int port) return;
        var remembered = scannedRows.Where(row => row.Port == port && (row.Status is "Stopped" or "Starting") && (row.ProcessId == 0 || found.All(process => process.ProcessId != row.ProcessId))).ToList();
        scannedRows.Clear();
        foreach (var process in found)
        {
            var match = Projects.SelectMany(project => project.Services.Where(service => service.Port == port).Select(service => (Project: project, Service: service)))
                .FirstOrDefault(pair => !string.IsNullOrWhiteSpace(pair.Project.WorkingDirectory) && process.Command.Contains(pair.Project.WorkingDirectory, StringComparison.OrdinalIgnoreCase));
            scannedRows.Add(new ManagedServiceRow { IsScanned = true, IsExternal = true, CanRestart = true, ProcessId = process.ProcessId, Port = port, Project = match.Project, Service = match.Service, ProjectName = match.Project?.Name ?? "Scanned port", ServiceName = process.Name, SourceLocation = process.WorkingDirectory, GroupLocation = "Scanned port", Command = process.Command, Status = "Running" });
        }
        if (found.Count == 0)
        {
            scannedRows.AddRange(remembered);
            if (scannedRows.Count == 0)
            {
                var match = Projects.SelectMany(project => project.Services.Where(service => service.Port == port).Select(service => (Project: project, Service: service))).FirstOrDefault();
                scannedRows.Add(new ManagedServiceRow { IsScanned = true, IsExternal = true, CanRestart = true, Port = port, Project = match.Project, Service = match.Service, ProjectName = match.Project?.Name ?? "Scanned port", ServiceName = match.Service?.Name ?? "No listener", SourceLocation = match.Project?.WorkingDirectory ?? "Unknown", GroupLocation = "Scanned port", Command = match.Service?.Command ?? "", Status = "No listener" });
            }
        }
    }
    async Task StopScannedProcessAsync(ManagedServiceRow row)
    {
        if (row.ProcessId <= 0 || row.Status != "Running") return;
        if (MessageBox.Show($"Stop PID {row.ProcessId} listening on port {row.Port}?\n\nProcess: {row.ServiceName}\nCommand: {row.Command}", "Stop scanned process", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        row.Status = "Stopping";
        try { await PortProcessDiscovery.StopAsync(row.Port, row.ProcessId); row.Status = "Stopped"; await RefreshScannedPortAsync(); FooterMessage = $"Stopped PID {row.ProcessId} on port {row.Port}."; }
        catch (Exception ex) { row.Status = "Running"; Warn($"Could not stop PID {row.ProcessId}: {ex.Message}"); }
    }
    async Task StartScannedProcessAsync(ManagedServiceRow row)
    {
        if (row.Status == "Running") return;
        try
        {
            if (row.Project is not null && row.Service is not null)
            {
                if (manager.IsServiceRunning(row.Service)) await manager.RestartServiceAsync(row.Project, row.Service);
                else await manager.StartServiceAsync(row.Project, row.Service);
            }
            else
            {
                var command = PromptDialog.Show("Start scanned port", $"Launch command for port {row.Port}:", row.Command);
                if (command is null) return;
                var directory = PromptDialog.Show("Working directory", "Working directory for this command:", Directory.Exists(row.SourceLocation) ? row.SourceLocation : Environment.CurrentDirectory);
                if (directory is null) return;
                await Task.Run(() => PortProcessDiscovery.Start(row.Port, command, directory));
                scannedRows.Remove(row);
                scannedRows.Add(new ManagedServiceRow { IsScanned = true, IsExternal = true, CanRestart = true, Port = row.Port, ProjectName = "Scanned port", ServiceName = row.ServiceName, SourceLocation = directory, GroupLocation = "Scanned port", Command = command, Status = "Starting" });
            }
            if (row.Project is not null && row.Service is not null) row.Status = "Starting";
            await Task.Delay(500);
            await RefreshScannedPortAsync();
            FooterMessage = $"Started a process for port {row.Port}. Refresh if it is still binding.";
        }
        catch (Exception ex) { Warn($"Could not start port {row.Port}: {ex.Message}"); }
    }
    async void RefreshServers_Click(object s, RoutedEventArgs e)
    {
        if (refreshingServers) return;
        refreshingServers = true;
        RefreshServersButton.IsEnabled = false;
        FooterMessage = "Refreshing running servers…";
        try
        {
            var projects = Projects.ToList();
            var port = scannedPort;
            var scan = await Task.Run(() => (Nodes: NodeProcessDiscovery.Find(projects), Scanned: port is int number ? PortProcessDiscovery.Find(number, projects) : null));
            if (port == scannedPort && scan.Scanned is not null) ApplyScannedPort(scan.Scanned);
            RefreshManagedServices(scan.Nodes);
            FooterMessage = "Running servers refreshed.";
        }
        catch (Exception ex) { Warn($"Could not refresh running servers: {ex.Message}"); }
        finally { refreshingServers = false; RefreshServersButton.IsEnabled = true; }
    }
    void RefreshManagedServices(IReadOnlyList<ExternalNodeProcess>? discovered = null)
    {
        ManagedServices.Clear();
        foreach (var row in Projects.SelectMany(project => project.Services.Where(IsNodeService).Select(service => ManagedServiceRow.FromManaged(project, service, 0)))) ManagedServices.Add(row);
        foreach (var process in discovered ?? NodeProcessDiscovery.Find(Projects)) ManagedServices.Add(new ManagedServiceRow { IsExternal = true, ProcessId = process.ProcessId, ProjectName = "External Node", ServiceName = "node.exe", SourceLocation = process.SourceLocation, Command = process.Command, Status = "Running", CanRestart = process.CanRestart });
        foreach (var process in stoppedExternalProcesses) if (process.CanRestart) ManagedServices.Add(new ManagedServiceRow { IsExternal = true, ProcessId = process.ProcessId, ProjectName = "External Node", ServiceName = "node.exe", SourceLocation = process.SourceLocation, Command = process.Command, Status = "Stopped", CanRestart = true });
        foreach (var row in scannedRows) ManagedServices.Add(row);
        foreach (var group in ServerGroups) groupExpansion[group.SourceLocation] = group.IsExpanded;
        ServerGroups.Clear();
        foreach (var group in ManagedServices.GroupBy(x => string.IsNullOrWhiteSpace(x.GroupLocation) ? x.SourceLocation : x.GroupLocation).OrderBy(x => x.Key))
        {
            var sourceGroup = new ServerSourceGroup(group.Key, group, groupExpansion.GetValueOrDefault(group.Key, group.Key == "Scanned port"));
            sourceGroup.PropertyChanged += (_, e) => { if (e.PropertyName != nameof(ServerSourceGroup.IsExpanded)) return; groupExpansion[sourceGroup.SourceLocation] = sourceGroup.IsExpanded; uiSettings.SaveServerGroupExpansion(groupExpansion); };
            ServerGroups.Add(sourceGroup);
        }
    }
    void SetNavigationActive(int page) { NavServers.Style = (Style)FindResource(page == 1 ? "SidebarNavActive" : "SidebarNavButton"); }
    void ServerScroll_PreviewMouseWheel(object s, MouseWheelEventArgs e)
    {
        if (s is not ScrollViewer viewer) return;
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }
    static bool IsNodeService(ServiceDefinition service) => service.Command.Contains("node", StringComparison.OrdinalIgnoreCase) || service.Command.Contains("npm", StringComparison.OrdinalIgnoreCase) || service.Command.Contains("npx", StringComparison.OrdinalIgnoreCase) || service.Command.Contains("ng ", StringComparison.OrdinalIgnoreCase);
    void RefreshLog() { var entries = SelectedProject is null ? [] : manager.GetLog(SelectedProject.Name); if (!string.IsNullOrWhiteSpace(logFilter)) entries = entries.Where(x => x.Contains(logFilter, StringComparison.OrdinalIgnoreCase)).ToList(); LogOutput.Text = string.Join(Environment.NewLine, entries); LogOutput.ScrollToEnd(); } void AppendLog(string x) { if (string.IsNullOrWhiteSpace(logFilter) || x.Contains(logFilter, StringComparison.OrdinalIgnoreCase)) { LogOutput.AppendText(x + Environment.NewLine); LogOutput.ScrollToEnd(); } } void Warn(string x) { FooterMessage = x; MessageBox.Show(x, "Dev Server Manager", MessageBoxButton.OK, MessageBoxImage.Warning); }
    public event PropertyChangedEventHandler? PropertyChanged; void Changed(string x) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(x));
}
