using FluentAssertions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

// ReSharper disable StringLiteralTypo

namespace DotNetPlease.Commands
{
    public class MoveProjectTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_moves_project_file_and_directory(bool stage)
        {
            var originalProjectFileName = GetFullPath("OriginalProjectDirectory/OriginalProjectName.csproj");
            CreateProject(originalProjectFileName);
            var newProjectFileName = GetFullPath("NewProjectDirectory/NewProjectName.csproj");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "move-project",
                "OriginalProjectDirectory/OriginalProjectName.csproj",
                "NewProjectDirectory/NewProjectName.csproj",
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            File.Exists(newProjectFileName).Should().BeTrue();
            File.Exists(originalProjectFileName).Should().BeFalse();
            Directory.Exists(Path.GetDirectoryName(originalProjectFileName)).Should().BeFalse();
        }

        [Theory, CombinatorialData]
        public async Task It_replaces_the_project_in_the_solution_file(bool stage)
        {
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);
            var originalProjectFileName = GetFullPath("OriginalProjectDirectory/OriginalProjectName.csproj");
            CreateProject(originalProjectFileName);
            AddProjectToSolution(originalProjectFileName, solutionFileName);
            var newProjectFileName = GetFullPath("NewProjectDirectory/NewProjectName.csproj");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "move-project",
                "OriginalProjectDirectory/OriginalProjectName.csproj",
                "NewProjectDirectory/NewProjectName.csproj",
                "Test.sln",
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var solution = LoadAndValidateSolution(solutionFileName);
            var project = solution.ProjectsInOrder.FirstOrDefault(p => IsSamePath(p.AbsolutePath, newProjectFileName));
            project.Should().NotBeNull();
            var oldProject =
                solution.ProjectsInOrder.FirstOrDefault(p => IsSamePath(p.AbsolutePath, originalProjectFileName));
            oldProject.Should().BeNull();
        }

        [Theory, CombinatorialData]
        public async Task It_fixes_ProjectReferences(bool stage)
        {
            var originalProjectFileName = GetFullPath("OriginalProject/OriginalProject.csproj");
            CreateProject(originalProjectFileName);
            var referencedProjectFileName = GetFullPath("OtherProject/OtherProject.csproj");
            CreateProject(referencedProjectFileName);
            AddProjectReference(originalProjectFileName, referencedProjectFileName);
            AddProjectReference(referencedProjectFileName, originalProjectFileName);
            var newProjectFileName = GetFullPath("NewProjectDirectory/NewProjectName.csproj");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess(
                "move-project",
                "OriginalProject/OriginalProject.csproj",
                "NewProjectDirectory/NewProjectName.csproj",
                StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            FindProjectReference(newProjectFileName, referencedProjectFileName).Should().NotBeNull();
            FindProjectReference(referencedProjectFileName, newProjectFileName).Should().NotBeNull();
            FindProjectReference(referencedProjectFileName, originalProjectFileName).Should().BeNull();
        }

        public MoveProjectTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}