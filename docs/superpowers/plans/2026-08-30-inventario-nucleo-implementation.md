# Núcleo de inventario multi-almacén (INV-1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir el núcleo del subsistema de inventario: tablas de existencias por almacén y kardex, motor de valuación puro (promedio móvil + estándar), servicio de asiento atómico `IInventarioAsientoService`, y API + Web de consulta — sin engancharse todavía a ningún documento.

**Architecture:** API N-capas (.NET 7): Entity → Infraestructure.Repository (repositorio genérico) → Domain.Core → Application.Main → Service.WebApi. El servicio de asiento muta `ExistenciaArticulo` / `Articulo` / `MovimientoInventario` en el `ChangeTracker` del `ApiDbTestContext` scoped y **no** llama `SaveChangesAsync` — lo hace el caller (mismo patrón que la numeración con `serie.SigNumero`). El motor de valuación es una función pura sin I/O. Web (.NET 8 MVC): pantalla de consulta de existencias + kardex.

**Tech Stack:** C# / .NET 7 (API) y .NET 8 (Web), Entity Framework Core (SQL Server), AutoMapper, xUnit + Moq (pruebas API), jQuery + DataTables + Bootstrap (Web).

**Spec:** `API/docs/superpowers/specs/2026-08-30-inventario-nucleo-design.md`

## Global Constraints

- **Repos y ramas:** API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`); Web en `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad de git ya configurada (`panchoman08`). Sin push hasta aprobación final del usuario.
- **Build sin chocar con Visual Studio:** compilar/probar a carpeta externa con `-p:BaseOutputPath=`. Rutas: `C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\` y `...\apitest\` y `...\webbuild\`.
- **No hay .NET 7 SDK**; el SDK 9/10 compila `net7.0`. No añadir `global.json`.
- **`dotnet test` de la suite completa de la API en verde** antes de terminar cualquier tarea que toque la API. Baseline actual: **619 pruebas, 0 fallos**.
- **BD local:** `sqlcmd -S localhost -U sa -P '#Integra1' -d API_DB_TEST -C`. El connection string real vive en User Secrets; `appsettings.json` puede aparecer modificado localmente con el connstring — **nunca commitearlo**.
- **No hay módulo de contabilidad.** La `VariacionPrecio` del costo estándar solo se registra en el kardex, no se contabiliza.
- **Métodos de valuación:** `'P'` promedio móvil (default), `'E'` estándar. Sin FIFO.
- **Granularidad:** cantidad por `(CodArticulo, CodAlmacen)`; costo promedio y valor a nivel artículo.
- **Nombres exactos de BD:**
  - Tablas: `ExistenciaArticulo`, `MovimientoInventario`.
  - PK: `pk_existencia_articulo (CodArticulo, CodAlmacen)`, `pk_movimiento_inventario (Entry)`.
  - FK: `fk_existencia_articulo` → `Articulo.Codigo`, `fk_existencia_almacen` → `Almacen.Codigo`, `fk_movimiento_articulo` → `Articulo.Codigo`, `fk_movimiento_almacen` → `Almacen.Codigo`, `fk_movimiento_reversa` → `MovimientoInventario.Entry` (self, opcional).
  - Índices: `ix_movimiento_articulo_fecha (CodArticulo, Fecha, Entry)`, `ix_movimiento_origen (TipoDoc, DocEntry)`.
  - Columnas nuevas en `Articulo`: `MetodoValuacion nvarchar(1)` default `'P'` CHECK `IN ('P','E')`, `CostoPromedio decimal(19,6)` default `0`, `CostoEstandar decimal(19,6)` default `0`, `ValorInventario decimal(19,6)` default `0`.
- **Contrato del servicio de asiento:** `AsentarAsync` / `RevertirAsync` **nunca** llaman `SaveChangesAsync` ni `_repo.InsertarAsync/ActualizarAsync/EliminarAsync` (esos guardan). Solo leen (entidades rastreadas) y usan `AgregarSinGuardarAsync` para filas nuevas.
- **Fuera de alcance:** enganche a documentos, stock negativo real en salidas, edición de `MetodoValuacion`/`CostoEstandar` por pantalla, traslados, documentos Entrada/Salida de mercancías, reserva de stock (`Comprometido`/`Pedido` quedan en 0), reintento por `DbUpdateConcurrencyException`.

---

## Task 1: DDL de las tablas de inventario

**Files:**
- Create: `API/sql/2026-08-30-inventario-nucleo.sql`

**Interfaces:**
- Consumes: nada.
- Produces: en `API_DB_TEST`, las tablas `ExistenciaArticulo` y `MovimientoInventario`, y 4 columnas nuevas en `Articulo`.

- [ ] **Step 1: Crear el script SQL**

Crear `API/sql/2026-08-30-inventario-nucleo.sql`:

```sql
-- INV-1: nucleo de inventario multi-almacen.
-- Idempotente: cada objeto se crea solo si no existe.
SET NOCOUNT ON;

