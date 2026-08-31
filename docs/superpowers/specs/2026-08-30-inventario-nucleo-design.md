# Diseño: Núcleo de inventario multi-almacén (INV-1)

## Contexto

El sistema ya tiene los procesos de venta (Cotización → Pedido → Entrega → Factura) y de
compra (PedidoCompra → EntregaCompra → FacturaCompra), API + Web, pero **ningún control de
inventario**: registrar una entrega o factura no mueve stock, no hay existencias por
almacén ni kardex.

El usuario pidió llevar control de inventario multi-almacén y asentar stock al registrar
documentos de compra, **tomando como referencia SAP B1**. Durante el brainstorming el
alcance creció a un subsistema completo, que se descompone en fases independientes:

| Fase | Contenido |
|---|---|
| **INV-1 — Núcleo** (este spec) | Tablas, motor de valuación, servicio de asiento atómico, API + Web de consulta. Sin enganche a documentos. |
| INV-2 — Compras | Enganchar el núcleo en `EntregaCompraDomain` / `FacturaCompraDomain` (alta = entrada, cancelación = reversó, bloqueo de edición de líneas tras asiento). |
| INV-3 — Ventas | Enganchar en `EntregaDomain` / `FacturaDomain` de venta (alta = salida con bloqueo de stock negativo, cancelación = reversó). |
| INV-4 — Entrada / Salida de mercancías | Dos documentos nuevos estilo SAP B1 (OIGN / OIGE): entrada sin proveedor y salida sin cliente, para conteos, mermas, saldos iniciales, consumos. Reemplaza la idea original de "pantalla de ajuste manual". |

Cada fase tiene su propio ciclo spec → plan → implementación.

### Hallazgos de SAP B1 (SBO_TEST, Service Layer, config por defecto)

Verificado creando datos de prueba (artículo `TESTINV01`, proveedor `PTEST01`, un Goods
Receipt PO y una factura de compra standalone):

- **Cantidad**: se lleva **por artículo + almacén** (`OITW.OnHand`). Recibí 10 y luego
  facturé 5 → el almacén 01 quedó con 15.
- **Valuación**: **costo promedio móvil**, a nivel **artículo** (`OITM.EvalSystem='A'`,
  `OITM.AvgPrice`). Tras 10@25 y 5@30 → costo = (250 + 150) / 15 = 26.67. Con la config
  por defecto (`ManageStockByWarehouse='tNO'`), el costo/valor **no** se desglosa por
  almacén; solo la cantidad.
- **Kardex** (`OINM`): una fila por línea de documento que mueve stock — `InQty`/`OutQty`,
  precio de línea, `CalcPrice`, `TransValue`, almacén, tipo de documento.
- **Entrega de compra** (GRPO, `TransType=20`): suma stock siempre.
- **Factura de compra standalone** (`TransType=18`): **también suma stock**. En SAP, una
  factura *basada en* un GRPO no re-suma; este sistema no tiene encadenamiento de
  documentos, así que toda `FacturaCompra` es standalone → suma.
- Stock negativo: bloqueado (`BlockStockNegativeQuantity='tYES'`). Irrelevante para
  compras (solo entra stock); relevante para INV-3 / INV-4.

### Estado del proyecto relevante

- `Articulo` ya tiene: `ArticuloInventario` (bandera de artículo de inventario),
  `CantDisponible`, `CantConfirmada`, `CantPedida` (agregados a nivel artículo, hoy sin
  mantener), `AlmacenDefecto`, `GestPorAlmacen`, `Minimo`, `Maximo`.
- `Almacen` es básico (código, nombre, dirección, `Bloqueado`).
- Los encabezados de documento (venta y compra) tienen `EstadoInv` (`'A'`/`'C'`) — bandera
  de estado de inventario, hoy sin usar.
- **No existe módulo de contabilidad** (33 tablas, ninguna de cuentas/asientos/mayor).
- No existe ninguna tabla de inventario.
- Patrón de numeración atómica ya en uso: un domain lee una entidad rastreada
  (`serie`), la modifica en memoria y deja que el `SaveChangesAsync` del `InsertarAsync`
  persista el cambio junto con el INSERT del documento — una sola transacción implícita.
  El servicio de asiento de inventario usa exactamente este patrón.

