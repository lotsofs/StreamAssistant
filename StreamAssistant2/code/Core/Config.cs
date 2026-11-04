using System.Text.Json;

namespace StreamAssistant2 {
	/// <summary>
	/// Loads paths.json from the working directory (the project folder), then secrets.json from the
	/// BotInput directory it names, environment variables expanded. A missing secrets.json is created
	/// there from the secrets.json.example template, and startup stops until it's filled in. Says when
	/// the BotInput folder must be picked, and writes paths.json naming it.
	/// </summary>
	static class Config {
		const string PATHS_FILE = "paths.json";
		const string SECRETS_FILE = "secrets.json";
		const string SECRETS_TEMPLATE = "secrets.json.example";
		const string DEFAULT_BOT_INPUT = @"%APPDATA%\StreamAssistant2\Bot Input";

		static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
		static readonly JsonSerializerOptions _indented = new() { WriteIndented = true };

		public static ConfigModel Data { get; private set; } = new();

		/// <summary>
		/// Why the BotInput folder must be picked, and the folder to open the picker at. Null when paths.json
		/// names a folder holding secrets.json, or isn't valid JSON.
		/// </summary>
		internal static (string Reason, string StartAt)? NeedsBotInput(string pathsFile = PATHS_FILE) {
			string fallback = NearestExisting(Environment.ExpandEnvironmentVariables(DEFAULT_BOT_INPUT));
			if (!File.Exists(pathsFile)) {
				return ($"No {PATHS_FILE}. Pick the Bot Input folder", fallback);
			}
			ConfigModel paths;
			try {
				paths = Read(pathsFile);
			}
			catch (JsonException) {
				return null;
			}
			string botInput = Environment.ExpandEnvironmentVariables(paths.Directories.BotInput);
			if (botInput.Length == 0) {
				return ($"{PATHS_FILE} has no BotInput. Pick the Bot Input folder", fallback);
			}
			if (File.Exists(Path.Combine(botInput, SECRETS_FILE))) {
				return null;
			}
			return ($"{botInput} has no {SECRETS_FILE}. Pick the Bot Input folder", NearestExisting(botInput));
		}

		/// <summary>
		/// Writes paths.json naming the BotInput folder.
		/// </summary>
		internal static void SavePaths(string botInput, string pathsFile = PATHS_FILE) {
			var paths = new { directories = new { BotInput = botInput } };
			File.WriteAllText(pathsFile, JsonSerializer.Serialize(paths, _indented));
		}

		public static void Load() {
			ConfigModel paths = Read(PATHS_FILE);
			string botInput = Environment.ExpandEnvironmentVariables(paths.Directories.BotInput);
			if (botInput.Length == 0) {
				throw new InvalidDataException($"{Path.GetFullPath(PATHS_FILE)} has no BotInput. Fill in the folder for secrets.json and settings.json and start again.");
			}

			string secretsPath = Path.Combine(botInput, SECRETS_FILE);
			if (!File.Exists(secretsPath)) {
				Directory.CreateDirectory(botInput);
				File.Copy(SECRETS_TEMPLATE, secretsPath);
				throw new FileNotFoundException($"Created {secretsPath} from {SECRETS_TEMPLATE}. Fill in the real values and start again.");
			}
			ConfigModel secrets = Read(secretsPath);
			if (secrets.TwitchAuth.Login.Length == 0) {
				throw new InvalidDataException($"{secretsPath} has no twitchAuth login. Fill in the broadcaster's login and start again.");
			}
			secrets.TwitchAuth.Login = secrets.TwitchAuth.Login.ToLowerInvariant();
			secrets.TwitchBot.Login = secrets.TwitchBot.Login.ToLowerInvariant();

			Data = new ConfigModel {
				Directories = new DirectoriesConfig { BotInput = botInput },
				Obs = secrets.Obs,
				TwitchAuth = secrets.TwitchAuth,
				TwitchBot = secrets.TwitchBot,
				TwitchIds = secrets.TwitchIds,
				StreamElements = secrets.StreamElements,
			};
		}

		static ConfigModel Read(string path) {
			if (!File.Exists(path)) {
				throw new FileNotFoundException($"Config file not found: {Path.GetFullPath(path)}");
			}
			return JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(path), _jsonOptions) ?? new ConfigModel();
		}

		/// <summary>
		/// The folder, or the nearest one above it that exists; empty when none does.
		/// </summary>
		static string NearestExisting(string folder) {
			string? dir = Path.GetFullPath(folder);
			while (dir != null && !Directory.Exists(dir)) {
				dir = Path.GetDirectoryName(dir);
			}
			return dir ?? "";
		}
	}

	public class ConfigModel {
		// paths.json
		public DirectoriesConfig Directories { get; set; } = new();
		// secrets.json
		public ObsSocketConfig Obs { get; set; } = new();
		public TwitchAuthConfig TwitchAuth { get; set; } = new();
		public TwitchAuthConfig TwitchBot { get; set; } = new();
		public TwitchIdConfig TwitchIds { get; set; } = new();
		public StreamElementsConfig StreamElements { get; set; } = new();
	}

	public class DirectoriesConfig {
		// Holds secrets.json, settings.json, games.json and leftpanel.json
		public string BotInput { get; set; } = "";
	}

	public class ObsSocketConfig {
		public string SocketPassword { get; set; } = "";
	}

	public class TwitchAuthConfig {
		public string Login { get; set; } = "";
		public string AccessToken { get; set; } = "";
		public string RefreshToken { get; set; } = "";
		public string ClientId { get; set; } = "";
	}

	public class TwitchIdConfig {
		public string BroadcasterId { get; set; } = "";
		public string ModeratorId { get; set; } = "";
		public string UserId { get; set; } = "";
		public string TestBroadcasterId { get; set; } = "";
	}

	public class StreamElementsConfig {
		public string ChannelId { get; set; } = "";
		public string Jwt { get; set; } = "";
	}
}
