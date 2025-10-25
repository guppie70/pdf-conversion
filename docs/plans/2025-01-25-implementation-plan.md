# Implementation Plan: Docling + AI Hierarchy Feature

**Design Doc:** See `2025-01-25-docling-ai-hierarchy-design.md` for full details

**Branch:** `feature/docling-ai-hierarchy`

**Timeline:** 7 weeks (Jan 27 - Mar 17, 2025)

---

## Quick Start

**To begin implementation:**
```bash
# Create feature branch
git checkout main
git pull
git checkout -b feature/docling-ai-hierarchy

# Start coding!
```

**To resume after a break:**
1. Read design doc: `docs/plans/2025-01-25-docling-ai-hierarchy-design.md`
2. Check this file for progress (✅ = done, 🔄 = in progress, ⏸️ = blocked)
3. Review recent commits: `git log --oneline -10`
4. Continue from current task

---

## Phase 1: Docling Pipeline (Weeks 1-2)

**Goal:** Replace Adobe XML pipeline with reliable Docling conversion

### 1.1: Docling Docker Service Setup
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `docling-service/` directory structure
- [ ] Write `docling-service/requirements.txt`:
  ```txt
  fastapi==0.104.1
  uvicorn[standard]==0.24.0
  docling==1.0.0
  python-multipart==0.0.6
  pydantic==2.5.0
  ```
- [ ] Write `docling-service/main.py` with FastAPI skeleton
- [ ] Add service to `docker-compose.yml`
- [ ] Test: `docker compose up docling-service`
- [ ] Verify hot-reload: Edit main.py → check logs for restart
- [ ] Access Swagger UI: http://localhost:4807/swagger-ui

**Acceptance Criteria:**
- ✅ Service starts without errors
- ✅ Hot-reload works (edit Python → auto-restart)
- ✅ Swagger UI accessible and shows endpoints
- ✅ Health endpoint returns 200 OK

**Reference:** Design doc Section "1. Docling Service"

---

### 1.2: Docling Conversion API
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `docling-service/services/docling_converter.py`
- [ ] Implement `DoclingConverter` class
- [ ] Add `POST /convert` endpoint in main.py
  - Accept file upload (PDF/Word)
  - Accept project_id parameter
  - Return output_file path and page_count
- [ ] Test conversion with sample PDF
- [ ] Verify output saved to correct path: `data/input/optiver/projects/{project_id}/docling-output.xml`
- [ ] Add error handling:
  - Invalid file type → 400 error
  - Corrupt PDF → 500 with message
  - Large file → progress indication

**Test Commands:**
```bash
# Upload test PDF
curl -X POST http://localhost:4807/convert \
  -F "file=@test.pdf" \
  -F "project_id=test-project" \
  -F "output_format=docbook"

# Check output
cat data/input/optiver/projects/test-project/docling-output.xml
```

**Acceptance Criteria:**
- ✅ PDF converts to DocBook XML successfully
- ✅ Word (.docx) converts successfully
- ✅ Invalid files return clear error messages
- ✅ Output file saved to correct location
- ✅ API returns page count

**Reference:** Design doc Section "1. Docling Service"

---

### 1.3: `/docling-convert` Page
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `PdfConversion/Pages/DoclingConvert.razor`
- [ ] Add route: `@page "/docling-convert"`
- [ ] Add to navigation menu (position 2, after Home)
- [ ] Implement UI:
  - [ ] Project selector (reuse existing component)
  - [ ] File upload (drag-drop + browse button)
  - [ ] "Convert with Docling" button
  - [ ] Progress indicator (spinner + elapsed time)
  - [ ] Monaco editor preview (reuse from /transform)
  - [ ] "Save to Project" button
  - [ ] "Next: Transform →" button
- [ ] Implement backend:
  - [ ] Call Docling API via HttpClient
  - [ ] Handle file upload
  - [ ] Display conversion progress
  - [ ] Load result into Monaco editor
  - [ ] Save to project directory
- [ ] Add error handling:
  - [ ] Service offline → show error + fallback option
  - [ ] Conversion failed → show error details
  - [ ] Timeout → cancel button

