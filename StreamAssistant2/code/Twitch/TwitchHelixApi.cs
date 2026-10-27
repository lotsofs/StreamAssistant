using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StreamAssistant2 {
	public static class TwitchHelixApi {
		static HttpClient _http = new HttpClient();
		// No default headers: each request carries its own account's token
		static readonly HttpClient _userHttp = new() { Timeout = TimeSpan.FromSeconds(60) };

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

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Helix, $"HELIX> points reward {redemptionId} status update {status}: {responseCode}");
			
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

		/// <summary>
		/// The channel's current category id, "" for none; null if Twitch returns no channel.
		/// </summary>
		public static async Task<string?> GetChannelCategoryIdAsync() {
			var url = $"https://api.twitch.tv/helix/channels?broadcaster_id={_broadcasterId}";
			var response = await _http.GetAsync(url);
			int responseCode = (int)response.StatusCode;
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Helix, $"HELIX> channel info: {responseCode}");

			var txtContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode) {
				throw new Exception($"Channel info failed: {txtContent}");
			}
			return ParseCategoryId(txtContent);
		}

		/// <summary>
		/// data[0].game_id from a Get Channel Information response; null when data is empty.
		/// </summary>
		internal static string? ParseCategoryId(string json) {
			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement data = doc.RootElement.ReadElement("data");
			if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0) {
				return null;
			}
			return data[0].ReadString("game_id");
		}

		/// <summary>
		/// The token's client id, login, user id and scopes, from Twitch's validate endpoint; throws when Twitch refuses it.
		/// </summary>
		public static async Task<TwitchToken> ValidateTokenAsync(string accessToken) {
			var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
			request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
			var response = await _userHttp.SendAsync(request);
			var txtContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode) {
				throw new Exception($"Token validation failed ({(int)response.StatusCode}): {txtContent}");
			}
			return ParseToken(txtContent) ?? throw new Exception($"Token validation returned no user: {txtContent}");
		}

		/// <summary>
		/// A validate response's client id, login, user id and scopes; null without a client or user id.
		/// </summary>
		internal static TwitchToken? ParseToken(string json) {
			using JsonDocument doc = JsonDocument.Parse(json);
			JsonElement root = doc.RootElement;
			string clientId = root.ReadString("client_id");
			string userId = root.ReadString("user_id");
			if (clientId.Length == 0 || userId.Length == 0) {
				return null;
			}
			JsonElement scopes = root.ReadElement("scopes");
			string[] scopeNames = scopes.ValueKind == JsonValueKind.Array
				? scopes.EnumerateArray().Where(scope => scope.ValueKind == JsonValueKind.String).Select(scope => scope.GetString()!).ToArray()
				: [];
			return new TwitchToken(clientId, root.ReadString("login"), userId, scopeNames);
		}

		/// <summary>
		/// Sets the token's user's chat name colour: a named colour, or a hex one for Turbo and Prime users.
		/// </summary>
		public static async Task SetChatColorAsync(TwitchToken token, string accessToken, string color) {
			var request = new HttpRequestMessage(HttpMethod.Put, ChatColorUrl(token.UserId, color));
			request.Headers.Add("Client-Id", token.ClientId);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
			var response = await _userHttp.SendAsync(request);
			if (!response.IsSuccessStatusCode) {
				throw new Exception($"Chat colour {color} failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
			}
		}

		// The Update User Chat Color URL, the colour escaped
		internal static string ChatColorUrl(string userId, string color) {
			return $"https://api.twitch.tv/helix/chat/color?user_id={Uri.EscapeDataString(userId)}&color={Uri.EscapeDataString(color)}";
		}

	}

	public sealed record TwitchToken(string ClientId, string Login, string UserId, string[] Scopes);
}