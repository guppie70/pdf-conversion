# Docling + AI Hierarchy Generation Design Document

**Date:** 2025-01-25
**Status:** Approved - Ready for Implementation
**Feature Branch:** `feature/docling-ai-hierarchy`

---

## Executive Summary

This design introduces a new document conversion pipeline that replaces the unreliable Adobe Acrobat XML export with **Docling** (automated PDF/Word to structured XML conversion) and adds **AI-powered hierarchy generation** using local LLMs via Ollama. The goal is to reduce manual conversion time from 8-10 hours to 30-45 minutes per document while maintaining 95%+ accuracy through human validation gates.

**Key Changes:**
- Add Docling service for reliable PDF/Word → XML conversion
- Add AI hierarchy generation using local LLM (Ollama)
- Add drag-drop hierarchy editor for human validation
- Preserve all existing functionality (backward compatible)
- Support both legacy (Adobe XML) and new (Docling) pipelines

**Success Metrics:**
- **Time savings:** 85-90% reduction (8-10 hours → 30-45 minutes)
- **Accuracy:** 95%+ maintained through validation gates
- **Cognitive load:** Eliminate tedious tasks (table recreation, hierarchy debugging)

---

## Problem Statement

### Current Manual Process Challenges

**Challenge #1: Unstable Adobe XML Export**
- Adobe Acrobat Professional produces inconsistent XML structure
- Different exports require custom XSLT modifications
- Headers sometimes exported as `<P>` instead of `<H1-6>`
- Requires manual steering markers (`@data-forceheader`)

**Challenge #2: Asymmetrical Tables**
- Tables lose structure in Adobe XML
- Rows have varying cell counts without colspan/rowspan
- Manual table reconstruction required (2-3 hours per document)

**Challenge #3: Manual Hierarchy Creation Errors**
- Manually recreating hierarchy in Taxxor DM is error-prone
- Typos in item names break header matching in Step 4
- Difficult to debug mismatches
- Takes 1-2 hours per document

**Challenge #4: Sensitive Data**
- Unpublished financial data cannot go to cloud
- Limits AI automation options
- Requires local processing

**Current Time Investment:** 8-10 hours per document × 15 documents/month = 120-150 hours/month

---

## Goals & Success Criteria

### Primary Goal
Automate conversion process to handle 15 documents/month workload while maintaining 95%+ accuracy.

### Success Criteria (Prioritized)

**1. Accuracy & Reliability (NON-NEGOTIABLE)**
- 95%+ conversion accuracy
- Predictable, consistent results
- Human validation at critical checkpoints
- Trust in output is paramount

**2. Time Savings**
- 85-90% reduction in manual work
- 8-10 hours → 30-45 minutes per document
- ROI: Save 100+ hours/month

**3. Reduced Cognitive Load**
- Eliminate tedious table recreation
- Automate hierarchy structure analysis
- Let AI handle repetitive decisions
- Human focuses on edge cases and quality review

### Constraints

**Security:**
- Document content must stay local (sensitive financial data)
- Hierarchy XML examples can use cloud AI (structural patterns only, no sensitive data)
- All LLM processing on local machine via Ollama

**Volume:**
- ~15 conversions per month (3-4 per week)
- Spikes during annual report season

**Compatibility:**
- Preserve existing Blazor/Docker architecture
- Maintain backward compatibility with Adobe pipeline
- Reuse existing services and UI components

---

## Architectural Approach: "Approach 1.5"

### High-Level Architecture

**New Docker Compose Stack:**
```
services:
  pdfconversion (existing - Blazor Server)
  xslt3service (existing - Saxon XSLT engine)
  docling-service (NEW - Python/FastAPI)

host machine:
  ollama (NEW - Local LLM, running natively on M1 MacBook Pro)
```

### Pipeline Comparison

**Legacy Pipeline (Adobe XML):**
```
[Manual] PDF → Adobe Export → input.xml
[Manual] Create hierarchy in Taxxor → hierarchy.xml
[You] Debug XSLT on /transform → normalized.xml
[App] Generate sections on /convert → section XMLs
```

**New Pipeline (Docling + AI):**
```
[App] PDF/Word → Docling → docling-output.xml
[App] XSLT transform → normalized.xml (reliable, minimal debugging)
[App] AI proposes hierarchy → hierarchy.xml (with confidence scores)
[You] Review/adjust via drag-drop editor (quick validation)
[App] Generate sections on /convert → section XMLs (unchanged)
```

### Dual-Pipeline Support

**Both pipelines coexist:**
- Legacy projects (ar24-1, ar24-2, ar24-3) stay on Adobe pipeline
- New projects use Docling pipeline
- User can manually re-process legacy projects to compare quality
- All downstream services (section generation, validation) work with both

---

## Technical Components

### 1. Docling Service (NEW)

**Technology Stack:**
- Python 3.11
- FastAPI (with built-in Swagger UI)
- Docling library for PDF/Word conversion
- Uvicorn server with hot-reload

**Docker Configuration:**
```yaml
docling-service:
  image: python:3.11
  container_name: taxxor-docling-service
  volumes:
    - ./docling-service:/app          # Hot-reload
    - ./data:/app/data                # Shared data
  working_dir: /app
  command: >
    sh -c "pip install -r requirements.txt &&
           uvicorn main:app --host 0.0.0.0 --port 4807 --reload"
  ports:
    - "4807:4807"
  networks:
    - blazornetwork
```

**API Endpoints:**
- `GET /health` - Service health check
- `POST /convert` - Convert PDF/Word to XML
  - Parameters: file (upload), project_id, output_format
  - Formats: docbook (default), html, markdown
  - Returns: output_file path, page_count
- `GET /formats` - List supported formats
- `GET /swagger-ui` - Interactive API documentation

**Output Format:** DocBook XML (chosen for XSLT compatibility)

**Project Structure:**
```
docling-service/
├── main.py                    # FastAPI app
├── requirements.txt           # Dependencies
├── services/
│   └── docling_converter.py  # Conversion logic
└── models/
    └── schemas.py            # Pydantic models
```

**Development Workflow:**
- Edit Python files → auto-reload (like `dotnet watch`)
- Test via Swagger UI at http://localhost:4807/swagger-ui
- Check logs: `docker logs taxxor-docling-service`

---

### 2. Ollama Integration (NEW)

**Deployment:**
- Runs **natively on host machine** (not Docker)
- Installation: `brew install ollama`
- Startup: `ollama serve` (or background service)
- Port: 11434 (default)

**Access from Docker:**
- Use `host.docker.internal:11434` hostname
- Add to docker-compose.yml:
  ```yaml
  pdfconversion:
    extra_hosts:
      - "host.docker.internal:host-gateway"
  ```

**Model Recommendations:**
- **16GB RAM:** Use `llama3.1:8b` or `mistral:latest`
- **32GB+ RAM:** Use `llama3.1:70b` for better accuracy
- User-selectable in UI based on available models

