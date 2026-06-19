# Mejoras para Administrador de Pagos

App orientada a Norma (72 años) para control y administración de pagos de servicios, organizados por tipo de servicio y propiedad/persona.

---

## 🔵 1. Resumen por propiedad en dólares (prioridad máxima)

Hoy los totales son globales (todo ARS + todo USD en dos cards). Agregar cards
por cada referencia (propiedad/persona) debajo de los totales globales:

```
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Yrigoyen │ │  Norma   │ │  Velez   │ │  Gesell  │ │   Nico   │ │ Raquel   │ │  Renee   │
│ $12.340  │ │ $8.500   │ │ $15.200  │ │ $9.800   │ │ $3.200   │ │ $5.600   │ │ $2.100   │
│ USD 340  │ │ USD 120  │ │ USD 410  │ │ USD 280  │ │ USD 90   │ │ USD 150  │ │ USD 60   │
└──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘
```

Se actualizan automáticamente al filtrar la tabla (búsqueda, filtros avanzados, paginación).

### Archivos a modificar
- `App/Scripts/app.js`: nueva función `totalizarPorReferencia()`, llamarla desde
  `totalizarPagos()` y `applyAdvancedFilters()`.
- `App/Views/Home/Index.cshtml`: agregar contenedor `.stats-grid-ref` debajo del
  `.stats-grid` existente.
- `App/css/custom.css`: estilos para las nuevas cards.

---

## 🔵 2. Filtros con un clic por propiedad (sin sidebar)

Hoy hay que abrir sidebar → dropdown → elegir → Aplicar → Limpiar.
Agregar pills/botones arriba de la tabla (entre column-toggle y table-wrap):

```
[🏠 Todas] [🏠 Yrigoyen] [🏠 Norma] [🏠 Nico] [🏠 Velez] [🏠 Gesell] [🏠 Raquel] [🏠 Renee]
```

Un clic filtra la tabla y actualiza los totals de las cards de resumen.

### Archivos a modificar
- `App/Views/Home/Index.cshtml`: agregar contenedor `.filter-pills` antes de la
  tabla con los botones.
- `App/Scripts/app.js`: handler de click en los pills, usa el custom filter de
  DataTables (columna Referencia / índice 11). Propagar a `totalizarPagos()`.
- `App/css/custom.css`: estilos para pills (`.pill`, `.pill-active`).

---

## 🔵 3. Modo "Texto grande" (accesibilidad)

La tabla usa fuentes de `0.72rem` a `0.85rem`. Para 72 años es chico.
Agregar toggle en el header que aumente la escala ~30%:

```css
html[data-font="large"] {
    font-size: 130%;
}
```

Persistir en `localStorage` como `adp-fontsize`.

### Archivos a modificar
- `App/Views/Home/Index.cshtml`:
  - Agregar script bloqueante en `<head>` que lea `localStorage` y aplique
    `data-font` antes del paint (similar al theme flash fix).
  - Agregar botón `#fontSizeToggle` en `.topbar-right`.
- `App/Scripts/app.js`: función `setFontSize(size)`, handler del toggle.
- `App/css/custom.css`: regla `html[data-font="large"]` con `font-size: 130%`.

---

## 🔵 4. Simplificar vista por defecto

Ajustar las columnas visibles inicialmente para que Norma vea solo lo esencial:

**Visibles por defecto:** Ente, Fecha Vto, Fecha Pago, Importe $, Importe USD,
PDF, Referencia.

**Ocultas por defecto:** Nro Transacción, Nº Cliente, Nº CtaDébito, Cuota,
Tipo Comp. Siguen disponibles desde los toggles.

Mover los filtros avanzados (Importe desde/hasta) detrás de un panel colapsable
"Avanzado" dentro de la sidebar.

### Archivos a modificar
- `App/Scripts/app.js`: cambiar array de columnas ocultas en `initComplete`
  (línea 468) de `[0, 2, 3, 5, 7]` a `[0, 2, 3, 5, 7, 8?]` según el nuevo
  orden. Revisar índices de columnas.
- `App/Views/Home/Index.cshtml`: reorganizar `#showHiddenCol` para que los
  toggles de columnas visibles por defecto aparezcan primeros.
- `App/css/custom.css`: ajustar si es necesario.

