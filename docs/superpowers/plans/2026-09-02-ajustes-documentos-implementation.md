# Ajustes en documentos y kardex — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cuatro ajustes de UI/consulta: nombre del tipo de documento en el kardex; quitar `% Impuesto` del encabezado y mostrar el total de impuesto bloqueado; dos buscadores de artículo (código / descripción) en el detalle; autocompletado del "Almacén por defecto" en Artículos.

**Architecture:** API N-capas (.NET 7) + Web MVC (.NET 8). §A enriquece `MovimientoInventarioDTO` con `TipoDocNombre` en la capa Application (LEFT JOIN lógico a `NumeracionDocumento.DocAlias`). §B/§C/§D son UI: se hacen canónicos en `EntregasCompra` y `Artículos`, y se transforman a las otras 6 pantallas de documento con tabla de sustitución.

**Tech Stack:** C# / .NET 7 (API) y .NET 8 (Web), EF Core, AutoMapper, xUnit + Moq, jQuery + Bootstrap.

**Spec:** `API/docs/superpowers/specs/2026-09-02-ajustes-documentos-design.md`

## Global Constraints

- **Repos y ramas:** API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`); Web en `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad git `panchoman08`. Sin push hasta aprobación final del usuario.
- **Build/test a carpeta externa:** `-p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"` (y `...\apitest\`, `...\webbuild\`).
- **`appsettings.json` (`API.Service.WebApi`) puede aparecer modificado localmente con un connection string real — NUNCA commitearlo.** `git add` con rutas explícitas, nunca `git add -A`.
- **Baseline suite API: 744 pruebas, 0 fallos.** `dotnet test API.sln` en verde antes de terminar cualquier tarea que toque la API. Gate de Web: `dotnet build Web.slnx` → `0 Errores`.
- **Los 7 documentos comerciales:** Cotización, Pedido, Entrega, Factura, Pedido de compra, Entrega de compra, Factura de compra. Los documentos de mercancía (Entrada/Salida) NO entran en §B ni §C.
- **`PrctjeImpuesto`:** se deja de renderizar en el encabezado; NO se toca ni la columna de BD ni la entidad ni el DTO de la API.
- **`PrctjeDesc` ("% Descuento") se mantiene** en el encabezado tal como está.
- **El hidden del artículo en el detalle sigue llamándose `#detCodArticulo`** (es lo que se postea); no renombrarlo.
- **`App.autocompletar().establecer(item)` re-dispara `onSeleccion`** (llama internamente a `elegir`/`limpiar`, que invocan `onSeleccion`). Toda sincronización entre las dos cajas de artículo DEBE llevar un flag anti-recursión.
- **`ArticuloDomain.ObtenerContengaCodigoAsync` / `ObtenerContengaNombreAsync` y `IArticuloApplication.ObtenerContenganCodigoAsync` / `ObtenerContenganNombreAsync` YA EXISTEN.**

---

## File Structure

**§A — Kardex (API + Web):**
- `API.Application.DTO/inventario/MovimientoInventarioDTO.cs` — + `TipoDocNombre`
- `API.Application.Main/MovimientoInventarioApplication.cs` — + ctor `INumeracionDocumentoDomain`; poblar `TipoDocNombre`
- `API.Service.WebApi.Tests/Application/MovimientoInventarioApplicationTests.cs` — nuevo (o donde vivan las de kardex)
- `Web.ApiClient/Dtos/MovimientoInventario/MovimientoInventarioDTO.cs` — + `TipoDocNombre`
- `Web.UI/wwwroot/js/existencias.js` — render de la celda del tipo de documento

**§D — Artículos / Almacén por defecto (Web):**
- `Web.UI/Controllers/ArticulosController.cs` — + `IAlmacenApiClient` en ctor; + `BuscarAlmacenes`, `ObtenerAlmacenPorCodigo`
- `Web.UI/Views/Articulos/_Form.cshtml` — `AlmacenDefecto` → bloque autocompletar
- `Web.UI/Views/Articulos/Crear.cshtml` — idem
- `Web.UI/wwwroot/js/articulos.js` — inicializar buscador (modal + página), resolver en edición

**§B + §C — canónico y transformaciones (Web):** por cada una de las 7 pantallas — `{Doc}Controller.cs` (+ `BuscarArticulosPorCodigo`), `Views/{Doc}/_Form.cshtml` (quitar `% Impuesto`, + `#TotalImp`, 2 buscadores), `wwwroot/js/{doc}.js` (`#TotalImp`, `aplicarArticuloSeleccionado`, `setArticuloEnAmbasCajas`, 2 buscadores).
Mapeo pantalla → archivos:
| Pantalla | Controller | Vista | JS |
|---|---|---|---|
| Entrega de compra (canónico) | `EntregasCompraController.cs` | `EntregasCompra/_Form.cshtml` | `entregascompra.js` |
| Factura de compra | `FacturasCompraController.cs` | `FacturasCompra/_Form.cshtml` | `facturascompra.js` |
| Entrega | `EntregasController.cs` | `Entregas/_Form.cshtml` | `entregas.js` |
| Factura | `FacturasController.cs` | `Facturas/_Form.cshtml` | `facturas.js` |
| Cotización | `CotizacionesController.cs` | `Cotizaciones/_Form.cshtml` | `cotizaciones.js` |
| Pedido | `PedidosController.cs` | `Pedidos/_Form.cshtml` | `pedidos.js` |
| Pedido de compra | `PedidosCompraController.cs` | `PedidosCompra/_Form.cshtml` | `pedidoscompra.js` |

---

## Task 1: §A — Nombre del tipo de documento en el kardex

**Files:**
- Modify: `API.Application.DTO/inventario/MovimientoInventarioDTO.cs`
- Modify: `API.Application.Main/MovimientoInventarioApplication.cs`
- Modify: `Web.ApiClient/Dtos/MovimientoInventario/MovimientoInventarioDTO.cs`
- Modify: `Web.UI/wwwroot/js/existencias.js`
- Test: `API.Service.WebApi.Tests/Application/MovimientoInventarioApplicationTests.cs` (crear)

**Interfaces:**
- Consumes: `INumeracionDocumentoDomain.ObtenerTodoAsync()` → `Task<IQueryable<NumeracionDocumento>>` (ya registrado en DI). `NumeracionDocumento` tiene `CodigoObj` (string), `SubTipoDoc` (string), `DocAlias` (string?).
- Produces: `MovimientoInventarioDTO.TipoDocNombre` (string?) = `DocAlias` de la fila `NumeracionDocumento` con `CodigoObj == TipoDoc && SubTipoDoc == "--"`, o el propio `TipoDoc` si no hay fila / alias nulo.

- [ ] **Step 1: Agregar la propiedad al DTO de la API**

En `API.Application.DTO/inventario/MovimientoInventarioDTO.cs`, después de `MovReversaDe`:
```csharp
        public int? MovReversaDe { get; set; }

        /// <summary>Nombre del tipo de documento (DocAlias de NumeracionDocumento); si no hay, el propio código.</summary>
        public string? TipoDocNombre { get; set; }
```

- [ ] **Step 2: Escribir el test (falla)**

Crear `API.Service.WebApi.Tests/Application/MovimientoInventarioApplicationTests.cs`:
```csharp
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using API.Transversal.Mapper;
using AutoMapper;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class MovimientoInventarioApplicationTests
    {
        private readonly Mock<IMovimientoInventarioDomain> _domain = new();
        private readonly Mock<INumeracionDocumentoDomain> _numeracion = new();
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>()).CreateMapper();
        private readonly MovimientoInventarioApplication _app;

        public MovimientoInventarioApplicationTests()
        {
            _app = new MovimientoInventarioApplication(_domain.Object, _mapper, _numeracion.Object);
        }

        [Fact]
        public async Task ObtenerPorArticuloAsync_PoblaTipoDocNombreConAliasOFallback()
        {
            _domain.Setup(d => d.ObtenerPorArticuloAsync("ART1", null, null, null))
                .ReturnsAsync(new[]
                {
                    new MovimientoInventario { Entry = 1, TipoDoc = "5",  CodArticulo = "ART1", CodAlmacen = "01" },
                    new MovimientoInventario { Entry = 2, TipoDoc = "12", CodArticulo = "ART1", CodAlmacen = "01" },
                    new MovimientoInventario { Entry = 3, TipoDoc = "99", CodArticulo = "ART1", CodAlmacen = "01" },
                });
            _numeracion.Setup(n => n.ObtenerTodoAsync()).ReturnsAsync(new[]
            {
                new NumeracionDocumento { CodigoObj = "5",  SubTipoDoc = "--", DocAlias = "Entrega" },
                new NumeracionDocumento { CodigoObj = "12", SubTipoDoc = "--", DocAlias = null },
                new NumeracionDocumento { CodigoObj = "5",  SubTipoDoc = "X",  DocAlias = "NO USAR" },
            }.AsAsyncQueryable());

            var r = await _app.ObtenerPorArticuloAsync("ART1", null, null, null);

            var lista = System.Linq.Enumerable.ToList(r.Dato!);
            Assert.Equal("Entrega", lista[0].TipoDocNombre); // alias de la fila '--'
            Assert.Equal("12", lista[1].TipoDocNombre);      // alias nulo -> fallback al código
            Assert.Equal("99", lista[2].TipoDocNombre);      // sin fila -> fallback al código
        }
    }
}
```
(Si `AsAsyncQueryable` no existe como helper en `TestHelpers`, usar el que ya usan otros tests para envolver `IEnumerable` como `IQueryable` async — buscar en `API.Service.WebApi.Tests/TestHelpers/`.)

- [ ] **Step 3: Correr el test → FALLA**

Run: `dotnet test API.sln --filter "FullyQualifiedName~MovimientoInventarioApplicationTests" -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
Expected: FALLA de compilación (el ctor de `MovimientoInventarioApplication` toma 2 args, no 3).

- [ ] **Step 4: Implementar en la Application**

En `API.Application.Main/MovimientoInventarioApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class MovimientoInventarioApplication : IMovimientoInventarioApplication
    {
        private readonly IMovimientoInventarioDomain _domain;
        private readonly IMapper _mapper;
        private readonly INumeracionDocumentoDomain _numeracion;

        public MovimientoInventarioApplication(IMovimientoInventarioDomain domain, IMapper mapper, INumeracionDocumentoDomain numeracion)
        {
            _domain = domain;
            _mapper = mapper;
            _numeracion = numeracion;
        }

        public async Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta)
        {
            var respuesta = new Respuesta<IEnumerable<MovimientoInventarioDTO>>();
            try
            {
                var lista = await _domain.ObtenerPorArticuloAsync(codArticulo, almacen, desde, hasta);
                var dtos = _mapper.Map<List<MovimientoInventarioDTO>>(lista);

                var alias = (await (await _numeracion.ObtenerTodoAsync())
                        .Where(n => n.SubTipoDoc == "--" && n.DocAlias != null)
                        .ToListAsync())
                    .ToDictionary(n => n.CodigoObj, n => n.DocAlias!);

                foreach (var dto in dtos)
                    dto.TipoDocNombre = alias.TryGetValue(dto.TipoDoc, out var nombre) ? nombre : dto.TipoDoc;

                respuesta.Dato = dtos;
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }
    }
}
```
(Si el mock del test devuelve un array plano en `ObtenerTodoAsync` y `.ToListAsync()` truena por no ser un `IQueryable` async real, usar `alias = ... .Where(...).ToList()` sin `ToListAsync` — el dato ya viene materializado del mock; el implementer ajusta y deja constancia. La tabla real es chica, cualquiera de las dos formas sirve.)

- [ ] **Step 5: DI — verificar**

`INumeracionDocumentoDomain` ya está registrado (`Startup.cs:165`). El contenedor resuelve por tipo; el nuevo parámetro del ctor se inyecta solo. NO hace falta cambiar `Startup.cs`. Si el build de `API.Service.WebApi` reporta un fallo de resolución, ENTONCES revisar el registro y anotarlo.

- [ ] **Step 6: Correr el test → PASA + suite completa**

Run: `dotnet test API.sln -p:BaseOutputPath="...\apitest\"`
Expected: `Con error: 0` (baseline 744 + 1 = 745).

