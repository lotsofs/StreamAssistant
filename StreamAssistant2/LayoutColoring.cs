using System.Threading.Channels;

namespace StreamAssistant2 {
	internal static class LayoutColoring {
		public record ColorRequest(string Inner, string Outer, string Text);

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
			if (TripleColorParser.TryParse(input, out Coloring.ColorEntry? color)) {
				ChangeColor(color!);
				return true;
			}
			return false;
		}

		public static bool TryChangeToSingle(string input) {
			if (SingleColorParser.TryParse(input, out Coloring.ColorEntry? color)) {
				ChangeColor(color!);
				return true;
			}
			return false;
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
		public static void QueueLayoutColorChange(string inner, string outer, string text) {
			if (!_colorChangeQueue.Writer.TryWrite(new ColorRequest(inner, outer, text))) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"QueueLayoutColorChange failed ({inner}, {outer}, {text})");
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

		static async Task ChangeColorAsync(ColorRequest req) {
			string filterName = "Color Correction";
			long colInner = ColorUtil.ToOBS(req.Inner);
			long colOuter = ColorUtil.ToOBS(req.Outer);
			long colText = ColorUtil.ToOBS(req.Text);
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Changing layout color to {colInner} {colOuter} {colText}");

			Obs.SetFilterProperty("Border: Colorable Inner (Transitionary)", filterName, "color_multiply", colInner);
			Obs.SetFilterProperty("Border: Colorable Outer (Transitionary)", filterName, "color_add", colOuter);
			Obs.SetFilterProperty("!Scene: All Colorable (Transitionary)", filterName, "color_multiply", colText);
			Obs.SetSourceEnabled("!Scene: Layout", "!Layout: Colorables (Transitionary)", true);
			await Task.Delay(5500);
			Obs.SetSourceEnabled("!Scene: Layout", "!Layout: Colorables", false);
			await Task.Delay(2500);
			Obs.SetFilterProperty("Border: Colorable Inner", filterName, "color_multiply", colInner);
			Obs.SetFilterProperty("Border: Colorable Outer", filterName, "color_add", colOuter);
			Obs.SetFilterProperty("!Scene: All Colorable", filterName, "color_multiply", colText);
			await Task.Delay(1000);
			Obs.SetSourceEnabled("!Scene: Layout", "!Layout: Colorables", true);
			Obs.SetSourceEnabled("!Scene: Layout", "!Layout: Colorables (Transitionary)", false);
		}
	}
}