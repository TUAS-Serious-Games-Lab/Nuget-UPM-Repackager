# NugetUpmRepackager

A stand-alone CLI tool to repackage a dependency DAGs (dependency trees of multiple root packages, potentially with shared dependencies) of NuGet packages into UPM packages.

## Usage

```
  -v                                               Produce more verbose output. Can be specified up to 3 times.
  -d, --directory                                  The main operating directory, from which the NuGet.config is looked-up and where overrides.json is searched.
  -o, --output-dir                                 The directory to which the repackaged packages shall be written.
  -f, --framework                                  The target framework for which to extract the matching assemblies.
  -u, --unity                                      Unity version to specify in package manifest.
  -r, --unity-release                              Unity release within the version, to specify in package manifest.
  --dependency-usage                               Collect and display information about how many / which package use each gathered package as a dependency.
                                                   Without -v the package counts are given. With -v the packages are listed.
                                                   Primary packages usually have not usages and are thus not listed.
  --help                                           Display this help screen.
  --version                                        Display version information.
  PRIMARY_PACKAGES (pos. 0) PKG_NAME_AT_VERSION
```

# Examples

Convert version `1.2.3` of `Some.Example.Package` and all of its dependencies, and store the converted packages in output/.:
```
NugetUpmRepackager -d . -f netstandard2.0 -o output -u 2021.3 Some.Example.Package@1.2.3
```

As a more real-life example: Convert a client package and a few extension packages, together with all of their combined dependencies into corresponding UPM packages under 
```
NugetUpmRepackager -o upm-pkg -f netstandard2.1 -u 2022.1 SGL.Community.Client@0.1.8 SGL.Community.Client.LocalData.Sqlite@0.1.8 SGL.Community.Client.QrCodes@0.1.8 Grpc.Net.Client.Web@2.56.0
```

## Operation

The tool uses `NuGet.Protocol` and `NuGet.Resolver` to download the indicated root packages, resolve their dependencies transitively and also download those.
For this transitive resolution, the framework indicated by the `-f` parameter is used, as NuGet packages can have different dependencies for different target frameworks.
NuGet configuration, e.g. for `<packageSources>` and `<packageSourceMapping>` can be done as normal through `NuGet.config`. User and machine settings are also loaded like with normal NuGet usage.
Package names are then mapped to UPM conventions as specified in the overrides file (see below).
For each package, a manifest is created from the NuGet package's metadata and from the Unity version given in the `-u` and `-r` parameters.
The package contents are filtered and remapped and overlays can be merged over the contents, as indicated by the overrides file.
For each content file, a `.meta` file with a random UUID is generated, where some known files get a specific content and all other get one with `DefaultImporter`.
Each non-empty directory also gets its own `.meta` file that Unity needs.
For more special / complex cases, a .meta file needs to be generated externally (e.g. by importing the manually once and configuring the import settings as needed) and can be injected using an overlay, which replaces the generated `.meta` file.
The known special types are:
- Managed Plugin `PluginImporter` for `*.dll` and `*.dll.config`
- `PackageManifestImporter` for the package manifest `package.json`
- `AssemblyDefinitionImporter` for assembly definitions `*.asmdef`
- `TextScriptImporter` for `*.json` and `*.xml`

The contents are then written to a `{packageId}-{version}.tgz` file as expected by UPM (which uses the same structure as NPM packages).

## Overrides

The transformation process is controlled by the file 'overrides.json' in the main operating directory.
It contains the following sections.

### Package Name Mappings

Under the key `NamePrefixMapping` a dictionary (string -> string) is expected that specifies how to map NuGet package names to UPM package names by prefix.
While both systems use a hierarchy convention where the root is on the left, NuGet packages are a bit more freely named with UPM being prefixed by a (reversed) domain by convention.
The `NamePrefixMapping` dictionary contains name prefixes of the NuGet packages as keys.
The entry with longest matching prefix (ignoring case) is used and the matching prefix in the package name is replaced by the corresponding value from the `NamePrefixMapping` dictionary.

