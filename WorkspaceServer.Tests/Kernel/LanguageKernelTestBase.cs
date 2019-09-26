﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;

using Pocket;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.FSharp;
using WorkspaceServer.Kernel;
using Xunit.Abstractions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive.Commands;

namespace WorkspaceServer.Tests.Kernel
{
    public abstract class LanguageKernelTestBase : IDisposable
    {
        public LanguageKernelTestBase(ITestOutputHelper output)
        {
            DisposeAfterTest(output.SubscribeToPocketLogger());
        }

        private KernelBase CreateLanguageKernel(Language language)
        {
            KernelBase kernelBase;
            switch (language)
            {
                case Language.FSharp:
                    kernelBase = new FSharpKernel().UseDefaultRendering();
                    break;

                case Language.CSharp:
                    kernelBase = new CSharpKernel().UseDefaultRendering()
                                                   .UseExtendDirective()
                                                   .UseKernelHelpers();
                    break;

                default:
                    throw new InvalidOperationException("Unknown language specified");
            }
            return kernelBase;
        }

        protected KernelBase CreateKernel(Language language)
        {
            var kernel = CreateLanguageKernel(language).LogEventsToPocketLogger();

            DisposeAfterTest(
                kernel.KernelEvents.Timestamp().Subscribe(KernelEvents.Add));

            return kernel;
        }

        protected KernelBase CreateKernel()
        {
            return CreateKernel(Language.CSharp);
        }

        public async Task SubmitSource(KernelBase kernel, string[] lines)
        {
            foreach (string line in lines)
            {
                await SubmitSource(kernel, line);
            }
        }

        public async Task SubmitSource(KernelBase kernel, string line)
        {
            await kernel.SendAsync(new SubmitCode(line));
        }

        /// IDispose
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        protected IList<Timestamped<IKernelEvent>> KernelEvents { get; } = new List<Timestamped<IKernelEvent>>();

        protected void DisposeAfterTest(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
        }
    }
}
