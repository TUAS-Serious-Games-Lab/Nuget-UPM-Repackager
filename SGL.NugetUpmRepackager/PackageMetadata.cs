using NuGet.Packaging;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGL.NugetUpmRepackager {
	public class PackageMetadata {
		public PackageMetadata(string title, string authors, string tags, string owners, string description, string releaseNotes,
			string summary, string projectUrl, string icon, string iconUrl, string copyright, string readme, string licenseUrl,
			LicenseMetadata licenseMetadata, bool requireLicenseAcceptance, RepositoryMetadata repositoryMetadata,
			IReadOnlyDictionary<string, string> metadataDictionary) {
			Title = title;
			Authors = authors;
			Tags = tags;
			Owners = owners;
			Description = description;
			ReleaseNotes = releaseNotes;
			Summary = summary;
			ProjectUrl = projectUrl;
			Icon = icon;
			IconUrl = iconUrl;
			Copyright = copyright;
			Readme = readme;
			LicenseUrl = licenseUrl;
			LicenseMetadata = licenseMetadata;
			RequireLicenseAcceptance = requireLicenseAcceptance;
			RepositoryMetadata = repositoryMetadata;
			MetadataDictionary = metadataDictionary;
		}

		public string Title { get; init; }
		public string Authors { get; init; }
		public string Tags { get; init; }
		public string Owners { get; init; }
		public string Description { get; init; }
		public string ReleaseNotes { get; init; }
		public string Summary { get; init; }
		public string ProjectUrl { get; init; }
		public string Icon { get; init; }
		public string IconUrl { get; init; }
		public string Copyright { get; init; }
		public string Readme { get; init; }
		public string LicenseUrl { get; init; }
		public LicenseMetadata LicenseMetadata { get; init; }
		public bool RequireLicenseAcceptance { get; init; }
		public RepositoryMetadata RepositoryMetadata { get; init; }
		public IReadOnlyDictionary<string, string> MetadataDictionary { get; init; }
	}
}
