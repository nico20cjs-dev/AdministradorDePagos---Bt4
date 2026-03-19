using System.Web.Optimization;

namespace App
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            // JS: jQuery → DataTables core → app utilities
            // In Release mode: bundled + minified into a single request
            // In Debug mode: served as individual files for easier debugging
            bundles.Add(new ScriptBundle("~/bundles/appscripts").Include(
                "~/Scripts/jquery-{version}.js",
                "~/vendor/datatables/jquery.dataTables.min.js",
                "~/js/generales.js"));

            // CSS: DataTables base skin + app styles
            bundles.Add(new StyleBundle("~/bundles/appcss").Include(
                "~/vendor/datatables/dataTables.bootstrap4.min.css",
                "~/css/custom.css"));
        }
    }
}