**Configuration:**
```bash
# Keep model loaded for 1 hour during active work
OLLAMA_KEEP_ALIVE=1h ollama serve
```

**Memory Management:**
- Cold start: 30-60 seconds to load model
- Warm: Instant responses
- Auto-unload after inactivity (default: 5 minutes, configurable)

**Preload Strategy:**
- Warm up model on page load (`/generate-hierarchy`)
- Send "Hi" prompt with 1 token generation
- Model ready when user clicks "Generate Hierarchy"

---

### 3. XSLT Organization (UPDATED)

**New Structure:**
```
xslt/
├── adobe/                     # Legacy pipeline
│   ├── transformation.xslt   # Moved from root
│   └── modules/              # Moved from root
└── docling/                  # New pipeline
    ├── transformation.xslt   # New for Docling XML
    └── modules/              # New modules
```

**Path Management:**
- Store selected pipeline in `data/user-selections.json`
- Build full path: `xslt/{pipeline}/transformation.xslt`
- UI shows pipeline indicator: "Using: Adobe XSLT" or "Using: Docling XSLT"

**Docling XSLT Characteristics:**
- Simpler than Adobe XSLT (cleaner input structure)
- Headers already properly tagged (h1-h6)
- Tables well-structured with thead/tbody
- Less need for complex XPath workarounds

---

### 4. Hierarchy Generator Service (NEW)

**Service Interface:**
```csharp
public class HierarchyGeneratorService
{
    public async Task<HierarchyProposal> GenerateHierarchyAsync(
        string normalizedXmlPath,
        List<string> exampleHierarchyPaths,  // User-selected 2-3 examples
        string modelName,                     // e.g., "llama3.1:70b"
        CancellationToken cancellationToken
    );
}

public class HierarchyProposal
{
    public List<HierarchyItem> Items { get; set; }
    public double OverallConfidence { get; set; }      // 0.0 - 1.0
    public List<string> Uncertainties { get; set; }    // AI flags ambiguous decisions
    public string Reasoning { get; set; }              // LLM explains choices
}

public class HierarchyItem
{
    public string ItemId { get; set; }
    public string Title { get; set; }
    public int Level { get; set; }                     // 1, 2, 3, etc.
    public double Confidence { get; set; }             // Per-item confidence
    public string MatchedHeader { get; set; }          // Which h1/h2/h3 from XML
    public List<HierarchyItem> Children { get; set; }
}
```

**Ollama Client:**
```csharp
// POST to http://host.docker.internal:11434/api/generate
var request = new
{
    model = "llama3.1:70b",
    prompt = constructedPrompt,
    stream = false,
    format = "json",               // Request JSON response
    options = new
    {
        temperature = 0.3,         // Low for consistency
        top_p = 0.9,
        num_predict = 4096
    }
};
```

**Timeout Handling:**
- Default timeout: 5 minutes
- Show progress: "Generating hierarchy... (45 seconds)"
- Cancel button available
- Retry logic for invalid JSON responses

---

### 5. Ollama Service (NEW)

**Responsibilities:**
- Check Ollama health
- List available models
- Warm up model (preload)
- Generate hierarchy with LLM
- Handle errors (offline, model missing, timeouts)

**Key Methods:**
```csharp
public class OllamaService
{
    public async Task<bool> CheckHealthAsync();
    public async Task<List<OllamaModel>> GetAvailableModelsAsync();
    public async Task WarmUpModelAsync(string modelName);
    public async Task<string> GenerateAsync(
        string model,
        string prompt,
        CancellationToken ct
    );
}

public class OllamaModel
{
    public string Name { get; set; }      // "llama3.1:70b"
    public long Size { get; set; }        // Bytes
    public DateTime ModifiedAt { get; set; }
}
```

**Error Scenarios:**
- Ollama offline → Show instructions to start
- Model not installed → Offer to install (`ollama pull`)
- Out of memory → Suggest smaller model
- Timeout → Show progress, allow cancel
- Invalid JSON → Auto-retry with stricter prompt

---

### 6. Prompt Engineering Strategy

**Prompt Structure (4 parts):**

**PART 1: System Instructions & Rules**
```
You are an expert at analyzing financial annual reports and creating hierarchical structures.

HIERARCHY DECISION CRITERIA:
1. SECTION NUMBERING (highest priority)
2. HEADER STYLING (h1 > h2 > h3 > h4 > h5 > h6)
3. INTER-DOCUMENT LINKS ("see chapter X", "refer to note Y")
4. STANDARD REPORT STRUCTURE (Intro, Marketing, Financial, ESG, Outro)
5. SIMPLICITY PRINCIPLE (minimize sections)

[Full rules from brainstorm-input.md lines 52-76]
```

**PART 2: Example Hierarchies** (2-3 user-selected)
```xml
EXAMPLE 1 (from project ar23-1):
<hierarchy>
  <item id="intro" level="1" title="Introduction">
    ...
  </item>
</hierarchy>

EXAMPLE 2 (from project ar23-2):
[Full hierarchy XML]
```

**PART 3: Current Document** (full Normalized XML)
```xml
CURRENT DOCUMENT TO ANALYZE:
<html xmlns="http://www.w3.org/1999/xhtml">
  <body>
    <h1>Annual Report 2024</h1>
    [Full normalized XML - may be 500KB-2MB]
  </body>
</html>
```

**PART 4: Task & Output Format**
```json
OUTPUT FORMAT (JSON):
{
  "proposedHierarchy": {
    "items": [...]
  },
  "overallConfidence": 0.87,
  "uncertainties": ["..."],
  "reasoning": "..."
}
```

**Prompt Assembly:**
```csharp
private string BuildPrompt(
    string normalizedXmlPath,
    List<string> examplePaths)
{
    var sb = new StringBuilder();
    sb.AppendLine(PART1_SYSTEM_INSTRUCTIONS);
    sb.AppendLine();

    // Add user-selected examples
    foreach (var examplePath in examplePaths)
    {
        var exampleXml = File.ReadAllText(examplePath);
        sb.AppendLine($"EXAMPLE ({Path.GetFileName(examplePath)}):");
        sb.AppendLine(exampleXml);
        sb.AppendLine();
    }

    // Add current document
    var normalizedXml = File.ReadAllText(normalizedXmlPath);
    sb.AppendLine("CURRENT DOCUMENT TO ANALYZE:");
    sb.AppendLine(normalizedXml);
    sb.AppendLine();

    sb.AppendLine(PART4_TASK_AND_FORMAT);

    return sb.ToString();
}
```

---

## User Interface Changes

### New Pages

#### 1. `/docling-convert` (NEW)

**Purpose:** Upload PDF/Word, convert to Docling XML

