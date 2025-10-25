# Lorem Ipsum Document Sanitization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a "Sanitize Document" tool to the Special Tools modal that creates lorem ipsum versions of XML files for safe sharing.

**Architecture:** Mirror the existing "Fix Characters" tool pattern - add `SanitizeXmlFileAsync()` method to FileService that uses Xslt3Service to apply `lorem_replace_text.xsl` transformation, then add UI section to Transform.razor with matching event handler.

**Tech Stack:** C# .NET 9, Blazor Server, XSLT 3.0 (Saxon via Xslt3Service), Docker

**Pre-requisites:** Fix 50 compilation errors in integration tests before starting implementation (see Task 0).

---

## Task 0: Fix Broken Integration Tests (MUST DO FIRST)

**Context:** Integration tests have 50 compilation errors related to missing methods on `TransformToolbarState`. These must be fixed before implementing the lorem ipsum feature to ensure we start with a clean baseline.

**Files:**
- Investigate: `PdfConversion/Tests/ComponentIntegrationTests.cs`
- Investigate: `PdfConversion/Models/TransformToolbarState.cs` (or wherever toolbar state is defined)

**Step 1: Run tests to confirm baseline failures**

Run: `npm run test:integration 2>&1 | grep "error CS" | wc -l`
Expected: Output should show `50` (or similar number)

**Step 2: Identify missing method signatures**

Read the test file and find what methods are being called:
- `OnFileChanged()`
- `OnProjectChanged()`

Grep for TransformToolbarState definition to understand where these should be added.

**Step 3: Fix the missing methods**

Based on investigation, either:
- Add missing methods to `TransformToolbarState`
- Update tests to use correct method names
- Fix whatever architectural issue is causing the problem

**Step 4: Verify tests compile and run**

Run: `npm run test:integration`
Expected: Tests should compile successfully (no error CS messages)

**Step 5: Commit test fixes**

```bash
git add PdfConversion/Tests/ PdfConversion/Models/ # or wherever fixes are
git commit -m "Fix integration test compilation errors

Resolved 50 compilation errors related to TransformToolbarState
missing method definitions. Tests now compile successfully."
```

**Step 6: Create feature branch**

```bash
git checkout -b feature/lorem-sanitization
```

---

## Task 1: Add Backend Method to FileService

**Goal:** Add `SanitizeXmlFileAsync()` method that transforms XML using the lorem ipsum XSLT.

**Files:**
- Modify: `PdfConversion/Services/FileService.cs` (add new method after `FixInvalidXmlCharactersAsync`)

**Step 1: Locate insertion point**

Open `FileService.cs` and find the `FixInvalidXmlCharactersAsync` method (around line 256).
The new method will go right after this one.

**Step 2: Add method signature and documentation**

Add after `FixInvalidXmlCharactersAsync`:

```csharp
/// <summary>
/// Creates a sanitized version of an XML file with lorem ipsum placeholder text.
/// </summary>
/// <param name="projectId">The project identifier</param>
/// <param name="fileName">The source XML file name</param>
/// <returns>Success status and result message</returns>
public async Task<(bool Success, string Message)> SanitizeXmlFileAsync(
    string projectId,
    string fileName)
{
    try
    {
        _logger.LogInformation("Starting sanitization for {FileName} in project {ProjectId}",
            fileName, projectId);

        // Build source file path
        var sourcePath = Path.Combine(_baseInputPath, projectId, fileName);

        if (!File.Exists(sourcePath))
        {
            var message = $"Source file not found: {fileName}";
            _logger.LogWarning(message);
            return (false, message);
        }

        // Read source XML content
        var xmlContent = await File.ReadAllTextAsync(sourcePath);

        // Transform using lorem ipsum XSLT
        var xsltPath = "xslt/_system/lorem_replace_text.xsl";
        var transformResult = await _xslt3Service.TransformXmlAsync(
            xmlContent,
            xsltPath,
            new Dictionary<string, string>() // No parameters needed
        );

        if (!transformResult.Success)
        {
            var message = $"Sanitization failed: {transformResult.Error}";
            _logger.LogError(message);
            return (false, message);
        }

        // Generate output filename: insert -lorem before extension
        var outputFileName = GenerateLoremFileName(fileName);
        var outputPath = Path.Combine(_baseInputPath, projectId, outputFileName);

        // Write sanitized XML
        await File.WriteAllTextAsync(outputPath, transformResult.TransformedXml);

        var successMessage = $"Successfully created lorem version: {outputFileName}";
        _logger.LogInformation(successMessage);
        return (true, successMessage);
    }
    catch (Exception ex)
    {
        var message = $"Failed to sanitize XML: {ex.Message}";
        _logger.LogError(ex, "Error during XML sanitization");
        return (false, message);
    }
}

/// <summary>
/// Generates output filename by inserting -lorem before file extension.
/// </summary>
/// <param name="fileName">Original filename (e.g., "report.xml")</param>
/// <returns>Lorem filename (e.g., "report-lorem.xml")</returns>
private string GenerateLoremFileName(string fileName)
{
    var extension = Path.GetExtension(fileName); // ".xml"
    var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName); // "report"
    return $"{nameWithoutExtension}-lorem{extension}"; // "report-lorem.xml"
}
```

