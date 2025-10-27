using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

public class HierarchyServiceTests : IDisposable
{
    private readonly Mock<ILogger<HierarchyService>> _loggerMock;
    private readonly HierarchyService _service;
    private readonly string _testDirectory;

    public HierarchyServiceTests()
    {
        _loggerMock = new Mock<ILogger<HierarchyService>>();
        _service = new HierarchyService(_loggerMock.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"hierarchy-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadHierarchyAsync_PreservesTocStartAttribute()
    {
        // Arrange
        var testXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<items>
    <structured>
        <item id=""root"" level=""0"" data-ref=""root.xml"">
            <web_page>
                <path>/</path>
                <linkname>Root</linkname>
            </web_page>
            <sub_items>
                <item id=""message-from-ceo"" level=""1"" data-ref=""message-from-ceo.xml"" data-tocstart=""true"">
                    <web_page>
                        <path>/message-from-ceo</path>
                        <linkname>Message from CEO</linkname>
                    </web_page>
                </item>
            </sub_items>
        </item>
    </structured>
</items>";
        var testFile = Path.Combine(_testDirectory, "test-tocstart.xml");
        await File.WriteAllTextAsync(testFile, testXml);

        // Act
        var hierarchy = await _service.LoadHierarchyAsync(testFile);

        // Assert
        Assert.NotNull(hierarchy.Root);
        Assert.Single(hierarchy.Root.SubItems);
        var item = hierarchy.Root.SubItems[0];
        Assert.Equal("message-from-ceo", item.Id);
        Assert.True(item.TocStart); // data-tocstart attribute should be preserved as TocStart=true
    }

    [Fact]
    public async Task LoadHierarchyAsync_PreservesTocEndAttribute()
    {
        // Arrange
        var testXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<items>
    <structured>
        <item id=""root"" level=""0"" data-ref=""root.xml"">
            <web_page>
                <path>/</path>
                <linkname>Root</linkname>
            </web_page>
            <sub_items>
                <item id=""back-cover"" level=""1"" data-ref=""back-cover.xml"" data-tocend=""true"">
                    <web_page>
                        <path>/back-cover</path>
                        <linkname>Back Cover</linkname>
                    </web_page>
                </item>
            </sub_items>
        </item>
    </structured>
</items>";
        var testFile = Path.Combine(_testDirectory, "test-tocend.xml");
        await File.WriteAllTextAsync(testFile, testXml);

        // Act
        var hierarchy = await _service.LoadHierarchyAsync(testFile);

        // Assert
        var item = hierarchy.Root.SubItems[0];
        Assert.True(item.TocEnd); // data-tocend attribute should be preserved as TocEnd=true
    }

    [Fact]
    public async Task LoadHierarchyAsync_PreservesTocNumberAttribute()
    {
        // Arrange
        var testXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<items>
    <structured>
        <item id=""root"" level=""0"" data-ref=""root.xml"">
            <web_page>
                <path>/</path>
                <linkname>Root</linkname>
            </web_page>
            <sub_items>
                <item id=""section-1"" level=""1"" data-ref=""section-1.xml"" data-tocnumber=""1"">
                    <web_page>
                        <path>/section-1</path>
                        <linkname>Section 1</linkname>
                    </web_page>
                    <sub_items>
                        <item id=""subsection-1-1"" level=""2"" data-ref=""subsection-1-1.xml"" data-tocnumber=""1.1"">
                            <web_page>
                                <path>/subsection-1-1</path>
                                <linkname>Subsection 1.1</linkname>
                            </web_page>
                        </item>
                    </sub_items>
                </item>
            </sub_items>
        </item>
    </structured>
</items>";
        var testFile = Path.Combine(_testDirectory, "test-tocnumber.xml");
        await File.WriteAllTextAsync(testFile, testXml);

        // Act
        var hierarchy = await _service.LoadHierarchyAsync(testFile);

        // Assert
        var section = hierarchy.Root.SubItems[0];
        Assert.Equal("1", section.TocNumber); // data-tocnumber should be preserved

        var subsection = section.SubItems[0];
        Assert.Equal("1.1", subsection.TocNumber); // nested data-tocnumber should be preserved
    }

    [Fact]
    public async Task LoadHierarchyAsync_PreservesTocStyleAttribute()
    {
        // Arrange
        var testXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<items>
    <structured>
        <item id=""root"" level=""0"" data-ref=""root.xml"">
            <web_page>
                <path>/</path>
                <linkname>Root</linkname>
            </web_page>
            <sub_items>
                <item id=""message-from-ceo"" level=""1"" data-ref=""message-from-ceo.xml"" data-tocstyle=""default"">
                    <web_page>
                        <path>/message-from-ceo</path>
                        <linkname>Message from CEO</linkname>
                    </web_page>
                </item>
                <item id=""notes"" level=""1"" data-ref=""notes.xml"" data-tocstyle=""notes123"">
                    <web_page>
                        <path>/notes</path>
                        <linkname>Notes</linkname>
                    </web_page>
                </item>
            </sub_items>
        </item>
    </structured>
</items>";
        var testFile = Path.Combine(_testDirectory, "test-tocstyle.xml");
        await File.WriteAllTextAsync(testFile, testXml);

        // Act
        var hierarchy = await _service.LoadHierarchyAsync(testFile);

        // Assert
        Assert.Equal("default", hierarchy.Root.SubItems[0].TocStyle);
        Assert.Equal("notes123", hierarchy.Root.SubItems[1].TocStyle);
    }

    [Fact]
    public async Task SaveAndLoadHierarchy_PreservesAllTocAttributes()
    {
        // Arrange - Create a hierarchy with all TOC attributes
        var hierarchy = new HierarchyStructure
        {
            Root = new HierarchyItem
            {
                Id = "root",
                Level = 0,
                DataRef = "root.xml",
                LinkName = "Root",
                Path = "/",
                SubItems = new List<HierarchyItem>
                {
                    new HierarchyItem
                    {
                        Id = "message-from-ceo",
                        Level = 1,
                        DataRef = "message-from-ceo.xml",
                        LinkName = "Message from CEO",
                        Path = "/message-from-ceo",
                        TocStart = true,
                        TocStyle = "default",
                        TocNumber = "1"
                    },
                    new HierarchyItem
                    {
                        Id = "financial-performance",
                        Level = 1,
                        DataRef = "financial-performance.xml",
                        LinkName = "Financial Performance",
                        Path = "/financial-performance",
                        TocNumber = "2",
                        SubItems = new List<HierarchyItem>
                        {
                            new HierarchyItem
                            {
                                Id = "sales-analysis",
                                Level = 2,
                                DataRef = "sales-analysis.xml",
                                LinkName = "Sales Analysis",
                                Path = "/sales-analysis",
                                TocNumber = "2.1"
                            }
                        }
                    },
                    new HierarchyItem
                    {
                        Id = "back-cover",
                        Level = 1,
                        DataRef = "back-cover.xml",
                        LinkName = "Back Cover",
                        Path = "/back-cover",
                        TocEnd = true
                    }
                }
            }
        };

        var testFile = Path.Combine(_testDirectory, "roundtrip-test.xml");

        // Act - Save and reload
        await _service.SaveHierarchyAsync(testFile, hierarchy);
        var reloadedHierarchy = await _service.LoadHierarchyAsync(testFile);

        // Assert - All TOC attributes should be preserved
        var ceo = reloadedHierarchy.Root.SubItems[0];
        Assert.Equal("message-from-ceo", ceo.Id);
        Assert.True(ceo.TocStart); // TocStart should be preserved through save/load
        Assert.Equal("default", ceo.TocStyle); // TocStyle should be preserved
        Assert.Equal("1", ceo.TocNumber); // TocNumber should be preserved

        var financial = reloadedHierarchy.Root.SubItems[1];
        Assert.Equal("2", financial.TocNumber);
        Assert.False(financial.TocStart); // TocStart should default to false
        Assert.False(financial.TocEnd); // TocEnd should default to false

        var salesAnalysis = financial.SubItems[0];
        Assert.Equal("2.1", salesAnalysis.TocNumber); // Nested TocNumber should be preserved

        var backCover = reloadedHierarchy.Root.SubItems[2];
        Assert.True(backCover.TocEnd); // TocEnd should be preserved through save/load
    }

    [Fact]
    public async Task SaveHierarchyAsync_WritesTocAttributesCorrectly()
    {
        // Arrange
        var hierarchy = new HierarchyStructure
        {
            Root = new HierarchyItem
            {
                Id = "root",
                Level = 0,
                DataRef = "root.xml",
                LinkName = "Root",
                Path = "/",
                SubItems = new List<HierarchyItem>
                {
                    new HierarchyItem
                    {
                        Id = "section-1",
                        Level = 1,
                        DataRef = "section-1.xml",
                        LinkName = "Section 1",
                        Path = "/section-1",
                        TocStart = true,
                        TocStyle = "default",
                        TocNumber = "1"
                    }
                }
            }
        };

        var testFile = Path.Combine(_testDirectory, "save-test.xml");

        // Act
        await _service.SaveHierarchyAsync(testFile, hierarchy);

        // Assert - Check the actual XML content
        var xmlContent = await File.ReadAllTextAsync(testFile);
        Assert.Contains("data-tocstart=\"true\"", xmlContent); // XML should contain data-tocstart attribute
        Assert.Contains("data-tocstyle=\"default\"", xmlContent); // XML should contain data-tocstyle attribute
        Assert.Contains("data-tocnumber=\"1\"", xmlContent); // XML should contain data-tocnumber attribute
    }

    [Fact]
    public async Task SaveHierarchyAsync_OmitsTocAttributesWhenFalseOrNull()
    {
        // Arrange - Item without any TOC attributes set
        var hierarchy = new HierarchyStructure
        {
            Root = new HierarchyItem
            {
                Id = "root",
                Level = 0,
                DataRef = "root.xml",
                LinkName = "Root",
                Path = "/",
                SubItems = new List<HierarchyItem>
                {
                    new HierarchyItem
                    {
                        Id = "section-1",
                        Level = 1,
                        DataRef = "section-1.xml",
                        LinkName = "Section 1",
                        Path = "/section-1"
                        // No TOC attributes set (all default values)
                    }
                }
            }
        };

        var testFile = Path.Combine(_testDirectory, "omit-test.xml");

        // Act
        await _service.SaveHierarchyAsync(testFile, hierarchy);

        // Assert - Check that TOC attributes are not present
        var xmlContent = await File.ReadAllTextAsync(testFile);
        Assert.DoesNotContain("data-tocstart", xmlContent); // XML should not contain data-tocstart when false
        Assert.DoesNotContain("data-tocend", xmlContent); // XML should not contain data-tocend when false
        Assert.DoesNotContain("data-tocnumber", xmlContent); // XML should not contain data-tocnumber when null
        Assert.DoesNotContain("data-tocstyle", xmlContent); // XML should not contain data-tocstyle when null
    }

    [Fact]
    public async Task LoadHierarchyAsync_PreservesTocHideAttribute()
    {
        // Arrange
        var testXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<items>
  <structured>
    <item id=""root"" level=""0"" data-ref=""root.xml"">
      <web_page>
        <path>/</path>
        <linkname>Root</linkname>
      </web_page>
      <sub_items>
        <item id=""contact"" level=""1"" data-ref=""contact.xml"" data-tochide=""true"">
          <web_page>
            <path>/</path>
            <linkname>Contact</linkname>
          </web_page>
        </item>
      </sub_items>
    </item>
  </structured>
</items>";

        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_tochide_{Guid.NewGuid()}.xml");
        await File.WriteAllTextAsync(testFilePath, testXml);

        try
        {
            // Act
            var hierarchy = await _service.LoadHierarchyAsync(testFilePath);

            // Assert
            var contactItem = hierarchy.Root.SubItems.First();
            Assert.True(contactItem.TocHide);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task SaveHierarchyAsync_WritesTocHideAttributeCorrectly()
    {
        // Arrange
        var hierarchy = new HierarchyStructure
        {
            Root = new HierarchyItem
            {
                Id = "root",
                Level = 0,
                DataRef = "root.xml",
                LinkName = "Root",
                Path = "/",
                SubItems = new List<HierarchyItem>
                {
                    new HierarchyItem
                    {
                        Id = "contact",
                        Level = 1,
                        DataRef = "contact.xml",
                        LinkName = "Contact",
                        Path = "/",
                        TocHide = true,
                        SubItems = new List<HierarchyItem>()
                    }
                }
            }
        };

        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_save_tochide_{Guid.NewGuid()}.xml");

        try
        {
            // Act
            await _service.SaveHierarchyAsync(testFilePath, hierarchy);

            // Assert
            var savedXml = await File.ReadAllTextAsync(testFilePath);
            Assert.Contains("data-tochide=\"true\"", savedXml);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task SaveHierarchyAsync_OmitsTocHideWhenFalse()
    {
        // Arrange
        var hierarchy = new HierarchyStructure
        {
            Root = new HierarchyItem
            {
                Id = "root",
                Level = 0,
                DataRef = "root.xml",
                LinkName = "Root",
                Path = "/",
                TocHide = false,
                SubItems = new List<HierarchyItem>()
            }
        };

        var testFilePath = Path.Combine(Path.GetTempPath(), $"test_omit_tochide_{Guid.NewGuid()}.xml");

        try
        {
            // Act
            await _service.SaveHierarchyAsync(testFilePath, hierarchy);

            // Assert
            var savedXml = await File.ReadAllTextAsync(testFilePath);
            Assert.DoesNotContain("data-tochide", savedXml);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }
}
