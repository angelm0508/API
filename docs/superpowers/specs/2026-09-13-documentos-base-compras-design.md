# Generar documentos de compra en base a otro documento — Diseño

**Fecha:** 2026-09-13
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

En el módulo de compras, permitir generar:

- una **Entrega de Compra** en base a uno o varios **Pedidos de Compra** abiertos,
- una **Factura de Compra** en base a una o varias **Entregas de Compra** abiertas, **o** directo
  en base a uno o varios **Pedidos de Compra** abiertos (salteando la Entrega),

copiando líneas y cantidades (parciales o completas) del documento origen al destino, igual que el
mecanismo "Copy From" de SAP Business One. Si una línea de Factura de Compra viene de una Entrega,
**no** se vuelve a asentar inventario (la Entrega ya lo hizo); si viene de un Pedido directo (sin
Entrega de por medio), sí se asienta, porque nadie recibió la mercadería todavía.

Sin cambios de esquema: `PedidoCompraDetalle`, `EntregaCompraDetalle` y `FacturaCompraDetalle` ya
tienen `BaseTipo`/`BaseEntry`/`BaseLinea`, y sus `CHECK` constraints ya anticipan exactamente esta
cadena (ver más abajo). No se ejecuta ningún DDL en este plan.

Referencia de comportamiento: SAP Business One — "Copy From" al crear un GRPO/AP Invoice, con
cantidad abierta por fila y la fila de destino apuntando a la fila de origen exacta (`BaseType`/
`BaseEntry`/`BaseLine`, tablas `OPCH1`/`PDN1`/`POR1`).

### Decisiones tomadas (brainstorming)

1. **Alcance del encadenamiento:** Pedido Compra → Entrega Compra, Entrega Compra → Factura
   Compra, **y además** Pedido Compra → Factura Compra directo. Oferta de Compra (CodigoObj `10`)
   queda fuera — no existe ese módulo.
2. **Cantidad:** copia **parcial con cantidad abierta**, como SAP B1 — se puede generar más de un
   documento destino contra el mismo origen hasta agotar la cantidad; el origen sigue apareciendo
   como "abierto" con lo que falta.
3. **Punto de entrada:** al crear el documento destino, un botón "Copiar desde..." que abre un
   buscador de líneas abiertas — no un botón "Copiar a..." desde el documento origen.
4. **Multi-base:** un documento destino puede combinar líneas de **varios** documentos origen a la
   vez (ej. una Entrega que junta líneas de 2 Pedidos de Compra distintos).

## Contexto verificado

### Esquema (confirmado con `sys.check_constraints` contra `API_DB_TEST`)

| Tabla detalle | `BaseTipo` permitido | Significa |
|---|---|---|
| `PedidoCompraDetalle` | `10`, `-1` | Oferta Compra (fuera de alcance) / ninguno |
| `EntregaCompraDetalle` | `11`, `10`, `-1` | Pedido Compra / Oferta Compra (fuera) / ninguno |
| `FacturaCompraDetalle` | `12`, `11`, `10`, `-1` | Entrega Compra / Pedido Compra / Oferta (fuera) / ninguno |

`CodigoObj` (`NumeracionDocumento`): Pedido Compra = `11`, Entrega Compra = `12`, Factura Compra =
`13` (ojo: es el `CodigoObj`, no el `TipoObjeto` de la tabla misma — `TipoObjeto` de
`FacturaCompra` es `'13'`, de `EntregaCompra` es `'12'`, de `PedidoCompra` es `'11'`; coinciden
numéricamente con su propio `CodigoObj` pero el `BaseTipo` de una tabla detalle se compara contra
el `TipoObjeto`/`CodigoObj` de la tabla **origen**, que son los mismos números).

`TipoDocDestino`/`DocDestinoEntry` (puntero "hacia adelante", un solo valor) existen en las tres
tablas detalle pero **no se usan**: con multi-base y cantidad parcial, una línea origen puede
tener varios consumidores parciales a la vez, y un solo campo no puede representarlo. El lado de
ventas ya deja esta columna siempre en `-1` sin usarla — mismo criterio acá. La cantidad abierta
se calcula **siempre en vivo** (nunca se guarda un contador), así que cancelar un documento
destino libera automáticamente la cantidad de vuelta al origen sin tocar nada del origen.

