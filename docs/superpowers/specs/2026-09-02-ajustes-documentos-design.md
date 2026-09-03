# Ajustes en documentos y kardex — Diseño

**Fecha:** 2026-09-02
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

Cuatro ajustes de UI/consulta, independientes entre sí:

- **A.** En el kardex (consulta de existencias) mostrar el **nombre del tipo de documento** en vez
  del código numérico.
- **B.** En el encabezado de los 7 documentos comerciales, quitar el campo `% Impuesto` y en su
  lugar mostrar el **total de impuesto del documento**, bloqueado (solo lectura).
- **C.** En el detalle de esos 7 documentos, ofrecer **dos buscadores independientes** de
  artículo: uno por código y otro por descripción.
- **D.** En la pantalla de **Artículos**, el campo "Almacén por defecto" pasa de texto libre a un
  **buscador/autocompletado** de almacenes.

**Los 7 documentos comerciales:** Cotización, Pedido, Entrega, Factura, Pedido de compra,
Entrega de compra, Factura de compra. (Los documentos de mercancía — Entrada/Salida — no entran
en B ni C.)

Sin cambios de esquema. Sin migración de datos.

## Contexto verificado

- **Kardex:** `Web /Existencias/Kardex` → `IMovimientoInventarioApiClient.ObtenerPorArticuloAsync`
  → `api/MovimientoInventario` → `MovimientoInventarioApplication.ObtenerPorArticuloAsync` →
  `MovimientoInventarioDomain.ObtenerPorArticuloAsync` (filtra por artículo/almacén/fecha sobre
  el repo genérico; devuelve entidades `MovimientoInventario` sin navegación a
  `NumeracionDocumento`). `existencias.js` pinta `<td>${esc(m.tipoDoc)}</td>`.
- **`% Impuesto` (`PrctjeImpuesto`):** campo del encabezado que **ningún JS de documento ni
  ningún domain/application de la API lee**. Se captura con `App.recolectarFormulario` y se
  guarda, pero no participa en ningún cálculo. El total de impuesto real ya se calcula en el
  cliente: `calcularTotalesDesdeLineas` acumula `totalImp` = Σ del monto `Impuesto` de cada
  línea (que viene del buscador de impuesto por línea) y lo manda en `datos.TotalImp`.
  `PrctjeDesc` ("% Descuento") **sí se usa** a nivel línea y se mantiene.
- **Buscador de artículo (detalle):** hoy es **un** `App.autocompletar` por pantalla sobre
  `/{Doc}/BuscarArticulos` (que llama `ArticuloApplication.ObtenerContenganNombreAsync` o
  `ObtenerTodoAsync`), etiqueta `código - nombre`. `ArticuloDomain` **ya tiene**
  `ObtenerContengaCodigoAsync(sku)` y `ObtenerContengaNombreAsync(name)`.
- **Almacén por defecto del artículo:** hoy `<input asp-for="AlmacenDefecto" class="form-control" />`
  (texto libre) en `Web.UI/Views/Articulos/_Form.cshtml:62` y `Crear.cshtml:126`.
  `ArticuloDTO` (Web y API) ya tiene `AlmacenDefecto`.
- **`NumeracionDocumento`:** PK `(CodigoObj, SubTipoDoc)`. Columna `DocAlias nvarchar(20) NULL`.
  `INumeracionDocumentoDomain.ObtenerTodoAsync()` existe y está registrado en DI. La convención
  del proyecto usa `SubTipoDoc = '--'` para la fila "primaria" de cada objeto (ver `SerieAuto`
  en los tests, y el seed de INV-4).
- **Códigos de tipo de documento** (`TipoObjeto` / `CodigoObj`, constantes privadas en los
  domains): Cotización=`3`, Pedido=`4`, Entrega=`5`, Factura=`6`, Pedido de compra=`11`,
  Entrega de compra=`12`, Factura de compra=`13`, Entrada de mercancía=`59`, Salida de
  mercancía=`60`. En el kardex solo aparecen los que mueven inventario (`5,6,12,13,59,60`).

## §A — Nombre del tipo de documento en el kardex

**Decisión:** el nombre sale de `NumeracionDocumento.DocAlias` (LEFT JOIN por
`CodigoObj = TipoDoc AND SubTipoDoc = '--'`), con **fallback al código** si el alias es nulo o
no hay fila. Sin mapa estático ni constantes nuevas.

### API

- **`API.Application.DTO/inventario/MovimientoInventarioDTO.cs`:** agregar
  `public string? TipoDocNombre { get; set; }`.
