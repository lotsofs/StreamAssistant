using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StreamAssistant2 {
	public static class TwitchHelixApi {
		static HttpClient _http = new HttpClient();

		static string _clientId = "";
		static string _accessToken = "";
		static string _broadcasterId = "";
		static string _moderatorId = "";

		public static void Init() {
			_clientId = Config.Data.TwitchAuth.ClientId;
			_accessToken = Config.Data.TwitchAuth.AccessToken;
			_broadcasterId = Config.Data.TwitchIds.BroadcasterId;
			_moderatorId = Config.Data.TwitchIds.ModeratorId;

			_http.DefaultRequestHeaders.Clear();
			_http.DefaultRequestHeaders.Add("Client-Id", _clientId);
			_http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
			_http.Timeout = TimeSpan.FromSeconds(60);
		}

		public static async Task UpdateRedemption(string rewardId, string redemptionId, string status) {
			if (status != "FULFILLED" && status != "CANCELED") {
				throw new Exception($"Redemption update failed: Status is invalid ({status})");
			}
			var url = $"https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions?broadcaster_id={_broadcasterId}&reward_id={rewardId}&id={redemptionId}";
			var body = new StringContent($"{{\"status\":\"{status}\"}}", Encoding.UTF8, "application/json");

			var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) {
				Content = body
			};
			
			var response = await _http.SendAsync(request);
			
			int responseCode = (int)response.StatusCode;

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"HELIX> points reward {redemptionId} status update {status}: {responseCode}");
			
			if (responseCode != 200) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Unexpected response code");
			}

			var txtContent = await response.Content.ReadAsStringAsync();
			ConsoleLogger.LogToFile(txtContent);
			if (!response.IsSuccessStatusCode) {
				switch (responseCode) {
					default:
						throw new Exception($"Redemption update failed: {txtContent}");
				}
			}
		}

	}
}