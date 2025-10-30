# PDF to Taxxor TDM XHTML Conversion Tool

## Quick Start

**Purpose:** Convert Adobe Acrobat-generated XML from PDF annual reports into Taxxor DM-compatible XHTML format
**Challenge:** PDFs created by different teams with inconsistent structure
**Tech Stack:** C# .NET 9 Blazor Server | XSLT 2.0/3.0 (Saxon) | Docker Compose

**Application URL:** http://localhost:8085

## 🎯 DEFAULT TESTING APPROACH (USE THIS FIRST)

### Sandbox Endpoint Workflow

**Standard Pattern for ALL testing:**

1. **Add new test to SandboxEndpoint.cs**
   - Create `HandleYourTestAsync()` method
   - The LATEST test becomes the default (no mode parameter needed)
   - Previous tests remain accessible via `?mode=<name>`

2. **Run tests with direct curl commands:**
   ```bash
   # Run latest test (default, no mode parameter)
   curl http://localhost:8085/sandbox

   # Run specific test by name
   curl "http://localhost:8085/sandbox?mode=test-hierarchy"
   curl "http://localhost:8085/sandbox?mode=prompt-gen"
   curl "http://localhost:8085/sandbox?mode=llm-comparison"
   ```

**NEVER:**
- ❌ Create .sh files in project root
- ❌ Add test endpoints to Program.cs
- ❌ Create test files outside approved locations
- ❌ Use anything except direct curl commands for testing

---

## 🚀 Development Patterns & Workflows

### MANDATORY Development Pattern Decision Tree

**Before writing ANY code, follow this decision tree:**

```
Start Here
    ↓
Is this complex logic (algorithm/transformation/parsing)?
    ├─ YES → Use SANDBOX PATTERN FIRST
    │         Create /sandbox endpoint → Test isolated → Then implement
    │
    └─ NO → Is this in a specialized domain?
             ├─ YES → Use appropriate AGENT
             │         (frontend, backend, XSLT, etc.)
             │
             └─ NO → Is this just investigation?
                      ├─ YES → Direct work OK
                      │         (reading files, checking logs)
                      │
                      └─ NO → Use appropriate AGENT
```

### The Sandbox-First Rule

**MANDATORY for these scenarios:**
- Algorithm development (sorting, filtering, transformation logic)
- Complex data processing (parsing, normalization, extraction)
- Business logic that can be tested in isolation
- Any logic where you need rapid iteration to get it right
- **ANY TESTING** - Always use sandbox endpoint with direct curl commands

**WHY:** Sandbox enables sub-second iteration cycles. Full integration takes minutes.

**HOW:**
1. Add new method to `PdfConversion/Endpoints/SandboxEndpoint.cs`:
   ```csharp
   private static async Task HandleYourTestAsync(
       HttpContext context,
       ILogger logger)
   {
       // Your test logic here with hardcoded test data
   }
   ```

2. Make it the default in `HandleAsync()`:
   ```csharp
   else
   {
       // DEFAULT: Latest active test
       await HandleYourTestAsync(context, logger);
   }
   ```

3. Iterate rapidly: Edit → Hot-reload (2s) → `curl http://localhost:8085/sandbox` → Verify

4. Once working, move previous default to named mode (if needed):
   ```csharp
   else if (mode == "previous-test")
   {
       await HandlePreviousTestAsync(context, logger);
   }
   ```

5. Port working logic to actual service and add unit tests

### 🚨 CRITICAL Testing Rules

**ALWAYS use these patterns:**
- ✅ **Direct curl commands** - `curl http://localhost:8085/sandbox` for latest test
- ✅ **Sandbox endpoints** - Add methods to SandboxEndpoint.cs for all tests
- ✅ **Hardcoded test data** - Put test data directly in sandbox methods
- ✅ **Latest test is default** - No mode parameter needed for active development
- ✅ **Named modes for history** - Previous tests accessible via `?mode=<name>`

**NEVER do these:**
- ❌ **Create .sh files in project root** - Use direct curl commands instead
- ❌ **Create test files in project root** - Only use approved test data locations
- ❌ **Use Program.cs for test endpoints** - Always use SandboxEndpoint.cs
- ❌ **Create temporary test endpoints** - All tests go in sandbox with modes

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

**Key Points:**
- These files represent the **last operation** regardless of project
- Always updated on success (logs written even on errors/cancellation)
- Not project-specific - reflects current working state only
- Read these when user provides insufficient context in their request
- Never ask user for project paths if you can use these context files instead
- Extended logs include prompts for LLM analysis - use them!

