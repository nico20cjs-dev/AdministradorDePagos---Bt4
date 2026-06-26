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

        /// <summary>
        /// Obtiene la cotización para la primera fecha inmediatamente anterior con valor
        /// a partir de una fecha dada. Si no existe ninguna fecha anterior, devuelve 0.
        /// </summary>
        public static double GetCotizacionAnterior(DateTime fecha)
        {
            if (fecha == new DateTime()) return 0;

            string fechaStr = fecha.ToString("yyyy-MM-dd");

            // Intento exacto primero
            if (CacheBD.TryGetValue(fechaStr, out double cotizacion))
                return cotizacion;

            // Busca la fecha más cercana anterior en el diccionario
            string anterior = null;
            foreach (var key in CacheBD.Keys)
            {
                if (string.Compare(key, fechaStr, StringComparison.Ordinal) < 0)
                {
                    if (anterior == null || string.Compare(key, anterior, StringComparison.Ordinal) > 0)
                    {
                        anterior = key;
                    }
                }
            }

            if (anterior != null && CacheBD.TryGetValue(anterior, out cotizacion))
                return cotizacion;

            return 0;
        }

    }
}
