# Spec: Buscador con autocompletado para Socio de Negocio / Artículo / Almacén / Impuesto en Cotización, Pedido, Entrega y Factura

## Contexto

Los formularios de Cotización, Pedido, Entrega y Factura (los 4 son estructuralmente idénticos, generados del mismo template) usan hoy `<select>` planos para elegir:

- El **Socio de Negocio** del encabezado (`selectCodigoSn`).
- El **Artículo**, **Almacén** e **Impuesto** de cada línea de detalle (`detCodArticulo`, `detCodAlmacen`, `detCodigoImpuesto`).

Cada `<select>` se llena cargando la lista completa de la entidad al abrir el formulario. Esto no escala si el catálogo de Artículos o Socios de Negocio crece, y obliga al usuario a buscar visualmente en una lista larga en vez de escribir y filtrar.

El proyecto no tiene hoy ninguna librería de autocompletado/typeahead (ni jQuery UI, ni Select2, ni similar) — este spec define un widget propio, mínimo, construido sobre jQuery + Bootstrap (ya presentes), reutilizado en los ~16 campos afectados (4 campos × 4 módulos).

Este cambio es continuación de dos mejoras ya implementadas y comiteadas en esta misma sesión sobre estos 4 módulos: la preselección de la serie por defecto + previsualización de No. Documento, y el renombrado de estado "Activo" → "Abierto".

## Objetivo

- Reemplazar los `<select>` de Socio de Negocio, Artículo, Almacén e Impuesto por un campo de búsqueda con autocompletado contra la API, en los 4 módulos.
- Preservar exactamente el comportamiento actual que depende de esos campos (autocompletar Nombre del socio, Descripción/Precio/Almacén por defecto del artículo, tasa de impuesto para el cálculo de la línea).
- Evitar que el usuario deje un campo con texto escrito que no corresponde a ninguna selección real.

## Fuera de alcance

- No se cambia la lógica de negocio de creación/edición de Cotización/Pedido/Entrega/Factura (numeración, estados, totales) — ya se ajustó en trabajo previo de esta sesión.
- No se agrega una librería de terceros (Select2, Choices.js, etc.) — el widget es propio y minimalista.
- No se pagina el resultado de búsqueda (se limita a un máximo de resultados, ver Diseño).
- No se aplica este patrón a ningún otro `<select>` del sistema fuera de los 4 campos mencionados en estos 4 módulos.

## Diseño

### 1. Widget reutilizable (`App.autocompletar`)

Nueva función en `Web.UI/wwwroot/js/site.js`, dentro del namespace `App` ya existente (junto a `App.enviarJson`, `App.mostrarError`, etc.):

```javascript
/**
 * Convierte un input de texto en un buscador con autocompletado contra un endpoint de la API.
 *
 * @param {jQuery} $input - el <input type="text"> visible donde el usuario escribe.
 * @param {object} opciones
 * @param {string} opciones.endpoint - URL a la que se pide `?texto=` (debe devolver Respuesta<IEnumerable<T>>).
 * @param {(item: object) => string} opciones.obtenerCodigo - extrae el código/valor real de un resultado.
 * @param {(item: object) => string} opciones.obtenerEtiqueta - arma el texto a mostrar en la lista y en el input tras elegir (ej. "Código - Nombre").
 * @param {(item: object|null) => void} opciones.onSeleccion - se llama con el objeto completo elegido, o null si el campo quedó vacío.
 * @param {number} [opciones.minCaracteres=2]
 * @param {number} [opciones.debounceMs=300]
 * @param {number} [opciones.maxResultados=10]
 */
App.autocompletar = function ($input, opciones) { ... }
```

