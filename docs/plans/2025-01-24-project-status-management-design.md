# Project Status Management Feature Design

**Date:** 2025-01-24
**Status:** Approved
**Author:** Design Session with User

## Overview

Add project status management to the PDF Conversion Tool, allowing users to track project lifecycle states and filter active projects in dropdowns.

## Goals

1. Add status tracking for all projects (Open, In Progress, Ready, Parked)
2. Enable inline status editing on Home page via double-click
3. Filter dropdowns on Transform/Convert pages to show only active projects
4. Provide subtle visual feedback via row coloring (VS Code Dark Modern Theme)
5. Create extensible metadata structure for future enhancements

## Data Structure

### File Rename & Migration

**Old:** `data/project-labels.json`
**New:** `data/project-metadata.json`

### New Structure

```json
{
  "projects": {
    "optiver": {
      "ar24-3": {
        "label": "Optiver Australia Holdings Pty Limited",
        "status": "Open",
        "createdAt": "2025-01-24T10:00:00Z",
        "lastModified": "2025-01-24T10:00:00Z"
      }
    },
    "antea-group": {}
  },
  "lastModified": "2025-01-24T10:00:00Z"
}
```

### Status Values

| Status | Description | Visible in Dropdowns? | Row Color |
|--------|-------------|----------------------|-----------|
| Open | Default status for new projects | ✅ Yes | `rgba(0, 122, 204, 0.08)` (faint blue) |
| In Progress | Actively being worked on | ✅ Yes | `rgba(206, 145, 120, 0.08)` (faint orange) |
| Ready | Completed, ready for delivery | ❌ No (stale) | `rgba(78, 201, 176, 0.08)` (faint teal-green) |
| Parked | On hold, not active | ❌ No (stale) | `rgba(133, 133, 133, 0.08)` (faint gray) |

### Migration Strategy

On first load, if `project-labels.json` exists:
1. Detect old format (has `labels` property instead of `projects`)
2. Transform structure to new format
3. Set all existing projects to status "Open" (default)
4. Add `createdAt` and `lastModified` timestamps
5. Write to `project-metadata.json`
6. Delete old `project-labels.json` (no backup needed)

## Architecture

### Backend Components

#### 1. ProjectMetadataService

**Responsibility:** Manage all project metadata operations

**Key Methods:**
- `GetAllProjects()` → Returns all projects with metadata
- `GetProjectMetadata(tenant, projectId)` → Get metadata for specific project
- `UpdateProjectStatus(tenant, projectId, newStatus)` → Update status, auto-update lastModified
- `UpdateProjectLabel(tenant, projectId, newLabel)` → Update label, auto-update lastModified
- `GetActiveProjects()` → Returns only "Open" and "In Progress" projects (for dropdowns)

**Features:**
- Thread-safe read/write operations
- File watcher for external changes
- Validates status values against enum
- Automatic timestamp management

#### 2. Models

**ProjectMetadata:**
```csharp
public class ProjectMetadata
{
    public string Label { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
}

public enum ProjectStatus
{
    Open,
    InProgress,
    Ready,
    Parked
}
```

### Frontend Components

#### 1. Home Page Updates (`Pages/Home.razor`)

**Table Structure:**
- Column 1: Project ID (existing)
- Column 2: Project Label (existing, double-click to edit)
- Column 3: **NEW** - Status (display as text, double-click to show dropdown)

**Status Column Behavior:**
- Default: Show status text with color coding
- Double-click: Transform to dropdown with 4 status options
- On change: Save immediately via `ProjectMetadataService.UpdateProjectStatus()`
- Click outside / Escape: Close dropdown, revert to text display

**Row Coloring:**
- Subtle background tints based on project status
- Colors aligned with VS Code Dark Modern Theme
- Applied via scoped CSS (`Home.razor.css`)

#### 2. Transform & Convert Pages

**Project Selector Dropdowns:**
- Call `GetActiveProjects()` instead of `GetAllProjects()`
- Show only "Open" and "In Progress" projects
- "Ready" and "Parked" projects completely hidden

## UI/UX Details

### Color Scheme (VS Code Dark Modern Theme)

**Status Text Colors:**
- Open: `#007acc` (VS Code blue)
- In Progress: `#ce9178` (VS Code orange)
- Ready: `#4ec9b0` (VS Code teal-green)
- Parked: `#858585` (VS Code gray)

**Row Background Colors:**
- Very subtle alpha (0.08) to avoid overwhelming interface
- Provides visual grouping without distraction

### Interaction Patterns

**Inline Status Editing:**
- Consistent with existing label editing pattern
- Double-click to activate
- Dropdown automatically opens on activation
- Auto-save on selection change
- Escape/click-outside to cancel

## Error Handling

### Migration Scenarios

| Scenario | Handling |
|----------|----------|
| Old file corrupted | Show error toast, create fresh file with defaults |
| File doesn't exist | Create new file with discovered projects (all "Open") |
| Multiple concurrent edits | Last-write-wins (read before each write) |

### Runtime Errors

| Error | Handling |
|-------|----------|
| Status update save fails | Show error toast, revert to previous value |
| Invalid status value | Log warning, default to "Open" |
| All projects stale | Show "No active projects available" in dropdowns |

## Implementation Order

1. Create `ProjectMetadataService` with migration logic
2. Create C# models (`ProjectMetadata`, `ProjectStatus` enum)
3. Update Home page with status column and inline editing
4. Add scoped CSS for row/text coloring (`Home.razor.css`)
5. Update Transform/Convert page dropdowns to filter by active status
6. Add integration tests (bUnit)
7. Add E2E tests (Playwright)
8. Manual testing of visual design

## Testing Strategy

### Integration Tests (bUnit)
- Migration from old format to new format
- CRUD operations on project metadata
- Active project filtering logic
- Status enum validation

### E2E Tests (Playwright)
- Home page status editing workflow
- Dropdown filtering on Transform page
- Dropdown filtering on Convert page
- Row coloring verification

### Manual Testing
- Verify VS Code theme color accuracy
- Confirm row coloring subtlety
- Test double-click interactions
- Verify dropdown behavior

## Success Criteria

- [ ] Old `project-labels.json` successfully migrated to `project-metadata.json`
- [ ] Status defaults to "Open" for all existing projects
- [ ] Double-click editing works for status column
- [ ] Row coloring is subtle and matches VS Code Dark Modern Theme
- [ ] Ready/Parked projects hidden from Transform/Convert dropdowns
- [ ] All integration tests pass
- [ ] All E2E tests pass
- [ ] No compilation errors in Docker
- [ ] User confirms feature works as expected

## Future Extensibility

The new `ProjectMetadata` model supports adding additional fields:
- `owner` - Project owner name
- `dueDate` - Deadline tracking
- `tags` - Categorization
- `notes` - Project notes
- `priority` - Priority level

The extensible structure was chosen specifically to accommodate these future enhancements without requiring another migration.
