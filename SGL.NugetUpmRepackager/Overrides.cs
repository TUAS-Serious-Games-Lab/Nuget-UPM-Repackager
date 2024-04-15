using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;

namespace SGL.NugetUpmRepackager {
	public class Overrides {
		private ILogger<Overrides> logger;
		private OverrideSettings settings;
		private readonly string mainDirectory;
		private List<string> packageNameMatchPrefixes;
		private List<OverrideSettings.PathMappingEntry> globalPathMappings = new List<OverrideSettings.PathMappingEntry>();
		private Dictionary<string, OverrideSettings.PackageSpecificOverrideSettings> pkgSpecificOverrides;

		public Overrides(ILogger<Overrides> logger, OverrideSettings settings, string mainDirectory) {
			this.logger = logger;
			this.settings = settings;
			this.mainDirectory = mainDirectory;
			packageNameMatchPrefixes = settings.NamePrefixMapping.Keys.OrderByDescending(k => k.Length).ToList();
			globalPathMappings = settings.PathMapping.OrderByDescending(m => m.MatchPrefix.Length).ThenByDescending(m => m.MatchSuffix.Length).ToList();
			pkgSpecificOverrides = settings.PackageSpecific.ToDictionary(kvp => kvp.Key.ToLowerInvariant(), kvp => {
				var pathMappings = kvp.Value.PathMapping;
				if (pathMappings == null) {
					pathMappings = globalPathMappings;
				}
				else if (kvp.Value.IgnoreGlobalPathMapping) {
					pathMappings = pathMappings.OrderByDescending(m => m.MatchPrefix.Length).ThenByDescending(m => m.MatchSuffix.Length).ToList();
				}
				else {
					pathMappings = pathMappings.OrderByDescending(m => m.MatchPrefix.Length).ThenByDescending(m => m.MatchSuffix.Length).Concat(globalPathMappings).ToList();
				}
				return new OverrideSettings.PackageSpecificOverrideSettings {
					ContentPathFilterPrefixes = kvp.Value.ContentPathFilterPrefixes,
					Overlays = kvp.Value.Overlays,
					IgnoreGlobalPathMapping = kvp.Value.IgnoreGlobalPathMapping,
					PathMapping = pathMappings,
				};
			});
		}

		public string MapPackageName(string packageName) {
			foreach (var key in packageNameMatchPrefixes) {
				if (packageName.StartsWith(key, StringComparison.OrdinalIgnoreCase)) {
					var replacement = settings.NamePrefixMapping[key];
					var remainder = packageName.Substring(key.Length);
					return replacement + remainder;
				}
			}
			// No match -> return unchanged
			return packageName;
		}

		public string MapPath(string contentPath, PackageIdentity pkgIdent) {
			IEnumerable<OverrideSettings.PathMappingEntry> pathMappings = globalPathMappings;
			if (pkgSpecificOverrides.TryGetValue(pkgIdent.Id.ToLowerInvariant(), out var pkgOverrides)) {
				pathMappings = pkgOverrides.PathMapping ?? globalPathMappings;
			}
			foreach (var entry in pathMappings) {
				if (contentPath.StartsWith(entry.MatchPrefix, StringComparison.OrdinalIgnoreCase) &&
					contentPath.EndsWith(entry.MatchSuffix, StringComparison.OrdinalIgnoreCase)) {
					if (entry.ReplacePrefix != null && entry.ReplaceSuffix != null) {
						var remainder = contentPath.Substring(entry.MatchPrefix.Length, contentPath.Length - entry.MatchPrefix.Length - entry.MatchSuffix.Length);
						return entry.ReplacePrefix + remainder + entry.ReplaceSuffix;
					}
					else if (entry.ReplacePrefix != null) {
						var remainder = contentPath.Substring(entry.MatchPrefix.Length);
						return entry.ReplacePrefix + remainder;
					}
					else if (entry.ReplaceSuffix != null) {
						var remainder = contentPath.Substring(0, contentPath.Length - entry.MatchSuffix.Length);
						return remainder + entry.ReplaceSuffix;
					}
					else {
						logger.LogWarning("Content {contentPath} matched path mapping with prefix {prefix} and suffix {suffix}, " +
							"but the entry had neither a replacement prefix nor a replacement suffix and thus has no effect.",
							contentPath, entry.MatchPrefix, entry.MatchSuffix);
						return contentPath;
					}
				}
			}
			// No match -> return unchanged
			return contentPath;
		}

		public bool FilterContents(PackageIdentity inPkgIdent, string contentPath) {
			if (!pkgSpecificOverrides.TryGetValue(inPkgIdent.Id.ToLowerInvariant(), out var pkgOverrides)) {
				return true;
			}
			else if (pkgOverrides.ContentPathFilterPrefixes == null) {
				return true;
			}
			else {
				return pkgOverrides.ContentPathFilterPrefixes.Any(prefix => contentPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
			}
		}

		public IEnumerable<KeyValuePair<string, Func<CancellationToken, Task<Stream>>>> GetOverlays(PackageIdentity inPkgIdent) {
			if (pkgSpecificOverrides.TryGetValue(inPkgIdent.Id.ToLowerInvariant(), out var pkgOverrides)) {
				return pkgOverrides.Overlays.ToDictionary(kvp => kvp.Key, kvp => GetOverlayFile(kvp.Value, kvp.Key, inPkgIdent));
			}
			else {
				return Enumerable.Empty<KeyValuePair<string, Func<CancellationToken, Task<Stream>>>>();
			}
		}

		private Func<CancellationToken, Task<Stream>> GetOverlayFile(string filePath, string contentPath, PackageIdentity pkgIdent) {
			return ct => {
				try {
					var file = Path.Combine(mainDirectory, filePath);
					var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 4096, useAsync: true);
					return Task.FromResult<Stream>(stream);
				}
				catch (Exception ex) {
					logger.LogError(ex, "Couldn't read file {filePath} for overlay {contentPath} in package {pkg}.", filePath, contentPath, pkgIdent.Id);
					throw;
				}
			};
		}
	}
}