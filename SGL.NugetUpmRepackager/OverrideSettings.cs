using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUpmRepackager {
	public class OverrideSettings {
		public Dictionary<string, string> NamePrefixMapping { get; set; } = new Dictionary<string, string>();
		public List<PathMappingEntry> PathMapping { get; set; } = new List<PathMappingEntry>();
		public Dictionary<string, PackageSpecificOverrideSettings> PackageSpecific { get; set; } = new Dictionary<string, PackageSpecificOverrideSettings>();
		public class PathMappingEntry {
			public string MatchPrefix { get; set; } = "";
			public string MatchSuffix { get; set; } = "";
			public string? ReplacePrefix { get; set; } = null;
			public string? ReplaceSuffix { get; set; } = null;
		}

		public class PackageSpecificOverrideSettings {
			public bool IgnoreGlobalPathMapping { get; set; } = false;
			public List<PathMappingEntry>? PathMapping { get; set; } = null;
			public List<string>? ContentPathFilterPrefixes { get; set; } = null;
			public Dictionary<string, string> Overlays { get; set; } = new Dictionary<string, string>();
		}
	}
}
