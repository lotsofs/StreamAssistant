using System.Drawing;
using System.Globalization;

namespace StreamAssistant2 {
	/// <summary>
	/// Colour maths: hex helpers, the Darken/Lighten pair that turns a single colour into a
	/// triple, OBS conversion and HSV → RGB.
	/// </summary>
	public static class ColorUtil {
		/// <summary>
		/// Splits #RRGGBB (leading '#' optional) into its components.
		/// </summary>
		static (int r, int g, int b) ParseHex(string hex) {
			ReadOnlySpan<char> digits = hex.AsSpan().TrimStart('#');
			int r = int.Parse(digits[0..2], NumberStyles.HexNumber);
			int g = int.Parse(digits[2..4], NumberStyles.HexNumber);
			int b = int.Parse(digits[4..6], NumberStyles.HexNumber);
			return (r, g, b);
		}

		/// <summary>
		/// Accepts RRGGBB with or without a leading '#', in any case, and returns it as #rrggbb
		/// lowercase. Used at load so everything downstream can trust table and scheme hex.
		/// </summary>
		public static bool TryNormalizeHex(string? value, out string hex) {
			hex = "";
			if (value == null) {
				return false;
			}
			ReadOnlySpan<char> digits = value.AsSpan().Trim();
			if (digits.StartsWith("#")) {
				digits = digits[1..];
			}
			if (digits.Length != 6) {
				return false;
			}
			foreach (char c in digits) {
				if (!char.IsAsciiHexDigit(c)) {
					return false;
				}
			}
			hex = "#" + digits.ToString().ToLowerInvariant();
			return true;
		}

		public static string Darken(string hex) {
			(int r, int g, int b) = ParseHex(hex);
			r /= 2;
			g /= 2;
			b /= 2;
			return $"#{r:x2}{g:x2}{b:x2}";
		}
		public static Color Darken(Color color) {
			return Color.FromArgb(color.R / 2, color.G / 2, color.B / 2);
		}

		public static string Lighten(string hex) {
			(int r, int g, int b) = ParseHex(hex);
			r += (255-r)/2;
			g += (255-g)/2;
			b += (255-b)/2;
			return $"#{r:x2}{g:x2}{b:x2}";
		}
		public static Color Lighten(Color color) {
			return Color.FromArgb(
				color.R + (255 - color.R) / 2,
				color.G + (255 - color.G) / 2,
				color.B + (255 - color.B) / 2
			);
		}

		public static string ToHex(Color color) {
			return $"#{color.R:x2}{color.G:x2}{color.B:x2}";
		}
		public static string ToHex(int r, int g, int b) {
			r = Math.Clamp(r,0,255);
			g = Math.Clamp(g,0,255);
			b = Math.Clamp(b,0,255);
			return $"#{r:x2}{g:x2}{b:x2}";
		}

		/// <summary>
		/// #RRGGBB to the opaque ABGR value OBS colour filters expect.
		/// </summary>
		public static long ToOBS(string hex) {
			(int r, int g, int b) = ParseHex(hex);
			return 0xFF000000L | (long)r | ((long)g << 8) | ((long)b << 16);
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
			return (
				(int)Math.Round((r1+m) * 255),
				(int)Math.Round((g1+m) * 255),
				(int)Math.Round((b1+m) * 255)
			);
		}
	}
}
