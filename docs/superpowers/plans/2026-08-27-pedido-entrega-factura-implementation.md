# Pedido, Entrega y Factura Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar los módulos Pedido, Entrega y Factura (API completa + CRUD completo en la Web), cada uno una réplica estructural exacta de Cotización/CotizaciónDetalle, con su propio `TipoObjeto`/`CodigoObj`.

**Architecture:** Cada módulo replica el stack N-layer ya construido y probado para Cotización: entidad EF Core → dominio (con la misma lógica de numeración "el consecutivo avanza solo al registrar") → repositorio genérico → aplicación → controlador API; y en la Web: cliente HTTP tipado → controlador MVC → vista con modal (encabezado + detalle embebido) → JS. Solo cambian nombres, rutas y las constantes `TipoObjeto`/`CodigoObj`.

**Tech Stack:** .NET 7 (API, EF Core 7, AutoMapper, xUnit + Moq), .NET 8 (Web MVC), SQL Server (`API_DB_TEST`), jQuery + DataTables + Bootstrap 5 + SweetAlert2 (Web).

**Spec:** `docs/superpowers/specs/2026-08-27-pedido-entrega-factura-design.md`

## Global Constraints

- Tres módulos, cada uno independiente entre sí (sin lógica de "crear desde el anterior" -- fuera de alcance).
- Constantes por módulo, ya confirmadas en el esquema real de la base de datos:

| Módulo | `TipoObjeto` (constraint+default real) | `CodigoObj` (NumeracionDocumento) | PK constraint | FKs |
|---|---|---|---|---|
| Pedido | `'4'` | `4` | `pk_pedido` / `pk_pedido_det` | `fk_pedido_sn`, `fk_pedido_moneda`, `fk_pedido_serie`, `fk_pedido_det_cod_art`, `fk_pedido_det_almacen` |
| Entrega | `'5'` | `5` | `pk_entrega` / `pk_entrega_det` | `fk_entrega_sn`, `fk_entrega_moneda`, `fk_entrega_serie`, `fk_entrega_det_cod_art`, `fk_entrega_det_almacen` |
| Factura | `'6'` | `6` | `pk_factura` / `pk_factura_det` | `fk_factura_sn`, `fk_factura_moneda`, `fk_factura_serie`, `fk_factura_det_cod_art`, `fk_factura_det_almacen` |

- Ninguna tabla existente cambia. Las tres tablas nuevas (`Pedido`/`PedidoDetalle`, `Entrega`/`EntregaDetalle`, `Factura`/`FacturaDetalle`) ya existen en la base de datos con exactamente la misma estructura de columnas que `Cotizacion`/`CotizacionDetalle` -- no se ejecuta ningún DDL en este plan.
- El usuario configura manualmente al menos una serie por módulo en la pantalla "Numeración de documentos" antes de poder crear registros -- no se siembran filas de `NumeracionDocumento`/`NumeracionDocumentoDet`.
- Cada tarea termina con `dotnet build`/`dotnet test` en verde antes de pasar a la siguiente.
- Rutas de trabajo: API en `C:\Users\Miguel\source\repos\angelm0508\API`, Web en `C:\Users\Miguel\source\repos\angelm0508\Web`. Compilar siempre con `-p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish"` (API) o `...\web_test_publish` (Web) para no chocar con los locks de Visual Studio.

---

## Fase 1: Pedido

### Task 1: API completa de Pedido y PedidoDetalle

**Files:**
- Create: `API.Domain.Entity/Models/Pedido.cs`
- Create: `API.Domain.Entity/Models/PedidoDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs` (agregar `DbSet<Pedido>`, `DbSet<PedidoDetalle>`, dos bloques `OnModelCreating`)
- Modify: `API.Domain.Entity/Models/SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs` (agregar `ICollection<Pedido> Pedidos`)
- Modify: `API.Domain.Entity/Models/Articulo.cs`, `Almacen.cs` (agregar `ICollection<PedidoDetalle> PedidoDetalles`)
- Create: `API.Application.DTO/pedido/PedidoDTO.cs`, `PedidoCrearDTO.cs`, `PedidoActualizarDTO.cs`
- Create: `API.Application.DTO/pedido/PedidoDetalleDTO.cs`, `PedidoDetalleCrearDTO.cs`, `PedidoDetalleActualizarDTO.cs`
- Create: `API.Domain.Interface/IPedidoDomain.cs`, `IPedidoDetalleDomain.cs`
- Create: `API.Domain.Core/PedidoDomain.cs`, `PedidoDetalleDomain.cs`
- Create: `API.Infraestructure.Repository/PedidoRepositorio.cs`, `PedidoDetalleRepositorio.cs`
- Create: `API.Application.Interface/IPedidoApplication.cs`, `IPedidoDetalleApplication.cs`
- Create: `API.Application.Main/PedidoApplication.cs`, `PedidoDetalleApplication.cs`
- Create: `API.Service.WebApi/Controllers/PedidoController.cs`, `PedidoDetalleController.cs`
- Modify: `API.Service.WebApi/Startup.cs` (6 líneas de DI, junto a las de Cotizacion)
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs` (`using` + 6 `CreateMap`)
- Create: `API.Service.WebApi.Tests/Controllers/PedidoControllerTests.cs`, `PedidoDetalleControllerTests.cs`
- Create: `API.Service.WebApi.Tests/Domain/PedidoDomainTests.cs`

**Interfaces:**
- Produces: `IPedidoDomain.InsertarAsync(Pedido)/ActualizarAsync(int,Pedido)/EliminarAsync(int)/ObtenerAsync(int)/ObtenerTodoAsync()`; `IPedidoDetalleDomain.InsertarAsync(PedidoDetalle)/ActualizarAsync(int,int,PedidoDetalle)/EliminarAsync(int,int)/ObtenerAsync(int,int)/ObtenerTodoAsync()/ObtenerPorPedidoAsync(int)`; rutas `api/Pedido` y `api/PedidoDetalle` (`{entry:int}/{noLinea:int}`, `PorPedido/{entry:int}`).

- [ ] **Step 1: Crear las entidades `Pedido` y `PedidoDetalle`**

`API.Domain.Entity/Models/Pedido.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Pedido
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? Imprimido { get; set; }

    public string? EstadoDoc { get; set; }

    public string? EstadoInv { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public string? Direccion { get; set; }

    public string? MonedaDoc { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public decimal? PrctjeImpuesto { get; set; }

    public decimal? TotalImp { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public decimal? TotalDesc { get; set; }

    public decimal? TotalBruto { get; set; }

    public decimal? TotalDoc { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual Monedum? MonedaDocNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;
}
```

`API.Domain.Entity/Models/PedidoDetalle.cs`:
```csharp
namespace API.Domain.Entity.Models;

public partial class PedidoDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public int? TipoDocDestino { get; set; }

    public int? DocDestinoEntry { get; set; }

    public int? BaseRef { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public int? BaseLinea { get; set; }

    public string? EstadoLinea { get; set; }

    public string? CodArticulo { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Cantidad { get; set; }

    public decimal? Precio { get; set; }

    public decimal? PrecioBruto { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public string? CodigoImpuesto { get; set; }

    public decimal? Impuesto { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? TipoObjeto { get; set; }

    public string? CodAlmacen { get; set; }

    public virtual Almacen? CodAlmacenNavigation { get; set; }

    public virtual Articulo? CodArticuloNavigation { get; set; }
}
```

- [ ] **Step 2: Agregar las colecciones inversas en las entidades relacionadas**

En `SocioNegocio.cs`, `Monedum.cs` y `NumeracionDocumentoDet.cs`, junto a la línea existente `public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();`, agregar debajo:
```csharp
    public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
```

En `Articulo.cs` y `Almacen.cs`, junto a la línea existente `public virtual ICollection<CotizacionDetalle> CotizacionDetalles { get; set; } = new List<CotizacionDetalle>();`, agregar debajo:
```csharp
    public virtual ICollection<PedidoDetalle> PedidoDetalles { get; set; } = new List<PedidoDetalle>();
```

- [ ] **Step 3: Mapear `Pedido`/`PedidoDetalle` en `ApiDbTestContext.cs`**

Agregar `public virtual DbSet<Pedido> Pedidos { get; set; }` y `public virtual DbSet<PedidoDetalle> PedidoDetalles { get; set; }` junto a los `DbSet` de `Cotizacion`/`CotizacionDetalle`.

En `OnModelCreating`, agregar (después del bloque de `CotizacionDetalle`, antes de `Departamento`):
```csharp
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_pedido");

            entity.ToTable("Pedido");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Direccion).HasMaxLength(254);
            entity.Property(e => e.EstadoDoc)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.EstadoInv)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.Imprimido)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.MonedaDoc).HasMaxLength(3);
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeImpuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('4')");
            entity.Property(e => e.TotalBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalImp).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.CodigoSn)
                .HasConstraintName("fk_pedido_sn");

            entity.HasOne(d => d.MonedaDocNavigation).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.MonedaDoc)
                .HasConstraintName("fk_pedido_moneda");

            entity.HasOne(d => d.SerieNavigation).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pedido_serie");
        });

        modelBuilder.Entity<PedidoDetalle>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_pedido_det");

            entity.ToTable("PedidoDetalle");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cantidad).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CodAlmacen).HasMaxLength(8);
            entity.Property(e => e.CodArticulo).HasMaxLength(15);
            entity.Property(e => e.CodigoImpuesto).HasMaxLength(8);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.EstadoLinea)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.Impuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Precio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrecioBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoDocDestino).HasDefaultValueSql("((-1))");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(20)
                .HasDefaultValueSql("((3))");
            entity.Property(e => e.TotalLinea).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodAlmacenNavigation).WithMany(p => p.PedidoDetalles)
                .HasForeignKey(d => d.CodAlmacen)
                .HasConstraintName("fk_pedido_det_almacen");

            entity.HasOne(d => d.CodArticuloNavigation).WithMany(p => p.PedidoDetalles)
                .HasForeignKey(d => d.CodArticulo)
                .HasConstraintName("fk_pedido_det_cod_art");
        });

