using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class OverrideSettings {
		public Dictionary<string, string> NamePrefixMapping { get; set; } = new Dictionary<string, string>();
		public List<PathMappingEntry> PathMapping { get; set; } = new List<PathMappingEntry>();

		public class PathMappingEntry {
			public string MatchPrefix { get; set; } = "";
			public string MatchSuffix { get; set; } = "";
			public string? ReplacePrefix { get; set; } = null;
			public string? ReplaceSuffix { get; set; } = null;
		}
	}
}