**Acceptance Criteria:**
- ✅ Page accessible at /docling-convert
- ✅ Can upload PDF/Word files
- ✅ Conversion shows progress indicator
- ✅ Output displayed in Monaco editor
- ✅ "Save" creates file in correct location
- ✅ "Next" navigates to /transform
- ✅ Errors display clearly

**Reference:** Design doc Section "New Pages: 1. /docling-convert"

---

### 1.4: XSLT Reorganization
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create directory structure:
  ```
  xslt/
  ├── adobe/
  └── docling/
  ```
- [ ] Move files:
  ```bash
  mv xslt/transformation.xslt xslt/adobe/
  mv xslt/modules/ xslt/adobe/
  mv xslt/pass2/ xslt/adobe/
  ```
- [ ] Update code references:
  - [ ] Search for hardcoded "xslt/" paths
  - [ ] Update TransformationService.cs
  - [ ] Update /transform page
  - [ ] Update appsettings.json (if applicable)
- [ ] Add pipeline selector to `data/user-selections.json`:
  ```json
  {
    "selectedPipeline": "adobe",
    "selectedXsltFile": "adobe/transformation.xslt"
  }
  ```
- [ ] Test: Transform page still works with Adobe pipeline
- [ ] Update Docker volume mounts if needed

**Acceptance Criteria:**
- ✅ Existing /transform page works unchanged
- ✅ Adobe XSLT files load from new location
- ✅ No broken file references
- ✅ Tests still pass

**Reference:** Design doc Section "3. XSLT Organization"

---

### 1.5: Docling XSLT Transformation
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `xslt/docling/transformation.xslt`
- [ ] Implement basic structure:
  ```xml
  <xsl:stylesheet version="2.0">
    <xsl:output method="xml" indent="yes"/>
    <xsl:template match="/">
      <!-- Root template -->
    </xsl:template>
  </xsl:stylesheet>
  ```
- [ ] Implement transformations:
  - [ ] Headers (h1-h6) → preserve as-is
  - [ ] Tables → add tablewrapper divs
  - [ ] Lists (ul/ol) → preserve structure
  - [ ] Paragraphs → preserve
- [ ] Test with Docling output from ar24-3
- [ ] Compare output quality: Docling vs. Adobe
- [ ] Iterate on XSLT until output matches schema

**Test Process:**
```bash
# Use /transform-test endpoint for rapid iteration
echo '<docbook-xml>...</docbook-xml>' > data/input/_test.xml
curl http://localhost:8085/transform-test?xslt=docling/transformation.xslt
```

**Acceptance Criteria:**
- ✅ Docling XML transforms to valid Normalized XML
- ✅ Headers preserved correctly
- ✅ Tables have proper structure
- ✅ Lists nested correctly
- ✅ Output validates against schema
- ✅ Quality equal or better than Adobe pipeline

**Reference:** Design doc Section "1.5: Docling XSLT Transformation"

---

### 1.6: Integration Testing
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create integration test: `DoclingPipeline_PdfToNormalizedXml_Succeeds()`
- [ ] Test full workflow:
  1. Upload PDF to Docling service
  2. Convert to DocBook XML
  3. Transform to Normalized XML
  4. Validate output
- [ ] Test with existing projects:
  - [ ] ar24-1
  - [ ] ar24-2
  - [ ] ar24-3
- [ ] Document quality improvements:
  - [ ] Table structure preservation
  - [ ] Header consistency
  - [ ] Processing time
- [ ] Create comparison report: Docling vs. Adobe

**Acceptance Criteria:**
- ✅ End-to-end pipeline works reliably
- ✅ Output quality meets requirements
- ✅ Tests pass consistently
- ✅ Documentation updated

**Reference:** Design doc Section "Testing Strategy"

---

## Phase 2: AI Hierarchy Generation (Weeks 3-4)

**Goal:** Add LLM-powered hierarchy generation with local Ollama

### 2.1: Ollama Service Integration
**Status:** ⏳ Not Started

**Prerequisites:**
- [ ] Install Ollama: `brew install ollama`
- [ ] Pull model: `ollama pull llama3.1:70b`
- [ ] Start service: `ollama serve`
- [ ] Configure keep-alive: `OLLAMA_KEEP_ALIVE=1h`
- [ ] Verify: `curl http://localhost:11434/api/tags`

