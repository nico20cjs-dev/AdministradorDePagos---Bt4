using AdminPagosDLL.Models;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace AdminPagosDLL.Core
{
    public class Funciones
    {
        public List<Pago> lstModelos = new List<Pago>();
        public List<Pago> noVal = new List<Pago>();
        public List<string> NoAbiertosPaths = new List<string>();
        public List<string> NoIdentificadosPaths = new List<string>();
        public int CantNoAbiertos { get; set; }
        public int CantNoIdentificados { get; set; }
        public FMensaje Mensajes = new FMensaje();
        private static readonly string ArchivoSerializacion = GetSerializacionPath();
        private static readonly ConcurrentDictionary<string, DateTime> _fileTimestamps = new ConcurrentDictionary<string, DateTime>();

        private static string GetSerializacionPath()
        {
            var dir = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            if (dir.Name.Equals("bin", StringComparison.OrdinalIgnoreCase))
                dir = dir.Parent;
            var appData = System.IO.Path.Combine(dir.FullName, "App_Data");
            // Limpiar serialización vieja (.xml) si existe
            var oldXml = System.IO.Path.Combine(appData, "pagos_serializados.xml");
            if (File.Exists(oldXml))
                try { File.Delete(oldXml); } catch { }
            return System.IO.Path.Combine(appData, "pagos_serializados.json");
        }

        private static DateTime? ObtenerFechaPdf(string fechaTxt)
        {
            if (string.IsNullOrEmpty(fechaTxt)) return null;

            // 1. Sanitizamos la cadena: quitamos el prefijo "D:" y las comillas simples del offset
            // "D:20190225182235-03'00'" -> "20190225182235-0300"
            string textoLimpio = fechaTxt.Replace("D:", "").Replace("'", "");

            // 2. Definimos los dos formatos posibles de metadatos PDF
            string[] formatos = {
                "yyyyMMddHHmmsszzz", // Con zona horaria (ej: 20190225182235-0300)
                "yyyyMMddHHmmss"     // Sin zona horaria (ej: 20241104104351)
            };

            // 3. Parseamos usando DateTimeOffset para no perder la precisión horaria
            if (DateTimeOffset.TryParseExact(textoLimpio, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset resultadoOffset))
            {
                // Retornamos la fecha adaptada a la hora local del servidor
                return resultadoOffset.LocalDateTime;
            }

            return null;
        }

        #region Metodos Públicos

        public List<Pago> CargarPagos(string path = "", bool forceReinterpret = false)
        {
            if (!forceReinterpret)
            {
                try
                {
                    var datos = DeSerializeDatos();
                    if (datos != null && datos.Pagos.Count > 0 && datos.Pagos[0] is PagoEfectuado)
                    {
                        lstModelos = datos.Pagos;
                        noVal = datos.NoValidos;
                        NoAbiertosPaths = datos.NoAbiertosPaths;
                        NoIdentificadosPaths = datos.NoIdentificadosPaths;
                        CantNoAbiertos = NoAbiertosPaths.Count;
                        CantNoIdentificados = NoIdentificadosPaths.Count;
                        return lstModelos;
                    }
                }
                catch
                {
                }
            }

            return InterpretarPDF(path);
        }

        public List<Pago> InterpretarPDF(string path = "")
        {
            int i = 0;
            
            try
            {
                var directorio = "";
                string defecto = @"D:\Norma\000   PAGOS\";
                string rutaConfig = ConfigurationManager.AppSettings["RutaDePagos"];
                directorio = !String.IsNullOrEmpty(rutaConfig) ? rutaConfig : defecto;

                // Validaciones

                try
                {
                    var acces = Directory.GetAccessControl(directorio);
                }
                catch (UnauthorizedAccessException)
                {
                    var msj = "La ruta parametrizada en el config (" + directorio + ") existe pero no se tiene acceso a ella. Revise que la misma no este en una carpeta de un usuario del sistema.";
                    Mensajes.Agregar(msj);
                    Logger.Add(msj);
                    return lstModelos;
                }
                catch
                {
                    var msj = "La ruta parametrizada en el config (" + directorio + ") tuvo un error al intentar acceder, puede que la misma no exista.";
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

                // Cargar estado previo para skip de archivos sin cambios
                DatosSerializados oldState = null;
                bool hasCache = _fileTimestamps.Count > 0;
                if (hasCache)
                {
                    try { oldState = DeSerializeDatos(); } catch { }
                    // Verificar que los objetos sean del tipo correcto (PagoEfectuado).
                    // Si el JSON fue guardado sin TypeNameHandling, se deserializan como Pago base.
                    if (oldState != null && oldState.Pagos.Count > 0 && !(oldState.Pagos[0] is PagoEfectuado))
                    {
                        oldState = null;
                    }
                }
                if (hasCache && oldState != null)
                {
                    lstModelos = oldState.Pagos;
                    noVal = oldState.NoValidos;
                    NoAbiertosPaths = oldState.NoAbiertosPaths;
                    NoIdentificadosPaths = oldState.NoIdentificadosPaths;
                }

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

                    string nombreArchivo = System.IO.Path.GetFileName(path);

                    // Skip archivos sin cambios si tenemos estado previo
                    if (hasCache && oldState != null && _fileTimestamps.TryGetValue(path, out var lastWrite) && lastWrite == File.GetLastWriteTimeUtc(path))
                    {
                        continue;
                    }

                    if (i == 991)
                    {

                    }
                    if (nombreArchivo.Contains("2020-03-09 VG EXP vto.5-3 detalle"))
                    {
                        
                    }
                    
                    try
                    {
                        reader = new PdfReader(path);
                    }
                    catch (Exception ex)
                    {
                        CantNoAbiertos++;
                        NoAbiertosPaths.Add(path);

                        continue;
                    }

                    string text = string.Empty;

                    int pageRead = 1;
                    int pageEnd = 2;

                    bool ifExpensasHY = false;
                    bool ifExpensasVG = false;
                    if (nombreArchivo.ToLower().Contains("expensa") &&
                        nombreArchivo.ToLower().Contains("san rafael"))
                    {

                        if (nombreArchivo.ToLower().Contains("movistar"))
                        {

                        }
                        else
                        {
                            ifExpensasHY = true;

                            if (reader.NumberOfPages == 1)
                            {
                                pageRead = 1;
                                pageEnd = 2;
                            }

                            else if (reader.NumberOfPages == 4)
                            {
                                pageRead = 3;
                                pageEnd = 4;
                            }

                            else if (reader.NumberOfPages == 5)
                            {
                                pageRead = 4;
                                pageEnd = 5;
                            }

                            else
                            {

                            }
                        }
                        
                    }
                    if (nombreArchivo.ToLower().Contains("gesell")
                        || nombreArchivo.ToLower().Contains(" vg "))
                    {
                        ifExpensasVG = true;

                        if (reader.NumberOfPages == 1)
                        {
                            pageRead = 1;
                            pageEnd = 2;
                        }

                        else if (reader.NumberOfPages == 3)
                        {
                            pageRead = 3;
                            pageEnd = 4;
                        }

                        else if (reader.NumberOfPages == 4)
                        {
                            pageRead = 4;
                            pageEnd = 5;
                        }

                        else
                        {

                        }
                    }

                    for (; pageRead < pageEnd; pageRead++)
                    {
                        bool leer = false;
                        text += PdfTextExtractor.GetTextFromPage(reader, pageRead);

                        //----------------
                        
                        // Nueva version

                        // Validar que el texto contenga alguna de las frases aprobadas para asegurarnos de que es un comprobante de pago
                        var frasesOk = new List<string> { "pago efectuado", "pagos realizados", "operación realizada con éxito", 
                            "5º-A" , "metrogas", "San Rafael", "CONSORCIO DE COPROPIETARIOS EDIFICIO", "ungar"
                        };
                        if (text != null && frasesOk.Any(frase => text.IndexOf(frase, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            leer = true;
                        }
                        else
                        {
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
                                        case "Pagos Realizados":
                                        case "Operación realizada con éxito":
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
                        }
                        
                        if (!leer)
                        {
                            CantNoIdentificados++;
                            NoIdentificadosPaths.Add(path);
                            continue;
                        }

                        using (StringReader readerTxt = new StringReader(text))
                        {
                            var _pago = new Models.PagoEfectuado();
                            _pago.Path = path;

                            string lineaAnterior = "";
                            string line;

                            string auxFechaPago = "";
                            string auxHora = "";

                            int cantLineas = 0;

                            //----------------
                            bool newFormatPdf = false;
                            try
                            {
                                string textoOriginal = null;

                                // 1. Intentamos leer "CreationDate". Si no existe, intentamos con "ModDate"
                                if (!reader.Info.TryGetValue("CreationDate", out textoOriginal))
                                {
                                    reader.Info.TryGetValue("ModDate", out textoOriginal);
                                }

                                // 2. Al llegar acá, 'textoOriginal' tendrá el valor de la primera que haya encontrado,
                                // o seguirá siendo 'null' si no existía ninguna de las dos en los metadatos.
                                DateTime? fecha = ObtenerFechaPdf(textoOriginal);

                                if (
                                    text.Contains("Operación realizada con éxito")
                                    //|| fecha.HasValue && fecha.Value.Year > 2022
                                )
                                {
                                    newFormatPdf = true;
                                }
                            }
                            catch (Exception ex)
                            {

                            }

                            //----------------

                            while ((line = readerTxt.ReadLine()) != null)
                            {
                                // Do something with the line
                                cantLineas++;

                                var lineaFormato = line.Split(':');
                                string clave = lineaFormato[0];
                                string valor = "";
                                valor = lineaFormato.Count() > 1 ? lineaFormato[1] : "";
                                valor = valor.Trim();

                                if (ifExpensasHY)
                                {
                                    try
                                    {
                                        if (!clave.Contains("016"))
                                        {
                                            continue;
                                        }

                                        if (clave.Contains("016"))
                                        {
                                            var valores = clave.Split(' ');
                                            _pago.Referencia = EReferencia.Norma;
                                            _pago.Ente = "Exp San Rafael";

                                            _pago.Importe = decimal.Parse(valores.Last().Replace("$", ""));                                            
                                            _pago.FechaPago = format.CrearFecha(nombreArchivo.Split(' ').FirstOrDefault());
                                            _pago.FechaVencimiento = _pago.FechaPago;
                                            break;
                                        }
                                    }
                                    catch (Exception )
                                    {

                                    }
                                }

                                switch (clave.Trim())
                                {
                                    case "Fecha de Transacción":
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
                                    case "Cod. Pago":

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
                                    case "NRO. DE CLIENTE":

                                        _pago.NroCliente = valor;

                                        if (!String.IsNullOrEmpty(_pago.Ente))
                                        {
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
                                        }

                                        
                                        break;
                                    case "Código/Usuario":
                                    case "Codigo/Usuario":
                                        _pago.NroCliente = valor;
                                        break;
                                    case "Nro de cuenta débito":
                                    case "Cuenta a debitar":
                                        _pago.NroCtaDebito = valor;
                                        break;
                                    case "Importe":
                                    case "IMPORTE":

                                        string patron = @"(?<transaccion>\$\s*\d+(?:\.\d{3})*,\d{2})\s+Número de transacción";

                                        Match match = Regex.Match(text, patron, RegexOptions.IgnoreCase);

                                        if (match.Success)
                                        {
                                            // Extrae el importe completo (ej: "$ 10.669,06")
                                            string importeCompleto = match.Groups["transaccion"].Value;
                                            _pago.Importe = decimal.Parse(importeCompleto.Replace("$", ""));
                                        }
                                        else
                                        {
                                            _pago.Importe = decimal.Parse(valor.Replace("$", ""));
                                        }

                                        
                                        break;
                                    case "Fecha de Vencimiento":
                                    case "VTO":
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

                                    case "Nombre Originante":
                                        if (valor.Contains("NORMA D AQUILA"))
                                        {
                                            _pago.Referencia = EReferencia.Norma;
                                        }
                                        break;
                                    case "Nombre del destinatario":

                                        if (valor.Contains("CONSORCIO DE COPROPIETARIOS EDIFICIO"))
                                        {
                                            _pago.Ente = "Exp Ungar";
                                        }
                                        break;

                                    default:

                                        if (newFormatPdf)
                                        {
                                            if (lineaAnterior == "Operación realizada con éxito")
                                            {
                                                _pago.FechaPago = format.CrearFecha(line);
                                            }
                                            if (line.Contains("Nombre del Ente Abonado"))
                                            {
                                                _pago.Ente = line.Trim().Replace("Nombre del Ente Abonado: ", "");
                                            }
                                        }

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
                                    case "20902705060": //Claro-Norma
                                    case "08620902705060": //Claro
                                    case "08620382717056": //Claro
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
                                    case "00250089457": //Arba-Velez
                                    case "3860000616796": //Aysa-Velez
                                    case "0000616796": //Aysa-Velez
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

                                    //TIA RENEE
                                    case "00904777178": //Edesur
                                        _pago.Referencia = EReferencia.TiaRenee;
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
                            else
                            {

                                if (_pago.Referencia == EReferencia.Desconocido)
                                {
                                    if (text.Contains("GESELL"))
                                    {
                                        _pago.Referencia = EReferencia.VillaGesell;
                                    }

                                    else if (text.Contains("UNGAR") ||
                                        text.Contains("EDIFICIO SAN MARTIN"))
                                    {
                                        _pago.Referencia = EReferencia.VillaGesell;
                                    }

                                    else if (text.Contains("20902705060")
                                        || nombreArchivo.ToLower().Contains("nico"))
                                    {
                                        _pago.Referencia = EReferencia.Nico;
                                    }

                                    else if (text.Contains("20902705060")
                                        || nombreArchivo.ToLower().Contains("nico"))
                                    {
                                        _pago.Referencia = EReferencia.Nico;
                                    }

                                    else if (text.Contains("30003392507"))
                                    {
                                        _pago.Referencia = EReferencia.TiaRaquel;
                                    }

                                    else if (text.Contains("2398966") ||
                                        text.Contains("30006245390") ||
                                        text.Contains("ADM San Rafael"))
                                    {
                                        _pago.Referencia = EReferencia.Norma;
                                    }

                                    else
                                    {
                                    
                                    }
                                }
                                

                                if (String.IsNullOrEmpty(_pago.Ente))
                                {
                                    string[] lineas = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                                    if (nombreArchivo.ToLower().Contains("bonaerenses"))
                                    {
                                        _pago.Ente = "Aguas BonaerenSes";
                                    }

                                    else if (nombreArchivo.ToLower().Contains("cevige"))
                                    {
                                        _pago.Ente = "Cevige";
                                    }

                                    else if (text.Contains("UNGAR") ||
                                        text.Contains("EDIFICIO SAN MARTIN"))
                                    {
                                        _pago.Ente = "Exp Ungar";

                                        if (reader.NumberOfPages == 1)
                                        {
                                            if (_pago.Importe == 0)
                                            {
                                                foreach (var linea in lineas)
                                                {
                                                    if (linea.StartsWith("SALDO A PAGAR"))
                                                    {
                                                        //string patron = @"\$\s*(?<importe>\d+\.\d{2})";
                                                        string patron = @"\$(?:\s|\u00A0)*(?<importe>\d+(?:,\d{3})*\.\d{2})";

                                                        Match match = Regex.Match(linea, patron, RegexOptions.IgnoreCase);

                                                        if (match.Success)
                                                        {
                                                            // Extrae el string del importe (ej: "10669.06")
                                                            string importeString = match.Groups["importe"].Value;

                                                            // Al tener formato internacional (punto para decimales), la conversión es directa con InvariantCulture
                                                            decimal importeDecimal = Convert.ToDecimal(importeString, System.Globalization.CultureInfo.InvariantCulture);
                                                            _pago.Importe = decimal.Parse(importeString.Replace("$", "").Replace(",", "").Replace(".", ","));
                                                        }
                                                        else
                                                        { 
                                                        
                                                        }
                                                        
                                                        break;
                                                    }
                                                }

                                            }

                                            if (_pago.FechaVencimiento == new DateTime())
                                            {
                                                int star = text.IndexOf("SALDO A PAGAR AL");
                                                if (star != -1)
                                                {
                                                    var fechaVto = text.Substring(star + 17, 10).Trim();
                                                    _pago.FechaVencimiento = format.CrearFecha(fechaVto);
                                                    _pago.FechaPago = _pago.FechaVencimiento;
                                                }
                                                else
                                                {
                                                    foreach (var fila in lineas)
                                                    {
                                                        if (fila.Contains("Fecha de la Transacció"))
                                                        {
                                                            string patron = @"Fecha de la Transacción:\s*(?<fecha>\d{2}/\d{2}/\d{4})";

                                                            Match match = Regex.Match(text, patron, RegexOptions.IgnoreCase);

                                                            if (match.Success)
                                                            {
                                                                _pago.FechaVencimiento = format.CrearFecha(match.Groups["fecha"].Value);
                                                                _pago.FechaPago = _pago.FechaVencimiento;
                                                            }
                                                        }
                                                    }
                                                }

                                            }
                                        }

                                        else if (reader.NumberOfPages == 3 || reader.NumberOfPages == 4)
                                        {
                                            if (_pago.Importe == 0)
                                            {

                                                // 1. Dividimos el texto completo extraído por iTextSharp en líneas individuales
                                                

                                                string importeEncontrado = "0,00";

                                                // 2. Buscamos el patrón exacto línea por línea
                                                // ^\s*6\s+D'AQUILA de forma estricta al inicio de la fila
                                                string patronImporte = @"(?<importe>\d+(?:\.\d{3})*,\d{2})\b\s*$";

                                                foreach (string linea in lineas)
                                                {
                                                    // Verificamos si la línea actual pertenece al Dpto 6 de D'AQUILA
                                                    if (linea.StartsWith("6 DPTO"))
                                                    {
                                                        // Una vez parados en la línea correcta, extraemos el ÚLTIMO importe de esa fila
                                                        Match matchImporte = Regex.Match(linea, patronImporte);
                                                        if (matchImporte.Success)
                                                        {
                                                            try
                                                            {
                                                                importeEncontrado = matchImporte.Groups["importe"].Value;
                                                                _pago.Importe = decimal.Parse(importeEncontrado);
                                                            }
                                                            catch (Exception ex)
                                                            {

                                                            }
                                                            break; // Ya lo encontramos, salimos del bucle
                                                        }
                                                        else
                                                        { 
                                                        
                                                        }
                                                        break;
                                                    }
                                                }
                                            }

                                            if (_pago.FechaVencimiento == new DateTime())
                                            {
                                                try
                                                {
                                                    int star = text.IndexOf("Vencimiento el día");
                                                    var fechaVto = text.Substring(star + 19, 10).Trim();
                                                    _pago.FechaVencimiento = format.CrearFecha(fechaVto);
                                                    _pago.FechaPago = _pago.FechaVencimiento;
                                                }
                                                catch (Exception ex)
                                                {

                                                }
                                            }
                                        }
                                    }

                                    else if (text.Contains("0001280445"))
                                    {
                                        _pago.Ente = "Edesur";
                                    }

                                    else if (text.Contains("2398966"))
                                    {
                                        _pago.Ente = "Telecentro";
                                    }

                                    else if (text.Contains("MetroGAS"))
                                    {
                                        _pago.Ente = "Metrogas";

                                        if (_pago.Importe == 0)
                                        {
                                            int star = text.IndexOf("TOTAL A PAGAR");
                                            var importe = (text.Length >= (star + 20)) ? text.Substring(star + 15, 10).Trim() : "Fuera de rango";
                                            _pago.Importe = decimal.Parse(importe.Replace("$", ""));
                                        }

                                        if (_pago.FechaVencimiento == new DateTime())
                                        {
                                            int star = text.IndexOf("FECHA DE VENCIMIENTO");
                                            var fechaVto = text.Substring(star + 22, 10).Trim();
                                            _pago.FechaVencimiento = format.CrearFecha(fechaVto);
                                            _pago.FechaPago = _pago.FechaVencimiento;
                                        }
                                    }

                                    else if (text.Contains("ADM San Rafael"))
                                    {
                                        _pago.Ente = "Exp San Rafael";
                                    }
                                }
                            }

                            if (_pago.Ente == "" && _pago.Referencia == EReferencia.Desconocido && _pago.Importe == 0)
                            { 
                                
                            }

                            //Calcular valor en Dolares
                            var valCotizacion = (decimal)CotizacionHistorica.GetCotizacionPorFecha(_pago.FechaPago);
                            if (valCotizacion != 0)
                            {
                                _pago.ImporteDolar = decimal.Round(_pago.Importe / valCotizacion, 2);
                            }                            
                            
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
                            if (!String.IsNullOrEmpty(_pago.Ente) || _pago.FechaVencimiento != new DateTime() || _pago.Importe != 0)
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

                    _fileTimestamps[path] = File.GetLastWriteTimeUtc(path);
                }

                // Limpiar entradas huérfanas (archivos que ya no existen)
                var currentFiles = new HashSet<string>(lstArchivos);
                foreach (var key in _fileTimestamps.Keys.ToList())
                {
                    if (!currentFiles.Contains(key))
                        _fileTimestamps.TryRemove(key, out _);
                }

            }
            catch (Exception ex)
            {
                //return lstModelos;
                Mensajes.Agregar(ex.Message + ". Indice: " + i + ". Archivo: " + path);
            }


            SerializarDatos();

            return lstModelos;
        }

        public void SerializarDatos()
        {
            try
            {
                var datos = new DatosSerializados
                {
                    Pagos = lstModelos,
                    NoValidos = noVal,
                    NoAbiertosPaths = NoAbiertosPaths,
                    NoIdentificadosPaths = NoIdentificadosPaths
                };

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ArchivoSerializacion));
                string json = JsonConvert.SerializeObject(datos, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });
                System.IO.File.WriteAllText(ArchivoSerializacion, json);
            }
            catch
            {
            }
        }

        public static DatosSerializados DeSerializeDatos()
        {
            if (!File.Exists(ArchivoSerializacion)) return null;

            try
            {
                string json = System.IO.File.ReadAllText(ArchivoSerializacion);
                return JsonConvert.DeserializeObject<DatosSerializados>(json, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });
            }
            catch
            {
                return null;
            }
        }

        public static void BorrarSerializacion()
        {
            try
            {
                if (File.Exists(ArchivoSerializacion))
                    File.Delete(ArchivoSerializacion);
                _fileTimestamps.Clear();
            }
            catch (Exception)
            {
            }
        }

        #endregion

    }
}
