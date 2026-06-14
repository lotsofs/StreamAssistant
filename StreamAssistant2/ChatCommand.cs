namespace StreamAssistant2 {
	public class ChatCommand {
		internal string RegEx;
		internal string Output;
		internal string Name;

		internal ChatCommand(string name, string regEx, string output) {
			RegEx = regEx;
			Output = output;
			Name = name;
		}
	}

}