- [ ] **Step 7: DTO Web + render en existencias.js**

En `Web.ApiClient/Dtos/MovimientoInventario/MovimientoInventarioDTO.cs`, después de `MovReversaDe`:
```csharp
        public int? MovReversaDe { get; set; }
        public string? TipoDocNombre { get; set; }
```
En `Web.UI/wwwroot/js/existencias.js`, en el `.map(m => ...)` del kardex, cambiar:
```javascript
                <td>${esc(m.tipoDoc)}</td>
```
por:
```javascript
                <td>${esc(m.tipoDocNombre ?? m.tipoDoc)}</td>
```

- [ ] **Step 8: Build Web**

Run: `cd "C:\Users\migue\source\repos\angelm0508\Web" && dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"`
Expected: `0 Errores`.

- [ ] **Step 9: Commit (2 commits — uno por repo)**

```bash
cd "C:\Users\migue\source\repos\angelm0508\API"
git add API.Application.DTO/inventario/MovimientoInventarioDTO.cs API.Application.Main/MovimientoInventarioApplication.cs API.Service.WebApi.Tests/Application/MovimientoInventarioApplicationTests.cs
git commit -m "feat(api): el kardex expone TipoDocNombre (DocAlias de NumeracionDocumento con fallback al codigo)"

cd "C:\Users\migue\source\repos\angelm0508\Web"
git add Web.ApiClient/Dtos/MovimientoInventario/MovimientoInventarioDTO.cs Web.UI/wwwroot/js/existencias.js
git commit -m "feat(web): el kardex muestra el nombre del tipo de documento en vez del codigo"
```
(trailer `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>` en ambos.)

