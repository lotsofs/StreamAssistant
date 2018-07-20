using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssistant2
{
	interface ISaveable
	{
		void LoadSettings();
		void SaveSettings();
	}
}
