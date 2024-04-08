using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SGL.NugetUnityRepackager {
	public class PackageConverter {
		private readonly ILoggerFactory loggerFactory;
		private string? unity;
		private string? unityRelease;
		private string mainDirectory;
		private AsyncLazy<Overrides> overrides;

		public PackageConverter(ILoggerFactory loggerFactory, string? unity, string? unityRelease, string mainDirectory, CancellationToken ct) {
			this.loggerFactory = loggerFactory;
			this.unity = unity;
			this.unityRelease = unityRelease;
			this.mainDirectory = mainDirectory;
			overrides = new AsyncLazy<Overrides>(() => PrepareOverridesAsync(ct));
		}

		private async Task<Overrides> PrepareOverridesAsync(CancellationToken ct) {
			var overridesFile = Path.Combine(mainDirectory, "overrides.json");
			var logger = loggerFactory.CreateLogger<Overrides>();
			OverrideSettings settings;
			if (File.Exists(overridesFile)) {
				try {
					await using var settingsStream = new FileStream(overridesFile, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 4096, useAsync: true);
					var options = new JsonSerializerOptions(JsonSerializerDefaults.General) {
						ReadCommentHandling = JsonCommentHandling.Skip
					};
					var readSettings = await JsonSerializer.DeserializeAsync<OverrideSettings>(settingsStream, options, ct);
					if (readSettings == null) {
						throw new ArgumentNullException("Couldn't read valid override settings from existing overrides.json.");
					}
					settings = readSettings;
				}
				catch (Exception ex) {
					logger.LogError(ex, "Failed to load overrides.json");
					throw;
				}
			}
			else {
				settings = new OverrideSettings();
			}
			return new Overrides(logger, settings, mainDirectory);
		}

		public async Task<IReadOnlyDictionary<PackageIdentity, Package>> ConvertPackagesAsync(IReadOnlyDictionary<PackageIdentity, Package> input, ISet<PackageIdentity> primaryPackages) {
			var overrides = await this.overrides;
			var result = new Dictionary<PackageIdentity, Package>();
			foreach (var (inPkgIdent, inPkg) in input) {
				var outIdent = new PackageIdentity(ConvertName(overrides, inPkgIdent.Id), inPkgIdent.Version);
				var contents = inPkg.Contents
					.Where(kvp => overrides.FilterContents(inPkgIdent, kvp.Key))
					.Prepend(GenerateUpmManifest(inPkg, primaryPackages.Contains(inPkgIdent)))
					.Select(kvp => new KeyValuePair<string, Func<CancellationToken, Task<Stream>>>(overrides.MapPath(kvp.Key, inPkgIdent), kvp.Value))
					.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
				foreach (var (metaName, metaContent) in MetaFileGenerator.GenerateMetaFileForFiles(contents.Keys.ToList(), inPkgIdent.Id)
												.Concat(MetaFileGenerator.GenerateMetaFileForDirectories(contents.Keys.ToList(), inPkgIdent.Id))) {
					contents[metaName] = metaContent;
				}
				foreach (var (overlayName, overlayContent) in overrides.GetOverlays(inPkgIdent)) {
					contents[overlayName] = overlayContent;
				}
				var outPkg = new Package(
					outIdent,
					inPkg.Dependencies.Select(inDep => new PackageIdentity(ConvertName(overrides, inDep.Id), inDep.Version)).ToList(),
					inPkg.Metadata,
					contents,
					inPkg.NativeRuntimesContents
						.Where(path => overrides.FilterContents(inPkgIdent, path))
						.Select(path => overrides.MapPath(path, inPkgIdent))
						.ToList());
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
				var overrides = await this.overrides;
				var manifest = new PackageManifest(ConvertName(overrides, inPkg.Identifier.Id), inPkg.Identifier.Version.ToString()) {
					DisplayName = string.IsNullOrEmpty(inPkg.Metadata.Title) ? null : inPkg.Metadata.Title,
					Description = string.IsNullOrEmpty(inPkg.Metadata.Description) ? null : inPkg.Metadata.Description,
					Keywords = string.IsNullOrEmpty(inPkg.Metadata.Tags) ? null : inPkg.Metadata.Tags?.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
					ChangelogUrl = string.IsNullOrEmpty(inPkg.Metadata.ReleaseNotes) ? null : inPkg.Metadata.ReleaseNotes,
					Samples = null,
					LicenseUrl = string.IsNullOrEmpty(inPkg.Metadata.LicenseUrl) ? null : inPkg.Metadata.LicenseUrl,
					License = inPkg.Metadata.LicenseMetadata != null ? $"{inPkg.Metadata.LicenseMetadata.License} {inPkg.Metadata.LicenseMetadata.Version}" : null,
					DocumentationUrl = inPkg.Metadata.RepositoryMetadata == null || string.IsNullOrEmpty(inPkg.Metadata.RepositoryMetadata.Url) ?
						(string.IsNullOrEmpty(inPkg.Metadata.ProjectUrl) ? null : inPkg.Metadata.ProjectUrl) :
						inPkg.Metadata.RepositoryMetadata.Url,
					Author = string.IsNullOrEmpty(inPkg.Metadata.Authors) ? null : new PackageAuthor(inPkg.Metadata.Authors) {
						Url = string.IsNullOrEmpty(inPkg.Metadata.ProjectUrl) ? null : inPkg.Metadata.ProjectUrl,
						Email = null
					},
					Dependencies = inPkg.Dependencies.ToDictionary(inPkg => ConvertName(overrides, inPkg.Id), inPkg => inPkg.Version.ToString()),
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

		private static string ConvertName(Overrides overrides, string origName) {
			var mappedName = overrides.MapPackageName(origName);
			return mappedName.Replace('_', '-').ToLower();
		}
	}
}
