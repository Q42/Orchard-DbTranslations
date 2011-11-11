using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using ICSharpCode.SharpZipLib.Zip;
using NHibernate;
using NHibernate.Linq;
using Orchard;
using Orchard.ContentManagement;
using Orchard.Data;
using Orchard.Roles.Models;
using Q42.DbTranslations.Models;
using Q42.DbTranslations.ViewModels;
using System.Collections.Generic;
using Orchard.Localization.Services;
using Orchard.UI.Admin.Notification;
using Orchard.UI.Notify;
using System.Web.Mvc;
using Orchard.Localization;
using Orchard.Caching;

namespace Q42.DbTranslations.Services
{
  public interface ILocalizationService : IDependency
  {
    CultureDetailsViewModel GetCultureDetailsViewModel(string culture);
    byte[] GetZipBytes(string culture);
    CultureIndexViewModel GetCultures();
    void UpdateTranslation(int id, string culture, string value);
    void RemoveTranslation(int id, string culture);
    IEnumerable<StringEntry> GetTranslationsFromZip(Stream stream);
    bool IsCultureAllowed(string culture);
    void ResetCache();
    IEnumerable<StringEntry> TranslateFile(string path, string content, string culture);
    void SaveStringsToDatabase(IEnumerable<StringEntry> strings);
    CultureGroupDetailsViewModel GetModules(string culture);
    CultureGroupDetailsViewModel GetTranslations(string culture, string path);
    IEnumerable<StringEntry> GetTranslations(string culture);
  }

  public class LocalizationService : ILocalizationService, INotificationProvider
  {
    private readonly ISessionLocator _sessionLocator;
    private readonly IWorkContextAccessor _wca;
    private readonly ICultureManager _cultureManager;
    public Localizer T { get; set; }
    private readonly ICacheManager _cacheManager;
    private readonly ISignals _signals;

    public LocalizationService(ISessionLocator sessionLocator, IWorkContextAccessor wca, ICultureManager cultureManager,
      ICacheManager cacheManager, ISignals signals)
    {
      _signals = signals;
      T = NullLocalizer.Instance;
      _sessionLocator = sessionLocator;
      _wca = wca;
      _cultureManager = cultureManager;
      _cacheManager = cacheManager;
    }

