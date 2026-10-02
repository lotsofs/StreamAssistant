// using Newtonsoft.Json;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StreamAssistant2 {
	public static class Coloring {
		public sealed record ColorEntry(string Source, string Name, string Hex1, string Hex2, string Hex3);

		static readonly Regex HexRegex = new(@"^#([0-9A-Fa-f]{6})$");
		static readonly Regex RgbRegex = new(@"^rgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$", RegexOptions.IgnoreCase);
		static readonly Regex PlainRgbRegex = new(@"^\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*$", RegexOptions.IgnoreCase);
		static readonly Regex HsvRegex = new(@"^hsv\s*\(\s*(\d+)°?\s*,\s*(\d+)%?\s*,\s*(\d+)%?\s*\)$", RegexOptions.IgnoreCase);

		public static void Load() {
			ColorSchemes.LoadSets();
			ColorTables.LoadTables();
		}

		public static ColorEntry GetRandomColor() {
			var color = ColorTables.GetRandomColor();
			var hex = color.Hex;
			return new ColorEntry(color.Source, color.OriginalName, hex.ToUpperInvariant(), Darken(hex), Lighten(hex));
		}

		static bool TryGetNamed(string input, out ColorEntry? colorEntry) {
			colorEntry = null;
			if (ColorSchemes.TryGetScheme(input, out ColorSchemes.ColorSchemeData? data)) {
				string source = data!.SetName;
				string name = $"{data.CategoryName}:{data.SchemeName}";
				colorEntry = new(source, name, data.Scheme.Inner, data.Scheme.Outer, data.Scheme.Text);
				return true;
			}
			if (ColorTables.TryGetTableColor(input, out ColorTables.NamedColor? colorInfo)) {
				string hex = colorInfo!.Hex;
				colorEntry = new(colorInfo.Source, colorInfo.OriginalName, hex, Darken(hex), Lighten(hex));
				return true;
			}
			return false;
		}

		public static bool TryGetTriple(string input, out ColorEntry? colorEntry) {
			colorEntry = null;
			
			// EMPTY
			if (string.IsNullOrWhiteSpace(input)) {
				return false;
			}
			ColorEntry[] entries = new ColorEntry[3];
			int foundEntries = 0;

			input = input.Trim();
			input = input.Replace(',',' ');
			input = input.Replace(';',' ');
			string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			for (int s = 0; s < parts.Length; s++) {
				if (foundEntries >= 3) break;
				for (int e = parts.Length - 1; e >= s; e--) {
					if (foundEntries >= 3) break;
					string combined = string.Join(" ", parts, s, e-s+1);
					if (TryGetSingle(combined, out ColorEntry? entry)) {
						entries[foundEntries] = entry!;
						foundEntries++;
						s = e;
					}
				}
			}
			if (entries[0] == null) {
				return false;
			}
			if (entries[1] == null) {
				entries[1] = entries[0];
			}
			if (entries[2] == null) {
				entries[2] = entries[0];
			}
			string source = $"{entries[0].Source}|{entries[1].Source}|{entries[2].Source}";
			string name = $"{entries[0].Name}|{entries[1].Name}|{entries[2].Name}";
			colorEntry = new ColorEntry(source, name, entries[0].Hex1, entries[1].Hex1, entries[2].Hex1);
			return true;
		}

		public static bool TryGetSingle(string input, out ColorEntry? colorEntry) {
			colorEntry = null;

			// EMPTY
			if (string.IsNullOrWhiteSpace(input)) {
				return false;
			}

			input = input.Trim();

			// #RRGGBB
			if (HexRegex.IsMatch(input)) {
				colorEntry = new ColorEntry("hex", input, input.ToUpperInvariant(), Darken(input), Lighten(input));
				return true;
			}

			// rgb(123,123,123)
			var rgbMatch = RgbRegex.Match(input);
			if (rgbMatch.Success) {
				int r = int.Parse(rgbMatch.Groups[1].Value);
				int g = int.Parse(rgbMatch.Groups[2].Value);
				int b = int.Parse(rgbMatch.Groups[3].Value);
				string hex = ToHex(r,g,b);
				colorEntry = new ColorEntry("rgb", $"rgb({r},{g},{b})", hex, Darken(hex), Lighten(hex));
				return true;
			}

			// hsv(123,123,123)
			var hsvMatch = HsvRegex.Match(input);
			if (hsvMatch.Success) {
				float h = float.Parse(hsvMatch.Groups[1].Value, CultureInfo.InvariantCulture);
				float s = float.Parse(hsvMatch.Groups[2].Value, CultureInfo.InvariantCulture) / 100f;
				float v = float.Parse(hsvMatch.Groups[3].Value, CultureInfo.InvariantCulture) / 100f;
				(int r, int g, int b) = HsvToRgb(h,s,v);
				string hex = ToHex(r,g,b);
				colorEntry = new ColorEntry("hsv", $"hsv({h}°,{s}%,{v}%)", hex, Darken(hex), Lighten(hex));
				return true;
			}
			
			// 123,123,123 (raw rgb)
			var plainRgbmatch = PlainRgbRegex.Match(input);
			if (plainRgbmatch.Success) {
				int r = int.Parse(plainRgbmatch.Groups[1].Value);
				int g = int.Parse(plainRgbmatch.Groups[2].Value);
				int b = int.Parse(plainRgbmatch.Groups[3].Value);
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

		#region util funcs

		public static string Darken(string hex) {
			hex = hex.TrimStart('#');
			int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			r /= 2;
			g /= 2;
			b /= 2;
			return $"#{r:X2}{g:X2}{b:X2}";
		}
		public static Color Darken(Color color) {
			return Color.FromArgb(color.R / 2, color.G / 2, color.B / 2);
		}

		public static string Lighten(string hex) {
			hex = hex.TrimStart('#');
			int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			r += (255-r)/2;
			g += (255-g)/2;
			b += (255-b)/2;
			return $"#{r:X2}{g:X2}{b:X2}";
		}
		public static Color Lighten(Color color) {
			return Color.FromArgb(
				color.R + (255 - color.R) / 2,
				color.G + (255 - color.G) / 2,
				color.B + (255 - color.B) / 2
			);
		}

		public static string ToHex(Color color) {
			return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
		}
		public static string ToHex(int r, int g, int b) {
			r = Math.Clamp(r,0,255);
			g = Math.Clamp(g,0,255);
			b = Math.Clamp(b,0,255);
			return $"#{r:X2}{g:X2}{b:X2}";
		}

		public static long ToOBS(string c) {
			string hex = c.TrimStart('#');
			uint r = uint.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			uint g = uint.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			uint b = uint.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			return 0xFF000000L | r | (g << 8) | (b << 16);
		}

		public static (int r, int g, int b) HsvToRgb(double h, double s, double v) {
			// https://en.wikipedia.org/wiki/HSL_and_HSV#Color_conversion_formulae
			h = Util.TrueModulo(h, 360);
			s = Math.Clamp(s, 0, 1);
			v = Math.Clamp(v, 0, 1);
			double c = v * s;
			double hPrime = h/60;
			double x = c * (1 - Math.Abs(hPrime%2-1));
			
			double r1 = 0;
			double g1 = 0;
			double b1 = 0;
			if (0 <= hPrime && hPrime < 1) { r1=c; g1=x; b1=0; }
			else if (1 <= hPrime && hPrime < 2) { r1=x; g1=c; b1=0; }
			else if (2 <= hPrime && hPrime < 3) { r1=0; g1=c; b1=x; }
			else if (3 <= hPrime && hPrime < 4) { r1=0; g1=x; b1=c; }
			else if (4 <= hPrime && hPrime < 5) { r1=x; g1=0; b1=c; }
			else if (5 <= hPrime && hPrime < 6) { r1=c; g1=0; b1=x; }
			
			double m = v - c;
			return ((int)(r1+m),(int)(g1+m),(int)(b1+m));
		}

		#endregion

	}
}