`EstadoDoc`/`EstadoLinea` (`'C'`/`'A'`) existen en las tres tablas pero ningún dominio actual los
lee ni escribe — **se dejan así**. "Abierto" se deriva de la cantidad abierta calculada; un
documento sin ninguna línea con cantidad abierta > 0 simplemente no aparece en el buscador.

### Dominios existentes

- **`PedidoCompraDomain`** (`API.Domain.Core/PedidoCompraDomain.cs`): `InsertarAsync(PedidoCompra
  obj)` — **sin** parámetro `lineas`; las líneas se agregan una por una después, vía
  `PedidoCompraDetalleController`/`PedidoCompraDetalleDomain.InsertarAsync(PedidoCompraDetalle)`.
  No tiene `_tx` ni `_asiento` (un Pedido de Compra nunca mueve inventario). Esto no afecta este
  feature: Pedido de Compra es siempre **origen**, nunca destino de "copiar desde" acá.
- **`EntregaCompraDomain.InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle>
  lineas)`** y **`FacturaCompraDomain.InsertarAsync(FacturaCompra obj,
  IEnumerable<FacturaCompraDetalle> lineas)`**: ambos toman `lineas`, corren dentro de
  `_tx.EjecutarAsync` (una sola transacción: header + líneas + asiento de inventario), y llaman
  `_asiento.AsentarAsync(movimientos)` con **todas** las líneas de cantidad > 0, sin excepción hoy
  — es el bug que hay que corregir para `FacturaCompraDomain` (ver §1).
- **`EntregaCompraDomain`** postea inventario para **toda** línea con cantidad > 0, sin excepción
  — correcto y sin cambios: la Entrega de Compra es el evento de "recepción de mercadería" en
  este sistema (equivalente al GRPO de SAP B1), y un Pedido de Compra nunca mueve stock, así que
  no hay caso de doble conteo en este lado de la cadena.
- **`IRepositorioGenerico<TEntity, TKey>.ObtenerTodoAsync()`** devuelve `IQueryable<TEntity>` del
  mismo `ApiDbTestContext` — repos genéricos de entidades distintas resueltos en el mismo scope
  comparten contexto, así que un `.Join()` entre dos `IQueryable` de repos distintos SÍ se traduce
  a una sola consulta SQL si hace falta (no obstante, ver la nota de implementación en §1: para
  este volumen se recomienda materializar y calcular en memoria, más simple de escribir y probar).
- **DTOs de línea de creación** (`PedidoCompraDetalleCrearDTO`, `EntregaCompraDetalleCrearDTO`,
  `FacturaCompraDetalleCrearDTO`): **ya** exponen `BaseTipo`, `BaseEntry`, `BaseLinea` (`int?`) —
  hoy siempre llegan en `null` porque nada los llena. No requieren cambios.
- **Web:** los 7 `.js` de documento comparten el patrón `lineasLocales` (creación, arreglo en
  memoria) / `lineasRemotas` (edición, contra la API) + `pintarDetalle()` + `aplicarArticuloSeleccionado`
  + `App.autocompletar` para buscadores (ver spec `2026-09-09-lista-de-precio-en-documentos-design.md`,
  que ya documentó esta zona en detalle). La creación de Entrega de Compra y Factura de Compra
  arma `lineasLocales` y hace **un solo POST** con `Lineas` embebido — coincide exactamente con lo
  que necesita este feature (agregar líneas "copiadas" al mismo arreglo antes de guardar). La
  creación de Pedido de Compra, en cambio, es header-only + N POSTs de línea — **no se toca**,
  Pedido de Compra no es destino acá.

## §1 — API: reglas de negocio y validación

### Filtro de inventario en `FacturaCompraDomain.InsertarAsync`

Cambia:

```csharp
var movimientos = lineasList
    .Where(l => (l.Cantidad ?? 0m) > 0m)
    ...
```

a:

