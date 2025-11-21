namespace PdfConversion.Models;

/// <summary>
/// Represents an Ollama model available on the local system
/// </summary>
public class OllamaModel
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedAt { get; set; }
    public OllamaModelDetails? Details { get; set; }
}

public class OllamaModelDetails
{
    public string Format { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string ParameterSize { get; set; } = string.Empty;
    public string QuantizationLevel { get; set; } = string.Empty;
}

/// <summary>
/// Request/response models for Ollama API
/// </summary>
public class OllamaGenerateRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool Stream { get; set; } = false;
    public string? Format { get; set; }  // "json" to request JSON response
    public OllamaOptions? Options { get; set; }
}

public class OllamaOptions
{
    public double Temperature { get; set; } = 0.3;
    public double TopP { get; set; } = 0.9;
    public int NumPredict { get; set; } = 4096;
}

public class OllamaGenerateResponse
{
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public int PromptEvalCount { get; set; }
    public long PromptEvalDuration { get; set; }
    public int EvalCount { get; set; }
    public long EvalDuration { get; set; }
}