**Layout:**
```
┌─────────────────────────────────────────┐
│ Docling Convert - Step 1                │
├─────────────────────────────────────────┤
│ Project: [ar24-3 ▼]                     │
│                                          │
│ ┌─ Upload Document ──────────────────┐  │
│ │  Drag & drop PDF/Word file here    │  │
│ │  or click to browse                 │  │
│ │  [📄 annual-report-2024.pdf]        │  │
│ └─────────────────────────────────────┘  │
│                                          │
│ Output Format: [DocBook XML ▼]          │
│                                          │
│ [Convert with Docling]                   │
│                                          │
│ ┌─ Conversion Output ─────────────────┐  │
│ │ Monaco Editor (XML view)            │  │
│ │ Shows docling-output.xml            │  │
│ └─────────────────────────────────────┘  │
│                                          │
│ [Save to Project] [Next: Transform →]   │
└─────────────────────────────────────────┘
```

**Features:**
- File upload (drag-drop or browse)
- Progress indicator during conversion (30-60 sec)
- Monaco editor preview (consistent with /transform)
- Save button → stores as `docling-output.xml`
- "Next" button → navigates to /transform

#### 2. `/transform` (UPDATED)

**Changes:**
- Add "Source Type" selector: "Adobe XML" | "Docling XML"
- Add "Pipeline" selector: "adobe" | "docling"
- XSLT file selector filtered by pipeline
- Otherwise identical to current implementation

**Layout:**
```
┌─────────────────────────────────────────┐
│ Transform - Step 2                       │
├─────────────────────────────────────────┤
│ Project: [ar24-3 ▼]                     │
│ Source Type: [Docling XML ▼]            │
│ Source File: [docling-output.xml ▼]     │
│ XSLT: [docling/transformation.xslt ▼]   │
│                                          │
│ [Transform] [Save Normalized XML]       │
│                                          │
│ [Preview pane - existing component]     │
│                                          │
│ [← Back] [Next: Generate Hierarchy →]   │
└─────────────────────────────────────────┘
```

#### 3. `/generate-hierarchy` (NEW)

**Purpose:** AI proposes hierarchy, user validates/adjusts

**Layout:**
```
┌──────────────────────────────────────────────────────────────┐
│ Generate Hierarchy - Step 3                                  │
├──────────────────────────────────────────────────────────────┤
│ ┌─ Settings ────────────────────────────────────────────┐    │
│ │ Model: [llama3.1:70b ▼] 🔄  Status: ✓ Model ready   │    │
│ │ Examples: ☑ ar23-1  ☑ ar23-2  ☐ ar22-5               │    │
│ │ Temperature: [0.3 ──●─── 1.0]                        │    │
│ │ [Generate with AI] [Load Existing] [Save Hierarchy]  │    │
│ └───────────────────────────────────────────────────────┘    │
├──────────────────────────────────────────────────────────────┤
│ ┌─ Proposed Hierarchy ──┐ ┌─ Available Headers ──────────┐  │
│ │ (editable tree)        │ │ (drag sources)                │  │
│ │                        │ │                                │  │
│ │ 📄 Introduction  [95%] │ │ 🔍 Search: [__________]       │  │
│ │   ├─ Cover       [98%] │ │ Filter: [All ▼] [h1 ▼]       │  │
│ │   └─ TOC         [90%] │ │                                │  │
│ │ 📄 Directors     [87%] │ │ Available Headers (12):       │  │
│ │   ├─ Governance  [92%] │ │ [h1] Annual Report 2024       │  │
│ │   └─ Risk Mgmt   [65%]⚠│ │ [h2] Table of Contents        │  │
│ │ 📄 Financial     [78%]⚠│ │ [h2] Directors' Report        │  │
│ │   └─ Note 1      [88%] │ │ [h3] Corporate Governance     │  │
│ │                        │ │ [h3] Risk Management          │  │
│ │ [+ Add Section]        │ │ [h2] Financial Statements     │  │
│ └────────────────────────┘ └───────────────────────────────┘  │
├──────────────────────────────────────────────────────────────┤
│ ┌─ AI Reasoning ────────────────────────────────────────┐    │
│ │ Overall Confidence: 87%                                │    │
│ │ ✓ Strong matches (10 items): Used section numbering   │    │
│ │ ⚠️ Uncertainties (2 items):                            │    │
│ │   • "Risk Mgmt" - Could be level 1 OR level 2         │    │
│ │   • "Financial" - Many sub-sections, review needed    │    │
│ └────────────────────────────────────────────────────────┘    │
│ [← Back: Transform] [Next: Convert Sections →]              │
└──────────────────────────────────────────────────────────────┘
```

**Features:**

**Settings Panel:**
- Model selector (dropdown of available Ollama models)
- Example hierarchies (multi-select, 2-3 recommended)
- Temperature slider (0.1-1.0, default 0.3)
- Generate/Load/Save buttons

**Left Panel: Hierarchy Tree View**
- Visual hierarchy with indentation
- Confidence badges (percentage + color coding)
- Warning indicators (⚠️) for low confidence (<70%)
- Drag-drop to reorder items
- Right-click menu: Add child, Delete, Indent/Outdent
- Inline editing: Click to rename

**Right Panel: Available Headers**
- All headers from Normalized XML (h1-h6)
- Search/filter functionality
- Header level badges
- Drag to left panel to add to hierarchy
- Grayed out if already used
- Context preview on hover

**Drag-Drop Interactions:**
- Drag header from right → drop on item → becomes child
- Drag item within tree → reorder at same level
- Drag item onto another → becomes child (indented)
- Keyboard shortcuts: Tab (indent), Shift+Tab (outdent)

**Reasoning Panel:**
- Overall confidence score
- List of strong matches
- List of uncertainties with explanations
- AI reasoning summary

**Validation:**
- Check all items have titles
- Check unique IDs
- Warn about low confidence items
- Warn about unused headers

#### 4. `/convert` (EXISTING - No Changes)

**Compatibility:**
- Works with Adobe-generated Normalized XML
- Works with Docling-generated Normalized XML
- Works with manual hierarchies
- Works with AI-generated hierarchies
- No code changes required

#### 5. Home Page Status Section (UPDATED)

**Add Service Status Indicators:**
```
System Status
┌─────────────────────────────────────────┐
│ 🌐 Blazor App           ✓ Connected     │
│ ⚙️  XSLT3 Service       ✓ Connected :4806│
│ 📄 Docling Service      ✓ Connected :4807│
│ 🤖 Ollama (Local LLM)   ✓ Connected :11434│
│                         Model: llama3.1:70b│
└─────────────────────────────────────────┘
```

**Health Checks:**
- Docling: `GET http://localhost:4807/health`
- Ollama: `GET http://localhost:11434/api/tags`
- Show currently loaded model if any

### Updated Navigation Menu

**New Order (follows workflow):**
```
Home
1. Docling Convert
2. Transform
3. Generate Hierarchy
4. Convert
Debug Validation
Production
```

---

## Complete User Workflow

### End-to-End Process

**STEP 1: Upload & Convert with Docling** (`/docling-convert`)
1. Select project: "ar24-3"
2. Upload "annual-report-2024.pdf" (drag-drop)
3. Click "Convert with Docling"
4. Wait 30-60 seconds (progress indicator)
5. Preview DocBook XML in Monaco editor
6. Click "Save to Project"
   - Saves as `data/input/optiver/projects/ar24-3/docling-output.xml`
