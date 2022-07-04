using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace SGL.NugetUnityRepackager {
	internal class Program {
		static async Task Main(string[] args) {
			ILogger logger = NullLogger.Instance;
			CancellationToken ct = CancellationToken.None;

			SourceCacheContext cache = new SourceCacheContext();
			var settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
			var packageSources = new PackageSourceProvider(settings).LoadPackageSources().ToDictionary(ps => ps.Name);
			var packageSourcesMapping = PackageSourceMapping.GetPackageSourceMapping(settings);

			var packageName = "SGL.Utilities";
			var sourceNames = packageSourcesMapping.GetConfiguredPackageSources(packageName);
			var repository = Repository.Factory.GetCoreV3(packageSources[sourceNames.First()]);
			FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

			IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(packageName, cache, logger, ct);

			foreach (NuGetVersion version in versions) {
				Console.WriteLine($"Found version {version}");
			}
		}
	}
}
