using DotNetPlease.Services.Reporting.Abstractions;
using MediatR;

namespace DotNetPlease.Internal
{
    public class CommandHandlerDependencies
    {
        public IReporter Reporter { get; }
        public Workspace Workspace { get; }
        public IMediator Mediator { get; }

        public CommandHandlerDependencies(
            IMediator mediator,
            IReporter reporter,
            Workspace workspace)
        {
            Mediator = mediator;
            Reporter = reporter;
            Workspace = workspace;
        }
    }
}