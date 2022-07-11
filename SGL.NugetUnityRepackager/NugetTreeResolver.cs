using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class NugetTreeResolver {
		public IReadOnlyDictionary<PackageIdentity, Package> GetAllDependencies(params PackageIdentity[] primaryPackageIdentifiers) {
			throw new NotImplementedException();
		}
	}
}