```

- [ ] **Step 4: Crear los DTOs de Pedido**

`API.Application.DTO/pedido/PedidoDTO.cs`:
```csharp
namespace API.Application.DTO.pedido
{
    public class PedidoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/pedido/PedidoCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.pedido
{
    public class PedidoCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual" -- para series autogeneradas el
        // servidor calcula el siguiente número al momento de registrar el pedido (ver
        // PedidoDomain.InsertarAsync), así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/pedido/PedidoActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.pedido
{
    public class PedidoActualizarDTO
    {
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/pedido/PedidoDetalleDTO.cs`:
```csharp
namespace API.Application.DTO.pedido
{
    public class PedidoDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`API.Application.DTO/pedido/PedidoDetalleCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.pedido
{
    public class PedidoDetalleCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Entry { get; set; }

        // NoLinea no lo asigna el usuario: el backend calcula max(NoLinea existentes del Entry) + 1.
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`API.Application.DTO/pedido/PedidoDetalleActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.pedido
{
    public class PedidoDetalleActualizarDTO
    {
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

- [ ] **Step 5: Crear la capa de dominio de Pedido**

`API.Domain.Interface/IPedidoDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPedidoDomain
    {
        #region async methods
        Task<int> InsertarAsync(Pedido obj);
        Task<bool> ActualizarAsync(int id, Pedido obj);
        Task<bool> EliminarAsync(int id);
        Task<Pedido> ObtenerAsync(int id);
        Task<IQueryable<Pedido>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Domain.Core/PedidoDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoDomain : IPedidoDomain
    {
        // Código de objeto/documento reservado para Pedidos -- exigido por el CHECK constraint
        // de la tabla (TipoObjeto='4'). Se fuerza siempre en el servidor, sin confiar en lo que
        // envíe el cliente.
        private const string TipoObjetoPedido = "4";

        private readonly IRepositorioGenerico<Pedido, int> _repoGenericoPedido;
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public PedidoDomain(
            IRepositorioGenerico<Pedido, int> repoGenericoPedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoPedido = repoGenericoPedido;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<int> InsertarAsync(Pedido obj)
        {
            obj.TipoObjeto = TipoObjetoPedido;

            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar pedidos.");
            }

            if (serie.Manual == "S")
            {
                // Serie manual: el número lo escribe el usuario, el consecutivo automático no aplica.
                if (obj.NumDoc <= 0)
                {
                    throw new Exception("El número de documento es requerido para series manuales.");
                }
            }
            else
            {
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el pedido -- no
                // al solo consultar/previsualizar el número.
                if (serie.SigNumero == null)
                {
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                }

                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                {
                    throw new Exception("Se agotó la numeración disponible en esta serie.");
                }

                obj.NumDoc = serie.SigNumero.Value;

                // No se llama a _repoGenericoNumeracion.ActualizarAsync aquí a propósito: "serie"
                // ya es una entidad rastreada por el mismo ApiDbTestContext que usa
                // _repoGenericoPedido (ambos repos genéricos se resuelven en el mismo scope de la
                // petición), así que este cambio en memoria queda pendiente y se guarda junto con
                // el INSERT del pedido en el único SaveChangesAsync de abajo -- las dos operaciones
                // quedan en la misma transacción implícita: si el INSERT falla, el incremento del
                // consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoPedido.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, Pedido obj)
        {
            obj.TipoObjeto = TipoObjetoPedido;
            return await _repoGenericoPedido.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            // No existe FK/cascada entre PedidoDetalle.Entry y Pedido.Entry en la base de datos,
            // así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoPedido.EliminarAsync(id);
        }

        public async Task<Pedido> ObtenerAsync(int id)
        {
            return await _repoGenericoPedido.ObtenerAsync(id);
        }

        public async Task<IQueryable<Pedido>> ObtenerTodoAsync()
        {
            return await _repoGenericoPedido.ObtenerTodoAsync();
        }
        #endregion
    }
}
```

`API.Domain.Interface/IPedidoDetalleDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPedidoDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(PedidoDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, PedidoDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<PedidoDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<PedidoDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<PedidoDetalle>> ObtenerPorPedidoAsync(int entry);
        #endregion
    }
}
```

`API.Domain.Core/PedidoDetalleDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PedidoDetalleDomain : IPedidoDetalleDomain
    {
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _repoGenericoDet;

        public PedidoDetalleDomain(IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoGenericoDet)
        {
            _repoGenericoDet = repoGenericoDet;
        }

        #region async methods
        public async Task<int> InsertarAsync(PedidoDetalle obj)
        {
            var lineasExistentes = await ObtenerPorPedidoAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, PedidoDetalle obj)
        {
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<PedidoDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<PedidoDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<PedidoDetalle>> ObtenerPorPedidoAsync(int entry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
        #endregion
    }
}
```

- [ ] **Step 6: Crear los repositorios de Pedido**

`API.Infraestructure.Repository/PedidoRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PedidoRepositorio : RepositorioGenericoEfCore<Pedido, int>
    {
        public PedidoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`API.Infraestructure.Repository/PedidoDetalleRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PedidoDetalleRepositorio : RepositorioGenericoEfCore<PedidoDetalle, (int Entry, int NoLinea)>
    {
        public PedidoDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<PedidoDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
```

- [ ] **Step 7: Crear la capa de aplicación de Pedido**

`API.Application.Interface/IPedidoApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;

namespace API.Application.Interface
{
    public interface IPedidoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PedidoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, PedidoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<PedidoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PedidoDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Application.Main/PedidoApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PedidoApplication : IPedidoApplication
    {
        private readonly IPedidoDomain _pedidoDomain;
        private readonly IMapper _mapper;

        public PedidoApplication(IPedidoDomain pedidoDomain, IMapper mapper)
        {
            _pedidoDomain = pedidoDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PedidoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var pedido = _mapper.Map<Pedido>(obj);
                respuesta.Dato = await _pedidoDomain.InsertarAsync(pedido);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, PedidoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var pedido = _mapper.Map<Pedido>(obj);
                respuesta.Dato = await _pedidoDomain.ActualizarAsync(id, pedido);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _pedidoDomain.EliminarAsync(id);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<PedidoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PedidoDTO>();
            try
            {
                var pedido = await _pedidoDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<PedidoDTO>(pedido);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PedidoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDTO>>();
            try
            {
                var queryable = await _pedidoDomain.ObtenerTodoAsync();
                var pedidos = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PedidoDTO>>(pedidos);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
```

`API.Application.Interface/IPedidoDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;

namespace API.Application.Interface
{
    public interface IPedidoDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PedidoDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, PedidoDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<PedidoDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerPorPedidoAsync(int entry);
        #endregion
    }
}
```

`API.Application.Main/PedidoDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PedidoDetalleApplication : IPedidoDetalleApplication
    {
        private readonly IPedidoDetalleDomain _pedidoDetalleDomain;
        private readonly IMapper _mapper;

        public PedidoDetalleApplication(IPedidoDetalleDomain pedidoDetalleDomain, IMapper mapper)
        {
            _pedidoDetalleDomain = pedidoDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PedidoDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<PedidoDetalle>(obj);
                respuesta.Dato = await _pedidoDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, PedidoDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<PedidoDetalle>(obj);
                respuesta.Dato = await _pedidoDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _pedidoDetalleDomain.EliminarAsync(entry, noLinea);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<PedidoDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<PedidoDetalleDTO>();
            try
            {
                var entidad = await _pedidoDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<PedidoDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDetalleDTO>>();
            try
            {
                var queryable = await _pedidoDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PedidoDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerPorPedidoAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDetalleDTO>>();
            try
            {
                var lista = await _pedidoDetalleDomain.ObtenerPorPedidoAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<PedidoDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
```

- [ ] **Step 8: Crear los controladores API de Pedido**

`API.Service.WebApi/Controllers/PedidoController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Pedido")]
    public class PedidoController : ControllerBase
    {
        private readonly IPedidoApplication _pedidoApplication;

        public PedidoController(IPedidoApplication pedidoApplication)
        {
            _pedidoApplication = pedidoApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<PedidoDTO>>> Obtener([FromRoute] int id)
        {
            var pedido = await _pedidoApplication.ObtenerAsync(id);

            if (!pedido.Resultado)
            {
                return BadRequest(pedido);
            }

            if (pedido.Dato == null)
            {
                pedido.Resultado = false;
                pedido.Mensaje = "El código del pedido no se encontró.";
                return NotFound(pedido);
            }

            return Ok(pedido);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<PedidoDTO>>>> ObtenerTodoAsync()
        {
            var pedidos = await _pedidoApplication.ObtenerTodoAsync();

            if (!pedidos.Resultado)
            {
                return BadRequest(pedidos);
            }

            return Ok(pedidos);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PedidoCrearDTO obj)
        {
            var insert = await _pedidoApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] PedidoActualizarDTO obj)
        {
            var pedido = await _pedidoApplication.ObtenerAsync(id);

            if (pedido.Dato == null)
            {
                pedido.Resultado = false;
                pedido.Mensaje = "El código del pedido no se encontró.";
                return NotFound(pedido);
            }

            var update = await _pedidoApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var pedido = await _pedidoApplication.ObtenerAsync(id);

            if (pedido.Dato == null)
            {
                pedido.Resultado = false;
                pedido.Mensaje = "El código del pedido no se encontró.";
                return NotFound(pedido);
            }

            var delete = await _pedidoApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
```

`API.Service.WebApi/Controllers/PedidoDetalleController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/PedidoDetalle")]
    public class PedidoDetalleController : ControllerBase
    {
        private readonly IPedidoDetalleApplication _pedidoDetalleApplication;

        public PedidoDetalleController(IPedidoDetalleApplication pedidoDetalleApplication)
        {
            _pedidoDetalleApplication = pedidoDetalleApplication;
        }

        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<PedidoDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _pedidoDetalleApplication.ObtenerAsync(entry, noLinea);

            if (!det.Resultado)
            {
                return BadRequest(det);
            }

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            return Ok(det);
        }

        [HttpGet("PorPedido/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<PedidoDetalleDTO>>>> ObtenerPorPedido([FromRoute] int entry)
        {
            var detalles = await _pedidoDetalleApplication.ObtenerPorPedidoAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<PedidoDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _pedidoDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] PedidoDetalleCrearDTO obj)
        {
            var insert = await _pedidoDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] PedidoDetalleActualizarDTO obj)
        {
            var det = await _pedidoDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _pedidoDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _pedidoDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _pedidoDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
```

- [ ] **Step 9: Registrar Pedido en la inyección de dependencias**

En `API.Service.WebApi/Startup.cs`, junto a las líneas de `Cotizacion`/`CotizacionDetalle`, agregar:
```csharp
            services.AddTransient<IRepositorioGenerico<Pedido, int>, PedidoRepositorio>();
            services.AddTransient<IPedidoDomain, PedidoDomain>();
            services.AddTransient<IPedidoApplication, PedidoApplication>();

            services.AddTransient<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>, PedidoDetalleRepositorio>();
            services.AddTransient<IPedidoDetalleDomain, PedidoDetalleDomain>();
            services.AddTransient<IPedidoDetalleApplication, PedidoDetalleApplication>();
```

- [ ] **Step 10: Registrar los mapeos de AutoMapper**

En `API.Transversal.Mapper/PerfilMapeo.cs`, agregar `using API.Application.DTO.pedido;` junto a los demás `using`, y junto a los `CreateMap` de Cotizacion:
```csharp
            // Pedido
            CreateMap<Pedido, PedidoDTO>();
            CreateMap<PedidoCrearDTO, Pedido>();
            CreateMap<PedidoActualizarDTO, Pedido>();

            // PedidoDetalle
            CreateMap<PedidoDetalle, PedidoDetalleDTO>();
            CreateMap<PedidoDetalleCrearDTO, PedidoDetalle>();
            CreateMap<PedidoDetalleActualizarDTO, PedidoDetalle>();
```

- [ ] **Step 11: Compilar la API para confirmar que todo lo anterior encaja**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\API && dotnet build API.sln -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish"`
Expected: `0 Errores`. Si hay errores de tipos/usings, corregirlos antes de seguir (los pasos de test dependen de que esto compile).

- [ ] **Step 12: Escribir las pruebas de `PedidoController`**

`API.Service.WebApi.Tests/Controllers/PedidoControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PedidoControllerTests
    {
        private readonly Mock<IPedidoApplication> _applicationMock;
        private readonly PedidoController _controller;

        public PedidoControllerTests()
        {
            _applicationMock = new Mock<IPedidoApplication>();
            _controller = new PedidoController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<PedidoDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<PedidoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<PedidoDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código del pedido no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new PedidoDTO { Entry = 1, NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<PedidoDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDTO>> { Resultado = true, Dato = new List<PedidoDTO> { new PedidoDTO { Entry = 1 } } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new PedidoCrearDTO { NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new PedidoCrearDTO { NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<PedidoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, new PedidoActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PedidoDTO> { Resultado = true, Dato = new PedidoDTO { Entry = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<PedidoActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new PedidoActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PedidoDTO> { Resultado = true, Dato = new PedidoDTO { Entry = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<PedidoActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new PedidoActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<PedidoDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PedidoDTO> { Resultado = true, Dato = new PedidoDTO { Entry = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<PedidoDTO> { Resultado = true, Dato = new PedidoDTO { Entry = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
```

- [ ] **Step 13: Escribir las pruebas de `PedidoDetalleController`**

`API.Service.WebApi.Tests/Controllers/PedidoDetalleControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.pedido;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PedidoDetalleControllerTests
    {
        private readonly Mock<IPedidoDetalleApplication> _applicationMock;
        private readonly PedidoDetalleController _controller;

        public PedidoDetalleControllerTests()
        {
            _applicationMock = new Mock<IPedidoDetalleApplication>();
            _controller = new PedidoDetalleController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<PedidoDetalleDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<PedidoDetalleDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new PedidoDetalleDTO { Entry = 1, NoLinea = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorPedido_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorPedidoAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorPedido(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorPedido_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<PedidoDetalleDTO> { new PedidoDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerPorPedidoAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorPedido(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<PedidoDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<PedidoDetalleDTO> { new PedidoDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new PedidoDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new PedidoDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, 1, new PedidoDetalleActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = new PedidoDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<PedidoDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new PedidoDetalleActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = new PedidoDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<PedidoDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new PedidoDetalleActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = new PedidoDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<PedidoDetalleDTO> { Resultado = true, Dato = new PedidoDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
```

- [ ] **Step 14: Escribir las pruebas de dominio de `PedidoDomain`**

`API.Service.WebApi.Tests/Domain/PedidoDomainTests.cs`:
```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PedidoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Pedido, int>> _repoPedidoMock;
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _repoDetalleMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly PedidoDomain _domain;

        public PedidoDomainTests()
        {
            _repoPedidoMock = new Mock<IRepositorioGenerico<Pedido, int>>();
            _repoDetalleMock = new Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new PedidoDomain(_repoPedidoMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "4",
            Serie = 4,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N"
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_AsignaSigNumeroYLoIncrementa()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>()))
                .ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });

            var obj = new Pedido { Serie = 4, NumDoc = 0, TipoObjeto = "algo-que-el-cliente-mando" };
            var entry = await _domain.InsertarAsync(obj);

            Assert.Equal(99, entry);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal("4", obj.TipoObjeto);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaNumDocDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>()))
                .ReturnsAsync((Pedido c) => { c.Entry = 1; return c; });

            var obj = new Pedido { Serie = 4, NumDoc = 12345 };
            await _domain.InsertarAsync(obj);

            Assert.Equal(12345, obj.NumDoc);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Pedido { Serie = 4, NumDoc = 0 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Pedido { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Pedido { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new Pedido { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_FuerzaTipoObjetoACuatro()
        {
            _repoPedidoMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).ReturnsAsync(true);

            var obj = new Pedido { TipoObjeto = "otro-valor" };
            var resultado = await _domain.ActualizarAsync(1, obj);

            Assert.True(resultado);
            Assert.Equal("4", obj.TipoObjeto);
        }
    }
}
```

- [ ] **Step 15: Correr toda la suite de pruebas de la API**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\API && dotnet test API.Service.WebApi.Tests/API.Service.WebApi.Tests.csproj -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish_tests"`
Expected: todas las pruebas en verde (las 376 anteriores + las 7 nuevas de `PedidoDomainTests` + las de `PedidoControllerTests`/`PedidoDetalleControllerTests`).

- [ ] **Step 16: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\API
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar módulo Pedido (API completa)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

### Task 2: Cliente HTTP de Pedido en Web.ApiClient

**Files:**
- Create: `Web.ApiClient/Dtos/Pedido/PedidoDTO.cs`, `PedidoCrearDTO.cs`, `PedidoActualizarDTO.cs`
- Create: `Web.ApiClient/Dtos/PedidoDetalle/PedidoDetalleDTO.cs`, `PedidoDetalleCrearDTO.cs`, `PedidoDetalleActualizarDTO.cs`
- Create: `Web.ApiClient/Clientes/IPedidoApiClient.cs`, `PedidoApiClient.cs`
- Create: `Web.ApiClient/Clientes/IPedidoDetalleApiClient.cs`, `PedidoDetalleApiClient.cs`
- Modify: `Web.UI/Program.cs`

**Interfaces:**
- Consumes: rutas API `api/Pedido`, `api/PedidoDetalle` (Task 1).
- Produces: `IPedidoApiClient.{ObtenerTodoAsync,ObtenerAsync,InsertarAsync,ActualizarAsync,EliminarAsync}`, `IPedidoDetalleApiClient.{ObtenerTodoAsync,ObtenerPorPedidoAsync,ObtenerAsync,InsertarAsync,ActualizarAsync,EliminarAsync}` -- usados por el controlador Web en Task 3.

- [ ] **Step 1: Crear los DTOs de Pedido en Web.ApiClient**

`Web.ApiClient/Dtos/Pedido/PedidoDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Pedido
{
    public class PedidoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/Pedido/PedidoCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Web.ApiClient.Dtos.Pedido
{
    public class PedidoCrearDTO
    {
        // Requerido solo para series "Manual" -- para series autogeneradas la API calcula el
        // siguiente número al registrar el pedido, así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/Pedido/PedidoActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Pedido
{
    public class PedidoActualizarDTO
    {
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/PedidoDetalle/PedidoDetalleDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.PedidoDetalle
{
    public class PedidoDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`Web.ApiClient/Dtos/PedidoDetalle/PedidoDetalleCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Web.ApiClient.Dtos.PedidoDetalle
{
    public class PedidoDetalleCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Entry { get; set; }

        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`Web.ApiClient/Dtos/PedidoDetalle/PedidoDetalleActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.PedidoDetalle
{
    public class PedidoDetalleActualizarDTO
    {
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

- [ ] **Step 2: Crear los clientes HTTP de Pedido**

`Web.ApiClient/Clientes/IPedidoApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Pedido;

namespace Web.ApiClient.Clientes
{
    public interface IPedidoApiClient
    {
        Task<Respuesta<IEnumerable<PedidoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<PedidoDTO>> ObtenerAsync(int entry);
        Task<Respuesta<int>> InsertarAsync(PedidoCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, PedidoActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry);
    }
}
```

`Web.ApiClient/Clientes/PedidoApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Pedido;

namespace Web.ApiClient.Clientes
{
    public class PedidoApiClient : ApiClientBase, IPedidoApiClient
    {
        private const string Recurso = "api/Pedido";

        public PedidoApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<PedidoDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<PedidoDTO>>(Recurso);

        public Task<Respuesta<PedidoDTO>> ObtenerAsync(int entry) =>
            GetAsync<PedidoDTO>($"{Recurso}/{entry}");

        public Task<Respuesta<int>> InsertarAsync(PedidoCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, PedidoActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry) =>
            DeleteAsync<bool>($"{Recurso}/{entry}");
    }
}
```

`Web.ApiClient/Clientes/IPedidoDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.PedidoDetalle;

namespace Web.ApiClient.Clientes
{
    public interface IPedidoDetalleApiClient
    {
        Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerPorPedidoAsync(int entry);
        Task<Respuesta<PedidoDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<int>> InsertarAsync(PedidoDetalleCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, PedidoDetalleActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
    }
}
```

`Web.ApiClient/Clientes/PedidoDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.PedidoDetalle;

namespace Web.ApiClient.Clientes
{
    public class PedidoDetalleApiClient : ApiClientBase, IPedidoDetalleApiClient
    {
        private const string Recurso = "api/PedidoDetalle";

        public PedidoDetalleApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<PedidoDetalleDTO>>(Recurso);

        public Task<Respuesta<IEnumerable<PedidoDetalleDTO>>> ObtenerPorPedidoAsync(int entry) =>
            GetAsync<IEnumerable<PedidoDetalleDTO>>($"{Recurso}/PorPedido/{entry}");

        public Task<Respuesta<PedidoDetalleDTO>> ObtenerAsync(int entry, int noLinea) =>
            GetAsync<PedidoDetalleDTO>($"{Recurso}/{entry}/{noLinea}");

        public Task<Respuesta<int>> InsertarAsync(PedidoDetalleCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, PedidoDetalleActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}/{noLinea}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea) =>
            DeleteAsync<bool>($"{Recurso}/{entry}/{noLinea}");
    }
}
```

- [ ] **Step 3: Registrar los HttpClient tipados en `Program.cs`**

Junto a las líneas de `ICotizacionApiClient`/`ICotizacionDetalleApiClient`, agregar:
```csharp
builder.Services.AddHttpClient<IPedidoApiClient, PedidoApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IPedidoDetalleApiClient, PedidoDetalleApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 4: Compilar Web.ApiClient y Web.UI**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\Web && dotnet build Web.slnx -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\web_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 5: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\Web
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar cliente HTTP de Pedido en Web.ApiClient

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

### Task 3: Pantalla Web de Pedidos

**Files:**
- Create: `Web.UI/Controllers/PedidosController.cs`
- Create: `Web.UI/Views/Pedidos/Index.cshtml`, `_Form.cshtml`
- Create: `Web.UI/wwwroot/js/pedidos.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `IPedidoApiClient`, `IPedidoDetalleApiClient` (Task 2); `ISocioNegocioApiClient`, `IMonedaApiClient`, `IArticuloApiClient`, `IAlmacenApiClient`, `IImpuestoApiClient`, `INumeracionDocumentoDetApiClient` (ya existentes, usados igual que en `CotizacionesController`).

- [ ] **Step 1: Crear `PedidosController`**

`Web.UI/Controllers/PedidosController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.Pedido;
using Web.ApiClient.Dtos.PedidoDetalle;

namespace Web.UI.Controllers
{
    [Authorize]
    public class PedidosController : Controller
    {
        private readonly IPedidoApiClient _pedidos;
        private readonly IPedidoDetalleApiClient _detalles;
        private readonly ISocioNegocioApiClient _socios;
        private readonly IMonedaApiClient _monedas;
        private readonly IArticuloApiClient _articulos;
        private readonly IAlmacenApiClient _almacenes;
        private readonly IImpuestoApiClient _impuestos;
        private readonly INumeracionDocumentoDetApiClient _series;

        // CodigoObj de NumeracionDocumento que identifica a "Pedidos" como tipo de objeto.
        private const string CodigoObjPedido = "4";
        private const string SubTipoDocPedido = "--";

        public PedidosController(
            IPedidoApiClient pedidos,
            IPedidoDetalleApiClient detalles,
            ISocioNegocioApiClient socios,
            IMonedaApiClient monedas,
            IArticuloApiClient articulos,
            IAlmacenApiClient almacenes,
            IImpuestoApiClient impuestos,
            INumeracionDocumentoDetApiClient series)
        {
            _pedidos = pedidos;
            _detalles = detalles;
            _socios = socios;
            _monedas = monedas;
            _articulos = articulos;
            _almacenes = almacenes;
            _impuestos = impuestos;
            _series = series;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _pedidos.ObtenerTodoAsync();
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> FormularioCrear()
        {
            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjPedido);
            ViewBag.SeriesPedido = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocPedido);
            ViewBag.EsEdicion = false;
            return PartialView("_Form", new PedidoCrearDTO { EstadoDoc = "A", TipoObjeto = "4" });
        }

        [HttpGet]
        public async Task<IActionResult> FormularioEditar(int entry)
        {
            var respuesta = await _pedidos.ObtenerAsync(entry);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            await CargarDropdownsAsync();
            ViewBag.EsEdicion = true;
            ViewBag.EntryActual = entry;

            var serieInfo = await _series.ObtenerAsync(respuesta.Dato.Serie);
            ViewBag.NombreSerieActual = serieInfo.Resultado ? serieInfo.Dato?.NombreSerie : null;

            var dto = new PedidoCrearDTO
            {
                NumDoc = respuesta.Dato.NumDoc,
                Serie = respuesta.Dato.Serie,
                EstadoDoc = respuesta.Dato.EstadoDoc,
                TipoObjeto = respuesta.Dato.TipoObjeto,
                FechaDoc = respuesta.Dato.FechaDoc,
                FechaEmision = respuesta.Dato.FechaEmision,
                CodigoSn = respuesta.Dato.CodigoSn,
                NombreSn = respuesta.Dato.NombreSn,
                Direccion = respuesta.Dato.Direccion,
                MonedaDoc = respuesta.Dato.MonedaDoc,
                PrctjeImpuesto = respuesta.Dato.PrctjeImpuesto,
                TotalImp = respuesta.Dato.TotalImp,
                PrctjeDesc = respuesta.Dato.PrctjeDesc,
                TotalDesc = respuesta.Dato.TotalDesc,
                TotalBruto = respuesta.Dato.TotalBruto,
                TotalDoc = respuesta.Dato.TotalDoc,
                Comentario = respuesta.Dato.Comentario
            };

            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromBody] PedidoCrearDTO dto)
        {
            var respuesta = await _pedidos.InsertarAsync(dto);
            if (!respuesta.Resultado)
                return Json(respuesta);

            var creado = await _pedidos.ObtenerAsync(respuesta.Dato);
            return Json(new { respuesta.Resultado, respuesta.Mensaje, dato = respuesta.Dato, numDoc = creado.Dato?.NumDoc });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int entry, [FromBody] PedidoCrearDTO dto)
        {
            var actual = await _pedidos.ObtenerAsync(entry);
            if (!actual.Resultado || actual.Dato is null)
                return NotFound(actual);

            var actualizar = new PedidoActualizarDTO
            {
                NumDoc = actual.Dato.NumDoc,
                Serie = actual.Dato.Serie,
                EstadoDoc = dto.EstadoDoc,
                TipoObjeto = dto.TipoObjeto,
                FechaDoc = dto.FechaDoc,
                FechaEmision = dto.FechaEmision,
                CodigoSn = dto.CodigoSn,
                NombreSn = dto.NombreSn,
                Direccion = dto.Direccion,
                MonedaDoc = dto.MonedaDoc,
                PrctjeImpuesto = dto.PrctjeImpuesto,
                TotalImp = dto.TotalImp,
                PrctjeDesc = dto.PrctjeDesc,
                TotalDesc = dto.TotalDesc,
                TotalBruto = dto.TotalBruto,
                TotalDoc = dto.TotalDoc,
                Comentario = dto.Comentario
            };

            var respuesta = await _pedidos.ActualizarAsync(entry, actualizar);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int entry)
        {
            var respuesta = await _pedidos.EliminarAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDetalle(int entry)
        {
            var respuesta = await _detalles.ObtenerPorPedidoAsync(entry);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearLinea([FromBody] PedidoDetalleCrearDTO dto)
        {
            var respuesta = await _detalles.InsertarAsync(dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarLinea(int entry, int noLinea, [FromBody] PedidoDetalleActualizarDTO dto)
        {
            var respuesta = await _detalles.ActualizarAsync(entry, noLinea, dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarLinea(int entry, int noLinea)
        {
            var respuesta = await _detalles.EliminarAsync(entry, noLinea);
            return Json(respuesta);
        }

        private async Task CargarDropdownsAsync()
        {
            var socios = await _socios.ObtenerTodoAsync();
            var monedas = await _monedas.ObtenerTodoAsync();
            var articulos = await _articulos.ObtenerTodoAsync();
            var almacenes = await _almacenes.ObtenerTodoAsync();
            var impuestos = await _impuestos.ObtenerTodoAsync();

            ViewBag.Socios = new SelectList(socios.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Articulos = articulos.Dato ?? [];
            ViewBag.Almacenes = new SelectList(almacenes.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Impuestos = impuestos.Dato ?? [];
        }
    }
}
```

- [ ] **Step 2: Crear `Views/Pedidos/Index.cshtml`**

```html
@{
    ViewData["Title"] = "Pedidos";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Pedidos</h3>
    <button type="button" class="btn btn-primary" id="btnNuevo">
        <i class="fa-solid fa-plus me-1"></i>Nuevo
    </button>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblPedidos" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>No. Documento</th>
                        <th>Socio de negocio</th>
                        <th>Fecha</th>
                        <th>Estado</th>
                        <th>Total</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalFormulario" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-xl modal-dialog-scrollable">
        <div class="modal-content" id="contenidoModal">
            <!-- se carga por AJAX -->
        </div>
    </div>
</div>

@section Scripts {
    <script src="~/js/pedidos.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: Crear `Views/Pedidos/_Form.cshtml`**

Idéntico a `Views/Cotizaciones/_Form.cshtml`, cambiando el modelo, el título, los ids de tabla/script/formulario y las rutas de acción:
```html
@using System.Text.Json
@model Web.ApiClient.Dtos.Pedido.PedidoCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
    var opcionesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? "Editar pedido" : "Nuevo pedido")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formPedido" novalidate>
        <div asp-validation-summary="ModelOnly" class="alert alert-danger py-2 px-3 small"></div>

        <div class="row g-3">
            @if (!esEdicion)
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <select id="selectSeriePedido" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                    <span class="form-text">Si eliges una serie distinta de "Manual", el número se genera solo al guardar.</span>
                </div>
            }
            else
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <input class="form-control" value="@ViewBag.NombreSerieActual" disabled />
                </div>
            }
            <div class="col-md-3">
                <label asp-for="NumDoc" class="form-label">No. documento</label>
                <input asp-for="NumDoc" type="number" class="form-control" readonly="@esEdicion" />
                <span asp-validation-for="NumDoc" class="text-danger small"></span>
            </div>
            <div class="col-md-3">
                <label asp-for="EstadoDoc" class="form-label">Estado</label>
                <select asp-for="EstadoDoc" class="form-select">
                    <option value="A">Activo</option>
                    <option value="C">Cancelado</option>
                </select>
            </div>
            <div class="col-md-3" hidden>
                <label asp-for="TipoObjeto" class="form-label">Tipo</label>
                <input asp-for="TipoObjeto" class="form-control" />
            </div>

            <div class="col-md-4">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <select asp-for="CodigoSn" id="selectCodigoSn" class="form-select" asp-items="ViewBag.Socios">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
            <div class="col-md-4">
                <label asp-for="NombreSn" class="form-label">Nombre</label>
                <input asp-for="NombreSn" class="form-control" />
            </div>
            <div class="col-md-4">
                <label asp-for="MonedaDoc" class="form-label">Moneda</label>
                <select asp-for="MonedaDoc" class="form-select" asp-items="ViewBag.Monedas">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>

            <div class="col-md-4">
                <label asp-for="Direccion" class="form-label"></label>
                <input asp-for="Direccion" class="form-control" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha documento</label>
                <input type="date" name="FechaDoc" id="FechaDoc" class="form-control" value="@Model.FechaDoc?.ToString("yyyy-MM-dd")" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha emisión</label>
                <input type="date" name="FechaEmision" id="FechaEmision" class="form-control" value="@Model.FechaEmision?.ToString("yyyy-MM-dd")" />
            </div>

            <div class="col-md-3">
                <label asp-for="PrctjeDesc" class="form-label">% Descuento</label>
                <input asp-for="PrctjeDesc" type="number" step="0.01" class="form-control" />
            </div>
            <div class="col-md-3">
                <label asp-for="PrctjeImpuesto" class="form-label">% Impuesto</label>
                <input asp-for="PrctjeImpuesto" type="number" step="0.01" class="form-control" />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total bruto</label>
                <input id="TotalBruto" class="form-control" value="@Model.TotalBruto" disabled />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total documento</label>
                <input id="TotalDoc" class="form-control" value="@Model.TotalDoc" disabled />
            </div>

            <div class="col-12">
                <label asp-for="Comentario" class="form-label"></label>
                <textarea asp-for="Comentario" class="form-control" rows="2"></textarea>
            </div>
        </div>
    </form>

    <hr />
    <div class="d-flex justify-content-between align-items-center mb-2">
        <h6 class="mb-0">Detalle</h6>
        <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevaLinea">
            <i class="fa-solid fa-plus me-1"></i>Agregar línea
        </button>
    </div>

    @if (!esEdicion)
    {
        <p class="text-muted small">Las líneas agregadas aquí se guardarán junto con el pedido.</p>
    }

    <div class="table-responsive">
        <table id="tblDetallePedido" class="table table-sm table-hover align-middle w-100" data-entry="@ViewBag.EntryActual" data-es-edicion="@esEdicion.ToString().ToLower()">
            <thead>
                <tr>
                    <th>Artículo</th>
                    <th>Descripción</th>
                    <th>Cantidad</th>
                    <th>Precio</th>
                    <th>% Desc.</th>
                    <th>Impuesto</th>
                    <th>Total línea</th>
                    <th class="text-end">Acciones</th>
                </tr>
            </thead>
            <tbody></tbody>
        </table>
    </div>

    <div id="panelLineaDetalle" class="border rounded p-3 mb-2 d-none">
        <form id="formLineaDetalle">
            <input type="hidden" id="detNoLineaOriginal" value="" />
            <div class="row g-2">
                <div class="col-md-4">
                    <label class="form-label">Artículo</label>
                    <select id="detCodArticulo" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Almacén</label>
                    <select name="CodAlmacen" id="detCodAlmacen" class="form-select" asp-items="ViewBag.Almacenes">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Impuesto</label>
                    <select id="detCodigoImpuesto" class="form-select">
                        <option value="">-- Ninguno --</option>
                    </select>
                </div>

                <div class="col-md-8">
                    <label class="form-label">Descripción</label>
                    <input name="Descripcion" id="detDescripcion" class="form-control" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Cantidad</label>
                    <input name="Cantidad" id="detCantidad" type="number" step="0.01" class="form-control" value="1" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Precio</label>
                    <input name="Precio" id="detPrecio" type="number" step="0.01" class="form-control" />
                </div>

                <div class="col-md-2">
                    <label class="form-label">% Desc.</label>
                    <input name="PrctjeDesc" id="detPrctjeDesc" type="number" step="0.01" class="form-control" value="0" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Impuesto (Q)</label>
                    <input name="Impuesto" id="detImpuestoMonto" type="number" step="0.01" class="form-control" readonly />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Total línea</label>
                    <input name="TotalLinea" id="detTotalLinea" type="number" step="0.01" class="form-control" readonly />
                </div>
            </div>
            <div class="text-end mt-2">
                <button type="button" class="btn btn-sm btn-secondary" id="btnCancelarLinea">Cancelar</button>
                <button type="button" class="btn btn-sm btn-primary" id="btnGuardarLinea">Guardar línea</button>
            </div>
        </form>
    </div>

    <script id="datosArticulosPedido" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Articulos, opcionesJson))
    </script>
    <script id="datosImpuestosPedido" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Impuestos, opcionesJson))
    </script>

    @if (!esEdicion)
    {
        <script id="datosSeriesPedido" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(ViewBag.SeriesPedido, opcionesJson))
        </script>
    }
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
    <button type="button" class="btn btn-primary" id="btnGuardarPedido" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
        <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
    </button>
</div>
```

- [ ] **Step 4: Crear `wwwroot/js/pedidos.js`**

Idéntico a `cotizaciones.js`, sustituyendo cada identificador `cotizacion(es)`/`Cotizacion(es)` por `pedido(s)`/`Pedido(s)` y cada endpoint `/Cotizaciones/...` por `/Pedidos/...`:
```javascript
$(function () {
    const tabla = $('#tblPedidos').DataTable({
        ajax: { url: '/Pedidos/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'numDoc' },
            { data: 'nombreSn', render: (d, t, row) => d || row.codigoSn || '' },
            { data: 'fechaDoc', render: d => d ? new Date(d).toLocaleDateString() : '' },
            { data: 'estadoDoc', render: d => d === 'C' ? '<span class="badge text-bg-secondary">Cancelado</span>' : '<span class="badge text-bg-success">Activo</span>' },
            { data: 'totalDoc', render: d => d != null ? Number(d).toFixed(2) : '' },
            {
                data: 'entry', orderable: false, className: 'text-end',
                render: entry => `
                    <button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"><i class="fa-solid fa-pen"></i></button>
                    <button class="btn btn-sm btn-outline-danger btn-eliminar" data-entry="${entry}"><i class="fa-solid fa-trash"></i></button>
                `
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSeriePedido();
        inicializarDetalle();
    }

    $('#btnNuevo').on('click', async function () {
        const html = await $.get('/Pedidos/FormularioCrear');
        abrirModal(html);
    });

    $('#tblPedidos').on('click', '.btn-editar', async function () {
        const entry = $(this).data('entry');
        const html = await $.get('/Pedidos/FormularioEditar', { entry });
        abrirModal(html);
    });

    $('#tblPedidos').on('click', '.btn-eliminar', async function () {
        const entry = $(this).data('entry');
        const confirmado = await App.confirmarEliminar(`Se eliminará el pedido #${entry}.`);
        if (!confirmado) return;

        const respuesta = await App.eliminar(`/Pedidos/Eliminar?entry=${entry}`);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Pedido eliminado correctamente.');
        recargarTabla();
    });

    // --- Serie de numeración para generar el número de documento (solo aplica al crear) ---

    function inicializarSeriePedido() {
        const $sel = $('#selectSeriePedido');
        if ($sel.length === 0) return;

        const datosEl = document.getElementById('datosSeriesPedido');
        const series = datosEl ? (JSON.parse(datosEl.textContent) || []) : [];

        $sel.html('<option value="">-- Seleccione --</option>');
        let serieManual = null;
        series.forEach(s => {
            const serie = s.serie ?? s.Serie;
            const nombre = s.nombreSerie ?? s.NombreSerie;
            const manual = s.manual ?? s.Manual;
            if (manual === 'S' && serieManual === null) serieManual = serie;
            $sel.append(`<option value="${serie}" data-manual="${manual}">${nombre}</option>`);
        });

        if (serieManual !== null) $sel.val(serieManual);

        actualizarNumDocSegunSerie();
    }

    function esSerieManualPedido() {
        const $sel = $('#selectSeriePedido');
        if ($sel.length === 0 || !$sel.val()) return true;
        return $sel.find('option:selected').data('manual') === 'S';
    }

    function actualizarNumDocSegunSerie() {
        const $numDoc = $('#NumDoc');
        if ($numDoc.length === 0) return;

        if (esSerieManualPedido()) {
            $numDoc.prop('disabled', false).attr('placeholder', '');
        } else {
            $numDoc.val('').prop('disabled', true).attr('placeholder', 'Se generará al guardar');
        }
    }

    $(document).on('change', '#selectSeriePedido', actualizarNumDocSegunSerie);

    // Auto-completa el nombre del socio de negocio al elegirlo (queda editable después).
    $(document).on('change', '#selectCodigoSn', function () {
        const texto = $(this).find('option:selected').text();
        if (texto && texto !== '-- Seleccione --') {
            $('#NombreSn').val(texto);
        }
    });

    $(document).on('click', '#btnGuardarPedido', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const entry = $boton.data('entry');

        if (!esEdicion) {
            const serieSeleccionada = $('#selectSeriePedido').val();
            if (!serieSeleccionada) {
                App.mostrarError('Debes seleccionar una serie.');
                return;
            }
        }

        // El número de documento (No. documento) no se solicita aquí para series no manuales: el
        // servidor lo calcula y avanza el consecutivo al registrar el pedido (ver
        // PedidoDomain.InsertarAsync en la API), no antes. Para series Manual, el campo #NumDoc
        // está habilitado y su valor viaja normalmente en recolectarFormulario.
        const datos = App.recolectarFormulario('#formPedido');
        if (!esEdicion) {
            datos.Serie = $('#selectSeriePedido').val();
        }

        const totales = calcularTotalesDesdeLineas(esEdicionDetalle() ? lineasRemotas : lineasLocales);
        datos.TotalBruto = totales.totalBruto;
        datos.TotalDesc = totales.totalDesc;
        datos.TotalImp = totales.totalImp;
        datos.TotalDoc = totales.totalDoc;

        if (!esEdicion) {
            const respuestaCabecera = await App.enviarJson('/Pedidos/Crear', 'POST', datos);
            if (!respuestaCabecera.resultado) {
                App.mostrarError(respuestaCabecera.mensaje);
                return;
            }

            const entryCreado = respuestaCabecera.dato;

            if (respuestaCabecera.numDoc != null) {
                $('#NumDoc').val(respuestaCabecera.numDoc).prop('disabled', false);
            }

            let exitosas = 0;
            let fallidas = 0;

            for (const linea of lineasLocales) {
                const { _id, ...lineaSinId } = linea;
                const respuestaLinea = await App.enviarJson('/Pedidos/CrearLinea', 'POST', {
                    ...lineaSinId,
                    Entry: entryCreado
                });

                if (respuestaLinea.resultado) {
                    exitosas++;
                } else {
                    fallidas++;
                    App.mostrarError(respuestaLinea.mensaje);
                }
            }

            const sufijoNumDoc = respuestaCabecera.numDoc != null ? ` No. documento: ${respuestaCabecera.numDoc}.` : '';
            if (fallidas > 0) {
                await App.mostrarExito(`Pedido creado correctamente. Líneas guardadas: ${exitosas} de ${exitosas + fallidas}.${sufijoNumDoc}`);
            } else {
                await App.mostrarExito(`Pedido creado correctamente.${sufijoNumDoc}`);
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            recargarTabla();
            return;
        }

        const respuesta = await App.enviarJson(`/Pedidos/Editar?entry=${entry}`, 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }

        bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
        App.mostrarExito('Pedido actualizado correctamente.');
        recargarTabla();
    });

    // --- Detalle (grid anidado): en creación se administra localmente, en edición en vivo contra la API ---

    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let articulosDisponibles = [];
    let impuestosDisponibles = [];

    function esEdicionDetalle() {
        const v = $('#tblDetallePedido').data('es-edicion');
        return v === true || v === 'true';
    }

    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;

        const $tabla = $('#tblDetallePedido');
        if ($tabla.length === 0) return;

        const datosArt = document.getElementById('datosArticulosPedido');
        articulosDisponibles = datosArt ? (JSON.parse(datosArt.textContent) || []) : [];

        const datosImp = document.getElementById('datosImpuestosPedido');
        impuestosDisponibles = datosImp ? (JSON.parse(datosImp.textContent) || []) : [];

        const $selArt = $('#detCodArticulo');
        $selArt.html('<option value="">-- Seleccione --</option>');
        articulosDisponibles.forEach(a => {
            const codigo = a.codigo ?? a.Codigo;
            const nombre = a.nombre ?? a.Nombre;
            $selArt.append(`<option value="${codigo}">${codigo} - ${nombre ?? ''}</option>`);
        });

        const $selImp = $('#detCodigoImpuesto');
        $selImp.html('<option value="">-- Ninguno --</option>');
        impuestosDisponibles.forEach(i => {
            const codigo = i.codigo ?? i.Codigo;
            const nombre = i.nombre ?? i.Nombre;
            const tasa = i.tasa ?? i.Tasa ?? 0;
            $selImp.append(`<option value="${codigo}" data-tasa="${tasa}">${nombre} (${tasa}%)</option>`);
        });

        if (esEdicionDetalle()) {
            cargarDetalleRemoto();
        } else {
            pintarDetalle();
        }
    }

    async function cargarDetalleRemoto() {
        const entry = $('#tblDetallePedido').data('entry');
        const respuesta = await $.get('/Pedidos/ObtenerDetalle', { entry });
        lineasRemotas = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        pintarDetalle();
    }

    function calcularTotalesDesdeLineas(lista) {
        let totalBruto = 0, totalDesc = 0, totalImp = 0, totalDoc = 0;
        lista.forEach(l => {
            const cantidad = Number(l.cantidad ?? l.Cantidad ?? 0);
            const precio = Number(l.precio ?? l.Precio ?? 0);
            const prctjeDesc = Number(l.prctjeDesc ?? l.PrctjeDesc ?? 0);
            const impuesto = Number(l.impuesto ?? l.Impuesto ?? 0);
            const bruto = cantidad * precio;
            const desc = bruto * (prctjeDesc / 100);
            totalBruto += bruto;
            totalDesc += desc;
            totalImp += impuesto;
            totalDoc += (bruto - desc + impuesto);
        });
        return {
            totalBruto: totalBruto.toFixed(2),
            totalDesc: totalDesc.toFixed(2),
            totalImp: totalImp.toFixed(2),
            totalDoc: totalDoc.toFixed(2)
        };
    }

    function pintarDetalle() {
        const $tbody = $('#tblDetallePedido tbody');
        if ($tbody.length === 0) return;

        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;

        const totales = calcularTotalesDesdeLineas(lista);
        $('#TotalBruto').val(totales.totalBruto);
        $('#TotalDoc').val(totales.totalDoc);

        if (lista.length === 0) {
            $tbody.html('<tr><td colspan="8" class="text-center text-muted">Sin líneas de detalle</td></tr>');
            return;
        }

        $tbody.html(lista.map(linea => {
            const noLinea = linea.noLinea ?? linea.NoLinea;
            const codArticulo = linea.codArticulo ?? linea.CodArticulo;
            const descripcion = linea.descripcion ?? linea.Descripcion;
            const cantidad = linea.cantidad ?? linea.Cantidad;
            const precio = linea.precio ?? linea.Precio;
            const prctjeDesc = linea.prctjeDesc ?? linea.PrctjeDesc;
            const impuesto = linea.impuesto ?? linea.Impuesto;
            const totalLinea = linea.totalLinea ?? linea.TotalLinea;
            const clave = esEdicionDetalle() ? noLinea : linea._id;
            return `
                <tr>
                    <td>${codArticulo ?? ''}</td>
                    <td>${descripcion ?? ''}</td>
                    <td>${cantidad ?? ''}</td>
                    <td>${precio != null ? Number(precio).toFixed(2) : ''}</td>
                    <td>${prctjeDesc ?? 0}</td>
                    <td>${impuesto != null ? Number(impuesto).toFixed(2) : '0.00'}</td>
                    <td>${totalLinea != null ? Number(totalLinea).toFixed(2) : ''}</td>
                    <td class="text-end">
                        <button type="button" class="btn btn-sm btn-outline-primary btn-editar-linea" data-clave="${clave}"><i class="fa-solid fa-pen"></i></button>
                        <button type="button" class="btn btn-sm btn-outline-danger btn-eliminar-linea" data-clave="${clave}"><i class="fa-solid fa-trash"></i></button>
                    </td>
                </tr>
            `;
        }).join(''));
    }

    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        $('#detCodArticulo').val('');
        $('#detCodAlmacen').val('');
        $('#detCodigoImpuesto').val('');
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }

    /** Recalcula el monto de impuesto y el total de la línea con base en los campos actuales del panel. */
    function recalcularLinea() {
        const cantidad = Number($('#detCantidad').val()) || 0;
        const precio = Number($('#detPrecio').val()) || 0;
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;
        const tasa = Number($('#detCodigoImpuesto').find('option:selected').data('tasa')) || 0;

        const bruto = cantidad * precio;
        const desc = bruto * (prctjeDesc / 100);
        const subtotal = bruto - desc;
        const impuesto = subtotal * (tasa / 100);
        const total = subtotal + impuesto;

        $('#detImpuestoMonto').val(impuesto.toFixed(2));
        $('#detTotalLinea').val(total.toFixed(2));
    }

    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc, #detCodigoImpuesto', recalcularLinea);

    $(document).on('change', '#detCodArticulo', function () {
        const codigo = $(this).val();
        const articulo = articulosDisponibles.find(a => (a.codigo ?? a.Codigo) === codigo);
        if (articulo) {
            $('#detDescripcion').val(articulo.nombre ?? articulo.Nombre ?? '');
            $('#detPrecio').val(articulo.precioUnitario ?? articulo.PrecioUnitario ?? 0);
            $('#detCodAlmacen').val(articulo.almacenDefecto ?? articulo.AlmacenDefecto ?? '');
        }
        recalcularLinea();
    });

    $(document).on('click', '#btnNuevaLinea', function () {
        limpiarPanelLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '#btnCancelarLinea', function () {
        $('#panelLineaDetalle').addClass('d-none');
    });

    $(document).on('click', '.btn-editar-linea', function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        $('#detNoLineaOriginal').val(clave);
        $('#detCodArticulo').val(linea.codArticulo ?? linea.CodArticulo ?? '');
        $('#detCodAlmacen').val(linea.codAlmacen ?? linea.CodAlmacen ?? '');
        $('#detCodigoImpuesto').val(linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '');
        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '.btn-eliminar-linea', async function () {
        const clave = $(this).data('clave');

        const confirmado = await App.confirmarEliminar('Se eliminará la línea de detalle seleccionada.');
        if (!confirmado) return;

        if (esEdicionDetalle()) {
            const entry = $('#tblDetallePedido').data('entry');
            const respuesta = await App.eliminar(`/Pedidos/EliminarLinea?entry=${entry}&noLinea=${clave}`);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            App.mostrarExito('Línea eliminada correctamente.');
            cargarDetalleRemoto();
        } else {
            lineasLocales = lineasLocales.filter(l => l._id !== clave);
            pintarDetalle();
        }
    });

    $(document).on('click', '#btnGuardarLinea', async function () {
        const datosForm = App.recolectarFormulario('#formLineaDetalle');
        datosForm.CodArticulo = $('#detCodArticulo').val() || null;
        datosForm.CodigoImpuesto = $('#detCodigoImpuesto').val() || null;

        if (!datosForm.CodArticulo) {
            App.mostrarError('Selecciona un artículo.');
            return;
        }

        if (esEdicionDetalle()) {
            const entry = $('#tblDetallePedido').data('entry');
            const esEdicionLinea = noLineaOriginalEnEdicion !== null;
            const url = esEdicionLinea
                ? `/Pedidos/EditarLinea?entry=${entry}&noLinea=${noLineaOriginalEnEdicion}`
                : '/Pedidos/CrearLinea';
            const datos = { ...datosForm, Entry: entry };

            const respuesta = await App.enviarJson(url, 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }

            App.mostrarExito(esEdicionLinea ? 'Línea actualizada correctamente.' : 'Línea agregada correctamente.');
            $('#panelLineaDetalle').addClass('d-none');
            cargarDetalleRemoto();
        } else {
            if (noLineaOriginalEnEdicion !== null) {
                lineasLocales = lineasLocales.map(l => l._id === noLineaOriginalEnEdicion ? { ...datosForm, _id: l._id } : l);
            } else {
                lineasLocales.push({ ...datosForm, _id: proximoIdLocal++ });
            }

            $('#panelLineaDetalle').addClass('d-none');
            pintarDetalle();
        }
    });
});
```

- [ ] **Step 5: Agregar Pedidos al submenú "Ventas" en `_Layout.cshtml`**

Cambiar:
```csharp
    bool EsActivoVentas = new[] { "Cotizaciones" }.Any(EsActivo);
```
por:
```csharp
    bool EsActivoVentas = new[] { "Cotizaciones", "Pedidos" }.Any(EsActivo);
```

Y agregar, dentro de `<div class="collapse ..." id="submenuVentas">`, después del enlace de Cotizaciones:
```html
                        <a class="nav-link nav-sublink @(EsActivo("Pedidos") ? "active" : "")" asp-controller="Pedidos" asp-action="Index">
                            <i class="fa-solid fa-cart-arrow-down"></i><span>Pedidos</span>
                        </a>
```

- [ ] **Step 6: Compilar la Web**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\Web && dotnet build Web.slnx -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\web_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 7: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\Web
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar pantalla Web de Pedidos

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

## Fase 2: Entrega

### Task 4: API completa de Entrega y EntregaDetalle

**Files:**
- Create: `API.Domain.Entity/Models/Entrega.cs`
- Create: `API.Domain.Entity/Models/EntregaDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs` (agregar `DbSet<Entrega>`, `DbSet<EntregaDetalle>`, dos bloques `OnModelCreating`)
- Modify: `API.Domain.Entity/Models/SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs` (agregar `ICollection<Entrega> Entregas`)
- Modify: `API.Domain.Entity/Models/Articulo.cs`, `Almacen.cs` (agregar `ICollection<EntregaDetalle> EntregaDetalles`)
- Create: `API.Application.DTO/entrega/EntregaDTO.cs`, `EntregaCrearDTO.cs`, `EntregaActualizarDTO.cs`
- Create: `API.Application.DTO/entrega/EntregaDetalleDTO.cs`, `EntregaDetalleCrearDTO.cs`, `EntregaDetalleActualizarDTO.cs`
- Create: `API.Domain.Interface/IEntregaDomain.cs`, `IEntregaDetalleDomain.cs`
- Create: `API.Domain.Core/EntregaDomain.cs`, `EntregaDetalleDomain.cs`
- Create: `API.Infraestructure.Repository/EntregaRepositorio.cs`, `EntregaDetalleRepositorio.cs`
- Create: `API.Application.Interface/IEntregaApplication.cs`, `IEntregaDetalleApplication.cs`
- Create: `API.Application.Main/EntregaApplication.cs`, `EntregaDetalleApplication.cs`
- Create: `API.Service.WebApi/Controllers/EntregaController.cs`, `EntregaDetalleController.cs`
- Modify: `API.Service.WebApi/Startup.cs` (6 líneas de DI, junto a las de Cotizacion)
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs` (`using` + 6 `CreateMap`)
- Create: `API.Service.WebApi.Tests/Controllers/EntregaControllerTests.cs`, `EntregaDetalleControllerTests.cs`
- Create: `API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs`

**Interfaces:**
- Produces: `IEntregaDomain.InsertarAsync(Entrega)/ActualizarAsync(int,Entrega)/EliminarAsync(int)/ObtenerAsync(int)/ObtenerTodoAsync()`; `IEntregaDetalleDomain.InsertarAsync(EntregaDetalle)/ActualizarAsync(int,int,EntregaDetalle)/EliminarAsync(int,int)/ObtenerAsync(int,int)/ObtenerTodoAsync()/ObtenerPorEntregaAsync(int)`; rutas `api/Entrega` y `api/EntregaDetalle` (`{entry:int}/{noLinea:int}`, `PorEntrega/{entry:int}`).

- [ ] **Step 1: Crear las entidades `Entrega` y `EntregaDetalle`**

`API.Domain.Entity/Models/Entrega.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Entrega
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? Imprimido { get; set; }

    public string? EstadoDoc { get; set; }

    public string? EstadoInv { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public string? Direccion { get; set; }

    public string? MonedaDoc { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public decimal? PrctjeImpuesto { get; set; }

    public decimal? TotalImp { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public decimal? TotalDesc { get; set; }

    public decimal? TotalBruto { get; set; }

    public decimal? TotalDoc { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual Monedum? MonedaDocNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;
}
```

`API.Domain.Entity/Models/EntregaDetalle.cs`:
```csharp
namespace API.Domain.Entity.Models;

public partial class EntregaDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public int? TipoDocDestino { get; set; }

    public int? DocDestinoEntry { get; set; }

    public int? BaseRef { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public int? BaseLinea { get; set; }

    public string? EstadoLinea { get; set; }

    public string? CodArticulo { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Cantidad { get; set; }

    public decimal? Precio { get; set; }

    public decimal? PrecioBruto { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public string? CodigoImpuesto { get; set; }

    public decimal? Impuesto { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? TipoObjeto { get; set; }

    public string? CodAlmacen { get; set; }

    public virtual Almacen? CodAlmacenNavigation { get; set; }

    public virtual Articulo? CodArticuloNavigation { get; set; }
}
```

- [ ] **Step 2: Agregar las colecciones inversas en las entidades relacionadas**

En `SocioNegocio.cs`, `Monedum.cs` y `NumeracionDocumentoDet.cs`, junto a la línea existente `public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();`, agregar debajo:
```csharp
    public virtual ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();
```

En `Articulo.cs` y `Almacen.cs`, junto a la línea existente `public virtual ICollection<CotizacionDetalle> CotizacionDetalles { get; set; } = new List<CotizacionDetalle>();`, agregar debajo:
```csharp
    public virtual ICollection<EntregaDetalle> EntregaDetalles { get; set; } = new List<EntregaDetalle>();
```

- [ ] **Step 3: Mapear `Entrega`/`EntregaDetalle` en `ApiDbTestContext.cs`**

Agregar `public virtual DbSet<Entrega> Entregas { get; set; }` y `public virtual DbSet<EntregaDetalle> EntregaDetalles { get; set; }` junto a los `DbSet` de `Cotizacion`/`CotizacionDetalle`.

En `OnModelCreating`, agregar (después del bloque de `CotizacionDetalle`, antes de `Departamento`):
```csharp
        modelBuilder.Entity<Entrega>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_entrega");

            entity.ToTable("Entrega");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Direccion).HasMaxLength(254);
            entity.Property(e => e.EstadoDoc)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.EstadoInv)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.Imprimido)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.MonedaDoc).HasMaxLength(3);
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeImpuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('4')");
            entity.Property(e => e.TotalBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalImp).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany(p => p.Entregas)
                .HasForeignKey(d => d.CodigoSn)
                .HasConstraintName("fk_entrega_sn");

            entity.HasOne(d => d.MonedaDocNavigation).WithMany(p => p.Entregas)
                .HasForeignKey(d => d.MonedaDoc)
                .HasConstraintName("fk_entrega_moneda");

            entity.HasOne(d => d.SerieNavigation).WithMany(p => p.Entregas)
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_entrega_serie");
        });

        modelBuilder.Entity<EntregaDetalle>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_entrega_det");

            entity.ToTable("EntregaDetalle");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cantidad).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CodAlmacen).HasMaxLength(8);
            entity.Property(e => e.CodArticulo).HasMaxLength(15);
            entity.Property(e => e.CodigoImpuesto).HasMaxLength(8);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.EstadoLinea)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.Impuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Precio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrecioBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoDocDestino).HasDefaultValueSql("((-1))");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(20)
                .HasDefaultValueSql("((3))");
            entity.Property(e => e.TotalLinea).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodAlmacenNavigation).WithMany(p => p.EntregaDetalles)
                .HasForeignKey(d => d.CodAlmacen)
                .HasConstraintName("fk_entrega_det_almacen");

            entity.HasOne(d => d.CodArticuloNavigation).WithMany(p => p.EntregaDetalles)
                .HasForeignKey(d => d.CodArticulo)
                .HasConstraintName("fk_entrega_det_cod_art");
        });

```

- [ ] **Step 4: Crear los DTOs de Entrega**

`API.Application.DTO/entrega/EntregaDTO.cs`:
```csharp
namespace API.Application.DTO.entrega
{
    public class EntregaDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/entrega/EntregaCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.entrega
{
    public class EntregaCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual" -- para series autogeneradas el
        // servidor calcula el siguiente número al momento de registrar el entrega (ver
        // EntregaDomain.InsertarAsync), así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/entrega/EntregaActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.entrega
{
    public class EntregaActualizarDTO
    {
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/entrega/EntregaDetalleDTO.cs`:
```csharp
namespace API.Application.DTO.entrega
{
    public class EntregaDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`API.Application.DTO/entrega/EntregaDetalleCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.entrega
{
    public class EntregaDetalleCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Entry { get; set; }

        // NoLinea no lo asigna el usuario: el backend calcula max(NoLinea existentes del Entry) + 1.
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`API.Application.DTO/entrega/EntregaDetalleActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.entrega
{
    public class EntregaDetalleActualizarDTO
    {
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

- [ ] **Step 5: Crear la capa de dominio de Entrega**

`API.Domain.Interface/IEntregaDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntregaDomain
    {
        #region async methods
        Task<int> InsertarAsync(Entrega obj);
        Task<bool> ActualizarAsync(int id, Entrega obj);
        Task<bool> EliminarAsync(int id);
        Task<Entrega> ObtenerAsync(int id);
        Task<IQueryable<Entrega>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Domain.Core/EntregaDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class EntregaDomain : IEntregaDomain
    {
        // Código de objeto/documento reservado para Entregas -- exigido por el CHECK constraint
        // de la tabla (TipoObjeto='5'). Se fuerza siempre en el servidor, sin confiar en lo que
        // envíe el cliente.
        private const string TipoObjetoEntrega = "5";

        private readonly IRepositorioGenerico<Entrega, int> _repoGenericoEntrega;
        private readonly IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public EntregaDomain(
            IRepositorioGenerico<Entrega, int> repoGenericoEntrega,
            IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoEntrega = repoGenericoEntrega;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<int> InsertarAsync(Entrega obj)
        {
            obj.TipoObjeto = TipoObjetoEntrega;

            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar entregas.");
            }

            if (serie.Manual == "S")
            {
                // Serie manual: el número lo escribe el usuario, el consecutivo automático no aplica.
                if (obj.NumDoc <= 0)
                {
                    throw new Exception("El número de documento es requerido para series manuales.");
                }
            }
            else
            {
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el entrega -- no
                // al solo consultar/previsualizar el número.
                if (serie.SigNumero == null)
                {
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                }

                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                {
                    throw new Exception("Se agotó la numeración disponible en esta serie.");
                }

                obj.NumDoc = serie.SigNumero.Value;

                // No se llama a _repoGenericoNumeracion.ActualizarAsync aquí a propósito: "serie"
                // ya es una entidad rastreada por el mismo ApiDbTestContext que usa
                // _repoGenericoEntrega (ambos repos genéricos se resuelven en el mismo scope de la
                // petición), así que este cambio en memoria queda pendiente y se guarda junto con
                // el INSERT del entrega en el único SaveChangesAsync de abajo -- las dos operaciones
                // quedan en la misma transacción implícita: si el INSERT falla, el incremento del
                // consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoEntrega.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, Entrega obj)
        {
            obj.TipoObjeto = TipoObjetoEntrega;
            return await _repoGenericoEntrega.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            // No existe FK/cascada entre EntregaDetalle.Entry y Entrega.Entry en la base de datos,
            // así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoEntrega.EliminarAsync(id);
        }

        public async Task<Entrega> ObtenerAsync(int id)
        {
            return await _repoGenericoEntrega.ObtenerAsync(id);
        }

        public async Task<IQueryable<Entrega>> ObtenerTodoAsync()
        {
            return await _repoGenericoEntrega.ObtenerTodoAsync();
        }
        #endregion
    }
}
```

`API.Domain.Interface/IEntregaDetalleDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntregaDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(EntregaDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, EntregaDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<EntregaDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<EntregaDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<EntregaDetalle>> ObtenerPorEntregaAsync(int entry);
        #endregion
    }
}
```

`API.Domain.Core/EntregaDetalleDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class EntregaDetalleDomain : IEntregaDetalleDomain
    {
        private readonly IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> _repoGenericoDet;

        public EntregaDetalleDomain(IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> repoGenericoDet)
        {
            _repoGenericoDet = repoGenericoDet;
        }

        #region async methods
        public async Task<int> InsertarAsync(EntregaDetalle obj)
        {
            var lineasExistentes = await ObtenerPorEntregaAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, EntregaDetalle obj)
        {
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<EntregaDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<EntregaDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<EntregaDetalle>> ObtenerPorEntregaAsync(int entry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
        #endregion
    }
}
```

- [ ] **Step 6: Crear los repositorios de Entrega**

`API.Infraestructure.Repository/EntregaRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class EntregaRepositorio : RepositorioGenericoEfCore<Entrega, int>
    {
        public EntregaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`API.Infraestructure.Repository/EntregaDetalleRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class EntregaDetalleRepositorio : RepositorioGenericoEfCore<EntregaDetalle, (int Entry, int NoLinea)>
    {
        public EntregaDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<EntregaDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
```

- [ ] **Step 7: Crear la capa de aplicación de Entrega**

`API.Application.Interface/IEntregaApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;

namespace API.Application.Interface
{
    public interface IEntregaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntregaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, EntregaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<EntregaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<EntregaDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Application.Main/EntregaApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class EntregaApplication : IEntregaApplication
    {
        private readonly IEntregaDomain _entregaDomain;
        private readonly IMapper _mapper;

        public EntregaApplication(IEntregaDomain entregaDomain, IMapper mapper)
        {
            _entregaDomain = entregaDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(EntregaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entrega = _mapper.Map<Entrega>(obj);
                respuesta.Dato = await _entregaDomain.InsertarAsync(entrega);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, EntregaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entrega = _mapper.Map<Entrega>(obj);
                respuesta.Dato = await _entregaDomain.ActualizarAsync(id, entrega);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _entregaDomain.EliminarAsync(id);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<EntregaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<EntregaDTO>();
            try
            {
                var entrega = await _entregaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<EntregaDTO>(entrega);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntregaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDTO>>();
            try
            {
                var queryable = await _entregaDomain.ObtenerTodoAsync();
                var entregas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<EntregaDTO>>(entregas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
```

`API.Application.Interface/IEntregaDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;

namespace API.Application.Interface
{
    public interface IEntregaDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(EntregaDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntregaDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<EntregaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerPorEntregaAsync(int entry);
        #endregion
    }
}
```

`API.Application.Main/EntregaDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class EntregaDetalleApplication : IEntregaDetalleApplication
    {
        private readonly IEntregaDetalleDomain _entregaDetalleDomain;
        private readonly IMapper _mapper;

        public EntregaDetalleApplication(IEntregaDetalleDomain entregaDetalleDomain, IMapper mapper)
        {
            _entregaDetalleDomain = entregaDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(EntregaDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<EntregaDetalle>(obj);
                respuesta.Dato = await _entregaDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntregaDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<EntregaDetalle>(obj);
                respuesta.Dato = await _entregaDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _entregaDetalleDomain.EliminarAsync(entry, noLinea);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<EntregaDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<EntregaDetalleDTO>();
            try
            {
                var entidad = await _entregaDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<EntregaDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDetalleDTO>>();
            try
            {
                var queryable = await _entregaDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<EntregaDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerPorEntregaAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDetalleDTO>>();
            try
            {
                var lista = await _entregaDetalleDomain.ObtenerPorEntregaAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<EntregaDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
```

- [ ] **Step 8: Crear los controladores API de Entrega**

`API.Service.WebApi/Controllers/EntregaController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Entrega")]
    public class EntregaController : ControllerBase
    {
        private readonly IEntregaApplication _entregaApplication;

        public EntregaController(IEntregaApplication entregaApplication)
        {
            _entregaApplication = entregaApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<EntregaDTO>>> Obtener([FromRoute] int id)
        {
            var entrega = await _entregaApplication.ObtenerAsync(id);

            if (!entrega.Resultado)
            {
                return BadRequest(entrega);
            }

            if (entrega.Dato == null)
            {
                entrega.Resultado = false;
                entrega.Mensaje = "El código del entrega no se encontró.";
                return NotFound(entrega);
            }

            return Ok(entrega);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<EntregaDTO>>>> ObtenerTodoAsync()
        {
            var entregas = await _entregaApplication.ObtenerTodoAsync();

            if (!entregas.Resultado)
            {
                return BadRequest(entregas);
            }

            return Ok(entregas);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] EntregaCrearDTO obj)
        {
            var insert = await _entregaApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] EntregaActualizarDTO obj)
        {
            var entrega = await _entregaApplication.ObtenerAsync(id);

            if (entrega.Dato == null)
            {
                entrega.Resultado = false;
                entrega.Mensaje = "El código del entrega no se encontró.";
                return NotFound(entrega);
            }

            var update = await _entregaApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var entrega = await _entregaApplication.ObtenerAsync(id);

            if (entrega.Dato == null)
            {
                entrega.Resultado = false;
                entrega.Mensaje = "El código del entrega no se encontró.";
                return NotFound(entrega);
            }

            var delete = await _entregaApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
```

`API.Service.WebApi/Controllers/EntregaDetalleController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/EntregaDetalle")]
    public class EntregaDetalleController : ControllerBase
    {
        private readonly IEntregaDetalleApplication _entregaDetalleApplication;

        public EntregaDetalleController(IEntregaDetalleApplication entregaDetalleApplication)
        {
            _entregaDetalleApplication = entregaDetalleApplication;
        }

        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<EntregaDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _entregaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (!det.Resultado)
            {
                return BadRequest(det);
            }

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            return Ok(det);
        }

        [HttpGet("PorEntrega/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<EntregaDetalleDTO>>>> ObtenerPorEntrega([FromRoute] int entry)
        {
            var detalles = await _entregaDetalleApplication.ObtenerPorEntregaAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<EntregaDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _entregaDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] EntregaDetalleCrearDTO obj)
        {
            var insert = await _entregaDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] EntregaDetalleActualizarDTO obj)
        {
            var det = await _entregaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _entregaDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _entregaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _entregaDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
```

- [ ] **Step 9: Registrar Entrega en la inyección de dependencias**

En `API.Service.WebApi/Startup.cs`, junto a las líneas de `Cotizacion`/`CotizacionDetalle`, agregar:
```csharp
            services.AddTransient<IRepositorioGenerico<Entrega, int>, EntregaRepositorio>();
            services.AddTransient<IEntregaDomain, EntregaDomain>();
            services.AddTransient<IEntregaApplication, EntregaApplication>();

            services.AddTransient<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>, EntregaDetalleRepositorio>();
            services.AddTransient<IEntregaDetalleDomain, EntregaDetalleDomain>();
            services.AddTransient<IEntregaDetalleApplication, EntregaDetalleApplication>();
```

- [ ] **Step 10: Registrar los mapeos de AutoMapper**

En `API.Transversal.Mapper/PerfilMapeo.cs`, agregar `using API.Application.DTO.entrega;` junto a los demás `using`, y junto a los `CreateMap` de Cotizacion:
```csharp
            // Entrega
            CreateMap<Entrega, EntregaDTO>();
            CreateMap<EntregaCrearDTO, Entrega>();
            CreateMap<EntregaActualizarDTO, Entrega>();

            // EntregaDetalle
            CreateMap<EntregaDetalle, EntregaDetalleDTO>();
            CreateMap<EntregaDetalleCrearDTO, EntregaDetalle>();
            CreateMap<EntregaDetalleActualizarDTO, EntregaDetalle>();
```

- [ ] **Step 11: Compilar la API para confirmar que todo lo anterior encaja**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\API && dotnet build API.sln -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish"`
Expected: `0 Errores`. Si hay errores de tipos/usings, corregirlos antes de seguir (los pasos de test dependen de que esto compile).

- [ ] **Step 12: Escribir las pruebas de `EntregaController`**

`API.Service.WebApi.Tests/Controllers/EntregaControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class EntregaControllerTests
    {
        private readonly Mock<IEntregaApplication> _applicationMock;
        private readonly EntregaController _controller;

        public EntregaControllerTests()
        {
            _applicationMock = new Mock<IEntregaApplication>();
            _controller = new EntregaController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<EntregaDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<EntregaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<EntregaDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código del entrega no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new EntregaDTO { Entry = 1, NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<EntregaDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDTO>> { Resultado = true, Dato = new List<EntregaDTO> { new EntregaDTO { Entry = 1 } } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new EntregaCrearDTO { NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new EntregaCrearDTO { NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<EntregaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, new EntregaActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<EntregaDTO> { Resultado = true, Dato = new EntregaDTO { Entry = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<EntregaActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new EntregaActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<EntregaDTO> { Resultado = true, Dato = new EntregaDTO { Entry = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<EntregaActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new EntregaActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<EntregaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<EntregaDTO> { Resultado = true, Dato = new EntregaDTO { Entry = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<EntregaDTO> { Resultado = true, Dato = new EntregaDTO { Entry = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
```

- [ ] **Step 13: Escribir las pruebas de `EntregaDetalleController`**

`API.Service.WebApi.Tests/Controllers/EntregaDetalleControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.entrega;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class EntregaDetalleControllerTests
    {
        private readonly Mock<IEntregaDetalleApplication> _applicationMock;
        private readonly EntregaDetalleController _controller;

        public EntregaDetalleControllerTests()
        {
            _applicationMock = new Mock<IEntregaDetalleApplication>();
            _controller = new EntregaDetalleController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<EntregaDetalleDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<EntregaDetalleDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new EntregaDetalleDTO { Entry = 1, NoLinea = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorEntrega_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorEntregaAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorEntrega(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorEntrega_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<EntregaDetalleDTO> { new EntregaDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerPorEntregaAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorEntrega(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<EntregaDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<EntregaDetalleDTO> { new EntregaDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new EntregaDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new EntregaDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, 1, new EntregaDetalleActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = new EntregaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<EntregaDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new EntregaDetalleActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = new EntregaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<EntregaDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new EntregaDetalleActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = new EntregaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<EntregaDetalleDTO> { Resultado = true, Dato = new EntregaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
```

- [ ] **Step 14: Escribir las pruebas de dominio de `EntregaDomain`**

`API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs`:
```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EntregaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _repoEntregaMock;
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _repoDetalleMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly EntregaDomain _domain;

        public EntregaDomainTests()
        {
            _repoEntregaMock = new Mock<IRepositorioGenerico<Entrega, int>>();
            _repoDetalleMock = new Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new EntregaDomain(_repoEntregaMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "5",
            Serie = 4,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N"
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_AsignaSigNumeroYLoIncrementa()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoEntregaMock.Setup(r => r.InsertarAsync(It.IsAny<Entrega>()))
                .ReturnsAsync((Entrega c) => { c.Entry = 99; return c; });

            var obj = new Entrega { Serie = 4, NumDoc = 0, TipoObjeto = "algo-que-el-cliente-mando" };
            var entry = await _domain.InsertarAsync(obj);

            Assert.Equal(99, entry);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal("5", obj.TipoObjeto);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaNumDocDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoEntregaMock.Setup(r => r.InsertarAsync(It.IsAny<Entrega>()))
                .ReturnsAsync((Entrega c) => { c.Entry = 1; return c; });

            var obj = new Entrega { Serie = 4, NumDoc = 12345 };
            await _domain.InsertarAsync(obj);

            Assert.Equal(12345, obj.NumDoc);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Entrega { Serie = 4, NumDoc = 0 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoEntregaMock.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Entrega { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoEntregaMock.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Entrega { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoEntregaMock.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new Entrega { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoEntregaMock.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_FuerzaTipoObjetoACuatro()
        {
            _repoEntregaMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Entrega>())).ReturnsAsync(true);

            var obj = new Entrega { TipoObjeto = "otro-valor" };
            var resultado = await _domain.ActualizarAsync(1, obj);

            Assert.True(resultado);
            Assert.Equal("5", obj.TipoObjeto);
        }
    }
}
```

- [ ] **Step 15: Correr toda la suite de pruebas de la API**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\API && dotnet test API.Service.WebApi.Tests/API.Service.WebApi.Tests.csproj -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish_tests"`
Expected: todas las pruebas en verde (las 376 anteriores + las 7 nuevas de `EntregaDomainTests` + las de `EntregaControllerTests`/`EntregaDetalleControllerTests`).

- [ ] **Step 16: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\API
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar módulo Entrega (API completa)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

### Task 5: Cliente HTTP de Entrega en Web.ApiClient

**Files:**
- Create: `Web.ApiClient/Dtos/Entrega/EntregaDTO.cs`, `EntregaCrearDTO.cs`, `EntregaActualizarDTO.cs`
- Create: `Web.ApiClient/Dtos/EntregaDetalle/EntregaDetalleDTO.cs`, `EntregaDetalleCrearDTO.cs`, `EntregaDetalleActualizarDTO.cs`
- Create: `Web.ApiClient/Clientes/IEntregaApiClient.cs`, `EntregaApiClient.cs`
- Create: `Web.ApiClient/Clientes/IEntregaDetalleApiClient.cs`, `EntregaDetalleApiClient.cs`
- Modify: `Web.UI/Program.cs`

**Interfaces:**
- Consumes: rutas API `api/Entrega`, `api/EntregaDetalle` (Task 4).
- Produces: `IEntregaApiClient.{ObtenerTodoAsync,ObtenerAsync,InsertarAsync,ActualizarAsync,EliminarAsync}`, `IEntregaDetalleApiClient.{ObtenerTodoAsync,ObtenerPorEntregaAsync,ObtenerAsync,InsertarAsync,ActualizarAsync,EliminarAsync}` -- usados por el controlador Web en Task 3.

- [ ] **Step 1: Crear los DTOs de Entrega en Web.ApiClient**

`Web.ApiClient/Dtos/Entrega/EntregaDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Entrega
{
    public class EntregaDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/Entrega/EntregaCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Web.ApiClient.Dtos.Entrega
{
    public class EntregaCrearDTO
    {
        // Requerido solo para series "Manual" -- para series autogeneradas la API calcula el
        // siguiente número al registrar el entrega, así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/Entrega/EntregaActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Entrega
{
    public class EntregaActualizarDTO
    {
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/EntregaDetalle/EntregaDetalleDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.EntregaDetalle
{
    public class EntregaDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`Web.ApiClient/Dtos/EntregaDetalle/EntregaDetalleCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Web.ApiClient.Dtos.EntregaDetalle
{
    public class EntregaDetalleCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Entry { get; set; }

        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`Web.ApiClient/Dtos/EntregaDetalle/EntregaDetalleActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.EntregaDetalle
{
    public class EntregaDetalleActualizarDTO
    {
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

- [ ] **Step 2: Crear los clientes HTTP de Entrega**

`Web.ApiClient/Clientes/IEntregaApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Entrega;

namespace Web.ApiClient.Clientes
{
    public interface IEntregaApiClient
    {
        Task<Respuesta<IEnumerable<EntregaDTO>>> ObtenerTodoAsync();
        Task<Respuesta<EntregaDTO>> ObtenerAsync(int entry);
        Task<Respuesta<int>> InsertarAsync(EntregaCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, EntregaActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry);
    }
}
```

`Web.ApiClient/Clientes/EntregaApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Entrega;

namespace Web.ApiClient.Clientes
{
    public class EntregaApiClient : ApiClientBase, IEntregaApiClient
    {
        private const string Recurso = "api/Entrega";

        public EntregaApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<EntregaDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<EntregaDTO>>(Recurso);

        public Task<Respuesta<EntregaDTO>> ObtenerAsync(int entry) =>
            GetAsync<EntregaDTO>($"{Recurso}/{entry}");

        public Task<Respuesta<int>> InsertarAsync(EntregaCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, EntregaActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry) =>
            DeleteAsync<bool>($"{Recurso}/{entry}");
    }
}
```

`Web.ApiClient/Clientes/IEntregaDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.EntregaDetalle;

namespace Web.ApiClient.Clientes
{
    public interface IEntregaDetalleApiClient
    {
        Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerPorEntregaAsync(int entry);
        Task<Respuesta<EntregaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<int>> InsertarAsync(EntregaDetalleCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntregaDetalleActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
    }
}
```

`Web.ApiClient/Clientes/EntregaDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.EntregaDetalle;

namespace Web.ApiClient.Clientes
{
    public class EntregaDetalleApiClient : ApiClientBase, IEntregaDetalleApiClient
    {
        private const string Recurso = "api/EntregaDetalle";

        public EntregaDetalleApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<EntregaDetalleDTO>>(Recurso);

        public Task<Respuesta<IEnumerable<EntregaDetalleDTO>>> ObtenerPorEntregaAsync(int entry) =>
            GetAsync<IEnumerable<EntregaDetalleDTO>>($"{Recurso}/PorEntrega/{entry}");

        public Task<Respuesta<EntregaDetalleDTO>> ObtenerAsync(int entry, int noLinea) =>
            GetAsync<EntregaDetalleDTO>($"{Recurso}/{entry}/{noLinea}");

        public Task<Respuesta<int>> InsertarAsync(EntregaDetalleCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, EntregaDetalleActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}/{noLinea}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea) =>
            DeleteAsync<bool>($"{Recurso}/{entry}/{noLinea}");
    }
}
```

- [ ] **Step 3: Registrar los HttpClient tipados en `Program.cs`**

Junto a las líneas de `ICotizacionApiClient`/`ICotizacionDetalleApiClient`, agregar:
```csharp
builder.Services.AddHttpClient<IEntregaApiClient, EntregaApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IEntregaDetalleApiClient, EntregaDetalleApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 4: Compilar Web.ApiClient y Web.UI**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\Web && dotnet build Web.slnx -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\web_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 5: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\Web
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar cliente HTTP de Entrega en Web.ApiClient

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

### Task 6: Pantalla Web de Entregas

**Files:**
- Create: `Web.UI/Controllers/EntregasController.cs`
- Create: `Web.UI/Views/Entregas/Index.cshtml`, `_Form.cshtml`
- Create: `Web.UI/wwwroot/js/entregas.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `IEntregaApiClient`, `IEntregaDetalleApiClient` (Task 5); `ISocioNegocioApiClient`, `IMonedaApiClient`, `IArticuloApiClient`, `IAlmacenApiClient`, `IImpuestoApiClient`, `INumeracionDocumentoDetApiClient` (ya existentes, usados igual que en `CotizacionesController`).

- [ ] **Step 1: Crear `EntregasController`**

`Web.UI/Controllers/EntregasController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.Entrega;
using Web.ApiClient.Dtos.EntregaDetalle;

namespace Web.UI.Controllers
{
    [Authorize]
    public class EntregasController : Controller
    {
        private readonly IEntregaApiClient _entregas;
        private readonly IEntregaDetalleApiClient _detalles;
        private readonly ISocioNegocioApiClient _socios;
        private readonly IMonedaApiClient _monedas;
        private readonly IArticuloApiClient _articulos;
        private readonly IAlmacenApiClient _almacenes;
        private readonly IImpuestoApiClient _impuestos;
        private readonly INumeracionDocumentoDetApiClient _series;

        // CodigoObj de NumeracionDocumento que identifica a "Entregas" como tipo de objeto.
        private const string CodigoObjEntrega = "5";
        private const string SubTipoDocEntrega = "--";

        public EntregasController(
            IEntregaApiClient entregas,
            IEntregaDetalleApiClient detalles,
            ISocioNegocioApiClient socios,
            IMonedaApiClient monedas,
            IArticuloApiClient articulos,
            IAlmacenApiClient almacenes,
            IImpuestoApiClient impuestos,
            INumeracionDocumentoDetApiClient series)
        {
            _entregas = entregas;
            _detalles = detalles;
            _socios = socios;
            _monedas = monedas;
            _articulos = articulos;
            _almacenes = almacenes;
            _impuestos = impuestos;
            _series = series;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _entregas.ObtenerTodoAsync();
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> FormularioCrear()
        {
            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjEntrega);
            ViewBag.SeriesEntrega = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocEntrega);
            ViewBag.EsEdicion = false;
            return PartialView("_Form", new EntregaCrearDTO { EstadoDoc = "A", TipoObjeto = "5" });
        }

        [HttpGet]
        public async Task<IActionResult> FormularioEditar(int entry)
        {
            var respuesta = await _entregas.ObtenerAsync(entry);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            await CargarDropdownsAsync();
            ViewBag.EsEdicion = true;
            ViewBag.EntryActual = entry;

            var serieInfo = await _series.ObtenerAsync(respuesta.Dato.Serie);
            ViewBag.NombreSerieActual = serieInfo.Resultado ? serieInfo.Dato?.NombreSerie : null;

            var dto = new EntregaCrearDTO
            {
                NumDoc = respuesta.Dato.NumDoc,
                Serie = respuesta.Dato.Serie,
                EstadoDoc = respuesta.Dato.EstadoDoc,
                TipoObjeto = respuesta.Dato.TipoObjeto,
                FechaDoc = respuesta.Dato.FechaDoc,
                FechaEmision = respuesta.Dato.FechaEmision,
                CodigoSn = respuesta.Dato.CodigoSn,
                NombreSn = respuesta.Dato.NombreSn,
                Direccion = respuesta.Dato.Direccion,
                MonedaDoc = respuesta.Dato.MonedaDoc,
                PrctjeImpuesto = respuesta.Dato.PrctjeImpuesto,
                TotalImp = respuesta.Dato.TotalImp,
                PrctjeDesc = respuesta.Dato.PrctjeDesc,
                TotalDesc = respuesta.Dato.TotalDesc,
                TotalBruto = respuesta.Dato.TotalBruto,
                TotalDoc = respuesta.Dato.TotalDoc,
                Comentario = respuesta.Dato.Comentario
            };

            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromBody] EntregaCrearDTO dto)
        {
            var respuesta = await _entregas.InsertarAsync(dto);
            if (!respuesta.Resultado)
                return Json(respuesta);

            var creado = await _entregas.ObtenerAsync(respuesta.Dato);
            return Json(new { respuesta.Resultado, respuesta.Mensaje, dato = respuesta.Dato, numDoc = creado.Dato?.NumDoc });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int entry, [FromBody] EntregaCrearDTO dto)
        {
            var actual = await _entregas.ObtenerAsync(entry);
            if (!actual.Resultado || actual.Dato is null)
                return NotFound(actual);

            var actualizar = new EntregaActualizarDTO
            {
                NumDoc = actual.Dato.NumDoc,
                Serie = actual.Dato.Serie,
                EstadoDoc = dto.EstadoDoc,
                TipoObjeto = dto.TipoObjeto,
                FechaDoc = dto.FechaDoc,
                FechaEmision = dto.FechaEmision,
                CodigoSn = dto.CodigoSn,
                NombreSn = dto.NombreSn,
                Direccion = dto.Direccion,
                MonedaDoc = dto.MonedaDoc,
                PrctjeImpuesto = dto.PrctjeImpuesto,
                TotalImp = dto.TotalImp,
                PrctjeDesc = dto.PrctjeDesc,
                TotalDesc = dto.TotalDesc,
                TotalBruto = dto.TotalBruto,
                TotalDoc = dto.TotalDoc,
                Comentario = dto.Comentario
            };

            var respuesta = await _entregas.ActualizarAsync(entry, actualizar);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int entry)
        {
            var respuesta = await _entregas.EliminarAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDetalle(int entry)
        {
            var respuesta = await _detalles.ObtenerPorEntregaAsync(entry);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearLinea([FromBody] EntregaDetalleCrearDTO dto)
        {
            var respuesta = await _detalles.InsertarAsync(dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarLinea(int entry, int noLinea, [FromBody] EntregaDetalleActualizarDTO dto)
        {
            var respuesta = await _detalles.ActualizarAsync(entry, noLinea, dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarLinea(int entry, int noLinea)
        {
            var respuesta = await _detalles.EliminarAsync(entry, noLinea);
            return Json(respuesta);
        }

        private async Task CargarDropdownsAsync()
        {
            var socios = await _socios.ObtenerTodoAsync();
            var monedas = await _monedas.ObtenerTodoAsync();
            var articulos = await _articulos.ObtenerTodoAsync();
            var almacenes = await _almacenes.ObtenerTodoAsync();
            var impuestos = await _impuestos.ObtenerTodoAsync();

            ViewBag.Socios = new SelectList(socios.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Articulos = articulos.Dato ?? [];
            ViewBag.Almacenes = new SelectList(almacenes.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Impuestos = impuestos.Dato ?? [];
        }
    }
}
```

- [ ] **Step 2: Crear `Views/Entregas/Index.cshtml`**

```html
@{
    ViewData["Title"] = "Entregas";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Entregas</h3>
    <button type="button" class="btn btn-primary" id="btnNuevo">
        <i class="fa-solid fa-plus me-1"></i>Nuevo
    </button>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblEntregas" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>No. Documento</th>
                        <th>Socio de negocio</th>
                        <th>Fecha</th>
                        <th>Estado</th>
                        <th>Total</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalFormulario" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-xl modal-dialog-scrollable">
        <div class="modal-content" id="contenidoModal">
            <!-- se carga por AJAX -->
        </div>
    </div>
</div>

@section Scripts {
    <script src="~/js/entregas.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: Crear `Views/Entregas/_Form.cshtml`**

Idéntico a `Views/Cotizaciones/_Form.cshtml`, cambiando el modelo, el título, los ids de tabla/script/formulario y las rutas de acción:
```html
@using System.Text.Json
@model Web.ApiClient.Dtos.Entrega.EntregaCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
    var opcionesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? "Editar entrega" : "Nuevo entrega")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formEntrega" novalidate>
        <div asp-validation-summary="ModelOnly" class="alert alert-danger py-2 px-3 small"></div>

        <div class="row g-3">
            @if (!esEdicion)
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <select id="selectSerieEntrega" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                    <span class="form-text">Si eliges una serie distinta de "Manual", el número se genera solo al guardar.</span>
                </div>
            }
            else
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <input class="form-control" value="@ViewBag.NombreSerieActual" disabled />
                </div>
            }
            <div class="col-md-3">
                <label asp-for="NumDoc" class="form-label">No. documento</label>
                <input asp-for="NumDoc" type="number" class="form-control" readonly="@esEdicion" />
                <span asp-validation-for="NumDoc" class="text-danger small"></span>
            </div>
            <div class="col-md-3">
                <label asp-for="EstadoDoc" class="form-label">Estado</label>
                <select asp-for="EstadoDoc" class="form-select">
                    <option value="A">Activo</option>
                    <option value="C">Cancelado</option>
                </select>
            </div>
            <div class="col-md-3" hidden>
                <label asp-for="TipoObjeto" class="form-label">Tipo</label>
                <input asp-for="TipoObjeto" class="form-control" />
            </div>

            <div class="col-md-4">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <select asp-for="CodigoSn" id="selectCodigoSn" class="form-select" asp-items="ViewBag.Socios">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
            <div class="col-md-4">
                <label asp-for="NombreSn" class="form-label">Nombre</label>
                <input asp-for="NombreSn" class="form-control" />
            </div>
            <div class="col-md-4">
                <label asp-for="MonedaDoc" class="form-label">Moneda</label>
                <select asp-for="MonedaDoc" class="form-select" asp-items="ViewBag.Monedas">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>

            <div class="col-md-4">
                <label asp-for="Direccion" class="form-label"></label>
                <input asp-for="Direccion" class="form-control" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha documento</label>
                <input type="date" name="FechaDoc" id="FechaDoc" class="form-control" value="@Model.FechaDoc?.ToString("yyyy-MM-dd")" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha emisión</label>
                <input type="date" name="FechaEmision" id="FechaEmision" class="form-control" value="@Model.FechaEmision?.ToString("yyyy-MM-dd")" />
            </div>

            <div class="col-md-3">
                <label asp-for="PrctjeDesc" class="form-label">% Descuento</label>
                <input asp-for="PrctjeDesc" type="number" step="0.01" class="form-control" />
            </div>
            <div class="col-md-3">
                <label asp-for="PrctjeImpuesto" class="form-label">% Impuesto</label>
                <input asp-for="PrctjeImpuesto" type="number" step="0.01" class="form-control" />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total bruto</label>
                <input id="TotalBruto" class="form-control" value="@Model.TotalBruto" disabled />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total documento</label>
                <input id="TotalDoc" class="form-control" value="@Model.TotalDoc" disabled />
            </div>

            <div class="col-12">
                <label asp-for="Comentario" class="form-label"></label>
                <textarea asp-for="Comentario" class="form-control" rows="2"></textarea>
            </div>
        </div>
    </form>

    <hr />
    <div class="d-flex justify-content-between align-items-center mb-2">
        <h6 class="mb-0">Detalle</h6>
        <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevaLinea">
            <i class="fa-solid fa-plus me-1"></i>Agregar línea
        </button>
    </div>

    @if (!esEdicion)
    {
        <p class="text-muted small">Las líneas agregadas aquí se guardarán junto con el entrega.</p>
    }

    <div class="table-responsive">
        <table id="tblDetalleEntrega" class="table table-sm table-hover align-middle w-100" data-entry="@ViewBag.EntryActual" data-es-edicion="@esEdicion.ToString().ToLower()">
            <thead>
                <tr>
                    <th>Artículo</th>
                    <th>Descripción</th>
                    <th>Cantidad</th>
                    <th>Precio</th>
                    <th>% Desc.</th>
                    <th>Impuesto</th>
                    <th>Total línea</th>
                    <th class="text-end">Acciones</th>
                </tr>
            </thead>
            <tbody></tbody>
        </table>
    </div>

    <div id="panelLineaDetalle" class="border rounded p-3 mb-2 d-none">
        <form id="formLineaDetalle">
            <input type="hidden" id="detNoLineaOriginal" value="" />
            <div class="row g-2">
                <div class="col-md-4">
                    <label class="form-label">Artículo</label>
                    <select id="detCodArticulo" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Almacén</label>
                    <select name="CodAlmacen" id="detCodAlmacen" class="form-select" asp-items="ViewBag.Almacenes">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Impuesto</label>
                    <select id="detCodigoImpuesto" class="form-select">
                        <option value="">-- Ninguno --</option>
                    </select>
                </div>

                <div class="col-md-8">
                    <label class="form-label">Descripción</label>
                    <input name="Descripcion" id="detDescripcion" class="form-control" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Cantidad</label>
                    <input name="Cantidad" id="detCantidad" type="number" step="0.01" class="form-control" value="1" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Precio</label>
                    <input name="Precio" id="detPrecio" type="number" step="0.01" class="form-control" />
                </div>

                <div class="col-md-2">
                    <label class="form-label">% Desc.</label>
                    <input name="PrctjeDesc" id="detPrctjeDesc" type="number" step="0.01" class="form-control" value="0" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Impuesto (Q)</label>
                    <input name="Impuesto" id="detImpuestoMonto" type="number" step="0.01" class="form-control" readonly />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Total línea</label>
                    <input name="TotalLinea" id="detTotalLinea" type="number" step="0.01" class="form-control" readonly />
                </div>
            </div>
            <div class="text-end mt-2">
                <button type="button" class="btn btn-sm btn-secondary" id="btnCancelarLinea">Cancelar</button>
                <button type="button" class="btn btn-sm btn-primary" id="btnGuardarLinea">Guardar línea</button>
            </div>
        </form>
    </div>

    <script id="datosArticulosEntrega" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Articulos, opcionesJson))
    </script>
    <script id="datosImpuestosEntrega" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Impuestos, opcionesJson))
    </script>

    @if (!esEdicion)
    {
        <script id="datosSeriesEntrega" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(ViewBag.SeriesEntrega, opcionesJson))
        </script>
    }
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
    <button type="button" class="btn btn-primary" id="btnGuardarEntrega" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
        <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
    </button>
</div>
```

- [ ] **Step 4: Crear `wwwroot/js/entregas.js`**

Idéntico a `cotizaciones.js`, sustituyendo cada identificador `cotizacion(es)`/`Cotizacion(es)` por `entrega(s)`/`Entrega(s)` y cada endpoint `/Cotizaciones/...` por `/Entregas/...`:
```javascript
$(function () {
    const tabla = $('#tblEntregas').DataTable({
        ajax: { url: '/Entregas/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'numDoc' },
            { data: 'nombreSn', render: (d, t, row) => d || row.codigoSn || '' },
            { data: 'fechaDoc', render: d => d ? new Date(d).toLocaleDateString() : '' },
            { data: 'estadoDoc', render: d => d === 'C' ? '<span class="badge text-bg-secondary">Cancelado</span>' : '<span class="badge text-bg-success">Activo</span>' },
            { data: 'totalDoc', render: d => d != null ? Number(d).toFixed(2) : '' },
            {
                data: 'entry', orderable: false, className: 'text-end',
                render: entry => `
                    <button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"><i class="fa-solid fa-pen"></i></button>
                    <button class="btn btn-sm btn-outline-danger btn-eliminar" data-entry="${entry}"><i class="fa-solid fa-trash"></i></button>
                `
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSerieEntrega();
        inicializarDetalle();
    }

    $('#btnNuevo').on('click', async function () {
        const html = await $.get('/Entregas/FormularioCrear');
        abrirModal(html);
    });

    $('#tblEntregas').on('click', '.btn-editar', async function () {
        const entry = $(this).data('entry');
        const html = await $.get('/Entregas/FormularioEditar', { entry });
        abrirModal(html);
    });

    $('#tblEntregas').on('click', '.btn-eliminar', async function () {
        const entry = $(this).data('entry');
        const confirmado = await App.confirmarEliminar(`Se eliminará el entrega #${entry}.`);
        if (!confirmado) return;

        const respuesta = await App.eliminar(`/Entregas/Eliminar?entry=${entry}`);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Entrega eliminado correctamente.');
        recargarTabla();
    });

    // --- Serie de numeración para generar el número de documento (solo aplica al crear) ---

    function inicializarSerieEntrega() {
        const $sel = $('#selectSerieEntrega');
        if ($sel.length === 0) return;

        const datosEl = document.getElementById('datosSeriesEntrega');
        const series = datosEl ? (JSON.parse(datosEl.textContent) || []) : [];

        $sel.html('<option value="">-- Seleccione --</option>');
        let serieManual = null;
        series.forEach(s => {
            const serie = s.serie ?? s.Serie;
            const nombre = s.nombreSerie ?? s.NombreSerie;
            const manual = s.manual ?? s.Manual;
            if (manual === 'S' && serieManual === null) serieManual = serie;
            $sel.append(`<option value="${serie}" data-manual="${manual}">${nombre}</option>`);
        });

        if (serieManual !== null) $sel.val(serieManual);

        actualizarNumDocSegunSerie();
    }

    function esSerieManualEntrega() {
        const $sel = $('#selectSerieEntrega');
        if ($sel.length === 0 || !$sel.val()) return true;
        return $sel.find('option:selected').data('manual') === 'S';
    }

    function actualizarNumDocSegunSerie() {
        const $numDoc = $('#NumDoc');
        if ($numDoc.length === 0) return;

        if (esSerieManualEntrega()) {
            $numDoc.prop('disabled', false).attr('placeholder', '');
        } else {
            $numDoc.val('').prop('disabled', true).attr('placeholder', 'Se generará al guardar');
        }
    }

    $(document).on('change', '#selectSerieEntrega', actualizarNumDocSegunSerie);

    // Auto-completa el nombre del socio de negocio al elegirlo (queda editable después).
    $(document).on('change', '#selectCodigoSn', function () {
        const texto = $(this).find('option:selected').text();
        if (texto && texto !== '-- Seleccione --') {
            $('#NombreSn').val(texto);
        }
    });

    $(document).on('click', '#btnGuardarEntrega', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const entry = $boton.data('entry');

        if (!esEdicion) {
            const serieSeleccionada = $('#selectSerieEntrega').val();
            if (!serieSeleccionada) {
                App.mostrarError('Debes seleccionar una serie.');
                return;
            }
        }

        // El número de documento (No. documento) no se solicita aquí para series no manuales: el
        // servidor lo calcula y avanza el consecutivo al registrar el entrega (ver
        // EntregaDomain.InsertarAsync en la API), no antes. Para series Manual, el campo #NumDoc
        // está habilitado y su valor viaja normalmente en recolectarFormulario.
        const datos = App.recolectarFormulario('#formEntrega');
        if (!esEdicion) {
            datos.Serie = $('#selectSerieEntrega').val();
        }

        const totales = calcularTotalesDesdeLineas(esEdicionDetalle() ? lineasRemotas : lineasLocales);
        datos.TotalBruto = totales.totalBruto;
        datos.TotalDesc = totales.totalDesc;
        datos.TotalImp = totales.totalImp;
        datos.TotalDoc = totales.totalDoc;

        if (!esEdicion) {
            const respuestaCabecera = await App.enviarJson('/Entregas/Crear', 'POST', datos);
            if (!respuestaCabecera.resultado) {
                App.mostrarError(respuestaCabecera.mensaje);
                return;
            }

            const entryCreado = respuestaCabecera.dato;

            if (respuestaCabecera.numDoc != null) {
                $('#NumDoc').val(respuestaCabecera.numDoc).prop('disabled', false);
            }

            let exitosas = 0;
            let fallidas = 0;

            for (const linea of lineasLocales) {
                const { _id, ...lineaSinId } = linea;
                const respuestaLinea = await App.enviarJson('/Entregas/CrearLinea', 'POST', {
                    ...lineaSinId,
                    Entry: entryCreado
                });

                if (respuestaLinea.resultado) {
                    exitosas++;
                } else {
                    fallidas++;
                    App.mostrarError(respuestaLinea.mensaje);
                }
            }

            const sufijoNumDoc = respuestaCabecera.numDoc != null ? ` No. documento: ${respuestaCabecera.numDoc}.` : '';
            if (fallidas > 0) {
                await App.mostrarExito(`Entrega creado correctamente. Líneas guardadas: ${exitosas} de ${exitosas + fallidas}.${sufijoNumDoc}`);
            } else {
                await App.mostrarExito(`Entrega creado correctamente.${sufijoNumDoc}`);
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            recargarTabla();
            return;
        }

        const respuesta = await App.enviarJson(`/Entregas/Editar?entry=${entry}`, 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }

        bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
        App.mostrarExito('Entrega actualizado correctamente.');
        recargarTabla();
    });

    // --- Detalle (grid anidado): en creación se administra localmente, en edición en vivo contra la API ---

    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let articulosDisponibles = [];
    let impuestosDisponibles = [];

    function esEdicionDetalle() {
        const v = $('#tblDetalleEntrega').data('es-edicion');
        return v === true || v === 'true';
    }

    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;

        const $tabla = $('#tblDetalleEntrega');
        if ($tabla.length === 0) return;

        const datosArt = document.getElementById('datosArticulosEntrega');
        articulosDisponibles = datosArt ? (JSON.parse(datosArt.textContent) || []) : [];

        const datosImp = document.getElementById('datosImpuestosEntrega');
        impuestosDisponibles = datosImp ? (JSON.parse(datosImp.textContent) || []) : [];

        const $selArt = $('#detCodArticulo');
        $selArt.html('<option value="">-- Seleccione --</option>');
        articulosDisponibles.forEach(a => {
            const codigo = a.codigo ?? a.Codigo;
            const nombre = a.nombre ?? a.Nombre;
            $selArt.append(`<option value="${codigo}">${codigo} - ${nombre ?? ''}</option>`);
        });

        const $selImp = $('#detCodigoImpuesto');
        $selImp.html('<option value="">-- Ninguno --</option>');
        impuestosDisponibles.forEach(i => {
            const codigo = i.codigo ?? i.Codigo;
            const nombre = i.nombre ?? i.Nombre;
            const tasa = i.tasa ?? i.Tasa ?? 0;
            $selImp.append(`<option value="${codigo}" data-tasa="${tasa}">${nombre} (${tasa}%)</option>`);
        });

        if (esEdicionDetalle()) {
            cargarDetalleRemoto();
        } else {
            pintarDetalle();
        }
    }

    async function cargarDetalleRemoto() {
        const entry = $('#tblDetalleEntrega').data('entry');
        const respuesta = await $.get('/Entregas/ObtenerDetalle', { entry });
        lineasRemotas = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        pintarDetalle();
    }

    function calcularTotalesDesdeLineas(lista) {
        let totalBruto = 0, totalDesc = 0, totalImp = 0, totalDoc = 0;
        lista.forEach(l => {
            const cantidad = Number(l.cantidad ?? l.Cantidad ?? 0);
            const precio = Number(l.precio ?? l.Precio ?? 0);
            const prctjeDesc = Number(l.prctjeDesc ?? l.PrctjeDesc ?? 0);
            const impuesto = Number(l.impuesto ?? l.Impuesto ?? 0);
            const bruto = cantidad * precio;
            const desc = bruto * (prctjeDesc / 100);
            totalBruto += bruto;
            totalDesc += desc;
            totalImp += impuesto;
            totalDoc += (bruto - desc + impuesto);
        });
        return {
            totalBruto: totalBruto.toFixed(2),
            totalDesc: totalDesc.toFixed(2),
            totalImp: totalImp.toFixed(2),
            totalDoc: totalDoc.toFixed(2)
        };
    }

    function pintarDetalle() {
        const $tbody = $('#tblDetalleEntrega tbody');
        if ($tbody.length === 0) return;

        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;

        const totales = calcularTotalesDesdeLineas(lista);
        $('#TotalBruto').val(totales.totalBruto);
        $('#TotalDoc').val(totales.totalDoc);

        if (lista.length === 0) {
            $tbody.html('<tr><td colspan="8" class="text-center text-muted">Sin líneas de detalle</td></tr>');
            return;
        }

        $tbody.html(lista.map(linea => {
            const noLinea = linea.noLinea ?? linea.NoLinea;
            const codArticulo = linea.codArticulo ?? linea.CodArticulo;
            const descripcion = linea.descripcion ?? linea.Descripcion;
            const cantidad = linea.cantidad ?? linea.Cantidad;
            const precio = linea.precio ?? linea.Precio;
            const prctjeDesc = linea.prctjeDesc ?? linea.PrctjeDesc;
            const impuesto = linea.impuesto ?? linea.Impuesto;
            const totalLinea = linea.totalLinea ?? linea.TotalLinea;
            const clave = esEdicionDetalle() ? noLinea : linea._id;
            return `
                <tr>
                    <td>${codArticulo ?? ''}</td>
                    <td>${descripcion ?? ''}</td>
                    <td>${cantidad ?? ''}</td>
                    <td>${precio != null ? Number(precio).toFixed(2) : ''}</td>
                    <td>${prctjeDesc ?? 0}</td>
                    <td>${impuesto != null ? Number(impuesto).toFixed(2) : '0.00'}</td>
                    <td>${totalLinea != null ? Number(totalLinea).toFixed(2) : ''}</td>
                    <td class="text-end">
                        <button type="button" class="btn btn-sm btn-outline-primary btn-editar-linea" data-clave="${clave}"><i class="fa-solid fa-pen"></i></button>
                        <button type="button" class="btn btn-sm btn-outline-danger btn-eliminar-linea" data-clave="${clave}"><i class="fa-solid fa-trash"></i></button>
                    </td>
                </tr>
            `;
        }).join(''));
    }

    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        $('#detCodArticulo').val('');
        $('#detCodAlmacen').val('');
        $('#detCodigoImpuesto').val('');
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }

    /** Recalcula el monto de impuesto y el total de la línea con base en los campos actuales del panel. */
    function recalcularLinea() {
        const cantidad = Number($('#detCantidad').val()) || 0;
        const precio = Number($('#detPrecio').val()) || 0;
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;
        const tasa = Number($('#detCodigoImpuesto').find('option:selected').data('tasa')) || 0;

        const bruto = cantidad * precio;
        const desc = bruto * (prctjeDesc / 100);
        const subtotal = bruto - desc;
        const impuesto = subtotal * (tasa / 100);
        const total = subtotal + impuesto;

        $('#detImpuestoMonto').val(impuesto.toFixed(2));
        $('#detTotalLinea').val(total.toFixed(2));
    }

    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc, #detCodigoImpuesto', recalcularLinea);

    $(document).on('change', '#detCodArticulo', function () {
        const codigo = $(this).val();
        const articulo = articulosDisponibles.find(a => (a.codigo ?? a.Codigo) === codigo);
        if (articulo) {
            $('#detDescripcion').val(articulo.nombre ?? articulo.Nombre ?? '');
            $('#detPrecio').val(articulo.precioUnitario ?? articulo.PrecioUnitario ?? 0);
            $('#detCodAlmacen').val(articulo.almacenDefecto ?? articulo.AlmacenDefecto ?? '');
        }
        recalcularLinea();
    });

    $(document).on('click', '#btnNuevaLinea', function () {
        limpiarPanelLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '#btnCancelarLinea', function () {
        $('#panelLineaDetalle').addClass('d-none');
    });

    $(document).on('click', '.btn-editar-linea', function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        $('#detNoLineaOriginal').val(clave);
        $('#detCodArticulo').val(linea.codArticulo ?? linea.CodArticulo ?? '');
        $('#detCodAlmacen').val(linea.codAlmacen ?? linea.CodAlmacen ?? '');
        $('#detCodigoImpuesto').val(linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '');
        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '.btn-eliminar-linea', async function () {
        const clave = $(this).data('clave');

        const confirmado = await App.confirmarEliminar('Se eliminará la línea de detalle seleccionada.');
        if (!confirmado) return;

        if (esEdicionDetalle()) {
            const entry = $('#tblDetalleEntrega').data('entry');
            const respuesta = await App.eliminar(`/Entregas/EliminarLinea?entry=${entry}&noLinea=${clave}`);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            App.mostrarExito('Línea eliminada correctamente.');
            cargarDetalleRemoto();
        } else {
            lineasLocales = lineasLocales.filter(l => l._id !== clave);
            pintarDetalle();
        }
    });

    $(document).on('click', '#btnGuardarLinea', async function () {
        const datosForm = App.recolectarFormulario('#formLineaDetalle');
        datosForm.CodArticulo = $('#detCodArticulo').val() || null;
        datosForm.CodigoImpuesto = $('#detCodigoImpuesto').val() || null;

        if (!datosForm.CodArticulo) {
            App.mostrarError('Selecciona un artículo.');
            return;
        }

        if (esEdicionDetalle()) {
            const entry = $('#tblDetalleEntrega').data('entry');
            const esEdicionLinea = noLineaOriginalEnEdicion !== null;
            const url = esEdicionLinea
                ? `/Entregas/EditarLinea?entry=${entry}&noLinea=${noLineaOriginalEnEdicion}`
                : '/Entregas/CrearLinea';
            const datos = { ...datosForm, Entry: entry };

            const respuesta = await App.enviarJson(url, 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }

            App.mostrarExito(esEdicionLinea ? 'Línea actualizada correctamente.' : 'Línea agregada correctamente.');
            $('#panelLineaDetalle').addClass('d-none');
            cargarDetalleRemoto();
        } else {
            if (noLineaOriginalEnEdicion !== null) {
                lineasLocales = lineasLocales.map(l => l._id === noLineaOriginalEnEdicion ? { ...datosForm, _id: l._id } : l);
            } else {
                lineasLocales.push({ ...datosForm, _id: proximoIdLocal++ });
            }

            $('#panelLineaDetalle').addClass('d-none');
            pintarDetalle();
        }
    });
});
```

- [ ] **Step 5: Agregar Entregas al submenú "Ventas" en `_Layout.cshtml`**

Cambiar:
```csharp
    bool EsActivoVentas = new[] { "Cotizaciones", "Pedidos" }.Any(EsActivo);
```
por:
```csharp
    bool EsActivoVentas = new[] { "Cotizaciones", "Pedidos", "Entregas" }.Any(EsActivo);
```

Y agregar, dentro de `<div class="collapse ..." id="submenuVentas">`, después del enlace de Pedidos (agregado en la Fase 1):
```html
                        <a class="nav-link nav-sublink @(EsActivo("Entregas") ? "active" : "")" asp-controller="Entregas" asp-action="Index">
                            <i class="fa-solid fa-truck"></i><span>Entregas</span>
                        </a>
```

- [ ] **Step 6: Compilar la Web**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\Web && dotnet build Web.slnx -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\web_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 7: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\Web
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar pantalla Web de Entregas

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

## Fase 3: Factura

### Task 7: API completa de Factura y FacturaDetalle

**Files:**
- Create: `API.Domain.Entity/Models/Factura.cs`
- Create: `API.Domain.Entity/Models/FacturaDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs` (agregar `DbSet<Factura>`, `DbSet<FacturaDetalle>`, dos bloques `OnModelCreating`)
- Modify: `API.Domain.Entity/Models/SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs` (agregar `ICollection<Factura> Facturas`)
- Modify: `API.Domain.Entity/Models/Articulo.cs`, `Almacen.cs` (agregar `ICollection<FacturaDetalle> FacturaDetalles`)
- Create: `API.Application.DTO/factura/FacturaDTO.cs`, `FacturaCrearDTO.cs`, `FacturaActualizarDTO.cs`
- Create: `API.Application.DTO/factura/FacturaDetalleDTO.cs`, `FacturaDetalleCrearDTO.cs`, `FacturaDetalleActualizarDTO.cs`
- Create: `API.Domain.Interface/IFacturaDomain.cs`, `IFacturaDetalleDomain.cs`
- Create: `API.Domain.Core/FacturaDomain.cs`, `FacturaDetalleDomain.cs`
- Create: `API.Infraestructure.Repository/FacturaRepositorio.cs`, `FacturaDetalleRepositorio.cs`
- Create: `API.Application.Interface/IFacturaApplication.cs`, `IFacturaDetalleApplication.cs`
- Create: `API.Application.Main/FacturaApplication.cs`, `FacturaDetalleApplication.cs`
- Create: `API.Service.WebApi/Controllers/FacturaController.cs`, `FacturaDetalleController.cs`
- Modify: `API.Service.WebApi/Startup.cs` (6 líneas de DI, junto a las de Cotizacion)
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs` (`using` + 6 `CreateMap`)
- Create: `API.Service.WebApi.Tests/Controllers/FacturaControllerTests.cs`, `FacturaDetalleControllerTests.cs`
- Create: `API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs`

**Interfaces:**
- Produces: `IFacturaDomain.InsertarAsync(Factura)/ActualizarAsync(int,Factura)/EliminarAsync(int)/ObtenerAsync(int)/ObtenerTodoAsync()`; `IFacturaDetalleDomain.InsertarAsync(FacturaDetalle)/ActualizarAsync(int,int,FacturaDetalle)/EliminarAsync(int,int)/ObtenerAsync(int,int)/ObtenerTodoAsync()/ObtenerPorFacturaAsync(int)`; rutas `api/Factura` y `api/FacturaDetalle` (`{entry:int}/{noLinea:int}`, `PorFactura/{entry:int}`).

- [ ] **Step 1: Crear las entidades `Factura` y `FacturaDetalle`**

`API.Domain.Entity/Models/Factura.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Factura
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? Imprimido { get; set; }

    public string? EstadoDoc { get; set; }

    public string? EstadoInv { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public string? Direccion { get; set; }

    public string? MonedaDoc { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public decimal? PrctjeImpuesto { get; set; }

    public decimal? TotalImp { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public decimal? TotalDesc { get; set; }

    public decimal? TotalBruto { get; set; }

    public decimal? TotalDoc { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual Monedum? MonedaDocNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;
}
```

`API.Domain.Entity/Models/FacturaDetalle.cs`:
```csharp
namespace API.Domain.Entity.Models;

public partial class FacturaDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public int? TipoDocDestino { get; set; }

    public int? DocDestinoEntry { get; set; }

    public int? BaseRef { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public int? BaseLinea { get; set; }

    public string? EstadoLinea { get; set; }

    public string? CodArticulo { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Cantidad { get; set; }

    public decimal? Precio { get; set; }

    public decimal? PrecioBruto { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public string? CodigoImpuesto { get; set; }

    public decimal? Impuesto { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? TipoObjeto { get; set; }

    public string? CodAlmacen { get; set; }

    public virtual Almacen? CodAlmacenNavigation { get; set; }

    public virtual Articulo? CodArticuloNavigation { get; set; }
}
```

- [ ] **Step 2: Agregar las colecciones inversas en las entidades relacionadas**

En `SocioNegocio.cs`, `Monedum.cs` y `NumeracionDocumentoDet.cs`, junto a la línea existente `public virtual ICollection<Cotizacion> Cotizacions { get; set; } = new List<Cotizacion>();`, agregar debajo:
```csharp
    public virtual ICollection<Factura> Facturas { get; set; } = new List<Factura>();
```

En `Articulo.cs` y `Almacen.cs`, junto a la línea existente `public virtual ICollection<CotizacionDetalle> CotizacionDetalles { get; set; } = new List<CotizacionDetalle>();`, agregar debajo:
```csharp
    public virtual ICollection<FacturaDetalle> FacturaDetalles { get; set; } = new List<FacturaDetalle>();
```

- [ ] **Step 3: Mapear `Factura`/`FacturaDetalle` en `ApiDbTestContext.cs`**

Agregar `public virtual DbSet<Factura> Facturas { get; set; }` y `public virtual DbSet<FacturaDetalle> FacturaDetalles { get; set; }` junto a los `DbSet` de `Cotizacion`/`CotizacionDetalle`.

En `OnModelCreating`, agregar (después del bloque de `CotizacionDetalle`, antes de `Departamento`):
```csharp
        modelBuilder.Entity<Factura>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_factura");

            entity.ToTable("Factura");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Direccion).HasMaxLength(254);
            entity.Property(e => e.EstadoDoc)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.EstadoInv)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.Imprimido)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.MonedaDoc).HasMaxLength(3);
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeImpuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('4')");
            entity.Property(e => e.TotalBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalImp).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany(p => p.Facturas)
                .HasForeignKey(d => d.CodigoSn)
                .HasConstraintName("fk_factura_sn");

            entity.HasOne(d => d.MonedaDocNavigation).WithMany(p => p.Facturas)
                .HasForeignKey(d => d.MonedaDoc)
                .HasConstraintName("fk_factura_moneda");

            entity.HasOne(d => d.SerieNavigation).WithMany(p => p.Facturas)
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_factura_serie");
        });

        modelBuilder.Entity<FacturaDetalle>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_factura_det");

            entity.ToTable("FacturaDetalle");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cantidad).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CodAlmacen).HasMaxLength(8);
            entity.Property(e => e.CodArticulo).HasMaxLength(15);
            entity.Property(e => e.CodigoImpuesto).HasMaxLength(8);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.EstadoLinea)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.Impuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Precio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrecioBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoDocDestino).HasDefaultValueSql("((-1))");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(20)
                .HasDefaultValueSql("((3))");
            entity.Property(e => e.TotalLinea).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodAlmacenNavigation).WithMany(p => p.FacturaDetalles)
                .HasForeignKey(d => d.CodAlmacen)
                .HasConstraintName("fk_factura_det_almacen");

            entity.HasOne(d => d.CodArticuloNavigation).WithMany(p => p.FacturaDetalles)
                .HasForeignKey(d => d.CodArticulo)
                .HasConstraintName("fk_factura_det_cod_art");
        });

```

- [ ] **Step 4: Crear los DTOs de Factura**

`API.Application.DTO/factura/FacturaDTO.cs`:
```csharp
namespace API.Application.DTO.factura
{
    public class FacturaDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/factura/FacturaCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.factura
{
    public class FacturaCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual" -- para series autogeneradas el
        // servidor calcula el siguiente número al momento de registrar el factura (ver
        // FacturaDomain.InsertarAsync), así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/factura/FacturaActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.factura
{
    public class FacturaActualizarDTO
    {
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`API.Application.DTO/factura/FacturaDetalleDTO.cs`:
```csharp
namespace API.Application.DTO.factura
{
    public class FacturaDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`API.Application.DTO/factura/FacturaDetalleCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.factura
{
    public class FacturaDetalleCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Entry { get; set; }

        // NoLinea no lo asigna el usuario: el backend calcula max(NoLinea existentes del Entry) + 1.
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`API.Application.DTO/factura/FacturaDetalleActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.factura
{
    public class FacturaDetalleActualizarDTO
    {
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

- [ ] **Step 5: Crear la capa de dominio de Factura**

`API.Domain.Interface/IFacturaDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFacturaDomain
    {
        #region async methods
        Task<int> InsertarAsync(Factura obj);
        Task<bool> ActualizarAsync(int id, Factura obj);
        Task<bool> EliminarAsync(int id);
        Task<Factura> ObtenerAsync(int id);
        Task<IQueryable<Factura>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Domain.Core/FacturaDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaDomain : IFacturaDomain
    {
        // Código de objeto/documento reservado para Facturas -- exigido por el CHECK constraint
        // de la tabla (TipoObjeto='6'). Se fuerza siempre en el servidor, sin confiar en lo que
        // envíe el cliente.
        private const string TipoObjetoFactura = "6";

        private readonly IRepositorioGenerico<Factura, int> _repoGenericoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoGenericoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public FacturaDomain(
            IRepositorioGenerico<Factura, int> repoGenericoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoFactura = repoGenericoFactura;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<int> InsertarAsync(Factura obj)
        {
            obj.TipoObjeto = TipoObjetoFactura;

            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar facturas.");
            }

            if (serie.Manual == "S")
            {
                // Serie manual: el número lo escribe el usuario, el consecutivo automático no aplica.
                if (obj.NumDoc <= 0)
                {
                    throw new Exception("El número de documento es requerido para series manuales.");
                }
            }
            else
            {
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el factura -- no
                // al solo consultar/previsualizar el número.
                if (serie.SigNumero == null)
                {
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                }

                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                {
                    throw new Exception("Se agotó la numeración disponible en esta serie.");
                }

                obj.NumDoc = serie.SigNumero.Value;

                // No se llama a _repoGenericoNumeracion.ActualizarAsync aquí a propósito: "serie"
                // ya es una entidad rastreada por el mismo ApiDbTestContext que usa
                // _repoGenericoFactura (ambos repos genéricos se resuelven en el mismo scope de la
                // petición), así que este cambio en memoria queda pendiente y se guarda junto con
                // el INSERT del factura en el único SaveChangesAsync de abajo -- las dos operaciones
                // quedan en la misma transacción implícita: si el INSERT falla, el incremento del
                // consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }

            var creado = await _repoGenericoFactura.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<bool> ActualizarAsync(int id, Factura obj)
        {
            obj.TipoObjeto = TipoObjetoFactura;
            return await _repoGenericoFactura.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            // No existe FK/cascada entre FacturaDetalle.Entry y Factura.Entry en la base de datos,
            // así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoFactura.EliminarAsync(id);
        }

        public async Task<Factura> ObtenerAsync(int id)
        {
            return await _repoGenericoFactura.ObtenerAsync(id);
        }

        public async Task<IQueryable<Factura>> ObtenerTodoAsync()
        {
            return await _repoGenericoFactura.ObtenerTodoAsync();
        }
        #endregion
    }
}
```

`API.Domain.Interface/IFacturaDetalleDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFacturaDetalleDomain
    {
        #region async methods
        Task<int> InsertarAsync(FacturaDetalle obj);
        Task<bool> ActualizarAsync(int entry, int noLinea, FacturaDetalle obj);
        Task<bool> EliminarAsync(int entry, int noLinea);
        Task<FacturaDetalle> ObtenerAsync(int entry, int noLinea);
        Task<IQueryable<FacturaDetalle>> ObtenerTodoAsync();
        Task<IEnumerable<FacturaDetalle>> ObtenerPorFacturaAsync(int entry);
        #endregion
    }
}
```

`API.Domain.Core/FacturaDetalleDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FacturaDetalleDomain : IFacturaDetalleDomain
    {
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoGenericoDet;

        public FacturaDetalleDomain(IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoGenericoDet)
        {
            _repoGenericoDet = repoGenericoDet;
        }

        #region async methods
        public async Task<int> InsertarAsync(FacturaDetalle obj)
        {
            var lineasExistentes = await ObtenerPorFacturaAsync(obj.Entry);
            obj.NoLinea = lineasExistentes.Any() ? lineasExistentes.Max(x => x.NoLinea) + 1 : 1;

            var insertado = await _repoGenericoDet.InsertarAsync(obj);
            return insertado.NoLinea;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, FacturaDetalle obj)
        {
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }

        public async Task<FacturaDetalle> ObtenerAsync(int entry, int noLinea)
        {
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<FacturaDetalle>> ObtenerTodoAsync()
        {
            return await _repoGenericoDet.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<FacturaDetalle>> ObtenerPorFacturaAsync(int entry)
        {
            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
        #endregion
    }
}
```

- [ ] **Step 6: Crear los repositorios de Factura**

`API.Infraestructure.Repository/FacturaRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FacturaRepositorio : RepositorioGenericoEfCore<Factura, int>
    {
        public FacturaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`API.Infraestructure.Repository/FacturaDetalleRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FacturaDetalleRepositorio : RepositorioGenericoEfCore<FacturaDetalle, (int Entry, int NoLinea)>
    {
        public FacturaDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<FacturaDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
```

- [ ] **Step 7: Crear la capa de aplicación de Factura**

`API.Application.Interface/IFacturaApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;

namespace API.Application.Interface
{
    public interface IFacturaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(FacturaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, FacturaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<FacturaDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<FacturaDTO>>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Application.Main/FacturaApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FacturaApplication : IFacturaApplication
    {
        private readonly IFacturaDomain _facturaDomain;
        private readonly IMapper _mapper;

        public FacturaApplication(IFacturaDomain facturaDomain, IMapper mapper)
        {
            _facturaDomain = facturaDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(FacturaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var factura = _mapper.Map<Factura>(obj);
                respuesta.Dato = await _facturaDomain.InsertarAsync(factura);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, FacturaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var factura = _mapper.Map<Factura>(obj);
                respuesta.Dato = await _facturaDomain.ActualizarAsync(id, factura);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _facturaDomain.EliminarAsync(id);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<FacturaDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<FacturaDTO>();
            try
            {
                var factura = await _facturaDomain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<FacturaDTO>(factura);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FacturaDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDTO>>();
            try
            {
                var queryable = await _facturaDomain.ObtenerTodoAsync();
                var facturas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FacturaDTO>>(facturas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
```

`API.Application.Interface/IFacturaDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;

namespace API.Application.Interface
{
    public interface IFacturaDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(FacturaDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, FacturaDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        Task<Respuesta<FacturaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerPorFacturaAsync(int entry);
        #endregion
    }
}
```

`API.Application.Main/FacturaDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FacturaDetalleApplication : IFacturaDetalleApplication
    {
        private readonly IFacturaDetalleDomain _facturaDetalleDomain;
        private readonly IMapper _mapper;

        public FacturaDetalleApplication(IFacturaDetalleDomain facturaDetalleDomain, IMapper mapper)
        {
            _facturaDetalleDomain = facturaDetalleDomain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(FacturaDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entidad = _mapper.Map<FacturaDetalle>(obj);
                respuesta.Dato = await _facturaDetalleDomain.InsertarAsync(entidad);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, FacturaDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var entidad = _mapper.Map<FacturaDetalle>(obj);
                respuesta.Dato = await _facturaDetalleDomain.ActualizarAsync(entry, noLinea, entidad);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _facturaDetalleDomain.EliminarAsync(entry, noLinea);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<FacturaDetalleDTO>> ObtenerAsync(int entry, int noLinea)
        {
            var respuesta = new Respuesta<FacturaDetalleDTO>();
            try
            {
                var entidad = await _facturaDetalleDomain.ObtenerAsync(entry, noLinea);
                respuesta.Dato = _mapper.Map<FacturaDetalleDTO>(entidad);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDetalleDTO>>();
            try
            {
                var queryable = await _facturaDetalleDomain.ObtenerTodoAsync();
                var lista = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FacturaDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerPorFacturaAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDetalleDTO>>();
            try
            {
                var lista = await _facturaDetalleDomain.ObtenerPorFacturaAsync(entry);
                respuesta.Dato = _mapper.Map<IEnumerable<FacturaDetalleDTO>>(lista);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
```

- [ ] **Step 8: Crear los controladores API de Factura**

`API.Service.WebApi/Controllers/FacturaController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Factura")]
    public class FacturaController : ControllerBase
    {
        private readonly IFacturaApplication _facturaApplication;

        public FacturaController(IFacturaApplication facturaApplication)
        {
            _facturaApplication = facturaApplication;
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<FacturaDTO>>> Obtener([FromRoute] int id)
        {
            var factura = await _facturaApplication.ObtenerAsync(id);

            if (!factura.Resultado)
            {
                return BadRequest(factura);
            }

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código del factura no se encontró.";
                return NotFound(factura);
            }

            return Ok(factura);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FacturaDTO>>>> ObtenerTodoAsync()
        {
            var facturas = await _facturaApplication.ObtenerTodoAsync();

            if (!facturas.Resultado)
            {
                return BadRequest(facturas);
            }

            return Ok(facturas);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] FacturaCrearDTO obj)
        {
            var insert = await _facturaApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] FacturaActualizarDTO obj)
        {
            var factura = await _facturaApplication.ObtenerAsync(id);

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código del factura no se encontró.";
                return NotFound(factura);
            }

            var update = await _facturaApplication.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var factura = await _facturaApplication.ObtenerAsync(id);

            if (factura.Dato == null)
            {
                factura.Resultado = false;
                factura.Mensaje = "El código del factura no se encontró.";
                return NotFound(factura);
            }

            var delete = await _facturaApplication.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
```

`API.Service.WebApi/Controllers/FacturaDetalleController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/FacturaDetalle")]
    public class FacturaDetalleController : ControllerBase
    {
        private readonly IFacturaDetalleApplication _facturaDetalleApplication;

        public FacturaDetalleController(IFacturaDetalleApplication facturaDetalleApplication)
        {
            _facturaDetalleApplication = facturaDetalleApplication;
        }

        [HttpGet("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<FacturaDetalleDTO>>> Obtener([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _facturaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (!det.Resultado)
            {
                return BadRequest(det);
            }

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            return Ok(det);
        }

        [HttpGet("PorFactura/{entry:int}")]
        public async Task<ActionResult<Respuesta<IEnumerable<FacturaDetalleDTO>>>> ObtenerPorFactura([FromRoute] int entry)
        {
            var detalles = await _facturaDetalleApplication.ObtenerPorFacturaAsync(entry);

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FacturaDetalleDTO>>>> ObtenerTodoAsync()
        {
            var detalles = await _facturaDetalleApplication.ObtenerTodoAsync();

            if (!detalles.Resultado)
            {
                return BadRequest(detalles);
            }

            return Ok(detalles);
        }

        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] FacturaDetalleCrearDTO obj)
        {
            var insert = await _facturaDetalleApplication.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] FacturaDetalleActualizarDTO obj)
        {
            var det = await _facturaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var update = await _facturaDetalleApplication.ActualizarAsync(entry, noLinea, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var det = await _facturaDetalleApplication.ObtenerAsync(entry, noLinea);

            if (det.Dato == null)
            {
                det.Resultado = false;
                det.Mensaje = "La línea de detalle no se encontró.";
                return NotFound(det);
            }

            var delete = await _facturaDetalleApplication.EliminarAsync(entry, noLinea);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
```

- [ ] **Step 9: Registrar Factura en la inyección de dependencias**

En `API.Service.WebApi/Startup.cs`, junto a las líneas de `Cotizacion`/`CotizacionDetalle`, agregar:
```csharp
            services.AddTransient<IRepositorioGenerico<Factura, int>, FacturaRepositorio>();
            services.AddTransient<IFacturaDomain, FacturaDomain>();
            services.AddTransient<IFacturaApplication, FacturaApplication>();

            services.AddTransient<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>, FacturaDetalleRepositorio>();
            services.AddTransient<IFacturaDetalleDomain, FacturaDetalleDomain>();
            services.AddTransient<IFacturaDetalleApplication, FacturaDetalleApplication>();
```

- [ ] **Step 10: Registrar los mapeos de AutoMapper**

En `API.Transversal.Mapper/PerfilMapeo.cs`, agregar `using API.Application.DTO.factura;` junto a los demás `using`, y junto a los `CreateMap` de Cotizacion:
```csharp
            // Factura
            CreateMap<Factura, FacturaDTO>();
            CreateMap<FacturaCrearDTO, Factura>();
            CreateMap<FacturaActualizarDTO, Factura>();

            // FacturaDetalle
            CreateMap<FacturaDetalle, FacturaDetalleDTO>();
            CreateMap<FacturaDetalleCrearDTO, FacturaDetalle>();
            CreateMap<FacturaDetalleActualizarDTO, FacturaDetalle>();
```

- [ ] **Step 11: Compilar la API para confirmar que todo lo anterior encaja**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\API && dotnet build API.sln -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish"`
Expected: `0 Errores`. Si hay errores de tipos/usings, corregirlos antes de seguir (los pasos de test dependen de que esto compile).

- [ ] **Step 12: Escribir las pruebas de `FacturaController`**

`API.Service.WebApi.Tests/Controllers/FacturaControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class FacturaControllerTests
    {
        private readonly Mock<IFacturaApplication> _applicationMock;
        private readonly FacturaController _controller;

        public FacturaControllerTests()
        {
            _applicationMock = new Mock<IFacturaApplication>();
            _controller = new FacturaController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<FacturaDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<FacturaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<FacturaDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código del factura no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new FacturaDTO { Entry = 1, NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<FacturaDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDTO>> { Resultado = true, Dato = new List<FacturaDTO> { new FacturaDTO { Entry = 1 } } };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new FacturaCrearDTO { NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new FacturaCrearDTO { NumDoc = 100, Serie = 1 };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<FacturaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, new FacturaActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<FacturaDTO> { Resultado = true, Dato = new FacturaDTO { Entry = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<FacturaActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new FacturaActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<FacturaDTO> { Resultado = true, Dato = new FacturaDTO { Entry = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, It.IsAny<FacturaActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, new FacturaActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<FacturaDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<FacturaDTO> { Resultado = true, Dato = new FacturaDTO { Entry = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1))
                .ReturnsAsync(new Respuesta<FacturaDTO> { Resultado = true, Dato = new FacturaDTO { Entry = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
```

- [ ] **Step 13: Escribir las pruebas de `FacturaDetalleController`**

`API.Service.WebApi.Tests/Controllers/FacturaDetalleControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.factura;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class FacturaDetalleControllerTests
    {
        private readonly Mock<IFacturaDetalleApplication> _applicationMock;
        private readonly FacturaDetalleController _controller;

        public FacturaDetalleControllerTests()
        {
            _applicationMock = new Mock<IFacturaDetalleApplication>();
            _controller = new FacturaDetalleController(_applicationMock.Object);
        }

        [Fact]
        public async Task Obtener_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<FacturaDetalleDTO> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task Obtener_DevuelveNotFound_CuandoDatoEsNulo()
        {
            var respuesta = new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<FacturaDetalleDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
        }

        [Fact]
        public async Task Obtener_DevuelveOk_CuandoExiste()
        {
            var dto = new FacturaDetalleDTO { Entry = 1, NoLinea = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = dto };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.Obtener(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerPorFactura_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerPorFacturaAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorFactura(1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerPorFactura_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<FacturaDetalleDTO> { new FacturaDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerPorFacturaAsync(1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerPorFactura(1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDetalleDTO>> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var respuesta = new Respuesta<IEnumerable<FacturaDetalleDTO>>
            {
                Resultado = true,
                Dato = new List<FacturaDetalleDTO> { new FacturaDetalleDTO { Entry = 1, NoLinea = 1 } }
            };
            _applicationMock.Setup(a => a.ObtenerTodoAsync()).ReturnsAsync(respuesta);

            var resultado = await _controller.ObtenerTodoAsync();

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso()
        {
            var crearDto = new FacturaDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuesta, badRequest.Value);
        }

        [Fact]
        public async Task InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso()
        {
            var crearDto = new FacturaDetalleCrearDTO { Entry = 1, CodArticulo = "ART1" };
            var respuesta = new Respuesta<int> { Resultado = true, Dato = 1 };
            _applicationMock.Setup(a => a.InsertarAsync(crearDto)).ReturnsAsync(respuesta);

            var resultado = await _controller.InsertarAsync(crearDto);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuesta, ok.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.ActualizarAsync(1, 1, new FacturaDetalleActualizarDTO());

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = new FacturaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<FacturaDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new FacturaDetalleActualizarDTO());

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, badRequest.Value);
        }

        [Fact]
        public async Task ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = new FacturaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaUpdate = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.ActualizarAsync(1, 1, It.IsAny<FacturaDetalleActualizarDTO>())).ReturnsAsync(respuestaUpdate);

            var resultado = await _controller.ActualizarAsync(1, 1, new FacturaDetalleActualizarDTO());

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaUpdate, ok.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveNotFound_CuandoNoExiste()
        {
            var respuesta = new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = null! };
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1)).ReturnsAsync(respuesta);

            var resultado = await _controller.EliminarAsync(1, 1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Same(respuesta, notFound.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = new FacturaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, badRequest.Value);
        }

        [Fact]
        public async Task EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente()
        {
            _applicationMock.Setup(a => a.ObtenerAsync(1, 1))
                .ReturnsAsync(new Respuesta<FacturaDetalleDTO> { Resultado = true, Dato = new FacturaDetalleDTO { Entry = 1, NoLinea = 1 } });
            var respuestaDelete = new Respuesta<bool> { Resultado = true, Dato = true };
            _applicationMock.Setup(a => a.EliminarAsync(1, 1)).ReturnsAsync(respuestaDelete);

            var resultado = await _controller.EliminarAsync(1, 1);

            var ok = Assert.IsType<OkObjectResult>(resultado.Result);
            Assert.Same(respuestaDelete, ok.Value);
        }
    }
}
```

- [ ] **Step 14: Escribir las pruebas de dominio de `FacturaDomain`**

`API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs`:
```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FacturaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFacturaMock;
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoDetalleMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly FacturaDomain _domain;

        public FacturaDomainTests()
        {
            _repoFacturaMock = new Mock<IRepositorioGenerico<Factura, int>>();
            _repoDetalleMock = new Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new FacturaDomain(_repoFacturaMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "6",
            Serie = 4,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N"
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_AsignaSigNumeroYLoIncrementa()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoFacturaMock.Setup(r => r.InsertarAsync(It.IsAny<Factura>()))
                .ReturnsAsync((Factura c) => { c.Entry = 99; return c; });

            var obj = new Factura { Serie = 4, NumDoc = 0, TipoObjeto = "algo-que-el-cliente-mando" };
            var entry = await _domain.InsertarAsync(obj);

            Assert.Equal(99, entry);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal("6", obj.TipoObjeto);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaNumDocDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoFacturaMock.Setup(r => r.InsertarAsync(It.IsAny<Factura>()))
                .ReturnsAsync((Factura c) => { c.Entry = 1; return c; });

            var obj = new Factura { Serie = 4, NumDoc = 12345 };
            await _domain.InsertarAsync(obj);

            Assert.Equal(12345, obj.NumDoc);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Factura { Serie = 4, NumDoc = 0 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaMock.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Factura { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaMock.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var obj = new Factura { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaMock.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new Factura { Serie = 4 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoFacturaMock.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_FuerzaTipoObjetoACuatro()
        {
            _repoFacturaMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Factura>())).ReturnsAsync(true);

            var obj = new Factura { TipoObjeto = "otro-valor" };
            var resultado = await _domain.ActualizarAsync(1, obj);

            Assert.True(resultado);
            Assert.Equal("6", obj.TipoObjeto);
        }
    }
}
```

- [ ] **Step 15: Correr toda la suite de pruebas de la API**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\API && dotnet test API.Service.WebApi.Tests/API.Service.WebApi.Tests.csproj -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish_tests"`
Expected: todas las pruebas en verde (las 376 anteriores + las 7 nuevas de `FacturaDomainTests` + las de `FacturaControllerTests`/`FacturaDetalleControllerTests`).

- [ ] **Step 16: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\API
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar módulo Factura (API completa)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

### Task 8: Cliente HTTP de Factura en Web.ApiClient

**Files:**
- Create: `Web.ApiClient/Dtos/Factura/FacturaDTO.cs`, `FacturaCrearDTO.cs`, `FacturaActualizarDTO.cs`
- Create: `Web.ApiClient/Dtos/FacturaDetalle/FacturaDetalleDTO.cs`, `FacturaDetalleCrearDTO.cs`, `FacturaDetalleActualizarDTO.cs`
- Create: `Web.ApiClient/Clientes/IFacturaApiClient.cs`, `FacturaApiClient.cs`
- Create: `Web.ApiClient/Clientes/IFacturaDetalleApiClient.cs`, `FacturaDetalleApiClient.cs`
- Modify: `Web.UI/Program.cs`

**Interfaces:**
- Consumes: rutas API `api/Factura`, `api/FacturaDetalle` (Task 7).
- Produces: `IFacturaApiClient.{ObtenerTodoAsync,ObtenerAsync,InsertarAsync,ActualizarAsync,EliminarAsync}`, `IFacturaDetalleApiClient.{ObtenerTodoAsync,ObtenerPorFacturaAsync,ObtenerAsync,InsertarAsync,ActualizarAsync,EliminarAsync}` -- usados por el controlador Web en Task 3.

- [ ] **Step 1: Crear los DTOs de Factura en Web.ApiClient**

`Web.ApiClient/Dtos/Factura/FacturaDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Factura
{
    public class FacturaDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/Factura/FacturaCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Web.ApiClient.Dtos.Factura
{
    public class FacturaCrearDTO
    {
        // Requerido solo para series "Manual" -- para series autogeneradas la API calcula el
        // siguiente número al registrar el factura, así que aquí no puede ser obligatorio.
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/Factura/FacturaActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Factura
{
    public class FacturaActualizarDTO
    {
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? EstadoInv { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`Web.ApiClient/Dtos/FacturaDetalle/FacturaDetalleDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.FacturaDetalle
{
    public class FacturaDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`Web.ApiClient/Dtos/FacturaDetalle/FacturaDetalleCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace Web.ApiClient.Dtos.FacturaDetalle
{
    public class FacturaDetalleCrearDTO
    {
        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Entry { get; set; }

        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`Web.ApiClient/Dtos/FacturaDetalle/FacturaDetalleActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.FacturaDetalle
{
    public class FacturaDetalleActualizarDTO
    {
        public int? TipoDocDestino { get; set; }
        public int? DocDestinoEntry { get; set; }
        public int? BaseRef { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public int? BaseLinea { get; set; }
        public string? EstadoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

- [ ] **Step 2: Crear los clientes HTTP de Factura**

`Web.ApiClient/Clientes/IFacturaApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Factura;

namespace Web.ApiClient.Clientes
{
    public interface IFacturaApiClient
    {
        Task<Respuesta<IEnumerable<FacturaDTO>>> ObtenerTodoAsync();
        Task<Respuesta<FacturaDTO>> ObtenerAsync(int entry);
        Task<Respuesta<int>> InsertarAsync(FacturaCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, FacturaActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry);
    }
}
```

`Web.ApiClient/Clientes/FacturaApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Factura;

namespace Web.ApiClient.Clientes
{
    public class FacturaApiClient : ApiClientBase, IFacturaApiClient
    {
        private const string Recurso = "api/Factura";

        public FacturaApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<FacturaDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<FacturaDTO>>(Recurso);

        public Task<Respuesta<FacturaDTO>> ObtenerAsync(int entry) =>
            GetAsync<FacturaDTO>($"{Recurso}/{entry}");

        public Task<Respuesta<int>> InsertarAsync(FacturaCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, FacturaActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry) =>
            DeleteAsync<bool>($"{Recurso}/{entry}");
    }
}
```

`Web.ApiClient/Clientes/IFacturaDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.FacturaDetalle;

namespace Web.ApiClient.Clientes
{
    public interface IFacturaDetalleApiClient
    {
        Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerPorFacturaAsync(int entry);
        Task<Respuesta<FacturaDetalleDTO>> ObtenerAsync(int entry, int noLinea);
        Task<Respuesta<int>> InsertarAsync(FacturaDetalleCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, FacturaDetalleActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
    }
}
```

`Web.ApiClient/Clientes/FacturaDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.FacturaDetalle;

namespace Web.ApiClient.Clientes
{
    public class FacturaDetalleApiClient : ApiClientBase, IFacturaDetalleApiClient
    {
        private const string Recurso = "api/FacturaDetalle";

        public FacturaDetalleApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<FacturaDetalleDTO>>(Recurso);

        public Task<Respuesta<IEnumerable<FacturaDetalleDTO>>> ObtenerPorFacturaAsync(int entry) =>
            GetAsync<IEnumerable<FacturaDetalleDTO>>($"{Recurso}/PorFactura/{entry}");

        public Task<Respuesta<FacturaDetalleDTO>> ObtenerAsync(int entry, int noLinea) =>
            GetAsync<FacturaDetalleDTO>($"{Recurso}/{entry}/{noLinea}");

        public Task<Respuesta<int>> InsertarAsync(FacturaDetalleCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, FacturaDetalleActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}/{noLinea}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea) =>
            DeleteAsync<bool>($"{Recurso}/{entry}/{noLinea}");
    }
}
```

- [ ] **Step 3: Registrar los HttpClient tipados en `Program.cs`**

Junto a las líneas de `ICotizacionApiClient`/`ICotizacionDetalleApiClient`, agregar:
```csharp
builder.Services.AddHttpClient<IFacturaApiClient, FacturaApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IFacturaDetalleApiClient, FacturaDetalleApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 4: Compilar Web.ApiClient y Web.UI**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\Web && dotnet build Web.slnx -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\web_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 5: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\Web
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar cliente HTTP de Factura en Web.ApiClient

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

### Task 9: Pantalla Web de Facturas

**Files:**
- Create: `Web.UI/Controllers/FacturasController.cs`
- Create: `Web.UI/Views/Facturas/Index.cshtml`, `_Form.cshtml`
- Create: `Web.UI/wwwroot/js/facturas.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `IFacturaApiClient`, `IFacturaDetalleApiClient` (Task 8); `ISocioNegocioApiClient`, `IMonedaApiClient`, `IArticuloApiClient`, `IAlmacenApiClient`, `IImpuestoApiClient`, `INumeracionDocumentoDetApiClient` (ya existentes, usados igual que en `CotizacionesController`).

- [ ] **Step 1: Crear `FacturasController`**

`Web.UI/Controllers/FacturasController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.Factura;
using Web.ApiClient.Dtos.FacturaDetalle;

namespace Web.UI.Controllers
{
    [Authorize]
    public class FacturasController : Controller
    {
        private readonly IFacturaApiClient _facturas;
        private readonly IFacturaDetalleApiClient _detalles;
        private readonly ISocioNegocioApiClient _socios;
        private readonly IMonedaApiClient _monedas;
        private readonly IArticuloApiClient _articulos;
        private readonly IAlmacenApiClient _almacenes;
        private readonly IImpuestoApiClient _impuestos;
        private readonly INumeracionDocumentoDetApiClient _series;

        // CodigoObj de NumeracionDocumento que identifica a "Facturas" como tipo de objeto.
        private const string CodigoObjFactura = "6";
        private const string SubTipoDocFactura = "--";

        public FacturasController(
            IFacturaApiClient facturas,
            IFacturaDetalleApiClient detalles,
            ISocioNegocioApiClient socios,
            IMonedaApiClient monedas,
            IArticuloApiClient articulos,
            IAlmacenApiClient almacenes,
            IImpuestoApiClient impuestos,
            INumeracionDocumentoDetApiClient series)
        {
            _facturas = facturas;
            _detalles = detalles;
            _socios = socios;
            _monedas = monedas;
            _articulos = articulos;
            _almacenes = almacenes;
            _impuestos = impuestos;
            _series = series;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _facturas.ObtenerTodoAsync();
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> FormularioCrear()
        {
            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjFactura);
            ViewBag.SeriesFactura = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocFactura);
            ViewBag.EsEdicion = false;
            return PartialView("_Form", new FacturaCrearDTO { EstadoDoc = "A", TipoObjeto = "6" });
        }

        [HttpGet]
        public async Task<IActionResult> FormularioEditar(int entry)
        {
            var respuesta = await _facturas.ObtenerAsync(entry);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            await CargarDropdownsAsync();
            ViewBag.EsEdicion = true;
            ViewBag.EntryActual = entry;

            var serieInfo = await _series.ObtenerAsync(respuesta.Dato.Serie);
            ViewBag.NombreSerieActual = serieInfo.Resultado ? serieInfo.Dato?.NombreSerie : null;

            var dto = new FacturaCrearDTO
            {
                NumDoc = respuesta.Dato.NumDoc,
                Serie = respuesta.Dato.Serie,
                EstadoDoc = respuesta.Dato.EstadoDoc,
                TipoObjeto = respuesta.Dato.TipoObjeto,
                FechaDoc = respuesta.Dato.FechaDoc,
                FechaEmision = respuesta.Dato.FechaEmision,
                CodigoSn = respuesta.Dato.CodigoSn,
                NombreSn = respuesta.Dato.NombreSn,
                Direccion = respuesta.Dato.Direccion,
                MonedaDoc = respuesta.Dato.MonedaDoc,
                PrctjeImpuesto = respuesta.Dato.PrctjeImpuesto,
                TotalImp = respuesta.Dato.TotalImp,
                PrctjeDesc = respuesta.Dato.PrctjeDesc,
                TotalDesc = respuesta.Dato.TotalDesc,
                TotalBruto = respuesta.Dato.TotalBruto,
                TotalDoc = respuesta.Dato.TotalDoc,
                Comentario = respuesta.Dato.Comentario
            };

            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromBody] FacturaCrearDTO dto)
        {
            var respuesta = await _facturas.InsertarAsync(dto);
            if (!respuesta.Resultado)
                return Json(respuesta);

            var creado = await _facturas.ObtenerAsync(respuesta.Dato);
            return Json(new { respuesta.Resultado, respuesta.Mensaje, dato = respuesta.Dato, numDoc = creado.Dato?.NumDoc });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int entry, [FromBody] FacturaCrearDTO dto)
        {
            var actual = await _facturas.ObtenerAsync(entry);
            if (!actual.Resultado || actual.Dato is null)
                return NotFound(actual);

            var actualizar = new FacturaActualizarDTO
            {
                NumDoc = actual.Dato.NumDoc,
                Serie = actual.Dato.Serie,
                EstadoDoc = dto.EstadoDoc,
                TipoObjeto = dto.TipoObjeto,
                FechaDoc = dto.FechaDoc,
                FechaEmision = dto.FechaEmision,
                CodigoSn = dto.CodigoSn,
                NombreSn = dto.NombreSn,
                Direccion = dto.Direccion,
                MonedaDoc = dto.MonedaDoc,
                PrctjeImpuesto = dto.PrctjeImpuesto,
                TotalImp = dto.TotalImp,
                PrctjeDesc = dto.PrctjeDesc,
                TotalDesc = dto.TotalDesc,
                TotalBruto = dto.TotalBruto,
                TotalDoc = dto.TotalDoc,
                Comentario = dto.Comentario
            };

            var respuesta = await _facturas.ActualizarAsync(entry, actualizar);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int entry)
        {
            var respuesta = await _facturas.EliminarAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDetalle(int entry)
        {
            var respuesta = await _detalles.ObtenerPorFacturaAsync(entry);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearLinea([FromBody] FacturaDetalleCrearDTO dto)
        {
            var respuesta = await _detalles.InsertarAsync(dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarLinea(int entry, int noLinea, [FromBody] FacturaDetalleActualizarDTO dto)
        {
            var respuesta = await _detalles.ActualizarAsync(entry, noLinea, dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarLinea(int entry, int noLinea)
        {
            var respuesta = await _detalles.EliminarAsync(entry, noLinea);
            return Json(respuesta);
        }

        private async Task CargarDropdownsAsync()
        {
            var socios = await _socios.ObtenerTodoAsync();
            var monedas = await _monedas.ObtenerTodoAsync();
            var articulos = await _articulos.ObtenerTodoAsync();
            var almacenes = await _almacenes.ObtenerTodoAsync();
            var impuestos = await _impuestos.ObtenerTodoAsync();

            ViewBag.Socios = new SelectList(socios.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Articulos = articulos.Dato ?? [];
            ViewBag.Almacenes = new SelectList(almacenes.Dato ?? [], "Codigo", "Nombre");
            ViewBag.Impuestos = impuestos.Dato ?? [];
        }
    }
}
```

- [ ] **Step 2: Crear `Views/Facturas/Index.cshtml`**

```html
@{
    ViewData["Title"] = "Facturas";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Facturas</h3>
    <button type="button" class="btn btn-primary" id="btnNuevo">
        <i class="fa-solid fa-plus me-1"></i>Nuevo
    </button>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblFacturas" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>No. Documento</th>
                        <th>Socio de negocio</th>
                        <th>Fecha</th>
                        <th>Estado</th>
                        <th>Total</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalFormulario" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-xl modal-dialog-scrollable">
        <div class="modal-content" id="contenidoModal">
            <!-- se carga por AJAX -->
        </div>
    </div>
</div>

@section Scripts {
    <script src="~/js/facturas.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: Crear `Views/Facturas/_Form.cshtml`**

Idéntico a `Views/Cotizaciones/_Form.cshtml`, cambiando el modelo, el título, los ids de tabla/script/formulario y las rutas de acción:
```html
@using System.Text.Json
@model Web.ApiClient.Dtos.Factura.FacturaCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
    var opcionesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? "Editar factura" : "Nueva factura")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formFactura" novalidate>
        <div asp-validation-summary="ModelOnly" class="alert alert-danger py-2 px-3 small"></div>

        <div class="row g-3">
            @if (!esEdicion)
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <select id="selectSerieFactura" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                    <span class="form-text">Si eliges una serie distinta de "Manual", el número se genera solo al guardar.</span>
                </div>
            }
            else
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <input class="form-control" value="@ViewBag.NombreSerieActual" disabled />
                </div>
            }
            <div class="col-md-3">
                <label asp-for="NumDoc" class="form-label">No. documento</label>
                <input asp-for="NumDoc" type="number" class="form-control" readonly="@esEdicion" />
                <span asp-validation-for="NumDoc" class="text-danger small"></span>
            </div>
            <div class="col-md-3">
                <label asp-for="EstadoDoc" class="form-label">Estado</label>
                <select asp-for="EstadoDoc" class="form-select">
                    <option value="A">Activo</option>
                    <option value="C">Cancelado</option>
                </select>
            </div>
            <div class="col-md-3" hidden>
                <label asp-for="TipoObjeto" class="form-label">Tipo</label>
                <input asp-for="TipoObjeto" class="form-control" />
            </div>

            <div class="col-md-4">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                <select asp-for="CodigoSn" id="selectCodigoSn" class="form-select" asp-items="ViewBag.Socios">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>
            <div class="col-md-4">
                <label asp-for="NombreSn" class="form-label">Nombre</label>
                <input asp-for="NombreSn" class="form-control" />
            </div>
            <div class="col-md-4">
                <label asp-for="MonedaDoc" class="form-label">Moneda</label>
                <select asp-for="MonedaDoc" class="form-select" asp-items="ViewBag.Monedas">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>

            <div class="col-md-4">
                <label asp-for="Direccion" class="form-label"></label>
                <input asp-for="Direccion" class="form-control" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha documento</label>
                <input type="date" name="FechaDoc" id="FechaDoc" class="form-control" value="@Model.FechaDoc?.ToString("yyyy-MM-dd")" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha emisión</label>
                <input type="date" name="FechaEmision" id="FechaEmision" class="form-control" value="@Model.FechaEmision?.ToString("yyyy-MM-dd")" />
            </div>

            <div class="col-md-3">
                <label asp-for="PrctjeDesc" class="form-label">% Descuento</label>
                <input asp-for="PrctjeDesc" type="number" step="0.01" class="form-control" />
            </div>
            <div class="col-md-3">
                <label asp-for="PrctjeImpuesto" class="form-label">% Impuesto</label>
                <input asp-for="PrctjeImpuesto" type="number" step="0.01" class="form-control" />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total bruto</label>
                <input id="TotalBruto" class="form-control" value="@Model.TotalBruto" disabled />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total documento</label>
                <input id="TotalDoc" class="form-control" value="@Model.TotalDoc" disabled />
            </div>

            <div class="col-12">
                <label asp-for="Comentario" class="form-label"></label>
                <textarea asp-for="Comentario" class="form-control" rows="2"></textarea>
            </div>
        </div>
    </form>

    <hr />
    <div class="d-flex justify-content-between align-items-center mb-2">
        <h6 class="mb-0">Detalle</h6>
        <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevaLinea">
            <i class="fa-solid fa-plus me-1"></i>Agregar línea
        </button>
    </div>

    @if (!esEdicion)
    {
        <p class="text-muted small">Las líneas agregadas aquí se guardarán junto con la factura.</p>
    }

    <div class="table-responsive">
        <table id="tblDetalleFactura" class="table table-sm table-hover align-middle w-100" data-entry="@ViewBag.EntryActual" data-es-edicion="@esEdicion.ToString().ToLower()">
            <thead>
                <tr>
                    <th>Artículo</th>
                    <th>Descripción</th>
                    <th>Cantidad</th>
                    <th>Precio</th>
                    <th>% Desc.</th>
                    <th>Impuesto</th>
                    <th>Total línea</th>
                    <th class="text-end">Acciones</th>
                </tr>
            </thead>
            <tbody></tbody>
        </table>
    </div>

    <div id="panelLineaDetalle" class="border rounded p-3 mb-2 d-none">
        <form id="formLineaDetalle">
            <input type="hidden" id="detNoLineaOriginal" value="" />
            <div class="row g-2">
                <div class="col-md-4">
                    <label class="form-label">Artículo</label>
                    <select id="detCodArticulo" class="form-select">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Almacén</label>
                    <select name="CodAlmacen" id="detCodAlmacen" class="form-select" asp-items="ViewBag.Almacenes">
                        <option value="">-- Seleccione --</option>
                    </select>
                </div>
                <div class="col-md-4">
                    <label class="form-label">Impuesto</label>
                    <select id="detCodigoImpuesto" class="form-select">
                        <option value="">-- Ninguno --</option>
                    </select>
                </div>

                <div class="col-md-8">
                    <label class="form-label">Descripción</label>
                    <input name="Descripcion" id="detDescripcion" class="form-control" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Cantidad</label>
                    <input name="Cantidad" id="detCantidad" type="number" step="0.01" class="form-control" value="1" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Precio</label>
                    <input name="Precio" id="detPrecio" type="number" step="0.01" class="form-control" />
                </div>

                <div class="col-md-2">
                    <label class="form-label">% Desc.</label>
                    <input name="PrctjeDesc" id="detPrctjeDesc" type="number" step="0.01" class="form-control" value="0" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Impuesto (Q)</label>
                    <input name="Impuesto" id="detImpuestoMonto" type="number" step="0.01" class="form-control" readonly />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Total línea</label>
                    <input name="TotalLinea" id="detTotalLinea" type="number" step="0.01" class="form-control" readonly />
                </div>
            </div>
            <div class="text-end mt-2">
                <button type="button" class="btn btn-sm btn-secondary" id="btnCancelarLinea">Cancelar</button>
                <button type="button" class="btn btn-sm btn-primary" id="btnGuardarLinea">Guardar línea</button>
            </div>
        </form>
    </div>

    <script id="datosArticulosFactura" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Articulos, opcionesJson))
    </script>
    <script id="datosImpuestosFactura" type="application/json">
        @Html.Raw(JsonSerializer.Serialize(ViewBag.Impuestos, opcionesJson))
    </script>

    @if (!esEdicion)
    {
        <script id="datosSeriesFactura" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(ViewBag.SeriesFactura, opcionesJson))
        </script>
    }
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
    <button type="button" class="btn btn-primary" id="btnGuardarFactura" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
        <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
    </button>
</div>
```

- [ ] **Step 4: Crear `wwwroot/js/facturas.js`**

Idéntico a `cotizaciones.js`, sustituyendo cada identificador `cotizacion(es)`/`Cotizacion(es)` por `factura(s)`/`Factura(s)` y cada endpoint `/Cotizaciones/...` por `/Facturas/...`:
```javascript
$(function () {
    const tabla = $('#tblFacturas').DataTable({
        ajax: { url: '/Facturas/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'numDoc' },
            { data: 'nombreSn', render: (d, t, row) => d || row.codigoSn || '' },
            { data: 'fechaDoc', render: d => d ? new Date(d).toLocaleDateString() : '' },
            { data: 'estadoDoc', render: d => d === 'C' ? '<span class="badge text-bg-secondary">Cancelado</span>' : '<span class="badge text-bg-success">Activo</span>' },
            { data: 'totalDoc', render: d => d != null ? Number(d).toFixed(2) : '' },
            {
                data: 'entry', orderable: false, className: 'text-end',
                render: entry => `
                    <button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"><i class="fa-solid fa-pen"></i></button>
                    <button class="btn btn-sm btn-outline-danger btn-eliminar" data-entry="${entry}"><i class="fa-solid fa-trash"></i></button>
                `
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSerieFactura();
        inicializarDetalle();
    }

    $('#btnNuevo').on('click', async function () {
        const html = await $.get('/Facturas/FormularioCrear');
        abrirModal(html);
    });

    $('#tblFacturas').on('click', '.btn-editar', async function () {
        const entry = $(this).data('entry');
        const html = await $.get('/Facturas/FormularioEditar', { entry });
        abrirModal(html);
    });

    $('#tblFacturas').on('click', '.btn-eliminar', async function () {
        const entry = $(this).data('entry');
        const confirmado = await App.confirmarEliminar(`Se eliminará la factura #${entry}.`);
        if (!confirmado) return;

        const respuesta = await App.eliminar(`/Facturas/Eliminar?entry=${entry}`);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Factura eliminada correctamente.');
        recargarTabla();
    });

    // --- Serie de numeración para generar el número de documento (solo aplica al crear) ---

    function inicializarSerieFactura() {
        const $sel = $('#selectSerieFactura');
        if ($sel.length === 0) return;

        const datosEl = document.getElementById('datosSeriesFactura');
        const series = datosEl ? (JSON.parse(datosEl.textContent) || []) : [];

        $sel.html('<option value="">-- Seleccione --</option>');
        let serieManual = null;
        series.forEach(s => {
            const serie = s.serie ?? s.Serie;
            const nombre = s.nombreSerie ?? s.NombreSerie;
            const manual = s.manual ?? s.Manual;
            if (manual === 'S' && serieManual === null) serieManual = serie;
            $sel.append(`<option value="${serie}" data-manual="${manual}">${nombre}</option>`);
        });

        if (serieManual !== null) $sel.val(serieManual);

        actualizarNumDocSegunSerie();
    }

    function esSerieManualFactura() {
        const $sel = $('#selectSerieFactura');
        if ($sel.length === 0 || !$sel.val()) return true;
        return $sel.find('option:selected').data('manual') === 'S';
    }

    function actualizarNumDocSegunSerie() {
        const $numDoc = $('#NumDoc');
        if ($numDoc.length === 0) return;

        if (esSerieManualFactura()) {
            $numDoc.prop('disabled', false).attr('placeholder', '');
        } else {
            $numDoc.val('').prop('disabled', true).attr('placeholder', 'Se generará al guardar');
        }
    }

    $(document).on('change', '#selectSerieFactura', actualizarNumDocSegunSerie);

    // Auto-completa el nombre del socio de negocio al elegirlo (queda editable después).
    $(document).on('change', '#selectCodigoSn', function () {
        const texto = $(this).find('option:selected').text();
        if (texto && texto !== '-- Seleccione --') {
            $('#NombreSn').val(texto);
        }
    });

    $(document).on('click', '#btnGuardarFactura', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const entry = $boton.data('entry');

        if (!esEdicion) {
            const serieSeleccionada = $('#selectSerieFactura').val();
            if (!serieSeleccionada) {
                App.mostrarError('Debes seleccionar una serie.');
                return;
            }
        }

        // El número de documento (No. documento) no se solicita aquí para series no manuales: el
        // servidor lo calcula y avanza el consecutivo al registrar la factura (ver
        // FacturaDomain.InsertarAsync en la API), no antes. Para series Manual, el campo #NumDoc
        // está habilitado y su valor viaja normalmente en recolectarFormulario.
        const datos = App.recolectarFormulario('#formFactura');
        if (!esEdicion) {
            datos.Serie = $('#selectSerieFactura').val();
        }

        const totales = calcularTotalesDesdeLineas(esEdicionDetalle() ? lineasRemotas : lineasLocales);
        datos.TotalBruto = totales.totalBruto;
        datos.TotalDesc = totales.totalDesc;
        datos.TotalImp = totales.totalImp;
        datos.TotalDoc = totales.totalDoc;

        if (!esEdicion) {
            const respuestaCabecera = await App.enviarJson('/Facturas/Crear', 'POST', datos);
            if (!respuestaCabecera.resultado) {
                App.mostrarError(respuestaCabecera.mensaje);
                return;
            }

            const entryCreado = respuestaCabecera.dato;

            if (respuestaCabecera.numDoc != null) {
                $('#NumDoc').val(respuestaCabecera.numDoc).prop('disabled', false);
            }

            let exitosas = 0;
            let fallidas = 0;

            for (const linea of lineasLocales) {
                const { _id, ...lineaSinId } = linea;
                const respuestaLinea = await App.enviarJson('/Facturas/CrearLinea', 'POST', {
                    ...lineaSinId,
                    Entry: entryCreado
                });

                if (respuestaLinea.resultado) {
                    exitosas++;
                } else {
                    fallidas++;
                    App.mostrarError(respuestaLinea.mensaje);
                }
            }

            const sufijoNumDoc = respuestaCabecera.numDoc != null ? ` No. documento: ${respuestaCabecera.numDoc}.` : '';
            if (fallidas > 0) {
                await App.mostrarExito(`Factura creada correctamente. Líneas guardadas: ${exitosas} de ${exitosas + fallidas}.${sufijoNumDoc}`);
            } else {
                await App.mostrarExito(`Factura creada correctamente.${sufijoNumDoc}`);
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            recargarTabla();
            return;
        }

        const respuesta = await App.enviarJson(`/Facturas/Editar?entry=${entry}`, 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }

        bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
        App.mostrarExito('Factura actualizada correctamente.');
        recargarTabla();
    });

    // --- Detalle (grid anidado): en creación se administra localmente, en edición en vivo contra la API ---

    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let articulosDisponibles = [];
    let impuestosDisponibles = [];

    function esEdicionDetalle() {
        const v = $('#tblDetalleFactura').data('es-edicion');
        return v === true || v === 'true';
    }

    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;

        const $tabla = $('#tblDetalleFactura');
        if ($tabla.length === 0) return;

        const datosArt = document.getElementById('datosArticulosFactura');
        articulosDisponibles = datosArt ? (JSON.parse(datosArt.textContent) || []) : [];

        const datosImp = document.getElementById('datosImpuestosFactura');
        impuestosDisponibles = datosImp ? (JSON.parse(datosImp.textContent) || []) : [];

        const $selArt = $('#detCodArticulo');
        $selArt.html('<option value="">-- Seleccione --</option>');
        articulosDisponibles.forEach(a => {
            const codigo = a.codigo ?? a.Codigo;
            const nombre = a.nombre ?? a.Nombre;
            $selArt.append(`<option value="${codigo}">${codigo} - ${nombre ?? ''}</option>`);
        });

        const $selImp = $('#detCodigoImpuesto');
        $selImp.html('<option value="">-- Ninguno --</option>');
        impuestosDisponibles.forEach(i => {
            const codigo = i.codigo ?? i.Codigo;
            const nombre = i.nombre ?? i.Nombre;
            const tasa = i.tasa ?? i.Tasa ?? 0;
            $selImp.append(`<option value="${codigo}" data-tasa="${tasa}">${nombre} (${tasa}%)</option>`);
        });

        if (esEdicionDetalle()) {
            cargarDetalleRemoto();
        } else {
            pintarDetalle();
        }
    }

    async function cargarDetalleRemoto() {
        const entry = $('#tblDetalleFactura').data('entry');
        const respuesta = await $.get('/Facturas/ObtenerDetalle', { entry });
        lineasRemotas = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        pintarDetalle();
    }

    function calcularTotalesDesdeLineas(lista) {
        let totalBruto = 0, totalDesc = 0, totalImp = 0, totalDoc = 0;
        lista.forEach(l => {
            const cantidad = Number(l.cantidad ?? l.Cantidad ?? 0);
            const precio = Number(l.precio ?? l.Precio ?? 0);
            const prctjeDesc = Number(l.prctjeDesc ?? l.PrctjeDesc ?? 0);
            const impuesto = Number(l.impuesto ?? l.Impuesto ?? 0);
            const bruto = cantidad * precio;
            const desc = bruto * (prctjeDesc / 100);
            totalBruto += bruto;
            totalDesc += desc;
            totalImp += impuesto;
            totalDoc += (bruto - desc + impuesto);
        });
        return {
            totalBruto: totalBruto.toFixed(2),
            totalDesc: totalDesc.toFixed(2),
            totalImp: totalImp.toFixed(2),
            totalDoc: totalDoc.toFixed(2)
        };
    }

    function pintarDetalle() {
        const $tbody = $('#tblDetalleFactura tbody');
        if ($tbody.length === 0) return;

        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;

        const totales = calcularTotalesDesdeLineas(lista);
        $('#TotalBruto').val(totales.totalBruto);
        $('#TotalDoc').val(totales.totalDoc);

        if (lista.length === 0) {
            $tbody.html('<tr><td colspan="8" class="text-center text-muted">Sin líneas de detalle</td></tr>');
            return;
        }

        $tbody.html(lista.map(linea => {
            const noLinea = linea.noLinea ?? linea.NoLinea;
            const codArticulo = linea.codArticulo ?? linea.CodArticulo;
            const descripcion = linea.descripcion ?? linea.Descripcion;
            const cantidad = linea.cantidad ?? linea.Cantidad;
            const precio = linea.precio ?? linea.Precio;
            const prctjeDesc = linea.prctjeDesc ?? linea.PrctjeDesc;
            const impuesto = linea.impuesto ?? linea.Impuesto;
            const totalLinea = linea.totalLinea ?? linea.TotalLinea;
            const clave = esEdicionDetalle() ? noLinea : linea._id;
            return `
                <tr>
                    <td>${codArticulo ?? ''}</td>
                    <td>${descripcion ?? ''}</td>
                    <td>${cantidad ?? ''}</td>
                    <td>${precio != null ? Number(precio).toFixed(2) : ''}</td>
                    <td>${prctjeDesc ?? 0}</td>
                    <td>${impuesto != null ? Number(impuesto).toFixed(2) : '0.00'}</td>
                    <td>${totalLinea != null ? Number(totalLinea).toFixed(2) : ''}</td>
                    <td class="text-end">
                        <button type="button" class="btn btn-sm btn-outline-primary btn-editar-linea" data-clave="${clave}"><i class="fa-solid fa-pen"></i></button>
                        <button type="button" class="btn btn-sm btn-outline-danger btn-eliminar-linea" data-clave="${clave}"><i class="fa-solid fa-trash"></i></button>
                    </td>
                </tr>
            `;
        }).join(''));
    }

    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        $('#detCodArticulo').val('');
        $('#detCodAlmacen').val('');
        $('#detCodigoImpuesto').val('');
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }

    /** Recalcula el monto de impuesto y el total de la línea con base en los campos actuales del panel. */
    function recalcularLinea() {
        const cantidad = Number($('#detCantidad').val()) || 0;
        const precio = Number($('#detPrecio').val()) || 0;
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;
        const tasa = Number($('#detCodigoImpuesto').find('option:selected').data('tasa')) || 0;

        const bruto = cantidad * precio;
        const desc = bruto * (prctjeDesc / 100);
        const subtotal = bruto - desc;
        const impuesto = subtotal * (tasa / 100);
        const total = subtotal + impuesto;

        $('#detImpuestoMonto').val(impuesto.toFixed(2));
        $('#detTotalLinea').val(total.toFixed(2));
    }

    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc, #detCodigoImpuesto', recalcularLinea);

    $(document).on('change', '#detCodArticulo', function () {
        const codigo = $(this).val();
        const articulo = articulosDisponibles.find(a => (a.codigo ?? a.Codigo) === codigo);
        if (articulo) {
            $('#detDescripcion').val(articulo.nombre ?? articulo.Nombre ?? '');
            $('#detPrecio').val(articulo.precioUnitario ?? articulo.PrecioUnitario ?? 0);
            $('#detCodAlmacen').val(articulo.almacenDefecto ?? articulo.AlmacenDefecto ?? '');
        }
        recalcularLinea();
    });

    $(document).on('click', '#btnNuevaLinea', function () {
        limpiarPanelLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '#btnCancelarLinea', function () {
        $('#panelLineaDetalle').addClass('d-none');
    });

    $(document).on('click', '.btn-editar-linea', function () {
        const clave = $(this).data('clave');
        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const linea = esEdicionDetalle()
            ? lista.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lista.find(l => l._id === clave);
        if (!linea) return;

        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        $('#detNoLineaOriginal').val(clave);
        $('#detCodArticulo').val(linea.codArticulo ?? linea.CodArticulo ?? '');
        $('#detCodAlmacen').val(linea.codAlmacen ?? linea.CodAlmacen ?? '');
        $('#detCodigoImpuesto').val(linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '');
        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '.btn-eliminar-linea', async function () {
        const clave = $(this).data('clave');

        const confirmado = await App.confirmarEliminar('Se eliminará la línea de detalle seleccionada.');
        if (!confirmado) return;

        if (esEdicionDetalle()) {
            const entry = $('#tblDetalleFactura').data('entry');
            const respuesta = await App.eliminar(`/Facturas/EliminarLinea?entry=${entry}&noLinea=${clave}`);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            App.mostrarExito('Línea eliminada correctamente.');
            cargarDetalleRemoto();
        } else {
            lineasLocales = lineasLocales.filter(l => l._id !== clave);
            pintarDetalle();
        }
    });

    $(document).on('click', '#btnGuardarLinea', async function () {
        const datosForm = App.recolectarFormulario('#formLineaDetalle');
        datosForm.CodArticulo = $('#detCodArticulo').val() || null;
        datosForm.CodigoImpuesto = $('#detCodigoImpuesto').val() || null;

        if (!datosForm.CodArticulo) {
            App.mostrarError('Selecciona un artículo.');
            return;
        }

        if (esEdicionDetalle()) {
            const entry = $('#tblDetalleFactura').data('entry');
            const esEdicionLinea = noLineaOriginalEnEdicion !== null;
            const url = esEdicionLinea
                ? `/Facturas/EditarLinea?entry=${entry}&noLinea=${noLineaOriginalEnEdicion}`
                : '/Facturas/CrearLinea';
            const datos = { ...datosForm, Entry: entry };

            const respuesta = await App.enviarJson(url, 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }

            App.mostrarExito(esEdicionLinea ? 'Línea actualizada correctamente.' : 'Línea agregada correctamente.');
            $('#panelLineaDetalle').addClass('d-none');
            cargarDetalleRemoto();
        } else {
            if (noLineaOriginalEnEdicion !== null) {
                lineasLocales = lineasLocales.map(l => l._id === noLineaOriginalEnEdicion ? { ...datosForm, _id: l._id } : l);
            } else {
                lineasLocales.push({ ...datosForm, _id: proximoIdLocal++ });
            }

            $('#panelLineaDetalle').addClass('d-none');
            pintarDetalle();
        }
    });
});
```

- [ ] **Step 5: Agregar Facturas al submenú "Ventas" en `_Layout.cshtml`**

Cambiar:
```csharp
    bool EsActivoVentas = new[] { "Cotizaciones", "Pedidos", "Entregas" }.Any(EsActivo);
```
por:
```csharp
    bool EsActivoVentas = new[] { "Cotizaciones", "Pedidos", "Entregas", "Facturas" }.Any(EsActivo);
```

Y agregar, dentro de `<div class="collapse ..." id="submenuVentas">`, después del enlace de Entregas (agregado en la Fase 2):
```html
                        <a class="nav-link nav-sublink @(EsActivo("Facturas") ? "active" : "")" asp-controller="Facturas" asp-action="Index">
                            <i class="fa-solid fa-file-invoice-dollar"></i><span>Facturas</span>
                        </a>
```

- [ ] **Step 6: Compilar la Web**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\Web && dotnet build Web.slnx -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\web_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 7: Commit**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\Web
git add -A -- ':!.vs' ':!*.suo'
git commit -m "feat: agregar pantalla Web de Facturas

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 4: Verificación final conjunta

### Task 10: Confirmar que los tres módulos conviven sin romperse entre sí

**Files:**
- Ninguno (solo verificación).

- [ ] **Step 1: Build completo de la API**

Run: `dotnet build API.sln -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 2: Suite completa de pruebas de la API**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\API && dotnet test API.Service.WebApi.Tests/API.Service.WebApi.Tests.csproj -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\api_test_publish_tests"`
Expected: todas las pruebas en verde (376 previas a este plan + 9 nuevas de dominio -- `PedidoDomainTests`, `EntregaDomainTests`, `FacturaDomainTests`, 7 cada una -- + las de `*ControllerTests` de los 3 módulos, encabezado y detalle).

- [ ] **Step 3: Build completo de la Web**

Run: `cd C:\Users\Miguel\source\repos\angelm0508\Web && dotnet build Web.slnx -p:OutputPath="C:\Users\Miguel\AppData\Local\Temp\claude\web_test_publish"`
Expected: `0 Errores`.

- [ ] **Step 4: Revisión visual del menú "Ventas"**

Confirmar en `_Layout.cshtml` que el submenú "Ventas" quedó con los cuatro enlaces en orden: Cotizaciones, Pedidos, Entregas, Facturas, y que `EsActivoVentas` incluye los cuatro nombres de controlador.

- [ ] **Step 5: Recordatorio para el usuario**

Avisar al usuario que debe:
1. Reiniciar la depuración de la API y de Web.UI en Visual Studio (ambas tocan código).
2. Configurar al menos una serie por cada nuevo módulo desde "Numeración de documentos" (`CodigoObj` 4 = Pedido, 5 = Entrega, 6 = Factura) antes de poder crear registros.

- [ ] **Step 6: Commit final (si quedó algo suelto)**

```bash
cd C:\Users\Miguel\source\repos\angelm0508\API
git status --short
# Si hay cambios sin commit (p. ej. el propio archivo de plan marcado con checkboxes), confirmarlos:
git add -A -- ':!.vs' ':!*.suo'
git commit -m "docs: completar plan de Pedido/Entrega/Factura

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```
