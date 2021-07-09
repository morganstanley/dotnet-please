using DotNetPlease.Services.Reporting.Abstractions;
using MediatR;

namespace DotNetPlease.Internal
{
    public abstract class CommandHandlerBase<TCommand> : AsyncRequestHandler<TCommand> where TCommand : IRequest
    {
        protected IReporter Reporter { get; }
        protected IMediator Mediator { get; }
        protected Workspace Workspace { get; }

        protected CommandHandlerBase(CommandHandlerDependencies dependencies)
        {
            Reporter = dependencies.Reporter;
            Mediator = dependencies.Mediator;
            Workspace = dependencies.Workspace;
        }
    }
}