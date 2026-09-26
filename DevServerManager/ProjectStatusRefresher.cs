namespace DevServerManager;

public static class ProjectStatusRefresher
{
    public static void Apply(ProjectDefinition project, IReadOnlyList<TcpPortOwner> listeners, Func<ServiceDefinition, bool> isTracked)
    {
        foreach (var service in project.Services)
        {
            var owners = listeners.Where(listener => listener.Port == service.Port).Select(listener => listener.ProcessId).Distinct().Order().ToArray();
            service.ListenerPids = string.Join(", ", owners);
            service.Status = owners.Length > 0 ? isTracked(service) ? "Running" : "Listening" : isTracked(service) ? "Starting" : "Stopped";
        }
        var enabled = project.Services.Where(service => service.Enabled).ToList();
        project.Status = enabled.Count == 0 || enabled.All(service => service.Status == "Stopped") ? "Stopped"
            : enabled.All(service => service.Status is "Running" or "Listening") ? "Running"
            : "Degraded";
    }
}
