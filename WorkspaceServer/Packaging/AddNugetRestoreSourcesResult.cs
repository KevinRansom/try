// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Interactive;

namespace WorkspaceServer.Packaging
{
    public class AddNugetRestoreSourcesResult : AddNugetResult
    {
        public AddNugetRestoreSourcesResult(
            bool succeeded,
            PackageReference requestedPackage,
            IReadOnlyList<ResolvedPackageReference> addedReferences = null,
            IReadOnlyCollection<string> errors = null) : base(succeeded, requestedPackage, errors)
        {
        }
    }
}