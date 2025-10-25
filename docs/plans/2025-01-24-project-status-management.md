# Project Status Management Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add project status tracking with inline editing on Home page and filter dropdowns to show only active projects.

**Architecture:** Migrate `project-labels.json` to extensible `project-metadata.json` structure. Create `ProjectMetadataService` for CRUD operations. Update Home page UI with status column and subtle row coloring. Filter Transform/Convert dropdowns to exclude stale projects.

**Tech Stack:** C# .NET 9, Blazor Server, System.Text.Json, bUnit, Playwright

---

## Task 1: Create ProjectMetadata Models

**Files:**
- Create: `PdfConversion/Models/ProjectMetadata.cs`
- Create: `PdfConversion/Models/ProjectStatus.cs`

**Step 1: Write the failing test**

Create: `PdfConversion/Tests/Unit/ProjectMetadataTests.cs`

```csharp
using PdfConversion.Models;
using Xunit;

namespace PdfConversion.Tests.Unit;

public class ProjectMetadataTests
{
    [Fact]
    public void ProjectMetadata_DefaultStatus_ShouldBeOpen()
    {
        var metadata = new ProjectMetadata
        {
            Label = "Test Project"
        };

        Assert.Equal(ProjectStatus.Open, metadata.Status);
    }

    [Fact]
    public void ProjectStatus_ShouldHaveFourValues()
    {
        var values = Enum.GetValues<ProjectStatus>();
        Assert.Equal(4, values.Length);
        Assert.Contains(ProjectStatus.Open, values);
        Assert.Contains(ProjectStatus.InProgress, values);
        Assert.Contains(ProjectStatus.Ready, values);
        Assert.Contains(ProjectStatus.Parked, values);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataTests" --verbosity normal
```

Expected: FAIL with "type or namespace 'ProjectMetadata' could not be found"

**Step 3: Create ProjectStatus enum**

Create: `PdfConversion/Models/ProjectStatus.cs`

```csharp
namespace PdfConversion.Models;

public enum ProjectStatus
{
    Open,
    InProgress,
    Ready,
    Parked
}
```

**Step 4: Create ProjectMetadata class**

Create: `PdfConversion/Models/ProjectMetadata.cs`

```csharp
using System.Text.Json.Serialization;

namespace PdfConversion.Models;

public class ProjectMetadata
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectStatus Status { get; set; } = ProjectStatus.Open;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
```

**Step 5: Run test to verify it passes**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataTests" --verbosity normal
```

Expected: PASS (2 tests)

**Step 6: Commit**

```bash
git add PdfConversion/Models/ProjectMetadata.cs PdfConversion/Models/ProjectStatus.cs PdfConversion/Tests/Unit/ProjectMetadataTests.cs
git commit -m "Add ProjectMetadata and ProjectStatus models"
```

---

## Task 2: Create ProjectMetadataService with File I/O

**Files:**
- Create: `PdfConversion/Services/ProjectMetadataService.cs`
- Create: `PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs`

**Step 1: Write the failing test**

Create: `PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs`

```csharp
using PdfConversion.Models;
using PdfConversion.Services;
using System.Text.Json;
using Xunit;

namespace PdfConversion.Tests.Integration;