## Decisiones confirmadas con el usuario

1. **Métodos de valuación**: promedio móvil (`'P'`, default) y estándar (`'E'`). FIFO
   queda fuera.
2. **Granularidad**: cantidad por `(artículo, almacén)`; costo promedio y valor total a
   nivel **artículo** (igual que SAP con la config por defecto).
3. **Variación de costo estándar**: sin módulo contable, se **registra en el kardex** —
   columna `VariacionPrecio` en la fila de `MovimientoInventario`. El stock se valúa
   siempre al costo estándar; la diferencia `cantidad · (precioLínea − costoEstándar)`
   queda trazable para reportes. No se contabiliza (no hay dónde) ni se usa tabla aparte.
4. **Edición de documento ya asentado** (aplica en INV-2/3/4, se documenta aquí): se
   **bloquea** la edición de líneas (artículo, cantidad, almacén, precio); campos de
   cabecera inocuos (p. ej. `Comentario`) siguen editables.
5. **Creación de tablas**: DDL completo versionado en `API/sql/`, aplicado por el
   asistente a `API_DB_TEST`; luego se scaffoldean / escriben a mano las entidades EF
   contra la base ya creada.
6. **Denormalización**: cada fila de `MovimientoInventario` guarda los saldos resultantes
   (cantidad, costo promedio, valor) del artículo; y se mantiene
   `Articulo.CostoPromedio` / `ValorInventario` / `CantDisponible` como caché del total.
7. **INV-1 incluye pantalla Web** de consulta (existencias + kardex), no solo API.

## Enfoque

Un **servicio de dominio** `IInventarioAsientoService`, inyectado en los domains de
documento (en INV-2 en adelante), que se llama dentro de `InsertarAsync` usando el
**mismo `ApiDbTestContext` scoped** que el domain. Todas las mutaciones de inventario
quedan pendientes en el `ChangeTracker` y las persiste el único `SaveChangesAsync` del
caller — una sola transacción implícita, igual que la numeración.

Alternativas descartadas:

- **Eventos de dominio / interceptor de `SaveChanges`**: el código no tiene infraestructura
  de eventos; introducirla sería un patrón nuevo inconsistente con el resto (N-capas con
  llamadas explícitas). Sobre-ingeniería.
- **Lógica de valuación repetida en cada domain**: `EntregaCompra`, `FacturaCompra`,
  `Entrega`, `Factura`, y los dos documentos de mercancías → 6+ copias de matemática no
  trivial (promedio móvil + estándar + variación + upsert + kardex + sync). Exactamente el
  tipo de duplicación que la revisión final del sub-proyecto de compra marcó como riesgo.

## Componentes (API)

### 1. Modelo de datos — `API/sql/2026-08-30-inventario-nucleo.sql`

**`ExistenciaArticulo`** — fuente de verdad de la cantidad multi-almacén.

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `CodArticulo` | `nvarchar(15)` | NO | PK, FK → `Articulo.Codigo` (`fk_existencia_articulo`) |
| `CodAlmacen` | `nvarchar(8)` | NO | PK, FK → `Almacen.Codigo` (`fk_existencia_almacen`) |
| `Disponible` | `decimal(19,6)` | NO | default `0` |
| `Comprometido` | `decimal(19,6)` | NO | default `0` (lo mueve INV-3; aquí siempre 0) |
| `Pedido` | `decimal(19,6)` | NO | default `0` (lo mueve INV-2 en pedidos; aquí siempre 0) |
| `FechaActualizacion` | `datetime` | NO | default `getdate()` |
| `RowVersion` | `rowversion` | NO | control de concurrencia optimista |

PK compuesta `pk_existencia_articulo (CodArticulo, CodAlmacen)`.

**`MovimientoInventario`** — kardex, append-only (nunca `UPDATE` ni `DELETE`).

