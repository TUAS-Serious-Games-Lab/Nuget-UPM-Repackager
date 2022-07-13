using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class PackageConverter {
		private string? unity;
		private string? unityRelease;

		public PackageConverter(string? unity, string? unityRelease) {
			this.unity = unity;
			this.unityRelease = unityRelease;
		}

		public IReadOnlyDictionary<PackageIdentity, Package> ConvertPackages(IReadOnlyDictionary<PackageIdentity, Package> input, ISet<PackageIdentity> primaryPackages) {
			var result = new Dictionary<PackageIdentity, Package>();
			foreach (var (inPkgIdent, inPkg) in input) {
				var outIdent = new PackageIdentity(ConvertName(inPkgIdent.Id), inPkgIdent.Version);
				var outPkg = new Package(outIdent, inPkg.Dependencies.Select(inDep => new PackageIdentity(ConvertName(inDep.Id), inDep.Version)).ToList(), inPkg.Metadata,
					inPkg.Contents.Prepend(GenerateUpmManifest(inPkg, primaryPackages.Contains(inPkgIdent))).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
				result.Add(outIdent, outPkg);
			}
			return result;
		}

		private KeyValuePair<string, Func<CancellationToken, Task<Stream>>> GenerateUpmManifest(Package inPkg, bool primaryPackage) {
			Func<CancellationToken, Task<Stream>> getter = async (CancellationToken ct) => {
				JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
					WriteIndented = true,
					DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
				};
				var stream = new MemoryStream();
				var manifest = new PackageManifest {
					Name = ConvertName(inPkg.Identifier.Id),
					Version = inPkg.Identifier.Version.ToString(),
					DisplayName = String.IsNullOrEmpty(inPkg.Metadata.Title) ? null : inPkg.Metadata.Title,
					Description = String.IsNullOrEmpty(inPkg.Metadata.Description) ? null : inPkg.Metadata.Description,
					Keywords = String.IsNullOrEmpty(inPkg.Metadata.Tags) ? null : inPkg.Metadata.Tags?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
					ChangelogUrl = String.IsNullOrEmpty(inPkg.Metadata.ReleaseNotes) ? null : inPkg.Metadata.ReleaseNotes,
					Samples = null,
					LicenseUrl = String.IsNullOrEmpty(inPkg.Metadata.LicenseUrl) ? null : inPkg.Metadata.LicenseUrl,
					License = inPkg.Metadata.LicenseMetadata != null ? $"{inPkg.Metadata.LicenseMetadata.License} {inPkg.Metadata.LicenseMetadata.Version}" : null,
					DocumentationUrl = inPkg.Metadata.RepositoryMetadata == null || string.IsNullOrEmpty(inPkg.Metadata.RepositoryMetadata.Url) ?
						(String.IsNullOrEmpty(inPkg.Metadata.ProjectUrl) ? null : inPkg.Metadata.ProjectUrl) :
						inPkg.Metadata.RepositoryMetadata.Url,
					Author = String.IsNullOrEmpty(inPkg.Metadata.Authors) ? null : new PackageAuthor {
						Name = inPkg.Metadata.Authors,
						Url = String.IsNullOrEmpty(inPkg.Metadata.ProjectUrl) ? null : inPkg.Metadata.ProjectUrl,
						Email = null
					},
					Dependencies = inPkg.Dependencies.ToDictionary(inPkg => ConvertName(inPkg.Id), inPkg => inPkg.Version.ToString()),
					HideInEditor = !primaryPackage,
					Unity = unity,
					UnityRelease = unityRelease
				};
				await JsonSerializer.SerializeAsync(stream, manifest, options, ct);
				stream.Position = 0;
				return stream;
			};
			return new("package.json", getter);
		}

		private string ConvertName(string origName) {
			return origName.ToLower();
		}
	}
}
