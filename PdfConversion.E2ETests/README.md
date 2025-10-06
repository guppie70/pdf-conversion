# End-to-End Tests

This project contains Playwright-based end-to-end tests for the PDF Conversion application.

## Prerequisites

1. **Playwright Browsers**: Install Playwright browsers before running tests:
   ```bash
   pwsh bin/Debug/net9.0/playwright.ps1 install
   ```
   Or on macOS/Linux:
   ```bash
   playwright install
   ```

2. **Running Application**: The application must be running at `http://localhost:8085`:
   ```bash
   # From project root
   npm start
   ```

## Running Tests

### Run All Tests
```bash
# From PdfConversion.E2ETests directory
dotnet test

# Or from project root
npm run test:e2e
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~UserCanLoadProjectAndViewXML"
```

### Run with Verbose Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Organization

All tests are in `DevelopmentWorkflowTests.cs` and organized by criticality:

### Critical Path Tests (Must Pass)
1. **UserCanLoadProjectAndViewXML** - Loading projects and files (MOST CRITICAL)
2. **UserCanTransformXMLWithXSLT** - Core transformation feature

### Feature Tests
3. **UserCanToggleBetweenRenderedAndSourceViews** - View switching
4. **UserCanOpenAndUseSettingsPanel** - Settings functionality
5. **UserCanSaveXSLTChanges** - Saving transformations
6. **UserCanResetWorkflow** - Reset functionality

### System Tests
7. **UserCanNavigateBetweenPages** - Navigation flow
8. **ApplicationHandlesErrorsGracefully** - Error handling
9. **UIElementsAreProperlyAlignedAndVisible** - Layout regression detection
10. **MonacoEditorLoadsAndDisplaysXSLT** - Monaco editor integration

## Test Data

Tests use the `ar24-3` project with file `oahpl-financial-statements-fy24.xml` as the reference data.

## Debugging Failed Tests

1. **Check application is running**: Navigate to http://localhost:8085
2. **View Playwright trace**: Tests automatically capture traces on failure
3. **Check Docker logs**: `npm run logs` to see backend errors
4. **Run in headed mode**: Set `HEADED=1` environment variable to see browser

## CI/CD Integration

To run in CI/CD pipeline:
```bash
# Ensure application is started first
npm start &
sleep 10  # Wait for app to be ready

# Run tests
dotnet test PdfConversion.E2ETests/PdfConversion.E2ETests.csproj

# Cleanup
npm stop
```

## Writing New Tests

Follow the existing pattern:
```csharp
[Test]
public async Task UserCanDoSomething()
{
    // Arrange - Set up initial state
    await Page.SelectOptionAsync("select", "value");

    // Act - Perform user action
    await Page.ClickAsync("button");

    // Assert - Verify expected outcome
    await Expect(Page.Locator(".result")).ToBeVisibleAsync();
}
```

Key principles:
- Test from user's perspective
- Use meaningful test names (UserCan...)
- Wait for network idle before assertions
- Add descriptive assertion messages
