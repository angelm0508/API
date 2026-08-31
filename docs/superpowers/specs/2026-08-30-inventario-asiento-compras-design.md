# Diseño: Asiento de inventario en documentos de compra (INV-2)

## Contexto

INV-1 (mergeado) entregó el núcleo de inventario: tablas `ExistenciaArticulo` /
`MovimientoInventario`, `Articulo` con `MetodoValuacion` / `CostoPromedio` /
`CostoEstandar` / `ValorInventario`, el motor de valuación `IValuacionInventario` (promedio
móvil + estándar), el servicio de asiento `IInventarioAsientoService`
(`AsentarAsync` / `RevertirAsync`, que mutan el `ChangeTracker` y **no** llaman
`SaveChangesAsync`), y una API + pantalla Web de consulta. **INV-1 no engancha el asiento a
ningún documento.**

INV-2 engancha el asiento en el registro de los documentos de compra `EntregaCompra` y
`FacturaCompra`: al registrarse, suman stock; al cancelarse, lo revierten.

### Obstáculo de diseño: el flujo actual guarda en transacciones separadas

Hoy la Web registra un documento de compra así (patrón clonado de ventas):

1. `POST /EntregasCompra/Crear` → `EntregaCompraDomain.InsertarAsync(EntregaCompra obj)`
   inserta **solo el encabezado** (`obj` no trae líneas). Un `SaveChangesAsync`.
2. El JS recorre `lineasLocales` y hace un `POST /EntregasCompra/CrearLinea` por cada
   línea → `EntregaCompraDetalleDomain.InsertarAsync` → un `SaveChangesAsync` **por
   línea**.

No existe ningún momento en que "el documento completo con sus líneas" viva en una sola
transacción. El servicio de asiento de INV-1 está diseñado justo para lo contrario: el
caller hace **un** `SaveChangesAsync` con encabezado + líneas + mutaciones de inventario.
Además, el flujo actual tiene un bug latente: si la línea 3 de 5 falla al insertar, queda
un documento a medio guardar sin rollback.

Estructura relevante verificada: `EntregaCompra` / `FacturaCompra` **no** tienen colección
de navegación al detalle, y **no hay FK en la base de datos** entre
`{Doc}Detalle.Entry` y `{Doc}.Entry` (los `EliminarAsync` de dominio lo dicen
explícitamente: borran las líneas a mano). Por eso EF no puede hacer fix-up del `Entry` de
las líneas: hay que obtener el `Entry` del encabezado con un `SaveChangesAsync` antes de
armar las líneas y los `MovimientoRequest`.

### Hallazgos de SAP B1 (SBO_TEST, Service Layer)

Probado sobre un Goods Receipt PO ya asentado (`PurchaseDeliveryNotes(1)`):

- `PATCH { "Comments": "..." }` → **aceptado** (204). El comentario/observaciones del
  encabezado sí es editable tras asentar.
- `PATCH { "DocumentLines": [{ "LineNum": 0, "Quantity": 999 }] }` → **rechazado**
  (error -5002). Artículo / cantidad / almacén / precio de las líneas son inmutables tras
  asentar.
- Cancelar en SAP es una acción discreta (`.../Cancel`) que genera un documento de
  reversión y está limitada por una ventana de días configurable. Nuestro sistema es más
  simple y no tiene documento de reversión ni contabilidad: la cancelación se modela con
  el flag `Cancelado='S'` que dispara `RevertirAsync`.

### Semántica de los flags en las tablas de compra (CHECK constraints reales)

| Flag | Valores | Significado |
|---|---|---|
| `EstadoDoc` | `'A'` / `'C'` | Abierto / Cerrado (drawn-down), como SAP `DocStatus`. INV-2 no lo toca. |
| `Cancelado` | `'S'` / `'N'` | Documento anulado. INV-2: `'S'` dispara el reversó de inventario. |
| `EstadoInv` | `'A'` / `'C'` | Estado del asiento de inventario. `'A'` = asentado; `'C'` = revertido. |

