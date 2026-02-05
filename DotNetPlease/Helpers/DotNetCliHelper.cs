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

namespace DotNetPlease.Helpers
{
    public static class DotNetCliHelper
    {
        public static void CreateSolution(string solutionFileName)
        {
            var outputDirectory = Path.GetDirectoryName(solutionFileName);
            var solutionName = Path.ChangeExtension(Path.GetFileName(solutionFileName), null);

            ProcessHelper.Run(
                "dotnet",
                $"new sln --format sln --name \"{solutionName}\" --output \"{outputDirectory}\"",
                _environmentOverrides);
        }

        public static void CreateProject(string projectFileName, string projectTemplateName = "classlib")
        {
            var projectName = Path.ChangeExtension(Path.GetFileName(projectFileName), null);
            var projectDirectory = Path.GetDirectoryName(projectFileName);

            ProcessHelper.Run(
                "dotnet",
                $"new {projectTemplateName} --name \"{projectName}\" --output \"{projectDirectory}\"",
                _environmentOverrides);
        }

        public static void AddProjectToSolution(string projectFileName, string solutionFileName)
        {
            ProcessHelper.Run("dotnet", $"sln \"{solutionFileName}\" add \"{projectFileName}\"", _environmentOverrides);
        }

        public static void AddProjectToSolution(string projectFileName, string solutionFileName, string solutionFolder)
        {
            Directory.CreateDirectory(solutionFolder);

            ProcessHelper.Run(
                "dotnet",
                $"sln \"{solutionFileName}\" add \"{projectFileName}\" --solution-folder \"{solutionFolder}\"",
                _environmentOverrides);
        }

        private static readonly Dictionary<string, string?> _environmentOverrides = new()
        {
            { "MSBUILD_EXE_PATH", null },
            { "MSBuildLoadMicrosoftTargetsReadOnly", null },
            { "MSBuildSDKsPath", null },
        };
    }
}
