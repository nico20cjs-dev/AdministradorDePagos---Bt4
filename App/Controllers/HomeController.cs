using System.Web;
using System.Web.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
using AdminPagosDLL.Models;
using AdminPagosDLL.Core;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.IO;
using System.Data;
using System.Web.UI;

namespace AdminPagosDLL.Controllers
{
    public class HomeController : Controller
    {
        public FMensaje Mensajes = new FMensaje();

        public static string TxtCotizacionHistoria = "";
        public static List<Pago> LstPagos = new List<Pago>();

        public ActionResult Index()
        {
            return View();
        }

        public void GetCacheCotizacionHistoria()
        {
            Mensajes.Limpiar();
            var retorno = Json(new { Mensajes }, JsonRequestBehavior.AllowGet);
            try
            {
                if (String.IsNullOrEmpty(TxtCotizacionHistoria))
                {
                    using (var client = new System.Net.WebClient())
                    {
                        TxtCotizacionHistoria = client.DownloadString("https://apis.datos.gob.ar/series/api/series/?ids=168.1_T_CAMBIOR_D_0_0_26&limit=5000&format=json");
                    }

                    CotizacionHistorica.CargarCotizacion(TxtCotizacionHistoria);
                }

            }
            catch (Exception ex)
            {
                Mensajes.Agregar(ex.Message);
                //return retorno;
            }
        }

        /// <summary>
        /// Devuelve el comprobante de pago
        /// </summary>
        /// <param name="path">Ruta de ubicación del pdf.</param>
        /// <returns></returns>
        public FileResult GetReport(string path)
        {
            string ReportURL = path;
            byte[] FileBytes = System.IO.File.ReadAllBytes(ReportURL);
            return File(FileBytes, "application/pdf");
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

        public ActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public ActionResult Privacy()
        {
            return View();
        }

        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public ActionResult Error()
        //{
        //    return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}

    }
}
