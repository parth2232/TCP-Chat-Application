using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Windows_Forms_Chat
{
    public class DatabaseAccess
    {
        private string connectionString;

        public DatabaseAccess(string dbPath)
        {
            connectionString = $"Data Source={dbPath};Version=3;";
        }

        public void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Users (
                            ID INTEGER PRIMARY KEY AUTOINCREMENT,
                            Username TEXT UNIQUE NOT NULL,
                            Password TEXT NOT NULL,
                            Wins INTEGER DEFAULT 0,
                            Losses INTEGER DEFAULT 0,
                            Draws INTEGER DEFAULT 0
                        )";
                    command.ExecuteNonQuery();
                }
            }
        }

        public bool AddUser(string username, string password)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = "INSERT INTO Users (Username, Password) VALUES (@username, @password)";
                    command.Parameters.AddWithValue("@username", username);
                    command.Parameters.AddWithValue("@password", password);
                    try
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                    catch (SQLiteException)
                    {
                        return false; // Username already exists
                    }
                }
            }
        }

        public bool ValidateUser(string username, string password)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = @username AND Password = @password";
                    command.Parameters.AddWithValue("@username", username);
                    command.Parameters.AddWithValue("@password", password);
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        public List<(string Username, int Wins, int Losses, int Draws)> GetSortedScores()
        {
            var scores = new List<(string Username, int Wins, int Losses, int Draws)>();

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                    SELECT Username, Wins, Losses, Draws 
                    FROM Users 
                    ORDER BY Wins DESC, Draws DESC, Losses ASC";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            scores.Add((
                                reader.GetString(0),
                                reader.GetInt32(1),
                                reader.GetInt32(2),
                                reader.GetInt32(3)
                            ));
                        }
                    }
                }
            }

            return scores;
        }

        public void UpdatePlayerScore(string username, bool isWin, bool isDraw)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    string columnToUpdate = isWin ? "Wins" : (isDraw ? "Draws" : "Losses");
                    command.CommandText = $"UPDATE Users SET {columnToUpdate} = {columnToUpdate} + 1 WHERE Username = @username";
                    command.Parameters.AddWithValue("@username", username);
                    command.ExecuteNonQuery();
                }
            }

        }


            public bool UpdateUsername(string oldUsername, string newUsername)
            {      
            using (var connection = new SQLiteConnection(connectionString))
               {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = "UPDATE Users SET Username = @newUsername WHERE Username = @oldUsername";
                    command.Parameters.AddWithValue("@newUsername", newUsername);
                    command.Parameters.AddWithValue("@oldUsername", oldUsername);
                    try
                    {
                        int rowsAffected = command.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                    catch (SQLiteException)
                    {
                        return false; // New username already exists
                    }
                }
            }
        }
    }
}
