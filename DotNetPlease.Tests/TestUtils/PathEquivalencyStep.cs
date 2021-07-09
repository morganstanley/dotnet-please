using DotNetPlease.Helpers;
using FluentAssertions.Equivalency;

namespace DotNetPlease.TestUtils
{
    public class PathEquivalencyStep : IEquivalencyStep
    {
        public bool CanHandle(IEquivalencyValidationContext context, IEquivalencyAssertionOptions config)
        {
            return (context.Subject is null || context.Subject is string)
                   && (context.Expectation is null || context.Expectation is string);
        }

        public bool Handle(IEquivalencyValidationContext context, IEquivalencyValidator parent, IEquivalencyAssertionOptions config)
        {
            return FileSystemHelper.PathComparer.Equals((string)context.Subject, (string)context.Expectation);
        }
    }

    public static class EquivalencyAssertionOptionsPathExtensions
    {
        public static EquivalencyAssertionOptions<string> CompareAsFileName(this EquivalencyAssertionOptions<string> options)
        {
            return options.Using(new PathEquivalencyStep());
        }
    }
}