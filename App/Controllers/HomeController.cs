using System.Runtime.Caching;
using System.Web;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using AdminPagosDLL.Models;
using AdminPagosDLL.Core;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

namespace AdminPagosDLL.Controllers
{
    public class HomeController : Controller
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public FMensaje Mensajes = new FMensaje();

        private const string PagosCacheKey = "PagosCache";
        private const string CotizacionCacheKey = "CotizacionHistoricaJson";
        private const string CantNoAbiertosCacheKey = "CantNoAbiertos";
        private const string CantNoIdentificadosCacheKey = "CantNoIdentificados";
        private const string CantNoValidosCacheKey = "CantNoValidos";
        private const string NoAbiertosPathsCacheKey = "NoAbiertosPaths";
        private const string NoIdentificadosPathsCacheKey = "NoIdentificadosPaths";
        private const string NoValPathsCacheKey = "NoValPaths";
        private const string UsdInflationFactorsCacheKey = "UsdInflationFactors";

        public async Task<ActionResult> Index()
        {
            await InicializarCotizacionHistoricaAsync();
            return View();
        }

        private async Task InicializarCotizacionHistoricaAsync()
        {
            Mensajes.Limpiar();

            var cache = MemoryCache.Default;
            var json = cache.Get(CotizacionCacheKey) as string;

            if (!String.IsNullOrWhiteSpace(json))
            {
                CotizacionHistorica.CargarCotizacion(json);
                return;
            }

            try
            {
                var cachePath = Server.MapPath("~/App_Data/cotizacionesHistoricas.json");
                if (System.IO.File.Exists(cachePath))
                {
                    using (var reader = new StreamReader(cachePath))
                        json = await reader.ReadToEndAsync();
                }

                if (String.IsNullOrWhiteSpace(json))
                {
                    json = await _httpClient.GetStringAsync("https://apis.datos.gob.ar/series/api/series/?ids=168.1_T_CAMBIOR_D_0_0_26&limit=5000&format=json");
                    using (var writer = new StreamWriter(cachePath, false))
                        await writer.WriteAsync(json);
                }

                cache.Set(CotizacionCacheKey, json, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                CotizacionHistorica.CargarCotizacion(json);
            }
            catch (Exception ex)
            {
                Mensajes.Agregar(ex.Message);
            }
        }

        public static Dictionary<string, decimal> PrecomputeMonthlyFactors(string jsonFilePath)
        {
            if (!System.IO.File.Exists(jsonFilePath))
                return new Dictionary<string, decimal>();

            var json = System.IO.File.ReadAllText(jsonFilePath);
            var rawRates = JsonConvert.DeserializeObject<Dictionary<string, double?>>(json);
            if (rawRates == null || rawRates.Count == 0)
                return new Dictionary<string, decimal>();

            var monthlyRates = rawRates
                .Where(kvp => kvp.Value.HasValue)
                .Select(kvp => new
                {
                    Month = DateTime.ParseExact(kvp.Key, "yyyy-MM", CultureInfo.InvariantCulture),
                    Rate = kvp.Value.Value
                })
                .OrderByDescending(m => m.Month)
                .ToList();

            if (monthlyRates.Count == 0)
                return new Dictionary<string, decimal>();

            var latestMonth = monthlyRates.First().Month;
            var factors = new Dictionary<string, decimal>();
            double runningMultiplier = 1.0;
            factors[latestMonth.ToString("yyyy-MM")] = 1.0m;

            foreach (var item in monthlyRates.Skip(1))
            {
                runningMultiplier *= 1.0 + item.Rate / 100.0;
                factors[item.Month.ToString("yyyy-MM")] = (decimal)Math.Round(runningMultiplier, 4);
            }

            return factors;
        }

        public static async Task<Dictionary<string, decimal>> PrecomputeMonthlyFactorsAsync(string jsonFilePath)
        {
            if (!System.IO.File.Exists(jsonFilePath))
                return new Dictionary<string, decimal>();

            string json;
            using (var reader = new StreamReader(jsonFilePath))
                json = await reader.ReadToEndAsync();
            var rawRates = JsonConvert.DeserializeObject<Dictionary<string, double?>>(json);
            if (rawRates == null || rawRates.Count == 0)
                return new Dictionary<string, decimal>();

            var monthlyRates = rawRates
                .Where(kvp => kvp.Value.HasValue)
                .Select(kvp => new
                {
                    Month = DateTime.ParseExact(kvp.Key, "yyyy-MM", CultureInfo.InvariantCulture),
                    Rate = kvp.Value.Value
                })
                .OrderByDescending(m => m.Month)
                .ToList();

            if (monthlyRates.Count == 0)
                return new Dictionary<string, decimal>();

            var latestMonth = monthlyRates.First().Month;
            var factors = new Dictionary<string, decimal>();
            double runningMultiplier = 1.0;
            factors[latestMonth.ToString("yyyy-MM")] = 1.0m;

            foreach (var item in monthlyRates.Skip(1))
            {
                runningMultiplier *= 1.0 + item.Rate / 100.0;
                factors[item.Month.ToString("yyyy-MM")] = (decimal)Math.Round(runningMultiplier, 4);
            }

            return factors;
        }

        private async Task<Dictionary<string, decimal>> GetUsdInflationFactorsAsync()
        {
            var cache = MemoryCache.Default;
            var factors = cache.Get(UsdInflationFactorsCacheKey) as Dictionary<string, decimal>;
            if (factors != null) return factors;

            var path = Server.MapPath("~/App_Data/usdInflactionFactors.json");
            factors = await PrecomputeMonthlyFactorsAsync(path);
            if (factors.Count > 0)
            {
                cache.Set(UsdInflationFactorsCacheKey, factors, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
            }
            return factors;
        }

        private async Task AplicarInflacionYEnteDisplayAsync(List<Pago> pagos)
        {
            var inflationFactors = await GetUsdInflationFactorsAsync();
            foreach (var pe in pagos.OfType<PagoEfectuado>())
            {
                pe.EnteDisplayText = Funciones.GetEnteDisplayText(pe.Ente);
                if (inflationFactors.TryGetValue(pe.FechaPago.ToString("yyyy-MM"), out var factor))
                    pe.ImporteDolarActualizado = decimal.Round(pe.ImporteDolar * factor, 2);
                else
                    pe.ImporteDolarActualizado = pe.ImporteDolar;
            }
        }

        /// <summary>
        /// Devuelve el comprobante de pago
        /// </summary>
        /// <param name="path">Ruta de ubicación del pdf.</param>
        /// <returns></returns>
        public async Task<ActionResult> GetReport(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Ruta inválida.");
            }

            var decodedPath = Server.UrlDecode(path)?.Trim();
            if (String.IsNullOrWhiteSpace(decodedPath))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Ruta inválida.");
            }

            string reportPath;
            try
            {
                reportPath = System.IO.Path.GetFullPath(decodedPath);
            }
            catch
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Ruta inválida.");
            }

