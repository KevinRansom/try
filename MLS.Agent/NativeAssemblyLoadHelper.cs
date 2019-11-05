// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MLS.Agent.Tools;
using Pocket;
using WorkspaceServer.Kernel;

namespace MLS.Agent
{
    public class NativeAssemblyLoadHelper : INativeAssemblyLoadHelper
    {
        // Todo, figure out what we need!!!!!!!
        private static readonly HashSet<DirectoryInfo> allProbingPaths = new HashSet<DirectoryInfo>();
        private readonly HashSet<DirectoryInfo> _probingPaths = new HashSet<DirectoryInfo>();

        private readonly Dictionary<string, AssemblyDependencyResolver> _resolvers =
            new Dictionary<string, AssemblyDependencyResolver>(StringComparer.OrdinalIgnoreCase);

        public NativeAssemblyLoadHelper()
        {
            // var currentDomain = AppDomain.CurrentDomain;
            // currentDomain.AssemblyLoad += DoTheThing;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;
        }

        public void SetNativeDllProbingPaths(
            FileInfo assemblyPath,
            IReadOnlyList<DirectoryInfo> probingPaths)
        {
            _probingPaths.UnionWith(probingPaths);
            allProbingPaths.UnionWith(probingPaths);
        }

        public void Handle(FileInfo assemblyFile)
        {
            foreach (var dir in _probingPaths)
            {
                Logger.Log.Info($"Probing: {dir}");

                // Okay this is deps.json based resolution
                if (assemblyFile.FullName.Contains(dir.FullName))
                {
                    var resolver = new AssemblyDependencyResolver(assemblyFile.FullName);
                    _resolvers.TryAdd(assemblyFile.FullName, resolver);
                }
            }
        }

        // AssemblyLoad event is notification only
        // note the void return value:
        //public delegate void AssemblyLoadEventHandler([Nullable((byte)2)] object sender, AssemblyLoadEventArgs args);
        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            // Dynamic dll's have no location so bail.
            if (args.LoadedAssembly.IsDynamic ||
                string.IsNullOrWhiteSpace(args.LoadedAssembly.Location))
            {
                return;
            }

            Logger.Log.Info("OnAssemblyLoad: {location}", args.LoadedAssembly.Location);
            Console.WriteLine($"OnAssemblyLoad: {args.LoadedAssembly.Location}");

            // Okay we have loaded an assembly let's add a dll resolve handler for it.
            NativeLibrary.SetDllImportResolver(
                args.LoadedAssembly,
                (libraryName, assembly, searchPath) =>
                {
/* Probe all probing paths

                    if (_resolvers.TryGetValue(
                        args.LoadedAssembly.Location,
                        out var resolver))
                    {
*/
// Let's look into lots of kewl places to find this dll.
                        // Why not start with the _probing paths list given at the start.
                        Logger.Log.Info("OnAssemblyLoad: {location}", args.LoadedAssembly.Location);
                        Console.WriteLine($"SetDllImportResolver: {args.LoadedAssembly.Location}");

                        foreach (var path in allProbingPaths)
                        {
                            try
                            {
                                var dll =
                                path.Subdirectory("runtimes")
                                    .Subdirectory("win-x64")
                                    .GetFiles($"{libraryName}.dll", SearchOption.AllDirectories);

                                if (dll.Length == 1)
                                {
                                    var ptr = IntPtr.Zero;
                                    ptr = NativeLibrary.Load(dll[0].FullName);
                                    return ptr;
                                }
                            }
                            catch (Exception)
                            {
                                //Console.WriteLine(e);
                            }
                        }
/* Probe all probing paths
                    }
*/
                    return IntPtr.Zero;
                });
        }


        private IntPtr OnResolvingUnmanagedDll(Assembly assembly, string name)
        {
            return IntPtr.Zero;
        }

        //
        // private AssemblyLoadEventHandler AssemblyLoaded(FileInfo assembly)
        // {
        //     return (_, args) =>
        //     {
        //         if (args.LoadedAssembly.Location == assembly.FullName)
        //         {
        //             NativeLibrary.SetDllImportResolver(args.LoadedAssembly, Resolve);
        //         }
        //     };
        // }
        //
        // private IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        // {
        //     var path = _resolver.ResolveUnmanagedDllToPath(libraryName);
        //
        //     return NativeLibrary.Load(path);
        // }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;

            //
            // _resolvers.Clear();
        }
    }
}