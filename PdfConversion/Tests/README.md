# Integration Tests

This project contains bUnit-based integration tests for Blazor components and their interactions with services.

## Prerequisites

No special setup required. All dependencies are included via NuGet packages:
- bUnit 1.40.0
- bUnit.web 1.40.0
- xUnit 2.9.2
- Moq 4.20.72
- FluentAssertions 6.12.1

## Running Tests

### Run All Tests
```bash
# From PdfConversion/Tests directory
dotnet test

# Or from project root
npm run test
```

### Run Specific Test Category
```bash
# Service integration tests
dotnet test --filter "FullyQualifiedName~ServiceIntegration"

# Toolbar state communication tests
dotnet test --filter "FullyQualifiedName~ToolbarState"

# Data binding tests
dotnet test --filter "FullyQualifiedName~DataBinding"
```

### Run with Coverage
```bash
dotnet test /p:CollectCoverage=true
```

## Test Organization

All integration tests are in `ComponentIntegrationTests.cs` and organized into logical groups:

### Group 1: Service Integration Tests (Tests 1-8)
Tests component-to-service communication:
- ProjectSelection_LoadsFileList
- FileSelection_LoadsXmlContent
- ProjectSelection_ClearsFileSelection
- AutoTransform_TriggersOnFileLoad
- TransformationService_CalledWithCorrectParameters
- SaveXslt_CallsService
- Reset_ClearsAllState
- ErrorInService_DisplaysErrorMessage

### Group 2: Toolbar State Communication Tests (Tests 9-14)
Tests cross-component state management via `DevelopmentToolbarState`:
- ToolbarStateUpdate_NotifiesComponent
- ComponentUpdate_UpdatesToolbarState
- ProjectChange_UpdatesToolbarAndComponent
- FileChange_UpdatesToolbarAndComponent
- TransformButton_EnabledBasedOnState
- SettingsToggle_UpdatesToolbarState

### Group 3: Data Binding and UI State Tests (Tests 15-20)
Tests UI updates and data binding:
- XmlContent_RendersInTextarea
- OutputContent_RendersInPreview
- ErrorMessage_DisplaysAlert
- SuccessMessage_DisplaysAlert
- LoadingState_ShowsSpinner
- DisabledState_DisablesInputs

### Group 4: Event Handling Tests (Tests 21-26)
Tests event flow and state changes:
- ProjectDropdown_OnChangeTriggersHandler
- FileDropdown_OnChangeTriggersHandler
- TransformButton_ClickTriggersTransformation
- SaveButton_ClickTriggersSave
- ResetButton_ClickTriggersReset
- SettingsButton_ClickTogglesPanel

### Group 5: Transformation Options Tests (Tests 27-30)
Tests settings and transformation behavior:
- UseXslt3_TogglesService
- NormalizeHeaders_PassedToService
- AutoTransform_TriggersOnXsltChange
- TransformationOptions_PersistAcrossTransforms

## Test Pattern

All tests follow the bUnit pattern:

```csharp
[Fact]
public async Task ComponentBehavior_ExpectedOutcome()
{
    // Arrange - Set up mocks and services
    _mockProjectService.Setup(s => s.Method()).ReturnsAsync(result);
    var cut = RenderComponent<Development>();

    // Act - Trigger component behavior
    await cut.InvokeAsync(() => cut.Instance.Method());

    // Assert - Verify outcome
    _mockProjectService.Verify(s => s.Method(), Times.Once);
    Assert.Equal(expected, actual);
}
```

## Mocking Strategy

Tests use Moq to mock external dependencies:
- `Mock<IProjectManagementService>`: File system operations
- `Mock<IXsltTransformationService>`: XSLT transformations
- `Mock<ILogger<Development>>`: Logging (optional)

This ensures:
- Fast test execution (no file I/O or network calls)
- Predictable test results
- Isolation from external systems

## Key Testing Concepts

### What We Test
✅ Component renders correctly with given state
✅ Component calls services with correct parameters
✅ Component updates UI when state changes
✅ Component handles service errors gracefully
✅ Cross-component communication via shared state
✅ Event handlers trigger expected behavior

### What We Don't Test
❌ Service implementation details (tested in unit tests)
❌ XSLT transformation logic (tested in E2E tests)
❌ Browser-specific rendering (tested in E2E tests)
❌ Full user journeys (tested in E2E tests)

## Debugging Failed Tests

1. **Check mock setup**: Verify mock returns expected values
2. **Inspect component state**: Use `cut.Instance.PropertyName`
3. **Check rendering output**: Use `cut.Markup` to see HTML
4. **Verify service calls**: Use `_mockService.Verify()` with `Times.Exactly(n)`
5. **Check StateHasChanged**: Ensure component triggers re-rendering

## Common Issues

### StateHasChanged Not Called
**Symptom**: Component state changes but UI doesn't update
**Fix**: Add `StateHasChanged()` call in event handlers
**Example**: See OnProjectChanged, OnFileChanged in Development.razor

### Mock Not Returning Value
**Symptom**: NullReferenceException in component
**Fix**: Ensure mock setup matches method signature exactly
```csharp
// Wrong
_mock.Setup(s => s.Method(It.IsAny<string>())).ReturnsAsync(null);

// Right
_mock.Setup(s => s.Method(It.IsAny<string>())).ReturnsAsync(new List<string>());
```

### Component Not Re-rendering
**Symptom**: Assert fails even though state changed
**Fix**: Wrap state change in `cut.InvokeAsync()`
```csharp
await cut.InvokeAsync(async () => await cut.Instance.Method());
```

## Writing New Tests

When adding features, add tests in appropriate group:
- Component talks to service? → Service Integration
- Component talks to another component? → Toolbar State Communication
- UI updates based on state? → Data Binding
- User interaction triggers action? → Event Handling
- Settings affect behavior? → Transformation Options

Follow naming convention:
```
ActionOrState_ExpectedOutcome
```

Examples:
- `ProjectSelection_LoadsFileList`
- `ErrorInService_DisplaysErrorMessage`
- `AutoTransform_TriggersOnFileLoad`
