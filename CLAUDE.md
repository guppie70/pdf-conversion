# PDF to Taxxor TDM XHTML Conversion Tool

## Quick Start

**Purpose:** Convert Adobe Acrobat-generated XML from PDF annual reports into Taxxor DM-compatible XHTML format
**Challenge:** PDFs created by different teams with inconsistent structure
**Tech Stack:** C# .NET 9 Blazor Server | XSLT 2.0/3.0 (Saxon) | Docker Compose

**Application URL:** http://localhost:8085

---

## 🧪 Sandbox Development Pattern (MANDATORY)

### When to Use Sandbox

**Before writing ANY code, follow this decision tree:**

```
Start Here
    ↓
Is this complex logic (algorithm/transformation/parsing)?
    ├─ YES → SANDBOX FIRST
    │         Isolate → Iterate → Integrate
    │
    └─ NO → Is this in a specialized domain?
             ├─ YES → Use appropriate AGENT
             │
             └─ NO → Direct work OK (investigation only)
```

**Use sandbox for:**
- Algorithms (sorting, filtering, transformation)
- Data processing (parsing, normalization, extraction)
- Business logic testable in isolation
- Any logic requiring rapid iteration
- **ALL testing** (never use .sh files)

### The 3-Phase Workflow

#### Phase 1: Isolate & Iterate (Sandbox)

**Add method to `PdfConversion/Endpoints/SandboxEndpoint.cs`:**
```csharp
private static async Task HandleYourTestAsync(HttpContext context, ILogger logger)
{
    // Hardcode test data here
    var input = @"<h3>Header1</h3><p>Content</p>";
    var result = YourAlgorithm(input);
    await context.Response.WriteAsync(result);
}
```

**Make it the default:**
```csharp
else { await HandleYourTestAsync(context, logger); } // Latest test
```

**Iterate rapidly:** Edit → Hot-reload (2s) → `curl http://localhost:8085/sandbox` → Verify

**Target:** < 5 second iteration cycles

#### Phase 2: Integrate (Service)

**Port working logic only:**
```csharp
// Service version - add structure, error handling, logging
public class YourService {
    public Result<string> Process(string input) {
        try {
            // Same core logic from sandbox
            return Result.Success(YourAlgorithm(input));
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed");
            return Result.Failure<string>(ex.Message);
        }
    }
}
```

#### Phase 3: Validate (Tests)

- Unit tests match sandbox test cases
- Add edge cases discovered during iteration
- Tests pass immediately (logic already proven)

### Implementation Rules

**Endpoint Structure:**
- Latest test = default (no mode parameter)
- Previous tests = named modes: `curl "http://localhost:8085/sandbox?mode=old-test"`
- Always use direct curl (never .sh files)

**Test Data:**
- Hardcode in endpoint (NEVER via parameters/files)
- Use approved locations only:
  - Sandbox tests: Hardcoded in `SandboxEndpoint.cs`
  - XSLT tests: `data/input/_test.xml`
  - Unit tests: `PdfConversion/Tests/TestData/`
  - Never in project root

**Quality Gates:**
- ✅ Correct output for all cases
- ✅ Edge cases handled
- ✅ Performance measured (< 100ms for algorithms)
- ✅ Ready for service integration

### Anti-Patterns

❌ Skip sandbox for "simple" logic → Complex edge cases emerge
❌ Test via UI workflow → Too slow for iteration
❌ Pass test data as parameters → URL encoding, size limits
❌ Mix infrastructure with logic → Reduces testability

---

## 📖 Core Definitions: Document Types

Understanding these 5 document types is critical - they represent stages in the transformation pipeline:

### 1. Input XML
- **What:** Raw XML extracted from PDF by Adobe Acrobat Professional
- **Contains:** Adobe-specific elements (`<H1>`, `<H2>`, `<P>`, `<Table>`)
- **Location:** `data/input/[customer]/projects/[project-id]/input.xml`

### 2. Normalized XML
- **What:** Taxxor DM-compatible XHTML after XSLT transformation
- **Contains:** Standard XHTML (`<h1>`, `<h2>`, `<p>`, `<table>` with wrappers)
- **Location:** `data/output/[customer]/projects/[project-id]/normalized.xml`

