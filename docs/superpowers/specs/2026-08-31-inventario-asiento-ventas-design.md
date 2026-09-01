# Asiento de inventario en documentos de venta (INV-3) — Diseño

**Fecha:** 2026-08-31
**Fase:** INV-3 (sigue a INV-1 núcleo de inventario, INV-2 asiento en compras)
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

Enganchar `IInventarioAsientoService` (INV-1) en el registro y la cancelación de la **Entrega de
venta** (`TipoObjeto = "5"`) y la **Factura de venta** (`TipoObjeto = "6"`): al registrar
(encabezado + líneas en una sola transacción) **sale** stock; al poner `Cancelado = 'S'` el stock
**reingresa**. El stock negativo se bloquea de forma dura. Editar un documento asentado solo
permite cambiar `Comentario`; las líneas son inmutables.

Es el espejo de INV-2 (compras): misma mecánica, cantidad de signo contrario en el
`MovimientoRequest`, y `permitirNegativo: false` para que la salida rechace stock insuficiente.

## Contexto ya resuelto por fases previas (no se re-implementa)

- **`IInventarioAsientoService`** (INV-1) ya soporta salidas: `MovimientoRequest.Cantidad < 0` =
  salida; `AsentarAsync(movimientos, permitirNegativo: false)` lanza `StockInsuficienteException`
  si `Disponible - Cantidad < 0`. `RevertirAsync(tipoDoc, docEntry)` reingresa (movimientos
  inversos). Ninguno llama `SaveChangesAsync`. Firma:
  `MovimientoRequest(string TipoDoc, int DocEntry, int DocLinea, string CodArticulo, string CodAlmacen, decimal Cantidad, decimal PrecioUnitario, DateTime Fecha)`.
- **`IEjecutorTransaccion.EjecutarAsync<T>(Func<Task<T>>)`** (INV-2): abre transacción, corre el
  func, `SaveChangesAsync` + `Commit`; si lanza, `Rollback` + `ChangeTracker.Clear()` + repropaga.
  Los domains nunca llaman `SaveChangesAsync` directo.
- **`IRepositorioGenerico.AgregarSinGuardarAsync(T)`** = `DbSet.AddAsync` sin guardar.
- **Columnas ya presentes** en `Entrega` y `Factura` (idénticas a las tablas de compra, con los
  mismos defaults): `Cancelado` (`'N'`), `EstadoDoc` (`'A'`), `EstadoInv` (`'A'`),
  `FechaCancelado`, `Comentario` (máx. 254). **No se necesita script SQL.**
- **DI:** `IRepositorioGenerico<Entrega,int>`, `<EntregaDetalle,(int,int)>`,
  `<Factura,int>`, `<FacturaDetalle,(int,int)>`, `<NumeracionDocumentoDet,int>`,
  `IEjecutorTransaccion`, `IInventarioAsientoService` — **todos ya registrados**. `Startup.cs` no
  cambia.
- **Estado de los domains de venta:** `EntregaDomain` / `FacturaDomain` / `EntregaDetalleDomain` /
  `FacturaDetalleDomain` tienen hoy exactamente la forma que tenían `EntregaCompraDomain` etc.
  **antes** de INV-2 Task 2 (ctor de 3 repos, `InsertarAsync(obj)` de 1 arg, `EliminarAsync`
  borra líneas a mano, detalle domain sin repo de encabezado ni guardas). La transformación es
  1:1 con lo que INV-2 le hizo a compras.
- **Mapas AutoMapper** `CreateMap<EntregaDetalleCrearDTO, EntregaDetalle>()` y
  `CreateMap<FacturaDetalleCrearDTO, FacturaDetalle>()` ya existen. Los `*DetalleCrearDTO`
  (API) ya tienen `BaseEntry` / `BaseTipo` / `BaseLinea` / `EstadoLinea`.

## Decisiones (tomadas en brainstorming)

1. **Documentos que asientan:**
   - `EntregaDomain` (`"5"`): **siempre** genera una salida por cada línea con `Cantidad > 0`.
   - `FacturaDomain` (`"6"`): genera una salida por cada línea con `Cantidad > 0` **y
     `BaseEntry == null`**. Una línea con `BaseEntry != null` proviene (o provendrá) de una
     Entrega que ya movió ese stock. Es un guard con visión de futuro: hoy nada llena
     `BaseEntry`, así que en la práctica toda Factura descuenta; el hook queda correcto para
     cuando exista el flujo de chaining.
