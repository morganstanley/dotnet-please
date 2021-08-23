using System.Linq;
using System.Threading.Tasks;
using DotNetPlease.Constants;
using FluentAssertions;
using Microsoft.Build.Evaluation;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public class PullPackageVersionsTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_moves_package_versions_to_the_central_file(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            AddPackageReference(projectFileName, "Example.Package", "1.2.3");
            var packageVersionsFileName = GetFullPath("Dependencies.props");
            var packageVersions = new Project();
            packageVersions.Save(packageVersionsFileName);

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("pull-package-versions", "Dependencies.props", "Project1/Project1.csproj", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var project = LoadProjectFromFile(projectFileName);
            project.Xml.Items
                .Single(i => i.ItemType == "PackageReference" && i.Include == "Example.Package")
                .GetMetadataValue("Version")
                .Should().BeNull();

            packageVersions = LoadProjectFromFile(packageVersionsFileName);
            
            packageVersions.Xml.Items
                .Single(i => i.ItemType == "PackageVersion" && i.Include == "Example.Package")
                .Metadata
                .Single(m => m.Name == "Version")
                .Value
                .Should().Be("1.2.3");
        }

        public PullPackageVersionsTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}