| Columna | Tipo | Nulo | Notas |
|---|---|---|---|
| `Entry` | `int identity` | NO | PK `pk_movimiento_inventario` |
| `TipoDoc` | `nvarchar(20)` | NO | `TipoObjeto` del documento origen (`'11'`/`'12'`/`'13'`, tipos de venta, y los de Entrada/Salida de mercancías en INV-4) |
| `DocEntry` | `int` | NO | `Entry` del documento origen |
| `DocLinea` | `int` | NO | `NoLinea` de la línea origen |
| `CodArticulo` | `nvarchar(15)` | NO | FK → `Articulo.Codigo` (`fk_movimiento_articulo`) |
| `CodAlmacen` | `nvarchar(8)` | NO | FK → `Almacen.Codigo` (`fk_movimiento_almacen`) |
| `Fecha` | `datetime` | NO | fecha del documento origen |
| `CantidadEntra` | `decimal(19,6)` | NO | default `0` |
| `CantidadSale` | `decimal(19,6)` | NO | default `0` |
| `PrecioUnitario` | `decimal(19,6)` | NO | precio de la línea del documento |
| `CostoUnitario` | `decimal(19,6)` | NO | costo con que se valuó el movimiento (= `PrecioUnitario` en entrada promedio; = `CostoEstandar` en estándar; = costo promedio vigente en salida promedio) |
| `ValorMovimiento` | `decimal(19,6)` | NO | `CostoUnitario · (CantidadEntra − CantidadSale)` |
| `VariacionPrecio` | `decimal(19,6)` | NO | default `0`; solo se llena en entradas de artículos con método estándar: `cantidad · (PrecioUnitario − CostoEstandar)` |
| `SaldoCantidad` | `decimal(19,6)` | NO | cantidad total del artículo (todos los almacenes) tras este movimiento |
| `SaldoCostoPromedio` | `decimal(19,6)` | NO | costo promedio del artículo tras este movimiento |
| `SaldoValor` | `decimal(19,6)` | NO | valor de inventario del artículo tras este movimiento |
| `MovReversaDe` | `int` | SÍ | `Entry` del movimiento que este revierte; `NULL` = movimiento normal. FK → `MovimientoInventario.Entry` (`fk_movimiento_reversa`) |

Índices: `ix_movimiento_articulo_fecha (CodArticulo, Fecha, Entry)`,
`ix_movimiento_origen (TipoDoc, DocEntry)`.

**`Articulo`** — columnas nuevas (todas con default para no romper filas existentes):

| Columna | Tipo | Default | Notas |
|---|---|---|---|
| `MetodoValuacion` | `nvarchar(1)` | `'P'` | `'P'` promedio móvil / `'E'` estándar. CHECK `IN ('P','E')` |
| `CostoPromedio` | `decimal(19,6)` | `0` | costo promedio móvil vigente (nivel artículo) |
| `CostoEstandar` | `decimal(19,6)` | `0` | costo estándar (lo fija el usuario; en INV-1 no hay pantalla para editarlo — se hace por SQL o queda para otra fase) |
| `ValorInventario` | `decimal(19,6)` | `0` | valor total de inventario del artículo (= `CostoPromedio · Σ Disponible`, o `CostoEstandar · Σ Disponible` en estándar) |

`Articulo.CantDisponible` (existente) se sincroniza como `Σ ExistenciaArticulo.Disponible`
del artículo. `CantConfirmada` / `CantPedida` quedan a 0 hasta INV-2/INV-3.

El script SQL crea solo estructura, **sin sembrar filas de `ExistenciaArticulo`**: cada
fila se crea bajo demanda en el primer movimiento de esa combinación
`(artículo, almacén)`. Las consultas tratan la ausencia de fila como existencia 0.

### 2. Motor de valuación — `IValuacionInventario` (`API.Domain.Core`)

Función pura, sin dependencias, sin I/O. Interfaz + implementación `ValuacionInventario`.

```csharp
public record ResultadoValuacion(
    decimal NuevoCostoPromedio,   // costo promedio del artículo tras el movimiento
    decimal CostoUnitarioMov,     // costo con que se valúa esta línea
    decimal ValorMovimiento,      // CostoUnitarioMov * cantidad con signo
    decimal VariacionPrecio);     // 0 salvo entrada con método 'E'

public interface IValuacionInventario
{
    ResultadoValuacion CalcularEntrada(
        decimal cantActual, decimal costoPromActual, decimal costoEstandar,
        string metodo, decimal cantidad, decimal precioUnitario);

    ResultadoValuacion CalcularSalida(
        decimal cantActual, decimal costoPromActual, decimal costoEstandar,
        string metodo, decimal cantidad);
}
```