## Decisiones confirmadas con el usuario

1. **Modelo transaccional: Opción B** — la Web envía encabezado + líneas en una sola
   petición; `InsertarAsync` asienta todo (documento + líneas + inventario) en una sola
   transacción. **Registrar = asentar.** Sin concepto de borrador.
2. **Cancelación**: vía el flag `Cancelado='S'` (no `EstadoDoc`). Ponerlo en `'S'` sobre un
   documento asentado dispara `RevertirAsync` y marca `EstadoInv='C'`. `Eliminar` de un
   documento asentado-no-cancelado se bloquea (cancele primero).
3. **Edición post-asiento**: solo campos inocuos del encabezado — en la práctica, solo
   `Comentario` (igual que SAP B1). Las líneas son inmutables.
4. **Pre-arreglos del review final de INV-1** (tipar excepciones del servicio de asiento;
   validar el almacén): dentro de INV-2, como primeras tareas.
5. **Transacción testeable**: se introduce `IEjecutorTransaccion` inyectable (impl real
   envuelve `ApiDbTestContext.Database.BeginTransactionAsync` / commit / rollback; el doble
   de test solo ejecuta el `Func`). El dominio nunca usa `Database.BeginTransactionAsync`
   directo. Reutilizable por INV-3.

## Componentes (API)

### 0. `IEjecutorTransaccion`

Nuevo archivo `API.Domain.Interface/IEjecutorTransaccion.cs` + impl
`API.Infraestructure.Repository/EjecutorTransaccion.cs`.

```csharp
public interface IEjecutorTransaccion
{
    /// <summary>
    /// Ejecuta `operacion` dentro de una transacción de BD. Al terminar sin excepción:
    /// SaveChangesAsync (flushea todo lo que `operacion` dejó pendiente en el ChangeTracker)
    /// + Commit. Si `operacion` lanza: Rollback y se repropaga la excepción.
    /// </summary>
    Task<T> EjecutarAsync<T>(Func<Task<T>> operacion);
}
```

Impl real (`EjecutorTransaccion`, ctor toma `ApiDbTestContext _context`):

```csharp
public async Task<T> EjecutarAsync<T>(Func<Task<T>> operacion)
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    try
    {
        var resultado = await operacion();
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return resultado;
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
}
```

DI: `services.AddTransient<IEjecutorTransaccion, EjecutorTransaccion>();` en `Startup.cs`.
Doble de test: `public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion) => operacion();`
(sin transacción ni save; los repos y el asiento están mockeados).

### 1. Excepciones tipadas del inventario

Nuevo archivo `API.Domain.Core/Inventario/ExcepcionesInventario.cs` (namespace
`API.Domain.Core.Inventario`):

```csharp
public class ArticuloNoExisteException : Exception
{
    public ArticuloNoExisteException(string codArticulo)
        : base($"El artículo {codArticulo} no existe.") { }
}

public class AlmacenNoExisteException : Exception
{
    public AlmacenNoExisteException(string codAlmacen)
        : base($"El almacén {codAlmacen} no existe.") { }
}

public class StockInsuficienteException : Exception
{
    public StockInsuficienteException(string codArticulo, string codAlmacen, decimal disponible, decimal requerido)
        : base($"Stock insuficiente en el almacén {codAlmacen} para el artículo {codArticulo}: disponible {disponible}, requerido {requerido}.") { }
}
```

`InventarioAsientoService.AplicarMovimientoAsync` (INV-1) cambia:
- `?? throw new Exception($"El artículo {codArticulo} no existe.")` → `throw new ArticuloNoExisteException(codArticulo)`.
- El throw de stock negativo → `throw new StockInsuficienteException(codArticulo, codAlmacen, existencia.Disponible, -cantidad)`.
- **Nueva dependencia** `IRepositorioGenerico<Almacen, string> _repoAlmacen`. Antes del
  cálculo: `if (await _repoAlmacen.ObtenerAsync(codAlmacen) is null) throw new AlmacenNoExisteException(codAlmacen);`.
