﻿using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class Package {
		internal Package(PackageIdentity identifier, IReadOnlyList<PackageIdentity> dependencies, PackageMetadata metadata,
				IReadOnlyDictionary<string, Func<CancellationToken, Task<Stream>>> contents) {
			Identifier = identifier;
			Dependencies = dependencies;
			Metadata = metadata;
			Contents = contents;
		}

		public PackageIdentity Identifier { get; }
		public IReadOnlyList<PackageIdentity> Dependencies { get; }
		public PackageMetadata Metadata { get; }
		public IReadOnlyDictionary<string, Func<CancellationToken, Task<Stream>>> Contents { get; }
	}
}