# Reportería dinámica — fase 4 (catálogo de reportes) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar al motor de reportes existente 20 reportes de negocio en las cuatro familias (Ventas y utilidad, Inventario, Cobros y pagos, Compras), para responder preguntas como «¿cuál es el artículo más vendido?» y «¿cuánto gané en tal mes?».

**Architecture:** Cada reporte es una clase `IReporte` en `API.Domain.Core/Reportes/Definiciones/` (definición de filtros y columnas + consulta EF/LINQ sin SQL libre) registrada en `Startup`. Los reportes de ventas comparten una consulta de líneas de venta con costo (`ConsultaLineasVenta`, en `Domain.Core/Reportes/Compartido`). No cambia el motor, el controlador, los exportadores ni la Web: la pantalla genérica ya arma todo desde la definición.

**Tech Stack:** .NET 7 / EF Core 7 / xUnit 2.4 + Moq 4.20 (el patrón de pruebas de `ExistenciasValorReporteTests`: repos `Mock<IRepositorioGenerico<…>>` con `.AsAsyncQueryable()`).

**Spec:** `docs/superpowers/specs/2026-11-08-reporteria-dinamica-design.md` (§4 catálogo, §2 motor) y el plan previo `docs/superpowers/plans/2026-11-08-reporteria-dinamica-implementation.md` (contratos y reglas). Léelos antes de empezar. Este plan **amplía** el catálogo del spec §4 (ver Rulings 1 y 2).

## Global Constraints

- **Repo y rama:** API `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`, sin worktree. La Web no se toca (pantalla genérica), salvo lo que indique la Tarea 12.
- **Git:** commits con rutas explícitas (`git add <archivo>`), **nunca** `git add -A` ni `git add .`. **Nunca** commitear `API.Service.WebApi/appsettings.json` ni `sql/crear-api-db-completo*.sql` ni `sql/temporal-*.sql`. **No hacer `push`.** Mensajes `tipo(modulo): descripcion` en español sin tildes. Todo commit termina con la línea `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Conserva el fin de línea de cada archivo (autocrlf activo).
- **Capas:** `Domain.Core` puede usar `Domain.Interface` y `Infraestructure.Interface`. Los servicios existentes que se reutilizan (`ISaldoFacturaService`, `ISaldoFacturaCompraService`, `IEstadoCuentaService`, `IAntiguedadService`, `StockAlmacenCalculadora`, `UnidadesLinea`) no se modifican.
- **Reglas de reporte (spec §4):** se excluyen documentos cancelados (`Cancelado = 'S'`), salvo en los reportes «por estado»; las notas de crédito restan (importe con signo negativo) y las de débito suman; todo reporte con fecha lleva filtro de rango (`RangoFechas`). Los reportes de monto traen la columna `moneda` (`VisiblePorDefecto: false`) para que el motor aplique la regla multimoneda (no suma monedas distintas).
- **Sin SQL libre:** consultas EF/LINQ sobre `IRepositorioGenerico<…>.ObtenerTodoAsync()`. Filtros, columnas y campos de orden o agrupación solo los declarados.
- **Límite:** el parámetro `limite` de `EjecutarAsync` se respeta en toda definición de detalle (`.Take(limite)`). Los reportes agregados (una fila por artículo, mes o cliente) leen todas las líneas del período con tope duro `ConsultaLineasVenta.MaxLineas = 200000`; si se supera lanzan `ReporteInvalidoException("El período tiene demasiadas líneas para este reporte. Acorta el rango de fechas.")`.
- **Códigos de reporte** (únicos; son los de este plan, no los cambies): `ventas-por-articulo`, `articulos-mas-vendidos`, `utilidad-por-periodo`, `ventas-por-cliente`, `articulos-sin-ventas`, `notas-credito-debito`, `documentos-venta-estado`, `stock-bajo-minimo`, `kardex-articulo`, `movimientos-inventario`, `facturas-por-cobrar`, `facturas-por-pagar`, `pagos-recibidos`, `pagos-efectuados`, `estado-cuenta-socio`, `antiguedad-saldos`, `compras-por-documento`, `compras-por-articulo`, `documentos-compra-estado`, `historial-precios-compra`.
- **Familia y módulo:** Ventas → `Familia: "Ventas"`, `Modulo: "ReportesVentas"`; Inventario → `"Inventario"` / `"ReportesInventario"`; Cobros y pagos → `"Cobros y pagos"` / `"ReportesCobrosPagos"`; Compras → `"Compras"` / `"ReportesCompras"`. (Verifica que `ExistenciasValorReporte` use exactamente `"Inventario"` y `VentasPorDocumentoReporte` `"Ventas"`.)
- **Mensajes de negocio:** literales en español; las pruebas los verifican. Los de este plan son definitivos.
- **Pruebas:** TDD (prueba primero y verla fallar). Cada reporte tiene su clase de pruebas en `API.Service.WebApi.Tests/Domain/<Nombre>ReporteTests.cs` con, como mínimo: la prueba de definición (código, módulo, familia, campos de filtros y columnas, `Totaliza`/`Agrupable` relevantes), una prueba por regla de cálculo, una por filtro, una por exclusión de cancelados y una de `limite`. Cada tarea cierra con build sin errores y suite completa en verde. **Línea base al empezar: 2930** (corre la suite y anótala). Si una API/Web en ejecución bloquea `bin`, compila/prueba con `-p:BaseOutputPath="$env:TEMP\gescom-build-reporteria\"` (con la barra final).
- **SQL real:** las pruebas de humo (Tareas 5 y 13) **solo** en bases temporales `API_DB_EVID<n>` (scripts `evid-up.ps1` / `evid-down.ps1` en `<scratchpad>`), jamás contra `API_DB_TEST` ni una base del usuario. La contraseña de `sa` y la clave JWT nunca se imprimen, copian ni commitean (si el `sa` de `appsettings.json` no autentica, detente y pregunta). Una consulta que pasa en xUnit (LINQ a objetos) puede **no traducirse** a SQL: la prueba de humo es obligatoria.
- **`<scratchpad>`** = `C:\Users\migue\AppData\Local\Temp\claude\9f8881082ead4f04\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad` (los scripts y evidencias de la fase anterior están ahí; nada se commitea).
- **No añadas paquetes, tipos de columna ni cambios al motor.** Si un reporte necesita algo que el motor no ofrece, elige la alternativa más simple dentro de lo existente y regístrala en tu reporte (Rulings).

## Rulings (decisiones tomadas al planificar)

