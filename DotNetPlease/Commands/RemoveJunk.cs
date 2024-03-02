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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNetPlease.Annotations;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Commands
{
    public static class RemoveJunk
    {
        [Command("remove-junk", "Removes junk from the solution folder recursively")]
        public class Command : IRequest
        {
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
                var context = new Context(command, Workspace.SolutionFileName);

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

                foreach (var dir in GetHiddenVSFiles(context.SolutionFileName, ".suo"))
                {
                    TryDeleteFile(dir, context);
                }
            }

            private void RemoveTestStore(Context context)
            {
                if (context.SolutionFileName == null)
                {
                    Reporter.Warning("Test Store can only be removed from solutions");
                    return;
                }

                foreach (var dir in GetHiddenVSDirectories(context.SolutionFileName, "TestStore"))
                {
                    TryDeleteDirectory(dir, context);
                }
            }

            private void RemoveBinFolders(Context context)
            {
                foreach (var projectFileName in Workspace.ProjectFileNames)
                {
                    var projectDirectory = Path.GetDirectoryName(projectFileName)!;
                    TryDeleteDirectory(Path.Combine(projectDirectory!, "bin"), context);
                    TryDeleteDirectory(Path.Combine(projectDirectory!, "obj"), context);
                }
            }

            private void TryDeleteDirectory(string path, Context context)
            {
                if (Workspace.SafeDeleteDirectory(path))
                {
                    context.FilesRemoved.Add(path);
                }
            }

            private void TryDeleteFile(string path, Context context)
            {
                if (Workspace.SafeDeleteFile(path))
                {
                    context.FilesRemoved.Add(path);
                }
            }

            private class Context
            {
                public Command Command { get; }
                public string? SolutionFileName { get; }
                public HashSet<string> FilesRemoved { get; } = new();

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