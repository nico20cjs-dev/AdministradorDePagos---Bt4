# Mejoras para Administrador de Pagos

App orientada a Norma (72 años) para control y administración de pagos de servicios, organizados por tipo de servicio y propiedad/persona.

---

## ✅ Implementadas

Las siguientes mejoras ya fueron realizadas en sesiones anteriores y se eliminan de la lista de pendientes:

- **Resumen por propiedad en dólares** — Tarjetas por referencia debajo de los totales globales, se actualizan al filtrar. (stats-grid-ref + renderRefCards)
- **Simplificar vista por defecto** — Columnas `[0, 2, 3, 5, 7]` ocultas al iniciar, disponibles desde toggles. Sidebar colapsable.
- **Columna USD Actualizados** — Nueva columna con inflación US CPI compuesta mensualmente, con modal informativo.
- **Validación de fecha de pago futura** — Si FechaPago > hoy, se usa FechaVencimiento como fallback.
- **Mensajes de DataTable en español** — emptyTable, zeroRecords, info, search, paginate traducidos.
- **StateSave sin column visibility** — La visibilidad de columnas ya no se guarda en localStorage; siempre arrancan ocultas.
- **Doble enumeración de directorio eliminada** — `EnumerateFiles` + `Count()` + `ToList()` reemplazado por `GetFiles().ToList()` único.
- **Código muerto eliminado** — Propiedad `ImporteEnDolares` y referencias `System.Drawing`, `System.Web.DynamicData`, `System.Web.Entity`, `System.Web.Extensions`, `System.Web.Services` del `.csproj`.
- **Lógica duplicada de inflación factorizada** — Bloque de inflación + ente display extraído a método `AplicarInflacionYEnteDisplay()`.

---

## Pendientes (priorizados)

---

### 🔴 Alta prioridad

#### 1. Seguridad: reemplazar `TypeNameHandling.Auto`

**Problema:** `Funciones.cs` usa `TypeNameHandling.Auto` en `JsonConvert.SerializeObject` y `DeserializeObject`. Esto permite deserialización de tipos arbitrarios. Si alguien modifica el archivo `pagos_serializados.json` manualmente podría ejecutar código malicioso.

**Solución:** Reemplazar por un DTO sin herencia polimórfica, o usar `TypeNameHandling.None` con `[JsonInheritanceConverter]` explícito.

**Archivos:**
- `AdminPagosDLL/Core/Funciones.cs` — `SerializarDatos()`, `DeSerializeDatos()`
- `AdminPagosDLL/Models/DatosSerializados.cs` — posible refactor

**Esfuerzo:** Bajo

---

#### 2. Agregar toggles de columna faltantes

**Problema:** En `Index.cshtml` los botones `toggle-vis` cubren columnas 0-7, 10, 11, 12 pero faltan para `Fecha Pago` (col 8) e `Importe $` (col 9).

**Solución:** Agregar los botones faltantes y reordenar para que coincidan visualmente con el orden de las columnas en la tabla.

**Archivos:**
- `App/Views/Home/Index.cshtml` — agregar toggles para columnas 8 y 9
- `App/css/custom.css` — ajustar layout si es necesario

**Esfuerzo:** Muy bajo

---

### 🟡 Media prioridad

#### 4. Refactor mayor: unificar identificación de referencia

**Problema:** Existen dos mecanismos paralelos e inconsistentes para determinar `Referencia`:
1. `ClienteMap` (diccionario estático en Funciones.cs:145-184) — mapea NroCliente → (Ente, Referencia)
2. Switch inline enorme en InterpretarPDF (líneas 768-838) — hace lo mismo manualmente

El mecanismo 1 se llama `TryMapClienteData`, el mecanismo 2 se ejecuta después. La misma información mantenida en dos lugares.

**Solución:** Unificar todo en `ClienteMap`. Eliminar el switch duplicado. Si hay casos no cubiertos por el mapa, agregarlos al mapa.