-- ===== ExistenciaArticulo: cantidad por (articulo, almacen) =====
IF OBJECT_ID('dbo.ExistenciaArticulo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExistenciaArticulo (
        CodArticulo        nvarchar(15)  NOT NULL,
        CodAlmacen         nvarchar(8)   NOT NULL,
        Disponible         decimal(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Disponible   DEFAULT (0),
        Comprometido       decimal(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Comprometido DEFAULT (0),
        Pedido             decimal(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Pedido       DEFAULT (0),
        FechaActualizacion datetime      NOT NULL CONSTRAINT DF_ExistenciaArticulo_Fecha        DEFAULT (getdate()),
        RowVersion         rowversion    NOT NULL,
        CONSTRAINT pk_existencia_articulo PRIMARY KEY (CodArticulo, CodAlmacen),
        CONSTRAINT fk_existencia_articulo FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_existencia_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo)
    );
END

-- ===== MovimientoInventario: kardex append-only =====
IF OBJECT_ID('dbo.MovimientoInventario', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MovimientoInventario (
        Entry              int identity(1,1) NOT NULL,
        TipoDoc            nvarchar(20)  NOT NULL,
        DocEntry           int           NOT NULL,
        DocLinea           int           NOT NULL,
        CodArticulo        nvarchar(15)  NOT NULL,
        CodAlmacen         nvarchar(8)   NOT NULL,
        Fecha              datetime      NOT NULL,
        CantidadEntra      decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_CantEntra DEFAULT (0),
        CantidadSale       decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_CantSale  DEFAULT (0),
        PrecioUnitario     decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Precio    DEFAULT (0),
        CostoUnitario      decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Costo     DEFAULT (0),
        ValorMovimiento    decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Valor     DEFAULT (0),
        VariacionPrecio    decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Variacion DEFAULT (0),
        SaldoCantidad      decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoCant DEFAULT (0),
        SaldoCostoPromedio decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoCP   DEFAULT (0),
        SaldoValor         decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoVal  DEFAULT (0),
        MovReversaDe       int           NULL,
        CONSTRAINT pk_movimiento_inventario PRIMARY KEY (Entry),
        CONSTRAINT fk_movimiento_articulo FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_movimiento_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo),
        CONSTRAINT fk_movimiento_reversa  FOREIGN KEY (MovReversaDe) REFERENCES dbo.MovimientoInventario(Entry)
    );
    CREATE INDEX ix_movimiento_articulo_fecha ON dbo.MovimientoInventario (CodArticulo, Fecha, Entry);
    CREATE INDEX ix_movimiento_origen         ON dbo.MovimientoInventario (TipoDoc, DocEntry);
END

-- ===== Columnas nuevas en Articulo =====
IF COL_LENGTH('dbo.Articulo', 'MetodoValuacion') IS NULL
    ALTER TABLE dbo.Articulo ADD MetodoValuacion nvarchar(1) NOT NULL
        CONSTRAINT DF_Articulo_MetodoValuacion DEFAULT ('P')
        CONSTRAINT CK_Articulo_MetodoValuacion CHECK (MetodoValuacion IN ('P','E'));
IF COL_LENGTH('dbo.Articulo', 'CostoPromedio') IS NULL
    ALTER TABLE dbo.Articulo ADD CostoPromedio decimal(19,6) NOT NULL CONSTRAINT DF_Articulo_CostoPromedio DEFAULT (0);
IF COL_LENGTH('dbo.Articulo', 'CostoEstandar') IS NULL
    ALTER TABLE dbo.Articulo ADD CostoEstandar decimal(19,6) NOT NULL CONSTRAINT DF_Articulo_CostoEstandar DEFAULT (0);
IF COL_LENGTH('dbo.Articulo', 'ValorInventario') IS NULL
    ALTER TABLE dbo.Articulo ADD ValorInventario decimal(19,6) NOT NULL CONSTRAINT DF_Articulo_ValorInventario DEFAULT (0);

PRINT 'INV-1 DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('ExistenciaArticulo','MovimientoInventario') ORDER BY name;
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_NAME='Articulo' AND COLUMN_NAME IN ('MetodoValuacion','CostoPromedio','CostoEstandar','ValorInventario')
 ORDER BY COLUMN_NAME;
```

- [ ] **Step 2: Aplicar el script**

Run:
```bash
sqlcmd -S localhost -U sa -P '#Integra1' -d API_DB_TEST -C -i "C:/Users/migue/source/repos/angelm0508/API/sql/2026-08-30-inventario-nucleo.sql"
```
Expected: `INV-1 DDL aplicado.`; luego lista `ExistenciaArticulo`, `MovimientoInventario`; luego las 4 columnas de `Articulo`.

- [ ] **Step 3: Verificar estructura**

Run:
```bash
sqlcmd -S localhost -U sa -P '#Integra1' -d API_DB_TEST -C -W -Q "SET NOCOUNT ON; SELECT c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_NAME IN ('ExistenciaArticulo','MovimientoInventario') ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;"
```
Expected: `ExistenciaArticulo` con 7 columnas (incluye `RowVersion` timestamp); `MovimientoInventario` con 18 columnas, `Entry` int, `MovReversaDe` nullable.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add sql/2026-08-30-inventario-nucleo.sql
git commit -m "feat(db): tablas de inventario ExistenciaArticulo y MovimientoInventario + columnas de valuacion en Articulo"
```

---

## Task 2: Entidades EF, mapeo del contexto, repositorios y `AgregarSinGuardarAsync`

**Files:**
- Create: `API.Domain.Entity/Models/ExistenciaArticulo.cs`
- Create: `API.Domain.Entity/Models/MovimientoInventario.cs`
- Modify: `API.Domain.Entity/Models/Articulo.cs` (4 propiedades + 2 colecciones inversas)
- Modify: `API.Domain.Entity/Models/Almacen.cs` (2 colecciones inversas)
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs` (2 DbSets + 2 bloques `OnModelCreating` + 4 `Property` en el bloque de `Articulo`)
- Modify: `API.Infraestructure.Interface/IRepositorioGenerico.cs` (método `AgregarSinGuardarAsync`)
- Modify: `API.Infraestructure.Repository/RepositorioGenericoEfCore.cs` (impl por defecto)
- Create: `API.Infraestructure.Repository/ExistenciaArticuloRepositorio.cs`
- Create: `API.Infraestructure.Repository/MovimientoInventarioRepositorio.cs`
- Modify: `API.Service.WebApi/Startup.cs` (registrar los 2 repos genéricos)
- Test: `API.Service.WebApi.Tests/Domain/ModeloInventarioTests.cs`

**Interfaces:**
- Produces:
  - Entidad `ExistenciaArticulo { string CodArticulo; string CodAlmacen; decimal Disponible; decimal Comprometido; decimal Pedido; DateTime FechaActualizacion; byte[] RowVersion; Articulo CodArticuloNavigation; Almacen CodAlmacenNavigation; }`
  - Entidad `MovimientoInventario { int Entry; string TipoDoc; int DocEntry; int DocLinea; string CodArticulo; string CodAlmacen; DateTime Fecha; decimal CantidadEntra; decimal CantidadSale; decimal PrecioUnitario; decimal CostoUnitario; decimal ValorMovimiento; decimal VariacionPrecio; decimal SaldoCantidad; decimal SaldoCostoPromedio; decimal SaldoValor; int? MovReversaDe; ... navs }`
  - `Articulo.MetodoValuacion` (string, default "P"), `Articulo.CostoPromedio` / `CostoEstandar` / `ValorInventario` (decimal).
  - `IRepositorioGenerico<TEntity,TKey>.AgregarSinGuardarAsync(TEntity entity)` → `Task` (hace `DbSet.AddAsync` **sin** `SaveChangesAsync`).
  - `ExistenciaArticuloRepositorio : RepositorioGenericoEfCore<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>` con `override ObtenerAsync` → `FindAsync(id.CodArticulo, id.CodAlmacen)`.
  - `MovimientoInventarioRepositorio : RepositorioGenericoEfCore<MovimientoInventario, int>`.
  - DI: `IRepositorioGenerico<ExistenciaArticulo, (string,string)>` y `IRepositorioGenerico<MovimientoInventario, int>`.

- [ ] **Step 1: Crear `ExistenciaArticulo.cs`**

`API.Domain.Entity/Models/ExistenciaArticulo.cs`:

```csharp
using System;

namespace API.Domain.Entity.Models;

public partial class ExistenciaArticulo
{
    public string CodArticulo { get; set; } = null!;

    public string CodAlmacen { get; set; } = null!;

    public decimal Disponible { get; set; }

    public decimal Comprometido { get; set; }

    public decimal Pedido { get; set; }

    public DateTime FechaActualizacion { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Articulo CodArticuloNavigation { get; set; } = null!;

    public virtual Almacen CodAlmacenNavigation { get; set; } = null!;
}
```

- [ ] **Step 2: Crear `MovimientoInventario.cs`**

`API.Domain.Entity/Models/MovimientoInventario.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class MovimientoInventario
{
    public int Entry { get; set; }

    public string TipoDoc { get; set; } = null!;

    public int DocEntry { get; set; }

    public int DocLinea { get; set; }

    public string CodArticulo { get; set; } = null!;

    public string CodAlmacen { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public decimal CantidadEntra { get; set; }

    public decimal CantidadSale { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal CostoUnitario { get; set; }

    public decimal ValorMovimiento { get; set; }

    public decimal VariacionPrecio { get; set; }

    public decimal SaldoCantidad { get; set; }

    public decimal SaldoCostoPromedio { get; set; }

    public decimal SaldoValor { get; set; }

    public int? MovReversaDe { get; set; }

    public virtual Articulo CodArticuloNavigation { get; set; } = null!;

    public virtual Almacen CodAlmacenNavigation { get; set; } = null!;

    public virtual MovimientoInventario? MovReversaDeNavigation { get; set; }

    public virtual ICollection<MovimientoInventario> InverseMovReversaDeNavigation { get; set; } = new List<MovimientoInventario>();
}
```

- [ ] **Step 3: Añadir propiedades y colecciones a `Articulo.cs`**

En `API.Domain.Entity/Models/Articulo.cs`, después de `public int Serie { get; set; }` y antes de la primera `public virtual`, añadir:

```csharp
    public string MetodoValuacion { get; set; } = null!;

    public decimal CostoPromedio { get; set; }

    public decimal CostoEstandar { get; set; }

    public decimal ValorInventario { get; set; }
```

Y junto a las colecciones `ICollection<...Detalle>` existentes, añadir:

```csharp
    public virtual ICollection<ExistenciaArticulo> ExistenciaArticulos { get; set; } = new List<ExistenciaArticulo>();

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();
```

- [ ] **Step 4: Añadir colecciones a `Almacen.cs`**

En `API.Domain.Entity/Models/Almacen.cs`, junto a las colecciones existentes:

```csharp
    public virtual ICollection<ExistenciaArticulo> ExistenciaArticulos { get; set; } = new List<ExistenciaArticulo>();

    public virtual ICollection<MovimientoInventario> MovimientoInventarios { get; set; } = new List<MovimientoInventario>();
```

- [ ] **Step 5: Mapear en `ApiDbTestContext.cs`**

**a)** Junto a los `DbSet` existentes:

```csharp
    public virtual DbSet<ExistenciaArticulo> ExistenciaArticulos { get; set; }

    public virtual DbSet<MovimientoInventario> MovimientoInventarios { get; set; }
```

**b)** Dentro del bloque `modelBuilder.Entity<Articulo>(entity => { ... })`, junto a las demás `entity.Property(...)`:

```csharp
            entity.Property(e => e.MetodoValuacion)
                .HasMaxLength(1)
                .HasDefaultValueSql("('P')");
            entity.Property(e => e.CostoPromedio).HasColumnType("decimal(19, 6)").HasDefaultValueSql("((0))");
            entity.Property(e => e.CostoEstandar).HasColumnType("decimal(19, 6)").HasDefaultValueSql("((0))");
            entity.Property(e => e.ValorInventario).HasColumnType("decimal(19, 6)").HasDefaultValueSql("((0))");
```

**c)** Al final de `OnModelCreating` (antes de `OnModelCreatingPartial(modelBuilder);` si existe, o al final del método), añadir:

```csharp
        modelBuilder.Entity<ExistenciaArticulo>(entity =>
        {
            entity.HasKey(e => new { e.CodArticulo, e.CodAlmacen }).HasName("pk_existencia_articulo");

            entity.ToTable("ExistenciaArticulo");

            entity.Property(e => e.CodArticulo).HasMaxLength(15);
            entity.Property(e => e.CodAlmacen).HasMaxLength(8);
            entity.Property(e => e.Disponible).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Comprometido).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Pedido).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasOne(d => d.CodArticuloNavigation).WithMany(p => p.ExistenciaArticulos)
                .HasForeignKey(d => d.CodArticulo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_existencia_articulo");

            entity.HasOne(d => d.CodAlmacenNavigation).WithMany(p => p.ExistenciaArticulos)
                .HasForeignKey(d => d.CodAlmacen)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_existencia_almacen");
        });

        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_movimiento_inventario");

            entity.ToTable("MovimientoInventario");

            entity.HasIndex(e => new { e.CodArticulo, e.Fecha, e.Entry }, "ix_movimiento_articulo_fecha");
            entity.HasIndex(e => new { e.TipoDoc, e.DocEntry }, "ix_movimiento_origen");

            entity.Property(e => e.TipoDoc).HasMaxLength(20);
            entity.Property(e => e.CodArticulo).HasMaxLength(15);
            entity.Property(e => e.CodAlmacen).HasMaxLength(8);
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.CantidadEntra).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CantidadSale).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CostoUnitario).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.ValorMovimiento).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.VariacionPrecio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.SaldoCantidad).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.SaldoCostoPromedio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.SaldoValor).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodArticuloNavigation).WithMany(p => p.MovimientoInventarios)
                .HasForeignKey(d => d.CodArticulo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_movimiento_articulo");

            entity.HasOne(d => d.CodAlmacenNavigation).WithMany(p => p.MovimientoInventarios)
                .HasForeignKey(d => d.CodAlmacen)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_movimiento_almacen");

            entity.HasOne(d => d.MovReversaDeNavigation).WithMany(p => p.InverseMovReversaDeNavigation)
                .HasForeignKey(d => d.MovReversaDe)
                .HasConstraintName("fk_movimiento_reversa");
        });
```

- [ ] **Step 6: Añadir `AgregarSinGuardarAsync` al repositorio genérico**

En `API.Infraestructure.Interface/IRepositorioGenerico.cs`, añadir a la interfaz:

```csharp
        /// <summary>
        /// Adjunta una entidad nueva al ChangeTracker SIN llamar SaveChangesAsync.
        /// El caller es responsable de persistir (p. ej. junto con el INSERT de un documento,
        /// en una sola transacción implícita). Usado por el servicio de asiento de inventario.
        /// </summary>
        Task AgregarSinGuardarAsync(TEntity entity);
```

En `API.Infraestructure.Repository/RepositorioGenericoEfCore.cs`, añadir la implementación por defecto:

```csharp
        public virtual async Task AgregarSinGuardarAsync(TEntity entity)
        {
            await DbSet.AddAsync(entity);
        }
