using System.Text.RegularExpressions;

namespace StreamAssistant2 {
	public class ChatCommand {
		internal Regex RegEx;
		internal string Output;
		internal string Name;

		internal ChatCommand(string name, string regEx, string output) {
			RegEx = new Regex(regEx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			Output = output;
			Name = name;
		}
	}

}
