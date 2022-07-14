using Microsoft.Extensions.Logging;

namespace SGL.NugetUnityRepackager {
	public class Overrides {
		private ILogger<Overrides> logger;
		private OverrideSettings settings;
		private List<string> packageNameMatchPrefixes;
		private List<OverrideSettings.PathMappingEntry> globalPathMappings = new List<OverrideSettings.PathMappingEntry>();

		public Overrides(ILogger<Overrides> logger, OverrideSettings settings) {
			this.logger = logger;
			this.settings = settings;
			packageNameMatchPrefixes = settings.NamePrefixMapping.Keys.OrderByDescending(k => k.Length).ToList();
			globalPathMappings = settings.PathMapping.OrderByDescending(m => m.MatchPrefix.Length).ThenByDescending(m => m.MatchSuffix.Length).ToList();
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

		public string MapPath(string contentPath) {
			foreach (var entry in globalPathMappings) {
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
	}
}