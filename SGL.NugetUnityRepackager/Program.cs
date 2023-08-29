using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Reflection;

namespace SGL.NugetUnityRepackager {
	internal class Program {

		class Options {
			[Option('v', FlagCounter = true, HelpText = "Produce more verbose output. Can be specified up to 3 times.")]
			public int Verbosity { get; set; } = 0;

			[Option('d', "directory", HelpText = "The main operating directory, from which the NuGet.config is looked-up and where overrides.json is searched.")]
			public string MainDirectory { get; set; } = ".";

			[Option('o', "output-dir", HelpText = "The directory to which the repackaged packages shall be written.")]
			public string OutputDirectory { get; set; } = "output";

			[Value(0, MetaName = "PRIMARY_PACKAGES", MetaValue = "PKG_NAME_AT_VERSION", Min = 1)]
			public IEnumerable<ParsedPackageIdentity> PrimaryPackages { get; set; } = Enumerable.Empty<ParsedPackageIdentity>();

			[Option('f', "framework", HelpText = "The target framework for which to extract the matching assemblies.")]
			public ParsedFramework Framework { get; set; } = new ParsedFramework("netstandard2.1");

			[Option('u', "unity", HelpText = "Unity version to specify in package manifest.")]
			public string UnityVersion { get; set; } = "2021.3";

			[Option('r', "unity-release", HelpText = "Unity release within the version, to specify in package manifest.")]
			public string? UnityRelease { get; set; } = null;

			[Option("dependency-usage", HelpText = "Collect and display information about how many / which package use each gathered package as a dependency.\nWithout -v the package counts are given. With -v the packages are listed.\nPrimary packages usually have not usages and are thus not listed.")]
			public bool DependencyUsage { get; set; } = false;

			[Usage]
			public static IEnumerable<Example> Examples => new List<Example> {
				new Example("Convert version 1.2.3 of Some.Example.Package and all of its dependencies, and store the converted packages in output/.",
					new UnParserSettings(){ PreferShortName = true},
					new Options() {
						OutputDirectory = "output",
						PrimaryPackages = new ParsedPackageIdentity[]{ new ParsedPackageIdentity("Some.Example.Package@1.2.3")},
						Framework = new ParsedFramework("netstandard2.0")
					})
			};
		}

		class ParsedPackageIdentity : PackageIdentity {
			public ParsedPackageIdentity(string argText) : base(argText.Split('@')[0], NuGetVersion.Parse(argText.Split('@')[1])) { }

			public override string ToString() => $"{Id}@{Version}";
		}

		class ParsedFramework : NuGetFramework {
			public ParsedFramework(string argText) : base(NuGetFramework.Parse(argText)) { }
			public override string ToString() => GetShortFolderName();
		}

		static async Task Main(string[] args) {
			var parser = new Parser(c => {
				c.HelpWriter = null;
				c.AllowMultiInstance = true;
			});
			var result = parser.ParseArguments<Options>(args);
			result = await result.WithParsedAsync(RealMain);
			result = await result.WithNotParsedAsync(errs => DisplayHelp(result, errs));
			Environment.Exit(0);
		}

