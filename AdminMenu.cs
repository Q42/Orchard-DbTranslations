﻿using Orchard.Localization;
using Orchard.UI.Navigation;

namespace Q42.DbTranslations
{
  public class AdminMenu : INavigationProvider
  {

    public Localizer T { get; set; }
    public string MenuName { get { return "admin"; } }

    public void GetNavigation(NavigationBuilder builder)
    {
        builder
          .AddImageSet("Translations")
          .Add(T("Translations"), "20", menu => menu.Action("Index", "Admin", new { area = "Q42.DbTranslations" })
            .Add(T("Translate"), "0", item => item.Action("Index", "Admin", new { area = "Q42.DbTranslations" }).LocalNav())
            .Add(T("Import"), "1", item => item.Action("Import", "Admin", new { area = "Q42.DbTranslations" }).LocalNav())
            .Add(T("Export"), "2", item => item.Action("Export", "Admin", new { area = "Q42.DbTranslations" }).LocalNav())
            .Add(T("Search"), "3", item => item.Action("Search", "Admin", new { area = "Q42.DbTranslations" }).LocalNav())
            .Add(T("Extra"), "4", item => item.Action("Extra", "Admin", new { area = "Q42.DbTranslations"}).LocalNav())
          );
    }

  }
}