### 3. Hierarchy XML
- **What:** Document structure/table of contents for Taxxor DM
- **Contains:** Hierarchical item nodes with `data-ref` attributes
- **Location:** `data/output/[customer]/projects/[project-id]/hierarchy.xml`

### 4. Section XML
- **What:** Individual section files stored separately in Taxxor DM
- **Contains:** Self-contained XHTML fragments for one section
- **Location:** `data/output/[customer]/projects/[project-id]/sections/section-[id].xml`

### 5. In-Between XML
- **What:** Temporary fragment during section extraction (not persisted)
- **Contains:** Accumulated content for current section being processed
- **Location:** Memory only

### Context Files (Development Aid)

These files provide quick access to the current working state for development and debugging:

**Context Normalized XML** = `data/_work/_normalized.xml`
- **What:** Copy of the most recent Normalized XML from any transformation
- **When Written:** Automatically after every successful XSLT transformation in Transform page
- **Purpose:** Provides immediate context when developing/debugging without specifying project paths
- **Usage:** Read this file when user asks about "the normalized XML" without specifying a project

**Context Hierarchy XML** = `data/_work/_hierarchy.xml`
- **What:** Copy of the most recent Hierarchy XML from any save operation
- **When Written:** Automatically whenever hierarchy is saved (manual edit or auto-generated)
- **Purpose:** Provides immediate context when developing/debugging hierarchy-related features
- **Usage:** Read this file when user asks about "the hierarchy" without specifying a project

**Conversion Log** = `data/_work/_conversion.log`
- **What:** Timestamped log of the most recent section extraction process
- **When Written:** Automatically at the end of every conversion run (success, failure, or cancellation)
- **Purpose:** Provides detailed trace of section extraction for debugging matching/extraction issues
- **Usage:** Read this when user reports conversion problems or asks about extraction failures
- **Format:** Simple timestamped messages: `[HH:mm:ss] Log message`

**Conversion Extended Log** = `data/_work/_conversion-extended.log`
- **What:** Enhanced conversion log with statistics, categorized errors, and LLM analysis prompt
- **When Written:** Alongside _conversion.log at the end of every conversion run
- **Purpose:** Structured format optimized for LLM analysis of conversion problems
- **Usage:** Feed this file directly to an LLM when debugging complex extraction issues
- **Sections:** Configuration, Full Log, Statistics, Errors, Warnings, Duplicates, Analysis Prompt

**Roundtrip XML** = `data/_work/_roundtrip.xml`
- **What:** Reconstructed Normalized XML from combining all Section XML files
- **When Written:** After successful roundtrip validation completes
- **Purpose:** Enables comparison of original normalized XML vs. reconstructed to find extraction issues
- **Usage:** Read this when debugging why sections don't match the original transformation
- **Compare with:** `_normalized.xml` to identify what changed during section extraction

**Generation Technical Log** = `data/_work/_generation-technical.log`
- **What:** Detailed technical logs from rule-based hierarchy generation
- **When Written:** Automatically after successful rule-based hierarchy generation
- **Purpose:** Provides low-level debugging information about pattern matching and decision logic
- **Usage:** Read this when debugging hierarchy generation issues or understanding pattern application
- **Format:** Plain text log lines from RuleBasedHierarchyGenerator

**Generation Generic Log** = `data/_work/_generation-generic.log`
- **What:** User-friendly logs from rule-based hierarchy generation
- **When Written:** Automatically after successful rule-based hierarchy generation
- **Purpose:** Provides high-level summary of generation process and results
- **Usage:** Read this for quick overview of what the generator did
- **Format:** Plain text log lines suitable for end users

**Key Points:**
- These files represent the **last operation** regardless of project
- Always updated on success (logs written even on errors/cancellation)
- Not project-specific - reflects current working state only
- Read these when user provides insufficient context in their request
- Never ask user for project paths if you can use these context files instead
- Extended logs include prompts for LLM analysis - use them!

### Pipeline Overview

**Two input paths converging at transformation:**

```
PDF → [Adobe Acrobat Export] ──┐
                               ├──→ ① Input XML → [XSLT] → ② Normalized XML → [C# Services] → ③ Hierarchy + ④ Section XMLs → [Taxxor DM]
PDF → [Docling Service] ───────┘                                                                        ↑
                                                                                              ⑤ In-Between XML (temp)
```

