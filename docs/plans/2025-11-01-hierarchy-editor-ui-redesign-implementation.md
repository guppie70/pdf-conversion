# Hierarchy Editor UI Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Redesign `/generate-hierarchy` page to make Restricted Mode default, improve file selection UX, and provide clearer mode switching while preserving all existing functionality.

**Architecture:** Restructure panel headers to contain contextual controls, replace separate Load button with integrated reload icon, implement tab-based mode switcher, and add overflow menu for less-used operations. All existing functionality preserved with no behavioral changes except UI organization.

**Tech Stack:** C# .NET 9 Blazor Server, Bootstrap 5, Scoped CSS, VS Code Dark Modern colors

---

## Implementation Phases

This plan is organized into 5 phases that can be implemented sequentially with commits after each phase:

1. **Phase 1: Mode Infrastructure** - Enum, state management, mode switcher UI
2. **Phase 2: Panel Header Restructure** - Move selectors into headers, add reload button
3. **Phase 3: Restricted Mode Controls** - Primary actions, overflow menu, status dot
4. **Phase 4: Mode Switching Logic** - Confirmation modals, state clearing, transitions
5. **Phase 5: Testing & Polish** - Manual testing, CSS refinements, documentation

---

## Phase 1: Mode Infrastructure

### Task 1: Add Mode Enum and State Management

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor` (add near top of @code block)

**Step 1: Add mode enum**

Add this enum definition inside the `@code` block (near existing enums if any):

```csharp
private enum HierarchyMode
{
    Restricted,  // Default mode - indent/outdent/remove, preserves order
    Free        // Full drag-drop reordering
}
```

**Step 2: Add mode state field**

Add this field below the enum:

```csharp
private HierarchyMode _currentMode = HierarchyMode.Restricted; // Default to Restricted
```

**Step 3: Add mode change handler stub**

Add this method:

```csharp
private async Task ChangeModeAsync(HierarchyMode newMode)
{
    if (_currentMode == newMode) return;

    // TODO: Check for unsaved changes (Phase 4)
    // TODO: Clear state (Phase 4)
    // TODO: Load appropriate content (Phase 4)

    _currentMode = newMode;
    StateHasChanged();
}
```

**Step 4: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 2>&1 | tail -20
```

Expected: "Application started" message with no build errors

**Step 5: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor
git commit -m "Add HierarchyMode enum and state management infrastructure"
```

---

### Task 2: Create Mode Switcher UI Component

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor` (replace existing mode buttons in markup)
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor.css` (add mode tab styles)

**Step 1: Remove old mode buttons**

Find and remove this section (around line 24-49):

```razor
<div class="generation-mode-bar">
    <div class="mode-buttons">
        <!-- All existing mode buttons -->
    </div>
</div>
```

**Step 2: Add mode tabs to left panel header**

Find the left panel header section (around line 134-225) and replace the header with:

```razor
<div class="panel-header">
    <div class="mode-tabs">
        <button class="mode-tab @(_currentMode == HierarchyMode.Restricted ? "active" : "")"
                @onclick="() => ChangeModeAsync(HierarchyMode.Restricted)">
            Restricted Mode
        </button>
        <button class="mode-tab @(_currentMode == HierarchyMode.Free ? "active" : "")"
                @onclick="() => ChangeModeAsync(HierarchyMode.Free)">
            Free Mode
        </button>
    </div>

    <!-- TODO: Add hierarchy selector here (Phase 2) -->

    <div class="header-buttons">
        <!-- Existing status and save button will stay here -->
        <span class="change-status @(HasChanges ? "has-changes" : "no-changes")"
              title="@(HasChanges ? "You have unsaved changes" : "No unsaved changes")">
            <i class="bi @(HasChanges ? "bi-exclamation-circle" : "bi-check-circle")"></i>
            <span class="status-text">@(HasChanges ? "Unsaved changes" : "No unsaved changes")</span>
        </span>
        <button class="btn btn-success btn-sm"
                @onclick="SaveHierarchyAsync"
                disabled="@(!HasChanges || string.IsNullOrEmpty(SelectedProject))"
                title="Save hierarchy">
            <i class="bi bi-save"></i> Save
        </button>
    </div>
