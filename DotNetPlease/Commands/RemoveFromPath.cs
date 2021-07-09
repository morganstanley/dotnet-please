using DotNetPlease.Annotations;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.FileSystemHelper;

namespace DotNetPlease.Commands
{
    public static class RemoveFromPath
    {
        [Command("remove-from-path", "Removes a directory from the PATH environment variable of the current user.")]
        public class Command : IRequest
        {
            [Argument(0, "The path to the directory, relative to the working directory (defaults to \".\")")]
            public string? Directory { get; set; }

            [Option("--self", "Remove the directory of DotNetPlease.exe from the PATH variable (and ignore the directory argument)")]
            public bool RemoveSelf { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }

            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                var path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                var paths = path.Split(Path.PathSeparator).Select(p => p.Trim()).ToHashSet(PathComparer);
                var directory = command.RemoveSelf
                    ? Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!
                    : Workspace.GetFullPath(command.Directory ?? ".");

                if (paths.RemoveWhere(p => IsSamePath(p, directory)) > 0)
                {
                    path = string.Join(Path.PathSeparator, paths);
                    if (!Workspace.IsStaging)
                    {
                        Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.User);
                        Reporter.Success($"Removed \"{directory}\" from the PATH variable.");
                    }
                    else
                    {
                        Reporter.Success($"Remove \"{directory}\" from the PATH variable.");
                    }
                }
                else
                {
                    Reporter.Success("Nothing to update.");
                }

                return Task.CompletedTask;
            }
        }
    }
}