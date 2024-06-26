﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUpmRepackager {
	public class PackageValidator {
		private ILogger<PackageValidator> logger;
		private static ISet<string> nativeSuffixes = new HashSet<string>(new[] { ".dll", ".so", ".a", ".dylib" }, StringComparer.OrdinalIgnoreCase);

		public PackageValidator(ILogger<PackageValidator> logger) {
			this.logger = logger;
		}

		public int Validate(Package package) {
			int problemCount = 0;
			foreach (var path in package.NativeRuntimesContents) {
				if (nativeSuffixes.Any(suffix => path.EndsWith(suffix)) && !package.Contents.ContainsKey($"{path}.meta")) {
					problemCount++;
					logger.LogWarning("{pkg}: No .meta file for native runtimes content '{path}'.", package.Identifier.Id, path);
				}
			}
			foreach (var path in package.Contents.Keys) {
				if (path.EndsWith(".meta")) {
					var beforeMeta = path.Substring(0, path.Length - 5);
					if (!package.Contents.ContainsKey(beforeMeta) && !package.Contents.Keys.Any(name => name.StartsWith(beforeMeta))) {
						problemCount++;
						logger.LogWarning("{pkg}: Stray .meta file '{path}'.", package.Identifier.Id, path);
					}
				}
			}
			return problemCount;
		}
	}
}