public class ProjectMetadataServiceTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly ProjectMetadataService _service;

    public ProjectMetadataServiceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test-metadata-{Guid.NewGuid()}.json");
        _service = new ProjectMetadataService(_testFilePath);
    }

    [Fact]
    public async Task GetAllProjects_EmptyFile_ReturnsEmptyDictionary()
    {
        var projects = await _service.GetAllProjects();
        Assert.NotNull(projects);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task UpdateProjectStatus_NewProject_CreatesMetadata()
    {
        await _service.UpdateProjectStatus("optiver", "ar24-1", ProjectStatus.InProgress);

        var metadata = await _service.GetProjectMetadata("optiver", "ar24-1");
        Assert.NotNull(metadata);
        Assert.Equal(ProjectStatus.InProgress, metadata.Status);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataServiceTests" --verbosity normal
```

Expected: FAIL with "type or namespace 'ProjectMetadataService' could not be found"

**Step 3: Create ProjectMetadataService skeleton**

Create: `PdfConversion/Services/ProjectMetadataService.cs`

```csharp
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

    public async Task UpdateProjectStatus(string tenant, string projectId, ProjectStatus newStatus)
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
```

**Step 4: Run test to verify it passes**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataServiceTests" --verbosity normal
```

Expected: PASS (2 tests)

**Step 5: Commit**

```bash
git add PdfConversion/Services/ProjectMetadataService.cs PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs
git commit -m "Add ProjectMetadataService with basic CRUD operations"
```

---

## Task 3: Add Migration Logic to ProjectMetadataService

**Files:**
- Modify: `PdfConversion/Services/ProjectMetadataService.cs`
- Create: `PdfConversion/Tests/Integration/ProjectMetadataMigrationTests.cs`

**Step 1: Write the failing test**

Create: `PdfConversion/Tests/Integration/ProjectMetadataMigrationTests.cs`

```csharp
using PdfConversion.Models;
using PdfConversion.Services;
using System.Text.Json;
using Xunit;

namespace PdfConversion.Tests.Integration;

public class ProjectMetadataMigrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _oldFilePath;
    private readonly string _newFilePath;

    public ProjectMetadataMigrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"test-migration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _oldFilePath = Path.Combine(_testDir, "project-labels.json");
        _newFilePath = Path.Combine(_testDir, "project-metadata.json");
    }

    [Fact]
    public async Task GetAllProjects_OldFormatExists_MigratesAndDeletesOld()
    {
        // Create old format file
        var oldFormat = new
        {
            labels = new
            {
                optiver = new Dictionary<string, string>
                {
                    ["ar24-3"] = "Optiver Australia Holdings Pty Limited",
                    ["ar24-5"] = "Optiver Services B.V."
                }
            },
            lastModified = DateTime.UtcNow
        };
        await File.WriteAllTextAsync(_oldFilePath, JsonSerializer.Serialize(oldFormat, new JsonSerializerOptions { WriteIndented = true }));

        var service = new ProjectMetadataService(_newFilePath, _oldFilePath);
        var projects = await service.GetAllProjects();

        // Verify migration
        Assert.True(File.Exists(_newFilePath), "New metadata file should exist");
        Assert.False(File.Exists(_oldFilePath), "Old labels file should be deleted");
        Assert.Contains("optiver", projects.Keys);
        Assert.Equal(2, projects["optiver"].Count);
        Assert.Equal("Optiver Australia Holdings Pty Limited", projects["optiver"]["ar24-3"].Label);
        Assert.Equal(ProjectStatus.Open, projects["optiver"]["ar24-3"].Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataMigrationTests" --verbosity normal
```

Expected: FAIL with "constructor does not accept 2 parameters"

**Step 3: Add migration logic to ProjectMetadataService**

Modify: `PdfConversion/Services/ProjectMetadataService.cs`

```csharp
// Update constructor
private readonly string _oldFilePath;

public ProjectMetadataService(string filePath, string? oldFilePath = null)
{
    _filePath = filePath;
    _oldFilePath = oldFilePath ?? Path.Combine(Path.GetDirectoryName(filePath) ?? "", "project-labels.json");
}

// Add after GetAllProjects method
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
                Status = ProjectStatus.Open,
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

// Update GetAllProjects to check for migration
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

// Add at end of class
private class OldProjectLabelsRoot
{
    [JsonPropertyName("labels")]
    public Dictionary<string, Dictionary<string, string>> Labels { get; set; } = new();
}
```

**Step 4: Run test to verify it passes**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataMigrationTests" --verbosity normal
```

Expected: PASS

**Step 5: Commit**

```bash
git add PdfConversion/Services/ProjectMetadataService.cs PdfConversion/Tests/Integration/ProjectMetadataMigrationTests.cs
git commit -m "Add migration logic from project-labels.json to project-metadata.json"
```

---

## Task 4: Add GetActiveProjects Method

**Files:**
- Modify: `PdfConversion/Services/ProjectMetadataService.cs`
- Modify: `PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs`

**Step 1: Write the failing test**

Modify: `PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs`

Add this test method:

```csharp
[Fact]
public async Task GetActiveProjects_FiltersOutReadyAndParked()
{
    await _service.UpdateProjectStatus("optiver", "ar24-1", ProjectStatus.Open);
    await _service.UpdateProjectStatus("optiver", "ar24-2", ProjectStatus.InProgress);
    await _service.UpdateProjectStatus("optiver", "ar24-3", ProjectStatus.Ready);
    await _service.UpdateProjectStatus("optiver", "ar24-4", ProjectStatus.Parked);

    var active = await _service.GetActiveProjects();

    Assert.Equal(2, active["optiver"].Count);
    Assert.Contains("ar24-1", active["optiver"].Keys);
    Assert.Contains("ar24-2", active["optiver"].Keys);
    Assert.DoesNotContain("ar24-3", active["optiver"].Keys);
    Assert.DoesNotContain("ar24-4", active["optiver"].Keys);
}
```

**Step 2: Run test to verify it fails**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataServiceTests.GetActiveProjects" --verbosity normal
```

Expected: FAIL with "'ProjectMetadataService' does not contain a definition for 'GetActiveProjects'"

**Step 3: Implement GetActiveProjects method**

Modify: `PdfConversion/Services/ProjectMetadataService.cs`

Add this method after `GetProjectMetadata`:

```csharp
public async Task<Dictionary<string, Dictionary<string, ProjectMetadata>>> GetActiveProjects()
{
    var allProjects = await GetAllProjects();
    var activeProjects = new Dictionary<string, Dictionary<string, ProjectMetadata>>();

    foreach (var (tenant, projects) in allProjects)
    {
        var activeInTenant = projects
            .Where(p => p.Value.Status == ProjectStatus.Open || p.Value.Status == ProjectStatus.InProgress)
            .ToDictionary(p => p.Key, p => p.Value);

        if (activeInTenant.Any())
        {
            activeProjects[tenant] = activeInTenant;
        }
    }

    return activeProjects;
}
```

**Step 4: Run test to verify it passes**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataServiceTests.GetActiveProjects" --verbosity normal
```

Expected: PASS

**Step 5: Commit**

```bash
git add PdfConversion/Services/ProjectMetadataService.cs PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs
git commit -m "Add GetActiveProjects method to filter stale projects"
```

---

## Task 5: Add UpdateProjectLabel Method

**Files:**
- Modify: `PdfConversion/Services/ProjectMetadataService.cs`
- Modify: `PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs`

**Step 1: Write the failing test**

Modify: `PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs`

Add this test method:

```csharp
[Fact]
public async Task UpdateProjectLabel_ExistingProject_UpdatesLabelAndTimestamp()
{
    await _service.UpdateProjectStatus("optiver", "ar24-1", ProjectStatus.Open);
    var before = await _service.GetProjectMetadata("optiver", "ar24-1");

    await Task.Delay(100); // Ensure different timestamp
    await _service.UpdateProjectLabel("optiver", "ar24-1", "New Label");

    var after = await _service.GetProjectMetadata("optiver", "ar24-1");
    Assert.NotNull(after);
    Assert.Equal("New Label", after.Label);
    Assert.True(after.LastModified > before!.LastModified);
}
```

**Step 2: Run test to verify it fails**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataServiceTests.UpdateProjectLabel" --verbosity normal
```

Expected: FAIL with "'ProjectMetadataService' does not contain a definition for 'UpdateProjectLabel'"

**Step 3: Implement UpdateProjectLabel method**

Modify: `PdfConversion/Services/ProjectMetadataService.cs`

Add this method after `UpdateProjectStatus`:

```csharp
public async Task UpdateProjectLabel(string tenant, string projectId, string newLabel)
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
                Label = newLabel,
                Status = ProjectStatus.Open,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
        }
        else
        {
            projects[tenant][projectId].Label = newLabel;
            projects[tenant][projectId].LastModified = DateTime.UtcNow;
        }

        await SaveProjects(projects);
    }
    finally
    {
        _lock.Release();
    }
}
```

**Step 4: Run test to verify it passes**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectMetadataServiceTests.UpdateProjectLabel" --verbosity normal
```

Expected: PASS

**Step 5: Commit**

```bash
git add PdfConversion/Services/ProjectMetadataService.cs PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs
git commit -m "Add UpdateProjectLabel method to ProjectMetadataService"
```

---

## Task 6: Register ProjectMetadataService in DI Container

**Files:**
- Modify: `PdfConversion/Program.cs`

**Step 1: Update Program.cs**

Modify: `PdfConversion/Program.cs`

Find where services are registered (after `builder.Services.AddRazorPages()`) and add:

```csharp
// Register ProjectMetadataService
var metadataPath = Path.Combine(builder.Environment.ContentRootPath, "data", "project-metadata.json");
builder.Services.AddSingleton(new ProjectMetadataService(metadataPath));
```

**Step 2: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 --tail 50 2>&1 | grep -E "(Building|error CS|Application started)"
```

Expected: "Application started" with no "error CS" messages

**Step 3: Commit**

```bash
git add PdfConversion/Program.cs
git commit -m "Register ProjectMetadataService in dependency injection"
```

---

## Task 7: Update Home Page - Add Status Column UI

**Files:**
- Modify: `PdfConversion/Pages/Home.razor`
- Modify: `PdfConversion/Pages/Home.razor.cs`

**Step 1: Update Home.razor to add status column**

Modify: `PdfConversion/Pages/Home.razor`

Find the table section and update to add status column:

```razor
<table class="project-table">
    <thead>
        <tr>
            <th>Project ID</th>
            <th>Project Name</th>
            <th>Status</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var tenant in Projects.Keys)
        {
            @foreach (var (projectId, metadata) in Projects[tenant])
            {
                <tr class="project-row status-@metadata.Status.ToString().ToLower()">
                    <td class="project-id">@projectId</td>
                    <td class="project-label" @ondblclick="() => StartEditingLabel(tenant, projectId)">
                        @if (IsEditingLabel(tenant, projectId))
                        {
                            <input type="text"
                                   class="edit-input"
                                   value="@metadata.Label"
                                   @onblur="() => SaveLabel(tenant, projectId)"
                                   @onkeydown="@(e => HandleLabelKeyDown(e, tenant, projectId))"
                                   @ref="@LabelInputRef" />
                        }
                        else
                        {
                            @metadata.Label
                        }
                    </td>
                    <td class="project-status" @ondblclick="() => StartEditingStatus(tenant, projectId)">
                        @if (IsEditingStatus(tenant, projectId))
                        {
                            <select class="status-select"
                                    value="@metadata.Status.ToString()"
                                    @onchange="@(e => SaveStatus(tenant, projectId, e.Value?.ToString()))"
                                    @onblur="() => CancelEditingStatus()"
                                    @onkeydown="@(e => HandleStatusKeyDown(e))"
                                    @ref="@StatusSelectRef">
                                <option value="Open">Open</option>
                                <option value="InProgress">In Progress</option>
                                <option value="Ready">Ready</option>
                                <option value="Parked">Parked</option>
                            </select>
                        }
                        else
                        {
                            <span class="status-text">@GetStatusDisplayText(metadata.Status)</span>
                        }
                    </td>
                </tr>
            }
        }
    </tbody>
</table>
```

**Step 2: Update Home.razor.cs to add status editing logic**

Modify: `PdfConversion/Pages/Home.razor.cs`

Update the code-behind:

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PdfConversion.Models;
using PdfConversion.Services;

namespace PdfConversion.Pages;

public partial class Home
{
    [Inject] private ProjectMetadataService MetadataService { get; set; } = null!;

    private Dictionary<string, Dictionary<string, ProjectMetadata>> Projects { get; set; } = new();

    private string? _editingLabelTenant;
    private string? _editingLabelProjectId;
    private string? _editingStatusTenant;
    private string? _editingStatusProjectId;

    private ElementReference LabelInputRef;
    private ElementReference StatusSelectRef;

    protected override async Task OnInitializedAsync()
    {
        Projects = await MetadataService.GetAllProjects();
    }

    private bool IsEditingLabel(string tenant, string projectId)
    {
        return _editingLabelTenant == tenant && _editingLabelProjectId == projectId;
    }

    private bool IsEditingStatus(string tenant, string projectId)
    {
        return _editingStatusTenant == tenant && _editingStatusProjectId == projectId;
    }

    private void StartEditingLabel(string tenant, string projectId)
    {
        _editingLabelTenant = tenant;
        _editingLabelProjectId = projectId;
        StateHasChanged();
    }

    private void StartEditingStatus(string tenant, string projectId)
    {
        _editingStatusTenant = tenant;
        _editingStatusProjectId = projectId;
        StateHasChanged();
    }

    private async Task SaveLabel(string tenant, string projectId)
    {
        if (Projects[tenant][projectId].Label != null)
        {
            await MetadataService.UpdateProjectLabel(tenant, projectId, Projects[tenant][projectId].Label);
        }
        _editingLabelTenant = null;
        _editingLabelProjectId = null;
    }

    private async Task SaveStatus(string tenant, string projectId, string? statusValue)
    {
        if (Enum.TryParse<ProjectStatus>(statusValue, out var newStatus))
        {
            await MetadataService.UpdateProjectStatus(tenant, projectId, newStatus);
            Projects = await MetadataService.GetAllProjects();
        }
        _editingStatusTenant = null;
        _editingStatusProjectId = null;
    }

    private void CancelEditingStatus()
    {
        _editingStatusTenant = null;
        _editingStatusProjectId = null;
    }

    private async Task HandleLabelKeyDown(KeyboardEventArgs e, string tenant, string projectId)
    {
        if (e.Key == "Enter")
        {
            await SaveLabel(tenant, projectId);
        }
        else if (e.Key == "Escape")
        {
            _editingLabelTenant = null;
            _editingLabelProjectId = null;
        }
    }

    private void HandleStatusKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            CancelEditingStatus();
        }
    }

    private string GetStatusDisplayText(ProjectStatus status)
    {
        return status switch
        {
            ProjectStatus.InProgress => "In Progress",
            _ => status.ToString()
        };
    }
}
```

**Step 3: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 --tail 50 2>&1 | grep -E "(Building|error CS|Application started)"
```

