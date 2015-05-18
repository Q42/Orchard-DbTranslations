using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using ICSharpCode.SharpZipLib.Zip;
using NHibernate;
using Orchard;
using Orchard.Caching;
using Orchard.Data;
using Orchard.Environment;
using Orchard.Environment.Descriptor.Models;
using Orchard.Environment.ShellBuilders;
using Orchard.Localization;
using Orchard.Localization.Services;
using Orchard.Mvc;
using Orchard.UI.Admin.Notification;
using Orchard.UI.Notify;
using Q42.DbTranslations.Models;
using Q42.DbTranslations.ViewModels;
using Orchard.Environment.Configuration;

namespace Q42.DbTranslations.Services {
    public interface ILocalizationService : IDependency {
        CultureDetailsViewModel GetCultureDetailsViewModel(string culture);
        byte[] GetZipBytes(string culture);
        CultureIndexViewModel GetCultures();
        void UpdateTranslation(int id, string culture, string value);
        void RemoveTranslation(int id, string culture);
        IEnumerable<StringEntry> GetTranslationsFromZip(Stream stream);
        void ResetCache();
        IEnumerable<StringEntry> TranslateFile(string path, string content, string culture);
        void SaveStringsToDatabase(IEnumerable<StringEntry> strings, bool overwrite);
        CultureGroupDetailsViewModel GetModules(string culture);
        CultureGroupDetailsViewModel GetTranslations(string culture, string path);
        IEnumerable<StringEntry> GetTranslations(string culture);
        void SavePoFilesToDisk(string culture);
        void SavePoFilesToDisk();
        CultureGroupDetailsViewModel Search(string culture, string querystring);
    }

    public class LocalizationService : ILocalizationService, INotificationProvider {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISessionLocator _sessionLocator;
        private readonly ICultureManager _cultureManager;
        private readonly ISignals _signals;
        private readonly ShellSettings _shellSettings;
        private readonly IRepository<TranslationRecord> _translationRepository;
        private readonly IRepository<LocalizableStringRecord> _localizableStringRepository;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IShellContextFactory _shellContextFactory;
        private readonly ShellDescriptor _shellDescriptor;

        public LocalizationService(IHttpContextAccessor httpContextAccessor,
            ISessionLocator sessionLocator,
            ICultureManager cultureManager,
            ISignals signals,
            ShellSettings shellSettings,
            IRepository<TranslationRecord> translationRepository,
            IRepository<LocalizableStringRecord> localizableStringRepository,
            IHostEnvironment hostEnvironment,
            IShellContextFactory shellContextFactory,
            ShellDescriptor shellDescriptor) {
            _httpContextAccessor = httpContextAccessor;
            _sessionLocator = sessionLocator;
            _shellSettings = shellSettings;
            _signals = signals;
            T = NullLocalizer.Instance;
            _cultureManager = cultureManager;
            _translationRepository = translationRepository;
            _localizableStringRepository = localizableStringRepository;
            _hostEnvironment = hostEnvironment;
            _shellContextFactory = shellContextFactory;
            _shellDescriptor = shellDescriptor;
        }

        public Localizer T { get; set; }

        private string DataTablePrefix() {
            if (string.IsNullOrEmpty(_shellSettings.DataTablePrefix)) return string.Empty;
            return _shellSettings.DataTablePrefix + "_";
        }

        public CultureDetailsViewModel GetCultureDetailsViewModel(string culture) {
            var model = new CultureDetailsViewModel { Culture = culture };
            var query = _localizableStringRepository.Table.AsEnumerable();
            var currentPath = "";
            var group = default(CultureDetailsViewModel.TranslationGroupViewModel);
            foreach (LocalizableStringRecord s in query) {
                if (s.Path != currentPath) {
                    group = new CultureDetailsViewModel.TranslationGroupViewModel {
                        Path = String.Format(s.Path, culture)
                    };
                    if (!group.Path.Contains(culture))
                        throw new Exception("Something went wrong: the culture is not included in the path.");
                    model.Groups.Add(group);
                    currentPath = s.Path;
                }
                if (group != null) {
                    var translation = s.Translations.FirstOrDefault(t => t.Culture == culture);
                    group.Translations.Add(new CultureDetailsViewModel.TranslationViewModel {
                        Context = s.Context,
                        Key = s.StringKey,
                        OriginalString = s.OriginalLanguageString,
                        LocalString = translation != null ? translation.Value : null
                    });
                }
            }

            return model;
        }



