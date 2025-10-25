# Custom Project Labels Feature Design

**Date:** 2025-10-24
**Status:** Approved
**Author:** Design Session with User

## Overview

Add ability to configure custom display labels for projects while maintaining filesystem folder references. Projects are currently identified by folder names like `ar24-3`, which get auto-converted to "Annual Report 2024 (3)". This feature allows users to configure custom labels like "Q3 Financial Report (ar24-3)" while keeping the folder name visible for filesystem navigation.

## Goals

1. Allow custom labels to be configured and persistently stored
2. Maintain visibility of folder names (e.g., `ar24-3`) for filesystem navigation
3. Auto-discover new projects when folders are added
4. Update all UI components to use custom labels consistently
5. Provide simple inline editing interface for label maintenance

## Non-Goals

- Renaming actual folders on filesystem
- Multi-user label management or permissions
- Label versioning or history
- Import/export of labels

## Display Format

**Approved Format:** `"{CustomLabel} ({folder-name})"`

**Examples:**
- With custom label: "Q3 Financial Report (ar24-3)"
- Without custom label (ar-pattern): "Annual Report 2024 (3) (ar24-3)"
- Without custom label (non-standard): "xyz-project-123 (xyz-project-123)"

**Fallback Behavior:**
- Pattern `ar\d\d-\d+`: Auto-generate "Annual Report YYYY (N)"
- Non-standard patterns: Use folder name as label
- Result: Consistent parenthetical format across all cases

## Architecture

### Approach: Dedicated LabelService with Separate JSON

**Decision:** Use dedicated `ProjectLabelService` with separate `project-labels.json` file.

**Rationale:**
- Clean separation of concerns
- Easy to export/import labels in future
- Scales well with multiple customers
- Independent lifecycle from user selections

### Core Components

#### 1. ProjectLabelService

**Responsibilities:**
- Label CRUD operations (Get, Set, Delete)
- Project discovery via filesystem scanning
- Display string generation with fallback logic
- JSON persistence

**Key Methods:**
```csharp
Task<List<ProjectInfo>> GetAllProjects()
Task<string?> GetProjectLabel(string customer, string projectId)
Task SetProjectLabel(string customer, string projectId, string label)
Task DeleteProjectLabel(string customer, string projectId)
Task<string> GetDisplayString(string customer, string projectId)
```

**Discovery Algorithm:**
1. Enumerate all `{customer}` folders under `data/input/`
2. For each customer, enumerate `projects/{project-id}` subfolders
3. Return list of (customer, projectId) tuples
4. Merge with labels from JSON to produce display strings

**Fallback Label Generation:**
```csharp
private string GenerateFallbackLabel(string projectId)
{
    // Match "ar24-3" pattern
    var match = Regex.Match(projectId, @"^ar(\d{2})-(\d+)$");
    if (match.Success)
    {
        int year = 2000 + int.Parse(match.Groups[1].Value);
        string sequence = match.Groups[2].Value;
        return $"Annual Report {year} ({sequence})";
    }

    // Non-standard: return the ID itself
    return projectId;
}

// Final display: "{customLabelOrFallback} ({projectId})"
```

#### 2. ProjectDirectoryWatcherService

**Responsibilities:**
- Watch `data/input/*/projects/` directories for changes
- Detect new/deleted project folders
- Debounce rapid changes (500ms)
- Fire events when projects change

**Pattern:** Follows existing `XmlFileWatcherService` / `XsltFileWatcherService` pattern

**Key Differences:**
- Watches directories instead of files
- Monitors multiple customer paths simultaneously
- Fires events for directory creation/deletion
- Same debouncing and callback pattern

**Events:**
```csharp
public event EventHandler<ProjectsChangedEventArgs>? ProjectsChanged;
```

#### 3. Home Page (Enhanced)

**New Purpose:** Project labels management dashboard

**Layout:**
- Header: "Project Labels Management"
- Project table grouped by customer
- Inline editing with auto-save (debounced 500ms)
- Auto-refresh when new projects detected