As UPM also uses lower-case names and doesn't allow `_`, the resulting package name is then forced to lower-case and `_` is replaced by `-`.

Note that a catch-all case can be defined by specifiying an empty key / prefix string. It will be matched with the lowest priority but will match anything.
For example, the settings
```json
"NamePrefixMapping": {
  "SGL.": "de.hochschule-trier.sgl.",
  "": "org.nuget."
}
```
let all packages starting with `SGL.` be mapped to `de.hochschule-trier.sgl.` and all other packages (in this case all from the public registry) get the prefix `org.nuget.`.

### Content Path Mappings

Under the key `PathMapping` a list of path mapping entry objects is expected, that is used to map the intra-package location of content files and to filter the contents.
Each of these can have the following keys:
- `MatchPrefix`: A prefix string to match the (package root-relative) path of the content file. This is usually used to match directories.
- `MatchSuffix`: A prefix string to match the (package root-relative) path of the content file. This is usually used to match file suffixes or full file names.
- `ReplacePrefix`: If set, replaces the matched prefix in the content path with this string to produce the path in the output package. This allows putting files into a different directory in the UPM package.
- `ReplaceSuffix`: If set, replaces the matched suffix in the content path with this string to produce the path in the output package. This allows renaming files.

For an entry to potentially apply for a content file, both `MatchPrefix` and `MatchSuffix` must match the file path.
Note however that the match strings default to an empty string if not specified in `overrides.json`, which means that any path matches.
The path mapping entries are ordered by length of `MatchPrefix` and then (i.e. for equal length `MatchPrefix`) by length of `MatchSuffix` to prioritize longer, more specific matches.
The best match is then applied for the file in question.

For example, with the mapping
```json
"PathMapping": [
  {
    "MatchPrefix": "lib/netstandard2.1/",
    "ReplacePrefix": "Runtime/"
  },
  {
    "MatchPrefix": "lib/netstandard2.0/",
    "ReplacePrefix": "Runtime/"
  },
  {
    "MatchPrefix": "lib/netstandard2.1/",
    "MatchSuffix": ".xml",
    "ReplacePrefix": "Documentation~/"
  },
  {
    "MatchPrefix": "lib/netstandard2.0/",
    "MatchSuffix": ".xml",
    "ReplacePrefix": "Documentation~/"
  }
]
```
files from the `lib/netstandard2.1/` and `lib/netstandard2.0/` directories with names that end in `.xml` are put into the `Documentation~/` directory of the target package,
other files from the `lib/netstandard2.1/` and `lib/netstandard2.0/` directories are put into the `Runtime/` directory of the target package.
Files from outside those directory are kept in their original path.

### Package-Specific Override Settings

Under the key `PackageSpecific` a dictionary is expected, where the keys are (case-insensitve) full NuGet package names and the values are specific override settings for that package.
These apply when this package is being processed, if it appears in the dependency graph.

The package-specific override settings are objects that can contain the follwing keys:
- `PathMapping`: The same as the global `PathMapping`, but specific for this package.
- `IgnoreGlobalPathMapping`: Specifies how the package-specific and global `PathMapping` interact.
    - `false` (default): The ordered package-specific mappings are prepended to the ordered global mappings, giving them precedence over the global ones. The global ones however still apply to files not afected by package-specific ones.
    - `true`: The global mappings are ignored completely for this package.
- `ContentPathFilterPrefixes`: Expected to be either `null` (default) or a list of string path prefixes. If the list is present, only files for which their path starts with one of the listed strings are included in the output package at all, other files are discarded.
- `Overlays`: A dictionary (string -> string, empty by default) that allows injecting external files into the output package.
  The keys in the dictionary are the paths in the output package where to put the file.
  The value is a path under the main operating directory from where to copy the content.

