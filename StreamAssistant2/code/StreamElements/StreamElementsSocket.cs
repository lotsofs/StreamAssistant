using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace StreamAssistant2 {
	// StreamElements' Astro WebSocket: subscribes to tips and passes each event on
	internal static class StreamElementsSocket {
		internal enum SessionExitReason {
			None,
			Error,
			SilenceTimeout,
			ReconnectRequested,
			SubscribeFailed,
			Unauthorized,
			CancelRequested,
			SocketClosed,
			SocketDied,
			ConnectionLost,
		}

		// Probe interval, silence limit, and the first and longest pause between sessions
		internal sealed record Timings(TimeSpan Probe, TimeSpan Silence, TimeSpan RetryBase, TimeSpan RetryCap);

		// The topic subscribed to
		internal const string TIPS_TOPIC = "channel.tips";
		// Nonce of a session's first subscribe; later ones are probes
		const string SUBSCRIBE_NONCE = "subscribe";
		// Error and message of a subscribe to a topic already subscribed: a success
		const string ALREADY_SUBSCRIBED_ERROR = "err_bad_request";
		const string ALREADY_SUBSCRIBED_MESSAGE = "already subscribed to topic";

		// From secrets.json
		static string _channelId = "";
		static string _jwt = "";
		// Token from a reconnect message, used by the next connect only
		static string _reconnectToken = "";
		// Sessions failed since the last acknowledged subscribe
		static int _failures;
		// Subscribe acknowledged this session
		static volatile bool _connected;
		// Chat was told "SE Connected" and not yet "SE Disconnected"
		static bool _announced;

		// Why the current session ended
		static SessionExitReason _exitReason = SessionExitReason.None;

		// The current session's socket
		static ClientWebSocket? _socket;
		// Cancels the connection loop
		static CancellationTokenSource? _cts;

		// Time since the last message of any kind
		internal static readonly Stopwatch SinceLastMessage = Stopwatch.StartNew();
		// The subscribe has been acknowledged this session
		internal static bool IsConnected => _connected;
		// Channel id and JWT are both set
		internal static bool IsEnabled => Config.Data.StreamElements.ChannelId.Length > 0 && Config.Data.StreamElements.Jwt.Length > 0;
		// No message for this long: reconnect
		internal static readonly TimeSpan SilenceTimeout = TimeSpan.FromSeconds(70);
		internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

		// Astro endpoint, the timings the session loop uses, and where events go
		internal static string AstroUrl = "wss://astro.streamelements.com";
		internal static Timings Times = new(TimeSpan.FromSeconds(30), SilenceTimeout, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(30));
		internal static Action<string, JsonElement> NotificationHandler = (topic, data) => StreamElementsEventHandler.Handle(topic, data);

		// Starts the connection loop, or logs that tips are off without credentials
		internal static void Connect() {
			if (!IsEnabled) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "StreamElements: no channelId or jwt in secrets.json, tips are off");
				return;
			}
			_cts = new CancellationTokenSource();
			_ = Task.Run(() => StartConnectionLoop(_cts.Token));
		}

		// Stops the connection loop
		internal static void Disconnect() {
			_cts?.Cancel();
		}

		// Connects, listens, and reconnects until cancelled or the token is refused
		internal static async Task StartConnectionLoop(CancellationToken token) {
			_channelId = Config.Data.StreamElements.ChannelId;
			_jwt = Config.Data.StreamElements.Jwt;
			_reconnectToken = "";
			_failures = 0;
			_announced = false;
			while (!token.IsCancellationRequested) {
				_exitReason = SessionExitReason.None;
				using (var session = CancellationTokenSource.CreateLinkedTokenSource(token)) {
					try {
						await ConnectOnce(token);
						SinceLastMessage.Restart();
						await ListenLoop(session.Token, token);
					}
					catch (OperationCanceledException) when (token.IsCancellationRequested) {
						_exitReason = SessionExitReason.CancelRequested;
					}
					catch (Exception ex) {
						_exitReason = SessionExitReason.Error;
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error SES1: {ex.GetType().Name}: {ex.Message}");
						ConsoleLogger.LogToFile(ex);
					}
					session.Cancel();
				}

				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsConfusion, $"StreamElements session ended: {_exitReason} ({DescribeSocket()})");
				await CleanupSession(_exitReason == SessionExitReason.CancelRequested);
				if (_exitReason == SessionExitReason.CancelRequested) {
					return;
				}
				if (_announced && _exitReason != SessionExitReason.ReconnectRequested) {
					_announced = false;
					TwitchIRCManager.SendMessage("💥 SE Disconnected");
				}
				if (_exitReason == SessionExitReason.Unauthorized) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "StreamElements stopped: fix streamElements in secrets.json and restart the bot");
					return;
				}
				if (_exitReason == SessionExitReason.ReconnectRequested) {
					continue;
				}
				_failures++;
				await Task.Delay(RetryDelay(_failures, Times.RetryBase, Times.RetryCap), token);
			}
		}

		// Pause before the next session: doubles from retryBase per failure, up to cap
		internal static TimeSpan RetryDelay(int failures, TimeSpan retryBase, TimeSpan cap) {
			int doublings = Math.Clamp(failures - 1, 0, 20);
			TimeSpan delay = retryBase * Math.Pow(2, doublings);
			return delay > cap ? cap : delay;
		}

		// Opens a new socket, resuming with the reconnect token if there is one
		static async Task ConnectOnce(CancellationToken token) {
			_socket?.Dispose();
			_socket = new ClientWebSocket();

			var url = new UriBuilder(AstroUrl);
			bool resuming = _reconnectToken.Length > 0;
			if (resuming) {
				url.Query = "reconnect_token=" + Uri.EscapeDataString(_reconnectToken);
				_reconnectToken = "";
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, $"Connecting to {AstroUrl}{(resuming ? " (resuming)" : "")}");

			await ConnectOrTimeoutAsync(_socket, url.Uri, ConnectTimeout, token);

			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.ConnectionNotification, "Connected to StreamElements");
		}

		// Reads messages until the session ends; sets _exitReason
		static async Task ListenLoop(CancellationToken session, CancellationToken token) {
			bool probing = false;
			while (!token.IsCancellationRequested) {
				var (json, reason) = await ReceiveFullMessage(_socket!, SinceLastMessage, Times.Silence, token);
				if (reason != SessionExitReason.None) {
					_exitReason = reason;
					return;
				}
				SinceLastMessage.Restart();

				using var doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				string type = root.ReadString("type");

				switch (type) {
					case "welcome":
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsNotification, "StreamElements welcome");
						if (!probing) {
							probing = true;
							_ = SubscribeAndProbeAsync(_socket!, session);
						}
						break;
					case "response":
						SessionExitReason end = HandleResponse(root);
						if (end != SessionExitReason.None) {
							_exitReason = end;
							return;
						}
						break;
					case "message":
						NotificationHandler(root.ReadString("topic"), root.ReadElement("data"));
						break;
					case "reconnect":
						_reconnectToken = root.ReadString("data.reconnect_token");
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsConfusion, $"StreamElements sent reconnect: {root.ReadString("data.message")}");
						_exitReason = SessionExitReason.ReconnectRequested;
						return;
					default:
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsConfusion, $"StreamElements sent a message of type {type}");
						ConsoleLogger.LogToFile(json);
						break;
				}
			}
			_exitReason = SessionExitReason.CancelRequested;
		}

		// None to carry on, or why the session ends
		static SessionExitReason HandleResponse(JsonElement root) {
			string nonce = root.ReadString("nonce");
			string error = root.ReadString("error");
			string message = root.ReadString("data.message");
			bool alreadySubscribed = error == ALREADY_SUBSCRIBED_ERROR && message == ALREADY_SUBSCRIBED_MESSAGE;

			if (error.Length > 0 && !alreadySubscribed) {
				if (error == "err_unauthorized") {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error SES3: StreamElements refused the token: {message}");
					return SessionExitReason.Unauthorized;
				}
				if (nonce == SUBSCRIBE_NONCE) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error SES3: StreamElements subscribe failed: {error} {message}");
					return SessionExitReason.SubscribeFailed;
				}
				ConsoleLogger.LogToFile($"StreamElements {nonce}: {error} {message}", true);
				return SessionExitReason.None;
			}

			if (!_connected) {
				_connected = true;
				_failures = 0;
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsNotification, $"StreamElements subscribed to {TIPS_TOPIC}");
				if (!_announced) {
					_announced = true;
					TwitchIRCManager.SendMessage("🟣 SE Connected");
				}
			}
			return SessionExitReason.None;
		}

		// Subscribes, then subscribes again every probe interval until the session ends
		static async Task SubscribeAndProbeAsync(WebSocket socket, CancellationToken session) {
			try {
				string nonce = SUBSCRIBE_NONCE;
				for (int probe = 1; ; probe++) {
					await socket.SendAsync(SubscribeFrame(nonce), WebSocketMessageType.Text, true, session);
					await Task.Delay(Times.Probe, session);
					nonce = $"probe-{probe}";
				}
			}
			catch (Exception ex) {
				if (session.IsCancellationRequested || socket.State != WebSocketState.Open) {
					return;
				}
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error SES4");
				ConsoleLogger.LogToFile(ex);
			}
		}

		// A subscribe command for the tips topic
		static byte[] SubscribeFrame(string nonce) {
			var body = new {
				type = "subscribe",
				nonce,
				data = new {
					topic = TIPS_TOPIC,
					room = _channelId,
					token = _jwt,
					token_type = "jwt",
				},
			};
			return JsonSerializer.SerializeToUtf8Bytes(body);
		}

		// Throws TimeoutException if the connect hangs, OperationCanceledException only when token is cancelled
		static async Task ConnectOrTimeoutAsync(ClientWebSocket socket, Uri uri, TimeSpan timeout, CancellationToken token) {
			using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
			linked.CancelAfter(timeout);
			try {
				await socket.ConnectAsync(uri, linked.Token);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested) {
				throw new TimeoutException($"StreamElements connect took over {timeout}");
			}
		}

		// Reason is None when a whole message arrived. Gives up with SilenceTimeout once sinceLast passes timeout;
		// a timed-out receive aborts the socket. A dropped connection returns ConnectionLost. Cancelling token throws.
		static async Task<(string Json, SessionExitReason Reason)> ReceiveFullMessage(WebSocket socket, Stopwatch sinceLast, TimeSpan timeout, CancellationToken token) {
			var buffer = new byte[8192];
			var sb = new StringBuilder();

			WebSocketReceiveResult result;

			do {
				if (socket.State != WebSocketState.Open) {
					return ("", SessionExitReason.SocketDied);
				}
				TimeSpan remaining = timeout - sinceLast.Elapsed;
				if (remaining <= TimeSpan.Zero) {
					return ("", SessionExitReason.SilenceTimeout);
				}
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token)) {
					linked.CancelAfter(remaining);
					try {
						result = await socket.ReceiveAsync(buffer, linked.Token);
					}
					catch (OperationCanceledException) when (!token.IsCancellationRequested) {
						return ("", SessionExitReason.SilenceTimeout);
					}
					catch (WebSocketException ex) {
						ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsConfusion, $"StreamElements connection lost ({ex.WebSocketErrorCode}): {ex.GetBaseException().Message}");
						return ("", SessionExitReason.ConnectionLost);
					}
				}
				if (result.MessageType == WebSocketMessageType.Close) {
					string close = FormatClose(result.CloseStatus, result.CloseStatusDescription);
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.StreamElementsConfusion, $"StreamElements closed the socket: {close}");
					return ("", SessionExitReason.SocketClosed);
				}
				sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
			}
			while (!result.EndOfMessage);

			return (sb.ToString(), SessionExitReason.None);
		}

		// Closes or aborts the socket and resets the session state
		static async Task CleanupSession(bool graceful) {
			try {
				if (graceful) {
					if (_socket != null && (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)) {
						using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
						await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", timeout.Token);
					}
				}
				else {
					_socket?.Abort();
				}
				_socket?.Dispose();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error SES2");
				ConsoleLogger.LogToFile(ex);
			}

			_socket = null;
			_connected = false;
			SinceLastMessage.Restart();
		}

		// Socket state and close status, for the session-ended line
		static string DescribeSocket() {
			if (_socket == null) {
				return "no socket";
			}
			string close = FormatClose(_socket.CloseStatus, _socket.CloseStatusDescription);
			return $"socket {_socket.State}, close {close}";
		}

		// "<code> <name> "<description>"", or "none"
		static string FormatClose(WebSocketCloseStatus? status, string? description) {
			if (status == null) {
				return "none";
			}
			return $"{(int)status} {status} \"{description}\"";
		}
	}
}
