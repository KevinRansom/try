﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Interactive
{
    public class AddNugetPackageResult : AddNugetResult
    {
        public AddNugetPackageResult(
            bool succeeded,
            PackageReference requestedPackage,
            IReadOnlyList<ResolvedPackageReference> addedReferences = null,
            IReadOnlyCollection<string> errors = null) : base(succeeded, requestedPackage, errors)
        {
            AddedReferences = addedReferences ?? Array.Empty<ResolvedPackageReference>();

            if (Succeeded)
            {
                InstalledVersion = AddedReferences
                                   .Single(r => r.PackageName.Equals(requestedPackage.PackageName, StringComparison.OrdinalIgnoreCase))
                                   .PackageVersion;
            }
        }

        public IReadOnlyList<ResolvedPackageReference> AddedReferences { get; }

        public string InstalledVersion { get; }

        public IReadOnlyList<DirectoryInfo> NativeLibraryProbingPaths =>
            AddedReferences
                .SelectMany(path => path.ProbingPaths)
                .Distinct()
                .ToArray();
    }
}