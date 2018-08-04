using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;

namespace StreamAssistant2
{
	public class EventLog : ISaveable
	{
		public Action OnCountChanged;

		public string Path;

		int count;
		public int Count
		{
			get {
				return count;
			}
			set {
				count = value;
				if (OnCountChanged != null) {
					OnCountChanged();
				}
			}
		}

		string[] events;	// circular array
		int nextEventIndex;

		public EventLog() {
			LoadSettings();
			RebuildEventLog();

			OnCountChanged += RebuildEventLog;
		}

		public void AddEvent(int a) {
			
			// stuff that needs to be written here

			WriteEvents(nextEventIndex);
			nextEventIndex--;
			if (nextEventIndex == -1) {
				nextEventIndex = count - 1;
			}
		}

		/// <summary>
		/// Writes the recent event list to file
		/// </summary>
		/// <param name="startFrom"></param>
		public void WriteEvents(int startFrom) {
			if (string.IsNullOrEmpty(Path)) {
				return;
			}

			string output = string.Empty;
			for (int i = 0; i < count; i++) {
				int j = i + startFrom;
				j %= count;
				output += events[j];
				output += Environment.NewLine;
			}
			File.WriteAllText(Path, output);
		}

		/// <summary>
		/// reads the file to view event log. eg. at startup or when size changed.
		/// </summary>
		public void RebuildEventLog() {
			events = new string[count];

			if (string.IsNullOrEmpty(Path)) {
				return;
			}
			string[] currentFileContents = File.ReadAllLines(Path);
			currentFileContents.Reverse();
			Array.Copy(currentFileContents, events, count);

			nextEventIndex = count - 1;
		}

		#region saveload

		/// <summary>
		/// load settings
		/// </summary>
		public void LoadSettings() {
			SaveLoad.OnSave += SaveSettings;
			System.Collections.Specialized.NameValueCollection appSettings = ConfigurationManager.AppSettings;

			Path = appSettings["EventLogOutputFilePath"];
			count = int.Parse(appSettings["EventLogCount"]);
		}

		/// <summary>
		/// Save
		/// </summary>
		public void SaveSettings() {
			Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			config.AppSettings.Settings["EventLogOutputFilePath"].Value = Path;
			config.AppSettings.Settings["EventLogCount"].Value = count.ToString();
			config.Save();
		}

		#endregion

	}
}
