# Entrada y Salida de Mercancías (INV-4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dos documentos nuevos de ajuste de inventario sin socio de negocio — `EntradaMercancia` (`TipoObjeto="59"`, suma stock a un costo por línea) y `SalidaMercancia` (`"60"`, descuenta al costo promedio móvil con bloqueo duro de negativo), con el mismo patrón de asiento atómico + cancelación de INV-2/INV-3.

**Architecture:** Task 1 crea el esquema (DDL en `API/sql/` que aplica el usuario + entidades EF + `OnModelCreating` + repos concretos + DI). Task 2 escribe `EntradaMercanciaDomain` tomando `EntregaCompraDomain` (canónico de INV-2, ya en `main`) como plantilla, sin socio/moneda/impuesto, con `CostoUnitario` y `CostoVigenteAsync` (fallback `MetodoValuacion=="E" ? CostoEstandar : CostoPromedio`). Task 3 = `SalidaMercancia*` por transformación de Task 2 + 3 deltas (costo siempre del artículo, `Cantidad` negativa, test de stock insuficiente). Tasks 4-5 = pantallas Web (Entradas plantilla de `EntregasCompra` recortando socio/impuesto; Salidas transformación de Entradas). Task 6 = verificación conjunta.

**Tech Stack:** C# / .NET 7 (API) y .NET 8 (Web), EF Core (SQL Server), AutoMapper, xUnit + Moq, jQuery + Bootstrap.

**Spec:** `API/docs/superpowers/specs/2026-09-01-inventario-mercancias-design.md`

## Global Constraints

