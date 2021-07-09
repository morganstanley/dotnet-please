using DotNetPlease.Annotations;
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class FindStrayProjects
    {
        [Command("find-stray-projects", "Searches for projects NOT included in a solution.")]
        public class Command : IRequest
        {
            [Argument(0, CommandArguments.Projects.Description)]
            public string? Projects { get; set; }

            [Argument(1, CommandArguments.RequiredSolutionFileName.Description)]
            public string? SolutionFileName { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info($"Searching for stray projects");

                var solutionFileName = string.IsNullOrEmpty(command.SolutionFileName)
                    ? GetSolutionFromDirectory(Workspace.WorkingDirectory)
                    : Workspace.GetFullPath(command.SolutionFileName);

                var results = new List<string>();

                var projectsInSolution = GetProjectsFromSolution(solutionFileName).ToHashSet(PathComparer);

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