1. **Catálogo ampliado a 20 reportes.** El spec §4 lista 16 pendientes; se agregan por pedido del usuario *Artículos más vendidos*, *Utilidad por período*, *Ventas por cliente* y *Artículos sin ventas* (reportes **agregados**: una fila por artículo, período o cliente). «Pagos recibidos» y «Pagos efectuados» son dos reportes (como en el spec).
2. **Utilidad (decisión del usuario).** `Utilidad = Venta neta − Costo`, con las notas de crédito restando de la venta del período. **IVA nunca entra.** Documentos cancelados no cuentan.
3. **Venta neta de una línea** = `TotalLinea − Impuesto` (neto con descuento, sin IVA). Antes de usarlo, verifica en `FacturaDomain`/el calculador de líneas que `TotalLinea = Cantidad × Precio × (1 − PrctjeDesc/100) + Impuesto`; si la fórmula real difiere, usa `Cantidad × Precio × (1 − PrctjeDesc/100)` y regístralo en tu reporte.
4. **Costo de una línea** = `CantidadBase × costoUnitarioNeto`, donde `costoUnitarioNeto = −ΣValorMovimiento / (ΣCantidadSale − ΣCantidadEntra)` sobre **todos** los `MovimientoInventario` de la «línea de origen» (las reversas por cancelación netean solas porque entran con signo opuesto). Si el denominador es ≤ 0 (sin movimientos: servicios o no inventariables; o totalmente revertido) el costo es 0. La línea de origen es:
   - línea con `BaseTipo = 5` → movimientos `TipoDoc = "5"`, `DocEntry = BaseEntry`, `DocLinea = BaseLinea` (la Entrega movió el stock);
   - `BaseTipo = 70` → `TipoDoc = "70"`, `DocEntry = BaseEntry`, `DocLinea = BaseLinea` (la orden de servicio);
   - cualquier otra (directa, o con base Cotización/Pedido) → `TipoDoc = "6"`, `DocEntry = Entry` de la factura, `DocLinea = NoLinea`.
   Verifica en `FacturaDomain.CrearAsync` (líneas 140-160) y `DocumentoBaseService` que `BaseLinea` es el `NoLinea` de la línea del documento base; si no lo es, detente y repórtalo.
5. **Cantidades en unidad base:** `CantidadBase ?? Cantidad`. Los precios netos se muestran por unidad base (`VentaNeta / CantidadBase`), así artículos con presentaciones se comparan bien.
6. **Fecha de la venta** = `FechaDoc` de la factura (no la del movimiento). Las notas de débito (`DocSubType = "ND"`) con líneas de artículo cuentan como venta, igual que en «Ventas por documento».
7. **Notas de crédito** no tienen artículo: restan **solo** en los reportes por período y por cliente (`TotalDoc − TotalImp`, es decir, neto sin IVA); no entran en reportes por artículo.
8. **Estado de un documento** (reportes «por estado»): `Cancelado = "S"` → «Cancelado»; si no, `EstadoDoc = "C"` → «Cerrado»; si no → «Abierto».
9. **Saldos de cobros y pagos son los actuales.** «Fecha de corte» solo define qué facturas se incluyen (`FechaDoc ≤ corte`) y los días de atraso; el saldo lo calcula `ISaldoFacturaService`/`ISaldoFacturaCompraService` al día de hoy. «Antigüedad de saldos» siempre es a hoy (el servicio no admite fecha): **no lleva** filtro de fecha de corte (diverge del spec §4, que lo mencionaba).
10. **Pagos:** una fila por medio de pago (`PagoRecibidoMedio`/`PagoEfectuadoMedio`); `facturasAplicadas` es texto con los números de documento aplicados separados por coma. `PagoRecibido` no tiene moneda: esos reportes no llevan columna `moneda`.
11. **«Mostrar los primeros N»:** filtro `Numero`; debe ser entero > 0, si no `ReporteInvalidoException("El filtro 'Mostrar los primeros' debe ser un entero mayor que cero.")`. Vacío = todos.
12. **Sin columna de margen %** (el motor no totaliza porcentajes correctamente). Se deja anotado como pendiente.

## Estructura de archivos (todo en `API/`)

| Archivo | Responsabilidad |
|---|---|
| `API.Domain.Core/Reportes/Compartido/ConsultaLineasVenta.cs` | Líneas de venta con venta neta y costo (Tarea 1) |
| `API.Domain.Core/Reportes/Definiciones/*Reporte.cs` | Un archivo por reporte (los 20 códigos) |
| `API.Service.WebApi/Startup.cs` | `services.AddTransient<IReporte, X>()` junto a los dos existentes (línea ~322) |
| `API.Service.WebApi.Tests/Domain/*ReporteTests.cs`, `ConsultaLineasVentaTests.cs` | Pruebas |
| `API.Service.WebApi.Tests/Domain/CatalogoReportesCompletoTests.cs` | Prueba de coherencia del catálogo (Tarea 13) |
| `AGENTS.md`, `docs/traspaso-2026-11-07.md` | Documentación (Tarea 13) |

---

### Task 1: Consulta compartida de líneas de venta con costo

**Files:**
- Create: `API.Domain.Core/Reportes/Compartido/ConsultaLineasVenta.cs`
- Test: `API.Service.WebApi.Tests/Domain/ConsultaLineasVentaTests.cs`

**Interfaces:**
- Produces (lo usan las Tareas 2-4):

```csharp
namespace API.Domain.Core.Reportes.Compartido
{
    public sealed record LineaVenta(
        int DocEntry, int NoLinea, DateTime? Fecha, int Serie, int NumDoc, string Tipo,   // Tipo: "Factura" | "Nota de débito"
        string? CodigoSn, string? NombreSn, string? Moneda,
        string CodArticulo, string? NombreArticulo, string? Grupo, string? CodAlmacen,
        decimal CantidadBase, decimal VentaNeta, decimal PrctjeDesc, decimal Costo);

    public sealed record FiltroLineasVenta(
        DateTime? Desde, DateTime? HastaExclusiva, string? Cliente, string? Articulo, string? Almacen, string? Grupo);

    public sealed class ConsultaLineasVenta
    {
        public const int MaxLineas = 200000;
        public ConsultaLineasVenta(
            IRepositorioGenerico<Factura, int> facturas,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> detalles,
            IRepositorioGenerico<MovimientoInventario, int> movimientos);
        public Task<IReadOnlyList<LineaVenta>> ObtenerAsync(FiltroLineasVenta filtro);   // ordenadas por Fecha, DocEntry, NoLinea; lanza ReporteInvalidoException si > MaxLineas
    }
}
```

- Consumes: entidades `Factura`, `FacturaDetalle`, `MovimientoInventario`, `Articulo`, `GrupoArticulo` (lee sus propiedades en `API.Domain.Entity/Models`); `ReporteInvalidoException`.

**Qué hace.** Aplica los Rulings 2-6.

