using System.Collections.Generic;

namespace AdminPagosDLL.Models
{
    /// <summary>
    /// DTO para persistir el estado de los pagos procesados.
    /// Usa <see cref="PagoEfectuado"/> de forma explícita para evitar
    /// <see cref="Newtonsoft.Json.TypeNameHandling.Auto"/> y sus riesgos de seguridad.
    /// </summary>
    public class DatosSerializados
    {
        public List<PagoEfectuado> Pagos { get; set; }
        public List<PagoEfectuado> NoValidos { get; set; }
        public List<string> NoAbiertosPaths { get; set; }
        public List<string> NoIdentificadosPaths { get; set; }

        public DatosSerializados()
        {
            Pagos = new List<PagoEfectuado>();
            NoValidos = new List<PagoEfectuado>();
            NoAbiertosPaths = new List<string>();
            NoIdentificadosPaths = new List<string>();
        }
    }
}
