using System.Text.Json;

namespace StreamAssistant2 {
	/// <summary>
	/// Loads paths.json from the working directory (the project folder), then secrets.json from the
	/// BotInput directory it names. A missing secrets.json is created there from the
	/// secrets.json.example template, and startup stops until it's filled in.
	/// </summary>
	static class Config {
		const string PATHS_FILE = "paths.json";
		const string SECRETS_FILE = "secrets.json";
		const string SECRETS_TEMPLATE = "secrets.json.example";

		static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

		public static ConfigModel Data { get; private set; } = new();

		public static void Load() {
			ConfigModel paths = Read(PATHS_FILE);

			string secretsPath = Path.Combine(paths.Directories.BotInput, SECRETS_FILE);
			if (!File.Exists(secretsPath)) {
				File.Copy(SECRETS_TEMPLATE, secretsPath);
				throw new FileNotFoundException($"Created {secretsPath} from {SECRETS_TEMPLATE}. Fill in the real values and start again.");
			}
			ConfigModel secrets = Read(secretsPath);

			Data = new ConfigModel {
				Directories = paths.Directories,
				Obs = secrets.Obs,
				TwitchAuth = secrets.TwitchAuth,
				TwitchIds = secrets.TwitchIds,
			};
		}

		static ConfigModel Read(string path) {
			if (!File.Exists(path)) {
				throw new FileNotFoundException($"Config file not found: {Path.GetFullPath(path)}");
			}
			return JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(path), _jsonOptions) ?? new ConfigModel();
		}
	}

	public class ConfigModel {
		// paths.json
		public DirectoriesConfig Directories { get; set; } = new();
		// secrets.json
		public ObsSocketConfig Obs { get; set; } = new();
		public TwitchAuthConfig TwitchAuth { get; set; } = new();
		public TwitchIdConfig TwitchIds { get; set; } = new();
	}

	public class DirectoriesConfig {
		public string BotInput { get; set; } = "";
		public string BotOutput { get; set; } = "";
		public string Trains { get; set; } = "";
		public string Colors { get; set; } = "";
		public string ColorSchemes { get; set; } = "";
		public string Backgrounds { get; set; } = "";
	}

	public class ObsSocketConfig {
		public string SocketPassword { get; set; } = "";
	}

	public class TwitchAuthConfig {
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
}