    public CultureDetailsViewModel GetCultureDetailsViewModel(string culture)
    {
      var model = new CultureDetailsViewModel { Culture = culture };
      using (var session = _sessionLocator.For(typeof(LocalizableStringRecord)))
      {
        var query = session.CreateQuery(@"
                    select s
                    from Q42.DbTranslations.Models.LocalizableStringRecord as s fetch all properties
                    order by s.Path");
        var currentPath = "";
        var group = default(CultureDetailsViewModel.TranslationGroupViewModel);
        foreach (LocalizableStringRecord s in query.Enumerable())
        {
          if (s.Path != currentPath)
          {
            group = new CultureDetailsViewModel.TranslationGroupViewModel
            {
              Path = String.Format(s.Path, culture)
            };
            model.Groups.Add(group);
            currentPath = s.Path;
          }
          var translation = s.Translations
              .Where(t => t.Culture == culture)
              .FirstOrDefault();
          if (group != null &&
              ((s.Translations.Count(t => t.Culture.Equals("en-US", StringComparison.OrdinalIgnoreCase)) > 0) ||
               (translation != null && !String.IsNullOrEmpty(translation.Value))))
          {
            group.Translations.Add(new CultureDetailsViewModel.TranslationViewModel
            {
              Context = s.Context,
              Key = s.StringKey,
              OriginalString = s.OriginalLanguageString,
              LocalString = translation != null ? translation.Value : null
            });
          }
        }
      }
      return model;
    }



    public CultureGroupDetailsViewModel GetModules(string culture)
    {
      var model = new CultureGroupDetailsViewModel { Culture = culture };
      using (var session = _sessionLocator.For(typeof(LocalizableStringRecord)))
      {
        // haal alle mogelijke strings, en hun vertaling in deze culture uit db
        var paths = session.CreateSQLQuery(
                @"  SELECT Localizable.Path,
                        COUNT(Localizable.Id) AS TotalCount,
                        COUNT(Translation.Id) AS TranslatedCount
                    FROM Q42_DbTranslations_LocalizableStringRecord AS Localizable
                    LEFT OUTER JOIN Q42_DbTranslations_TranslationRecord AS Translation
                        ON Localizable.Id = Translation.LocalizableStringRecord_id
                        AND Translation.Culture = :culture
                    GROUP BY Localizable.Path
                    ORDER BY Localizable.Path")
            .AddScalar("Path", NHibernateUtil.String)
            .AddScalar("TotalCount", NHibernateUtil.Int32)
            .AddScalar("TranslatedCount", NHibernateUtil.Int32)
            .SetParameter("culture", culture);
        model.Groups = paths.List<object[]>()
            .Select(t => new CultureGroupDetailsViewModel.TranslationGroup
            {
              Path = (string)t[0],
              TotalCount = (int)t[1],
              TranslationCount = (int)t[2],
            }).ToList();
      }
      return model;
    }

    public CultureGroupDetailsViewModel GetTranslations(string culture, string path)
    {
      var model = new CultureGroupDetailsViewModel { Culture = culture };
      using (var session = _sessionLocator.For(typeof(LocalizableStringRecord)))
      {
        // haalt alle mogelijke strings en description en hun vertaling in culture op
        var paths = session.CreateSQLQuery(
          @"  SELECT Localizable.Id,
              Localizable.StringKey,
              Localizable.Context,
              Localizable.OriginalLanguageString,
              Translation.Value
          FROM Q42_DbTranslations_LocalizableStringRecord AS Localizable
          LEFT OUTER JOIN Q42_DbTranslations_TranslationRecord AS Translation
              ON Localizable.Id = Translation.LocalizableStringRecord_id
              AND Translation.Culture = :culture
          WHERE Localizable.Path = :path")
          .AddScalar("Id", NHibernateUtil.Int32)
          .AddScalar("StringKey", NHibernateUtil.String)
          .AddScalar("Context", NHibernateUtil.String)
          .AddScalar("OriginalLanguageString", NHibernateUtil.String)
          .AddScalar("Value", NHibernateUtil.String)
          .SetParameter("culture", culture)
          .SetParameter("path", path);
        model.CurrentGroupTranslations = paths.List<object[]>()
            .Select(t => new CultureGroupDetailsViewModel.TranslationViewModel
            {
              Id = (int)t[0],
              GroupPath = path,
              Key = (string)t[1],
              Context = (string)t[2],
              OriginalString = (string)t[3],
              LocalString = (string)t[4]
            }).ToList().GroupBy(t => t.Context);
      }
      return model;
    }

    public IEnumerable<StringEntry> GetTranslations(string culture)
    {
      var wc = _wca.GetContext();
      {
        var _sessionLocator = wc.Resolve<ISessionLocator>();
        using (var session = _sessionLocator.For(typeof(LocalizableStringRecord)))
        {
          // haalt alle mogelijke strings en description en hun vertaling in culture op
          var paths = session.CreateSQLQuery(
            @"  SELECT 
              Localizable.StringKey,
              Localizable.Context,
              Translation.Value
          FROM Q42_DbTranslations_LocalizableStringRecord AS Localizable
          INNER JOIN Q42_DbTranslations_TranslationRecord AS Translation
              ON Localizable.Id = Translation.LocalizableStringRecord_id
              AND Translation.Culture = :culture")
            .AddScalar("StringKey", NHibernateUtil.String)
            .AddScalar("Context", NHibernateUtil.String)
            .AddScalar("Value", NHibernateUtil.String)
            .SetParameter("culture", culture);
          return paths.List<object[]>()
              .Select(t => new StringEntry
              {
                Key = (string)t[0],
                Context = (string)t[1],
                Translation = (string)t[2]
              }).ToList();
        }
      }
    }


    public IEnumerable<StringEntry> TranslateFile(string path, string content, string culture)
    {
      string currentContext = null;
      string currentOriginal = null;
      string currentId = null;
      using (var textStream = new StringReader(content))
      {
        string line;
        while ((line = textStream.ReadLine()) != null)
        {
          if (line.StartsWith("#: "))
          {
            currentContext = line.Substring(3);
          }
          if (line.StartsWith("msgctxt "))
          {
            currentContext = line.Substring(8);
          }
          else if (line.StartsWith("#| msgid \""))
          {
            currentId = line.Substring(10, line.Length - 11);
          }
          else if (line.StartsWith("msgid \""))
          {
            currentOriginal = line.Substring(7, line.Length - 8);
          }
          else if (line.StartsWith("msgstr \""))
          {
            var id = currentId;
            var context = currentContext;
            var translation = line.Substring(8, line.Length - 9);
            if (!string.IsNullOrEmpty(translation))
            {
              yield return new StringEntry
              {
                Context = context,
                Path = path,
                Culture = culture,
                Key = currentId,
                English = currentOriginal,
                Translation = translation
              };
            }
          }
        }
      }
    }

    public void SaveStringsToDatabase(IEnumerable<StringEntry> strings)
    {
      using (var session = _sessionLocator.For(typeof(LocalizableStringRecord)))
      {
        foreach (var s in strings)
        {
          SaveStringToDatabase(session, s);
        }
      }
      // todo: delete where < datetime.now
    }

    /// <summary>
    /// Saves to database. If culture is present, Translation entry is saved
    /// </summary>
    /// <param name="session"></param>
    /// <param name="input"></param>
    private void SaveStringToDatabase(ISession session, StringEntry input)
    {
      var translatedString =
          (from s in session.Linq<LocalizableStringRecord>()
           where s.StringKey == input.Key
              && s.Context == input.Context
           select s).FirstOrDefault();
      if (translatedString == null)
      {
        translatedString = new LocalizableStringRecord
        {
          Path = input.Path,
          Context = input.Context,
          StringKey = input.Key,
          OriginalLanguageString = input.English
        };
        session.SaveOrUpdate(translatedString);
      }
      else if (translatedString.OriginalLanguageString != input.English)
      {
        translatedString.OriginalLanguageString = input.English;
        session.SaveOrUpdate(translatedString);
      }

      if (!string.IsNullOrEmpty(input.Culture) && !string.IsNullOrEmpty(input.Translation))
      {
        var translation =
            (from t in translatedString.Translations
             where t.Culture.Equals(input.Culture)
             select t).FirstOrDefault() ?? new TranslationRecord
             {
               Culture = input.Culture,
               Value = input.Translation
             };
        if (translation.LocalizableStringRecord == null)
          translatedString.AddTranslation(translation);
        session.SaveOrUpdate(translatedString);
        session.SaveOrUpdate(translation);
      }

      SetCacheInvalid();
    }

    public byte[] GetZipBytes(string culture)
    {
      var model = GetCultureDetailsViewModel(culture);

      if (model.Groups.Count == 0)
        return null;

      using (var stream = new MemoryStream())
      {
        using (var zip = new ZipOutputStream(stream))
        {
          using (var writer = new StreamWriter(zip, Encoding.UTF8))
          {
            foreach (var translationGroup in model.Groups)
            {
              var file = new ZipEntry(translationGroup.Path) { DateTime = DateTime.Now };
              zip.PutNextEntry(file);
              writer.WriteLine(@"# Orchard resource strings - {0}
# Copyright (c) 2010 Outercurve Foundation
# All rights reserved
# This file is distributed under the BSD license
# This file is generated using the Q42.DbTranslations module
", culture);
              foreach (var translation in translationGroup.Translations)
              {
                writer.WriteLine("#: " + translation.Context);
                writer.WriteLine("#| msgid \"" + translation.Key + "\"");
                writer.WriteLine("msgctx \"" + translation.Context + "\"");
                writer.WriteLine("msgid \"" + translation.OriginalString + "\"");
                writer.WriteLine("msgstr \"" + translation.LocalString + "\"");
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

    public CultureIndexViewModel GetCultures()
    {
      var model = new CultureIndexViewModel();

      using (var session = _sessionLocator.For(typeof(TranslationRecord)))
      {
        var cultures =
            (from t in session.Linq<TranslationRecord>()
             group t by t.Culture
               into c
               select new
               {
                 c.First().Culture,
                 Count = c.Count()
               }
            ).ToList();
        foreach (var culture in cultures)
        {
          model.TranslationStates.Add(
              culture.Culture,
              new CultureIndexViewModel.CultureTranslationState { NumberOfTranslatedStrings = culture.Count });
        }
        model.NumberOfStringsInDefaultCulture = GetNumberOfTranslatableStrings(session);
      }

      foreach (var cult in _cultureManager.ListCultures())
      {
        if (!model.TranslationStates.ContainsKey(cult))
          model.TranslationStates.Add(cult, new CultureIndexViewModel.CultureTranslationState { NumberOfTranslatedStrings = 0 });
      }

      return model;
    }

    private int GetNumberOfTranslatableStrings(ISession session)
    {
      return (from t in session.Linq<LocalizableStringRecord>() select t).Count();
    }

    public void UpdateTranslation(int id, string culture, string value)
    {
      using (var session = _sessionLocator.For(typeof(LocalizableStringRecord)))
      {
        var localizable = session.Get<LocalizableStringRecord>(id);
        var translation = localizable.Translations.Where(t => t.Culture == culture).FirstOrDefault();
        if (translation == null)
        {
          var newTranslation = new TranslationRecord
          {
            Culture = culture,
            Value = value,
            LocalizableStringRecord = localizable
          };
          localizable.Translations.Add(newTranslation);
          session.SaveOrUpdate(newTranslation);
          session.SaveOrUpdate(localizable);
        }
        else
        {
          translation.Value = value;
          session.SaveOrUpdate(translation);
        }

        SetCacheInvalid();
      }
    }

    public void RemoveTranslation(int id, string culture)
    {
      using (var session = _sessionLocator.For(typeof(LocalizableStringRecord)))
      {
        var translation = session.Get<LocalizableStringRecord>(id)
            .Translations.Where(t => t.Culture == culture).FirstOrDefault();
        if (translation != null)
        {
          translation.LocalizableStringRecord.Translations.Remove(translation);
          session.Delete("Translation", translation);
        }

        SetCacheInvalid();
      }
    }

    public IEnumerable<StringEntry> GetTranslationsFromZip(Stream stream)
    {
      var zip = new ZipInputStream(stream);
      ZipEntry zipEntry;
      while ((zipEntry = zip.GetNextEntry()) != null)
      {
        if (zipEntry.IsFile)
        {
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

    public bool IsCultureAllowed(string culture)
    {
      return true;

      // todo wtf?
      var ctx = _wca.GetContext();
      var rolesPart = ctx.CurrentUser.As<UserRolesPart>();
      return (rolesPart != null && rolesPart.Roles.Contains(culture));
    }

    public IEnumerable<NotifyEntry> GetNotifications()
    {
      if (!IsCacheValid())
      {
        var request = _wca.GetContext().HttpContext.Request;
        UrlHelper urlHelper = new UrlHelper(request.RequestContext);
        var currentUrl = request.Url.PathAndQuery;

        yield return new NotifyEntry
        {
          Message = T("Translation cache needs to be flushed. <a href=\"{0}\">Click here to flush!</a>", urlHelper.Action("FlushCache", "Admin", new { area = "Q42.DbTranslations", redirectUrl = currentUrl })),
          Type = NotifyType.Warning
        };
      }
    }

    public void ResetCache()
    {
      _signals.Trigger("culturesChanged");
      _wca.GetContext().HttpContext.Application.Remove("q42TranslationsDirty");
    }

    private void SetCacheInvalid()
    {
      _wca.GetContext().HttpContext.Application["q42TranslationsDirty"] = true;
    }

    private bool IsCacheValid()
    {
      return !_wca.GetContext().HttpContext.Application.AllKeys.Contains("q42TranslationsDirty");
    }
  }
}