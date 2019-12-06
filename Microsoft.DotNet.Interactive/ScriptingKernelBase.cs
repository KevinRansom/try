// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;

namespace Microsoft.DotNet.Interactive
{
    public abstract class ScriptingKernelBase : KernelBase
    {
        protected ScriptingKernelBase()
        {
            NativeAssemblyLoadHelper = new NativeAssemblyLoadHelper();
            RegisterForDisposal(NativeAssemblyLoadHelper);
        }

        public abstract void AddScriptReferences(IReadOnlyList<ResolvedPackageReference> assemblyPaths);

        internal NativeAssemblyLoadHelper NativeAssemblyLoadHelper { get; }
    }
}