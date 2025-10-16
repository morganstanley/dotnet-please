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
using System.Linq;
using DotNetPlease.Helpers;
using DotNetPlease.Services.Reporting.Abstractions;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Internal
{
    public partial class Workspace : IDisposable
    {
        public Workspace(
            string? workspaceSpec = null,
            string? workingDirectory = null,
            IReporter? reporter = null,
            bool isDryRun = false)
        {
            WorkspaceSpec = workspaceSpec;
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
            Reporter = reporter ?? NullReporter.Singleton;
            IsDryRun = isDryRun;

            if (!Directory.Exists(WorkingDirectory))
            {
                Directory.CreateDirectory(WorkingDirectory);
            }

            LocateMSBuild();

            _workspaceItems = new Lazy<WorkspaceItems>(LoadWorkspaceItems);
        }

        public string? WorkspaceSpec { get; }
        public string RootDirectory => _workspaceItems.Value.RootDirectory;
        public string WorkingDirectory { get; }
        public IReporter Reporter { get; }
        public bool IsDryRun { get; }

        public string GetFullPath(string path) =>
            NormalizePathSeparators(Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(path, WorkingDirectory));

        public string GetRelativePath(string path) => NormalizePathSeparators(Path.GetRelativePath(WorkingDirectory, path));

        public void Dispose() { }

        private WorkspaceItems LoadWorkspaceItems()
        {
            var workspaceSpec = WorkspaceSpec;

            if (string.IsNullOrEmpty(workspaceSpec))
            {
                workspaceSpec = DetectWorkspace();

                Reporter.Info($"Detected workspace: {workspaceSpec}");
            }

            if (workspaceSpec.Contains('*'))
            {
                return new WorkspaceItems(
                    null,
                    GetProjectInfosFromGlob(workspaceSpec, WorkingDirectory, allowSolutions: true),
                    WorkingDirectory);
            }

            workspaceSpec = Path.GetFullPath(workspaceSpec, WorkingDirectory);

            if (File.Exists(workspaceSpec))
            {
                if (IsSolutionFileName(workspaceSpec))
                    return new WorkspaceItems(
                        workspaceSpec,
                        GetProjectsFromSolution(workspaceSpec).Select(p => new ProjectInfo(p, workspaceSpec)),
                        Path.GetDirectoryName(workspaceSpec)!);

                if (IsProjectFileName(workspaceSpec))
                    return new WorkspaceItems(
                        null,
                        new[] { new ProjectInfo(workspaceSpec) },
                        Path.GetDirectoryName(workspaceSpec)!);
            }

            return new WorkspaceItems(null, Enumerable.Empty<ProjectInfo>(), WorkingDirectory);
        }

        private string DetectWorkspace()
        {
            return FindSingleSolution()
                   ?? FindSingleProject()
                   ?? string.Join('|', KnownProjectFileExtensions.Select(ext => "**/*" + ext));

            string? FindSingleSolution()
            {
                var currentDirectory = WorkingDirectory;

                while (currentDirectory is not null)
                {
                    var solutions = GetSolutionsFromDirectory(currentDirectory);

                    if (solutions.Count == 1)
                        return solutions[0];

                    if (IsRootDirectory(currentDirectory))
                        return null;

                    currentDirectory = Path.GetDirectoryName(currentDirectory);
                }

                return null;
            }

            string? FindSingleProject()
            {
                var currentDirectory = WorkingDirectory;

                while (currentDirectory is not null)
                {
                    var projects = GetProjectsFromDirectory(currentDirectory, recursive: false);

                    if (projects.Count == 1)
                        return projects[0];

                    if (IsRootDirectory(currentDirectory))
                        return null;

                    currentDirectory = Path.GetDirectoryName(currentDirectory);
                }

                return null;
            }
        }

        private static bool IsRootDirectory(string path) => Directory.Exists(Path.Combine(path, ".git"));

        private readonly Lazy<WorkspaceItems> _workspaceItems;

        private class WorkspaceItems
        {
            public string? SolutionFileName { get; }
            public List<ProjectInfo> ProjectInfos { get; }
            public string RootDirectory { get; }

            public WorkspaceItems(string? solutionFileName, IEnumerable<ProjectInfo> projectInfos, string rootDirectory)
            {
                SolutionFileName = solutionFileName;
                ProjectInfos = projectInfos.ToList();
                RootDirectory = rootDirectory;
            }
        }
    }
}
