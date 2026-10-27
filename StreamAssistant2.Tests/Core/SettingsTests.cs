using System.Text.Json;
using Xunit;

namespace StreamAssistant2.Tests {
	public class SettingsTests {
		static string Json(SettingsModel model) => JsonSerializer.Serialize(model);

		[Theory]
		[InlineData("{}")]
		[InlineData("{ \"Train\": {} }")]
		[InlineData("// a comment\n{ \"Rewards\": {}, }")]
		public void NothingSet_IsTheDefaults(string json) {
			Assert.Equal(Json(new SettingsModel()), Json(Settings.Parse(json)));
			Assert.Empty(Settings.Changed(Settings.Parse(json)));
		}

		[Fact]
		public void PartialSection_KeepsTheOtherKeys() {
			SettingsModel model = Settings.Parse("{ \"Train\": { \"Seconds\": 30 } }");
			Assert.Equal(30, model.Train.Seconds);
			Assert.Equal("Image: Train", model.Train.Source);
			Assert.Equal(["Train.Seconds"], Settings.Changed(model));
		}

		[Fact]
		public void PartialSound_KeepsItsFile() {
			SettingsModel model = Settings.Parse("{ \"Sounds\": { \"TribalHymn\": { \"Volume\": 0.3 } } }");
			Assert.Equal(0.3, model.Sounds.TribalHymn.Volume);
			Assert.Equal("Tribal hymn.mp3", model.Sounds.TribalHymn.File);
			Assert.Equal("theclap.mp3", model.Sounds.TheClap.File);
			Assert.Equal(["Sounds.TribalHymn.Volume"], Settings.Changed(model));
		}

		[Fact]
		public void Keys_AnyCase() {
			Assert.Equal("X", Settings.Parse("{ \"train\": { \"source\": \"X\" } }").Train.Source);
		}

		[Fact]
		public void SidebarSources_ReplaceTheDefaults() {
			Assert.Equal(["A"], Settings.Parse("{ \"Game\": { \"SidebarSources\": [\"A\"] } }").Game.SidebarSources);
		}

		[Fact]
		public void NameColor_OnByDefault_AndSwitchesOff() {
			Assert.True(new SettingsModel().NameColor.Enabled);
			SettingsModel model = Settings.Parse("{ \"NameColor\": { \"Enabled\": false } }");
			Assert.False(model.NameColor.Enabled);
			Assert.Equal(["NameColor.Enabled"], Settings.Changed(model));
		}

		[Fact]
		public void Directories_ExpandEnvironmentVariables() {
			string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Out");
			Assert.Equal(expected, Settings.Parse("{ \"Directories\": { \"BotOutput\": \"%APPDATA%\\\\Out\" } }").Directories.BotOutput);
		}

		[Theory]
		[InlineData("{ \"Trian\": {} }", "Trian")]
		[InlineData("{ \"Train\": { \"Second\": 3 } }", "Second")]
		[InlineData("{ \"Train\": { \"Source\": null } }", "Train.Source")]
		[InlineData("{ \"Train\": null }", "Train")]
		[InlineData("{ \"Game\": { \"SidebarSources\": [null] } }", "Game.SidebarSources")]
		[InlineData("{ \"Train\": { \"Seconds\": -1 } }", "Train.Seconds")]
		[InlineData("{ \"Sounds\": { \"Flush\": { \"Volume\": 2 } } }", "Sounds.Flush.Volume")]
		[InlineData("{ \"DiskSpace\": { \"WarnEveryMinutes\": 7 } }", "DiskSpace.WarnEveryMinutes")]
		[InlineData("{ \"Ads\": { \"IntervalMinutes\": 4 } }", "Ads.IntervalMinutes")]
		[InlineData("{ \"Game\": { \"CaptureSlots\": 0 } }", "Game.CaptureSlots")]
		[InlineData("{ \"MoneyGoal\": { \"DefaultGoal\": 0 } }", "MoneyGoal.DefaultGoal")]
		[InlineData("{ \"NameColor\": { \"IntervalSeconds\": 4 } }", "NameColor.IntervalSeconds")]
		[InlineData("{ \"Train\": { \"Seconds\": \"x\" } }", "Train.Seconds")]
		public void BadSettings_ThrowNamingTheKey(string json, string key) {
			Exception ex = Assert.ThrowsAny<Exception>(() => Settings.Parse(json));
			Assert.True(ex is JsonException or InvalidDataException, ex.GetType().Name);
			Assert.Contains(key, ex.Message);
		}

		[Theory]
		[InlineData("")]
		[InlineData("null")]
		[InlineData("{ \"Train\": ")]
		public void NotSettings_Throw(string json) {
			Exception ex = Assert.ThrowsAny<Exception>(() => Settings.Parse(json));
			Assert.True(ex is JsonException or InvalidDataException, ex.GetType().Name);
		}

		[Fact]
		public void Example_IsTheDefaults() {
			string example = Path.Combine(ColorData.RepoPath(), "StreamAssistant2", "settings.json.example");
			SettingsModel model = Settings.Parse(File.ReadAllText(example));
			Assert.Equal(Json(new SettingsModel()), Json(model));
		}

		[Fact]
		public void Example_HasEverySetting() {
			string example = Path.Combine(ColorData.RepoPath(), "StreamAssistant2", "settings.json.example");
			Assert.Empty(Settings.Missing(File.ReadAllText(example)));
		}

		[Fact]
		public void Missing_EmptyFile_IsEverySection() {
			Assert.Equal(typeof(SettingsModel).GetProperties().Select(p => p.Name), Settings.Missing("{}"));
		}

		[Fact]
		public void Missing_ListsTheKeysLeftOut_AnyCase() {
			List<string> missing = Settings.Missing("// c\n{ \"train\": { \"seconds\": 1, \"SCENE\": \"x\", }, \"Sounds\": { \"Flush\": { \"File\": \"f\" } } }");
			Assert.Contains("Train.Source", missing);
			Assert.Contains("Train.Images", missing);
			Assert.DoesNotContain("Train.Seconds", missing);
			Assert.DoesNotContain("Train.Scene", missing);
			Assert.Contains("Sounds.Flush.Volume", missing);
			Assert.Contains("Sounds.TribalHymn", missing);
			Assert.DoesNotContain("Sounds.TribalHymn.File", missing);
			Assert.Contains("Rewards", missing);
			Assert.DoesNotContain("Rewards.Train", missing);
		}

		[Fact]
		public void MissingLine_NamesTheKeysAndTheExample() {
			Assert.Equal("settings.json lacks Train, Sounds.Flush.Volume; their defaults apply (settings.json.example has every key)",
				Settings.MissingLine(["Train", "Sounds.Flush.Volume"]));
		}

		[Fact]
		public void LoadedLine_GroupsTheKeysBySection() {
			Assert.Equal("Loaded settings.json, all defaults", Settings.LoadedLine([]));
			Assert.Equal("Loaded settings.json, changed Train: Seconds, Images; Sounds: Flush.Volume",
				Settings.LoadedLine(["Train.Seconds", "Train.Images", "Sounds.Flush.Volume"]));
		}
	}
}
