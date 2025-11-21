using System.Text.Json;
using System.Text.Json.Serialization;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for creating new projects with proper directory structure and metadata
/// </summary>
public class ProjectCreationService : IProjectCreationService
{
    private readonly ILogger<ProjectCreationService> _logger;
    private readonly ProjectMetadataService _metadataService;
    private readonly string _inputBasePath;
    private readonly string _outputBasePath;

    public ProjectCreationService(
        ILogger<ProjectCreationService> logger,
        ProjectMetadataService metadataService)
    {
        _logger = logger;
        _metadataService = metadataService;
        _inputBasePath = "/app/data/input";
        _outputBasePath = "/app/data/output";
    }

    public async Task<ProjectCreationResult> CreateProjectAsync(string customer, string projectId, string projectLabel)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(customer))
                return ProjectCreationResult.Failure("Customer is required");

            if (string.IsNullOrWhiteSpace(projectId))
                return ProjectCreationResult.Failure("Project ID is required");

            if (string.IsNullOrWhiteSpace(projectLabel))
                return ProjectCreationResult.Failure("Project label is required");

            // Normalize customer and projectId (lowercase, no special characters)
            customer = NormalizeIdentifier(customer);
            projectId = NormalizeIdentifier(projectId);

            // Check if project already exists
            if (await ProjectExistsAsync(customer, projectId))
            {
                return ProjectCreationResult.Failure($"Project '{customer}/{projectId}' already exists");
            }

            var createdPaths = new List<string>();

            // Create input directory structure
            var inputProjectPath = Path.Combine(_inputBasePath, customer, "projects", projectId);
            var inputImagesPath = Path.Combine(inputProjectPath, "images", "from-conversion");

            Directory.CreateDirectory(inputProjectPath);
            createdPaths.Add(inputProjectPath);

            Directory.CreateDirectory(inputImagesPath);
            createdPaths.Add(inputImagesPath);

            _logger.LogInformation("Created input directories for {Customer}/{ProjectId}", customer, projectId);

            // Create output directory structure
            var outputProjectPath = Path.Combine(_outputBasePath, customer, "projects", projectId);
            var outputDataPath = Path.Combine(outputProjectPath, "data");

            Directory.CreateDirectory(outputProjectPath);
            createdPaths.Add(outputProjectPath);

            Directory.CreateDirectory(outputDataPath);
            createdPaths.Add(outputDataPath);

            _logger.LogInformation("Created output directories for {Customer}/{ProjectId}", customer, projectId);

            // Update project metadata
            await _metadataService.UpdateProjectLabel(customer, projectId, projectLabel);
            await _metadataService.UpdateProjectStatus(customer, projectId, ProjectLifecycleStatus.Open);

            _logger.LogInformation("Updated metadata for {Customer}/{ProjectId} with label '{Label}'",
                customer, projectId, projectLabel);

            return ProjectCreationResult.Successful(customer, projectId, createdPaths);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project {Customer}/{ProjectId}", customer, projectId);
            return ProjectCreationResult.Failure($"Failed to create project: {ex.Message}");
        }
    }

    public Task<List<string>> GetExistingCustomersAsync()
    {
        try
        {
            if (!Directory.Exists(_inputBasePath))
            {
                return Task.FromResult(new List<string>());
            }

            var customers = Directory.GetDirectories(_inputBasePath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(name => name)
                .ToList();

            return Task.FromResult(customers!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get existing customers");
            return Task.FromResult(new List<string>());
        }
    }

    public Task<bool> ProjectExistsAsync(string customer, string projectId)
    {
        try
        {
            var inputPath = Path.Combine(_inputBasePath, customer, "projects", projectId);
            return Task.FromResult(Directory.Exists(inputPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if project exists: {Customer}/{ProjectId}", customer, projectId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Normalize identifier by converting to lowercase and removing special characters
    /// </summary>
    private string NormalizeIdentifier(string identifier)
    {
        // Remove leading/trailing whitespace
        identifier = identifier.Trim();

        // Convert to lowercase
        identifier = identifier.ToLowerInvariant();

        // Replace spaces with hyphens
        identifier = identifier.Replace(' ', '-');

        // Remove any characters that aren't alphanumeric or hyphens
        identifier = new string(identifier.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        return identifier;
    }
}
