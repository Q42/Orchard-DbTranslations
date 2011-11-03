using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Orchard.Localization.Services;
using Orchard.ContentManagement;
using Orchard;
using System.Web;
using Orchard.Environment.Extensions;
using Q42.DbTranslations.Models;

namespace Q42.DbTranslations.Services
{
  [OrchardFeature("Q42.AdminCultureSelector")]
  public class AdminCultureSelector : ICultureSelector
  {
    private readonly IWorkContextAccessor _workContextAccessor;

    public AdminCultureSelector(IWorkContextAccessor workContextAccessor)
    {
      _workContextAccessor = workContextAccessor;
    }

    public CultureSelectorResult GetCulture(HttpContextBase context)
    {
      bool isAdmin = context.Request.Path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);
      if (!isAdmin)return null;

      var settings = _workContextAccessor.GetContext().CurrentSite.As<AdminCultureSettingsPart>();
      if (settings == null) return null;
      
      string currentCultureName = settings.AdminCulture;
      if (string.IsNullOrEmpty(currentCultureName)) return null;

      return new CultureSelectorResult { Priority = 42, CultureName = currentCultureName };
    }
  }
}
