# Registro de Mejoras y Arreglos - AdministradorDePagos

> Estado: [ ] Pendiente | [x] Hecho | [-] Descartado

---

## 🔴 BUGS (cosas rotas)

- [x] **#1** — `_Layout.cshtml`: `@RenderBody()` está ANTES del `<body>` — HTML inválido. El layout renderiza el contenido antes de abrir el tag `<body>`. Aunque Index.cshtml lo esquiva por ser autocontenido, el layout queda inutilizable para cualquier futura vista.
- [x] **#2** — `generales.js` (línea ~12): `convertContenedor()` tiene lógica rota. El `else` evalúa `if (contenedor == '')` que nunca puede ser verdad (ese caso ya fue evaluado). Si se pasa un selector real, la función devuelve `null`.
- [x] **#3** — `generales.js` (línea ~36): Color de mensajes incorrecto. Tipo `2` (Advertencia) asigna `'success'` (verde) en lugar de `'warning'` (amarillo).
- [x] **#4** — `generales.js` (línea ~93): `hourglass()` y su `setInterval` se ejecutan en cada carga, pero el `<div id="div1">` en Index.cshtml está comentado. Código que corre en vano.
- [x] **#5** — `Index.cshtml` (línea ~267): Dos botones con el mismo `id="exportReport"` — HTML inválido. Solo el primero responde a eventos JS.
- [x] **#6** — `Index.cshtml` (createdRow de DataTables): `data[colPath] = "pepe"` — código de debug olvidado.
- [x] **#7** — `HomeController.cs` (`GetReport`): Sin validación del parámetro `path` — vulnerabilidad de path traversal. Cualquiera puede pedir cualquier archivo del servidor.
- [x] **#8** — `HomeController.cs` (`GetCacheCotizacionHistoria`): El método es `void` pero la vista lo llama vía AJAX esperando una respuesta JSON. La llamada siempre falla silenciosamente.
- [x] **#9** — `Index.cshtml` (Modal de Filtros): El botón "Filtrar" no hace nada. Cierra el modal (data-dismiss) pero no hay handler JS que aplique los filtros a la DataTable.

---

## 🟡 CÓDIGO MUERTO / SIN USAR (limpieza)

- [x] **#10** — `Index.cshtml`: Sidebar completa del template SB Admin 2 con links a páginas inexistentes (`buttons.html`, `cards.html`, `login.html`, `404.html`, etc.). Está completamente oculta con `class="... toggled ocultar"`.
- [x] **#11** — `Index.cshtml`: Tarjetas "Tasks (50%)" y "Pending Requests (18)" con datos hardcodeados y clase `ocultar`. Nunca se usan.
- [x] **#12** — `Index.cshtml`: Botón `#btnLeerPdf` oculto con `ocultar`. Su función es idéntica al `#btnActualizarPdf`. Redundante.
- [x] **#13** — `Index.cshtml`: jQuery cargado dos veces — una en el `<head>` (CDN) y otra al final del `<body>` (`vendor/jquery/jquery.min.js`).
- [x] **#14** — `Index.cshtml`: DataTables CSS duplicado — `dataTables.bootstrap4.min.css` (local) y `jquery.dataTables.css` (CDN) cargados al mismo tiempo.
- [x] **#15** — `Index.cshtml`: Scripts de gráficos del template sin uso — `Chart.min.js`, `chart-area-demo.js`, `chart-pie-demo.js`.
- [x] **#16** — `Index.cshtml`: Modal de Logout ("Ready to Leave?") con link a `login.html` que no existe.
- [x] **#17** — `Index.cshtml`: Función `agregarFilaFilter()` definida pero su llamada está comentada. Dead code; su lógica fue reimplementada directamente en el `document.ready`.
- [x] **#18** — `Index2.cshtml`: Página de prueba/desarrollo. No es accesible desde ningún link ni ruta del sistema.
- [x] **#19** — `AbmPago.cshtml`: Vista ABM incompleta sin route, sin actions en el controller y sin modelo real.
- [x] **#20** — `Tabla.cshtml`: Tabla con datos "Michael Bruce" hardcodeados. Artefacto del template original.
- [x] **#21** — `Privacy.cshtml`: Página de privacidad vacía/placeholder.
- [x] **#22** — `site.js` / `site.min.js`: Archivos vacíos con solo un comentario.
- [x] **#23** — `_CookieConsentPartial.cshtml`: Cookie consent sin implementar. Texto placeholder, sin lógica de persistencia.
- [x] **#24** — `_ValidationScriptsPartial.cshtml`: Scripts de validación jQuery no incluidos en ninguna vista.
- [x] **#25** — `HomeController.cs`: Acciones `About()` y `Contact()` definidas sin vistas correspondientes.
- [x] **#26** — `HomeController.cs` (línea ~7): `//using Microsoft.AspNetCore.Mvc;` comentado. Vestigio de una migración desde .NET Core.
- [x] **#27** — `Dolar.cs`: Clase vacía con credenciales de API en comentarios. No se usa en ningún lado.
- [x] **#28** — `PagoRealizado.cs`: Clase sin propiedades ni implementación. Nunca se instancia.
- [x] **#29** — `_Layout.cshtml`: Navbar y footer con links a About/Contact sin vistas. No se renderiza porque Index.cshtml es autocontenido, pero contamina el proyecto.

---

## 🟢 MEJORAS DE UX / UI

- [x] **#30** — Sin feedback de carga: al hacer click en "Actualizar Pagos" no hay spinner ni se deshabilita el botón. El usuario no sabe si está procesando.
- [x] **#31** — Importes sin formato moneda: se muestran como `377.1` en vez de `$ 377,10`.
- [x] **#32** — Footer con texto placeholder: "Copyright © Your Website 2022".
- [x] **#33** — Título de pestaña del browser incorrecto: dice "SB Admin 2 - Dashboard" en vez del nombre real de la app.
- [x] **#34** — Modal de filtros no funcional (ver Bug #9): botón "Filtrar" no aplica nada.
- [x] **#35** — Cards de Pesos/Dólares muestran `-` al entrar. Los datos se cargan solo al hacer click en el botón. Auto-carga al iniciar sería mejor UX.

---

## ⚙️ MEJORAS DE CÓDIGO / ARQUITECTURA

- [ ] **#36** — `HomeController.cs`: `LstPagos` y `TxtCotizacionHistoria` son `static`. Si dos requests concurren, los datos se pueden corromper. Usar caché thread-safe (`MemoryCache` o `ConcurrentDictionary`).
- [ ] **#37** — `Funciones.cs`: Ruta de pagos hardcodeada (`D:\Norma\000   PAGOS\`). Debería salir exclusivamente del `Web.config`.
- [ ] **#38** — `Pago.cs`: Propiedades comentadas sin limpiar y dos campos con nombre confuso (`ImporteDolar` / `ImporteEnDolares`).


Otras mejoras:
 - Informar las fechas max y minimas de los Pagos (del total de los pagos filtrados en ese momento)
 - paginado en español