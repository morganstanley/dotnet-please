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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNetPlease.Annotations;
using DotNetPlease.Commands.Internal;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class MoveProject
    {
        [Command("move-project", "Move or rename a project within a solution, including its folder")]
        public class Command : IRequest
        {
            [Argument(0, "The original project reference (project name in solution or relative path to project file)")]
            public string ProjectName { get; set; } = null!;

            [Argument(1, "The new project reference (name or relative path)")]
            public string NewProjectName { get; set; } = null!;

            [Option("--force", "Force delete existing directories")]
            public bool Force { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            public override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Moving/renaming project \"{command.ProjectName}\" to \"{command.NewProjectName}\"");

                var projectFileName = Workspace.FindProject(command.ProjectName)
                                      ?? throw new InvalidOperationException($"Project \"{command.ProjectName}\" not found");

                var projectDirectory = Path.GetDirectoryName(projectFileName)!;
                var projectDirectoryParent = Path.GetDirectoryName(projectDirectory);
                var projectsInDirectory = GetProjectsFromDirectory(projectDirectory, recursive: false);

                if (projectsInDirectory.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Cannot move project because the directory contains multiple project files");
                }

                var existingProject = Workspace.FindProject(command.NewProjectName);

                if (existingProject != null)
                {
                    throw new InvalidOperationException(
                        $"The solution already contains a project at \"{Workspace.GetRelativePath(existingProject)}\"");
                }

                string newProjectFileName;

                if (!IsProjectFileName(command.NewProjectName))
                {
                    var newProjectDirectory = Path.Combine(projectDirectoryParent!, command.NewProjectName);

                    newProjectFileName = Path.Combine(
                        newProjectDirectory,
                        command.NewProjectName + Path.GetExtension(projectFileName));
                }
                else
                {
                    newProjectFileName = Workspace.GetFullPath(command.NewProjectName);
                }

                var moveCommand = new MoveProjects.Command(
                    new List<MoveProjects.ProjectMoveItem>
                    {
                        new(projectFileName, newProjectFileName)
                    },
                    command.Force);

                return Mediator.Send(moveCommand, cancellationToken);
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies) { }
        }
    }
}
