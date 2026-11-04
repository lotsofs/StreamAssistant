using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	public sealed class ConfigTests : IDisposable {
		readonly string _dir = Directory.CreateTempSubdirectory("config-").FullName;
		string PathsFile => Path.Combine(_dir, "paths.json");

		public void Dispose() {
			Directory.Delete(_dir, true);
		}

		void WritePaths(string botInput) {
			File.WriteAllText(PathsFile, JsonSerializer.Serialize(new { directories = new { BotInput = botInput } }));
		}

		string FolderWithSecrets(string name) {
			string folder = Path.Combine(_dir, name);
			Directory.CreateDirectory(folder);
			File.WriteAllText(Path.Combine(folder, "secrets.json"), "{}");
			return folder;
		}

		[Fact]
		public void NoPathsFile_AsksFromTheDefault() {
			string fallback = Environment.ExpandEnvironmentVariables(@"%APPDATA%\StreamAssistant2\Bot Input");
			(string Reason, string StartAt)? result = Config.NeedsBotInput(PathsFile);
			Assert.NotNull(result);
			Assert.Equal("No paths.json. Pick the Bot Input folder", result.Value.Reason);
			Assert.True(Directory.Exists(result.Value.StartAt), result.Value.StartAt);
			Assert.StartsWith(result.Value.StartAt, fallback);
		}

		[Theory]
		[InlineData("{}")]
		[InlineData("""{ "directories": {} }""")]
		[InlineData("""{ "directories": { "BotInput": "" } }""")]
		[InlineData("null")]
		public void NoBotInput_Asks(string json) {
			File.WriteAllText(PathsFile, json);
			(string Reason, string StartAt)? result = Config.NeedsBotInput(PathsFile);
			Assert.NotNull(result);
			Assert.Equal("paths.json has no BotInput. Pick the Bot Input folder", result.Value.Reason);
		}

		[Fact]
		public void FolderWithoutSecrets_AsksFromThatFolder() {
			string folder = Path.Combine(_dir, "input");
			Directory.CreateDirectory(folder);
			WritePaths(folder);
			(string Reason, string StartAt)? result = Config.NeedsBotInput(PathsFile);
			Assert.NotNull(result);
			Assert.Equal($"{folder} has no secrets.json. Pick the Bot Input folder", result.Value.Reason);
			Assert.Equal(folder, result.Value.StartAt);
		}

		[Fact]
		public void MissingFolder_AsksFromTheNearestThatExists_CreatingNothing() {
			string folder = Path.Combine(_dir, "a", "b");
			WritePaths(folder);
			(string Reason, string StartAt)? result = Config.NeedsBotInput(PathsFile);
			Assert.NotNull(result);
			Assert.Equal($"{folder} has no secrets.json. Pick the Bot Input folder", result.Value.Reason);
			Assert.Equal(_dir, result.Value.StartAt);
			Assert.False(Directory.Exists(Path.Combine(_dir, "a")));
		}

		[Fact]
		public void FolderWithSecrets_NeedsNothing() {
			WritePaths(FolderWithSecrets("input"));
			Assert.Null(Config.NeedsBotInput(PathsFile));
		}

		[Fact]
		public void BotInput_ExpandsEnvironmentVariables() {
			string name = "CONFIGTESTS_" + Guid.NewGuid().ToString("N");
			Environment.SetEnvironmentVariable(name, _dir);
			try {
				FolderWithSecrets("input");
				WritePaths($"%{name}%\\input");
				Assert.Null(Config.NeedsBotInput(PathsFile));
			}
			finally {
				Environment.SetEnvironmentVariable(name, null);
			}
		}

		[Fact]
		public void BrokenJson_IsLeftToLoad() {
			File.WriteAllText(PathsFile, """{ "directories": """);
			Assert.Null(Config.NeedsBotInput(PathsFile));
		}

		[Fact]
		public void SavePaths_NamesTheFolder() {
			string folder = FolderWithSecrets("Bot Input é");
			Config.SavePaths(folder, PathsFile);
			using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(PathsFile));
			Assert.Equal(folder, doc.RootElement.GetProperty("directories").GetProperty("BotInput").GetString());
			Assert.Null(Config.NeedsBotInput(PathsFile));
		}
	}
}
