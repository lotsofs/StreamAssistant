public class CommunityGiftSub {
	readonly object _lock = new();
	public readonly HashSet<string> Recipients = new();
	readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
	int _expectedCount = int.MaxValue;
	bool _completed = false;

	public string Id = "";

	public CommunityGiftSub(string id) {
		Id = id;
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
			CheckCondition();
		}
	}

	public void CheckCondition() {
		if (_completed) return;
		if (Recipients.Count >= _expectedCount) {
			_completed = true;
			_tcs.TrySetResult(true);
		}
	}

	public async Task<HashSet<string>> WaitForRecipientsAsync() {
		await Task.WhenAny(_tcs.Task, Task.Delay(10000));
		lock (_lock) {
			return [..Recipients];
		}
	}
}