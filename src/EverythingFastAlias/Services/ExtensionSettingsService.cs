using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using EverythingFastAlias.Config;
using EverythingFastAlias.Models;

namespace EverythingFastAlias.Services
{
    public class ExtensionSettingsService
    {
        private static readonly Lazy<ExtensionSettingsService> _instance = new(() => new ExtensionSettingsService());
        public static ExtensionSettingsService Instance => _instance.Value;

        private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public event Action? ExtensionsChanged;

        private ExtensionSettingsService()
        {
            Reload();
        }

        public void Reload()
        {
            lock (_lock)
            {
                _cache.Clear();
                var db = DatabaseService.Instance;

                foreach (var category in FileExtensionConstants.AllCategories)
                {
                    string settingKey = GetSettingKey(category);
                    string savedValue = db.GetSetting(settingKey, "");

                    if (!string.IsNullOrWhiteSpace(savedValue))
                    {
                        _cache[category] = NormalizeExtensions(savedValue);
                    }
                    else
                    {
                        _cache[category] = FileExtensionConstants.GetDefaultExtensions(category);
                    }
                }
            }
        }

        public string GetExtensions(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return string.Empty;

            lock (_lock)
            {
                if (_cache.TryGetValue(category.Trim(), out var exts))
                {
                    return exts;
                }
                return FileExtensionConstants.GetDefaultExtensions(category.Trim());
            }
        }

        public void SaveExtensions(string category, string rawExtensions)
        {
            if (string.IsNullOrWhiteSpace(category)) return;

            string normalized = NormalizeExtensions(rawExtensions);
            string key = GetSettingKey(category.Trim());

            lock (_lock)
            {
                _cache[category.Trim()] = normalized;
                DatabaseService.Instance.SaveSetting(key, normalized);
            }

            ExtensionsChanged?.Invoke();
        }

        public void SaveAll(IEnumerable<ExtensionCategoryItem> items)
        {
            if (items == null) return;

            var settingsToSave = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            lock (_lock)
            {
                foreach (var item in items)
                {
                    string category = item.CategoryName.Trim();
                    string normalized = NormalizeExtensions(item.Extensions);
                    _cache[category] = normalized;

                    string key = GetSettingKey(category);
                    settingsToSave[key] = normalized;
                }

                DatabaseService.Instance.SaveSettingsBulk(settingsToSave);
            }

            ExtensionsChanged?.Invoke();
        }

        public void ResetCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return;

            string defaultVal = FileExtensionConstants.GetDefaultExtensions(category.Trim());
            string key = GetSettingKey(category.Trim());

            lock (_lock)
            {
                _cache[category.Trim()] = defaultVal;
                DatabaseService.Instance.SaveSetting(key, defaultVal);
            }

            ExtensionsChanged?.Invoke();
        }

        public void ResetAll()
        {
            var settingsToSave = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            lock (_lock)
            {
                foreach (var category in FileExtensionConstants.AllCategories)
                {
                    string defaultVal = FileExtensionConstants.GetDefaultExtensions(category);
                    _cache[category] = defaultVal;

                    string key = GetSettingKey(category);
                    settingsToSave[key] = defaultVal;
                }

                DatabaseService.Instance.SaveSettingsBulk(settingsToSave);
            }

            ExtensionsChanged?.Invoke();
        }

        public static string NormalizeExtensions(string rawExtensions)
        {
            if (string.IsNullOrWhiteSpace(rawExtensions)) return string.Empty;

            var parts = Regex.Split(rawExtensions, @"[\s,;|]+");
            var uniqueExts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var part in parts)
            {
                var trimmed = part.Trim().TrimStart('.');
                if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                {
                    uniqueExts.Add(trimmed);
                }
            }

            return string.Join(";", uniqueExts);
        }

        private static string GetSettingKey(string category) => $"MediaExt_{category.Trim()}";
    }
}