1. Facturas válidas = `Cancelado != "S"` y `DocSubType` `"--"` o `"ND"`, con `FechaDoc` en el rango (`>= Desde`, `< HastaExclusiva`) y `CodigoSn == Cliente` si se pidió.
2. Líneas = `FacturaDetalle` cuya `Entry` está en las facturas válidas (subconsulta `Contains`, **no** una lista materializada), con `CodArticulo != null`; filtros opcionales de artículo, almacén y grupo (el grupo se filtra por `CodArticuloNavigation.CodigoGrupoNavigation.Nombre.Contains(grupo)`, como `ExistenciasValorReporte`). Proyecta a un tipo anónimo con solo lo necesario (incluye `Factura` por join o por diccionario de facturas leído con la misma subconsulta).
3. Si hay más de `MaxLineas` líneas (usar `Take(MaxLineas + 1)`), lanza la excepción del Global Constraints.
4. Movimientos: **tres** consultas, una por tipo de origen, cada una con subconsulta sobre las líneas filtradas (no `Contains` de listas grandes materializadas):
   - `TipoDoc == "6"` y `DocEntry` en las `Entry` de líneas cuyo `BaseTipo` no es 5 ni 70;
   - `TipoDoc == "5"` y `DocEntry` en los `BaseEntry` de líneas con `BaseTipo == 5`;
   - `TipoDoc == "70"` y `DocEntry` en los `BaseEntry` de líneas con `BaseTipo == 70`.
   Se agrupan en memoria por `(TipoDoc, DocEntry, DocLinea)` sumando `ValorMovimiento`, `CantidadSale`, `CantidadEntra`.
5. `Costo = CantidadBase × (−ΣValor / (ΣSale − ΣEntra))` si `ΣSale − ΣEntra > 0`, si no 0. `VentaNeta = (TotalLinea ?? 0) − (Impuesto ?? 0)` (Ruling 3). `CantidadBase = CantidadBase ?? Cantidad ?? 0`. Redondea `VentaNeta` y `Costo` a 6 decimales.

- [ ] **Step 1: Escribe las pruebas (fallan).** En `ConsultaLineasVentaTests`, con `Mock<IRepositorioGenerico<…>>` y `AsAsyncQueryable()`, cubre cada caso con números concretos:
  - factura directa: línea `Cantidad 2, CantidadBase 2, TotalLinea 112, Impuesto 12`; movimiento `("6", entry, línea)` `CantidadSale 2, ValorMovimiento −60` → `VentaNeta 100`, `Costo 60`.
  - línea con `BaseTipo 5`: el costo sale del movimiento `("5", BaseEntry, BaseLinea)`; **ignora** movimientos `"6"` con la misma entry.
  - línea con `BaseTipo 70`: igual con `"70"`.
  - facturación parcial de una entrega: movimiento de la entrega `CantidadSale 10, ValorMovimiento −100`; la línea factura 4 → `Costo 40`.
  - reversa: movimiento original `CantidadSale 2, Valor −60` + reversa `CantidadEntra 2, Valor +60` → denominador 0 → `Costo 0`.
  - sin movimientos (servicio) → `Costo 0`, `VentaNeta` intacta.
  - factura cancelada, nota de crédito inexistente aquí, `DocSubType` `"ND"` incluida con `Tipo "Nota de débito"`, líneas sin artículo excluidas.
  - filtros: fechas (límite superior exclusivo), cliente, artículo, almacén, grupo.
  - exceso: con 200,001 líneas lanza `ReporteInvalidoException` con el mensaje del Global Constraints (genera las líneas con un bucle).
- [ ] **Step 2:** `dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~ConsultaLineasVentaTests" --nologo -v q` → falla (no compila / no existe).
- [ ] **Step 3: Implementa** `ConsultaLineasVenta`.
- [ ] **Step 4:** el mismo comando → todo verde. Luego build de `API.sln` (0 errores) y suite completa (línea base + las nuevas).
- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/Reportes/Compartido/ConsultaLineasVenta.cs API.Service.WebApi.Tests/Domain/ConsultaLineasVentaTests.cs
git commit -m "feat(reporteria): consulta compartida de lineas de venta con costo"
```

---

### Task 2: Ventas por artículo, Artículos más vendidos, Artículos sin ventas

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{VentasPorArticuloReporte,ArticulosMasVendidosReporte,ArticulosSinVentasReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs` (registro DI)
- Test: `API.Service.WebApi.Tests/Domain/{VentasPorArticulo,ArticulosMasVendidos,ArticulosSinVentas}ReporteTests.cs`

**Interfaces:**
- Consumes: `ConsultaLineasVenta`, `FiltroLineasVenta`, `LineaVenta` (Tarea 1); `IRepositorioGenerico<Articulo, string>` (solo `ArticulosSinVentas`).
- Produces: tres `IReporte` registrados.

**Reporte `ventas-por-articulo` — «Ventas por artículo»** (detalle por línea; descripción: «Cada línea vendida con su venta neta, costo y utilidad.»)
- Filtros: `fechas` RangoFechas; `cliente` Socio SubTipo "C"; `articulo` Articulo; `grupo` Texto («Grupo de artículos»); `almacen` Almacen.
- Columnas (campo, título, tipo, flags): `fecha` Fecha agrupable; `tipo` Texto agrupable; `serie` Entero (oculta por defecto); `numDoc` Entero «No.»; `codigoSn` Texto agrupable «Código cliente» (oculta); `nombreSn` Texto agrupable «Cliente»; `articulo` Texto agrupable «Artículo»; `nombreArticulo` Texto «Nombre»; `grupo` Texto agrupable «Grupo»; `almacen` Texto agrupable «Almacén»; `moneda` Texto agrupable (oculta); `cantidad` Cantidad Suma «Cantidad (base)»; `precioNeto` Dinero «Precio neto» (= `VentaNeta / CantidadBase`, 0 si cantidad 0; sin totalización); `ventaNeta` Dinero Suma «Venta neta»; `costo` Dinero Suma «Costo»; `utilidad` Dinero Suma «Utilidad» (= venta neta − costo).
- Una fila por línea, orden natural fecha, documento, línea; `.Take(limite)`.

**Reporte `articulos-mas-vendidos` — «Artículos más vendidos»** (agregado; descripción: «Ranking de artículos por cantidad vendida, venta neta o utilidad en el período.»)
- Filtros: `fechas` RangoFechas **obligatorio**; `cliente` Socio "C"; `grupo` Texto; `almacen` Almacen; `medirPor` Lista `cantidad`/«Cantidad vendida», `venta`/«Venta neta», `utilidad`/«Utilidad», `ValorPorDefecto "cantidad"`, `Obligatorio false`; `primeros` Numero «Mostrar los primeros» (Ruling 11).
- Columnas: `posicion` Entero «#»; `articulo` Texto «Artículo»; `nombreArticulo` Texto «Nombre»; `grupo` Texto agrupable «Grupo»; `moneda` Texto (oculta); `cantidad` Cantidad Suma; `ventaNeta` Dinero Suma; `costo` Dinero Suma; `utilidad` Dinero Suma; `documentos` Entero Suma «Facturas» (número de documentos distintos que incluyen el artículo).
- Una fila por (`articulo`, `moneda`) (la clave incluye moneda para no mezclar monedas). Se ordena descendente por la medida elegida (`cantidad`, `ventaNeta` o `utilidad`), desempate por código de artículo ascendente, se aplica `primeros` y se asigna `posicion` 1…n **después** de recortar. `.Take(limite)`.
- Pruebas obligatorias: ranking por cada medida con datos donde el orden **cambia** según la medida; `primeros`; `primeros` inválido (0, −1, 2.5) lanza la excepción del Ruling 11; agrega líneas del mismo artículo en varias facturas; dos monedas → filas separadas; `documentos` cuenta facturas distintas.

