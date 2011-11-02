using Orchard.Localization;
using Orchard.Security;
using Orchard.UI.Navigation;

namespace Q42.DbTranslations
{
  public class AdminMenu : INavigationProvider
  {

    public Localizer T { get; set; }
    public string MenuName { get { return "admin"; } }

    public void GetNavigation(NavigationBuilder builder)
    {
      builder.AddImageSet("Translations").Add(T("Translations"), "20", menu => BuildViaselectMenu(menu), new[] { "collapsed" });
    }

    private NavigationBuilder BuildViaselectMenu(NavigationBuilder menu)
    {
      menu.Add(T("Translations"), "0", item => item.Action("Index", "Admin", new { area = "Q42.DbTranslations" })
                .Permission(Permissions.Translate));
      return menu;
    }

  }
}