**Key Points:**
- **Two input methods:** Adobe (manual export) or Docling (automated via API)
- **Converge at XSLT:** Both produce Input XML, then follow same transformation
- **XSLT workstream-aware:** Automatically detects Adobe vs. Docling input format
- **Same output:** Both paths produce identical Taxxor DM-compatible structure

---

## 🤖 Agent Quick Reference

| Workstream | Primary Agent | Model | Trigger Keywords |
|------------|---------------|-------|------------------|
| **WS1: XSLT** | `xslt-expert` | Session | "XSLT", "XPath", "transformation", "template", "pass1", "pass2" |
| **WS2: Section Gen** | `backend-developer` | Sonnet | "section", "hierarchy", "C# service", "extraction", "structure analysis" |
| **WS3: Blazor UI** | `frontend-developer` | Sonnet | "UI", "CSS", "Razor", "styling", "component", "layout", "preview" |
| **WS4: Architecture** | `architect-agent`, `devops-agent` | Opus/Sonnet | "Docker", "deploy", "AWS", "infrastructure", "design" |
| **Cross-Feature** | `fullstack-developer` | Sonnet | "implement feature", "end-to-end", "integrate" |

### ⚠️ CRITICAL Agent Rules

1. **SANDBOX FIRST for Algorithms** → See **🧪 Sandbox Development Pattern**
   - MANDATORY for: normalization, parsing, transformation, extraction
   - Skipping sandbox = automatic failure

2. **Domain Expertise Required**
   - Invoke agents for specialized work (frontend, backend, XSLT)
   - Do NOT perform domain work directly

3. **Testing Obligation**
   - Verify logic in isolation before integration

### Debugging Workflow

When investigating and fixing bugs, follow this **two-phase approach**:

**Phase 1: Diagnosis (direct work OK)**
- Read files, use browser tools, check logs, examine state
- Identify root cause through investigation
- Determine which component/service/file needs changes

**Phase 2: Implementation (use agent)**
- Call the appropriate agent to implement the fix
- Agent validates patterns, tests changes, and ensures quality
- **Even for "simple" fixes - use the agent if it modifies application behavior**

**Phase 3: Verification & Commit (direct work OK)**
- User tests the fix in their environment
- Verify all tests pass
- Commit the changes with clear description

### When to Use Agents

**Always use agents for:**
- ✅ Work involving **multiple files** in the agent's domain
- ✅ Changes to **shared/reusable components** (e.g., `Shared/*.razor`, service base classes)
- ✅ **User-visible behavior changes** (UI/CSS, API contracts, transformation logic)
- ✅ Work requiring **cross-file coordination** (e.g., component + CSS + JavaScript)
- ✅ **New features or capabilities** (even if seemingly straightforward)
- ✅ Changes that need **specialized testing or validation**

**Pattern-Based Examples:**
- Modifying any `.razor` file (component or page) → Use `frontend-developer`
- Modifying any `.razor.css` or global CSS → Use `frontend-developer`
- Adding/changing XSLT templates or XPath → Use `xslt-expert`
- Updating C# services, models, or business logic → Use `backend-developer`
- Changes requiring both frontend + backend → Use `fullstack-developer`
- Docker, deployment, infrastructure changes → Use `devops-agent` or `architect-agent`

### "But It's Just One Line!"

**Size doesn't matter - domain matters.** Even small changes should use the appropriate agent:

- ✅ 1-line CSS fix in a Razor file → Use `frontend-developer`
- ✅ 10-line markup change in a page component → Use `frontend-developer`
- ✅ Simple XSLT template addition → Use `xslt-expert`
- ✅ Minor service method change → Use `backend-developer`

Why? Agents ensure:
- Proper patterns and best practices
- Thorough testing and validation
- Consistent code quality standards
- Better documentation of changes

### When NOT to Use Agents

