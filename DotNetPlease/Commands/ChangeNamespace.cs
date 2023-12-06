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

using DotNetPlease.Annotations;
using DotNetPlease.Commands.Internal;
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class ChangeNamespace
    {
        [Command("change-namespace", "Renames projects matching the specified namespace (does not change code files, use a tool like ReSharper to do that)")]
        public class Command : IRequest
        {
            [Argument(0, "The old namespace"), Required]
            public string OldNamespace { get; set; } = null!;

            [Argument(1, "The new namespace"), Required]
            public string NewNamespace { get; set; } = null!;

            [Option("--force", "Force delete existing directories")]
            public bool Force { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Changing namespace \"{command.OldNamespace}\" to \"{command.NewNamespace}\"");

                MoveProjects.Command moveCommand;

                using (Reporter.BeginScope("Searching for projects to rename"))
                {
                    var moves = Workspace.ProjectFileNames
                        .Where(
                            projectFileName =>
                                IsFileNameInNamespace(
                                    GetProjectNameFromFileName(projectFileName),
                                    command.OldNamespace))
                        .Select(
                            projectFileName =>
                            {
                                var projectDirectory = Path.GetDirectoryName(projectFileName)!;
                                var projectDirectoryParent = Directory.GetParent(projectDirectory).FullName!;
                                var projectName = GetProjectNameFromFileName(projectFileName);
                                var newProjectName = ChangeFileNameWithNamespace(projectName, command.OldNamespace, command.NewNamespace);
                                var newProjectFileName = newProjectName + Path.GetExtension(projectFileName);
                                var newProjectDirectory =
                                    string.Equals(
                                        Path.GetFileName(projectDirectory),
                                        projectName,
                                        StringComparison.OrdinalIgnoreCase)
                                        ? Path.Combine(
                                            projectDirectoryParent,
                                            newProjectName)!
                                        : projectDirectory;
                                newProjectFileName = Path.Combine(newProjectDirectory, newProjectFileName);
                                return new MoveProjects.ProjectMoveItem(projectFileName, newProjectFileName);
                            })
                        .ToList();

                    moveCommand = new MoveProjects.Command(moves, command.Force);
                }

                if (moveCommand.Moves.Count == 0)
                {
                    Reporter.Success("Nothing to rename");
                    return Task.CompletedTask;
                }

                return Mediator.Send(moveCommand, cancellationToken);
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}