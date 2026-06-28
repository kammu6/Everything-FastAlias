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

        private Dictionary<string, HashSet<string>> _aliasGroups = new(StringComparer.OrdinalIgnoreCase);
        private List<string> _sortedAliasKeys = new();
        private readonly object _aliasLock = new();

        private DatabaseService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(baseDir, "fastalias.db");
            _connectionString = $"Data Source={_dbPath};Default Timeout=5";
            InitializeDatabase();
            LoadMappingsToCache();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // WAL 모드 활성화로 동시 읽기/쓰기 성능 향상 및 락 예외 방지
            using (var commandWAL = new SqliteCommand("PRAGMA journal_mode=WAL;", connection))
            {
                commandWAL.ExecuteNonQuery();
            }

            var createTableQuery = @"
                CREATE TABLE IF NOT EXISTS AliasMappings (
                    Keyword TEXT PRIMARY KEY COLLATE NOCASE,
                    Words TEXT NOT NULL
                );";

            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();

            var createSettingsTableQuery = @"
                CREATE TABLE IF NOT EXISTS AppSettings (
                    SettingKey TEXT PRIMARY KEY COLLATE NOCASE,
                    SettingValue TEXT
                );";

            using var commandSettings = new SqliteCommand(createSettingsTableQuery, connection);
            commandSettings.ExecuteNonQuery();
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

            // 동의어 대그룹 확장 맵 캐시 빌드 (1회 수행으로 속도 극대화)
            BuildAliasGroupsCache();
        }

        public (Dictionary<string, HashSet<string>> Groups, List<string> SortedKeys) GetAliasGroupsCache()
        {
            lock (_aliasLock)
            {
                return (_aliasGroups, _sortedAliasKeys);
            }
        }

        private void BuildAliasGroupsCache()
        {
            lock (_aliasLock)
            {
                var newGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                // _cache 복사본 획득
                Dictionary<string, List<string>> tempCache;
                lock (_cacheLock)
                {
                    tempCache = new Dictionary<string, List<string>>(_cache, StringComparer.OrdinalIgnoreCase);
                }

                foreach (var kvp in tempCache)
                {
                    var keyword = kvp.Key.Trim();
                    if (string.IsNullOrEmpty(keyword)) continue;

                    var rowElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };
                    if (kvp.Value != null)
                    {
                        foreach (var syn in kvp.Value)
                        {
                            var trimmedSyn = syn.Trim().TrimEnd(';');
                            if (!string.IsNullOrEmpty(trimmedSyn))
                            {
                                rowElements.Add(trimmedSyn);
                            }
                        }
                    }

                    foreach (var member in rowElements)
                    {
                        if (!newGroups.TryGetValue(member, out var existingGroup))
                        {
                            existingGroup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            newGroups[member] = existingGroup;
                        }

                        foreach (var m in rowElements)
                        {
                            existingGroup.Add(m);
                        }
                    }
                }

                _aliasGroups = newGroups;

                // 고유 키들을 글자 수 역순으로 정렬
                var keys = new List<string>(_aliasGroups.Keys);
                keys.Sort((a, b) => b.Length.CompareTo(a.Length));
                _sortedAliasKeys = keys;
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

        public void SaveAllSync(IEnumerable<(string Keyword, string Words)> mappings)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var deleteQuery = "DELETE FROM AliasMappings";
                using var deleteCmd = new SqliteCommand(deleteQuery, connection, transaction);
                deleteCmd.ExecuteNonQuery();

                var insertQuery = @"
                    INSERT INTO AliasMappings (Keyword, Words) 
                    VALUES ($keyword, $words);";

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

        public void ClearAllMappings()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var deleteQuery = "DELETE FROM AliasMappings";
            using var command = new SqliteCommand(deleteQuery, connection);
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

        public string GetSetting(string key, string defaultValue = "")
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var selectQuery = "SELECT SettingValue FROM AppSettings WHERE SettingKey = $key";
                using var command = new SqliteCommand(selectQuery, connection);
                command.Parameters.AddWithValue("$key", key);
                var val = command.ExecuteScalar();
                return val != null ? val.ToString() ?? defaultValue : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public void SaveSetting(string key, string value)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var insertQuery = @"
                    INSERT INTO AppSettings (SettingKey, SettingValue) 
                    VALUES ($key, $value)
                    ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue;";

                using var command = new SqliteCommand(insertQuery, connection);
                command.Parameters.AddWithValue("$key", key);
                command.Parameters.AddWithValue("$value", value);
                command.ExecuteNonQuery();
            }
            catch
            {
                // 설정 저장 중 예외는 조용히 무시하여 크래시 방지
            }
        }

        public void SaveSettingsBulk(Dictionary<string, string> settings)
        {
            if (settings == null || settings.Count == 0) return;
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var transaction = connection.BeginTransaction();
                try
                {
                    var insertQuery = @"
                        INSERT INTO AppSettings (SettingKey, SettingValue) 
                        VALUES ($key, $value)
                        ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue;";

                    using var command = new SqliteCommand(insertQuery, connection, transaction);
                    var keyParam = command.Parameters.Add("$key", SqliteType.Text);
                    var valueParam = command.Parameters.Add("$value", SqliteType.Text);

                    foreach (var kvp in settings)
                    {
                        keyParam.Value = kvp.Key;
                        valueParam.Value = kvp.Value ?? "";
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 일괄 저장 실패: {ex.Message}");
            }
        }
    }
}