```

- [ ] **Step 7: Crear los repositorios**

`API.Infraestructure.Repository/ExistenciaArticuloRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ExistenciaArticuloRepositorio : RepositorioGenericoEfCore<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>
    {
        public ExistenciaArticuloRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta (CodArticulo + CodAlmacen), en el mismo orden del HasKey.
        public override async Task<ExistenciaArticulo?> ObtenerAsync((string CodArticulo, string CodAlmacen) id)
        {
            return await DbSet.FindAsync(id.CodArticulo, id.CodAlmacen);
        }
    }
}
```

`API.Infraestructure.Repository/MovimientoInventarioRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class MovimientoInventarioRepositorio : RepositorioGenericoEfCore<MovimientoInventario, int>
    {
        public MovimientoInventarioRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

- [ ] **Step 8: Registrar los repos en `Startup.cs`**

En `API.Service.WebApi/Startup.cs`, junto a los `AddTransient<IRepositorioGenerico<...>>` existentes:

```csharp
            services.AddTransient<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>, ExistenciaArticuloRepositorio>();
            services.AddTransient<IRepositorioGenerico<MovimientoInventario, int>, MovimientoInventarioRepositorio>();
```

- [ ] **Step 9: Escribir la prueba de modelo**

`API.Service.WebApi.Tests/Domain/ModeloInventarioTests.cs`:

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las entidades de inventario sin tocar la base de datos:
    // construye el modelo en memoria y verifica tablas, claves y la FK auto-referente.
    public class ModeloInventarioTests
    {
        // El constructor sin parámetros de ApiDbTestContext configura SqlServer vía OnConfiguring,
        // pero construir el modelo (ctx.Model) NO abre conexión. Si el modelo estuviera mal
        // configurado, este acceso lanzaría InvalidOperationException.
        private static IModel Modelo()
        {
            using var ctx = new ApiDbTestContext();
            return ctx.Model;
        }

        [Fact]
        public void ExistenciaArticulo_MapeaTablaYClaveCompuesta()
        {
            var et = Modelo().FindEntityType(typeof(ExistenciaArticulo))!;
            Assert.Equal("ExistenciaArticulo", et.GetTableName());
            var pk = et.FindPrimaryKey()!;
            Assert.Equal(new[] { "CodArticulo", "CodAlmacen" }, pk.Properties.Select(p => p.Name).ToArray());
            Assert.True(et.FindProperty("RowVersion")!.IsConcurrencyToken);
        }

        [Fact]
        public void MovimientoInventario_MapeaTablaClaveYAutoReferencia()
        {
            var et = Modelo().FindEntityType(typeof(MovimientoInventario))!;
            Assert.Equal("MovimientoInventario", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var selfFk = et.GetForeignKeys().Single(fk => fk.PrincipalEntityType == et);
            Assert.Equal("MovReversaDe", selfFk.Properties.Single().Name);
            Assert.False(selfFk.IsRequired);
        }

        [Fact]
        public void Articulo_GanaColumnasDeValuacion()
        {
            var et = Modelo().FindEntityType(typeof(Articulo))!;
            Assert.NotNull(et.FindProperty("MetodoValuacion"));
            Assert.NotNull(et.FindProperty("CostoPromedio"));
            Assert.NotNull(et.FindProperty("CostoEstandar"));
            Assert.NotNull(et.FindProperty("ValorInventario"));
        }
    }
}
```

- [ ] **Step 10: Compilar la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 11: Correr las pruebas de modelo y luego toda la suite**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~ModeloInventarioTests" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 3 passed.

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~622 passed (619 + 3), 0 fallos.

- [ ] **Step 12: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Domain.Entity/ API.Infraestructure.Interface/ API.Infraestructure.Repository/ API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ModeloInventarioTests.cs
git commit -m "feat(api): entidades EF de inventario (ExistenciaArticulo, MovimientoInventario), mapeo del contexto, repos y AgregarSinGuardarAsync"
```

---

## Task 3: Motor de valuación `IValuacionInventario`

**Files:**
- Create: `API.Domain.Interface/IValuacionInventario.cs` (interfaz + record `ResultadoValuacion`)
- Create: `API.Domain.Core/ValuacionInventario.cs`
- Modify: `API.Service.WebApi/Startup.cs` (DI)
- Test: `API.Service.WebApi.Tests/Domain/ValuacionInventarioTests.cs`

**Interfaces:**
- Produces:
  - `record ResultadoValuacion(decimal NuevoCostoPromedio, decimal CostoUnitarioMov, decimal ValorMovimiento, decimal VariacionPrecio)` en `API.Domain.Interface`.
  - `IValuacionInventario.CalcularEntrada(decimal cantActual, decimal costoPromActual, decimal costoEstandar, string metodo, decimal cantidad, decimal precioUnitario)` → `ResultadoValuacion`.
  - `IValuacionInventario.CalcularSalida(decimal cantActual, decimal costoPromActual, decimal costoEstandar, string metodo, decimal cantidad)` → `ResultadoValuacion`.
  - `metodo`: `"P"` promedio móvil, cualquier otro valor (incluido `"E"`) → estándar.

- [ ] **Step 1: Escribir las pruebas (fallan a compilar)**

`API.Service.WebApi.Tests/Domain/ValuacionInventarioTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Interface;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ValuacionInventarioTests
    {
        private readonly IValuacionInventario _v = new ValuacionInventario();

        [Fact]
        public void CalcularEntrada_Promedio_PrimeraEntrada_CostoIgualAlPrecio()
        {
            var r = _v.CalcularEntrada(cantActual: 0m, costoPromActual: 0m, costoEstandar: 0m, metodo: "P", cantidad: 10m, precioUnitario: 25m);
            Assert.Equal(25m, r.NuevoCostoPromedio);
            Assert.Equal(25m, r.CostoUnitarioMov);
            Assert.Equal(250m, r.ValorMovimiento);
            Assert.Equal(0m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularEntrada_Promedio_SegundaEntrada_PromediaPonderado()
        {
            // 10 @ 25 ya en stock; entra 5 @ 30 => (250 + 150) / 15 = 26.666...
            var r = _v.CalcularEntrada(cantActual: 10m, costoPromActual: 25m, costoEstandar: 0m, metodo: "P", cantidad: 5m, precioUnitario: 30m);
            Assert.Equal(400m / 15m, r.NuevoCostoPromedio);
            Assert.Equal(30m, r.CostoUnitarioMov);
            Assert.Equal(150m, r.ValorMovimiento);
            Assert.Equal(0m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularEntrada_Promedio_TotalCero_ConservaElCostoActual()
        {
            var r = _v.CalcularEntrada(cantActual: -5m, costoPromActual: 12m, costoEstandar: 0m, metodo: "P", cantidad: 5m, precioUnitario: 99m);
            Assert.Equal(12m, r.NuevoCostoPromedio);
        }

        [Fact]
        public void CalcularEntrada_Estandar_ValuaAlEstandar_YRegistraVariacion()
        {
            // estandar 20; se recibe 10 @ 25 => stock vale 10*20; variacion 10*(25-20) = 50
            var r = _v.CalcularEntrada(cantActual: 3m, costoPromActual: 20m, costoEstandar: 20m, metodo: "E", cantidad: 10m, precioUnitario: 25m);
            Assert.Equal(20m, r.NuevoCostoPromedio);
            Assert.Equal(20m, r.CostoUnitarioMov);
            Assert.Equal(200m, r.ValorMovimiento);
            Assert.Equal(50m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularEntrada_Estandar_PrecioMenorAlEstandar_VariacionNegativa()
        {
            var r = _v.CalcularEntrada(cantActual: 0m, costoPromActual: 0m, costoEstandar: 20m, metodo: "E", cantidad: 4m, precioUnitario: 18m);
            Assert.Equal(-8m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularSalida_Promedio_ValuaAlCostoPromedio_NoRecalcula()
        {
            var r = _v.CalcularSalida(cantActual: 15m, costoPromActual: 26m, costoEstandar: 0m, metodo: "P", cantidad: 4m);
            Assert.Equal(26m, r.NuevoCostoPromedio);
            Assert.Equal(26m, r.CostoUnitarioMov);
            Assert.Equal(-104m, r.ValorMovimiento);
            Assert.Equal(0m, r.VariacionPrecio);
        }

        [Fact]
        public void CalcularSalida_Estandar_ValuaAlEstandar()
        {
            var r = _v.CalcularSalida(cantActual: 15m, costoPromActual: 26m, costoEstandar: 20m, metodo: "E", cantidad: 4m);
            Assert.Equal(20m, r.CostoUnitarioMov);
            Assert.Equal(-80m, r.ValorMovimiento);
        }
    }
}
```

- [ ] **Step 2: Correr — falla a compilar**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~ValuacionInventarioTests" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: error de compilación — `ValuacionInventario` / `IValuacionInventario` no existen.

- [ ] **Step 3: Crear la interfaz + record**

`API.Domain.Interface/IValuacionInventario.cs`:

```csharp
namespace API.Domain.Interface
{
    /// <summary>Resultado de valuar un movimiento de inventario a nivel artículo.</summary>
    public record ResultadoValuacion(
        decimal NuevoCostoPromedio,
        decimal CostoUnitarioMov,
        decimal ValorMovimiento,
        decimal VariacionPrecio);

    /// <summary>
    /// Cálculo de costos de inventario. Función pura: no toca base de datos ni estado.
    /// Métodos soportados: "P" = promedio móvil, cualquier otro valor = estándar.
    /// </summary>
    public interface IValuacionInventario
    {
        ResultadoValuacion CalcularEntrada(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad, decimal precioUnitario);

        ResultadoValuacion CalcularSalida(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad);
    }
}
```

- [ ] **Step 4: Implementar**

`API.Domain.Core/ValuacionInventario.cs`:

```csharp
using API.Domain.Interface;

namespace API.Domain.Core
{
    public class ValuacionInventario : IValuacionInventario
    {
        private const string PromedioMovil = "P";

        public ResultadoValuacion CalcularEntrada(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad, decimal precioUnitario)
        {
            if (metodo == PromedioMovil)
            {
                var total = cantActual + cantidad;
                var nuevoCosto = total == 0m
                    ? costoPromActual
                    : (cantActual * costoPromActual + cantidad * precioUnitario) / total;
                return new ResultadoValuacion(
                    NuevoCostoPromedio: nuevoCosto,
                    CostoUnitarioMov: precioUnitario,
                    ValorMovimiento: cantidad * precioUnitario,
                    VariacionPrecio: 0m);
            }

            // Estándar: el stock siempre se valúa al costo estándar; la diferencia va a variación.
            return new ResultadoValuacion(
                NuevoCostoPromedio: costoEstandar,
                CostoUnitarioMov: costoEstandar,
                ValorMovimiento: cantidad * costoEstandar,
                VariacionPrecio: cantidad * (precioUnitario - costoEstandar));
        }

        public ResultadoValuacion CalcularSalida(
            decimal cantActual, decimal costoPromActual, decimal costoEstandar,
            string metodo, decimal cantidad)
        {
            var costo = metodo == PromedioMovil ? costoPromActual : costoEstandar;
            return new ResultadoValuacion(
                NuevoCostoPromedio: costo,       // la salida no recalcula el promedio
                CostoUnitarioMov: costo,
                ValorMovimiento: -cantidad * costo,
                VariacionPrecio: 0m);
        }
    }
}
```

- [ ] **Step 5: Registrar en DI**

En `API.Service.WebApi/Startup.cs`, junto a los demás `AddTransient` de dominio:

```csharp
            services.AddTransient<IValuacionInventario, ValuacionInventario>();
```

- [ ] **Step 6: Correr las pruebas del motor**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~ValuacionInventarioTests" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 7 passed.

- [ ] **Step 7: Suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~629 passed (622 + 7), 0 fallos.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Domain.Interface/IValuacionInventario.cs API.Domain.Core/ValuacionInventario.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ValuacionInventarioTests.cs
git commit -m "feat(api): motor de valuacion de inventario (promedio movil + estandar)"
```

---

## Task 4: Servicio de asiento `IInventarioAsientoService`

**Files:**
- Create: `API.Domain.Interface/IInventarioAsientoService.cs` (interfaz + record `MovimientoRequest`)
- Create: `API.Domain.Core/InventarioAsientoService.cs`
- Modify: `API.Service.WebApi/Startup.cs` (DI)
- Test: `API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<Articulo, string>`, `IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>`, `IRepositorioGenerico<MovimientoInventario, int>` (incluye `AgregarSinGuardarAsync` de Task 2), `IValuacionInventario` (Task 3).
- Produces:
  - `record MovimientoRequest(string TipoDoc, int DocEntry, int DocLinea, string CodArticulo, string CodAlmacen, decimal Cantidad, decimal PrecioUnitario, DateTime Fecha)` — `Cantidad > 0` entrada, `< 0` salida.
  - `IInventarioAsientoService.AsentarAsync(IEnumerable<MovimientoRequest> movimientos, bool permitirNegativo = false)` → `Task`. **No** llama `SaveChangesAsync`.
  - `IInventarioAsientoService.RevertirAsync(string tipoDoc, int docEntry)` → `Task`. **No** llama `SaveChangesAsync`.

- [ ] **Step 1: Escribir las pruebas (fallan a compilar)**

`API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class InventarioAsientoServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArt = new();
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _repoExist = new();
        private readonly Mock<IRepositorioGenerico<MovimientoInventario, int>> _repoMov = new();
        private readonly InventarioAsientoService _svc;

        private readonly List<MovimientoInventario> _movAgregados = new();

        public InventarioAsientoServiceTests()
        {
            _svc = new InventarioAsientoService(_repoArt.Object, _repoExist.Object, _repoMov.Object, new ValuacionInventario());
            _repoMov.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<MovimientoInventario>()))
                .Callback<MovimientoInventario>(m => _movAgregados.Add(m))
                .Returns(Task.CompletedTask);
            _repoExist.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<ExistenciaArticulo>())).Returns(Task.CompletedTask);
        }

        private void ArticuloDeInventario(string cod, string metodo = "P", decimal costoProm = 0m, decimal costoEst = 0m, decimal cantActual = 0m) =>
            _repoArt.Setup(r => r.ObtenerAsync(cod)).ReturnsAsync(new Articulo
            {
                Codigo = cod, ArticuloInventario = "S", MetodoValuacion = metodo,
                CostoPromedio = costoProm, CostoEstandar = costoEst, CantDisponible = cantActual
            });

        private void SinExistenciaPrevia() =>
            _repoExist.Setup(r => r.ObtenerAsync(It.IsAny<(string, string)>())).ReturnsAsync((ExistenciaArticulo?)null);

        private void ConExistencia(string art, string alm, decimal disponible) =>
            _repoExist.Setup(r => r.ObtenerAsync((art, alm))).ReturnsAsync(new ExistenciaArticulo
            {
                CodArticulo = art, CodAlmacen = alm, Disponible = disponible
            });

        private static MovimientoRequest Req(string art, string alm, decimal cant, decimal precio) =>
            new("11", 100, 1, art, alm, cant, precio, new DateTime(2026, 8, 30));

        [Fact]
        public async Task AsentarAsync_PrimeraEntrada_CreaExistenciaYKardexConSaldos()
        {
            ArticuloDeInventario("ART1");
            SinExistenciaPrevia();

            await _svc.AsentarAsync(new[] { Req("ART1", "01", 10m, 25m) });

            _repoExist.Verify(r => r.AgregarSinGuardarAsync(It.Is<ExistenciaArticulo>(e =>
                e.CodArticulo == "ART1" && e.CodAlmacen == "01" && e.Disponible == 10m)), Times.Once);
            var mov = Assert.Single(_movAgregados);
            Assert.Equal(10m, mov.CantidadEntra);
            Assert.Equal(0m, mov.CantidadSale);
            Assert.Equal(25m, mov.CostoUnitario);
            Assert.Equal(250m, mov.ValorMovimiento);
            Assert.Equal(10m, mov.SaldoCantidad);
            Assert.Equal(25m, mov.SaldoCostoPromedio);
            Assert.Equal(250m, mov.SaldoValor);
            Assert.Null(mov.MovReversaDe);
            _repoMov.Verify(r => r.InsertarAsync(It.IsAny<MovimientoInventario>()), Times.Never);
        }

        [Fact]
        public async Task AsentarAsync_SegundaEntrada_AcumulaPromedioYExistencia()
        {
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 10m);
            ConExistencia("ART1", "01", 10m);

            await _svc.AsentarAsync(new[] { Req("ART1", "01", 5m, 30m) });

            var mov = Assert.Single(_movAgregados);
            Assert.Equal(400m / 15m, mov.SaldoCostoPromedio);
            Assert.Equal(15m, mov.SaldoCantidad);
        }

        [Fact]
        public async Task AsentarAsync_ArticuloNoInventario_SeIgnora()
        {
            _repoArt.Setup(r => r.ObtenerAsync("SERV1")).ReturnsAsync(new Articulo { Codigo = "SERV1", ArticuloInventario = "N" });

            await _svc.AsentarAsync(new[] { Req("SERV1", "01", 3m, 10m) });

            Assert.Empty(_movAgregados);
            _repoExist.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<ExistenciaArticulo>()), Times.Never);
        }

        [Fact]
        public async Task AsentarAsync_SalidaQueDejaNegativo_Lanza()
        {
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 2m);
            ConExistencia("ART1", "01", 2m);

            await Assert.ThrowsAsync<Exception>(() => _svc.AsentarAsync(new[] { Req("ART1", "01", -5m, 0m) }));
            Assert.Empty(_movAgregados);
        }

        [Fact]
        public async Task AsentarAsync_SalidaNegativaConPermitir_NoLanza()
        {
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 2m);
            ConExistencia("ART1", "01", 2m);

            await _svc.AsentarAsync(new[] { Req("ART1", "01", -5m, 0m) }, permitirNegativo: true);

            var mov = Assert.Single(_movAgregados);
            Assert.Equal(5m, mov.CantidadSale);
            Assert.Equal(-3m, mov.SaldoCantidad);
        }

        [Fact]
        public async Task RevertirAsync_GeneraInversosYNoDuplica()
        {
            // Kardex del documento ("11", 100): una entrada de 10, sin reversa previa.
            var original = new MovimientoInventario
            {
                Entry = 500, TipoDoc = "11", DocEntry = 100, DocLinea = 1,
                CodArticulo = "ART1", CodAlmacen = "01", Fecha = new DateTime(2026, 8, 30),
                CantidadEntra = 10m, CantidadSale = 0m, CostoUnitario = 25m, MovReversaDe = null
            };
            _repoMov.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new[] { original }.AsAsyncQueryable());
            ArticuloDeInventario("ART1", costoProm: 25m, cantActual: 10m);
            ConExistencia("ART1", "01", 10m);

            await _svc.RevertirAsync("11", 100);

            var rev = Assert.Single(_movAgregados);
            Assert.Equal(0m, rev.CantidadEntra);
            Assert.Equal(10m, rev.CantidadSale);
            Assert.Equal(500, rev.MovReversaDe);
            Assert.Equal(0m, rev.SaldoCantidad);
        }

        [Fact]
        public async Task RevertirAsync_YaRevertido_NoGeneraNada()
        {
            var original = new MovimientoInventario { Entry = 500, TipoDoc = "11", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadEntra = 10m, MovReversaDe = null };
            var reversa  = new MovimientoInventario { Entry = 501, TipoDoc = "11", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadSale = 10m, MovReversaDe = 500 };
            _repoMov.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[] { original, reversa }.AsAsyncQueryable());

            await _svc.RevertirAsync("11", 100);

            Assert.Empty(_movAgregados);
        }
    }
}
```

- [ ] **Step 2: Correr — falla a compilar**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~InventarioAsientoServiceTests" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: error de compilación — `InventarioAsientoService` / `MovimientoRequest` no existen.

- [ ] **Step 3: Crear la interfaz + record**

`API.Domain.Interface/IInventarioAsientoService.cs`:

```csharp
namespace API.Domain.Interface
{
    /// <summary>
    /// Un movimiento de inventario solicitado por un documento. Cantidad &gt; 0 = entrada, &lt; 0 = salida.
    /// </summary>
    public record MovimientoRequest(
        string TipoDoc, int DocEntry, int DocLinea,
        string CodArticulo, string CodAlmacen,
        decimal Cantidad, decimal PrecioUnitario, DateTime Fecha);

