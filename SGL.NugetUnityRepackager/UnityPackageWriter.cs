using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUnityRepackager {
	public class UnityPackageWriter {
		private string directory;

		public UnityPackageWriter(string directory) {
			this.directory = directory;
		}

		public async Task WriteUnityPackageAsync(Package package, CancellationToken ct) {
			var filename = $"{package.Identifier.Id}-{package.Identifier.Version}.tgz";
			using var fileStream = new FileStream(Path.Combine(directory, filename), FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
			using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal, leaveOpen: true);
			using var archiveStream = new TarOutputStream(gzipStream, Encoding.UTF8);
			foreach (var (name, contentGetter) in package.Contents) {
				await WriteTarEntry(archiveStream, $"{package.Identifier.Id}/{name}", contentGetter, ct);
			}
			var (metaName, metaContent) = MetaFileGenerator.GenerateMetaFileForDirectory(package.Identifier.Id);
			await WriteTarEntry(archiveStream, metaName, metaContent, ct);
			archiveStream.Close();
		}

		private static async Task WriteTarEntry(TarOutputStream archiveStream, string name, Func<CancellationToken, Task<Stream>> contentGetter, CancellationToken ct) {
			using var content = await contentGetter(ct);
			var entry = TarEntry.CreateTarEntry(name);
			entry.Size = content.Length;
			archiveStream.PutNextEntry(entry);
			await content.CopyToAsync(archiveStream);
			archiveStream.CloseEntry();
		}
	}
}