</div>
```

**Step 3: Add mode tab CSS**

Add to `GenerateHierarchy.razor.css`:

```css
/* Mode Tabs (horizontal segmented control) */
.mode-tabs {
    display: flex;
    gap: 0;
    background-color: #2d2d2d; /* --panel-background */
    border-radius: 4px;
    padding: 2px;
    margin-right: 12px;
}

.mode-tab {
    padding: 4px 12px;
    font-size: 13px;
    font-weight: 500;
    color: #cccccc; /* --foreground */
    background-color: transparent;
    border: none;
    border-radius: 3px;
    cursor: pointer;
    transition: all 0.15s ease;
    white-space: nowrap;
}

.mode-tab:hover:not(.active) {
    background-color: #3e3e3e; /* Slightly lighter on hover */
    color: #ffffff;
}

.mode-tab.active {
    background-color: #0078d4; /* --accent-blue */
    color: #ffffff;
    font-weight: 600;
}

.mode-tab:focus {
    outline: 1px solid #0078d4;
    outline-offset: -1px;
}
```

**Step 4: Test in browser**

Navigate to http://localhost:8085/generate-hierarchy

Expected: See two tab-like buttons "Restricted Mode" | "Free Mode", Restricted is highlighted blue

**Step 5: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor PdfConversion/Pages/GenerateHierarchy.razor.css
git commit -m "Add mode switcher tabs to left panel header"
```

---

## Phase 2: Panel Header Restructure

### Task 3: Move Hierarchy Selector to Left Panel Header

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor.css`

**Step 1: Remove old settings panel**

Find and remove the entire settings panel (around line 52-91):

```razor
<div class="settings-panel">
    <!-- Remove this entire section -->
</div>
```

**Step 2: Add hierarchy selector to left panel header**

In the left panel header (after mode tabs), add:

```razor
<div class="hierarchy-selector-group">
    <select class="form-select form-select-sm"
            @bind="SelectedHierarchyXml"
            disabled="@string.IsNullOrEmpty(SelectedSourceXml)">
        <option value="">Select hierarchy...</option>
        @foreach (var file in HierarchyXmlFiles)
        {
            <option value="@file">@Path.GetFileName(file)</option>
        }
    </select>
    <button class="btn btn-sm btn-outline-secondary reload-btn"
            @onclick="OnLoadHierarchyClickedAsync"
            disabled="@(string.IsNullOrEmpty(SelectedHierarchyXml) || IsLoadingHierarchy)"
            title="Load selected hierarchy">
        @if (IsLoadingHierarchy)
        {
            <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
        }
        else
        {
            <i class="bi bi-arrow-clockwise"></i>
        }
    </button>
</div>
```

**Step 3: Add CSS for hierarchy selector group**

Add to `GenerateHierarchy.razor.css`:

```css
/* Hierarchy selector group in header */
.hierarchy-selector-group {
    display: flex;
    gap: 4px;
    align-items: center;
    flex: 0 1 auto;
    min-width: 250px;
    max-width: 400px;
}

.hierarchy-selector-group .form-select-sm {
    font-size: 13px;
    padding: 4px 8px;
    height: 30px;
    background-color: #3c3c3c; /* --input-background */
    color: #cccccc; /* --foreground */
    border: 1px solid #5a5a5a; /* --input-border */
}

.hierarchy-selector-group .reload-btn {
    padding: 4px 8px;
    height: 30px;
    min-width: 36px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 14px;
    color: #cccccc;
    border-color: #5a5a5a;
}

.hierarchy-selector-group .reload-btn:hover:not(:disabled) {
    background-color: #0078d4;
    border-color: #0078d4;
    color: #ffffff;
}

.hierarchy-selector-group .reload-btn:disabled {
    opacity: 0.4;
    cursor: not-allowed;
}
```

**Step 4: Update panel-header layout**

Update the `.panel-header` CSS to use flexbox layout:

```css
.panel-header {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 16px;
    background-color: #252526; /* --panel-header-background */
    border-bottom: 1px solid #3e3e42; /* --panel-border */
}