7. Click "Next: Transform →"

**STEP 2: Transform to Normalized XML** (`/transform`)
1. Auto-loaded project: "ar24-3"
2. Source type: "Docling XML" (auto-selected)
3. Source file: "docling-output.xml" (auto-selected)
4. XSLT: "docling/transformation.xslt" (auto-selected)
5. Click "Transform"
6. Preview Normalized XML (rendered + source views)
7. Verify headers, tables, lists look correct
8. Click "Save Normalized XML"
   - Saves as `data/output/optiver/projects/ar24-3/normalized.xml`
9. Click "Next: Generate Hierarchy →"

**STEP 3: Generate Hierarchy with AI** (`/generate-hierarchy`)
1. Page loads, model warms up in background (30 sec)
2. Status changes: "Checking..." → "✓ Model ready"
3. Select example hierarchies: ☑ ar23-1, ☑ ar23-2
4. Click "Generate Hierarchy with AI"
5. Wait 10-30 seconds (progress: "Generating... 15 seconds")
6. Review proposed hierarchy (left panel):
   - Check confidence scores
   - Review uncertain items (⚠️)
   - Read AI reasoning
7. Adjust if needed:
   - Drag-drop to reorder
   - Add missing headers from right panel
   - Rename items via inline editing
   - Delete incorrect items
8. Click "Save Hierarchy"
   - Saves as `data/output/optiver/projects/ar24-3/hierarchy.xml`
9. Click "Next: Convert Sections →"

**STEP 4: Generate Section XMLs** (`/convert` - existing)
1. Auto-loaded project: "ar24-3"
2. Source: "normalized.xml"
3. Hierarchy: "hierarchy.xml"
4. Click "Start Conversion"
5. Watch progress bar
6. Review logs
7. Sections generated:
   - `data/output/optiver/projects/ar24-3/sections/*.xml`

**DONE!** ✅ Ready to import into Taxxor DM

### Time Comparison

| Task | Current Manual | With Docling+AI | Savings |
|------|----------------|-----------------|---------|
| Export PDF to XML | 5 min | **1 min** (automated) | 4 min |
| Debug XSLT | 2-4 hours | **10 min** (Docling reliable) | ~3.5 hours |
| Create hierarchy | 1-2 hours | **5 min** (AI + validation) | ~1.5 hours |
| Hierarchy fixes | 30-60 min | **Included** (drag-drop) | 45 min |
| Copy/paste sections | 2-3 hours | **2 min** (automated) | ~2.5 hours |
| **TOTAL** | **8-10 hours** | **~30-45 minutes** | **~85-90%** |

---

## Implementation Phases

### Phase 1: Docling Pipeline (Weeks 1-2)

**Goals:**
- Set up Docling service with hot-reload
- Create `/docling-convert` page
- Build `docling/transformation.xslt`
- Validate Docling output quality

**Tasks:**

**1.1: Docling Docker Service Setup**
- [ ] Create `docling-service/` directory
- [ ] Write `requirements.txt` with dependencies
- [ ] Write `main.py` with FastAPI skeleton
- [ ] Add to `docker-compose.yml`
- [ ] Test hot-reload: edit Python → auto-restart
- [ ] Verify Swagger UI at `:4807/swagger-ui`

**1.2: Docling Conversion API**
- [ ] Implement `DoclingConverter` class
- [ ] Add `/convert` endpoint with file upload
- [ ] Support DocBook XML output format
- [ ] Add error handling (invalid files, timeouts)
- [ ] Test with sample PDFs (small, medium, large)
- [ ] Verify output saved to correct project path

**1.3: `/docling-convert` Page**
- [ ] Create new Razor page
- [ ] Add project selector (reuse component)
- [ ] Add file upload component (drag-drop)
- [ ] Add "Convert" button with progress indicator
- [ ] Integrate Monaco editor for preview
- [ ] Add "Save to Project" functionality
- [ ] Add navigation: "Next: Transform →"

**1.4: XSLT Reorganization**
- [ ] Create `xslt/adobe/` and `xslt/docling/` folders
- [ ] Move existing XSLT to `xslt/adobe/`
- [ ] Update all file paths in code
- [ ] Add pipeline selector to `data/user-selections.json`
- [ ] Update XSLT file loading logic

**1.5: Docling XSLT Transformation**
- [ ] Create `xslt/docling/transformation.xslt`
- [ ] Implement header mappings (h1-h6)
- [ ] Implement table transformations
- [ ] Implement list transformations
- [ ] Test with ar24-3 Docling output
- [ ] Compare quality: Docling vs. Adobe

**1.6: Integration Testing**
- [ ] End-to-end: PDF → Docling → XSLT → Normalized XML
- [ ] Verify table preservation
- [ ] Verify header consistency
- [ ] Test with all 3 existing projects
- [ ] Document quality improvements

**Acceptance Criteria:**
- Docling service runs reliably in Docker
- Hot-reload works for Python code
- PDF → Normalized XML pipeline produces valid output
- Output quality equal or better than Adobe pipeline
- Swagger documentation available

---

### Phase 2: AI Hierarchy Generation (Weeks 3-4)

**Goals:**
- Integrate Ollama for local LLM
- Implement hierarchy generation service
- Build prompt engineering logic
- Add model selection and preloading

**Tasks:**

**2.1: Ollama Service Integration**
- [ ] Install Ollama on host machine
- [ ] Pull initial model: `ollama pull llama3.1:70b`
- [ ] Configure `OLLAMA_KEEP_ALIVE=1h`
- [ ] Add `host.docker.internal` to docker-compose
- [ ] Create `OllamaService.cs`
- [ ] Implement `CheckHealthAsync()`
- [ ] Implement `GetAvailableModelsAsync()`
- [ ] Implement `WarmUpModelAsync()`
- [ ] Implement `GenerateAsync()` with timeout
- [ ] Test connectivity from Docker container

**2.2: Hierarchy Generator Service**
- [ ] Create `HierarchyGeneratorService.cs`
- [ ] Define `HierarchyProposal` model
- [ ] Implement prompt building logic
- [ ] Load hierarchy decision rules (from brainstorm-input)
- [ ] Load example hierarchies (user-selected)
- [ ] Load normalized XML
- [ ] Assemble 4-part prompt
- [ ] Call Ollama API
- [ ] Parse JSON response
- [ ] Calculate confidence scores
- [ ] Handle errors (timeout, invalid JSON, low confidence)

**2.3: Prompt Engineering**
- [ ] Extract hierarchy rules to configuration file
- [ ] Create prompt template
- [ ] Test with 2-3 example hierarchies
- [ ] Validate JSON output structure
- [ ] Tune temperature for consistency
- [ ] Add retry logic for invalid responses
- [ ] Test with ar24-3 normalized XML
- [ ] Measure accuracy vs. manual hierarchy

