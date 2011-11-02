using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Mvc.Routes;

namespace OrchardPo
{
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
                            {"area", "OrchardPo"},
                            {"controller", "Admin"},
                            {"action", "Index"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "OrchardPo"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/remove",
                        new RouteValueDictionary {
                            {"area", "OrchardPo"},
                            {"controller", "Admin"},
                            {"action", "Remove"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "OrchardPo"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/update",
                        new RouteValueDictionary {
                            {"area", "OrchardPo"},
                            {"controller", "Admin"},
                            {"action", "Update"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "OrchardPo"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/upload",
                        new RouteValueDictionary {
                            {"area", "OrchardPo"},
                            {"controller", "Admin"},
                            {"action", "Upload"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "OrchardPo"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/download/{culture}",
                        new RouteValueDictionary {
                            {"area", "OrchardPo"},
                            {"controller", "Admin"},
                            {"action", "Download"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "OrchardPo"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/details",
                        new RouteValueDictionary {
                            {"area", "OrchardPo"},
                            {"controller", "Admin"},
                            {"action", "Details"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "OrchardPo"}
                        },
                        new MvcRouteHandler()
                    )
                },
                new RouteDescriptor {
                    Route = new Route(
                        "Admin/localize/{culture}",
                        new RouteValueDictionary {
                            {"area", "OrchardPo"},
                            {"controller", "Admin"},
                            {"action", "Culture"}
                        },
                        new RouteValueDictionary(),
                        new RouteValueDictionary {
                            {"area", "OrchardPo"}
                        },
                        new MvcRouteHandler()
                    )
                },
            };
    }
  }
}