For example, the following entry uses only the `lib/` directory and certain subdirectories of `runtimes/` from the source package.
It then (in the first block under `Overlays`) adds overlays for prepared `.meta` files that contain nativ plugin importer settings that constrain the native binaries from the source package to the appropriate plattforms (here Linux, OSX, and Windows).
This is a pattern that emerges for most packages that contain native code.
Afterwards, native binaries for Android and iOS are added, which are not normally included in this package, but would be pulled-in by plattform-specific dependencies in the parent package, which doesn't work in UPM.
For these binaries, corresponding `.meta` files are also needed and pulled-in.
Also note that for directories introduced by overlays, the automatic `.meta` generation for directories doesn't apply, as the overlays happen in a later stage.
Therefore there also need to be overlays to add those `.meta` files manually from a prepared one, 
as is the case for e.g. `runtimes/android-arm/native.meta` and `runtimes/android-arm.meta` because `runtime` doesn't have a `android-arm` subdirectory in the source package.
```json
"SQLitePCLRaw.lib.e_sqlite3": {
  "ContentPathFilterPrefixes": [
    "lib/",
    "runtimes/linux-x64/",
    "runtimes/osx-x64/",
    "runtimes/osx-arm64/",
    "runtimes/win-x64/",
    "runtimes/win-x86/"
  ],
  "Overlays": {
    "runtimes/linux-x64/native/libe_sqlite3.so.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/linux-x64/libe_sqlite3.so.meta",
    "runtimes/osx-x64/native/libe_sqlite3.dylib.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/osx-x64/libe_sqlite3.dylib.meta",
    "runtimes/osx-arm64/native/libe_sqlite3.dylib.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/osx-arm64/libe_sqlite3.dylib.meta",
    "runtimes/win-x64/native/e_sqlite3.dll.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/win-x64/e_sqlite3.dll.meta",
    "runtimes/win-x86/native/e_sqlite3.dll.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/win-x86/e_sqlite3.dll.meta",

    "runtimes/android-arm/native/libe_sqlite3.so": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm/libe_sqlite3.so",
    "runtimes/android-arm64/native/libe_sqlite3.so": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm64/libe_sqlite3.so",
    "runtimes/android-x64/native/libe_sqlite3.so": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x64/libe_sqlite3.so",
    "runtimes/android-x86/native/libe_sqlite3.so": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x86/libe_sqlite3.so",

    "runtimes/android-arm/native/libe_sqlite3.so.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm/libe_sqlite3.so.meta",
    "runtimes/android-arm64/native/libe_sqlite3.so.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm64/libe_sqlite3.so.meta",
    "runtimes/android-x64/native/libe_sqlite3.so.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x64/libe_sqlite3.so.meta",
    "runtimes/android-x86/native/libe_sqlite3.so.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x86/libe_sqlite3.so.meta",

    "runtimes/android-arm/native.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm/native.meta",
    "runtimes/android-arm64/native.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm64/native.meta",
    "runtimes/android-x64/native.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x64/native.meta",
    "runtimes/android-x86/native.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x86/native.meta",

    "runtimes/android-arm.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm.meta",
    "runtimes/android-arm64.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-arm64.meta",
    "runtimes/android-x64.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x64.meta",
    "runtimes/android-x86.meta": "overlays/SQLitePCLRaw.lib.e_sqlite3/android-x86.meta",

    "runtimes/ios.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios.meta",
    "runtimes/ios/native.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/native.meta",
    "runtimes/ios/native/e_sqlite3.a.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/e_sqlite3.a.meta",
    "runtimes/ios/native/e_sqlite3.a" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/e_sqlite3.a",
    "runtimes/ios/device.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/device.meta",
    "runtimes/ios/device/native.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/device/native.meta",
    "runtimes/ios/device/native/libe_sqlite3.dylib.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/device/libe_sqlite3.dylib.meta",
    "runtimes/ios/device/native/libe_sqlite3.dylib" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/device/libe_sqlite3.dylib",
    "runtimes/ios/simulator.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/simulator.meta",
    "runtimes/ios/simulator/native.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/simulator/native.meta",
    "runtimes/ios/simulator/native/libe_sqlite3.dylib.meta" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/simulator/libe_sqlite3.dylib.meta",
    "runtimes/ios/simulator/native/libe_sqlite3.dylib" : "overlays/SQLitePCLRaw.lib.e_sqlite3/ios/simulator/libe_sqlite3.dylib"
  }
}
```
