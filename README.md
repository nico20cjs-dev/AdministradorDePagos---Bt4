# Administrador de Pagos

Visor y gestor de comprobantes de pago en PDF. Escanea recibos de home banking (principalmente bancos argentinos), extrae los datos automáticamente y los muestra en una tabla interactiva con búsqueda, filtros y conversión a USD.

## Cómo funciona

1. Apuntás una carpeta llena de PDFs de pagos (servicios, impuestos, expensas).
2. La app los lee con **iTextSharp**, extrae fecha, importe, entidad, cliente, vencimiento, etc.
3. Los clasifica por persona/propiedad (Nico, Norma, Yrigoyen, Velez, Tía Raquel, Villa Gesell).
4. Convierte cada importe a USD usando la cotización histórica del BCRA (API de `apis.datos.gob.ar`).
5. Te muestra todo en una **DataTable** con búsqueda, filtros por columna y totales actualizados al instante.

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET Framework 4.8, ASP.NET MVC 5 |
| PDF | iTextSharp 5.5.13.1 |
| Frontend | jQuery 3.5.1, DataTables, CSS3 custom |
| API externa | `apis.datos.gob.ar` (cotizaciones históricas) |
| Caché | Serialización XML + JSON file |
| DB | Ninguna (todo file-based, hay un `DatosDB/` esqueleto pero sin implementar) |

## Proyectos

```
AdministradorDePagos.sln
├── App/                              → Web MVC (controllers, views, assets)
│   ├── Controllers/HomeController.cs → único controller
│   ├── Views/Home/Index.cshtml       → SPA de una sola página (template autocontenido)
│   ├── App_Start/                    → RouteConfig, FilterConfig, BundleConfig
│   ├── css/custom.css                → Dark/light mode con CSS custom properties
│   ├── js/generales.js               → Helpers JS
│   └── vendor/datatables/            → DataTables local
│
├── AdminPagosDLL/                    → Class Library (lógica de negocio)
│   ├── Core/
│   │   ├── Funciones.cs              → Parsing de PDF + serialización
│   │   ├── CotizacionHistorica.cs    → Carga de cotizaciones desde API/JSON
│   │   ├── Formatos.cs               → Parsing de fechas
│   │   ├── FMensaje.cs               → Agregador de mensajes
│   │   └── Logger.cs                 → Logger a archivo
│   └── Models/
│       ├── Pago.cs                   → Base: FechaPago, Importe, ImporteDolar
│       ├── PagoEfectuado.cs          → Hereda: NroTransaccion, Ente, NroCliente, etc.
│       ├── Mensaje.cs                → Modelo de mensajes al usuario
│       └── Enumerables.cs            → ETipoComprobante, ETipoMensaje, EReferencia
│
├── DatosDB/                          → Proyecto esqueleto (abandonado)
└── packages/                         → NuGet packages cache
```

## Modelo de datos

```
Pago (base)
├── FechaPago : DateTime
├── Importe : decimal
├── ImporteDolar : decimal        → Importe / cotización del día
└── ImporteEnDolares : decimal    → duplicado sin usar

PagoEfectuado : Pago
├── NroTransaccion : string
├── Ente : string                 → "Edesur", "Metrogas", "Arba Inmobiliario", etc.
├── NroCliente : string
├── NroCtaDebito : string
├── FechaVencimiento : DateTime
├── Cuota : string                → "006/17"
├── Concepto : string
├── Path : string                 → ruta al PDF original
├── TipoComprobante : enum        → Completo / Reducido
└── Referencia : enum             → Nico, Norma, Yrigoyen, VelezSarsfield, TiaRaquel, VillaGesell
```

## Endpoints

| Ruta | Método | Descripción |
|---|---|---|
| `/` | GET | Página principal: carga la tabla vacía y la cotización histórica |
| `Home/LeerPDF` | GET JSON | Escanea todos los PDFs (o devuelve caché) y los manda como JSON |
| `Home/ActualizarPDF` | GET JSON | Limpia caché y re-escanea todo |
| `Home/GetReport?path=...` | GET | Sirve un PDF individual con validación anti-path-traversal |

## Configuración (`Web.config`)

```xml
<appSettings>
    <!-- Carpeta donde están los PDFs. Si no está, usa D:\Norma\000   PAGOS\ -->
    <add key="RutaDePagos" value="C:\Users\tuuser\Pagos"/>
    <!-- Cantidad de archivos a leer (-1 = todos). Sirve para debug -->
    <add key="ArchivosLeidos" value="-1"/>
</appSettings>
```

> **Ojo:** la serialización XML de pagos se guarda en la ruta que indique la key `PagosSerializados` del config. Si no existe, se usa la carpeta por defecto.

## Para ponerlo a andar

1. Abrí `AdministradorDePagos.sln` en Visual Studio 2022.
2. Asegurate de tener .NET Framework 4.8 SDK.
3. Restaurá los paquetes NuGet (deberían estar en `packages/`).
4. Configurá `RutaDePagos` en `Web.config` apuntando a tu carpeta de PDFs.
5. Ejecutá (F5). La primera vez va a tardar porque parsea todos los PDFs; después usa caché.

## Cosas por hacer / deuda técnica conocida

**Del código:**
- [#36] `LstPagos` y `TxtCotizacionHistoria` son `static` y no son thread-safe → migrar a `MemoryCache` o similar.
- [#37] Ruta de pagos hardcodeada (`D:\Norma\000   PAGOS\`) como fallback si no está en config.
- [#38] `Pago.ImporteEnDolares` es un duplicado de `ImporteDolar`, uno de los dos sobra.
- Namespace `AdminPagosDLL.Controllers` está en el proyecto `App`, no en la DLL → confunde.
- `_ViewImports.cshtml` tiene `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` que es de .NET Core y no funciona acá. No rompe nada, está de adorno.

**Del TODO:**
- Implementar base de datos SQL (hay un proyecto `DatosDB` armado pero vacío).
- Filtros avanzados: por rango de fechas, rangos de importe, período.
- Exportar a PDF/CSV/HTML.
- Barra de progreso al leer PDFs (cuando son muchos, tarda).
- Identificar PDFs duplicados.
- Organizar PDFs en carpetas correctas automáticamente.
- Sincronización con DB (ABM de pagos).

## Notas

- Los PDFs protegidos con contraseña se saltan silenciosamente.
- Los formatos de PDF no reconocidos (Transferencias, Detalle de Movimientos, recibos de sueldo) se ignoran.
- Los pagos que no se pueden clasificar (ente vacío, fecha inválida, importe cero) van a la lista `noVal`.
- El modo oscuro se guarda en `localStorage`, no hace falta back-end para eso.
