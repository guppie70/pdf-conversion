using PdfConversion.Models;
using Xunit;

namespace PdfConversion.Tests.Unit;

public class ProjectMetadataTests
{
    [Fact]
    public void ProjectMetadata_DefaultStatus_ShouldBeOpen()
    {
        var metadata = new ProjectMetadata
        {
            Label = "Test Project"
        };

        Assert.Equal(ProjectLifecycleStatus.Open, metadata.Status);
    }

    [Fact]
    public void ProjectLifecycleStatus_ShouldHaveFourValues()
    {
        var values = Enum.GetValues<ProjectLifecycleStatus>();
        Assert.Equal(4, values.Length);
        Assert.Contains(ProjectLifecycleStatus.Open, values);
        Assert.Contains(ProjectLifecycleStatus.InProgress, values);
        Assert.Contains(ProjectLifecycleStatus.Ready, values);
        Assert.Contains(ProjectLifecycleStatus.Parked, values);
    }
}