            bool esPdf = reportPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            if (!esPdf)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "El archivo solicitado no es PDF.");
            }

            var pagos = MemoryCache.Default.Get(PagosCacheKey) as List<Pago>;
            bool rutaHabilitada = pagos != null && pagos
                .OfType<PagoEfectuado>()
                .Any(p =>
                    !String.IsNullOrWhiteSpace(p.Path) &&
                    String.Equals(System.IO.Path.GetFullPath(p.Path), reportPath, StringComparison.OrdinalIgnoreCase));

            if (!rutaHabilitada)
            {
                var fallosPaths = (MemoryCache.Default.Get(NoAbiertosPathsCacheKey) as List<string> ?? new List<string>())
                    .Concat(MemoryCache.Default.Get(NoIdentificadosPathsCacheKey) as List<string> ?? new List<string>())
                    .Concat(MemoryCache.Default.Get(NoValPathsCacheKey) as List<string> ?? new List<string>());
                rutaHabilitada = fallosPaths.Any(p =>
                    !String.IsNullOrWhiteSpace(p) &&
                    String.Equals(System.IO.Path.GetFullPath(p), reportPath, StringComparison.OrdinalIgnoreCase));
            }

            if (!rutaHabilitada)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Archivo no autorizado.");
            }

            if (!System.IO.File.Exists(reportPath))
            {
                return HttpNotFound("No se encontró el comprobante solicitado.");
            }

            byte[] fileBytes;
            using (var stream = new FileStream(reportPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                fileBytes = new byte[stream.Length];
                await stream.ReadAsync(fileBytes, 0, fileBytes.Length);
            }
            return File(fileBytes, "application/pdf");
        }

        /// <summary>
        /// Lee y procesa todos los Pdfs.
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> LeerPDF(bool forceReinterpret = false)
        {
            Mensajes.Limpiar();

            var cache = MemoryCache.Default;
            var pagos = cache.Get(PagosCacheKey) as List<Pago>;

            if (!forceReinterpret && pagos != null && pagos.Count > 0)
            {
                await AplicarInflacionYEnteDisplayAsync(pagos);
                return Json(new {
                    Mensajes,
                    pagos,
                    cantNoAbiertos = cache.Get(CantNoAbiertosCacheKey) as int? ?? 0,
                    cantNoIdentificados = cache.Get(CantNoIdentificadosCacheKey) as int? ?? 0,
                    cantNoValidos = cache.Get(CantNoValidosCacheKey) as int? ?? 0,
                    noAbiertosPaths = cache.Get(NoAbiertosPathsCacheKey) as List<string> ?? new List<string>(),
                    noIdentificadosPaths = cache.Get(NoIdentificadosPathsCacheKey) as List<string> ?? new List<string>(),
                    noValPaths = cache.Get(NoValPathsCacheKey) as List<string> ?? new List<string>()
                }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                var funcion = new Funciones();
                pagos = await funcion.CargarPagosAsync(forceReinterpret: forceReinterpret);

                if (funcion.Mensajes.Lista.Any())
                {
                    Mensajes.Agregar(funcion.Mensajes.Lista);
                    return Json(new { Mensajes }, JsonRequestBehavior.AllowGet);
                }

                var noValPaths = funcion.noVal.OfType<PagoEfectuado>().Select(p => p.Path).ToList();

                cache.Set(PagosCacheKey, pagos, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(CantNoAbiertosCacheKey, funcion.CantNoAbiertos, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(CantNoIdentificadosCacheKey, funcion.CantNoIdentificados, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(CantNoValidosCacheKey, funcion.noVal.Count, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(NoAbiertosPathsCacheKey, funcion.NoAbiertosPaths, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(NoIdentificadosPathsCacheKey, funcion.NoIdentificadosPaths, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(NoValPathsCacheKey, noValPaths, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                await AplicarInflacionYEnteDisplayAsync(pagos);
                return Json(new {
                    Mensajes,
                    pagos,
                    cantNoAbiertos = funcion.CantNoAbiertos,
                    cantNoIdentificados = funcion.CantNoIdentificados,
                    cantNoValidos = funcion.noVal.Count,
                    noAbiertosPaths = funcion.NoAbiertosPaths,
                    noIdentificadosPaths = funcion.NoIdentificadosPaths,
                    noValPaths
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Mensajes.Agregar("Error en el metodo LeerPDF");
                return Json(new { Mensajes }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Lee y procesa todos los Pdfs.
        /// </summary>
        /// <returns></returns>
        public async Task<JsonResult> ActualizarPDF()
        {
            Mensajes.Limpiar();

            try
            {
                MemoryCache.Default.Remove(PagosCacheKey);
                MemoryCache.Default.Remove(CantNoAbiertosCacheKey);
                MemoryCache.Default.Remove(CantNoIdentificadosCacheKey);
                MemoryCache.Default.Remove(CantNoValidosCacheKey);
                MemoryCache.Default.Remove(NoAbiertosPathsCacheKey);
                MemoryCache.Default.Remove(NoIdentificadosPathsCacheKey);
                MemoryCache.Default.Remove(NoValPathsCacheKey);
                MemoryCache.Default.Remove(UsdInflationFactorsCacheKey);

                await Funciones.BorrarSerializacionAsync();

                return await LeerPDF(forceReinterpret: true);
            }
            catch (Exception ex)
            {
                return Json(new { Mensajes }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult OpenFolder(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            try
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                    return HttpNotFound();

                Process.Start("explorer.exe", "\"" + dir + "\"");
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
            }
        }

        public async Task<JsonResult> ReprocesarPago(string path)
        {
            Mensajes.Limpiar();

            if (String.IsNullOrWhiteSpace(path))
                return Json(new { success = false, Mensajes }, JsonRequestBehavior.AllowGet);

            var decodedPath = Server.UrlDecode(path)?.Trim();
            if (String.IsNullOrWhiteSpace(decodedPath) || !decodedPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(decodedPath))
                return Json(new { success = false, Mensajes }, JsonRequestBehavior.AllowGet);

            try
            {
                var funcion = new Funciones();
                await Task.Run(() => funcion.ReprocesarPago(decodedPath));

                if (funcion.Mensajes.Lista.Any())
                {
                    Mensajes.Agregar(funcion.Mensajes.Lista);
                    return Json(new { success = false, Mensajes }, JsonRequestBehavior.AllowGet);
                }

                var cache = MemoryCache.Default;
                var pagos = funcion.lstModelos.Cast<Pago>().ToList();
                var noValPaths = funcion.noVal.OfType<PagoEfectuado>().Select(p => p.Path).ToList();

                cache.Set(PagosCacheKey, pagos, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(CantNoAbiertosCacheKey, funcion.CantNoAbiertos, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(CantNoIdentificadosCacheKey, funcion.CantNoIdentificados, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(CantNoValidosCacheKey, funcion.noVal.Count, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(NoAbiertosPathsCacheKey, funcion.NoAbiertosPaths, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(NoIdentificadosPathsCacheKey, funcion.NoIdentificadosPaths, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                cache.Set(NoValPathsCacheKey, noValPaths, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });

                await AplicarInflacionYEnteDisplayAsync(pagos);

                return Json(new
                {
                    success = true,
                    Mensajes,
                    pagos,
                    cantNoAbiertos = funcion.CantNoAbiertos,
                    cantNoIdentificados = funcion.CantNoIdentificados,
                    cantNoValidos = funcion.noVal.Count,
                    noAbiertosPaths = funcion.NoAbiertosPaths,
                    noIdentificadosPaths = funcion.NoIdentificadosPaths,
                    noValPaths
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Mensajes.Agregar("Error al reprocesar el pago: " + ex.Message);
                return Json(new { success = false, Mensajes }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