        public CultureGroupDetailsViewModel GetModules(string culture) {
            var model = new CultureGroupDetailsViewModel { Culture = culture };
            var session = _sessionLocator.For(typeof(LocalizableStringRecord));

            var paths = session.CreateSQLQuery(
                string.Format(@"  SELECT Localizable.Path,
                    COUNT(Localizable.Id) AS TotalCount,
                    COUNT(Translation.Id) AS TranslatedCount
                FROM {0}Q42_DbTranslations_LocalizableStringRecord AS Localizable
                LEFT OUTER JOIN {0}Q42_DbTranslations_TranslationRecord AS Translation
                    ON Localizable.Id = Translation.LocalizableStringRecord_id
                    AND Translation.Culture = :culture
                GROUP BY Localizable.Path
                ORDER BY Localizable.Path", DataTablePrefix()))
            .AddScalar("Path", NHibernateUtil.String)
            .AddScalar("TotalCount", NHibernateUtil.Int32)
            .AddScalar("TranslatedCount", NHibernateUtil.Int32)
            .SetParameter("culture", culture);
            model.Groups = paths.List<object[]>()
                .Select(t => new CultureGroupDetailsViewModel.TranslationGroup {
                    Path = (string)t[0],
                    TotalCount = (int)t[1],
                    TranslationCount = (int)t[2],
                }).ToList();
            return model;
        }

        public CultureGroupDetailsViewModel GetTranslations(string culture, string path) {
            var model = new CultureGroupDetailsViewModel { Culture = culture };
            var session = _sessionLocator.For(typeof(LocalizableStringRecord));
            // haalt alle mogelijke strings en description en hun vertaling in culture op
            var paths = session.CreateSQLQuery(
                    string.Format(@"  SELECT Localizable.Id,
                    Localizable.StringKey,
                    Localizable.Context,
                    Localizable.OriginalLanguageString,
                    Translation.Value
                FROM {0}Q42_DbTranslations_LocalizableStringRecord AS Localizable
                LEFT OUTER JOIN {0}Q42_DbTranslations_TranslationRecord AS Translation
                    ON Localizable.Id = Translation.LocalizableStringRecord_id
                    AND Translation.Culture = :culture
                WHERE Localizable.Path = :path", DataTablePrefix()))
                .AddScalar("Id", NHibernateUtil.Int32)
                .AddScalar("StringKey", NHibernateUtil.String)
                .AddScalar("Context", NHibernateUtil.String)
                .AddScalar("OriginalLanguageString", NHibernateUtil.String)
                .AddScalar("Value", NHibernateUtil.String)
                .SetParameter("culture", culture)
                .SetParameter("path", path);

            model.CurrentGroupTranslations = paths.List<object[]>()
                .Select(t => new CultureGroupDetailsViewModel.TranslationViewModel {
                    Id = (int)t[0],
                    GroupPath = path,
                    Key = (string)t[1],
                    Context = (string)t[2],
                    OriginalString = (string)t[3],
                    LocalString = (string)t[4]
                })
                .ToList()
                .GroupBy(t => t.Context);

            return model;
        }

        public CultureGroupDetailsViewModel Search(string culture, string querystring) {
            var model = new CultureGroupDetailsViewModel { Culture = culture };
            var session = _sessionLocator.For(typeof(LocalizableStringRecord));
            // haalt alle mogelijke strings en description en hun vertaling in culture op
            var paths = session.CreateSQLQuery(
                string.Format(@"  SELECT Localizable.Id,
                    Localizable.StringKey,
                    Localizable.Context,
                    Localizable.OriginalLanguageString,
                    Translation.Value,
                    Localizable.Path
                FROM {0}Q42_DbTranslations_LocalizableStringRecord AS Localizable
                LEFT OUTER JOIN {0}Q42_DbTranslations_TranslationRecord AS Translation
                    ON Localizable.Id = Translation.LocalizableStringRecord_id
                    AND Translation.Culture = :culture
                WHERE Localizable.OriginalLanguageString LIKE :query 
                    OR Translation.Value LIKE :query", DataTablePrefix()))
                .AddScalar("Id", NHibernateUtil.Int32)
                .AddScalar("StringKey", NHibernateUtil.String)
                .AddScalar("Context", NHibernateUtil.String)
                .AddScalar("OriginalLanguageString", NHibernateUtil.String)
                .AddScalar("Value", NHibernateUtil.String)
                .AddScalar("Path", NHibernateUtil.String)
                .SetParameter("culture", culture)
                .SetParameter("query", "%" + querystring + "%");
            model.CurrentGroupTranslations = paths.List<object[]>()
                .Select(t => new CultureGroupDetailsViewModel.TranslationViewModel {
                    Id = (int)t[0],
                    GroupPath = (string)t[5],
                    Key = (string)t[1],
                    Context = (string)t[2],
                    OriginalString = (string)t[3],
                    LocalString = (string)t[4]
                }).ToList().GroupBy(t => t.Context);
            return model;
        }

        public IEnumerable<StringEntry> GetTranslations(string culture) {
            var shellContext = _shellContextFactory.CreateDescribedContext(_shellSettings, _shellDescriptor);
            using (shellContext.LifetimeScope) {
                using (var standaloneEnvironment = shellContext.LifetimeScope.CreateWorkContextScope()) {
                    var session = standaloneEnvironment.Resolve<ISessionLocator>().For(typeof(LocalizableStringRecord));

                    return session.CreateSQLQuery(
                      string.Format(@"SELECT 
                                  Localizable.StringKey,
                                  Localizable.Context,
                                  Translation.Value
                              FROM {0}Q42_DbTranslations_LocalizableStringRecord AS Localizable
                              INNER JOIN {0}Q42_DbTranslations_TranslationRecord AS Translation
                                  ON Localizable.Id = Translation.LocalizableStringRecord_id
                                  AND Translation.Culture = :culture", DataTablePrefix()))
                      .AddScalar("StringKey", NHibernateUtil.String)
                      .AddScalar("Context", NHibernateUtil.String)
                      .AddScalar("Value", NHibernateUtil.String)
                      .SetParameter("culture", culture)
                    .List<object[]>()
                    .Select(t => new StringEntry {
                        Key = (string)t[0],
                        Context = (string)t[1],
                        Translation = (string)t[2]
                    })
                    .ToList();
                }
            }
        }


        public IEnumerable<StringEntry> TranslateFile(string path, string content, string culture) {
            string currentContext = null;
            string currentOriginal = null;
            using (var textStream = new StringReader(content)) {
                string line;
                while ((line = textStream.ReadLine()) != null) {
                    if (line.StartsWith("msgctx ")) {
                        currentContext = line.Substring(8);
                    }
                    if (line.StartsWith("msgid \"")) {
                        currentOriginal = ImportPoText(line.Substring(7, line.Length - 8));
                    }
                    
                    if (line.StartsWith("msgstr \"")) {
                        var translation = ImportPoText(line.Substring(8, line.Length - 9));
                        if (!string.IsNullOrEmpty(translation)) {
                            yield return new StringEntry {
                                Context = currentContext == null ? null : currentContext.Trim('"'),
                                Path = path,
                                Culture = culture,
                                Key = currentOriginal,
                                English = currentOriginal,
                                Translation = translation
                            };
                            currentOriginal = null;
                            currentContext = null;
                        }
                    }
                }
            }
        }

        public void SaveStringsToDatabase(IEnumerable<StringEntry> strings, bool overwrite) {
            foreach (var s in strings) {
                SaveStringToDatabase(s, overwrite);
            }
        }

        /// <summary>
        /// Saves to database. If culture is present, Translation entry is saved
        /// </summary>
        /// <param name="session"></param>
        /// <param name="input"></param>
        private void SaveStringToDatabase(StringEntry input, bool overwrite) {
            var translatableString = _localizableStringRepository
                .Table
                .FirstOrDefault(s => s.StringKey == input.Key && s.Context == input.Context);

            if (translatableString == null) {
                string path = input.Path;
                if (!path.Contains("{0}") && !string.IsNullOrEmpty(input.Culture))
                    path = path.Replace(input.Culture, "{0}");
                translatableString = new LocalizableStringRecord {
                    Path = path,
                    Context = input.Context,
                    StringKey = input.Key,
                    OriginalLanguageString = input.English
                };

                _localizableStringRepository.Create(translatableString);
            }
            else if (translatableString.OriginalLanguageString != input.English) {
                translatableString.OriginalLanguageString = input.English;
                _localizableStringRepository.Update(translatableString);
            }

            if (!string.IsNullOrEmpty(input.Culture) && !string.IsNullOrEmpty(input.Translation)) {
                var translation =
                    (from t in translatableString.Translations
                     where t.Culture.Equals(input.Culture)
                     select t).FirstOrDefault();

                if (translation == null) {
                    translation = new TranslationRecord {
                        Culture = input.Culture,
                        Value = input.Translation
                    };
                    _translationRepository.Create(translation);

                    translatableString.AddTranslation(translation);
                }
                else if (overwrite) {
                    translation.Value = input.Translation;
                }
                _localizableStringRepository.Update(translatableString);

            }

            SetCacheInvalid();
        }

        public byte[] GetZipBytes(string culture) {
            var model = GetCultureDetailsViewModel(culture);

            if (model.Groups.Count == 0)
                return null;

            using (var stream = new MemoryStream()) {
                using (var zip = new ZipOutputStream(stream)) {
                    using (var writer = new StreamWriter(zip, Encoding.UTF8)) {
                        foreach (var translationGroup in model.Groups) {
                            var file = new ZipEntry(translationGroup.Path) { DateTime = DateTime.Now };
                            zip.PutNextEntry(file);
                            writer.WriteLine(@"# Orchard resource strings - {0}
# Copyright (c) 2010 Outercurve Foundation
# All rights reserved
# This file is distributed under the BSD license
# This file is generated using the Q42.DbTranslations module
", culture);
                            foreach (var translation in translationGroup.Translations) {
                                writer.WriteLine("#: " + ExportPoText(translation.Context));
                                writer.WriteLine("#| msgid \"" + ExportPoText(translation.Key) + "\"");
                                writer.WriteLine("msgctx \"" + ExportPoText(translation.Context) + "\"");
                                writer.WriteLine("msgid \"" + ExportPoText(translation.OriginalString) + "\"");
                                writer.WriteLine("msgstr \"" + ExportPoText(translation.LocalString) + "\"");
                                writer.WriteLine();
                            }
                            writer.Flush();
                        }
                    }
                    zip.IsStreamOwner = false;
                    zip.Close();
                }
                return stream.ToArray();
            }
        }

        public static string ExportPoText(string input) {
            if (input == null) return null;
            return input.Replace("\"", "\\\"");
        }

        public static string ImportPoText(string input) {
            return input.Replace("\\\"", "\"");
        }

        public void SavePoFilesToDisk() {
            foreach (var culture in _cultureManager.ListCultures())
                SavePoFilesToDisk(culture);
        }

        public void SavePoFilesToDisk(string culture) {
            var model = GetCultureDetailsViewModel(culture);

            if (model.Groups.Count == 0)
                return;

            foreach (var translationGroup in model.Groups) {
                string path = Path.Combine(_hostEnvironment.MapPath("~"), translationGroup.Path);
                var file = new FileInfo(path);

                // delete the file if it already exists
                if (file.Exists)
                    file.Delete();

                // create directory if it doesn't exist
                else if (!file.Directory.Exists)
                    file.Directory.Create();

                using (var writer = File.CreateText(path)) {
                    writer.WriteLine(@"# Orchard resource strings - {0}
# Copyright (c) 2010 Outercurve Foundation
# All rights reserved
# This file is distributed under the BSD license
# This file is generated using the Q42.DbTranslations module
", culture);
                    foreach (var translation in translationGroup.Translations) {
                        writer.WriteLine("#: " + ExportPoText(translation.Context));
                        writer.WriteLine("#| msgid \"" + ExportPoText(translation.Key) + "\"");
                        writer.WriteLine("msgctx \"" + ExportPoText(translation.Context) + "\"");
                        writer.WriteLine("msgid \"" + ExportPoText(translation.OriginalString) + "\"");
                        writer.WriteLine("msgstr \"" + ExportPoText(translation.LocalString) + "\"");
                        writer.WriteLine();
                    }
                    writer.Flush();
                }
            }
        }

        public CultureIndexViewModel GetCultures() {
            var model = new CultureIndexViewModel {
                // Get Default number of translations
                NumberOfStringsInDefaultCulture = _localizableStringRepository.Table.Count()
            };

            foreach (var culture in _cultureManager.ListCultures()) {
                var localCulture = culture;
                var cultureTranslationCount = _translationRepository.Count(c => c.Culture == localCulture);

                model.TranslationStates.Add(
                    culture,
                    new CultureIndexViewModel.CultureTranslationState {
                        NumberOfTranslatedStrings = cultureTranslationCount
                    }
                );
            }

            return model;
        }

        public void UpdateTranslation(int id, string culture, string value) {
            var localizable = _localizableStringRepository.Get(id);
            var translation = localizable.Translations.FirstOrDefault(t => t.Culture == culture);

            if (translation == null) {
                if (!String.IsNullOrEmpty(value)) {
                    var newTranslation = new TranslationRecord {
                        Culture = culture,
                        Value = value,
                        LocalizableStringRecord = localizable
                    };
                    localizable.Translations.Add(newTranslation);

                    _translationRepository.Create(newTranslation);
                    SetCacheInvalid();
                }
            }
            else if (String.IsNullOrEmpty(value)) {
                _translationRepository.Delete(translation);
                SetCacheInvalid();
            }
            else if (String.CompareOrdinal(translation.Value, value) != 0) {
                translation.Value = value;
                _translationRepository.Update(translation);
                SetCacheInvalid();
            }
        }

        public void RemoveTranslation(int id, string culture) {
            var translation = _localizableStringRepository.Get(id)
                .Translations.FirstOrDefault(t => t.Culture == culture);

            if (translation != null) {
                translation.LocalizableStringRecord.Translations.Remove(translation);
                _translationRepository.Delete(translation);
                SetCacheInvalid();
            }
        }

        public IEnumerable<StringEntry> GetTranslationsFromZip(Stream stream) {
            var zip = new ZipInputStream(stream);
            ZipEntry zipEntry;
            while ((zipEntry = zip.GetNextEntry()) != null) {
                if (zipEntry.IsFile) {
                    var entrySize = (int)zipEntry.Size;
                    // Yeah yeah, but only a handful of people have upload rights here for the moment.
                    var entryBytes = new byte[entrySize];
                    zip.Read(entryBytes, 0, entrySize);
                    var content = entryBytes.ToStringUsingEncoding();
                    var cultureName = Path.GetFileName(Path.GetDirectoryName(zipEntry.Name));
                    var culture = cultureName;
                    foreach (var se in TranslateFile(zipEntry.Name, content, culture))
                        yield return se;
                }
            }
        }

        public IEnumerable<NotifyEntry> GetNotifications() {
            if (!IsCacheValid()) {
                var request = _httpContextAccessor.Current().Request;
                var urlHelper = new UrlHelper(request.RequestContext);

                yield return new NotifyEntry {
                    Message = T("Translation cache needs to be flushed. <a href=\"{0}\">Click here to flush!</a>", urlHelper.Action("FlushCache", "Admin", new { area = "Q42.DbTranslations", redirectUrl = request.Url.PathAndQuery })),
                    Type = NotifyType.Warning
                };
            }
        }

        public void ResetCache() {
            if (!IsCacheValid()) {
                _signals.Trigger("culturesChanged");
                _httpContextAccessor.Current().Application.Remove("q42TranslationsDirty");
            }
        }

        private void SetCacheInvalid() {
            _httpContextAccessor.Current().Application["q42TranslationsDirty"] = true;
        }

        private bool IsCacheValid() {
            return !_httpContextAccessor.Current().Application.AllKeys.Contains("q42TranslationsDirty");
        }
    }
}