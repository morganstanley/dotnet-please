using FluentAssertions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public class FixProjectReferencesTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_removes_ProjectReference_items_that_point_to_nonexistent_projects(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            var referencedProjectFileName = GetFullPath("Missing/Project.csproj");
            AddProjectReference(projectFileName, referencedProjectFileName);
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);
            AddProjectToSolution(projectFileName, solutionFileName);

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("fix-project-references", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            var projectReference = FindProjectReference(projectFileName, referencedProjectFileName);
            projectReference.Should().BeNull();
        }

        [Theory, CombinatorialData]
        public async Task It_fixes_the_ProjectReference_if_the_project_was_moved_to_a_different_directory(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            var referencedProjectFileName = GetFullPath("Project2/Project2.csproj");
            var actualReferencedProjectFileName = GetFullPath("Foo/Bar/Project2.csproj");
            CreateProject(actualReferencedProjectFileName);
            AddProjectReference(projectFileName, referencedProjectFileName);
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);
            AddProjectToSolution(projectFileName, solutionFileName);
            AddProjectToSolution(actualReferencedProjectFileName, solutionFileName);

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("fix-project-references", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            FindProjectReference(projectFileName, referencedProjectFileName).Should().BeNull();
            FindProjectReference(projectFileName, actualReferencedProjectFileName).Should().NotBeNull();
        }

        public FixProjectReferencesTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}