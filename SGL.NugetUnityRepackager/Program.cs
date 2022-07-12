using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace SGL.NugetUnityRepackager {
	internal class Program {
		static async Task Main(string[] args) {
			var loggerFactory = LoggerFactory.Create(config => {
				config.AddSimpleConsole();
				config.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
			});
			using var treeResolver = new NugetTreeResolver(loggerFactory, Directory.GetCurrentDirectory());
			var cancellationTokenSource = new CancellationTokenSource();
			var ct = cancellationTokenSource.Token;
			Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) => { cancellationTokenSource.Cancel(); };
			var packages = await treeResolver.GetAllDependenciesAsync(NuGetFramework.ParseFolder("netstandard2.1"), ct,
				new PackageIdentity("SGL.Community.Client", NuGetVersion.Parse("0.0.4")));

			foreach (var (identity, package) in packages) {
				await Console.Out.WriteLineAsync($"{identity} => {string.Join(", ", package.Dependencies)}");
			}
		}
	}
}