**Tasks:**
- [ ] Add `host.docker.internal` to docker-compose.yml:
  ```yaml
  pdfconversion:
    extra_hosts:
      - "host.docker.internal:host-gateway"
  ```
- [ ] Create `PdfConversion/Services/OllamaService.cs`
- [ ] Implement methods:
  - [ ] `CheckHealthAsync()` - ping Ollama API
  - [ ] `GetAvailableModelsAsync()` - list installed models
  - [ ] `WarmUpModelAsync(string model)` - preload model
  - [ ] `GenerateAsync(string model, string prompt, CancellationToken ct)` - generate text
- [ ] Add error handling:
  - [ ] Ollama offline → clear error message
  - [ ] Model not found → suggest installation
  - [ ] Timeout → cancellation support
- [ ] Test from Docker container:
  ```csharp
  var response = await _httpClient.GetAsync("http://host.docker.internal:11434/api/tags");
  ```

**Acceptance Criteria:**
- ✅ Can call Ollama API from Blazor container
- ✅ Can list available models
- ✅ Can warm up model (preload)
- ✅ Can generate text with timeout
- ✅ Errors handled gracefully

**Reference:** Design doc Section "2. Ollama Integration"

---

### 2.2: Hierarchy Generator Service
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `PdfConversion/Services/HierarchyGeneratorService.cs`
- [ ] Define models:
  ```csharp
  public class HierarchyProposal { ... }
  public class HierarchyItem { ... }
  ```
- [ ] Implement `GenerateHierarchyAsync()`:
  - [ ] Load normalized XML
  - [ ] Load example hierarchies (user-selected)
  - [ ] Build 4-part prompt
  - [ ] Call Ollama API
  - [ ] Parse JSON response
  - [ ] Calculate confidence scores
  - [ ] Extract uncertainties
- [ ] Add error handling:
  - [ ] Invalid JSON → retry with stricter prompt
  - [ ] Timeout → clear error message
  - [ ] Low confidence (<50%) → warning
- [ ] Add unit tests (10 tests):
  - [ ] Mock Ollama responses
  - [ ] Test prompt building
  - [ ] Test JSON parsing
  - [ ] Test confidence calculation
  - [ ] Test error scenarios

**Acceptance Criteria:**
- ✅ Generates hierarchy from Normalized XML
- ✅ Returns confidence scores per item
- ✅ Flags uncertainties
- ✅ Handles errors gracefully
- ✅ Unit tests pass

**Reference:** Design doc Section "4. Hierarchy Generator Service"

---

### 2.3: Prompt Engineering
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Extract hierarchy rules to configuration file
- [ ] Create prompt template with 4 parts:
  1. System instructions (from brainstorm-input.md)
  2. Example hierarchies (user-selected 2-3)
  3. Current document (full Normalized XML)
  4. Task & JSON output format
- [ ] Implement prompt assembly logic
- [ ] Test with ar24-3:
  - [ ] Generate hierarchy
  - [ ] Compare to manual hierarchy
  - [ ] Measure accuracy
- [ ] Tune parameters:
  - [ ] Temperature (try 0.1, 0.3, 0.5)
  - [ ] Top-p
  - [ ] Max tokens
- [ ] Handle edge cases:
  - [ ] No clear hierarchy
  - [ ] Deep nesting (>10 levels)
  - [ ] Duplicate header names
  - [ ] Empty/meaningless headers

**Test Process:**
```bash
# Manual test with Ollama CLI
ollama run llama3.1:70b < prompt.txt

# Programmatic test
var result = await _hierarchyGen.GenerateAsync(
    "normalized.xml",
    ["ar23-1", "ar23-2"],
    "llama3.1:70b"
);
```

**Acceptance Criteria:**
- ✅ Prompt includes all necessary context
- ✅ LLM returns valid JSON consistently
- ✅ Accuracy >70% vs. manual hierarchy
- ✅ Confidence scores reflect quality
- ✅ Edge cases handled gracefully

**Reference:** Design doc Section "6. Prompt Engineering Strategy"

---

### 2.4: Model Configuration
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Add model settings to `data/user-selections.json`:
  ```json
  {
    "selectedLlmModel": "llama3.1:70b",
    "llmTemperature": 0.3
  }
  ```
- [ ] Create model selector UI component:
  - [ ] Dropdown populated from Ollama API
  - [ ] Refresh button
  - [ ] Display model size
  - [ ] Display status (loaded/not loaded)
