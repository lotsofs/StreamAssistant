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
		private const string OBS_SETINPUTSETTINGS_DATA = 
			@"[{{
				""requestType"": ""SetInputSettings"",
				""requestData"": {{
					""inputName"": ""{0}"",
					""inputSettings"": {{
						""text"": ""{1}""
					}},
					""overlay"": true
				}}
			}}]";

		#endregion

		internal enum Sources {
			Text_Uptime,
			Text_Clock,
		}

		static Dictionary<Sources, string> _sources = new Dictionary<Sources, string> {
			{Sources.Text_Uptime, "Text: Stream Uptime" },
			{Sources.Text_Clock, "Text: Time Of Day" },
		};

		internal static void ChangeText(Sources source, string newText) {
			if (!_sources.ContainsKey(source)) { return; }
			string obsRaw = string.Format(OBS_SETINPUTSETTINGS_DATA, _sources[source], newText);
			obsRaw = Regex.Replace(obsRaw, @"\t|\n|\r", "");
			MsgQueue.Enqueue(MsgTypes.ObsRawI, obsRaw);
		}
	}
}
