using JetBrains.Annotations;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Drivers;
using Orchard.ContentManagement.Handlers;
using System;
using Q42.DbTranslations;
using Q42.DbTranslations.Models;

namespace Q42.DbTranslations.Drivers
{
  [UsedImplicitly]
  public class AdminCultureSettingsPartDriver : ContentPartDriver<AdminCultureSettingsPart>
  {

    protected override DriverResult Display(AdminCultureSettingsPart part, string displayType, dynamic shapeHelper)
    {
      return null;
    }

    protected override DriverResult Editor(AdminCultureSettingsPart part, dynamic shapeHelper)
    {
      return ContentShape("Parts_AdminCultureSettings_Edit",
         () => shapeHelper.EditorTemplate(
         TemplateName: "Parts/AdminCultureSettings",
         Model: part,
         Prefix: Prefix));
    }

    protected override DriverResult Editor(AdminCultureSettingsPart part, IUpdateModel updater, dynamic shapeHelper)
    {
      updater.TryUpdateModel(part, Prefix, null, null);
      return Editor(part, shapeHelper);
    }

    protected override void Importing(AdminCultureSettingsPart part, ImportContentContext context)
    {
      var AdminCulture = context.Attribute(part.PartDefinition.Name, "AdminCulture");
      if (AdminCulture != null) part.AdminCulture = AdminCulture;
    }

    protected override void Exporting(AdminCultureSettingsPart part, ExportContentContext context)
    {
      if (part.AdminCulture != null)
        context.Element(part.PartDefinition.Name).SetAttributeValue("AdminCulture", part.AdminCulture);
    }
  }
}