using AdminPagosDLL.Core;
using AdminPagosDLL.Models;
using iTextSharp.text.pdf;
using iTextSharpPdfParser = iTextSharp.text.pdf.parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdminPagosDLL.Tests
{
    class Program
    {
        static int _passed = 0;
        static int _failed = 0;
        static List<string> _failures = new List<string>();

        static void Main(string[] args)
        {
            Console.WriteLine("=== AdminPagosDLL Tests ===");
            Console.WriteLine();

            TestIdentifyEnte();
            TestGetEnteDisplayText();
            TestExtractEnteFromLine();
            TestClienteMap();
            TestTryMapClienteData();
            TestEsComprobantePago();
            TestResolverReferencia();
            TestParsearExpensasHY();
            TestParsearLineasEstandar();
            TestAplicarValidacionesFinales();
            TestResolverEnteFallback();
            TestIntegration_PdfExtraction();

            Console.WriteLine();
            Console.WriteLine($"=== Resultados: {_passed} passed, {_failed} failed ===");
            if (_failures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Fallos:");
                foreach (var f in _failures)
                    Console.WriteLine($"  - {f}");
            }

            Environment.Exit(_failed > 0 ? 1 : 0);
        }

        static void Assert(bool condition, string message)
        {
            if (condition) { _passed++; }
            else { _failed++; _failures.Add(message); Console.WriteLine($"  FAIL: {message}"); }
        }

        static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual)) { _passed++; }
            else { _failed++; _failures.Add($"{message} — Expected: {expected}, Actual: {actual}"); Console.WriteLine($"  FAIL: {message} — Expected: {expected}, Actual: {actual}"); }
        }

        static Funciones CreateFunciones() => new Funciones();

        // ─── IdentifyEnte ────────────────────────────────────────

        static void TestIdentifyEnte()
        {
            Console.WriteLine("--- IdentifyEnte ---");
            var f = CreateFunciones();

            AssertEqual(EEnte.Claro, f.IdentifyEnte("claro"), "IdentifyEnte: claro");
            AssertEqual(EEnte.Claro, f.IdentifyEnte("CLARO"), "IdentifyEnte: CLARO");
            AssertEqual(EEnte.Edesur, f.IdentifyEnte("Edesur"), "IdentifyEnte: Edesur");
            AssertEqual(EEnte.Metrogas, f.IdentifyEnte("metrogas"), "IdentifyEnte: metrogas");
            AssertEqual(EEnte.Arba, f.IdentifyEnte("buenos aires- arba inmobiliario"), "IdentifyEnte: arba inmobiliario");
            AssertEqual(EEnte.AguasBonaerenses, f.IdentifyEnte("AGUAS BONAERENSES"), "IdentifyEnte: AGUAS BONAERENSES");
            AssertEqual(EEnte.AguasBonaerenses, f.IdentifyEnte("AYSA"), "IdentifyEnte: AYSA → AguasBonaerenses (match exacto)");
            AssertEqual(EEnte.MunicipalidadLanus, f.IdentifyEnte("buenos aires- municipalidad de lanus"), "IdentifyEnte: lanus");
            AssertEqual(EEnte.Cevige, f.IdentifyEnte("cevige"), "IdentifyEnte: cevige");
            AssertEqual(EEnte.MunicipalidadLanus, f.IdentifyEnte("expensas lanus"), "IdentifyEnte: expensas lanus → MunicipalidadLanus (keyword ordering)");
            AssertEqual(EEnte.Movistar, f.IdentifyEnte("movistar"), "IdentifyEnte: movistar");
            AssertEqual(EEnte.Desconocido, f.IdentifyEnte(""), "IdentifyEnte: string vacio");
            AssertEqual(EEnte.Desconocido, f.IdentifyEnte(null), "IdentifyEnte: null");
            AssertEqual(EEnte.Desconocido, f.IdentifyEnte("lorem ipsum dolor"), "IdentifyEnte: texto irrelevante");
            Console.WriteLine();
        }

        // ─── GetEnteDisplayText ──────────────────────────────────

        static void TestGetEnteDisplayText()
        {
            Console.WriteLine("--- GetEnteDisplayText ---");

            var text = Funciones.GetEnteDisplayText(EEnte.Edesur);
            Assert(!string.IsNullOrEmpty(text), "GetEnteDisplayText: Edesur deberia devolver texto");
            Assert(text.Length > 0, "GetEnteDisplayText: Edesur texto no vacio");

            var unknown = Funciones.GetEnteDisplayText(EEnte.Desconocido);
            Assert(!string.IsNullOrEmpty(unknown), "GetEnteDisplayText: Desconocido deberia devolver texto");
            Console.WriteLine();
        }

        // ─── ExtractEnteFromLine ─────────────────────────────────

        static void TestExtractEnteFromLine()
        {
            Console.WriteLine("--- ExtractEnteFromLine ---");
            var f = CreateFunciones();

            AssertEqual(EEnte.Claro, f.ExtractEnteFromLine("Nombre del Ente Abonado: Claro"), "ExtractEnte: Claro");
            AssertEqual(EEnte.Edesur, f.ExtractEnteFromLine("Nombre del Ente Abonado: Edesur"), "ExtractEnte: Edesur");
            AssertEqual(EEnte.Metrogas, f.ExtractEnteFromLine("Nombre del Ente Abonado: Metrogas"), "ExtractEnte: Metrogas");
            Console.WriteLine();
        }

        // ─── ClienteMap ──────────────────────────────────────────

        static void TestClienteMap()
        {
            Console.WriteLine("--- ClienteMap ---");

            AssertEqual(41, Funciones.ClienteMap.Count, "ClienteMap: 41 entradas");

            Assert(Funciones.ClienteMap.ContainsKey("030006245390"), "ClienteMap contiene 030006245390");
            var entry = Funciones.ClienteMap["030006245390"];
            AssertEqual(EEnte.Metrogas, entry.Ente, "030006245390 → Metrogas");
            AssertEqual(EReferencia.Yrigoyen, entry.Referencia, "030006245390 → Yrigoyen");

            Assert(Funciones.ClienteMap.ContainsKey("00251361487"), "ClienteMap contiene 00251361487");
            var entry2 = Funciones.ClienteMap["00251361487"];
            AssertEqual(EEnte.Arba, entry2.Ente, "00251361487 → Arba");
            AssertEqual(EReferencia.Yrigoyen, entry2.Referencia, "00251361487 → Yrigoyen");

            Assert(Funciones.ClienteMap.ContainsKey("08620382717056"), "ClienteMap contiene 08620382717056");
            var entry3 = Funciones.ClienteMap["08620382717056"];
            AssertEqual(EEnte.Claro, entry3.Ente, "08620382717056 → Claro");
            AssertEqual(EReferencia.Guille, entry3.Referencia, "08620382717056 → Guille");

            Console.WriteLine();
        }

        // ─── TryMapClienteData ───────────────────────────────────

        static void TestTryMapClienteData()
        {
            Console.WriteLine("--- TryMapClienteData ---");
            var f = CreateFunciones();

            Assert(f.TryMapClienteData("20902705060", out var data), "TryMap: 20902705060 existe");
            AssertEqual(EEnte.Claro, data.Ente, "TryMap: 20902705060 → Claro");
            AssertEqual(EReferencia.Nico, data.Referencia, "TryMap: 20902705060 → Nico");

            Assert(!f.TryMapClienteData("99999999999", out _), "TryMap: 99999999999 no existe");
            Console.WriteLine();
        }

        // ─── EsComprobantePago ───────────────────────────────────

        static void TestEsComprobantePago()
        {
            Console.WriteLine("--- EsComprobantePago ---");
            var f = CreateFunciones();

            Assert(!f.EsComprobantePago("", "test.pdf"), "EsComprobante: texto vacio → false");
            Assert(!f.EsComprobantePago(null, "test.pdf"), "EsComprobante: null → false");
            Assert(f.EsComprobantePago("Pago efectuado\nImporte: $100", "test.pdf"), "EsComprobante: 'Pago efectuado' → true");
            Assert(f.EsComprobantePago("Operación realizada con éxito\n123", "test.pdf"), "EsComprobante: 'Operacion realizada con exito' → true");
            Assert(f.EsComprobantePago("METROGAS\nPago: $500", "test.pdf"), "EsComprobante: METROGAS → true");
            // Nota: "Agenda de Pagos" esta en frasesOmitir, pero el chequeo linea-por-linea
            // encuentra "Pago efectuado" individualmente y retorna true (comportamiento real)
            Assert(f.EsComprobantePago("Agenda de Pagos\nPago efectuado", "test.pdf"), "EsComprobante: Agenda de Pagos + Pago efectuado → true (match linea)");
            Assert(!f.EsComprobantePago("Pago efectuado", "historialPagosDetalle.pdf"), "EsComprobante: historialPagosDetalle → false");
            Assert(!f.EsComprobantePago("Transferencias a Cuentas de Tercero\nPago", "test.pdf"), "EsComprobante: Transferencias → false");
            Assert(!f.EsComprobantePago("Detalle de Movimientos", "test.pdf"), "EsComprobante: Detalle Movimientos → false");
            Console.WriteLine();
        }

        // ─── ResolverReferencia ──────────────────────────────────

        static void TestResolverReferencia()
        {
            Console.WriteLine("--- ResolverReferencia ---");
            var f = CreateFunciones();

            var p1 = new PagoEfectuado { NroCliente = "20902705060", Referencia = EReferencia.Desconocido };
            f.ResolverReferencia(p1, "", "test.pdf");
            AssertEqual(EReferencia.Nico, p1.Referencia, "ResolverRef: Nico");

            var p2 = new PagoEfectuado { NroCliente = "20199489345", Referencia = EReferencia.Desconocido };
            f.ResolverReferencia(p2, "", "test.pdf");
            AssertEqual(EReferencia.Norma, p2.Referencia, "ResolverRef: Norma");

            var p3 = new PagoEfectuado { NroCliente = "030006245390", Referencia = EReferencia.Desconocido };
            f.ResolverReferencia(p3, "", "test.pdf");
            AssertEqual(EReferencia.Yrigoyen, p3.Referencia, "ResolverRef: Yrigoyen");

            var p4 = new PagoEfectuado { NroCliente = "", Referencia = EReferencia.Desconocido };
            f.ResolverReferencia(p4, "GESELL - EXPENSAS", "test.pdf");
            AssertEqual(EReferencia.VillaGesell, p4.Referencia, "ResolverRef: GESELL textual");

            var p5 = new PagoEfectuado { NroCliente = "", Referencia = EReferencia.Desconocido };
            f.ResolverReferencia(p5, "ADM San Rafael - Pago", "test.pdf");
            AssertEqual(EReferencia.Norma, p5.Referencia, "ResolverRef: ADM San Rafael textual");

            var p6 = new PagoEfectuado { NroCliente = "000111222", Referencia = EReferencia.Desconocido };
            f.ResolverReferencia(p6, "UNGAR EDIFICIO", "test.pdf");
            AssertEqual(EReferencia.VillaGesell, p6.Referencia, "ResolverRef: UNGAR textual");
            Console.WriteLine();
        }

        // ─── ParsearExpensasHY ───────────────────────────────────

        static void TestParsearExpensasHY()
        {
            Console.WriteLine("--- ParsearExpensasHY ---");
            var f = CreateFunciones();
            var fmt = new Formatos();

            var p1 = new PagoEfectuado();
            f.ParsearExpensasHY(p1, "016 01 2023 $15000,50", "2023-01 expensas", fmt);
            AssertEqual(EEnte.ExpSanRafael, p1.Ente, "ParsearHY: ente");
            AssertEqual(EReferencia.Norma, p1.Referencia, "ParsearHY: referencia");
            Assert(p1.Importe > 0, "ParsearHY: importe > 0");
            Assert(p1.FechaPago != default(DateTime), "ParsearHY: fecha pago set");
            Assert(p1.FechaVencimiento != default(DateTime), "ParsearHY: fecha vto set");

            var p2 = new PagoEfectuado();
            f.ParsearExpensasHY(p2, "015 01 2023 $10000,00", "comprobante", fmt);
            AssertEqual(EEnte.Desconocido, p2.Ente, "ParsearHY sin016: ente desconocido");

            var p3 = new PagoEfectuado();
            var multi = "001 01 2023 $5000\n002 01 2023 $6000\n016 02 2023 $15000,50";
            f.ParsearExpensasHY(p3, multi, "2023-02 exp", fmt);
            AssertEqual(EEnte.ExpSanRafael, p3.Ente, "ParsearHY multilinea: ente");
            Assert(p3.Importe > 0, "ParsearHY multilinea: importe");
            Console.WriteLine();
        }

        // ─── ParsearLineasEstandar ───────────────────────────────

        static void TestParsearLineasEstandar()
        {
            Console.WriteLine("--- ParsearLineasEstandar ---");
            var f = CreateFunciones();
            var fmt = new Formatos();

            var p1 = new PagoEfectuado();
            f.ParsearLineasEstandar(p1, "Fecha de Pago: 15/06/2023", fmt, false);
            AssertEqual(new DateTime(2023, 6, 15), p1.FechaPago, "ParsearEstandar: fecha pago");

            var p2 = new PagoEfectuado();
            f.ParsearLineasEstandar(p2, "Fecha de Transacción: 20/01/2024", fmt, false);
            AssertEqual(new DateTime(2024, 1, 20), p2.FechaPago, "ParsearEstandar: fecha transaccion");

            var p3 = new PagoEfectuado();
            f.ParsearLineasEstandar(p3, "Cod. Pago: 12345", fmt, false);
            AssertEqual("12345", p3.NroTransaccion, "ParsearEstandar: cod pago");

            var p4 = new PagoEfectuado();
            f.ParsearLineasEstandar(p4, "Nro de Cliente: 20199489345", fmt, false);
            AssertEqual("20199489345", p4.NroCliente, "ParsearEstandar: nro cliente");
            AssertEqual(EEnte.Claro, p4.Ente, "ParsearEstandar: ente desde ClienteMap");
            AssertEqual(EReferencia.Norma, p4.Referencia, "ParsearEstandar: ref desde ClienteMap");

            var p5 = new PagoEfectuado();
            f.ParsearLineasEstandar(p5, "Importe: $ 15000,00", fmt, false);
            AssertEqual(15000.00m, p5.Importe, "ParsearEstandar: importe");

            var p6 = new PagoEfectuado();
            f.ParsearLineasEstandar(p6, "Fecha de Vencimiento: 30/06/2024", fmt, false);
            AssertEqual(new DateTime(2024, 6, 30), p6.FechaVencimiento, "ParsearEstandar: fecha vto");

            var p7 = new PagoEfectuado();
            f.ParsearLineasEstandar(p7, "Cuota: 006/17", fmt, false);
            AssertEqual("006/17", p7.Cuota, "ParsearEstandar: cuota");

            var p8 = new PagoEfectuado();
            var multi = "Fecha de Pago: 15/06/2023\nNro de Cliente: 20199489345\nImporte: $ 5000,00\nCódigo de seguridad: 12345";
            f.ParsearLineasEstandar(p8, multi, fmt, false);
            AssertEqual(new DateTime(2023, 6, 15), p8.FechaPago, "ParsearEstandar multi: fecha");
            AssertEqual("20199489345", p8.NroCliente, "ParsearEstandar multi: nro cliente");
            AssertEqual(5000.00m, p8.Importe, "ParsearEstandar multi: importe");
            AssertEqual("12345", p8.NroTransaccion, "ParsearEstandar multi: transaccion");

            var p9 = new PagoEfectuado();
            var lineas = f.ParsearLineasEstandar(p9, "a\nb\nc\nd\ne", fmt, false);
            AssertEqual(5, lineas, "ParsearEstandar: retorna 5 lineas");
            Console.WriteLine();
        }

        // ─── AplicarValidacionesFinales ──────────────────────────

        static void TestAplicarValidacionesFinales()
        {
            Console.WriteLine("--- AplicarValidacionesFinales ---");
            var f = CreateFunciones();

            var p1 = new PagoEfectuado { FechaPago = default, FechaVencimiento = new DateTime(2023, 6, 15), Importe = 1000m };
            f.AplicarValidacionesFinales(p1, 10);
            AssertEqual(new DateTime(2023, 6, 15), p1.FechaPago, "AplicarValid: fecha pago default → vto");

            var p2 = new PagoEfectuado { FechaPago = new DateTime(2023, 6, 15), FechaVencimiento = default, Importe = 1000m };
            f.AplicarValidacionesFinales(p2, 10);
            AssertEqual(new DateTime(2023, 6, 15), p2.FechaVencimiento, "AplicarValid: fecha vto default → pago");

            var p3 = new PagoEfectuado { FechaPago = DateTime.Today.AddDays(30), FechaVencimiento = new DateTime(2023, 6, 15), Importe = 1000m };
            f.AplicarValidacionesFinales(p3, 10);
            AssertEqual(new DateTime(2023, 6, 15), p3.FechaPago, "AplicarValid: fecha pago futura → vto");

            var p4 = new PagoEfectuado { FechaPago = new DateTime(2023, 6, 15), FechaVencimiento = new DateTime(2023, 7, 15), Importe = 1000m };
            f.AplicarValidacionesFinales(p4, 10);
            AssertEqual(ETipoComprobante.Completo, p4.TipoComprobante, "AplicarValid: 10 lineas → Completo");

            var p5 = new PagoEfectuado { FechaPago = new DateTime(2023, 6, 15), FechaVencimiento = new DateTime(2023, 7, 15), Importe = 1000m };
            f.AplicarValidacionesFinales(p5, 5);
            AssertEqual(ETipoComprobante.Reducido, p5.TipoComprobante, "AplicarValid: 5 lineas → Reducido");

            var p6 = new PagoEfectuado { FechaPago = new DateTime(2023, 6, 15), FechaVencimiento = default, Importe = 1000m, Cuota = "006/23" };
            f.AplicarValidacionesFinales(p6, 5);
            Assert(p6.FechaVencimiento != default, "AplicarValid: cuota → calcula vto");
            Console.WriteLine();
        }

        // ─── ResolverEnteFallback ────────────────────────────────

        static void TestResolverEnteFallback()
        {
            Console.WriteLine("--- ResolverEnteFallback ---");
            var f = CreateFunciones();
            var fmt = new Formatos();

            var p1 = new PagoEfectuado { Ente = EEnte.Desconocido };
            f.ResolverEnteFallback(p1, "agua", "", "bonaerenses_comprobante.pdf", null, fmt);
            AssertEqual(EEnte.AguasBonaerenses, p1.Ente, "ResolverEnte: bonaerenses → AguasBonaerenses");

            var p2 = new PagoEfectuado { Ente = EEnte.Desconocido };
            f.ResolverEnteFallback(p2, "cevige luz", "", "cevige_factura.pdf", null, fmt);
            AssertEqual(EEnte.Cevige, p2.Ente, "ResolverEnte: cevige → Cevige");

            var p3 = new PagoEfectuado { Ente = EEnte.Edesur };
            f.ResolverEnteFallback(p3, "san martin", "", "algo.pdf", null, fmt);
            AssertEqual(EEnte.Edesur, p3.Ente, "ResolverEnte: ente ya conocido no se sobrescribe");
            Console.WriteLine();
        }

        // ─── Integration: PDF extraction ─────────────────────────

        static void TestIntegration_PdfExtraction()
        {
            Console.WriteLine("--- Integration: PDF Extraction ---");
            var pdfRoot = @"D:\Norma\000   PAGOS\a - H. YRIGOYEN\H.Y. ARBA  2024  bimestral";

            if (!Directory.Exists(pdfRoot))
            {
                Console.WriteLine("  SKIP: Directorio de prueba no encontrado");
                Console.WriteLine();
                return;
            }

            var pdfs = Directory.GetFiles(pdfRoot, "*.pdf", SearchOption.TopDirectoryOnly);
            if (pdfs.Length == 0)
            {
                Console.WriteLine("  SKIP: No hay PDFs en el directorio de prueba");
                Console.WriteLine();
                return;
            }

            var firstPdf = pdfs[0];
            Console.WriteLine($"  Usando: {System.IO.Path.GetFileName(firstPdf)}");

            try
            {
                using (var reader = new PdfReader(firstPdf))
                {
                    string text = "";
                    for (int page = 1; page <= reader.NumberOfPages; page++)
                        text += iTextSharpPdfParser.PdfTextExtractor.GetTextFromPage(reader, page);

                    Assert(!string.IsNullOrEmpty(text), $"Integration: texto extraido de {System.IO.Path.GetFileName(firstPdf)}");
                    Assert(text.Length > 50, $"Integration: texto extraido > 50 chars ({text.Length})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: Error extrayendo PDF: {ex.Message}");
                _failed++;
            }

            // Integration: InterpretarPDF sobre directorio real
            try
            {
                var f = CreateFunciones();
                var pagos = f.InterpretarPDF();
                Assert(pagos != null, "Integration: InterpretarPDF retorna lista no nula");
                Console.WriteLine($"  InterpretarPDF: {pagos.Count} pagos encontrados");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL: InterpretarPDF lanzó excepción: {ex.Message}");
                _failed++;
            }

            Console.WriteLine();
        }
    }
}
