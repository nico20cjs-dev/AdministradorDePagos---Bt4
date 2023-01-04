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
using System.Xml.Serialization;
using System.Xml;

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

        public List<Pago> CargarPagos(string path = "")
        {
            #region Deserealización

            try
            {
                //Si es la primera vez que se ejecuta el sitio se lee los pagos serializados
                var pagos = DeSerializeObject<List<Pago>>();
                if (pagos != null)
                {
                    return pagos;
                }                
            }
            catch (Exception ex)
            {

            }

            #endregion


            return InterpretarPDF(path);
        }

        public List<Pago> InterpretarPDF(string path = "")
        {
            int i = 0;
            
            try
            {
                //DatosDB.Class1 obj = new DatosDB.Class1();
                //var datos = obj.Leer();

                var directorio = "";
                string defecto = @"D:\Norma\000   PAGOS\";
                string rutaConfig = ConfigurationManager.AppSettings["RutaDePagos"];
                directorio = !String.IsNullOrEmpty(rutaConfig) ? rutaConfig : defecto;

                try
                {
                    var acces = Directory.GetAccessControl(rutaConfig);
                }
                catch (UnauthorizedAccessException)
                {
                    var msj = "La ruta parametrizada en el config (" + rutaConfig + ") existe pero no se tiene acceso a ella. Revise que la misma no este en una carpeta de un usuario del sistema.";
                    Mensajes.Agregar(msj);
                    Logger.Add(msj);
                    return lstModelos;
                }
                catch
                {
                    var msj = "La ruta parametrizada en el config (" + rutaConfig + ") tuvo un error al intentar acceder, puede que la misma no exista.";
                    Mensajes.Agregar(msj);
                    Logger.Add(msj);
                    return lstModelos;
                }

                ////Validacion de directorio
                //Logger.Add("Existe rutaConfig?");
                //if (!Directory.Exists(rutaConfig))
                //{
                //    Logger.Add("NO existe");
                //    directorio = defecto;
                //}

                Logger.Add("\n Directorio a leer: " + directorio);

                //Obtener todos los archivos, de extención .pdf en todos los subdirectorios de ...
                var files = Directory.EnumerateFiles(directorio, "*.pdf", SearchOption.AllDirectories);
                int cantidadArchivos = files.Count();
                var lstArchivos = files.ToList();
                PdfReader reader = null;
                var format = new Formatos();

                //ArchivosLeidos
                int cantidadArchivosAux = cantidadArchivos;
                string filesReaded = ConfigurationManager.AppSettings["ArchivosLeidos"];
                if (cantidadArchivos != 0 &&!String.IsNullOrEmpty(filesReaded))
                {
                    if (int.TryParse(filesReaded, out int aux) && int.Parse(filesReaded) > 0)
                    {
                        cantidadArchivosAux = int.Parse(filesReaded);
                    }
                }

                for (i = 0; i < cantidadArchivosAux; i++)
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
                                valor = valor.Trim();

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

                                        if (!_pago.Ente.Contains("Lanus") &&
                                            _pago.Ente.Contains("Buenos Aires- Municipalidad de"))
                                        { 
                                        
                                        }

                                        //Si es comprobante Simple se completa el nombre del Ente
                                        if (_pago.Ente.Equals("Buenos Aires- Municipalidad de"))
                                        {
                                            if (_pago.NroCliente.Equals("0000000000104243024")) // muni-Yrigoyen
                                            {
                                                _pago.Ente += " Lanus";
                                            }
                                            if (_pago.NroCliente.Equals("0000000000445015012")) //muni-raquel
                                            {
                                                _pago.Ente += " Lanus";
                                            }
                                            if (_pago.NroCliente.Equals("101122140030"))
                                            {
                                                _pago.Ente += " Villa Gesell";
                                            }
                                        }
                                        break;
                                    case "Código/Usuario":
                                        _pago.NroCliente = valor;
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


                                        //Buenos Aires - Municipalidad de Lanus
                                        //Buenos Aires - Municipalidad de Villa Gesell
                                        if (lineaAnterior.Contains("Nombre del ente"))
                                        {
                                            var enteAux = lineaAnterior.Replace("Nombre del ente", "").Trim() + " " + valor;

                                            enteAux = enteAux.Replace("Códigos de Pago que comiencen con 2", "").Trim();

                                            _pago.Ente = enteAux.Trim();
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

                            //Setea la referencia, ej "Tia Raquel"; "Gesell"
                            if (!String.IsNullOrEmpty(_pago.NroCliente))
                            {
                                switch (_pago.NroCliente.Trim())
                                {
                                    //Pendientes de saber de quien son
                                    case "20902705060": //Claro
                                    case "08620902705060": //Claro
                                    case "08620382717056": //Claro
                                    case "00904777178": //Edesur                                        
                                        break;

                                    //NICO
                                    case "0001280444":
                                    case "00901280444":
                                        _pago.Referencia = EReferencia.Nico;
                                        break;

                                    //NORMA
                                    case "08620199489345": //Claro-Norma
                                    case "20199489345": //Claro-Norma
                                        _pago.Referencia = EReferencia.Norma;
                                        break;

                                    //VELEZ
                                    case "00100250089457": //Arba-Velez
                                    case "3860000616796": //Aysa-Velez
                                    case "0001280445": //Edesur-Velez
                                    case "00901280445": //Edesur-Velez
                                    case "29820144117001": //Metrogas-Velez (antes que se saque en el '18)
                                    case "0000000000402052000": //Municimal-Velez
                                        _pago.Referencia = EReferencia.VelezSarsfield;
                                        break;

                                    //YRIGOYEN
                                    case "00100251361487": //Arba-Yrigoyen
                                    case "00904022218": //Edesur-Yrigoyen
                                    case "0004022218": //Edesur-Yrigoyen
                                    case "00904675863": //Edesur-Yrigoyen
                                    case "030006245390": //Metrogas-Yrigoyen
                                    case "29830006245390": //Metrogas-Yrigoyen
                                    case "29820144118800": //Metrogas-Yrigoyen
                                    case "20144118800": //Metrogas-Yrigoyen
                                    case "0000000000104243024": //Municimal-Yrigoyen
                                        _pago.Referencia = EReferencia.Yrigoyen;
                                        break;

                                    //TIA RAQUEL
                                    case "00903846727": //Edesur-TiaRaquel
                                    case "0003846727": //Edesur-TiaRaquel
                                    case "030003392507": //Metrogas-TiaRaquel
                                    case "0290184409": //Movistar-TiaRaquel
                                    case "0000000000445015012": //Municimal-TiaRaquel
                                        _pago.Referencia = EReferencia.TiaRaquel;
                                        break;

                                    //GESELL
                                    case "0001392230": //AguasBonaerences-Gesell
                                    case "00101250019153": //Arba-Gesell
                                    case "01250019153": //Arba-Gesell
                                    case "0005820": //Cevige-Gesell
                                    case "101122140030": //Municipal-Gesell
                                    case "901043090065": //Municipal-Gesell
                                        _pago.Referencia = EReferencia.VillaGesell;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            
                            //D:\Norma\000   PAGOS\18-08 - Arba NO SE.pdf

                            //Calcular valor en Dolares
                            var valCotizacion = (decimal)CotizacionHistorica.GetCotizacionPorFecha(_pago.FechaPago);
                            if (valCotizacion != 0)
                            {
                                _pago.ImporteDolar = decimal.Round(_pago.Importe / valCotizacion, 2);
                            }                            
                            

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


            SerializePagos(lstModelos);

            return lstModelos;
        }

        /// <summary>
        /// Serializa los pagos.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject"></param>
        public void SerializePagos<T>(T serializableObject)
        {
            if (serializableObject == null) { return; }

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
                //XmlSerializer serializer = new XmlSerializer(typeof(Pago), new Type[] { typeof(Pago) });
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.Serialize(stream, serializableObject);
                    stream.Position = 0;
                    xmlDocument.Load(stream);
                    xmlDocument.Save(ConfigurationManager.AppSettings["PagosSerializados"]);
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }
        }


        /// <summary>
        /// Deserializes an xml file into an object list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T DeSerializeObject<T>()
        {
            //if (string.IsNullOrEmpty(fileName)) { return default(T); }

            T objectOut = default(T);
            string fileName = ConfigurationManager.AppSettings["PagosSerializados"];

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(fileName);
                string xmlString = xmlDocument.OuterXml;

                using (StringReader read = new StringReader(xmlString))
                {
                    Type outType = typeof(T);

                    XmlSerializer serializer = new XmlSerializer(outType);
                    using (XmlReader reader = new XmlTextReader(read))
                    {
                        objectOut = (T)serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                //Log exception here
            }

            return objectOut;
        }

        public static void BorrarSerializacion()
        {
            try
            {
                string fileName = ConfigurationManager.AppSettings["PagosSerializados"];
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
            catch (Exception)
            {

            }
        }

        #endregion

    }
}
