using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace EverythingFastAlias.Services
{
    public class DatabaseService
    {
        private static readonly Lazy<DatabaseService> _instance = new(() => new DatabaseService());
        public static DatabaseService Instance => _instance.Value;

        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly Dictionary<string, List<string>> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _cacheLock = new();

        private DatabaseService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(baseDir, "fastalias.db");
            _connectionString = $"Data Source={_dbPath}";
            InitializeDatabase();
            LoadMappingsToCache();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createTableQuery = @"
                CREATE TABLE IF NOT EXISTS AliasMappings (
                    Keyword TEXT PRIMARY KEY,
                    Words TEXT NOT NULL
                );";

            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        public void LoadMappingsToCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var selectQuery = "SELECT Keyword, Words FROM AliasMappings";
                using var command = new SqliteCommand(selectQuery, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var keyword = reader.GetString(0);
                    var wordsStr = reader.GetString(1);
                    var words = ParseWords(wordsStr);
                    _cache[keyword] = words;
                }
            }
        }

        public Dictionary<string, List<string>> GetCacheSnapshot()
        {
            lock (_cacheLock)
            {
                return new Dictionary<string, List<string>>(_cache, StringComparer.OrdinalIgnoreCase);
            }
        }

        public List<string>? GetWordsFromCache(string keyword)
        {
            lock (_cacheLock)
            {
                return _cache.TryGetValue(keyword, out var words) ? words : null;
            }
        }

        public Dictionary<string, string> GetAllMappings()
        {
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var selectQuery = "SELECT Keyword, Words FROM AliasMappings ORDER BY Keyword ASC";
            using var command = new SqliteCommand(selectQuery, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                mappings[reader.GetString(0)] = reader.GetString(1);
            }

            return mappings;
        }

        public void SaveBulk(IEnumerable<(string Keyword, string Words)> mappings)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var insertQuery = @"
                    INSERT INTO AliasMappings (Keyword, Words) 
                    VALUES ($keyword, $words)
                    ON CONFLICT(Keyword) DO UPDATE SET Words = excluded.Words;";

                using var command = new SqliteCommand(insertQuery, connection, transaction);
                var keywordParam = command.Parameters.Add("$keyword", SqliteType.Text);
                var wordsParam = command.Parameters.Add("$words", SqliteType.Text);

                foreach (var mapping in mappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.Keyword)) continue;
                    
                    keywordParam.Value = mapping.Keyword.Trim();
                    wordsParam.Value = mapping.Words.Trim();
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            LoadMappingsToCache();
        }

        public void SaveMapping(string keyword, string words)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var insertQuery = @"
                INSERT INTO AliasMappings (Keyword, Words) 
                VALUES ($keyword, $words)
                ON CONFLICT(Keyword) DO UPDATE SET Words = excluded.Words;";

            using var command = new SqliteCommand(insertQuery, connection);
            command.Parameters.AddWithValue("$keyword", keyword.Trim());
            command.Parameters.AddWithValue("$words", words.Trim());
            command.ExecuteNonQuery();

            LoadMappingsToCache();
        }

        public void DeleteMapping(string keyword)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var deleteQuery = "DELETE FROM AliasMappings WHERE Keyword = $keyword";
            using var command = new SqliteCommand(deleteQuery, connection);
            command.Parameters.AddWithValue("$keyword", keyword.Trim());
            command.ExecuteNonQuery();

            LoadMappingsToCache();
        }

        public static List<string> ParseWords(string wordsStr)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(wordsStr)) return list;

            var splitChars = new[] { ',', ';', '|' };
            var tokens = wordsStr.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);

            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !list.Contains(trimmed))
                {
                    list.Add(trimmed);
                }
            }
            return list;
        }
    }
}