- [ ] Add temperature slider (0.1 - 1.0)
- [ ] Save preferences on change
- [ ] Load preferences on page init

**Acceptance Criteria:**
- ✅ User can select from available models
- ✅ Preferences persist across sessions
- ✅ UI shows model status clearly

**Reference:** Design doc Section "5. Ollama Service"

---

### 2.5: Page Skeleton
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `PdfConversion/Pages/GenerateHierarchy.razor`
- [ ] Add route: `@page "/generate-hierarchy"`
- [ ] Add to navigation menu (position 4)
- [ ] Implement basic layout:
  - [ ] Settings panel (top)
  - [ ] Split view: Tree (left) + Headers (right)
  - [ ] Reasoning panel (bottom)
- [ ] Implement settings panel:
  - [ ] Model selector
  - [ ] Example hierarchy multi-select
  - [ ] Temperature slider
  - [ ] Generate button
  - [ ] Save button
- [ ] Implement "Generate with AI" flow:
  - [ ] Show progress indicator
  - [ ] Display elapsed time
  - [ ] Show cancel button
  - [ ] Handle completion/error
- [ ] Display results (basic table for now):
  - [ ] Show hierarchy items
  - [ ] Show confidence scores
  - [ ] Show uncertainties
- [ ] Implement model preload:
  - [ ] OnInitialized → warm up model
  - [ ] Show status: "Loading..." → "✓ Ready"

**Acceptance Criteria:**
- ✅ Page accessible at /generate-hierarchy
- ✅ Model warms up on page load
- ✅ Can generate hierarchy
- ✅ Results display clearly
- ✅ Progress indicator works

**Reference:** Design doc Section "New Pages: 3. /generate-hierarchy"

---

### 2.6: Integration Testing
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create integration test (marked [Explicit]):
  ```csharp
  [Test]
  [Explicit] // Requires Ollama running
  public async Task HierarchyGen_WithRealLlm_Succeeds()
  {
      // Test with real Ollama
      var result = await _hierarchyGen.GenerateAsync(...);
      Assert.That(result.OverallConfidence, Is.GreaterThan(0.7));
  }
  ```
- [ ] Test with multiple example hierarchies
- [ ] Measure generation time (target: <60 sec)
- [ ] Validate output hierarchy XML format
- [ ] Test error scenarios:
  - [ ] Ollama offline
  - [ ] Timeout
  - [ ] Invalid JSON
- [ ] Document accuracy vs. manual hierarchies

**Acceptance Criteria:**
- ✅ Real LLM test passes (when run manually)
- ✅ Generation completes in <60 seconds
- ✅ Output format valid
- ✅ Error handling works

**Reference:** Design doc Section "Testing Strategy"

---

## Phase 3: Drag-Drop Hierarchy Editor (Weeks 5-6)

**Goal:** Build interactive tree editor for validation

### 3.1: Hierarchy Tree View Component
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `PdfConversion/Shared/HierarchyTreeView.razor`
- [ ] Implement recursive tree rendering
- [ ] Add visual elements:
  - [ ] Indentation for hierarchy levels
  - [ ] Folder/document icons
  - [ ] Confidence badges with color coding
  - [ ] Warning indicators (⚠️) for <70% confidence
  - [ ] Expand/collapse buttons
- [ ] Add selection:
  - [ ] Click to select item
  - [ ] Highlight selected item
  - [ ] Keyboard navigation (arrow keys)
- [ ] Create scoped CSS: `HierarchyTreeView.razor.css`

**Acceptance Criteria:**
- ✅ Tree renders correctly
- ✅ Visual hierarchy clear via indentation
- ✅ Confidence badges display with colors
- ✅ Can expand/collapse items
- ✅ Styling consistent with app theme

**Reference:** Design doc Section "New Pages: 3. /generate-hierarchy (Left Panel)"

---

### 3.2: Inline Editing
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Add click handler to item title
- [ ] Show input field on click
- [ ] Implement keyboard shortcuts:
  - [ ] Enter → save
  - [ ] Escape → cancel
  - [ ] Tab → save and move to next
- [ ] Validate input:
  - [ ] Non-empty title
  - [ ] No special characters (if needed)
