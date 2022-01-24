using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2 {
	public class Game {
		public class Scene {
			public string Name;
			public int Duration;

			public Scene(string name, int duration) {
				Name = name;
				Duration = duration;
			}
		}
		
		public string Name;
		public List<Scene> Scenes;
		public Scene PreviousSplitScene;
	}
}