**Archivos:**
- `AdminPagosDLL/Core/Funciones.cs`
- Posible regresión: requiere testing porque la lógica actual tiene dos pasadas

**Esfuerzo:** Medio

---

#### 5. Refactor mayor: descomposición de InterpretarPDF

**Problema:** El método `InterpretarPDF()` tiene ~1100 líneas. Mezcla:
- Navegación de páginas PDF (pageRead/pageEnd según tipo de PDF)
- Extracción de texto con iTextSharp
- Parsing de campos con switches
- Identificación de ente por texto
- Determinación de referencia
- Validaciones de fechas
- Duplicación lógica

**Solución:** Extraer responsabilidades en métodos más pequeños:
- `ExtraerTexto(pageRange)` → texto plano
- `ParsearPagoEfectuado(texto)` → PagoEfectuado
- `DeterminarReferencia(pago, texto)`
- `CalcularImporteDolar(pago)`

**Archivos:**
- `AdminPagosDLL/Core/Funciones.cs`

**Esfuerzo:** Alto — requiere testing extensivo de regresión en parsing de PDFs.

---

#### 6. Reemplazar WebClient por HttpClient

**Problema:** `HomeController.InicializarCotizacionHistorica()` usa `WebClient` (deprecado desde .NET Framework 4.5+).

**Solución:** Reemplazar por `HttpClient`. Además agregar timeout configurable y manejo de errores más robusto.

**Archivos:**
- `App/Controllers/HomeController.cs`

**Esfuerzo:** Bajo

---

#### 7. Compilar Regex para mejorar performance

**Problema:** Varios patrones regex se usan dentro de loops en `InterpretarPDF()` sin compilar (`Regex.Match`, `Regex.Replace`). En cada iteración se parsea el string del patrón de nuevo.

**Solución:** Declarar como `static readonly Regex` compilados:
```csharp
private static readonly Regex _patronImporte = new Regex(@"...", RegexOptions.Compiled);
```

**Archivos:**
- `AdminPagosDLL/Core/Funciones.cs`

**Esfuerzo:** Bajo

---

### 🟢 Baja prioridad

#### 8. Botón de exportación a CSV

**Problema:** No hay forma de exportar los datos filtrados de la tabla.

**Solución:** Agregar botón que descargue las filas visibles (o todas las filtradas) como CSV usando JavaScript (DataTables tiene extensiones `buttons` para esto, o implementación manual).

**Archivos:**
- `App/Views/Home/Index.cshtml` — botón
- `App/Scripts/app.js` — función de exportación
- `App/css/custom.css` — estilo del botón

**Esfuerzo:** Medio

---

#### 9. Tema oscuro/claro respetando preferencia del sistema

**Problema:** En la primer visita siempre se forza dark mode. No respeta `prefers-color-scheme` del navegador.

**Solución:** En el script bloqueante del `<head>`, si no hay `localStorage.getItem('adp-theme')`, leer `matchMedia('(prefers-color-scheme: light)')`.

**Archivos:**
- `App/Views/Home/Index.cshtml` — script bloqueante en línea 13-18

**Esfuerzo:** Muy bajo

---

#### 10. Operaciones I/O asincrónicas

**Problema:** Todas las operaciones de archivo, red y PDF parsing son síncronas. En un app single-user no es crítico, pero bloquea el thread de IIS.

**Solución:** Migrar a `async/await`: `File.ReadAllTextAsync`, `HttpClient.GetStringAsync`, etc. El controller devolvería `async Task<ActionResult>`.

**Archivos:**
- `App/Controllers/HomeController.cs`
- `AdminPagosDLL/Core/Funciones.cs`
- `AdminPagosDLL/Core/CotizacionHistorica.cs`

**Esfuerzo:** Medio

---

#### 11. Filtros con un clic por propiedad (pills)

**Problema:** Hoy hay que abrir sidebar → dropdown → elegir. No hay acceso rápido.

**Solución:** Agregar pills/botones arriba de la tabla para filtrar por referencia con un clic.