**Table Structure:**
```
Customer: Optiver
┌─────────────┬──────────────────────────┐
│ Project ID  │ Custom Label             │
├─────────────┼──────────────────────────┤
│ ar24-3      │ [Q3 Financial Report   ] │
│ ar24-6      │ [                      ] │
└─────────────┴──────────────────────────┘

Customer: Customer2
┌─────────────┬──────────────────────────┐
│ Project ID  │ Custom Label             │
├─────────────┼──────────────────────────┤
│ ar24-1      │ [Annual Summary        ] │
└─────────────┴──────────────────────────┘
```

**Inline Editing Flow:**
1. User types in text input field
2. Auto-save triggers 500ms after typing stops (debounced)
3. Call `ProjectLabelService.SetProjectLabel()`
4. Success toast notification
5. All UI components update automatically

**Auto-refresh:**
- Subscribe to `ProjectDirectoryWatcherService.ProjectsChanged` event
- When event fires → reload project table
- Visual indicator: "Last updated: X minutes ago"

### Data Models

```csharp
// Models/ProjectInfo.cs
public class ProjectInfo
{
    public string Customer { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? CustomLabel { get; set; }
    public string DisplayString { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
}

// Models/ProjectLabelsData.cs
public class ProjectLabelsData
{
    public Dictionary<string, Dictionary<string, string>> Labels { get; set; } = new();
    public DateTime LastModified { get; set; }
}

// Models/ProjectsChangedEventArgs.cs
public class ProjectsChangedEventArgs : EventArgs
{
    public List<ProjectInfo> Projects { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
```

### JSON Storage Structure

**File:** `data/project-labels.json`

```json
{
  "labels": {
    "optiver": {
      "ar24-3": "Q3 Financial Report",
      "ar24-6": "Year-End Compliance Review",
      "xyz-custom": "Custom Project Label"
    },
    "customer2": {
      "ar24-1": "Annual Summary"
    }
  },
  "lastModified": "2025-01-24T10:30:00Z"
}
```

**Key Structure:**
- Top-level `labels` object contains customer dictionaries
- Each customer has dictionary of `{projectId: customLabel}`
- Empty/missing entries = use auto-generated fallback
- `lastModified` timestamp for debugging/auditing

## Integration Points

### Components to Update

All components that display project names must use `ProjectLabelService.GetDisplayString()`:

1. **MainLayout.razor** - Toolbar project selector
2. **Transform.razor** - Project dropdown
3. **Convert.razor** - Project selection
4. **Any Modal Dialogs** - If they reference project names

**Implementation Pattern:**
```csharp
// Before (current code):
string displayName = GetAutoGeneratedLabel(projectId);

// After (new code):
string displayName = await ProjectLabelService.GetDisplayString(customer, projectId);
```

**Service Injection:**
```csharp
@inject IProjectLabelService ProjectLabelService
```

### Service Registration

**Program.cs:**
```csharp
builder.Services.AddSingleton<IProjectLabelService, ProjectLabelService>();
builder.Services.AddSingleton<IProjectDirectoryWatcherService, ProjectDirectoryWatcherService>();
```

**Lifecycle:** Both services are singletons (shared state across app)

## Testing Strategy

### Integration Tests (bUnit)

**1. ProjectLabelService Tests** (`ProjectLabelServiceTests.cs`):
- `GetAllProjects_ShouldDiscoverAllCustomerProjects()`
- `SetProjectLabel_ShouldPersistToJson()`
- `GetDisplayString_WithCustomLabel_ReturnsFormattedString()`
- `GetDisplayString_WithoutLabel_ReturnsAutoGenerated()`
- `GetDisplayString_NonStandardId_ReturnsConsistentFormat()`
- `DeleteProjectLabel_ShouldRemoveFromJson()`
- `GetDisplayString_ArPatternFallback_GeneratesCorrectYear()`

**2. ProjectDirectoryWatcherService Tests** (`ProjectDirectoryWatcherServiceTests.cs`):
- `StartWatching_NewProjectCreated_FiresEvent()`
- `StartWatching_ProjectDeleted_FiresEvent()`
- `DebouncesMultipleRapidChanges()`
- `StopWatching_DisposesAllWatchers()`

**3. Home Page Component Tests** (`HomePageTests.cs`):
- `RendersProjectTableGroupedByCustomer()`
- `InlineEdit_AutoSaves_AfterDebounce()`
- `NewProjectAppears_UpdatesTableAutomatically()`
- `DisplaysCorrectFormatForAllProjects()`
- `EmptyLabel_ShowsPlaceholder()`