### Pipeline Overview

```
PDF → [Adobe] → ① Input XML → [XSLT] → ② Normalized XML → [C# Services] → ③ Hierarchy + ④ Section XMLs → [Taxxor DM]
                                                                    ↑
                                                         ⑤ In-Between XML (temp)
```

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

1. **SANDBOX FIRST for Algorithm Development**
   - Agents MUST use `/sandbox` pattern for complex logic BEFORE service implementation
   - This includes: normalization, parsing, transformation, extraction algorithms
   - Skipping sandbox = automatic failure for algorithm tasks

2. **Domain Expertise Required**
   - Agents MUST be invoked via Task tool when work matches their domain
   - Do NOT perform specialized work directly

3. **Testing Obligation**
   - Agents MUST verify logic works in isolation before integration
   - Use rapid iteration: sandbox → curl → verify → iterate

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

### Testing with Isolated XML Fragments

**Rapid XSLT testing using the `/transform-test` endpoint:**

This workflow enables sub-second iteration cycles when developing XSLT transformations.

**Fixed Test File Location:** `data/input/_test.xml` (not project-specific)

**Endpoint:** `GET /transform-test?xslt=<stylesheet>`

**Smart Defaults:** The endpoint automatically reads the last selected XSLT file from `data/user-selections.json`

**Parameters:**
- `xslt` (optional) - XSLT file path relative to `/app/xslt/` (overrides stored selection)

**Workflow:**

1. **Create test XML file** at fixed location:
   ```bash
   cat > data/input/_test.xml << 'EOF'
   <?xml version="1.0" encoding="UTF-8"?>
   <Document>
     <!-- Your test fragment here -->
     <L><LI><LBody>1. First item</LBody></LI></L>
   </Document>
   EOF
   ```

2. **Edit XSLT templates** in `xslt/` directory (hot-reload applies changes)

3. **Test transformation instantly:**
   ```bash
   # Test with default transformation.xslt
   curl http://localhost:8085/transform-test

   # Test with specific XSLT file
   curl http://localhost:8085/transform-test?xslt=modules/lists.xslt

   # Preview first 20 lines
   curl http://localhost:8085/transform-test | head -20

   # Save output to file
   curl http://localhost:8085/transform-test > result.xml
   ```

4. **Iterate until correct** (edit XSLT → curl → check output)

5. **Verify with full source** after isolated test passes

**Benefits:**
- ⚡ Sub-second iteration cycles (no UI interaction)
- 🎯 Isolated testing of specific transformations
- 📝 Clear examples of edge cases
- 🔧 Perfect for agent-based development

### Test Data Location Rules

**IMPORTANT:** Never create test files in the project root.

**Allowed locations for test data:**
- **`data/input/_test.xml`** - For manual XSLT testing via `/transform-test` endpoint
- **`PdfConversion/Tests/TestData/`** - For automated test fixtures (bUnit/Playwright)
- **`data/input/test/projects/`** - Test projects with sample data (test-docling-conversion, test-pdf, test-project)
- **`data/input/optiver/projects/ar24-[1-3]/`** - Use existing project data for full integration tests

**Examples of what NOT to do:**
- ❌ `test-*.xml` files in project root
- ❌ `sample-*.xml` files in project root
- ❌ Any test data outside the approved locations above

**Why this matters:** Test files in the project root clutter the directory structure and make it unclear where test data should live. Always use the designated locations above.

**Example: Testing nested lists**
```bash
# Create test fragment
cat > data/input/_test.xml << 'EOF'
<?xml version="1.0"?>
<Document>
  <L>
    <LI><LBody>1. Parent item</LBody>
      <L>
        <LI><LBody>1.1 Nested child</LBody></LI>
      </L>
    </LI>
  </L>
</Document>
EOF

# Edit xslt/modules/lists.xslt to fix nested list handling

# Test instantly
curl http://localhost:8085/transform-test

# Expected output: Valid nested <ul>/<li> structure
```

**Automated Workflow (via /xsltdevelop or xslt-expert agent):**

When using `/xsltdevelop` command or calling the xslt-expert agent directly:

1. **Fragment Detection**: Agent analyzes your request for XML fragments
2. **Auto-Setup**:
   - **If fragment found**: Writes to `data/input/_test.xml`
   - **If no fragment**: Reads `data/user-selections.json` and copies last selected source XML to `_test.xml`
   - **If uncertain**: Asks via AskUserQuestion