- **Repos y ramas:** API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`); Web en `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad git `panchoman08`. Sin push hasta aprobación final del usuario.
- **Build/test a carpeta externa:** `-p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"` (y `...\apitest\`, `...\webbuild\`).
- **No hay .NET 7 SDK**; el SDK 9/10 compila `net7.0`. No añadir `global.json`.
- **`dotnet test` de la suite completa de la API en verde** antes de terminar cualquier tarea que toque la API. **Baseline actual: 695 pruebas, 0 fallos.**
- **`appsettings.json` (`API.Service.WebApi`) y `appsettings*.json` (Web) pueden aparecer modificados localmente con un connection string real — NUNCA commitearlos.** `git add` con rutas explícitas, nunca `git add -A`.
- **`TipoObjeto`:** `EntradaMercancia` = `"59"`, `SalidaMercancia` = `"60"`. Se fuerza en el servidor.
- **Flags de las tablas** (defaults del DDL): `EstadoDoc` `'A'`/`'C'` (INV-4 no lo toca); `Cancelado` `'S'`/`'N'` (`'S'` dispara el reversó); `EstadoInv` `'A'`/`'C'` (asentado / revertido).
- **El DDL lo aplica el usuario** en `API_DB_TEST`. Los unit tests usan Moq (no necesitan la BD). La suite debe quedar verde SIN que el `.sql` se haya aplicado.
- **`Startup.cs`:** se toca en Task 1 (registrar los 4 repos + 4 domains + 4 applications nuevos) y en NINGUNA otra task.
- **Contrato de INV-1 (no romper):** `IInventarioAsientoService.AsentarAsync(IEnumerable<MovimientoRequest>, bool permitirNegativo = false)` / `RevertirAsync(string tipoDoc, int docEntry)` **nunca** llaman `SaveChangesAsync`. `AgregarSinGuardarAsync` hace `DbSet.AddAsync` sin guardar. `MovimientoRequest(string TipoDoc, int DocEntry, int DocLinea, string CodArticulo, string CodAlmacen, decimal Cantidad, decimal PrecioUnitario, DateTime Fecha)` — `Cantidad > 0` entrada, `< 0` salida; con `permitirNegativo:false` y `Disponible + Cantidad < 0` lanza `StockInsuficienteException`. Excepciones tipadas en `API.Domain.Core.Inventario`.
- **`IEjecutorTransaccion.EjecutarAsync<T>(Func<Task<T>>)`:** al retornar sin excepción hace `SaveChangesAsync` + `Commit`; si lanza, `Rollback` + `ChangeTracker.Clear()` + repropaga. Los domains **nunca** llaman `SaveChangesAsync` directo.
- **Lecciones de INV-3 aplicadas de entrada:** (a) `InsertarAsync` fuerza `Cancelado="N"` y `FechaCancelado=null`; (b) edición inocua copia `Comentario` incondicional (replace-semantics); (c) `EliminarAsync` envuelve el borrado en `_tx.EjecutarAsync`; (d) `DetalleDomain.InsertarAsync` rechaza incondicionalmente.
- **`Articulo`:** `MetodoValuacion` `IN ('P','E')` (P=promedio, E=estándar); `CostoPromedio` / `CostoEstandar` `decimal(19,6)`. `IRepositorioGenerico<Articulo,string>` ya registrado.
- **Estilo de plan: por transformación (DRY).** Tasks 3 y 5 referencian los archivos que dejó la task previa + tabla de sustitución + deltas escritos completos. Task 2 y Task 4 toman los archivos `*Compra` commiteados como plantilla.
- **Fuera de alcance:** conteo físico / Inventory Posting de SAP, traslados entre almacenes, `BaseEntry`/chaining, reintento por concurrencia, descancelar, editar líneas post-asiento, pantalla para editar `MetodoValuacion`/`CostoEstandar`.

---

## File Structure

**API (rama `desarrollo`):**

| Archivo | Task | Responsabilidad |
|---|---|---|
| `API/sql/2026-09-01-inventario-mercancias.sql` | 1 | DDL de las 4 tablas + seed de series `'59'`/`'60'` |
| `API.Domain.Entity/Models/EntradaMercancia.cs` · `EntradaMercanciaDetalle.cs` · `SalidaMercancia.cs` · `SalidaMercanciaDetalle.cs` | 1 | entidades EF (partial class) |
| `API.Domain.Entity/Models/ApiDbTestContext.cs` | 1 | `DbSet<>` + bloques `OnModelCreating` + colecciones inversas |
| `API.Infraestructure.Repository/EntradaMercanciaRepositorio.cs` · `EntradaMercanciaDetalleRepositorio.cs` · `SalidaMercanciaRepositorio.cs` · `SalidaMercanciaDetalleRepositorio.cs` | 1 | repos concretos |
| `API.Service.WebApi/Startup.cs` | 1 | DI de repos + domains + applications |
| `API.Domain.Interface/IEntradaMercanciaDomain.cs` · `IEntradaMercanciaDetalleDomain.cs` | 2 | contratos |
| `API.Domain.Core/EntradaMercanciaDomain.cs` · `EntradaMercanciaDetalleDomain.cs` | 2 | lógica |
| `API.Application.Interface/IEntradaMercanciaApplication.cs` · `IEntradaMercanciaDetalleApplication.cs` | 2 | contratos application |
| `API.Application.Main/EntradaMercanciaApplication.cs` · `EntradaMercanciaDetalleApplication.cs` | 2 | orquestación |
| `API.Application.DTO/entradaMercancia/EntradaMercanciaCrearDTO.cs` · `EntradaMercanciaActualizarDTO.cs` · `EntradaMercanciaDTO.cs` + carpeta `entradaMercanciaDetalle/` (`*CrearDTO`, `*ActualizarDTO`, `*DTO`) | 2 | DTOs |
| `API.Transversal.Mapper/PerfilMapeo.cs` | 2 (+3) | `CreateMap<>` de los DTOs |
| `API.Service.WebApi/Controllers/EntradaMercanciaController.cs` · `EntradaMercanciaDetalleController.cs` | 2 | endpoints |
| `API.Service.WebApi.Tests/Domain/EntradaMercanciaDomainTests.cs` · `EntradaMercanciaDetalleDomainTests.cs` | 2 | tests |
| todos los `SalidaMercancia*` equivalentes de Task 2 | 3 | transformación |

**Web (rama `main`):**

| Archivo | Task |
|---|---|
| `Web.ApiClient/Dtos/EntradaMercancia/` + `EntradaMercanciaDetalle/` (Crear/Actualizar/DTO) | 4 |
| `Web.ApiClient/Clientes/IEntradaMercanciaApiClient.cs` + `EntradaMercanciaApiClient.cs` (+ Detalle) | 4 |
| `Web.UI/Controllers/EntradasMercanciaController.cs` | 4 |
| `Web.UI/wwwroot/js/entradasmercancia.js` | 4 |
| `Web.UI/Views/EntradasMercancia/Index.cshtml` + `_Form.cshtml` | 4 |
| `Web.UI/Views/Shared/_Layout.cshtml` (submenú Inventario) | 4 |
| `Web.UI/Program.cs` (registro de los api-clients tipados, si el patrón lo exige) | 4 |
| todos los `SalidasMercancia*` equivalentes de Task 4 | 5 |

**No se tocan:** `InventarioAsientoService.cs`, `EjecutorTransaccion.cs`, `IInventarioAsientoService.cs`, ni ningún archivo de compra/venta/Cotización/Pedido.

---

## Task 1: Esquema — SQL, entidades EF, `OnModelCreating`, repos, DI

**Files:**
- Create: `API/sql/2026-09-01-inventario-mercancias.sql`
- Create: `API.Domain.Entity/Models/EntradaMercancia.cs`, `EntradaMercanciaDetalle.cs`, `SalidaMercancia.cs`, `SalidaMercanciaDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs`
- Create: `API.Infraestructure.Repository/EntradaMercanciaRepositorio.cs`, `EntradaMercanciaDetalleRepositorio.cs`, `SalidaMercanciaRepositorio.cs`, `SalidaMercanciaDetalleRepositorio.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Infraestructure/EsquemaMercanciasTests.cs` (mínimo, ver Step 8)

**Interfaces:**
- Produces:
  - Entidades `EntradaMercancia` (props: `int Entry`, `int NumDoc`, `int Serie`, `string? NumManual`, `string? Imprimido`, `string? EstadoDoc`, `string? EstadoInv`, `string? Cancelado`, `string? TipoObjeto`, `DateTime? FechaDoc`, `DateTime? FechaContab`, `DateTime? FechaCancelado`, `string? Referencia`, `string? Comentario`, `decimal? TotalDoc`, `virtual NumeracionDocumentoDet SerieNavigation`), `EntradaMercanciaDetalle` (props: `int Entry`, `int NoLinea`, `string? CodArticulo`, `string? Descripcion`, `decimal? Cantidad`, `decimal? CostoUnitario`, `decimal? TotalLinea`, `string? CodAlmacen`, `virtual Almacen? CodAlmacenNavigation`, `virtual Articulo? CodArticuloNavigation`). `SalidaMercancia` / `SalidaMercanciaDetalle` idénticas.
  - `IRepositorioGenerico<EntradaMercancia,int>`, `<EntradaMercanciaDetalle,(int Entry,int NoLinea)>`, `<SalidaMercancia,int>`, `<SalidaMercanciaDetalle,(int Entry,int NoLinea)>` registrados en DI.

- [ ] **Step 1: DDL — `API/sql/2026-09-01-inventario-mercancias.sql`**

Verificar primero, contra las tablas de compra existentes en `API_DB_TEST` o contra
`ApiDbTestContext.OnModelCreating` (bloque `EntregaCompra`): (a) el tipo real de `Serie` y si
`NumeracionDocumentoDet` tiene índice único en `Serie` sola (para poder poner FK); (b) las
columnas NOT NULL de `NumeracionDocumentoDet` y sus valores típicos (mirar filas existentes o
el helper `SerieAuto` de los tests: `SubTipoDoc`, `TipoSerie`, `NombreSerie`). Ajustar el seed
con esos valores reales.

```sql
-- INV-4: Entrada y Salida de Mercancias (ajustes de inventario sin socio de negocio).
-- Idempotente: cada objeto se crea solo si no existe.
-- Referencia: OIGN/IGN1 y OIGE/IGE1 de SAP B1, recortado a lo que el proyecto usa.
SET NOCOUNT ON;

-- ===== EntradaMercancia (ObjType 59) =====
IF OBJECT_ID('dbo.EntradaMercancia', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntradaMercancia (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL CONSTRAINT DF_EntradaMerc_NumDoc     DEFAULT (0),
        Serie          int           NOT NULL,
        NumManual      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_NumManual  DEFAULT ('N') CONSTRAINT CK_EntradaMerc_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_Imprimido  DEFAULT ('N'),
        EstadoDoc      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_EstadoDoc  DEFAULT ('A') CONSTRAINT CK_EntradaMerc_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        EstadoInv      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_EstadoInv  DEFAULT ('A') CONSTRAINT CK_EntradaMerc_EstadoInv CHECK (EstadoInv IN ('A','C')),
        Cancelado      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_Cancelado  DEFAULT ('N') CONSTRAINT CK_EntradaMerc_Cancelado CHECK (Cancelado IN ('S','N')),
        TipoObjeto     varchar(11)   NOT NULL CONSTRAINT DF_EntradaMerc_TipoObjeto DEFAULT ('59'),
        FechaDoc       datetime      NULL,
        FechaContab    datetime      NULL,
        FechaCancelado datetime      NULL,
        Referencia     nvarchar(100) NULL,
        Comentario     nvarchar(254) NULL,
        TotalDoc       decimal(19,6) NOT NULL CONSTRAINT DF_EntradaMerc_TotalDoc   DEFAULT (0),
        CONSTRAINT pk_entrada_mercancia PRIMARY KEY (Entry),
        CONSTRAINT fk_entrada_mercancia_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie)
    );
END

-- ===== EntradaMercanciaDetalle =====
IF OBJECT_ID('dbo.EntradaMercanciaDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntradaMercanciaDetalle (
        Entry         int           NOT NULL,
        NoLinea       int           NOT NULL,
        CodArticulo   varchar(20)   NULL,
        Descripcion   nvarchar(254) NULL,
        Cantidad      decimal(19,6) NULL,
        CostoUnitario decimal(19,6) NOT NULL CONSTRAINT DF_EntradaMercDet_Costo DEFAULT (0),
        TotalLinea    decimal(19,6) NULL,
        CodAlmacen    varchar(10)   NULL,
        CONSTRAINT pk_entrada_mercancia_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_entrada_mercancia_det_art FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_entrada_mercancia_det_alm FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo)
    );
END

-- ===== SalidaMercancia (ObjType 60) — misma estructura, default TipoObjeto '60' =====
-- (repetir los dos CREATE TABLE con nombres SalidaMercancia / SalidaMercanciaDetalle,
--  constraints renombradas *_SalidaMerc*, DEFAULT ('60') en TipoObjeto)

-- ===== Seed de numeracion (idempotente) =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '59')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, SigNumero, Manual, Bloqueado, SubTipoDoc, TipoSerie)
    VALUES ('59', (SELECT ISNULL(MAX(Serie),0)+1 FROM dbo.NumeracionDocumentoDet), 'Primario', 1, 'N', 'N', '--', 'N');
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '60')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, SigNumero, Manual, Bloqueado, SubTipoDoc, TipoSerie)
    VALUES ('60', (SELECT ISNULL(MAX(Serie),0)+1 FROM dbo.NumeracionDocumentoDet), 'Primario', 1, 'N', 'N', '--', 'N');
