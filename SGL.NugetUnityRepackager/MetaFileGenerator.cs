using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public static class MetaFileGenerator {

		private static string GeneratePluginMetaContent(Guid id) => @"fileFormatVersion: 2
" + $"guid: {id.ToString("N")}" + @"
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";
		private static string GenerateDefaultMetaContent(Guid id) => @"fileFormatVersion: 2
" + $"guid: {id.ToString("N")}" + @"
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";
		private static string GenerateTextScriptMetaContent(Guid id) => @"fileFormatVersion: 2
" + $"guid: {id.ToString("N")}" + @"
TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
";

		private static KeyValuePair<string, Func<CancellationToken, Task<Stream>>> BuildGenerator(string name, Func<Guid, string> contentGenerator) {
			return new KeyValuePair<string, Func<CancellationToken, Task<Stream>>>($"{name}.meta", async ct => {
				var stream = new MemoryStream();
				await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)) {
					var guid = Guid.NewGuid();
					var content = contentGenerator(guid);
					await writer.WriteAsync(content.AsMemory(), ct);
					await writer.FlushAsync();
				}
				stream.Position = 0;
				return stream;
			});
		}

		public static KeyValuePair<string, Func<CancellationToken, Task<Stream>>> GenerateMetaFileForFile(string fileName) {
			return BuildGenerator(fileName, fileName switch {
				var dll when dll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) => GeneratePluginMetaContent,
				var dllConfig when dllConfig.EndsWith(".dll.config", StringComparison.OrdinalIgnoreCase) => GeneratePluginMetaContent,
				var json when json.EndsWith(".json", StringComparison.OrdinalIgnoreCase) => GenerateTextScriptMetaContent,
				var xml when xml.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) => GenerateTextScriptMetaContent,
				_ => GenerateDefaultMetaContent
			});
		}

		public static KeyValuePair<string, Func<CancellationToken, Task<Stream>>> GenerateMetaFileForDirectory(string dirName) {
			return BuildGenerator(dirName, GenerateDefaultMetaContent);
		}

		public static IEnumerable<KeyValuePair<string, Func<CancellationToken, Task<Stream>>>> GenerateMetaFileForFiles(IEnumerable<string> filePaths) {
			return filePaths.Select(file => GenerateMetaFileForFile(file));
		}

		public static IEnumerable<KeyValuePair<string, Func<CancellationToken, Task<Stream>>>> GenerateMetaFileForDirectories(IEnumerable<string> filePaths) {
			var directories = filePaths.SelectMany(path => path.SelectMany((c, i) => c == '/' ? new[] { path.Substring(0, i) } : Enumerable.Empty<string>())).ToHashSet();
			return directories.Select(dir => GenerateMetaFileForDirectory(dir));
		}
	}
}
