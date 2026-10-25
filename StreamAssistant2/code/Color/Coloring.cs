using static StreamAssistant2.ColorUtil;

namespace StreamAssistant2 {
	public static class Coloring {
		public sealed record ColorEntry(string Source, string Name, string Hex1, string Hex2, string Hex3);

		/// <summary>
		/// Loads the colour schemes and tables, and logs how many.
		/// </summary>
		public static void Load() {
			ColorSchemeRegistry.LoadSets();
			ColorTableRegistry.LoadTables();
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Loaded {ColorTableRegistry.Tables.Count} colour tables and {ColorSchemeRegistry.Sets.Count} scheme sets");
		}

		/// <summary>
		/// The JSON files in a colour folder; null, logged, when it can't be listed.
		/// </summary>
		internal static string[]? ListFiles(string directory) {
			try {
				return Directory.GetFiles(directory, "*.json");
			}
			catch (Exception ex) {
				LoadFailed(directory, ex);
				return null;
			}
		}

		/// <summary>
		/// Logs a colour file or folder that couldn't be loaded.
		/// </summary>
		internal static void LoadFailed(string path, Exception ex) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error CLR1: can't load {path}: {ex.Message}");
			ConsoleLogger.LogToFile(ex);
		}

		public static ColorEntry GetRandomColor() {
			var color = ColorTableRegistry.GetRandomColor();
			var hex = color.Hex;
			return new ColorEntry(color.Source, color.OriginalName, hex, Darken(hex), Lighten(hex));
		}
	}
}
