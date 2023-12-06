/*
 * Morgan Stanley makes this available to you under the Apache License,
 * Version 2.0 (the "License"). You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0.
 *
 * See the NOTICE file distributed with this work for additional information
 * regarding copyright ownership. Unless required by applicable law or agreed
 * to in writing, software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
 * or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

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

            TestOutputReporter.Messages
                .Should().Contain(new TestOutputReporter.MessageItem("Misc/Stray/Stray.csproj", MessageType.Success));
            TestOutputReporter.Messages
                .Should().NotContain(new TestOutputReporter.MessageItem("Project1/Project1.csproj", MessageType.Success));
        }

        public FindStrayProjectsTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}