using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

public class HierarchyGeneratorServiceTests
{
    private readonly Mock<IOllamaService> _ollamaServiceMock;
    private readonly Mock<ILogger<HierarchyGeneratorService>> _loggerMock;
    private readonly Mock<RuleBasedHierarchyGenerator> _ruleBasedGeneratorMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly HierarchyGeneratorService _service;

    public HierarchyGeneratorServiceTests()
    {
        _ollamaServiceMock = new Mock<IOllamaService>();
        _loggerMock = new Mock<ILogger<HierarchyGeneratorService>>();
        _ruleBasedGeneratorMock = new Mock<RuleBasedHierarchyGenerator>(
            Mock.Of<ILogger<RuleBasedHierarchyGenerator>>());
        _configurationMock = new Mock<IConfiguration>();

        _service = new HierarchyGeneratorService(
            _ollamaServiceMock.Object,
            _loggerMock.Object,
            _ruleBasedGeneratorMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task GenerateHierarchyAsync_ValidJsonResponse_ReturnsProposal()
    {
        // Arrange
        var normalizedXml = @"<?xml version=""1.0""?>
<html>
    <body>
        <h1>Annual Report 2022</h1>
        <h2>CEO Message</h2>
        <p>Content here</p>
    </body>
</html>";

        var exampleHierarchies = new List<string>
        {
            @"<items><structured><item id=""root"" level=""0"" data-ref=""root.xml""><web_page><path>/</path><linkname>Root</linkname></web_page></item></structured></items>"
        };

        var mockJsonResponse = @"{
            ""reasoning"": ""Clear document structure detected"",
            ""root"": {
                ""id"": ""report-root"",
                ""level"": 0,
                ""linkName"": ""Annual Report 2022"",
                ""dataRef"": ""report-root.xml"",
                ""path"": ""/"",
                ""confidence"": 100,
                ""subItems"": [
                    {
                        ""id"": ""ceo-message"",
                        ""level"": 1,
                        ""linkName"": ""CEO Message"",
                        ""dataRef"": ""ceo-message.xml"",
                        ""path"": ""/"",
                        ""confidence"": 95,
                        ""tocStart"": true,
                        ""tocNumber"": ""1"",
                        ""tocStyle"": ""default"",
                        ""subItems"": []
                    }
                ]
            }
        }";

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJsonResponse);

        // Act
        var result = await _service.GenerateHierarchyAsync(
            normalizedXml,
            exampleHierarchies,
            "llama3.1:70b");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(97, result.OverallConfidence); // Average of 100 (root) + 95 (child) = 97
        Assert.Equal(2, result.TotalItems); // root + 1 child
        Assert.Equal("Clear document structure detected", result.Reasoning);
        Assert.Equal("report-root", result.Root.Id);
        Assert.Single(result.Root.SubItems);

        var ceoItem = result.Root.SubItems[0];
        Assert.Equal("ceo-message", ceoItem.Id);
        Assert.Equal(1, ceoItem.Level);
        Assert.Equal("CEO Message", ceoItem.LinkName);
        Assert.Equal(95, ceoItem.Confidence);
        Assert.True(ceoItem.TocStart);
        Assert.Equal("1", ceoItem.TocNumber);
        Assert.Equal("default", ceoItem.TocStyle);
    }

