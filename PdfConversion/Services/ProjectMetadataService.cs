using PdfConversion.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfConversion.Services;

public class ProjectMetadataService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProjectMetadataService(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<Dictionary<string, Dictionary<string, ProjectMetadata>>> GetAllProjects()
    {
        await _lock.WaitAsync();
        try
        {
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

    private class ProjectMetadataRoot
    {
        [JsonPropertyName("projects")]
        public Dictionary<string, Dictionary<string, ProjectMetadata>> Projects { get; set; } = new();

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }
    }
}