---

## Task 2: §D — Autocompletado de "Almacén por defecto" en Artículos

**Files:**
- Modify: `Web.UI/Controllers/ArticulosController.cs`
- Modify: `Web.UI/Views/Articulos/_Form.cshtml`
- Modify: `Web.UI/Views/Articulos/Crear.cshtml`
- Modify: `Web.UI/wwwroot/js/articulos.js`

**Interfaces:**
- Consumes: `IAlmacenApiClient` con `ObtenerTodoAsync()`, `ObtenerContenganNombreAsync(string)`, `ObtenerAsync(string codigo)` (misma interfaz que usan los controllers de documento).
- Produces: rutas `/Articulos/BuscarAlmacenes?texto=` y `/Articulos/ObtenerAlmacenPorCodigo?codigo=`.

- [ ] **Step 1: Controller — inyectar IAlmacenApiClient y agregar acciones**

En `Web.UI/Controllers/ArticulosController.cs`:
1. Agregar el campo y el parámetro del ctor (junto a `_articulos`):
```csharp
        private readonly IAlmacenApiClient _almacenes;
```
En el ctor `ArticulosController(...)` agregar el parámetro `IAlmacenApiClient almacenes` y `_almacenes = almacenes;` en el cuerpo. Agregar `using Web.ApiClient.Clientes;` si falta (o el namespace real de `IAlmacenApiClient` — verificar cómo lo importa `EntregasCompraController`).
2. Agregar las acciones (copiadas de `EntregasCompraController` líneas 194-217):
```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarAlmacenes(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerAlmacenPorCodigo(string codigo)
        {
            var respuesta = await _almacenes.ObtenerAsync(codigo);
            return Json(respuesta);
        }
```

