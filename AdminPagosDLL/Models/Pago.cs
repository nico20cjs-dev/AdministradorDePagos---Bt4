﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AdminPagosDLL.Models
{
    [XmlInclude(typeof(PagoEfectuado))]
    [XmlInclude(typeof(PagoRealizado))]
    [Serializable]
    public class Pago
    {

        #region Atributos

        //private DateTime _FechaPago = new DateTime();
        //private string _NroTransaccion = null;
        //private string _Ente = "";
        //private string _NroCliente = "";
        //private string _NroCtaDebito = "";
        //private decimal _Importe = 0;
        //private DateTime _FechaVencimiento = new DateTime();
        //private string _Cuota = "";
        //private string _Referencia = "";
        //private string _Concepto = "";
        //
        #endregion

        #region Propiedades

        public DateTime FechaPago { get; set; }
        public decimal Importe { get; set; }

        public decimal ImporteDolar { get; set; }

        public decimal ImporteEnDolares { get; set; }

        #endregion

        public Pago() { }
    }
}