- DI de `InventarioAsientoService` en `Startup.cs`: sin cambio (el repo genérico de
  `Almacen` ya está registrado).

`InventarioAsientoServiceTests` (INV-1) se actualiza: los `Assert.ThrowsAsync<Exception>`
pasan a `Assert.ThrowsAsync<StockInsuficienteException>` / `<ArticuloNoExisteException>`;
se añade un test de almacén inexistente (mockeando el nuevo repo de `Almacen`) y se
ajusta el fixture para devolver un `Almacen` válido por defecto.

### 2. `EntregaCompra` — asiento atómico al registrar

**DTOs** (`API.Application.DTO/entregaCompra/EntregaCompraCrearDTO.cs`):
- Gana `public List<EntregaCompraDetalleCrearDTO> Lineas { get; set; } = new();`.
- `EntregaCompraDetalleCrearDTO` ya tiene `Entry` como `[Required]`; para las líneas
  embebidas ese `Entry` se ignora (lo asigna el servidor). Se documenta con un comentario;
  no se quita el atributo para no romper el endpoint standalone.

**Dominio** — `IEntregaCompraDomain`:
```csharp
Task<int> InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas);
```
(firma nueva; el `InsertarAsync(EntregaCompra obj)` de un solo parámetro se elimina).

`EntregaCompraDomain` gana dependencias: `IEjecutorTransaccion _tx` (decisión 5) e
`IInventarioAsientoService _asiento`. **No** inyecta `ApiDbTestContext` directamente —
todo el `SaveChangesAsync` lo hace `EjecutorTransaccion` (que sí toma el contexto scoped,
el mismo que usan los repos). El dominio queda 100% mockeable con Moq.

`InsertarAsync(obj, lineas)`:
1. `obj.TipoObjeto = "12"`. Numeración exactamente como hoy (valida serie
   inexistente / bloqueada / manual sin número / agotada; serie autogenerada → `obj.NumDoc
   = serie.SigNumero`, `serie.SigNumero++` en memoria).
2. `obj.EstadoInv = "A"`.
3. `return await _tx.EjecutarAsync(async () =>`:
   1. `await _repoHeader.InsertarAsync(obj);` → guarda el encabezado (y el incremento de
      `serie.SigNumero`, rastreado por el mismo contexto) y asigna `obj.Entry`. **Save #1.**
   2. `int noLinea = 1;` por cada `linea` en `lineas` (en orden): `linea.Entry = obj.Entry;
      linea.NoLinea = noLinea++;` (no se fuerza `linea.TipoObjeto` — como en el módulo de
      compra, queda al default de la columna). `await _repoDetalle.AgregarSinGuardarAsync(linea);`
      (sin guardar todavía).
   3. `var movimientos = lineas.Where(l => (l.Cantidad ?? 0m) > 0m).Select(l => new MovimientoRequest(
          TipoDoc: "12", DocEntry: obj.Entry, DocLinea: l.NoLinea,
          CodArticulo: l.CodArticulo!, CodAlmacen: l.CodAlmacen!,
          Cantidad: l.Cantidad!.Value, PrecioUnitario: l.Precio ?? 0m,
          Fecha: obj.FechaDoc ?? DateTime.Now)).ToList();`
   4. `await _asiento.AsentarAsync(movimientos);` (muta existencias / costo del artículo /
      filas de kardex en el `ChangeTracker`; ignora líneas de artículos no-inventario).
   5. `return obj.Entry;`

   `EjecutarAsync` hace luego **Save #2** (flushea las líneas + el inventario pendientes) +
   Commit. Total: 2 `SaveChangesAsync` dentro de 1 transacción. Si cualquier paso lanza
   (`AlmacenNoExisteException`, etc.) → Rollback: no queda ni el documento, ni las líneas,
   ni el incremento de la serie, ni el inventario.

`ActualizarAsync(int id, EntregaCompra obj)`:
- Carga el documento actual (`existente`). Si `existente is null` → `false` (el
  Controller ya devuelve 404 antes).
