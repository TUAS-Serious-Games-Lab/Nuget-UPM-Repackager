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

		public async Task WriteUnityPackageAsync(Package package) {
			var filename = $"{package.Identifier.Id}-{package.Identifier.Version}.tgz";
			using var fileStream = new FileStream(Path.Combine(directory, filename), FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
			using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal, leaveOpen: true);
			using var archiveStream = new TarOutputStream(gzipStream, Encoding.UTF8);
			foreach (var (name, contentGetter) in package.Contents) {
				using var content = await contentGetter();
				var entry = TarEntry.CreateTarEntry(name);
				entry.Size = content.Length;
				archiveStream.PutNextEntry(entry);
				await content.CopyToAsync(archiveStream);
				archiveStream.CloseEntry();
			}
			archiveStream.Close();
		}
	}
}