**4. Updated Component Tests** (`ToolbarIntegrationTests.cs` additions):
- `ProjectSelector_DisplaysCustomLabels()`
- `ProjectSelector_FallsBackToAutoGenerated()`
- `ProjectSelector_UpdatesWhenLabelChanged()`

### E2E Tests (Playwright)

**New Test:** `UserCanManageProjectLabelsOnHomepage()`

**Steps:**
1. Navigate to homepage (http://localhost:8085)
2. Verify project table displays with correct grouping
3. Type custom label in input field for ar24-3
4. Wait 500ms (debounce)
5. Verify label saved (check JSON file content)
6. Navigate to Transform page
7. Verify dropdown shows new label format
8. Verify format: "Custom Label (ar24-3)"

### Test Data

**Location:** `PdfConversion/Tests/TestData/`

**Structure:**
```
TestData/
├── mock-projects/
│   ├── optiver/projects/
│   │   ├── ar24-3/
│   │   └── ar24-6/
│   └── customer2/projects/
│       └── ar24-1/
└── project-labels-test.json
```

**Cleanup:** Tests create temporary folders/files and clean up after execution

## Implementation Checklist

**Phase 1: Core Services**
- [ ] Create `Models/ProjectInfo.cs`
- [ ] Create `Models/ProjectLabelsData.cs`
- [ ] Create `Models/ProjectsChangedEventArgs.cs`
- [ ] Create `Services/IProjectLabelService.cs`
- [ ] Create `Services/ProjectLabelService.cs`
- [ ] Create `Services/IProjectDirectoryWatcherService.cs`
- [ ] Create `Services/ProjectDirectoryWatcherService.cs`
- [ ] Register services in `Program.cs`
- [ ] Write integration tests for `ProjectLabelService`
- [ ] Write integration tests for `ProjectDirectoryWatcherService`

**Phase 2: Homepage UI**
- [ ] Update `Pages/Home.razor` with project management table
- [ ] Create `Pages/Home.razor.css` with scoped styles
- [ ] Implement auto-save with 500ms debounce
- [ ] Implement customer grouping
- [ ] Subscribe to `ProjectsChanged` events
- [ ] Add "Last updated" timestamp display
- [ ] Write bUnit tests for homepage

**Phase 3: Component Integration**
- [ ] Update `Shared/MainLayout.razor` project selector
- [ ] Update `Pages/Transform.razor` project dropdown
- [ ] Update `Pages/Convert.razor` project selection
- [ ] Update any modal dialogs with project references
- [ ] Write integration tests for updated components

**Phase 4: E2E Testing**
- [ ] Create test data structure in `Tests/TestData/`
- [ ] Write Playwright test: `UserCanManageProjectLabelsOnHomepage()`
- [ ] Verify all existing E2E tests still pass
- [ ] Update tests that assert on project display names

**Phase 5: Documentation**
- [ ] Update `CLAUDE.md` with label management workflow
- [ ] Document JSON file structure
- [ ] Document fallback behavior patterns
- [ ] Add screenshots/examples to documentation

## Success Criteria

- [ ] Users can configure custom labels via homepage table
- [ ] Labels persist across app restarts
- [ ] All UI components show consistent label format
- [ ] New projects auto-appear in management table
- [ ] Folder names remain visible for filesystem navigation
- [ ] Auto-save works within 500ms of typing stop
- [ ] All integration tests pass (bUnit)
- [ ] Critical E2E test passes (Playwright)
- [ ] No regressions in existing functionality

## Future Enhancements (Out of Scope)

- Label import/export functionality
- Label templates or presets
- Multi-user label sharing/synchronization
- Label history/versioning
- Bulk label operations
- Search/filter in label management table
- Label validation rules (max length, allowed characters)

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| FileSystemWatcher doesn't work in Docker | High | Already proven working with XML/XSLT watchers |
| Large number of projects causes performance issues | Medium | Test with 50+ projects, add pagination if needed |
| Concurrent label edits cause conflicts | Low | Single-user app, file locking handles edge cases |
| JSON file corruption | Medium | Backup before write, validate on load |
| Label changes don't propagate to all components | High | Comprehensive integration testing |

## Open Questions

None - design approved and ready for implementation.
