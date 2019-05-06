using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Orchard.Caching;
using Orchard.Environment.Extensions;
using Orchard.Localization.Services;

namespace Q42.DbTranslations.Services {
    [OrchardSuppressDependency("Orchard.Localization.Services.DefaultLocalizedStringManager")]
    public class LiveLocalizedStringManager : ILocalizedStringManager {
        private readonly ICacheManager _cacheManager;
        private readonly ISignals _signals;
        private readonly ILocalizationService _localizationService;

        public LiveLocalizedStringManager(
            ICacheManager cacheManager,
            ISignals signals,
            ILocalizationService localizationService) {
            _localizationService = localizationService;
            _cacheManager = cacheManager;
            _signals = signals;
        }

        private readonly ConcurrentDictionary<string, CultureDictionary> _cultureValue = 
            new ConcurrentDictionary<string, CultureDictionary>();

        // This will translate a string into a string in the target cultureName.
        // The scope portion is optional, it amounts to the location of the file containing 
        // the string in case it lives in a view, or the namespace name if the string lives in a binary.
        // If the culture doesn't have a translation for the string, it will fallback to the 
        // parent culture as defined in the .net culture hierarchy. e.g. fr-FR will fallback to fr.
        // In case it's not found anywhere, the text is returned as is.
        public string GetLocalizedString(string scope, string text, string cultureName) {
            var culture = _cultureValue.GetOrAdd(cultureName, LoadCulture);

            string scopedKey = (scope + "|" + text).ToLowerInvariant();
            if (culture.Translations.ContainsKey(scopedKey)) {
                return culture.Translations[scopedKey];
            }

            string genericKey = ("|" + text).ToLowerInvariant();
            if (culture.Translations.ContainsKey(genericKey)) {
                return culture.Translations[genericKey];
            }

            return GetParentTranslation(scope, text, cultureName);
        }

        private string GetParentTranslation(string scope, string text, string cultureName) {
            string scopedKey = (scope + "|" + text).ToLowerInvariant();
            string genericKey = ("|" + text).ToLowerInvariant();
            try {
                CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo parentCultureInfo = cultureInfo.Parent;
                if (parentCultureInfo.IsNeutralCulture) {
                    var culture = _cultureValue.GetOrAdd(parentCultureInfo.Name, LoadCulture);
                    if (culture.Translations.ContainsKey(scopedKey)) {
                        return culture.Translations[scopedKey];
                    }
                    if (culture.Translations.ContainsKey(genericKey)) {
                        return culture.Translations[genericKey];
                    }
                    return text;
                }
            }
            catch (CultureNotFoundException) { }

            return text;
        }

        // Loads the culture dictionary in memory and caches it.
        // Cache entry will be invalidated any time the directories hosting 
        // the .po files are modified.
        private CultureDictionary LoadCulture(string culture) {
            return _cacheManager.Get(culture, ctx => {
                ctx.Monitor(_signals.When("culturesChanged"));
                return new CultureDictionary {
                    CultureName = culture,
                    Translations = LoadTranslationsForCulture(culture)
                };
            });
        }

        private IDictionary<string, string> LoadTranslationsForCulture(string culture) {
            return _localizationService
                .GetTranslations(culture)
                .GroupBy(x => string.Format("{0}|{1}", x.Context, x.Key), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    k => k.Key.ToLowerInvariant(), 
                    t => t.First().Translation);
        }

        class CultureDictionary {
            public string CultureName { get; set; }
            public IDictionary<string, string> Translations { get; set; }
        }
    }
}
