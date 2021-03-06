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

using FluentAssertions;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.DotNetCliHelper;

namespace DotNetPlease.Commands
{
    public class RemoveJunkTests : TestFixtureBase
    {
        [Theory, CombinatorialData]
        public async Task It_removes_bin_and_obj_directories(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            var projectDirectory = Path.GetDirectoryName(projectFileName);
            Directory.CreateDirectory(projectDirectory + "/bin");
            Directory.CreateDirectory(projectDirectory + "/obj");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("remove-junk", "--bin", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            Directory.Exists(projectDirectory + "/bin").Should().BeFalse();
            Directory.Exists(projectDirectory + "/obj").Should().BeFalse();
        }

        [Theory, CombinatorialData]
        public async Task It_only_removes_bin_and_obj_from_project_directories(bool stage)
        {
            var projectFileName = GetFullPath("Project1/Project1.csproj");
            CreateProject(projectFileName);
            var projectDirectory = Path.GetDirectoryName(projectFileName);
            Directory.CreateDirectory(projectDirectory + "/assets/bin");
            File.WriteAllText(projectDirectory + "/assets/bin/readme.txt", "...");
            Directory.CreateDirectory(WorkingDirectory + "/bin");

            if (stage) CreateSnapshot();

            await RunAndAssertSuccess("remove-junk", "--bin", StageOption(stage));

            if (stage)
            {
                VerifySnapshot();
                return;
            }

            Directory.Exists(projectDirectory + "/assets/bin").Should().BeTrue();
            Directory.Exists(WorkingDirectory + "/bin").Should().BeTrue();
        }

        public RemoveJunkTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }
    }
}