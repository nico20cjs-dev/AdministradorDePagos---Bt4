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

namespace AdminPagosDLL.Controllers
{
    public class HomeController : Controller
    {
        public FMensaje Mensajes = new FMensaje();

        private const string PagosCacheKey = "PagosCache";
        private const string CotizacionCacheKey = "CotizacionHistoricaJson";
        private const string CantNoAbiertosCacheKey = "CantNoAbiertos";
        private const string CantNoIdentificadosCacheKey = "CantNoIdentificados";
        private const string CantNoValidosCacheKey = "CantNoValidos";
        private const string NoAbiertosPathsCacheKey = "NoAbiertosPaths";
        private const string NoIdentificadosPathsCacheKey = "NoIdentificadosPaths";
        private const string NoValPathsCacheKey = "NoValPaths";

        public ActionResult Index()
        {
            InicializarCotizacionHistorica();
            return View();
        }

        private void InicializarCotizacionHistorica()
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
                    json = System.IO.File.ReadAllText(cachePath);
                }

                if (String.IsNullOrWhiteSpace(json))
                {
                    using (var client = new WebClient())
                    {
                        json = client.DownloadString("https://apis.datos.gob.ar/series/api/series/?ids=168.1_T_CAMBIOR_D_0_0_26&limit=5000&format=json");
                    }
                    System.IO.File.WriteAllText(cachePath, json);
                }

                cache.Set(CotizacionCacheKey, json, new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable });
                CotizacionHistorica.CargarCotizacion(json);
            }
            catch (Exception ex)
            {
                Mensajes.Agregar(ex.Message);
            }
        }

        /// <summary>
        /// Devuelve el comprobante de pago
        /// </summary>
        /// <param name="path">Ruta de ubicación del pdf.</param>
        /// <returns></returns>
        public ActionResult GetReport(string path)
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
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Archivo no autorizado.");
            }

            if (!System.IO.File.Exists(reportPath))
            {
                return HttpNotFound("No se encontró el comprobante solicitado.");
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(reportPath);
            return File(fileBytes, "application/pdf");
        }

        /// <summary>
        /// Lee y procesa todos los Pdfs.
        /// </summary>
        /// <returns></returns>
        public JsonResult LeerPDF(bool forceReinterpret = false)
        {
            Mensajes.Limpiar();

            var cache = MemoryCache.Default;
            var pagos = cache.Get(PagosCacheKey) as List<Pago>;

            //Primero lee la cache (saltear si forceReinterpret)
            if (!forceReinterpret && pagos != null && pagos.Count > 0)
            {
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
                pagos = funcion.CargarPagos(forceReinterpret: forceReinterpret);

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
                return Json(new { Mensajes }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Lee y procesa todos los Pdfs.
        /// </summary>
        /// <returns></returns>
        public JsonResult ActualizarPDF()
        {
            Mensajes.Limpiar();

            try
            {
                //Borra la cache en memoria, no la serialización
                MemoryCache.Default.Remove(PagosCacheKey);
                MemoryCache.Default.Remove(CantNoAbiertosCacheKey);
                MemoryCache.Default.Remove(CantNoIdentificadosCacheKey);
                MemoryCache.Default.Remove(CantNoValidosCacheKey);
                MemoryCache.Default.Remove(NoAbiertosPathsCacheKey);
                MemoryCache.Default.Remove(NoIdentificadosPathsCacheKey);
                MemoryCache.Default.Remove(NoValPathsCacheKey);
                return LeerPDF(forceReinterpret: true);
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
    }
}
