public class CommunityGiftSub {
	readonly object _lock = new();
	public readonly HashSet<string> Recipients = new();
	readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
	int _expectedCount = int.MaxValue;
	bool _completed = false;
	bool _hasExpected = false;
	bool _announced = false;

	public string Id = "";
	// When the first event for this bomb arrived
	public readonly DateTime CreatedUtc;

	// The bomb event itself has arrived
	public bool HasExpected { get { lock (_lock) return _hasExpected; } }

	// The bomb has been announced
	public bool Announced { get { lock (_lock) return _announced; } }

	public CommunityGiftSub(string id, DateTime createdUtc) {
		Id = id;
		CreatedUtc = createdUtc;
	}

	public void AddRecipient(string recipient) {
		lock (_lock) {
			Recipients.Add(recipient);
			CheckCondition();
		}
	}

	public void SetExpected(int total) {
		lock (_lock) {
			_expectedCount = total;
			_hasExpected = true;
			CheckCondition();
		}
	}

	// Records that the bomb has been announced
	public void MarkAnnounced() {
		lock (_lock) {
			_announced = true;
		}
	}

	// Recipients so far
	public List<string> RecipientsSoFar() {
		lock (_lock) {
			return [..Recipients];
		}
	}

	public void CheckCondition() {
		if (_completed) return;
		if (Recipients.Count >= _expectedCount) {
			_completed = true;
			_tcs.TrySetResult(true);
		}
	}

	// Waits until every recipient has arrived or the timeout (default 10 s) passes; returns the recipients so far
	public async Task<HashSet<string>> WaitForRecipientsAsync(TimeSpan? timeout = null) {
		await Task.WhenAny(_tcs.Task, Task.Delay(timeout ?? TimeSpan.FromSeconds(10)));
		lock (_lock) {
			return [..Recipients];
		}
	}
}