    /// <summary>
    /// Aplica movimientos de inventario (existencias, valuación, kardex) sobre el ChangeTracker
    /// del ApiDbTestContext scoped. NUNCA llama SaveChangesAsync: el caller persiste todo junto
    /// con su documento, en una sola transacción implícita (mismo patrón que la numeración).
    /// </summary>
    public interface IInventarioAsientoService
    {
        Task AsentarAsync(IEnumerable<MovimientoRequest> movimientos, bool permitirNegativo = false);

        Task RevertirAsync(string tipoDoc, int docEntry);
    }
}
```

- [ ] **Step 4: Implementar**

`API.Domain.Core/InventarioAsientoService.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class InventarioAsientoService : IInventarioAsientoService
    {
        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _repoExistencia;
        private readonly IRepositorioGenerico<MovimientoInventario, int> _repoMovimiento;
        private readonly IValuacionInventario _valuacion;

        public InventarioAsientoService(
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> repoExistencia,
            IRepositorioGenerico<MovimientoInventario, int> repoMovimiento,
            IValuacionInventario valuacion)
        {
            _repoArticulo = repoArticulo;
            _repoExistencia = repoExistencia;
            _repoMovimiento = repoMovimiento;
            _valuacion = valuacion;
        }

        public async Task AsentarAsync(IEnumerable<MovimientoRequest> movimientos, bool permitirNegativo = false)
        {
            // Procesa en el orden recibido: el promedio móvil es sensible al orden.
            foreach (var m in movimientos)
            {
                await AplicarMovimientoAsync(
                    m.TipoDoc, m.DocEntry, m.DocLinea, m.CodArticulo, m.CodAlmacen,
                    m.Cantidad, m.PrecioUnitario, m.Fecha, permitirNegativo, movReversaDe: null);
            }
        }

        public async Task RevertirAsync(string tipoDoc, int docEntry)
        {
            var queryable = await _repoMovimiento.ObtenerTodoAsync();
            var delDocumento = await queryable
                .Where(x => x.TipoDoc == tipoDoc && x.DocEntry == docEntry)
                .ToListAsync();

            var yaRevertidos = delDocumento
                .Where(x => x.MovReversaDe != null)
                .Select(x => x.MovReversaDe!.Value)
                .ToHashSet();

            foreach (var orig in delDocumento.Where(x => x.MovReversaDe == null && !yaRevertidos.Contains(x.Entry)))
            {
                var cantidadOriginal = orig.CantidadEntra - orig.CantidadSale;   // + entrada, - salida
                await AplicarMovimientoAsync(
                    orig.TipoDoc, orig.DocEntry, orig.DocLinea, orig.CodArticulo, orig.CodAlmacen,
                    cantidad: -cantidadOriginal,
                    // Se revierte al costo con que se valuó el original, para que el valor cuadre exacto.
                    precioUnitario: orig.CostoUnitario,
                    fecha: orig.Fecha,
                    permitirNegativo: true,   // una reversa nunca se bloquea por negativo
                    movReversaDe: orig.Entry);
            }
        }

        private async Task AplicarMovimientoAsync(
            string tipoDoc, int docEntry, int docLinea, string codArticulo, string codAlmacen,
            decimal cantidad, decimal precioUnitario, DateTime fecha, bool permitirNegativo, int? movReversaDe)
        {
            var articulo = await _repoArticulo.ObtenerAsync(codArticulo)
                ?? throw new Exception($"El artículo {codArticulo} no existe.");

            // Solo los artículos de inventario mueven stock; servicios/no-inventario se ignoran.
            if (articulo.ArticuloInventario != "S")
                return;

            var existencia = await _repoExistencia.ObtenerAsync((codArticulo, codAlmacen));
            var nuevaExistencia = existencia is null;
            existencia ??= new ExistenciaArticulo { CodArticulo = codArticulo, CodAlmacen = codAlmacen, Disponible = 0m };

            var nuevaDisponible = existencia.Disponible + cantidad;
            if (nuevaDisponible < 0m && !permitirNegativo)
                throw new Exception($"Stock insuficiente en el almacén {codAlmacen} para el artículo {codArticulo}: disponible {existencia.Disponible}, requerido {-cantidad}.");

            var cantArtActual = articulo.CantDisponible ?? 0m;
            var resultado = cantidad >= 0m
                ? _valuacion.CalcularEntrada(cantArtActual, articulo.CostoPromedio, articulo.CostoEstandar, articulo.MetodoValuacion, cantidad, precioUnitario)
                : _valuacion.CalcularSalida(cantArtActual, articulo.CostoPromedio, articulo.CostoEstandar, articulo.MetodoValuacion, -cantidad);

            // Mutaciones en memoria (entidades rastreadas por el contexto scoped).
            existencia.Disponible = nuevaDisponible;
            existencia.FechaActualizacion = DateTime.Now;
            if (nuevaExistencia)
                await _repoExistencia.AgregarSinGuardarAsync(existencia);

            articulo.CostoPromedio = resultado.NuevoCostoPromedio;
            articulo.CantDisponible = cantArtActual + cantidad;
            articulo.ValorInventario = articulo.CostoPromedio * articulo.CantDisponible.Value;

            var mov = new MovimientoInventario
            {
                TipoDoc = tipoDoc,
                DocEntry = docEntry,
                DocLinea = docLinea,
                CodArticulo = codArticulo,
                CodAlmacen = codAlmacen,
                Fecha = fecha,
                CantidadEntra = cantidad >= 0m ? cantidad : 0m,
                CantidadSale = cantidad < 0m ? -cantidad : 0m,
                PrecioUnitario = precioUnitario,
                CostoUnitario = resultado.CostoUnitarioMov,
                ValorMovimiento = resultado.ValorMovimiento,
                VariacionPrecio = resultado.VariacionPrecio,
                SaldoCantidad = articulo.CantDisponible.Value,
                SaldoCostoPromedio = articulo.CostoPromedio,
                SaldoValor = articulo.ValorInventario,
                MovReversaDe = movReversaDe
            };
            await _repoMovimiento.AgregarSinGuardarAsync(mov);
        }
    }
}
```

- [ ] **Step 5: Registrar en DI**

En `API.Service.WebApi/Startup.cs`:

```csharp
            services.AddTransient<IInventarioAsientoService, InventarioAsientoService>();