**2.4: Model Configuration**
- [ ] Add model selection to `data/user-selections.json`
- [ ] Create model selector UI component
- [ ] Add refresh button to reload available models
- [ ] Display model size and info
- [ ] Add temperature slider (0.1-1.0)
- [ ] Save user preferences

**2.5: Page Skeleton**
- [ ] Create `/generate-hierarchy` page
- [ ] Add settings panel (model, examples, temp)
- [ ] Add "Generate with AI" button
- [ ] Add progress indicator with elapsed time
- [ ] Add cancel button
- [ ] Display generated hierarchy (basic table view for now)
- [ ] Add reasoning panel
- [ ] Add "Save Hierarchy" button

**2.6: Integration Testing**
- [ ] Test with real Ollama (mark as `[Explicit]`)
- [ ] Test with multiple example hierarchies
- [ ] Test confidence scoring
- [ ] Test error handling (offline, timeout, invalid)
- [ ] Measure generation time (should be <60 sec)
- [ ] Validate output hierarchy XML format

**Acceptance Criteria:**
- Ollama accessible from Blazor container
- Model preloading works (warm start)
- Hierarchy generation completes in <60 seconds
- Output includes confidence scores and reasoning
- JSON parsing handles edge cases gracefully
- Error messages are clear and actionable

---

### Phase 3: Drag-Drop Hierarchy Editor (Weeks 5-6)

**Goals:**
- Build interactive tree view component
- Implement drag-drop functionality
- Add header panel with search/filter
- Complete validation logic

**Tasks:**

**3.1: Hierarchy Tree View Component**
- [ ] Create `HierarchyTreeView.razor` component
- [ ] Render items with indentation
- [ ] Display confidence badges with color coding
- [ ] Add warning indicators (⚠️) for low confidence
- [ ] Implement expand/collapse for children
- [ ] Add item selection (highlight selected item)
- [ ] Style with scoped CSS

**3.2: Inline Editing**
- [ ] Click item title to edit
- [ ] Show input field with current value
- [ ] Save on Enter, cancel on Escape
- [ ] Validate non-empty titles
- [ ] Update item in hierarchy object
- [ ] Highlight changed items

**3.3: Drag-Drop Interactions**
- [ ] Add JavaScript interop file: `hierarchy-editor.js`
- [ ] Implement HTML5 Drag & Drop API
- [ ] Drag item to reorder at same level
- [ ] Drag item onto another to make child
- [ ] Visual feedback during drag (drop zones)
- [ ] Update hierarchy object after drop
- [ ] Callback to Blazor: `DotNet.invokeMethodAsync()`

**3.4: Context Menu**
- [ ] Right-click to show context menu
- [ ] Options: Add child, Delete, Indent, Outdent
- [ ] Keyboard shortcuts: Tab, Shift+Tab, Del
- [ ] Implement each action
- [ ] Update UI immediately

**3.5: Available Headers Panel**
- [ ] Create `AvailableHeadersPanel.razor`
- [ ] Extract headers from Normalized XML
- [ ] Display with level badges ([h1], [h2], etc.)
- [ ] Add search box (filter by title)
- [ ] Add level filter dropdown
- [ ] Gray out headers already in hierarchy
- [ ] Enable drag from panel to tree
- [ ] Show context preview on hover

**3.6: Validation Logic**
- [ ] Check all items have non-empty titles
- [ ] Check unique IDs (auto-generate if needed)
- [ ] Check no orphaned items
- [ ] Warn about low-confidence items (<70%)
- [ ] Warn about unused headers
- [ ] Show validation summary before save

**3.7: Reasoning Panel**
- [ ] Display overall confidence with gauge
- [ ] List strong matches (>80% confidence)
- [ ] List uncertainties with explanations
- [ ] Show AI reasoning text
- [ ] Collapsible sections

**3.8: Save & Load**
- [ ] Serialize hierarchy to XML format
- [ ] Validate XML schema
- [ ] Save to `data/output/.../hierarchy.xml`
- [ ] Load existing hierarchy (if editing)
- [ ] Compare manual vs. AI-generated

**3.9: Integration Testing**
- [ ] Test all drag-drop scenarios
- [ ] Test inline editing
- [ ] Test context menu actions
- [ ] Test search/filter
- [ ] Test validation logic
- [ ] Test save/load round-trip

**Acceptance Criteria:**
- Tree view displays hierarchy correctly
- Drag-drop feels smooth and intuitive
- All editing actions work reliably
- Validation catches common errors
- Saved hierarchy XML is valid
- UI is responsive and polished

---

### Phase 4: Testing & Polish (Week 7)

**Goals:**
- Complete unit and integration tests
- Add E2E tests for new workflows
- Fix bugs and edge cases
- Polish UI/UX

**Tasks:**

**4.1: Unit Tests**
- [ ] Docling service tests (5 tests)
- [ ] HierarchyGeneratorService tests (10 tests)
- [ ] OllamaService tests (3 tests)
- [ ] XSLT transformation tests (5 tests)
- [ ] UI component tests (7 tests)
- [ ] Target: 30 unit tests, >80% coverage

**4.2: Integration Tests**
- [ ] Docling pipeline tests (3 tests)
- [ ] Hierarchy generation tests (3 tests)
- [ ] UI workflow tests (3 tests)
- [ ] Cross-service tests (3 tests)
- [ ] Target: 15 integration tests

**4.3: E2E Tests (Playwright)**
- [ ] `UserCanConvertPdfWithDocling`
- [ ] `UserCanGenerateHierarchyWithAI`
- [ ] `UserCanCompleteFullDoclingWorkflow`
- [ ] Target: 3 E2E tests, all passing

**4.4: Error Handling Review**
- [ ] Test all error scenarios (see Section 9)
- [ ] Verify error messages are clear
- [ ] Test recovery actions
- [ ] Add missing error handlers

**4.5: UI/UX Polish**
- [ ] Consistent styling across new pages
- [ ] Smooth transitions and animations
- [ ] Loading states and spinners
- [ ] Responsive layout (if needed)
- [ ] Accessibility (keyboard navigation, ARIA labels)

**4.6: Documentation**
- [ ] Update CLAUDE.md with new workflow
- [ ] Add XSLT comments for Docling pipeline
- [ ] Document prompt engineering approach
- [ ] Add troubleshooting guide

**4.7: Performance Testing**
- [ ] Test with large PDFs (>200 pages)
- [ ] Test with complex hierarchies (>50 items)
- [ ] Measure LLM generation time
- [ ] Optimize if needed

**Acceptance Criteria:**
- All tests passing (unit, integration, E2E)
- No critical bugs remaining
- UI feels polished and professional
- Documentation up to date
- Ready for production use

---

## Testing Strategy

### Test Pyramid

```
           /\
          /E2E\         3 new tests
         /────\
        / Integ \       15 new tests
       /────────\
      /  Unit    \      30 new tests
     /────────────\
```

### Unit Tests (30 tests)

