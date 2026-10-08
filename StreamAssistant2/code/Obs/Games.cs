using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Games {
		internal sealed record Game(string Name, List<string> GameCapture, List<string> WindowCapture, List<string> AudioCapture);

		sealed class GameDto {
			public string? Name { get; set; }
			public List<string>? GameCaptureExecutable { get; set; }
			public List<string>? WindowCaptureExecutable { get; set; }
			public List<string>? AudioCaptureExecutable { get; set; }
		}

		const string GAMES_FILE = "games.json";
		const string FALLBACK_ID = "0";

		static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
		static readonly Game NONE = new("None", [], [], []);

		static Dictionary<string, Game> _games = new();
		static string? _categoryId;
		static readonly object _categoryLock = new();

		/// <summary>
		/// The last category seen; null until one arrives after a start.
		/// </summary>
		internal static string? CategoryId {
			get { lock (_categoryLock) return _categoryId; }
			set { lock (_categoryLock) _categoryId = value; }
		}

		/// <summary>
		/// Fetches the channel's current category id; replaced in tests.
		/// </summary>
		internal static Func<Task<string?>> FetchCategoryId = TwitchHelixApi.GetChannelCategoryIdAsync;

		/// <summary>
		/// Loads games.json from BotInput; a missing or broken file leaves the list empty.
		/// </summary>
		internal static void Load() {
			try {
				string path = Path.Combine(Config.Data.Directories.BotInput, GAMES_FILE);
				if (!File.Exists(path)) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"{GAMES_FILE} not found, per-game setup off");
					return;
				}
				_games = Parse(File.ReadAllText(path));
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Loaded {_games.Count} games");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error GMS1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// Category id → game; entries without a name are skipped, missing executable lists become empty.
		/// </summary>
		internal static Dictionary<string, Game> Parse(string json) {
			var raw = JsonSerializer.Deserialize<Dictionary<string, GameDto>>(json, _jsonOptions) ?? new();
			Dictionary<string, Game> games = new();
			foreach (var (id, dto) in raw) {
				if (string.IsNullOrWhiteSpace(dto?.Name)) {
					continue;
				}
				games[id] = new Game(dto.Name, dto.GameCaptureExecutable ?? [], dto.WindowCaptureExecutable ?? [], dto.AudioCaptureExecutable ?? []);
			}
			return games;
		}

		/// <summary>
		/// The category's game, else the fallback "0" game, else None.
		/// </summary>
		internal static Game Lookup(IReadOnlyDictionary<string, Game> games, string categoryId) {
			if (games.TryGetValue(categoryId, out Game? game)) {
				return game;
			}
			return games.TryGetValue(FALLBACK_ID, out Game? fallback) ? fallback : NONE;
		}

		/// <summary>
		/// The chat line for a category change, or null when the category is unchanged.
		/// </summary>
		internal static string? CategoryChangeMessage(string? oldId, string newId) {
			if (oldId == newId) {
				return null;
			}
			return oldId == null ? $"Stream category change to {newId}" : $"Stream category change from {oldId} to {newId}";
		}

		/// <summary>
		/// On a channel.update that changes the category: posts the change and applies the new game off the caller's thread.
		/// </summary>
		internal static void HandleUpdate(JsonElement evt) {
			string newId = evt.ReadString("category_id", "");
			string? message;
			lock (_categoryLock) {
				message = CategoryChangeMessage(_categoryId, newId);
				if (message == null) {
					return;
				}
				_categoryId = newId;
			}
			TwitchIRCManager.SendMessage(message);
			FireForget.Run("GMS_86", "category change", () => ApplyAsync(newId));
		}

		/// <summary>
		/// Applies a category's game on the thread pool.
		/// </summary>
		static async Task ApplyAsync(string categoryId) {
			await Task.Yield();
			Apply(categoryId);
		}

		/// <summary>
		/// On going live: applies the last category seen, or the one Helix reports when none has been seen.
		/// </summary>
		internal static async Task OnStreamStartedAsync() {
			await Task.Yield();
			string? id = CategoryId;
			if (id == null) {
				id = await FetchCategoryId();
				if (id == null) {
					ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Going live: category unknown, no game setup");
					return;
				}
				lock (_categoryLock) {
					_categoryId ??= id;
				}
			}
			Apply(id);
		}

		/// <summary>
		/// Runs an action when OBS is connected, now or on its next connect; replaced in tests.
		/// </summary>
		internal static Action<Action> WhenObsConnected = ObsConnection.WhenConnected;

		static int _bootApplied;

		/// <summary>
		/// On bot start: stores the channel's current category from Helix and applies it once, when OBS is first connected.
		/// </summary>
		internal static async Task OnBootAsync() {
			Interlocked.Exchange(ref _bootApplied, 0);
			string? fetched = await FetchCategoryId();
			if (fetched == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Boot: category unknown, no game setup");
				return;
			}
			lock (_categoryLock) {
				_categoryId ??= fetched;
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Boot: category {fetched}, applied when OBS is connected");
			WhenObsConnected(ApplyBootCategory);
		}

		/// <summary>
		/// Applies the stored category on the thread pool, the first time it's called after a boot.
		/// </summary>
		static void ApplyBootCategory() {
			if (Interlocked.Exchange(ref _bootApplied, 1) == 1) {
				return;
			}
			string? id = CategoryId;
			if (id == null) {
				return;
			}
			FireForget.Run("GMS_boot", "boot game setup", async () => {
				await Task.Yield();
				Apply(id);
			});
		}

		/// <summary>
		/// Sets up the layout for a category's game.
		/// </summary>
		internal static void Apply(string categoryId) {
			Game game = Lookup(_games, categoryId);
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Game setup: {game.Name} ({categoryId})");
			GameBackground.Set(game.Name);
			GameColor.Set(game.Name);
			GameCapture.Set(game);
		}
	}
}