**Reporte `articulos-sin-ventas` — «Artículos sin ventas»** (descripción: «Artículos activos de venta que no se vendieron en el período.»)
- Filtros: `fechas` RangoFechas **obligatorio**; `grupo` Texto.
- Fuente: `Articulo` con `Activo == "S"` y `ArticuloVenta == "S"` (verifica los valores reales en `ArticuloDomain`) menos los que aparecen en `ConsultaLineasVenta` del período (sin otros filtros).
- Columnas: `articulo` Texto «Artículo»; `nombre` Texto; `grupo` Texto agrupable; `existencias` Cantidad Suma «En stock» (`CantDisponible ?? 0`); `costoUnitario` Dinero «Costo promedio» (`CostoPromedio`); `valorInventario` Dinero Suma «Valor del inventario» (`ValorInventario`). Orden: `valorInventario` descendente, luego código.
- Pruebas: excluye vendidos, excluye inactivos y no vendibles, filtro de grupo, `limite`.

- [ ] **Step 1:** Escribe las pruebas de los tres (fallan). Para los dos primeros mockea los tres repos de `ConsultaLineasVenta` (construye la consulta real con los mocks); es la cadena completa.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~VentasPorArticuloReporteTests|FullyQualifiedName~ArticulosMasVendidosReporteTests|FullyQualifiedName~ArticulosSinVentasReporteTests"` → fallan.
- [ ] **Step 3:** Implementa los tres reportes y regístralos en `Startup`:

```csharp
services.AddTransient<ConsultaLineasVenta>();
services.AddTransient<IReporte, VentasPorArticuloReporte>();
services.AddTransient<IReporte, ArticulosMasVendidosReporte>();
services.AddTransient<IReporte, ArticulosSinVentasReporte>();
```

- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit** (rutas explícitas de los 3 reportes, sus 3 pruebas y `Startup.cs`): `feat(reporteria): ventas por articulo, mas vendidos y sin ventas`.

---

