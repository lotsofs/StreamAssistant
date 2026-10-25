using Xunit;

namespace StreamAssistant2.Tests {
	public class SoundTests {
		[Fact]
		public void SettingsFor_EachSoundHasItsOwn() {
			SoundsSettings sounds = new();
			List<string> files = Enum.GetValues<Sound.Sounds>().Select(s => Sound.SettingsFor(s, sounds).File).ToList();
			Assert.Equal(files.Count, files.Distinct().Count());
			Assert.Equal("Tribal hymn.mp3", Sound.SettingsFor(Sound.Sounds.TribalHymn, sounds).File);
			Assert.Equal(0.5, Sound.SettingsFor(Sound.Sounds.TribalHymn, sounds).Volume);
		}
	}
}
