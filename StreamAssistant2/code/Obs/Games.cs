using System.Text.Json;

namespace StreamAssistant2 {
	internal static class Games {
		/// <summary>
		/// One capture slot: the executable to follow, and its volume in dB if the slot sets one.
		/// </summary>
		internal sealed record Capture(string Executable, double? Volume = null);

		internal sealed record Game(string Name, List<Capture> GameCapture, List<Capture> WindowCapture, List<Capture> AudioCapture);

		sealed class GameDto {
			public string? Name { get; set; }
			public double? Volume { get; set; }
			public List<JsonElement>? GameCaptureExecutable { get; set; }
			public List<JsonElement>? WindowCaptureExecutable { get; set; }
			public List<JsonElement>? AudioCaptureExecutable { get; set; }
		}

		const string GAMES_FILE = "games.json";
		const string FALLBACK_ID = "0";
		const string DEFAULT_ID = "default";

		static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
		static readonly Game NONE = new("None", [], [], []);

		static Dictionary<string, Game> _games = new();
		static double? _defaultVolume;
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
				string json = File.ReadAllText(path);
				_games = Parse(json);
				_defaultVolume = ParseDefaultVolume(json);
				string volume = _defaultVolume is double db ? $"default volume {db} dB" : "no default volume, slots without one keep theirs";
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Loaded {_games.Count} games, {volume}");
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, "Error GMS1");
				ConsoleLogger.LogToFile(ex);
			}
		}

		/// <summary>
		/// The dB volume for slots that set none, from the "default" entry; also replaced in tests.
		/// </summary>
		internal static double? DefaultVolume {
			get => _defaultVolume;
			set => _defaultVolume = value;
		}

		/// <summary>
		/// Category id → game; the "default" entry and entries without a name are skipped, missing executable lists become empty.
		/// </summary>
		internal static Dictionary<string, Game> Parse(string json) {
			var raw = JsonSerializer.Deserialize<Dictionary<string, GameDto>>(json, _jsonOptions) ?? new();
			Dictionary<string, Game> games = new();
			foreach (var (id, dto) in raw) {
				if (id.Equals(DEFAULT_ID, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(dto?.Name)) {
					continue;
				}
				games[id] = new Game(dto.Name, Captures(dto.GameCaptureExecutable), Captures(dto.WindowCaptureExecutable), Captures(dto.AudioCaptureExecutable));
			}
			return games;
		}

		/// <summary>
		/// The "default" entry's Volume, or null if there is none.
		/// </summary>
		internal static double? ParseDefaultVolume(string json) {
			var raw = JsonSerializer.Deserialize<Dictionary<string, GameDto>>(json, _jsonOptions) ?? new();
			foreach (var (id, dto) in raw) {
				if (id.Equals(DEFAULT_ID, StringComparison.OrdinalIgnoreCase)) {
					return dto?.Volume;
				}
			}
			return null;
		}

		/// <summary>
		/// A capture list's entries: a plain "exe" string, or { "Exe": …, "Volume": dB }. Anything else, or an object without Exe, is skipped.
		/// </summary>
		static List<Capture> Captures(List<JsonElement>? entries) {
			List<Capture> captures = new();
			foreach (JsonElement e in entries ?? []) {
				if (e.ValueKind == JsonValueKind.String) {
					captures.Add(new Capture(e.GetString()!));
					continue;
				}
				if (e.ValueKind != JsonValueKind.Object) {
					continue;
				}
				string executable = "";
				double? volume = null;
				foreach (JsonProperty p in e.EnumerateObject()) {
					if (p.Name.Equals("Exe", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String) {
						executable = p.Value.GetString()!;
					}
					else if (p.Name.Equals("Volume", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Number) {
						volume = p.Value.GetDouble();
					}
				}
				if (executable.Length > 0) {
					captures.Add(new Capture(executable, volume));
				}
			}
			return captures;
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
		/// A category id from a games.json key, a game name (any case), or any all-digit id; null for anything else.
		/// </summary>
		internal static string? ResolveCategory(IReadOnlyDictionary<string, Game> games, string input) {
			if (games.ContainsKey(input)) {
				return input;
			}
			foreach (var (id, game) in games) {
				if (game.Name.Equals(input, StringComparison.OrdinalIgnoreCase)) {
					return id;
				}
			}
			return input.Length > 0 && input.All(char.IsAsciiDigit) ? input : null;
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
		/// On a channel.update: changes to its category, if that's a change.
		/// </summary>
		internal static void HandleUpdate(JsonElement evt) {
			ChangeCategory(evt.ReadString("category_id", ""));
		}

		/// <summary>
		/// "!changegame" / "!changecategory": changes to a category given by games.json key, game name or numeric id,
		/// locally only (Twitch's category stays). Logs when the input doesn't resolve or the category is already current.
		/// </summary>
		internal static void TryChangeCategory(string input) {
			string? id = ResolveCategory(_games, input);
			if (id == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, $"No game or category id \"{input}\"");
				return;
			}
			if (!ChangeCategory(id)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Category is already {id}");
			}
		}

		/// <summary>
		/// Stores a new category, posts the change and applies its game off the caller's thread; false if it's the current one.
		/// </summary>
		internal static bool ChangeCategory(string newId) {
			string? message;
			lock (_categoryLock) {
				message = CategoryChangeMessage(_categoryId, newId);
				if (message == null) {
					return false;
				}
				_categoryId = newId;
			}
			TwitchIRCManager.SendMessage(message);
			FireForget.Run("GMS_86", "category change", () => ApplyAsync(newId));
			return true;
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
		/// Runs an action when OBS is ready, now or on its next connect, returning whether it ran now; replaced in tests.
		/// </summary>
		internal static Func<Action, bool> WhenObsConnected = ObsConnection.WhenConnected;

		static int _bootApplied;

		/// <summary>
		/// On bot start: stores the channel's current category from Helix and applies it once, when OBS is first
		/// ready. Skipped if a category change already stored and applied one while the fetch was running.
		/// </summary>
		internal static async Task OnBootAsync() {
			Interlocked.Exchange(ref _bootApplied, 0);
			string? fetched = await FetchCategoryId();
			if (fetched == null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Important, "Boot: category unknown, no game setup");
				return;
			}
			string? earlier;
			lock (_categoryLock) {
				earlier = _categoryId;
				_categoryId ??= fetched;
			}
			if (earlier != null) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Boot: category {earlier} already set up by a category change, boot setup skipped");
				return;
			}
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, $"Boot: category {fetched}");
			if (!WhenObsConnected(ApplyBootCategory)) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Notification, "Boot: OBS not ready, game setup waits for it");
			}
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
		/// Sets up the layout for a category's game; a step that throws is logged and the later steps still run.
		/// </summary>
		internal static void Apply(string categoryId) {
			Game game = Lookup(_games, categoryId);
			ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.SceneChangesImportant, $"Game setup: {game.Name} ({categoryId})");
			RunStep("background", () => GameBackground.Set(game.Name));
			RunStep("colour", () => GameColor.Set(game.Name));
			RunStep("capture sources", () => GameCapture.Set(game));
		}

		/// <summary>
		/// Runs one setup step, logging a throw instead of passing it on.
		/// </summary>
		internal static void RunStep(string step, Action action) {
			try {
				action();
			}
			catch (Exception ex) {
				ConsoleLogger.ColoredLine(ConsoleLogger.ColorType.Error, $"Error GMS2: game setup {step} failed: {ex.Message}");
				ConsoleLogger.LogToFile(ex);
			}
		}
	}
}
