# Resumen técnico del refactor de `InterpretarPDF`

> Estado: Fases 0, 1, 2, 4 y 5 completadas. Fase 3 descartada (sobre-ingeniería).
> Fecha: 2026-06-25
> Proyecto: AdministradorDePagos (.NET Framework 4.8)
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

## 4. Métodos extraídos

En la Fase 1 se extrajeron 12 métodos privados. En la Fase 4 se sumaron 2 más, llevando `ProcesarTextoComprobante` de ~230 a ~30 líneas:

| Método extraído | Responsabilidad |
|---|---|
| `ParsearExpensasHY()` | Escanea texto buscando patrón `"016"`, extrae datos de expensas San Rafael |
| `ParsearLineasEstandar()` | Switch `clave:valor` genérico + casos especiales MercadoPago/Ungar |

Los métodos extraídos en Fase 1 (`ResolverReferencia`, `ResolverEnteFallback`, `AplicarValidacionesFinales`, etc.) se mantienen como orquestadores post-parsing.

---

## 5. Patrones utilizados

- **Single Responsibility Principle (SRP):** cada método privado tiene una responsabilidad clara.
- **Extract Method:** principal técnica de las Fases 1 y 4.
- **Facade:** `Funciones` actúa como fachada; `InterpretarPDF` orquesta.
- **Table-driven / Lookup:** `ClienteMap` unificado como única fuente de verdad para `NroCliente → (Ente, Referencia)`, reemplazando un switch duplicado.

---

## 6. Tareas pendientes

### Fase 2 — Unificar resolución de `Referencia` ✅ COMPLETADA

**Objetivo:** eliminar la duplicación entre `ClienteMap` (diccionario estático) y el switch gigante en `ResolverReferencia()`.

**Cambios realizados:**

- Corregidas 2 entradas erróneas en `ClienteMap`:
  - `"030006245390"`: `(Edesur, Yrigoyen)` → `(Metrogas, Yrigoyen)` (validado por el usuario)
  - `"00251361487"`: `(Edesur, Yrigoyen)` → `(Arba, Yrigoyen)` (validado por el usuario)
- Agregadas 14 entradas faltantes a `ClienteMap` con el `EEnte` y `EReferencia` validados.
- Nuevo valor `Guille` en el enum `EReferencia` (para Claro).
- Switch de ~70 líneas en `ResolverReferencia()` reemplazado por `ClienteMap.TryGetValue()` (4 líneas) + fallback textual preservado.
- `ClienteMap` quedó con **38 entradas** como única fuente de verdad para el mapeo `NroCliente → (Ente, Referencia)`.
- Método `ResolverReferencia()` pasó de ~105 líneas a ~40 líneas.

### Fase 3 — Extraer servicios de dominio ❌ DESCARTADA

**Decisión del usuario:** descartada el 2026-06-25 por considerarse sobre-ingeniería para un sistema monousuario simple. Los beneficios de testabilidad y desacople no justificaban la complejidad adicional (interfaces, cableado manual sin DI, más archivos). Se pasó directamente a la Fase 4.

### Fase 4 — Extraer parsers por formato ✅ COMPLETADA (versión pragmática)

**Enfoque:** en lugar del Strategy pattern con interfaz `IPagoParser` + múltiples implementaciones, se aplicó Extract Method a los dos algoritmos de parsing radicalmente distintos que estaban entremezclados en `ProcesarTextoComprobante`.

**Análisis previo:** el sistema tiene solo dos algoritmos de parsing reales, no 6 formatos distintos. MercadoPago, Ungar y Claro no son parsers independientes, sino casos extra dentro del switch estándar.

| Algoritmo | Cuándo se usa | Lógica |
|---|---|---|
| **Expensas HY** | `ifExpensasHY = true` (PDFs San Rafael) | Escanea líneas buscando `"016"`, extrae datos de columnas |
| **Genérico** | Todo lo demás | Línea por línea, split por `:`, switch `clave → valor` |

**Cambios realizados:**

- **`ParsearExpensasHY(PagoEfectuado, string, string, Formatos)`** (~30 líneas): método privado nuevo que encapsula el bloque `if (ifExpensasHY) { ... }`.
- **`ParsearLineasEstandar(PagoEfectuado, string, Formatos, bool)`** (~170 líneas): método privado nuevo que encapsula el `while` + switch de parsing genérico. Retorna `cantLineas`.
- **`ProcesarTextoComprobante`**: pasó de ~230 líneas a **~30 líneas**. Ahora es un orquestador limpio que detecta `newFormatPdf`, delega a `ParsearExpensasHY` o `ParsearLineasEstandar`, y luego aplica `ResolverReferencia` + `ResolverEnteFallback` + `AplicarValidacionesFinales`.

**Resultado:**