```

- [ ] **Step 6: Correr las pruebas del servicio**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~InventarioAsientoServiceTests" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 7 passed.

- [ ] **Step 7: Suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~636 passed (629 + 7), 0 fallos.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Domain.Interface/IInventarioAsientoService.cs API.Domain.Core/InventarioAsientoService.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs
git commit -m "feat(api): servicio de asiento de inventario (AsentarAsync/RevertirAsync, sin SaveChanges)"
```

---

## Task 5: API de consulta — Existencias

**Files:**
- Create: `API.Application.DTO/inventario/ExistenciaArticuloDTO.cs`
- Create: `API.Domain.Interface/IExistenciaDomain.cs`
- Create: `API.Domain.Core/ExistenciaDomain.cs`
- Create: `API.Application.Interface/IExistenciaApplication.cs`
- Create: `API.Application.Main/ExistenciaApplication.cs`
- Create: `API.Service.WebApi/Controllers/ExistenciaController.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs` (CreateMap)
- Modify: `API.Service.WebApi/Startup.cs` (DI)
- Test: `API.Service.WebApi.Tests/Controllers/ExistenciaControllerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>` (Task 2).
- Produces:
  - `ExistenciaArticuloDTO { string CodArticulo; string CodAlmacen; decimal Disponible; decimal Comprometido; decimal Pedido; DateTime FechaActualizacion; }`
  - `GET api/Existencia` (opcional `?articulo=`, `?almacen=`) → `Respuesta<IEnumerable<ExistenciaArticuloDTO>>`
  - `GET api/Existencia/{codArticulo}/{codAlmacen}` → `Respuesta<ExistenciaArticuloDTO>` (ausencia → DTO con `Disponible = 0`)
  - `GET api/Existencia/PorArticulo/{codArticulo}` → `Respuesta<IEnumerable<ExistenciaArticuloDTO>>`
  - `IExistenciaDomain.ObtenerTodoAsync(string? articulo, string? almacen)` → `Task<IEnumerable<ExistenciaArticulo>>`
  - `IExistenciaDomain.ObtenerAsync(string codArticulo, string codAlmacen)` → `Task<ExistenciaArticulo?>`
  - `IExistenciaDomain.ObtenerPorArticuloAsync(string codArticulo)` → `Task<IEnumerable<ExistenciaArticulo>>`

- [ ] **Step 1: DTO**

`API.Application.DTO/inventario/ExistenciaArticuloDTO.cs`:

```csharp
namespace API.Application.DTO.inventario
{
    public class ExistenciaArticuloDTO
    {
        public string CodArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public decimal Disponible { get; set; }
        public decimal Comprometido { get; set; }
        public decimal Pedido { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }
}
```

- [ ] **Step 2: Dominio**

`API.Domain.Interface/IExistenciaDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IExistenciaDomain
    {
        Task<IEnumerable<ExistenciaArticulo>> ObtenerTodoAsync(string? articulo, string? almacen);
        Task<ExistenciaArticulo?> ObtenerAsync(string codArticulo, string codAlmacen);
        Task<IEnumerable<ExistenciaArticulo>> ObtenerPorArticuloAsync(string codArticulo);
    }
}
```

`API.Domain.Core/ExistenciaDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class ExistenciaDomain : IExistenciaDomain
    {
        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _repo;

        public ExistenciaDomain(IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<ExistenciaArticulo>> ObtenerTodoAsync(string? articulo, string? almacen)
        {
            var q = await _repo.ObtenerTodoAsync();
            if (!string.IsNullOrWhiteSpace(articulo))
                q = q.Where(x => x.CodArticulo == articulo);
            if (!string.IsNullOrWhiteSpace(almacen))
                q = q.Where(x => x.CodAlmacen == almacen);
            return await q.ToListAsync();
        }

        public async Task<ExistenciaArticulo?> ObtenerAsync(string codArticulo, string codAlmacen)
        {
            var q = await _repo.ObtenerTodoAsync();
            return await q.FirstOrDefaultAsync(x => x.CodArticulo == codArticulo && x.CodAlmacen == codAlmacen);
        }

        public async Task<IEnumerable<ExistenciaArticulo>> ObtenerPorArticuloAsync(string codArticulo)
        {
            var q = await _repo.ObtenerTodoAsync();
            return await q.Where(x => x.CodArticulo == codArticulo).ToListAsync();
        }
    }
}
```

- [ ] **Step 3: Aplicación**

`API.Application.Interface/IExistenciaApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;

namespace API.Application.Interface
{
    public interface IExistenciaApplication
    {
        Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerTodoAsync(string? articulo, string? almacen);
        Task<Respuesta<ExistenciaArticuloDTO>> ObtenerAsync(string codArticulo, string codAlmacen);
        Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerPorArticuloAsync(string codArticulo);
    }
}
```