```csharp
var movimientos = lineasList
    .Where(l => (l.Cantidad ?? 0m) > 0m && l.BaseTipo != 12)   // BaseTipo=12 (Entrega Compra) -> ya recibido, no se repostea
    ...
```

`EntregaCompraDomain` no cambia (siempre postea, ver Contexto). La reversión al cancelar
(`_asiento.RevertirAsync`) tampoco cambia: opera sobre las filas que existan realmente en
`MovimientoInventario` para ese `TipoDoc`+`DocEntry`; como las líneas basadas en Entrega nunca
llegan a insertarse ahí, no hay nada que revertir para ellas — comportamiento correcto sin tocar
código de reversión.

### Validación de líneas con base (nueva, en `EntregaCompraDomain.InsertarAsync` y
`FacturaCompraDomain.InsertarAsync`, antes de `_tx.EjecutarAsync`)

Para cada línea con `BaseTipo` no nulo y distinto de `-1`:

1. `BaseEntry` y `BaseLinea` deben venir informados (no nulos) → si no,
   `"La línea {NoLinea} indica BaseTipo sin BaseEntry/BaseLinea."`
2. `BaseTipo` debe ser uno de los permitidos para el documento destino (Entrega Compra: solo
   `11`; Factura Compra: `12` o `11`) → si no, `"BaseTipo {valor} no es válido para {documento}."`
   (defensa temprana; el `CHECK` de la tabla igual lo bloquearía con un error genérico).
3. La línea origen debe existir (`PedidoCompraDetalle`/`EntregaCompraDetalle` según `BaseTipo`,
   por `(BaseEntry, BaseLinea)`) → si no, `"La línea base {BaseEntry}/{BaseLinea} no existe."`.
4. El encabezado origen (`PedidoCompra`/`EntregaCompra` según corresponda, `Entry = BaseEntry`)
   debe pertenecer al **mismo `CodigoSn`** que el documento destino (`obj.CodigoSn`) → si no,
   `"El documento base {BaseEntry} pertenece a otro proveedor."`
5. El encabezado origen no debe estar cancelado (`Cancelado == "S"`) → si no,
   `"El documento base {BaseEntry} está cancelado."`
6. `linea.Cantidad ?? 0m` ≤ cantidad abierta de esa línea origen, **recalculada en este mismo
   instante** dentro de la misma unidad de trabajo (no se confía en lo que mandó el navegador) →
   si no, `"La cantidad de la línea {NoLinea} excede lo pendiente de la línea base ({abierta}
   disponible)."`

La cantidad abierta se recalcula con la misma función que alimenta `ObtenerAbiertosAsync` (§
siguiente) — una sola fuente de verdad para "cuánto queda", usada tanto para mostrar el buscador
como para validar el guardado. Como corre dentro de la transacción de EF (mismo `ApiDbTextContext`,
aislamiento por defecto de SQL Server), dos guardados concurrentes contra el mismo origen no
pueden sobregirarlo: el segundo en llegar ve ya reflejada la cantidad consumida por el primero una
vez que este confirma, o ambos serializan sobre el mismo rango de filas según el nivel de
aislamiento — es una validación de "mejor esfuerzo" razonable para el volumen de esta aplicación,
no un control de concurrencia pesimista con locks explícitos (fuera de alcance).

**Importante:** si el propio documento que se está guardando trae más de una línea apuntando a la
misma `(BaseTipo, BaseEntry, BaseLinea)`, la validación debe comparar la **suma** de esas líneas
contra la cantidad abierta — no cada línea por separado. Agrupar `lineasList` por
`(BaseTipo, BaseEntry, BaseLinea)` antes de comparar contra `ObtenerAbiertosAsync`, para no dejar
pasar un documento cuyas líneas individualmente parecen válidas pero que en conjunto sobregiran el
origen.