**Step 3: Verify the code compiles**

Check Docker logs for compilation:
```bash
docker logs taxxor-pdfconversion-1 --tail 100 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: Should see "Application started" with recent timestamp, no "error CS" lines.

**Step 4: Commit the backend changes**

```bash
git add PdfConversion/Services/FileService.cs
git commit -m "Add SanitizeXmlFileAsync method to FileService

Implements lorem ipsum sanitization workflow:
- Reads source XML file
- Applies xslt/_system/lorem_replace_text.xsl transformation
- Writes output to [filename]-lorem.xml in same directory
- Returns success/error status with descriptive message"
```

---

## Task 2: Add Frontend State Variables to Transform.razor

**Goal:** Add state variables to track sanitization progress and results.

**Files:**
- Modify: `PdfConversion/Pages/Transform.razor` (around line 794, in the "Special Tools State" section)

**Step 1: Locate the state variables section**

Open `Transform.razor` and find the comment `// Special Tools State` (around line 793).
You'll see the Fix Characters state variables:
```csharp
private bool IsFixingCharacters = false;
private string? FixCharactersResult = null;
private bool FixCharactersSuccess = false;
```

**Step 2: Add sanitization state variables**

Add immediately after the Fix Characters variables:

```csharp
// Sanitization State
private bool IsSanitizing = false;
private string? SanitizeResult = null;
private bool SanitizeSuccess = false;
```

**Step 3: Verify compilation**

Check Docker logs:
```bash
docker logs taxxor-pdfconversion-1 --tail 100 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: Should compile successfully.

**Step 4: Commit state variables**

```bash
git add PdfConversion/Pages/Transform.razor
git commit -m "Add sanitization state variables to Transform.razor