- **Si `existente.Cancelado == "S"`** → `throw new Exception("El documento está cancelado y no se puede modificar.")`.
- **Si `obj.Cancelado == "S"` (y `existente.Cancelado != "S"`)** — cancelación:
  `return await _tx.EjecutarAsync(async () => { await _asiento.RevertirAsync("12", id);
  existente.Cancelado = "S"; existente.EstadoInv = "C"; existente.FechaCancelado =
  DateTime.Now; existente.Comentario = obj.Comentario; return true; });`
  (`EjecutarAsync` hace el `SaveChangesAsync` + Commit; el reversó y los cambios de flags
  quedan en una sola transacción).
- **Si no se está cancelando** — edición inocua:
  `return await _tx.EjecutarAsync(async () => { existente.Comentario = obj.Comentario;
  return true; });` (`EjecutarAsync` hace el `SaveChangesAsync`). Se ignoran cambios a
  `CodigoSn` / `NombreSn` / `Direccion` / `MonedaDoc` / fechas / totales / `Serie` /
  `NumDoc` / `EstadoDoc`.

Las tres rutas mutantes (`InsertarAsync`, cancelación, edición inocua) pasan por
`_tx.EjecutarAsync`; `EntregaCompraDomain` nunca llama `SaveChangesAsync` directo.
- `obj.TipoObjeto` ya no se fuerza aquí (el documento existente ya lo tiene bien y no se
  reescribe el encabezado completo).

`EliminarAsync(int id)`:
- Carga `existente`. Si `existente is null` → `false`.
- **Si `existente.EstadoInv == "A"` y `existente.Cancelado != "S"`** → `throw new
  Exception("Cancele el documento (Cancelado='S') antes de eliminarlo.")`.
- En otro caso (documento cancelado / sin asiento): borra las líneas de detalle a mano y
  luego el encabezado, como hoy. No hay reversó adicional (el asiento ya se revirtió al
  cancelar).

`ObtenerAsync` / `ObtenerTodoAsync`: sin cambio.

**`EntregaCompraDetalleDomain`** — `InsertarAsync` / `ActualizarAsync` / `EliminarAsync`:
al inicio, cargar el encabezado padre (`_repoHeader.ObtenerAsync(entry)`); si existe →
`throw new Exception("Las líneas se definen al crear el documento y no se pueden modificar después.")`.
(Requiere inyectar `IRepositorioGenerico<EntregaCompra, int>` en
`EntregaCompraDetalleDomain`.) Los `Obtener*` de solo lectura no cambian.

**Aplicación** — `EntregaCompraApplication`:
- `InsertarAsync(EntregaCompraCrearDTO obj)`: `_mapper.Map<EntregaCompra>(obj)` para el
  encabezado y `_mapper.Map<IEnumerable<EntregaCompraDetalle>>(obj.Lineas)` para las
  líneas; llama `_domain.InsertarAsync(entrega, lineas)`. El `catch` traduce las
  excepciones tipadas a `respuesta.Mensaje` igual que cualquier `Exception` (el patrón de
  casa ya lo hace; no se necesita `catch` específico salvo que se quiera un mensaje
  distinto — no se requiere).
- `ActualizarAsync` / `EliminarAsync`: sin cambio de forma (el dominio hace el trabajo).
- `EntregaCompraDetalleApplication`: sin cambio de forma (el dominio lanza).

**Mapper** (`PerfilMapeo.cs`): añadir `CreateMap<EntregaCompraDetalleCrearDTO,
EntregaCompraDetalle>()` ya existe del módulo de compra; no hace falta nada nuevo salvo
confirmar que el mapeo de la colección funciona (AutoMapper mapea `List<X>` → `IEnumerable<Y>`
si existe el `CreateMap<X,Y>`).

**DI** (`Startup.cs`): `EntregaCompraDomain` y `EntregaCompraDetalleDomain` no cambian de
registro (siguen siendo `AddTransient` con sus interfaces); solo cambian sus constructores
(más dependencias, todas ya registradas).