- [ ] Update hierarchy object
- [ ] Highlight changed items (dirty flag)
- [ ] Add "Revert" button for changed items

**Acceptance Criteria:**
- ✅ Can edit item titles inline
- ✅ Keyboard shortcuts work
- ✅ Validation prevents empty titles
- ✅ Changes tracked visually

**Reference:** Design doc Section "3.2: Inline Editing"

---

### 3.3: Drag-Drop Interactions
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `wwwroot/js/hierarchy-editor.js`
- [ ] Implement HTML5 Drag & Drop API:
  ```javascript
  function initDragDrop(treeId, headersId) {
    // dragstart, dragover, drop handlers
    // Callback to Blazor: DotNet.invokeMethodAsync()
  }
  ```
- [ ] Add drag handlers in Razor component
- [ ] Implement drop zones:
  - [ ] Drop above item → reorder
  - [ ] Drop on item → make child
- [ ] Add visual feedback:
  - [ ] Drop zone highlight
  - [ ] Cursor change
  - [ ] Ghost image during drag
- [ ] Update hierarchy object after drop
- [ ] Add undo support (optional)

**Drag Scenarios:**
- Drag item within tree → reorder at same level
- Drag item onto another → make child
- Drag header from right panel → add to hierarchy

**Acceptance Criteria:**
- ✅ All drag-drop scenarios work
- ✅ Visual feedback clear
- ✅ Hierarchy updates correctly
- ✅ No JavaScript errors

**Reference:** Design doc Section "3.3: Drag-Drop Interactions"

---

### 3.4: Context Menu
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create context menu component
- [ ] Show on right-click:
  - [ ] Add child
  - [ ] Delete
  - [ ] Indent (make child of previous sibling)
  - [ ] Outdent (move up one level)
- [ ] Implement keyboard shortcuts:
  - [ ] Tab → indent
  - [ ] Shift+Tab → outdent
  - [ ] Delete → delete item
- [ ] Add confirmation for delete
- [ ] Update hierarchy after each action

**Acceptance Criteria:**
- ✅ Context menu shows on right-click
- ✅ All actions work correctly
- ✅ Keyboard shortcuts work
- ✅ Delete confirmation prevents accidents

**Reference:** Design doc Section "3.4: Context Menu"

---

### 3.5: Available Headers Panel
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `PdfConversion/Shared/AvailableHeadersPanel.razor`
- [ ] Extract headers from Normalized XML:
  ```csharp
  var headers = normalizedXml.Descendants()
      .Where(e => e.Name.LocalName.StartsWith("h"))
      .Select(e => new Header {
          Level = e.Name.LocalName,
          Title = e.Value,
          XPath = e.GetXPath()
      });
  ```
- [ ] Display headers with:
  - [ ] Level badges ([h1], [h2], etc.)
  - [ ] Title text
  - [ ] Used/unused indicator
- [ ] Add search box:
  - [ ] Filter by title text
  - [ ] Clear button
- [ ] Add level filter:
  - [ ] Dropdown: All, h1, h2, h3, etc.
- [ ] Enable drag from panel:
  - [ ] Set data transfer on drag start
  - [ ] Gray out when added to hierarchy
- [ ] Add hover preview:
  - [ ] Show surrounding context
  - [ ] Tooltip or side panel

**Acceptance Criteria:**
- ✅ All headers from XML displayed
- ✅ Search filters work
- ✅ Level filter works
- ✅ Can drag to tree view
- ✅ Used headers grayed out

**Reference:** Design doc Section "New Pages: 3. /generate-hierarchy (Right Panel)"

---

### 3.6: Validation Logic
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create `HierarchyValidator.cs`
- [ ] Implement validation rules:
  - [ ] All items have non-empty titles
  - [ ] All items have unique IDs
  - [ ] No orphaned items
  - [ ] Valid hierarchy structure (no cycles)
- [ ] Add warnings:
  - [ ] Low confidence items (<70%)
  - [ ] Unused headers (in XML but not in hierarchy)
  - [ ] Very deep nesting (>8 levels)
- [ ] Display validation summary:
  - [ ] Show errors (blocking)
  - [ ] Show warnings (non-blocking)
  - [ ] Highlight invalid items
- [ ] Prevent save if errors exist

