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
                    if (!Workspace.IsDryRun)
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