### 3. `FacturaCompra` — idéntico a §2 con `TipoDoc = "13"`

Mismos cambios en: `FacturaCompraCrearDTO` (+ `Lineas`), `IFacturaCompraDomain.InsertarAsync`
(2 parámetros), `FacturaCompraDomain` (ctor + asiento + cancelación + guardas de
`Eliminar`), `FacturaCompraDetalleDomain` (los 3 mutantes lanzan), `FacturaCompraApplication`.

## Componentes (Web)

### `EntregasCompra` (y `FacturasCompra`, idéntico)

- `Web.ApiClient/Dtos/EntregaCompra/EntregaCompraCrearDTO.cs`: gana
  `public List<EntregaCompraDetalleCrearDTO> Lineas { get; set; } = new();`
  (con `using Web.ApiClient.Dtos.EntregaCompraDetalle;`).
- `EntregasCompraController.Crear`: ya recibe `[FromBody] EntregaCompraCrearDTO dto`;
  ahora `dto.Lineas` viene poblado. Una sola llamada `_entregasCompra.InsertarAsync(dto)`;
  se elimina cualquier expectativa de que el JS mande líneas aparte. (El resto de la
  acción — devolver `numDoc` real — no cambia.)
- `entregascompra.js`, camino **crear** (`btnGuardarEntregaCompra`): en vez de
  `POST /Crear` (solo encabezado) + `for (linea of lineasLocales) POST /CrearLinea`,
  hace **un** `POST /Crear` con `{ ...datosEncabezado, lineas: lineasLocales }`.
  El manejo de éxito/error se simplifica (un solo resultado; ya no hay "líneas guardadas:
  X de Y").
- `entregascompra.js` / `_Form.cshtml`, modo **edición**:
  - El detalle se muestra en solo-lectura: se ocultan "Agregar línea" y los botones de
    editar/eliminar fila.
  - El botón "Guardar" en edición solo envía `{ comentario }` (más el flag de cancelación
    si aplica).
  - Nuevo botón **"Cancelar documento"** (visible solo en edición, si `Cancelado != 'S'`):
    confirma (`App.confirmarEliminar`-style) y llama `POST /EntregasCompra/Editar?entry={id}`
    con `{ cancelado: "S" }`. Al volver, recarga la lista.
  - `EntregasCompraController.Editar` (hoy clona del de venta y copia campos fijos al
    `EntregaCompraActualizarDTO`): pasa a reenviar **solo** `Comentario` y `Cancelado`
    desde el `dto` entrante; el resto del `EntregaCompraActualizarDTO` puede quedar con lo
    que ya trae el documento (se relee con `ObtenerAsync` como hoy) porque el dominio de
    todos modos ignora esos campos.
  - Si el documento ya está cancelado (`Cancelado == 'S'`): el formulario de edición se
    muestra todo en solo-lectura y sin botón de cancelar.
- `CrearLinea` / `EditarLinea` / `EliminarLinea` (Web + API) se conservan pero el dominio
  los rechaza para documentos existentes; el `_Form` en edición ya no los invoca.

## Pruebas

- `EntregaCompraDomainTests` / `FacturaCompraDomainTests` (reescritos/ampliados; hoy
  prueban la firma vieja de `InsertarAsync`):
  - Insert con 2 líneas de inventario: numeración correcta, `EstadoInv='A'`, todo el
    trabajo va dentro de `_tx.EjecutarAsync` (verificar que se invocó), se llama
    `_asiento.AsentarAsync` con 2 `MovimientoRequest` (`Cantidad`, `TipoDoc='12'/'13'`,
    `DocEntry` = el `Entry` de prueba, `DocLinea` 1 y 2, `PrecioUnitario = Precio ?? 0`
    correctos), las 2 líneas se pasan a `_repoDetalle.AgregarSinGuardarAsync` con
    `Entry`/`NoLinea` asignados.
  - Línea con `Cantidad` null/0 → no genera `MovimientoRequest`.
  - Serie bloqueada / agotada / manual sin número → lanza, `AsentarAsync` nunca se llama.
  - `ActualizarAsync` con `Cancelado='S'` sobre doc asentado → `RevertirAsync("12"/"13", id)`,
    `EstadoInv='C'`, `Cancelado='S'`, `FechaCancelado` seteada.
  - `ActualizarAsync` con `Cancelado='S'` sobre doc ya cancelado → lanza.
  - `ActualizarAsync` normal sobre doc asentado → solo copia `Comentario`; no toca
    `CodigoSn`, `MonedaDoc`, etc.
  - `EliminarAsync` sobre doc asentado-no-cancelado → lanza; sobre doc cancelado → borra
    líneas + encabezado.
  - Se mockean `IInventarioAsientoService`, `IEjecutorTransaccion` (doble que ejecuta el
    `Func` directo, sin transacción ni save) y los 3 repos. `_repoHeader.InsertarAsync`
    mockeado devuelve el `obj` con un `Entry` de prueba fijado (p. ej. 99). No se necesita
    ningún `ApiDbTestContext` ni proveedor EF en las pruebas del dominio: el dominio no
    llama `SaveChangesAsync`.
- `EntregaCompraDetalleDomainTests` / `FacturaCompraDetalleDomainTests`: `InsertarAsync` /
  `ActualizarAsync` / `EliminarAsync` lanzan cuando el encabezado padre existe.
- `InventarioAsientoServiceTests` (INV-1, actualizado): excepciones tipadas; almacén
  inexistente → `AlmacenNoExisteException`.
- Verificación: `dotnet build` de ambas soluciones sin errores; `dotnet test` de la suite
  completa de la API en verde; prueba manual en el navegador —
  1. Crear una `EntregaCompra` con 2 líneas de un artículo de inventario → en "Existencias"
     el disponible del almacén sube; el kardex del artículo muestra los 2 movimientos con
     `TipoDoc=12` y saldos corridos; `Articulo.CostoPromedio` refleja el promedio móvil.
  2. Editar esa entrega: cambiar el comentario (se guarda); confirmar que no se pueden
     editar/agregar líneas.
  3. "Cancelar documento" → el kardex gana los movimientos inversos, el disponible vuelve
     al valor previo, `EstadoInv='C'`.
  4. Repetir 1-3 para `FacturaCompra` (`TipoDoc=13`).

## Riesgos / notas

- **`IEjecutorTransaccion` real no se cubre con pruebas unitarias** (necesitaría un
  proveedor EF): su corrección — commit al retornar, rollback al lanzar, un solo
  `SaveChangesAsync` final — se valida en la prueba manual del navegador (crear con líneas
  → todo cuaja; forzar un fallo de almacén → nada cuaja).
- **Migración de datos**: los documentos de compra ya existentes en `API_DB_TEST` (creados
  antes de INV-2, sin asiento) quedan con `EstadoInv='A'` pero sin movimientos de
  inventario. No se reprocesan. Si hay que "sembrar" inventario a partir de ellos, es una
  tarea aparte (o se usa el documento Entrada de Mercancías de INV-4). El plan debe
  incluir una nota para el usuario.
- **`EntregaCompraDetalleController` / endpoints de línea**: siguen expuestos pero el
  dominio los rechaza para documentos existentes. Un cliente que hoy dependa de
  `CrearLinea` post-creación se romperá — es intencional (con Opción B las líneas son del
  documento, no post-hoc).

## Fuera de alcance de INV-2 (explícito)

- Documentos de venta (INV-3).
- Documentos Entrada / Salida de mercancías estilo SAP (INV-4).
- Traslados entre almacenes.
- Reserva de stock (`Comprometido` / `Pedido` siguen en 0 y sin lógica; `PedidoCompra` no
  asienta nada).
- Reintento por `DbUpdateConcurrencyException` (queda para una capa Application futura).
- "Descancelar" un documento; documento de reversión estilo SAP.
- Cambiar `MetodoValuacion` / `CostoEstandar` del artículo por pantalla.