.panel-header .header-buttons {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-left: auto; /* Push to right */
}
```

**Step 5: Test in browser**

Expected: Hierarchy dropdown and reload button appear in left panel header after mode tabs

**Step 6: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor PdfConversion/Pages/GenerateHierarchy.razor.css
git commit -m "Move hierarchy selector into left panel header with reload button"
```

---

### Task 4: Move Source Selector to Right Panel Header

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor.css`

**Step 1: Add source selector to right panel header**

Find the right panel section (around line 282-288) and update:

```razor
<div class="right-panel">
    <div class="panel-header">
        <h5 class="panel-title">Available Headers</h5>
        <div class="source-selector-group">
            <ProjectFileSelector
                FileGroups="@ProjectFileGroups"
                SelectedValue="@GetSelectedFilePath()"
                OnSelectionChanged="@OnFilePathChangedFromComponent"
                CssClass="form-select form-select-sm"
                PlaceholderText="Select source XML..." />
        </div>
    </div>
    <div class="panel-content">
        <AvailableHeadersPanel @ref="HeadersPanel"
                              NormalizedXmlPath="@NormalizedXmlPath"
                              HierarchyXmlPath="@HierarchyXmlPath"
                              OnHeadersChanged="HandleHeadersChanged" />
    </div>
</div>
```

**Step 2: Add right panel header CSS**

Add to `GenerateHierarchy.razor.css`:

```css
/* Right panel header */
.right-panel .panel-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px 16px;
    background-color: #252526;
    border-bottom: 1px solid #3e3e42;
}

.right-panel .panel-title {
    margin: 0;
    font-size: 14px;
    font-weight: 600;
    color: #cccccc;
}

.source-selector-group {
    flex: 0 1 auto;
    min-width: 250px;
    max-width: 400px;
}

.source-selector-group .form-select-sm {
    font-size: 13px;
    padding: 4px 8px;
    height: 30px;
    background-color: #3c3c3c;
    color: #cccccc;
    border: 1px solid #5a5a5a;
}
```

**Step 3: Update right panel structure**

Ensure right panel has proper content wrapper:

```css
.right-panel {
    display: flex;
    flex-direction: column;
    height: 100%;
    overflow: hidden;
}

.right-panel .panel-content {
    flex: 1;
    overflow-y: auto;
    padding: 16px;
}
```

**Step 4: Test in browser**

Expected: Source XML selector appears in right panel header, "Available Headers" title on left

**Step 5: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor PdfConversion/Pages/GenerateHierarchy.razor.css
git commit -m "Move source XML selector into right panel header"
```

---

## Phase 3: Restricted Mode Controls

### Task 5: Add Primary Action Buttons (Indent/Outdent/Remove)

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor.css`

**Step 1: Add primary actions to left panel header**

Add after hierarchy selector group (only show in Restricted Mode):

```razor
@if (_currentMode == HierarchyMode.Restricted)
{
    <div class="primary-actions">
        <button class="btn btn-sm btn-outline-secondary"
                @onclick="OutdentSelectedHeaders"
                disabled="@(!CanOutdentCurrentSelection())"
                title="Outdent selected headers (Shift+Tab)">
            <i class="bi bi-arrow-left"></i>
        </button>

        <button class="btn btn-sm btn-outline-secondary"
                @onclick="IndentSelectedHeaders"
                disabled="@(!CanIndentCurrentSelection())"
                title="Indent selected headers (Tab)">
            <i class="bi bi-arrow-right"></i>
        </button>

        <button class="btn btn-sm btn-outline-secondary"
                @onclick="ExcludeSelectedHeaders"
                disabled="@(!_selectedManualItemIds.Any())"
                title="Remove selected headers (Delete)">
            <i class="bi bi-trash"></i>
        </button>
    </div>
}
```

**Step 2: Add primary actions CSS**

```css
/* Primary action buttons (Restricted Mode only) */
.primary-actions {
    display: flex;
    gap: 4px;
    align-items: center;
    padding-left: 12px;
    border-left: 1px solid #3e3e42;
}

.primary-actions .btn-sm {
    padding: 4px 8px;
    height: 30px;
    min-width: 36px;
    font-size: 14px;
    color: #cccccc;
    border-color: #5a5a5a;
    background-color: transparent;
}