- **`CalcularEntrada`, método `'P'`**:
  `NuevoCostoPromedio = (cantActual·costoPromActual + cantidad·precioUnitario) / (cantActual + cantidad)`
  (si `cantActual + cantidad == 0` → `NuevoCostoPromedio = costoPromActual`);
  `CostoUnitarioMov = precioUnitario`; `ValorMovimiento = cantidad · precioUnitario`;
  `VariacionPrecio = 0`.
- **`CalcularEntrada`, método `'E'`**:
  `NuevoCostoPromedio = costoEstandar`; `CostoUnitarioMov = costoEstandar`;
  `ValorMovimiento = cantidad · costoEstandar`;
  `VariacionPrecio = cantidad · (precioUnitario − costoEstandar)`.
- **`CalcularSalida`, método `'P'`**: `CostoUnitarioMov = costoPromActual`;
  `NuevoCostoPromedio = costoPromActual` (la salida no recalcula el promedio);
  `ValorMovimiento = −cantidad · costoPromActual`; `VariacionPrecio = 0`.
- **`CalcularSalida`, método `'E'`**: igual pero con `costoEstandar`.
- Redondeo: los cálculos internos en `decimal` sin redondeo intermedio; el consumidor
  decide la precisión de almacenamiento (las columnas son `decimal(19,6)`).

Pruebas: primera entrada (cantActual 0), entradas sucesivas con promedio, entrada con
cantidad que lleva el total a 0, método estándar con variación positiva y negativa,
salida en ambos métodos, salida que deja saldo 0.

### 3. Servicio de asiento — `IInventarioAsientoService` (`API.Domain.Core`)

```csharp
public record MovimientoRequest(
    string TipoDoc, int DocEntry, int DocLinea,
    string CodArticulo, string CodAlmacen,
    decimal Cantidad,          // > 0 entrada, < 0 salida
    decimal PrecioUnitario,
    DateTime Fecha);

public interface IInventarioAsientoService
{
    // Aplica los movimientos al ChangeTracker del contexto scoped. NO llama SaveChangesAsync.
    Task AsentarAsync(IEnumerable<MovimientoRequest> movimientos, bool permitirNegativo = false);

    // Genera los movimientos inversos de un documento ya asentado. NO llama SaveChangesAsync.
    Task RevertirAsync(string tipoDoc, int docEntry);
}
```

Dependencias (inyectadas, resueltas en el mismo scope que el domain que lo llama):
`IRepositorioGenerico<Articulo, string>`, `IRepositorioGenerico<Almacen, string>`,
`IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>`,
`IRepositorioGenerico<MovimientoInventario, int>`, `IValuacionInventario`.

**`AsentarAsync`**, por cada `MovimientoRequest`:

1. Carga el `Articulo`. Si `ArticuloInventario != "S"` → **ignora la línea** (servicios,
   no-inventario). No genera movimiento.
2. Carga (o crea, con `Disponible = 0`) la fila `ExistenciaArticulo (CodArticulo,
   CodAlmacen)`. La entidad queda rastreada por el contexto scoped.
3. `nuevaDisponible = ExistenciaArticulo.Disponible + Cantidad`. Si `< 0` y
   `!permitirNegativo` → `throw new Exception("Stock insuficiente en {almacén} para {artículo}: disponible {x}, requerido {y}.")`.
4. `cantArtActual = Articulo.CantDisponible ?? 0` (total sobre almacenes).
5. Llama `IValuacionInventario`: si `Cantidad > 0`, `CalcularEntrada(cantArtActual,
   Articulo.CostoPromedio, Articulo.CostoEstandar, Articulo.MetodoValuacion, abs(Cantidad),
   PrecioUnitario)`; si `Cantidad < 0`, `CalcularSalida(cantArtActual,
   Articulo.CostoPromedio, Articulo.CostoEstandar, Articulo.MetodoValuacion, abs(Cantidad))`
   (la salida no recibe `PrecioUnitario` — se valúa al costo vigente).
