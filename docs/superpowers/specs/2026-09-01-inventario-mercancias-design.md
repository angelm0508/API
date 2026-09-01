# Entrada y Salida de Mercancías (INV-4) — Diseño

**Fecha:** 2026-09-01
**Fase:** INV-4 (sigue a INV-1 núcleo, INV-2 asiento en compras, INV-3 asiento en ventas)
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

Dos documentos nuevos de **ajuste de inventario sin socio de negocio**, tomando como referencia
OIGN/OIGE de SAP B1:

- **`EntradaMercancia`** (`TipoObjeto = "59"`): suma stock a un costo por línea. Casos:
  inventario inicial, sobrantes de conteo físico, devoluciones internas.
- **`SalidaMercancia`** (`TipoObjeto = "60"`): descuenta stock al costo promedio móvil (COGS),
  con bloqueo duro de negativo. Casos: mermas, consumo interno, muestras, faltantes de conteo.

Mismo patrón de asiento atómico + cancelación por `Cancelado='S'` + edición post-asiento solo
`Comentario` que INV-2 (compras) e INV-3 (ventas), ya mergeados en `main`.

## Contexto ya resuelto por fases previas (no se re-implementa)

- **`IInventarioAsientoService`** (INV-1): `AsentarAsync(IEnumerable<MovimientoRequest>, bool permitirNegativo = false)`
  — `MovimientoRequest.Cantidad > 0` = entrada, `< 0` = salida; con `permitirNegativo:false` y
  `Disponible + Cantidad < 0` lanza `StockInsuficienteException`. `RevertirAsync(string tipoDoc, int docEntry)`
  genera los movimientos inversos. Ninguno llama `SaveChangesAsync`. Firma:
  `MovimientoRequest(string TipoDoc, int DocEntry, int DocLinea, string CodArticulo, string CodAlmacen, decimal Cantidad, decimal PrecioUnitario, DateTime Fecha)`.
  Excepciones tipadas en `API.Domain.Core.Inventario`: `ArticuloNoExisteException`,
  `AlmacenNoExisteException`, `StockInsuficienteException(string codArticulo, string codAlmacen, decimal disponible, decimal requerido)`.
- **`IEjecutorTransaccion.EjecutarAsync<T>(Func<Task<T>>)`** (INV-2): abre transacción, corre el
  func, `SaveChangesAsync` + `Commit`; si lanza, `Rollback` + `ChangeTracker.Clear()` + repropaga.
  Los domains nunca llaman `SaveChangesAsync` directo.
- **`IRepositorioGenerico.AgregarSinGuardarAsync(T)`** = `DbSet.AddAsync` sin guardar.
- **`Articulo`**: `MetodoValuacion` `nvarchar(1)` CHECK `IN ('P','E')` (P=promedio móvil,
  E=estándar), `CostoPromedio` / `CostoEstandar` / `CantDisponible` / `ValorInventario`
  `decimal(19,6)`. `IRepositorioGenerico<Articulo,string>` ya registrado en DI.
- **`NumeracionDocumentoDet`**: PK compuesta `(CodigoObj, Serie)`; `Serie int` (identificador de
  serie global que el usuario ve). Columnas relevantes: `SigNumero int?`, `FinNumero int?`,
  `Bloqueado`, `Manual`, `NombreSerie`, `SubTipoDoc` (NOT NULL), `TipoSerie` (NOT NULL). Los
  domains de compra/venta ya numeran contra esta tabla; INV-4 reusa la misma lógica.
- **Lecciones de INV-3 aplicadas de entrada:** (a) `InsertarAsync` fuerza `Cancelado="N"` y
  `FechaCancelado=null` (un documento recién registrado nunca nace cancelado); (b) edición
  inocua copia `Comentario` incondicional (replace-semantics: vaciar borra); (c) `EliminarAsync`
  envuelve el borrado en `_tx.EjecutarAsync`; (d) `DetalleDomain.InsertarAsync` rechaza
  incondicionalmente.

## Decisiones (tomadas en brainstorming)

1. **Forma de las tablas:** referencia de OIGN/IGN1 y OIGE/IGE1 de SAP B1, recortada a lo que el
   proyecto usa (sin módulo contable → sin `AcctCode`/`TransId`/`JrnlMemo`/dimensiones; sin
   socio; sin multi-moneda), alineada con la forma de `EntregaCompra`.