    [Fact]
    public async Task GenerateHierarchyAsync_JsonWithUncertainItems_IdentifiesUncertainties()
    {
        // Arrange
        var normalizedXml = "<html><body><h1>Test</h1></body></html>";
        var exampleHierarchies = new List<string> { "<items></items>" };

        var mockJsonResponse = @"{
            ""reasoning"": ""Some uncertainty in structure"",
            ""root"": {
                ""id"": ""root"",
                ""level"": 0,
                ""linkName"": ""Root"",
                ""dataRef"": ""root.xml"",
                ""path"": ""/"",
                ""confidence"": 80,
                ""subItems"": [
                    {
                        ""id"": ""section-1"",
                        ""level"": 1,
                        ""linkName"": ""Section 1"",
                        ""dataRef"": ""section-1.xml"",
                        ""path"": ""/"",
                        ""confidence"": 65,
                        ""reasoning"": ""Unclear structure"",
                        ""subItems"": []
                    },
                    {
                        ""id"": ""section-2"",
                        ""level"": 1,
                        ""linkName"": ""Section 2"",
                        ""dataRef"": ""section-2.xml"",
                        ""path"": ""/"",
                        ""confidence"": 90,
                        ""subItems"": []
                    }
                ]
            }
        }";

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJsonResponse);

        // Act
        var result = await _service.GenerateHierarchyAsync(normalizedXml, exampleHierarchies);

        // Assert
        Assert.Single(result.Uncertainties); // Only section-1 with confidence < 70%
        var uncertainItem = result.Uncertainties[0];
        Assert.Equal("section-1", uncertainItem.Id);
        Assert.Equal(65, uncertainItem.Confidence);
        Assert.True(uncertainItem.IsUncertain);
        Assert.Equal("Unclear structure", uncertainItem.Reasoning);
    }

    [Fact]
    public async Task GenerateHierarchyAsync_CalculatesOverallConfidence()
    {
        // Arrange
        var normalizedXml = "<html><body><h1>Test</h1></body></html>";
        var exampleHierarchies = new List<string> { "<items></items>" };

        var mockJsonResponse = @"{
            ""reasoning"": ""Test"",
            ""root"": {
                ""id"": ""root"",
                ""level"": 0,
                ""linkName"": ""Root"",
                ""dataRef"": ""root.xml"",
                ""path"": ""/"",
                ""confidence"": 100,
                ""subItems"": [
                    {
                        ""id"": ""item1"",
                        ""level"": 1,
                        ""linkName"": ""Item 1"",
                        ""dataRef"": ""item1.xml"",
                        ""path"": ""/"",
                        ""confidence"": 80,
                        ""subItems"": []
                    },
                    {
                        ""id"": ""item2"",
                        ""level"": 1,
                        ""linkName"": ""Item 2"",
                        ""dataRef"": ""item2.xml"",
                        ""path"": ""/"",
                        ""confidence"": 90,
                        ""subItems"": []
                    }
                ]
            }
        }";

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJsonResponse);

        // Act
        var result = await _service.GenerateHierarchyAsync(normalizedXml, exampleHierarchies);

        // Assert
        // Average: (100 + 80 + 90) / 3 = 90
        Assert.Equal(90, result.OverallConfidence);
    }

    [Fact]
    public async Task GenerateHierarchyAsync_HandlesNestedSubItems()
    {
        // Arrange
        var normalizedXml = "<html><body><h1>Test</h1></body></html>";
        var exampleHierarchies = new List<string> { "<items></items>" };

        var mockJsonResponse = @"{
            ""reasoning"": ""Nested structure"",
            ""root"": {
                ""id"": ""root"",
                ""level"": 0,
                ""linkName"": ""Root"",
                ""dataRef"": ""root.xml"",
                ""path"": ""/"",
                ""confidence"": 100,
                ""subItems"": [
                    {
                        ""id"": ""section"",
                        ""level"": 1,
                        ""linkName"": ""Section"",
                        ""dataRef"": ""section.xml"",
                        ""path"": ""/"",
                        ""confidence"": 95,
                        ""tocNumber"": ""1"",
                        ""subItems"": [
                            {
                                ""id"": ""subsection"",
                                ""level"": 2,
                                ""linkName"": ""Subsection"",
                                ""dataRef"": ""subsection.xml"",
                                ""path"": ""/"",
                                ""confidence"": 85,
                                ""tocNumber"": ""1.1"",
                                ""subItems"": []
                            }
                        ]
                    }
                ]
            }
        }";

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJsonResponse);

        // Act
        var result = await _service.GenerateHierarchyAsync(normalizedXml, exampleHierarchies);

        // Assert
        Assert.Equal(3, result.TotalItems); // root + section + subsection
        var section = result.Root.SubItems[0];
        Assert.Equal("1", section.TocNumber);
        Assert.Single(section.SubItems);

        var subsection = section.SubItems[0];
        Assert.Equal("subsection", subsection.Id);
        Assert.Equal(2, subsection.Level);
        Assert.Equal("1.1", subsection.TocNumber);
    }

    [Fact]
    public async Task GenerateHierarchyAsync_CleansJsonMarkdown()
    {
        // Arrange - LLM sometimes wraps JSON in markdown code blocks
        var normalizedXml = "<html><body><h1>Test</h1></body></html>";
        var exampleHierarchies = new List<string> { "<items></items>" };

        var mockJsonResponse = @"```json
{
    ""reasoning"": ""Test"",
    ""root"": {
        ""id"": ""root"",
        ""level"": 0,
        ""linkName"": ""Root"",
        ""dataRef"": ""root.xml"",
        ""path"": ""/"",
        ""confidence"": 100,
        ""subItems"": []
    }
}
```";

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJsonResponse);

        // Act
        var result = await _service.GenerateHierarchyAsync(normalizedXml, exampleHierarchies);

        // Assert - Should successfully parse despite markdown wrapping
        Assert.NotNull(result);
        Assert.Equal("root", result.Root.Id);
    }

    [Fact]
    public async Task GenerateHierarchyAsync_InvalidJson_ThrowsException()
    {
        // Arrange
        var normalizedXml = "<html><body><h1>Test</h1></body></html>";
        var exampleHierarchies = new List<string> { "<items></items>" };

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is not valid JSON");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.GenerateHierarchyAsync(normalizedXml, exampleHierarchies));
    }

    [Fact]
    public async Task GenerateHierarchyAsync_PassesCorrectParameters()
    {
        // Arrange
        var normalizedXml = "<html><body><h1>Test</h1></body></html>";
        var exampleHierarchies = new List<string> { "<items></items>" };
        var modelName = "llama3.1:70b";

        var mockJsonResponse = @"{
            ""reasoning"": ""Test"",
            ""root"": {
                ""id"": ""root"",
                ""level"": 0,
                ""linkName"": ""Root"",
                ""dataRef"": ""root.xml"",
                ""path"": ""/"",
                ""confidence"": 100,
                ""subItems"": []
            }
        }";

        string? capturedModel = null;
        double? capturedTemperature = null;

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, double, CancellationToken>((model, prompt, temp, ct) =>
            {
                capturedModel = model;
                capturedTemperature = temp;
            })
            .ReturnsAsync(mockJsonResponse);

        // Act
        await _service.GenerateHierarchyAsync(normalizedXml, exampleHierarchies, modelName);

        // Assert
        Assert.Equal(modelName, capturedModel);
        Assert.Equal(0.3, capturedTemperature); // Should use deterministic temperature
    }

    [Fact]
    public async Task GenerateHierarchyAsync_ConvertToHierarchyStructure()
    {
        // Arrange
        var normalizedXml = "<html><body><h1>Test</h1></body></html>";
        var exampleHierarchies = new List<string> { "<items></items>" };

        var mockJsonResponse = @"{
            ""reasoning"": ""Test"",
            ""root"": {
                ""id"": ""root"",
                ""level"": 0,
                ""linkName"": ""Root"",
                ""dataRef"": ""root.xml"",
                ""path"": ""/"",
                ""confidence"": 100,
                ""subItems"": []
            }
        }";

        _ollamaServiceMock
            .Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJsonResponse);

        // Act
        var proposal = await _service.GenerateHierarchyAsync(normalizedXml, exampleHierarchies);
        var hierarchyStructure = proposal.ToHierarchyStructure();

        // Assert
        Assert.NotNull(hierarchyStructure);
        Assert.Equal(proposal.Root.Id, hierarchyStructure.Root.Id);
        Assert.Equal(proposal.OverallConfidence, hierarchyStructure.OverallConfidence);
        Assert.Equal(proposal.Uncertainties.Count, hierarchyStructure.Uncertainties.Count);
    }
}
