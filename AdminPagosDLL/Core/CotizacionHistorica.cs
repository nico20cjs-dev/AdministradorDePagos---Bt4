using System;
using System.Collections.Concurrent;

namespace AdminPagosDLL.Core
{
    public static class CotizacionHistorica
    {

        public static FMensaje Mensajes = new FMensaje();
        static ConcurrentDictionary<string, double> CacheBD = new ConcurrentDictionary<string, double>();

        public static void CargarCotizacion(string jsonHistorico)
        {            
            dynamic jo = Newtonsoft.Json.Linq.JObject.Parse(jsonHistorico);
            var items = jo.data;

            CacheBD.Clear();
            foreach (var item in items)
            {
                string fecha = item.First.Value;
                double cotizacion = item.Last.Value;

                CacheBD.TryAdd(fecha, cotizacion);
            }

        }

        public static double GetCotizacionPorFecha(DateTime fecha)
        {
            string fechaAux = fecha.ToString("yyyy-MM-dd");
            return GetCotizacionPorFecha(fechaAux);
        }

        public static double GetCotizacionPorFecha(string fecha)
        { 
            return CacheBD.TryGetValue(fecha, out double cotizacion) ? cotizacion : 0;
        }

    }
}
