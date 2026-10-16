using System.Threading.Channels;

namespace StreamAssistant2 {
	internal static class LayoutColoring {
		public record ColorRequest(string Inner, string Outer, string Text);

		internal const string FILTER = "Color Correction";
		const string LAYOUT_PREFIX = "!Scene: Layout ";
		internal const string COLORABLES = "!Layout: Colorables";
		internal const string TWIN = " (Transitionary)";
		internal const string INNER = "Border: Colorable Inner";
		internal const string INNER_ATTACHMENT = "Border: Colorable Inner Attachment";
		internal const string OUTER = "Border: Colorable Outer";
		internal const string OUTER_ATTACHMENT = "Border: Colorable Outer Attachment";
		internal const string TEXT_SCENE = "!Scene: All Colorable";

		static readonly Channel<ColorRequest> _colorChangeQueue = Channel.CreateUnbounded<ColorRequest>();

		static bool _running = false;

		public static void StartWorker() {
			if (_running) { 
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Attempt to start color worker twice");
				return; 
			}
			_ = Task.Run(ProcessColorChangeQueueAsync);
			_running = true;
		}

		static void ChangeColor(Coloring.ColorEntry color) {
			ColorRequest r = new ColorRequest(color.Hex1, color.Hex2, color.Hex3);
			QueueLayoutColorChange(r);
			TwitchIRCManager.SendMessage($"Changing to color {color.Name} [{color.Source}]: {color.Hex1} {color.Hex2} {color.Hex3}");
		}

		public static bool TryChangeToTriple(string input) {
			bool success = TripleColorParser.TryParse(input, out Coloring.ColorEntry? color);
			if (success) {
				ChangeColor(color!);
			}
			ReportRequest(input, success);
			return success;
		}

		public static bool TryChangeToSingle(string input) {
			bool success = SingleColorParser.TryParse(input, out Coloring.ColorEntry? color);
			if (success) {
				ChangeColor(color!);
			}
			ReportRequest(input, success);
			return success;
		}

		static void ReportRequest(string input, bool success) {
			if (!success) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Color change request FAILED: {input}");
				TwitchIRCManager.SendMessage($"🎨 Couldn't find a color called \"{input}\"");
			}
		}

		public static void ChangeToRandom() {
			Coloring.ColorEntry color = Coloring.GetRandomColor();
			ChangeColor(color);
		}

		public static void QueueLayoutColorChange(ColorRequest req) {
			if (!_colorChangeQueue.Writer.TryWrite(req)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"QueueLayoutColorChange failed ({req.Inner}, {req.Outer}, {req.Text})");
			}
		}

		static async Task ProcessColorChangeQueueAsync() {
			await foreach (var request in _colorChangeQueue.Reader.ReadAllAsync()) {
				try {
					await ChangeColorAsync(request);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error Obs29");
					ConsoleLogger.LogToFile(ex);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, ex.Message);
				}
			}
		}

		/// <summary>
		/// Fades the layout to the new colours: colours the fade twins and shows their group over the real one in every
		/// layout scene, hides the real group, colours it, then shows it again and hides the twins. A source or scene
		/// that can't be set is logged and the rest still go.
		/// </summary>
		static async Task ChangeColorAsync(ColorRequest req) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChanges, $"Changing layout color to {ColorUtil.ToOBS(req.Inner)} {ColorUtil.ToOBS(req.Outer)} {ColorUtil.ToOBS(req.Text)}");
			List<string> layouts = LayoutScenes(Obs.GetSceneNames());
			if (layouts.Count == 0 && ObsConnection.IsConnected()) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No {LAYOUT_PREFIX}<aspect> scenes, layout colour changes without the fade");
			}

			SetFilters(Filters(req, twins: true));
			Toggle(layouts, COLORABLES + TWIN, true);
			await Task.Delay(5500);
			Toggle(layouts, COLORABLES, false);
			await Task.Delay(2500);
			SetFilters(Filters(req, twins: false));
			await Task.Delay(1000);
			Toggle(layouts, COLORABLES, true);
			Toggle(layouts, COLORABLES + TWIN, false);
		}

		/// <summary>
		/// The colour filter settings for the colourable sources, or for their fade twins: the inner border and its
		/// attachment multiply by the inner colour, the outer ones add the outer colour, and the colourable scene
		/// multiplies by the text colour.
		/// </summary>
		internal static List<(string Source, string Property, long Color)> Filters(ColorRequest req, bool twins) {
			string twin = twins ? TWIN : "";
			long inner = ColorUtil.ToOBS(req.Inner);
			long outer = ColorUtil.ToOBS(req.Outer);
			long text = ColorUtil.ToOBS(req.Text);
			return [
				(INNER + twin, "color_multiply", inner),
				(INNER_ATTACHMENT + twin, "color_multiply", inner),
				(OUTER + twin, "color_add", outer),
				(OUTER_ATTACHMENT + twin, "color_add", outer),
				(TEXT_SCENE + twin, "color_multiply", text),
			];
		}

		/// <summary>
		/// The layout scenes among the scene names: those named "!Scene: Layout &lt;aspect&gt;".
		/// </summary>
		internal static List<string> LayoutScenes(IEnumerable<string> scenes) {
			return scenes.Where(scene => scene.StartsWith(LAYOUT_PREFIX, StringComparison.Ordinal)).ToList();
		}

		/// <summary>
		/// Sets each colour on its source's colour filter, logging any that fails.
		/// </summary>
		static void SetFilters(List<(string Source, string Property, long Color)> filters) {
			foreach (var (source, property, color) in filters) {
				try {
					Obs.SetFilterProperty(source, FILTER, property, color);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't colour {source}: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// Shows or hides an item in each layout scene, logging any that fails.
		/// </summary>
		static void Toggle(List<string> layouts, string item, bool on) {
			foreach (string layout in layouts) {
				try {
					Obs.SetSourceEnabled(layout, item, on);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Couldn't {(on ? "show" : "hide")} {item} in {layout}: {ex.Message}");
				}
			}
		}
	}
}