using System.Text.Json;

namespace StreamAssistant2
{
	static class Config
	{
		private static readonly string _path = "secrets.json";

		public static SecretConfigModel Data { get; private set; } = new();

		public static void Load() {
			if (!File.Exists(_path)) {
				throw new FileNotFoundException($"Config file not found: {_path}");
			}

			string json = File.ReadAllText(_path);

			Data = JsonSerializer.Deserialize<SecretConfigModel>(json, new JsonSerializerOptions {
				PropertyNameCaseInsensitive = true
			}) ?? new SecretConfigModel();
		}
	}

	public class SecretConfigModel {
		public DirectoriesConfig Directories { get; set; } = new();
		public ObsSocketConfig Obs { get; set; } = new();
		public TwitchAuthConfig TwitchAuth { get; set; } = new();
		public TwitchIdConfig TwitchIds { get; set; } = new();
	}

	public class DirectoriesConfig {
		public string Trains { get; set; } = "";
		public string Colors { get; set; } = "";
		public string ColorSchemes { get; set; } = "";
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
