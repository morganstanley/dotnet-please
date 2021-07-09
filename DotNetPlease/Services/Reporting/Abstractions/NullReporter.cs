using DotNetPlease.Utilities;
using System;

namespace DotNetPlease.Services.Reporting.Abstractions
{
    public class NullReporter : IReporter
    {
        private NullReporter()
        {
        }

        public static readonly NullReporter Singleton = new NullReporter();

        public IDisposable BeginScope(string scope)
        {
            return new NullDisposable();
        }

        public void Message(string message, MessageType type = MessageType.Information)
        {
        }
    }
}