`API.Application.Main/ExistenciaApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class ExistenciaApplication : IExistenciaApplication
    {
        private readonly IExistenciaDomain _domain;
        private readonly IMapper _mapper;

        public ExistenciaApplication(IExistenciaDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        public async Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerTodoAsync(string? articulo, string? almacen)
        {
            var respuesta = new Respuesta<IEnumerable<ExistenciaArticuloDTO>>();
            try
            {
                var lista = await _domain.ObtenerTodoAsync(articulo, almacen);
                respuesta.Dato = _mapper.Map<IEnumerable<ExistenciaArticuloDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }

        public async Task<Respuesta<ExistenciaArticuloDTO>> ObtenerAsync(string codArticulo, string codAlmacen)
        {
            var respuesta = new Respuesta<ExistenciaArticuloDTO>();
            try
            {
                var e = await _domain.ObtenerAsync(codArticulo, codAlmacen);
                respuesta.Dato = e is not null
                    ? _mapper.Map<ExistenciaArticuloDTO>(e)
                    : new ExistenciaArticuloDTO { CodArticulo = codArticulo, CodAlmacen = codAlmacen, Disponible = 0m };
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerPorArticuloAsync(string codArticulo)
        {
            var respuesta = new Respuesta<IEnumerable<ExistenciaArticuloDTO>>();
            try
            {
                var lista = await _domain.ObtenerPorArticuloAsync(codArticulo);
                respuesta.Dato = _mapper.Map<IEnumerable<ExistenciaArticuloDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }
    }
}
```

- [ ] **Step 4: Controller**

`API.Service.WebApi/Controllers/ExistenciaController.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Existencia")]
    public class ExistenciaController : ControllerBase
    {
        private readonly IExistenciaApplication _app;

        public ExistenciaController(IExistenciaApplication app)
        {
            _app = app;
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<ExistenciaArticuloDTO>>>> ObtenerTodo([FromQuery] string? articulo, [FromQuery] string? almacen)
        {
            var r = await _app.ObtenerTodoAsync(articulo, almacen);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }

        [HttpGet("{codArticulo}/{codAlmacen}")]
        public async Task<ActionResult<Respuesta<ExistenciaArticuloDTO>>> Obtener([FromRoute] string codArticulo, [FromRoute] string codAlmacen)
        {
            var r = await _app.ObtenerAsync(codArticulo, codAlmacen);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }

        [HttpGet("PorArticulo/{codArticulo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<ExistenciaArticuloDTO>>>> ObtenerPorArticulo([FromRoute] string codArticulo)
        {
            var r = await _app.ObtenerPorArticuloAsync(codArticulo);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }
    }
}
```

- [ ] **Step 5: Mapper + DI**

En `API.Transversal.Mapper/PerfilMapeo.cs`, añadir el `using API.Application.DTO.inventario;` y:

```csharp
            // Inventario
            CreateMap<ExistenciaArticulo, ExistenciaArticuloDTO>();
```

En `API.Service.WebApi/Startup.cs`:

```csharp
            services.AddTransient<IExistenciaDomain, ExistenciaDomain>();
            services.AddTransient<IExistenciaApplication, ExistenciaApplication>();
```

- [ ] **Step 6: Pruebas del controller**

`API.Service.WebApi.Tests/Controllers/ExistenciaControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ExistenciaControllerTests
    {
        private readonly Mock<IExistenciaApplication> _app = new();
        private readonly ExistenciaController _controller;

        public ExistenciaControllerTests() => _controller = new ExistenciaController(_app.Object);

        [Fact]
        public async Task ObtenerTodo_DevuelveOk_ConFiltros()
        {
            var resp = new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = true, Dato = new List<ExistenciaArticuloDTO>() };
            _app.Setup(a => a.ObtenerTodoAsync("ART1", "01")).ReturnsAsync(resp);

            var r = await _controller.ObtenerTodo("ART1", "01");

            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
            _app.Verify(a => a.ObtenerTodoAsync("ART1", "01"), Times.Once);
        }

        [Fact]
        public async Task ObtenerTodo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _app.Setup(a => a.ObtenerTodoAsync(null, null)).ReturnsAsync(new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = false });
            var r = await _controller.ObtenerTodo(null, null);
            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task Obtener_DevuelveOk()
        {
            var resp = new Respuesta<ExistenciaArticuloDTO> { Resultado = true, Dato = new ExistenciaArticuloDTO { CodArticulo = "ART1", CodAlmacen = "01" } };
            _app.Setup(a => a.ObtenerAsync("ART1", "01")).ReturnsAsync(resp);
            var r = await _controller.Obtener("ART1", "01");
            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorArticulo_DevuelveOk()
        {
            var resp = new Respuesta<IEnumerable<ExistenciaArticuloDTO>> { Resultado = true, Dato = new List<ExistenciaArticuloDTO>() };
            _app.Setup(a => a.ObtenerPorArticuloAsync("ART1")).ReturnsAsync(resp);
            var r = await _controller.ObtenerPorArticulo("ART1");
            Assert.IsType<OkObjectResult>(r.Result);
        }
    }
}
```

- [ ] **Step 7: Build + pruebas del módulo + suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~640 passed (636 + 4), 0 fallos.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Application.DTO/inventario/ API.Domain.Interface/IExistenciaDomain.cs API.Domain.Core/ExistenciaDomain.cs API.Application.Interface/IExistenciaApplication.cs API.Application.Main/ExistenciaApplication.cs API.Service.WebApi/Controllers/ExistenciaController.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Controllers/ExistenciaControllerTests.cs
git commit -m "feat(api): endpoints de consulta de existencias (api/Existencia)"
```

---

## Task 6: API de consulta — Kardex (`MovimientoInventario`)

**Files:**
- Create: `API.Application.DTO/inventario/MovimientoInventarioDTO.cs`
- Create: `API.Domain.Interface/IMovimientoInventarioDomain.cs`
- Create: `API.Domain.Core/MovimientoInventarioDomain.cs`
- Create: `API.Application.Interface/IMovimientoInventarioApplication.cs`
- Create: `API.Application.Main/MovimientoInventarioApplication.cs`
- Create: `API.Service.WebApi/Controllers/MovimientoInventarioController.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Controllers/MovimientoInventarioControllerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<MovimientoInventario, int>` (Task 2).
- Produces:
  - `MovimientoInventarioDTO` — todas las columnas de la entidad (§ Modelo de datos del spec), tipos idénticos.
  - `GET api/MovimientoInventario/PorArticulo/{codArticulo}` (opcional `?almacen=&desde=&hasta=`) → `Respuesta<IEnumerable<MovimientoInventarioDTO>>`, ordenado por `Fecha, Entry`.
  - `IMovimientoInventarioDomain.ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta)` → `Task<IEnumerable<MovimientoInventario>>`

- [ ] **Step 1: DTO**

`API.Application.DTO/inventario/MovimientoInventarioDTO.cs`:

```csharp
namespace API.Application.DTO.inventario
{
    public class MovimientoInventarioDTO
    {
        public int Entry { get; set; }
        public string TipoDoc { get; set; } = null!;
        public int DocEntry { get; set; }
        public int DocLinea { get; set; }
        public string CodArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public decimal CantidadEntra { get; set; }
        public decimal CantidadSale { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal ValorMovimiento { get; set; }
        public decimal VariacionPrecio { get; set; }
        public decimal SaldoCantidad { get; set; }
        public decimal SaldoCostoPromedio { get; set; }
        public decimal SaldoValor { get; set; }
        public int? MovReversaDe { get; set; }
    }
}
```

- [ ] **Step 2: Dominio**

`API.Domain.Interface/IMovimientoInventarioDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMovimientoInventarioDomain
    {
        Task<IEnumerable<MovimientoInventario>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta);
    }
}
```

`API.Domain.Core/MovimientoInventarioDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class MovimientoInventarioDomain : IMovimientoInventarioDomain
    {
        private readonly IRepositorioGenerico<MovimientoInventario, int> _repo;

        public MovimientoInventarioDomain(IRepositorioGenerico<MovimientoInventario, int> repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<MovimientoInventario>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta)
        {
            var q = await _repo.ObtenerTodoAsync();
            q = q.Where(x => x.CodArticulo == codArticulo);
            if (!string.IsNullOrWhiteSpace(almacen))
                q = q.Where(x => x.CodAlmacen == almacen);
            if (desde.HasValue)
                q = q.Where(x => x.Fecha >= desde.Value);
            if (hasta.HasValue)
                q = q.Where(x => x.Fecha <= hasta.Value);
            return await q.OrderBy(x => x.Fecha).ThenBy(x => x.Entry).ToListAsync();
        }
    }
}
```

- [ ] **Step 3: Aplicación**

`API.Application.Interface/IMovimientoInventarioApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;

namespace API.Application.Interface
{
    public interface IMovimientoInventarioApplication
    {
        Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta);
    }
}
```

`API.Application.Main/MovimientoInventarioApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class MovimientoInventarioApplication : IMovimientoInventarioApplication
    {
        private readonly IMovimientoInventarioDomain _domain;
        private readonly IMapper _mapper;

        public MovimientoInventarioApplication(IMovimientoInventarioDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        public async Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(string codArticulo, string? almacen, DateTime? desde, DateTime? hasta)
        {
            var respuesta = new Respuesta<IEnumerable<MovimientoInventarioDTO>>();
            try
            {
                var lista = await _domain.ObtenerPorArticuloAsync(codArticulo, almacen, desde, hasta);
                respuesta.Dato = _mapper.Map<IEnumerable<MovimientoInventarioDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex) { respuesta.Mensaje = ex.Message; }
            return respuesta;
        }
    }
}
```

- [ ] **Step 4: Controller**

`API.Service.WebApi/Controllers/MovimientoInventarioController.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/MovimientoInventario")]
    public class MovimientoInventarioController : ControllerBase
    {
        private readonly IMovimientoInventarioApplication _app;

        public MovimientoInventarioController(IMovimientoInventarioApplication app)
        {
            _app = app;
        }

        [HttpGet("PorArticulo/{codArticulo}")]
        public async Task<ActionResult<Respuesta<IEnumerable<MovimientoInventarioDTO>>>> ObtenerPorArticulo(
            [FromRoute] string codArticulo, [FromQuery] string? almacen, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            var r = await _app.ObtenerPorArticuloAsync(codArticulo, almacen, desde, hasta);
            return r.Resultado ? Ok(r) : BadRequest(r);
        }
    }
}
```

- [ ] **Step 5: Mapper + DI**

`API.Transversal.Mapper/PerfilMapeo.cs` (bloque `// Inventario`):