Expected: "Application started" with no "error CS" messages

**Step 4: Commit**

```bash
git add PdfConversion/Pages/Home.razor PdfConversion/Pages/Home.razor.cs
git commit -m "Add status column to Home page with inline editing"
```

---

## Task 8: Add Scoped CSS for Status Column and Row Coloring

**Files:**
- Create: `PdfConversion/Pages/Home.razor.css` (or modify if exists)

**Step 1: Create scoped CSS**

Create or modify: `PdfConversion/Pages/Home.razor.css`

```css
/* Project table styling */
.project-table {
    width: 100%;
    border-collapse: collapse;
    margin: 20px 0;
}

.project-table th {
    text-align: left;
    padding: 12px;
    background-color: var(--vscode-editor-background);
    border-bottom: 2px solid var(--vscode-panel-border);
    font-weight: 600;
}

.project-table td {
    padding: 10px 12px;
    border-bottom: 1px solid var(--vscode-panel-border);
}

/* Row coloring based on status - subtle VS Code Dark Modern theme */
.project-row.status-open {
    background-color: rgba(0, 122, 204, 0.08);
}

.project-row.status-inprogress {
    background-color: rgba(206, 145, 120, 0.08);
}

.project-row.status-ready {
    background-color: rgba(78, 201, 176, 0.08);
}

.project-row.status-parked {
    background-color: rgba(133, 133, 133, 0.08);
}

.project-row:hover {
    filter: brightness(1.1);
}

/* Status text coloring */
.project-status .status-text {
    font-weight: 500;
}

.status-open .status-text {
    color: #007acc;
}

.status-inprogress .status-text {
    color: #ce9178;
}

.status-ready .status-text {
    color: #4ec9b0;
}

.status-parked .status-text {
    color: #858585;
}

/* Inline editing */
.project-label,
.project-status {
    cursor: pointer;
    position: relative;
}

.project-label:hover::after,
.project-status:hover::after {
    content: '✎';
    position: absolute;
    right: 8px;
    opacity: 0.4;
    font-size: 14px;
}

.edit-input,
.status-select {
    width: 100%;
    padding: 6px 8px;
    background-color: var(--vscode-input-background);
    color: var(--vscode-input-foreground);
    border: 1px solid var(--vscode-input-border);
    border-radius: 3px;
    font-family: inherit;
    font-size: inherit;
}

.edit-input:focus,
.status-select:focus {
    outline: 1px solid var(--vscode-focusBorder);
    border-color: var(--vscode-focusBorder);
}

.status-select {
    cursor: pointer;
}
```

