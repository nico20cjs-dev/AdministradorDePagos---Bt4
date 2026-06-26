# Resumen técnico del refactor de `InterpretarPDF`

> Estado: Fases 0 y 1 completadas. Fases 2-5 pendientes.
> Fecha: 2026-06-23
> Proyecto: AdministradorDePagos (.NET Framework 4.8)

---

## 1. Contexto y objetivo

El método `InterpretarPDF()` en `AdminPagosDLL/Core/Funciones.cs` era un método monolítico de ~1000 líneas que acumulaba ~15 responsabilidades: I/O de archivos, extracción PDF, parsing heurístico, identificación de entes, resolución de referencias/propiedades, cálculo de cotización, deduplicación y serialización.

**Objetivo del refactor:**
- Mejorar mantenibilidad y testabilidad.
- Eliminar deuda técnica crítica (`TypeNameHandling.Auto`, recursos no liberados, código muerto).
- Preparar el terreno para una arquitectura basada en servicios de dominio y parsers por formato (Strategy).

**Restricciones respetadas:**
- .NET Framework 4.8.
- iTextSharp 5.5.13.1 (no tiene API async; se mantiene sync envuelto en `Task.Run` donde corresponda).
- No modificar `HomeController` ni la API pública de `Funciones` salvo lo estrictamente necesario.

---

## 2. Decisiones arquitectónicas

### 2.1 Compatibilidad de API pública

`Funciones.CargarPagos()` y `Funciones.CargarPagosAsync()` siguen retornando `List<Pago>`. Internamente `Funciones` ahora trabaja con `List<PagoEfectuado>`, y los retornos hacen `Cast<Pago>().ToList()`. Esto evita tocar `HomeController` y reduce casteos internos.

### 2.2 Eliminación de `TypeNameHandling.Auto`

`DatosSerializados.Pagos` y `NoValidos` pasaron de `List<Pago>` a `List<PagoEfectuado>`, eliminando la necesidad de `TypeNameHandling.Auto` en Newtonsoft.Json. El JSON resultante sigue siendo compatible hacia atrás: al deserializar con `TypeNameHandling.None`, el campo `$type` se ignora y se mapea al tipo declarado.

### 2.3 `InterpretarPDF` como orquestador

El método ahora solo coordina pasos de alto nivel. El parsing propiamente dicho, la resolución de ente/referencia, las validaciones y la deduplicación viven en métodos privados especializados.

### 2.4 Manejo de errores por archivo

Anteriormente un error inesperado dentro del loop de archivos abortaba todo el proceso. Ahora cada archivo se procesa dentro de un `try/catch` propio: si un PDF falla, se loguea y se continúa con el siguiente.

### 2.5 Preservación de comportamiento

Se mantuvo el acumulado de `text` entre páginas (`text += PdfTextExtractor.GetTextFromPage(...)`), aunque es un comportamiento discutible. Cambiarlo alteraría la salida y requiere pruebas de regresión con PDFs reales.

---

## 3. Refactorizaciones realizadas

### Fase 0 — Seguridad y baseline

| Cambio | Motivación |
|--------|------------|
| Remover `TypeNameHandling.Auto` | Vulnerabilidad de deserialización insegura. |
| Cambiar `DatosSerializados` a `List<PagoEfectuado>` | Evitar polimorfismo implícito en JSON. |
| Cambiar `lstModelos` y `noVal` a `List<PagoEfectuado>` | Eliminar casteos internos `(PagoEfectuado)p`. |
| `PdfReader` dentro de `using` | Evitar leak de handles de PDF. |
| `try/catch` por archivo | Robustez: un PDF malo no aborta todo. |
| Eliminar bloque debug vacío, condición Nico duplicada, `if (_pago.Importe == 0) {}`, variable `importeDecimal` sin uso | Reducir ruido y deuda técnica. |
| Calcular importe Ungar con `Convert.ToDecimal(importeString, InvariantCulture)` | Reemplazar lógica confusa que calculaba y descartaba el valor. |

### Fase 1 — Descomposición en métodos privados

`InterpretarPDF` pasó de ~1000 líneas a ~87 líneas. Se extrajeron los siguientes métodos privados en `AdminPagosDLL/Core/Funciones.cs`:

