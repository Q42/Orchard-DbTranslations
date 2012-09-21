using System.Collections.Generic;
using Orchard.Environment.Extensions.Models;
using Orchard.Security.Permissions;

namespace Q42.DbTranslations
{
  public class Permissions : IPermissionProvider
  {
    public static readonly Permission ImportExport = new Permission { Description = "Import / Export translations", Name = "Upload" };
    public static readonly Permission Translate = new Permission { Description = "Translate strings", Name = "Translate", ImpliedBy = new[] { ImportExport } };

    public virtual Feature Feature { get; set; }

    public IEnumerable<Permission> GetPermissions()
    {
      return new[] {
                ImportExport,
                Translate
            };
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
      return new[] {
                new PermissionStereotype {
                    Name = "Administrator",
                    Permissions = new[] {ImportExport, Translate}
                }
            };
    }

  }
}