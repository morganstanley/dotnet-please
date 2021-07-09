﻿using DotNetPlease.Internal;
using DotNetPlease.Services.Reporting.Abstractions;
using DotNetPlease.Services.Reporting.Console;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace DotNetPlease
{
    public partial class App : IDisposable
    {
        public IServiceProvider ServiceProvider { get; }

        public App() : this(null)
        {
        }

        internal App(Func<IServiceCollection, IServiceCollection>? overrideServices)
        {
            ServiceProvider = BuildServiceProvider(overrideServices);
            BindCommands();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMediatR(typeof(Program).Assembly)
                .AddTransient<CommandHandlerDependencies>()
                .AddSingleton<IConsole, SystemConsole>();
            services.TryAddSingleton<IReporter, SystemConsoleReporter>();
            services.TryAddTransient<Workspace>(p => _workspace!);
        }

        private IServiceProvider BuildServiceProvider(Func<IServiceCollection, IServiceCollection>? overrideServices)
        {
            IServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            if (overrideServices != null)
            {
                serviceCollection = overrideServices(serviceCollection);
            }
            return serviceCollection.BuildServiceProvider();
        }

        private Workspace? _workspace;

        public async Task<int> ExecuteAsync(params string[] args)
        {
            using var scope = ServiceProvider.CreateScope();
            var parser = BuildCommandLineParser();
            var cursorVisible = !Console.IsOutputRedirected && Console.CursorVisible;
            if (!Console.IsOutputRedirected)
            {
                Console.CursorVisible = false;
            }
            try
            {
                return await parser.InvokeAsync(args, scope.ServiceProvider.GetRequiredService<IConsole>());
            }
            finally
            {
                if (!Console.IsOutputRedirected)
                {
                    Console.CursorVisible = cursorVisible;
                }
            }
        }

        public void Dispose()
        {
            (ServiceProvider as IDisposable)?.Dispose();
            _workspace?.Dispose();
        }
    }
}