- [ ] **Step 2: `_Form.cshtml` — AlmacenDefecto a autocompletar**

En `Web.UI/Views/Articulos/_Form.cshtml` (líneas ~61-64), reemplazar:
```html
            <div class="col-md-6">
                <label asp-for="AlmacenDefecto" class="form-label"></label>
                <input asp-for="AlmacenDefecto" class="form-control" />
            </div>
```
por:
```html
            <div class="col-md-6 position-relative">
                <label asp-for="AlmacenDefecto" class="form-label"></label>
                <input type="text" id="almacenDefectoTexto" class="form-control" placeholder="Buscar almacén..." autocomplete="off" />
                <input asp-for="AlmacenDefecto" type="hidden" id="AlmacenDefecto" />
                <div class="invalid-feedback" id="almacenDefectoError">Selecciona una opción de la lista o borra el texto.</div>
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="almacenDefectoResultados"></ul>
            </div>
```

- [ ] **Step 3: `Crear.cshtml` — mismo cambio**

En `Web.UI/Views/Articulos/Crear.cshtml` (líneas ~125-128), aplicar el mismo reemplazo. **Verificar** que el `data-check="AlmacenDefecto"` de la línea ~171 siga apuntando a un elemento con `name="AlmacenDefecto"` (el hidden mantiene el `asp-for`, así que el `name` se conserva). Si `actualizarChecklistArticulo()` lee `.val()` del elemento, el hidden funciona igual.

- [ ] **Step 4: `articulos.js` — inicializar el buscador**

En `Web.UI/wwwroot/js/articulos.js`, dentro del `$(function () { ... })`:
1. Declarar arriba: `let buscadorAlmacenDefecto = null;`
2. Función de init (llamable desde el modal y desde la página):
```javascript
    function inicializarBuscadorAlmacenDefecto() {
        if ($('#almacenDefectoTexto').length === 0) return;
        buscadorAlmacenDefecto = App.autocompletar({
            texto: $('#almacenDefectoTexto'),
            oculto: $('#AlmacenDefecto'),
            lista: $('#almacenDefectoResultados'),
            error: $('#almacenDefectoError'),
            endpoint: '/Articulos/BuscarAlmacenes',
            obtenerCodigo: a => a.codigo ?? a.Codigo,
            obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
            requerido: false
        });
        // En edición, el hidden ya trae el código: resolverlo para mostrar "código - nombre".
        const codActual = $('#AlmacenDefecto').val();
        if (codActual) {
            $.get('/Articulos/ObtenerAlmacenPorCodigo', { codigo: codActual }).then(r => {
                buscadorAlmacenDefecto.establecer(r.resultado && r.dato ? r.dato : { codigo: codActual, nombre: codActual });
            });
        }
    }
```
3. Llamarla:
   - En `abrirModal(html)` (línea ~23), después de `inicializarSerieArticulo();` agregar `inicializarBuscadorAlmacenDefecto();` (verificar que `inicializarSerieArticulo` se llama ahí; si el patrón del archivo llama los `inicializar*` en otro punto tras inyectar el HTML del modal, seguir ese punto).
   - Para la página `Crear.cshtml` (que no pasa por `abrirModal`): al final del `$(function(){...})`, junto a la llamada suelta `inicializarSerieArticulo();` (línea ~107), agregar `inicializarBuscadorAlmacenDefecto();`.

- [ ] **Step 5: Build Web**

Run: `dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"` → `0 Errores`.

- [ ] **Step 6: Commit**

```bash
git add Web.UI/Controllers/ArticulosController.cs Web.UI/Views/Articulos/_Form.cshtml Web.UI/Views/Articulos/Crear.cshtml Web.UI/wwwroot/js/articulos.js
git commit -m "feat(web): el campo Almacen por defecto del articulo usa buscador con autocompletado"
```

---

## Task 3: §B + §C — canónico en Entrega de compra

**Files:**
- Modify: `Web.UI/Controllers/EntregasCompraController.cs`
- Modify: `Web.UI/Views/EntregasCompra/_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/entregascompra.js`