Estas dos validaciones (líneas de Entrega con base=Pedido, líneas de Factura con base=Pedido o
Entrega) comparten la misma forma; se recomienda un método privado reutilizable por firma
genérica (ej. recibiendo el tipo de base esperado y un delegado para resolver "la cantidad ya
consumida de esa línea origen") en vez de duplicar la lógica en los dos dominios.

### Cálculo de cantidad abierta y endpoints de lectura

**`IPedidoCompraDomain` — nuevo método:**

```csharp
Task<IEnumerable<DocumentoAbierto>> ObtenerAbiertosAsync(string codigoSn);
```

Nuevas dependencias en el constructor de `PedidoCompraDomain`: `IRepositorioGenerico<EntregaCompra,
int>`, `IRepositorioGenerico<EntregaCompraDetalle, (int, int)>`, `IRepositorioGenerico<FacturaCompra,
int>`, `IRepositorioGenerico<FacturaCompraDetalle, (int, int)>` (todas ya registradas en DI para
sus propios dominios; se agregan también acá).

Algoritmo (recomendado: materializar y combinar en memoria — el volumen por proveedor es bajo y
es mucho más simple de escribir/leer/probar que forzar un solo `GroupJoin` en SQL):

1. `pedidos = pedidosCompra.Where(p => p.CodigoSn == codigoSn && p.Cancelado != "S").ToListAsync()`
   + sus `PedidoCompraDetalle` (`detalles.Where(d => pedidos.Select(p => p.Entry).Contains(d.Entry))`).
2. `consumidoPorEntrega`: de `EntregaCompraDetalle` con `BaseTipo == 11`, unido a su
   `EntregaCompra` (encabezado) con `Cancelado != "S"`, agrupado por `(BaseEntry, BaseLinea)`,
   sumando `Cantidad ?? 0m`.
3. `consumidoPorFacturaDirecta`: igual pero de `FacturaCompraDetalle` con `BaseTipo == 11`, unido
   a su `FacturaCompra` con `Cancelado != "S"`, agrupado por `(BaseEntry, BaseLinea)`.
4. Por cada línea de Pedido: `cantidadAbierta = Cantidad - consumidoPorEntrega.GetValueOrDefault((Entry,NoLinea)) - consumidoPorFacturaDirecta.GetValueOrDefault((Entry,NoLinea))`.
   Un solo pool compartido entre las dos vías de consumo (igual que SAP B1: lo ya entregado deja
   de estar disponible para facturar directo desde el pedido).
5. Devolver solo los `PedidoCompra` que tengan ≥ 1 línea con `cantidadAbierta > 0`, y de esos,
   solo esas líneas.

**`IEntregaCompraDomain` — nuevo método:**

```csharp
Task<IEnumerable<DocumentoAbierto>> ObtenerAbiertosAsync(string codigoSn);
```

Mismo patrón pero más simple (un solo pool, sin necesidad de "Oferta Compra"): `EntregaCompra` no
cancelada de ese `codigoSn` + sus líneas; `consumidoPorFactura` de `FacturaCompraDetalle` con
`BaseTipo == 12` unido a `FacturaCompra` no cancelada, agrupado por `(BaseEntry, BaseLinea)`;
`cantidadAbierta = Cantidad - consumidoPorFactura.GetValueOrDefault(...)`.

**Tipo de dominio compartido** (`API.Domain.Core` o `API.Domain.Interface`, a criterio del
implementer — sin dependencias de EF):

```csharp
public record LineaAbierta(
    int NoLinea, string CodArticulo, string? Descripcion, decimal Cantidad,
    decimal CantidadAbierta, decimal? Precio, string? CodAlmacen, string? CodigoImpuesto);

public record DocumentoAbierto(
    int Entry, int NumDoc, int Serie, DateTime? FechaDoc, IReadOnlyList<LineaAbierta> Lineas);
```

**DTOs de aplicación** (`API.Application.DTO/documentoAbierto/`, uno solo, reusado por los dos
endpoints):

```csharp
public class LineaAbiertaDTO
{
    public int NoLinea { get; set; }
    public string CodArticulo { get; set; } = null!;
    public string? Descripcion { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CantidadAbierta { get; set; }
    public decimal? Precio { get; set; }
    public string? CodAlmacen { get; set; }
    public string? CodigoImpuesto { get; set; }
}

public class DocumentoAbiertoDTO
{
    public int Entry { get; set; }
    public int NumDoc { get; set; }
    public int Serie { get; set; }
    public DateTime? FechaDoc { get; set; }
    public List<LineaAbiertaDTO> Lineas { get; set; } = new();
}
```

**Métodos de aplicación:**

```csharp
// IPedidoCompraApplication
Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn);
// IEntregaCompraApplication
Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn);
```

Mapeo manual o AutoMapper (`DocumentoAbierto` → `DocumentoAbiertoDTO`), mismo patrón `try/catch` →
`Respuesta` del resto de la aplicación.

**Endpoints nuevos:**

```csharp
// PedidoCompraController
[HttpGet("Abiertos")]
public async Task<ActionResult<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>> Abiertos([FromQuery] string codigoSn)
{
    if (string.IsNullOrWhiteSpace(codigoSn)) return BadRequest(new Respuesta<IEnumerable<DocumentoAbiertoDTO>> { Mensaje = "codigoSn es requerido." });
    var res = await _pedidoCompraApplication.ObtenerAbiertosAsync(codigoSn);
    return res.Resultado ? Ok(res) : BadRequest(res);
}

// EntregaCompraController — misma forma, delega a _entregaCompraApplication.ObtenerAbiertosAsync
```

Rutas resultantes: `GET api/PedidoCompra/Abiertos?codigoSn=...`, `GET
api/EntregaCompra/Abiertos?codigoSn=...`.

### DI (`Startup.cs`)

Sin registros nuevos de servicios (los mismos `IPedidoCompraDomain`/`IEntregaCompraDomain`/
`IFacturaCompraDomain`/`IPedidoCompraApplication`/`IEntregaCompraApplication` ya están
registrados) — solo cambian sus constructores para recibir los repos genéricos adicionales, que
también ya están registrados individualmente (patrón `AddTransient<IRepositorioGenerico<T,K>,
XxxRepositorio>` ya usado para cada entidad).

## §2 — API: pruebas

### `PedidoCompraDomain.ObtenerAbiertosAsync` (unit, mocks)

- Pedido con 1 línea `Cantidad=10`, sin consumo → `CantidadAbierta == 10`.
- Pedido con 1 línea `Cantidad=10`, una Entrega activa con `BaseTipo=11, BaseEntry=<pedido>,
  BaseLinea=1, Cantidad=4` → `CantidadAbierta == 6`.
- Igual, pero la Entrega tiene `Cancelado='S'` → `CantidadAbierta == 10` (se ignora lo cancelado).
- Pedido con 1 línea `Cantidad=10`, una Factura directa (`BaseTipo=11`) de `Cantidad=3` y una
  Entrega (`BaseTipo=11`) de `Cantidad=4` sobre la misma línea → `CantidadAbierta == 3` (pool
  compartido entre las dos vías).
- Línea con `CantidadAbierta == 0` → el pedido no aparece en el resultado si es su única línea;
  si tiene otra línea con cantidad abierta > 0, aparece pero solo con esa línea.
- Pedido de otro `codigoSn` → no aparece.
- Pedido cancelado → no aparece aunque tenga cantidad abierta.

### `EntregaCompraDomain.ObtenerAbiertosAsync` (unit, mocks)

- Análogo: Entrega con línea `Cantidad=10`, Factura activa `BaseTipo=12` de `Cantidad=6` →
  `CantidadAbierta == 4`. Factura cancelada → se ignora. Entrega de otro socio/cancelada → no
  aparece.

### `FacturaCompraDomain.InsertarAsync` — asiento de inventario (unit, mocks, extender
`FacturaCompraDomainTests` existente)

