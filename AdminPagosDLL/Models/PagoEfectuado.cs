using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdminPagosDLL.Models
{
    [Serializable]
    public class PagoEfectuado : Pago
    {

        public string NroTransaccion { get; set; }
        public string Ente { get; set; }
        public string NroCliente { get; set; }
        public string NroCtaDebito { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string Cuota { get; set; }
        //public string Referencia { get; set; }
        public string Concepto { get; set; }
        public string Path { get; set; }
        public ETipoComprobante TipoComprobante { get; set; }
        public EReferencia Referencia { get; set; }

        public PagoEfectuado() { }
    }
}