**Step 2: Verify changes in browser**

```bash
# Check if app is running
curl -s http://localhost:8085 > /dev/null && echo "App is running" || echo "App is not running"
```

Expected: "App is running"

**Step 3: Commit**

```bash
git add PdfConversion/Pages/Home.razor.css
git commit -m "Add scoped CSS for status column and subtle row coloring"
```

---

## Task 9: Update Transform Page Dropdown to Use GetActiveProjects

**Files:**
- Modify: `PdfConversion/Pages/Transform.razor.cs`

**Step 1: Update Transform page code-behind**

Modify: `PdfConversion/Pages/Transform.razor.cs`

Find where projects are loaded and update to use `GetActiveProjects`:

```csharp
// Add injection
[Inject] private ProjectMetadataService MetadataService { get; set; } = null!;

// Update OnInitializedAsync or wherever projects are loaded
protected override async Task OnInitializedAsync()
{
    // Replace old project loading with:
    var activeProjects = await MetadataService.GetActiveProjects();

    // Build project list for dropdown
    _projects = new List<string>();
    foreach (var (tenant, projects) in activeProjects)
    {
        foreach (var projectId in projects.Keys)
        {
            _projects.Add($"{tenant}/{projectId}");
        }
    }
}
```

**Step 2: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 --tail 50 2>&1 | grep -E "(Building|error CS|Application started)"
```

Expected: "Application started" with no "error CS" messages

**Step 3: Commit**

```bash
git add PdfConversion/Pages/Transform.razor.cs
git commit -m "Update Transform page to show only active projects"
```

---

## Task 10: Update Convert Page Dropdown to Use GetActiveProjects

**Files:**
- Modify: `PdfConversion/Pages/Convert.razor.cs`

**Step 1: Update Convert page code-behind**

Modify: `PdfConversion/Pages/Convert.razor.cs`

Find where projects are loaded and update to use `GetActiveProjects`:

```csharp
// Add injection
[Inject] private ProjectMetadataService MetadataService { get; set; } = null!;

