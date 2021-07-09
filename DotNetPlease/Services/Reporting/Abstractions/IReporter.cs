using System;

namespace DotNetPlease.Services.Reporting.Abstractions
{
    public interface IReporter
    {
        IDisposable BeginScope(string scope);
        void Message(string message, MessageType type = MessageType.Information);
    }
}