```csharp
            CreateMap<MovimientoInventario, MovimientoInventarioDTO>();
```

`API.Service.WebApi/Startup.cs`:

```csharp
            services.AddTransient<IMovimientoInventarioDomain, MovimientoInventarioDomain>();
            services.AddTransient<IMovimientoInventarioApplication, MovimientoInventarioApplication>();
```

- [ ] **Step 6: Pruebas del controller**

`API.Service.WebApi.Tests/Controllers/MovimientoInventarioControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.inventario;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MovimientoInventarioControllerTests
    {
        private readonly Mock<IMovimientoInventarioApplication> _app = new();
        private readonly MovimientoInventarioController _controller;

        public MovimientoInventarioControllerTests() => _controller = new MovimientoInventarioController(_app.Object);

        [Fact]
        public async Task ObtenerPorArticulo_DevuelveOk_YReenviaFiltros()
        {
            var desde = new DateTime(2026, 1, 1);
            var resp = new Respuesta<IEnumerable<MovimientoInventarioDTO>> { Resultado = true, Dato = new List<MovimientoInventarioDTO>() };
            _app.Setup(a => a.ObtenerPorArticuloAsync("ART1", "01", desde, null)).ReturnsAsync(resp);

            var r = await _controller.ObtenerPorArticulo("ART1", "01", desde, null);

            var ok = Assert.IsType<OkObjectResult>(r.Result);
            Assert.Same(resp, ok.Value);
            _app.Verify(a => a.ObtenerPorArticuloAsync("ART1", "01", desde, null), Times.Once);
        }

        [Fact]
        public async Task ObtenerPorArticulo_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            _app.Setup(a => a.ObtenerPorArticuloAsync("ART1", null, null, null))
                .ReturnsAsync(new Respuesta<IEnumerable<MovimientoInventarioDTO>> { Resultado = false });
            var r = await _controller.ObtenerPorArticulo("ART1", null, null, null);
            Assert.IsType<BadRequestObjectResult>(r.Result);
        }
    }
}
```

- [ ] **Step 7: Build + suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~642 passed (640 + 2), 0 fallos.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Application.DTO/inventario/ API.Domain.Interface/IMovimientoInventarioDomain.cs API.Domain.Core/MovimientoInventarioDomain.cs API.Application.Interface/IMovimientoInventarioApplication.cs API.Application.Main/MovimientoInventarioApplication.cs API.Service.WebApi/Controllers/MovimientoInventarioController.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Controllers/MovimientoInventarioControllerTests.cs
git commit -m "feat(api): endpoint de kardex por articulo (api/MovimientoInventario/PorArticulo)"
```

---

## Task 7: Web — `Web.ApiClient` de inventario

**Files:**
- Create: `Web.ApiClient/Dtos/Existencia/ExistenciaArticuloDTO.cs`
- Create: `Web.ApiClient/Dtos/MovimientoInventario/MovimientoInventarioDTO.cs`
- Create: `Web.ApiClient/Clientes/IExistenciaApiClient.cs`
- Create: `Web.ApiClient/Clientes/ExistenciaApiClient.cs`
- Create: `Web.ApiClient/Clientes/IMovimientoInventarioApiClient.cs`
- Create: `Web.ApiClient/Clientes/MovimientoInventarioApiClient.cs`
- Modify: `Web.UI/Program.cs`

**Interfaces:**
- Consumes: `api/Existencia*` (Task 5), `api/MovimientoInventario/PorArticulo` (Task 6).
- Produces:
  - `IExistenciaApiClient.ObtenerTodoAsync(string? articulo = null, string? almacen = null)`, `ObtenerPorArticuloAsync(string codArticulo)`.
  - `IMovimientoInventarioApiClient.ObtenerPorArticuloAsync(string codArticulo, string? almacen = null, DateTime? desde = null, DateTime? hasta = null)`.

- [ ] **Step 1: DTOs**

`Web.ApiClient/Dtos/Existencia/ExistenciaArticuloDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.Existencia
{
    public class ExistenciaArticuloDTO
    {
        public string CodArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public decimal Disponible { get; set; }
        public decimal Comprometido { get; set; }
        public decimal Pedido { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }
}
```

`Web.ApiClient/Dtos/MovimientoInventario/MovimientoInventarioDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.MovimientoInventario
{
    public class MovimientoInventarioDTO
    {
        public int Entry { get; set; }
        public string TipoDoc { get; set; } = null!;
        public int DocEntry { get; set; }
        public int DocLinea { get; set; }
        public string CodArticulo { get; set; } = null!;
        public string CodAlmacen { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public decimal CantidadEntra { get; set; }
        public decimal CantidadSale { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal ValorMovimiento { get; set; }
        public decimal VariacionPrecio { get; set; }
        public decimal SaldoCantidad { get; set; }
        public decimal SaldoCostoPromedio { get; set; }
        public decimal SaldoValor { get; set; }
        public int? MovReversaDe { get; set; }
    }
}
```

- [ ] **Step 2: Clientes**

`Web.ApiClient/Clientes/IExistenciaApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Existencia;

namespace Web.ApiClient.Clientes
{
    public interface IExistenciaApiClient
    {
        Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerTodoAsync(string? articulo = null, string? almacen = null);
        Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerPorArticuloAsync(string codArticulo);
    }
}
```

`Web.ApiClient/Clientes/ExistenciaApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Existencia;

namespace Web.ApiClient.Clientes
{
    public class ExistenciaApiClient : ApiClientBase, IExistenciaApiClient
    {
        private const string Recurso = "api/Existencia";

        public ExistenciaApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerTodoAsync(string? articulo = null, string? almacen = null)
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(articulo)) qs.Add($"articulo={Uri.EscapeDataString(articulo)}");
            if (!string.IsNullOrWhiteSpace(almacen)) qs.Add($"almacen={Uri.EscapeDataString(almacen)}");
            var url = qs.Count == 0 ? Recurso : $"{Recurso}?{string.Join("&", qs)}";
            return GetAsync<IEnumerable<ExistenciaArticuloDTO>>(url);
        }

        public Task<Respuesta<IEnumerable<ExistenciaArticuloDTO>>> ObtenerPorArticuloAsync(string codArticulo) =>
            GetAsync<IEnumerable<ExistenciaArticuloDTO>>($"{Recurso}/PorArticulo/{Uri.EscapeDataString(codArticulo)}");
    }
}
```

`Web.ApiClient/Clientes/IMovimientoInventarioApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.MovimientoInventario;

namespace Web.ApiClient.Clientes
{
    public interface IMovimientoInventarioApiClient
    {
        Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(
            string codArticulo, string? almacen = null, DateTime? desde = null, DateTime? hasta = null);
    }
}
```

`Web.ApiClient/Clientes/MovimientoInventarioApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.MovimientoInventario;

namespace Web.ApiClient.Clientes
{
    public class MovimientoInventarioApiClient : ApiClientBase, IMovimientoInventarioApiClient
    {
        private const string Recurso = "api/MovimientoInventario";

        public MovimientoInventarioApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<MovimientoInventarioDTO>>> ObtenerPorArticuloAsync(
            string codArticulo, string? almacen = null, DateTime? desde = null, DateTime? hasta = null)
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(almacen)) qs.Add($"almacen={Uri.EscapeDataString(almacen)}");
            if (desde.HasValue) qs.Add($"desde={desde.Value:yyyy-MM-dd}");
            if (hasta.HasValue) qs.Add($"hasta={hasta.Value:yyyy-MM-dd}");
            var url = $"{Recurso}/PorArticulo/{Uri.EscapeDataString(codArticulo)}";
            if (qs.Count > 0) url += $"?{string.Join("&", qs)}";
            return GetAsync<IEnumerable<MovimientoInventarioDTO>>(url);
        }
    }
}
```

- [ ] **Step 3: Registrar en `Program.cs`**

En `Web.UI/Program.cs`, junto a los demás `AddHttpClient`:

```csharp
builder.Services.AddHttpClient<IExistenciaApiClient, ExistenciaApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IMovimientoInventarioApiClient, MovimientoInventarioApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 4: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/ Web.UI/Program.cs
git commit -m "feat(web): clientes HTTP de inventario (Existencia, MovimientoInventario)"
```

---

## Task 8: Web — pantalla "Existencias" + submenú "Inventario"

**Files:**
- Create: `Web.UI/Controllers/ExistenciasController.cs`
- Create: `Web.UI/Views/Existencias/Index.cshtml`
- Create: `Web.UI/wwwroot/js/existencias.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml` (submenú "Inventario")

**Interfaces:**
- Consumes: `IExistenciaApiClient`, `IMovimientoInventarioApiClient` (Task 7); `IArticuloApiClient.ObtenerContenganNombreAsync` (ya existe) para el autocompletado.
- Produces: rutas Web `/Existencias` (`Index`, `ObtenerTodos`, `BuscarArticulos`, `Kardex`).

- [ ] **Step 1: Controlador Web**

`Web.UI/Controllers/ExistenciasController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;

namespace Web.UI.Controllers
{
    [Authorize]
    public class ExistenciasController : Controller
    {
        private readonly IExistenciaApiClient _existencias;
        private readonly IMovimientoInventarioApiClient _movimientos;
        private readonly IArticuloApiClient _articulos;

