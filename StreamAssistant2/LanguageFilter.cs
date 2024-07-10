using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace StreamAssistant2 {
	internal static class LanguageFilter {

		// This list is from some Oxford English Dictionary, found at https://github.com/sujithps/Dictionary 
		// Yes, it is 11 years old. It is impossible to find definition dictionaries online in a Ctrl+F'able format.
		// It includes all words/definitions marked as "Offens."
		// Replacement suggestions are mine based on the definitions given.
		// Complaints for inclusion or non-inclusion of words go their way, not mine. 
		static Dictionary<string, string> Replacements = new Dictionary<string, string>() {
			//{"test", "blah"},
			{"yid", "judaist"},
			{"wrinkly", "shriveled"},
			{"wop", "italian"},
			{"wog", "foreigner"},
			{"vegetable", "herbaceous food"},
			{"turk", "central asian"},
			{"tart", "pastry cake"},
			{"taffy", "welsh"},
			{"spastic", "affected by muscle spasm"},
			{"spade", "shovel"},
			{"skirt", "draped garment"},
			{"redskin", "american indian"},
			{"red indian", "american indian"},
			{"queer", "strange"},
			{"queen", "female monarch"},
			{"poof", "sudden disappearance"},
			{"ponce", "pimp"},
			{"pommy", "british immigrant"},
			{"pom", "british immigrant"},
			{"pickaninny", "child"},
			{"piccaninny", "child"},
			{"pansy", "cultivated plant"},
			{"paki", "purity"},
			{"paddy", "irish"},
			{"nip", "nihonjin"},
			{"nigger", "person"},
			{"native", "indigenous"},
			{"nancy", "effeminate man"},
			{"moonie", "unificationist"},
			{"mongoloid", "nomadic asianlike"},
			{"mongol", "nomadic asian"},
			{"mick", "irish"},
			{"loony-bin", "psychiatric hospital"},
			{"limey", "brit"},
			{"lay", "fuck buddy"},
			{"kraut", "german"},
			{"knock", "strike"},
			{"kaffir", "infidel"},
			{"jim crow", "segregation"},
			{"jewess", "judaist"},
			{"jew", "judaist"},
			{"jesuitical", "equivocating"},
			{"jap", "nihonjin"},
			{"hun", "nomadic german"},
			{"homo", "same"},
			{"harelip", "cleft"},
			{"half-caste", "mixed"},
			{"half-breed", "mixed"},
			{"frog", "amphibian"},
			{"fluff", "lint"},
			{"fairy", "winged legendary being"},
			{"fagot", "bundle of sticks"},
			{"faggot", "chopped liver"},
			{"fag", "cigarette"},
			{"darky", "person"},
			{"dago", "foreigner"},
			{"cunt", "vagina"},
			{"crumpet", "flat cake"},
			{"coon", "trash panda"},
			{"coloured", "non-white"},
			{"colored", "non-white"},
			{"chink", "chinese"},
			{"chinaman", "cricket ball"},
			{"bitch", "female dog"},
			{"bint", "woman"},
			{"bastard", "misbegotten"},
			{"bantustan", "homeland"},
			{"bantu", "people"},
			{"asshole", "anus"},
			{"asiatic", "asian"},
			{"arsehole", "anus"},
			{"abo", "indigenous"},
			// Unescape erroneously escaped characters
			{"\\.", "." },
			{"\\'", "'" },
		};

		// Yes, this is a really dumb replacement algorithm. The "Scunthorpe problem" is a solution, not a problem.
		internal static string ReplaceBadWords(string s) {
			foreach (string key in Replacements.Keys) {
				s = s.Replace(key, Replacements[key], true, null);
			}
			return s;
		}
	}
}
