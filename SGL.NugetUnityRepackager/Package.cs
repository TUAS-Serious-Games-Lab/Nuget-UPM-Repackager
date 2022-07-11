using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class Package {
		public PackageIdentity Identifier { get; }
		public IReadOnlyList<PackageIdentity> Dependencies { get; }
		public PackageMetadata Metadata { get; }
		public IReadOnlyDictionary<string, Func<Stream>> Contents { get; }
	}
}