2. **Creación:** DDL idempotente versionado en `API/sql/2026-09-01-inventario-mercancias.sql`,
   **aplicado por el usuario** en `API_DB_TEST` (igual que INV-1) + entidades EF +
   `OnModelCreating` + DI.
3. **Costo de la Entrada sin costo:** si `linea.CostoUnitario` es null o ≤ 0, el servidor usa
   el costo vigente del artículo: `MetodoValuacion == "E" ? CostoEstandar : CostoPromedio`. Una
   entrada sin costo no distorsiona la valuación (ideal para ajustes de conteo).
4. **Salida:** bloqueo duro de negativo (`permitirNegativo: false`). El costo lo determina
   siempre el servidor (`MetodoValuacion == "E" ? CostoEstandar : CostoPromedio`), ignorando
   cualquier `CostoUnitario` que venga en la línea (una salida no recalcula el promedio, así que
   ese costo es estable durante todo el documento y coincide con el COGS del asiento).
5. **Web:** dos pantallas nuevas (`EntradasMercancia`, `SalidasMercancia`) bajo el submenú
   Inventario, mismo patrón post-fix-wave que INV-2/INV-3.
6. **Numeración:** el `.sql` siembra una serie "Primario" autogenerada (`SigNumero=1`,
   `Manual='N'`, `Bloqueado='N'`) para `CodigoObj='59'` y `'60'` si no existen; el usuario las
   ajusta después en "Numeración de documentos".
7. **Estructura del plan:** un spec y un plan; `SalidaMercanciaDomain` = transformación DRY de
   `EntradaMercanciaDomain` (sustitución + 3 deltas: costo simplificado, signo negativo, test de
   stock insuficiente); Web Salidas = transformación de Web Entradas; ejecución subagent-driven.

## §1 — Modelo de datos

### `API/sql/2026-09-01-inventario-mercancias.sql`

Idempotente (guardas `IF OBJECT_ID(...) IS NULL` / `IF NOT EXISTS`), estilo del script de INV-1.

**`EntradaMercancia`** y **`SalidaMercancia`** (misma estructura, distinto nombre y default de
`TipoObjeto`):

| Columna | Tipo | Default | Nota |
|---|---|---|---|
| `Entry` | `int IDENTITY` PK | | |
| `NumDoc` | `int NOT NULL` | `0` | número de documento (lo asigna el server) |
| `Serie` | `int NOT NULL` | | FK a `NumeracionDocumentoDet` — seguir el patrón de las tablas de compra existentes (revisar si hay índice único en `NumeracionDocumentoDet.Serie`; si la FK simple no es limpia contra la PK compuesta, dejar `Serie` sin FK declarada como ya hacen otras tablas del proyecto y confiar en la validación del domain) |
| `NumManual` | `char(1) NOT NULL` | `'N'` | CHECK `IN ('S','N')` |
| `Imprimido` | `char(1) NOT NULL` | `'N'` | |
| `EstadoDoc` | `char(1) NOT NULL` | `'A'` | CHECK `IN ('A','C')` — INV-4 no lo toca |
| `EstadoInv` | `char(1) NOT NULL` | `'A'` | CHECK `IN ('A','C')` — asentado / revertido |
| `Cancelado` | `char(1) NOT NULL` | `'N'` | CHECK `IN ('S','N')` — `'S'` dispara el reversó |
| `TipoObjeto` | `varchar(11) NOT NULL` | `'59'` / `'60'` | |
| `FechaDoc` | `datetime NULL` | | |
| `FechaContab` | `datetime NULL` | | fecha que va al kardex (TaxDate de SAP) |
| `FechaCancelado` | `datetime NULL` | | |
| `Referencia` | `nvarchar(100) NULL` | | referencia libre (p.ej. "conteo físico #12") |
| `Comentario` | `nvarchar(254) NULL` | | |
| `TotalDoc` | `decimal(19,6) NOT NULL` | `0` | `Σ (Cantidad · CostoUnitario)`, lo calcula el server |

**`EntradaMercanciaDetalle`** y **`SalidaMercanciaDetalle`**:

| Columna | Tipo | Default | Nota |
|---|---|---|---|
| `Entry` | `int NOT NULL` | | parte de la PK — **sin FK** a la tabla de encabezado (mismo criterio que el resto del proyecto; el domain borra líneas a mano) |
| `NoLinea` | `int NOT NULL` | | PK `(Entry, NoLinea)` |
| `CodArticulo` | `varchar(20) NULL` | | FK a `Articulo` |
| `Descripcion` | `nvarchar(254) NULL` | | |
| `Cantidad` | `decimal(19,6) NULL` | | |
| `CostoUnitario` | `decimal(19,6) NOT NULL` | `0` | Entrada: lo teclea el usuario (o fallback). Salida: lo pone el server |
| `TotalLinea` | `decimal(19,6) NULL` | | `Cantidad · CostoUnitario`, lo calcula el server |
| `CodAlmacen` | `varchar(10) NULL` | | FK a `Almacen` |

**Seed de numeración** (al final del script, idempotente):

```sql
IF NOT EXISTS (SELECT 1 FROM NumeracionDocumentoDet WHERE CodigoObj = '59')
    INSERT INTO NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, SigNumero, Manual, Bloqueado, SubTipoDoc, TipoSerie)
    VALUES ('59', <siguiente Serie libre>, 'Primario', 1, 'N', 'N', '--', 'N');
-- ídem para '60'
```
El `<siguiente Serie libre>` = `(SELECT ISNULL(MAX(Serie), 0) + 1 FROM NumeracionDocumentoDet)`
(o `+2` para el segundo). El implementer confirma las columnas NOT NULL reales de
`NumeracionDocumentoDet` y les da valores válidos mirando filas existentes.

### Entidades EF

`API.Domain.Entity/Models/EntradaMercancia.cs`, `EntradaMercanciaDetalle.cs`,
`SalidaMercancia.cs`, `SalidaMercanciaDetalle.cs` — `partial class` al estilo de
`EntregaCompra.cs` / `EntregaCompraDetalle.cs`, propiedades que espejan las columnas de arriba,
navegación `SerieNavigation : NumeracionDocumentoDet` en el encabezado y
`CodArticuloNavigation`/`CodAlmacenNavigation` en el detalle.

### `OnModelCreating` (`ApiDbTestContext.cs`)

Cuatro bloques `modelBuilder.Entity<...>(entity => { ... })` copiando el patrón de
`EntregaCompra` / `EntregaCompraDetalle`:
- Encabezado: `HasKey(e => e.Entry)`, `ToTable("EntradaMercancia")`, `HasMaxLength(1)` +
  `HasDefaultValueSql` en `NumManual`/`Imprimido`/`EstadoDoc`/`EstadoInv`/`Cancelado`,
  `HasMaxLength(11)` + default en `TipoObjeto`, `HasColumnType("datetime")` en las 3 fechas,
  `HasColumnType("decimal(19, 6)")` en `TotalDoc`, `HasMaxLength(100)` en `Referencia`,
  `HasMaxLength(254)` en `Comentario`, `HasOne(d => d.SerieNavigation).WithMany(...).HasForeignKey(d => d.Serie).OnDelete(DeleteBehavior.ClientSetNull)`.
- Detalle: `HasKey(e => new { e.Entry, e.NoLinea })`, `ToTable("EntradaMercanciaDetalle")`,
  `decimal(19,6)` en `Cantidad`/`CostoUnitario`/`TotalLinea`, `HasMaxLength` en
  `CodArticulo`/`CodAlmacen`/`Descripcion`, `HasOne` a `Almacen` y `Articulo`.
- Añadir los `DbSet<...>` correspondientes y las `ICollection<...>` inversas en
  `NumeracionDocumentoDet` / `Articulo` / `Almacen` si el patrón del proyecto las exige para que
  compile (mirar cómo lo hizo `EntregaCompra`).

### DI (`Startup.cs`)

Registrar (junto a los bloques de `EntregaCompra`):
- `IRepositorioGenerico<EntradaMercancia, int>` → `RepositorioGenericoEfCore<EntradaMercancia, int>`
  (o el repo concreto si el proyecto usa uno por entidad — seguir el patrón de `EntregaCompra`).
- `IRepositorioGenerico<EntradaMercanciaDetalle, (int Entry, int NoLinea)>`.
- `IRepositorioGenerico<SalidaMercancia, int>`, `<SalidaMercanciaDetalle, (int, int)>`.
- `IEntradaMercanciaDomain` → `EntradaMercanciaDomain`, `IEntradaMercanciaDetalleDomain` → …,
  `IEntradaMercanciaApplication` → …, y los 3 equivalentes de `Salida`.
