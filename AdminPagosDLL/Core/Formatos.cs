using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace AdminPagosDLL.Core
{
    public class Formatos
    {
        public FMensaje Mensajes = new FMensaje();

        // ─────────────────────────────────────────────────────────────────
        //  Formatos soportados — en orden de probabilidad descendente
        //  para que TryParseExact falle lo antes posible en los casos raros.
        //
        //  Fuentes observadas en los comprobantes:
        //    "08-02-2021"               → dd-MM-yyyy
        //    "08-02-2021 13:43:32"      → dd-MM-yyyy HH:mm:ss
        //    "01/01/2024"               → dd/MM/yyyy
        //    "01/01/2024 20:32:22"      → dd/MM/yyyy HH:mm:ss   ← nuevo
        // ─────────────────────────────────────────────────────────────────
        private static readonly string[] FormatosSoportados =
        {
            "dd-MM-yyyy HH:mm:ss",  // con guión + hora     (más frecuente)
            "dd-MM-yyyy",           // con guión sin hora
            "dd/MM/yyyy HH:mm:ss",  // con barra  + hora
            "dd/MM/yyyy",           // con barra  sin hora
            "dd/MM/yy",             // con barra  + año corto  (10/11/23 → 10/11/2023)
            "dd-MM-yy",             // con guión  + año corto
        };

        /// <summary>
        /// Convierte una cadena de texto en un <see cref="DateTime"/>.
        /// Soporta los separadores '-' y '/' con y sin componente horaria.
        /// Devuelve <see cref="DateTime.MinValue"/> si el texto es inválido.
        /// </summary>
        /// <param name="fecha">Texto a convertir (puede tener espacios extra).</param>
        /// <param name="formatosAdicionales">Uno o más formatos extra a intentar además de los predeterminados.</param>
        public DateTime CrearFecha(string fecha, params string[] formatosAdicionales)
        {
            if (string.IsNullOrWhiteSpace(fecha))
                return DateTime.MinValue;

            string texto = fecha.Trim();

            string[] formatos = (formatosAdicionales?.Length > 0)
                ? FormatosSoportados.Concat(formatosAdicionales).ToArray()
                : FormatosSoportados;

            if (DateTime.TryParseExact(
                    texto,
                    formatos,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime resultado))
            {
                return resultado;
            }

            // Fallback: intento cultural genérico (cubre formatos no anticipados)
            if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out resultado))
                return resultado;

            Logger.Add($"[Formatos.CrearFecha] No se pudo parsear la fecha: '{texto}'");
            return DateTime.MinValue;
        }

    }
}
