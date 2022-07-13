using NuGet.Packaging;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class PackageMetadata {
		public string Title { get; init; }
		public string Authors { get; init; }
		public string Tags { get; init; }
		public string Owners { get; init; }
		public string Description { get; init; }
		public string ReleaseNotes { get; init; }
		public string Summary { get; init; }
		public string ProjectUrl { get; init; }
		public string Icon { get; init; }
		public string IconUrl { get; init; }
		public string Copyright { get; init; }
		public string Readme { get; init; }
		public string LicenseUrl { get; init; }
		public LicenseMetadata LicenseMetadata { get; init; }
		public bool RequireLicenseAcceptance { get; init; }
		public RepositoryMetadata RepositoryMetadata { get; init; }
		public IReadOnlyDictionary<string, string> MetadataDictionary { get; init; }
	}
}