- `IRepositorioGenerico<Articulo, string>` ya está registrado — no duplicar.

## §2 — `EntradaMercanciaDomain` (canónico)

### DTO e interfaz

- `API.Application.DTO/entradaMercancia/EntradaMercanciaCrearDTO.cs` — campos del encabezado
  (`NumDoc?`, `Serie` `[Required]`, `NumManual?`, `FechaDoc?`, `FechaContab?`, `Referencia?`,
  `Comentario?`, `Cancelado?` — se ignora, ver lección INV-3) + `public List<EntradaMercanciaDetalleCrearDTO> Lineas { get; set; } = new();`.
  `EntradaMercanciaDetalleCrearDTO`: `Entry` `[Required]`, `CodArticulo?`, `Descripcion?`,
  `Cantidad?`, `CostoUnitario?`, `CodAlmacen?`.
- También `EntradaMercanciaActualizarDTO` (para editar) y `EntradaMercanciaDTO` (lectura), al
  estilo de los de `EntregaCompra`.
- Mapas en `PerfilMapeo.cs`: `CreateMap<EntradaMercanciaCrearDTO, EntradaMercancia>()`,
  `CreateMap<EntradaMercanciaDetalleCrearDTO, EntradaMercanciaDetalle>()`,
  `CreateMap<EntradaMercancia, EntradaMercanciaDTO>()`, `<EntradaMercanciaActualizarDTO, EntradaMercancia>()`
  (+ los 4 de `Salida`).
- `IEntradaMercanciaDomain`:
  `Task<int> InsertarAsync(EntradaMercancia obj, IEnumerable<EntradaMercanciaDetalle> lineas)`,
  `Task<bool> ActualizarAsync(int id, EntradaMercancia obj)`, `Task<bool> EliminarAsync(int id)`,
  `Task<EntradaMercancia> ObtenerAsync(int id)`, `Task<IQueryable<EntradaMercancia>> ObtenerTodoAsync()`.

### `EntradaMercanciaDomain`

Ctor (6 deps): `IRepositorioGenerico<EntradaMercancia,int> repoEntrada`,
`IRepositorioGenerico<EntradaMercanciaDetalle,(int Entry,int NoLinea)> repoDetalle`,
`IRepositorioGenerico<NumeracionDocumentoDet,int> repoNumeracion`, `IEjecutorTransaccion tx`,
`IInventarioAsientoService asiento`, `IRepositorioGenerico<Articulo,string> repoArticulo`.

**`InsertarAsync(EntradaMercancia obj, IEnumerable<EntradaMercanciaDetalle> lineas)`:**

1. `obj.TipoObjeto = "59";` `obj.EstadoInv = "A";` `obj.Cancelado = "N";` `obj.FechaCancelado = null;`
2. Numeración idéntica al canónico de INV-2/INV-3 (`_repoNumeracion.ObtenerAsync(obj.Serie)` →
   inexistente lanza; `Bloqueado == "S"` lanza; `Manual == "S"` y `NumDoc <= 0` lanza; si no,
   `SigNumero == null` lanza, `FinNumero` agotado lanza, `obj.NumDoc = serie.SigNumero.Value`,
   `serie.SigNumero++` en memoria). Antes de abrir la transacción.