.primary-actions .btn-sm:hover:not(:disabled) {
    background-color: #0078d4;
    border-color: #0078d4;
    color: #ffffff;
}

.primary-actions .btn-sm:disabled {
    opacity: 0.4;
    cursor: not-allowed;
}
```

**Step 3: Test in browser**

Switch to Restricted Mode, select items in tree
Expected: Three icon buttons (outdent/indent/remove) appear, enable/disable based on selection

**Step 4: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor PdfConversion/Pages/GenerateHierarchy.razor.css
git commit -m "Add primary action buttons for Restricted Mode"
```

---

### Task 6: Create Overflow Menu Component

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor.css`

**Step 1: Add overflow menu markup**

Add after primary actions (still inside Restricted Mode conditional):

```razor
<div class="overflow-menu">
    <div class="dropdown">
        <button class="btn btn-sm btn-outline-secondary dropdown-toggle"
                type="button"
                id="overflowMenuButton"
                data-bs-toggle="dropdown"
                aria-expanded="false"
                title="More options">
            <i class="bi bi-three-dots"></i>
        </button>
        <ul class="dropdown-menu dropdown-menu-end" aria-labelledby="overflowMenuButton">
            <li>
                <button class="dropdown-item"
                        @onclick="ShowRuleBasedModal"
                        disabled="@(string.IsNullOrEmpty(SelectedSourceXml))">
                    <i class="bi bi-gear me-2"></i> Generate with Rules
                </button>
            </li>
            <li>
                <button class="dropdown-item"
                        @onclick="ShowAiGenerationModal"
                        disabled="@(string.IsNullOrEmpty(SelectedSourceXml) || !_ollamaHealthy)">
                    <i class="bi bi-robot me-2"></i> Generate with AI
                </button>
            </li>
            <li><hr class="dropdown-divider"></li>
            <li>
                <button class="dropdown-item"
                        @onclick="ExpandAllItems"
                        disabled="@(_manualHierarchyItems == null || !_manualHierarchyItems.Any())">
                    <i class="bi bi-arrows-expand me-2"></i> Expand All
                </button>
            </li>
            <li>
                <button class="dropdown-item"
                        @onclick="CollapseAllItems"
                        disabled="@(_manualHierarchyItems == null || !_manualHierarchyItems.Any())">
                    <i class="bi bi-arrows-collapse me-2"></i> Collapse All
                </button>
            </li>
            <li>
                <button class="dropdown-item"
                        @onclick="IncludeAllHeaders"
                        disabled="@(_manualHeaders == null || !_manualHeaders.Any())">
                    <i class="bi bi-arrow-counterclockwise me-2"></i> Reset to Flat List
                </button>
            </li>
            <li>
                <button class="dropdown-item"
                        @onclick="DeselectAllHeaders"
                        disabled="@(!_selectedManualItemIds.Any())">
                    <i class="bi bi-x-square me-2"></i> Clear Selection
                </button>
            </li>
        </ul>
    </div>
</div>
```

**Step 2: Add overflow menu CSS**

```css
/* Overflow menu */
.overflow-menu {
    display: flex;
    align-items: center;
}

.overflow-menu .btn-sm {
    padding: 4px 8px;
    height: 30px;
    min-width: 36px;
    font-size: 14px;
    color: #cccccc;
    border-color: #5a5a5a;
}

.overflow-menu .btn-sm:hover {
    background-color: #0078d4;
    border-color: #0078d4;
    color: #ffffff;
}

