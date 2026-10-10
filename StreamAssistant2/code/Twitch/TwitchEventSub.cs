using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public static class TwitchEventSub {
		internal enum SessionExitReason {
			None,
			Error,
			KeepAliveTimeout,
			ReconnectRequested,
			SubscriptionFailed,
			CancelRequested,
			SocketClosed,
			SocketDied,
			ConnectionLost,
		}
		
		static readonly bool IS_TEST = false;

		static string _clientId = "";
		static string _accessToken = "";
		static string _broadcasterId = "";
		static string _moderatorId = "";
		static string _userId = "";
		static string _testId = "";

		static SessionExitReason _exitReason = SessionExitReason.None;

		static ClientWebSocket? _socket;
		static CancellationTokenSource? _cts;

		static readonly HttpClient _http = new HttpClient();
		
		static string _sessionId = "";

		internal static Stopwatch KeepAliveTimer = Stopwatch.StartNew();
		// A session has been welcomed and not yet cleaned up
		internal static bool IsConnected => _sessionId.Length > 0;
		// No message for this long: reconnect
		internal static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(20);
		internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
		internal static readonly TimeSpan ReconnectWelcomeTimeout = TimeSpan.FromSeconds(10);
		// How long the old socket is read for leftover events after a reconnect
		internal static readonly TimeSpan OldSocketDrainLimit = TimeSpan.FromSeconds(1);

		// EventSub endpoint, subscribe endpoint, pause between sessions, and where notifications go
		internal static string EventSubUrl = "wss://eventsub.wss.twitch.tv/ws";
		internal static string SubscriptionsUrl = "https://api.twitch.tv/helix/eventsub/subscriptions";
		internal static TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
		internal static Action<string, JsonElement> NotificationHandler = (type, evt) => TwitchEventHandler.Handle(type, evt);

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

		internal static async Task StartConnectionLoop(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				_exitReason = SessionExitReason.None;
				try {
					await ConnectOnce(token);
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
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"EventSub session ended: {_exitReason} ({DescribeSocket()})");
				await CleanupSession(_exitReason == SessionExitReason.CancelRequested);
				TwitchIRCManager.SendMessage("💥 ES Disconnected");
				await Task.Delay(RetryDelay, token);
			}
		}

		static async Task ConnectOnce(CancellationToken token) {
			_socket?.Dispose();
			_socket = new ClientWebSocket();

			var url = EventSubUrl;
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, $"Connecting to {url}");

			await ConnectOrTimeoutAsync(_socket, new Uri(url), ConnectTimeout, token);

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "Connected to EventSub");
			TwitchIRCManager.SendMessage("🟣 ES Connected");
		}
		
		static async Task ListenLoop(CancellationToken token) {
			while (!token.IsCancellationRequested) {
				if (!IsSocketAlive()) {
					_exitReason = SessionExitReason.Error;
					return;
				}
				if (KeepAliveTimer.Elapsed > KeepAliveTimeout) {
					_exitReason = SessionExitReason.KeepAliveTimeout;
					return;
				}

				var (json, reason) = await ReceiveFullMessage(_socket!, KeepAliveTimer, KeepAliveTimeout, token);

				if (reason != SessionExitReason.None) {
					_exitReason = reason;
					return;
				}

				using var doc = JsonDocument.Parse(json);
				var root = doc.RootElement;
				string messageType = root.ReadString("metadata.message_type");

				switch (messageType) {
					case "session_welcome":
						KeepAliveTimer.Restart();
						// ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, "session_welcome");
						_sessionId = root.ReadString("payload.session.id");
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"EventSub Session Welcome. Session ID: {_sessionId}");
						await SubscribeToEvents();
						break;
					case "notification":
						KeepAliveTimer.Restart();
						// ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, "notification");
						HandleNotification(root);
						break;
					case "session_keepalive":
						KeepAliveTimer.Restart();
						// ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, "keepalive");
						break;
					case "session_reconnect":
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, "EventSub sent session_reconnect");
						string reconnectUrl = root.ReadString("payload.session.reconnect_url");
						if (!await SwitchToReconnectUrl(reconnectUrl, token)) {
							ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "EventSub reconnect failed, starting a fresh session");
							_exitReason = SessionExitReason.ReconnectRequested;
							return;
						}
						break;
					default:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"wtf?? EventSub send message of type {messageType}");
						ConsoleLogger.LogToFile(json);
						break;
				}
			}
			_exitReason = SessionExitReason.CancelRequested;
		}

		static void HandleNotification(JsonElement root) {
			string subscriptionType = root.ReadString("payload.subscription.type");
			JsonElement event_element = root.ReadElement("payload.event");
			NotificationHandler(subscriptionType, event_element);
		}

		// Connects to url, switches to it on welcome, then drains and closes the old socket. False if it failed.
		static async Task<bool> SwitchToReconnectUrl(string url, CancellationToken token) {
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"EventSub reconnect: unusable URL \"{url}\"");
				return false;
			}
			var (socket, sessionId) = await ConnectToReconnectUrlAsync(uri, ConnectTimeout, ReconnectWelcomeTimeout, token);
			if (socket == null) {
				return false;
			}

			ClientWebSocket? old = _socket;
			_socket = socket;
			_sessionId = sessionId;
			KeepAliveTimer.Restart();
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, $"EventSub reconnected. Session ID: {_sessionId}");

			if (old != null) {
				int events = 0;
				try {
					events = await DrainOldSocketAsync(old, OldSocketDrainLimit, HandleNotification, token);
				}
				catch (Exception ex) when (!token.IsCancellationRequested) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error TES5");
					ConsoleLogger.LogToFile(ex);
				}
				string close = FormatClose(old.CloseStatus, old.CloseStatusDescription);
				await CloseQuietlyAsync(old);
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, $"EventSub old connection closed ({events} events delivered during the switch, close {close})");
			}
			return true;
		}

		// Returns the open socket and its session id, or (null, "") after logging why not. Cancelling token throws.
		internal static async Task<(ClientWebSocket? Socket, string SessionId)> ConnectToReconnectUrlAsync(Uri url, TimeSpan connectTimeout, TimeSpan welcomeTimeout, CancellationToken token) {
			var socket = new ClientWebSocket();
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, $"Connecting to {url}");
			try {
				await ConnectOrTimeoutAsync(socket, url, connectTimeout, token);
				var (json, reason) = await ReceiveFullMessage(socket, Stopwatch.StartNew(), welcomeTimeout, token);
				if (reason != SessionExitReason.None) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"EventSub reconnect: no welcome ({reason})");
				}
				else {
					using var doc = JsonDocument.Parse(json);
					string messageType = doc.RootElement.ReadString("metadata.message_type");
					string sessionId = doc.RootElement.ReadString("payload.session.id");
					if (messageType == "session_welcome" && sessionId.Length > 0) {
						return (socket, sessionId);
					}
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"EventSub reconnect: expected a session_welcome with an id, got {messageType}");
					ConsoleLogger.LogToFile(json);
				}
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested) {
				socket.Dispose();
				throw;
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error TES4");
				ConsoleLogger.LogToFile(ex);
			}
			socket.Dispose();
			return (null, "");
		}

		// Reads leftover messages until the socket closes or limit passes; returns how many notifications were handled
		internal static async Task<int> DrainOldSocketAsync(WebSocket old, TimeSpan limit, Action<JsonElement> onNotification, CancellationToken token) {
			var elapsed = Stopwatch.StartNew();
			int count = 0;
			while (true) {
				var (json, reason) = await ReceiveFullMessage(old, elapsed, limit, token);
				if (reason != SessionExitReason.None) {
					return count;
				}
				using var doc = JsonDocument.Parse(json);
				if (doc.RootElement.ReadString("metadata.message_type") == "notification") {
					onNotification(doc.RootElement);
					count++;
				}
			}
		}

		static async Task CloseQuietlyAsync(WebSocket socket) {
			try {
				if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived) {
					using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
					await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Reconnected", timeout.Token);
				}
			}
			catch (Exception ex) {
				ConsoleLogger.LogToFile(ex);
			}
			socket.Dispose();
		}

		// Throws TimeoutException if the connect hangs, OperationCanceledException only when token is cancelled
		internal static async Task ConnectOrTimeoutAsync(ClientWebSocket socket, Uri uri, TimeSpan timeout, CancellationToken token) {
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
			linked.CancelAfter(timeout);
			try {
				await socket.ConnectAsync(uri, linked.Token);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested) {
				throw new TimeoutException($"EventSub connect took over {timeout}");
			}
		}

		// Reason is None when a whole message arrived. Gives up with KeepAliveTimeout once keepAlive passes timeout;
		// a timed-out receive aborts the socket. A dropped connection returns ConnectionLost. Cancelling token throws.
		internal static async Task<(string Json, SessionExitReason Reason)> ReceiveFullMessage(WebSocket socket, Stopwatch keepAlive, TimeSpan timeout, CancellationToken token) {
			var buffer = new byte[8192];
			var sb = new StringBuilder();

			WebSocketReceiveResult result;

			do {
				if (socket.State != WebSocketState.Open) {
					return ("", SessionExitReason.SocketDied);
				}
				TimeSpan remaining = timeout - keepAlive.Elapsed;
				if (remaining <= TimeSpan.Zero) {
					return ("", SessionExitReason.KeepAliveTimeout);
				}
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token)) {
					linked.CancelAfter(remaining);
					try {
						result = await socket.ReceiveAsync(buffer, linked.Token);
					}
					catch (OperationCanceledException) when (!token.IsCancellationRequested) {
						return ("", SessionExitReason.KeepAliveTimeout);
					}
					catch (WebSocketException ex) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"EventSub connection lost ({ex.WebSocketErrorCode}): {ex.GetBaseException().Message}");
						return ("", SessionExitReason.ConnectionLost);
					}
				}
				if (result.MessageType == WebSocketMessageType.Close) {
					string close = FormatClose(result.CloseStatus, result.CloseStatusDescription);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubConfusion, $"EventSub closed the socket: {close}");
					return ("", SessionExitReason.SocketClosed);
				}
				sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
			}
			while (!result.EndOfMessage);

			return (sb.ToString(), SessionExitReason.None);
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
				SubscriptionsUrl,
				new StringContent(json, Encoding.UTF8, "application/json")
			);

			var responseText = await response.Content.ReadAsStringAsync();

			int responseCode = (int)response.StatusCode;

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"EventSub attempt to {es.Type}: {responseCode}");

			if (responseCode == 409) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.EventSubNotification, $"Subscription {es.Type} already exists");
				return;
			}

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
				ConsoleLogger.LogToFile(ex);
			}

			_socket = null;
			_sessionId = "";
			KeepAliveTimer.Restart();
		}

		static string DescribeSocket() {
			if (_socket == null) {
				return "no socket";
			}
			string close = FormatClose(_socket.CloseStatus, _socket.CloseStatusDescription);
			return $"socket {_socket.State}, close {close}";
		}

		static string FormatClose(WebSocketCloseStatus? status, string? description) {
			if (status == null) {
				return "none";
			}
			return $"{(int)status} {status} \"{description}\"";
		}

		static bool IsSocketAlive() {
			return _socket != null && _socket.State == WebSocketState.Open;
		}
	}
}
