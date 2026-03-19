using System.Web;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AdminPagosDLL.Models;
using AdminPagosDLL.Core;
using System.IO;
using System.Data;
using System.Web.UI;
using System.Net;

namespace AdminPagosDLL.Controllers
{
    public class HomeController : Controller
    {
        public FMensaje Mensajes = new FMensaje();
        private static readonly object CotizacionHistoricaLock = new object();

        public static string TxtCotizacionHistoria = "";
        public static List<Pago> LstPagos = new List<Pago>();

        public ActionResult Index()
        {
            InicializarCotizacionHistorica();

            return View();
        }

        private void InicializarCotizacionHistorica()
        {
            Mensajes.Limpiar();

            if (!String.IsNullOrWhiteSpace(TxtCotizacionHistoria))
            {
                CotizacionHistorica.CargarCotizacion(TxtCotizacionHistoria);
                return;
            }

            try
            {
                lock (CotizacionHistoricaLock)
                {
                    if (!String.IsNullOrWhiteSpace(TxtCotizacionHistoria))
                    {
                        CotizacionHistorica.CargarCotizacion(TxtCotizacionHistoria);
                        return;
                    }

                    var cachePath = Server.MapPath("~/App_Data/cotizacionesHistoricas.json");
                    if (System.IO.File.Exists(cachePath))
                    {
                        TxtCotizacionHistoria = System.IO.File.ReadAllText(cachePath);
                    }

                    if (String.IsNullOrWhiteSpace(TxtCotizacionHistoria))
                    {
                        using (var client = new WebClient())
                        {
                            TxtCotizacionHistoria = client.DownloadString("https://apis.datos.gob.ar/series/api/series/?ids=168.1_T_CAMBIOR_D_0_0_26&limit=5000&format=json");
                        }

                        System.IO.File.WriteAllText(cachePath, TxtCotizacionHistoria);
                    }

                    CotizacionHistorica.CargarCotizacion(TxtCotizacionHistoria);
                }

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

            bool rutaHabilitada = LstPagos
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
        public JsonResult LeerPDF()
        {
            Mensajes.Limpiar();
            var retorno = Json(new { Mensajes }, JsonRequestBehavior.AllowGet);

            //Primero lee la cache
            if (LstPagos.Count > 0) return Json(new { Mensajes, pagos = LstPagos }, JsonRequestBehavior.AllowGet);

            try
            {
                var funcion = new Funciones();
                var pagos = new List<Pago>();
                pagos = funcion.CargarPagos();
                if (funcion.Mensajes.Lista.Any())
                {
                    Mensajes.Agregar(funcion.Mensajes.Lista);
                    return retorno;
                }

                LstPagos = pagos;

                return Json(new { Mensajes, pagos }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                //listaMensjes.Add( return Json(new { Mensajes = "Error: " + ex.Message });
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
            var retorno = Json(new { Mensajes }, JsonRequestBehavior.AllowGet);

            try
            {
                //1. Borra la cache
                LstPagos.Clear();

                //2. Borro lo serializado
                Funciones.BorrarSerializacion();

                return LeerPDF();

            }
            catch (Exception ex)
            {
                //listaMensjes.Add( return Json(new { Mensajes = "Error: " + ex.Message });
                return Json(new { Mensajes }, JsonRequestBehavior.AllowGet);
            }
        }

        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public ActionResult Error()
        //{
        //    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}

    }
}
