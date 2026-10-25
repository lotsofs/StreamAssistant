using Game = StreamAssistant2.Games.Game;

namespace StreamAssistant2 {
	internal static class GameScene {
		const string DEFAULT_ASPECT = "16x9";

		/// <summary>
		/// The stream scene, Basic Stream Layout.
		/// </summary>
		internal static string Stream => Settings.Data.Layout.StreamScene;

		/// <summary>
		/// The layout scenes' names before the aspect, with the space.
		/// </summary>
		internal static string LayoutsPrefix => Settings.Data.Layout.LayoutScenePrefix + " ";

		/// <summary>
		/// The games scenes' names before the aspect, with the space.
		/// </summary>
		internal static string GamesPrefix => Settings.Data.Layout.GamesScenePrefix + " ";

		/// <summary>
		/// Shows the layout scene and the games scene for the game's aspect in the stream scene and hides the other ones
		/// there; logs and leaves a kind when OBS isn't ready or there's no scene of it for that aspect.
		/// </summary>
		internal static void Set(Game game) {
			if (!ObsConnection.IsReady) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "OBS not connected, layout and games scenes not set");
				return;
			}
			string stream = Stream;
			List<string> items = Obs.GetSceneItemNames(stream);
			foreach (string prefix in new[] { LayoutsPrefix, GamesPrefix }) {
				Switch(stream, Named(items, prefix), prefix, game.Aspect);
			}
		}

		/// <summary>
		/// Shows the aspect's scene among one kind and hides the others, or logs that there's none for it.
		/// </summary>
		static void Switch(string stream, List<string> scenes, string prefix, string? aspect) {
			if (Choose(scenes, prefix, aspect) is not var (show, hide)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {prefix}{aspect ?? DEFAULT_ASPECT} in {stream}, left as is");
				return;
			}
			foreach (string scene in hide.Prepend(show)) {
				try {
					Obs.SetSourceEnabled(stream, scene, scene == show);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set {scene}: {ex.Message}");
				}
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"{stream} → {show}");
		}

		/// <summary>
		/// The games scenes among the stream scene's top-level items.
		/// </summary>
		internal static List<string> GamesScenes() {
			return Named(Obs.GetSceneItemNames(Stream), GamesPrefix);
		}

		/// <summary>
		/// The items named "&lt;prefix&gt;&lt;aspect&gt;", in order.
		/// </summary>
		internal static List<string> Named(IEnumerable<string> items, string prefix) {
			return items.Where(item => item.StartsWith(prefix, StringComparison.Ordinal)).Distinct().ToList();
		}

		/// <summary>
		/// The scene to show for the aspect (16x9 without one; any case) and the others to hide; null when none is for
		/// that aspect.
		/// </summary>
		internal static (string Show, List<string> Hide)? Choose(IReadOnlyList<string> scenes, string prefix, string? aspect) {
			string wanted = prefix + (aspect ?? DEFAULT_ASPECT);
			string? show = scenes.FirstOrDefault(scene => scene.Equals(wanted, StringComparison.OrdinalIgnoreCase));
			return show == null ? null : (show, scenes.Where(scene => scene != show).ToList());
		}
	}
}
