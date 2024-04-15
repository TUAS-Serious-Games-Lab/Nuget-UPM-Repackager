using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUpmRepackager {
	public class NugetTreeResolver : IDisposable {
		private readonly ILoggerFactory loggerFactory;
		private readonly string mainDirectory;
		private ILogger<NugetTreeResolver> logger;
		private SourceCacheContext cache = new SourceCacheContext();
		private ISettings settings;
		private PackageSourceProvider packageSourceProvider;
		private Dictionary<string, PackageSource> packageSources;
		private PackageSourceMapping packageSourcesMapping;
		private PackageResolver resolver;
		private FrameworkReducer frameworkReducer;
		private PackageDownloadContext packageDownloadContext;
		private string globalPackagesFolder;
		private List<IDisposable> disposables = new List<IDisposable>();

		public NugetTreeResolver(ILoggerFactory loggerFactory, string mainDirectory) {
			disposables.Add(cache);
			this.loggerFactory = loggerFactory;
			this.mainDirectory = mainDirectory;
			logger = loggerFactory.CreateLogger<NugetTreeResolver>();
			settings = Settings.LoadDefaultSettings(mainDirectory);
			packageSourceProvider = new PackageSourceProvider(settings);
			packageSources = packageSourceProvider.LoadPackageSources().ToDictionary(ps => ps.Name);
			packageSourcesMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
			resolver = new PackageResolver();
			frameworkReducer = new FrameworkReducer();
			packageDownloadContext = new PackageDownloadContext(cache);
			globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
		}

		private IReadOnlyList<SourceRepository> GetSourceRepositories(string packageId) {
			var sources = packageSourcesMapping.GetConfiguredPackageSources(packageId);
			var repositories = sources.SelectMany(src => {
				if (packageSources.TryGetValue(src, out var value)) {
					return new[] { Repository.Factory.GetCoreV3(value) }.AsEnumerable();
				}
				else {
					logger.LogWarning("PackageSoruceMapping for {pkgId} maps to source {srcName} which was not found.", packageId, src);
					return Enumerable.Empty<SourceRepository>().AsEnumerable();
				}
			}).ToList();
			return repositories;
		}

		public void Dispose() {
			if (disposables == null) return;
			foreach (var item in disposables) {
				item?.Dispose();
			}
			disposables.Clear();
		}

		public async Task<IReadOnlyDictionary<PackageIdentity, Package>> GetAllDependenciesAsync(NuGetFramework framework, CancellationToken ct,
				ISet<string> ignoredDependencies, params PackageIdentity[] primaryPackageIdentifiers) {
			var gatherLogger = loggerFactory.CreateLogger<Gatherer>();
			var gatheredPackages = new Dictionary<PackageIdentity, SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
			foreach (var packageIdentity in primaryPackageIdentifiers) {
				await GatherPackageDependenciesAsync(gatherLogger, packageIdentity, framework, gatheredPackages, ct);
			}

			var resolverContext = new PackageResolverContext(DependencyBehavior.Lowest,
				primaryPackageIdentifiers.Select(pkg => pkg.Id),
				Enumerable.Empty<string>(),
				Enumerable.Empty<PackageReference>(),
				primaryPackageIdentifiers,
				gatheredPackages.Values,
				packageSources.Values,
				loggerFactory.CreateLogger<Resolver>().GetNuGetLogger());
			var resolvedPackages = resolver.Resolve(resolverContext, ct)
				.Select(pkg => gatheredPackages[pkg]).ToList();
			var resolvedVersions = resolvedPackages.ToDictionary(pkg => pkg.Id, pkg => new PackageIdentity(pkg.Id, pkg.Version), StringComparer.OrdinalIgnoreCase);

			var loadTasks = resolvedPackages
				.Where(pkg => !ignoredDependencies.Contains(pkg.Id.ToLowerInvariant()))
				.Select(pkg => LoadPackage(pkg, framework, resolvedVersions, ct));
			var loadedPackages = await Task.WhenAll(loadTasks);
			return loadedPackages.ToDictionary(pkg => pkg.Ident, pkg => pkg.Pkg);
		}

		private async Task<(PackageIdentity Ident, Package Pkg)> LoadPackage(SourcePackageDependencyInfo pkgId, NuGetFramework framework,
				IReadOnlyDictionary<string, PackageIdentity> resolvedVersions, CancellationToken ct) {
			var logger = loggerFactory.CreateLogger<Loader>();
			var downloadResource = await pkgId.Source.GetResourceAsync<DownloadResource>(ct);
			var downloadResult = await downloadResource.GetDownloadResourceResultAsync(pkgId, packageDownloadContext, globalPackagesFolder, logger.GetNuGetLogger(), ct);
			disposables.Add(downloadResult);
			logger.LogDebug("Download of {pkg} {version}: {status}", pkgId.Id, pkgId.Version, downloadResult.Status);
			var pkgReader = downloadResult.PackageReader;

			var supportedFrameworks = (await pkgReader.GetSupportedFrameworksAsync(ct)).ToList();
			var nearestFramework = frameworkReducer.GetNearest(framework, supportedFrameworks);
			if (nearestFramework == null) {
				logger.LogError("No compatible framework for {framework} found in package {pkg}.", framework, pkgId);
				throw new FrameworkException($"No compatible framework for {framework} found in package {pkgId}.");
			}

			var allLibItems = (await pkgReader.GetLibItemsAsync(ct)).ToList();
			var frameworkLibItems = allLibItems.GetNearest(framework);
			if (frameworkLibItems == null) {
				logger.LogWarning("No lib items for {framework} in package {pkg}.", framework, pkgId);
			}
			var contentItems = frameworkLibItems?.Items ?? Enumerable.Empty<string>();
			var nativeRuntimesContents = (await GetRuntimesItemsAsync(pkgReader, ct)).ToList();
			contentItems = contentItems.Concat(nativeRuntimesContents);
			var contents = contentItems.ToDictionary(item => item, item => GetItemReader(item, pkgReader));
			var allDependencies = (await pkgReader.GetPackageDependenciesAsync(ct)).ToList();
			var frameworkDependencies = allDependencies.GetNearest(framework);
			if (frameworkDependencies == null) {
				logger.LogWarning("No dependency information for {framework} in package {pkg}.", framework, pkgId);
			}
			if ((frameworkDependencies?.TargetFramework ?? nearestFramework) != nearestFramework) {
				logger.LogWarning("Nearest supported framework for {pkg} is {nearestFramework}, but nearest dependency group is for {frameworkDependencies}.",
					pkgId, nearestFramework, frameworkDependencies?.TargetFramework);
			}
			var dependencies = frameworkDependencies?.Packages?.Select(pkg => resolvedVersions[pkg.Id]).ToList() ?? new List<PackageIdentity>();
			return (new PackageIdentity(pkgId.Id, pkgId.Version), new Package(pkgId, dependencies, GetMetadata(await pkgReader.GetNuspecReaderAsync(ct), ct), contents, nativeRuntimesContents));
		}

		private async Task<IEnumerable<string>> GetRuntimesItemsAsync(PackageReaderBase pkgReader, CancellationToken ct) {
			var allFiles = await pkgReader.GetFilesAsync(ct);
			return allFiles.Where(name => name.StartsWith("runtimes", StringComparison.OrdinalIgnoreCase));
		}

		private PackageMetadata GetMetadata(NuspecReader nuspecReader, CancellationToken ct) => new PackageMetadata(
			title: nuspecReader.GetTitle(),
			authors: nuspecReader.GetAuthors(),
			owners: nuspecReader.GetOwners(),
			copyright: nuspecReader.GetCopyright(),
			description: nuspecReader.GetDescription(),
			summary: nuspecReader.GetSummary(),
			icon: nuspecReader.GetIcon(),
			iconUrl: nuspecReader.GetIconUrl(),
			tags: nuspecReader.GetTags(),
			licenseUrl: nuspecReader.GetLicenseUrl(),
			licenseMetadata: nuspecReader.GetLicenseMetadata(),
			projectUrl: nuspecReader.GetProjectUrl(),
			readme: nuspecReader.GetReadme(),
			releaseNotes: nuspecReader.GetReleaseNotes(),
			requireLicenseAcceptance: nuspecReader.GetRequireLicenseAcceptance(),
			repositoryMetadata: nuspecReader.GetRepositoryMetadata(),
			metadataDictionary: nuspecReader.GetMetadata().ToDictionary(md => md.Key, md => md.Value)
		);

		private Func<CancellationToken, Task<Stream>> GetItemReader(string item, PackageReaderBase pkgReader) => (ct) => pkgReader.GetStreamAsync(item, ct);

		public class Gatherer { }
		public class Resolver { }
		public class Loader { }
		private async Task GatherPackageDependenciesAsync(ILogger<Gatherer> logger, PackageIdentity package, NuGetFramework framework,
				IDictionary<PackageIdentity, SourcePackageDependencyInfo> gatheredPackages, CancellationToken ct) {
			if (gatheredPackages.ContainsKey(package)) return;

			using var scope = logger.BeginScope(package);
			foreach (var sourceRepo in GetSourceRepositories(package.Id)) {
				var resource = await sourceRepo.GetResourceAsync<FindPackageByIdResource>(ct);
				var dependencyInfoResource = await resource.GetDependencyInfoAsync(package.Id, package.Version, cache, logger.GetNuGetLogger(), ct);
				if (dependencyInfoResource == null) continue;
				var dependencyGroup = dependencyInfoResource.DependencyGroups.GetNearest(framework);
				if (dependencyGroup == null) {
					logger.LogWarning("Package {pkg} has no DependencyGroup for framework {framework} in {source}.", package, framework, sourceRepo.PackageSource.Name);
					gatheredPackages.Add(package, new SourcePackageDependencyInfo(package.Id, package.Version, Enumerable.Empty<PackageDependency>(), true, sourceRepo));
					continue;
				}
				logger.LogDebug("Selecting {framework} for {pkg}.", dependencyGroup.TargetFramework, package);
				var dependencyInfo = new SourcePackageDependencyInfo(package.Id, package.Version, dependencyGroup.Packages, true, sourceRepo);
				gatheredPackages.Add(dependencyInfo, dependencyInfo);
				if (dependencyInfo.Dependencies.Any()) {
					logger.LogDebug("Gathered package {pkg}, gathering its dependencies...", package);
					foreach (var dependency in dependencyInfo.Dependencies) {
						await GatherPackageDependenciesAsync(logger, new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion), framework, gatheredPackages, ct);
					}
				}
			}
		}
	}
}