/* Dropdown menu styling */
.overflow-menu .dropdown-menu {
    background-color: #2d2d2d;
    border: 1px solid #5a5a5a;
    border-radius: 4px;
    padding: 4px 0;
    min-width: 200px;
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

.overflow-menu .dropdown-item {
    padding: 6px 12px;
    font-size: 13px;
    color: #cccccc;
    background-color: transparent;
    border: none;
    width: 100%;
    text-align: left;
    cursor: pointer;
    display: flex;
    align-items: center;
}

.overflow-menu .dropdown-item:hover:not(:disabled) {
    background-color: #0078d4;
    color: #ffffff;
}

.overflow-menu .dropdown-item:disabled {
    opacity: 0.4;
    cursor: not-allowed;
}

.overflow-menu .dropdown-divider {
    border-top: 1px solid #5a5a5a;
    margin: 4px 0;
}
```

**Step 3: Test in browser**

Click "..." button in Restricted Mode
Expected: Dropdown menu appears with all options, disabled states work correctly

**Step 4: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor PdfConversion/Pages/GenerateHierarchy.razor.css
git commit -m "Add overflow menu with generation and utility options"
```

---

### Task 7: Add Selection Counter and Status Dot

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor.css`

**Step 1: Update header-buttons section**

Replace the existing change status text with dot:

```razor
<div class="header-buttons">
    @if (_currentMode == HierarchyMode.Restricted && _selectedManualItemIds.Any())
    {
        <span class="selection-counter">@_selectedManualItemIds.Count selected</span>
    }

    <span class="status-dot @(HasChanges ? "has-changes" : "")"
          title="@(HasChanges ? "Unsaved changes" : "No changes")">
    </span>

    <button class="btn btn-success btn-sm"
            @onclick="SaveHierarchyAsync"
            disabled="@(!HasChanges || string.IsNullOrEmpty(SelectedProject))"
            title="Save hierarchy">
        <i class="bi bi-save"></i> Save
    </button>
</div>
```

**Step 2: Add selection counter and status dot CSS**

```css
/* Selection counter */
.selection-counter {
    font-size: 12px;
    color: #cccccc;
    padding: 4px 8px;
    background-color: #3c3c3c;
    border-radius: 3px;
    white-space: nowrap;
}

/* Status dot (replaces text indicator) */
.status-dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    background-color: #4ec9b0; /* Green - no changes */
    flex-shrink: 0;
}

.status-dot.has-changes {
    background-color: #ff8c00; /* Orange - unsaved changes */
}
```

**Step 3: Remove old status text CSS**

Find and remove these old CSS rules:

```css
/* Remove these */
.change-status { ... }
.status-text { ... }
.has-changes { ... }
.no-changes { ... }
```

**Step 4: Test in browser**

Select items in Restricted Mode
Expected: "3 selected" counter appears, colored dot shows change status (green/orange)

**Step 5: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor PdfConversion/Pages/GenerateHierarchy.razor.css
git commit -m "Add selection counter and status dot indicator"
```

---

## Phase 4: Mode Switching Logic

### Task 8: Implement Confirmation Modal for Unsaved Changes

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`

**Step 1: Add modal state fields**

Add to @code block:

```csharp
private bool _showModeChangeConfirmation = false;
private HierarchyMode _pendingMode = HierarchyMode.Restricted;
```

**Step 2: Add confirmation modal markup**

Add at end of file (before existing modals):

```razor
@if (_showModeChangeConfirmation)
{
    <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
        <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">Unsaved Changes</h5>
                    <button type="button" class="btn-close" @onclick="CancelModeChange" aria-label="Close"></button>
                </div>
                <div class="modal-body">
                    <p>You have unsaved changes. Save before switching modes?</p>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-secondary" @onclick="CancelModeChange">Cancel</button>
                    <button type="button" class="btn btn-outline-danger" @onclick="DiscardAndChangeMode">Discard</button>
                    <button type="button" class="btn btn-primary" @onclick="SaveAndChangeMode">Save</button>
                </div>
            </div>
        </div>
    </div>
}
```

**Step 3: Add modal handler methods**

```csharp
private void CancelModeChange()
{
    _showModeChangeConfirmation = false;
    _pendingMode = _currentMode; // Reset
    StateHasChanged();
}

private async Task DiscardAndChangeMode()
{
    _showModeChangeConfirmation = false;
    HasChanges = false;
    await CompleteModeChangeAsync(_pendingMode);
}

private async Task SaveAndChangeMode()
{
    _showModeChangeConfirmation = false;
    await SaveHierarchyAsync();
    await CompleteModeChangeAsync(_pendingMode);
}
```

**Step 4: Test modal display**

Make changes in tree, try to switch modes
Expected: Modal appears asking to Save/Discard/Cancel

**Step 5: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor
git commit -m "Add confirmation modal for unsaved changes on mode switch"
```

---

