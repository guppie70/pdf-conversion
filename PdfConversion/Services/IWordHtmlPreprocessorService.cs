namespace PdfConversion.Services;

public interface IWordHtmlPreprocessorService
{
    Task<PreprocessorResult> PreprocessAsync(
        string inputPath,
        string outputPath,
        bool writeDebugFiles = true,
        IProgress<PreprocessorProgress>? progress = null
    );
}

public record PreprocessorResult(
    bool Success,
    string? OutputPath,
    string? ErrorMessage,
    List<PreprocessorStepLog> StepLogs
);

public record PreprocessorStepLog(
    string StepName,
    TimeSpan Duration,
    string Summary
);

public record PreprocessorProgress(
    int CurrentStep,
    int TotalSteps,
    string StepName,
    string Status
);