| Método | Antes | Después |
|---|---|---|
| `ProcesarTextoComprobante` | ~230 líneas | ~30 líneas |
| `ParsearLineasEstandar` (nuevo) | — | ~170 líneas |
| `ParsearExpensasHY` (nuevo) | — | ~30 líneas |

El switch grande queda encapsulado en `ParsearLineasEstandar`. Si en el futuro un formato nuevo necesita un algoritmo distinto, se crea otro `ParsearX` sin tocar el estándar.

### Fase 5 — Tests de regresión y async final ✅ COMPLETADA

**Proyecto de tests:** creado como `AdminPagosDLL.Tests` (consola autocontenida, sin dependencias externas). Runner autoejecutable que lanza 83 tests y reporta pass/fail.

**Tests unitarios (12 módulos):**

| Módulo | Qué testea |
|---|---|
| `IdentifyEnte` | 14 tests: todos los entes, casos edge (vacío, null, irrelevante) |
| `GetEnteDisplayText` | 2 tests: ente conocido y desconocido |
| `ExtractEnteFromLine` | 3 tests: Claro, Edesur, Metrogas |
| `ClienteMap` | 5 tests: count (41 entradas), correcciones validadas, Guille |
| `TryMapClienteData` | 3 tests: existente, inexistente, datos correctos |
| `EsComprobantePago` | 9 tests: todos los casos positivos y negativos |
| `ResolverReferencia` | 7 tests: ClienteMap (todas las referencias) + fallback textual |
| `ParsearExpensasHY` | 5 tests: simple, sin 016, multilínea |
| `ParsearLineasEstandar` | 10 tests: todos los campos, múltiples campos, línea count |
| `AplicarValidacionesFinales` | 7 tests: fechas, tipo comprobante, cuota, futura |
| `ResolverEnteFallback` | 3 tests: bonaerenses, cevige, ente ya conocido |
| **Integration** | 2 tests: extracción real de PDF + InterpretarPDF end-to-end (1823 pagos encontrados) |

**Resultado:** 83 passed, 0 failed.

**Async:** Ya implementado en fases previas:
- `SerializarDatosAsync` → `StreamWriter.WriteAsync`
- `DeSerializeDatosAsync` → `StreamReader.ReadToEndAsync`
- `CargarPagosAsync` → `Task.Run(() => InterpretarPDF(...))` para I/O pesado
- `CotizacionHistorica` → `HttpClient` async
- iTextSharp 5.x permanece sync (no tiene API async), envuelto en `Task.Run`

---

## 7. Archivos modificados

- `AdminPagosDLL/Core/Funciones.cs` — refactor principal (Fases 0, 1, 2 y 4). Métodos cambiados de `private` a `internal` para testeo.
- `AdminPagosDLL/Models/DatosSerializados.cs` — cambio de tipos para eliminar `TypeNameHandling.Auto`.
- `AdminPagosDLL/Models/Enumerables.cs` — agregado `Guille` al enum `EReferencia`.
- `AdminPagosDLL/Properties/AssemblyInfo.cs` — agregado `InternalsVisibleTo("AdminPagosDLL.Tests")`.
- `AdminPagosDLL.Tests/` — nuevo proyecto de tests (consola autocontenida, 83 tests).
- `AdministradorDePagos.sln` — agregado proyecto de tests.
- Otros `.cs` (`CotizacionHistorica.cs`, `Formatos.cs`, `Logger.cs`, `Mensaje.cs`) tienen solo cambios de formato/whitespace por `dotnet format`.

## 8. Estado de compilación

```bash
dotnet build AdminPagosDLL/AdminPagosDLL.csproj
```

Resultado: **0 advertencias, 0 errores**.

El proyecto web `App` no se puede compilar con `dotnet build` en este entorno porque falta `Microsoft.WebApplication.targets` del SDK de .NET Framework, pero no requiere cambios: `HomeController` sigue usando `funcion.CargarPagosAsync()`, `funcion.noVal.OfType<PagoEfectuado>()`, `funcion.noVal.Count`, etc., todos compatibles con los nuevos tipos.

---

## 9. Notas finales

- **El refactor de `InterpretarPDF` está completo** (Fases 0, 1, 2, 4 y 5).
- Para correr los tests: `dotnet build AdminPagosDLL.Tests && AdminPagosDLL.Tests\bin\Debug\AdminPagosDLL.Tests.exe`
- `ProcesarTextoComprobante` es un orquestador de ~30 líneas. El switch grande vive en `ParsearLineasEstandar`.
- `ClienteMap` es la única fuente de verdad para `NroCliente → (Ente, Referencia)` con 41 entradas.
- Preservar la API pública de `Funciones` (`CargarPagos`, `CargarPagosAsync`) para no romper `HomeController`.
- El acumulado de `text` entre páginas es comportamiento legado; decidir si corregirlo requiere comparar salidas con PDFs reales.
- Si se agrega un nuevo formato de PDF, crear un nuevo `ParsearX` sin tocar `ParsearLineasEstandar`.
