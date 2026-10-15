using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;

namespace StreamAssistant2 {
	internal static class Obs {
		static OBSWebsocket _obs = ObsConnection.ObsSocket;
		
		public static void SetSourceEnabled(string sceneName, string sourceName, bool enabled) {
			if (!ObsConnection.IsConnected()) return;
			var sceneItemId = _obs.GetSceneItemId(sceneName, sourceName, 0);
			_obs.SetSceneItemEnabled(sceneName, sceneItemId, enabled);
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
			_obs.SetInputSettings(inputName, set, true);
		}

		/// <summary>
		/// Sets an input's volume in dB (OBS takes -100 to 26; -100 and below is silent).
		/// </summary>
		public static void SetInputVolumeDb(string inputName, double db) {
			if (!ObsConnection.IsConnected()) return;
			_obs.SetInputVolume(inputName, (float)db, true);
		}

		public static void SetFilterProperty(string sourceName, string filterName, string propertyName, JToken value) {
			if (!ObsConnection.IsConnected()) return;

			var settings = new JObject {
				[propertyName] = value,
			};
			_obs.SetSourceFilterSettings(sourceName, filterName, settings, true);
		}
	}
}
