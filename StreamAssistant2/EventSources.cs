using System.Collections.Generic;

namespace StreamAssistant2 {
	enum EventSources {
		None = 0,
		TwitchCheer = 102,
		TwitchSub = 103,
		TwitchReSub = 104,
		TwitchGiftSub = 105,
		TwitchGiftBomb = 106,
		TwitchRaid = 107,
		TwitchRewardRedemption = 112,
		TwitchStreamUpdate = 118,
		TwitchBroadcastUpdate = 122,
		TwitchChatMessage = 133,
		TwitchStreamOnline = 154,
		TwitchUpcomingAd = 186,
		StreamElementsTip = 1201,
	}
}