**Docling Service (5 tests):**
- `test_convert_pdf_to_docbook()` - Valid PDF conversion
- `test_convert_docx_to_docbook()` - Valid Word conversion
- `test_invalid_file_type()` - Reject unsupported formats
- `test_output_file_created()` - Verify file generation
- `test_page_count_extraction()` - Verify metadata

**HierarchyGeneratorService (10 tests):**
- `GenerateHierarchy_WithValidXml_ReturnsProposal()`
- `GenerateHierarchy_WithExamples_IncludesInPrompt()`
- `GenerateHierarchy_ParsesLlmResponse_ToHierarchyObject()`
- `GenerateHierarchy_CalculatesConfidence_Correctly()`
- `GenerateHierarchy_HandlesLlmTimeout_Gracefully()`
- `GenerateHierarchy_RetriesOnInvalidJson()`
- `GenerateHierarchy_FlagsLowConfidence()`
- `GenerateHierarchy_ExtractsUncertainties()`
- `GenerateHierarchy_GeneratesUniqueIds()`
- `GenerateHierarchy_ValidatesOutputStructure()`

**OllamaService (3 tests):**
- `GetAvailableModels_ReturnsModelList()`
- `WarmUpModel_LoadsModel_Successfully()`
- `WarmUpModel_HandlesOffline_Gracefully()`

**XSLT Transformation (5 tests):**
- `DoclingTransform_PreservesHeaders_Correctly()`
- `DoclingTransform_PreservesTables_WithStructure()`
- `DoclingTransform_PreservesLists_WithNesting()`
- `DoclingTransform_HandlesEmptyDocument()`
- `DoclingTransform_OutputValidatesAgainstSchema()`

**UI Components (7 tests):**
- `HierarchyTreeView_RendersItems_Correctly()`
- `HierarchyTreeView_DragDrop_ReordersItems()`
- `HierarchyTreeView_ShowsConfidence_Badges()`
- `HierarchyTreeView_InlineEdit_UpdatesTitle()`
- `AvailableHeadersPanel_FiltersHeaders_BySearch()`
- `AvailableHeadersPanel_ShowsUsedHeaders_AsDisabled()`
- `AvailableHeadersPanel_DragStart_SetsDataTransfer()`

### Integration Tests (15 tests)

**Pipeline Tests (3 tests):**
- `DoclingPipeline_PdfToNormalizedXml_Succeeds()`
- `DoclingPipeline_PreservesTables_BetterThanAdobe()`
- `DoclingPipeline_ProducesCompatibleOutput()`

**Hierarchy Generation (3 tests):**
- `HierarchyGen_WithRealLlm_GeneratesValidStructure()` [Explicit]
- `HierarchyGen_WithExamples_ImprovesAccuracy()`
- `HierarchyGen_ConfidenceScores_ReflectQuality()`

**UI Workflow (3 tests):**
- `DoclingConvertPage_UploadAndConvert_ShowsPreview()`
- `GenerateHierarchyPage_LoadsModel_ShowsReadyState()`
- `GenerateHierarchyPage_DragDrop_UpdatesHierarchy()`

**Cross-Service (3 tests):**
- `TransformPage_WithDoclingXml_UsesCorrectXslt()`
- `ConvertPage_WithAiHierarchy_GeneratesSections()`
- `ProjectMetadata_TracksPlaceholder_Correctly()`

**Error Handling (3 tests):**
- `DoclingService_HandlesCorruptPdf_Gracefully()`
- `OllamaService_HandlesTimeout_Gracefully()`
- `HierarchyGen_HandlesInvalidJson_Retries()`

### E2E Tests (3 tests)

**Playwright Tests:**

```csharp
[Test]
public async Task UserCanConvertPdfWithDocling()
{
    await Page.GotoAsync("http://localhost:8085/docling-convert");
    await Page.SelectOptionAsync("select", "ar24-3");
    await Page.SetInputFilesAsync("input[type=file]", "test-document.pdf");
    await Page.ClickAsync("button:has-text('Convert with Docling')");
    await Page.WaitForSelectorAsync(".monaco-editor", new() { Timeout = 60000 });
    await Expect(Page.Locator("button:has-text('Save to Project')")).ToBeEnabledAsync();
}

[Test]
public async Task UserCanGenerateHierarchyWithAI()
{
    await Page.GotoAsync("http://localhost:8085/generate-hierarchy");
    await Page.WaitForSelectorAsync("text=✓ Model ready", new() { Timeout = 60000 });
    await Page.CheckAsync("input[value='ar23-1']");
    await Page.CheckAsync("input[value='ar23-2']");
    await Page.ClickAsync("button:has-text('Generate Hierarchy with AI')");
    await Page.WaitForSelectorAsync(".hierarchy-tree", new() { Timeout = 60000 });
    await Expect(Page.Locator(".confidence-badge")).ToHaveCountAsync(GreaterThan(0));
}

[Test]
public async Task UserCanCompleteFullDoclingWorkflow()
{
    // Step 1: Docling convert
    await Page.GotoAsync("http://localhost:8085/docling-convert");
    // ... upload and convert ...

    // Step 2: Transform
    await Page.ClickAsync("text=Next: Transform");
    // ... transform ...

    // Step 3: Generate hierarchy
    await Page.ClickAsync("text=Next: Generate Hierarchy");
    // ... generate and adjust ...

    // Step 4: Convert sections
    await Page.ClickAsync("text=Next: Convert Sections");
    // ... convert ...

    // Verify all outputs exist
    await Expect(Page.Locator("text=Conversion complete")).ToBeVisibleAsync();
}
```

### Test Data

**Small test document:**
- Location: `data/input/_test-small.pdf`
- Size: 3 pages, 5 headers, 1 table, 1 list
- Purpose: Fast unit/integration tests (<5 sec)

**Realistic test document:**
- Location: `data/input/optiver/projects/ar24-3/`
- Purpose: E2E validation with real complexity

### LLM Testing Strategy

**Option A: Mock LLM responses (fast, deterministic)**
```csharp
_mockOllamaService.Setup(x => x.GenerateAsync(...))
    .ReturnsAsync(mockHierarchyProposal);
```

**Option B: Real LLM integration test (slow, realistic)**
```csharp
[Test]
[Category("Integration")]
[Explicit] // Don't run in CI, run manually
public async Task HierarchyGen_WithRealLlm_Succeeds()
{
    var result = await _hierarchyGen.GenerateAsync(...);
    Assert.That(result.OverallConfidence, Is.GreaterThan(0.7));
}
```

### Running Tests

```bash
# Fast tests only (unit + mocked integration)
npm test -- --filter "Category!=Integration"

# All tests including real LLM (slow)
npm test

# E2E only
npm run test:e2e

# Watch mode during development
npm test -- --watch
```

---

## Error Handling & Edge Cases

### Error Categories

1. **External Service Failures** (Docling, Ollama)
2. **File Processing Errors** (corrupt PDFs, unsupported formats)
3. **XSLT Transformation Failures** (malformed XML)
4. **LLM Generation Issues** (timeouts, hallucinations, low confidence)
5. **User Input Validation** (missing files, invalid hierarchies)
6. **Resource Constraints** (memory, disk space, model size)