```

Si al verificar (arriba) resulta que la FK simple a `NumeracionDocumentoDet(Serie)` no es
posible (PK compuesta, sin índice único en `Serie`), **omitir la línea `fk_*_serie`** y dejar
`Serie` sin FK declarada (como ya hacen las tablas de compra si ese es el caso), y anotarlo en
el reporte.

- [ ] **Step 2: Entidades EF**

`API.Domain.Entity/Models/EntradaMercancia.cs` — copiar la forma de `EntregaCompra.cs`
(namespace `API.Domain.Entity.Models`, `partial class`), con exactamente las propiedades del
bloque **Produces** de arriba. `SerieNavigation` es `virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;`.

`EntradaMercanciaDetalle.cs` — forma de `EntregaCompraDetalle.cs`, propiedades del bloque
Produces, con `virtual Almacen? CodAlmacenNavigation` y `virtual Articulo? CodArticuloNavigation`.

`SalidaMercancia.cs` / `SalidaMercanciaDetalle.cs` — idénticas (solo cambia el nombre de la
clase).

- [ ] **Step 3: `OnModelCreating` + `DbSet` en `ApiDbTestContext.cs`**

Añadir 4 `public virtual DbSet<...> ...s { get; set; }` junto a los de `EntregaCompra`.

Añadir 4 bloques `modelBuilder.Entity<EntradaMercancia>(entity => { ... })` copiando el patrón
del bloque `EntregaCompra` (líneas ~572-640 de `ApiDbTestContext.cs`):
- `entity.HasKey(e => e.Entry).HasName("pk_entrada_mercancia");`
- `entity.ToTable("EntradaMercancia");`
- `HasMaxLength(1)` + `HasDefaultValueSql("('N')")` en `NumManual`, `Imprimido`, `Cancelado`;
  `HasDefaultValueSql("('A')")` en `EstadoDoc`, `EstadoInv`.
- `entity.Property(e => e.TipoObjeto).HasMaxLength(11).HasDefaultValueSql("('59')");`
- `HasColumnType("datetime")` en `FechaDoc`, `FechaContab`, `FechaCancelado`.
- `entity.Property(e => e.Referencia).HasMaxLength(100);` `entity.Property(e => e.Comentario).HasMaxLength(254);`
- `entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");`
- `entity.HasOne(d => d.SerieNavigation).WithMany(p => p.EntradaMercancias).HasForeignKey(d => d.Serie).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("fk_entrada_mercancia_serie");`

Detalle (patrón del bloque `EntregaCompraDetalle`):
- `entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_entrada_mercancia_det");`
- `entity.ToTable("EntradaMercanciaDetalle");`
- `HasColumnType("decimal(19, 6)")` en `Cantidad`, `CostoUnitario`, `TotalLinea`.
- `HasMaxLength` en `CodArticulo` (20), `CodAlmacen` (10), `Descripcion` (254).
- `entity.HasOne(d => d.CodAlmacenNavigation).WithMany(p => p.EntradaMercanciaDetalles).HasForeignKey(d => d.CodAlmacen).HasConstraintName("fk_entrada_mercancia_det_almacen");`
- `entity.HasOne(d => d.CodArticuloNavigation).WithMany(p => p.EntradaMercanciaDetalles).HasForeignKey(d => d.CodArticulo).HasConstraintName("fk_entrada_mercancia_det_cod_art");`

Añadir las colecciones inversas requeridas por `WithMany(...)`:
`public virtual ICollection<EntradaMercancia> EntradaMercancias { get; set; } = new List<EntradaMercancia>();` en `NumeracionDocumentoDet.cs`;
`ICollection<EntradaMercanciaDetalle> EntradaMercanciaDetalles` en `Almacen.cs` y `Articulo.cs`.
Ídem las 3 de `SalidaMercancia`.

- [ ] **Step 4: Repos concretos**

`API.Infraestructure.Repository/EntradaMercanciaRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class EntradaMercanciaRepositorio : RepositorioGenericoEfCore<EntradaMercancia, int>
    {
        public EntradaMercanciaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```
`EntradaMercanciaDetalleRepositorio.cs` — copiar `EntregaCompraDetalleRepositorio.cs` (hereda
`RepositorioGenericoEfCore<EntradaMercanciaDetalle, (int Entry, int NoLinea)>`, override
`ObtenerAsync` que hace `DbSet.FindAsync(id.Entry, id.NoLinea)`).
Ídem `SalidaMercanciaRepositorio.cs` / `SalidaMercanciaDetalleRepositorio.cs`.

- [ ] **Step 5: DI en `Startup.cs`**

Junto al bloque de `EntregaCompra` (líneas ~192-198), añadir:
```csharp
            services.AddTransient<IRepositorioGenerico<EntradaMercancia, int>, EntradaMercanciaRepositorio>();
            services.AddTransient<IRepositorioGenerico<EntradaMercanciaDetalle, (int Entry, int NoLinea)>, EntradaMercanciaDetalleRepositorio>();
            services.AddTransient<IRepositorioGenerico<SalidaMercancia, int>, SalidaMercanciaRepositorio>();
            services.AddTransient<IRepositorioGenerico<SalidaMercanciaDetalle, (int Entry, int NoLinea)>, SalidaMercanciaDetalleRepositorio>();
```
Los `IEntradaMercanciaDomain` / `IEntradaMercanciaApplication` / etc. se registran en **Task 2**
(este step solo los repos, que es lo que Task 1 produce). Dejar un comentario
`// IEntradaMercanciaDomain/Application se registran en INV-4 Task 2`.

- [ ] **Step 6: Build**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 7: Suite completa**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: **0 fallos** (695 + el del Step 8).

- [ ] **Step 8: Test mínimo de esquema**

`API.Service.WebApi.Tests/Infraestructure/EsquemaMercanciasTests.cs` — un test que verifica que
el modelo EF conoce las 4 entidades con su PK, usando el mismo patrón que
`ModeloInventarioTests` (de INV-1: construir `new ApiDbTestContext(options)` con un connection
string literal nunca abierto y consultar `.Model`). Si ese test de INV-1 no existe o usa otro
patrón, mirar cómo se construye el contexto offline en la suite y replicarlo.

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructure
{
    public class EsquemaMercanciasTests
    {
        private static ApiDbTestContext Contexto()
        {
            var options = new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=(localdb)\\nunca;Database=x;Trust Server Certificate=True")
                .Options;
            return new ApiDbTestContext(options);
        }

        [Theory]
        [InlineData(typeof(EntradaMercancia))]
        [InlineData(typeof(EntradaMercanciaDetalle))]
        [InlineData(typeof(SalidaMercancia))]
        [InlineData(typeof(SalidaMercanciaDetalle))]
        public void ModeloConoceLaEntidadConPk(System.Type tipo)
        {
            using var ctx = Contexto();
            var et = ctx.Model.FindEntityType(tipo);
            Assert.NotNull(et);
            Assert.NotNull(et!.FindPrimaryKey());
        }
    }
}
```
Si el ctor de `ApiDbTestContext` no acepta `DbContextOptions` (solo el sin-parámetros con
`IConfiguration`), replicar exactamente el enfoque del test de esquema de INV-1
(`ModeloInventarioTests` o equivalente) — está en `API.Service.WebApi.Tests/`.

- [ ] **Step 9: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API/sql/2026-09-01-inventario-mercancias.sql API.Domain.Entity/Models/EntradaMercancia.cs API.Domain.Entity/Models/EntradaMercanciaDetalle.cs API.Domain.Entity/Models/SalidaMercancia.cs API.Domain.Entity/Models/SalidaMercanciaDetalle.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Domain.Entity/Models/NumeracionDocumentoDet.cs API.Domain.Entity/Models/Almacen.cs API.Domain.Entity/Models/Articulo.cs API.Infraestructure.Repository/EntradaMercanciaRepositorio.cs API.Infraestructure.Repository/EntradaMercanciaDetalleRepositorio.cs API.Infraestructure.Repository/SalidaMercanciaRepositorio.cs API.Infraestructure.Repository/SalidaMercanciaDetalleRepositorio.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Infraestructure/EsquemaMercanciasTests.cs
git commit -m "feat(api): esquema de Entrada y Salida de Mercancias (INV-4) — DDL, entidades EF, repos, DI"
```
(trailer `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`. Ajustar la lista de `git add` a los archivos realmente tocados.)

---

## Task 2: `EntradaMercanciaDomain` — asiento de entrada atómico + resolución de costo