**Direct work is acceptable for:**
- ❌ **Investigation only** - reading code, examining state, checking logs
- ❌ **Trivial fixes** - typos in comments, obvious formatting issues
- ❌ **Documentation updates** - README, comments (that don't affect code behavior)
- ❌ **Testing/verification** - running tests, using browser to verify fixes

**Rule of Thumb:** If you're **writing/modifying code that affects application behavior**, use an agent. When in doubt, use an agent.

---

## 🎯 WORKSTREAM 1: XSLT Transformation

**Goal:** Transform ① Input XML → ② Normalized XML
**Agent:** `xslt-expert`

### Input Pipelines: Adobe vs Docling

**Two distinct pipelines** with different XSLT transformation roots:

**(a) Adobe XML:** `PDF → Acrobat Export XML 1.0 → xslt/adobe/transformation.xslt → Normalized`
Root: `<Document>` | Elements: `<H1>`, `<H2>`, `<P>`, `<Table>`, `<L>` | Wrapper: `<Document>{fragment}</Document>`

**(b) Docling:** `PDF → /docling-convert → docling-service → xslt/docling/transformation.xslt → Normalized`
Root: `<html>` | Generator: Docling HTML Serializer | Wrapper: `<html><head>...</head><body><div class='page'>{fragment}</div></body></html>`

**Auto-detection:** xslt-expert reads `LastSelectedXslt` from `data/user-selections.json`:
- Path contains `"adobe/"` → Adobe workstream
- Path contains `"docling/"` → Docling workstream

Full wrapper templates documented in `.claude/agents/xslt-expert.md`

### Key Files
- **Adobe:** `xslt/adobe/transformation.xslt` + `xslt/adobe/modules/`
- **Docling:** `xslt/docling/transformation.xslt` + `xslt/docling/modules/`
- **Shared:** `xslt/pass2/postprocess.xslt` (both workstreams)

### Architecture
- **Two-pass strategy:** Pass 1 (transform) → Pass 2 (cleanup)
- **XSLT 2.0/3.0** via XSLT3Service (Saxon-HE engine, port 4806)
- **Hot reload:** Changes auto-picked up by Docker
- **Mode strategy:**
  - Default mode (Pass 1): Main transformation, no `mode=` needed
  - `mode="pass2"`: Post-processing cleanup
  - `mode="table-header"`, `mode="table-body"`: Internal table processing
  - `mode="strip-prefix"`: List item prefix stripping

### Common Tasks

**Add element transformation:**
```xml
<!-- In appropriate module file -->
<xsl:template match="H4" priority="10">
    <h4><xsl:apply-templates select="@*|node()"/></h4>
</xsl:template>
```

**Debug XPath:** Check XSLT3Service logs at http://localhost:4806/swagger-ui

**Test changes:**
1. Edit XSLT file
2. Reload in web UI (http://localhost:8085)
3. Check preview pane

### XSLT-Specific Testing: `/transform-test` Endpoint

**For general sandbox workflow, see 🧪 Sandbox Development Pattern above.**

**XSLT Rapid Testing** using fixed test file:

**Test File:** `data/input/_test.xml` (fixed location, not project-specific)
**Endpoint:** `GET /transform-test?xslt=<stylesheet>`
**Smart Default:** Reads last selected XSLT from `data/user-selections.json`

**Quick Workflow:**
```bash
# 1. Create test fragment
cat > data/input/_test.xml << 'EOF'
<?xml version="1.0"?>
<Document>
  <L><LI><LBody>1. First item</LBody></LI></L>
</Document>
EOF

# 2. Edit XSLT templates in xslt/ (hot-reload)

# 3. Test instantly
curl http://localhost:8085/transform-test
curl http://localhost:8085/transform-test?xslt=modules/lists.xslt
curl http://localhost:8085/transform-test | head -20

# 4. Iterate until correct
```

**Agent Automation** (`/xsltdevelop` or `xslt-expert`):
- Auto-detects XML fragments in request
- Writes to `_test.xml` or copies from last selection
- Iterates using curl testing
- Uses stored selections from `data/user-selections.json`

### Transformation Rules
- `<H1>` → `<h1>` (headers normalized hierarchically)
- `<Table>` → Complex wrapper with `tablewrapper_[id]` div
- `<L>` → `<ul>` or `<ol>` (prefix detection)
- Remove: `<Artifact>`, `<bookmark-tree>`, `<x:xmpmeta>`

**Xslt3Service Fallback:** If unavailable, use System.Xml.Xsl for XSLT 1.0 (limited features)

---

## 🎯 WORKSTREAM 2: Section Generation

**Goal:** Convert ② Normalized XML → ③ Hierarchy + ④ Section XML files
**Agent:** `backend-developer`

### Key Files
- Services: `PdfConversion/Services/`
- Models: `PdfConversion/Models/`

### Process Flow
1. **Structure Analysis:** Parse Normalized XML, identify sections
2. **Hierarchy Building:** Create navigation tree with `data-ref` attributes
3. **Section Extraction:** Split content using ⑤ In-Between XML accumulator
4. **File Generation:** Write individual Section XML files

### Hierarchy Editor - Two Operating Modes

**The `/generate-hierarchy` page has two distinct modes:**

#### 1. Restricted Mode (Default)
- **Purpose:** Preserves document order (required for section generation)
- **Operations:** Indent, outdent, remove items from hierarchy
- **Generation:** Access to Rules-based and AI-based generation
- **Keyboard:** Tab (indent), Shift+Tab (outdent), Delete (remove), Esc (clear)
- **Initial State:** Loads flat header list automatically from selected source XML
- **Best for:** Creating new hierarchies, editing while preserving order

#### 2. Free Mode
- **Purpose:** Full editing flexibility for pre-existing hierarchies
- **Operations:** Drag-and-drop reordering, edit labels, delete items
- **Initial State:** Empty - requires explicit hierarchy file selection
- **Best for:** Fine-tuning existing hierarchy files

**UI Layout:**
- **Left Panel:** Hierarchy tree with mode tabs, hierarchy selector, action buttons
- **Right Panel:** Available headers with source XML selector
- **Mode Switching:** Confirms unsaved changes before switching, clears state on switch

**Generation Methods (Restricted Mode only):**

1. **Rule-Based** - Automatic hierarchy from header types (deterministic, 0.05s)
   - Service: `HierarchyGeneratorService.cs`
   - Best for: Well-structured documents with consistent header levels

2. **AI-Based** - LLM analyzes content and proposes structure (experimental)
   - Service: `OllamaService.cs`, `LlmPromptGenerator.cs`
   - Best for: Complex documents requiring semantic understanding

### Development Workflow
1. Edit C# files
2. Docker hot-reload applies changes
3. **CRITICAL:** Verify compilation (see below)
4. Test in UI

### ⚠️ VERIFY COMPILATION (CRITICAL)

**Common Mistake:** BashOutput contains ALL logs since container start. You'll see OLD "Application started" messages and incorrectly think it's working!

```bash
# The ONLY reliable way to check build status
docker logs taxxor-pdfconversion-1 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10

# Alternative: Count errors in last 100 lines
docker logs taxxor-pdfconversion-1 --tail 100 2>&1 | grep -c "error CS"
# Output: 0 = good, >0 = build failing
```

**❌ WRONG Example:**
```
[07:00:00] Application started    ← OLD (2 hours ago)
[07:30:00] Building...
[07:30:01] error CS0246            ← ACTUAL CURRENT STATE (failing!)
```

**✅ CORRECT Example:**
```
[08:31:25 INF] Building...
[08:31:25 INF] Now listening on: http://0.0.0.0:8085
[08:31:25 INF] Application started. Press Ctrl+C to shut down.
```
Last line shows "Application started" with RECENT timestamp → SUCCESS!

---

## 🎯 WORKSTREAM 3: Blazor UI & Features

**Goal:** Build user interface for transformation workflow
**Agents:** `frontend-developer`, `fullstack-developer`

### Key Files
- Components: `PdfConversion/Shared/*.razor`
- Pages: `PdfConversion/Pages/*.razor`
- Scoped CSS: `*.razor.css` (PREFERRED for component styles)
- Global CSS: `wwwroot/css/` (use sparingly)

### CSS Best Practices
1. **Always use scoped CSS first:** Create `Component.razor.css`
2. **Avoid global CSS** unless truly needed (typography, themes)
3. **Use `::deep` carefully** for child component styling
4. **Cache bust global CSS:** Add `?v=N` to references

**❌ DON'T Duplicate styles:**
```css
/* site.css - WRONG */
.toolbar-select { min-width: 250px; }
/* MainLayout.razor.css - WRONG (duplicate) */
.toolbar-select { min-width: 250px; }
```

**✅ DO Keep in scoped CSS only:**
```css
/* MainLayout.razor.css - CORRECT */
.nav-toolbar .toolbar-select {
    min-width: 250px;
    max-width: 400px;
}
```

### CSS Architecture

**Pattern:** Bootstrap 5 components + VS Code Dark Modern color palette

**Structure:**
- Use Bootstrap 5 for layout (grid, components, utilities)
- Override colors with VS Code Dark Modern theme for consistency
- Prefer scoped CSS (`.razor.css`) for component-specific styles
- Global CSS sparingly (typography, theme variables only)

**Key Principles:**
- Bootstrap provides structure and responsive behavior
- VS Code colors provide dark theme consistency
- Official palette: [dark_modern.json](https://github.com/microsoft/vscode/blob/main/extensions/theme-defaults/themes/dark_modern.json)

**Complete Color Palette:** See `frontend-developer` agent for 170+ color definitions and implementation examples

**Bootstrap Documentation:** Available via Context7 MCP server (`/websites/getbootstrap_5_3`) for component patterns and utilities

### Testing UI (MANDATORY)
```javascript
// 1. Navigate
mcp__playwright__browser_navigate → http://localhost:8085

// 2. Screenshot
mcp__playwright__browser_take_screenshot → verify visuals

// 3. Check computed styles
mcp__playwright__browser_evaluate →
() => {
  const element = document.querySelector('.your-element');
  const styles = window.getComputedStyle(element);
  return {
    color: styles.color,
    backgroundColor: styles.backgroundColor,
    fontSize: styles.fontSize
  };
}

// 4. Check console errors
mcp__playwright__browser_console_messages → onlyErrors: true
```

### Playwright MCP Browser Issues

**Problem:** Error message like "Browser is already in use for /Users/jthijs/Library/Caches/ms-playwright/mcp-chrome-a414727"

**Automatic Recovery Pattern:**
1. **First attempt:** Run `pkill -f 'mcp-chrome'` to kill stale browser processes
2. **Retry:** Attempt the browser test again
3. **Notify user only if retry fails:** If the issue persists after the retry, inform the user that manual intervention may be needed

This pattern allows for automatic recovery without interrupting the workflow in most cases.

### Common Issues
- Browser caching old CSS (solution: increment `?v=N`)
- Bootstrap overriding custom styles (solution: more specific selectors)
- Scoped CSS not applying (solution: check `::deep` usage)
- Playwright browser locked (solution: ask user to kill browser process)

---

## 🎯 WORKSTREAM 4: Architecture & Deployment

**Goal:** Infrastructure and deployment
**Agents:** `architect-agent`, `devops-agent`

### Docker Services

- **pdfconversion** (8085): Blazor app | Image: `txdotnetdevelop:net09` | Hot-reload: `./PdfConversion:/app`, `./xslt:/app/xslt`, `./data:/app/data`
- **xslt3service** (4806): Saxon XSLT 3.0 engine | Image: `xslt3service:production` | Config: `./config/Xslt3Service:/mnt/_config`
- **docling-service** (4808): Python Docling API | Platform: `linux/amd64` | Hot-reload: `./docling-service:/app`, `./data:/app/data`
- **Network:** `blazornetwork` (bridge driver)

See `docker-compose.yml` for full configuration.

### Development Commands

**NEVER use local `dotnet run` - always Docker:**

```bash
npm start      # Start services + follow logs
npm stop       # Stop all services
npm restart    # Restart pdfconversion
npm run logs   # View logs

# Direct Docker commands
docker logs taxxor-pdfconversion-1 --tail 100
docker compose restart pdfconversion
```

### Volume Mounts (hot reload)
- `./data:/app/data` - Input/output files
- `./xslt:/app/xslt` - XSLT files
- `./PdfConversion:/src` - Source code

---

## 📝 Documentation & File Conventions

### Documentation File Naming

**Convention**: Use lowercase filenames for all markdown documentation files.

**Exceptions** (industry standard):
- `README.md` (uppercase)
- `CLAUDE.md` (uppercase)

**Examples**:
- ✅ `architecture.md`
- ✅ `results.md`
- ✅ `sandbox-experiments.md`
- ❌ `Architecture.md`
- ❌ `RESULTS.md`

**Rationale**: Lowercase is easier to type, consistent across platforms, and avoids case-sensitivity issues in version control.

### data/_work/ Directory Usage

**Purpose**: Store ONLY application working files for debugging the conversion process.

**Allowed Files**:
- `_normalized.xml` - Copy of last transformed normalized XML
- `_hierarchy.xml` - Copy of last saved hierarchy XML
- `_conversion.log` - Conversion process log
- `_conversion-extended.log` - Extended conversion log with statistics
- `_roundtrip.xml` - Reconstructed XML from roundtrip validation
- Other `.xml` and `.log` files generated by the application

**NOT Allowed**:
- Documentation files (`.md`)
- Test output files (`.txt`, `.json`)
- Development notes
- Temporary scripts
- Any files not directly used by the application for debugging

**Cleanup**: If non-working files appear in `data/_work/`, move them to appropriate documentation folders or delete them.

**Why**: Keeps the working directory clean and makes it obvious which files the application actually uses vs developer notes.

---

## 📋 Cross-Cutting Concerns

### Git Workflow

**CRITICAL: Never commit before user tests!**

1. Make changes
2. Wait for user to test at http://localhost:8085
3. User confirms it works
4. THEN commit with descriptive message (no AI references)

### Testing Strategy

```bash
npm test                  # All tests
npm run test:integration  # Fast (bUnit) - 30 tests
npm run test:e2e         # Browser tests (Playwright) - 10 tests
```

**E2E Tests (Critical User Journeys):**
1. UserCanLoadProjectAndViewXML ⭐ **MOST CRITICAL**
2. UserCanTransformXMLWithXSLT ⭐ **CORE FEATURE**
3. UserCanToggleBetweenRenderedAndSourceViews
4. UserCanOpenAndUseSettingsPanel
5. UserCanSaveXSLTChanges

**Integration Tests (Component Tests):**
- Service Integration (8 tests)
- Toolbar State (6 tests)
- Data Binding (6 tests)
- Event Handling (6 tests)
- Transformation Options (4 tests)

### Development Checklist

- [ ] Changes made in Docker environment
- [ ] Compilation verified with `docker logs ... | tail -10`
- [ ] UI tested with Playwright MCP
- [ ] User tested functionality
- [ ] Tests pass
- [ ] Ready to commit

---

## 📚 Quick References

### Directory Structure
```
pdf-conversion/
├── CLAUDE.md
├── docker-compose.yml
├── package.json
├── data/
│   ├── input/
│   │   ├── optiver/projects/ar24-[1-9]/
│   │   ├── taxxor/projects/ar25-[1-9]/
│   │   └── test/projects/[test-*]/
│   ├── output/
│   │   ├── optiver/projects/ar24-[1-9]/
│   │   ├── taxxor/projects/ar25-[1-9]/
│   │   └── test/projects/[test-*]/
│   └── user-selections.json
├── xslt/
│   ├── adobe/
│   │   ├── transformation.xslt
│   │   └── modules/
│   ├── docling/
│   │   ├── transformation.xslt
│   │   └── modules/
│   └── pass2/
│       └── postprocess.xslt
├── docling-service/
│   ├── main.py
│   ├── requirements.txt
│   └── models/
└── PdfConversion/
    ├── Pages/
    ├── Shared/
    ├── Services/
    └── Tests/
```

### File Path Patterns
- Input XML: `data/input/[customer]/projects/[id]/input.xml`
- Normalized: `data/output/[customer]/projects/[id]/normalized.xml`
- Hierarchy: `data/output/[customer]/projects/[id]/hierarchy.xml`
- Sections: `data/output/[customer]/projects/[id]/sections/*.xml`
- XSLT Adobe: `xslt/adobe/transformation.xslt`
- XSLT Docling: `xslt/docling/transformation.xslt`
- User Selections: `data/user-selections.json`

**Examples:**
- Optiver production: `data/input/optiver/projects/ar24-3/input.xml`
- Taxxor production: `data/input/taxxor/projects/ar25-1/taxxor-full.pdf`
- Test project: `data/input/test/projects/test-pdf/input.xml`

### Service Endpoints
- Application: http://localhost:8085
- XSLT3Service API: http://localhost:4806/swagger-ui
- Docling Service API: http://localhost:4808/docs

### Application URLs & Testing (Base: http://localhost:8085)

**User-Facing Pages:**

| URL | Purpose | Test Checklist |
|-----|---------|----------------|
| `/` | Landing, service status | Nav links • Health indicators (✓ Connected) |
| `/transform` | XSLT transformation with preview | Select project → Load XML → Verify preview & source view |
| `/docling-convert` | PDF→XML conversion (SignalR) | Upload/select PDF → Monitor real-time progress → Check output file |
| `/generate-hierarchy` | Hierarchy builder (3 modes) | Load XML → Choose mode (Rule-based/AI/Manual) → Edit → Save |
| `/convert` | Section generation | Select files → Start conversion → Monitor log → Verify output files |
| `/production` | Batch processing | Multi-project workflows → Error handling |
| `/debug-validation` | Round-trip validation | Select files → Run validation → Review diff viewer |

**Testing & Development Pages:**

| URL | Purpose | Test Checklist |
|-----|---------|----------------|
| `/sandbox` | **Algorithm testing endpoint** | `curl http://localhost:8085/sandbox` • See 🧪 Sandbox Pattern |
| `/transform-test` | XSLT fragment testing | `curl http://localhost:8085/transform-test` • Uses `data/input/_test.xml` |
| `/hierarchy-test` | Hierarchy UI testing page | Manual hierarchy builder testing |
| `/test-modal` | Color palette testing | Verify modal dialogs • Test VS Code colors |

**API Endpoints:**

| URL | Purpose | Usage |
|-----|---------|-------|
| `/api/debug/disambiguation` | **Debug duplicate header resolution** | See details below |
| `/api/hierarchy/last-request-params` | Get last hierarchy request | Debug hierarchy generation |
| `/api/projects/{customer}/{projectId}` | Delete project | `DELETE` method |
| `/health` | Health check | Docker health probe |
| `/health/liveness` | Liveness probe | Kubernetes/container orchestration |
| `/health/readiness` | Readiness probe | Load balancer health checks |

**Disambiguation Debug Endpoint** (`/api/debug/disambiguation`):

This endpoint exposes the internal logic of duplicate header resolution for debugging. When multiple headers in a document match the same hierarchy item, this shows WHY specific candidates were selected or rejected.

```bash
# Basic usage
curl "http://localhost:8085/api/debug/disambiguation?projectId=dutch-flower-group/ar24&hierarchyFile=hierarchies/hierarchy-pdf-xml.xml"

# With explicit normalized file
curl "http://localhost:8085/api/debug/disambiguation?projectId=dutch-flower-group/ar24&hierarchyFile=hierarchies/hierarchy-pdf-xml.xml&normalizedFile=normalized/docling-pdf.xml"
```

**Returns JSON with:**
- `duplicateGroups[]` - Each group of duplicate headers
  - `candidates[]` - Each candidate match with:
    - `position` - Document position
    - `includedAfterFilter` - Whether it passed position filtering
    - `filterReason` - Why it was included/excluded
    - `forwardLookingScore` - Score based on subsequent headers
    - `expectedHeaders` / `foundHeaders` - What was checked
    - `previousContext` / `nextContext` - Text around the header
  - `autoSelectionResult` - Which candidate was auto-selected (or "MANUAL_REQUIRED")
  - `autoSelectionReason` - Why that decision was made

**Use this when:** Conversion stops for manual selection and you don't understand why the system can't auto-resolve.

**Playwright pattern:** `mcp__playwright__browser_navigate({ url: "http://localhost:8085/[page]" })`

### Common Problems & Solutions

| Problem | Solution |
|---------|----------|
| Build errors | Check `docker logs taxxor-pdfconversion-1 --tail 100` |
| CSS not updating | Increment `?v=N` in references |
| XSLT not working | Check XSLT3Service logs, verify XPath |
| Docker not responding | `npm restart` |
| Tests failing | Check app running at :8085 |

---

**Last Updated:** 2025-01-30
**Status:** Production-ready, dual-pipeline (Adobe + Docling), Manual Mode complete