2. **Stock negativo:** bloqueo duro, sin override. `AsentarAsync(..., permitirNegativo: false)`.
3. **`MovimientoInventario.PrecioUnitario` en la salida:** `l.Precio ?? 0m` (precio de venta de
   la línea). `CalcularSalida` lo ignora para valuación (usa el costo promedio móvil / COGS);
   sirve solo de referencia en el kardex (margen = `PrecioUnitario` − `CostoUnitario`).
4. **Estructura:** un solo spec y plan; `FacturaDomain` = transformación de `EntregaDomain` + el
   guard `BaseEntry`; Web `Entregas`/`Facturas` = transformación de `EntregasCompra`/`FacturasCompra`
   (estado post-fix-wave de INV-2); ejecución subagent-driven.

## §1 — `EntregaDomain` (canónico)

### DTO e interfaz

- `API.Application.DTO/entrega/EntregaCrearDTO.cs` gana
  `public List<EntregaDetalleCrearDTO> Lineas { get; set; } = new();`.
- `IEntregaDomain.InsertarAsync` cambia a
  `Task<int> InsertarAsync(Entrega obj, IEnumerable<EntregaDetalle> lineas)` — se elimina la
  sobrecarga de 1 argumento.
- `EntregaApplication.InsertarAsync` mapea
  `_mapper.Map<IEnumerable<EntregaDetalle>>(obj.Lineas)` y llama al método de 2 argumentos.

### `EntregaDomain`

Ctor añade `IEjecutorTransaccion tx` e `IInventarioAsientoService asiento` (a los 3 repos
existentes: `<Entrega,int>`, `<EntregaDetalle,(int,int)>`, `<NumeracionDocumentoDet,int>`).

**`InsertarAsync(Entrega obj, IEnumerable<EntregaDetalle> lineas)`:**

1. `obj.TipoObjeto = "5"`; `obj.EstadoInv = "A"`.
2. Numeración de serie **idéntica a la actual** (serie inexistente → lanza; `Bloqueado == "S"`
   → lanza; `Manual == "S"` y `NumDoc <= 0` → lanza; si no, `SigNumero == null` → lanza,
   `FinNumero` agotado → lanza, `obj.NumDoc = serie.SigNumero`, `serie.SigNumero++` en memoria).
   Todo **antes** de abrir la transacción.
