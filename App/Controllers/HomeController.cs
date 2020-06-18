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

namespace AdminPagosDLL.Controllers
{
    public class HomeController : Controller
    {
        public FMensaje Mensajes = new FMensaje();

        public ActionResult Index()
        {

            ViewBag.data = "Hola Mundo!";

            return View();
        }

        private string ConvertDataTableToHTML(DataTable dt)
        {
            string html = "<table>";
            //add header row
            html += "<tr>";
            for (int i = 0; i < dt.Columns.Count; i++)
                html += "<td>" + dt.Columns[i].ColumnName + "</td>";
            html += "</tr>";
            //add rows
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                html += "<tr>";
                for (int j = 0; j < dt.Columns.Count; j++)
                    html += "<td>" + dt.Rows[i][j].ToString() + "</td>";
                html += "</tr>";
            }
            html += "</table>";
            return html;
        }

        private string ConvertListPagosToHTML(List<Pago> lstPagos)
        {
            string html = "<tbody>";
            //add header row
            //html += "<tr>";
            //for (int i = 0; i < lstPagos.Count; i++)
            //{
            //    html += "<td>" + "PAGO" + "</td>";
            //}
            //html += "</tr>";
            //add rows
            for (int i = 0; i < lstPagos.Count; i++)
            {
                html += "<tr>";
                html += "    <td>" + lstPagos[i].Importe.ToString() + "</td>";
                html += "</tr>";
            }
            html += "</tbody>";
            return html;
        }

        public FileResult GetReport(string path)
        {
            string ReportURL = path;
            byte[] FileBytes = System.IO.File.ReadAllBytes(ReportURL);
            return File(FileBytes, "application/pdf");
        }

        public JsonResult LeerPDF()
        {
            Mensajes.Limpiar();
            var retorno = Json(new { Mensajes }, JsonRequestBehavior.AllowGet);
            try
            {
                var funcion = new Funciones();
                var pagos = new List<Pago>();
                pagos = funcion.InterpretarPDF();
                if (funcion.Mensajes.Lista.Any())
                {
                    Mensajes.Agregar(funcion.Mensajes.Lista);
                    return retorno;
                }

                return Json(new { Mensajes, pagos }, JsonRequestBehavior.AllowGet);

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