**Files:**
- Create: `API.Domain.Interface/IEntradaMercanciaDomain.cs`, `IEntradaMercanciaDetalleDomain.cs`
- Create: `API.Domain.Core/EntradaMercanciaDomain.cs`, `EntradaMercanciaDetalleDomain.cs`
- Create: `API.Application.Interface/IEntradaMercanciaApplication.cs`, `IEntradaMercanciaDetalleApplication.cs`
- Create: `API.Application.Main/EntradaMercanciaApplication.cs`, `EntradaMercanciaDetalleApplication.cs`
- Create: `API.Application.DTO/entradaMercancia/EntradaMercanciaCrearDTO.cs`, `EntradaMercanciaActualizarDTO.cs`, `EntradaMercanciaDTO.cs`; `API.Application.DTO/entradaMercanciaDetalle/EntradaMercanciaDetalleCrearDTO.cs`, `EntradaMercanciaDetalleActualizarDTO.cs`, `EntradaMercanciaDetalleDTO.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`
- Create: `API.Service.WebApi/Controllers/EntradaMercanciaController.cs`, `EntradaMercanciaDetalleController.cs`
- Modify: `API.Service.WebApi/Startup.cs` (registrar los domains + applications)
- Create: `API.Service.WebApi.Tests/Domain/EntradaMercanciaDomainTests.cs`, `EntradaMercanciaDetalleDomainTests.cs`

**Interfaces:**
- Consumes (Task 1): entidades + `IRepositorioGenerico<EntradaMercancia,int>` / `<EntradaMercanciaDetalle,(int,int)>`. (INV-1/INV-2): `IEjecutorTransaccion`, `IInventarioAsientoService`, `MovimientoRequest`, `IRepositorioGenerico<Articulo,string>`, `IRepositorioGenerico<NumeracionDocumentoDet,int>`.
- Produces: `IEntradaMercanciaDomain.InsertarAsync(EntradaMercancia obj, IEnumerable<EntradaMercanciaDetalle> lineas) -> Task<int>`; `EntradaMercanciaCrearDTO.Lineas : List<EntradaMercanciaDetalleCrearDTO>`; `EntradaMercanciaDetalleDomain(IRepositorioGenerico<EntradaMercanciaDetalle,(int,int)>, IRepositorioGenerico<EntradaMercancia,int>)` (detalle primero, encabezado segundo).

### Plantillas (canónico de INV-2, en HEAD de `desarrollo`)

`API.Domain.Core/EntregaCompraDomain.cs`, `EntregaCompraDetalleDomain.cs`,
`API.Domain.Interface/IEntregaCompraDomain.cs`, `API.Application.Main/EntregaCompraApplication.cs`,
`API.Application.Interface/IEntregaCompraApplication.cs`,
`API.Application.DTO/entregaCompra/*`, `API.Application.DTO/entregaCompraDetalle/*`,
`API.Service.WebApi/Controllers/EntregaCompraController.cs`,
`API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs`, `EntregaCompraDetalleDomainTests.cs`.

### Tabla de sustitución (plantilla → Task 2)

| Plantilla (`EntregaCompra`) | Task 2 (`EntradaMercancia`) |
|---|---|
| `EntregaCompra` / `EntregaCompraDetalle` (tipos, DTO ns `entregaCompra`) | `EntradaMercancia` / `EntradaMercanciaDetalle` (ns `entradaMercancia`) |
| `TipoObjetoEntregaCompra` / `"12"` | `TipoObjetoEntradaMercancia` / `"59"` |
| `IEntregaCompraDomain` / `IEntregaCompraDetalleDomain` / `IEntregaCompraApplication` | `IEntradaMercanciaDomain` / `IEntradaMercanciaDetalleDomain` / `IEntradaMercanciaApplication` |
| `EntregaCompraApplication` / `EntregaCompraController` | `EntradaMercanciaApplication` / `EntradaMercanciaController` |
| campo `_repoEntregaCompra` | `_repoEntrada` |
| `ObtenerPorEntregaCompraAsync` | `ObtenerPorEntradaMercanciaAsync` |
| `RevertirAsync(TipoObjetoEntregaCompra, id)` | `RevertirAsync(TipoObjetoEntradaMercancia, id)` |
| "entregas de compra" / "entrega de compra" (mensajes, ruta `api/EntregaCompra`) | "entradas de mercancía" / "entrada de mercancía" (`api/EntradaMercancia`) |
| `EntregaCompraDomainTests` / `EntregaCompraDetalleDomainTests` | `EntradaMercanciaDomainTests` / `EntradaMercanciaDetalleDomainTests` |

### Deltas de INV-4 (lo que NO es sustitución)

**D1 — Sin socio de negocio ni impuestos en el encabezado.** El DTO y la entidad de
`EntradaMercancia` **no tienen** `CodigoSn`/`NombreSn`/`Direccion`/`MonedaDoc`/`BaseTipo`/`BaseEntry`/
`PrctjeImpuesto`/`TotalImp`/`PrctjeDesc`/`TotalDesc`/`TotalBruto`. `EntradaMercanciaCrearDTO`
tiene solo: `int? NumDoc`, `[Required] int Serie`, `string? NumManual`, `DateTime? FechaDoc`,
`DateTime? FechaContab`, `string? Referencia`, `string? Comentario`, `string? Cancelado`
(se ignora), `List<EntradaMercanciaDetalleCrearDTO> Lineas`.
`EntradaMercanciaDetalleCrearDTO`: `[Required] int Entry`, `string? CodArticulo`,
`string? Descripcion`, `decimal? Cantidad`, `decimal? CostoUnitario`, `string? CodAlmacen`.
`EntradaMercanciaActualizarDTO`: los mismos del Crear salvo `Lineas`, más nada.

**D2 — Ctor del domain con `IRepositorioGenerico<Articulo,string>`** (6ª dependencia, al final).

**D3 — `InsertarAsync` resuelve el costo por línea y calcula totales.** El canónico
`EntregaCompraDomain.InsertarAsync`, dentro de `_tx.EjecutarAsync`, hace el `foreach` de líneas
y luego el `Select` de `movimientos`. En `EntradaMercanciaDomain` el cuerpo de la transacción es:

```csharp
            return await _tx.EjecutarAsync(async () =>
            {
                await _repoEntrada.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                decimal totalDoc = 0m;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    var costo = (linea.CostoUnitario ?? 0m) > 0m
                        ? linea.CostoUnitario!.Value
                        : await CostoVigenteAsync(linea.CodArticulo);
                    linea.CostoUnitario = costo;
                    linea.TotalLinea = (linea.Cantidad ?? 0m) * costo;
                    totalDoc += linea.TotalLinea.Value;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }
                obj.TotalDoc = totalDoc;

                var movimientos = lineasList
                    .Where(l => (l.Cantidad ?? 0m) > 0m)
                    .Select(l => new MovimientoRequest(
                        TipoDoc: TipoObjetoEntradaMercancia,
                        DocEntry: obj.Entry,
                        DocLinea: l.NoLinea,
                        CodArticulo: l.CodArticulo!,
                        CodAlmacen: l.CodAlmacen!,
                        Cantidad: l.Cantidad!.Value,             // positiva = entrada
                        PrecioUnitario: l.CostoUnitario!.Value,  // costo ya resuelto
                        Fecha: obj.FechaContab ?? obj.FechaDoc ?? DateTime.Now))
                    .ToList();

                await _asiento.AsentarAsync(movimientos);

                return obj.Entry;
            });
```

Y el método privado:
```csharp
        private async Task<decimal> CostoVigenteAsync(string? codArticulo)
        {
            if (codArticulo is null) return 0m;
            var art = await _repoArticulo.ObtenerAsync(codArticulo);
            if (art is null) return 0m;
            return art.MetodoValuacion == "E" ? art.CostoEstandar : art.CostoPromedio;
        }
```

