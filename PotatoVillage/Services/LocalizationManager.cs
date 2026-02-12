using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PotatoVillage.Services
{
    public class LocalizationManager : INotifyPropertyChanged
    {
        private static LocalizationManager? _instance;
        private Dictionary<string, Dictionary<string, string>> _translations = new();
        private string _currentLanguage = "zh";
        private List<string> _availableLanguages = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public static LocalizationManager Instance
        {
            get
            {
                _instance ??= new LocalizationManager();
                return _instance;
            }
        }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value && _availableLanguages.Contains(value))
                {
                    _currentLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<string> AvailableLanguages => _availableLanguages;

        private LocalizationManager()
        {
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            try
            {
                // Load translations from embedded resource
                var assembly = typeof(LocalizationManager).Assembly;
                var resourceName = "PotatoVillage.Resources.Translations.translations.json";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            var json = reader.ReadToEnd();
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            _translations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json, options) ?? new();
                            _availableLanguages = _translations.Keys.ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading translations: {ex.Message}");
                // Fallback to empty dictionary
                _translations = new();
                _availableLanguages = new() { "en" };
            }
        }

        public string GetString(string key, string? defaultValue = null)
        {
            if (_translations.TryGetValue(_currentLanguage, out var languageDict))
            {
                if (languageDict.TryGetValue(key, out var value))
                {
                    return value;
                }
            }

            // Fallback to English
            if (_currentLanguage != "en" && _translations.TryGetValue("en", out var englishDict))
            {
                if (englishDict.TryGetValue(key, out var englishValue))
                {
                    return englishValue;
                }
            }

            return defaultValue ?? key;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
