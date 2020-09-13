using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdminPagosDLL.Core
{
    public static class CotizacionHistorica
    {

        public static FMensaje Mensajes = new FMensaje();
        static Dictionary<string, double> CacheBD = new Dictionary<string, double>();

        #region Métodos Públicos

        public static void CargarCotizacion(string jsonHistorico)
        {            
            dynamic jo = Newtonsoft.Json.Linq.JObject.Parse(jsonHistorico);
            var items = jo.data;
            foreach (var item in items)
            {
                string fecha = item.First.Value;
                double cotizacion = item.Last.Value;

                CacheBD[fecha] = cotizacion;
            }

        }

        public static double GetCotizacionPorFecha(DateTime fecha)
        {
            string fechaAux = fecha.ToString("yyyy-MM-dd");
            return GetCotizacionPorFecha(fechaAux);
        }

        public static double GetCotizacionPorFecha(string fecha)
        { 
            return CacheBD.ContainsKey(fecha) ? CacheBD[fecha] : 0;
        }

        #endregion

    }
}