Comportamiento interno:
- Crea (si no existe ya) un `<input type="hidden">` hermano con el mismo `name`/`id` base + sufijo `Codigo` (ej. `#selectCodigoSn` visible pasa a ser `#CodigoSnTexto` + `#CodigoSn` oculto, conservando el `name="CodigoSn"` para que `App.recolectarFormulario` lo siga capturando sin cambios).
- Crea un `<ul class="list-group position-absolute w-100 shadow-sm">` posicionado debajo del input (`position: relative` en el contenedor padre), oculto por defecto (`d-none`).
- Al escribir (evento `input`, con `debounceMs`): si el texto tiene menos de `minCaracteres`, oculta la lista y limpia el código oculto. Si tiene suficientes caracteres, llama a `opciones.endpoint` vía `$.get(endpoint, { texto })`, muestra hasta `maxResultados` en la lista usando `opciones.obtenerEtiqueta`.
- Si `opciones.minCaracteres` es `0` (caso Impuesto): además de escuchar `input`, el widget escucha `focus` sobre el input vacío y dispara la misma búsqueda con `texto=''` de inmediato (sin esperar el debounce) — así el catálogo completo aparece apenas el usuario hace clic en el campo, sin tener que escribir nada.
- Al hacer clic en un ítem de la lista, o navegar con flechas ↑/↓ y confirmar con Enter: llena el input visible con `obtenerEtiqueta(item)`, el input oculto con `obtenerCodigo(item)`, oculta la lista, marca el campo como "resuelto" (ver validación abajo), y llama a `opciones.onSeleccion(item)`.
- Si el usuario borra el input hasta dejarlo vacío: limpia también el input oculto, marca el campo como "resuelto" (vacío es un estado válido para salir), y llama a `opciones.onSeleccion(null)`.
- **Validación de salida:** en el evento `blur` del input visible, si el campo tiene texto no vacío y no está marcado como "resuelto" (el usuario escribió pero nunca eligió una sugerencia ni lo vació), se cancela el blur re-enfocando el input (`event.preventDefault()` no aplica a blur; se usa `setTimeout(() => $input.trigger('focus'), 0)` para revertir el cambio de foco) y se le agrega la clase `is-invalid` de Bootstrap junto con un `<div class="invalid-feedback d-block">Selecciona una opción de la lista o borra el texto.</div>` debajo del input. Esto se re-evalúa en cada blur hasta que el campo quede resuelto (con selección o vacío).
- **Excepción explícita:** el blur causado por click en el botón "Cancelar" del formulario, en la "X" de cierre del modal, o por `bootstrap.Modal.hide()` en general, **no se bloquea** — el widget escucha el evento `hide.bs.modal` del modal contenedor y, mientras esté en curso, no reintenta el refocus. (Los formularios de estos 4 módulos siempre viven dentro de un `.modal`, así que basta con buscar el `.modal` ancestro más cercano al inicializar el widget.)

### 2. Backend nuevo

**API — `Impuesto` gana búsqueda por nombre** (no existe hoy; SocioNegocio/Articulo/Almacen ya lo tienen):
- `IImpuestoDomain`/`ImpuestoDomain`: `Task<IEnumerable<Impuesto>> ObtenerContengaNombreAsync(string nombre)` — mismo patrón que `AlmacenDomain.ObtenerContengaNombreAsync`.
- `IImpuestoApplication`/`ImpuestoApplication`: `Task<Respuesta<IEnumerable<ImpuestoDTO>>> ObtenerContenganNombreAsync(string nombre)`.
- `ImpuestoController`: `[HttpGet("ContengaNombre/{nombre}")]`, mismo patrón que `AlmacenController`.
- Pruebas nuevas espejo de las de `AlmacenDomain`/`AlmacenController` para este método.

