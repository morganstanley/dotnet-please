using DotNetPlease.Services.Reporting.Abstractions;
using DotNetPlease.TestUtils;
using FluentAssertions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;

namespace DotNetPlease.Commands
{
    public class FindStrayProjectsTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_finds_projects_that_are_not_part_of_the_solution(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            var strayProjectFileName = GetFullPath("Misc/Stray/Stray.csproj");
            CreateProject(strayProjectFileName);
            var solutionFileName = GetFullPath("Test.sln");
            CreateSolution(solutionFileName);
            AddProjectToSolution(projectFileName, solutionFileName);

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("find-stray-projects");

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            Reporter.Messages
                .Should().Contain(new TestOutputReporter.MessageItem("Misc/Stray/Stray.csproj", MessageType.Success));
            Reporter.Messages
                .Should().NotContain(new TestOutputReporter.MessageItem("Project1/Project1.csproj", MessageType.Success));
        }

        public FindStrayProjectsTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}