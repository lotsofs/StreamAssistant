using System.Globalization;

namespace StreamAssistant2 {
	internal static class GameSidebar {
		const string FOLDER = "Dynamic";

		static readonly (string Source, string Series)[] _sources = [
			("Sidebar: Dynamic 1", ""),
			("Sidebar: Dynamic 2", "_2"),
		];

		static readonly object _lock = new();
		static string? _game;
		static readonly Dictionary<string, string> _shown = new();

		/// <summary>
		/// Makes the game the one whose images follow LiveSplit's split index, and shows its images for the current index.
		/// </summary>
		internal static void SetGame(string gameName) {
			lock (_lock) {
				_game = gameName;
				_shown.Clear();
			}
			Update();
		}

		/// <summary>
		/// On a change of LiveSplit's split index: shows the game's images for it.
		/// </summary>
		internal static void OnIndexChanged() {
			Update();
		}

		/// <summary>
		/// On an OBS connect: shows the images again, off the connecting thread, since OBS may have missed changes while it
		/// was closed.
		/// </summary>
		internal static void OnObsConnected() {
			lock (_lock) {
				if (_game == null) {
					return;
				}
				_shown.Clear();
			}
			FireForget.Run("SDB1", "sidebar on OBS connect", async () => {
				await Task.Yield();
				Update();
			});
		}

		/// <summary>
		/// Points each dynamic sidebar source at the game's image in its series for the split index now, unless it already
		/// shows it, the game has none or OBS isn't ready. An OBS failure is logged, not thrown.
		/// </summary>
		static void Update() {
			lock (_lock) {
				if (_game == null || !ObsConnection.IsReady) {
					return;
				}
				int index = SplitWatcher.CurrentIndex ?? -1;
				string directory = Config.Data.Directories.Sidebar;
				string folder = Path.Combine(directory, FOLDER);
				if (string.IsNullOrEmpty(directory) || !Directory.Exists(folder)) {
					return;
				}
				List<string> files = Directory.EnumerateFiles(folder, "*.png").Select(Path.GetFileName).OfType<string>().ToList();
				foreach (var (source, series) in _sources) {
					string? file = Choose(files, _game + series, index);
					if (file == null || _shown.GetValueOrDefault(source) == file) {
						continue;
					}
					try {
						Obs.SetImageSource(source, Path.Combine(folder, file));
						_shown[source] = file;
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChangesUnimportant, $"{source} → {file}");
					}
					catch (Exception ex) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set {source}: {ex.Message}");
					}
				}
			}
		}

		/// <summary>
		/// The image for a split index among the file names: &lt;series&gt;_&lt;n&gt;.png with n the index + 1, so 0 before
		/// a run; else the highest n below that, else the lowest. Null when the series has none.
		/// </summary>
		internal static string? Choose(IEnumerable<string> files, string series, int index) {
			int wanted = Math.Max(index + 1, 0);
			List<(string File, int Number)> numbered = files.Select(file => (File: file, Number: Number(file, series)))
				.Where(f => f.Number != null).Select(f => (f.File, f.Number!.Value)).OrderBy(f => f.Item2).ToList();
			if (numbered.Count == 0) {
				return null;
			}
			return numbered.LastOrDefault(f => f.Number <= wanted).File ?? numbered[0].File;
		}

		/// <summary>
		/// The number in a file named &lt;series&gt;_&lt;digits&gt;.png (any case), or null for any other name.
		/// </summary>
		static int? Number(string file, string series) {
			string prefix = series + "_";
			if (!file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			string digits = file[prefix.Length..^4];
			return digits.Length > 0 && digits.All(char.IsAsciiDigit) && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : null;
		}
	}
}
