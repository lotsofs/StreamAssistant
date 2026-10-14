using Game = StreamAssistant2.Games.Game;

namespace StreamAssistant2 {
	internal static class GameScene {
		const string STREAM = "Basic Stream Layout";
		internal const string LAYOUTS = "!Scene: Layout ";
		internal const string GAMES = "!Scene: Games ";
		const string DEFAULT_ASPECT = "16x9";

		/// <summary>
		/// Shows the layout scene and the games scene for the game's aspect in Basic Stream Layout and hides the other ones
		/// there; logs and leaves a kind when OBS isn't ready or there's no scene of it for that aspect.
		/// </summary>
		internal static void Set(Game game) {
			if (!ObsConnection.IsReady) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "OBS not connected, layout and games scenes not set");
				return;
			}
			List<string> items = Obs.GetSceneItemNames(STREAM);
			foreach (string prefix in new[] { LAYOUTS, GAMES }) {
				Switch(Named(items, prefix), prefix, game.Aspect);
			}
		}

		/// <summary>
		/// Shows the aspect's scene among one kind and hides the others, or logs that there's none for it.
		/// </summary>
		static void Switch(List<string> scenes, string prefix, string? aspect) {
			if (Choose(scenes, prefix, aspect) is not var (show, hide)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {prefix}{aspect ?? DEFAULT_ASPECT} in {STREAM}, left as is");
				return;
			}
			foreach (string scene in hide.Prepend(show)) {
				try {
					Obs.SetSourceEnabled(STREAM, scene, scene == show);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't set {scene}: {ex.Message}");
				}
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"{STREAM} → {show}");
		}

		/// <summary>
		/// The games scenes among Basic Stream Layout's top-level items.
		/// </summary>
		internal static List<string> GamesScenes() {
			return Named(Obs.GetSceneItemNames(STREAM), GAMES);
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