- **`MovimientoInventarioApplication`:** inyectar `INumeracionDocumentoDomain` (ya en DI). En
  `ObtenerPorArticuloAsync`, después de mapear la lista a DTOs:

  ```csharp
  var alias = (await (await _numeracionDomain.ObtenerTodoAsync())
          .Where(n => n.SubTipoDoc == "--" && n.DocAlias != null)
          .ToListAsync())
      .ToDictionary(n => n.CodigoObj, n => n.DocAlias!);

  foreach (var dto in respuesta.Dato)
      dto.TipoDocNombre = alias.TryGetValue(dto.TipoDoc, out var nombre) ? nombre : dto.TipoDoc;
  ```

  (`NumeracionDocumento` es una tabla chica; materializarla entera es aceptable. Si el
  implementer prefiere hacerlo en el domain con un segundo repo y proyección, es válido — el
  contrato es: `MovimientoInventarioDTO.TipoDocNombre` poblado con `DocAlias ?? TipoDoc`.)
- **`MovimientoInventarioApplication` ctor + `Startup.cs`:** el nuevo parámetro
  `INumeracionDocumentoDomain` ya está registrado; agregar solo el parámetro al ctor.

### Web

- **`Web.UI/wwwroot/js/existencias.js`:** en el `render` de filas del kardex,
  `<td>${esc(m.tipoDoc)}</td>` → `<td>${esc(m.tipoDocNombre ?? m.tipoDoc)}</td>`.
- **`MovimientoInventarioDTO` (Web, `Web.ApiClient/Dtos/MovimientoInventario/`):** agregar
  `public string? TipoDocNombre { get; set; }`.

### Pruebas (API)

`MovimientoInventarioApplicationTests` (o donde vivan las de kardex): con `INumeracionDocumentoDomain`
mockeado devolviendo `[{ CodigoObj="5", SubTipoDoc="--", DocAlias="Entrega" }, { CodigoObj="12", SubTipoDoc="--", DocAlias=null }]`
y movimientos con `TipoDoc` `"5"`, `"12"` y `"99"`:
- `"5"` → `TipoDocNombre == "Entrega"`.
- `"12"` → `TipoDocNombre == "12"` (alias nulo → fallback).
- `"99"` → `TipoDocNombre == "99"` (sin fila → fallback).

## §B — Quitar `% Impuesto`, mostrar Total impuesto bloqueado

Cambio puramente visual (no toca cálculos ni la BD).

### `_Form.cshtml` (los 7)

En el bloque de totales del encabezado (donde están `PrctjeDesc`, `PrctjeImpuesto`,
`#TotalBruto`, `#TotalDoc`):

- **Eliminar** el `<div>` que contiene el `<label asp-for="PrctjeImpuesto">` + su `<input>`.
- **Agregar** junto a `#TotalBruto` / `#TotalDoc`:

  ```html
  <div class="col-md-3">
      <label class="form-label">Total impuesto</label>
      <input id="TotalImp" class="form-control" value="@Model.TotalImp" disabled />
  </div>
  ```

- **Mantener** `PrctjeDesc` ("% Descuento") como está.

### `.js` (los 7)

Donde hoy se sincronizan los totales calculados con el encabezado (típicamente
`$('#TotalBruto').val(totales.totalBruto); $('#TotalDoc').val(totales.totalDoc);` dentro de
`pintarDetalle` / tras `calcularTotalesDesdeLineas`), agregar:

```javascript
$('#TotalImp').val(totales.totalImp);
```

La columna `PrctjeImpuesto` de la BD permanece; solo deja de exponerse. El `datos.PrctjeImpuesto`
que hoy viaja en `recolectarFormulario` simplemente dejará de existir (el input ya no está) — la
API lo trata como nullable y no lo usa.

## §C — Dos buscadores de artículo en el detalle

### API — endpoint de búsqueda por código

Cada uno de los 7 controllers de documento (`CotizacionesController`, `PedidosController`,
`EntregasController`, `FacturasController`, `PedidosCompraController`, `EntregasCompraController`,
`FacturasCompraController`) tiene hoy:

```csharp
[HttpGet]
public async Task<IActionResult> BuscarArticulos(string texto)
{
    var respuesta = string.IsNullOrEmpty(texto)
        ? await _articulos.ObtenerTodoAsync()
        : await _articulos.ObtenerContenganNombreAsync(texto);
    return Json(respuesta);
}
```

- **`BuscarArticulos` pasa a ser "por nombre"** (sin cambio de firma ni de nombre — ya filtra por
  nombre).
- **Agregar `BuscarArticulosPorCodigo`:**

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

  `IArticuloApplication.ObtenerContenganCodigoAsync` debe existir (envuelve
  `ArticuloDomain.ObtenerContengaCodigoAsync`, que ya existe). Si el Application no lo expone
  todavía, agregarlo siguiendo el patrón de `ObtenerContenganNombreAsync`.