**Acceptance Criteria:**
- ✅ Validation catches common errors
- ✅ Error messages clear and actionable
- ✅ Can't save invalid hierarchy
- ✅ Warnings don't block save

**Reference:** Design doc Section "3.6: Validation Logic"

---

### 3.7: Reasoning Panel
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create reasoning panel component
- [ ] Display overall confidence:
  - [ ] Large percentage (e.g., "87%")
  - [ ] Gauge or progress bar
  - [ ] Color coding (green/yellow/red)
- [ ] List strong matches:
  - [ ] Items with >80% confidence
  - [ ] Count and summary
- [ ] List uncertainties:
  - [ ] Items with <70% confidence
  - [ ] AI explanation for each
  - [ ] Expandable details
- [ ] Show AI reasoning:
  - [ ] Text summary from LLM
  - [ ] Decision criteria used
  - [ ] Collapsible section

**Acceptance Criteria:**
- ✅ Confidence displayed prominently
- ✅ Uncertainties clearly flagged
- ✅ Reasoning helps user understand decisions
- ✅ UI clean and readable

**Reference:** Design doc Section "New Pages: 3. /generate-hierarchy (Bottom Panel)"

---

### 3.8: Save & Load
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Implement `SaveHierarchyAsync()`:
  - [ ] Serialize to Hierarchy XML format
  - [ ] Validate XML schema
  - [ ] Write to `data/output/.../hierarchy.xml`
  - [ ] Update project metadata
- [ ] Implement `LoadHierarchyAsync()`:
  - [ ] Read existing hierarchy.xml
  - [ ] Parse to HierarchyProposal object
  - [ ] Load into tree view
- [ ] Add "Load Existing" button:
  - [ ] Show dialog with available hierarchies
  - [ ] Select and load
- [ ] Add "New" button:
  - [ ] Clear current hierarchy
  - [ ] Start fresh
- [ ] Track changes:
  - [ ] Dirty flag if modified
  - [ ] Confirm before load/new

**Acceptance Criteria:**
- ✅ Can save hierarchy to XML
- ✅ Saved XML validates
- ✅ Can load existing hierarchy
- ✅ Changes tracked correctly

**Reference:** Design doc Section "3.8: Save & Load"

---

### 3.9: Integration Testing
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Create UI workflow tests:
  - [ ] `GenerateHierarchyPage_LoadsModel_ShowsReady()`
  - [ ] `GenerateHierarchyPage_DragDrop_UpdatesHierarchy()`
  - [ ] `GenerateHierarchyPage_SaveHierarchy_CreatesValidXml()`
- [ ] Test all interactions:
  - [ ] Drag-drop
  - [ ] Inline editing
  - [ ] Context menu
  - [ ] Search/filter
  - [ ] Save/load
- [ ] Test validation logic
- [ ] Test with complex hierarchy (>30 items)

**Acceptance Criteria:**
- ✅ All UI tests pass
- ✅ Complex hierarchies work smoothly
- ✅ No performance issues

**Reference:** Design doc Section "Testing Strategy"

---

## Phase 4: Testing & Polish (Week 7)

**Goal:** Complete test coverage, fix bugs, polish UX

### 4.1: Unit Tests
**Status:** ⏳ Not Started

**Target:** 30 unit tests, >80% code coverage

**Tasks:**
- [ ] Docling service tests (5 tests) - Python
- [ ] HierarchyGeneratorService tests (10 tests) - C#
- [ ] OllamaService tests (3 tests) - C#
- [ ] XSLT transformation tests (5 tests) - C#
- [ ] UI component tests (7 tests) - C# bUnit
- [ ] Run coverage report: `npm run test:coverage`
- [ ] Fix gaps in coverage

**Acceptance Criteria:**
- ✅ 30 unit tests written
- ✅ All tests pass
- ✅ Coverage >80%

---

### 4.2: Integration Tests
**Status:** ⏳ Not Started

**Target:** 15 integration tests

**Tasks:**
- [ ] Pipeline tests (3 tests)
- [ ] Hierarchy generation tests (3 tests)
- [ ] UI workflow tests (3 tests)
- [ ] Cross-service tests (3 tests)
- [ ] Error handling tests (3 tests)

**Acceptance Criteria:**
- ✅ 15 integration tests written
- ✅ All tests pass reliably

---

