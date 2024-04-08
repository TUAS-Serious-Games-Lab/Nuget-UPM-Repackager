using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class PackageManifest {
		public PackageManifest(string name, string version) {
			Name = name;
			Version = version;
		}

		public string Name { get; }
		public string Version { get; }
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
		public PackageSample(string displayName, string description, string path) {
			DisplayName = displayName;
			Description = description;
			Path = path;
		}

		public string DisplayName { get; }
		public string Description { get; }
		public string Path { get; }
	}

	public class PackageAuthor {
		public PackageAuthor(string name) {
			Name = name;
		}

		public string Name { get; }
		public string? Email { get; init; }
		public string? Url { get; init; }
	}
}
