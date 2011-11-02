using System.Collections.Generic;
using Orchard.Environment.Extensions.Models;
using Orchard.Security.Permissions;

namespace Q42.DbTranslations
{
  public class Permissions : IPermissionProvider
  {
    public static readonly Permission UploadTranslation = new Permission { Description = "Upload new translations", Name = "Upload" };
    public static readonly Permission Translate = new Permission { Description = "Translate strings", Name = "Translate", ImpliedBy = new[] { UploadTranslation } };

    public virtual Feature Feature { get; set; }

    public IEnumerable<Permission> GetPermissions()
    {
      return new[] {
                UploadTranslation,
                Translate
            };
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
      return new[] {
                new PermissionStereotype {
                    Name = "Administrator",
                    Permissions = new[] {UploadTranslation, Translate}
                }
            };
    }

  }
}