```text
ObtenerDirectorio()
ObtenerArchivosPdf(string directorio)
ObtenerLimiteArchivos(int cantidadTotal)
CargarEstadoPrevio(out bool hasCache)
ObtenerRangoPaginas(PdfReader reader, string nombreArchivo, string path)
EsComprobantePago(string texto, string nombreArchivo)
ProcesarTextoComprobante(string text, string path, string nombreArchivo, PdfReader reader, Formatos format, bool ifExpensasHY)
ResolverReferencia(PagoEfectuado pago, string text, string nombreArchivo)
ResolverEnteFallback(PagoEfectuado pago, string text, string path, string nombreArchivo, PdfReader reader, Formatos format)
AplicarValidacionesFinales(PagoEfectuado pago, int cantLineas)
AgregarPago(PagoEfectuado pago)
LimpiarTimestampsHuérfanos(List<string> archivosActuales)
```

#### Flujo actual de `InterpretarPDF`

```csharp
public List<PagoEfectuado> InterpretarPDF(string path = "")
{
    // 1. Setup
    string directorio = ObtenerDirectorio();
    var lstArchivos = ObtenerArchivosPdf(directorio);
    var oldState = CargarEstadoPrevio(out bool hasCache);
    var format = new Formatos();
    int cantidadArchivosAux = ObtenerLimiteArchivos(lstArchivos.Count);

    // 2. Procesar cada PDF
    for (int i = 0; i < cantidadArchivosAux; i++)
    {
        path = lstArchivos[i];
        string nombreArchivo = Path.GetFileName(path);

        // Skip si no cambió desde la última vez
        // try/catch por archivo
        // Abrir PDF con PdfReader (using)
        // Extraer texto del rango de páginas determinado
        // Validar que sea un comprobante de pago
        // Procesar texto -> PagoEfectuado
        // AgregarPago(pago)
    }

    // 3. Limpieza y persistencia
    LimpiarTimestampsHuérfanos(lstArchivos);
    SerializarDatos();
    return lstModelos;
}
```

#### Flujo actual de `ProcesarTextoComprobante`

```csharp
private PagoEfectuado ProcesarTextoComprobante(...)
{
    using (StringReader readerTxt = new StringReader(text))
    {
        var _pago = new PagoEfectuado { Path = path };
        bool newFormatPdf = DetectarNuevoFormato(reader, text);
        int cantLineas = 0;

        while ((line = readerTxt.ReadLine()) != null)
        {
            cantLineas++;
            // switch gigante por clave de línea (Fecha, Importe, NroCliente, etc.)
        }

        ResolverReferencia(_pago, text, nombreArchivo);
        ResolverEnteFallback(_pago, text, path, nombreArchivo, reader, format);
        AplicarValidacionesFinales(_pago, cantLineas);

        return _pago;
    }
}
```

---

## 4. Servicios extraídos

Aún no se extrajeron clases de servicio (eso corresponde a la Fase 3). Los métodos privados actuales son los candidatos naturales a convertirse en servicios:

| Método actual | Futuro servicio |
|---------------|-----------------|
| `ObtenerDirectorio()` / `ObtenerArchivosPdf()` | `IPdfDirectoryScanner` / `IPagoSource` |
| `PdfTextExtractor.GetTextFromPage` + `ObtenerRangoPaginas()` | `IPdfTextExtractor` |
| `EsComprobantePago()` | `IComprobanteValidator` |
| `ProcesarTextoComprobante()` | `IPagoParser` / pipeline de parsers |
| `ResolverReferencia()` | `IReferenciaResolver` |
| `ResolverEnteFallback()` | `IEnteResolver` |
| `AplicarValidacionesFinales()` | `IPagoValidator` / `ICotizacionService` |
| `AgregarPago()` | `IPagoRepository` (o `IPagoDeduplicator`) |

---

## 5. Patrones utilizados

- **Single Responsibility Principle (SRP):** cada método privado tiene una responsabilidad clara.
- **Extract Method:** principal técnica de la Fase 1.
- **Facade:** `Funciones` actúa como fachada; `InterpretarPDF` orquesta.
- **Strategy (parcial):** `ObtenerRangoPaginas` y `EsComprobantePago` son pasos hacia un pipeline de parsers especializados.

---

## 6. Tareas pendientes

### Fase 2 — Unificar resolución de `Referencia`

**Problema:** existen dos mecanismos paralelos:

