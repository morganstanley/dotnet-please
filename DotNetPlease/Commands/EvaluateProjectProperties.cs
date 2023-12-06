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
using DotNetPlease.Constants;
using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using JetBrains.Annotations;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static DotNetPlease.Helpers.MSBuildHelper;


namespace DotNetPlease.Commands
{
    public static class EvaluateProjectProperties
    {
        [Command("evaluate-props", "Evaluates and lists all properties from the selected project(s)")]
        public class Command : IRequest
        {
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info("Evaluating project properties");

                var projectFileNames = Workspace.ProjectFileNames.ToList();

                foreach (var fileName in projectFileNames)
                {
                    EvaluateProjectProperties(fileName);
                }

                if (projectFileNames.Count == 0)
                {
                    Reporter.Info("Found no projects to evaluate");
                }

                return Task.CompletedTask;
            }

            private void EvaluateProjectProperties(string fileName)
            {
                var projectRelativePath = Workspace.GetRelativePath(fileName);
                using (Reporter.BeginScope($"Project: {projectRelativePath}"))
                {
                    var project = LoadProjectFromFile(fileName);
                    foreach (var prop in project.AllEvaluatedProperties.OrderBy(p => p.Name))
                    {
                        Reporter.Success(
                            prop.EvaluatedValue != prop.UnevaluatedValue
                                ? $"{prop.Name} = '{prop.UnevaluatedValue}' = '{prop.EvaluatedValue}'"
                                : $"{prop.Name} = '{prop.UnevaluatedValue}'");
                    }
                }
            }

            public CommandHandler(CommandHandlerDependencies dependencies) : base(dependencies)
            {
            }
        }
    }
}