**Interfaces:**
- Consumes: `IArticuloApiClient.ObtenerContenganCodigoAsync(string)` (vía el Application; ya existe). `App.autocompletar` (helper de `site.js`) — `establecer(item)` re-dispara `onSeleccion`.
- Produces (para las transformaciones Task 4/5):
  - Endpoint `/{Doc}/BuscarArticulosPorCodigo?texto=` en cada controller.
  - `_Form.cshtml`: el bloque de un buscador de artículo → dos (`#detArtCodTexto`/`#detArtCodResultados`/`#detArtCodError` y `#detArtDescTexto`/`#detArtDescResultados`/`#detArtDescError`), hidden `#detCodArticulo` sin cambios.
  - `_Form.cshtml`: quitar `<input asp-for="PrctjeImpuesto">` (`% Impuesto`); agregar `<input id="TotalImp" ... disabled>` ("Total impuesto").
  - JS: funciones `aplicarArticuloSeleccionado(a, origen)` y `setArticuloEnAmbasCajas(item)` con flag `sincronizandoArticulo`; `$('#TotalImp').val(totales.totalImp)` donde se sincronizan `#TotalBruto`/`#TotalDoc`.

- [ ] **Step 1: Controller — endpoint de búsqueda por código**

En `Web.UI/Controllers/EntregasCompraController.cs`, junto a `BuscarArticulos` (línea ~186):
```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarArticulosPorCodigo(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganCodigoAsync(texto);
            return Json(respuesta);
        }
```
`BuscarArticulos` existente NO se toca (ya filtra por nombre vía `ObtenerContenganNombreAsync`).

- [ ] **Step 2: `_Form.cshtml` — encabezado: quitar `% Impuesto`, agregar `Total impuesto`**

En `Web.UI/Views/EntregasCompra/_Form.cshtml`, en el bloque de totales (líneas ~92-104):
- **Eliminar** el `<div class="col-md-3">` que contiene `<label asp-for="PrctjeImpuesto" ...>` + su `<input asp-for="PrctjeImpuesto" ...>`.
- **Agregar** (después del `<div>` de `TotalBruto`, antes o después de `TotalDoc`):
```html
            <div class="col-md-3">
                <label class="form-label">Total impuesto</label>
                <input id="TotalImp" class="form-control" value="@Model.TotalImp" disabled />
            </div>
```
`PrctjeDesc` ("% Descuento") queda igual.

- [ ] **Step 3: `_Form.cshtml` — panel de línea: un buscador de artículo → dos**

En el `#panelLineaDetalle` (líneas ~150-160), reemplazar el bloque:
```html
                    <label class="form-label">Artículo</label>
                    <input type="text" id="detCodArticuloTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodArticulo" />
                    <div class="invalid-feedback" id="detCodArticuloError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodArticuloResultados"></ul>
```
por (respetando el `<div class="col-md-...">` contenedor — dividirlo en dos columnas si hace falta):
```html
                    <label class="form-label">Código de artículo</label>
                    <input type="text" id="detArtCodTexto" class="form-control" placeholder="Buscar por código..." autocomplete="off" />
                    <div class="invalid-feedback" id="detArtCodError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detArtCodResultados"></ul>
                </div>
                <div class="col-md-6 position-relative">
                    <label class="form-label">Descripción de artículo</label>
                    <input type="text" id="detArtDescTexto" class="form-control" placeholder="Buscar por descripción..." autocomplete="off" />
                    <div class="invalid-feedback" id="detArtDescError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detArtDescResultados"></ul>
                    <input type="hidden" id="detCodArticulo" />
```
(El implementer ajusta las clases `col-md-*` del `<div>` contenedor para que las dos cajas quepan lado a lado; el hidden `#detCodArticulo` queda una sola vez. `#detDescripcion`, `#detPrecio`, etc. no se tocan.)

- [ ] **Step 4: `entregascompra.js` — `#TotalImp` en la sincronización de totales**

En `pintarDetalle()` (líneas ~334-336), después de `$('#TotalDoc').val(totales.totalDoc);` agregar:
```javascript
        $('#TotalImp').val(totales.totalImp);
```
(`calcularTotalesDesdeLineas` ya devuelve `totalImp`.)

- [ ] **Step 5: `entregascompra.js` — reemplazar `buscadorArticulo` por dos + sincronización**

