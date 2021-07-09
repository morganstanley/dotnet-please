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
            [Argument(0, CommandArguments.ProjectsOrSolution.Description)]
            public string? Projects { get; set; }
        }

        [UsedImplicitly]
        public class CommandHandler : CommandHandlerBase<Command>
        {
            protected override Task Handle(Command command, CancellationToken cancellationToken)
            {
                Reporter.Info("Evaluating project properties");

                var projectFileNames = Workspace.GetProjects(command.Projects);

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