using System.Globalization;
using System.Text.Json;

namespace StreamAssistant2 {
	/// <summary>
	/// Null-safe reads from EventSub payloads by dotted path ("sub.sub_tier"). A missing step, a null, or
	/// a value of the wrong kind gives the fallback instead of throwing; Twitch sends null for absent values.
	/// </summary>
	internal static class JsonElementExtensions {
		/// <summary>The element at the path, or an Undefined element if any step is missing or null.</summary>
		internal static JsonElement ReadElement(this JsonElement element, string path) {
			JsonElement current = element;
			foreach (string step in path.Split('.')) {
				if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(step, out current)) {
					return default;
				}
			}
			return current.ValueKind == JsonValueKind.Null ? default : current;
		}

		internal static string ReadString(this JsonElement element, string path, string fallback = "") {
			JsonElement value = element.ReadElement(path);
			return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
		}

		/// <summary>A number, or a string holding one (Twitch sends sub_tier and the ad fields as strings).</summary>
		internal static int ReadInt(this JsonElement element, string path, int fallback = 0) {
			JsonElement value = element.ReadElement(path);
			return value.ValueKind switch {
				JsonValueKind.Number when value.TryGetInt32(out int n) => n,
				JsonValueKind.String when int.TryParse(value.GetString(), out int n) => n,
				_ => fallback,
			};
		}

		/// <summary>A number, or a string holding one with a dot.</summary>
		internal static decimal ReadDecimal(this JsonElement element, string path, decimal fallback = 0) {
			JsonElement value = element.ReadElement(path);
			return value.ValueKind switch {
				JsonValueKind.Number when value.TryGetDecimal(out decimal d) => d,
				JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d) => d,
				_ => fallback,
			};
		}

		/// <summary>true/false, or a string holding one.</summary>
		internal static bool ReadBool(this JsonElement element, string path, bool fallback = false) {
			JsonElement value = element.ReadElement(path);
			return value.ValueKind switch {
				JsonValueKind.True => true,
				JsonValueKind.False => false,
				JsonValueKind.String when bool.TryParse(value.GetString(), out bool b) => b,
				_ => fallback,
			};
		}
	}
}
