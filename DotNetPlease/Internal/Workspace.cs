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

using DotNetPlease.Services.Reporting.Abstractions;
using System;
using System.IO;
using static DotNetPlease.Helpers.FileSystemHelper;
using static DotNetPlease.Helpers.MSBuildHelper;

namespace DotNetPlease.Internal
{
    public partial class Workspace : IDisposable
    {
        public Workspace(string? workingDirectory = null, IReporter? reporter = null, bool isStaging = false)
        {
            Reporter = reporter ?? NullReporter.Singleton;
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
            IsStaging = isStaging;

            if (!Directory.Exists(WorkingDirectory))
            {
                Directory.CreateDirectory(WorkingDirectory);
            }

            LocateMSBuild();
        }

        public IReporter Reporter { get; }
        public string WorkingDirectory { get; }
        public bool IsStaging { get; }

        public string GetFullPath(string path) => NormalizePath(Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(path, WorkingDirectory));
        public string GetRelativePath(string path) => NormalizePath(Path.GetRelativePath(WorkingDirectory, path));

        public void Dispose()
        {
        }
    }
}