		static async Task RealMain(Options opts) {
			using var loggerFactory = opts.Verbosity switch {
				0 => LoggerFactory.Create(config => {
					config.AddSimpleConsole(conf => {
						conf.SingleLine = true;
					});
					config.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
				}),
				1 => LoggerFactory.Create(config => {
					config.AddSimpleConsole(conf => {
						conf.SingleLine = true;
					});
					config.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
				}),
				2 => LoggerFactory.Create(config => {
					config.AddSimpleConsole(conf => {
						conf.SingleLine = true;
					});
					config.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
				}),
				_ => LoggerFactory.Create(config => {
					config.AddSimpleConsole(conf => {
						conf.IncludeScopes = true;
					});
					config.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
				})
			};
			try {
				using var treeResolver = new NugetTreeResolver(loggerFactory, Path.GetFullPath(opts.MainDirectory));
				var cancellationTokenSource = new CancellationTokenSource();
				var ct = cancellationTokenSource.Token;
				Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) => { cancellationTokenSource.Cancel(); };
				var ignoredDependencies = (await File.ReadAllLinesAsync("ignored-dependencies.txt", ct))
					.Select(line => line.Trim())
					.Where(line => !string.IsNullOrEmpty(line))
					.Where(line => !line.StartsWith('#'))
					.Select(name => name.ToLowerInvariant())
					.ToHashSet();
				await Console.Out.WriteLineAsync("Gathering and resolving package dependencies:");
				await Console.Out.WriteLineAsync(new string('-', Console.WindowWidth));
				var packages = await treeResolver.GetAllDependenciesAsync(opts.Framework, ct, ignoredDependencies, opts.PrimaryPackages.ToArray());
				await Console.Out.WriteLineAsync(new string('-', Console.WindowWidth));
				await Console.Out.WriteLineAsync($"Resolved {packages.Count} packages.");
				await Console.Out.WriteLineAsync();

				packages = packages
					.Where(pkg => !ignoredDependencies.Contains(pkg.Key.Id.ToLowerInvariant()))
					.Select(pkg => new Package(
						pkg.Value.Identifier,
						pkg.Value.Dependencies.Where(dep => !ignoredDependencies.Contains(dep.Id.ToLowerInvariant())).ToList(),
						pkg.Value.Metadata, pkg.Value.Contents, pkg.Value.NativeRuntimesContents))
					.ToDictionary(pkg => new PackageIdentity(pkg.Identifier.Id, pkg.Identifier.Version));

				if (opts.DependencyUsage) {
					var depUsers = new Dictionary<PackageIdentity, List<PackageIdentity>>();
					foreach (var (userIdent, userPkg) in packages) {
						foreach (var dependency in userPkg.Dependencies) {
							if (!depUsers.TryGetValue(dependency, out var deps)) {
								deps = new List<PackageIdentity>();
								depUsers[dependency] = deps;
							}
							deps.Add(userIdent);
						}
					}
					await Console.Out.WriteLineAsync();
					await Console.Out.WriteLineAsync(new string('-', Console.WindowWidth));
					await Console.Out.WriteLineAsync(opts.Verbosity > 0 ? "Dependency Usages:\n" : "Dependency Counts:\n");
					foreach (var (pkg, users) in depUsers) {
						await Console.Out.WriteLineAsync($"{pkg}: {users.Count}");
						if (opts.Verbosity > 0) {
							foreach (var user in users) {
								await Console.Out.WriteLineAsync($"\t{user}");
							}
						}
					}
				}

				var converter = new PackageConverter(loggerFactory, opts.UnityVersion, opts.UnityRelease, Path.GetFullPath(opts.MainDirectory), ct);
				var convertedPackages = await converter.ConvertPackagesAsync(packages, opts.PrimaryPackages.Cast<PackageIdentity>().ToHashSet());

				await Console.Out.WriteLineAsync();
				await Console.Out.WriteLineAsync(new string('-', Console.WindowWidth));
				await Console.Out.WriteLineAsync("Packing UPM packages:\n");

				Directory.CreateDirectory(opts.OutputDirectory);
				var upmWriter = new UnityPackageWriter(opts.OutputDirectory);

				foreach (var (identity, package) in convertedPackages) {
					await Console.Out.WriteAsync($"{identity}");
					if (opts.Verbosity > 0) {
						await Console.Out.WriteLineAsync(":");
					}
					else {
						await Console.Out.WriteAsync("...");
					}
					if (opts.Verbosity > 0) {
						foreach (var dep in package.Dependencies) {
							await Console.Out.WriteLineAsync($"\tdep: {dep}");
						}
					}
					if (opts.Verbosity > 1) {
						foreach (var (content, _) in package.Contents) {
							await Console.Out.WriteLineAsync($"\tcontent: {content}");
						}
					}
					await upmWriter.WriteUnityPackageAsync(package, ct);
					if (opts.Verbosity > 2) {
						await Console.Out.WriteLineAsync("\t=> done");
					}
					if (opts.Verbosity == 0) {
						await Console.Out.WriteLineAsync(" done");
					}
				}

				await Console.Out.WriteLineAsync();
				await Console.Out.WriteLineAsync(new string('-', Console.WindowWidth));

				var validator = new PackageValidator(loggerFactory.CreateLogger<PackageValidator>());
				int problemCount = 0;
				foreach (var package in convertedPackages.Values) {
					problemCount += validator.Validate(package);
				}
				if (problemCount > 0) {
					loggerFactory.CreateLogger<PackageValidator>().LogWarning("Found {count} problems with the converted packages.", problemCount);
				}
				await Console.Out.FlushAsync();
				await Console.Error.FlushAsync();
			}
			catch (Exception ex) {
				loggerFactory.CreateLogger<Program>().LogError(ex, "Package conversion failed.");
				loggerFactory.Dispose();
				Environment.Exit(2);
			}
		}
		static async Task DisplayHelp(ParserResult<Options> result, IEnumerable<Error> errs) {
			await Console.Out.WriteLineAsync(HelpText.AutoBuild(result, h => {
				h.AddNewLineBetweenHelpSections = true;
				h.AdditionalNewLineAfterOption = false;
				h.Heading = $"SGL NugetUnityRepackager {Assembly.GetExecutingAssembly().GetName().Version}";
				h.MaximumDisplayWidth = 170;
				return h;
			}));
			Environment.Exit(1);
		}
	}
}