- Línea sin base (`BaseTipo == null`) → se postea en `_asiento` (comportamiento actual, no
  regresiona).
- Línea con `BaseTipo == 11` (Pedido directo) → se postea igual (nadie recibió la mercadería).
- Línea con `BaseTipo == 12` (Entrega) → **no** se postea (`_asiento.AsentarAsync` no recibe ese
  movimiento).
- Documento con una línea de cada tipo → `_asiento` recibe exactamente las dos que no son
  `BaseTipo == 12`.

### Validación de líneas con base (unit, mocks, `EntregaCompraDomainTests` y
`FacturaCompraDomainTests`)

- `BaseTipo` set, `BaseEntry`/`BaseLinea` nulos → excepción con el mensaje esperado.
- `BaseTipo` fuera de los permitidos para ese documento destino → excepción.
- Línea base inexistente → excepción.
- Línea base de otro `CodigoSn` → excepción.
- Línea base de un documento cancelado → excepción.
- `Cantidad` > cantidad abierta recalculada → excepción, mensaje incluye la cantidad disponible.
- `Cantidad` == cantidad abierta exacta → inserta sin error (caso límite).
- Dos líneas del documento destino apuntando a la **misma** línea origen, cuya suma excede lo
  abierto → la validación debe sumar lo que el propio documento en curso ya está pidiendo de esa
  línea antes de comparar (no solo lo ya consumido por documentos previos), para no permitir que
  dos líneas del mismo POST sobregiren juntas.