### `_Form.cshtml` (los 7) — panel de línea

El bloque actual del buscador de artículo (un `label` + `#detCodArticuloTexto` +
`#detCodArticulo` hidden + `#detCodArticuloResultados` + `#detCodArticuloError`) se **reemplaza
por dos bloques** que comparten el hidden:

```html
<div class="col-md-6 position-relative">
    <label class="form-label">Código de artículo</label>
    <input type="text" id="detArtCodTexto" class="form-control" placeholder="Buscar por código..." autocomplete="off" />
    <div class="invalid-feedback" id="detArtCodError">Selecciona una opción de la lista o borra el texto.</div>
    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index:1055; max-height:220px; overflow-y:auto;" id="detArtCodResultados"></ul>
</div>
<div class="col-md-6 position-relative">
    <label class="form-label">Descripción de artículo</label>
    <input type="text" id="detArtDescTexto" class="form-control" placeholder="Buscar por descripción..." autocomplete="off" />
    <div class="invalid-feedback" id="detArtDescError">Selecciona una opción de la lista o borra el texto.</div>
    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index:1055; max-height:220px; overflow-y:auto;" id="detArtDescResultados"></ul>
</div>
<input type="hidden" id="detCodArticulo" />
```

(Ids exactos a criterio del implementer siempre que el `.js` y el `.cshtml` coincidan; el hidden
sigue llamándose `#detCodArticulo` para no tocar el resto del flujo de guardado.)

### `.js` (los 7)

Donde hoy se inicializa `buscadorArticulo = App.autocompletar({...})`, crear **dos**:

```javascript
buscadorArticuloCod = App.autocompletar({
    texto: $('#detArtCodTexto'), oculto: $('#detCodArticulo'),
    lista: $('#detArtCodResultados'), error: $('#detArtCodError'),
    endpoint: '/{Doc}/BuscarArticulosPorCodigo',
    obtenerCodigo: a => a.codigo ?? a.Codigo,
    obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
    onSeleccion: a => aplicarArticuloSeleccionado(a, 'cod')
});

buscadorArticuloDesc = App.autocompletar({
    texto: $('#detArtDescTexto'), oculto: $('#detCodArticulo'),
    lista: $('#detArtDescResultados'), error: $('#detArtDescError'),
    endpoint: '/{Doc}/BuscarArticulos',
    obtenerCodigo: a => a.codigo ?? a.Codigo,
    obtenerEtiqueta: a => `${a.nombre ?? a.Nombre} (${a.codigo ?? a.Codigo})`,
    onSeleccion: a => aplicarArticuloSeleccionado(a, 'desc')
});
```

`aplicarArticuloSeleccionado(a, origen)`:
- Si `a` es null → limpiar ambos buscadores (`buscadorArticuloCod.establecer(null)` /
  `buscadorArticuloDesc.establecer(null)` **sin re-disparar** `onSeleccion` — el helper
  `App.autocompletar` ya expone `establecer`; ver cómo lo usa `entregascompra.js` para el
  buscador de almacén) y `#detCodArticulo` queda vacío. Return.
- Setear `#detCodArticulo` con el código.
- **Sincronizar la otra caja:** si `origen === 'cod'` → `buscadorArticuloDesc.establecer(a)`;
  si `origen === 'desc'` → `buscadorArticuloCod.establecer(a)`. (`establecer` no debe re-invocar
  `onSeleccion`; si el helper no lo garantiza, usar un flag `sincronizando` para cortar la
  recursión.)
- Hacer el autofill que ya existe hoy en el `onSeleccion` único: `#detDescripcion`, precio
  (`#detPrecio` / `#detCostoUnitario` según la pantalla) y **almacén por defecto** (el bloque
  `a.almacenDefecto → buscadorAlmacen.establecer(...)` se mueve acá tal cual).

`limpiarPanelLinea()` / el reset del panel debe limpiar **ambos** buscadores.

### Pruebas

Sin tests unitarios nuevos obligatorios (es UI). Si se agrega
`ArticuloApplication.ObtenerContenganCodigoAsync`, un test de que delega en
`ArticuloDomain.ObtenerContengaCodigoAsync` (espejo del de nombre, si existe).

## §D — Autocompletado de "Almacén por defecto" en Artículos

### `ArticulosController`