3. **XSLT Development**: Agent edits XSLT templates
4. **Instant Testing**: Uses `curl http://localhost:8085/transform-test` for feedback
5. **Iteration**: Repeats steps 3-4 until correct

**User Selection Persistence:**

Selections made in the UI are automatically saved to:
- **Browser**: `localStorage` (dev_selectedProject, dev_selectedFile)
- **Server**: `/app/data/user-selections.json` (persistent, agent-accessible)

This enables the `/transform-test` endpoint and agents to use your last selected XSLT file automatically.

**Error Handling:**
- `404` - Test file or XSLT file not found
- `500` - Transformation failed (error message in response body)

**Note:** Always test with full `input.xml` after verifying isolated transformation works.

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

### VS Code Dark Modern Color Palette

**IMPORTANT:** All UI components MUST use the official VS Code Default Dark Modern theme colors for consistency.

**Source:** [microsoft/vscode - dark_modern.json](https://github.com/microsoft/vscode/blob/main/extensions/theme-defaults/themes/dark_modern.json)

#### Core Backgrounds & Borders

```css
/* Primary Backgrounds */
--editor-bg: #1F1F1F;              /* Main editor background */
--panel-bg: #181818;               /* Panels, sidebar, activity bar */
--widget-bg: #202020;              /* Editor widgets (find, peek) */
--menu-bg: #1F1F1F;                /* Menus, notifications */

/* Borders & Dividers */
--border-primary: #2B2B2B;         /* Primary borders (panels, tabs) */
--border-secondary: #3C3C3C;       /* Secondary borders (inputs, checkboxes) */
--border-subtle: #FFFFFF17;        /* Subtle borders (editor groups) */
--border-widget: #313131;          /* Widget borders */

/* Interactive Backgrounds */
--input-bg: #313131;               /* Input fields, dropdowns */
--button-secondary-bg: #313131;    /* Secondary buttons */
--checkbox-bg: #313131;            /* Checkboxes */
```

#### Text & Foreground Colors

```css
/* Text Colors */
--text-primary: #CCCCCC;           /* Primary text, editor text */
--text-bright: #FFFFFF;            /* Active tab text, headers */
--text-muted: #9D9D9D;             /* Inactive text, descriptions */
--text-subtle: #868686;            /* Very subtle text (inactive activity bar) */
--text-input-placeholder: #989898; /* Input placeholder text */

/* Specialized Text */
--line-number: #6E7681;            /* Inactive line numbers */
--line-number-active: #CCCCCC;     /* Active line number */
--icon-foreground: #CCCCCC;        /* Icon color */
--keybinding-label: #CCCCCC;       /* Keybinding labels */
```

#### Accent & State Colors

```css
/* Primary Accent (Blue) */
--accent-blue: #0078D4;            /* Focus border, active borders, buttons */
--accent-blue-hover: #026EC1;      /* Button hover state */
--accent-blue-subtle: #2489DB82;   /* Input option active background */
--accent-blue-badge: #0078D4;      /* Activity bar badge, status bar remote */

/* State Indicators */
--success-green: #2EA043;          /* Added files, positive states */
--error-red: #F85149;              /* Errors, deleted files */
--warning-yellow: #BB800966;       /* Warnings, modified indicators */
--modified-blue: #0078D4;          /* Modified files */

/* Tab Selection */
--tab-selected-top: #6caddf;       /* Selected but unfocused tab border */
```

#### Buttons & Interactive Elements

```css
/* Primary Button */
--button-bg: #0078D4;              /* Primary button background */
--button-fg: #FFFFFF;              /* Primary button text */
--button-hover-bg: #026EC1;        /* Primary button hover */
--button-border: #FFFFFF12;        /* Button border (subtle) */

/* Secondary Button */
--button-secondary-bg: #313131;    /* Secondary button background */
--button-secondary-fg: #CCCCCC;    /* Secondary button text */
--button-secondary-hover: #3C3C3C; /* Secondary button hover */

/* Badge */
--badge-bg: #616161;               /* Generic badge background */
--badge-fg: #F8F8F8;               /* Generic badge text */
```

#### Status Bar & Activity Bar

```css
/* Status Bar */
--statusbar-bg: #181818;           /* Status bar background */
--statusbar-fg: #CCCCCC;           /* Status bar text */
--statusbar-border: #2B2B2B;       /* Status bar border */
--statusbar-hover-bg: #F1F1F133;   /* Status bar item hover */
--statusbar-hover-fg: #FFFFFF;     /* Status bar item hover text */
--statusbar-debug-bg: #0078D4;     /* Debugging mode background */
--statusbar-debug-fg: #FFFFFF;     /* Debugging mode text */
--statusbar-no-folder: #1F1F1F;    /* No folder open background */

/* Activity Bar */
--activitybar-bg: #181818;         /* Activity bar background */
--activitybar-fg: #D7D7D7;         /* Activity bar icons */
--activitybar-inactive-fg: #868686;/* Inactive activity bar icons */
--activitybar-border: #2B2B2B;     /* Activity bar border */
--activitybar-active-border: #0078D4; /* Active view indicator */
```

#### Tabs & Panels

```css
/* Tabs */
--tab-active-bg: #1F1F1F;          /* Active tab background */
--tab-active-fg: #FFFFFF;          /* Active tab text */
--tab-active-border-top: #0078D4;  /* Active tab top border */
--tab-inactive-bg: #181818;        /* Inactive tab background */
--tab-inactive-fg: #9D9D9D;        /* Inactive tab text */
--tab-hover-bg: #1F1F1F;           /* Tab hover background */
--tab-border: #2B2B2B;             /* Tab borders */

/* Panel Titles */
--panel-title-active-fg: #CCCCCC;  /* Active panel title */
--panel-title-inactive-fg: #9D9D9D;/* Inactive panel title */
--panel-title-active-border: #0078D4; /* Active panel bottom border */
```

#### Dropdowns & Inputs

```css
/* Dropdown */
--dropdown-bg: #313131;            /* Dropdown background */
--dropdown-fg: #CCCCCC;            /* Dropdown text */
--dropdown-border: #3C3C3C;        /* Dropdown border */
--dropdown-list-bg: #1F1F1F;       /* Dropdown list background */

/* Input Fields */
--input-bg: #313131;               /* Input background */
--input-fg: #CCCCCC;               /* Input text */
--input-border: #3C3C3C;           /* Input border */
--input-placeholder: #989898;      /* Placeholder text */
--input-focus-border: #0078D4;     /* Focus border (via focusBorder) */
```

#### Editor Gutter (Git Decorations)

```css
/* Git Status Colors */
--gutter-added: #2EA043;           /* Added lines */
--gutter-modified: #0078D4;        /* Modified lines */
--gutter-deleted: #F85149;         /* Deleted lines */
```

#### Quick Picker & Menus

```css
/* Quick Input */
--quickinput-bg: #222222;          /* Quick picker background */
--quickinput-fg: #CCCCCC;          /* Quick picker text */

/* Menu */
--menu-bg: #1F1F1F;                /* Menu background */
--menu-selection-bg: #0078D4;      /* Menu item selection */

/* Picker Groups */
--picker-group-border: #3C3C3C;    /* Picker group separator */
```

#### Progress & Notifications

```css
/* Progress Bar */
--progress-bg: #0078D4;            /* Progress bar fill */

/* Notifications */
--notification-bg: #1F1F1F;        /* Notification background */
--notification-fg: #CCCCCC;        /* Notification text */
--notification-border: #2B2B2B;    /* Notification border */
--notification-header-bg: #1F1F1F; /* Notification header background */
```

#### Terminal & Text Blocks

```css
/* Terminal */
--terminal-fg: #CCCCCC;            /* Terminal text */
--terminal-active-border: #0078D4; /* Active terminal tab */

/* Text Blocks */
--text-blockquote-bg: #2B2B2B;     /* Blockquote background */
--text-blockquote-border: #616161; /* Blockquote border */
--text-codeblock-bg: #2B2B2B;      /* Code block background */
--text-preformat-bg: #3C3C3C;      /* Preformatted text background */
--text-preformat-fg: #D0D0D0;      /* Preformatted text */
--text-separator: #21262D;         /* Text separator line */
```

#### Links & Chat

```css
/* Links */
--link-fg: #4daafc;                /* Link color */
--link-active-fg: #4daafc;         /* Active link color */

/* Chat Features */
--chat-slash-command-bg: #26477866; /* Slash command background */
--chat-slash-command-fg: #85B6FF;  /* Slash command text */
--chat-edited-file-fg: #E2C08D;    /* Edited file indicator */
```

#### Usage Examples

**Panels:**
```css
.panel {
    background: #181818;  /* --panel-bg */
    border: 1px solid #2B2B2B;  /* --border-primary */
}

.panel-header {
    background: #1F1F1F;  /* --editor-bg or menu-bg */
    color: #FFFFFF;  /* --text-bright */
    border-bottom: 1px solid #2B2B2B;
}
```

**Form Controls:**
```css
.form-select, .form-control {
    background: #313131;  /* --input-bg */
    border: 1px solid #3C3C3C;  /* --input-border */
    color: #CCCCCC;  /* --text-primary */
}

.form-select:focus {
    border-color: #0078D4;  /* --accent-blue */
    outline: none;
}
```

**Tabs:**
```css
.nav-tabs .nav-link {
    background: #181818;  /* --tab-inactive-bg */
    color: #9D9D9D;  /* --tab-inactive-fg */
    border: 1px solid #2B2B2B;
}

.nav-tabs .nav-link.active {
    background: #1F1F1F;  /* --tab-active-bg */
    color: #FFFFFF;  /* --tab-active-fg */
    border-top: 2px solid #0078D4;  /* --tab-active-border-top */
}
```

**Buttons:**
```css
.btn-primary {
    background: #0078D4;  /* --button-bg */
    color: #FFFFFF;  /* --button-fg */
    border: 1px solid #FFFFFF12;  /* --button-border */
}

.btn-primary:hover {
    background: #026EC1;  /* --button-hover-bg */
}

.btn-secondary {
    background: #313131;  /* --button-secondary-bg */
    color: #CCCCCC;  /* --button-secondary-fg */
}

.btn-secondary:hover {
    background: #3C3C3C;  /* --button-secondary-hover */
}
```

**Why This Palette:**
- **Official VS Code theme** - Users instantly recognize the familiar colors
- **Comprehensive** - Covers all UI elements (170+ color definitions)
- **Accessibility** - High contrast ratios meet WCAG standards
- **Professional** - Clean, modern dark theme reduces eye strain
- **Consistent** - Single source of truth from Microsoft's repository
- **Proven** - Used by millions of developers daily

**Fonts:**
- **Primary UI Font:** Segoe UI, system-ui, -apple-system, sans-serif
- **Monospace/Code Font:** 'Consolas', 'Monaco', 'Courier New', monospace
- **Font Sizes:** UI text 13-14px, Code 14px, Headers 16-20px

**Reference Implementation:**
See `PdfConversion/Shared/ToastNotification.razor.css` for toast styling with proper contrast.

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

### Algorithm Development Workflow

**CRITICAL: This workflow is MANDATORY for any complex logic development.**

#### Phase 1: Sandbox Development (ALWAYS FIRST)

**Rule 1: Complex Logic = Sandbox First**
If your task involves ANY of these, you MUST start with sandbox:
- Data transformation algorithms
- Parsing or extraction logic
- Normalization or standardization rules
- Business logic calculations
- Pattern matching or filtering
- Hierarchical data processing

**Rule 2: Hardcode Test Data**
```csharp
// CORRECT: Test data hardcoded in endpoint
var testXml = @"<h3>Header1</h3><p>Content</p><h3>Header2</h3>";
var expected = @"<h1>Header1</h1><p>Content</p><h2>Header2</h2>";

// WRONG: Reading from files or parameters (too slow for iteration)
var testXml = File.ReadAllText(Request.Query["file"]);
```

**Rule 3: Iteration Speed is Everything**
- Target: < 5 seconds per iteration cycle
- Edit code → Hot-reload (2s) → curl test (1s) → See results
- If iteration takes > 10 seconds, you're doing it wrong

#### Phase 2: Service Integration

**Rule 4: Port Working Logic Only**
- NEVER port untested logic to services
- Sandbox logic MUST produce correct output first
- Copy the exact working algorithm, then add error handling

**Rule 5: Add Proper Structure**
```csharp
// Sandbox version (minimal)
var result = TransformHeaders(input);
return Results.Text(result);

// Service version (production-ready)
public class HeaderService {
    public Result<string> TransformHeaders(input) {
        try {
            // Same core logic from sandbox
            // Add validation, logging, error handling
        } catch (Exception ex) {
            _logger.LogError(ex, "Transform failed");
            return Result.Failure<string>(ex.Message);
        }
    }
}
```

#### Phase 3: Testing & Validation

**Rule 6: Unit Tests Match Sandbox Tests**
- Every test case from sandbox → unit test
- Add edge cases discovered during development
- Tests should pass immediately (logic already proven)

### Sandbox Pattern Implementation

**Core Principle:** Isolate complex logic from infrastructure concerns.

#### Available Sandbox Modes

The sandbox endpoint (`/sandbox`) supports multiple testing modes via query parameter:

```bash
# Default: LLM comparison
curl http://localhost:8085/sandbox

# Prompt generation mode
curl "http://localhost:8085/sandbox?mode=prompt-gen"

# Test hierarchy XML serialization (example of custom test mode)
curl "http://localhost:8085/sandbox?mode=test-hierarchy"

# Add new modes as needed for testing
curl "http://localhost:8085/sandbox?mode=your-test-mode"
```

**Adding New Test Modes:**
1. Open `PdfConversion/Endpoints/SandboxEndpoint.cs`
2. Add new `else if (mode == "your-test-mode")` in `HandleAsync`
3. Create `HandleYourTestModeAsync` method
4. Hardcode test data in the method
5. Test with direct curl command

#### Structure Rules

**Rule 1: Dedicated Endpoint Files**
```
PdfConversion/
├── Endpoints/
│   ├── SandboxEndpoint.cs      # Primary sandbox (USE THIS FOR ALL TESTS)
│   ├── AlgorithmTestEndpoint.cs # Algorithm-specific (rarely needed)
│   └── ValidationEndpoint.cs    # Validation logic (rarely needed)
```

**Rule 2: Static Methods for Simplicity**
```csharp
public static class SandboxEndpoint {
    public static async Task HandleAsync(
        HttpContext context,
        IServiceA serviceA,  // Inject only what you need
        IServiceB serviceB) {
        // Logic here
    }
}
```

**Rule 3: Registration Pattern**
```csharp
// Program.cs - Keep routing separate from logic
app.MapGet("/sandbox", async (HttpContext ctx, IServiceA a, IServiceB b) =>
    await SandboxEndpoint.HandleAsync(ctx, a, b));
```

#### Development Rules

**Rule 4: Test Data Management**
- Hardcode directly in endpoint for speed
- Use representative real-world samples
- Include edge cases in test data
- Document expected output

**Rule 5: Output Format**
- Return raw results for algorithm testing
- Use text/xml for XML transformations
- Use application/json for data structures
- Include diagnostics in development mode

**Rule 6: Parameter Usage**
- Minimal parameters only (for mode switching)
- Never pass test data via parameters
- Use query params for output format only
```csharp
var format = context.Request.Query["format"].FirstOrDefault() ?? "xml";
```

#### Quality Gates

**Before Moving to Production:**

✅ **Sandbox Checklist**
- [ ] Algorithm produces correct output
- [ ] Edge cases handled properly
- [ ] Performance acceptable (measure with Stopwatch)
- [ ] No hardcoded test data remains
- [ ] Error cases identified

✅ **Integration Checklist**
- [ ] Core logic extracted to service
- [ ] Proper error handling added
- [ ] Logging implemented
- [ ] Unit tests written
- [ ] Integration tested end-to-end

#### Anti-Patterns to Avoid

**❌ DON'T: Skip Sandbox for "Simple" Logic**
- Even "simple" logic benefits from isolation
- Complex edge cases emerge during iteration
- Integration adds complexity

**❌ DON'T: Test with Full Workflow**
- UI interaction is slow
- Multiple components obscure issues
- Debugging is harder

**❌ DON'T: Pass Test Data as Parameters**
- Slows down iteration
- URL encoding issues
- Size limitations

**❌ DON'T: Mix Infrastructure with Logic**
- Keep algorithm pure and testable
- Add infrastructure concerns later
- Maintain separation of concerns

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

| URL | Purpose | Test Checklist |
|-----|---------|----------------|
| `/` | Landing, service status | Nav links • Health indicators (✓ Connected) |
| `/transform` | XSLT transformation with preview | Select project → Load XML → Verify preview & source view |
| `/docling-convert` | PDF→XML conversion (SignalR) | Upload/select PDF → Monitor real-time progress → Check output file |
| `/generate-hierarchy` | Hierarchy tree editor (drag/drop) | Load XML → Drag/drop reorder → Edit labels → Save changes |
| `/convert` | Section generation | Select files → Start conversion → Monitor log → Verify output files |
| `/production` | Batch processing | Multi-project workflows → Error handling |
| `/debug-validation` | Round-trip validation | Select files → Run validation → Review diff viewer |
| `/test-modal` | Color testing page | Render test → Modal dialogs |
| `/transform-test` | API endpoint (headless) | `curl http://localhost:8085/transform-test` |

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

**Last Updated:** 2025-01-27
**Status:** Production-ready, dual-pipeline (Adobe + Docling)