### Key Error Scenarios & Handling

**Docling Service Offline:**
```
User Experience: "⚠️ Docling service is offline. Please check Docker."
Recovery: [Retry] button, [Use Adobe Pipeline] fallback button
```

**Ollama Offline:**
```
User Experience: "🔴 LLM service offline"
Recovery: Instructions to run "ollama serve", link to docs
```

**Model Not Installed:**
```
User Experience: "Model 'llama3.1:70b' not found"
Recovery: [Install Now] button, manual command: "ollama pull llama3.1:70b"
```

**LLM Timeout:**
```
User Experience: Progress spinner + "Still processing... (45 seconds)"
Recovery: [Cancel] button, extend timeout, try smaller model
```

**Low Confidence (<50%):**
```
User Experience: "⚠️ AI is uncertain. Manual review strongly recommended."
Recovery: Show uncertainties in detail, enable manual mode
```

**Invalid JSON Response:**
```
User Experience: "AI response was invalid, retrying..."
Recovery: Auto-retry once with stricter prompt, fallback to manual
```

**Corrupt PDF:**
```
User Experience: "PDF appears corrupted: [error details]"
Recovery: Upload different file, try Adobe pipeline
```

**Out of Memory (Model Too Large):**
```
User Experience: "Insufficient memory for model (requires 42GB)"
Recovery: Suggest smaller model, show available RAM
```

### Global Error Boundary

```razor
<ErrorBoundary>
    <ChildContent>@Body</ChildContent>
    <ErrorContent Context="exception">
        <div class="error-boundary">
            <h2>⚠️ Something went wrong</h2>
            <p>@exception.Message</p>
            <details><summary>Technical details</summary>
                <pre>@exception.StackTrace</pre>
            </details>
            <button @onclick="RecoverAsync">Try Again</button>
            <button @onclick="ReportIssueAsync">Report Issue</button>
        </div>
    </ErrorContent>
</ErrorBoundary>
```

### Logging Strategy

All services log errors with rich context:
```csharp
_logger.LogError(ex,
    "Hierarchy generation failed for project {ProjectId}, model {Model}, confidence {Confidence}",
    projectId, selectedModel, proposal?.OverallConfidence);
```

---

## Migration Strategy

### Dual-Pipeline Support

**Both pipelines coexist permanently:**
- Legacy (Adobe XML) pipeline stays available
- New (Docling) pipeline added alongside
- User chooses per project
- All downstream services work with both

### Project Metadata Tracking

```json
// data/input/optiver/projects/ar24-3/metadata/project-metadata.json
{
  "projectId": "ar24-3",
  "projectName": "Optiver Annual Report 2024",
  "pipeline": "docling",  // "adobe" or "docling"
  "pipelineHistory": [
    {
      "pipeline": "adobe",
      "date": "2025-01-15T10:30:00Z",
      "xslt": "adobe/transformation.xslt",
      "success": true
    },
    {
      "pipeline": "docling",
      "date": "2025-01-25T14:20:00Z",
      "xslt": "docling/transformation.xslt",
      "success": true,
      "notes": "Better table structure, more reliable headers"
    }
  ],
  "hierarchyGeneration": {
    "method": "ai",
    "llmModel": "llama3.1:70b",
    "confidence": 0.87,
    "examplesUsed": ["ar23-1", "ar23-2"],
    "date": "2025-01-25T14:45:00Z"
  }
}
```

### Migration Options

**Option 1: Keep Legacy Unchanged (RECOMMENDED)**
- Existing projects (ar24-1, ar24-2, ar24-3) stay on Adobe pipeline
- Already working, already in Taxxor DM
- No risk of breaking existing conversions
- New projects (ar25-1+) use Docling pipeline
- **Lowest risk, allows gradual adoption**

**Option 2: Re-process for Comparison**
- Process ar24-3 through both pipelines
- Compare quality side-by-side
- Choose best output manually
- Good for validating Docling quality
- **Recommended for validation phase**

**Option 3: Hybrid Validation**
- Use Docling for new projects
- Keep Adobe as fallback
- Build confidence over time
- Migrate fully once proven
- **Best long-term strategy**

### UI Indicators

```razor
<select @bind="selectedProject">
    <option value="ar24-1">ar24-1 - Optiver 2024 [Adobe]</option>
    <option value="ar24-2">ar24-2 - TomTom 2024 [Adobe]</option>
    <option value="ar24-3">ar24-3 - Shell 2024 [Adobe]</option>
    <option value="ar25-1">ar25-1 - Optiver 2025 [Docling] ✨</option>
</select>
```

### Backward Compatibility

**Critical:** Normalized XML schema stays identical
- Adobe pipeline → Normalized XML
- Docling pipeline → Normalized XML (same schema!)
- Section generation service doesn't care about origin
- Validation tools work with both
- No breaking changes to existing code

---

## Future Enhancements

### Phase 5: RAG System (Future)

**When to Add:**
- After 20-30 successful conversions with current system
- When pattern library is large enough (30+ hierarchies)
- When simple prompt-based approach shows limitations

**What Changes:**
- Add vector database (Chroma, FAISS) to Docker stack
- Pre-process all example hierarchies into embeddings
- Query RAG for most relevant 3-5 examples (vs. user-selected 2-3)
- Hierarchy generation quality improves automatically

