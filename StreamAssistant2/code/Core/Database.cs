using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace StreamAssistant2 {
	internal static class Database {
		static string _dbPath = "";

		static string _connectionString = "";
		
		internal async static Task InitAsync() {
			_dbPath = Path.Combine(Config.Data.Directories.BotOutput, "streamAssistant.db");
			_connectionString = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();

			await using var connection = new SqliteConnection(_connectionString);
			connection.Open();

			var cmd = connection.CreateCommand();
			cmd.CommandText = 
			@"
			CREATE TABLE IF NOT EXISTS flushes (
				flush_id TEXT PRIMARY KEY,
				user_id TEXT NOT NULL,
				user_login TEXT NOT NULL,
				created_at INTEGER NOT NULL
			);

			CREATE INDEX IF NOT EXISTS idx_flush_user_id
			ON flushes(user_id);

			CREATE TABLE IF NOT EXISTS money_goal (
				id INTEGER PRIMARY KEY CHECK (id = 1),
				current_cents INTEGER NOT NULL,
				goal_cents INTEGER NOT NULL
			);

			INSERT OR IGNORE INTO money_goal (id, current_cents, goal_cents)
			VALUES (1, 0, " + MoneyGoal.DEFAULT_GOAL_CENTS + @");
			";

			await cmd.ExecuteNonQueryAsync();
		}

		internal static async Task InsertFlushAsync(string flushId, string userId, string userLogin) {
			await using var connection = new SqliteConnection(_connectionString);
			await connection.OpenAsync();

			var cmd = connection.CreateCommand();
			cmd.CommandText =
			@"
			INSERT INTO flushes (flush_id, user_id, user_login, created_at)
			VALUES ($flush_id, $user_id, $user_login, $created_at);
			";

			cmd.Parameters.AddWithValue("$flush_id", flushId);
			cmd.Parameters.AddWithValue("$user_id", userId);
			cmd.Parameters.AddWithValue("$user_login", userLogin);
			cmd.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

			await cmd.ExecuteNonQueryAsync();
		}

		internal static async Task<List<string>> RetrieveFlushesAsync(string userId) {
			await using var connection = new SqliteConnection(_connectionString);
			await connection.OpenAsync();

			var results = new List<string>();
			
			var select = connection.CreateCommand();
			select.CommandText =
			@"
			SELECT flush_id
			FROM flushes
			WHERE user_id = $user_id
			";

			select.Parameters.AddWithValue("$user_id", userId);

			await using (var reader = await select.ExecuteReaderAsync()) {
				while (await reader.ReadAsync()) {
					results.Add(reader.GetString(0));
				}
			}

			return results;
		}

		internal static async Task DeleteFlushesAsync(string userId) {
			await using var connection = new SqliteConnection(_connectionString);
			await connection.OpenAsync();

			var results = new List<string>();
			
			var delete = connection.CreateCommand();
			delete.CommandText =
			@"
			DELETE FROM flushes
			WHERE user_id = $user_id
			";

			delete.Parameters.AddWithValue("$user_id", userId);

			await delete.ExecuteNonQueryAsync();
		}

		// The money total and goal, in cents
		internal static async Task<(long Current, long Goal)> GetMoneyAsync() {
			await using var connection = new SqliteConnection(_connectionString);
			await connection.OpenAsync();

			var select = connection.CreateCommand();
			select.CommandText =
			@"
			SELECT current_cents, goal_cents
			FROM money_goal
			WHERE id = 1
			";

			await using var reader = await select.ExecuteReaderAsync();
			if (!await reader.ReadAsync()) {
				return (0, MoneyGoal.DEFAULT_GOAL_CENTS);
			}
			return (reader.GetInt64(0), reader.GetInt64(1));
		}

		// Adds cents to the money total
		internal static Task AddMoneyAsync(long cents) {
			return UpdateMoneyAsync("current_cents = current_cents + $cents", cents);
		}

		// Sets the money goal, in cents
		internal static Task SetMoneyGoalAsync(long cents) {
			return UpdateMoneyAsync("goal_cents = $cents", cents);
		}

		// Sets the money total to 0
		internal static Task ResetMoneyAsync() {
			return UpdateMoneyAsync("current_cents = $cents", 0);
		}

		// Updates the money row with a SET clause that takes $cents
		static async Task UpdateMoneyAsync(string set, long cents) {
			await using var connection = new SqliteConnection(_connectionString);
			await connection.OpenAsync();

			var update = connection.CreateCommand();
			update.CommandText = $"UPDATE money_goal SET {set} WHERE id = 1";
			update.Parameters.AddWithValue("$cents", cents);

			await update.ExecuteNonQueryAsync();
		}

	}
}