- Línea sin `BaseTipo` (o `BaseTipo == -1`) → no dispara ninguna validación de base, se inserta
  como hoy.

### Controllers (`PedidoCompraControllerTests`, `EntregaCompraControllerTests`)

- `GET api/PedidoCompra/Abiertos?codigoSn=PV0001` con Application mockeado → `200 Ok` con la lista.
- `GET api/PedidoCompra/Abiertos` sin `codigoSn` → `400`.
- Igual para `EntregaCompra`.

Verificar baseline de la suite (773 al momento de escribir este spec) antes y después.

## §3 — Web

### `Web.ApiClient`

- **`Dtos/DocumentoAbierto/DocumentoAbiertoDTO.cs`** y **`LineaAbiertaDTO.cs`** — espejo de los
  DTO de la API.
- **`IPedidoCompraApiClient` / `PedidoCompraApiClient`:**
  `Task<RespuestaDTO<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn)` →
  `GET api/PedidoCompra/Abiertos?codigoSn={codigoSn}`.
- **`IEntregaCompraApiClient` / `EntregaCompraApiClient`:** mismo método,
  `GET api/EntregaCompra/Abiertos?codigoSn={codigoSn}`.

### `Web.UI` — proxy MVC

En `PedidosCompraController` y `EntregasCompraController` (los controladores MVC existentes de
esos documentos, mismo patrón que sus acciones `BuscarSocios`/`BuscarArticulos`):

```csharp
[HttpGet]
public async Task<IActionResult> Abiertos(string codigoSn)
    => Json(await _pedidoCompraApiClient.ObtenerAbiertosAsync(codigoSn));
```

Rutas resultantes: `GET /PedidosCompra/Abiertos?codigoSn=...` (consumida por el picker de
`entregascompra.js` y por el de `facturascompra.js` cuando el origen es Pedido), `GET
/EntregasCompra/Abiertos?codigoSn=...` (consumida por el picker de `facturascompra.js` cuando el
origen es Entrega).

### `entregascompra.js` y `facturascompra.js` — nuevo picker "Copiar desde"

Solo estos dos `.js` cambian (Pedido de Compra no es destino en este feature). Ambos ya usan
`lineasLocales` + un solo POST con `Lineas` embebido al crear (confirmado en Contexto) — el
picker simplemente agrega entradas a ese mismo arreglo.

1. **Botón(es) "Copiar desde..."** en el modal de creación, junto al botón "Nueva línea" existente:
   - `entregascompra.js`: un botón, "Copiar desde Pedido de Compra".
   - `facturascompra.js`: dos botones, "Copiar desde Pedido de Compra" y "Copiar desde Entrega de
     Compra".
   Deshabilitados hasta que haya un `CodigoSn` seleccionado en el encabezado (atributo `disabled`
   controlado en el mismo `onSeleccion` del buscador de socio que ya existe).

