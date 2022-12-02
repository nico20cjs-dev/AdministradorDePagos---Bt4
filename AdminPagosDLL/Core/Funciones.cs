using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminPagosDLL.Models;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.IO;
using DatosDB;
using System.Configuration;

namespace AdminPagosDLL.Core
{
    public class Funciones
    {
        public List<Pago> lstModelos = new List<Pago>();
        public List<Pago> noVal = new List<Pago>();
        public FMensaje Mensajes = new FMensaje();

        #region Metodos Públicos

        private void LeerDirectorio()
        { 
        
        }

        public List<Pago> InterpretarPDF(string path = "")
        {
            int i = 0;
            
            try
            {
                //DatosDB.Class1 obj = new DatosDB.Class1();
                //var datos = obj.Leer();

                string rutaConfig = ConfigurationManager.AppSettings["RutaDePagos"];
                var directorio = !String.IsNullOrEmpty(rutaConfig) ? rutaConfig : @"D:\Norma\000   PAGOS\";

                //Obtener todos los archivos, de extención .pdf en todos los subdirectorios de ...
                var files = Directory.EnumerateFiles(directorio, "*.pdf", SearchOption.AllDirectories);
                int cantidadArchivos = files.Count();
                var lstArchivos = files.ToList();
                PdfReader reader = null;
                var format = new Formatos();

                for (i = 0; i < cantidadArchivos; i++)
                {
                    path = lstArchivos[i];

                    //if (i == 120 || path.Contains("2020-06-22 CEVIGE VTO"))
                    //{
                    //    break;
                    //}
                    
                    try
                    {
                        reader = new PdfReader(path);
                    }
                    catch (Exception ex)
                    {
                        //Si no se puede abrir puede tener pass
                        if (ex.Message.Contains("password"))
                        {

                        }
                        else
                        {
                            //InvalidPdfException
                        }

                        continue;
                    }
                    
                    string text = string.Empty;
                    
                    for (int page = 1; page < 2; page++)
                    {
                        bool leer = false;
                        text += PdfTextExtractor.GetTextFromPage(reader, page);

                        //TODO: optimizar, en vez de iterar x las lineas y leerlas para compararlas -> usar "text.Contein(textoABuscar)"
                        using (StringReader readerTxt = new StringReader(text))
                        {
                            string linea;
                            bool identificoTipo = false;

                            //Se crea el Pago en base al tipo de Pdf
                            while ((linea = readerTxt.ReadLine()) != null && !identificoTipo)
                            {
                                if (String.IsNullOrEmpty(linea)) continue;
                                
                                switch (linea)
                                {
                                    case "Pago efectuado":
                                    case "Pago Efectuado":
                                        //_pago = new PagoEfectuado();
                                        leer = true;
                                        identificoTipo = true;
                                        break;
                                    case "Pagos Realizados":
                                        //_pago = new PagoRealizado();
                                        leer = true;
                                        identificoTipo = true;
                                        break;

                                    //Las siguientes (x ahora) no se leen

                                    case "Transferencias a Cuentas de Tercero":
                                        //TODO: función para Transferencias
                                        leer = false;
                                        identificoTipo = true;
                                        break;
                                    case "Detalle de Movimientos":
                                        //TODO: función para Detalles de Movs del banco
                                        leer = false;
                                        identificoTipo = true;
                                        break;
                                        ;
                                    default:

                                        //Si es un recibo de sueldo de mamá, se busca el nro de legajo
                                        if (linea.Contains("265014"))
                                        {
                                            leer = false;
                                            identificoTipo = true;
                                        }

                                        break;
                                }
                            }
                        }

                        if (!leer) continue;

                        using (StringReader readerTxt = new StringReader(text))
                        {
                            var _pago = new Models.PagoEfectuado();

                            string lineaAnterior = "";
                            string line;

                            string auxFechaPago = "";
                            string auxHora = "";

                            int cantLineas = 0;

                            while ((line = readerTxt.ReadLine()) != null)
                            {
                                // Do something with the line
                                cantLineas++;

                                var lineaFormato = line.Split(':');
                                string clave = lineaFormato[0];
                                string valor = "";
                                valor = lineaFormato.Count() > 1 ? lineaFormato[1] : "";


                                switch (clave)
                                {
                                    case "Fecha de Pago":
                                        auxFechaPago = valor;

                                        if (!String.IsNullOrEmpty(auxFechaPago))
                                        {
                                            _pago.FechaPago = format.CrearFecha(auxFechaPago);
                                        }

                                        break;
                                    case "Hora":
                                        auxHora = valor;

                                        if (!String.IsNullOrEmpty(auxFechaPago))
                                        {
                                            //Le agrega la hora, minutos y segundos a la "Fecha de Pago"
                                            int hora = int.Parse(lineaFormato[1]);
                                            int min = int.Parse(lineaFormato[2]);
                                            int sec = int.Parse(lineaFormato[3]);

                                            _pago.FechaPago = _pago.FechaPago.AddHours(hora)
                                                                   .AddMinutes(min)
                                                                   .AddSeconds(sec);
                                        }

                                        break;
                                    case "Código de seguridad":
                                    case "Código de Seguridad":

                                        //Si es comprobante Simple
                                        if (!String.IsNullOrEmpty(valor))
                                        {
                                            _pago.NroTransaccion = valor;
                                        }
                                        else //Si es comprobante Completo
                                        {
                                            _pago.NroTransaccion = lineaAnterior;
                                        }

                                        break;
                                    case "Nro de Cliente":

                                        _pago.NroCliente = valor;

                                        //Si es comprobante Simple
                                        if (_pago.NroCliente.Equals("101122140030") && _pago.Ente.Equals("Buenos Aires- Municipalidad de"))
                                        {
                                            _pago.Ente = "Buenos Aires- Municipalidad de Villa Gesell";
                                        }

                                        break;
                                    case "Nro de cuenta débito":
                                        _pago.NroCtaDebito = valor;
                                        break;
                                    case "Importe":
                                        _pago.Importe = decimal.Parse(valor);
                                        break;
                                    case "Fecha de Vencimiento":
                                        //_pago.FechaVencimiento = format.CrearFecha(auxFechaPago);
                                        _pago.FechaVencimiento = format.CrearFecha(valor);
                                        break;
                                    case "Cuota":
                                    case "Cuota/Año":
                                        //006/17
                                        _pago.Cuota = valor;
                                        break;                                    
                                    case "Pago de":
                                        _pago.Ente = valor;
                                        break;
                                    case "abonado":


                                        // Buenos Aires - Municipalidad de Lanus
                                        if (lineaAnterior.Contains("Nombre del ente"))
                                        {
                                            var enteAux = lineaAnterior.Replace("Nombre del ente", "").Trim() + valor;

                                            enteAux = enteAux.Replace("Códigos de Pago que comiencen con 2", "").Trim();

                                            _pago.Ente = enteAux;
                                        }
                                        else
                                        {
                                            _pago.Ente = lineaAnterior;

                                            ////Buenos Aires- Arba Inmobiliario
                                            //if (lineaAnterior.Contains("Buenos Aires"))
                                            //{
                                            //    _pago.Ente = lineaAnterior;
                                            //
                                            //}
                                            //else
                                            //{
                                            //    _pago.Ente = valor;
                                            //}

                                        }

                                        break;
                                    default:
                                        break;
                                }

                                lineaAnterior = line;
                            }

                            //D:\Norma\000   PAGOS\18-08 - Arba NO SE.pdf

                            //Calcular valor en Dolares
                            _pago.ImporteDolar = (decimal)CotizacionHistorica.GetCotizacionPorFecha(_pago.FechaPago);

                            _pago.Path = path;

                            if (_pago.FechaPago.Day == 7 && _pago.FechaPago.Month == 9)
                            { 
                            
                            }

                            if (cantLineas > 8)
                            {
                                _pago.TipoComprobante = ETipoComprobante.Completo;
                            }
                            else
                            {
                                _pago.TipoComprobante = ETipoComprobante.Reducido;

                                //Se interpreta la fecha de vencimiento
                                if (_pago.FechaVencimiento == new DateTime() && !String.IsNullOrEmpty(_pago.Cuota))
                                {
                                    var variablesAux = _pago.Cuota.Split('/');
                                    var mesAux = int.Parse(variablesAux[0]);
                                    var añoAux = int.Parse(variablesAux[1]);
                                    if (añoAux < 2000) añoAux += 2000;

                                    try
                                    {
                                        var diaAux = DateTime.DaysInMonth(añoAux, mesAux);

                                        _pago.FechaVencimiento = new DateTime(añoAux, mesAux, diaAux);
                                    }
                                    catch (Exception)
                                    {
                                        //Si no se puede interpretar la fecha se toma la de pago
                                        _pago.FechaVencimiento = _pago.FechaPago;
                                    }
                                }
                            }

                            //Si se cargaron datos válidos
                            if (!String.IsNullOrEmpty(_pago.Ente) && _pago.FechaVencimiento != new DateTime() && _pago.Importe != 0)
                            {
                                lstModelos.Add(_pago);
                            }
                            else
                            {
                                noVal.Add(_pago);
                            }

                        }
                    }

                    reader.Close();
                }


            }
            catch (Exception ex)
            {
                //return lstModelos;
                Mensajes.Agregar(ex.Message + ". Indice: " + i + ". Archivo: " + path);
            }

            return lstModelos;
        }

        #endregion

    }
}