**Web.ApiClient — exponer la búsqueda a la Web** (la API ya la tiene para 3 de las 4 entidades, pero ningún cliente Web la usa hoy):
- `IArticuloApiClient`/`ArticuloApiClient`: `Task<Respuesta<IEnumerable<ArticuloDTO>>> ObtenerContenganNombreAsync(string nombre)`.
- `ISocioNegocioApiClient`/`SocioNegocioApiClient`: ídem con `SocioNegocioDTO`.
- `IAlmacenApiClient`/`AlmacenApiClient`: ídem con `AlmacenDTO`.
- `IImpuestoApiClient`/`ImpuestoApiClient`: ídem con `ImpuestoDTO` (consume el endpoint nuevo del punto anterior).

**Web.UI — acciones proxy por módulo** (uno de cada tipo × 4 controladores = 16 acciones nuevas, todas del mismo esqueleto de una línea, reusando los campos `_socios`/`_articulos`/`_almacenes`/`_impuestos` ya inyectados en cada controller):

```csharp
[HttpGet]
public async Task<IActionResult> BuscarSocios(string texto)
{
    var respuesta = await _socios.ObtenerContenganNombreAsync(texto ?? string.Empty);
    return Json(respuesta);
}
```
(Y análogos `BuscarArticulos`, `BuscarAlmacenes`, `BuscarImpuestos` en cada uno de los 4 controllers.)

### 3. Markup: de `<select>` a buscador

Ejemplo para Socio de Negocio en `Cotizaciones/_Form.cshtml` (idéntico patrón en los otros 3 módulos y en los 3 campos de línea):

```html
<div class="col-md-4 position-relative">
    <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
    <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
    <input type="hidden" asp-for="CodigoSn" id="CodigoSn" />
    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1050;" id="CodigoSnResultados"></ul>
</div>
```

En modo edición, el input visible se precarga (ver punto 5) en vez de quedar vacío.

### 4. Wiring por campo (efectos secundarios preservados)

**Socio de Negocio** (encabezado, 1 por módulo):
```javascript
App.autocompletar($('#CodigoSnTexto'), {
    endpoint: '/Cotizaciones/BuscarSocios',
    obtenerCodigo: s => s.codigo ?? s.Codigo,
    obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
    onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')
});
```

**Artículo** (línea de detalle):
```javascript
App.autocompletar($('#detCodArticuloTexto'), {
    endpoint: '/Cotizaciones/BuscarArticulos',
    obtenerCodigo: a => a.codigo ?? a.Codigo,
    obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
    onSeleccion: a => {
        if (!a) return;
        $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
        $('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);
        $('#detCodAlmacenTexto, #detCodAlmacen').val(a.almacenDefecto ?? a.AlmacenDefecto ?? '');
        recalcularLinea();
    }
});
```
(La asignación directa a `#detCodAlmacen` de arriba es una comodidad — si el almacén por defecto del artículo no tiene nombre cargado para mostrar en el input visible del buscador de Almacén, se resuelve con la misma consulta puntual del punto 5.)

**Almacén** (línea de detalle, sin efectos secundarios):
```javascript
App.autocompletar($('#detCodAlmacenTexto'), {
    endpoint: '/Cotizaciones/BuscarAlmacenes',
    obtenerCodigo: al => al.codigo ?? al.Codigo,
    obtenerEtiqueta: al => `${al.codigo ?? al.Codigo} - ${al.nombre ?? al.Nombre}`,
    onSeleccion: () => {}
});
```

**Impuesto** (línea de detalle, opcional — puede quedar vacío):
```javascript
let tasaImpuestoSeleccionado = 0;
App.autocompletar($('#detCodigoImpuestoTexto'), {
    endpoint: '/Cotizaciones/BuscarImpuestos',
    obtenerCodigo: i => i.codigo ?? i.Codigo,
    obtenerEtiqueta: i => `${i.nombre ?? i.Nombre} (${i.tasa ?? i.Tasa ?? 0}%)`,
    minCaracteres: 0,
    onSeleccion: i => {
        tasaImpuestoSeleccionado = i ? Number(i.tasa ?? i.Tasa ?? 0) : 0;
        recalcularLinea();
    }
});
```
`recalcularLinea()` cambia su lectura de la tasa: en vez de `$('#detCodigoImpuesto').find('option:selected').data('tasa')`, usa la variable `tasaImpuestoSeleccionado` mantenida por el `onSeleccion` de arriba. `minCaracteres: 0` para Impuesto porque es un catálogo pequeño y tiene sentido mostrar todas las opciones apenas se hace foco en el campo, incluso sin escribir (se dispara la búsqueda con texto vacío, la API ya soporta esto — `ContengaNombre` con cadena vacía coincide con todo).

