using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal class Obs {

		#region obsRaw
		internal enum RequestTypes {
			SetInputSettings,
			SetSourceFilterSettings,
		}

		static Dictionary<RequestTypes, string> OBS_RAW_ITEMS = new Dictionary<RequestTypes, string>() {
			{ RequestTypes.SetInputSettings,
				@"{{
					""requestType"": ""SetInputSettings"",
					""requestData"": {{
						""inputName"": ""{0}"",
						""inputSettings"": {{
							{1}
						}},
						""overlay"": true
					}}
				}}"
			},
			{ RequestTypes.SetSourceFilterSettings,
				@"{{
					""requestType"": ""SetSourceFilterSettings"",
					""requestData"": {{
						""sourceName"": ""{0}"",
						""filterName"": ""{1}"",
						""filterSettings"": {{
							{2}
						}},
						""overlay"": true
					}}
				}}"
			},
		};
		
		private const string OBS_INPUT_SETTING = @"""{0}"": ""{1}"",";
		private const string OBS_INPUT_SETTING_NONSTRING = @"""{0}"": {1},";

		#endregion

		#region OBSconfigurables

		internal enum Sources {
			None,
			Text_Uptime,
			Text_Clock,
			Image_Background,
			Audio_Game0,
			Audio_Game1,
			Audio_Game2,
			Audio_Game3,
			Audio_Game4,
			Layout_InnerBorders,
			Layout_OuterBorders,
			Layout_Body,
			LayoutTransition_InnerBorders,
			LayoutTransition_OuterBorders,
			LayoutTransition_Body,
			LayoutGroup_Colorables,
			LayoutGroupTransition_Colorables,
		}

		static Dictionary<Sources, string> _sources = new Dictionary<Sources, string> {
			{Sources.Text_Uptime,	"Text: Stream Uptime" },
			{Sources.Text_Clock,	"Text: Time Of Day" },
			{Sources.Image_Background, "Image: Background" },
			{Sources.Audio_Game0, "Audio: Z5 Game0" },
			{Sources.Audio_Game1, "Audio: Z5 Game1" },
			{Sources.Audio_Game2, "Audio: Z5 Game2" },
			{Sources.Audio_Game3, "Audio: Z5 Game3" },
			{Sources.Audio_Game4, "Audio: Z5 Game4" },
			{Sources.Layout_InnerBorders,				"Border: Colorable Inner" },
			{Sources.Layout_OuterBorders,				"Border: Colorable Outer" },
			{Sources.Layout_Body,						"!Scene: All Colorable" },
			{Sources.LayoutTransition_InnerBorders,		"Border: Colorable Inner (Transitionary)"},
			{Sources.LayoutTransition_OuterBorders,		"Border: Colorable Outer (Transitionary)"},
			{Sources.LayoutTransition_Body,				"!Scene: All Colorable (Transitionary)"},
			{Sources.LayoutGroup_Colorables,			"!Layout: Colorables"},
			{Sources.LayoutGroupTransition_Colorables,	"!Layout: Colorables (Transitionary)"},
		};

		internal enum Scenes {
			Layout,
		}

		static Dictionary<Scenes, string> _scenes = new Dictionary<Scenes, string> {
			{Scenes.Layout, "!Scene: Layout" },
		};

		#endregion

		private static string BuildJsonStringEntries(Dictionary<string, string> inputSettings) {
			string s = "";
			foreach (string k in inputSettings.Keys) {
				s += string.Format(OBS_INPUT_SETTING, k, inputSettings[k]);
			}
			s = s.Trim(',');    // Json is a good format. Json does not allow trailing commas. Everybody loves Json :)
			return s;
		}

		private static string BuildJsonIntEntries(Dictionary<string, int> inputSettings) {
			string s = "";
			foreach (string k in inputSettings.Keys) {
				s += string.Format(OBS_INPUT_SETTING_NONSTRING, k, inputSettings[k]);
			}
			s = s.Trim(',');    // Json is a good format. Json does not allow trailing commas. Everybody loves Json :)
			return s;
		}

		private static string BuildRequestItem_SetInputSettings(string inputName, Dictionary<string, string> inputSettings) {
			return string.Format(OBS_RAW_ITEMS[RequestTypes.SetInputSettings], inputName, BuildJsonStringEntries(inputSettings));
		}

		// Todo: This now does not allow strings, and the above does not allow non-strings. Make flexible.
		private static string BuildRequestItem_SetSourceFilterSettings(string sourceName, string filterName, Dictionary<string, int> filterSettings) {
			return string.Format(OBS_RAW_ITEMS[RequestTypes.SetSourceFilterSettings], sourceName, filterName, BuildJsonIntEntries(filterSettings));
		}

		internal static void ChangeSingle_SetInputSettings(Sources source, Dictionary<string, string> inputSettings) {
			if (!_sources.ContainsKey(source)) { return; }
			string obsRaw = string.Format("[{0}]", BuildRequestItem_SetInputSettings(_sources[source], inputSettings));
			obsRaw = Regex.Replace(obsRaw, @"\t|\n|\r", "");
			MsgQueue.Enqueue(MsgTypes.ObsRawI, obsRaw);
		}

		internal static void ChangeTextSourceText(Sources source, string newText) {
			Dictionary<string, string> inputSettings = new Dictionary<string, string> {
				{ "text", newText },
			};
			ChangeSingle_SetInputSettings(source, inputSettings);
		}

		internal static void ChangeImageSourceFile(Sources source, string newPath) {
			Dictionary<string, string> inputSettings = new Dictionary<string, string> {
				{ "file", newPath },
			};
			ChangeSingle_SetInputSettings(source, inputSettings);
		}

		internal static void ChangeAudioSourceWindow(Sources source, string executableName) {
			Dictionary<string, string> inputSettings = new Dictionary<string, string> {
				{ "window", "GAMESOUND:Set by StreamerBot:" + executableName },
			};
			ChangeSingle_SetInputSettings(source, inputSettings);
		}

		internal static string GetSingle_SetSourceFilterSettings(Sources source, string filterName, Dictionary<string, int> filterSettings) {
			if (!_sources.ContainsKey(source)) { return ""; }
			string obsRaw = BuildRequestItem_SetSourceFilterSettings(_sources[source], filterName, filterSettings);
			obsRaw = Regex.Replace(obsRaw, @"\t|\n|\r", "");
			return obsRaw;
		}

		internal static void ChangeLayoutColor(Color colorInner, Color colorOuter, Color colorText, int delayInMs = 0) {
			string filterName = "Color Correction";
			long colInner = ColorTranslator.ToWin32(colorInner) | 4278190080;
			long colOuter = ColorTranslator.ToWin32(colorOuter) | 4278190080;
			long colText = ColorTranslator.ToWin32(colorText) | 4278190080;
			Dictionary<string, int> dictInner = new Dictionary<string, int>() {
				{ "color_multiply", (int)colInner }
			};
			Dictionary<string, int> dictOuter = new Dictionary<string, int>() {
				{ "color_add", (int)colOuter }
			};
			Dictionary<string, int> dictText = new Dictionary<string, int>() {
				{ "color_multiply", (int)colText }
			};
			Sources innerSource = Sources.Layout_InnerBorders;
			Sources outerSource = Sources.Layout_OuterBorders;
			Sources textSource = Sources.Layout_Body;
			Sources innerSourceT = Sources.LayoutTransition_InnerBorders;
			Sources outerSourceT = Sources.LayoutTransition_OuterBorders;
			Sources textSourceT = Sources.LayoutTransition_Body;
			Sources mainSource = Sources.LayoutGroup_Colorables;
			Sources mainSourceT = Sources.LayoutGroupTransition_Colorables;

			string obsRaw = "[";
			obsRaw += GetSingle_SetSourceFilterSettings(Sources.LayoutTransition_InnerBorders, filterName, dictInner);
			obsRaw += ",";
			obsRaw += GetSingle_SetSourceFilterSettings(Sources.LayoutTransition_OuterBorders, filterName, dictOuter);
			obsRaw += ",";
			obsRaw += GetSingle_SetSourceFilterSettings(Sources.LayoutTransition_Body, filterName, dictText);
			obsRaw += "]";

			obsRaw = Regex.Replace(obsRaw, @"\t|\n|\r", "");


			MsgQueue.TimedEnqueue(delayInMs, MsgTypes.ObsRawI, obsRaw);
			MsgQueue.TimedEnqueue(delayInMs, MsgTypes.ShowSrc, string.Format("{0}|{1}", _scenes[Scenes.Layout], _sources[mainSourceT]));
			delayInMs += 5500;
			MsgQueue.TimedEnqueue(delayInMs, MsgTypes.HideSrc, string.Format("{0}|{1}", _scenes[Scenes.Layout], _sources[mainSource]));
			delayInMs += 2500;

			obsRaw = "[";
			obsRaw += GetSingle_SetSourceFilterSettings(Sources.Layout_InnerBorders, filterName, dictInner);
			obsRaw += ",";
			obsRaw += GetSingle_SetSourceFilterSettings(Sources.Layout_OuterBorders, filterName, dictOuter);
			obsRaw += ",";
			obsRaw += GetSingle_SetSourceFilterSettings(Sources.Layout_Body, filterName, dictText);
			obsRaw += "]";

			obsRaw = Regex.Replace(obsRaw, @"\t|\n|\r", "");
			MsgQueue.TimedEnqueue(delayInMs, MsgTypes.ObsRawI, obsRaw);

			delayInMs += 2500;
			MsgQueue.TimedEnqueue(delayInMs, MsgTypes.ShowSrc, string.Format("{0}|{1}", _scenes[Scenes.Layout], _sources[mainSource]));
			delayInMs += 2500;
			MsgQueue.TimedEnqueue(delayInMs, MsgTypes.HideSrc, string.Format("{0}|{1}", _scenes[Scenes.Layout], _sources[mainSourceT]));
		}

	}
}