// Update OnInitializedAsync or wherever projects are loaded
protected override async Task OnInitializedAsync()
{
    // Replace old project loading with:
    var activeProjects = await MetadataService.GetActiveProjects();

    // Build project list for dropdown
    _projects = new List<string>();
    foreach (var (tenant, projects) in activeProjects)
    {
        foreach (var projectId in projects.Keys)
        {
            _projects.Add($"{tenant}/{projectId}");
        }
    }
}
```

**Step 2: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 --tail 50 2>&1 | grep -E "(Building|error CS|Application started)"
```

Expected: "Application started" with no "error CS" messages

**Step 3: Commit**

```bash
git add PdfConversion/Pages/Convert.razor.cs
git commit -m "Update Convert page to show only active projects"
```

---

## Task 11: Add E2E Test for Status Editing

**Files:**
- Create: `PdfConversion/Tests/E2E/ProjectStatusTests.cs`

**Step 1: Create E2E test**

Create: `PdfConversion/Tests/E2E/ProjectStatusTests.cs`

```csharp
using Microsoft.Playwright;
using Xunit;

namespace PdfConversion.Tests.E2E;

public class ProjectStatusTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        _page = await _browser.NewPageAsync();
    }

    [Fact]
    public async Task UserCanChangeProjectStatus()
    {
        await _page!.GotoAsync("http://localhost:8085");

        // Wait for table to load
        await _page.WaitForSelectorAsync(".project-table");

        // Find first status cell and double-click
        var statusCell = await _page.QuerySelectorAsync(".project-status");
        await statusCell!.DblClickAsync();

        // Verify dropdown appears
        var dropdown = await _page.QuerySelectorAsync(".status-select");
        Assert.NotNull(dropdown);

        // Select "In Progress"
        await dropdown.SelectOptionAsync("InProgress");

        // Verify status updated
        await _page.WaitForTimeoutAsync(500); // Wait for save
        var statusText = await statusCell.TextContentAsync();
        Assert.Contains("In Progress", statusText);
    }

    [Fact]
    public async Task StatusChangesRowColor()
    {
        await _page!.GotoAsync("http://localhost:8085");
        await _page.WaitForSelectorAsync(".project-table");

        // Get first row
        var row = await _page.QuerySelectorAsync(".project-row");
        var initialClass = await row!.GetAttributeAsync("class");

        // Change status
        var statusCell = await row.QuerySelectorAsync(".project-status");
        await statusCell!.DblClickAsync();
        var dropdown = await _page.QuerySelectorAsync(".status-select");
        await dropdown!.SelectOptionAsync("Ready");

        // Verify class changed
        await _page.WaitForTimeoutAsync(500);
        var newClass = await row.GetAttributeAsync("class");
        Assert.NotEqual(initialClass, newClass);
        Assert.Contains("status-ready", newClass);
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
```

