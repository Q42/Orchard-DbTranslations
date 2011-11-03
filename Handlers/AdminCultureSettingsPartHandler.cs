using Orchard.ContentManagement.Handlers;
using Orchard.Data;
using Orchard.Environment.Extensions;
using Q42.DbTranslations.Models;

namespace Q42.DbTranslations.Handlers
{
  [OrchardFeature("Q42.AdminCultureSelector")]
  public class AdminCultureSettingsPartHandler : ContentHandler
  {

    // contenthandler: http://www.orchardproject.net/docs/Understanding-content-handlers.ashx

    public AdminCultureSettingsPartHandler(IRepository<AdminCultureSettingsPartRecord> repository)
    {
      Filters.Add(new ActivatingFilter<AdminCultureSettingsPart>("Site"));
      Filters.Add(StorageFilter.For(repository));
    }

  }

}