Lo de antes de la transacción (`obj.TipoObjeto = "59"; obj.EstadoInv = "A"; obj.Cancelado = "N"; obj.FechaCancelado = null;`
+ numeración) es sustitución 1:1 del canónico (el canónico ya trae `Cancelado="N"` /
`FechaCancelado=null` gracias al fix wave de INV-3... **verificar**: si el `EntregaCompraDomain`
de HEAD todavía NO los fuerza, añadirlos igual — es lección obligatoria de INV-3).

**D4 — `ActualizarAsync`, `EliminarAsync`, `EntradaMercanciaDetalleDomain`, `EntradaMercanciaApplication`,
controller** = sustitución 1:1 del canónico. `ActualizarAsync`: cancelación →
`RevertirAsync("59", id)` + flags + `Comentario` con guard `!= null`; inocua → `Comentario`
incondicional. `EliminarAsync`: lanza si `EstadoInv=="A" && Cancelado!="S"`, si no borra dentro
de `_tx`. `EntradaMercanciaDetalleDomain`: ctor `(repoDet, repoEncabezado<EntradaMercancia,int>)`,
`InsertarAsync` rechaza siempre, `Actualizar`/`Eliminar` con `LanzarSiElDocumentoExisteAsync`.
`EntradaMercanciaApplication.InsertarAsync` mapea `_mapper.Map<IEnumerable<EntradaMercanciaDetalle>>(obj.Lineas)`.

**D5 — Mapas en `PerfilMapeo.cs`.** Añadir (junto a los de `EntregaCompra*`):
`CreateMap<EntradaMercanciaCrearDTO, EntradaMercancia>();`
`CreateMap<EntradaMercanciaActualizarDTO, EntradaMercancia>();`
`CreateMap<EntradaMercancia, EntradaMercanciaDTO>();`
`CreateMap<EntradaMercanciaDetalleCrearDTO, EntradaMercanciaDetalle>();`
`CreateMap<EntradaMercanciaDetalleActualizarDTO, EntradaMercanciaDetalle>();`
`CreateMap<EntradaMercanciaDetalle, EntradaMercanciaDetalleDTO>();`

**D6 — DI en `Startup.cs`.** Añadir (donde Task 1 dejó el comentario):
```csharp
            services.AddTransient<IEntradaMercanciaDomain, EntradaMercanciaDomain>();
            services.AddTransient<IEntradaMercanciaApplication, EntradaMercanciaApplication>();
            services.AddTransient<IEntradaMercanciaDetalleDomain, EntradaMercanciaDetalleDomain>();
            services.AddTransient<IEntradaMercanciaDetalleApplication, EntradaMercanciaDetalleApplication>();
```

### Tests (`EntradaMercanciaDomainTests.cs`)

Tomar `EntregaCompraDomainTests.cs` (HEAD) + sustitución, y añadir el mock de
`IRepositorioGenerico<Articulo,string>` (`_repoArticulo`) al ctor. Ajustes:
- El helper `Linea(art, alm, cant, precio)` del canónico → `Linea(string art, string alm, decimal? cant, decimal? costo)` que setea `CostoUnitario = costo` (no `Precio`).
- `SerieAuto` con `CodigoObj = "59"`.
- El test `InsertarAsync_ConLineas_...`: assert del `MovimientoRequest` → `TipoDoc == "59"`,
  `Cantidad` positiva, `PrecioUnitario == <costo>`; + `Assert.Equal("59", obj.TipoObjeto)`;
  + `Assert.Equal(<Σ cant·costo>, obj.TotalDoc)`.
- `RevertirAsync("59", 7)` en el test de cancelación.
- **Tests nuevos de costo** (D3):
```csharp
        [Fact]
        public async Task InsertarAsync_LineaConCostoExplicito_UsaEseCosto()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new EntradaMercancia { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 10m, 15m) });

            Assert.Equal(15m, _movimientosAsentados[0].PrecioUnitario);
            Assert.Equal(150m, obj.TotalDoc);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinCosto_UsaCostoPromedioDelArticulo()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            _repoArticulo.Setup(r => r.ObtenerAsync("ART1"))
                .ReturnsAsync(new Articulo { Codigo = "ART1", MetodoValuacion = "P", CostoPromedio = 22m, CostoEstandar = 99m });
            var obj = new EntradaMercancia { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 10m, null) });

            Assert.Equal(22m, _movimientosAsentados[0].PrecioUnitario);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinCosto_MetodoEstandar_UsaCostoEstandar()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            _repoArticulo.Setup(r => r.ObtenerAsync("ART1"))
                .ReturnsAsync(new Articulo { Codigo = "ART1", MetodoValuacion = "E", CostoPromedio = 22m, CostoEstandar = 30m });
            var obj = new EntradaMercancia { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 10m, 0m) });

            Assert.Equal(30m, _movimientosAsentados[0].PrecioUnitario);
        }
```
- El resto de los tests del canónico (serie, cancelar/recancelar, edición inocua incl. null
  borra, eliminar) se transforman 1:1. Añadir también
  `InsertarAsync_ConCanceladoEnviadoPorElCliente_LoIgnora` (lección I-1 de INV-3): pasa
  `Cancelado="S"`, assert `obj.Cancelado == "N"` y `obj.FechaCancelado == null`.

`EntradaMercanciaDetalleDomainTests.cs` = `EntregaCompraDetalleDomainTests.cs` sustituido (4
tests, ctor `new EntradaMercanciaDetalleDomain(_repoDet.Object, _repoHeader.Object)`).

### Steps

- [ ] **Step 1:** DTOs (`entradaMercancia/` + `entradaMercanciaDetalle/`) con los campos de D1.
- [ ] **Step 2:** Interfaces `IEntradaMercanciaDomain` (con `InsertarAsync` de 2 args) + `IEntradaMercanciaDetalleDomain` + las 2 de Application.
- [ ] **Step 3:** `EntradaMercanciaDomain.cs` — plantilla `EntregaCompraDomain` sustituida + D2 (ctor +`_repoArticulo`) + D3 (cuerpo de `InsertarAsync` + `CostoVigenteAsync`). Verificar/forzar `Cancelado="N"`+`FechaCancelado=null` en `InsertarAsync`.
- [ ] **Step 4:** `EntradaMercanciaDetalleDomain.cs` — plantilla sustituida.
- [ ] **Step 5:** `EntradaMercanciaApplication.cs` + `EntradaMercanciaDetalleApplication.cs` — plantilla sustituida (Insertar mapea `Lineas`).
- [ ] **Step 6:** `PerfilMapeo.cs` — D5.
- [ ] **Step 7:** `EntradaMercanciaController.cs` + `EntradaMercanciaDetalleController.cs` — plantilla sustituida (`[Route("api/EntradaMercancia")]`).
- [ ] **Step 8:** `Startup.cs` — D6.
- [ ] **Step 9:** `EntradaMercanciaDomainTests.cs` + `EntradaMercanciaDetalleDomainTests.cs`.
- [ ] **Step 10: Build** — `dotnet build API.sln -p:BaseOutputPath=".../apibuild/"` → `0 Errores`.
- [ ] **Step 11: Suite completa** — `dotnet test API.sln -p:BaseOutputPath=".../apitest/"` → **0 fallos**; los nuevos de `EntradaMercancia` en verde.
- [ ] **Step 12: Commit**
```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Domain.Interface/IEntradaMercancia*.cs API.Domain.Core/EntradaMercancia*.cs API.Application.Interface/IEntradaMercancia*.cs API.Application.Main/EntradaMercancia*.cs API.Application.DTO/entradaMercancia/ API.Application.DTO/entradaMercanciaDetalle/ API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Controllers/EntradaMercancia*.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/EntradaMercancia*.cs
git commit -m "feat(api): EntradaMercancia asienta inventario al registrar (costo por linea con fallback al costo vigente) y lo revierte al cancelar"
```

---

## Task 3: `SalidaMercanciaDomain` — transformación de Task 2 + 3 deltas

