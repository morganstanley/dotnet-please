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
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Internal
{
    public partial class Workspace
    {
        public IEnumerable<ProjectInfo> ProjectInfos => _workspaceItems.Value.ProjectInfos;

        public IEnumerable<string> ProjectFileNames =>
            _workspaceItems.Value.ProjectInfos.Select(p => p.ProjectFileName);

        public string? SolutionFileName => _workspaceItems.Value.SolutionFileName;

        public List<Project> LoadProjects()
        {
            return MSBuildHelper.LoadProjects(ProjectFileNames);
        }

        public string? FindProject(string projectNameOrRelativePath)
        {
            if (SolutionFileName != null)
            {
                var solution = SolutionFile.Parse(SolutionFileName);
                var project = FindProjectInSolution(solution, projectNameOrRelativePath);
                
                return project?.AbsolutePath;
            }

            if (!IsProjectFileName(projectNameOrRelativePath))
            {
                return ProjectFileNames
                    .FirstOrDefault(
                        p => string.Equals(
                            projectNameOrRelativePath,
                            GetProjectNameFromFileName(p),

                            StringComparison.OrdinalIgnoreCase));
            }

            var projectFileName = GetFullPath(projectNameOrRelativePath);

            return File.Exists(projectFileName) ? projectFileName : null;

        }
    }
}