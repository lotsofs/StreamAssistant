using Xunit;

// The colour registries are static, so tests that load data must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
// The bot assembly is Windows-only, so the tests that call into it are too.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace StreamAssistant2.Tests {
	/// <summary>
	/// Points Config at a colour data directory and loads it into the static registries.
	/// </summary>
	public static class ColorData {
		// The real colour directories, read from the bot's own paths.json so the smoke test follows
		// wherever the data is moved. Null when there is no paths.json (e.g. a fresh clone).
		static readonly DirectoriesConfig? _real = ReadRealDirectories();
		public static string? RealColors => _real?.Colors;
		public static string? RealColorSchemes => _real?.ColorSchemes;

		static DirectoriesConfig? ReadRealDirectories() {
			// This source file's own path, fixed at compile time, so it works wherever the build output goes:
			// <repo>/StreamAssistant2.Tests/ColorData.cs → <repo>/StreamAssistant2/paths.json.
			string repo = Path.GetDirectoryName(Path.GetDirectoryName(SourcePath()))!;
			string paths = Path.Combine(repo, "StreamAssistant2", "paths.json");
			if (!File.Exists(paths)) {
				return null;
			}
			var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			return System.Text.Json.JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(paths), options)?.Directories;
		}

		static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

		public static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

		public static void Load(string colors, string colorSchemes) {
			Config.Data.Directories.Colors = colors;
			Config.Data.Directories.ColorSchemes = colorSchemes;
			Coloring.Load();
		}

		public static void LoadFixtures() => Load(Fixture("Colors"), Fixture("ColorSchemes"));
	}

	public class ColorDataFixture {
		public ColorDataFixture() => ColorData.LoadFixtures();
	}

	/// <summary>
	/// Every test that reads the registries joins this collection, so the fixtures are loaded first.
	/// </summary>
	[CollectionDefinition(Name)]
	public class ColorDataCollection : ICollectionFixture<ColorDataFixture> {
		public const string Name = "ColorData";
	}

	/// <summary>
	/// A fact that only runs where paths.json exists and names colour directories that exist.
	/// </summary>
	public sealed class RealDataFactAttribute : FactAttribute {
		public RealDataFactAttribute() {
			if (ColorData.RealColors == null || ColorData.RealColorSchemes == null) {
				Skip = "No StreamAssistant2/paths.json found above the test output.";
			}
			else if (!Directory.Exists(ColorData.RealColors) || !Directory.Exists(ColorData.RealColorSchemes)) {
				Skip = $"paths.json names colour directories that don't exist: {ColorData.RealColors}, {ColorData.RealColorSchemes}";
			}
		}
	}
}
