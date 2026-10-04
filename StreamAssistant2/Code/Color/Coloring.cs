using static StreamAssistant2.ColorUtil;

namespace StreamAssistant2 {
	public static class Coloring {
		public sealed record ColorEntry(string Source, string Name, string Hex1, string Hex2, string Hex3);

		public static void Load() {
			ColorSchemes.LoadSets();
			ColorTables.LoadTables();
		}

		public static ColorEntry GetRandomColor() {
			var color = ColorTables.GetRandomColor();
			var hex = color.Hex;
			return new ColorEntry(color.Source, color.OriginalName, hex, Darken(hex), Lighten(hex));
		}
	}
}