**Archivos:**
- `App/Views/Home/Index.cshtml`
- `App/Scripts/app.js`
- `App/css/custom.css`

**Esfuerzo:** Bajo

---

#### 12. Modo "Texto grande" (accesibilidad)

**Problema:** La tabla usa fuentes de 0.72rem a 0.85rem. Para una usuaria de 72 años es pequeño.

**Solución:** Toggle que aumente la escala ~30% con `html[data-font="large"]` persistido en localStorage.

**Archivos:**
- `App/Views/Home/Index.cshtml`
- `App/Scripts/app.js`
- `App/css/custom.css`

**Esfuerzo:** Muy bajo

---

#### 13. Filtros por tipo de servicio

**Problema:** No hay forma de filtrar por categoría de servicio (luz, gas, agua, etc.).

**Solución:** Pills combinables con los de propiedad, agrupando entes por categoría.

**Archivos:**
- `App/Views/Home/Index.cshtml`
- `App/Scripts/app.js`
- `App/css/custom.css`

**Esfuerzo:** Bajo

---

#### 14. Exportar resumen a PDF o imprimir

**Problema:** No hay vista imprimible del resumen.

**Solución:** Botón "Imprimir resumen" que use `@media print` para ocultar controles y mostrar datos limpios.

**Archivos:**
- `App/Views/Home/Index.cshtml`
- `App/Scripts/app.js`
- `App/css/custom.css`

**Esfuerzo:** Medio

---

#### 15. Auto-guardado de filtros activos como "vista"

**Problema:** No hay forma de guardar combinaciones de filtros con nombre.

**Solución:** Backend en JSON + UI para guardar/cargar vistas.

**Archivos:**
- `AdminPagosDLL/Core/Funciones.cs`
- `App/Controllers/HomeController.cs`
- `App/Views/Home/Index.cshtml`
- `App/Scripts/app.js`

**Esfuerzo:** Medio

---

#### 16. Contraste mejorado en modo claro

**Problema:** `--muted: #64748b` sobre fondo blanco tiene ratio ~4.5:1. Para adulto mayor conviene ~7:1.

**Solución:** Oscurecer `--muted` a `#475569` y `--line` a `#cbd5e1`.

**Archivos:**
- `App/css/custom.css`

**Esfuerzo:** Muy bajo

---

## Prioridad de implementación recomendada

| #  | Mejora                          | Esfuerzo | Impacto | Archivos |
|----|---------------------------------|----------|---------|----------|
| 1  | TypeNameHandling.Auto (seguridad)| Bajo    | 🔴 Alta | 2        |
| 2  | Toggles faltantes               | Muy bajo | 🔴 Alta | 1-2      |
| 3  | Unificar ClienteMap             | Medio    | 🟡 Media | 1       |
| 4  | Refactor InterpretarPDF         | Alto     | 🟡 Media | 1       |
| 5  | WebClient → HttpClient          | Bajo     | 🟡 Media | 1       |
| 6  | Regex compilados                | Bajo     | 🟡 Media | 1       |
| 7  | Exportación CSV                 | Medio    | 🟢 Baja  | 3       |
| 8  | prefers-color-scheme            | Muy bajo | 🟢 Baja  | 1       |
| 9  | Async I/O                       | Medio    | 🟢 Baja  | 3       |
| 10 | Pills de propiedad              | Bajo     | 🟡 Media | 3       |
| 11 | Modo texto grande               | Muy bajo | 🟡 Media | 3       |
| 12 | Pills por servicio              | Bajo     | 🟢 Baja  | 3       |
| 13 | Imprimir resumen                | Medio    | 🟢 Baja  | 3       |
| 14 | Guardar vistas                  | Medio    | 🟢 Baja  | 4       |
| 15 | Contraste mejorado              | Muy bajo | 🟢 Baja  | 1       |

---

**Orden sugerido:** 1 → 2 → 3 → 4 → 5 → 6 → 10 → 11 → 12 → 7 → 13 → 8 → 9 → 15 → 14
