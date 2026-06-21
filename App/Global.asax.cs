using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Runtime.Caching;

namespace App
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            PreloadUsdInflationFactors();
        }

        private void PreloadUsdInflationFactors()
        {
            try
            {
                var path = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/usdInflactionFactors.json");
                var factors = AdminPagosDLL.Controllers.HomeController.PrecomputeMonthlyFactors(path);
                if (factors.Count > 0)
                {
                    MemoryCache.Default.Set("UsdInflationFactors", factors,
                        new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                }
            }
            catch { }
        }
    }
}
