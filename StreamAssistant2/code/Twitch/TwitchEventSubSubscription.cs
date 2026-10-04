namespace StreamAssistant2 {
	public class TwitchEventSubSubscription {
		public class TwitchEventSubEvent {
			public string Type { get; set; } = "";
			public string Version { get; set; } = "0";
			public bool RequiresBroadcasterId { get; set; }
			public bool RequiresModeratorId { get; set; }
			public bool WantsBroadcasterAsModerator { get; set; }
			public bool RequiresUserId { get; set; }
		}

		public static readonly List<TwitchEventSubEvent> Subscriptions = [
			new() {
				Type = "channel.ad_break.begin",
				Version = "1",
				RequiresBroadcasterId = true,
			},
			new() {
				Type = "channel.channel_points_custom_reward_redemption.add",
				Version = "1",
				RequiresBroadcasterId = true,
			},
			new() {
				Type = "channel.chat.notification",
				Version = "1",
				RequiresBroadcasterId = true,
				RequiresUserId = true,
			},
			new() {
				Type = "channel.cheer",
				Version = "1",
				RequiresBroadcasterId = true,
			},
			new() {
				Type = "channel.follow",
				Version = "2",
				RequiresBroadcasterId = true,
				RequiresModeratorId = true,
				WantsBroadcasterAsModerator = true,
			},
		];
	}
}
