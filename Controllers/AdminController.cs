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
      model.CanUpload = Services.Authorizer.Authorize(Permissions.UploadTranslation);
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

    public ActionResult ImportCachedPo(string culture)
    {
      if (!Services.Authorizer.Authorize(
          Permissions.UploadTranslation, T("You are not allowed to upload translations.")))
        return new HttpUnauthorizedResult();

      var filePath = Server.MapPath("~/Modules/Q42.DbTranslations/Content/cache/orchard." + culture + ".po.zip");
      var file = new FileInfo(filePath);
      if (file.Exists)
      {
        var strings = _localizationService.GetTranslations(file).ToList();
        _localizationService.SaveStringsToDatabase(strings);
        Services.Notifier.Add(Orchard.UI.Notify.NotifyType.Information, T("Imported {0} translations in {1}", strings.Count, culture));
      }
      else
      {
        Services.Notifier.Add(Orchard.UI.Notify.NotifyType.Warning, T("File could not be found: {0}", filePath));
      }
      
      return RedirectToAction("Index");
    }

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
        strings.AddRange(_localizationService.GetTranslations(file));
      }

      //foreach (var t in strings)
      //{
      //  Response.Write("<pre>" + string.Join("\n", 
      //    "Path: " + t.Path,
      //    "Context: " + t.Context,
      //    "Key: " + t.Key,
      //    "English: " + t.English,
      //    t.Culture + ": " + t.Translation) + "</pre>");
      //}
      //return new EmptyResult();

      _localizationService.SaveStringsToDatabase(strings);
      Services.Notifier.Add(Orchard.UI.Notify.NotifyType.Information, T("Imported {0} translations", strings.Count));

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
      var cachePath = Server.MapPath("~/Modules/Q42.DbTranslations/Content/orchard." + culture + ".po.zip");
      if (System.IO.File.Exists(cachePath) &&
          DateTime.Now - System.IO.File.GetLastWriteTime(cachePath) < TimeSpan.FromDays(1))
      {
        return new FilePathResult(cachePath, "application/zip");
      }
      byte[] zipBytes = _localizationService.GetZipBytes(culture);
      System.IO.File.WriteAllBytes(cachePath, zipBytes);
      // todo: hij schrijft een goed bestand naar schijf en levert een corrupt bestand op
      return new FileContentResult(zipBytes, "application/zip")
      {
        FileDownloadName = "orchard." + culture + ".po.zip"
      };
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
      _localizationService.SaveStringsToDatabase(translations);

      //Response.Write("done: " + translations.Count());
      //foreach (var t in translations)
      //{
      //  Response.Write("<pre>" + string.Join("; ", t.Path, t.Context, t.Key, t.Culture, t.English, t.Translation, t.Used) + "</pre>");
      //}

      Services.Notifier.Add(Orchard.UI.Notify.NotifyType.Information, T("Imported {0} translatable strings", translations.Count));

      if (!string.IsNullOrEmpty(culture))
        ImportCachedPo(culture);

      return RedirectToAction("Index");
    }
  }
}