Add IsSanitizing, SanitizeResult, SanitizeSuccess to track
sanitization tool progress and display feedback to user."
```

---

## Task 3: Add Frontend Event Handler to Transform.razor

**Goal:** Add `OnSanitizeXmlClickedAsync()` handler that calls FileService and updates UI.

**Files:**
- Modify: `PdfConversion/Pages/Transform.razor` (around line 1895, after `OnFixCharactersClickedAsync`)

**Step 1: Locate insertion point**

Find the `OnFixCharactersClickedAsync` method (ends around line 1894).
Add the new method right after it.

**Step 2: Add the event handler method**

```csharp
private async Task OnSanitizeXmlClickedAsync()
{
    if (string.IsNullOrEmpty(SelectedProjectId) || string.IsNullOrEmpty(SelectedFileName))
    {
        SanitizeResult = "No file selected";
        SanitizeSuccess = false;
        StateHasChanged();
        return;
    }

    try
    {
        IsSanitizing = true;
        SanitizeResult = null;
        StateHasChanged();

        Logger.LogInformation("Running Sanitize tool on {FileName} in project {ProjectId}",
            SelectedFileName, SelectedProjectId);

        var (success, message) = await FileService.SanitizeXmlFileAsync(
            SelectedProjectId,
            SelectedFileName);

        SanitizeResult = message;
        SanitizeSuccess = success;

        if (success)
        {
            ToastNotification.ShowSuccess(message);
            Logger.LogInformation("Sanitize completed: {Message}", message);

            // Reload XML content to make new file available in dropdown
            await LoadXmlContentFromFileAsync();
        }
        else
        {
            ToastNotification.ShowError(message);
            Logger.LogError("Sanitize failed: {Message}", message);
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error running Sanitize tool");
        SanitizeResult = $"Error: {ex.Message}";
        SanitizeSuccess = false;
        ToastNotification.ShowError($"Failed to sanitize document: {ex.Message}");
    }
    finally
    {
        IsSanitizing = false;
        StateHasChanged();
    }
}
```

**Step 3: Verify compilation**

Check Docker logs:
```bash
docker logs taxxor-pdfconversion-1 --tail 100 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: Should compile successfully.

**Step 4: Commit event handler**

```bash
git add PdfConversion/Pages/Transform.razor
git commit -m "Add OnSanitizeXmlClickedAsync event handler

Implements sanitization workflow:
- Validates file selection
- Calls FileService.SanitizeXmlFileAsync
- Shows toast notifications for success/error
- Reloads XML content to make new file available
- Updates UI state throughout process"
```

---

## Task 4: Add UI Section to Special Tools Modal

**Goal:** Add the "Sanitize Document" tool card to the Special Tools modal UI.

**Files:**
- Modify: `PdfConversion/Pages/Transform.razor` (around line 244, after the Fix Characters tool card)

**Step 1: Locate insertion point**

Find the closing `</div>` of the "Fix Characters" tool card (around line 244).
The new tool card will be added right after this, before the info alert.

**Step 2: Add the Sanitize Document tool card**

Insert this markup after the Fix Characters `</div>` and before the info alert:

```html
                <div class="tool-card mt-4">
                    <h3><i class="bi bi-shield-lock"></i> Sanitize Document</h3>

                    <p class="attribute-description">
                        Create a lorem ipsum version with bogus data for safe sharing.
                    </p>

                    <div class="attribute-usage">
                        <strong>What it does:</strong> Creates a copy of the XML file with all text replaced by lorem ipsum placeholder text while preserving the document structure. The sanitized file will be saved as [filename]-lorem.xml in the same directory.
                    </div>

                    <div class="mt-3">
                        <button class="btn btn-primary"
                                @onclick="OnSanitizeXmlClickedAsync"
                                disabled="@(IsSanitizing || string.IsNullOrEmpty(SelectedFileName))">
                            @if (IsSanitizing)
                            {
                                <span class="spinner-border spinner-border-sm me-2"></span>
                                <span>Sanitizing...</span>
                            }
                            else
                            {
                                <i class="bi bi-play-fill me-2"></i>
                                <span>Run Sanitize</span>
                            }
                        </button>
                    </div>

                    @if (!string.IsNullOrEmpty(SanitizeResult))
                    {
                        <div class="alert @(SanitizeSuccess ? "alert-success" : "alert-danger") mt-3 mb-0" role="alert">
                            <i class="bi @(SanitizeSuccess ? "bi-check-circle" : "bi-exclamation-triangle") me-2"></i>
                            @SanitizeResult
                        </div>
                    }
                </div>
```

**Step 3: Verify compilation and UI rendering**

Check Docker logs:
```bash
docker logs taxxor-pdfconversion-1 --tail 100 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: Should compile successfully.

**Step 4: Test in browser**

1. Navigate to http://localhost:8085
2. Open Special Tools modal (wrench icon)
3. Verify new "Sanitize Document" section appears
4. Verify button is disabled when no file selected
5. Verify icon and styling match Fix Characters

**Step 5: Commit UI changes**

```bash
git add PdfConversion/Pages/Transform.razor
git commit -m "Add Sanitize Document UI to Special Tools modal

Adds second tool card with:
- Description of lorem ipsum sanitization
- Run button with loading state
- Success/error alert display
- Consistent styling with Fix Characters tool"
```

---

## Task 5: Manual Testing - Happy Path

**Goal:** Verify the lorem ipsum sanitization works end-to-end with valid input.

**Prerequisites:**
- Docker services running: `docker ps | grep taxxor-pdfconversion`
- At least one XML file in `data/input/optiver/projects/ar24-3/`

**Step 1: Open application and select file**

1. Navigate to http://localhost:8085/transform
2. Select project: `ar24-3`
3. Select file: any `.xml` file (e.g., `oahpl-financial-statements-fy24.xml`)
4. Verify file loads in editor

**Step 2: Open Special Tools modal**

1. Click wrench icon in toolbar
2. Modal opens showing two tools: "Fix Characters" and "Sanitize Document"
3. Verify "Run Sanitize" button is enabled

**Step 3: Run sanitization**

1. Click "Run Sanitize" button
2. Observe button shows spinner and "Sanitizing..." text
3. Wait for completion (should take 2-5 seconds)
4. Verify success alert appears: "Successfully created lorem version: [filename]-lorem.xml"
5. Verify toast notification appears with success message

**Step 4: Verify sanitized file was created**

```bash
ls -lh data/input/optiver/projects/ar24-3/*-lorem.xml
```

Expected: File exists with recent timestamp.

**Step 5: Load and inspect sanitized file**

1. Close Special Tools modal
2. In file dropdown, select the new `-lorem.xml` file
3. Verify file loads in editor
4. Inspect content - text should be lorem ipsum, structure should be preserved
5. Verify XML is well-formed (no parsing errors)

**Step 6: Document test results**

Create or update `TEST-RESULTS.md` in project root:

```markdown
## Manual Test - Lorem Ipsum Sanitization Happy Path

**Date:** 2025-01-23
**Tested by:** [Your name]
**Result:** PASS / FAIL

**Steps:**
1. Selected file: [filename]
2. Ran sanitization: SUCCESS
3. Output file created: [filename]-lorem.xml
4. Output file loads correctly: YES
5. Content sanitized: YES
6. Structure preserved: YES

**Notes:**
[Any observations]
```

---

## Task 6: Manual Testing - Error Scenarios

**Goal:** Verify error handling works correctly for edge cases.

**Step 1: Test with no file selected**

1. Open http://localhost:8085/transform
2. Don't select any file
3. Open Special Tools modal
4. Verify "Run Sanitize" button is **disabled**
5. Verify no error messages appear

**Step 2: Test with invalid XSLT**

1. Temporarily rename the XSLT file:
   ```bash
   mv xslt/_system/lorem_replace_text.xsl xslt/_system/lorem_replace_text.xsl.bak
   ```
2. Select a valid XML file
3. Open Special Tools and click "Run Sanitize"
4. Expected: Error alert appears with "Sanitization failed" message
5. Expected: Toast notification shows error
6. Restore XSLT file:
   ```bash
   mv xslt/_system/lorem_replace_text.xsl.bak xslt/_system/lorem_replace_text.xsl
   ```

**Step 3: Test with malformed XML**

1. Create a test file with invalid XML:
   ```bash
   echo "<?xml version='1.0'?><root><unclosed>" > data/input/optiver/projects/ar24-3/test-invalid.xml
   ```
2. Select `test-invalid.xml` in UI
3. Run sanitization
4. Expected: Error alert shows transformation failure
5. Clean up:
   ```bash
   rm data/input/optiver/projects/ar24-3/test-invalid.xml
   ```

**Step 4: Test file creation in protected directory (optional)**

If you have permissions to test this:
1. Make input directory read-only:
   ```bash
   chmod 444 data/input/optiver/projects/ar24-3/
   ```
2. Run sanitization
3. Expected: Error alert shows "Failed to save sanitized file"
4. Restore permissions:
   ```bash
   chmod 755 data/input/optiver/projects/ar24-3/
   ```

**Step 5: Document error test results**

Update `TEST-RESULTS.md`:

```markdown
## Manual Test - Lorem Ipsum Error Scenarios

**Date:** 2025-01-23

### No File Selected
- Button disabled: YES
- No spurious errors: YES

### Invalid XSLT Path
- Error message shown: YES
- Error is descriptive: YES
- Toast notification: YES

### Malformed XML
- Error handled gracefully: YES
- User-friendly message: YES

### File Write Failure
- Error handled: [Result]
- Message shown: [Result]
```

---

## Task 7: Run Automated Tests

**Goal:** Verify existing tests still pass and the new feature doesn't break anything.

**Step 1: Run integration tests**

```bash
npm run test:integration
```

Expected: All tests pass (0 failures).
Note: We fixed the 50 compilation errors in Task 0, so tests should compile and run.

**Step 2: Check test output**

Look for:
- Number of tests passed
- No unexpected failures
- Reasonable execution time

**Step 3: Run E2E tests (if time permits)**

```bash
npm run test:e2e
```

Expected: All tests pass.
Note: These take longer (~5 minutes) and use Playwright.

**Step 4: Document test results**

Update `TEST-RESULTS.md`:

```markdown
## Automated Tests

**Date:** 2025-01-23

### Integration Tests
- Command: `npm run test:integration`
- Result: [N] passed, [N] failed
- Notes: [Any observations]

### E2E Tests
- Command: `npm run test:e2e`
- Result: [N] passed, [N] failed
- Notes: [Any observations]
```

---

## Task 8: Code Review and Cleanup

**Goal:** Review all changes for quality, consistency, and best practices.

**Step 1: Review FileService.cs changes**

Open `PdfConversion/Services/FileService.cs` and verify:
- [ ] Method follows existing code style
- [ ] Error handling is comprehensive
- [ ] Logging statements are appropriate
- [ ] No hardcoded paths (uses configuration)
- [ ] Documentation comments are clear
- [ ] No TODO comments left behind

**Step 2: Review Transform.razor changes**

Open `PdfConversion/Pages/Transform.razor` and verify:
- [ ] State variables follow naming conventions
- [ ] Event handler mirrors Fix Characters pattern
- [ ] UI markup is consistent with existing tools
- [ ] No console.log or debug code
- [ ] Proper null checking
- [ ] StateHasChanged() called appropriately

**Step 3: Check for accidental changes**

```bash
git status
git diff
```

Verify:
- [ ] No unintended files modified
- [ ] No commented-out code
- [ ] No debug statements
- [ ] No formatting-only changes in unrelated code

**Step 4: Run final compilation check**

```bash
docker logs taxxor-pdfconversion-1 --tail 50 2>&1 | grep -E "(error|warning)" | head -20
```

Note any new warnings introduced (should be zero).

**Step 5: Clean up any temporary test files**

```bash
# Remove any test files created during manual testing
rm -f data/input/optiver/projects/ar24-3/test-*.xml
```

---

## Task 9: Final Commit and Documentation

**Goal:** Create a final commit tying everything together and update documentation.

**Step 1: Review commit history**

```bash
git log --oneline -10
```

Expected commits:
1. Fix integration test compilation errors
2. Add SanitizeXmlFileAsync method to FileService
3. Add sanitization state variables to Transform.razor
4. Add OnSanitizeXmlClickedAsync event handler
5. Add Sanitize Document UI to Special Tools modal

**Step 2: Check for uncommitted changes**

```bash
git status
```

If there are uncommitted changes (like test results), decide whether to commit or discard them.

**Step 3: Update CLAUDE.md if needed**

Check if `CLAUDE.md` needs updates to reference the new feature:
- Special Tools section
- Example usage
- Testing instructions

If updates needed:
```bash
git add CLAUDE.md
git commit -m "docs: Update CLAUDE.md with lorem sanitization feature

Add documentation for Sanitize Document tool in Special Tools section."
```

**Step 4: Verify all changes are committed**

```bash
git status
```

Expected: "nothing to commit, working tree clean"

**Step 5: Review the feature branch**

```bash
git log main..feature/lorem-sanitization --oneline
```

Verify all expected commits are present.

---

## Task 10: Merge to Main and Deploy

**Goal:** Integrate the feature into the main branch.

**Step 1: Switch to main branch**

```bash
git checkout main
```

**Step 2: Merge feature branch**

```bash
git merge feature/lorem-sanitization --no-ff
```

Expected: Clean merge with no conflicts.

**Step 3: Verify application still works**

1. Services should still be running (Docker watches files)
2. Navigate to http://localhost:8085
3. Open Special Tools modal
4. Verify both tools present and working

**Step 4: Push to remote (if applicable)**

```bash
git push origin main
```

**Step 5: Clean up feature branch**

```bash
git branch -d feature/lorem-sanitization
```

**Step 6: Celebrate! 🎉**

The lorem ipsum sanitization feature is now complete and integrated!

---

## Future Phase 2: XSLT Adaptation (Out of Scope)

**Context:** The current XSLT (`lorem_replace_text.xsl`) was designed for Taxxor DM XHTML format. Adobe Acrobat Professional exports use different XML elements (`<H1>`, `<P>`, `<Table>` instead of `<h1>`, `<p>`, `<table>`).

**When to do this:**
- After Phase 1 is complete and tested
- When you have real Adobe XML files to test with
- When you want full compatibility with source XML format

**How to do this:**
1. Use `/xsltdevelop` slash command OR call `xslt-expert` agent directly
2. Provide Adobe XML fragment showing element structure
3. Agent will adapt XSLT templates to match Adobe format
4. Use `/transform-test` endpoint for rapid iteration
5. Test with full source XML files

**Agent to use:** `xslt-expert` (Session model - maintains context)

---

## Appendix: File Reference

**Modified Files:**
- `PdfConversion/Services/FileService.cs` - Added SanitizeXmlFileAsync method
- `PdfConversion/Pages/Transform.razor` - Added state, handler, and UI

**Referenced Files:**
- `xslt/_system/lorem_replace_text.xsl` - XSLT transformation (unchanged)
- `PdfConversion/Services/Xslt3Service.cs` - Used for transformation (unchanged)

**New Files:**
- None (feature uses existing infrastructure)

**Test Files:**
- `TEST-RESULTS.md` - Manual test documentation (created during testing)

---

## Appendix: Verification Checklist

Use this checklist to verify implementation completeness:

**Backend:**
- [ ] `SanitizeXmlFileAsync` method added to FileService
- [ ] Method signature matches design: `Task<(bool Success, string Message)>`
- [ ] Calls Xslt3Service.TransformXmlAsync correctly
- [ ] Generates correct output filename with `-lorem` suffix
- [ ] Error handling covers: file not found, transformation failure, write failure
- [ ] Logging statements added for success and error cases

**Frontend State:**
- [ ] `IsSanitizing` boolean added
- [ ] `SanitizeResult` string added (nullable)
- [ ] `SanitizeSuccess` boolean added
- [ ] Variables placed in "Special Tools State" section

**Frontend Handler:**
- [ ] `OnSanitizeXmlClickedAsync` method added
- [ ] Validates file selection before proceeding
- [ ] Sets loading state before async operation
- [ ] Calls FileService.SanitizeXmlFileAsync
- [ ] Shows toast notifications for success/error
- [ ] Reloads XML content after success
- [ ] Handles exceptions gracefully
- [ ] Clears loading state in finally block

**Frontend UI:**
- [ ] Tool card added after Fix Characters
- [ ] Icon: `bi-shield-lock`
- [ ] Description explains lorem ipsum sanitization
- [ ] Button disabled when no file selected
- [ ] Button shows loading spinner during operation
- [ ] Success/error alert displays result message
- [ ] Styling matches Fix Characters tool

**Testing:**
- [ ] Happy path tested with real XML file
- [ ] Error scenarios tested (no file, invalid XSLT, malformed XML)
- [ ] Sanitized file created with correct naming
- [ ] Sanitized file loads and displays correctly
- [ ] Integration tests pass
- [ ] E2E tests pass (if run)

**Code Quality:**
- [ ] No compilation errors
- [ ] No new warnings introduced
- [ ] Code follows existing patterns
- [ ] Comments and documentation clear
- [ ] No debug code or TODOs left behind
- [ ] All changes committed with descriptive messages

**Deployment:**
- [ ] Feature branch merged to main
- [ ] Application runs without errors
- [ ] Feature accessible in production (Docker)
- [ ] Documentation updated if needed
