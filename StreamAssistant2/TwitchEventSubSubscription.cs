namespace StreamAssistant2 {
	public class TwitchEventSubSubscription {
		public class TwitchEventSubEvent {
			public string Type { get; set; }
			public string Version { get; set; }
			public bool RequiresBroadcasterId { get; set; }
			public bool RequiresModeratorId { get; set; }
			public bool WantsBroadcasterAsModerator { get; set; }
			public bool RequiresUserId { get; set; }
		}

		public static readonly List<TwitchEventSubEvent> Subscriptions = new() {
			new() {
				Type = "channel.follow",
				Version = "2",
				RequiresBroadcasterId = true,
				RequiresModeratorId = true,
				WantsBroadcasterAsModerator = true,
			},
			new() {
				Type = "channel.channel_points_custom_reward_redemption.add",
				Version = "1",
				RequiresBroadcasterId = true,
				RequiresModeratorId = false,
			},
			new() {
				Type = "channel.chat.notification",
				Version = "1",
				RequiresBroadcasterId = true,
				RequiresUserId = true,
			}
		};
	}
}
