// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using static Microsoft.DotNet.Interactive.Formatting.PocketViewTags;
using Microsoft.DotNet.Interactive.Extensions;

namespace Microsoft.DotNet.Interactive.CSharp
{
    public static class CSharpKernelExtensions
    {
        public static CSharpKernel UseDefaultFormatting(
            this CSharpKernel kernel)
        {
            Task.Run(() =>
                         kernel.SendAsync(
                         new SubmitCode($@"
using static {typeof(PocketViewTags).FullName};
using {typeof(PocketView).Namespace};
"))).Wait();

            return kernel;
        }

        public static CSharpKernel UseKernelHelpers(
            this CSharpKernel kernel)
        {
            Task.Run(() =>
                         kernel.SendAsync(
                             new SubmitCode($@"
using static {typeof(Kernel).FullName};
"))).Wait();

            return kernel;
        }

        public static CSharpKernel UseNugetDirective(
            this CSharpKernel kernel,
            Func<NativeAssemblyLoadHelper> getHelper = null)
        {
            var packageRefArg = new Argument<PackageReference>((SymbolResult result, out PackageReference reference) =>
                                                                        PackageReference.TryParse(result.Token.Value, out reference))
            {
                Name = "package"
            };

            var command = new Command("#r")
            {
                packageRefArg
            };

            var restoreContext = new PackageRestoreContext();

            command.Handler = CommandHandler.Create<PackageReference, KernelInvocationContext>(async (package, pipelineContext) =>
            {
                var addPackage = new AddPackage(package)
                {
                    Handler = async context =>
                    {
                        var added =
                            await Task.FromResult(
                                restoreContext.AddPackagReference(
                                    package.PackageName,
                                    package.PackageVersion,
                                    package.RestoreSources));

                        if (!added)
                        {
                            var errorMessage = $"{GenerateErrorMessage(package)}{Environment.NewLine}";
                            context.Publish(new ErrorProduced(errorMessage));
                        }

                        context.Complete();
                    }
                   };

                await pipelineContext.HandlingKernel.SendAsync(addPackage);
            });

            kernel.AddDirective(command);

            var restore = NugetExtensions.NugetRestoreCommand(kernel, restoreContext);

            kernel.AddDirective(restore);

            return kernel;

            static string GenerateErrorMessage(PackageReference package)
            {
                if (!string.IsNullOrEmpty(package.PackageName))
                {
                    if (!string.IsNullOrEmpty(package.PackageVersion))
                    {
                        return $"Package Reference already added: '{package.PackageName}, {package.PackageVersion}'";
                    }
                    else
                    {
                        return $"Package Reference already added: '{package.PackageName}'";
                    }
                }
                else if (!string.IsNullOrEmpty(package.RestoreSources))
                {
                    return $"Package RestoreSource already added: '{package.RestoreSources}'";
                }
                else
                {
                    return $"Invalid Package specification: '{package.PackageName}'";
                }
            }
        }

        public static CSharpKernel UseWho(this CSharpKernel kernel)
        {
            kernel.AddDirective(who_and_whos());

            Formatter<CurrentVariables>.Register((variables, writer) =>
            {
                PocketView output = null;

                if (variables.Detailed)
                {
                    output = table(
                        thead(
                            tr(
                                th("Variable"),
                                th("Type"),
                                th("Value"))),
                        tbody(
                            variables.Select(v =>
                                 tr(
                                     td(v.Name),
                                     td(v.Type),
                                     td(v.Value.ToDisplayString())
                                 ))));
                }
                else
                {
                    output = div(variables.Select(v => v.Name + "\t "));
                }

                output.WriteTo(writer, HtmlEncoder.Default);
            }, "text/html");

            return kernel;
        }

        private static Command who_and_whos()
        {
            var command = new Command("%whos")
            {
                Handler = CommandHandler.Create((ParseResult parseResult, KernelInvocationContext context) =>
                {
                    var alias = parseResult.CommandResult.Token.Value;

                    var detailed = alias == "%whos";

                    if (context.Command is SubmitCode &&
                        context.HandlingKernel is CSharpKernel kernel)
                    {
                        var variables = kernel.ScriptState.Variables;

                        var currentVariables = new CurrentVariables(
                            variables, 
                            detailed);

                        var html = currentVariables
                            .ToDisplayString(HtmlFormatter.MimeType);

                        context.Publish(
                            new DisplayedValueProduced(
                                html,
                                context.Command,
                                new[]
                                {
                                    new FormattedValue(
                                        HtmlFormatter.MimeType,
                                        html)
                                }));
                    }

                    return Task.CompletedTask;
                })
            };

            command.AddAlias("%who");

            return command;
        }
    }
}