3. `var lineasList = lineas?.ToList() ?? new List<EntregaDetalle>();`
4. `return await _tx.EjecutarAsync(async () => {`
   - `await _repoEntrega.InsertarAsync(obj);` → asigna `obj.Entry` (Save #1, dentro de la tx).
   - `var noLinea = 1; foreach (var linea in lineasList) { linea.Entry = obj.Entry; linea.NoLinea = noLinea++; await _repoDetalle.AgregarSinGuardarAsync(linea); }`
   - ```
     var movimientos = lineasList
         .Where(l => (l.Cantidad ?? 0m) > 0m)
         .Select(l => new MovimientoRequest(
             TipoDoc: "5",
             DocEntry: obj.Entry,
             DocLinea: l.NoLinea,
             CodArticulo: l.CodArticulo!,
             CodAlmacen: l.CodAlmacen!,
             Cantidad: -(l.Cantidad!.Value),        // negativo = salida
             PrecioUnitario: l.Precio ?? 0m,
             Fecha: obj.FechaDoc ?? DateTime.Now))
         .ToList();
     ```
   - `await _asiento.AsentarAsync(movimientos);` — `permitirNegativo` por defecto `false`:
     si alguna línea deja `Disponible < 0`, lanza `StockInsuficienteException` y
     `EjecutarAsync` hace rollback de todo (encabezado, líneas, `SigNumero`).
   - `return obj.Entry;`
   `});`

**`ActualizarAsync(int id, Entrega obj)`:**

- `var existente = await _repoEntrega.ObtenerAsync(id); if (existente is null) return false;`
- `if (existente.Cancelado == "S") throw new Exception("El documento está cancelado y no se puede modificar.");`
- **Cancelación** — `if (obj.Cancelado == "S")`:
  `return await _tx.EjecutarAsync(async () => { await _asiento.RevertirAsync("5", id); existente.Cancelado = "S"; existente.EstadoInv = "C"; existente.FechaCancelado = DateTime.Now; if (obj.Comentario != null) existente.Comentario = obj.Comentario; return true; });`
- **Edición inocua** — resto:
  `return await _tx.EjecutarAsync(async () => { existente.Comentario = obj.Comentario; return true; });`
  (copia incondicional — replace-semantics: enviar vacío borra el comentario).

**`EliminarAsync(int id)`:**

- `var existente = await _repoEntrega.ObtenerAsync(id); if (existente is null) return false;`
- `if (existente.EstadoInv == "A" && existente.Cancelado != "S") throw new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo.");`
- `return await _tx.EjecutarAsync(async () => { ...borrar líneas del Entry a mano, luego el encabezado...; return await _repoEntrega.EliminarAsync(id); });`

**`ObtenerAsync` / `ObtenerTodoAsync`:** sin cambios.

### `EntregaDetalleDomain`

- Ctor añade `IRepositorioGenerico<Entrega, int> repoEncabezado` como **segundo** parámetro
  (el repo de detalle sigue primero).
- **`InsertarAsync(EntregaDetalle obj)`:** rechaza incondicionalmente —
  `await Task.CompletedTask; throw new Exception("Las líneas se definen al crear el documento y no se pueden agregar después.");`
  (Las líneas solo se crean dentro de `EntregaDomain.InsertarAsync` vía
  `AgregarSinGuardarAsync`. Sin FK entre `EntregaDetalle.Entry` y `Entrega.Entry`, un `Entry`
  inexistente generaría una línea huérfana.)
- **`ActualizarAsync` / `EliminarAsync`:** al inicio, `await LanzarSiElDocumentoExisteAsync(entry);`
  con `private async Task LanzarSiElDocumentoExisteAsync(int entry) { if (await _repoEncabezado.ObtenerAsync(entry) is not null) throw new Exception("Las líneas se definen al crear el documento y no se pueden modificar después."); }`
- **`Obtener*` / `ObtenerPorEntregaAsync`:** sin cambios.

## §2 — `FacturaDomain` (transformación de §1 + guard `BaseEntry`)

`FacturaCrearDTO`, `IFacturaDomain`, `FacturaDomain`, `FacturaDetalleDomain`, `FacturaApplication`
y los archivos de test = §1 con la tabla de sustitución:

| §1 (`Entrega`) | §2 (`Factura`) |
|---|---|
| `Entrega` / `EntregaDetalle` (tipos, DTO ns `entrega`) | `Factura` / `FacturaDetalle` (ns `factura`) |
| `TipoObjeto = "5"` | `TipoObjeto = "6"` |
| `IEntregaDomain` / `IEntregaDetalleDomain` | `IFacturaDomain` / `IFacturaDetalleDomain` |
| `EntregaApplication` | `FacturaApplication` |
| `ObtenerPorEntregaAsync` (detalle domain) | `ObtenerPorFacturaAsync` |
| `RevertirAsync("5", id)` | `RevertirAsync("6", id)` |
| "entregas" / "entrega" en mensajes | "facturas" / "factura" |
| `EntregaDomainTests` / `EntregaDetalleDomainTests` | `FacturaDomainTests` / `FacturaDetalleDomainTests` |

**Única diferencia de lógica** — el filtro de `movimientos` en `InsertarAsync` gana una segunda
cláusula:

```csharp
var movimientos = lineasList
    .Where(l => (l.Cantidad ?? 0m) > 0m && l.BaseEntry == null)   // BaseEntry != null -> ya lo movió su Entrega
    .Select(l => new MovimientoRequest(
        TipoDoc: "6", DocEntry: obj.Entry, DocLinea: l.NoLinea,
        CodArticulo: l.CodArticulo!, CodAlmacen: l.CodAlmacen!,
        Cantidad: -(l.Cantidad!.Value), PrecioUnitario: l.Precio ?? 0m,
        Fecha: obj.FechaDoc ?? DateTime.Now))
    .ToList();
```

## §3 — Web

### `Entregas` — transformación de `EntregasCompra` (estado post-fix-wave de INV-2)

- `Web.ApiClient/Dtos/Entrega/EntregaCrearDTO.cs` gana
  `using Web.ApiClient.Dtos.EntregaDetalle;` + `public List<EntregaDetalleCrearDTO> Lineas { get; set; } = new();`.
- `Web.UI/Controllers/EntregasController.cs`: `Editar` añade `Cancelado = dto.Cancelado,` al
  `EntregaActualizarDTO`; `FormularioEditar` añade `Cancelado = respuesta.Dato.Cancelado,` al
  `EntregaCrearDTO` de la vista. (Si el `EntregaActualizarDTO` Web no tiene `Cancelado`, se
  añade igualando al de compras.)
- `Web.UI/wwwroot/js/entregas.js`:
  - Camino de crear: **un** `POST /Entregas/Crear` con
    `datos.Lineas = lineasLocales.map(({ _id, ...linea }) => linea);` (se elimina el bucle
    `POST /CrearLinea` y el contador "X de Y"). Éxito → toast → cerrar modal → recargar tabla.
  - **Dos guards cliente** antes de armar `datos.Lineas`: `lineasLocales.length === 0` →
    error + return; alguna línea con `Cantidad > 0` sin `CodAlmacen` → error + return.
  - Handler `$(document).on('click', '#btnCancelarDocEntrega', ...)`: confirmar → botón
    `prop('disabled', true)` → `POST /Entregas/Editar?entry=${entry}` con `{ Cancelado: 'S' }`
    → error/return o cerrar modal + toast + recargar; `finally` re-habilita el botón.
  - `pintarDetalle()` en modo edición: sin botones editar/eliminar por fila.
  - Columna de estado de la lista: badge `text-bg-danger` "Cancelado" cuando
    `row.cancelado === 'S'` (verificar que el list endpoint devuelve `cancelado`; `EntregaDTO`
    lo tiene).
- `Web.UI/Views/Entregas/_Form.cshtml`:
  - "Agregar línea" envuelto en `@if (!esEdicion)`.
  - Botón "Cancelar documento" en el `modal-footer`, visible solo si
    `esEdicion && (Model.Cancelado ?? "N") != "S"`, con `data-entry="@ViewBag.EntryActual"`.
  - En modo edición, **todos los inputs de encabezado salvo el textarea `Comentario`** quedan
    `readonly="@esEdicion"` (inputs) / `disabled="@esEdicion"` (selects). El autocomplete de
    `CodigoSN` se renderiza como display de solo lectura en edición, igual que ya se hace con
    `Serie`.
  - "Guardar" oculto cuando `Model.Cancelado == "S"`.

### `Facturas` — transformación de `Entregas`

Misma sustitución: `Entrega`→`Factura`, `entregas`→`facturas`, `#btnCancelarDocEntrega`→
`#btnCancelarDocFactura`, `#btnGuardarEntrega`→`#btnGuardarFactura`, `/Entregas/`→`/Facturas/`,
"entrega de venta"→"factura de venta". **Sin lógica extra en Web** — el guard `BaseEntry` es
100% servidor.

## §4 — Semántica de cancelación, edición y errores

- **Cancelar** (`Cancelado='S'`): `RevertirAsync("5"/"6", id)` reingresa el stock (movimientos
  inversos con `+cantidad`), marca `EstadoInv='C'`, `FechaCancelado`. Reingresar nunca se
  bloquea (`RevertirAsync` fuerza `permitirNegativo: true` internamente). Cancelar dos veces →
  excepción. `EstadoDoc` no se toca.
- **Edición post-asiento:** solo `Comentario` (replace-semantics). Cambios a
  socio/fechas/moneda/totales/serie/`NumDoc`/`EstadoDoc` se ignoran en el dominio; la UI los
  deja `readonly`.
- **Eliminar:** bloqueado si `EstadoInv='A'` y no cancelado. Cancelado o sin asiento → borra
  líneas + encabezado dentro de `_tx.EjecutarAsync`.
- **Errores tipados** (INV-1): `StockInsuficienteException` (el caso nuevo importante de
  ventas), `ArticuloNoExisteException`, `AlmacenNoExisteException`. Cualquiera dentro de
  `EjecutarAsync` → rollback total: no queda documento, ni líneas, ni movimientos, ni avanza
  `SigNumero`.

## §5 — Pruebas

xUnit + Moq, sin proveedor EF (igual que INV-2). `_tx` mock corre el `Func` para
`Func<Task<int>>` y `Func<Task<bool>>`.

- **`EntregaDomainTests` / `FacturaDomainTests`:**
  - `InsertarAsync` con líneas: numera, asienta salida (capturar los `MovimientoRequest`:
    `Cantidad` negativa, `TipoDoc` correcto, `PrecioUnitario == l.Precio`), `EstadoInv == "A"`.
  - Línea con `Cantidad <= 0` → no genera movimiento.
  - `_tx.EjecutarAsync` invocado `Times.Once`; `serie.SigNumero` avanzó.
  - Stock insuficiente: `_asiento.AsentarAsync` configurado para lanzar
    `StockInsuficienteException` → `InsertarAsync` propaga y **no** se llamó
    `_repoEntrega.InsertarAsync` fuera de la tx (o el test verifica que la excepción sale).
  - Serie: inexistente / bloqueada / manual sin `NumDoc` / agotada → lanza y no inserta.
  - Cancelar: `obj.Cancelado == "S"` → `RevertirAsync("5"/"6", id)` invocado,
    `EstadoInv == "C"`, `FechaCancelado` no nulo. Con `Comentario` no nulo lo copia; sin
    `Comentario` lo preserva.
  - Recancelar (`existente.Cancelado == "S"`) → lanza.
  - Edición inocua: copia solo `Comentario`; `Comentario == null` lo borra;
    socio/moneda intactos.
  - `EliminarAsync` con `EstadoInv=='A'` y no cancelado → lanza; cancelado → borra 2 líneas
    (verificar `_repoDetalle.EliminarAsync((id,1))` y `((id,2))`) + encabezado.
- **`FacturaDomainTests` extra:**
  - Línea con `BaseEntry != null` → **no** genera movimiento (pero sí se inserta como línea).
  - Línea con `BaseEntry == null` → sí genera movimiento.
  - Mezcla: 1 línea con `BaseEntry` + 1 sin → un solo `MovimientoRequest`.
- **`EntregaDetalleDomainTests` / `FacturaDetalleDomainTests`:**
  - `InsertarAsync` → siempre lanza; `_repoGenericoDet.InsertarAsync` nunca se llama.
  - `ActualizarAsync` / `EliminarAsync` → lanzan si el encabezado existe.
- **`InventarioAsientoServiceTests`:** sin cambios (salida + `StockInsuficienteException` ya
  cubiertos en INV-1).
- **Verificación conjunta:** build API + suite completa verde (baseline 666) + build Web +
  checklist manual navegador.

## §6 — Riesgos y deuda conocida

- **`EjecutorTransaccion` real** (commit/rollback contra SQL Server) sigue sin cobertura
  automatizada — se valida en la prueba manual del navegador.
- **Re-mezcla del promedio al cancelar una venta:** `RevertirAsync` de una salida es una
  entrada (`+cantidad`) al `CostoUnitario` con que salió, y una entrada **sí** recalcula el
  promedio móvil. Si nada cambió entre medias, restaura exacto; si el promedio se movió,
  re-mezcla al costo viejo. Es el espejo (y mejora) de la limitación conocida de INV-2, donde
  la reversa de una compra no recalculaba.
- **Race de doble cancelación:** el re-check `existente.Cancelado == "S"` está fuera de la
  transacción; dos peticiones solapadas podrían ambas entrar y `RevertirAsync` dos veces
  (mitigado por el `yaRevertidos` de INV-1 salvo por nivel de aislamiento). Mitigación en
  cliente: botón deshabilitado durante la petición. Re-check server-side dentro de la
  transacción → futuro (fuera de alcance, igual que INV-2).
- **`AsentarAsync` con `permitirNegativo: false`** en ventas: si dos ventas concurrentes del
  mismo artículo+almacén compiten, `ExistenciaArticulo.RowVersion` (`IsRowVersion`) hace que
  una falle en el Save con `DbUpdateConcurrencyException` cruda. La transacción hace rollback
  bien (sin corrupción) pero el mensaje es feo. Reintento fuera de alcance; considerar
  atrapar en la capa Application y devolver "Otro usuario modificó la existencia; reintente".

## Fuera de alcance

- Flujo "facturar/entregar desde otro documento" (chaining) — solo se deja el hook por línea
  (`BaseEntry == null`); nada llena `BaseEntry` todavía.
- Cotización y Pedido de venta (no mueven stock).
- Documentos Entrada / Salida de mercancías (INV-4).
- Traslados entre almacenes; reserva (`Comprometido` / `Pedido`).
- Reintento por `DbUpdateConcurrencyException`; descancelar; reprocesar documentos de venta
  previos a INV-3.