---

## 🟡 5. Exportar resumen a PDF o imprimir

Botón "Imprimir resumen" que genere una vista limpia con:

- Totales por propiedad (ARS + USD)
- Desglose por tipo de servicio dentro de cada propiedad
- Período (desde/hasta seleccionado o todo)

Dos enfoques posibles:
a) **Print CSS**: abrir la página con `@media print` que muestre una vista
   optimizada.
b) **PDF server-side**: endpoint que genere PDF con los datos actuales.

Enfoque (a) es más simple y no requiere librerías adicionales.

### Archivos a modificar
- `App/Views/Home/Index.cshtml`: botón "Imprimir resumen".
- `App/css/custom.css`: reglas `@media print` para ocultar controles, mostrar
  resumen.
- `App/Scripts/app.js`: handler del botón (`window.print()`).

---

## 🟡 6. Filtros por tipo de servicio

Pills similares a las de propiedad pero para tipo de servicio:

```
[⚡ Todos]  [⚡ Luz]  [🔥 Gas]  [💧 Agua]  [🏠 Expensas]  [🏛️ Municipal]  [📞 Teléfono]  [💰 Arba]
```

Combinables con los de propiedad (ej: "Yrigoyen + Luz" = solo pagos de luz en
Yrigoyen). Se agregan en la misma fila que los pills de propiedad.

### Archivos a modificar
- `App/Views/Home/Index.cshtml`: agregar segunda fila de pills.
- `App/Scripts/app.js`: filtro custom por columna Ente para cada categoría.
  Mapa de `Ente → categoría`:
  | Categoría | Entes |
  |-----------|-------|
  | Luz | Edesur, Cevige |
  | Gas | Metrogas |
  | Agua | Aguas Bonaerenses, AySA |
  | Expensas | Exp San Rafael, Exp Ungar |
  | Municipal | Municipalidad de Lanus, Municipalidad de Villa Gesell |
  | Telefonía | Claro, Movistar |
  | Arba | Arba |

---

## 🟡 7. Auto-guardado de filtros activos como "vista"

Hoy `stateSave` guarda filtros en sessionStorage. Opcionalmente permitir
guardar una "vista" con nombre que persista entre dispositivos.

Backend: guardar en `App_Data/vistas.json`.
Frontend: botón "Guardar vista" y dropdown "Cargar vista".

Prioridad baja, solo si las mejoras anteriores ya están implementadas.

### Archivos a modificar
- `AdminPagosDLL/Core/Funciones.cs` o nuevo helper: serialización de vistas.
- `App/Controllers/HomeController.cs`: endpoints `GuardarVista`, `CargarVista`,
  `ListarVistas`.
- `App/Views/Home/Index.cshtml`: UI para guardar/cargar vistas.
- `App/Scripts/app.js`: lógica cliente.

---

## 🟢 8. Contraste mejorado en modo claro

El light theme tiene `--muted: #64748b` sobre fondo blanco. Ratio de contraste
~4.5:1. Para adulto mayor conviene subir a ~7:1.

```css
:root {
    --muted: #475569;  /* actual: #64748b */
    --line: #cbd5e1;   /* actual: #e2e8f0 */
}
```

### Archivos a modificar
- `App/css/custom.css`: variables CSS del `:root`.

---

## Prioridad de implementación

| #  | Mejora                          | Esfuerzo | Impacto | Archivos |
|----|---------------------------------|----------|---------|----------|
| 1  | Resumen por propiedad           | Medio    | 🔥🔥🔥   | 3        |
| 2  | Filtros con un clic             | Bajo     | 🔥🔥🔥   | 3        |
| 3  | Modo texto grande               | Muy bajo | 🔥🔥🔥   | 3        |
| 4  | Simplificar vista default       | Bajo     | 🔥🔥     | 2-3      |
| 5  | Exportar resumen                | Medio    | 🔥🔥     | 2-3      |
| 6  | Filtros por tipo servicio       | Bajo     | 🔥🔥     | 2        |
| 7  | Guardar vistas                  | Medio    | 🔥       | 4        |
| 8  | Contraste mejorado              | Muy bajo | 🔥       | 1        |

---

**Orden sugerido:** 1 → 2 → 3 → 6 → 4 → 5 → 8 → 7
