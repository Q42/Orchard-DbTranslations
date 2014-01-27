﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Orchard;
using Orchard.Localization;
using Orchard.Themes;
using Orchard.UI.Notify;
using Q42.DbTranslations.Models;
using Q42.DbTranslations.Services;
using Orchard.Logging;
using Orchard.Environment.Extensions;
using Orchard.Environment.Extensions.Models;

namespace Q42.DbTranslations.Controllers
{
    [Themed]
    public class AdminController : Controller
    {
        private readonly ILocalizationService _localizationService;

        public ILogger Logger { get; set; }
        public Localizer T { get; set; }
        public IOrchardServices Services { get; set; }
        public ILocalizationManagementService ManagementService { get; set; }
        private readonly IExtensionManager _extensionManager;

        public AdminController(
                ILocalizationService localizationService,
                IOrchardServices services,
                ILocalizationManagementService managementService,
                IExtensionManager extensionManager)
        {
            _localizationService = localizationService;
            Services = services;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
            ManagementService = managementService;
            _extensionManager = extensionManager;
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

            var extensions = _extensionManager.AvailableExtensions();
            ViewBag.Modules = extensions
                .Where(e => DefaultExtensionTypes.IsModule(e.ExtensionType))
                .OrderBy(e => e.Name);
            ViewBag.Themes = extensions
                .Where(extensionDescriptor => DefaultExtensionTypes.IsTheme(extensionDescriptor.ExtensionType))
                .OrderBy(e => e.Name);
            return View(model);
        }
        public ActionResult Export()
        {
            var model = _localizationService.GetCultures();
            if (model.TranslationStates.Count == 0)
                return RedirectToAction("Import");
            return View(model);
        }
        public ActionResult Search(string querystring, string culture)
        {
            var cultures = _localizationService.GetCultures();
            ViewBag.querystring = querystring;
            ViewBag.culture = culture;
            if (!string.IsNullOrEmpty(querystring) || !string.IsNullOrEmpty(culture))
            {
                var model = _localizationService.Search(culture, querystring);
                model.CurrentGroupPath = querystring;
                model.CanTranslate = Services.Authorizer.Authorize(Permissions.UploadTranslation) &&
                               _localizationService.IsCultureAllowed(culture);
                ViewBag.Details = model;
            }
            return View(cultures);
        }

        public ActionResult Extra(string moduleName = "")
        {
            if (!Services.Authorizer.Authorize(Permissions.UploadTranslation))
                return RedirectToAction("Index");
            ViewBag.selectedModule = "";
            if (moduleName != "")
            {
                ViewBag.selectedModule = moduleName;
                var selectedAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(item => item.FullName.Contains(moduleName)).FirstOrDefault();

                if (selectedAssembly != null)
                {
                    ViewBag.Assemblies = selectedAssembly.GetReferencedAssemblies().OrderBy(e => e.Name);
                    
                }
            }

            var extensions = _extensionManager.AvailableExtensions();
            ViewBag.Modules = extensions
                .Where(e => DefaultExtensionTypes.IsModule(e.ExtensionType))
                .OrderBy(e => e.Name);

            return View();
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
        public ActionResult ImportCurrentPos(bool? overwrite)
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
                {
                    Logger.Error("Importing current PO's; cannot find culture in path " + file);
                }
                else
                {
                    string culture = match.Groups[1].Value;
                    string path = new Fluent.IO.Path(file).MakeRelativeTo(new Fluent.IO.Path(Server.MapPath("~"))).ToString().Replace('\\', '/');

                    strings.AddRange(_localizationService.TranslateFile(path, System.IO.File.ReadAllText(file, Encoding.UTF8), culture));
                }
            }

            _localizationService.SaveStringsToDatabase(strings, overwrite ?? false);
            Services.Notifier.Add(NotifyType.Information, T("Imported {0} translations from {1} *.po files", strings.Count, files.Count()));
            _localizationService.ResetCache();
            return RedirectToAction("Import");
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
            _localizationService.SaveStringsToDatabase(strings, false);
            _localizationService.ResetCache();
            return RedirectToAction("Import");
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
            _localizationService.SaveStringsToDatabase(strings, false);
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
            var translations = ManagementService.ExtractDefaultTranslation(Server.MapPath("~"), null).ToList();
            //return Log(translations);
            _localizationService.SaveStringsToDatabase(translations, false);

            Services.Notifier.Add(NotifyType.Information, T("Imported {0} translatable strings", translations.Count));

            if (!string.IsNullOrEmpty(culture))
                ImportLiveOrchardPo(culture);

            _localizationService.ResetCache();
            return RedirectToAction("Import");
        }
        
        public ActionResult FromFeature(string culture, string path, string type)
        {
            if (String.IsNullOrEmpty(path) || String.IsNullOrEmpty(type))
            {
                Services.Notifier.Add(NotifyType.Error, T("Invalid request!"));
                return RedirectToAction("Import");
            }

            var featurePath = "module".Equals(type) ? "~/Modules/" : "~/Themes/";
            featurePath += path;

            var translations = ManagementService.ExtractDefaultTranslation(Server.MapPath("~"), Server.MapPath(featurePath)).ToList();
            //return Log(translations);
            _localizationService.SaveStringsToDatabase(translations, false);

            Services.Notifier.Add(NotifyType.Information, T("Imported {0} translatable strings", translations.Count));

            if (!string.IsNullOrEmpty(culture))
                ImportLiveOrchardPo(culture);

            _localizationService.ResetCache();
            return RedirectToAction("Import");
        }

        public ActionResult FromAssembly(string referencedAssemblyName, string selectedModule)
        {
            //do stuff to get translations
            var translations = ManagementService.ExtractTranslationsFromAssembly(referencedAssemblyName, selectedModule);

            //Save the strings to the database
            _localizationService.SaveStringsToDatabase(translations, false);

            //do the notifier thing
            Services.Notifier.Add(NotifyType.Information, T("Imported {0} translatable strings", translations.Count()));

            //Reset Cache
            _localizationService.ResetCache();
            //do the redirect
            return RedirectToAction("Extra");
        }
    }
}