1. `ClienteMap` (diccionario estático `Dictionary<string, (EEnte, EReferencia)>`): mapea `NroCliente` → `(Ente, Referencia)`.
2. Switch gigante en `ResolverReferencia()`: mapea `NroCliente` → `Referencia`.

**Bloqueo identificado:** hay mapeos conflictivos. Ejemplo:

```csharp
// ClienteMap
{ "030006245390", (EEnte.Edesur, EReferencia.Yrigoyen) }

// ResolverReferencia switch (comentario original)
case "030006245390": // Metrogas-Yrigoyen
```

No se consolidó automáticamente porque cambiar el mapeo afecta directamente la clasificación de facturas. Requiere validación del usuario con PDFs reales.

**Trabajo pendiente:**
- Revisar cada `NroCliente` del switch y agregarlo a `ClienteMap` con el `EEnte` correcto.
- Reemplazar el switch de `ResolverReferencia()` por una consulta a `ClienteMap` (posiblemente con un método que solo devuelva `EReferencia`).
- Resolver el conflicto de `"030006245390"` y otros similares.

### Fase 3 — Extraer servicios de dominio

Crear clases en `AdminPagosDLL/Core/`:

```text
IEnteResolver / EnteResolver
IReferenciaResolver / ReferenciaResolver
IPdfTextExtractor / PdfTextExtractor
IPagoValidator / PagoValidator
IComprobanteValidator / ComprobanteValidator
```

Inyectarlos manualmente en `Funciones` (no hay contenedor DI en el proyecto actual).

### Fase 4 — Parsers por formato (Strategy)

Definir interfaz:

```csharp
public interface IPagoParser
{
    bool PuedeParsear(string texto, string nombreArchivo);
    PagoEfectuado Parsear(string texto, string path, string nombreArchivo, PdfReader reader);
}
```

Implementar parsers específicos para:
- Banco / comprobante tradicional
- MercadoPago
- Expensas San Rafael (HY)
- Expensas Villa Gesell (VG)
- Ungar
- MetroGAS
- Fallback

Registrar una lista ordenada de parsers en `Funciones`.

### Fase 5 — Tests de regresión y async final

- Crear proyecto de tests compatible con .NET Framework 4.8 (MSTest/NUnit/xUnit).
- Generar fixtures de texto a partir de los PDFs reales de `D:\Norma\000   PAGOS` (anonimizados).
- Tests unitarios para `EsComprobantePago`, `ObtenerRangoPaginas`, `ResolverReferencia`, `ResolverEnteFallback`, etc.
- Tests de integración con PDFs reales para validar extracción.
- Async en I/O de archivos y serialización (iTextSharp 5.x seguirá siendo sync).

---

## 7. Archivos modificados

- `AdminPagosDLL/Core/Funciones.cs` — refactor principal.
- `AdminPagosDLL/Models/DatosSerializados.cs` — cambio de tipos para eliminar `TypeNameHandling.Auto`.
- Otros `.cs` (`CotizacionHistorica.cs`, `Formatos.cs`, `Logger.cs`, `Enumerables.cs`, `Mensaje.cs`) tienen solo cambios de formato/whitespace por `dotnet format`.

## 8. Estado de compilación

```bash
dotnet build AdminPagosDLL/AdminPagosDLL.csproj
```

Resultado: **0 advertencias, 0 errores**.

El proyecto web `App` no se puede compilar con `dotnet build` en este entorno porque falta `Microsoft.WebApplication.targets` del SDK de .NET Framework, pero no requiere cambios: `HomeController` sigue usando `funcion.CargarPagosAsync()`, `funcion.noVal.OfType<PagoEfectuado>()`, `funcion.noVal.Count`, etc., todos compatibles con los nuevos tipos.

---

## 9. Notas para el siguiente modelo

- **No iniciar Fase 3 sin resolver Fase 2.** Si se extraen servicios antes de unificar `ClienteMap`, se arrastrará la duplicación a las nuevas clases.
- Antes de tocar lógica de parsing, generar fixtures de texto de PDFs reales y crear tests de regresión.
- Preservar la API pública de `Funciones` (`CargarPagos`, `CargarPagosAsync`) para no romper `HomeController`.
- `ProcesarTextoComprobante` todavía contiene el switch gigante (~200 líneas); es el próximo candidato a dividirse en parsers por formato.
- El acumulado de `text` entre páginas es comportamiento legado; decidir si corregirlo requiere comparar salidas con PDFs reales.
