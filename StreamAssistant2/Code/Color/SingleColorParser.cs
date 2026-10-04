using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using static StreamAssistant2.ColorUtil;
using static StreamAssistant2.Coloring;

namespace StreamAssistant2 {
	/// <summary>
	/// Resolves one colour from a chat string. See CLAUDE.md for the order the forms are tried in.
	/// </summary>
	public static partial class SingleColorParser {
		[GeneratedRegex(@"^#([0-9A-Fa-f]{6})$")]
		private static partial Regex HexRegex();
		[GeneratedRegex(@"^rgb\s*\(\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)\s*\)$", RegexOptions.IgnoreCase)]
		private static partial Regex RgbRegex();
		[GeneratedRegex(@"^\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)\s*$", RegexOptions.IgnoreCase)]
		private static partial Regex PlainRgbRegex();
		[GeneratedRegex(@"^hsv\s*\(\s*(\d+)°?[\s,]+(\d+)%?[\s,]+(\d+)%?\s*\)$", RegexOptions.IgnoreCase)]
		private static partial Regex HsvRegex();

		public static bool TryParse(string input, out ColorEntry? colorEntry) {
			colorEntry = null;

			// EMPTY
			if (string.IsNullOrWhiteSpace(input)) {
				return false;
			}

			input = input.Trim();

			// #RRGGBB
			if (HexRegex().IsMatch(input)) {
				string hex = input.ToLowerInvariant();
				colorEntry = new ColorEntry("hex", hex, hex, Darken(hex), Lighten(hex));
				return true;
			}

			// rgb(123,123,123)
			var rgbMatch = RgbRegex().Match(input);
			if (rgbMatch.Success) {
				int r = ParseComponent(rgbMatch.Groups[1].Value);
				int g = ParseComponent(rgbMatch.Groups[2].Value);
				int b = ParseComponent(rgbMatch.Groups[3].Value);
				string hex = ToHex(r,g,b);
				colorEntry = new ColorEntry("rgb", $"rgb({r},{g},{b})", hex, Darken(hex), Lighten(hex));
				return true;
			}

			// hsv(123,123,123)
			var hsvMatch = HsvRegex().Match(input);
			if (hsvMatch.Success) {
				float h = float.Parse(hsvMatch.Groups[1].Value, CultureInfo.InvariantCulture);
				float sPercent = float.Parse(hsvMatch.Groups[2].Value, CultureInfo.InvariantCulture);
				float vPercent = float.Parse(hsvMatch.Groups[3].Value, CultureInfo.InvariantCulture);
				(int r, int g, int b) = HsvToRgb(h, sPercent/100f, vPercent/100f);
				string hex = ToHex(r,g,b);
				colorEntry = new ColorEntry("hsv", $"hsv({h}°,{sPercent}%,{vPercent}%)", hex, Darken(hex), Lighten(hex));
				return true;
			}

			// 123,123,123 (raw rgb)
			var plainRgbmatch = PlainRgbRegex().Match(input);
			if (plainRgbmatch.Success) {
				int r = ParseComponent(plainRgbmatch.Groups[1].Value);
				int g = ParseComponent(plainRgbmatch.Groups[2].Value);
				int b = ParseComponent(plainRgbmatch.Groups[3].Value);
				string hex = ToHex(r,g,b);
				colorEntry = new ColorEntry("rgb", $"rgb({r},{g},{b})", hex, Darken(hex), Lighten(hex));
				return true;
			}

			// named color
			if (TryGetNamed(input, out colorEntry)) {
				return true;
			}

			// system colors
			Color c = Color.FromName(input);
			if (c.IsKnownColor) {
				string hex = ToHex(c);
				colorEntry = new ColorEntry("system", input, hex, Darken(hex), Lighten(hex));
				return true;
			}

			// random
			if (input == "random") {
				colorEntry = GetRandomColor();
				return true;
			}

			// none
			return false;
		}

		/// <summary>
		/// Schemes (strict only), then table colours strictly, then table colours through
		/// ColorTable.TryGetLoose. So an exact spelling anywhere beats a loose match anywhere.
		/// </summary>
		static bool TryGetNamed(string input, out ColorEntry? colorEntry) {
			colorEntry = null;
			if (ColorSchemeRegistry.TryGetScheme(input, out ColorSchemeRegistry.ColorSchemeData? data)) {
				string source = data!.SetName;
				string name = $"{data.CategoryName}:{data.SchemeName}";
				colorEntry = new(source, name, data.Scheme.Inner, data.Scheme.Outer, data.Scheme.Text);
				return true;
			}
			foreach (bool loose in (bool[])[false, true]) {
				if (ColorTableRegistry.TryGetTableColor(input, loose, out NamedColor? colorInfo)) {
					string hex = colorInfo!.Hex;
					colorEntry = new(colorInfo.Source, colorInfo.OriginalName, hex, Darken(hex), Lighten(hex));
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Parse a given number falling back to 255 if overflowing int max
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		static int ParseComponent(string value) {
			return int.TryParse(value, out int parsed) ? parsed : 255;
		}
	}
}
