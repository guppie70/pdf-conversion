namespace PdfConversion.Services;

public class ThemeService
{
    public event Action? OnThemeChanged;

    public string CurrentTheme { get; private set; } = "dark";

    public void SetTheme(string theme)
    {
        CurrentTheme = theme;
        OnThemeChanged?.Invoke();
    }

    public void ToggleTheme()
    {
        CurrentTheme = CurrentTheme == "light" ? "dark" : "light";
        OnThemeChanged?.Invoke();
    }

    public string GetMonacoTheme()
    {
        return CurrentTheme == "light" ? "vs-light" : "vs-dark";
    }
}
