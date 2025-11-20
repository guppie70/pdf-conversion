using PdfConversion.Models;
using PdfConversion.Services;
using System.Text.Json;
using Xunit;

namespace PdfConversion.Tests.Integration;

public class ProjectMetadataMigrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _oldFilePath;
    private readonly string _newFilePath;

    public ProjectMetadataMigrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"test-migration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _oldFilePath = Path.Combine(_testDir, "project-labels.json");
        _newFilePath = Path.Combine(_testDir, "project-metadata.json");
    }

    [Fact]
    public async Task GetAllProjects_OldFormatExists_MigratesAndDeletesOld()
    {
        // Create old format file
        var oldFormat = new
        {
            labels = new
            {
                optiver = new Dictionary<string, string>
                {
                    ["ar24-3"] = "Optiver Australia Holdings Pty Limited",
                    ["ar24-5"] = "Optiver Services B.V."
                }
            },
            lastModified = DateTime.UtcNow
        };
        await File.WriteAllTextAsync(_oldFilePath, JsonSerializer.Serialize(oldFormat, new JsonSerializerOptions { WriteIndented = true }));

        var service = new ProjectMetadataService(_newFilePath, _oldFilePath);
        var projects = await service.GetAllProjects();

        // Verify migration
        Assert.True(File.Exists(_newFilePath), "New metadata file should exist");
        Assert.False(File.Exists(_oldFilePath), "Old labels file should be deleted");
        Assert.Contains("optiver", projects.Keys);
        Assert.Equal(2, projects["optiver"].Count);
        Assert.Equal("Optiver Australia Holdings Pty Limited", projects["optiver"]["ar24-3"].Label);
        Assert.Equal(ProjectLifecycleStatus.Open, projects["optiver"]["ar24-3"].Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }
}
