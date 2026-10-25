using System.Globalization;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;

namespace StreamAssistant2 {
	internal static class Obs {
		static OBSWebsocket _obs = ObsConnection.ObsSocket;
		
		public static void SetSourceEnabled(string sceneName, string sourceName, bool enabled) {
			if (!ObsConnection.IsConnected()) return;
			var sceneItemId = _obs.GetSceneItemId(sceneName, sourceName, 0);
			_obs.SetSceneItemEnabled(sceneName, sceneItemId, enabled);
			ObsInputs.Current.SetVisible(sceneName, sourceName, enabled);
		}

		/// <summary>
		/// The names of a scene's top-level sources; empty when not connected.
		/// </summary>
		public static List<string> GetSceneItemNames(string sceneName) {
			if (!ObsConnection.IsConnected()) return [];
			return _obs.GetSceneItemList(sceneName).Select(item => item.SourceName).ToList();
		}

		/// <summary>
		/// The names of every scene; empty when not connected.
		/// </summary>
		public static List<string> GetSceneNames() {
			if (!ObsConnection.IsConnected()) return [];
			return _obs.GetSceneList().Scenes.Select(scene => scene.Name).ToList();
		}

		/// <summary>
		/// A scene's items, or a group's, each with whether it's enabled and whether it's a scene or group of its own; null
		/// when OBS isn't ready or refuses (no such scene or group). Sent as raw requests, since the library's own scene
		/// items leave out the enabled state and its group listing doesn't work.
		/// </summary>
		public static List<ObsInputs.SceneItem>? SceneItems(string name, bool group) {
			if (!ObsConnection.IsReady) return null;
			try {
				JObject response = _obs.SendRequest(group ? "GetGroupSceneItemList" : "GetSceneItemList", new JObject { ["sceneName"] = name });
				if ((response["sceneItems"] ?? response["responseData"]?["sceneItems"]) is not JArray items) {
					return null;
				}
				return items.OfType<JObject>().Select(item => new ObsInputs.SceneItem(
					(string?)item["sourceName"] ?? "",
					(bool?)item["sceneItemEnabled"] ?? false,
					(string?)item["sourceType"] == "OBS_SOURCE_TYPE_SCENE",
					(bool?)item["isGroup"] ?? false)).ToList();
			}
			catch (ErrorResponseException) {
				return null;
			}
		}

		/// <summary>
		/// The names of every input in OBS; null when OBS isn't ready or refuses. Sent as a raw request, like SceneItems.
		/// </summary>
		public static List<string>? InputNames() {
			if (!ObsConnection.IsReady) return null;
			try {
				JObject response = _obs.SendRequest("GetInputList", new JObject());
				if ((response["inputs"] ?? response["responseData"]?["inputs"]) is not JArray inputs) {
					return null;
				}
				return inputs.OfType<JObject>().Select(input => (string?)input["inputName"]).OfType<string>().ToList();
			}
			catch (ErrorResponseException) {
				return null;
			}
		}

		public static void SetImageSource(string sourceName, string newFile) {
			SetInputSetting(sourceName, "file", newFile);
		}

		/// <summary>
		/// Sets one setting on an input, keeping the rest.
		/// </summary>
		public static void SetInputSetting(string inputName, string key, string value) {
			if (!ObsConnection.IsConnected()) return;
			var set = new JObject {
				[key] = value
			};
			Recorded(inputName, key, ObsInputs.KindOf(key, value), value, () => _obs.SetInputSettings(inputName, set, true));
		}

		/// <summary>
		/// Sets an input's volume in dB (OBS takes -100 to 26; -100 and below is silent).
		/// </summary>
		public static void SetInputVolumeDb(string inputName, double db) {
			if (!ObsConnection.IsConnected()) return;
			string shown = db.ToString("0.##", CultureInfo.InvariantCulture);
			Recorded(inputName, "volume", ObsInputs.Kind.Volume, shown, () => _obs.SetInputVolume(inputName, (float)db, true));
		}

		/// <summary>
		/// Sets one property on a source's filter, keeping the rest.
		/// </summary>
		public static void SetFilterProperty(string sourceName, string filterName, string propertyName, JToken value) {
			if (!ObsConnection.IsConnected()) return;

			var settings = new JObject {
				[propertyName] = value,
			};
			bool color = value.Type == JTokenType.Integer && propertyName.StartsWith("color", StringComparison.Ordinal);
			string shown = color ? ObsInputs.ColorHex(value.Value<long>()) : value.ToString();
			Recorded(sourceName, $"{filterName} · {propertyName}", ObsInputs.Kind.Filter, shown, () => _obs.SetSourceFilterSettings(sourceName, filterName, settings, true));
		}

		/// <summary>
		/// Makes the OBS call and records what it set for the dashboard, or OBS's refusal before passing it on.
		/// </summary>
		static void Recorded(string source, string setting, ObsInputs.Kind kind, string value, Action call) {
			try {
				call();
			}
			catch (Exception ex) {
				ObsInputs.Current.Failed(source, setting, kind, value, ex.Message, DateTime.UtcNow);
				throw;
			}
			ObsInputs.Current.Set(source, setting, kind, value, DateTime.UtcNow);
		}
	}
}
