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