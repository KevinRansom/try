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


namespace Microsoft.DotNet.Interactive.Extensions
{
    public static class NugetExtensions
    {
        private static string InstallingPackageMessage(PackageReference package)
        {
            string message = null;

            if (!string.IsNullOrEmpty(package.PackageName))
            {
                message = $"Installing package {package.PackageName}";
                if (!string.IsNullOrWhiteSpace(package.PackageVersion))
                {
                    message += $", version {package.PackageVersion}";
                }
            }
            else if (!string.IsNullOrEmpty(package.RestoreSources))
            {
                message += $"    RestoreSources: {package.RestoreSources}" + br;
            }
            return message;
        }

        public static Command NugetRestoreCommand(this ScriptingKernelBase kernel, PackageRestoreContext restoreContext)
        {
            return new Command("#!nuget-restore")
            {
                Handler = CommandHandler.Create(async (KernelInvocationContext pipelineContext) =>
                {
                    var nugetRestoreDirective = new RestoreNugetDirective();

                    nugetRestoreDirective.Handler = async context =>
                    {
                        var messages = new Dictionary<string, string>();
                        foreach (var package in restoreContext.PackageReferences)
                        {
                            var key = InstallingPackageMessage(package);
                            if (key == null)
                            {
                                context.Publish(new ErrorProduced($"Invalid Package Id: '{package.PackageName}'{Environment.NewLine}"));
                            }
                            else
                            {
                                var message = key + "...";
                                var displayed = new DisplayedValueProduced(message, context.Command, null, valueId: key);
                                context.Publish(displayed);
                                messages.Add(key, message);
                            }
                        }

                        // Restore packages
                        var restorePackagesTask = restoreContext.Restore();
                        while (await Task.WhenAny(Task.Delay(500), restorePackagesTask) != restorePackagesTask)
                        {
                            foreach (var key in messages.Keys.ToArray())
                            {
                                var message = messages[key] + ".";
                                context.Publish(new DisplayedValueUpdated(message, key, null, null));
                                messages[key] = message;
                            }
                        }

                        var helper = kernel.NativeAssemblyLoadHelper;

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
                                        string message;
                                        string key = InstallingPackageMessage(resolvedReference);
                                        if (messages.TryGetValue(key, out message))
                                        {
                                            context.Publish(new DisplayedValueUpdated(message + " done!", key, null, null));
                                            messages[key] = message;
                                        }

                                        context.Publish(new PackageAdded(new AddPackage(resolvedReference)));

                                        // Load extensions
                                        await context.HandlingKernel.SendAsync(
                                            new LoadExtensionsInDirectory(
                                                resolvedReference.PackageRoot,
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
        }
    }
}