3. `var lineasList = lineas?.ToList() ?? new List<EntradaMercanciaDetalle>();`
4. `return await _tx.EjecutarAsync(async () => {`
   - `await _repoEntrada.InsertarAsync(obj);` → asigna `obj.Entry` (Save #1, dentro de la tx).
   - `var noLinea = 1; decimal totalDoc = 0m;`
   - `foreach (var linea in lineasList) {`
     - `linea.Entry = obj.Entry; linea.NoLinea = noLinea++;`
     - `var costo = (linea.CostoUnitario ?? 0m) > 0m ? linea.CostoUnitario!.Value : await CostoVigenteAsync(linea.CodArticulo);`
     - `linea.CostoUnitario = costo;`
     - `linea.TotalLinea = (linea.Cantidad ?? 0m) * costo;`
     - `totalDoc += linea.TotalLinea.Value;`
     - `await _repoDetalle.AgregarSinGuardarAsync(linea);`
   - `}`
   - `obj.TotalDoc = totalDoc;`
   - ```
     var movimientos = lineasList
         .Where(l => (l.Cantidad ?? 0m) > 0m)
         .Select(l => new MovimientoRequest(
             TipoDoc: "59",
             DocEntry: obj.Entry,
             DocLinea: l.NoLinea,
             CodArticulo: l.CodArticulo!,
             CodAlmacen: l.CodAlmacen!,
             Cantidad: l.Cantidad!.Value,             // positiva = entrada
             PrecioUnitario: l.CostoUnitario!.Value,  // costo ya resuelto
             Fecha: obj.FechaContab ?? obj.FechaDoc ?? DateTime.Now))
         .ToList();
     ```
   - `await _asiento.AsentarAsync(movimientos);`  (default `permitirNegativo:false` — irrelevante para entradas)
   - `return obj.Entry;`
   `});`

**`CostoVigenteAsync(string? codArticulo)`** (privado): si `codArticulo` es null → `return 0m;`
si no, `var art = await _repoArticulo.ObtenerAsync(codArticulo); return art is null ? 0m : (art.MetodoValuacion == "E" ? art.CostoEstandar : art.CostoPromedio);`
(un artículo inexistente lo rechaza después `AsentarAsync` con `ArticuloNoExisteException`).

**`ActualizarAsync(int id, EntradaMercancia obj)`:** idéntico al canónico de INV-2/INV-3.
`existente is null` → `false`. `existente.Cancelado == "S"` → lanza. `obj.Cancelado == "S"` →
`_tx.EjecutarAsync`(`RevertirAsync("59", id)` + `Cancelado="S"` + `EstadoInv="C"` +
`FechaCancelado=DateTime.Now` + `if (obj.Comentario != null) existente.Comentario = obj.Comentario`).
Si no → `_tx.EjecutarAsync`(`existente.Comentario = obj.Comentario`  — incondicional).

**`EliminarAsync(int id)`:** `existente is null` → `false`;
`existente.EstadoInv == "A" && existente.Cancelado != "S"` → lanza
`"Cancele el documento (Cancelado='S') antes de eliminarlo."`; si no,
`return await _tx.EjecutarAsync(async () => { ...borrar líneas del Entry a mano...; return await _repoEntrada.EliminarAsync(id); });`.

**`ObtenerAsync` / `ObtenerTodoAsync`:** delegan al repo.

### `EntradaMercanciaDetalleDomain`

Ctor: `(IRepositorioGenerico<EntradaMercanciaDetalle,(int,int)> repoGenericoDet, IRepositorioGenerico<EntradaMercancia,int> repoEncabezado)`
— detalle primero, encabezado segundo. `InsertarAsync(obj)` rechaza incondicionalmente
(`await Task.CompletedTask; throw new Exception("Las líneas se definen al crear el documento y no se pueden agregar después.");`).
`ActualizarAsync` / `EliminarAsync` llaman `await LanzarSiElDocumentoExisteAsync(entry)` al
inicio (lanza si el encabezado existe). `ObtenerPorEntradaMercanciaAsync(int entry)` + `Obtener*`
delegan al repo.

### `EntradaMercanciaApplication.InsertarAsync`

`var entrada = _mapper.Map<EntradaMercancia>(obj); var lineas = _mapper.Map<IEnumerable<EntradaMercanciaDetalle>>(obj.Lineas); respuesta.Dato = await _domain.InsertarAsync(entrada, lineas);`
Resto de métodos (`Actualizar`/`Eliminar`/`Obtener`/`ObtenerTodo`) al estilo de
`EntregaCompraApplication`.

### Controller

`API.Service.WebApi/Controllers/EntradaMercanciaController.cs` — CRUD estándar
(`[HttpGet]` lista + por id, `[HttpPost]` crear, `[HttpPut("{id}")]` actualizar,
`[HttpDelete("{id}")]` eliminar), delegando a `IEntradaMercanciaApplication`, al estilo de
`EntregaCompraController`.

## §3 — `SalidaMercanciaDomain` (transformación de §2 + 3 deltas)

Todos los archivos `SalidaMercancia*` = los `EntradaMercancia*` con la tabla de sustitución:

| §2 (`EntradaMercancia`) | §3 (`SalidaMercancia`) |
|---|---|
| `EntradaMercancia` / `EntradaMercanciaDetalle` (tipos, DTO ns `entradaMercancia`) | `SalidaMercancia` / `SalidaMercanciaDetalle` (ns `salidaMercancia`) |
| `TipoObjeto = "59"` | `TipoObjeto = "60"` |
| `IEntradaMercanciaDomain` / `IEntradaMercanciaDetalleDomain` | `ISalidaMercanciaDomain` / `ISalidaMercanciaDetalleDomain` |
| `EntradaMercanciaApplication` | `SalidaMercanciaApplication` |
| `ObtenerPorEntradaMercanciaAsync` | `ObtenerPorSalidaMercanciaAsync` |
| `RevertirAsync("59", id)` | `RevertirAsync("60", id)` |
| "entrada(s) de mercancía" (mensajes) | "salida(s) de mercancía" |
| `EntradaMercanciaDomainTests` / `EntradaMercanciaDetalleDomainTests` | `SalidaMercanciaDomainTests` / `SalidaMercanciaDetalleDomainTests` |

**Delta A — costo simplificado.** En `SalidaMercanciaDomain.InsertarAsync`, el bloque de
resolución de costo por línea NO consulta `linea.CostoUnitario`: siempre
`var costo = await CostoVigenteAsync(linea.CodArticulo);` (que devuelve
`art.MetodoValuacion == "E" ? art.CostoEstandar : art.CostoPromedio`). El resto igual
(`linea.CostoUnitario = costo; linea.TotalLinea = (linea.Cantidad ?? 0m) * costo; totalDoc += ...`).

**Delta B — signo.** En el `Select` de `movimientos`:
```csharp
        Cantidad: -(l.Cantidad!.Value),   // negativo = salida
```
`TipoDoc: "60"`. `PrecioUnitario: l.CostoUnitario!.Value` (el costo resuelto — `CalcularSalida`
lo ignora para valuación pero se estampa en `MovimientoInventario.PrecioUnitario`).
`await _asiento.AsentarAsync(movimientos);` **sin segundo argumento** → `permitirNegativo`
default `false` → si alguna línea deja `Disponible < 0` lanza `StockInsuficienteException` y
`EjecutarAsync` hace rollback total.

**Delta C — test.** `SalidaMercanciaDomainTests` gana `InsertarAsync_StockInsuficiente_Propaga`
(configura `_asiento.AsentarAsync(...)` para lanzar `StockInsuficienteException`, assertea
`Assert.ThrowsAsync<StockInsuficienteException>` — tipo exacto).

## §4 — Web

### `EntradasMercancia` (canónica)

Sigue el patrón post-fix-wave de `EntregasCompra` (INV-2) / `Entregas` (INV-3):

- `Web.ApiClient/Dtos/EntradaMercancia/` — `EntradaMercanciaCrearDTO` (con
  `List<EntradaMercanciaDetalleCrearDTO> Lineas`), `EntradaMercanciaActualizarDTO` (con
  `Cancelado`), `EntradaMercanciaDTO`, `EntradaMercanciaDetalleCrearDTO`,
  `EntradaMercanciaDetalleDTO`.
- `Web.ApiClient` — `IEntradaMercanciaApiClient` + implementación (GET lista / por id, POST
  crear, PUT editar, DELETE), al estilo de `IEntregaCompraApiClient`.
- `Web.UI/Controllers/EntradasMercanciaController.cs` — `Index`, `ObtenerTodos`,
  `FormularioCrear`, `FormularioEditar` (pasa `Cancelado` + `ViewBag.EntryActual`), `Crear`
  (`[FromBody] EntradaMercanciaCrearDTO`), `Editar` (reenvía `Cancelado`), `Eliminar`,
  `BuscarArticulos`, `BuscarAlmacenes`, `ObtenerAlmacenPorCodigo`, `ObtenerDetalle`. **Sin**
  `BuscarSocios` ni `BuscarImpuestos`.
- `Web.UI/wwwroot/js/entradasmercancia.js` — mismo esqueleto que `entregascompra.js`:
  DataTable con badge `row.cancelado === 'S'`; alta en **una** petición
  `POST /EntradasMercancia/Crear` con `datos.Lineas`; dos guards cliente (≥1 línea; `Cantidad>0`
  sin `CodAlmacen`); handler `#btnCancelarDocEntradaMercancia` con `$btn.prop('disabled', true)`
  + `finally`; `pintarDetalle()` sin botones por fila en edición. El panel de línea tiene
  **artículo, almacén, cantidad, costo unitario** (sin impuesto ni descuento); al elegir
  artículo, autocompleta `Descripcion` y propone `CostoUnitario = CostoPromedio` (editable).
  `calcularTotalesDesdeLineas` = `Σ Cantidad·CostoUnitario`.
- `Web.UI/Views/EntradasMercancia/Index.cshtml` + `_Form.cshtml` — encabezado con Serie,
  FechaDoc, FechaContab, Referencia, Comentario; en edición todo read-only salvo `Comentario`
  (patrón de `Serie`); botón "Cancelar documento" gated `esEdicion && (Model.Cancelado ?? "N") != "S"`;
  "Guardar" oculto si `Cancelado == "S"`; "Agregar línea" en `@if (!esEdicion)`.
- `_Layout.cshtml` — submenú Inventario += `<a>` a `EntradasMercancia` y `SalidasMercancia`.

### `SalidasMercancia` (transformación de `EntradasMercancia`)

Sustitución `EntradaMercancia`→`SalidaMercancia`, `/EntradasMercancia/`→`/SalidasMercancia/`,
`#btn...EntradaMercancia`→`#btn...SalidaMercancia`, "entrada de mercancía"→"salida de mercancía".
**Diferencia:** el panel de línea de la Salida **no** pide `CostoUnitario` (el server lo
calcula); la columna de costo en el grid es informativa (muestra el `CostoPromedio` del
artículo al elegirlo) o se omite. `calcularTotalesDesdeLineas` puede quedar en `Σ Cantidad·costoInformativo`
o mostrar solo cantidades.

## §5 — Semántica de cancelación, edición y errores

Idéntica a INV-2/INV-3:

- **Cancelar** (`Cancelado='S'`): `RevertirAsync("59"/"60", id)` genera los movimientos inversos
  (Entrada → sale lo que entró; Salida → reingresa lo que salió), marca `EstadoInv='C'`,
  `FechaCancelado`. Reingresar/retirar en la reversa nunca se bloquea (`RevertirAsync` fuerza
  `permitirNegativo:true` internamente). Recancelar lanza. `EstadoDoc` no se toca.
- **Edición post-asiento:** solo `Comentario` (replace-semantics). Cambios a
  fechas/referencia/serie/`NumDoc`/líneas se ignoran en el dominio; la UI los deja read-only.
- **Eliminar:** bloqueado si `EstadoInv='A'` y no cancelado. Cancelado o sin asiento → borra
  líneas + encabezado dentro de `_tx.EjecutarAsync`.
- **Errores tipados:** `StockInsuficienteException` (Salida), `ArticuloNoExisteException`,
  `AlmacenNoExisteException`. Cualquiera dentro de `EjecutarAsync` → rollback total: ni
  documento, ni líneas, ni movimientos, ni avanza `SigNumero`.

## §6 — Pruebas

xUnit + Moq, sin proveedor EF. El `_tx` mock corre el `Func` para `Func<Task<int>>` y
`Func<Task<bool>>`. Mock de `IRepositorioGenerico<Articulo,string>` para el costo vigente.

- **`EntradaMercanciaDomainTests`:**
  - `InsertarAsync` con líneas: numera, asienta entrada (capturar los `MovimientoRequest`:
    `Cantidad` positiva, `TipoDoc == "59"`, `PrecioUnitario == costo resuelto`), `EstadoInv == "A"`,
    `TotalDoc == Σ Cantidad·costo`.
  - **Costo:** línea con `CostoUnitario = 15` explícito → el movimiento y `linea.CostoUnitario`
    usan 15. Línea con `CostoUnitario = null`/`0`, artículo `MetodoValuacion='P'`,
    `CostoPromedio = 22` → usa 22. Artículo `MetodoValuacion='E'`, `CostoEstandar = 30` → usa 30.
  - `Cantidad <= 0` → no genera movimiento (pero la línea se inserta).
  - `_tx.EjecutarAsync` `Times.Once`; `serie.SigNumero` avanzó.
  - Serie inexistente / bloqueada / manual sin `NumDoc` / agotada → lanza y no inserta.
  - `Cancelado = "S"` enviado por el cliente → se ignora (`obj.Cancelado == "N"`,
    `obj.FechaCancelado == null`).
  - Cancelar: `obj.Cancelado == "S"` → `RevertirAsync("59", id)` invocado, `EstadoInv == "C"`,
    `FechaCancelado` no nulo, `Comentario` copiado si no nulo / preservado si nulo.
  - Recancelar (`existente.Cancelado == "S"`) → lanza.
  - Edición inocua: copia solo `Comentario`; `Comentario == null` lo borra.
  - `EliminarAsync` asentado-no-cancelado → lanza; cancelado → borra 2 líneas (`_repoDetalle.EliminarAsync((id,1))` / `((id,2))`) + encabezado.
- **`SalidaMercanciaDomainTests`:** lo anterior transformado +
  - `InsertarAsync_StockInsuficiente_Propaga` (tipo exacto `StockInsuficienteException`).
  - Costo: aunque la línea traiga `CostoUnitario = 999`, el movimiento y `linea.CostoUnitario`
    usan `CostoPromedio` (o `CostoEstandar`) del artículo — nunca el valor del cliente.
  - Capturar `MovimientoRequest.Cantidad` negativa.
- **`EntradaMercanciaDetalleDomainTests` / `SalidaMercanciaDetalleDomainTests`:** `InsertarAsync`
  siempre lanza (`_repoDet.InsertarAsync` `Times.Never`); `ActualizarAsync`/`EliminarAsync`
  lanzan si el encabezado existe.
- **`InventarioAsientoServiceTests`:** sin cambios (entrada y salida + `StockInsuficienteException`
  ya cubiertos por INV-1/INV-3).
- **Verificación conjunta (Task final):** el usuario aplica el `.sql`; build API + suite
  completa verde (baseline **695**) + build Web + checklist manual navegador (crear Entrada →
  stock sube y `CostoPromedio` se re-pondera; crear Salida → stock baja, Salida > disponible →
  error + rollback + `SigNumero` intacto; cancelar cada uno → reversó; editar solo comentario).

## Riesgos y deuda conocida

- El `.sql` lo aplica el usuario antes de que nada corra contra la BD real; los unit tests usan
  mocks. La estructura real de FK de `Serie` y las columnas NOT NULL de `NumeracionDocumentoDet`
  las confirma el implementer de la Task 1 mirando las tablas de compra existentes.
- `EjecutorTransaccion` (commit/rollback real contra SQL Server) sigue sin cobertura
  automatizada — se valida en la prueba manual.
- `EntradaMercanciaDomain` hace una lectura extra de `Articulo` por línea sin costo; es el mismo
  `ApiDbTestContext` scoped con tracking, así que `InventarioAsientoService` reusa la instancia
  cacheada (sin round-trip).
- **I-3 heredado de INV-3:** el guard de negativo mira `ExistenciaArticulo.Disponible` (por
  artículo+almacén); la valuación y `Articulo.CantDisponible` son globales. Si están
  desincronizados (artículo con existencia sembrada a mano y `CantDisponible` en `NULL`), una
  Salida puede dejar `ValorInventario` negativo. Código de INV-1, fuera de alcance; se cubre en
  la prueba manual.
- Re-mezcla del promedio al cancelar una Salida (una entrada sí recalcula el promedio) —
  benigna, restaura exacto si nada cambió entre medias.
- Race de doble cancelación: el re-check `existente.Cancelado == "S"` está fuera de la
  transacción; mitigado por el `yaRevertidos` de INV-1 y por el botón deshabilitado en cliente.
- Mismo bug de INV-2 (`Cancelado` no forzado en el alta de `EntregaCompraDomain`/`FacturaCompraDomain`)
  sigue abierto — INV-4 lo evita de entrada en sus propios domains; el follow-up de compras es
  independiente.

## Fuera de alcance

- Flujo de conteo físico / "Inventory Counting" + "Inventory Posting" de SAP (INV-4 solo cubre
  el documento manual directo: crear Entrada/Salida tecleando líneas).
- Traslados entre almacenes (documento aparte, futura fase).
- `BaseEntry` / chaining (estos documentos no encadenan con otros).
- Reintento por `DbUpdateConcurrencyException`; descancelar; editar líneas post-asiento;
  reprocesar documentos previos.
- Pantalla para editar `MetodoValuacion` / `CostoEstandar` del artículo (sigue haciéndose por
  SQL, como en INV-1).
