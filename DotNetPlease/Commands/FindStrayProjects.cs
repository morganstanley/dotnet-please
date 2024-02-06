// Morgan Stanley makes this available to you under the Apache License,
// Version 2.0 (the "License"). You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0.
// 
// See the NOTICE file distributed with this work for additional information
// regarding copyright ownership. Unless required by applicable law or agreed
// to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
// or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetPlease.Annotations;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class FindStrayProjects
    {
        [Command("find-stray-projects", "Searches for projects NOT included in a solution.")]
        public class Command : IRequest
        {
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Searching for stray projects");

                if (Workspace.SolutionFileName == null)
                {
                    Reporter.Error("This command only works on solutions.");

                    return Task.CompletedTask;
                }

                var results = new List<string>();

                var projectsInSolution = GetProjectsFromSolution(Workspace.SolutionFileName).ToHashSet(PathComparer);

                var projectFileNames = GetProjectsFromDirectory(Workspace.WorkingDirectory, recursive: true);

                foreach (var fileName in projectFileNames)
                {
                    if (!projectsInSolution.Contains(fileName))
                    {
                        Reporter.Success(Workspace.GetRelativePath(fileName));
                        results.Add(fileName);
                    }
                }

                if (results.Count == 0)
                {
                    Reporter.Info("No stray projects were found");
                }

                return Task.CompletedTask;
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}