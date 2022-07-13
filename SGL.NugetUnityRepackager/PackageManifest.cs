using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class PackageManifest {
		public string Name { get; init; }
		public string Version { get; init; }
		public string? Description { get; init; }
		public string? DisplayName { get; init; }
		public string? Unity { get; init; }
		public string? UnityRelease { get; init; }
		public PackageAuthor? Author { get; init; }
		public string? ChangelogUrl { get; init; }
		public IReadOnlyDictionary<string, string>? Dependencies { get; init; }
		public string? DocumentationUrl { get; init; }
		public bool? HideInEditor { get; init; }
		public IReadOnlyList<string>? Keywords { get; init; }
		public string? License { get; init; }
		public string? LicenseUrl { get; init; }
		public IReadOnlyList<PackageSample>? Samples { get; init; }
	}

	public class PackageSample {
		public string DisplayName { get; init; }
		public string Description { get; init; }
		public string Path { get; init; }
	}

	public class PackageAuthor {
		public string Name { get; init; }
		public string? Email { get; init; }
		public string? Url { get; init; }
	}
}