2. **Modal del picker** (nuevo, un solo diseño reusado por los 3 botones, parametrizado por
   endpoint): tabla plana con columnas Documento (ej. "Pedido #123"), Artículo, Descripción,
   Cantidad abierta, un `<input type="number">` de cantidad a copiar (precargado con el máximo,
   `max` = cantidad abierta, no editable por encima), y un checkbox. Se llena con `$.get('/PedidosCompra/Abiertos',
   { codigoSn })` (o `/EntregasCompra/Abiertos`) al abrir. Botón "Agregar seleccionadas" al pie.

3. **Al confirmar**, por cada fila tildada con cantidad > 0:
   ```javascript
   lineasLocales.push({
       _id: proximoIdLocal++,
       CodArticulo: fila.codArticulo, Descripcion: fila.descripcion,
       Cantidad: cantidadElegida, Precio: fila.precio ?? 0,
       CodAlmacen: fila.codAlmacen, CodigoImpuesto: fila.codigoImpuesto,
       BaseTipo: origenEsEntrega ? 12 : 11, BaseEntry: fila.docEntry, BaseLinea: fila.noLinea,
       // resto de campos (PrctjeDesc, Impuesto, TotalLinea) calculados igual que una línea manual
   });
   ```
   Reusar el mismo cálculo de impuesto/total que ya usa `recalcularLinea()`/`aplicarArticuloSeleccionado`
   para no duplicar la fórmula — llamar a la función existente con estos valores en vez de rearmar
   el objeto de línea a mano donde sea posible. Cerrar el modal del picker y `pintarDetalle()`.

4. **Marca visual en el grid:** en `pintarDetalle()`, si la línea tiene `BaseEntry`, mostrar un
   badge pequeño junto al artículo (ej. "Pedido #123" / "Entrega #45") — texto plano, sin
   necesidad de volver a llamar a la API (el número de documento origen viaja ya en el objeto de
   línea local, agregado en el paso 3 junto con `BaseEntry`/`BaseTipo`).

5. **Guardado:** sin cambios en la función de guardar — `Lineas = lineasLocales.map(...)` ya viaja
   con `BaseTipo`/`BaseEntry`/`BaseLinea` incluidos porque están en el mismo objeto de línea. El
   servidor valida (§1) y responde con el mensaje de error correspondiente si algo cambió entre
   que se abrió el picker y que se guardó (otro usuario consumió la misma línea, por ejemplo) —
   mostrar ese mensaje con `App.mostrarError` como cualquier otro error de guardado.

No se toca la edición de un documento ya guardado (alcance: solo creación, ver Riesgos).

### Web — pruebas

No hay pruebas automatizadas de JS/MVC en el repo Web. Checklist manual:

- Pedido de Compra a un proveedor con 1 línea, `Cantidad = 10`. Crear una Entrega de Compra nueva
  para ese proveedor, "Copiar desde Pedido de Compra" → aparece la línea con abierta = 10; copiar
  4 → la Entrega se guarda con esa línea (`BaseTipo=11`); volver a abrir el picker en una Entrega
  nueva → la misma línea del Pedido ahora muestra abierta = 6.
- Con la Entrega de 4 unidades ya creada: crear una Factura de Compra, "Copiar desde Entrega de
  Compra" → aparece esa línea con abierta = 4; copiarla completa → la Factura se guarda sin volver
  a mover inventario (verificar `Articulo.CantDisponible` antes/después de guardar la Factura: no
  cambia; sí cambió cuando se guardó la Entrega).
- Sobre el mismo Pedido (6 unidades restantes): crear una Factura de Compra, "Copiar desde Pedido
  de Compra" directo (sin Entrega) → copiar las 6 → la Factura se guarda y **sí** mueve
  inventario (`Articulo.CantDisponible` sube en 6).
- Repetir el picker sobre ese Pedido → ya no aparece (cantidad abierta = 0 en su única línea).
- Multi-base: dos Pedidos de Compra abiertos al mismo proveedor → abrir el picker al crear una
  Entrega → aparecen líneas de ambos documentos en la misma tabla → tildar una de cada uno →
  ambas se agregan al grid con su `BaseEntry` distinto → guardar → la Entrega queda con dos líneas,
  cada una vinculada a su Pedido de origen.