1. Cambiar la declaración de la variable (buscar `let buscador...` o `var buscador...` cerca del top): `buscadorArticulo` → `buscadorArticuloCod, buscadorArticuloDesc` y agregar `let sincronizandoArticulo = false;`.
2. Reemplazar el bloque `buscadorArticulo = App.autocompletar({ ... onSeleccion: async a => { ... } });` por:
```javascript
        buscadorArticuloCod = App.autocompletar({
            texto: $('#detArtCodTexto'), oculto: $('#detCodArticulo'),
            lista: $('#detArtCodResultados'), error: $('#detArtCodError'),
            endpoint: '/EntregasCompra/BuscarArticulosPorCodigo',
            obtenerCodigo: a => a.codigo ?? a.Codigo,
            obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
            onSeleccion: a => aplicarArticuloSeleccionado(a, 'cod')
        });
        buscadorArticuloDesc = App.autocompletar({
            texto: $('#detArtDescTexto'), oculto: $('#detCodArticulo'),
            lista: $('#detArtDescResultados'), error: $('#detArtDescError'),
            endpoint: '/EntregasCompra/BuscarArticulos',
            obtenerCodigo: a => a.codigo ?? a.Codigo,
            obtenerEtiqueta: a => `${a.nombre ?? a.Nombre} (${a.codigo ?? a.Codigo})`,
            onSeleccion: a => aplicarArticuloSeleccionado(a, 'desc')
        });
```
3. Agregar las dos funciones (a nivel del mismo scope, cerca de `limpiarPanelLinea`):
```javascript
    // Sincroniza las dos cajas de artículo sin re-disparar onSeleccion (establecer() lo haría).
    function setArticuloEnAmbasCajas(item) {
        sincronizandoArticulo = true;
        try {
            buscadorArticuloCod.establecer(item || null);
            buscadorArticuloDesc.establecer(item || null);
            $('#detCodArticulo').val(item ? (item.codigo ?? item.Codigo) : '');
        } finally {
            sincronizandoArticulo = false;
        }
    }

    // Handler de selección del usuario en cualquiera de las dos cajas.
    function aplicarArticuloSeleccionado(a, origen) {
        if (sincronizandoArticulo) return; // corta la recursión de establecer()
        sincronizandoArticulo = true;
        try {
            $('#detCodArticulo').val(a ? (a.codigo ?? a.Codigo) : '');
            if (origen !== 'cod')  buscadorArticuloCod.establecer(a || null);
            if (origen !== 'desc') buscadorArticuloDesc.establecer(a || null);
        } finally {
            sincronizandoArticulo = false;
        }
        if (!a) { buscadorAlmacen.establecer(null); return; }
        $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
        $('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);
        aplicarAlmacenDefectoDeArticulo(a);
        recalcularLinea();
    }

    async function aplicarAlmacenDefectoDeArticulo(a) {
        const cod = a.almacenDefecto ?? a.AlmacenDefecto ?? '';
        if (!cod) { buscadorAlmacen.establecer(null); return; }
        const r = await $.get('/EntregasCompra/ObtenerAlmacenPorCodigo', { codigo: cod });
        buscadorAlmacen.establecer(r.resultado && r.dato ? r.dato : { codigo: cod, nombre: cod });
    }
```
4. En `limpiarPanelLinea()` (línea ~374-378): cambiar `buscadorArticulo.establecer(null);` por `setArticuloEnAmbasCajas(null);`.
5. En el handler de editar línea (línea ~433): cambiar
```javascript
        buscadorArticulo.establecer(codArticulo ? { codigo: codArticulo, nombre: linea.descripcion ?? linea.Descripcion ?? '' } : null);
```
por:
```javascript
        setArticuloEnAmbasCajas(codArticulo ? { codigo: codArticulo, nombre: linea.descripcion ?? linea.Descripcion ?? '' } : null);
```
6. Buscar cualquier otra referencia a `buscadorArticulo` en el archivo (`grep buscadorArticulo`) y migrarla: si es `.establecer(...)` para setear, usar `setArticuloEnAmbasCajas`; si lee estado, usar `buscadorArticuloCod`.

- [ ] **Step 6: Build Web**

Run: `dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"` → `0 Errores`.

- [ ] **Step 7: Auto-revisión**

- El `_Form` no tiene ya `PrctjeImpuesto`; tiene `#TotalImp` disabled y `PrctjeDesc` intacto.
- El panel de línea tiene `#detArtCodTexto` y `#detArtDescTexto`, un solo `#detCodArticulo` hidden.
- `entregascompra.js` no tiene ninguna referencia colgante a `buscadorArticulo` (singular).
- `grep -n "buscadorArticulo\b" entregascompra.js` → 0 resultados.

- [ ] **Step 8: Commit**

```bash
git add Web.UI/Controllers/EntregasCompraController.cs Web.UI/Views/EntregasCompra/_Form.cshtml Web.UI/wwwroot/js/entregascompra.js
git commit -m "feat(web): Entrega de compra - total impuesto bloqueado en el encabezado + dos buscadores de articulo (codigo/descripcion)"
```

---

## Task 4: §B + §C — transformar a Factura de compra, Entrega, Factura

**Files:** para cada una de las 3 pantallas — su `{Doc}Controller.cs`, `Views/{Doc}/_Form.cshtml`, `wwwroot/js/{doc}.js`.

**Interfaces:**
- Consumes: el estado de los archivos `EntregasCompra*` en HEAD tras Task 3 (canónico).

- [ ] **Step 1: Precondición**

Confirmar que `facturascompra.js`, `entregas.js`, `facturas.js` y sus `_Form.cshtml` tienen HOY la MISMA forma que tenían `entregascompra.js` / `EntregasCompra/_Form.cshtml` ANTES de Task 3: un solo `buscadorArticulo` sobre `/{Doc}/BuscarArticulos`, `PrctjeImpuesto` en el encabezado, `$('#TotalBruto').val(...)` + `$('#TotalDoc').val(...)` en `pintarDetalle`, `limpiarPanelLinea` con `buscadorArticulo.establecer(null)`, edición de línea con `buscadorArticulo.establecer({codigo,nombre})`. Si alguna difiere de forma que rompa la sustitución, PARAR y reportar NEEDS_CONTEXT con el diff.

- [ ] **Step 2: Aplicar los cambios de Task 3 a las 3 pantallas**

