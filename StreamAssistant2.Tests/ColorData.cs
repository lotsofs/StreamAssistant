using Xunit;

// The colour registries are static, so tests that load data must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace StreamAssistant2.Tests {
	/// <summary>
	/// Points Config at a colour data directory and loads it into the static registries.
	/// </summary>
	public static class ColorData {
		public const string RealColors = @"D:\Repositories\Stream-Resources\Input\Bot\Colors";
		public const string RealColorSchemes = @"D:\Repositories\Stream-Resources\Input\Bot\ColorSchemes";

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
	/// A fact that only runs where the real Stream-Resources colour data exists; skipped elsewhere.
	/// </summary>
	public sealed class RealDataFactAttribute : FactAttribute {
		public RealDataFactAttribute() {
			if (!Directory.Exists(ColorData.RealColors) || !Directory.Exists(ColorData.RealColorSchemes)) {
				Skip = "Stream-Resources colour data not found on this machine.";
			}
		}
	}
}
