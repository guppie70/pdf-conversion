using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Shared state service for the Development page toolbar
/// Allows toolbar controls in the navigation bar to communicate with the Development page
/// </summary>
public class DevelopmentToolbarState
{
    // State
    public List<Project>? Projects { get; set; }
    public List<string>? ProjectFiles { get; set; }
    public string? SelectedProjectId { get; set; }
    public string? SelectedFileName { get; set; }
    public bool IsLoading { get; set; }
    public bool IsTransforming { get; set; }
    public bool IsSaving { get; set; }
    public bool ShowSettings { get; set; }

    // Computed
    public bool CanTransform { get; set; }
    public bool CanSave { get; set; }

    // Callbacks - Development page sets these
    public Func<string, Task>? OnProjectChanged { get; set; }
    public Func<string, Task>? OnFileChanged { get; set; }
    public Func<Task>? OnTransform { get; set; }
    public Func<Task>? OnSave { get; set; }
    public Func<Task>? OnReset { get; set; }
    public Action? OnToggleSettings { get; set; }

    // Event to notify when state changes
    public event Action? OnChange;

    public void NotifyStateChanged() => OnChange?.Invoke();
}
