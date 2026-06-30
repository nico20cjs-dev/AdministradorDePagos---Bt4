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
using System.Threading.Tasks;

namespace AdminPagosDLL.Core
{
    public class Funciones
    {
        public List<PagoEfectuado> lstModelos = new List<PagoEfectuado>();
        public List<PagoEfectuado> noVal = new List<PagoEfectuado>();
        public List<string> NoAbiertosPaths = new List<string>();
        public List<string> NoIdentificadosPaths = new List<string>();
        public int CantNoAbiertos { get; set; }
        public int CantNoIdentificados { get; set; }
        public FMensaje Mensajes = new FMensaje();
        private static readonly string ArchivoSerializacion = GetSerializacionPath();
        private static readonly ConcurrentDictionary<string, DateTime> _fileTimestamps = new ConcurrentDictionary<string, DateTime>();

        private static readonly Regex _regexEspacios = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex _regexImporteTransaccion = new Regex(@"(?<transaccion>\$\s*\d+(?:\.\d{3})*,\d{2})\s+Número de transacción", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _regexSaldoAPagar = new Regex(@"\$(?:\s|\u00A0)*(?<importe>\d+(?:,\d{3})*\.\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _regexFechaTransaccion = new Regex(@"Fecha de la Transacción:\s*(?<fecha>\d{2}/\d{2}/\d{4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _regexImporteUngar = new Regex(@"(?<importe>\d+(?:\.\d{3})*,\d{2})\b\s*$", RegexOptions.Compiled);
        private static readonly Regex _regexVencimientoImporte = new Regex(@"(\d{2}/\d{2}/\d{4})\s+([\d\.]+,\d{2})", RegexOptions.Compiled);

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

        /// <summary>
        /// Identifica el Ente correspondiente del texto de manera inteligente.
        /// Tolera variaciones de mayúsculas, espacios extras, y palabras adicionales.
        /// Ejemplo: "Buenos Aires- Arba Inmobiliario" → EEnte.Arba
        /// </summary>
        /// <param name="line">Línea de texto a analizar</param>
        /// <returns>EEnte identificado o EEnte.Desconocido si no encuentra coincidencia</returns>
        internal EEnte IdentifyEnte(string line, PagoEfectuado pago = null, string text = null)
        {
            if (string.IsNullOrWhiteSpace(line))
                return EEnte.Desconocido;

            // Reemplazo de posibles textos
            line = line.Replace("Nombre del Ente Abonado:", "")
                .Replace("Nombre del Ente Abonado", "")
                .Replace("PAGO DE ", "")
                .Trim();

            var cleanText = line.ToLower().Trim();

            // Casos fijos que se sabe que se dan bastante seguido
            if (cleanText == "claro")
            {
                return EEnte.Claro;
            }
            else if (cleanText == "AGUAS BONAERENSES".ToLower() || cleanText == "aysa" || cleanText == "agua y saneamientos argentinos")
            {
                return EEnte.AguasBonaerenses;
            }
            else if (cleanText == "Buenos Aires- Arba Inmobiliario".ToLower())
            {
                return EEnte.Arba;
            }
            else if (cleanText == "buenos aires- municipalidad de lanus")
            {
                return EEnte.MunicipalidadLanus;
            }
            else if (cleanText == "Edesur".ToLower())
            {
                return EEnte.Edesur;
            }
            else if (cleanText == "metrogas".ToLower())
            {
                return EEnte.Metrogas;
            }
            else if (cleanText == "cablevision".ToLower())
            {
                return EEnte.Cablevision;
            }

            else if (!String.IsNullOrEmpty(text) && text.Contains("CABLEVISION") &&
                pago != null && pago.Path.ToLower().Contains("raquel"))
            {
                return EEnte.Cablevision;
            }

            // Intenta parse directo primero
            foreach (EEnte ente in Enum.GetValues(typeof(EEnte)))
            {
                if (ente == EEnte.Desconocido)
                    continue;

                if (cleanText.Contains(ente.ToString().ToLower()))
                    return ente;
            }

            // Si no encuentra coincidencia exacta, busca palabras clave

            // Limpiar el texto: trim, espacios múltiples, convertir a minúsculas
            cleanText = _regexEspacios.Replace(line.Trim(), " ");

            // Diccionario de palabras clave por Ente
            var enteKeywords = new Dictionary<EEnte, string[]>
            {
                { EEnte.Arba, new[] { "arba" } },
                { EEnte.AguasBonaerenses, new[] { "aguas bonaerenses", "aguas", "agua bonaerense" } },
                { EEnte.AySA, new[] { "aysa", "ays a" } },
                { EEnte.ExpSanRafael, new[] { "san rafael", "expensas lanus", "exp san rafael" } },
                { EEnte.ExpUngar, new[] { "ungar", "exp ungar", "expensas ungar" } },
                { EEnte.Edesur, new[] { "edesur" } },
                { EEnte.Cevige, new[] { "cevige" } },
                { EEnte.Metrogas, new[] { "metrogas", "metro gas" } },
                { EEnte.MunicipalidadLanus, new[] { "municipalidad de lanus", "lanus", "municipalidad lanus" } },
                { EEnte.MunicipalidadVillaGesell, new[] { "municipalidad de villa gesell", "villa gesell", "municipalidad villa gesell" } },
                { EEnte.Claro, new[] { "claro" } },
                { EEnte.Movistar, new[] { "movistar" } }
            };

            // Buscar coincidencias (primero las más específicas, luego las genéricas)
            var matches = enteKeywords
                .Where(kvp => kvp.Value.Any(keyword => cleanText.Contains(keyword)))
                .OrderByDescending(kvp => kvp.Value.Max(k => k.Length)) // Priorizar coincidencias más largas
                .FirstOrDefault();

            return matches.Key != default(EEnte) ? matches.Key : EEnte.Desconocido;
        }

        /// <summary>
        /// Extrae el Ente de una línea que contiene "Nombre del Ente Abonado: [texto]"
        /// </summary>
        internal EEnte ExtractEnteFromLine(string line, PagoEfectuado pago = null, string text = null)
        {
            //if (!line.Contains("Nombre del Ente Abonado"))
            //    return EEnte.Desconocido;

            var enteText = line
                .Replace("Nombre del Ente Abonado:", "")
                .Replace("Nombre del Ente Abonado", "")
                .Trim();

            return IdentifyEnte(enteText, pago, text);
        }

        internal static readonly Dictionary<string, (EEnte Ente, EReferencia Referencia)> ClienteMap =
            new Dictionary<string, (EEnte, EReferencia)>
            {
                { "0000000000104243024", (EEnte.MunicipalidadLanus, EReferencia.Yrigoyen) },
                { "00100251361487", (EEnte.Arba, EReferencia.Yrigoyen) },
                { "00251361487", (EEnte.Arba, EReferencia.Yrigoyen) },
                { "030006245390", (EEnte.Metrogas, EReferencia.Yrigoyen) },
                { "0004022218", (EEnte.Edesur, EReferencia.Yrigoyen) },
                { "00904022218", (EEnte.Edesur, EReferencia.Yrigoyen) },
                { "00904675863", (EEnte.Edesur, EReferencia.Yrigoyen) },
                { "29830006245390", (EEnte.Metrogas, EReferencia.Yrigoyen) },
                { "29820144118800", (EEnte.Metrogas, EReferencia.Yrigoyen) },
                { "20144118800", (EEnte.Metrogas, EReferencia.Yrigoyen) },
                { "24619974533928", (EEnte.Telefonica, EReferencia.Yrigoyen) },
                { "4002036838564", (EEnte.Telefonica, EReferencia.Yrigoyen) },

                { "101122140030", (EEnte.MunicipalidadVillaGesell, EReferencia.VillaGesell) },
                { "0001392230", (EEnte.AguasBonaerenses, EReferencia.VillaGesell) },
                { "0005820", (EEnte.Cevige, EReferencia.VillaGesell) },
                { "901043090065", (EEnte.MunicipalidadVillaGesell, EReferencia.VillaGesell) },
                { "00101250019153", (EEnte.Arba, EReferencia.VillaGesell) },
                { "01250019153", (EEnte.Arba, EReferencia.VillaGesell) },

                { "20199489345", (EEnte.Claro, EReferencia.Norma) },
                { "08620199489345", (EEnte.Claro, EReferencia.Norma) },

                { "20902705060", (EEnte.Claro, EReferencia.Nico) },
                { "08620902705060", (EEnte.Claro, EReferencia.Nico) },
                { "0001280444", (EEnte.Edesur, EReferencia.Nico) },
                { "00901280444", (EEnte.Edesur, EReferencia.Nico) },

                { "00100250089457", (EEnte.Arba, EReferencia.VelezSarsfield) },
                { "00250089457", (EEnte.Arba, EReferencia.VelezSarsfield) },
                { "3860000616796", (EEnte.AySA, EReferencia.VelezSarsfield) },
                { "0000616796", (EEnte.AySA, EReferencia.VelezSarsfield) },
                { "0001280445", (EEnte.Edesur, EReferencia.VelezSarsfield) },
                { "00901280445", (EEnte.Edesur, EReferencia.VelezSarsfield) },
                { "29820144117001", (EEnte.Metrogas, EReferencia.VelezSarsfield) },
                { "0000000000402052000", (EEnte.MunicipalidadLanus, EReferencia.VelezSarsfield) },

                { "0003846727", (EEnte.Edesur, EReferencia.TiaRaquel) },
                { "00903846727", (EEnte.Edesur, EReferencia.TiaRaquel) },
                { "030003392507", (EEnte.Metrogas, EReferencia.TiaRaquel) },
                { "0290184409", (EEnte.Movistar, EReferencia.TiaRaquel) },
                { "0000000000445015012", (EEnte.MunicipalidadLanus, EReferencia.TiaRaquel) },
                { "1003169065010001", (EEnte.Telefonica, EReferencia.TiaRaquel) },
                { "0564453739", (EEnte.Telefonica, EReferencia.TiaRaquel) },

                { "00904777178", (EEnte.Edesur, EReferencia.TiaRenee) },

                { "08620382717056", (EEnte.Claro, EReferencia.Guille) }
            };

        internal bool TryMapClienteData(string nroCliente, out (EEnte Ente, EReferencia Referencia) clienteData)
        {
            return ClienteMap.TryGetValue(nroCliente, out clienteData);
        }

        public static string GetEnteDisplayText(EEnte ente)
        {
            switch (ente)
            {
                case EEnte.Desconocido: return "Desconocido";
                case EEnte.Arba: return "\U0001F4B0 Arba";
                case EEnte.AguasBonaerenses: return "\U0001F4A7 Aguas Bonaerenses";
                case EEnte.AySA: return "\U0001F4A7 AySA";
                case EEnte.ExpSanRafael: return "\U0001F3E0 Expensas Lanus";
                case EEnte.ExpUngar: return "\U0001F3E0 Ungar VG";
                case EEnte.Edesur: return "\u26A1 Edesur";
                case EEnte.Cevige: return "\u26A1 Cevige";
                case EEnte.Metrogas: return "\U0001F525 Metrogas";
                case EEnte.MunicipalidadLanus: return "\U0001F3DB\uFE0F Municip. Lanus";
                case EEnte.MunicipalidadVillaGesell: return "\U0001F3DB\uFE0F Municip. Villa Gesell";
                case EEnte.Claro: return "\U0001F4DE Claro";
                case EEnte.Movistar: return "\U0001F4DE Movistar";
                case EEnte.Telecentro: return "\U0001F4DE Telecentro";
                case EEnte.Telefonica: return "\U0001F4DE Telefonica";
                default: return ente.ToString();
            }
        }

        #region Metodos Públicos

        public List<Pago> CargarPagos(string path = "", bool forceReinterpret = false)
        {
            if (!forceReinterpret)
            {
                try
                {
                    var datos = DeSerializeDatos();
                    if (datos != null && datos.Pagos.Count > 0)
                    {
                        lstModelos = datos.Pagos;
                        noVal = datos.NoValidos;
                        NoAbiertosPaths = datos.NoAbiertosPaths;
                        NoIdentificadosPaths = datos.NoIdentificadosPaths;
                        CantNoAbiertos = NoAbiertosPaths.Count;
                        CantNoIdentificados = NoIdentificadosPaths.Count;
                        return lstModelos.Cast<Pago>().ToList();
                    }
                }
                catch
                {
                }
            }

            return InterpretarPDF(path).Cast<Pago>().ToList();
        }

        public List<PagoEfectuado> InterpretarPDF(string path = "")
        {
            int i = 0;

            try
            {
                string directorio = ObtenerDirectorio();
                if (directorio == null)
                    return lstModelos;

                var lstArchivos = ObtenerArchivosPdf(directorio);
                int cantidadArchivos = lstArchivos.Count;

                var oldState = CargarEstadoPrevio(out bool hasCache);
                var format = new Formatos();
                int cantidadArchivosAux = ObtenerLimiteArchivos(cantidadArchivos);

                for (i = 0; i < cantidadArchivosAux; i++)
                {
                    path = lstArchivos[i];

                    string nombreArchivo = System.IO.Path.GetFileName(path);

                    if (path.Contains("Norma\\000   PAGOS\\X X X X X X X X X\\tia CONCE\\2022\\METROGAS\\2022-05-26 Metrogas vto.26-5.pdf"))
                    { 
                    
                    }

                    // Skip archivos sin cambios si tenemos estado previo
                    if (hasCache && oldState != null && _fileTimestamps.TryGetValue(path, out var lastWrite) && lastWrite == File.GetLastWriteTimeUtc(path))
                    {
                        continue;
                    }

                    try
                    {
                        PdfReader reader;
                        try
                        {
                            reader = new PdfReader(path);
                        }
                        catch (Exception ex)
                        {
                            CantNoAbiertos++;
                            NoAbiertosPaths.Add(path);
                            Logger.Add($"No se pudo abrir el PDF {path}: {ex.Message}");

                            continue;
                        }

                        using (reader)
                        {
                            string text = string.Empty;
                            var (pageRead, pageEnd, ifExpensasHY) = ObtenerRangoPaginas(reader, nombreArchivo, path);

                            for (; pageRead < pageEnd; pageRead++)
                            {
                                text += PdfTextExtractor.GetTextFromPage(reader, pageRead);

                                if (!EsComprobantePago(text, nombreArchivo))
                                {
                                    CantNoIdentificados++;
                                    NoIdentificadosPaths.Add(path);
                                    continue;
                                }

                                var pago = ProcesarTextoComprobante(text, path, nombreArchivo, reader, format, ifExpensasHY);
                                AgregarPago(pago);
                            }
                        }

                        _fileTimestamps[path] = File.GetLastWriteTimeUtc(path);
                    }
                    catch (Exception ex)
                    {
                        Logger.Add($"Error procesando {path}: {ex.Message}");
                        Mensajes.Agregar($"Error procesando {path}: {ex.Message}");
                    }
                }

                LimpiarTimestampsHuérfanos(lstArchivos);
            }
            catch (Exception ex)
            {
                Logger.Add($"Error general en InterpretarPDF. Indice: {i}. Archivo: {path}. {ex.Message}");
                Mensajes.Agregar($"Error general en InterpretarPDF. Indice: {i}. Archivo: {path}. {ex.Message}");
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
                string json = JsonConvert.SerializeObject(datos);
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
                return JsonConvert.DeserializeObject<DatosSerializados>(json);
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

        public async Task<List<Pago>> CargarPagosAsync(string path = "", bool forceReinterpret = false)
        {
            if (!forceReinterpret)
            {
                try
                {
                    var datos = await DeSerializeDatosAsync();
                    if (datos != null && datos.Pagos.Count > 0)
                    {
                        lstModelos = datos.Pagos;
                        noVal = datos.NoValidos;
                        NoAbiertosPaths = datos.NoAbiertosPaths;
                        NoIdentificadosPaths = datos.NoIdentificadosPaths;
                        CantNoAbiertos = NoAbiertosPaths.Count;
                        CantNoIdentificados = NoIdentificadosPaths.Count;
                        return lstModelos.Cast<Pago>().ToList();
                    }
                }
                catch
                {
                }
            }

            return await Task.Run(() => InterpretarPDF(path).Cast<Pago>().ToList());
        }

        public async Task SerializarDatosAsync()
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
                string json = JsonConvert.SerializeObject(datos);
                using (var writer = new StreamWriter(ArchivoSerializacion, false))
                    await writer.WriteAsync(json);
            }
            catch
            {
            }
        }

        public static async Task<DatosSerializados> DeSerializeDatosAsync()
        {
            if (!File.Exists(ArchivoSerializacion)) return null;

            try
            {
                string json;
                using (var reader = new StreamReader(ArchivoSerializacion))
                    json = await reader.ReadToEndAsync();
                return JsonConvert.DeserializeObject<DatosSerializados>(json);
            }
            catch
            {
                return null;
            }
        }

        public static Task BorrarSerializacionAsync()
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
            return Task.CompletedTask;
        }

        public PagoEfectuado ReprocesarPago(string path)
        {
            var datos = DeSerializeDatos();
            if (datos != null)
            {
                lstModelos = datos.Pagos;
                noVal = datos.NoValidos;
                NoAbiertosPaths = datos.NoAbiertosPaths;
                NoIdentificadosPaths = datos.NoIdentificadosPaths;
                CantNoAbiertos = NoAbiertosPaths.Count;
                CantNoIdentificados = NoIdentificadosPaths.Count;
            }

            lstModelos.RemoveAll(p => String.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
            noVal.RemoveAll(p => String.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
            NoAbiertosPaths.RemoveAll(p => String.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            NoIdentificadosPaths.RemoveAll(p => String.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            CantNoAbiertos = NoAbiertosPaths.Count;
            CantNoIdentificados = NoIdentificadosPaths.Count;

            if (!File.Exists(path))
            {
                Mensajes.Agregar($"El archivo {path} no existe.");
                SerializarDatos();
                return null;
            }

            string nombreArchivo = System.IO.Path.GetFileName(path);
            var format = new Formatos();
            PagoEfectuado pago = null;

            try
            {
                PdfReader reader;
                try
                {
                    reader = new PdfReader(path);
                }
                catch (Exception ex)
                {
                    CantNoAbiertos++;
                    NoAbiertosPaths.Add(path);
                    Logger.Add($"No se pudo abrir el PDF {path}: {ex.Message}");
                    SerializarDatos();
                    return null;
                }

                using (reader)
                {
                    string text = string.Empty;
                    var (pageRead, pageEnd, ifExpensasHY) = ObtenerRangoPaginas(reader, nombreArchivo, path);

                    for (; pageRead < pageEnd; pageRead++)
                    {
                        text += PdfTextExtractor.GetTextFromPage(reader, pageRead);

                        if (!EsComprobantePago(text, nombreArchivo))
                        {
                            CantNoIdentificados++;
                            NoIdentificadosPaths.Add(path);
                            SerializarDatos();
                            return null;
                        }

                        pago = ProcesarTextoComprobante(text, path, nombreArchivo, reader, format, ifExpensasHY);
                        AgregarPago(pago);
                    }
                }

                _fileTimestamps[path] = File.GetLastWriteTimeUtc(path);
            }
            catch (Exception ex)
            {
                Logger.Add($"Error reprocesando {path}: {ex.Message}");
                Mensajes.Agregar($"Error reprocesando {path}: {ex.Message}");
            }

            SerializarDatos();
            return pago;
        }

        #region Métodos privados de soporte

        /// <summary>
        /// Obtiene la ruta del directorio a procesar desde la configuración o el valor por defecto.
        /// Devuelve <c>null</c> si no se puede acceder al directorio.
        /// </summary>
        private string ObtenerDirectorio()
        {
            string defecto = @"D:\Norma\000   PAGOS\";
            string rutaConfig = ConfigurationManager.AppSettings["RutaDePagos"];
            string directorio = !String.IsNullOrEmpty(rutaConfig) ? rutaConfig : defecto;

            try
            {
                Directory.GetAccessControl(directorio);
            }
            catch (UnauthorizedAccessException)
            {
                var msj = "La ruta parametrizada en el config (" + directorio + ") existe pero no se tiene acceso a ella. Revise que la misma no este en una carpeta de un usuario del sistema.";
                Mensajes.Agregar(msj);
                Logger.Add(msj);
                return null;
            }
            catch
            {
                var msj = "La ruta parametrizada en el config (" + directorio + ") tuvo un error al intentar acceder, puede que la misma no exista.";
                Mensajes.Agregar(msj);
                Logger.Add(msj);
                return null;
            }

            return directorio;
        }

        /// <summary>
        /// Devuelve todos los archivos PDF del directorio y sus subdirectorios.
        /// </summary>
        private List<string> ObtenerArchivosPdf(string directorio)
        {
            Logger.Add("\n Directorio a leer: " + directorio);
            return Directory.GetFiles(directorio, "*.pdf", SearchOption.AllDirectories).ToList();
        }

        /// <summary>
        /// Lee el límite de archivos a procesar desde la configuración.
        /// Si no está configurado o es inválido, devuelve la cantidad total.
        /// </summary>
        private int ObtenerLimiteArchivos(int cantidadTotal)
        {
            string filesReaded = ConfigurationManager.AppSettings["ArchivosLeidos"];
            if (cantidadTotal != 0 && !String.IsNullOrEmpty(filesReaded) &&
                int.TryParse(filesReaded, out int limite) && limite > 0)
            {
                return limite;
            }
            return cantidadTotal;
        }

        /// <summary>
        /// Carga el estado previo serializado si existe caché de timestamps.
        /// Restaura las listas internas del procesador.
        /// </summary>
        private DatosSerializados CargarEstadoPrevio(out bool hasCache)
        {
            hasCache = _fileTimestamps.Count > 0;
            DatosSerializados oldState = null;

            if (hasCache)
            {
                try { oldState = DeSerializeDatos(); } catch { }
            }

            if (hasCache && oldState != null)
            {
                lstModelos = oldState.Pagos;
                noVal = oldState.NoValidos;
                NoAbiertosPaths = oldState.NoAbiertosPaths;
                NoIdentificadosPaths = oldState.NoIdentificadosPaths;
            }

            return oldState;
        }

        /// <summary>
        /// Determina el rango de páginas a leer según el tipo de archivo y cantidad de páginas.
        /// </summary>
        private (int inicio, int fin, bool esExpensasHY) ObtenerRangoPaginas(PdfReader reader, string nombreArchivo, string path)
        {
            int pageRead = 1;
            int pageEnd = 2;
            bool ifExpensasHY = false;

            if (nombreArchivo.ToLower().Contains("flow"))
            {
                if (reader.NumberOfPages == 3)
                {
                    pageRead = 1;
                    pageEnd = 2;
                }
            }
            else if (
                nombreArchivo.ToLower().Contains("expensa") && nombreArchivo.ToLower().Contains("san rafael")
                || (path.Contains("SAN RAFAEL") && nombreArchivo.ToLower().Contains("exp"))
                )
            {
                // es Expensas HY
                if (!nombreArchivo.ToLower().Contains("movistar"))
                {
                    
                    ifExpensasHY = true;

                    if (reader.NumberOfPages == 1)
                    {
                        pageRead = 1;
                        pageEnd = 2;
                    }
                    else if (reader.NumberOfPages == 2)
                    {
                        //Puede ser una factura de Telecentro
                        pageRead = 1;
                        pageEnd = 2;

                        // Extraemos el texto de la página actual de forma ultra rápida
                        string textoPagina = PdfTextExtractor.GetTextFromPage(reader, pageRead);

                        if (textoPagina.Contains("Telecentro"))
                        {
                            // Es una factura o comprobante de tipo "Telecentro", no es "ExpensasHY"
                            ifExpensasHY = false;
                        }
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
                }
            }
            else if (nombreArchivo.ToLower().Contains("gesell")
                || nombreArchivo.ToLower().Contains(" vg ")
                || (path.Contains("VILLA GESELL") && path.Contains("EXPENSAS")))
            {
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

            return (pageRead, pageEnd, ifExpensasHY);
        }

        /// <summary>
        /// Agrega un pago a la lista de modelos si no está duplicado; de lo contrario a no válidos.
        /// </summary>
        private void AgregarPago(PagoEfectuado pago)
        {
            bool esValido = pago.Ente != EEnte.Desconocido
                || pago.FechaVencimiento != default(DateTime)
                || pago.Importe != 0;

            if (!esValido)
            {
                noVal.Add(pago);
                return;
            }

            bool existeSimilar = lstModelos.Any(p =>
                p.Ente == pago.Ente &&
                p.Referencia == pago.Referencia &&
                p.FechaVencimiento == pago.FechaVencimiento &&
                p.Importe == pago.Importe
            );

            if (!existeSimilar)
            {
                lstModelos.Add(pago);
            }
        }

        /// <summary>
        /// Elimina del caché de timestamps los archivos que ya no existen en el directorio.
        /// </summary>
        private void LimpiarTimestampsHuérfanos(List<string> archivosActuales)
        {
            var currentFiles = new HashSet<string>(archivosActuales);
            foreach (var key in _fileTimestamps.Keys.ToList())
            {
                if (!currentFiles.Contains(key))
                    _fileTimestamps.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Indica si el texto extraído corresponde a un comprobante de pago válido.
        /// </summary>
        internal bool EsComprobantePago(string texto, string nombreArchivo)
        {
            if (String.IsNullOrEmpty(texto))
                return false;

            var frasesOk = new List<string>
            {
                "pago efectuado", "pagos realizados", "operación realizada con éxito",
                "5º-A", "metrogas", "San Rafael", "CONSORCIO DE COPROPIETARIOS EDIFICIO",
                "ungar", "20382717056", "Comprobante de pago"
            };
            var frasesOmitir = new List<string> { "Agenda de Pagos" };

            bool contieneFraseOk = frasesOk.Any(frase => texto.IndexOf(frase, StringComparison.OrdinalIgnoreCase) >= 0);
            bool contieneFraseOmitir = frasesOmitir.Any(frase => texto.IndexOf(frase, StringComparison.OrdinalIgnoreCase) >= 0);

            if (contieneFraseOk && !contieneFraseOmitir)
            {
                if (nombreArchivo.Contains("historialPagosDetalle") || nombreArchivo.Contains(" VISA.pdf"))
                    return false;

                return true;
            }

            using (var readerTxt = new StringReader(texto))
            {
                string linea;
                while ((linea = readerTxt.ReadLine()) != null)
                {
                    if (String.IsNullOrEmpty(linea)) continue;

                    switch (linea)
                    {
                        case "Pago efectuado":
                        case "Pago Efectuado":
                        case "Pagos Realizados":
                        case "Operación realizada con éxito":
                            return true;
                        case "Transferencias a Cuentas de Tercero":
                        case "Detalle de Movimientos":
                            return false;
                        default:
                            if (linea.Contains("265014"))
                                return false;
                            break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Procesa el texto extraido de un comprobante y devuelve un PagoEfectuado.
        /// </summary>
        private PagoEfectuado ProcesarTextoComprobante(string text, string path, string nombreArchivo, PdfReader reader, Formatos format, bool ifExpensasHY)
        {
            var pago = new PagoEfectuado();
            pago.Path = path;

            bool newFormatPdf = false;
            string textoOriginal = null;
            if (!reader.Info.TryGetValue("CreationDate", out textoOriginal))
            {
                reader.Info.TryGetValue("ModDate", out textoOriginal);
            }
            if (text.Contains("Operación realizada con éxito"))
            {
                newFormatPdf = true;
            }

            int cantLineas = 0;

            if (ifExpensasHY)
            {
                ParsearExpensasHY(pago, text, nombreArchivo, format);
            }
            else
            {
                cantLineas = ParsearLineasEstandar(pago, text, format, newFormatPdf);
            }

            ResolverReferencia(pago, text, nombreArchivo);
            ResolverEnteFallback(pago, text, path, nombreArchivo, reader, format);
            AplicarValidacionesFinales(pago, cantLineas);

            return pago;
        }

        internal void ParsearExpensasHY(PagoEfectuado pago, string text, string nombreArchivo, Formatos format)
        {
            using (StringReader readerTxt = new StringReader(text))
            {
                string line;
                while ((line = readerTxt.ReadLine()) != null)
                {
                    var lineaFormato = line.Split(':');
                    string clave = lineaFormato[0];

                    try
                    {
                        if (!clave.Contains("016"))
                        {
                            continue;
                        }

                        if (clave.Contains("016"))
                        {
                            var valores = clave.Split(' ');
                            pago.Referencia = EReferencia.Norma;
                            pago.Ente = EEnte.ExpSanRafael;

                            pago.Importe = decimal.Parse(valores.Last().Replace("$", ""));
                            pago.FechaPago = format.CrearFecha(nombreArchivo.Split(' ').FirstOrDefault());
                            pago.FechaVencimiento = pago.FechaPago;
                            break;
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }

        internal int ParsearLineasEstandar(PagoEfectuado pago, string text, Formatos format, bool newFormatPdf)
        {
            using (StringReader readerTxt = new StringReader(text))
            {
                string lineaAnterior = "";
                string line;
                int cantLineas = 0;
                bool isComprobanteMP = false;

                if (text.Contains("Comprobante de pago") && text.Contains("Pagaste a") && text.Contains("Total pagado") && text.Contains("Detalle de operación"))
                {
                    isComprobanteMP = true;
                }

                while ((line = readerTxt.ReadLine()) != null)
                {
                    cantLineas++;

                    var lineaFormato = line.Split(':');
                    string clave = lineaFormato[0];
                    string valor = "";
                    valor = lineaFormato.Count() > 1 ? lineaFormato[1] : "";
                    valor = valor.Trim();

                    switch (clave.Trim())
                    {
                        case "Fecha":
                        case "Fecha de Transacción":
                        case "Fecha de Pago":
                            if (!String.IsNullOrEmpty(valor))
                            {
                                pago.FechaPago = format.CrearFecha(valor);
                            }
                            break;

                        case "Código de seguridad":
                        case "Código de Seguridad":
                        case "Cod. Pago":

                            //Si es comprobante Simple
                            if (!String.IsNullOrEmpty(valor))
                            {
                                pago.NroTransaccion = valor;
                            }
                            else //Si es comprobante Completo
                            {
                                pago.NroTransaccion = lineaAnterior;
                            }

                            break;

                        case "Código/Usuario":
                        case "Codigo/Usuario":
                        case "Nº DE CLIENTE":
                        case "Nro de Cliente":
                        case "NRO. DE CLIENTE":

                            pago.NroCliente = valor;

                            if (pago.Ente == EEnte.Desconocido)
                            {
                                // Si no se pudo identificar el Ente se calcula por el Nro de cliente
                                if (TryMapClienteData(pago.NroCliente, out var clienteData))
                                {
                                    pago.Ente = clienteData.Ente;
                                    pago.Referencia = clienteData.Referencia;
                                }
                            }
                            break;

                        case "Nro de cuenta débito":
                        case "NRO DE CUENTA":
                        case "Cuenta a debitar":
                            pago.NroCtaDebito = valor;
                            break;

                        case "Importe":
                        case "IMPORTE":

                            Match match = _regexImporteTransaccion.Match(text);

                            if (match.Success)
                            {
                                // Extrae el importe completo (ej: "$ 10.669,06")
                                string importeCompleto = match.Groups["transaccion"].Value;
                                pago.Importe = decimal.Parse(importeCompleto.Replace("$", ""));
                            }
                            else
                            {
                                pago.Importe = decimal.Parse(valor.Replace("$", ""));
                            }


                            break;

                        case "Fecha de Vencimiento":
                        case "VTO":
                            pago.FechaVencimiento = format.CrearFecha(valor);
                            break;

                        case "FECHA VENCIMIENTO":
                            pago.FechaVencimiento = format.CrearFecha(valor, "dd/MM/yy");
                            break;

                        case "CUOTA":
                        case "Cuota":
                        case "Cuota/Año":
                            //006/17
                            pago.Cuota = valor;
                            break;
                        case "Pago de":
                            pago.Ente = ExtractEnteFromLine(valor);
                            break;
                        case "abonado":


                            try
                            {
                                var enteText = lineaAnterior;

                                //Buenos Aires - Municipalidad de Lanus
                                //Buenos Aires - Municipalidad de Villa Gesell
                                // Si contiene "Nombre del ente", extrae el valor real
                                if (enteText.Contains("Nombre del ente"))
                                {
                                    enteText = enteText.Replace("Nombre del ente", "").Trim();
                                    enteText = enteText + " " + valor;
                                    enteText = enteText.Replace("Códigos de Pago que comiencen con 2", "").Trim();
                                }

                                // Parse case-insensitive
                                pago.Ente = ExtractEnteFromLine(enteText);
                            }
                            catch (ArgumentException)
                            {
                                pago.Ente = EEnte.Desconocido; // Valor por defecto si no coincide
                                                                // O lanzar excepción si prefieres: throw new InvalidOperationException($"Ente no reconocido: {lineaAnterior}");
                            }

                            break;

                        case "Nombre Originante":
                            if (valor.Contains("NORMA D AQUILA"))
                            {
                                pago.Referencia = EReferencia.Norma;
                            }
                            break;
                        case "Nombre del destinatario":

                            if (valor.Contains("CONSORCIO DE COPROPIETARIOS EDIFICIO"))
                            {
                                pago.Ente = EEnte.ExpUngar;
                            }
                            break;

                        default:

                            if (newFormatPdf)
                            {
                                if (lineaAnterior == "Operación realizada con éxito")
                                {
                                    pago.FechaPago = format.CrearFecha(line);
                                }
                                if (line.Contains("Número de transacción"))
                                {
                                    var resultado = format.CrearFecha(lineaAnterior);
                                    if (resultado != DateTime.MinValue)
                                    {
                                        pago.FechaPago = resultado;
                                        var valores = line.Split(' ');
                                        pago.NroTransaccion = valores[valores.Length - 2];
                                    }
                                }
                            }

                            // Comprobante de MP
                            if (isComprobanteMP)
                            {
                                if (lineaAnterior == "Pagaste a")
                                {
                                    pago.Ente = ExtractEnteFromLine(clave, pago, text);
                                }
                                else if (lineaAnterior == "Pagador Fecha de pago" ||
                                    lineaAnterior == "Pagador final Pago es")
                                {
                                    if (pago.FechaPago == default(DateTime))
                                    {
                                        string pattern = @"\d{2}/\d{2}/\d{4}";

                                        Match match2 = Regex.Match(clave, pattern);

                                        if (match2.Success)
                                        {
                                            pago.FechaPago = format.CrearFecha(match2.Value);
                                        }
                                    }                                    
                                }
                            }

                            break;
                    }

                    lineaAnterior = line;
                }

                return cantLineas;
            }
        }

        /// <summary>
        /// Aplica validaciones finales de fechas, tipo de comprobante y cotización en dólares.
        /// </summary>
        internal void AplicarValidacionesFinales(PagoEfectuado pago, int cantLineas)
        {
            // Validacion de fechas
            pago.FechaPago = pago.FechaPago == default(DateTime) ? pago.FechaVencimiento : pago.FechaPago;
            pago.FechaVencimiento = pago.FechaVencimiento == default(DateTime) ? pago.FechaPago : pago.FechaVencimiento;

            // Si FechaPago es futura (dato invalido), usar FechaVencimiento
            if (pago.FechaPago > DateTime.Today && pago.FechaVencimiento != default(DateTime) && pago.FechaVencimiento <= DateTime.Today)
            {
                pago.FechaPago = pago.FechaVencimiento;
            }

            // Tipo de comprobante [ "Completo" ; "Reducido"]
            if (cantLineas > 8)
            {
                pago.TipoComprobante = ETipoComprobante.Completo;
            }
            else
            {
                pago.TipoComprobante = ETipoComprobante.Reducido;

                //Se interpreta la fecha de vencimiento
                if (pago.FechaVencimiento == default(DateTime) && !String.IsNullOrEmpty(pago.Cuota))
                {
                    var variablesAux = pago.Cuota.Split('/');
                    var mesAux = int.Parse(variablesAux[0]);
                    var añoAux = int.Parse(variablesAux[1]);
                    if (añoAux < 2000) añoAux += 2000;

                    try
                    {
                        var diaAux = DateTime.DaysInMonth(añoAux, mesAux);

                        pago.FechaVencimiento = new DateTime(añoAux, mesAux, diaAux);
                    }
                    catch (Exception)
                    {
                        //Si no se puede interpretar la fecha se toma la de pago
                        pago.FechaVencimiento = pago.FechaPago;
                    }
                }
            }

            // Cotización en Dolares
            var valCotizacion = (decimal)CotizacionHistorica.GetCotizacionPorFecha(pago.FechaPago);
            valCotizacion = valCotizacion != 0 ? valCotizacion : (decimal)CotizacionHistorica.GetCotizacionPorFecha(pago.FechaVencimiento);
            valCotizacion = valCotizacion != 0 ? valCotizacion : (decimal)CotizacionHistorica.GetCotizacionAnterior(pago.FechaPago);
            if (valCotizacion != 0)
            {
                pago.ImporteDolar = decimal.Round(pago.Importe / valCotizacion, 2);
            }
            else
            {
                // Si no se encontró cotización de ese día

                // TODO: hacer algo si llega hasta acá
            }
        }

        /// <summary>
        /// Resuelve la referencia/propiedad del pago a partir del número de cliente o del contenido textual.
        /// </summary>
        internal void ResolverReferencia(PagoEfectuado pago, string text, string nombreArchivo)
        {
            if (!String.IsNullOrEmpty(pago.NroCliente) && pago.Referencia == EReferencia.Desconocido)
            {
                if (ClienteMap.TryGetValue(pago.NroCliente.Trim(), out var clienteData))
                {
                    pago.Referencia = clienteData.Referencia;
                }
            }

            if (pago.Referencia == EReferencia.Desconocido)
            {
                if (text.Contains("GESELL"))
                {
                    pago.Referencia = EReferencia.VillaGesell;
                }
                else if (text.Contains("UNGAR") ||
                    text.Contains("EDIFICIO SAN MARTIN"))
                {
                    pago.Referencia = EReferencia.VillaGesell;
                }
                else if (text.Contains("20902705060"))
                {
                    pago.Referencia = EReferencia.Nico;
                }
                else if (text.Contains("30003392507"))
                {
                    pago.Referencia = EReferencia.TiaRaquel;
                }
                else if (text.Contains("2398966") || //Nro cliente de Telecentro Norma
                    text.Contains("30006245390") ||
                    text.Contains("20382717056") ||
                    text.Contains("ADM San Rafael"))
                {
                    pago.Referencia = EReferencia.Norma;
                }
                else if (pago.Path.ToLower().Contains("conce"))
                {
                    //TODO: si entra acá hacer alto
                    //Codigo usuario: "3860000075504"
                }

                else if (text.Contains("Cablevisión") && pago.Path.ToLower().Contains("raquel"))
                {
                    pago.Referencia = EReferencia.TiaRaquel;
                }
                else if (text.Contains("nico"))
                {
                    pago.Referencia = EReferencia.Nico;
                }
                else
                {
                    //TODO: si entra acá hacer alto
                }
            }
        }
        /// <summary>
        /// Intenta identificar el ente y completar datos cuando no se pudo determinar por el parsing estándar.
        /// </summary>
        internal void ResolverEnteFallback(PagoEfectuado pago, string text, string path, string nombreArchivo, PdfReader reader, Formatos format)
        {
            if (pago.Ente == EEnte.Desconocido)
            {
                string[] lineas = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (nombreArchivo.ToLower().Contains("bonaerenses"))
                {
                    pago.Ente = EEnte.AguasBonaerenses;
                }

                else if (nombreArchivo.ToLower().Contains("cevige"))
                {
                    pago.Ente = EEnte.Cevige;
                }

                else if (text.Contains("20382717056"))
                {
                    pago.Ente = EEnte.Claro;

                    if (pago.Importe == 0)
                    {
                        var data = lineas.First(l => l.Contains("20382717056"));
                        var datas = data.Split(' ');
                        pago.Importe = decimal.Parse(datas[1]);
                        pago.FechaVencimiento = format.CrearFecha(datas[2]);
                        pago.FechaPago = pago.FechaVencimiento;
                    }
                }

                else if (text.Contains("UNGAR") ||
                    text.Contains("EDIFICIO SAN MARTIN"))
                {
                    pago.Ente = EEnte.ExpUngar;

                    if (reader.NumberOfPages == 1)
                    {
                        if (pago.Importe == 0)
                        {
                            foreach (var linea in lineas)
                            {
                                if (linea.StartsWith("SALDO A PAGAR"))
                                {
                                    Match match = _regexSaldoAPagar.Match(linea);

                                    if (match.Success)
                                    {
                                        // Extrae el string del importe (ej: "10669.06")
                                        string importeString = match.Groups["importe"].Value;

                                        // Al tener formato internacional (punto para decimales), la conversión es directa con InvariantCulture
                                        pago.Importe = Convert.ToDecimal(importeString, System.Globalization.CultureInfo.InvariantCulture);
                                    }

                                    break;
                                }
                            }

                        }

                        if (pago.FechaVencimiento == default(DateTime))
                        {
                            int star = text.IndexOf("SALDO A PAGAR AL");
                            if (star != -1)
                            {
                                var fechaVto = text.Substring(star + 17, 10).Trim();
                                pago.FechaVencimiento = format.CrearFecha(fechaVto);
                                pago.FechaPago = pago.FechaVencimiento;
                            }
                            else
                            {
                                foreach (var fila in lineas)
                                {
                                    if (fila.Contains("Fecha de la Transacció"))
                                    {
                                        Match match = _regexFechaTransaccion.Match(text);

                                        if (match.Success)
                                        {
                                            pago.FechaVencimiento = format.CrearFecha(match.Groups["fecha"].Value);
                                            pago.FechaPago = pago.FechaVencimiento;
                                        }
                                    }
                                }

                                if (pago.FechaVencimiento == default(DateTime))
                                {
                                    pago.FechaVencimiento = pago.FechaPago;
                                }
                            }

                        }
                    }

                    else if (reader.NumberOfPages == 3 || reader.NumberOfPages == 4)
                    {
                        if (pago.Importe == 0)
                        {

                            // 1. Dividimos el texto completo extraído por iTextSharp en líneas individuales


                            string importeEncontrado = "0,00";

                            // 2. Buscamos el patrón exacto línea por línea
                            // ^\s*6\s+D'AQUILA de forma estricta al inicio de la fila

                            foreach (string linea in lineas)
                            {
                                // Verificamos si la línea actual pertenece al Dpto 6 de D'AQUILA
                                if (linea.StartsWith("6 DPTO"))
                                {
                                    // Una vez parados en la línea correcta, extraemos el ÚLTIMO importe de esa fila
                                    Match matchImporte = _regexImporteUngar.Match(linea);
                                    if (matchImporte.Success)
                                    {
                                        try
                                        {
                                            importeEncontrado = matchImporte.Groups["importe"].Value;
                                            pago.Importe = decimal.Parse(importeEncontrado);
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Add($"Error parseando importe Ungar en {path}: {ex.Message}");
                                        }
                                    }
                                    break;
                                }
                            }
                        }

                        if (pago.FechaVencimiento == default(DateTime))
                        {
                            foreach (string linea in lineas)
                            {
                                // Verificamos si la línea actual pertenece al Dpto 6 de D'AQUILA
                                if (linea.Contains("Vencimiento el día"))
                                {
                                    var fechaVto = linea.Split(' ').Last();
                                    pago.FechaVencimiento = format.CrearFecha(fechaVto);
                                    pago.FechaPago = pago.FechaVencimiento;
                                    break;
                                }
                            }
                        }
                    }
                }

                else if (text.Contains("0001280445"))
                {
                    pago.Ente = EEnte.Edesur;
                }

                else if (text.Contains("2398966"))
                {
                    pago.Ente = EEnte.Telecentro;
                }

                else if (text.Contains("MetroGAS"))
                {
                    pago.Ente = EEnte.Metrogas;

                    if (pago.Importe == 0)
                    {
                        int star = text.IndexOf("TOTAL A PAGAR");
                        var importe = (text.Length >= (star + 20)) ? text.Substring(star + 15, 10).Trim() : "Fuera de rango";
                        pago.Importe = decimal.Parse(importe.Replace("$", ""));
                    }

                    if (pago.FechaVencimiento == default(DateTime))
                    {
                        int star = text.IndexOf("FECHA DE VENCIMIENTO");
                        var fechaVto = text.Substring(star + 22, 10).Trim();
                        pago.FechaVencimiento = format.CrearFecha(fechaVto);
                        pago.FechaPago = pago.FechaVencimiento;
                    }
                }

                else if (text.Contains("ADM San Rafael"))
                {
                    pago.Ente = EEnte.ExpSanRafael;

                    if (pago.FechaPago == default(DateTime))
                    {
                        foreach (var linea in lineas)
                        {
                            if (linea.StartsWith("MES DE VENCIMIENTO DE LAS EXPENSA"))
                            {
                                var partes = linea.Split(':');

                                if (partes.Length > 1)
                                {
                                    var textoFecha = partes[1].Trim(); // "Enero 2026"

                                    DateTime fecha;

                                    if (DateTime.TryParseExact(
                                        "05 " + textoFecha,
                                        "dd MMMM yyyy",
                                        new System.Globalization.CultureInfo("es-AR"),
                                        System.Globalization.DateTimeStyles.None,
                                        out fecha))
                                    {
                                        pago.FechaPago = fecha;
                                        pago.FechaVencimiento = fecha;
                                    }
                                }

                                break;
                            }

                            if (linea.StartsWith("1er Vencimiento"))
                            {
                                var match = _regexVencimientoImporte.Match(linea);

                                if (match.Success)
                                {
                                    DateTime fechaVencimiento;
                                    decimal importe;

                                    if (DateTime.TryParseExact(
                                            match.Groups[1].Value,
                                            "dd/MM/yyyy",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None,
                                            out fechaVencimiento)
                                        &&
                                        decimal.TryParse(
                                            match.Groups[2].Value,
                                            NumberStyles.Number,
                                            new CultureInfo("es-AR"),
                                            out importe))
                                    {
                                        // Resultado
                                        pago.FechaVencimiento = fechaVencimiento;
                                        pago.FechaPago = fechaVencimiento;
                                        pago.Importe = importe;

                                        // Ejemplo:
                                        // fechaVencimiento = 15/04/2026
                                        // importe = 173579.16
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

        }

        #endregion

        #endregion

    }
}