### 5. Restaurar el texto visible al editar

Al abrir un documento o línea existente (donde solo se tiene el código guardado), antes de que el usuario vea el campo, se hace una consulta puntual para mostrar "Código - Nombre":

```javascript
async function precargarBuscador($textoInput, $ocultoInput, endpointObtenerPorCodigo, codigo, formatoEtiqueta) {
    if (!codigo) return;
    const respuesta = await $.get(endpointObtenerPorCodigo, { codigo });
    if (respuesta.resultado && respuesta.dato) {
        $textoInput.val(formatoEtiqueta(respuesta.dato));
        $ocultoInput.val(codigo);
    }
}
```
Se necesita una acción proxy adicional "ObtenerPorCodigo" por entidad en cada controller SOLO si no existe ya un endpoint reutilizable — `SociosNegocioController`, `ArticulosController`, `AlmacenesController` ya tienen `FormularioEditar`/lookups por código internos, pero no expuestos como JSON simple; se agrega uno delgado (`ObtenerSocioPorCodigo`, `ObtenerArticuloPorCodigo`, `ObtenerAlmacenPorCodigo` — Impuesto no lo necesita para líneas nuevas, solo para reabrir una línea guardada, mismo patrón) en cada uno de los 4 controllers de documentos, reusando los mismos clientes ya inyectados.

## Riesgos y trade-offs

- **Catálogos muy grandes:** con el enfoque de búsqueda contra la API, el tiempo de respuesta depende de que `ContengaNombre` tenga un índice razonable en `Nombre`/`Codigo` — no se audita el rendimiento de esas consultas en este spec (no es un cambio introducido aquí, ya existían para 3 de las 4 entidades).
- **Un catálogo de Impuesto muy grande** rompería la suposición de "mostrar todo con texto vacío" (`minCaracteres: 0`) — aceptable hoy porque es un catálogo pequeño por naturaleza del negocio.
- **El foco atrapado** (punto 1) es una restricción de UX fuera de lo común — se implementa de forma acotada (solo mientras el modal esté abierto y no se esté cerrando) para no dejar nunca al usuario sin forma de salir del formulario completo.

## Plan de pruebas

- API: nuevas pruebas de `ImpuestoDomain.ObtenerContengaNombreAsync` y `ImpuestoController.ObtenerContengaNombre`, mismo patrón que las ya existentes de `AlmacenDomain`/`AlmacenController`. `dotnet test` completo en verde.
- Web: sin proyecto de pruebas (confirmado); build limpio (`dotnet build Web.slnx`, 0 errores) y verificación manual en navegador, en los 4 módulos:
  1. Buscar y elegir un Socio de Negocio — confirmar que autocompleta el Nombre.
  2. Buscar y elegir un Artículo en una línea — confirmar que autocompleta Descripción, Precio y Almacén.
  3. Buscar y elegir un Almacén.
  4. Buscar y elegir un Impuesto (incluyendo con el campo vacío, para ver todas las opciones) — confirmar que el cálculo de impuesto/total de la línea sigue funcionando.
  5. Escribir texto sin elegir ninguna sugerencia e intentar pasar a otro campo — confirmar que se bloquea con el mensaje de error, y que sí se puede vaciar el campo o cerrar el modal completo en cualquier momento.
  6. Editar un documento y una línea existentes — confirmar que los 4 campos muestran "Código - Nombre" correctamente al abrir.