### Task 9: Implement Mode Switching Logic

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`

**Step 1: Update ChangeModeAsync to check for changes**

Replace the stub method:

```csharp
private async Task ChangeModeAsync(HierarchyMode newMode)
{
    if (_currentMode == newMode) return;

    if (HasChanges)
    {
        _pendingMode = newMode;
        _showModeChangeConfirmation = true;
        StateHasChanged();
        return;
    }

    await CompleteModeChangeAsync(newMode);
}
```

**Step 2: Implement CompleteModeChangeAsync**

```csharp
private async Task CompleteModeChangeAsync(HierarchyMode newMode)
{
    var oldMode = _currentMode;
    _currentMode = newMode;

    // Clear hierarchy state
    CurrentHierarchy = null;
    _manualHierarchyItems = null;
    _selectedManualItemIds.Clear();
    HasChanges = false;

    // Load appropriate content based on new mode
    if (newMode == HierarchyMode.Restricted)
    {
        // Restricted Mode: Load flat header list if source XML selected
        if (!string.IsNullOrEmpty(SelectedSourceXml))
        {
            await LoadFlatHeaderListAsync();
        }
    }
    else // Free Mode
    {
        // Free Mode: Show empty state, require explicit hierarchy load
        // User must select and load a hierarchy XML file
    }

    StateHasChanged();
}
```

**Step 3: Add LoadFlatHeaderListAsync method**

```csharp
private async Task LoadFlatHeaderListAsync()
{
    try
    {
        var headers = await HeaderExtractionService.ExtractHeadersAsync(SelectedSourceXml);
        if (headers != null && headers.Any())
        {
            _manualHeaders = headers.ToList();
            _manualHierarchyItems = ManualHierarchyBuilder.CreateFlatHierarchy(_manualHeaders);
            StateHasChanged();
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load flat header list");
        SetAlert("Failed to load headers: " + ex.Message, "danger");
    }
}
```

**Step 4: Update panel content to show appropriate empty states**

Update the tree view section to show mode-appropriate messages:

```razor
@if (_manualHierarchyItems != null && _manualHierarchyItems.Any())
{
    <!-- Existing tree rendering -->
}
else
{
    <div class="text-center p-5 text-muted">
        @if (_currentMode == HierarchyMode.Restricted)
        {
            @if (string.IsNullOrEmpty(SelectedSourceXml))
            {
                <i class="bi bi-file-earmark-text" style="font-size: 3rem;"></i>
                <p class="mt-3">Select a source XML file to begin</p>
            }
            else
            {
                <i class="bi bi-hourglass-split" style="font-size: 3rem;"></i>
                <p class="mt-3">Loading headers...</p>
            }
        }
        else
        {
            <i class="bi bi-file-earmark-arrow-up" style="font-size: 3rem;"></i>
            <p class="mt-3">Select a hierarchy file to edit in Free Mode</p>
        }
    </div>
}
```

**Step 5: Test mode switching**

Test all scenarios:
- Restricted → Free with unsaved changes (modal appears)
- Restricted → Free without changes (clears state)
- Free → Restricted with source selected (loads flat list)
- Free → Restricted without source (shows empty state)

**Step 6: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor
git commit -m "Implement complete mode switching logic with state management"
```

---

### Task 10: Update Initial Page Load Behavior

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor`

**Step 1: Update OnInitializedAsync method**

Find existing `OnInitializedAsync` and update to default to Restricted Mode:

```csharp
protected override async Task OnInitializedAsync()
{
    // Set default mode
    _currentMode = HierarchyMode.Restricted;

    // Existing Ollama health check
    _ollamaHealthy = await OllamaService.IsHealthyAsync();

    // Load remembered selections
    var selections = await UserSelectionService.LoadSelectionsAsync();

    if (!string.IsNullOrEmpty(selections?.LastSelectedSourceXml))
    {
        SelectedSourceXml = selections.LastSelectedSourceXml;
        await LoadSourceXmlAsync(); // Triggers header extraction
    }

    if (!string.IsNullOrEmpty(selections?.LastSelectedHierarchyXml))
    {
        SelectedHierarchyXml = selections.LastSelectedHierarchyXml;
        await PopulateHierarchyXmlFilesAsync();

        // Auto-load hierarchy if it exists
        if (!string.IsNullOrEmpty(SelectedHierarchyXml) && File.Exists(SelectedHierarchyXml))
        {
            await OnLoadHierarchyClickedAsync();
        }
        else if (!string.IsNullOrEmpty(SelectedSourceXml))
        {
            // No hierarchy file, load flat list
            await LoadFlatHeaderListAsync();
        }
    }
    else if (!string.IsNullOrEmpty(SelectedSourceXml))
    {
        // Source selected but no hierarchy, load flat list
        await LoadFlatHeaderListAsync();
    }
}
```

**Step 2: Test initial page load scenarios**

Test with different user selection states:
- No selections → Empty Restricted Mode
- Source only → Flat list in Restricted Mode
- Source + Hierarchy → Hierarchy loaded in Restricted Mode

**Step 3: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor
git commit -m "Update initial page load to default to Restricted Mode with remembered state"
```

---

## Phase 5: Testing & Polish

### Task 11: Manual Testing Checklist

**Files:**
- None (manual testing only)

**Step 1: Test Restricted Mode workflows**

1. Open page → Verify Restricted Mode active by default
2. Select source XML → Verify flat header list loads
3. Select items with Ctrl/Shift → Verify selection counter updates
4. Use Indent/Outdent/Remove buttons → Verify operations work
5. Use keyboard shortcuts (Tab/Shift+Tab/Delete) → Verify still functional
6. Open overflow menu → Test each menu item
7. Make changes → Verify orange status dot appears
8. Save → Verify green status dot appears
9. Generate with Rules → Verify modal workflow works
10. Generate with AI → Verify modal workflow works

**Step 2: Test Free Mode workflows**

1. Switch to Free Mode (no changes) → Verify immediate switch
2. Switch to Free Mode (with changes) → Verify modal appears
3. Select hierarchy from dropdown → Click reload → Verify loads
4. Drag-drop items → Verify reordering works
5. Edit item labels → Verify editing works
6. Make changes → Verify status dot updates
7. Save → Verify saves correctly

**Step 3: Test mode switching edge cases**

1. Restricted → Free → Save in modal → Verify saves then switches
2. Restricted → Free → Discard in modal → Verify discards then switches
3. Restricted → Free → Cancel in modal → Verify stays in Restricted
4. Free → Restricted with source → Verify loads flat list
5. Free → Restricted without source → Verify shows empty state

**Step 4: Test file selection**

1. Change hierarchy dropdown → Verify doesn't auto-load
2. Click reload button → Verify loads selected file
3. Change source dropdown → Verify immediately extracts headers
4. Reload hierarchy after external change → Verify picks up changes

**Step 5: Test persistence**

1. Make selections → Refresh page → Verify selections restored
2. Close browser → Reopen → Verify selections persisted
3. Switch projects → Verify selections are project-specific

**Step 6: Document any issues found**

Create GitHub issues or add to CLAUDE.md if bugs found

---

### Task 12: CSS Polish and Responsive Adjustments

**Files:**
- Modify: `PdfConversion/Pages/GenerateHierarchy.razor.css`

**Step 1: Add responsive breakpoints**

Add media queries for smaller screens:

```css
/* Responsive adjustments for smaller screens */
@media (max-width: 1200px) {
    .hierarchy-selector-group {
        min-width: 200px;
        max-width: 300px;
    }

    .source-selector-group {
        min-width: 200px;
        max-width: 300px;
    }

    .mode-tab {
        padding: 4px 8px;
        font-size: 12px;
    }
}

@media (max-width: 992px) {
    .panel-header {
        flex-wrap: wrap;
        gap: 8px;
    }

    .hierarchy-selector-group,
    .source-selector-group {
        flex: 1 1 100%;
        max-width: 100%;
    }
}
```

**Step 2: Polish button hover states**

Ensure consistent hover effects:

```css
/* Consistent button hover transitions */
.btn-sm,
.mode-tab,
.reload-btn,
.primary-actions .btn-sm {
    transition: all 0.15s ease;
}

/* Focus states for accessibility */
.btn-sm:focus,
.mode-tab:focus,
.reload-btn:focus {
    outline: 2px solid #0078d4;
    outline-offset: 2px;
}
```

**Step 3: Add loading state animations**

```css
/* Loading spinner animation */
@keyframes spin {
    0% { transform: rotate(0deg); }
    100% { transform: rotate(360deg); }
}

.spinner-border {
    animation: spin 0.75s linear infinite;
}

/* Disabled state visual consistency */
:disabled,
.disabled {
    cursor: not-allowed !important;
    opacity: 0.4;
}
```

**Step 4: Test in different screen sizes**

Use browser dev tools to test at 1920px, 1440px, 1200px, 992px

**Step 5: Commit**

```bash
git add PdfConversion/Pages/GenerateHierarchy.razor.css
git commit -m "Add responsive design and CSS polish for all screen sizes"
```

---

### Task 13: Update Documentation

**Files:**
- Modify: `CLAUDE.md` (update hierarchy editor section)

**Step 1: Update CLAUDE.md section**

Find the section about `/generate-hierarchy` and update:

```markdown
### /generate-hierarchy - Hierarchy Editor

**Two Modes:**

1. **Restricted Mode (default)**
   - Preserves document order (required for section generation)
   - Allows indent/outdent operations
   - Allows removing items from hierarchy
   - Includes generation features (Rules-based, AI-based)
   - Keyboard shortcuts: Tab (indent), Shift+Tab (outdent), Delete (remove), Esc (clear selection)

2. **Free Mode**
   - Full drag-and-drop reordering
   - Edit item names/labels
   - Delete items
   - For editing pre-existing hierarchy files only

**UI Layout:**
- Left Panel: Hierarchy builder with mode tabs, hierarchy selector, and action buttons
- Right Panel: Available headers with source XML selector
- File selection: Dropdowns + explicit reload button (hierarchy), immediate load (source)

**Mode Switching:**
- Tab interface at top of left panel
- Confirms unsaved changes before switching
- Clears state when switching modes
- Restricted Mode loads flat list from source, Free Mode requires hierarchy file selection
```

**Step 2: Verify documentation accuracy**

Read through to ensure all features documented correctly

**Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "Update documentation for redesigned hierarchy editor"
```

---

### Task 14: Final Verification and Cleanup

**Files:**
- Multiple files (verification only)

**Step 1: Run compilation check**

```bash
docker logs taxxor-pdfconversion-1 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: "Application started" with no errors

**Step 2: Check for unused code**

Search for any old mode-related code that can be removed:
- Old `HierarchyMode.LoadExisting` references
- Old mode button event handlers
- Unused CSS classes

**Step 3: Verify all existing tests still pass**

```bash
npm run test:integration
```

Expected: All existing tests pass (new tests out of scope for this redesign)

**Step 4: Check for console errors**

Use browser dev tools → Console tab while testing all features
Expected: No JavaScript errors

**Step 5: Review git diff**

```bash
git diff main feature/hierarchy-editor-ui-redesign --stat
```

Verify changes are only in expected files

**Step 6: Final commit if any cleanup needed**

```bash
git add .
git commit -m "Final cleanup and verification for hierarchy editor redesign"
```

---

## Success Criteria

All criteria from design document must be met:

- [x] Restricted Mode is default on page load
- [x] Mode switching via clear tab interface
- [x] No separate "Load" button (reload icon integrated)
- [x] File selectors in panel headers (contextual)
- [x] All existing functionality works identically
- [x] No regressions in keyboard shortcuts
- [x] State persistence works across modes
- [x] UI feels less cluttered
- [x] Status indicator uses compact dot design
- [x] Overflow menu groups less-used operations
- [x] Selection counter appears when items selected
- [x] Confirmation modal on unsaved changes

---

## Post-Implementation

**After all tasks complete:**

1. Create comprehensive test PR description
2. Include before/after screenshots
3. Document any deviations from original design
4. List any edge cases discovered during implementation
5. Suggest follow-up improvements if any

**User testing checklist:**
- [ ] Restricted Mode workflow (header selection, indent/outdent, save)
- [ ] Free Mode workflow (load hierarchy, drag-drop, save)
- [ ] Mode switching with/without changes
- [ ] File selection and reload
- [ ] Generation features (Rules/AI)
- [ ] Keyboard shortcuts
- [ ] Responsive design on smaller screens
