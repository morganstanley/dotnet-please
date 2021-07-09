using DotNetPlease.Annotations;
using DotNetPlease.Constants;
using DotNetPlease.Helpers;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetPlease.Commands
{
    public static class RemoveJunk
    {
        [Command("remove-junk", "Removes junk from the solution folder recursively")]
        public class Command : IRequest
        {
            [Argument(0, CommandArguments.SolutionFileName.Description)]
            public string? SolutionFileName { get; set; }

            [Option("--bin", "Remove bin and obj folders")]
            public bool RemoveBin { get; set; }

            [Option("--testStore", "Remove the hidden TestStore folder")]
            public bool RemoveTestStore { get; set; }

            [Option("--suo", "Remove the hidden .suo file")]
            public bool RemoveSuo { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                var context = new Context(command, Workspace.FindSolutionFileName(command.SolutionFileName));

                if (command.RemoveBin)
                {
                    RemoveBinFolders(context);
                }
                if (command.RemoveTestStore)
                {
                    RemoveTestStore(context);
                }
                if (command.RemoveSuo)
                {
                    RemoveSuo(context);
                }

                if (context.FilesRemoved.Count == 0)
                {
                    Reporter.Success("Nothing to remove");
                }

                return Task.CompletedTask;
            }

            private void RemoveSuo(Context context)
            {
                if (context.SolutionFileName == null)
                {
                    Reporter.Warning(".suo files can only be removed from solutions");
                    return;
                }

                var path = Path.Combine(MSBuildHelper.GetHiddenVsDirectory(context.SolutionFileName), "v16/.suo");
                TryDeleteFile(path, context);
            }

            private void RemoveTestStore(Context context)
            {
                if (context.SolutionFileName == null)
                {
                    Reporter.Warning("Test Store can only be removed from solutions");
                    return;
                }
                var path = Path.Combine(MSBuildHelper.GetHiddenVsDirectory(context.SolutionFileName), $"v16/TestStore");
                TryDeleteDirectory(path, context);
            }

            private void RemoveBinFolders(Context context)
            {
                foreach (var projectFileName in Workspace.GetProjects(context.SolutionFileName))
                {
                    var projectDirectory = Path.GetDirectoryName(projectFileName)!;
                    TryDeleteDirectory(Path.Combine(projectDirectory!, "bin"), context);
                    TryDeleteDirectory(Path.Combine(projectDirectory!, "obj"), context);
                }
            }

            private void TryDeleteDirectory(string path, Context context)
            {
                if (Workspace.TryDeleteDirectory(path))
                {
                    context.FilesRemoved.Add(path);
                }
            }

            private void TryDeleteFile(string path, Context context)
            {
                if (Workspace.TryDeleteFile(path))
                {
                    context.FilesRemoved.Add(path);
                }
            }

            private class Context
            {
                public Command Command { get; }
                public string? SolutionFileName { get; }
                public HashSet<string> FilesRemoved { get; } = new HashSet<string>();

                public Context(Command command, string? solutionFileName)
                {
                    Command = command;
                    SolutionFileName = solutionFileName;
                }
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}