Agregar (copiando el patrón de los controllers de documento):

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
    var respuesta = await _almacenes.ObtenerAsync(codigo); // ajustar al método real del ApiClient de almacenes
    return Json(respuesta);
}
```

Inyectar `IAlmacenApiClient` (o el nombre real) en el ctor si no está. Verificar los métodos
reales del ApiClient de almacenes contra cómo los usan `EntregasCompraController.BuscarAlmacenes` /
`ObtenerAlmacenPorCodigo`.

### `Web.UI/Views/Articulos/_Form.cshtml` y `Crear.cshtml`

Reemplazar:

```html
<label asp-for="AlmacenDefecto" class="form-label"></label>
<input asp-for="AlmacenDefecto" class="form-control" />
```

por el patrón `App.autocompletar`:

```html
<label asp-for="AlmacenDefecto" class="form-label"></label>
<input type="text" id="almacenDefectoTexto" class="form-control" placeholder="Buscar almacén..." autocomplete="off" />
<input asp-for="AlmacenDefecto" type="hidden" id="AlmacenDefecto" />
<div class="invalid-feedback" id="almacenDefectoError">Selecciona una opción de la lista o borra el texto.</div>
<ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index:1055; max-height:220px; overflow-y:auto;" id="almacenDefectoResultados"></ul>
```

(En `Crear.cshtml` hay además un `data-check="AlmacenDefecto"` en la línea 171 — verificar que
el mecanismo de "campos que cambian" siga apuntando al `name`/hidden correcto.)

### `Web.UI/wwwroot/js/articulos.js`

- Inicializar `App.autocompletar` para `AlmacenDefecto` (texto/oculto/lista/error/endpoint
  `/Articulos/BuscarAlmacenes`, `obtenerCodigo: a => a.codigo ?? a.Codigo`,
  `obtenerEtiqueta: a => \`${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}\``,
  `requerido: false` — el almacén por defecto es opcional).
- Al abrir el formulario en **modo edición**, si `AlmacenDefecto` tiene valor, resolverlo con
  `/Articulos/ObtenerAlmacenPorCodigo` y `buscador.establecer(...)` para mostrar `código - nombre`.
- Al **limpiar** el buscador, `AlmacenDefecto` (hidden) queda vacío.

## §E — Estructura del plan

Un spec (este), plan por transformación, ejecución subagent-driven (igual que INV-2/3/4).
Bosquejo de tareas:

1. **Kardex (§A)** — API (`MovimientoInventarioDTO` + `MovimientoInventarioApplication` +
   ctor/DI) + tests + `existencias.js` + DTO Web.
2. **Artículos: autocompletado de Almacén por defecto (§D)** — `ArticulosController` +
   `_Form.cshtml` + `Crear.cshtml` + `articulos.js`.
3. **`EntregasCompra` canónico (§B + §C)** — controller (`BuscarArticulosPorCodigo`) +
   `_Form.cshtml` (quitar `% Impuesto`, `#TotalImp`, dos buscadores) + `entregascompra.js`
   (`#TotalImp`, `aplicarArticuloSeleccionado`, dos buscadores).
4. **`FacturasCompra`** = transformación de (3).
5. **`Entregas`** = transformación de (3).
6. **`Facturas`** = transformación de (5).
7. **`Cotizaciones`** = transformación de (3), adaptando el flujo de detalle (crear en dos pasos
   con `/CrearLinea`, no líneas embebidas — el buscador y los totales son iguales, el resto del
   `.js` difiere).
8. **`Pedidos`** = transformación de (7).
9. **`PedidosCompra`** = transformación de (7).
10. **Verificación conjunta** — `dotnet build API.sln` + suite completa (baseline 744) +
    `dotnet build Web.slnx` + checklist manual navegador.

Cada tarea de transformación verifica su precondición (que el archivo destino tiene hoy la forma
esperada) y aplica una tabla de sustitución + los deltas §B/§C.

## Riesgos y fuera de alcance

- Los 7 `.js` no son idénticos: `entregascompra`/`facturascompra` (post-fix-wave INV-2),
  `entregas`/`facturas` (post INV-3) y `cotizaciones`/`pedidos`/`pedidoscompra` (más viejos, aún
  con creación de línea en dos pasos). El buscador de artículo y el bloque de totales sí son
  paralelos; la transformación se hace sobre esas zonas, no sobre todo el archivo.
- El JOIN del kardex por `SubTipoDoc = '--'` asume una fila "primaria" por objeto en
  `NumeracionDocumento`. Si falta (p. ej. un objeto con solo sub-tipos), el kardex cae al código
  — comportamiento aceptable.
- No se toca `PrctjeImpuesto` en la BD ni en las entidades/DTO de la API — solo deja de
  renderizarse en el encabezado.
- Fuera de alcance: impuesto a nivel encabezado (se elimina como concepto de UI; el impuesto
  sigue siendo por línea), documentos de mercancía (Entrada/Salida) en §B/§C, cualquier cambio
  al cálculo de totales.