Para cada `{Doc}` ∈ { FacturasCompra / `facturascompra.js`, Entregas / `entregas.js`, Facturas / `facturas.js` }:
- **Controller:** agregar `BuscarArticulosPorCodigo` (idéntico a Task 3 Step 1, con la ruta `/{Doc}/`).
- **`_Form.cshtml`:** quitar el `<div>` de `PrctjeImpuesto`; agregar el `<div>` de `#TotalImp` (Task 3 Step 2); partir el buscador de artículo en dos cajas `#detArtCodTexto` / `#detArtDescTexto` con el hidden `#detCodArticulo` único (Task 3 Step 3).
- **`{doc}.js`:** `$('#TotalImp').val(totales.totalImp);` en `pintarDetalle` (Task 3 Step 4); reemplazar `buscadorArticulo` por `buscadorArticuloCod` + `buscadorArticuloDesc` + `sincronizandoArticulo` + `setArticuloEnAmbasCajas` + `aplicarArticuloSeleccionado` + `aplicarAlmacenDefectoDeArticulo` (Task 3 Step 5), con los endpoints `/{Doc}/BuscarArticulosPorCodigo`, `/{Doc}/BuscarArticulos`, `/{Doc}/ObtenerAlmacenPorCodigo`; migrar `limpiarPanelLinea` y la edición de línea a `setArticuloEnAmbasCajas`.

Tabla de sustitución (aplicar sobre el código de Task 3):
| `EntregasCompra` / `entregascompra` | destino |
|---|---|
| `EntregasCompra` (rutas, ids de tabla) | `FacturasCompra` / `Entregas` / `Facturas` |
| `entregascompra` (nombre js) | `facturascompra` / `entregas` / `facturas` |
| `/EntregasCompra/` (endpoints) | `/FacturasCompra/` / `/Entregas/` / `/Facturas/` |

Los ids `#detArtCodTexto`, `#detArtDescTexto`, `#detCodArticulo`, `#TotalImp` y los nombres de función (`setArticuloEnAmbasCajas`, etc.) **NO cambian** entre pantallas (son locales a cada archivo).

- [ ] **Step 3: Build Web**

Run: `dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"` → `0 Errores`.

- [ ] **Step 4: Auto-revisión**

Por cada uno de los 3 js: `grep -n "buscadorArticulo\b"` → 0. `grep -n "PrctjeImpuesto"` en el `_Form` → 0. `#TotalImp` presente en `_Form` y seteado en el js.

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/FacturasCompraController.cs Web.UI/Views/FacturasCompra/_Form.cshtml Web.UI/wwwroot/js/facturascompra.js \
        Web.UI/Controllers/EntregasController.cs Web.UI/Views/Entregas/_Form.cshtml Web.UI/wwwroot/js/entregas.js \
        Web.UI/Controllers/FacturasController.cs Web.UI/Views/Facturas/_Form.cshtml Web.UI/wwwroot/js/facturas.js
git commit -m "feat(web): total impuesto bloqueado + dos buscadores de articulo en Factura de compra, Entrega y Factura"
```

---

## Task 5: §B + §C — transformar a Cotización, Pedido, Pedido de compra

**Files:** para cada una de las 3 pantallas — su `{Doc}Controller.cs`, `Views/{Doc}/_Form.cshtml`, `wwwroot/js/{doc}.js`.

**Interfaces:**
- Consumes: el canónico de Task 3.
- Nota: `cotizaciones.js` / `pedidos.js` / `pedidoscompra.js` crean las líneas en dos pasos (`POST /{Doc}/CrearLinea`), no embebidas. Eso NO afecta §B ni §C — el bloque del buscador de artículo, `calcularTotalesDesdeLineas`, `pintarDetalle` (sincronización de totales), `limpiarPanelLinea` y la edición de línea existen igual. La transformación se aplica solo a esas zonas.

- [ ] **Step 1: Precondición**

Igual que Task 4 Step 1, para `cotizaciones.js` / `pedidos.js` / `pedidoscompra.js` y sus `_Form.cshtml`. **Además** verificar que el nombre de la función que pinta los totales (`pintarDetalle` o equivalente) y el de `calcularTotalesDesdeLineas` son los mismos; si difieren, adaptar y anotar. Si el `_Form` de alguna no tiene `PrctjeImpuesto` en el encabezado (p. ej. Cotización lo maneja distinto), PARAR y reportar NEEDS_CONTEXT.

- [ ] **Step 2: Aplicar los cambios**

Igual que Task 4 Step 2, con la tabla de sustitución para `Cotizaciones`/`cotizaciones`, `Pedidos`/`pedidos`, `PedidosCompra`/`pedidoscompra` y sus endpoints. Si `pintarDetalle` se llama distinto en alguno, poner `$('#TotalImp').val(totales.totalImp);` donde ese archivo sincroniza `#TotalBruto`/`#TotalDoc`.

- [ ] **Step 3: Build Web**

Run: `dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"` → `0 Errores`.

- [ ] **Step 4: Auto-revisión**