        public ExistenciasController(
            IExistenciaApiClient existencias,
            IMovimientoInventarioApiClient movimientos,
            IArticuloApiClient articulos)
        {
            _existencias = existencias;
            _movimientos = movimientos;
            _articulos = articulos;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(string? articulo, string? almacen)
        {
            var respuesta = await _existencias.ObtenerTodoAsync(articulo, almacen);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarArticulos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> Kardex(string codArticulo)
        {
            var respuesta = await _movimientos.ObtenerPorArticuloAsync(codArticulo);
            return Json(respuesta);
        }
    }
}
```

- [ ] **Step 2: Vista `Index.cshtml`**

`Web.UI/Views/Existencias/Index.cshtml`:

```html
@{
    ViewData["Title"] = "Existencias";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Existencias</h3>
</div>

<div class="card card-modulo mb-3">
    <div class="card-body">
        <div class="row g-2 align-items-end">
            <div class="col-md-5 position-relative">
                <label class="form-label">Filtrar por artículo</label>
                <input type="text" id="filtroArticuloTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                <input type="hidden" id="filtroArticulo" />
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index:1055; max-height:220px; overflow-y:auto;" id="filtroArticuloResultados"></ul>
            </div>
            <div class="col-md-2">
                <button type="button" class="btn btn-outline-secondary" id="btnLimpiarFiltro">Limpiar</button>
            </div>
        </div>
    </div>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblExistencias" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>Artículo</th>
                        <th>Almacén</th>
                        <th class="text-end">Disponible</th>
                        <th class="text-end">Comprometido</th>
                        <th class="text-end">Pedido</th>
                        <th>Actualizado</th>
                        <th class="text-end">Kardex</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalKardex" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-xl modal-dialog-scrollable">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Kardex — <span id="kardexArticulo"></span></h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <div class="table-responsive">
                    <table id="tblKardex" class="table table-sm table-hover align-middle w-100">
                        <thead>
                            <tr>
                                <th>Fecha</th><th>Tipo doc</th><th>Doc</th><th>Almacén</th>
                                <th class="text-end">Entra</th><th class="text-end">Sale</th>
                                <th class="text-end">Precio</th><th class="text-end">Costo</th><th class="text-end">Valor mov.</th>
                                <th class="text-end">Saldo cant.</th><th class="text-end">Saldo costo</th><th class="text-end">Saldo valor</th>
                            </tr>
                        </thead>
                        <tbody></tbody>
                    </table>
                </div>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <script src="~/js/existencias.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: JS `existencias.js`**

`Web.UI/wwwroot/js/existencias.js`:

> Helpers reales confirmados en `wwwroot/js/site.js`: `App.dataSrcTabla` (para `dataSrc`).
> **No existen** `App.numero` / `App.fecha` / `App.escaparHtml` — se formatea inline
> (`Number(n).toFixed(2)`, `new Date(f).toLocaleDateString()`) y se define un `esc()` local.
> `App.autocompletar({ texto, oculto, lista, error, endpoint, obtenerCodigo, obtenerEtiqueta, onSeleccion, minCaracteres })`
> recibe **objetos jQuery** en `texto`/`oculto`/`lista`/`error` (no selectores).

```javascript
$(function () {
    const num = n => (n == null ? '' : Number(n).toFixed(6).replace(/\.?0+$/, '')) || '0';
    const fec = f => (f ? new Date(f).toLocaleDateString() : '');
    const esc = t => String(t ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    let articuloFiltro = '';

    const tabla = $('#tblExistencias').DataTable({
        ajax: {
            url: '/Existencias/ObtenerTodos',
            data: d => { if (articuloFiltro) d.articulo = articuloFiltro; },
            dataSrc: App.dataSrcTabla
        },
        columns: [
            { data: 'codArticulo' },
            { data: 'codAlmacen' },
            { data: 'disponible', className: 'text-end', render: num },
            { data: 'comprometido', className: 'text-end', render: num },
            { data: 'pedido', className: 'text-end', render: num },
            { data: 'fechaActualizacion', render: fec },
            {
                data: 'codArticulo', className: 'text-end', orderable: false,
                render: c => `<button class="btn btn-sm btn-outline-primary btn-kardex" data-articulo="${esc(c)}">Ver</button>`
            }
        ]
    });

    App.autocompletar({
        texto: $('#filtroArticuloTexto'),
        oculto: $('#filtroArticulo'),
        lista: $('#filtroArticuloResultados'),
        error: $('<span>'),                       // esta pantalla no valida el campo; error dummy
        endpoint: '/Existencias/BuscarArticulos',
        obtenerCodigo: item => item.codigo,
        obtenerEtiqueta: item => `${item.codigo} - ${item.nombre}`,
        onSeleccion: item => { articuloFiltro = item ? item.codigo : ''; tabla.ajax.reload(); },
        minCaracteres: 2
    });

    $('#btnLimpiarFiltro').on('click', function () {
        $('#filtroArticuloTexto').val('');
        $('#filtroArticulo').val('');
        articuloFiltro = '';
        tabla.ajax.reload();
    });

    $('#tblExistencias').on('click', '.btn-kardex', async function () {
        const articulo = $(this).data('articulo');
        $('#kardexArticulo').text(articulo);
        const respuesta = await $.get('/Existencias/Kardex', { codArticulo: articulo });
        const filas = ((respuesta && respuesta.dato) || []).map(m => `
            <tr>
                <td>${fec(m.fecha)}</td>
                <td>${esc(m.tipoDoc)}</td>
                <td>${m.docEntry}/${m.docLinea}</td>
                <td>${esc(m.codAlmacen)}</td>
                <td class="text-end">${num(m.cantidadEntra)}</td>
                <td class="text-end">${num(m.cantidadSale)}</td>
                <td class="text-end">${num(m.precioUnitario)}</td>
                <td class="text-end">${num(m.costoUnitario)}</td>
                <td class="text-end">${num(m.valorMovimiento)}</td>
                <td class="text-end">${num(m.saldoCantidad)}</td>
                <td class="text-end">${num(m.saldoCostoPromedio)}</td>
                <td class="text-end">${num(m.saldoValor)}</td>
            </tr>`).join('');
        $('#tblKardex tbody').html(filas || '<tr><td colspan="12" class="text-center text-muted">Sin movimientos</td></tr>');
        new bootstrap.Modal('#modalKardex').show();
    });
});
```

- [ ] **Step 4: Submenú "Inventario" en `_Layout.cshtml`**

En `Web.UI/Views/Shared/_Layout.cshtml`, en el bloque `@{ ... }` de arriba, tras `bool EsActivoCompras = ...`:

```csharp
    bool EsActivoInventario = new[] { "Existencias" }.Any(EsActivo);
```

Y justo después del `</div>` que cierra `id="submenuCompras"`:

```html
                    <a class="nav-link nav-link-toggle @(EsActivoInventario ? "active" : "")" data-bs-toggle="collapse" href="#submenuInventario" role="button" aria-expanded="@(EsActivoInventario ? "true" : "false")" aria-controls="submenuInventario">
                        <i class="fa-solid fa-boxes-stacked"></i><span>Inventario</span>
                        <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
                    </a>
                    <div class="collapse @(EsActivoInventario ? "show" : "")" id="submenuInventario">
                        <a class="nav-link nav-sublink @(EsActivo("Existencias") ? "active" : "")" asp-controller="Existencias" asp-action="Index">
                            <i class="fa-solid fa-warehouse"></i><span>Existencias</span>
                        </a>
                    </div>
```

- [ ] **Step 5: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.UI/Controllers/ExistenciasController.cs Web.UI/Views/Existencias/ Web.UI/wwwroot/js/existencias.js Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(web): pantalla de consulta de existencias + kardex, submenu Inventario"
```

---

## Task 9: Verificación final conjunta

**Files:** ninguno nuevo.

- [ ] **Step 1: Build completo de la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 2: Suite completa de la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~642 passed, **0 fallos**. (619 baseline + 3 modelo + 7 valuación + 7 asiento + 4 existencia + 2 kardex.)

- [ ] **Step 3: Build completo de la Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 4: Prueba manual en el navegador (para el usuario)**

INV-1 no tiene enganche que genere movimientos, así que la pantalla se prueba con datos sembrados por SQL:

```sql
-- Sembrar existencia y kardex de prueba (ajustar códigos a artículos/almacenes reales):
INSERT INTO ExistenciaArticulo (CodArticulo, CodAlmacen, Disponible) VALUES ('<ART>', '<ALM>', 15);
INSERT INTO MovimientoInventario (TipoDoc, DocEntry, DocLinea, CodArticulo, CodAlmacen, Fecha, CantidadEntra, PrecioUnitario, CostoUnitario, ValorMovimiento, SaldoCantidad, SaldoCostoPromedio, SaldoValor)
VALUES ('12', 1, 1, '<ART>', '<ALM>', getdate(), 10, 25, 25, 250, 10, 25, 250),
       ('13', 1, 1, '<ART>', '<ALM>', getdate(), 5, 30, 30, 150, 15, 26.666667, 400);
```

Levantar API + Web, iniciar sesión:
1. Menú → "Inventario" → "Existencias": aparece la fila sembrada con Disponible 15.
2. Filtrar por artículo con el buscador con autocompletado: la tabla se reduce.
3. Botón "Ver" en la fila → modal Kardex con los 2 movimientos y sus saldos corridos.

- [ ] **Step 5: Recordatorio para el usuario**

Imprimir:
- Reiniciar las sesiones de depuración de Visual Studio (API y Web.UI).
- Aplicar `API/sql/2026-08-30-inventario-nucleo.sql` en cualquier entorno no-local.
- Siguiente fase: **INV-2** (enganchar el asiento en EntregaCompra/FacturaCompra).
- El punto de concurrencia del spec (§3): en INV-1 solo se mapeó `rowversion`. El reintento por `DbUpdateConcurrencyException` se resuelve en la capa Application de INV-2.

- [ ] **Step 6: Commit final (si quedó algo suelto)**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add -A && git commit -m "chore: cierre INV-1" || echo "nada que commitear"
```

---

## Notas de auto-revisión (cobertura del spec)

- **Tablas `ExistenciaArticulo` / `MovimientoInventario` + columnas de `Articulo`** → Task 1 (DDL) + Task 2 (entidades/contexto).
- **`IValuacionInventario` (promedio móvil + estándar + variación)** → Task 3.
- **`IInventarioAsientoService` (`AsentarAsync`/`RevertirAsync`, sin `SaveChangesAsync`, filtra no-inventario, `permitirNegativo`, reversa sin duplicar)** → Task 4.
- **`AgregarSinGuardarAsync` en el repo genérico** → Task 2 Step 6.
- **Saldos corridos en el kardex + sync a `Articulo`** → Task 4 (`AplicarMovimientoAsync`).
- **API de consulta (`api/Existencia`, `api/MovimientoInventario/PorArticulo`)** → Tasks 5 y 6.
- **Web: cliente + pantalla "Existencias" + kardex + submenú "Inventario"** → Tasks 7 y 8.
- **DDL versionado en `API/sql/`, aplicado por el asistente** → Task 1.
- **`rowversion` para concurrencia (mapeado y documentado; reintento fuera de alcance)** → Task 2 Step 5a + Task 9 Step 5.
- **Fuera de alcance** (enganche, stock negativo real, edición de método/costo por pantalla, traslados, documentos de mercancías, reserva) → no hay tareas; documentado en Global Constraints.