**Step 2: Run E2E tests**

```bash
docker exec taxxor-pdfconversion-1 dotnet test --filter "FullyQualifiedName~ProjectStatusTests" --verbosity normal
```

Expected: PASS (2 tests)

**Step 3: Commit**

```bash
git add PdfConversion/Tests/E2E/ProjectStatusTests.cs
git commit -m "Add E2E tests for project status editing"
```

---

## Task 12: Manual Testing and Verification

**Manual Testing Checklist:**

1. **Navigate to Home page** (http://localhost:8085)
   - Verify three columns: Project ID, Project Name, Status
   - Verify row colors are subtle and match VS Code theme
   - Verify status text has appropriate colors

2. **Test status editing:**
   - Double-click on a status cell
   - Verify dropdown appears with 4 options
   - Select "In Progress"
   - Verify status updates and row color changes
   - Verify escape key cancels editing

3. **Test label editing:**
   - Double-click on a label cell
   - Edit the label text
   - Press Enter to save
   - Verify label persists after page reload

4. **Test Transform page filtering:**
   - Set a project to "Ready" status
   - Navigate to /transform
   - Verify "Ready" project is NOT in dropdown
   - Set project back to "Open"
   - Verify project reappears in dropdown

5. **Test Convert page filtering:**
   - Set a project to "Parked" status
   - Navigate to /convert
   - Verify "Parked" project is NOT in dropdown

6. **Test migration:**
   - Stop Docker containers
   - Rename `data/project-metadata.json` to `data/project-labels.json`
   - Update content to old format (labels structure)
   - Start Docker containers
   - Verify migration happens automatically
   - Verify old file is deleted
   - Verify all projects have "Open" status

**Step: Document any issues found**

If issues found, create new tasks to fix them before proceeding.

**Step: Final commit if manual changes made**

```bash
git add .
git commit -m "Manual testing adjustments and fixes"
```

---

## Final Verification

Run all tests:

```bash
npm test
```

Expected: All tests passing (integration + E2E)

Verify Docker compilation:

```bash
docker logs taxxor-pdfconversion-1 --tail 20 2>&1 | grep "Application started"
```

Expected: Recent "Application started" message with no errors

---

## Summary

**Files Created:**
- `PdfConversion/Models/ProjectMetadata.cs`
- `PdfConversion/Models/ProjectStatus.cs`
- `PdfConversion/Services/ProjectMetadataService.cs`
- `PdfConversion/Tests/Unit/ProjectMetadataTests.cs`
- `PdfConversion/Tests/Integration/ProjectMetadataServiceTests.cs`
- `PdfConversion/Tests/Integration/ProjectMetadataMigrationTests.cs`
- `PdfConversion/Tests/E2E/ProjectStatusTests.cs`
- `PdfConversion/Pages/Home.razor.css`
- `docs/plans/2025-01-24-project-status-management-design.md`
- `docs/plans/2025-01-24-project-status-management.md`

**Files Modified:**
- `PdfConversion/Program.cs` (DI registration)
- `PdfConversion/Pages/Home.razor` (status column UI)
- `PdfConversion/Pages/Home.razor.cs` (status editing logic)
- `PdfConversion/Pages/Transform.razor.cs` (active projects filter)
- `PdfConversion/Pages/Convert.razor.cs` (active projects filter)

**Migration:**
- `data/project-labels.json` → `data/project-metadata.json` (automatic on first load)

**Testing:**
- 6 integration tests (ProjectMetadataService)
- 2 E2E tests (status editing workflow)
- Manual testing checklist completed