Por cada js: `grep -n "buscadorArticulo\b"` → 0. `grep -n "PrctjeImpuesto"` en el `_Form` → 0. `#TotalImp` presente y seteado.

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/CotizacionesController.cs Web.UI/Views/Cotizaciones/_Form.cshtml Web.UI/wwwroot/js/cotizaciones.js \
        Web.UI/Controllers/PedidosController.cs Web.UI/Views/Pedidos/_Form.cshtml Web.UI/wwwroot/js/pedidos.js \
        Web.UI/Controllers/PedidosCompraController.cs Web.UI/Views/PedidosCompra/_Form.cshtml Web.UI/wwwroot/js/pedidoscompra.js
git commit -m "feat(web): total impuesto bloqueado + dos buscadores de articulo en Cotizacion, Pedido y Pedido de compra"
```

---

## Task 6: Verificación conjunta

**Files:** ninguno nuevo.

- [ ] **Step 1: Build + suite API**

Run: `cd "C:\Users\migue\source\repos\angelm0508\API" && dotnet build API.sln -p:BaseOutputPath="...\apibuild\"` → `0 Errores`.
Run: `dotnet test API.sln -p:BaseOutputPath="...\apitest\"` → `Con error: 0` (745).

- [ ] **Step 2: Build Web**

Run: `cd "C:\Users\migue\source\repos\angelm0508\Web" && dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"` → `0 Errores`.

- [ ] **Step 3: Checklist manual (para el usuario)**

- **Kardex:** Inventario → Existencias → Ver (kardex de un artículo con movimientos). La columna de tipo de documento muestra el **alias** (ej. "Entrega de compra") y no el código; para un objeto sin `DocAlias` configurado, muestra el código.
- **Artículos:** Artículos → editar uno con `AlmacenDefecto` seteado → el campo "Almacén por defecto" muestra `código - nombre` y es un buscador; cambiarlo y guardar; volver a abrir → persiste. Crear un artículo nuevo eligiendo un almacén por defecto desde el buscador.
- **Documentos (probar al menos Entrega de compra + Cotización):**
  - Encabezado: ya no está "% Impuesto"; está "Total impuesto" bloqueado y se actualiza al agregar/quitar líneas con impuesto.
  - Detalle → Agregar línea: hay dos cajas, "Código de artículo" y "Descripción de artículo". Buscar por código en la primera → al elegir, la segunda queda con el mismo artículo, y se llenan descripción, precio y **almacén por defecto**. Buscar por descripción en la segunda → la primera se sincroniza. Borrar el texto de una → ambas quedan vacías y el hidden también.
  - Editar una línea existente → ambas cajas cargan el artículo de la línea.
  - Guardar el documento → la línea persiste con el artículo correcto.

- [ ] **Step 4: Recordatorio para el usuario (imprimir)**

- Reiniciar las sesiones de depuración de Visual Studio (API y Web.UI).
- `MovimientoInventarioApplication` cambió de ctor (nuevo parámetro `INumeracionDocumentoDomain`).
- Nada de esquema cambió; no hay `.sql` que aplicar.

- [ ] **Step 5: Commit final (si quedó algo suelto)**

```bash
cd "C:\Users\migue\source\repos\angelm0508\API" && git add docs/ && git commit -m "chore: cierre ajustes documentos y kardex" || echo "nada que commitear"
```

---

## Notas de auto-revisión (cobertura del spec)

- **§A** (kardex: nombre tipo doc, `DocAlias` con fallback, JOIN por `SubTipoDoc='--'`, DTO API + Web, `existencias.js`) → Task 1.
- **§B** (quitar `% Impuesto`, `#TotalImp` bloqueado, `PrctjeDesc` intacto, columna BD sin tocar) → Task 3 (canónico) + Tasks 4/5 (transformación).
- **§C** (dos buscadores por pantalla, endpoint `BuscarArticulosPorCodigo` usando `ObtenerContenganCodigoAsync` existente, hidden `#detCodArticulo` sin renombrar, sincronización con flag anti-recursión, autofill de descripción/precio/almacén) → Task 3 + Tasks 4/5.
- **§D** (`AlmacenDefecto` a autocompletar en `_Form` y `Crear`, `ArticulosController` + `BuscarAlmacenes`/`ObtenerAlmacenPorCodigo`, resolver en edición) → Task 2.
- **§E** (un spec, plan por transformación, subagent-driven) → este plan; Task 3 canónico, Tasks 4/5 transformación batch (3 pantallas c/u).
- **Pruebas:** §A tiene test de Application (`TipoDocNombre` alias / fallback ×2). §B/§C/§D son UI → gate de build + checklist manual (Task 6). `ObtenerContenganCodigoAsync` ya tiene su camino probado (existe desde antes).
- **Riesgo `establecer` re-dispara `onSeleccion`** → mitigado con `sincronizandoArticulo` (Global Constraints + Task 3 Step 5).
- **Fuera de alcance:** impuesto a nivel encabezado (se elimina de la UI), documentos de mercancía en §B/§C, cambios al cálculo de totales, `PrctjeImpuesto` en BD/entidad/DTO API.
