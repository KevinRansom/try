// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Interactive
{
    public class ResolvedPackageReference : PackageReference
    {
        public ResolvedPackageReference(
            string packageName,
            string packageVersion,
            IReadOnlyList<FileInfo> assemblyPaths,
            DirectoryInfo packageRoot = null,
            IReadOnlyList<DirectoryInfo> probingPaths = null) : base(packageName, packageVersion)
        {
            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(packageVersion));
            }

            _AssemblyPaths = assemblyPaths ?? throw new ArgumentNullException(nameof(assemblyPaths));
            _ProbingPaths = probingPaths ?? Array.Empty<DirectoryInfo>();
            _PackageRoot = packageRoot ?? _AssemblyPaths.FirstOrDefault()?.Directory.Parent.Parent;
        }

        public IReadOnlyList<string> AssemblyPaths { get; }

        public IReadOnlyList<string> ProbingPaths { get; }

        public string PackageRoot { get; }

        public override string ToString() => $"{PackageName},{PackageVersion}";


        public IReadOnlyList<FileInfo> _AssemblyPaths { get; }

        public IReadOnlyList<DirectoryInfo> _ProbingPaths { get; }

        public DirectoryInfo _PackageRoot { get; }
    }
}
/*
// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Interactive
{
    public class ResolvedPackageReference : PackageReference
    {
        public ResolvedPackageReference(
            string packageName,
            string packageVersion,
            IEnumerable<FileInfo> assemblyPaths,
            DirectoryInfo packageRoot = null,
            IEnumerable<DirectoryInfo> probingPaths = null) : base(packageName, packageVersion)
        {
            if (string.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(packageVersion));
            }

            //            AssemblyPaths = assemblyPaths ?? throw new ArgumentNullException(nameof(assemblyPaths));
            //            ProbingPaths = probingPaths ?? Array.Empty<DirectoryInfo>();
            //            PackageRoot = packageRoot ?? AssemblyPaths.FirstOrDefault()?.Directory.Parent.Parent;
            _assemblyPaths = assemblyPaths?.Select(path => path.FullName);
            _probingPaths = probingPaths?.Select(path => path.FullName);
            _packageRoot = (packageRoot ?? assemblyPaths.FirstOrDefault()?.Directory.Parent.Parent).FullName;
        }

        IEnumerable<string> _assemblyPaths;

        IEnumerable<string> _probingPaths;

        string _packageRoot;

        public IReadOnlyList<FileInfo> AssemblyPaths { get; }

        private IReadOnlyList<DirectoryInfo> probingPaths;
        public IReadOnlyList<string> ProbingPaths => this.probingPaths.Select(path => new FileInfo(path));

        public DirectoryInfo PackageRoot { get; }

        public override string ToString() => $"{PackageName},{PackageVersion}";
    }
}
*/