### Task 3: Utilidad por período y Ventas por cliente

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{UtilidadPorPeriodoReporte,VentasPorClienteReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{UtilidadPorPeriodo,VentasPorCliente}ReporteTests.cs`

**Interfaces:**
- Consumes: `ConsultaLineasVenta` (Tarea 1); `IRepositorioGenerico<NotaCredito, int>`.
- Produces: dos `IReporte`.

**Reporte `utilidad-por-periodo` — «Utilidad por período»** (descripción: «Venta neta, costo y ganancia por mes, año o día. Las notas de crédito restan de la venta.»)
- Filtros: `fechas` RangoFechas **obligatorio**; `periodo` Lista `MES`/«Mes», `ANIO`/«Año», `DIA`/«Día», `ValorPorDefecto "MES"`; `cliente` Socio "C".
- Columnas: `periodo` Texto agrupable «Período» (`yyyy-MM`, `yyyy` o `yyyy-MM-dd`; ordena bien como texto); `moneda` Texto agrupable (oculta); `ventas` Dinero Suma «Ventas netas» (líneas de factura y nota de débito); `notasCredito` Dinero Suma «Notas de crédito» (**positivo**, es lo que se resta); `ventaNeta` Dinero Suma «Venta neta total» (= ventas − notas de crédito); `costo` Dinero Suma; `utilidad` Dinero Suma «Utilidad» (= venta neta total − costo).
- Una fila por (`periodo`, `moneda`), orden por período ascendente. Las notas de crédito = `NotaCredito` con `Cancelado != "S"`, mismo rango y cliente, importe `(TotalDoc ?? 0) − (TotalImp ?? 0)` en el período de su `FechaDoc`. Un período que solo tiene notas de crédito aparece con ventas 0 y utilidad negativa. `.Take(limite)`.
- Pruebas obligatorias: ejemplo numérico completo (2 meses, venta 1,000 + 500, costo 600 + 300, una NC de 100 en el 2.º mes ⇒ utilidades 400 y 100); agrupación por año y por día; NC sin ventas en el mes; NC cancelada excluida; dos monedas; filtro de cliente aplica a ventas **y** a NC; el IVA no altera nada (mismo resultado con `Impuesto` distinto).

**Reporte `ventas-por-cliente` — «Ventas por cliente»** (descripción: «Venta neta, costo y utilidad por cliente; las notas de crédito restan.»)
- Filtros: `fechas` RangoFechas **obligatorio**; `cliente` Socio "C"; `medirPor` Lista `venta`/«Venta neta total», `utilidad`/«Utilidad», `ValorPorDefecto "venta"`; `primeros` Numero (Ruling 11).
- Columnas: `posicion` Entero «#»; `codigoSn` Texto «Código»; `nombreSn` Texto «Cliente»; `moneda` Texto (oculta); `ventas` Dinero Suma; `notasCredito` Dinero Suma; `ventaNeta` Dinero Suma «Venta neta total»; `costo` Dinero Suma; `utilidad` Dinero Suma; `documentos` Entero Suma «Facturas».
- Una fila por (`cliente`, `moneda`); orden descendente por la medida, desempate por código; `primeros` y `posicion` como en `articulos-mas-vendidos`. Clientes con solo notas de crédito aparecen (ventas 0). `NombreSn` sale de la propia factura/nota.
- Pruebas: ranking por venta y por utilidad con orden distinto, NC resta, `primeros`, NC cancelada excluida, dos monedas.

- [ ] **Step 1:** Pruebas (fallan). **Step 2:** comando de filtro → fallan. **Step 3:** Implementa y registra (`AddTransient<IReporte, UtilidadPorPeriodoReporte>()`, `…VentasPorClienteReporte>()`). **Step 4:** verde + build + suite. 
- [ ] **Step 5: Commit** `feat(reporteria): utilidad por periodo y ventas por cliente`.

---

### Task 4: Notas de crédito y débito emitidas; Documentos de venta por estado

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{NotasCreditoDebitoReporte,DocumentosVentaEstadoReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{NotasCreditoDebito,DocumentosVentaEstado}ReporteTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<NotaCredito,int>`, `IRepositorioGenerico<Factura,int>`; para el segundo, además `Cotizacion`, `Pedido`, `Entrega` (todos con clave `int`; verifica el tipo de clave en los dominios).

**Reporte `notas-credito-debito` — «Notas de crédito y débito emitidas»** (descripción: «Notas de crédito (restan) y de débito (suman), con la factura a la que se refieren.»)
- Filtros: `fechas` RangoFechas; `cliente` Socio "C"; `tipo` Lista `NC`/«Nota de crédito», `ND`/«Nota de débito».
- Columnas: `fecha` Fecha agrupable; `tipo` Texto agrupable; `serie` Entero (oculta); `numDoc` Entero «No.»; `codigoSn` (oculta), `nombreSn` «Cliente» agrupable; `facturaBase` Entero «Factura base» (`NumDoc` de la factura referenciada por `BaseEntry` cuando `BaseTipo` es la factura (6); vacío si no hay base; resuélvelo con **una** consulta de facturas por los `BaseEntry` recolectados, no una por fila); `moneda`; `total` Dinero Suma (**NC con signo negativo**).
- Fuente: `NotaCredito` y `Factura` con `DocSubType == "ND"`, sin cancelados. Comprueba en `NotaDebitoDomain` qué guarda la ND en `BaseTipo/BaseEntry` y úsalo para `facturaBase`.
- Pruebas: signos, tipo, `facturaBase` de NC y de ND, sin base, cancelados, filtros, `limite`.

**Reporte `documentos-venta-estado` — «Documentos de venta por estado»** (descripción: «Cotizaciones, pedidos, entregas y facturas con su estado (abierto, cerrado o cancelado).»)
- Filtros: `fechas` RangoFechas; `cliente` Socio "C"; `tipo` Lista `COT`/«Cotización», `PED`/«Pedido», `ENT`/«Entrega», `FAC`/«Factura»; `estado` Lista `ABIERTO`/«Abierto», `CERRADO`/«Cerrado», `CANCELADO`/«Cancelado».
- Columnas: `tipo` Texto agrupable; `fecha` Fecha agrupable; `numDoc` Entero «No.»; `codigoSn` (oculta), `nombreSn` «Cliente» agrupable; `estado` Texto agrupable (Ruling 8); `moneda`; `total` Dinero Suma.
- **Incluye cancelados** (es el propósito del reporte). Las facturas incluyen `DocSubType` `"--"` y `"ND"` (tipo «Factura»). Si el filtro `tipo` está vacío consulta las cuatro fuentes y limita al final (`OrderBy fecha, tipo, numDoc` y `.Take(limite)`).
- Pruebas: estado por cada combinación (`Cancelado "S"` gana sobre `EstadoDoc "C"`), cada tipo, filtros, `limite`, que el filtro `estado` se aplica también a cancelados.

- [ ] **Steps 1-4** como en la Tarea 3 (pruebas → fallan → implementa y registra → verde + build + suite).
- [ ] **Step 5: Commit** `feat(reporteria): notas de credito y debito y documentos de venta por estado`.

---

### Task 5: Prueba de humo con SQL real — familia Ventas

**Files:** solo `<scratchpad>` (nada se commitea salvo el hallazgo documentado en el ledger).

- [ ] **Step 1:** Crea la base temporal `API_DB_EVID<n>` con `<scratchpad>\evid-up.ps1` (script completo + `sql/2026-11-08-reporteria.sql` ya incluido). Lee primero los scripts de humo de la fase anterior en `<scratchpad>` (`smoke*.ps1`) y la evidencia `evid-reporteria-api-2026-11-09.txt` para reutilizar su forma de arrancar la API contra esa base (variables de entorno `ConnectionStrings__API_DB` y `Jwt__Key`; **nunca** imprimas ni copies secretos) y de iniciar sesión como `admin`.
- [ ] **Step 2: Datos.** Crea por la propia API (o, solo si algo no se puede crear por API en 20 minutos, por `INSERT` en la base temporal) este escenario, con números conocidos:
  1. Artículo inventariable `ART-A` con entrada de mercancía de 100 unidades a costo 10 (costo promedio 10) y artículo `ART-B` (50 a costo 20).
  2. **Factura directa** de 5 `ART-A` a 25 neto con 10% de descuento (venta neta 112.50, costo 50).
  3. **Entrega → Factura** de 4 `ART-B` a 40 (venta 160, costo 80) y una **facturación parcial** de otra entrega de 10 `ART-A` facturando 6.
  4. **Orden de servicio** con un insumo (`BaseTipo 70`) facturada (si crear la orden por API no es práctico, inserta directamente el `MovimientoInventario` `("70", …)` y la `FacturaDetalle` con `BaseTipo 70` en la base temporal; solo se prueba la consulta del reporte).
  5. Una **factura cancelada** (no debe contar) y una **nota de crédito** de 100 neto en el mismo mes.
- [ ] **Step 3: Ejecuta** vía `POST api/Reporte/<codigo>/ejecutar` los 7 reportes de ventas y verifica contra los números calculados a mano: `articulos-mas-vendidos` (las tres medidas y `primeros`), `utilidad-por-periodo` (mes, año; la NC resta), `ventas-por-cliente`, `ventas-por-articulo` (costo y utilidad por línea de los tres orígenes), `articulos-sin-ventas`, `notas-credito-debito`, `documentos-venta-estado`. Prueba también `exportar?formato=xlsx|pdf|csv` de uno. Confirma **0 errores 500** y que ninguna consulta cae a evaluación en cliente que falle.
- [ ] **Step 4:** Si algo no se traduce a SQL o un número no cuadra, corrígelo en el reporte (con su prueba xUnit de regresión), vuelve a correr y commitea `fix(reporteria): <qué>`. Guarda la evidencia en `<scratchpad>\evid-reporteria-catalogo-ventas-2026-11-14.txt`. **Deja la base temporal viva** (las Tareas 8-12 la reutilizan; se elimina en la Tarea 13).

---

### Task 6: Inventario — Stock bajo mínimo o sobre máximo, Kardex, Movimientos

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{StockBajoMinimoReporte,KardexArticuloReporte,MovimientosInventarioReporte}.cs`; helper `API.Domain.Core/Reportes/Compartido/TiposDocumentoInventario.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{StockBajoMinimo,KardexArticulo,MovimientosInventario}ReporteTests.cs`, `TiposDocumentoInventarioTests.cs`

**Interfaces:**
- Produces: `TiposDocumentoInventario.Nombre(string tipoDoc): string` con: `"5"` → «Entrega», `"6"` → «Factura / Nota de débito», `"12"` → «Entrega de compra», `"13"` → «Factura de compra / Nota de débito de compra», `"15"` → «Nota de crédito de compra», `"59"` → «Entrada de mercancía», `"60"` → «Salida de mercancía», `"70"` → «Orden de servicio», otro → `"Documento " + tipoDoc`. Verifica los códigos que realmente generan movimientos (`grep AsentarAsync` en `Domain.Core`) y añade los que falten.
- Consumes: `IRepositorioGenerico<ArticuloAlmacen,(string,string)>`, `ExistenciaArticulo`, `MovimientoInventario` (clave `int`); `StockAlmacenCalculadora`.

**Reporte `stock-bajo-minimo` — «Stock bajo mínimo o sobre máximo»** (descripción: «Artículos cuyo stock por almacén está por debajo del mínimo o por encima del máximo configurado.»)
- Filtros: `almacen` Almacen; `grupo` Texto; `estado` Lista `FUERA`/«Bajo mínimo o sobre máximo», `BAJO`/«Bajo mínimo», `SOBRE`/«Sobre máximo», `TODOS`/«Todos», `ValorPorDefecto "FUERA"`.
- Fuente: `ArticuloAlmacen` (solo filas con mínimo o máximo > 0), existencia por (artículo, almacén) (0 si no hay fila).
- Columnas: `almacen` agrupable; `articulo`; `nombre`; `grupo` agrupable; `enStock` Cantidad Suma «En stock»; `minimo`, `maximo` Cantidad; `estado` Texto agrupable («Bajo mínimo», «Sobre máximo», «Normal» según `StockAlmacenCalculadora.Estado`); `nivelNecesario` Cantidad Suma «Por pedir» (= `StockAlmacenCalculadora.NivelNecesario(maximo, Disponible(enStock, comprometido, pedido))`, 0 si es null). Orden: almacén, artículo. `.Take(limite)`.
- Pruebas: los tres estados, `0` = sin nivel, artículo sin fila de existencia (enStock 0 → bajo mínimo si mínimo > 0), bloqueados siguen apareciendo, filtros, `limite`.

**Reporte `kardex-articulo` — «Kardex por artículo»** (descripción: «Movimientos de un artículo con saldo acumulado.»)
- Filtros: `articulo` Articulo **obligatorio**; `fechas` RangoFechas; `almacen` Almacen.
- Columnas: `fecha` Fecha; `tipoDoc` Texto agrupable «Documento» (`TiposDocumentoInventario.Nombre`); `docEntry` Entero «Entry del documento»; `linea` Entero; `almacen` Texto agrupable; `entra` Cantidad Suma; `sale` Cantidad Suma; `costoUnitario` Dinero «Costo»; `valor` Dinero Suma «Valor» (`ValorMovimiento`); `saldo` Cantidad «Saldo acumulado»; `reversa` Texto «Reversa» («Sí» si `MovReversaDe != null`, si no vacío).
- `saldo` = saldo inicial + acumulado. **Saldo inicial** = Σ(`CantidadEntra − CantidadSale`) de los movimientos del artículo (mismo filtro de almacén) con `Fecha < desde`; 0 si no hay `desde`. Orden y acumulado por `Fecha`, luego `Entry`. Si hay saldo inicial ≠ 0 añade una primera fila «Saldo inicial» (`tipoDoc = "Saldo inicial"`, `entra/sale/valor` 0, `saldo` = inicial). `.Take(limite)` sobre los movimientos (el acumulado se calcula antes del recorte).
- Pruebas: saldo acumulado con entradas, salidas y reversa; saldo inicial por `desde`; filtro de almacén afecta inicial y acumulado; artículo obligatorio (la valida el motor: verifícalo con `MotorReportes`); orden por fecha y entry.

**Reporte `movimientos-inventario` — «Movimientos por período»** (descripción: «Entradas y salidas de inventario con su valor.»)
- Filtros: `fechas` RangoFechas **obligatorio**; `articulo` Articulo; `almacen` Almacen; `tipoDoc` Lista con las opciones de `TiposDocumentoInventario` (`valor` = código `"5"`, `"6"`, `"12"`, `"13"`, `"59"`, `"60"`, `"70"`, `titulo` = nombre).
- Columnas: `fecha`; `tipoDoc` agrupable; `docEntry`; `linea`; `articulo` agrupable; `nombre`; `almacen` agrupable; `entra` Suma; `sale` Suma; `costoUnitario`; `valor` Dinero Suma. Orden: fecha, entry. `.Take(limite)`.
- Pruebas: filtros, `valor` firmado (salida negativa), `limite`.

- [ ] **Steps 1-4** como en la Tarea 3 (con `TiposDocumentoInventarioTests` primero).
- [ ] **Step 5: Commit** `feat(reporteria): reportes de inventario (stock minimo, kardex, movimientos)`.

---

### Task 7: Cobros — Facturas por cobrar y Facturas por pagar

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{FacturasPorCobrarReporte,FacturasPorPagarReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{FacturasPorCobrar,FacturasPorPagar}ReporteTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<Factura,int>` + `ISaldoFacturaService`; `IRepositorioGenerico<FacturaCompra,int>` + `ISaldoFacturaCompraService` (`ObtenerAsync(IEnumerable<int>)` → diccionario por entry; una factura ausente del diccionario se omite). `EstadoPagoFactura.Pendiente/Parcial/Pagada`.

**Reporte `facturas-por-cobrar` — «Facturas por cobrar»** (Familia «Cobros y pagos», descripción: «Facturas y notas de débito de clientes con su saldo, vencimiento y días de atraso.»)
- Filtros: `cliente` Socio "C"; `estadoPago` Lista `ABIERTAS`/«Pendientes y parciales», `PENDIENTE`, `PARCIAL`, `PAGADA`, `TODAS`, `ValorPorDefecto "ABIERTAS"`; `soloVencidas` Lista `N`/«No», `S`/«Sí», `ValorPorDefecto "N"`; `fechaCorte` Fecha «Fecha de corte» (vacía = hoy).
- Fuente: `Factura` con `Cancelado != "S"`, `DocSubType` `"--"`/`"ND"`, `FechaDoc ≤ corte` (día completo), cliente. Saldos con `ISaldoFacturaService.ObtenerAsync` **en lotes de 1,000 entries**.
- Columnas: `fecha` Fecha; `tipo` Texto agrupable («Factura»/«Nota de débito»); `serie` (oculta); `numDoc` «No.»; `codigoSn` (oculta); `nombreSn` «Cliente» agrupable; `vencimiento` Fecha («FechaVencimiento», si es null usa `FechaDoc`); `moneda`; `total` Dinero Suma; `acreditado` Dinero Suma «Notas de crédito»; `pagado` Dinero Suma; `saldo` Dinero Suma; `estado` Texto agrupable (el del servicio); `diasAtraso` Entero (= días entre corte y vencimiento si `saldo > 0` y vencimiento < corte; si no 0).
- `soloVencidas = "S"` deja las de `saldo > 0` y vencimiento < corte. Orden: vencimiento, fecha, número. `.Take(limite)` aplicado después de filtrar por saldo (no antes).
- Pruebas: cada `estadoPago`, vencida/no vencida, días de atraso, fecha de corte excluye facturas posteriores, ND incluida, cancelada excluida, lotes (2,500 facturas → 3 llamadas al servicio), `limite`.

**Reporte `facturas-por-pagar` — «Facturas por pagar»**: igual, con `FacturaCompra` (`DocSubType` `"--"`/`"DN"`), filtro `proveedor` Socio SubTipo "P" y `ISaldoFacturaCompraService`; tipos «Factura de compra»/«Nota de débito». Mismos nombres de campo (con `nombreSn` titulado «Proveedor»).

- [ ] **Steps 1-4** como en la Tarea 3.
- [ ] **Step 5: Commit** `feat(reporteria): facturas por cobrar y por pagar`.

---

### Task 8: Cobros — Pagos recibidos y Pagos efectuados

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{PagosRecibidosReporte,PagosEfectuadosReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{PagosRecibidos,PagosEfectuados}ReporteTests.cs`

**Interfaces:**
- Consumes: `PagoRecibido`, `PagoRecibidoMedio`, `PagoRecibidoFactura`, `Factura`, `FormaPago` (y los equivalentes `PagoEfectuado*`, `FacturaCompra`). Lee las entidades para las claves de repositorio y la navegación a `FormaPago` (la clave del medio es `CodFormaPago`).

**Reporte `pagos-recibidos` — «Pagos recibidos»** (descripción: «Pagos de clientes por forma de pago, con las facturas a las que se aplicaron.»)
- Filtros: `fechas` RangoFechas; `cliente` Socio "C"; `formaPago` Texto «Forma de pago (código)».
- Fuente: `PagoRecibido` con `Cancelado != "S"` y sus medios (una fila por medio).
- Columnas: `fecha` Fecha agrupable; `serie` (oculta); `numDoc` «No.»; `codigoSn` (oculta); `nombreSn` «Cliente» agrupable; `formaPago` Texto agrupable (nombre de la forma de pago; si no se puede resolver, el código); `referencia` Texto; `banco` Texto (oculta); `monto` Dinero Suma; `facturasAplicadas` Texto («12, 15, 20»: `NumDoc` de las facturas de `PagoRecibidoFactura`, ordenados, resueltos con **una** consulta de facturas por los entries recolectados). Sin columna `moneda` (Ruling 10).
- Pruebas: una fila por medio, facturas aplicadas ordenadas, pago cancelado excluido, filtros (incl. `formaPago`), `limite`, pago sin facturas aplicadas (texto vacío).

**Reporte `pagos-efectuados` — «Pagos efectuados»**: igual con `PagoEfectuado*`, `FacturaCompra`, filtro `proveedor` Socio "P", «Proveedor» y título «Pagos efectuados».

- [ ] **Steps 1-4** como en la Tarea 3.
- [ ] **Step 5: Commit** `feat(reporteria): pagos recibidos y efectuados`.

---

### Task 9: Cobros — Estado de cuenta y Antigüedad de saldos

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{EstadoCuentaSocioReporte,AntiguedadSaldosReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{EstadoCuentaSocio,AntiguedadSaldos}ReporteTests.cs`

**Interfaces:**
- Consumes: `IEstadoCuentaService.ObtenerAsync(string codigoSn, DateTime? desde, DateTime? hasta)` → `EstadoCuenta(Socio, Desde, Hasta, SaldoInicial, Movimientos, SaldoFinal, SaldoActual, Vencido)`; `IAntiguedadService.ObtenerAsync(string tipo)` → `IReadOnlyList<FilaAntiguedad>`. Mockéalos con Moq.

**Reporte `estado-cuenta-socio` — «Estado de cuenta de un socio»** (descripción: «Cargos, abonos y saldo de un cliente o proveedor.»)
- Filtros: `socio` Socio (sin subtipo) **obligatorio**; `fechas` RangoFechas.
- Columnas: `fecha` Fecha; `tipo` Texto agrupable; `numDoc` Entero «No.»; `descripcion` Texto; `cargo` Dinero Suma; `abono` Dinero Suma; `saldo` Dinero «Saldo».
- Primera fila «Saldo inicial» (`tipo = "Saldo inicial"`, `fecha` = `Desde` del estado, `saldo = SaldoInicial`, cargo/abono 0); luego un renglón por `MovimientoCuenta` (`saldo = SaldoAcumulado`). `.Take(limite)`.
- Si el servicio lanza (p. ej. «El socio de negocio no existe.»), la definición lo deja salir como `ReporteInvalidoException` con el **mismo mensaje** (captura `Exception` y relanza con `ex.Message`), para que el usuario vea 400 con ese texto. Prueba ese camino.
- Pruebas: filas, saldo inicial, informativos (cargo y abono 0), error del servicio, parámetros `desde/hasta` correctos al servicio.

**Reporte `antiguedad-saldos` — «Antigüedad de saldos»** (descripción: «Saldos por cobrar o por pagar repartidos por días de vencimiento, al día de hoy.»)
- Filtros: `tipo` Lista `C`/«Por cobrar (clientes)», `P`/«Por pagar (proveedores)», **obligatorio**, `ValorPorDefecto "C"`; `socio` Texto «Código de socio».
- Columnas: `codigoSn` «Código»; `nombreSn` «Socio»; `vigente` Dinero Suma «Corriente»; `de1a30` «1–30 días»; `de31a60` «31–60»; `de61a90` «61–90»; `mas90` «Más de 90»; `total` Dinero Suma (cada columna de importe con `Suma`). Sin columna `moneda`.
- Una fila por socio; filtro `socio` por `CodigoSn`; orden por `total` descendente. `.Take(limite)`.
- Pruebas: mapeo de cada columna, filtro de socio, orden, tipo pasado al servicio.

- [ ] **Steps 1-4** como en la Tarea 3.
- [ ] **Step 5: Commit** `feat(reporteria): estado de cuenta y antiguedad de saldos`.

---

### Task 10: Compras — Compras por documento y Compras por artículo

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{ComprasPorDocumentoReporte,ComprasPorArticuloReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{ComprasPorDocumento,ComprasPorArticulo}ReporteTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<FacturaCompra,int>`, `IRepositorioGenerico<NotaCreditoCompra,int>`, `IRepositorioGenerico<FacturaCompraDetalle,(int Entry,int NoLinea)>`.

**Reporte `compras-por-documento` — «Compras por documento»** (Familia «Compras», `ReportesCompras`): espejo exacto de `VentasPorDocumentoReporte` con `FacturaCompra` (`DocSubType` `"--"` → «Factura de compra», `"DN"` → «Nota de débito de compra»; **suman**) y `NotaCreditoCompra` («Nota de crédito de compra»; **restan**). Filtros: `fechas`; `proveedor` Socio SubTipo "P"; `tipo` Lista `FAC`/«Factura de compra», `DN`/«Nota de débito», `NC`/«Nota de crédito»; `serie` Numero. Mismas columnas que el de ventas (`codigoSn` «Código proveedor», `nombreSn` «Proveedor»). Pruebas: espejo de `VentasPorDocumentoReporteTests` (signos, tipos, cancelados, filtros, `limite`).

**Reporte `compras-por-articulo` — «Compras por artículo»** (descripción: «Cada línea comprada con su cantidad y compra neta; agrupa por artículo o proveedor para ver totales.»)
- Filtros: `fechas`; `proveedor` Socio "P"; `articulo` Articulo; `grupo` Texto; `almacen` Almacen.
- Fuente: `FacturaCompraDetalle` de facturas de compra con `Cancelado != "S"` y `DocSubType` `"--"`/`"DN"`, con `CodArticulo != null`.
- Columnas: `fecha` agrupable; `tipo` agrupable; `numDoc`; `codigoSn` (oculta); `nombreSn` «Proveedor» agrupable; `articulo` agrupable; `nombreArticulo`; `grupo` agrupable; `almacen` agrupable; `moneda`; `cantidad` Cantidad Suma «Cantidad (base)»; `precioNeto` Dinero «Precio neto» (= compra neta / cantidad base); `descuento` Cantidad «Descuento %» (`PrctjeDesc`); `compraNeta` Dinero Suma («TotalLinea − Impuesto», Ruling 3).
- Mismas reglas de subconsulta que la Tarea 1 (no materializar listas de entries). `.Take(limite)`.
- Pruebas: compra neta, cantidad base, precio neto, DN incluida, cancelada excluida, cada filtro, grupo, `limite`.

- [ ] **Steps 1-4** como en la Tarea 3.
- [ ] **Step 5: Commit** `feat(reporteria): compras por documento y por articulo`.

---

### Task 11: Compras — Documentos de compra por estado e Historial de precios

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/{DocumentosCompraEstadoReporte,HistorialPreciosCompraReporte}.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/{DocumentosCompraEstado,HistorialPreciosCompra}ReporteTests.cs`

**Reporte `documentos-compra-estado` — «Documentos de compra por estado»**: espejo de `documentos-venta-estado` con `PedidoCompra`, `EntregaCompra` (tipo «Entrega de compra») y `FacturaCompra` (`"--"`/`"DN"`), filtro `tipo` Lista `PED`/«Pedido de compra», `ENT`/«Entrega de compra», `FAC`/«Factura de compra», `proveedor` Socio "P", columna «Proveedor». Ruling 8 y cancelados incluidos.

**Reporte `historial-precios-compra` — «Historial de precios de compra»** (descripción: «Precio neto pagado por un artículo a lo largo del tiempo.»)
- Filtros: `articulo` Articulo **obligatorio**; `fechas` RangoFechas; `proveedor` Socio "P".
- Columnas: `fecha` Fecha; `numDoc` Entero «No.»; `codigoSn` (oculta), `nombreSn` «Proveedor» agrupable; `articulo`; `moneda` (oculta); `cantidad` Cantidad Suma «Cantidad (base)»; `precioNeto` Dinero «Precio neto por unidad base» (= `UnidadesLinea.PrecioBase(Precio, FactorUom)` con descuento aplicado: `× (1 − PrctjeDesc/100)`); `descuento` Cantidad «Descuento %». Orden: fecha, entry, línea. Cancelados excluidos, DN incluida. `.Take(limite)`.
- Pruebas: precio con unidad de medida (factor 12) y con descuento, orden, filtros, artículo obligatorio vía `MotorReportes`.

- [ ] **Steps 1-4** como en la Tarea 3.
- [ ] **Step 5: Commit** `feat(reporteria): documentos de compra por estado e historial de precios`.

---

### Task 12: Prueba de humo con SQL real — Inventario, Cobros y Compras

**Files:** solo `<scratchpad>`.

- [ ] **Step 1:** Reutiliza la base temporal y la API de la Tarea 5 (o créalas de nuevo igual). Agrega por la API: una factura de compra de `ART-A` en dos fechas y precios distintos y una nota de crédito de compra; pagos recibido y efectuado (con medios y facturas aplicadas, uno parcial); un artículo con `ArticuloAlmacen` mínimo 10 / máximo 50 y stock fuera de rango; un documento de compra cancelado.
- [ ] **Step 2:** Ejecuta los 13 reportes restantes y verifica números a mano: `stock-bajo-minimo`, `kardex-articulo` (saldo acumulado y con `desde` posterior), `movimientos-inventario`, `facturas-por-cobrar` / `facturas-por-pagar` (estados Pendiente/Parcial/Pagada, vencidas con fecha de corte), `pagos-recibidos` / `pagos-efectuados`, `estado-cuenta-socio` (compara el saldo final con la pantalla existente `api/EstadoCuenta`), `antiguedad-saldos` (compara con `api/Antiguedad`), `compras-por-documento`, `compras-por-articulo`, `documentos-compra-estado`, `historial-precios-compra`. 0 errores 500.
- [ ] **Step 3:** Corrige con prueba de regresión lo que no se traduzca o no cuadre (`fix(reporteria): <qué>`). Evidencia en `<scratchpad>\evid-reporteria-catalogo-otros-2026-11-14.txt`.

---

### Task 13: Coherencia del catálogo, documentación y cierre

**Files:**
- Create: `API.Service.WebApi.Tests/Domain/CatalogoReportesCompletoTests.cs`
- Modify: `API.Service.WebApi/Startup.cs` (solo si falta algún registro), `AGENTS.md`, `docs/traspaso-2026-11-07.md`

- [ ] **Step 1: Prueba de coherencia (falla si falta algo).** Construye el `CatalogoReportes` con **todas** las clases `IReporte` instanciadas con mocks y verifica: (a) los 22 códigos (los 20 de este plan + `ventas-por-documento` + `existencias-valor`) existen y son únicos; (b) cada `Familia/Modulo` es una de las cuatro parejas del Global Constraints; (c) todo reporte tiene al menos una columna `VisiblePorDefecto`; (d) los `Campo` de columnas y filtros son únicos dentro del reporte; (e) un test que revisa por reflexión que **todas** las clases `IReporte` del ensamblado `Domain.Core` están registradas en `Startup` (busca `AddTransient<IReporte, <Clase>>` en el texto de `Startup.cs` leyendo el archivo).
- [ ] **Step 2:** Pruebas verdes; build de `API.sln` 0 errores; suite completa verde (anota el conteo final).
- [ ] **Step 3: Documentación.**
  - `AGENTS.md`: reemplaza la frase «Solo hay 2 reportes de muestra; los otros 16 del catálogo (spec §4) son la fase 4, pendiente.» por el estado real (22 reportes, códigos por familia en una línea cada una, `ConsultaLineasVenta` como consulta compartida de ventas con costo, regla de utilidad y que el costo viene de `MovimientoInventario`), y actualiza la línea base de la suite.
  - `docs/traspaso-2026-11-07.md`: sección «Reportería fase 4» con qué se hizo, las limitaciones (saldos al día de hoy; antigüedad sin fecha de corte; sin margen %; tope de 200,000 líneas en reportes agregados; filtros de socio/artículo/almacén requieren permiso Ver en esos catálogos; QuestPDF Community; asignar los módulos `Reportes*` a plantillas y correr `sql/2026-11-08-reporteria.sql` en la base real) y la evidencia.
- [ ] **Step 4: Cierre.** Elimina la base temporal con `<scratchpad>\evid-down.ps1` y confirma que no queda ninguna `API_DB_EVID*`. Detén la API de humo.
- [ ] **Step 5: Commit** `docs(reporteria): catalogo completo de reportes y cobertura de coherencia` con rutas explícitas (`CatalogoReportesCompletoTests.cs`, `AGENTS.md`, `docs/traspaso-2026-11-07.md`).
