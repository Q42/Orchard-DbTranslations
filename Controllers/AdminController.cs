using System;
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
using Orchard.Environment;
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
        private readonly IOrchardServices _services;
        private readonly ILocalizationManagementService _managementService;
        private readonly IExtensionManager _extensionManager;
        private readonly IHostEnvironment _hostEnvironment;

        public AdminController(
            ILocalizationService localizationService,
            IOrchardServices services,
            ILocalizationManagementService managementService,
            IExtensionManager extensionManager,
            IHostEnvironment hostEnvironment)
        {
            _localizationService = localizationService;
            _services = services;
            _managementService = managementService;
            _extensionManager = extensionManager;
            _hostEnvironment = hostEnvironment;

            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
        }

        public Localizer T { get; set; }
        public ILogger Logger { get; set; }

        public ActionResult Index()
        {
            var model = _localizationService.GetCultures();
            if (model.TranslationStates.Count == 0)
                return RedirectToAction("Import");
            return View(model);
        }

        public ActionResult Import()
        {
            if (!_services.Authorizer.Authorize(Permissions.UploadTranslation))
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
                model.CanTranslate = _services.Authorizer.Authorize(Permissions.UploadTranslation);
                ViewBag.Details = model;
            }
            return View(cultures);
        }

        public ActionResult Extra(string moduleName = "")
        {
            if (!_services.Authorizer.Authorize(Permissions.UploadTranslation))
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
            model.CanTranslate = _services.Authorizer.Authorize(Permissions.UploadTranslation);
            return View(model);
        }

        public ActionResult Details(string path, string culture)
        {
            if (culture == null) throw new HttpException(404, "Not found");
            new CultureInfo(culture); // Throws if invalid culture
            var model = _localizationService.GetTranslations(culture, path);
            model.CurrentGroupPath = path;
            model.CanTranslate = _services.Authorizer.Authorize(Permissions.UploadTranslation);
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
            if (!_services.Authorizer.Authorize(
                Permissions.UploadTranslation, T("You are not allowed to upload translations.")))
                return new HttpUnauthorizedResult();

            List<StringEntry> strings = new List<StringEntry>();
            var files = Directory.GetFiles(_hostEnvironment.MapPath("~"), "*.po", SearchOption.AllDirectories);
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
                    string path = new Fluent.IO.Path(file).MakeRelativeTo(new Fluent.IO.Path(_hostEnvironment.MapPath("~"))).ToString().Replace('\\', '/');

                    strings.AddRange(_localizationService.TranslateFile(path, System.IO.File.ReadAllText(file, Encoding.UTF8), culture));
                }
            }

            _localizationService.SaveStringsToDatabase(strings, overwrite ?? false);
            _services.Notifier.Add(NotifyType.Information, T("Imported {0} translations from {1} *.po files", strings.Count, files.Count()));
            _localizationService.ResetCache();
            return RedirectToAction("Import");
        }

        /// <summary>
        /// downloads the po zip from orchardproject.net and imports into database
        /// </summary>
        /// <param name="culture"></param>
        /// <returns></returns>
        public ActionResult ImportLiveOrchardPo(string culture)
        {
            IEnumerable<StringEntry> strings;
            var cultureInfo = CultureInfo.CreateSpecificCulture(culture);
            const string BaseUrlFormat = "https://crowdin.net/download/project/orchard-cms/{0}.zip";
            var url = string.Format(BaseUrlFormat, cultureInfo.Name);

            WebResponse response;
            if (!TryGetResponse(url, out response))
            {
                url = string.Format(BaseUrlFormat, cultureInfo.TwoLetterISOLanguageName);
                if (!TryGetResponse(url, out response))
                {
                    _services.Notifier.Error(T("No po zip found for culture '{0}'.", culture));
                    return RedirectToAction("Import");
                }
            }

            using (var stream = response.GetResponseStream())
                strings = _localizationService.GetTranslationsFromZip(stream).ToList();

            _localizationService.SaveStringsToDatabase(strings, false);
            _localizationService.ResetCache();
            return RedirectToAction("Import");
        }

        [HttpPost]
        public ActionResult Upload()
        {
            if (!_services.Authorizer.Authorize(
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
            _services.Notifier.Information(T("Imported {0} translations", strings.Count));

            _localizationService.ResetCache();
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateInput(false)]
        public ActionResult Update(int id, string culture, string value)
        {
            if (!_services.Authorizer.Authorize(
                Permissions.Translate, T("You are not allowed to update translations.")))
                return new HttpUnauthorizedResult();

            _localizationService.UpdateTranslation(id, culture, value);
            return new JsonResult { Data = value };
        }

        [HttpPost]
        public ActionResult Remove(int id, string culture)
        {
            if (!_services.Authorizer.Authorize(
                Permissions.Translate, T("You are not allowed to delete translations.")))
                return new HttpUnauthorizedResult();

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
            _services.Notifier.Information(T("*.po files saved to disk"));
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
            var translations = _managementService.ExtractDefaultTranslation(_hostEnvironment.MapPath("~"), null).ToList();
            //return Log(translations);
            _localizationService.SaveStringsToDatabase(translations, false);

            _services.Notifier.Add(NotifyType.Information, T("Imported {0} translatable strings", translations.Count));

            if (!string.IsNullOrEmpty(culture))
                ImportLiveOrchardPo(culture);

            _localizationService.ResetCache();
            return RedirectToAction("Import");
        }

        public ActionResult FromFeature(string culture, string path, string type)
        {
            if (String.IsNullOrEmpty(path) || String.IsNullOrEmpty(type))
            {
                _services.Notifier.Error(T("Invalid request!"));
                return RedirectToAction("Import");
            }

            var featurePath = "module".Equals(type) ? "~/Modules/" : "~/Themes/";
            featurePath += path;

            var translations = _managementService.ExtractDefaultTranslation(_hostEnvironment.MapPath("~"), _hostEnvironment.MapPath(featurePath)).ToList();
            //return Log(translations);
            _localizationService.SaveStringsToDatabase(translations, false);

            _services.Notifier.Information(T("Imported {0} translatable strings", translations.Count));

            if (!string.IsNullOrEmpty(culture))
                ImportLiveOrchardPo(culture);

            _localizationService.ResetCache();
            return RedirectToAction("Import");
        }


        private static bool TryGetResponse(string url, out WebResponse response)
        {
            try
            {
                var req = HttpWebRequest.Create(url);
                response = req.GetResponse();
                return true;
            }
            catch (WebException)
            {
                response = null;
                return false;
            }
        }

        // pull merged from https://github.com/LievenVandeperre/Orchard-DbTranslations
        public ActionResult FromAssembly(string referencedAssemblyName, string selectedModule)
        {
            //do stuff to get translations
            var translations = _managementService.ExtractTranslationsFromAssembly(referencedAssemblyName, selectedModule);

            //Save the strings to the database
            _localizationService.SaveStringsToDatabase(translations, false);

            //do the notifier thing
            _services.Notifier.Add(NotifyType.Information, T("Imported {0} translatable strings", translations.Count()));

            //Reset Cache
            _localizationService.ResetCache();
            //do the redirect
            return RedirectToAction("Extra");

        }
    }
}