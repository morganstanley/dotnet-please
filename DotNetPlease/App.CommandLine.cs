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
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetPlease
{
    public partial class App
    {
        public string[] PreprocessArguments(string[] args)
        {
            // Try to construct a valid command if the original command name is provided as a sentence,
            // e.g. 'please consolidate packages' instead of 'please consolidate-packages'
            var commandNames = _rootCommand.Children
                .OfType<Command>()
                .Select(_ => _.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string? currentCommandName = null;
            string? candidate = null;
            var candidateLength = 0;
            for (var i = 0; i < args.Length; i++)
            {
                currentCommandName = currentCommandName == null ? args[i] : currentCommandName + "-" + args[i];
                if (commandNames.Contains(currentCommandName))
                {
                    candidate = currentCommandName;
                    candidateLength = i + 1;
                }
            }

            if (candidate != null)
            {
                var newArgs = new string[args.Length - candidateLength + 1];
                newArgs[0] = candidate;
                Array.Copy(args, candidateLength, newArgs, 1, args.Length - candidateLength);
                return newArgs;
            }

            return args;
        }

        private Parser BuildCommandLineParser()
        {
            return new CommandLineBuilder(_rootCommand)
                .ParseResponseFileAs(ResponseFileHandling.ParseArgsAsSpaceSeparated)
                .AddGlobalOption(_stageOption)
                .RegisterWithDotnetSuggest()
                .UseHelp()
                .UseParseErrorReporting()
                .Build();
        }

        private readonly RootCommand _rootCommand = new RootCommand();

        private readonly Option<bool> _stageOption =
            new Option<bool>(CommandOptions.Stage.Alias, CommandOptions.Stage.Description);

        private void BindCommands()
        {
            var commandTypes =
                Assembly.GetExecutingAssembly()
                    .ExportedTypes
                    .Where(t => t.GetCustomAttribute<CommandAttribute>() != null)
                    .ToList();
            foreach (var commandType in commandTypes)
            {
                BindCommand(commandType);
            }
        }

        private void BindCommand(Type commandType)
        {
            var commandAttribute = commandType.GetCustomAttribute<CommandAttribute>()!;

            var command = new Command(commandAttribute.Name, commandAttribute.Description);
            var commandBinding = new CommandBinding(command, commandType);

            foreach (var property in commandType.GetProperties())
            {
                var argumentAttribute = property.GetCustomAttribute<ArgumentAttribute>();
                if (argumentAttribute != null)
                {
                    var argument =
                        (Argument)Activator.CreateInstance(
                            typeof(Argument<>).MakeGenericType(property.PropertyType),
                            property.Name,
                            argumentAttribute.Description)!;
                    command.AddArgument(argument);
                    argument.Arity = property.GetCustomAttribute<RequiredAttribute>() != null
                        ? ArgumentArity.ExactlyOne
                        : ArgumentArity.ZeroOrOne;

                    commandBinding.Arguments.Add(new ArgumentBinding(argument, property));
                }

                var optionAttribute = property.GetCustomAttribute<OptionAttribute>();
                if (optionAttribute != null)
                {
                    var option = (Option)Activator.CreateInstance(
                        typeof(Option<>).MakeGenericType(property.PropertyType),
                        optionAttribute.Alias,
                        optionAttribute.Description)!;

                    command.AddOption(option);
                    option.IsRequired = property.GetCustomAttribute<RequiredAttribute>() != null;
                    commandBinding.Options.Add(new OptionBinding(option, property));
                }
            }

            command.Handler = new CommandHandler(commandBinding, this);
            _rootCommand.AddCommand(command);
        }

        private async Task<int> ExecuteCommand(InvocationContext context, CommandBinding commandBinding)
        {
            try
            {
                _workspace = new Workspace(
                    Directory.GetCurrentDirectory(),
                    ServiceProvider.GetRequiredService<IReporter>(),
                    isStaging: context.BindingContext.ParseResult.ValueForOption(_stageOption)
                );

                var command = Activator.CreateInstance(commandBinding.CommandType)!;

                foreach (var argumentBinding in commandBinding.Arguments)
                {
                    argumentBinding.Property.SetValue(
                        command,
                        context.BindingContext.ParseResult.ValueForArgument<object?>(argumentBinding.Argument));
                }

                foreach (var optionBinding in commandBinding.Options)
                {
                    optionBinding.Property.SetValue(
                        command,
                        context.BindingContext.ParseResult.ValueForOption<object?>(optionBinding.Option));
                }

                try
                {
                    await ServiceProvider.GetRequiredService<IMediator>().Send(command, CancellationToken.None);
                    return 0;
                }
                catch (Exception e)
                {
                    context.Console.Error.WriteLine(e.Message);
                    return e.HResult;
                }
            }
            finally
            {
                _workspace?.Dispose();
                _workspace = null;
            }
        }

        internal class CommandHandler : ICommandHandler
        {
            private readonly CommandBinding _commandBinding;
            private readonly App _app;

            public CommandHandler(CommandBinding commandBinding, App app)
            {
                _commandBinding = commandBinding;
                _app = app;
            }

            public Task<int> InvokeAsync(InvocationContext context)
            {
                return _app.ExecuteCommand(context, _commandBinding);
            }
        }

        internal class CommandBinding
        {
            public Command Command { get; }
            public Type CommandType { get; }

            public CommandBinding(Command command, Type commandType)
            {
                Command = command;
                CommandType = commandType;
            }

            public List<ArgumentBinding> Arguments { get; } = new List<ArgumentBinding>();

            public List<OptionBinding> Options { get; } = new List<OptionBinding>();
        }

        internal class ArgumentBinding
        {
            public Argument Argument { get; }
            public PropertyInfo Property { get; }

            public ArgumentBinding(Argument argument, PropertyInfo property)
            {
                Argument = argument;
                Property = property;
            }
        }

        internal class OptionBinding
        {
            public Option Option { get; }
            public PropertyInfo Property { get; }

            public OptionBinding(Option option, PropertyInfo property)
            {
                Option = option;
                Property = property;
            }
        }


    }
}