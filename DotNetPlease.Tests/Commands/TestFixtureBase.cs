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

using DotNetPlease.Constants;
using DotNetPlease.Helpers;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using DotNetPlease.TestUtils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;
using static DotNetPlease.Helpers.FileSystemHelper;

namespace DotNetPlease.Commands
{
    // Tests that change the current directory for the process must not run in parallel.
    [Collection("cwd")]
    public class TestFixtureBase : IDisposable
    {
        protected readonly string WorkingDirectory;

        protected readonly TestOutputReporter TestOutputReporter;
        protected readonly TestOutputConsole TestOutputConsole;

        public TestFixtureBase(ITestOutputHelper testOutputHelper)
        {
            MSBuildHelper.LocateMSBuild();
            TestOutputReporter = new TestOutputReporter(testOutputHelper);
            TestOutputConsole = new TestOutputConsole(testOutputHelper);
            WorkingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(WorkingDirectory);
        }

        private string? _snapshotDirectory;

        protected void CreateSnapshot()
        {
            var files = GetFileNamesFromGlob("**/*", WorkingDirectory);
            _snapshotDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            foreach (var fileName in files)
            {
                if (File.Exists(fileName))
                {
                    var relativePath = GetRelativePath(fileName);
                    var newPath = Path.GetFullPath(relativePath, _snapshotDirectory);
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                    File.Copy(fileName, newPath);
                }
            }
        }

        protected void VerifySnapshot()
        {
            if (_snapshotDirectory == null) return;
            var expected = Hash(_snapshotDirectory);
            var actual = Hash(WorkingDirectory);

            actual.Should().BeEquivalentTo(expected);

            static Dictionary<string, byte[]> Hash(string rootDirectory)
            {
                var files = GetFileNamesFromGlob("**/*", rootDirectory);
                var hasher = new SHA256Managed();
                hasher.Initialize();
                var result = new Dictionary<string, byte[]>();
                foreach (var fileName in files)
                {
                    var relativePath = Path.GetRelativePath(rootDirectory, fileName).ToLower();
                    using var stream = File.OpenRead(fileName);
                    result[relativePath] = hasher.ComputeHash(stream);
                }

                return result;
            }
        }

        protected string GetFullPath(string path) => Path.GetFullPath(path, WorkingDirectory);

        protected string GetRelativePath(string path) => Path.GetRelativePath(WorkingDirectory, path);

        protected async Task RunAndAssertSuccess(params string[] args)
        {
            var oldWorkingDirectory = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(WorkingDirectory);

                using var app = new App(
                    sc =>
                    {
                        sc.Replace(new ServiceDescriptor(typeof(IReporter), TestOutputReporter));
                        sc.Replace(new ServiceDescriptor(typeof(IConsole), TestOutputConsole));

                        return sc;
                    });

                var exitCode = await app.ExecuteAsync(
                    app.PreprocessArguments(args.Where(a => !string.IsNullOrEmpty(a)).ToArray()));

                exitCode.Should().Be(0);
            }
            finally
            {
                Directory.SetCurrentDirectory(oldWorkingDirectory);
            }
        }

        public void Dispose()
        {
            (TestOutputReporter as IDisposable)?.Dispose();
        }

        protected static string DryRunOption(bool dryRun) => dryRun ? CommandOptions.DryRun.Alias : "";
    }
}