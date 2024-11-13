using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	internal static class ChannelPoints {
		
		static Dictionary<string, List<string>> _flushes = new Dictionary<string, List<string>>();
		const string FLUSHES_FILEPATH = "D:\\Repositories\\Stream-Resources\\Bot Data\\Flushes.json";

		private static void LoadFlushes() {
			string json = File.ReadAllText(FLUSHES_FILEPATH);
			_flushes = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json) ?? new Dictionary<string, List<string>>();
		}

		private static void SaveFlushes() {
			string json = JsonConvert.SerializeObject(_flushes);
			File.WriteAllText(FLUSHES_FILEPATH, json);
		}

		internal static string Process(Dictionary<string, object> variables) {
			string rewardId = variables["rewardId"].ToString() ?? "";
			string userId = variables["userId"].ToString() ?? "";
			string user = variables["user"].ToString() ?? "";

			switch (rewardId) {
				case "44006120-a6fb-4653-9257-c26e2c67f0ee":
					// Toilet Flush
					LoadFlushes();
					string redemptionId = variables["redemptionId"].ToString() ?? "";
					if (!_flushes.ContainsKey(userId)) {
						_flushes[userId] = new List<string>();
					}
					_flushes[userId].Add(redemptionId);
					Sound.PlaySound(Sound.Sounds.Flush);
					SaveFlushes();
					break;
				case "785967e7-9b58-41eb-aa11-15fed82a72ec":
					// Retrieve Flushed Points
					LoadFlushes();
					List<string> flushes = _flushes[userId];
					int flushesCount = flushes.Count;
					foreach (var fl in flushes) {
						// Redemption Cancel
						MsgQueue.Enqueue(MsgTypes.RdmCncl, "44006120-a6fb-4653-9257-c26e2c67f0ee|" + fl);
					}
					_flushes[userId].Clear();
					MsgQueue.Enqueue(MsgTypes.ChatMsg, string.Format("🪠 Found and returned {0} chunks of {1}'s flushed channel points 🪠", flushesCount, user), false);
					SaveFlushes();
					break;
				case "6fbb1ffa-555b-4e24-9cb1-f784d9f63689":
					// Change Layout Color
					string changeLayoutColorInput = variables["rawInput"].ToString() ?? "";
					Coloring.ChangeColor(changeLayoutColorInput, true, true);
					break;
				case "76c02fbc-d4ad-4fb7-984d-d057c9ebb03f":
					// Change Layout Color - Advanced
					string changeLayoutColorAInput = variables["rawInput"].ToString() ?? "";
					Coloring.ChangeColor(changeLayoutColorAInput, true, false);
					break;
				case "d1ca4789-8461-40a0-9335-e9080fd91f29":
					// Change Layout Color - Random
					Coloring.RandomColor();
					break;
			}
			return string.Format("{0} {1} {2}", user, rewardId, userId);
		}
	}
}
