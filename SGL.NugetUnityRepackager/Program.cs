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
			[Option('v', FlagCounter = true)]
			public int Verbosity { get; set; } = 0;

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
		}

		static async Task RealMain(Options opts) {
			var loggerFactory = opts.Verbosity switch {
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
			using var treeResolver = new NugetTreeResolver(loggerFactory, Directory.GetCurrentDirectory());
			var cancellationTokenSource = new CancellationTokenSource();
			var ct = cancellationTokenSource.Token;
			Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) => { cancellationTokenSource.Cancel(); };
			var packages = await treeResolver.GetAllDependenciesAsync(NuGetFramework.ParseFolder("netstandard2.1"), ct, opts.PrimaryPackages.ToArray());

			var converter = new PackageConverter(opts.UnityVersion, opts.UnityRelease);
			var convertedPackages = converter.ConvertPackages(packages, opts.PrimaryPackages.Cast<PackageIdentity>().ToHashSet());

			Directory.CreateDirectory(opts.OutputDirectory);
			var upmWriter = new UnityPackageWriter(opts.OutputDirectory);

			foreach (var (identity, package) in convertedPackages) {
				await Console.Out.WriteLineAsync($"{identity} => {string.Join(", ", package.Dependencies)}");
				await upmWriter.WriteUnityPackageAsync(package, ct);
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
		}

	}
}
