using System;
using System.Globalization;
using System.Web;
using System.Web.Mvc;
using System.Linq;
using Orchard;
using Orchard.Localization;
using Orchard.Themes;
using Q42.DbTranslations.Services;
using Orchard.Caching;
using Q42.DbTranslations.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Orchard.UI.Notify;
using System.Text.RegularExpressions;
using System.Net;
using Orchard.Environment.Extensions;

namespace Q42.DbTranslations.Controllers
{
  [Themed]
  public class AdminController : Controller
  {
    private readonly ILocalizationService _localizationService;

    public Localizer T { get; set; }
    public IOrchardServices Services { get; set; }
    public ILocalizationManagementService ManagementService { get; set; }

    public AdminController(
      ILocalizationService localizationService,
      IOrchardServices services,
      ILocalizationManagementService managementService)
    {
      _localizationService = localizationService;
      Services = services;
      T = NullLocalizer.Instance;
      ManagementService = managementService;
    }

    public ActionResult Index()
    {
      var model = _localizationService.GetCultures();
      if (model.TranslationStates.Count == 0)
        return RedirectToAction("Import");
      return View(model);
    }
    public ActionResult Import()
    {
      if (!Services.Authorizer.Authorize(Permissions.UploadTranslation))
        return RedirectToAction("Index");
      var model = _localizationService.GetCultures();
      return View(model);
    }
    public ActionResult Export()
    {
      var model = _localizationService.GetCultures();
      if (model.TranslationStates.Count == 0)
        return RedirectToAction("Import");
      return View(model);
    }


    public ActionResult Culture(string culture)
    {
      if (culture == null) throw new HttpException(404, "Not found");
      new CultureInfo(culture); // Throws if invalid culture
      var model = _localizationService.GetModules(culture);
      model.CanTranslate = Services.Authorizer.Authorize(Permissions.UploadTranslation) &&
                           _localizationService.IsCultureAllowed(culture);
      return View(model);
    }

    public ActionResult Details(string path, string culture)
    {
      if (culture == null) throw new HttpException(404, "Not found");
      new CultureInfo(culture); // Throws if invalid culture
      var model = _localizationService.GetTranslations(culture, path);
      model.CurrentGroupPath = path;
      model.CanTranslate = Services.Authorizer.Authorize(Permissions.UploadTranslation) &&
                           _localizationService.IsCultureAllowed(culture);
      return View(model);
    }

    private readonly Regex cultureRegex = new Regex(@"\\App_Data\\Localization\\(\w{2}(-\w{2,})*)\\orchard\..*");

    /// <summary>
    /// Scans the site for *.po files and imports them into database
    /// </summary>
    /// <param name="culture"></param>
    /// <returns></returns>
    public ActionResult ImportCurrentPos()
    {
      if (!Services.Authorizer.Authorize(
          Permissions.UploadTranslation, T("You are not allowed to upload translations.")))
        return new HttpUnauthorizedResult();

      List<StringEntry> strings = new List<StringEntry>();
      var files = Directory.GetFiles(Server.MapPath("~"), "*.po", SearchOption.AllDirectories);
      foreach (var file in files)
      {
        var match = cultureRegex.Match(file);
        if (!match.Success || match.Groups.Count < 2)
          throw new Exception("Cannot find culture in path " + file);
        string culture = match.Groups[1].Value;
        string path = new Fluent.IO.Path(file).MakeRelativeTo(new Fluent.IO.Path(Server.MapPath("~"))).ToString().Replace('\\', '/');

        strings.AddRange(_localizationService.TranslateFile(path, System.IO.File.ReadAllText(file, Encoding.UTF8), culture));
      }

      //return Log(strings);
      _localizationService.SaveStringsToDatabase(strings);
      Services.Notifier.Add(NotifyType.Information, T("Imported {0} translations from {1} *.po files", strings.Count, files.Count()));
      _localizationService.ResetCache();
      return RedirectToAction("Index");
    }

    private ActionResult Log(IEnumerable<StringEntry> strings)
    {
      foreach (var t in strings)
      {
        Response.Write("<pre>" + string.Join("\n",
          "Path: " + t.Path,
          "Context: " + t.Context,
          "Key: " + t.Key,
          "English: " + t.English,
          t.Culture + ": " + t.Translation) + "</pre>");
      }
      return new EmptyResult();
    }

    /// <summary>
    /// downloads the po zip from orchardproject.net and imports into database
    /// </summary>
    /// <param name="culture"></param>
    /// <returns></returns>
    public ActionResult ImportLiveOrchardPo(string culture)
    {
      IEnumerable<StringEntry> strings;
      var url = "http://www.orchardproject.net/Localize/download/" + culture;
      var req = HttpWebRequest.Create(url);
      using (var stream = req.GetResponse().GetResponseStream())
        strings = _localizationService.GetTranslationsFromZip(stream).ToList();
      //return Log(strings);
      _localizationService.SaveStringsToDatabase(strings);
      _localizationService.ResetCache();
      return RedirectToAction("Index");
    }

