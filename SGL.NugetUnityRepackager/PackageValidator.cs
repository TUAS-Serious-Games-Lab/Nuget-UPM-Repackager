using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class PackageValidator {
		private ILogger<PackageValidator> logger;

		public PackageValidator(ILogger<PackageValidator> logger) {
			this.logger = logger;
		}

		public int Validate(Package package) {
			int problemCount = 0;
			foreach (var path in package.NativeRuntimesContents) {
				if (!package.Contents.ContainsKey($"{path}.meta")) {
					problemCount++;
					logger.LogWarning("{pkg}: No .meta file for native runtimes content '{path}'.", package.Identifier.Id, path);
				}
			}
			foreach (var path in package.Contents.Keys) {
				if (path.EndsWith(".meta") && !package.Contents.ContainsKey(path.Substring(0, path.Length - 5))) {
					problemCount++;
					logger.LogWarning("{pkg}: Stray .meta file '{path}'.", package.Identifier.Id, path);
				}
			}
			return problemCount;
		}
	}
}