- Intentar copiar más cantidad de la que el input permite (`max`) → el navegador lo bloquea; si se
  fuerza vía API con una cantidad mayor a la abierta real, el servidor rechaza con el mensaje de
  "excede lo pendiente".
- Cancelar la Entrega de 4 unidades generada arriba → volver a abrir el picker de Entrega sobre el
  Pedido original → la línea vuelve a mostrar abierta = 4 (se liberó automáticamente).

## §4 — Estructura del plan

Un spec (este) + un plan de implementación
(`docs/superpowers/plans/2026-09-13-documentos-base-compras-implementation.md`), ejecución
subagent-driven. Bosquejo de tareas:

1. **API — cálculo de cantidad abierta:** tipos de dominio `LineaAbierta`/`DocumentoAbierto` +
   `PedidoCompraDomain.ObtenerAbiertosAsync` (con sus nuevas dependencias) + tests.
2. **API — cálculo de cantidad abierta (Entrega):** `EntregaCompraDomain.ObtenerAbiertosAsync` +
   tests.
3. **API — DTOs y aplicación:** `DocumentoAbiertoDTO`/`LineaAbiertaDTO` +
   `IPedidoCompraApplication.ObtenerAbiertosAsync` + `IEntregaCompraApplication.ObtenerAbiertosAsync`
   + tests.
4. **API — endpoints:** `PedidoCompraController.Abiertos` + `EntregaCompraController.Abiertos` +
   tests de controller.
5. **API — validación de líneas con base + filtro de inventario en `EntregaCompraDomain.InsertarAsync`**
   (Pedido como única base válida) + tests.
6. **API — validación de líneas con base + filtro de inventario en `FacturaCompraDomain.InsertarAsync`**
   (Pedido o Entrega como base válida; skip de `_asiento` cuando `BaseTipo == 12`) + tests.
7. **Web — plomería compartida:** DTOs espejo + `IPedidoCompraApiClient.ObtenerAbiertosAsync` +
   `IEntregaCompraApiClient.ObtenerAbiertosAsync` + acciones `Abiertos` en `PedidosCompraController`
   y `EntregasCompraController` (Web.UI).
8. **Web — picker en `entregascompra.js`:** botón, modal, `App`-helper o función local para
   traer/pintar abiertos, agregar a `lineasLocales`, badge en `pintarDetalle`.
9. **Web — picker en `facturascompra.js`:** mismo picker, parametrizado para los dos orígenes
   (Pedido y Entrega).
10. **Verificación conjunta:** `dotnet build API.sln` + suite completa (baseline + N de 1-6) +
    `dotnet build Web.slnx` + checklist manual del §3.

## Riesgos y fuera de alcance

- **Solo creación**, no edición de un documento ya guardado — agregar líneas "copiadas" a un
  documento existente queda fuera (la edición de Entrega/Factura de Compra opera línea por línea
  contra la API, no en memoria; adaptarla duplicaría buena parte del picker para un caso de uso
  poco frecuente).
- **Oferta de Compra** (CodigoObj `10`) fuera de alcance — no existe ese módulo hoy.
- **Concurrencia:** la validación de cantidad abierta es de mejor esfuerzo (recalculada en el
  momento del guardado dentro de la transacción), no un lock pesimista. Dos usuarios generando
  documentos casi simultáneos contra el mismo origen pueden, en el peor caso, chocar con un error
  de "excede lo pendiente" en el segundo POST — aceptable para el volumen de esta aplicación.
- **`EstadoDoc`/`EstadoLinea`** no se escriben — "abierto/cerrado" es siempre derivado, nunca
  persistido.
- **`TipoDocDestino`/`DocDestinoEntry`** no se usan — quedan en `-1`/`null` como hasta ahora.
- Fuera de alcance: deshacer/editar la vinculación de una línea ya guardada con su base; fusionar
  líneas iguales del picker con líneas manuales ya cargadas (cada línea copiada se agrega como fila
  nueva, aunque el artículo ya esté en el grid); notificación o bloqueo si el documento origen se
  cancela después de que su cantidad ya fue parcialmente copiada (la cantidad copiada ya guardada
  queda firme; solo lo que seguía abierto deja de estar disponible).
