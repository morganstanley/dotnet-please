
using System.Diagnostics;

namespace DotNetPlease.Services.Reporting.Abstractions
{
    public static class ReporterExtensions
    {
        public static void Info(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Information);

        [Conditional("DEBUG")]
        public static void Debug(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Debug);

        public static void Success(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Success);

        public static void Warning(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Warning);

        public static void Error(this IReporter reporter, string message)
            => reporter.Message(message, MessageType.Error);

    }
}