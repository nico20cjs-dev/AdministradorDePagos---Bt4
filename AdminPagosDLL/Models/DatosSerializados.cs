using System.Collections.Generic;
using Newtonsoft.Json;

namespace AdminPagosDLL.Models
{
    public class DatosSerializados
    {
        public List<Pago> Pagos { get; set; }
        public List<Pago> NoValidos { get; set; }
        public List<string> NoAbiertosPaths { get; set; }
        public List<string> NoIdentificadosPaths { get; set; }

        public DatosSerializados()
        {
            Pagos = new List<Pago>();
            NoValidos = new List<Pago>();
            NoAbiertosPaths = new List<string>();
            NoIdentificadosPaths = new List<string>();
        }
    }
}
