// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Build.Globbing;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Rendering;
using MLS.Agent.Tools;
using WorkspaceServer.Packaging;
using static Microsoft.DotNet.Interactive.Rendering.PocketViewTags;

namespace WorkspaceServer.Kernel
{
    public static class CSharpKernelExtensions
    {
        public static CSharpKernel UseDefaultRendering(
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
using static {typeof(Microsoft.DotNet.Interactive.Kernel).FullName};
"))).Wait();

            return kernel;
        }

        public static CSharpKernel UseNugetDirective(
            this CSharpKernel kernel,
            Func<INativeAssemblyLoadHelper> getHelper = null)
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
                var addPackage = new AddPackage(package);

                addPackage.Handler = async context =>
                {
                    var result = restoreContext.AddPackagReference(
                        package.PackageName,
                        package.PackageVersion,
                        package.RestoreSources);

                    context.Complete();
                };

                await pipelineContext.HandlingKernel.SendAsync(addPackage);
            });

            kernel.AddDirective(command);

            var restore = new Command("#!nuget-restore")
            {
                Handler = CommandHandler.Create(async (KernelInvocationContext pipelineContext) =>
                {
                    var br = @"<br>";
                    var nugetRestoreDirective = new NugetRestoreDirective();

                    nugetRestoreDirective.Handler = async context =>
                    {
                        // Restoring packages messages ...
                        var message = $"Restoring packages :" + br;
                        foreach (var package in restoreContext.PackageReferences)
                        {
                            if (!string.IsNullOrEmpty(package.PackageName))
                            {
                                message += $"    Package: {package.PackageName}";
                                if (!string.IsNullOrWhiteSpace(package.PackageVersion))
                                {
                                    message += $", version = {package.PackageVersion}";
                                }
                                message += br;
                            }
                            if (!string.IsNullOrEmpty(package.RestoreSources))
                            {
                                message += $"    RestoreSources: {package.RestoreSources}" + br;
                            }
                        }

                        var key = message;

                        var displayed = new DisplayedValueProduced(new FormattedValue("text/html", message), context.Command, null, valueId: key);
                        context.Publish(displayed);

                        // Restore packages
                        var restorePackagesTask = restoreContext.Restore();
                        while (await Task.WhenAny(Task.Delay(500), restorePackagesTask) != restorePackagesTask)
                        {
                            message += ".";
                            context.Publish(new DisplayedValueUpdated(message, key, null, null));
                        }

                        var helper = getHelper?.Invoke();
                        if (helper != null)
                        {
                            kernel.RegisterForDisposal(helper);
                        }

                        var result = await restorePackagesTask;

                        if (result.Succeeded)
                        {
                            switch (result)
                            {
                                case PackageRestoreResult packageRestore:

                                    var nativeLibraryProbingPaths = packageRestore.NativeLibraryProbingPaths;
                                    helper?.SetNativeLibraryProbingPaths(nativeLibraryProbingPaths);

                                    var addedAssemblyPaths =
                                        packageRestore
                                            .ResolvedReferences
                                            .SelectMany(added => added.AssemblyPaths)
                                            .Distinct()
                                            .ToArray();

                                    if (helper != null)
                                    {
                                        foreach (var addedReference in packageRestore.ResolvedReferences)
                                        {
                                            helper.Handle(addedReference);
                                        }
                                    }

                                    kernel.AddScriptReferences(packageRestore.ResolvedReferences);

                                    foreach (var resolvedReference in packageRestore.ResolvedReferences)
                                    {
                                        context.Publish(
                                            new DisplayedValueProduced($"Successfully added reference to package {resolvedReference.PackageName}," + $"version {resolvedReference.PackageVersion}",
                                                                       context.Command, null, null));
                                        context.Publish(new PackageAdded(new AddPackage(resolvedReference)));

                                        // Load extensions
                                        var resolvedNugetPackageReference = await restoreContext.GetResolvedPackageReference(resolvedReference.PackageName);
                                        var nugetPackageDirectory = new FileSystemDirectoryAccessor(resolvedNugetPackageReference.PackageRoot);
                                        await context.HandlingKernel.SendAsync(
                                            new LoadExtensionsInDirectory(
                                                nugetPackageDirectory,
                                                addedAssemblyPaths));
                                    }
                                    break;

                                default:
                                    break;

                            }
                        }
                        else
                        {
                            var errors = $"{string.Join(Environment.NewLine, result.Errors)}";

                            switch (result)
                            {
                                case PackageRestoreResult packageRestore:
                                    foreach (var resolvedReference in packageRestore.ResolvedReferences)
                                    {
                                        if (string.IsNullOrEmpty(resolvedReference.PackageName))
                                        {
                                            context.Publish(new ErrorProduced($"Failed to apply RestoreSources {resolvedReference.RestoreSources}{Environment.NewLine}{errors}"));
                                        }
                                        else
                                        {
                                            context.Publish(new ErrorProduced($"Failed to add reference to package {resolvedReference.PackageName}{Environment.NewLine}{errors}"));
                                        }
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }

                        // Events for finished
                        context.Complete();
                    };

                    await pipelineContext.HandlingKernel.SendAsync(nugetRestoreDirective);
                })
            };

            kernel.AddDirective(restore);

            return kernel;
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
                Handler = CommandHandler.Create(async (ParseResult parseResult, KernelInvocationContext context) =>
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
                })
            };

            command.AddAlias("%who");

            return command;
        }
    }
}