6. Muta en memoria: `ExistenciaArticulo.Disponible = nuevaDisponible`,
   `ExistenciaArticulo.FechaActualizacion = now`; `Articulo.CostoPromedio =
   resultado.NuevoCostoPromedio`, `Articulo.CantDisponible = cantArtActual + Cantidad`,
   `Articulo.ValorInventario = Articulo.CostoPromedio · Articulo.CantDisponible`
   (uniforme: con método `'E'`, `CostoPromedio` ya quedó igual a `CostoEstandar`).
7. Hace `Insertar` de una fila `MovimientoInventario` con:
   `CantidadEntra/Sale` según signo, `PrecioUnitario`, `CostoUnitario =
   resultado.CostoUnitarioMov`, `ValorMovimiento = resultado.ValorMovimiento`,
   `VariacionPrecio = resultado.VariacionPrecio`, y los saldos resultantes del artículo
   (`SaldoCantidad = Articulo.CantDisponible`, `SaldoCostoPromedio = Articulo.CostoPromedio`,
   `SaldoValor = Articulo.ValorInventario`), `MovReversaDe = null`.
8. **No** llama `SaveChangesAsync`.

**Orden**: procesa los movimientos en el orden recibido (el caller pasa las líneas del
documento en orden de `NoLinea`); el promedio móvil es sensible al orden y así queda
determinista y reproducible.

**`RevertirAsync(tipoDoc, docEntry)`**:

1. Busca todas las filas `MovimientoInventario` con `TipoDoc == tipoDoc && DocEntry ==
   docEntry && MovReversaDe == null` que **no** tengan ya una fila con
   `MovReversaDe == esa.Entry` (evita doble reversa).
2. Para cada una, arma un `MovimientoRequest` con `Cantidad` negada
   (`−(CantidadEntra − CantidadSale)`), `PrecioUnitario` = el `CostoUnitario` original
   (para que el valor revertido cuadre exactamente), y aplica el mismo flujo de
   `AsentarAsync` (pasos 1-7) con `permitirNegativo = true` (una reversa no debe
   bloquearse por negativo), fijando `MovReversaDe = Entry` del movimiento original.
3. No llama `SaveChangesAsync`.

**Concurrencia**: `ExistenciaArticulo` y `Articulo` llevan `rowversion`
(`[Timestamp]` / `IsRowVersion`). Dos asientos simultáneos sobre el mismo artículo →
`DbUpdateConcurrencyException` en el `SaveChangesAsync` del caller. El manejo del reintento
(recargar y re-asentar) queda del lado del caller/Application en INV-2+; INV-1 deja el
`rowversion` mapeado y documentado. **Punto para revisión del spec**: alternativa es
computar los deltas en SQL (`UPDATE ExistenciaArticulo SET Disponible = Disponible + @x`)
para evitar la carrera read-modify-write, a costa de salirse del patrón EF/repo genérico.

### 4. Repositorios y DI

- `ExistenciaArticuloRepositorio : RepositorioGenericoEfCore<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>`
  con `override ObtenerAsync` → `FindAsync(id.CodArticulo, id.CodAlmacen)`.
- `MovimientoInventarioRepositorio : RepositorioGenericoEfCore<MovimientoInventario, int>`.
- `Startup.cs`: registrar ambos repos genéricos, `IValuacionInventario` → `ValuacionInventario`,
  `IInventarioAsientoService` → `InventarioAsientoService`, y (para la API de consulta)
  `IExistenciaDomain`/`IExistenciaApplication`, `IMovimientoInventarioDomain`/`...Application`.

### 5. Entidades EF + `ApiDbTestContext`

Entidades **escritas a mano** (no re-scaffold del contexto completo, igual que se hizo
con los módulos `*Compra`): `ExistenciaArticulo`, `MovimientoInventario` mapeadas a las
tablas reales (nombres de PK/FK/índice como arriba) con bloques `OnModelCreating`
explícitos. Colecciones inversas `ExistenciaArticulos` / `MovimientoInventarios` en
`Articulo` y `Almacen`. La auto-referencia `MovReversaDe` se mapea como FK opcional a sí
misma (`MovReversaDeNavigation`). Las 4 columnas nuevas de `Articulo` se añaden a la
entidad `Articulo` con sus defaults mapeados vía `HasDefaultValueSql`.