**What Stays Same:**
- UI remains identical (user doesn't see difference)
- `/generate-hierarchy` page unchanged
- Drag-drop editor unchanged
- Just better AI suggestions under the hood

**Migration Path:**
```csharp
// Phase 1-4 (current):
var examples = userSelectedExamples; // ["ar23-1", "ar23-2"]

// Phase 5 (future with RAG):
var examples = await ragService.FindSimilar(normalizedXml, topK: 5);
```

### Other Potential Enhancements

**Batch Processing:**
- Upload multiple PDFs at once
- Queue processing in background
- Email notification when complete
- Progress dashboard

**Cloud AI Option (for non-sensitive projects):**
- Toggle: "Use cloud AI for this project"
- Use Claude/GPT-4 instead of local LLM
- Better accuracy for complex documents
- User explicitly opts in per project

**Visual Diff Tool:**
- Compare Adobe vs. Docling output side-by-side
- Highlight differences
- Choose per-section which is better
- Merge best of both pipelines

**LLM Fine-Tuning:**
- Train custom model on financial report hierarchies
- Specialized vocabulary and patterns
- Potentially higher accuracy
- Requires significant dataset (100+ examples)

---

## Appendices

### Appendix A: Technology Stack Summary

**Existing Components:**
- .NET 9 Blazor Server (C#)
- Docker Compose orchestration
- XSLT 2.0/3.0 via Saxon (XSLT3Service)
- bUnit (unit tests)
- Playwright (E2E tests)

**New Components:**
- Python 3.11 + FastAPI (Docling service)
- Docling library (PDF/Word conversion)
- Ollama (local LLM runtime)
- Llama 3.1 / Mistral (LLM models)

**Development Tools:**
- Docker Desktop
- npm (script runner)
- Swagger UI (API docs)
- Monaco Editor (code preview)

### Appendix B: Configuration Files

**docker-compose.yml additions:**
```yaml
services:
  docling-service:
    image: python:3.11
    container_name: taxxor-docling-service
    volumes:
      - ./docling-service:/app
      - ./data:/app/data
    working_dir: /app
    command: >
      sh -c "pip install -r requirements.txt &&
             uvicorn main:app --host 0.0.0.0 --port 4807 --reload"
    ports:
      - "4807:4807"
    networks:
      - blazornetwork

  pdfconversion:
    # ... existing config ...
    extra_hosts:
      - "host.docker.internal:host-gateway"
```

**data/user-selections.json additions:**
```json
{
  "selectedProject": "ar24-3",
  "selectedXsltFile": "docling/transformation.xslt",
  "selectedPipeline": "docling",
  "selectedLlmModel": "llama3.1:70b",
  "llmTemperature": 0.3
}
```

**package.json additions:**
```json
{
  "scripts": {
    "restart:docling": "docker compose restart docling-service",
    "logs:docling": "docker logs taxxor-docling-service --tail 100 -f"
  }
}
```

### Appendix C: Key File Locations

**New Files:**
```
docling-service/
├── main.py
├── requirements.txt
├── services/docling_converter.py
└── models/schemas.py

PdfConversion/
├── Services/
│   ├── OllamaService.cs
│   └── HierarchyGeneratorService.cs
├── Pages/
│   ├── DoclingConvert.razor
│   └── GenerateHierarchy.razor
├── Shared/
│   ├── HierarchyTreeView.razor
│   └── AvailableHeadersPanel.razor
└── wwwroot/js/
    └── hierarchy-editor.js

xslt/
├── adobe/
│   ├── transformation.xslt
│   └── modules/
└── docling/
    ├── transformation.xslt
    └── modules/

docs/plans/
└── 2025-01-25-docling-ai-hierarchy-design.md (this file)
```

**Modified Files:**
```
PdfConversion/Pages/Index.razor (add service status)
PdfConversion/Shared/NavMenu.razor (reorder menu)
PdfConversion/Pages/Transform.razor (add pipeline selector)
data/user-selections.json (add new fields)
docker-compose.yml (add docling service)
package.json (add new scripts)
```

### Appendix D: Dependencies

**docling-service/requirements.txt:**
```txt
fastapi==0.104.1
uvicorn[standard]==0.24.0
docling==1.0.0
python-multipart==0.0.6
pydantic==2.5.0
```

**PdfConversion.csproj additions:**
```xml
<!-- No new NuGet packages required -->
<!-- Uses existing HttpClient for Ollama API -->
```

### Appendix E: Useful Commands

**Ollama:**
```bash
# Install
brew install ollama

# Start service
ollama serve

# Pull model
ollama pull llama3.1:70b

# List models
ollama list

# Check API
curl http://localhost:11434/api/tags
```

**Docker:**
```bash
# Start all services
npm start

# Restart specific service
npm run restart:docling

# View logs
npm run logs:docling

# Check service health
curl http://localhost:4807/health
```

**Development:**
```bash
# Run tests
npm test

# Run E2E tests
npm run test:e2e

# Check compilation
docker logs taxxor-pdfconversion-1 | grep -E "(Building|error|Application started)" | tail -10
```

---

## Summary & Next Steps

### What This Design Achieves

✅ **Solves core challenges:**
- Challenge #1: Docling eliminates Adobe XML instability
- Challenge #2: Docling handles tables better
- Challenge #3: AI eliminates manual hierarchy creation
- Challenge #4: Local processing preserves data security

✅ **Meets success criteria:**
- 95%+ accuracy maintained via validation gates
- 85-90% time savings (8-10 hours → 30-45 minutes)
- Eliminates tedious work (tables, hierarchy debugging)

✅ **Preserves investment:**
- All existing services and UI reusable
- Backward compatible with Adobe pipeline
- Incremental adoption (new pipeline alongside old)

✅ **Extensible for future:**
- RAG upgrade path clear (backend-only change)
- Cloud AI option possible (user opt-in)
- Batch processing feasible

### How to Use This Design

**For Implementation Sessions:**
1. Read this design doc first
2. Check git log for recent commits
3. Review implementation progress tracker (below)
4. Continue from current phase

**For Questions/Clarifications:**
- Reference specific sections: "See Section 5.3: Drag-Drop Interactions"
- Design decisions documented: "Why Approach 1.5? See Architecture section"
- Error handling: "See Section 9: Error Scenarios"

**For Future Sessions:**
```
User: "Continue implementing the Docling feature"
Assistant: *reads this design doc*
           *reads git log*
           "I see we're in Phase 2.3 (Prompt Engineering). Let me continue..."
```

### Implementation Progress Tracker

**Instructions:** Update this checklist as you complete tasks. Mark with [x] when done.

#### Phase 1: Docling Pipeline (Weeks 1-2)
- [ ] 1.1: Docling Docker Service Setup
- [ ] 1.2: Docling Conversion API
- [ ] 1.3: `/docling-convert` Page
- [ ] 1.4: XSLT Reorganization
- [ ] 1.5: Docling XSLT Transformation
- [ ] 1.6: Integration Testing

#### Phase 2: AI Hierarchy Generation (Weeks 3-4)
- [ ] 2.1: Ollama Service Integration
- [ ] 2.2: Hierarchy Generator Service
- [ ] 2.3: Prompt Engineering
- [ ] 2.4: Model Configuration
- [ ] 2.5: Page Skeleton
- [ ] 2.6: Integration Testing

#### Phase 3: Drag-Drop Hierarchy Editor (Weeks 5-6)
- [ ] 3.1: Hierarchy Tree View Component
- [ ] 3.2: Inline Editing
- [ ] 3.3: Drag-Drop Interactions
- [ ] 3.4: Context Menu
- [ ] 3.5: Available Headers Panel
- [ ] 3.6: Validation Logic
- [ ] 3.7: Reasoning Panel
- [ ] 3.8: Save & Load
- [ ] 3.9: Integration Testing

#### Phase 4: Testing & Polish (Week 7)
- [ ] 4.1: Unit Tests (30 tests)
- [ ] 4.2: Integration Tests (15 tests)
- [ ] 4.3: E2E Tests (3 tests)
- [ ] 4.4: Error Handling Review
- [ ] 4.5: UI/UX Polish
- [ ] 4.6: Documentation Updates
- [ ] 4.7: Performance Testing

### Ready to Start!

This design is complete and approved. Implementation can begin immediately on branch `feature/docling-ai-hierarchy`.

**First step:** Phase 1.1 - Docling Docker Service Setup

**Estimated timeline:** 7 weeks to complete all phases

**Questions?** Refer back to specific sections of this document.

---

*Document Version: 1.0*
*Last Updated: 2025-01-25*
*Status: Ready for Implementation*
