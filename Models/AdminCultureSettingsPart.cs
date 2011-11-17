using System.ComponentModel.DataAnnotations;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Records;
using Orchard.Environment.Extensions;

namespace Q42.DbTranslations.Models
{
  [OrchardFeature("Q42.AdminCultureSelector")]
  public class AdminCultureSettingsPartRecord : ContentPartRecord
  {
    public virtual string AdminCulture { get; set; }
  }

  [OrchardFeature("Q42.AdminCultureSelector")]
  public class AdminCultureSettingsPart : ContentPart<AdminCultureSettingsPartRecord>
  {
    public string AdminCulture
    {
      get { return Record.AdminCulture; }
      set { Record.AdminCulture = value; }
    }

  }
  
}