using DotNetPlease.Annotations;
using DotNetPlease.Commands.Internal;
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

            [Argument(2, CommandArguments.SolutionFileName.Description)]
            public string? SolutionFileName { get; set; }

            [Option("--force", "Force delete existing directories")]
            public bool Force { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Moving/renaming project \"{command.ProjectName}\" to \"{command.NewProjectName}\"");

                var projectFileName = Workspace.FindProject(command.ProjectName, command.SolutionFileName);

                if (projectFileName == null)
                {
                    throw new InvalidOperationException($"Project \"{command.ProjectName}\" not found");
                }

                var projectDirectory = Path.GetDirectoryName(projectFileName)!;
                var projectDirectoryParent = Path.GetDirectoryName(projectDirectory);
                var projectsInDirectory = GetProjectsFromDirectory(projectDirectory, recursive: false);
                if (projectsInDirectory.Count > 1)
                {
                    throw new InvalidOperationException($"Cannot move project because the directory contains multiple project files");
                }

                var existingProject = Workspace.FindProject(command.NewProjectName);
                if (existingProject != null)
                {
                    throw new InvalidOperationException($"The solution already contains a project at \"{Workspace.GetRelativePath(existingProject)}\"");
                }

                string newProjectFileName;

                if (!IsProjectFileName(command.NewProjectName))
                {
                    var newProjectDirectory = Path.Combine(projectDirectoryParent!, command.NewProjectName);
                    newProjectFileName = Path.Combine(newProjectDirectory, command.NewProjectName + Path.GetExtension(projectFileName));
                }
                else
                {
                    newProjectFileName = Workspace.GetFullPath(command.NewProjectName);
                }

                var moveCommand = new MoveProjects.Command(
                    new List<MoveProjects.ProjectMoveItem>
                    {
                        new MoveProjects.ProjectMoveItem(projectFileName, newProjectFileName)
                    },
                    command.SolutionFileName,
                    command.Force);

                return Mediator.Send(moveCommand, cancellationToken);

            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}