    //public ActionResult ImportCachedPo(string culture)
    //{
    //  if (!Services.Authorizer.Authorize(
    //      Permissions.UploadTranslation, T("You are not allowed to upload translations.")))
    //    return new HttpUnauthorizedResult();

    //  var filePath = Server.MapPath("~/Modules/Q42.DbTranslations/Content/cache/orchard." + culture + ".po.zip");
    //  var file = new FileInfo(filePath);
    //  if (file.Exists)
    //  {
    //    List<StringEntry> strings;
    //    using (var stream = file.OpenRead())
    //    {
    //      strings = _localizationService.GetTranslationsFromZip(stream).ToList();
    //    }
    //    //return Log(strings);
    //    _localizationService.SaveStringsToDatabase(strings);
    //    Services.Notifier.Add(NotifyType.Information, T("Imported {0} translations in {1}", strings.Count, culture));
    //  }
    //  else
    //  {
    //    Services.Notifier.Add(Orchard.UI.Notify.NotifyType.Warning, T("File could not be found: {0}", filePath));
    //  }

    //  _localizationService.ResetCache();
    //  return RedirectToAction("Index");
    //}

    [HttpPost]
    public ActionResult Upload()
    {
      if (!Services.Authorizer.Authorize(
          Permissions.UploadTranslation, T("You are not allowed to upload translations.")))
        return new HttpUnauthorizedResult();

      var strings = new List<StringEntry>();
      foreach (var file in
          from string fileName in Request.Files
          select Request.Files[fileName])
      {
        using (var stream = file.InputStream)
          strings.AddRange(_localizationService.GetTranslationsFromZip(stream));
      }

      //return Log(strings);
      _localizationService.SaveStringsToDatabase(strings);
      Services.Notifier.Add(Orchard.UI.Notify.NotifyType.Information, T("Imported {0} translations", strings.Count));

      _localizationService.ResetCache();
      return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateInput(false)]
    public ActionResult Update(int id, string culture, string value)
    {
      if (!Services.Authorizer.Authorize(
          Permissions.Translate, T("You are not allowed to update translations.")))
        return new HttpUnauthorizedResult();

      if (!_localizationService.IsCultureAllowed(culture))
      {
        return new HttpUnauthorizedResult();
      }

      _localizationService.UpdateTranslation(id, culture, value);
      return new JsonResult { Data = value };
    }

    [HttpPost]
    public ActionResult Remove(int id, string culture)
    {
      if (!Services.Authorizer.Authorize(
          Permissions.Translate, T("You are not allowed to delete translations.")))
        return new HttpUnauthorizedResult();

      if (!_localizationService.IsCultureAllowed(culture))
      {
        return new HttpUnauthorizedResult();
      }
      _localizationService.RemoveTranslation(id, culture);
      return new JsonResult { Data = null };
    }

    /// <summary>
    /// caches and serves, because plain serving doesn't work :(
    /// </summary>
    /// <param name="culture"></param>
    /// <returns></returns>
    public ActionResult Download(string culture)
    {
      //var cachePath = Server.MapPath("~/Modules/Q42.DbTranslations/Content/orchard." + culture + ".po.zip");
      byte[] zipBytes = _localizationService.GetZipBytes(culture);

      if (zipBytes == null)
        throw new Exception("There are no translations in " + culture);

      //System.IO.File.WriteAllBytes(cachePath, zipBytes);
      // todo: hij schrijft een goed bestand naar schijf en levert een corrupt bestand op
      return new FileContentResult(zipBytes, "application/zip")
      {
        FileDownloadName = "orchard." + culture + ".po.zip"
      };
    }

    public ActionResult PoFilesToDisk()
    {
      _localizationService.SavePoFilesToDisk();
      Services.Notifier.Add(NotifyType.Information, T("*.po files saved to disk"));
      return RedirectToAction("Export");
    }

    public ActionResult FlushCache(string redirectUrl)
    {
      _localizationService.ResetCache();
      if (!string.IsNullOrEmpty(redirectUrl))
        return new RedirectResult(redirectUrl);
      return RedirectToAction("Index");
    }

    public ActionResult FromSource(string culture)
    {
      var translations = ManagementService.ExtractDefaultTranslation(Server.MapPath("~")).ToList();
      //return Log(translations);
      _localizationService.SaveStringsToDatabase(translations);

      Services.Notifier.Add(NotifyType.Information, T("Imported {0} translatable strings", translations.Count));

      if (!string.IsNullOrEmpty(culture))
        ImportLiveOrchardPo(culture);

      _localizationService.ResetCache();
      return RedirectToAction("Index");
    }
  }
}