using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
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

			var packageName = "SGL.Utilities.Backend";
			var sourceNames = packageSourcesMapping.GetConfiguredPackageSources(packageName);
			var repository = Repository.Factory.GetCoreV3(packageSources[sourceNames.First()]);
			FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>(ct);

			IEnumerable<NuGetVersion> versions = await resource.GetAllVersionsAsync(packageName, cache, logger, ct);
			var versionRange = VersionRange.Parse("0.6.0", true);
			var version = versions.FindBestMatch(versionRange, v => v);
			var pkgWithVersion = $"{packageName} {version}";
			var downloader = await resource.GetPackageDownloaderAsync(new PackageIdentity(packageName, version), cache, logger, ct);
			await downloader.CopyNupkgFileToAsync($"{packageName}_{version}.nupkg", ct);
			await Console.Out.WriteLineAsync($"{pkgWithVersion} content:");
			foreach (var file in await downloader.CoreReader.GetFilesAsync(ct)) {
				await Console.Out.WriteAsync("\t");
				await Console.Out.WriteLineAsync(file);
			}
			await Console.Out.WriteLineAsync($"{pkgWithVersion} types: {string.Join(" ", (await downloader.CoreReader.GetPackageTypesAsync(ct)).Select(t => t.Name))}");
			await Console.Out.WriteLineAsync($"{pkgWithVersion} dependencies:");
			foreach (var dependencyGroup in await downloader.ContentReader.GetPackageDependenciesAsync(ct)) {
				await Console.Out.WriteAsync("\t");
				await Console.Out.WriteLineAsync(dependencyGroup.TargetFramework.ToString());
				foreach (var pkg in dependencyGroup.Packages) {
					await Console.Out.WriteAsync("\t\t");
					await Console.Out.WriteAsync(pkg.Id);
					await Console.Out.WriteAsync(" ");
					await Console.Out.WriteAsync(pkg.VersionRange.ToString());
					if (pkg.Include.Any()) await Console.Out.WriteAsync($" Incl: {string.Join(", ", pkg.Include)}");
					if (pkg.Exclude.Any()) await Console.Out.WriteAsync($" Excl: {string.Join(", ", pkg.Exclude)}");
					await Console.Out.WriteLineAsync();
				}
			}

		}
	}
}