### 4.3: E2E Tests
**Status:** ⏳ Not Started

**Target:** 3 E2E tests (Playwright)

**Tasks:**
- [ ] `UserCanConvertPdfWithDocling()`
- [ ] `UserCanGenerateHierarchyWithAI()`
- [ ] `UserCanCompleteFullDoclingWorkflow()`

**Acceptance Criteria:**
- ✅ 3 E2E tests written
- ✅ All tests pass against running app

---

### 4.4: Error Handling Review
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Test all error scenarios (see design doc Section 9)
- [ ] Verify error messages clear and actionable
- [ ] Test recovery actions work
- [ ] Add missing error handlers
- [ ] Document known issues/limitations

**Acceptance Criteria:**
- ✅ All error scenarios tested
- ✅ Error UX polished
- ✅ No unhandled exceptions

---

### 4.5: UI/UX Polish
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Consistent styling across new pages
- [ ] Smooth transitions and animations
- [ ] Loading states and spinners
- [ ] Responsive layout (if needed)
- [ ] Accessibility:
  - [ ] Keyboard navigation works everywhere
  - [ ] ARIA labels on interactive elements
  - [ ] Screen reader tested (optional)
- [ ] Dark theme consistency
- [ ] Browser compatibility (Chrome, Firefox, Safari)

**Acceptance Criteria:**
- ✅ UI feels polished and professional
- ✅ Consistent with existing app design
- ✅ Accessible via keyboard

---

### 4.6: Documentation Updates
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Update CLAUDE.md:
  - [ ] Add Docling workflow section
  - [ ] Update agent usage patterns
  - [ ] Add troubleshooting tips
- [ ] Add XSLT comments (docling/transformation.xslt)
- [ ] Document prompt engineering approach
- [ ] Create user guide (optional)
- [ ] Update README.md if needed

**Acceptance Criteria:**
- ✅ CLAUDE.md up to date
- ✅ Code well-commented
- ✅ Future developers can understand design

---

### 4.7: Performance Testing
**Status:** ⏳ Not Started

**Tasks:**
- [ ] Test with large PDFs:
  - [ ] 100 pages
  - [ ] 200+ pages
  - [ ] Measure conversion time
- [ ] Test with complex hierarchies:
  - [ ] 50+ items
  - [ ] Deep nesting (8+ levels)
  - [ ] Measure UI responsiveness
- [ ] Test LLM generation:
  - [ ] Measure time for different models
  - [ ] Test with large Normalized XML (>1MB)
- [ ] Optimize if needed:
  - [ ] Pagination for large documents
  - [ ] Lazy loading in tree view
  - [ ] Debounce search input

**Acceptance Criteria:**
- ✅ Large documents process in <5 minutes
- ✅ UI remains responsive
- ✅ LLM generation completes in <60 seconds

---

## Progress Tracking

**How to Update:**
- Mark ✅ when task complete
- Mark 🔄 when task in progress
- Mark ⏸️ when blocked
- Add notes after each session

**Current Status:** Not Started

**Last Updated:** 2025-01-25

**Next Task:** Phase 1.1 - Docling Docker Service Setup

---

## Session Notes

**Session 1 (YYYY-MM-DD):**
- Started Phase 1.1
- Created docling-service directory
- [Add notes here...]

**Session 2 (YYYY-MM-DD):**
- Completed Phase 1.1
- Started Phase 1.2
- [Add notes here...]

[Continue adding notes for each session...]

---

## Quick Commands

**Start services:**
```bash
npm start
```

**Check service status:**
```bash
docker ps
curl http://localhost:4807/health  # Docling
curl http://localhost:11434/api/tags  # Ollama
```

**Run tests:**
```bash
npm test                    # All tests
npm run test:integration    # Fast tests
npm run test:e2e           # Browser tests
```

**Check compilation:**
```bash
docker logs taxxor-pdfconversion-1 2>&1 | grep -E "(Building|error|Application started)" | tail -10
```

**View logs:**
```bash
npm run logs
npm run logs:docling
docker logs taxxor-pdfconversion-1 --tail 100
```

---

## Questions / Blockers

**Current blockers:** None

**Questions for next session:**
- [Add questions here...]

---

*For full architectural details, see: `2025-01-25-docling-ai-hierarchy-design.md`*