**Files:** los `SalidaMercancia*` equivalentes de todos los `Create` de Task 2 + Modify `PerfilMapeo.cs`, `Startup.cs`.

**Interfaces:**
- Produces: `ISalidaMercanciaDomain.InsertarAsync(SalidaMercancia obj, IEnumerable<SalidaMercanciaDetalle> lineas) -> Task<int>`.

### Precondición

`Task 2` dejó `EntradaMercancia*` en el estado destino. Transformar esos archivos (HEAD) a
`SalidaMercancia*`.

### Tabla de sustitución (Task 2 → Task 3)

| Task 2 (`EntradaMercancia`) | Task 3 (`SalidaMercancia`) |
|---|---|
| `EntradaMercancia` / `EntradaMercanciaDetalle` (tipos, DTO ns `entradaMercancia`) | `SalidaMercancia` / `SalidaMercanciaDetalle` (ns `salidaMercancia`) |
| `TipoObjetoEntradaMercancia` / `"59"` | `TipoObjetoSalidaMercancia` / `"60"` |
| `I*EntradaMercancia*` | `I*SalidaMercancia*` |
| `_repoEntrada` | `_repoSalida` |
| `ObtenerPorEntradaMercanciaAsync` | `ObtenerPorSalidaMercanciaAsync` |
| `RevertirAsync(TipoObjeto*, id)` | idem con `TipoObjetoSalidaMercancia` |
| "entrada(s) de mercancía" (mensajes, `api/EntradaMercancia`) | "salida(s) de mercancía" (`api/SalidaMercancia`) |
| `EntradaMercanciaDomainTests` / `EntradaMercanciaDetalleDomainTests` | `SalidaMercanciaDomainTests` / `SalidaMercanciaDetalleDomainTests` |
| `SerieAuto` `CodigoObj = "59"` + asserts `"59"` | `"60"` |

### Deltas (lo que NO es sustitución de Task 2)

**Delta A — costo siempre del artículo.** En `SalidaMercanciaDomain.InsertarAsync`, dentro del
`foreach`, la resolución de costo NO consulta `linea.CostoUnitario`:
```csharp
                    var costo = await CostoVigenteAsync(linea.CodArticulo);
```
(en Task 2 la línea era `var costo = (linea.CostoUnitario ?? 0m) > 0m ? linea.CostoUnitario!.Value : await CostoVigenteAsync(linea.CodArticulo);`).
Todo lo demás del `foreach` (`linea.CostoUnitario = costo; linea.TotalLinea = ...; totalDoc += ...`)
igual. `CostoVigenteAsync` queda idéntico.

**Delta B — signo.** En el `Select` de `movimientos`:
```csharp
                        Cantidad: -(l.Cantidad!.Value),   // negativo = salida
```
`TipoDoc: TipoObjetoSalidaMercancia`. `await _asiento.AsentarAsync(movimientos);` **sin segundo
argumento** → `permitirNegativo` default `false` → bloqueo duro; si alguna línea deja
`Disponible < 0` lanza `StockInsuficienteException` y `EjecutarAsync` hace rollback total.

**Delta C — tests.** En `SalidaMercanciaDomainTests`:
- `InsertarAsync_StockInsuficiente_Propaga`:
```csharp
        [Fact]
        public async Task InsertarAsync_StockInsuficiente_Propaga()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            _repoArticulo.Setup(r => r.ObtenerAsync("ART1"))
                .ReturnsAsync(new Articulo { Codigo = "ART1", MetodoValuacion = "P", CostoPromedio = 20m });
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .ThrowsAsync(new StockInsuficienteException("ART1", "01", 3m, 10m));

            await Assert.ThrowsAsync<StockInsuficienteException>(
                () => _domain.InsertarAsync(new SalidaMercancia { Serie = 4 }, new[] { Linea("ART1", "01", 10m, null) }));
        }
```
(`using API.Domain.Core.Inventario;` en el archivo.)
- Reemplazar los 3 tests de costo de Task 2 por uno solo:
```csharp
        [Fact]
        public async Task InsertarAsync_IgnoraElCostoDelClienteYUsaElCostoVigente()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            _repoArticulo.Setup(r => r.ObtenerAsync("ART1"))
                .ReturnsAsync(new Articulo { Codigo = "ART1", MetodoValuacion = "P", CostoPromedio = 18m });
            var obj = new SalidaMercancia { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 5m, 999m) });   // costo cliente ignorado

            Assert.Equal(18m, _movimientosAsentados[0].PrecioUnitario);
            Assert.Equal(-5m, _movimientosAsentados[0].Cantidad);
            Assert.Equal(90m, obj.TotalDoc);
        }
```

### Steps

- [ ] **Step 1-8:** crear cada `SalidaMercancia*` = el `EntradaMercancia*` de Task 2 (HEAD) sustituido; aplicar Delta A en `SalidaMercanciaDomain.InsertarAsync`, Delta B en el `Select`.
- [ ] **Step 9:** `PerfilMapeo.cs` — añadir los 6 `CreateMap` de `SalidaMercancia*` (transformación de los de Task 2).
- [ ] **Step 10:** `Startup.cs` — 4 repos ya registrados en Task 1; añadir los 4 `IEntradaMercancia*`→`ISalidaMercancia*` domain/application `AddTransient`.
- [ ] **Step 11:** `SalidaMercanciaDomainTests.cs` + `SalidaMercanciaDetalleDomainTests.cs` = transformación de los de Task 2 + Delta C (quita los 3 tests de costo de entrada, mete `_StockInsuficiente_Propaga` y `_IgnoraElCostoDelClienteYUsaElCostoVigente`).
- [ ] **Step 12: Build** → `0 Errores`.
- [ ] **Step 13: Suite completa** → **0 fallos**; nuevos de `SalidaMercancia` en verde.
- [ ] **Step 14: Commit**
```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Domain.Interface/ISalidaMercancia*.cs API.Domain.Core/SalidaMercancia*.cs API.Application.Interface/ISalidaMercancia*.cs API.Application.Main/SalidaMercancia*.cs API.Application.DTO/salidaMercancia/ API.Application.DTO/salidaMercanciaDetalle/ API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Controllers/SalidaMercancia*.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/SalidaMercancia*.cs
git commit -m "feat(api): SalidaMercancia descuenta inventario al registrar (costo promedio movil, bloqueo de negativo) y lo reingresa al cancelar"
```

---

## Task 4: Web — `EntradasMercancia`

