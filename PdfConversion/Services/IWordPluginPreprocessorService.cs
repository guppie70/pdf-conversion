namespace PdfConversion.Services;

public interface IWordPluginPreprocessorService
{
    Task<PreprocessorResult> PreprocessAsync(
        string inputPath,
        string outputPath,
        string imagesOutputPath,
        bool writeDebugFiles = true,
        IProgress<PreprocessorProgress>? progress = null
    );
}
