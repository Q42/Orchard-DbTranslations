using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Mvc.Routes;
using Orchard.Environment.Extensions;

namespace Q42.DbTranslations
{
  [OrchardFeature("Q42.DbTranslations")]
  public class Routes : IRouteProvider
  {
    public void GetRoutes(ICollection<RouteDescriptor> routes)
    {
      foreach (var routeDescriptor in GetRoutes())
        routes.Add(routeDescriptor);
    }

    public IEnumerable<RouteDescriptor> GetRoutes()
    {
      return new[] {
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize",
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"},
                            {"controller", "Admin"},
                            {"action", "Index"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/remove",
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"},
                            {"controller", "Admin"},
                            {"action", "Remove"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/update",
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"},
                            {"controller", "Admin"},
                            {"action", "Update"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/upload",
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"},
                            {"controller", "Admin"},
                            {"action", "Upload"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/download/{culture}",
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"},
                            {"controller", "Admin"},
                            {"action", "Download"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/details",
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"},
                            {"controller", "Admin"},
                            {"action", "Details"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/{culture}",
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"},
                            {"controller", "Admin"},
                            {"action", "Culture"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "Q42.DbTranslations"}
                        },
                        new MvcRouteHandler()
                    )
                },
            };
    }
  }
}