**Files:**
- Create: `Web.ApiClient/Dtos/EntradaMercancia/` (`EntradaMercanciaCrearDTO.cs`, `EntradaMercanciaActualizarDTO.cs`, `EntradaMercanciaDTO.cs`) + `Web.ApiClient/Dtos/EntradaMercanciaDetalle/` (`*CrearDTO.cs`, `*DTO.cs`)
- Create: `Web.ApiClient/Clientes/IEntradaMercanciaApiClient.cs`, `EntradaMercanciaApiClient.cs`, `IEntradaMercanciaDetalleApiClient.cs`, `EntradaMercanciaDetalleApiClient.cs`
- Create: `Web.UI/Controllers/EntradasMercanciaController.cs`
- Create: `Web.UI/wwwroot/js/entradasmercancia.js`
- Create: `Web.UI/Views/EntradasMercancia/Index.cshtml`, `_Form.cshtml`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml` (submenú Inventario)
- Modify: `Web.UI/Program.cs` si el patrón exige registrar los `HttpClient` tipados (mirar cómo se registra `EntregaCompraApiClient`)

**Interfaces:**
- Consumes: `api/EntradaMercancia` (Task 2) — `Crear` acepta `dto.Lineas`; `Editar` reacciona a `Cancelado='S'`.

### Plantillas (Web, HEAD de `main`)

`Web.ApiClient/Dtos/EntregaCompra/*`, `Web.ApiClient/Dtos/EntregaCompraDetalle/*`,
`Web.ApiClient/Clientes/{I,}EntregaCompraApiClient.cs` (+ Detalle),
`Web.UI/Controllers/EntregasCompraController.cs`, `Web.UI/wwwroot/js/entregascompra.js`,
`Web.UI/Views/EntregasCompra/Index.cshtml` + `_Form.cshtml`.

### Tabla de sustitución

| Plantilla (`EntregaCompra` / `EntregasCompra`) | Task 4 (`EntradaMercancia` / `EntradasMercancia`) |
|---|---|
| `EntregasCompra` (controller, `/EntregasCompra/...`, `#tblEntregasCompra`, `#tblDetalleEntregaCompra`) | `EntradasMercancia` (`/EntradasMercancia/...`, `#tblEntradasMercancia`, `#tblDetalleEntradaMercancia`) |
| `entregascompra` (js, `datosSeriesEntregaCompra`, `selectSerieEntregaCompra`) | `entradasmercancia` (`datosSeriesEntradaMercancia`, `selectSerieEntradaMercancia`) |
| `EntregaCompra` / `EntregaCompraDetalle` (tipos DTO), `entregaCompra` (ns) | `EntradaMercancia` / `EntradaMercanciaDetalle`, `entradaMercancia` |
| `#btnGuardarEntregaCompra` / `#btnCancelarDocEntregaCompra` | `#btnGuardarEntradaMercancia` / `#btnCancelarDocEntradaMercancia` |
| `CodigoObjEntregaCompra = "12"` | `CodigoObjEntradaMercancia = "59"` |
| "entrega de compra" / "entregas de compra" (textos UI) | "entrada de mercancía" / "entradas de mercancía" |

### Deltas de INV-4 (recorte)

**W1 — Sin socio, sin moneda, sin impuesto.** El `EntradasMercanciaController` **no** inyecta
`ISocioNegocioApiClient`, `IMonedaApiClient`, `IImpuestoApiClient` y **no** expone
`BuscarSocios` / `BuscarImpuestos`. `CargarDropdownsAsync` (si existe) pierde socios/monedas.
El `_Form.cshtml` **no** tiene el bloque de socio de negocio, dirección, moneda, ni la columna
de impuesto/descuento en el grid de líneas. El JS **no** inicializa `buscadorSocio` ni
`buscadorImpuesto`; `inicializarDetalle` solo crea `buscadorArticulo` y `buscadorAlmacen`.

**W2 — Campos del encabezado.** El `_Form.cshtml` del encabezado: Serie (select), `FechaDoc`,
`FechaContab`, `Referencia`, `Comentario`. En edición todos read-only salvo `Comentario`
(patrón de `Serie` del canónico). Botón "Cancelar documento" gated
`esEdicion && (Model.Cancelado ?? "N") != "S"`; "Guardar" oculto si `Cancelado == "S"`;
"Agregar línea" en `@if (!esEdicion)`.

**W3 — Panel de línea y totales.** El panel de línea tiene **artículo, almacén, cantidad,
costo unitario** (sin `PrctjeDesc`, sin impuesto). Al elegir artículo: autocompleta
`Descripcion` y setea `#detCostoUnitario` = `CostoPromedio` del artículo (editable — usar el
campo que devuelva `BuscarArticulos`; si no devuelve costo, dejar 0). `recalcularLinea` /
`calcularTotalesDesdeLineas`: `TotalLinea = Cantidad · CostoUnitario`, `TotalDoc = Σ`. El
`datos.Lineas` que se postea lleva `{ Entry, CodArticulo, Descripcion, Cantidad, CostoUnitario, CodAlmacen }`
(sin `Precio`, `PrctjeDesc`, `CodigoImpuesto`, `Impuesto`).

**W4 — Guards cliente y cancelar** (del fix wave de INV-2, se conservan): ≥1 línea; toda línea
con `Cantidad > 0` necesita `CodAlmacen`; handler `#btnCancelarDocEntradaMercancia` con
`$btn.prop('disabled', true)` + `finally`; badge `row.cancelado === 'S'` en la lista;
`pintarDetalle()` sin botones por fila en edición; alta en **una** petición
`POST /EntradasMercancia/Crear`.

**W5 — Submenú.** En `_Layout.cshtml`, dentro de `#submenuInventario`, añadir
`<a class="dropdown-item" asp-controller="EntradasMercancia" asp-action="Index">Entradas de mercancía</a>`
y la de `SalidasMercancia` (esta última la usa Task 5 pero se añaden juntas acá para no tocar
`_Layout` dos veces — anotarlo).

### Steps

- [ ] **Step 1:** DTOs Web (`EntradaMercancia/` + `EntradaMercanciaDetalle/`) con los campos de W1/W3. `EntradaMercanciaCrearDTO` con `List<EntradaMercanciaDetalleCrearDTO> Lineas`. `EntradaMercanciaActualizarDTO` con `Cancelado`.
- [ ] **Step 2:** Api-clients (`IEntradaMercanciaApiClient` + impl con `Recurso = "api/EntradaMercancia"`, + Detalle) = plantilla `EntregaCompraApiClient` sustituida.
- [ ] **Step 3:** `EntradasMercanciaController.cs` = plantilla `EntregasCompraController` sustituida + recorte W1. Registrar los `HttpClient` tipados en `Program.cs` si el patrón lo pide.
- [ ] **Step 4:** `entradasmercancia.js` = `entregascompra.js` (HEAD) sustituido + recorte W1 + W3 (panel de línea con costo, sin impuesto/desc) + W4.
- [ ] **Step 5:** `Views/EntradasMercancia/Index.cshtml` + `_Form.cshtml` = plantilla sustituida + W1/W2/W3.
- [ ] **Step 6:** `_Layout.cshtml` — W5 (añadir los 2 `<a>`).
- [ ] **Step 7: Build Web** — `dotnet build Web.slnx -p:BaseOutputPath=".../webbuild/"` → `0 Errores`.
- [ ] **Step 8: Commit**
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/Dtos/EntradaMercancia/ Web.ApiClient/Dtos/EntradaMercanciaDetalle/ Web.ApiClient/Clientes/*EntradaMercancia*.cs Web.UI/Controllers/EntradasMercanciaController.cs Web.UI/wwwroot/js/entradasmercancia.js Web.UI/Views/EntradasMercancia/ Web.UI/Views/Shared/_Layout.cshtml Web.UI/Program.cs
git commit -m "feat(web): pantalla de Entradas de mercancia (crear con lineas embebidas, cancelar, editar solo comentario)"
```

---

## Task 5: Web — `SalidasMercancia` (transformación de Task 4)

**Files:** los `SalidasMercancia*` equivalentes de todos los `Create` de Task 4 (sin volver a tocar `_Layout.cshtml` — Task 4 ya añadió su `<a>`).

### Precondición

Task 4 dejó `EntradasMercancia*` en el estado destino + el `<a>` de `SalidasMercancia` ya en
`_Layout`. Transformar los archivos `EntradasMercancia*` (HEAD) a `SalidasMercancia*`.

### Tabla de sustitución

| Task 4 (`EntradasMercancia`) | Task 5 (`SalidasMercancia`) |
|---|---|
| `EntradasMercancia` (`/EntradasMercancia/...`, `#tblEntradasMercancia`, `#tblDetalleEntradaMercancia`) | `SalidasMercancia` (`/SalidasMercancia/...`, `#tblSalidasMercancia`, `#tblDetalleSalidaMercancia`) |
| `entradasmercancia` (js, `datosSeriesEntradaMercancia`, `selectSerieEntradaMercancia`) | `salidasmercancia` (`datosSeriesSalidaMercancia`, `selectSerieSalidaMercancia`) |
| `EntradaMercancia` / `EntradaMercanciaDetalle` (tipos DTO), `entradaMercancia` (ns) | `SalidaMercancia` / `SalidaMercanciaDetalle`, `salidaMercancia` |
| `#btnGuardarEntradaMercancia` / `#btnCancelarDocEntradaMercancia` | `#btnGuardarSalidaMercancia` / `#btnCancelarDocSalidaMercancia` |
| `CodigoObjEntradaMercancia = "59"` | `CodigoObjSalidaMercancia = "60"` |
| `Recurso = "api/EntradaMercancia"` | `"api/SalidaMercancia"` |
| "entrada de mercancía" / "entradas de mercancía" (textos UI) | "salida de mercancía" / "salidas de mercancía" |

### Delta de INV-4

**S1 — El panel de línea de la Salida no pide `CostoUnitario`.** El server lo calcula. En
`salidasmercancia.js`: al elegir artículo, `#detCostoUnitario` (si se deja en el `_Form`) se
setea con el `CostoPromedio` como **informativo y `readonly`**, o se elimina el input del panel.
El `datos.Lineas` posteado NO necesita `CostoUnitario` (el server lo ignora). `calcularTotalesDesdeLineas`
puede seguir mostrando `Σ Cantidad·costoInformativo` o solo cantidades — el `TotalDoc` real lo
pone el server. Documentar cuál se eligió.
Todo lo demás (guards, cancelar, badge, `_Form` read-only en edición) = transformación 1:1.

### Steps

- [ ] **Step 1-6:** crear cada `SalidasMercancia*` = el `EntradasMercancia*` de Task 4 (HEAD) sustituido; aplicar S1 en `salidasmercancia.js` + `_Form.cshtml`.
- [ ] **Step 7: Build Web** → `0 Errores`.
- [ ] **Step 8: Commit**
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/Dtos/SalidaMercancia/ Web.ApiClient/Dtos/SalidaMercanciaDetalle/ Web.ApiClient/Clientes/*SalidaMercancia*.cs Web.UI/Controllers/SalidasMercanciaController.cs Web.UI/wwwroot/js/salidasmercancia.js Web.UI/Views/SalidasMercancia/ Web.UI/Program.cs
git commit -m "feat(web): pantalla de Salidas de mercancia (transformacion de Entradas; el costo lo calcula el servidor)"
```

---

## Task 6: Verificación final conjunta

**Files:** ninguno nuevo.

- [ ] **Step 1: Build API** — `dotnet build API.sln -p:BaseOutputPath=".../apibuild/"` → `0 Errores`.
- [ ] **Step 2: Suite API completa** — `dotnet test API.sln -p:BaseOutputPath=".../apitest/"` → **0 fallos**.
- [ ] **Step 3: Build Web** — `dotnet build Web.slnx -p:BaseOutputPath=".../webbuild/"` → `0 Errores`.
- [ ] **Step 4: Prueba manual en el navegador (para el usuario)**

Aplicar primero `API/sql/2026-09-01-inventario-mercancias.sql` en `API_DB_TEST`. Reiniciar las
sesiones de depuración de VS. Necesita un artículo con `ArticuloInventario='S'` y un almacén.

1. **Entrada de Mercancías**: menú Inventario → Entradas de mercancía → Nuevo. Serie por
   defecto, 2 líneas del mismo artículo — una con costo tecleado (p.ej. 25), otra con costo
   vacío → Guardar.
   - Existencias: el disponible del almacén subió por la suma de las 2 cantidades.
   - Kardex (`TipoDoc=59`): 2 movimientos; el de costo vacío entró al `CostoPromedio` vigente
     del artículo (no distorsionó la valuación); `Articulo.CostoPromedio` re-ponderado con el
     de costo 25.
   - `EntradaMercancia.TotalDoc` = suma de `Cantidad·CostoUnitario` (con el costo resuelto).
2. Editar esa entrada: cambiar el comentario → se guarda; vaciarlo → queda vacío. Encabezado
   read-only, sin "Agregar línea", sin botones por fila.
3. "Cancelar documento" → confirmar. Kardex: movimientos inversos; disponible vuelve al valor
   previo; `EstadoInv='C'`; badge rojo "Cancelado" en la lista. Intentar eliminar una entrada
   asentada no cancelada → error "Cancele el documento…". Eliminar una cancelada → se borra.
3b. **Rollback**: crear una Entrada con un `CodAlmacen` inexistente (vía Swagger si la UI no lo
   permite) → error y **no** queda documento, ni líneas, ni movimientos, ni avanza `SigNumero`.
4. **Salida de Mercancías** (`TipoDoc=60`): crear con 2 líneas dentro del stock disponible →
   Guardar. Disponible baja; kardex con `CantidadSale`>0 y `CostoUnitario` = costo promedio
   (COGS). Crear una Salida con cantidad **mayor** al disponible → error "stock insuficiente" +
   rollback (nada persiste, `SigNumero` intacto). Cancelar una Salida → el stock reingresa.
5. **I-3** (heredado): antes de todo, verificar que `Articulo.CantDisponible` del artículo de
   prueba **no es NULL** y coincide con la suma de sus `ExistenciaArticulo.Disponible`; si no,
   la primera Salida puede dejar `ValorInventario` negativo.

- [ ] **Step 5: Recordatorio para el usuario**

Imprimir:
- Aplicar `API/sql/2026-09-01-inventario-mercancias.sql` en `API_DB_TEST` (crea 4 tablas + 2
  series). Reiniciar depuración de VS (API y Web.UI).
- Ajustar las series de `'59'`/`'60'` en "Numeración de documentos" si se quiere otra
  numeración inicial.
- Deuda conocida: mismo I-3 de INV-3 (guard de negativo por almacén vs valuación global);
  `EjecutorTransaccion` sin test automatizado del path real; re-mezcla del promedio al cancelar
  una Salida (benigna); documentos previos no se reprocesan.
- Fase INV siguiente (si se decide): traslados entre almacenes, o el flujo de conteo físico.

- [ ] **Step 6: Commit final (si quedó algo suelto)**
```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add docs/ && git commit -m "chore: cierre INV-4" || echo "nada que commitear"
```

---

## Notas de auto-revisión (cobertura del spec)

- **§1 Modelo de datos** (SQL + entidades + `OnModelCreating` + repos + DI) → **Task 1**.
- **§2 `EntradaMercanciaDomain`**: DTO sin socio/impuesto, `InsertarAsync(obj, lineas)` atómico,
  `TipoObjeto="59"` + `EstadoInv="A"` + `Cancelado="N"`, resolución de costo (`CostoVigenteAsync`
  con fallback `MetodoValuacion=="E" ? CostoEstandar : CostoPromedio`), `TotalLinea`/`TotalDoc`
  server-side, `MovimientoRequest` positiva, cancelación → `RevertirAsync("59",id)`, edición
  inocua solo `Comentario`, `Eliminar` atómico, guardas en el detalle domain, Application +
  Controller + mapas → **Task 2**.
- **§3 `SalidaMercanciaDomain`**: transformación + costo siempre del artículo + `Cantidad`
  negativa + `permitirNegativo:false` + test de stock insuficiente → **Task 3**.
- **§4 Web**: 2 pantallas nuevas, patrón post-fix-wave, sin socio/impuesto, con costo (Entrada)
  / costo informativo (Salida), submenú Inventario → **Tasks 4 y 5**.
- **§5 Semántica** (cancelación, edición, eliminar, errores tipados, rollback total) → cubierta
  por la transformación en Tasks 2-3 + verificación Task 6.
- **§6 Pruebas** (`*DomainTests` con mock de `IRepositorioGenerico<Articulo>`, tests de costo,
  stock insuficiente, cancelar/recancelar, edición inocua, eliminar; `*DetalleDomainTests`;
  esquema en Task 1) → **Tasks 1-3**; verificación conjunta + manual → **Task 6**.
- **Riesgos** (`EjecutorTransaccion` sin test unitario → Task 6 manual; I-3 heredado; lectura
  extra de `Articulo`; re-mezcla del promedio) → anotados.
- **Fuera de alcance** (conteo físico, traslados, chaining, reintento, descancelar, editar
  líneas, pantalla de `MetodoValuacion`) → Global Constraints; sin tareas.