### 6. API de consulta

DTOs + Domain + Application + Controller siguiendo el patrón del proyecto
(`Respuesta<T>`, AutoMapper):

- `ExistenciaController` (`api/Existencia`):
  - `GET api/Existencia` — todas las existencias (opcional `?articulo=`, `?almacen=`).
  - `GET api/Existencia/{codArticulo}/{codAlmacen}` — una; ausencia → `Disponible = 0`.
  - `GET api/Existencia/PorArticulo/{codArticulo}` — existencias del artículo en todos los
    almacenes + total.
- `MovimientoInventarioController` (`api/MovimientoInventario`):
  - `GET api/MovimientoInventario/PorArticulo/{codArticulo}` — kardex, opcional
    `?almacen=&desde=&hasta=`, ordenado por `Fecha, Entry`.

Todos `[Authorize]`, solo lectura (sin POST/PUT/DELETE en INV-1 — el stock solo cambia por
el servicio de asiento, que aún no tiene enganche).

## Componentes (Web)

- `Web.ApiClient`: DTOs `Existencia*`, `MovimientoInventario*`; `IExistenciaApiClient` /
  `ExistenciaApiClient`, `IMovimientoInventarioApiClient` / `...ApiClient`; registro en
  `Program.cs`.
- **Submenú "Inventario"** nuevo en `_Layout.cshtml` (paralelo a "Ventas"/"Compras"), con
  un enlace **"Existencias"**.
- `ExistenciasController` (Web): `Index`, `ObtenerTodos` (existencias, con filtro
  opcional por artículo), `BuscarArticulos` (autocompletado, reusa el endpoint existente),
  `Kardex(codArticulo)` (devuelve los movimientos del artículo).
- Vistas `Views/Existencias/Index.cshtml` — tabla de existencias por artículo/almacén con
  buscador de artículo con autocompletado (`App.autocompletar` de `site.js`); al hacer
  clic en una fila, panel/modal con el **kardex** de ese artículo (movimientos con
  columnas de entra/sale, precio, costo, valor y saldos corridos).
- `wwwroot/js/existencias.js`.

## Pruebas

- `ValuacionInventarioTests` (dominio, xUnit, sin mocks — función pura): todos los casos
  del §2.
- `InventarioAsientoServiceTests` (dominio, Moq sobre los repos genéricos +
  `IValuacionInventario` real): primera entrada crea `ExistenciaArticulo` y fila de
  kardex con saldos correctos; entradas sucesivas acumulan promedio; artículo
  no-inventario se ignora; salida que dejaría negativo lanza (y no lanza con
  `permitirNegativo`); `RevertirAsync` genera los inversos y no duplica si ya revertido;
  el servicio **no** llama `SaveChangesAsync`.
- `ExistenciaControllerTests`, `MovimientoInventarioControllerTests` (patrón de los demás
  controllers, mockeando la Application).
- Verificación: `dotnet build` de ambas soluciones sin errores; `dotnet test` de la suite
  completa de la API en verde; verificación manual en el navegador de la pantalla
  "Existencias" (con datos sembrados por SQL, ya que INV-1 no tiene enganche que genere
  movimientos).

## Fuera de alcance de INV-1 (explícito)

- Enganche a cualquier documento (INV-2/3/4).
- Stock negativo real en salidas (el servicio ya lo soporta vía `permitirNegativo`, pero
  ningún caller lo ejercita todavía).
- Pantalla para editar `MetodoValuacion` / `CostoEstandar` del artículo (se hace por SQL
  en INV-1; puede añadirse a la pantalla de Artículos en otra fase).
- Traslados entre almacenes; documentos Entrada / Salida de mercancías (INV-4).
- Reserva de stock (`Comprometido` / `Pedido` quedan en 0 y sin lógica).
- Integración contable de la `VariacionPrecio` (no hay módulo contable).
- Manejo de reintento por `DbUpdateConcurrencyException` (INV-2+ lo resuelve en su capa
  Application).
