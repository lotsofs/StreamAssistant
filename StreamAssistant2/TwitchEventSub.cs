using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class TwitchEventSub {
		enum SessionExitReason {
			None,
			Error,
			KeepAliveTimeout,
			ReconnectRequested,
			SubscriptionFailed,
			CancelRequested,
			SocketClosed,
			SocketDied,
		}
		
		const string TEST_PATH = @"D:\Repositories\Stream-Resources\Input\Bot\Tests";

		const bool IS_TEST = false;

		static string _clientId = "";
		static string _accessToken = "";
		static string _broadcasterId = "";
		static string _moderatorId = "";
		static string _userId = "";
		static string _testId = "";

		static string? _pendingReconnectUrl = null;
		static SessionExitReason _exitReason = SessionExitReason.None;

		static ClientWebSocket? _socket;
		static CancellationTokenSource? _cts;

		static readonly HttpClient _http = new HttpClient();
		
		static string _sessionId = "";

		internal static Stopwatch KeepAliveTimer = Stopwatch.StartNew();

		internal static void Connect() {
			_clientId = Config.Data.TwitchAuth.ClientId;
			_accessToken = Config.Data.TwitchAuth.AccessToken;
			_broadcasterId = Config.Data.TwitchIds.BroadcasterId;
			_moderatorId = Config.Data.TwitchIds.ModeratorId;
			_userId = Config.Data.TwitchIds.UserId;
			_testId = Config.Data.TwitchIds.TestBroadcasterId;

			_http.DefaultRequestHeaders.Clear();
			_http.DefaultRequestHeaders.Add("Client-Id", _clientId);
			_http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

			_cts = new CancellationTokenSource();
			_ = Task.Run(() => StartConnectionLoop(_cts.Token));
		}

		static async Task StartConnectionLoop(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				_exitReason = SessionExitReason.None;
				try {
					await ConnectOnce();
					KeepAliveTimer.Restart();
					await ListenLoop(token);
				}
				catch (Exception ex) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error TES1: {_exitReason}");
					ConsoleLogger.LogToFile(ex);
				}
				if (_exitReason == SessionExitReason.None) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error TES3: Listen loop exited with no provided reason.");
				}
				await CleanupSession(_exitReason == SessionExitReason.CancelRequested);
				TwitchIRCManager.SendMessage("💥 ES Disconnected");
				await Task.Delay(3000, token);
			}
		}

		static async Task ConnectOnce() {
			_socket?.Dispose();
			_socket = new ClientWebSocket();

			var url = _pendingReconnectUrl ?? "wss://eventsub.wss.twitch.tv/ws";
			_pendingReconnectUrl = null;
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, $"Connecting to {url}");

			await _socket.ConnectAsync(new Uri(url), CancellationToken.None);

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "Connected to EventSub");
			TwitchIRCManager.SendMessage("🟣 ES Connected");
		}
		
		static async Task ListenLoop(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				if (!IsSocketAlive()) {
					_exitReason = SessionExitReason.Error;
					return;
				}
				if (KeepAliveTimer.Elapsed.TotalSeconds > 20) {
					_exitReason = SessionExitReason.KeepAliveTimeout;
					return;
				}

				string json = await ReceiveFullMessage(token);

				if (_exitReason != SessionExitReason.None) {
					return;
				}

				using var doc = JsonDocument.Parse(json);
				var root = doc.RootElement;
				var metadata = root.GetProperty("metadata");
				var messageType = metadata.GetProperty("message_type").GetString();

				switch (messageType) {
					case "session_welcome":
						KeepAliveTimer.Restart();
						// ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, "session_welcome");
						_sessionId = root.GetProperty("payload").GetProperty("session").GetProperty("id").GetString();
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"EventSub Session Welcome. Session ID: {_sessionId}");
						await SubscribeToEvents();
						break;
					case "notification":
						KeepAliveTimer.Restart();
						// ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, "notification");
						JsonElement payload = root.GetProperty("payload");
						string subscriptionType = payload.GetProperty("subscription").GetProperty("type").ToString();
						JsonElement event_element = payload.GetProperty("event");
						TwitchEventHandler.Handle(subscriptionType, event_element);
						break;
					case "session_keepalive":
						KeepAliveTimer.Restart();
						// ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, "keepalive");
						break;
					case "session_reconnect":
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, "EventSub sent session_reconnect");
						_pendingReconnectUrl = root.GetProperty("payload").GetProperty("session").GetProperty("reconnect_url").GetString();
						_exitReason = SessionExitReason.ReconnectRequested;
						return;
					default:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"wtf?? EventSub send message of type {messageType}");
						ConsoleLogger.LogToFile(json);
						break;
				}
			}
			_exitReason = SessionExitReason.CancelRequested;
		}

		static async Task<string> ReceiveFullMessage(CancellationToken token) {
			var buffer = new byte[8192];
			var sb = new StringBuilder();

			WebSocketReceiveResult result;

			do {
				if (!IsSocketAlive()) {
					_exitReason = SessionExitReason.SocketDied;
					return "";
				}
				result = await _socket!.ReceiveAsync(buffer, token);
				if (result.MessageType == WebSocketMessageType.Close) {
					_exitReason = SessionExitReason.SocketClosed;
					return "";
				}
				sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
			}
			while (!result.EndOfMessage);

			return sb.ToString();
		}

		static async Task SubscribeToEvents() {
			foreach (TwitchEventSubSubscription.TwitchEventSubEvent es in TwitchEventSubSubscription.Subscriptions) {
				await Subscribe(es);
			}
		}

		static async Task Subscribe(TwitchEventSubSubscription.TwitchEventSubEvent es) {
			var condition = new Dictionary<string, object>();
			if (es.RequiresBroadcasterId) {
				condition["broadcaster_user_id"] = _broadcasterId;
			}
			if (es.RequiresModeratorId) {
				condition["moderator_user_id"] = es.WantsBroadcasterAsModerator ? _broadcasterId : _moderatorId;
			}
			if (es.RequiresUserId) {
				condition["user_id"] = _userId;
				if (IS_TEST) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"In test mode. Monitoring events for user {_testId} instead");
					TwitchIRCManager.SendMessage($"🚨 Test mode. Monitoring events for user {_testId} instead");
					condition["broadcaster_user_id"] = _testId;
				}
			}

			var body = new {
				type = es.Type,
				version = es.Version,
				condition,
				transport = new {
					method = "websocket",
					session_id = _sessionId
				}
			};
			var json = JsonSerializer.Serialize(body);

			var response = await _http.PostAsync(
				"https://api.twitch.tv/helix/eventsub/subscriptions",
				new StringContent(json, Encoding.UTF8, "application/json")
			);

			var responseText = await response.Content.ReadAsStringAsync();
			
			int responseCode = (int)response.StatusCode;
			
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"EventSub attempt to {es.Type}: {responseCode}");
			
			if (responseCode != 202) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Unexpected response code");
			}

			if (!response.IsSuccessStatusCode) {
				_exitReason = SessionExitReason.SubscriptionFailed;

				switch (responseCode) {
					case 400:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"Subscription {es.Type} failed");
						break;
					default:
						throw new Exception($"Subscription failed ({(int)response.StatusCode})");
				}
			}
			ConsoleLogger.LogToFile(responseText);
		}

		internal static void Disconnect() {
			_cts?.Cancel();
		}

		static async Task CleanupSession(bool graceful = false) {
			try {
				if (graceful) {
					if (_socket != null && (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)) { 
						await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None); 
					}
				}
				else {
					_socket?.Abort();
				}
				_socket?.Dispose();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error TES2");
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, ex);
			}

			_socket = null;
			_sessionId = "";
			KeepAliveTimer.Restart();
		}

		static bool IsSocketAlive() {
			return _socket != null && _socket.State == WebSocketState.Open;
		}

		internal static void SendTest(string fileName) {
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, "Test Notification");
			string path = Path.Combine(TEST_PATH, $"{fileName}.txt");
			if (!File.Exists(path)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, "No such file");
				return;
			}
			JsonElement payload = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
			string subscriptionType = payload.GetProperty("subscription").GetProperty("type").ToString();
			JsonElement event_element = payload.GetProperty("event");
			TwitchEventHandler.Handle(subscriptionType, event_element);
		}
	}
}
