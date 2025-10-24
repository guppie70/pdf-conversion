using PdfConversion.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfConversion.Services;

public class ProjectMetadataService
{
    private readonly string _filePath;
    private readonly string _oldFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProjectMetadataService(string filePath, string? oldFilePath = null)
    {
        _filePath = filePath;
        _oldFilePath = oldFilePath ?? Path.Combine(Path.GetDirectoryName(filePath) ?? "", "project-labels.json");
    }

    public async Task<Dictionary<string, Dictionary<string, ProjectMetadata>>> GetAllProjects()
    {
        await _lock.WaitAsync();
        try
        {
            // Check if migration is needed
            if (!File.Exists(_filePath) && File.Exists(_oldFilePath))
            {
                return await MigrateFromOldFormat();
            }

            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, Dictionary<string, ProjectMetadata>>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var root = JsonSerializer.Deserialize<ProjectMetadataRoot>(json, JsonOptions);
            return root?.Projects ?? new Dictionary<string, Dictionary<string, ProjectMetadata>>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ProjectMetadata?> GetProjectMetadata(string tenant, string projectId)
    {
        var projects = await GetAllProjects();
        if (projects.TryGetValue(tenant, out var tenantProjects))
        {
            tenantProjects.TryGetValue(projectId, out var metadata);
            return metadata;
        }
        return null;
    }

    public async Task<Dictionary<string, Dictionary<string, ProjectMetadata>>> GetActiveProjects()
    {
        var allProjects = await GetAllProjects();
        var activeProjects = new Dictionary<string, Dictionary<string, ProjectMetadata>>();

        foreach (var (tenant, projects) in allProjects)
        {
            var activeInTenant = projects
                .Where(p => p.Value.Status == ProjectLifecycleStatus.Open || p.Value.Status == ProjectLifecycleStatus.InProgress)
                .ToDictionary(p => p.Key, p => p.Value);

            if (activeInTenant.Any())
            {
                activeProjects[tenant] = activeInTenant;
            }
        }

        return activeProjects;
    }

    public async Task UpdateProjectStatus(string tenant, string projectId, ProjectLifecycleStatus newStatus)
    {
        await _lock.WaitAsync();
        try
        {
            var projects = await GetAllProjects();

            if (!projects.ContainsKey(tenant))
            {
                projects[tenant] = new Dictionary<string, ProjectMetadata>();
            }

            if (!projects[tenant].ContainsKey(projectId))
            {
                projects[tenant][projectId] = new ProjectMetadata
                {
                    Label = projectId,
                    Status = newStatus,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };
            }
            else
            {
                projects[tenant][projectId].Status = newStatus;
                projects[tenant][projectId].LastModified = DateTime.UtcNow;
            }

            await SaveProjects(projects);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveProjects(Dictionary<string, Dictionary<string, ProjectMetadata>> projects)
    {
        var root = new ProjectMetadataRoot
        {
            Projects = projects,
            LastModified = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(root, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private async Task<Dictionary<string, Dictionary<string, ProjectMetadata>>> MigrateFromOldFormat()
    {
        if (!File.Exists(_oldFilePath))
        {
            return new Dictionary<string, Dictionary<string, ProjectMetadata>>();
        }

        var json = await File.ReadAllTextAsync(_oldFilePath);
        var oldRoot = JsonSerializer.Deserialize<OldProjectLabelsRoot>(json, JsonOptions);

        if (oldRoot?.Labels == null)
        {
            return new Dictionary<string, Dictionary<string, ProjectMetadata>>();
        }

        var projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>();

        foreach (var (tenant, labels) in oldRoot.Labels)
        {
            projects[tenant] = new Dictionary<string, ProjectMetadata>();
            foreach (var (projectId, label) in labels)
            {
                projects[tenant][projectId] = new ProjectMetadata
                {
                    Label = label,
                    Status = ProjectLifecycleStatus.Open,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };
            }
        }

        // Save to new format
        await SaveProjects(projects);

        // Delete old file
        File.Delete(_oldFilePath);

        return projects;
    }

    private class OldProjectLabelsRoot
    {
        [JsonPropertyName("labels")]
        public Dictionary<string, Dictionary<string, string>> Labels { get; set; } = new();
    }

    private class ProjectMetadataRoot
    {
        [JsonPropertyName("projects")]
        public Dictionary<string, Dictionary<string, ProjectMetadata>> Projects { get; set; } = new();

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }
    }
}
