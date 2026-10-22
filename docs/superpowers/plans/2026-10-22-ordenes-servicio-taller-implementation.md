# Órdenes de servicio del taller Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Registrar órdenes de servicio del taller de motocicletas (objeto 70) con catálogo de motocicletas y de mecánicos, insumos que descuentan inventario al agregarse a la orden (y se devuelven al quitarlos o cancelar), mano de obra como servicios, estados Recibida → En proceso → Terminada → Entregada, y facturación con "Copiar a Factura" sin volver a descontar lo que ya salió.

**Architecture:** La orden es un documento propio (`OrdenServicio` + `OrdenServicioDetalle`) con dominios `OrdenServicioDomain` (cabecera, estados, cancelación) y `OrdenServicioDetalleDomain` (líneas); un servicio compartido `OrdenServicioLineaService` valida las líneas, calcula los montos en el servidor y asienta el inventario con `IInventarioAsientoService` (que gana `RevertirLineaAsync`). La orden entra a la cadena "Copiar a" ya construida como origen 70 → Factura 6 (`DocumentoBaseService`), y `FacturaDomain` no descuenta las líneas con base 70. La Web agrega el menú Taller con Mecánicos, Motocicletas y Órdenes de servicio.

**Tech Stack:** .NET 7 API (N-capas, EF Core 7 DB-first, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server (solo bases temporales para la prueba de humo), bash/node para parches.

**Spec:** `docs/superpowers/specs/2026-10-22-ordenes-servicio-taller-design.md` (repo API, commit `5619249`). Referencias de patrón en el código: `NotaCreditoDomain` (serie, socio, totales del servidor, numeración), `EntregaDomain` (asiento de inventario en transacción), `DocumentoBaseService` y `docs/superpowers/specs/2026-10-18-copiar-a-documentos-venta-design.md` (cadena "Copiar a"), `AlmacenController`/`AlmacenesController` (catálogo simple) y `EntregasController`/`entregas.js` (formulario con líneas).

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. Antes de empezar: `git log --oneline -3` muestra `5619249` (spec) o posterior; `git status --short` solo con `API.Service.WebApi/appsettings.json` modificado. Línea base de pruebas: **1573** pasando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `223f686` (o posterior). `git status --short` muestra **4 archivos ajenos** sin commit que NO se tocan ni se stagean: `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`. Línea base: `dotnet build Web.slnx` con 0 errores.
- Build/test con `BaseOutputPath` en el scratchpad (el API de desarrollo del usuario puede estar corriendo y bloquear `bin\`; ejecutar **desde PowerShell**, en bash la barra final rompe el quoting):
  - API: `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`
  - Una sola clase de pruebas: `dotnet test API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="…\apitest\" --filter "FullyQualifiedName~NombreDeClase"` (`…\apitest\` = la ruta completa del scratchpad de arriba).
- SQL de pruebas: SOLO bases temporales (`API_DB_TALLER_A`, `API_DB_TALLER_B`); **jamás** `API_DB_TEST`. La contraseña de `sa` se extrae de `API.Service.WebApi\appsettings.json` (`[regex]::Match($cfg,'Password=([^;"]*)')`) a una variable de proceso y no se imprime ni se escribe en archivos. Toda base temporal se elimina al terminar (`ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE …`).

## Global Constraints

Cada tarea las incluye implícitamente.

- **Código de objeto:** orden de servicio = `70` (`TipoObjeto='70'`, `NumeracionDocumento.CodigoObj='70'`, `SubTipoDoc='--'`, `MovimientoInventario.TipoDoc="70"`, `FacturaDetalle.BaseTipo=70`). Los ocupados no se tocan: 1, 2, 3, 4, 5, 6, 10, 11, 12, 13, 59, 60 (más las filas con `SubTipoDoc` de notas).
- **Módulos de permisos (grupo `Taller`):** `Mecanicos`, `Motocicletas`, `OrdenesServicio`. Acciones `Ver`/`Agregar`/`Actualizar`/`Eliminar` (claims `Modulo:VAUE`).
- **Estados de la orden (`EstadoServicio`):** `R` Recibida, `P` En proceso, `T` Terminada, `E` Entregada. Solo avanzan (R<P<T<E); nunca retroceden. La cancelación es aparte (`Cancelado='S'`) y final. Una orden **activa** tiene `Cancelado != 'S'`.
- **Edición según estado:** en `R`, `P`, `T` se editan cabecera y líneas; en `E` solo el comentario; cancelada, nada.
- **Insumo vs. servicio:** lo decide el artículo (`Articulo.ArticuloInventario = 'S'` → insumo). El servidor guarda `OrdenServicioDetalle.EsInsumo` (`'S'`/`'N'`). Un insumo exige almacén y existencia suficiente (`permitirNegativo: false`); un servicio no mueve inventario.
- **Inventario por línea (siempre dentro de `IEjecutorTransaccion`, `TipoDoc="70"`, `DocEntry=Entry`, `DocLinea=NoLinea`):** agregar = salida; cambiar cantidad/almacén/artículo = `RevertirLineaAsync` + nueva salida; quitar = `RevertirLineaAsync`; cancelar la orden = `RevertirAsync("70", entry)`.
- **Totales:** los calcula siempre el servidor con `Math.Round(x, 2, MidpointRounding.AwayFromZero)`: `bruto = Cantidad×Precio`, `desc = bruto×PrctjeDesc/100`, `impuesto = (bruto−desc)×tasa/100`, `TotalLinea = bruto−desc+impuesto`; cabecera = sumas. Lo que mande el cliente en `Impuesto`/`TotalLinea`/totales se ignora.
- **Facturación:** origen 70 → destino 6 únicamente; solo con orden activa en estado `T` o `E`; cantidad abierta en vivo contra Facturas activas (`BaseTipo=70`); `FacturaDomain` descuenta inventario de las líneas con cantidad > 0 y `BaseTipo` distinto de 5 **y** de 70. Una orden con Facturas activas no se cancela ni cambia de cliente y sus líneas facturadas no se modifican ni se quitan.
- **Mensajes en español exactamente como aparecen** en los bloques de código del plan (las pruebas los verifican literalmente).
- **Bajas lógicas:** Mecánico y Motocicleta no se borran; `DELETE` pone `Activo='N'`. Las órdenes no se eliminan (no hay `DELETE`).
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` puede aparecer modificado: NUNCA commitearlo. Los 4 archivos ajenos de la Web (arriba) NUNCA se stagean ni se modifican. **Sin `git push`.**
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (CRLF/LF; comprobar con `file`); los archivos nuevos en LF; sin churn de EOL (`git diff --stat` de una edición pequeña no puede mostrar cientos de líneas). Comentarios y mensajes en español, estilo del repo.
- **Verificación al cerrar cada tarea de la API:** `dotnet build` sin errores y las pruebas de la tarea en verde; la suite completa (1573 + las nuevas) se corre en la Tarea 11.

## Ajustes respecto al spec (decididos al planificar, con el código leído)

1. **`OrdenServicioDetalle.EsInsumo`** (`'S'`/`'N'`): columna que fija el servidor desde el artículo. El spec decía "sin campo de tipo", pero sin ella la Web no puede pintar la insignia Insumo/Servicio (un servicio puede traer almacén por defecto) ni el filtro sin consultar cada artículo.
2. **`OrdenServicio.Direccion`** (nullable) y sin `BaseTipo`/`BaseEntry`/`EstadoDoc`/`Imprimido`: la dirección viaja al borrador de la Factura; las demás columnas no aplican a la orden.
3. **Cambio de una línea = revertir la línea + nuevo asiento** (en vez de un movimiento por la diferencia). El stock neto es el mismo, el bloqueo por stock se evalúa sobre la cantidad final y hay menos casos; el kardex muestra la reversa y la nueva salida.
4. **Líneas (`POST/PUT/DELETE api/OrdenServicioDetalle`) exigen `Actualizar`** sobre `OrdenesServicio` (editar las líneas es editar la orden). Cancelar una orden exige `Eliminar`; cambiar de estado, `Actualizar`; crear, `Agregar`.
5. **Almacén en servicios:** opcional; si se omite y el artículo tiene `AlmacenDefecto`, el servidor lo copia. La Factura exige almacén por línea (validación existente `Línea n: el almacén es requerido.`), así que en el formulario de Factura precargado el usuario elige uno para los servicios sin almacén (regla ya vigente de "Copiar a"); no mueve inventario porque el artículo no es de inventario.
6. **Sin `DELETE` de órdenes** (solo cancelación); catálogos con baja lógica.
7. **Listados con nombres:** `OrdenServicioRepositorio` incluye `MotoNavigation` y `MecanicoNavigation`, y el DTO trae `Placa`, `MotoDescripcion` y `NombreMecanico`; `MotocicletaRepositorio` incluye el socio y el DTO trae `NombreSn`.
8. **Mecánico por línea:** la API lo admite (`CodMecanico` en la línea, validado), pero la Web solo expone el mecánico de la cabecera en esta versión.

## File Structure

**API — entidades y datos**
- Crear `sql/2026-10-22-ordenes-servicio-taller.sql`.
- Crear `API.Domain.Entity/Models/{Mecanico,Motocicleta,OrdenServicio,OrdenServicioDetalle}.cs`; modificar `API.Domain.Entity/Models/ApiDbTestContext.cs`.
- Crear `API.Infraestructure.Repository/{Mecanico,Motocicleta,OrdenServicio,OrdenServicioDetalle}Repositorio.cs`.

**API — dominio**
- Modificar `API.Domain.Interface/IInventarioAsientoService.cs`, `API.Domain.Core/InventarioAsientoService.cs` (`RevertirLineaAsync`).
- Crear `API.Domain.Interface/{IMecanicoDomain,IMotocicletaDomain,IOrdenServicioLineaService,IOrdenServicioDomain,IOrdenServicioDetalleDomain}.cs`.
- Crear `API.Domain.Core/{MecanicoDomain,MotocicletaDomain,OrdenServicioLineaService,OrdenServicioDomain,OrdenServicioDetalleDomain}.cs`.
- Modificar `API.Domain.Interface/IDocumentoBaseService.cs`, `API.Domain.Core/DocumentoBaseService.cs`, `API.Domain.Core/FacturaDomain.cs`.

**API — aplicación / servicio web**
- Crear `API.Application.DTO/{mecanico,motocicleta,ordenServicio}/…`, `API.Application.Interface/{IMecanico,IMotocicleta,IOrdenServicio,IOrdenServicioDetalle}Application.cs`, `API.Application.Main/{Mecanico,Motocicleta,OrdenServicio,OrdenServicioDetalle}Application.cs`.
- Crear `API.Service.WebApi/Controllers/{Mecanico,Motocicleta,OrdenServicio,OrdenServicioDetalle}Controller.cs`.
- Modificar `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`, `API.Service.WebApi/Controllers/DocumentoBaseController.cs`, `sql/crear-api-db-test-completo.sql`.

**API — pruebas** (`API.Service.WebApi.Tests`)
- Crear `Domain/{ModeloTallerTests,MecanicoDomainTests,MotocicletaDomainTests,OrdenServicioLineaServiceTests,OrdenServicioDomainTests,OrdenServicioDetalleDomainTests}.cs`, `Controllers/{MecanicoControllerTests,MotocicletaControllerTests,OrdenServicioControllerTests,TallerPermisosTests}.cs`.
- Modificar `Domain/{InventarioAsientoServiceTests,DocumentoBaseContratosTests,DocumentoBaseServiceTests,FacturaDomainTests}.cs`, `Controllers/DocumentoBaseControllerTests.cs`.

**Web** (`Web.ApiClient`, `Web.UI`)
- Crear `Web.ApiClient/Dtos/{Mecanico,Motocicleta,OrdenServicio}/…` y `Web.ApiClient/Clientes/{IMecanico,IMotocicleta,IOrdenServicio}ApiClient.cs` + implementaciones.
- Crear `Web.UI/Controllers/{Mecanicos,Motocicletas,OrdenesServicio}Controller.cs`, `Web.UI/Views/{Mecanicos,Motocicletas,OrdenesServicio}/{Index,_Form}.cshtml`, `Web.UI/wwwroot/js/{mecanicos,motocicletas,ordenesservicio}.js`.
- Modificar `Web.UI/Program.cs`, `Web.UI/Views/Shared/_Layout.cshtml`, `Web.UI/wwwroot/js/site.js`.

---

## Fase 1 — Base de datos, entidades e inventario por línea (API)

### Task 1: Script SQL, entidades EF, mapeo y repositorios del taller

**Precondición:** `git log --oneline -1` en la API muestra `5619249` o posterior; `dotnet build API.sln -p:BaseOutputPath="…\apitest\"` (PowerShell) → 0 errores; `grep -n "Mecanico\|Motocicleta\|OrdenServicio" API.Domain.Entity/Models/ApiDbTestContext.cs` sin resultados.

**Files:**
- Create: `sql/2026-10-22-ordenes-servicio-taller.sql`
- Create: `API.Domain.Entity/Models/Mecanico.cs`, `Motocicleta.cs`, `OrdenServicio.cs`, `OrdenServicioDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs`
- Create: `API.Infraestructure.Repository/MecanicoRepositorio.cs`, `MotocicletaRepositorio.cs`, `OrdenServicioRepositorio.cs`, `OrdenServicioDetalleRepositorio.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/ModeloTallerTests.cs`

**Interfaces:**
- Produces (entidades, namespace `API.Domain.Entity.Models`): `Mecanico { string Codigo; string Nombre; string? Telefono; string Activo }`; `Motocicleta { int Entry; string CodigoSn; string Placa; string? Marca; string? Modelo; short? Anio; string? Color; string? Vin; string? Comentario; string Activo; SocioNegocio? CodigoSnNavigation }`; `OrdenServicio` (columnas del SQL de abajo, con `MotoNavigation` y `MecanicoNavigation`); `OrdenServicioDetalle` (clave compuesta `Entry`+`NoLinea`).
- Produces (repositorios): `IRepositorioGenerico<Mecanico, string>`, `<Motocicleta, int>`, `<OrdenServicio, int>`, `<OrdenServicioDetalle, (int Entry, int NoLinea)>` registrados en DI.
- Produces (SQL): tablas `Mecanico`, `Motocicleta`, `OrdenServicio`, `OrdenServicioDetalle`; serie "Primario" y fila padre de numeración para `'70'`; módulos del grupo `Taller`; `CHECK` de `FacturaDetalle.BaseTipo` ampliado a `{-1,3,4,5,70}`.

- [ ] **Step 1: Escribir la prueba del modelo (fallará)** — crear `API.Service.WebApi.Tests/Domain/ModeloTallerTests.cs`:

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las entidades del taller sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloTallerTests
    {
        private static readonly DbContextOptions<ApiDbTestContext> _opciones =
            new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=localhost;Database=API_DB_TEST;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

        private static IModel Modelo()
        {
            using var ctx = new ApiDbTestContext(_opciones);
            return ctx.Model;
        }

        [Fact]
        public void Mecanico_MapeaTablaYClave()
        {
            var et = Modelo().FindEntityType(typeof(Mecanico))!;
            Assert.Equal("Mecanico", et.GetTableName());
            Assert.Equal(new[] { "Codigo" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
        }

        [Fact]
        public void Motocicleta_MapeaTablaPlacaUnicaYClienteComoClaveForanea()
        {
            var et = Modelo().FindEntityType(typeof(Motocicleta))!;
            Assert.Equal("Motocicleta", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            var indicePlaca = et.GetIndexes().Single(i => i.Properties.Single().Name == "Placa");
            Assert.True(indicePlaca.IsUnique);
            Assert.Contains(et.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(SocioNegocio));
        }

        [Fact]
        public void OrdenServicio_MapeaTablaClavesForaneasYEstadoPorDefecto()
        {
            var et = Modelo().FindEntityType(typeof(OrdenServicio))!;
            Assert.Equal("OrdenServicio", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal("NombreSN", et.FindProperty("NombreSn")!.GetColumnName());

            var destinos = et.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).ToHashSet();
            Assert.Contains(typeof(SocioNegocio), destinos);
            Assert.Contains(typeof(Monedum), destinos);
            Assert.Contains(typeof(NumeracionDocumentoDet), destinos);
            Assert.Contains(typeof(Motocicleta), destinos);
            Assert.Contains(typeof(Mecanico), destinos);

            Assert.Equal("R", new OrdenServicio().EstadoServicio);
        }

        [Fact]
        public void OrdenServicioDetalle_MapeaTablaYClaveCompuesta()
        {
            var et = Modelo().FindEntityType(typeof(OrdenServicioDetalle))!;
            Assert.Equal("OrdenServicioDetalle", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var destinos = et.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).ToHashSet();
            Assert.Contains(typeof(Articulo), destinos);
            Assert.Contains(typeof(Almacen), destinos);
        }
    }
}
```

- [ ] **Step 2: Ejecutarla y ver que falla** — `dotnet test … --filter "FullyQualifiedName~ModeloTallerTests"` (PowerShell) → no compila (`Mecanico` no existe). Es el rojo esperado.

- [ ] **Step 3: Crear las cuatro entidades** (archivos nuevos, LF, mismo estilo que `Almacen.cs`):

`API.Domain.Entity/Models/Mecanico.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Mecanico
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Telefono { get; set; }

    public string Activo { get; set; } = null!;
}
```

`API.Domain.Entity/Models/Motocicleta.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Motocicleta
{
    public int Entry { get; set; }

    public string CodigoSn { get; set; } = null!;

    public string Placa { get; set; } = null!;

    public string? Marca { get; set; }

    public string? Modelo { get; set; }

    public short? Anio { get; set; }

    public string? Color { get; set; }

    public string? Vin { get; set; }

    public string? Comentario { get; set; }

    public string Activo { get; set; } = null!;

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }
}
```

`API.Domain.Entity/Models/OrdenServicio.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class OrdenServicio
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaIngreso { get; set; }

    public DateTime? FechaPromesa { get; set; }

    public DateTime? FechaEntrega { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public string? Direccion { get; set; }

    public string? MonedaDoc { get; set; }

    public int MotoEntry { get; set; }

    public string? CodMecanico { get; set; }

    public int? KmIngreso { get; set; }

    public string? FallaReportada { get; set; }

    public string? Diagnostico { get; set; }

    public string? Observaciones { get; set; }

    public string EstadoServicio { get; set; } = "R";

    public decimal? TotalBruto { get; set; }

    public decimal? TotalDesc { get; set; }

    public decimal? TotalImp { get; set; }

    public decimal? TotalDoc { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual Monedum? MonedaDocNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;

    public virtual Motocicleta? MotoNavigation { get; set; }

    public virtual Mecanico? MecanicoNavigation { get; set; }
}
```

`API.Domain.Entity/Models/OrdenServicioDetalle.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class OrdenServicioDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public string? CodArticulo { get; set; }

    public string? Descripcion { get; set; }

    public string? CodAlmacen { get; set; }

    public string? EsInsumo { get; set; }

    public decimal? Cantidad { get; set; }

    public decimal? Precio { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public string? CodigoImpuesto { get; set; }

    public decimal? Impuesto { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? CodMecanico { get; set; }

    public string? TipoObjeto { get; set; }

    public virtual Articulo? CodArticuloNavigation { get; set; }

    public virtual Almacen? CodAlmacenNavigation { get; set; }
}
```

- [ ] **Step 4: Mapeo EF en `ApiDbTestContext.cs`** (conservar su fin de línea y BOM).

a) Agregar los `DbSet` justo después de `public virtual DbSet<Almacen> Almacens { get; set; }`:
```csharp
    public virtual DbSet<Mecanico> Mecanicos { get; set; }

    public virtual DbSet<Motocicleta> Motocicletas { get; set; }

    public virtual DbSet<OrdenServicio> OrdenServicios { get; set; }

    public virtual DbSet<OrdenServicioDetalle> OrdenServicioDetalles { get; set; }

```

b) Agregar los cuatro bloques de configuración **justo antes** de la línea `        OnModelCreatingPartial(modelBuilder);` (al final de `OnModelCreating`):
```csharp
        modelBuilder.Entity<Mecanico>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_mecanico");

            entity.ToTable("Mecanico");

            entity.Property(e => e.Codigo).HasMaxLength(10);
            entity.Property(e => e.Activo)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.Telefono).HasMaxLength(30);
        });

        modelBuilder.Entity<Motocicleta>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_motocicleta");

            entity.ToTable("Motocicleta");

            entity.HasIndex(e => e.Placa, "uq_motocicleta_placa").IsUnique();

            entity.Property(e => e.Activo)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Color).HasMaxLength(30);
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Marca).HasMaxLength(50);
            entity.Property(e => e.Modelo).HasMaxLength(50);
            entity.Property(e => e.Placa).HasMaxLength(15);
            entity.Property(e => e.Vin).HasMaxLength(30);

            entity.HasOne(d => d.CodigoSnNavigation).WithMany()
                .HasForeignKey(d => d.CodigoSn)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_motocicleta_sn");
        });

        modelBuilder.Entity<OrdenServicio>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_orden_servicio");

            entity.ToTable("OrdenServicio");

            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodMecanico).HasMaxLength(10);
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Diagnostico).HasMaxLength(500);
            entity.Property(e => e.Direccion).HasMaxLength(254);
            entity.Property(e => e.EstadoServicio)
                .HasMaxLength(1)
                .HasDefaultValueSql("('R')");
            entity.Property(e => e.FallaReportada).HasMaxLength(500);
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.FechaEntrega).HasColumnType("datetime");
            entity.Property(e => e.FechaIngreso).HasColumnType("datetime");
            entity.Property(e => e.FechaPromesa).HasColumnType("datetime");
            entity.Property(e => e.MonedaDoc).HasMaxLength(3);
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Observaciones).HasMaxLength(500);
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('70')");
            entity.Property(e => e.TotalBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalImp).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany()
                .HasForeignKey(d => d.CodigoSn)
                .HasConstraintName("fk_orden_servicio_sn");

            entity.HasOne(d => d.MonedaDocNavigation).WithMany()
                .HasForeignKey(d => d.MonedaDoc)
                .HasConstraintName("fk_orden_servicio_moneda");

            entity.HasOne(d => d.SerieNavigation).WithMany()
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_orden_servicio_serie");

            entity.HasOne(d => d.MotoNavigation).WithMany()
                .HasForeignKey(d => d.MotoEntry)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_orden_servicio_moto");

            entity.HasOne(d => d.MecanicoNavigation).WithMany()
                .HasForeignKey(d => d.CodMecanico)
                .HasConstraintName("fk_orden_servicio_mecanico");
        });

        modelBuilder.Entity<OrdenServicioDetalle>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_orden_servicio_det");

            entity.ToTable("OrdenServicioDetalle");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CodAlmacen).HasMaxLength(8);
            entity.Property(e => e.CodArticulo).HasMaxLength(15);
            entity.Property(e => e.CodMecanico).HasMaxLength(10);
            entity.Property(e => e.CodigoImpuesto).HasMaxLength(8);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.EsInsumo)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.Impuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Precio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(20)
                .HasDefaultValueSql("('70')");
            entity.Property(e => e.TotalLinea).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodAlmacenNavigation).WithMany()
                .HasForeignKey(d => d.CodAlmacen)
                .HasConstraintName("fk_orden_servicio_det_almacen");

            entity.HasOne(d => d.CodArticuloNavigation).WithMany()
                .HasForeignKey(d => d.CodArticulo)
                .HasConstraintName("fk_orden_servicio_det_cod_art");
        });

```

- [ ] **Step 5: Repositorios** (archivos nuevos). `MecanicoRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class MecanicoRepositorio : RepositorioGenericoEfCore<Mecanico, string>
    {
        public MecanicoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`MotocicletaRepositorio.cs` (incluye el socio para el nombre del cliente):
```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class MotocicletaRepositorio : RepositorioGenericoEfCore<Motocicleta, int>
    {
        public MotocicletaRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<Motocicleta?> ObtenerAsync(int id)
        {
            return await DbSet
                .Include(x => x.CodigoSnNavigation)
                .FirstOrDefaultAsync(x => x.Entry == id);
        }

        public override Task<IQueryable<Motocicleta>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.CodigoSnNavigation).AsQueryable());
        }
    }
}
```

`OrdenServicioRepositorio.cs` (incluye moto y mecánico para los nombres del listado):
```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class OrdenServicioRepositorio : RepositorioGenericoEfCore<OrdenServicio, int>
    {
        public OrdenServicioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<OrdenServicio?> ObtenerAsync(int id)
        {
            return await DbSet
                .Include(x => x.MotoNavigation)
                .Include(x => x.MecanicoNavigation)
                .FirstOrDefaultAsync(x => x.Entry == id);
        }

        public override Task<IQueryable<OrdenServicio>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet
                .Include(x => x.MotoNavigation)
                .Include(x => x.MecanicoNavigation)
                .AsQueryable());
        }
    }
}
```

`OrdenServicioDetalleRepositorio.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class OrdenServicioDetalleRepositorio : RepositorioGenericoEfCore<OrdenServicioDetalle, (int Entry, int NoLinea)>
    {
        public OrdenServicioDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaró HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<OrdenServicioDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
```

- [ ] **Step 6: DI en `Startup.cs`** — agregar, junto a las líneas de `IRepositorioGenerico<NotaCredito, int>` (≈ línea 240):
```csharp
            services.AddTransient<IRepositorioGenerico<Mecanico, string>, MecanicoRepositorio>();
            services.AddTransient<IRepositorioGenerico<Motocicleta, int>, MotocicletaRepositorio>();
            services.AddTransient<IRepositorioGenerico<OrdenServicio, int>, OrdenServicioRepositorio>();
            services.AddTransient<IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>, OrdenServicioDetalleRepositorio>();
```

- [ ] **Step 7: Script SQL** — crear `sql/2026-10-22-ordenes-servicio-taller.sql` (UTF-8, idempotente; **el usuario lo aplica** en `API_DB_TEST`; aquí solo se escribe y se usa en bases temporales en la Task 11):

```sql
-- Taller: ordenes de servicio de motocicletas (objeto 70), mecanicos y motocicletas.
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

-- ===== Mecanico =====
IF OBJECT_ID('dbo.Mecanico', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Mecanico (
        Codigo   nvarchar(10)  NOT NULL,
        Nombre   nvarchar(100) NOT NULL,
        Telefono nvarchar(30)  NULL,
        Activo   nvarchar(1)   NOT NULL CONSTRAINT DF_Mecanico_Activo DEFAULT ('S') CONSTRAINT CK_Mecanico_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_mecanico PRIMARY KEY (Codigo)
    );
END

-- ===== Motocicleta =====
IF OBJECT_ID('dbo.Motocicleta', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Motocicleta (
        Entry      int identity(1,1) NOT NULL,
        CodigoSN   nvarchar(15)  NOT NULL,
        Placa      nvarchar(15)  NOT NULL,
        Marca      nvarchar(50)  NULL,
        Modelo     nvarchar(50)  NULL,
        Anio       smallint      NULL,
        Color      nvarchar(30)  NULL,
        Vin        nvarchar(30)  NULL,
        Comentario nvarchar(254) NULL,
        Activo     nvarchar(1)   NOT NULL CONSTRAINT DF_Motocicleta_Activo DEFAULT ('S') CONSTRAINT CK_Motocicleta_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_motocicleta PRIMARY KEY (Entry),
        CONSTRAINT uq_motocicleta_placa UNIQUE (Placa),
        CONSTRAINT fk_motocicleta_sn FOREIGN KEY (CodigoSN) REFERENCES dbo.SocioNegocio(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Motocicleta_CodigoSN' AND object_id = OBJECT_ID('dbo.Motocicleta'))
    CREATE NONCLUSTERED INDEX IX_Motocicleta_CodigoSN ON dbo.Motocicleta (CodigoSN);

-- ===== OrdenServicio (ObjType 70) =====
IF OBJECT_ID('dbo.OrdenServicio', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdenServicio (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_Cancelado DEFAULT ('N') CONSTRAINT CK_OrdenServicio_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_NumManual DEFAULT ('N') CONSTRAINT CK_OrdenServicio_NumManual CHECK (NumManual IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_OrdenServicio_TipoObjeto DEFAULT ('70') CONSTRAINT CK_OrdenServicio_TipoObjeto CHECK (TipoObjeto IN ('70')),
        FechaDoc       datetime      NOT NULL,
        FechaIngreso   datetime      NULL,
        FechaPromesa   datetime      NULL,
        FechaEntrega   datetime      NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        MotoEntry      int           NOT NULL,
        CodMecanico    nvarchar(10)  NULL,
        KmIngreso      int           NULL,
        FallaReportada nvarchar(500) NULL,
        Diagnostico    nvarchar(500) NULL,
        Observaciones  nvarchar(500) NULL,
        EstadoServicio nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_Estado DEFAULT ('R') CONSTRAINT CK_OrdenServicio_Estado CHECK (EstadoServicio IN ('R','P','T','E')),
        TotalBruto     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_orden_servicio PRIMARY KEY (Entry),
        CONSTRAINT fk_orden_servicio_serie    FOREIGN KEY (Serie)       REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_orden_servicio_sn       FOREIGN KEY (CodigoSN)    REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_orden_servicio_moneda   FOREIGN KEY (MonedaDoc)   REFERENCES dbo.Moneda(Codigo),
        CONSTRAINT fk_orden_servicio_moto     FOREIGN KEY (MotoEntry)   REFERENCES dbo.Motocicleta(Entry),
        CONSTRAINT fk_orden_servicio_mecanico FOREIGN KEY (CodMecanico) REFERENCES dbo.Mecanico(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrdenServicio_Moto' AND object_id = OBJECT_ID('dbo.OrdenServicio'))
    CREATE NONCLUSTERED INDEX IX_OrdenServicio_Moto ON dbo.OrdenServicio (MotoEntry) INCLUDE (Cancelado, EstadoServicio);

-- ===== OrdenServicioDetalle =====
IF OBJECT_ID('dbo.OrdenServicioDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdenServicioDetalle (
        Entry          int           NOT NULL,
        NoLinea        int           NOT NULL,
        CodArticulo    nvarchar(15)  NOT NULL,
        Descripcion    nvarchar(200) NULL,
        CodAlmacen     nvarchar(8)   NULL,
        EsInsumo       nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicioDet_EsInsumo DEFAULT ('N') CONSTRAINT CK_OrdenServicioDet_EsInsumo CHECK (EsInsumo IN ('S','N')),
        Cantidad       decimal(19,6) NULL,
        Precio         decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        CodigoImpuesto nvarchar(8)   NOT NULL,
        Impuesto       decimal(19,6) NULL,
        TotalLinea     decimal(19,6) NULL,
        CodMecanico    nvarchar(10)  NULL,
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_OrdenServicioDet_TipoObjeto DEFAULT ('70') CONSTRAINT CK_OrdenServicioDet_TipoObjeto CHECK (TipoObjeto IN ('70')),
        CONSTRAINT pk_orden_servicio_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_orden_servicio_det_orden    FOREIGN KEY (Entry)          REFERENCES dbo.OrdenServicio(Entry),
        CONSTRAINT fk_orden_servicio_det_cod_art  FOREIGN KEY (CodArticulo)    REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_orden_servicio_det_almacen  FOREIGN KEY (CodAlmacen)     REFERENCES dbo.Almacen(Codigo),
        CONSTRAINT fk_orden_servicio_det_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo),
        CONSTRAINT fk_orden_servicio_det_mecanico FOREIGN KEY (CodMecanico)    REFERENCES dbo.Mecanico(Codigo)
    );
END

-- ===== FacturaDetalle.BaseTipo admite 70 (orden de servicio como documento base) =====
-- El CHECK original es de columna y tiene nombre generado por SQL Server: se busca por definicion.
DECLARE @ck sysname;
DECLARE @sqlck nvarchar(400);
WHILE 1 = 1
BEGIN
    SET @ck = NULL;
    SELECT TOP 1 @ck = cc.name
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('dbo.FacturaDetalle')
      AND cc.definition LIKE '%BaseTipo%'
      AND cc.definition NOT LIKE '%70%';
    IF @ck IS NULL BREAK;
    SET @sqlck = N'ALTER TABLE dbo.FacturaDetalle DROP CONSTRAINT ' + QUOTENAME(@ck);
    EXEC (@sqlck);
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FacturaDetalle') AND name = 'CK_FacturaDetalle_BaseTipo')
    ALTER TABLE dbo.FacturaDetalle ADD CONSTRAINT CK_FacturaDetalle_BaseTipo CHECK (BaseTipo IN (-1, 3, 4, 5, 70));

-- ===== Numeracion (serie "Primario" y fila padre para la pantalla "Numeracion de documentos") =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('70', (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet), 'Primario', 'N', '--', 'D', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '70' AND SubTipoDoc = '--')
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SubTipoDoc, DocAlias, SerieDfct)
    SELECT '70', '--', 'Orden de servicio', MIN(Serie) FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70';

-- ===== Catalogo de permisos (grupo Taller) =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('Mecanicos', 'Mecánicos', 'Taller'),
    ('Motocicletas', 'Motocicletas', 'Taller'),
    ('OrdenesServicio', 'Órdenes de servicio', 'Taller')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Taller DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('Mecanico','Motocicleta','OrdenServicio','OrdenServicioDetalle') ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Grupo = 'Taller';
SELECT name, definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FacturaDetalle') AND definition LIKE '%BaseTipo%';
```

- [ ] **Step 8: Verificar** — `dotnet test … --filter "FullyQualifiedName~ModeloTallerTests"` (PowerShell) → 4 pasando; `dotnet build API.sln -p:BaseOutputPath="…\apitest\"` → 0 errores. (No se ejecuta el SQL aquí.)

- [ ] **Step 9: Commit**

```bash
git add sql/2026-10-22-ordenes-servicio-taller.sql API.Domain.Entity/Models/Mecanico.cs API.Domain.Entity/Models/Motocicleta.cs API.Domain.Entity/Models/OrdenServicio.cs API.Domain.Entity/Models/OrdenServicioDetalle.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Infraestructure.Repository/MecanicoRepositorio.cs API.Infraestructure.Repository/MotocicletaRepositorio.cs API.Infraestructure.Repository/OrdenServicioRepositorio.cs API.Infraestructure.Repository/OrdenServicioDetalleRepositorio.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ModeloTallerTests.cs
git commit -m "feat(taller): tablas, entidades EF y repositorios de mecanicos, motocicletas y ordenes de servicio

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `RevertirLineaAsync` en el servicio de inventario

**Precondición:** Task 1 completa. `grep -n "RevertirLineaAsync" API.Domain.Interface/IInventarioAsientoService.cs` sin resultados.

**Files:**
- Modify: `API.Domain.Interface/IInventarioAsientoService.cs`, `API.Domain.Core/InventarioAsientoService.cs`
- Test: `API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs`

**Interfaces:**
- Consumes: `MovimientoInventario` (`TipoDoc`, `DocEntry`, `DocLinea`, `MovReversaDe`) y el helper `AplicarMovimientoAsync` existente.
- Produces: `Task IInventarioAsientoService.RevertirLineaAsync(string tipoDoc, int docEntry, int docLinea)` — revierte los movimientos de **esa línea** que aún no tengan reversa (mismo orden determinista y mismo cálculo que `RevertirAsync`); no hace nada si la línea no tiene movimientos o ya fue revertida.

- [ ] **Step 1: Pruebas que fallan** — en `InventarioAsientoServiceTests.cs`, dentro de la clase y justo después del test `RevertirAsync_YaRevertido_NoGeneraNada`, agregar:

```csharp
        [Fact]
        public async Task RevertirLineaAsync_SoloRevierteLaLineaIndicada()
        {
            // Documento ("70", 100) con dos líneas: salida de 4 (línea 1) y salida de 6 (línea 2).
            var linea1 = new MovimientoInventario
            {
                Entry = 600, TipoDoc = "70", DocEntry = 100, DocLinea = 1,
                CodArticulo = "ART1", CodAlmacen = "01", Fecha = new DateTime(2026, 10, 22),
                CantidadEntra = 0m, CantidadSale = 4m, CostoUnitario = 20m, MovReversaDe = null
            };
            var linea2 = new MovimientoInventario
            {
                Entry = 601, TipoDoc = "70", DocEntry = 100, DocLinea = 2,
                CodArticulo = "ART1", CodAlmacen = "01", Fecha = new DateTime(2026, 10, 22),
                CantidadEntra = 0m, CantidadSale = 6m, CostoUnitario = 20m, MovReversaDe = null
            };
            _repoMov.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[] { linea1, linea2 }.AsAsyncQueryable());
            ArticuloDeInventario("ART1", costoProm: 20m, cantActual: 90m);
            ConExistencia("ART1", "01", 90m);

            await _svc.RevertirLineaAsync("70", 100, 1);

            var rev = Assert.Single(_movAgregados);
            Assert.Equal(4m, rev.CantidadEntra);
            Assert.Equal(0m, rev.CantidadSale);
            Assert.Equal(1, rev.DocLinea);
            Assert.Equal(600, rev.MovReversaDe);
            Assert.Equal(94m, rev.SaldoCantidad);
        }

        [Fact]
        public async Task RevertirLineaAsync_YaRevertida_NoGeneraNada()
        {
            var original = new MovimientoInventario { Entry = 600, TipoDoc = "70", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadSale = 4m, MovReversaDe = null };
            var reversa  = new MovimientoInventario { Entry = 601, TipoDoc = "70", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadEntra = 4m, MovReversaDe = 600 };
            _repoMov.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[] { original, reversa }.AsAsyncQueryable());

            await _svc.RevertirLineaAsync("70", 100, 1);

            Assert.Empty(_movAgregados);
        }

        [Fact]
        public async Task RevertirLineaAsync_LineaSinMovimientos_NoHaceNada()
        {
            var otraLinea = new MovimientoInventario { Entry = 600, TipoDoc = "70", DocEntry = 100, DocLinea = 2, CodArticulo = "ART1", CodAlmacen = "01", CantidadSale = 4m, MovReversaDe = null };
            _repoMov.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[] { otraLinea }.AsAsyncQueryable());

            await _svc.RevertirLineaAsync("70", 100, 1);

            Assert.Empty(_movAgregados);
        }

        [Fact]
        public async Task RevertirLineaAsync_UnaLineaEditadaVariasVeces_RevierteSoloLosMovimientosVigentes()
        {
            // Línea 1: salida de 4 ya revertida (edición previa) y nueva salida de 7 vigente.
            var salidaVieja = new MovimientoInventario { Entry = 600, TipoDoc = "70", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadSale = 4m, CostoUnitario = 20m, MovReversaDe = null };
            var reversaVieja = new MovimientoInventario { Entry = 601, TipoDoc = "70", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", CantidadEntra = 4m, CostoUnitario = 20m, MovReversaDe = 600 };
            var salidaNueva = new MovimientoInventario { Entry = 602, TipoDoc = "70", DocEntry = 100, DocLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", Fecha = new DateTime(2026, 10, 22), CantidadSale = 7m, CostoUnitario = 20m, MovReversaDe = null };
            _repoMov.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new[] { salidaVieja, reversaVieja, salidaNueva }.AsAsyncQueryable());
            ArticuloDeInventario("ART1", costoProm: 20m, cantActual: 93m);
            ConExistencia("ART1", "01", 93m);

            await _svc.RevertirLineaAsync("70", 100, 1);

            var rev = Assert.Single(_movAgregados);
            Assert.Equal(7m, rev.CantidadEntra);
            Assert.Equal(602, rev.MovReversaDe);
        }
```

- [ ] **Step 2: Ejecutarlas y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~RevertirLineaAsync"` → no compila (`RevertirLineaAsync` no existe).

- [ ] **Step 3: Interfaz** — en `IInventarioAsientoService.cs`, tras `Task RevertirAsync(string tipoDoc, int docEntry);` agregar:

```csharp

        /// <summary>
        /// Revierte los movimientos de UNA línea de un documento (los que aún no tengan reversa), por ejemplo al
        /// quitar o cambiar una línea de una orden de servicio. Idempotente: repetirla no genera nada.
        /// </summary>
        Task RevertirLineaAsync(string tipoDoc, int docEntry, int docLinea);
```

- [ ] **Step 4: Implementación** — en `InventarioAsientoService.cs` reemplazar el método `RevertirAsync` actual por estos tres (se conserva el cuerpo; solo se parametriza el filtro por línea):

```csharp
        public Task RevertirAsync(string tipoDoc, int docEntry) =>
            RevertirInternoAsync(tipoDoc, docEntry, null);

        public Task RevertirLineaAsync(string tipoDoc, int docEntry, int docLinea) =>
            RevertirInternoAsync(tipoDoc, docEntry, docLinea);

        private async Task RevertirInternoAsync(string tipoDoc, int docEntry, int? docLinea)
        {
            var queryable = await _repoMovimiento.ObtenerTodoAsync();
            var delDocumento = queryable.Where(x => x.TipoDoc == tipoDoc && x.DocEntry == docEntry);
            if (docLinea.HasValue)
                delDocumento = delDocumento.Where(x => x.DocLinea == docLinea.Value);

            var movimientos = await delDocumento
                // orden determinista: el promedio móvil es sensible al orden en que se aplican las reversas
                .OrderBy(x => x.Entry)
                .ToListAsync();

            var yaRevertidos = movimientos
                .Where(x => x.MovReversaDe != null)
                .Select(x => x.MovReversaDe!.Value)
                .ToHashSet();

            foreach (var orig in movimientos.Where(x => x.MovReversaDe == null && !yaRevertidos.Contains(x.Entry)))
            {
                var cantidadOriginal = orig.CantidadEntra - orig.CantidadSale;   // + entrada, - salida
                await AplicarMovimientoAsync(
                    orig.TipoDoc, orig.DocEntry, orig.DocLinea, orig.CodArticulo, orig.CodAlmacen,
                    cantidad: -cantidadOriginal,
                    // La reversa se registra como salida al costo con que se valuó el original.
                    // OJO: una salida NO recalcula el promedio móvil (ver ValuacionInventario.CalcularSalida),
                    // así que anular una compra revierte la CANTIDAD exacta pero deja CostoPromedio /
                    // ValorInventario en el valor posterior a la entrada. Ajuste de valuación en la reversa:
                    // pendiente (INV-3 / deuda de valuación conocida).
                    precioUnitario: orig.CostoUnitario,
                    fecha: orig.Fecha,
                    permitirNegativo: true,   // una reversa nunca se bloquea por negativo
                    movReversaDe: orig.Entry);
            }
        }
```
(Verificar antes de editar que el cuerpo anterior de `RevertirAsync` coincide con el copiado arriba — el comentario "OJO" debe conservarse tal cual estaba en el archivo; si difiere, conservar el texto original del archivo.)

- [ ] **Step 5: Verificar** — `dotnet test … --filter "FullyQualifiedName~InventarioAsientoServiceTests"` → todas pasando (las anteriores + 4 nuevas); `dotnet build API.sln …` → 0 errores.

- [ ] **Step 6: Commit**

```bash
git add API.Domain.Interface/IInventarioAsientoService.cs API.Domain.Core/InventarioAsientoService.cs API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs
git commit -m "feat(inventario): RevertirLineaAsync revierte los movimientos de una sola linea de un documento

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---
## Fase 2 — Catálogos del taller (API)

### Task 3: Catálogo de Mecánicos (dominio, aplicación y controlador)

**Precondición:** Task 2 completa. `grep -n "IMecanico" -r API.Domain.Interface API.Application.Interface` sin resultados.

**Files:**
- Create: `API.Application.DTO/mecanico/MecanicoDTO.cs`, `MecanicoCrearDTO.cs`, `MecanicoActualizarDTO.cs`
- Create: `API.Domain.Interface/IMecanicoDomain.cs`, `API.Domain.Core/MecanicoDomain.cs`
- Create: `API.Application.Interface/IMecanicoApplication.cs`, `API.Application.Main/MecanicoApplication.cs`
- Create: `API.Service.WebApi/Controllers/MecanicoController.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/MecanicoDomainTests.cs`, `API.Service.WebApi.Tests/Controllers/MecanicoControllerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<Mecanico, string>` (Task 1).
- Produces: `IMecanicoDomain { Task<bool> InsertarAsync(Mecanico); Task<bool> ActualizarAsync(string codigo, Mecanico); Task<bool> EliminarAsync(string codigo); Task<Mecanico?> ObtenerPorCodigoAsync(string codigo); Task<IQueryable<Mecanico>> ObtenerTodoAsync(); }` (`EliminarAsync` = baja lógica, `Activo='N'`); `IMecanicoApplication` con las mismas operaciones devolviendo `Respuesta<…>`; rutas `GET api/Mecanico`, `GET api/Mecanico/{codigo}`, `POST`, `PUT {codigo}`, `DELETE {codigo}` (módulo `Mecanicos`: Ver / Ver / Agregar / Actualizar / Eliminar). DTOs `MecanicoDTO { Codigo, Nombre, Telefono, Activo }`.

- [ ] **Step 1: Pruebas de dominio (fallan)** — crear `API.Service.WebApi.Tests/Domain/MecanicoDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class MecanicoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Mecanico, string>> _repo = new();
        private readonly MecanicoDomain _domain;

        public MecanicoDomainTests()
        {
            _domain = new MecanicoDomain(_repo.Object);
            _repo.Setup(r => r.InsertarAsync(It.IsAny<Mecanico>())).ReturnsAsync((Mecanico m) => m);
            _repo.Setup(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Mecanico>())).ReturnsAsync(true);
        }

        private async Task<string> MensajeAlInsertar(Mecanico m)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(m));
            return ex.Message;
        }

        [Fact]
        public async Task InsertarAsync_Valido_GuardaConActivoPorDefecto()
        {
            var ok = await _domain.InsertarAsync(new Mecanico { Codigo = " M01 ", Nombre = " Juan Pérez " });

            Assert.True(ok);
            _repo.Verify(r => r.InsertarAsync(It.Is<Mecanico>(m => m.Codigo == "M01" && m.Nombre == "Juan Pérez" && m.Activo == "S")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinCodigo_Lanza()
        {
            Assert.Equal("El código del mecánico es requerido.", await MensajeAlInsertar(new Mecanico { Codigo = "  ", Nombre = "Juan" }));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<Mecanico>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SinNombre_Lanza()
        {
            Assert.Equal("El nombre del mecánico es requerido.", await MensajeAlInsertar(new Mecanico { Codigo = "M01", Nombre = "" }));
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Otro", Activo = "S" });

            Assert.Equal("Ya existe un registro con el código: M01", await MensajeAlInsertar(new Mecanico { Codigo = "M01", Nombre = "Juan" }));
        }

        [Fact]
        public async Task InsertarAsync_ActivoInvalido_Lanza()
        {
            Assert.Equal("El campo Activo debe ser 'S' o 'N'.", await MensajeAlInsertar(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "X" }));
        }

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalso()
        {
            _repo.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.False(await _domain.ActualizarAsync("M99", new Mecanico { Nombre = "X", Activo = "S" }));
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Mecanico>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SinNombre_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("M01", new Mecanico { Nombre = " ", Activo = "S" }));
            Assert.Equal("El nombre del mecánico es requerido.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_Valido_Delega()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync("M01", new Mecanico { Nombre = "Juan P.", Telefono = "5555", Activo = "N" }));
            _repo.Verify(r => r.ActualizarAsync("M01", It.Is<Mecanico>(m => m.Nombre == "Juan P." && m.Telefono == "5555" && m.Activo == "N")), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_HaceBajaLogica()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Telefono = "5555", Activo = "S" });

            Assert.True(await _domain.EliminarAsync("M01"));
            _repo.Verify(r => r.ActualizarAsync("M01", It.Is<Mecanico>(m => m.Nombre == "Juan" && m.Telefono == "5555" && m.Activo == "N")), Times.Once);
            _repo.Verify(r => r.EliminarAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_YaInactivo_DevuelveVerdaderoSinGuardar()
        {
            _repo.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "N" });

            Assert.True(await _domain.EliminarAsync("M01"));
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<Mecanico>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalso()
        {
            _repo.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.False(await _domain.EliminarAsync("M99"));
        }
    }
}
```

- [ ] **Step 2: Ejecutarlas y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~MecanicoDomainTests"` → no compila (`MecanicoDomain` no existe).

- [ ] **Step 3: Interfaz y dominio**

`API.Domain.Interface/IMecanicoDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMecanicoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(Mecanico obj);
        Task<bool> ActualizarAsync(string codigo, Mecanico obj);
        /// <summary>Baja lógica: el mecánico pasa a Activo='N' (nunca se borra, puede tener órdenes).</summary>
        Task<bool> EliminarAsync(string codigo);
        Task<Mecanico?> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<Mecanico>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Domain.Core/MecanicoDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class MecanicoDomain : IMecanicoDomain
    {
        private readonly IRepositorioGenerico<Mecanico, string> _repo;

        public MecanicoDomain(IRepositorioGenerico<Mecanico, string> repo)
        {
            _repo = repo;
        }

        #region async methods
        public async Task<bool> InsertarAsync(Mecanico obj)
        {
            obj.Codigo = (obj.Codigo ?? string.Empty).Trim();
            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Codigo.Length == 0)
                throw new Exception("El código del mecánico es requerido.");
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre del mecánico es requerido.");
            obj.Activo = ActivoValido(obj.Activo);

            if (await _repo.ObtenerAsync(obj.Codigo) != null)
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");

            await _repo.InsertarAsync(obj);
            return true;
        }

        public async Task<bool> ActualizarAsync(string codigo, Mecanico obj)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre del mecánico es requerido.");
            obj.Activo = ActivoValido(obj.Activo);

            return await _repo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(string codigo)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            if (existente.Activo == "N")
                return true;

            return await _repo.ActualizarAsync(codigo, new Mecanico
            {
                Nombre = existente.Nombre,
                Telefono = existente.Telefono,
                Activo = "N"
            });
        }

        public async Task<Mecanico?> ObtenerPorCodigoAsync(string codigo)
        {
            return await _repo.ObtenerAsync(codigo);
        }

        public async Task<IQueryable<Mecanico>> ObtenerTodoAsync()
        {
            return await _repo.ObtenerTodoAsync();
        }
        #endregion

        private static string ActivoValido(string? activo)
        {
            if (string.IsNullOrWhiteSpace(activo))
                return "S";
            if (activo != "S" && activo != "N")
                throw new Exception("El campo Activo debe ser 'S' o 'N'.");
            return activo;
        }
    }
}
```

- [ ] **Step 4: DTOs, aplicación, controlador, mapeo y DI**

`API.Application.DTO/mecanico/MecanicoDTO.cs`:
```csharp
namespace API.Application.DTO.mecanico
{
    public class MecanicoDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string Activo { get; set; } = null!;
    }
}
```
`MecanicoCrearDTO.cs` (mismo namespace):
```csharp
namespace API.Application.DTO.mecanico
{
    public class MecanicoCrearDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Activo { get; set; }
    }
}
```
`MecanicoActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.mecanico
{
    public class MecanicoActualizarDTO
    {
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Activo { get; set; }
    }
}
```

`API.Application.Interface/IMecanicoApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.mecanico;

namespace API.Application.Interface
{
    public interface IMecanicoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(MecanicoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, MecanicoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<MecanicoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<MecanicoDTO>>> ObtenerAsync();
        #endregion
    }
}
```

`API.Application.Main/MecanicoApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.mecanico;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class MecanicoApplication : IMecanicoApplication
    {
        private readonly IMecanicoDomain _domain;
        private readonly IMapper _mapper;

        public MecanicoApplication(IMecanicoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(MecanicoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<Mecanico>(obj));
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, MecanicoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(codigo, _mapper.Map<Mecanico>(obj));
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro actualizado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(string codigo)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.EliminarAsync(codigo);
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro desactivado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<MecanicoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<MecanicoDTO>();
            try
            {
                var mecanico = await _domain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<MecanicoDTO>(mecanico);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MecanicoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<MecanicoDTO>>();
            try
            {
                var mecanicos = await (await _domain.ObtenerTodoAsync()).OrderBy(x => x.Nombre).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<MecanicoDTO>>(mecanicos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
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
(Si el proyecto `API.Application.Main` aún no referencia `Microsoft.EntityFrameworkCore` para `ToListAsync`, ya lo hace `NotaCreditoApplication` — usar el mismo `using`.)

`API.Service.WebApi/Controllers/MecanicoController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.mecanico;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Mecanico")]
    public class MecanicoController : ControllerBase
    {
        private readonly IMecanicoApplication _application;

        public MecanicoController(IMecanicoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Mecanicos", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<MecanicoDTO>>>> ObtenerTodo()
        {
            var mecanicos = await _application.ObtenerAsync();

            if (!mecanicos.Resultado)
                return BadRequest(mecanicos);

            return Ok(mecanicos);
        }

        [RequierePermiso("Mecanicos", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<MecanicoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var mecanico = await _application.ObtenerPorCodigoAsync(codigo);

            if (!mecanico.Resultado)
                return BadRequest(mecanico);

            if (mecanico.Dato == null)
            {
                mecanico.Resultado = false;
                mecanico.Mensaje = "Código de mecánico no encontrado.";
                return NotFound(mecanico);
            }

            return Ok(mecanico);
        }

        [RequierePermiso("Mecanicos", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] MecanicoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("Mecanicos", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] MecanicoActualizarDTO obj)
        {
            var mecanico = await _application.ObtenerPorCodigoAsync(codigo);

            if (mecanico.Dato == null)
            {
                mecanico.Resultado = false;
                mecanico.Mensaje = "Código de mecánico no encontrado.";
                return NotFound(mecanico);
            }

            var actualizar = await _application.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("Mecanicos", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var mecanico = await _application.ObtenerPorCodigoAsync(codigo);

            if (mecanico.Dato == null)
            {
                mecanico.Resultado = false;
                mecanico.Mensaje = "Código de mecánico no encontrado.";
                return NotFound(mecanico);
            }

            var eliminar = await _application.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
```

Mapeo en `PerfilMapeo.cs` — agregar `using API.Application.DTO.mecanico;` arriba y, tras el bloque `// Almacen`:
```csharp
            // Mecanico
            CreateMap<Mecanico, MecanicoDTO>();
            CreateMap<MecanicoCrearDTO, Mecanico>();
            CreateMap<MecanicoActualizarDTO, Mecanico>();
```

DI en `Startup.cs`, tras las líneas de repositorios del taller de la Task 1:
```csharp
            services.AddTransient<IMecanicoDomain, MecanicoDomain>();
            services.AddTransient<IMecanicoApplication, MecanicoApplication>();
```

- [ ] **Step 5: Pruebas del controlador** — crear `API.Service.WebApi.Tests/Controllers/MecanicoControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.mecanico;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MecanicoControllerTests
    {
        private readonly Mock<IMecanicoApplication> _app = new();

        private MecanicoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<MecanicoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<MecanicoDTO>> { Resultado = true, Dato = new[] { new MecanicoDTO() } };
            _app.SetupSequence(a => a.ObtenerAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodo()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodo()).Result).Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("M99")).ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ObtenerPorCodigo("M99");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("Código de mecánico no encontrado.", Assert.IsType<Respuesta<MecanicoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new MecanicoCrearDTO { Codigo = "M01", Nombre = "Juan" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("M99")).ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Actualizar("M99", new MecanicoActualizarDTO { Nombre = "X" });

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<string>(), It.IsAny<MecanicoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_Ok_CuandoExiste()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new MecanicoActualizarDTO { Nombre = "Juan P." };
            _app.Setup(a => a.ObtenerPorCodigoAsync("M01")).ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = new MecanicoDTO { Codigo = "M01" } });
            _app.Setup(a => a.ActualizarAsync("M01", dto)).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Actualizar("M01", dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_NotFoundYOk()
        {
            _app.SetupSequence(a => a.ObtenerPorCodigoAsync("M01"))
                .ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<MecanicoDTO> { Resultado = true, Dato = new MecanicoDTO { Codigo = "M01" } });
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.EliminarAsync("M01")).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Eliminar("M01")).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Eliminar("M01")).Result).Value);
        }
    }
}
```

- [ ] **Step 6: Verificar** — `dotnet test … --filter "FullyQualifiedName~Mecanico"` → todas pasando (11 de dominio + 6 de controlador); `dotnet build API.sln …` → 0 errores.

- [ ] **Step 7: Commit**

```bash
git add API.Application.DTO/mecanico API.Domain.Interface/IMecanicoDomain.cs API.Domain.Core/MecanicoDomain.cs API.Application.Interface/IMecanicoApplication.cs API.Application.Main/MecanicoApplication.cs API.Service.WebApi/Controllers/MecanicoController.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/MecanicoDomainTests.cs API.Service.WebApi.Tests/Controllers/MecanicoControllerTests.cs
git commit -m "feat(taller): catalogo de mecanicos con baja logica

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Catálogo de Motocicletas (dominio, aplicación y controlador)

**Precondición:** Task 3 completa. `grep -n "IMotocicleta" -r API.Domain.Interface API.Application.Interface` sin resultados.

**Files:**
- Create: `API.Application.DTO/motocicleta/MotocicletaDTO.cs`, `MotocicletaCrearDTO.cs`, `MotocicletaActualizarDTO.cs`
- Create: `API.Domain.Interface/IMotocicletaDomain.cs`, `API.Domain.Core/MotocicletaDomain.cs`
- Create: `API.Application.Interface/IMotocicletaApplication.cs`, `API.Application.Main/MotocicletaApplication.cs`
- Create: `API.Service.WebApi/Controllers/MotocicletaController.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/MotocicletaDomainTests.cs`, `API.Service.WebApi.Tests/Controllers/MotocicletaControllerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<Motocicleta, int>`, `<SocioNegocio, string>`, `<OrdenServicio, int>` (Task 1).
- Produces: `IMotocicletaDomain { Task<int> InsertarAsync(Motocicleta); Task<bool> ActualizarAsync(int entry, Motocicleta); Task<bool> EliminarAsync(int entry); Task<Motocicleta?> ObtenerAsync(int entry); Task<IEnumerable<Motocicleta>> BuscarAsync(string? codigoSn, string? placa); Task<IEnumerable<OrdenServicio>> ObtenerHistorialAsync(int entry); }`; `IMotocicletaApplication` (sin historial todavía: se agrega en la Task 10 junto con `OrdenServicioDTO`); rutas `GET api/Motocicleta?codigoSn=&placa=`, `GET api/Motocicleta/{entry:int}`, `POST` (devuelve `Respuesta<int>` con el `Entry`), `PUT {entry:int}`, `DELETE {entry:int}` (módulo `Motocicletas`: Ver / Ver / Agregar / Actualizar / Eliminar). `MotocicletaDTO { Entry, CodigoSn, NombreSn, Placa, Marca, Modelo, Anio, Color, Vin, Comentario, Activo }`.

- [ ] **Step 1: Pruebas de dominio (fallan)** — crear `API.Service.WebApi.Tests/Domain/MotocicletaDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class MotocicletaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Motocicleta, int>> _repoMoto = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<OrdenServicio, int>> _repoOrden = new();
        private readonly List<Motocicleta> _motos = new();
        private readonly List<OrdenServicio> _ordenes = new();
        private readonly MotocicletaDomain _domain;

        public MotocicletaDomainTests()
        {
            _domain = new MotocicletaDomain(_repoMoto.Object, _repoSocio.Object, _repoOrden.Object);
            _repoMoto.Setup(r => r.InsertarAsync(It.IsAny<Motocicleta>())).ReturnsAsync((Motocicleta m) => { m.Entry = 7; return m; });
            _repoMoto.Setup(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Motocicleta>())).ReturnsAsync(true);
            _repoMoto.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _motos.AsAsyncQueryable());
            _repoOrden.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _ordenes.AsAsyncQueryable());
            _repoSocio.Setup(r => r.ObtenerAsync("CL-1")).ReturnsAsync(new SocioNegocio { Codigo = "CL-1", TipoSn = "C", Activo = "S" });
        }

        private static Motocicleta Moto(Action<Motocicleta>? ajustar = null)
        {
            var m = new Motocicleta { CodigoSn = "CL-1", Placa = "m123abc", Marca = "Honda", Modelo = "CB125", Anio = 2022 };
            ajustar?.Invoke(m);
            return m;
        }

        private async Task<string> MensajeAlInsertar(Motocicleta m)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(m));
            return ex.Message;
        }

        [Fact]
        public async Task InsertarAsync_Valida_NormalizaPlacaYDevuelveEntry()
        {
            var entry = await _domain.InsertarAsync(Moto());

            Assert.Equal(7, entry);
            _repoMoto.Verify(r => r.InsertarAsync(It.Is<Motocicleta>(m => m.Placa == "M123ABC" && m.Activo == "S" && m.CodigoSn == "CL-1")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinPlaca_Lanza()
        {
            Assert.Equal("La placa es requerida.", await MensajeAlInsertar(Moto(m => m.Placa = "  ")));
        }

        [Fact]
        public async Task InsertarAsync_SinCliente_Lanza()
        {
            Assert.Equal("El cliente de la motocicleta es requerido.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "")));
        }

        [Fact]
        public async Task InsertarAsync_ClienteInexistente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("CL-9")).ReturnsAsync((SocioNegocio?)null);

            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "CL-9")));
        }

        [Fact]
        public async Task InsertarAsync_SocioQueNoEsCliente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("PR-1")).ReturnsAsync(new SocioNegocio { Codigo = "PR-1", TipoSn = "P", Activo = "S" });

            Assert.Equal("El socio de negocio debe ser un cliente.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "PR-1")));
        }

        [Fact]
        public async Task InsertarAsync_ClienteInactivo_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("CL-2")).ReturnsAsync(new SocioNegocio { Codigo = "CL-2", TipoSn = "C", Activo = "N" });

            Assert.Equal("El socio de negocio está inactivo.", await MensajeAlInsertar(Moto(m => m.CodigoSn = "CL-2")));
        }

        [Fact]
        public async Task InsertarAsync_PlacaDuplicada_Lanza()
        {
            _motos.Add(new Motocicleta { Entry = 1, Placa = "M123ABC", CodigoSn = "CL-1", Activo = "S" });

            Assert.Equal("Ya existe una motocicleta con la placa: M123ABC", await MensajeAlInsertar(Moto()));
            _repoMoto.Verify(r => r.InsertarAsync(It.IsAny<Motocicleta>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_AnioInvalido_Lanza()
        {
            Assert.Equal("El año de la motocicleta no es válido.", await MensajeAlInsertar(Moto(m => m.Anio = 1800)));
        }

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalso()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((Motocicleta?)null);

            Assert.False(await _domain.ActualizarAsync(99, Moto()));
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeClienteConOrdenes_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _repoSocio.Setup(r => r.ObtenerAsync("CL-3")).ReturnsAsync(new SocioNegocio { Codigo = "CL-3", TipoSn = "C", Activo = "S" });
            _ordenes.Add(new OrdenServicio { Entry = 1, MotoEntry = 5 });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, Moto(m => m.CodigoSn = "CL-3")));

            Assert.Equal("La motocicleta tiene órdenes de servicio y no puede cambiar de cliente.", ex.Message);
            _repoMoto.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Motocicleta>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeClienteSinOrdenes_Guarda()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _repoSocio.Setup(r => r.ObtenerAsync("CL-3")).ReturnsAsync(new SocioNegocio { Codigo = "CL-3", TipoSn = "C", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync(5, Moto(m => m.CodigoSn = "CL-3")));
            _repoMoto.Verify(r => r.ActualizarAsync(5, It.Is<Motocicleta>(m => m.CodigoSn == "CL-3")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_MismoClienteConOrdenes_Guarda()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _ordenes.Add(new OrdenServicio { Entry = 1, MotoEntry = 5 });

            Assert.True(await _domain.ActualizarAsync(5, Moto(m => m.Color = "Rojo")));
        }

        [Fact]
        public async Task ActualizarAsync_PlacaDeOtraMoto_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M111AAA", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 6, Placa = "M123ABC", CodigoSn = "CL-1", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, Moto()));
            Assert.Equal("Ya existe una motocicleta con la placa: M123ABC", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_ConsevaSuPropiaPlaca_Guarda()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 5, Placa = "M123ABC", CodigoSn = "CL-1", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync(5, Moto()));
        }

        [Fact]
        public async Task EliminarAsync_HaceBajaLogicaConservandoLosDatos()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new Motocicleta { Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Marca = "Honda", Activo = "S" });

            Assert.True(await _domain.EliminarAsync(5));
            _repoMoto.Verify(r => r.ActualizarAsync(5, It.Is<Motocicleta>(m => m.Activo == "N" && m.Placa == "M123ABC" && m.Marca == "Honda" && m.CodigoSn == "CL-1")), Times.Once);
            _repoMoto.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalso()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((Motocicleta?)null);

            Assert.False(await _domain.EliminarAsync(99));
        }

        [Fact]
        public async Task BuscarAsync_FiltraPorClienteYPlacaParcial()
        {
            _motos.Add(new Motocicleta { Entry = 1, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 2, CodigoSn = "CL-1", Placa = "M999ZZZ", Activo = "S" });
            _motos.Add(new Motocicleta { Entry = 3, CodigoSn = "CL-2", Placa = "M123XYZ", Activo = "S" });

            var porCliente = (await _domain.BuscarAsync("CL-1", null)).Select(m => m.Entry).ToList();
            var porPlaca = (await _domain.BuscarAsync(null, "m123")).Select(m => m.Entry).ToList();
            var ambos = (await _domain.BuscarAsync("CL-1", "m123")).Select(m => m.Entry).ToList();
            var todas = (await _domain.BuscarAsync(null, null)).Select(m => m.Entry).ToList();

            Assert.Equal(new[] { 1, 2 }, porCliente);
            Assert.Equal(new[] { 1, 3 }, porPlaca);
            Assert.Equal(new[] { 1 }, ambos);
            Assert.Equal(new[] { 1, 2, 3 }, todas);
        }

        [Fact]
        public async Task ObtenerHistorialAsync_DevuelveLasOrdenesDeLaMotoDeLaMasRecienteALaMasAntigua()
        {
            _ordenes.Add(new OrdenServicio { Entry = 1, MotoEntry = 5, FechaDoc = new DateTime(2026, 10, 1) });
            _ordenes.Add(new OrdenServicio { Entry = 2, MotoEntry = 9, FechaDoc = new DateTime(2026, 10, 2) });
            _ordenes.Add(new OrdenServicio { Entry = 3, MotoEntry = 5, FechaDoc = new DateTime(2026, 10, 3) });

            var historial = (await _domain.ObtenerHistorialAsync(5)).Select(o => o.Entry).ToList();

            Assert.Equal(new[] { 3, 1 }, historial);
        }
    }
}
```

- [ ] **Step 2: Ejecutarlas y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~MotocicletaDomainTests"` → no compila.

- [ ] **Step 3: Interfaz y dominio**

`API.Domain.Interface/IMotocicletaDomain.cs`:
```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IMotocicletaDomain
    {
        #region async methods
        /// <summary>Registra la motocicleta y devuelve su Entry.</summary>
        Task<int> InsertarAsync(Motocicleta obj);
        Task<bool> ActualizarAsync(int entry, Motocicleta obj);
        /// <summary>Baja lógica: la motocicleta pasa a Activo='N' (nunca se borra, puede tener órdenes).</summary>
        Task<bool> EliminarAsync(int entry);
        Task<Motocicleta?> ObtenerAsync(int entry);
        Task<IEnumerable<Motocicleta>> BuscarAsync(string? codigoSn, string? placa);
        /// <summary>Órdenes de servicio de la motocicleta, de la más reciente a la más antigua.</summary>
        Task<IEnumerable<OrdenServicio>> ObtenerHistorialAsync(int entry);
        #endregion
    }
}
```

`API.Domain.Core/MotocicletaDomain.cs`:
```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class MotocicletaDomain : IMotocicletaDomain
    {
        private const string TipoSocioCliente = "C";

        private readonly IRepositorioGenerico<Motocicleta, int> _repoMoto;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<OrdenServicio, int> _repoOrden;

        public MotocicletaDomain(
            IRepositorioGenerico<Motocicleta, int> repoMoto,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<OrdenServicio, int> repoOrden)
        {
            _repoMoto = repoMoto;
            _repoSocio = repoSocio;
            _repoOrden = repoOrden;
        }

        #region async methods
        public async Task<int> InsertarAsync(Motocicleta obj)
        {
            await NormalizarYValidarAsync(obj, entryActual: null);
            var guardada = await _repoMoto.InsertarAsync(obj);
            return guardada.Entry;
        }

        public async Task<bool> ActualizarAsync(int entry, Motocicleta obj)
        {
            var existente = await _repoMoto.ObtenerAsync(entry);
            if (existente is null)
                return false;

            // Se lee antes de guardar: existente es la entidad rastreada y ActualizarAsync la sobrescribe.
            var clienteAnterior = existente.CodigoSn;

            await NormalizarYValidarAsync(obj, entryActual: entry);

            if (!string.Equals(clienteAnterior, obj.CodigoSn, StringComparison.OrdinalIgnoreCase)
                && await TieneOrdenesAsync(entry))
                throw new Exception("La motocicleta tiene órdenes de servicio y no puede cambiar de cliente.");

            return await _repoMoto.ActualizarAsync(entry, obj);
        }

        public async Task<bool> EliminarAsync(int entry)
        {
            var existente = await _repoMoto.ObtenerAsync(entry);
            if (existente is null)
                return false;

            if (existente.Activo == "N")
                return true;

            return await _repoMoto.ActualizarAsync(entry, new Motocicleta
            {
                CodigoSn = existente.CodigoSn,
                Placa = existente.Placa,
                Marca = existente.Marca,
                Modelo = existente.Modelo,
                Anio = existente.Anio,
                Color = existente.Color,
                Vin = existente.Vin,
                Comentario = existente.Comentario,
                Activo = "N"
            });
        }

        public async Task<Motocicleta?> ObtenerAsync(int entry)
        {
            return await _repoMoto.ObtenerAsync(entry);
        }

        public async Task<IEnumerable<Motocicleta>> BuscarAsync(string? codigoSn, string? placa)
        {
            var consulta = await _repoMoto.ObtenerTodoAsync();

            if (!string.IsNullOrWhiteSpace(codigoSn))
                consulta = consulta.Where(x => x.CodigoSn == codigoSn);

            if (!string.IsNullOrWhiteSpace(placa))
            {
                var fragmento = placa.Trim().ToUpperInvariant();
                consulta = consulta.Where(x => x.Placa.Contains(fragmento));
            }

            return await consulta.OrderBy(x => x.Placa).ToListAsync();
        }

        public async Task<IEnumerable<OrdenServicio>> ObtenerHistorialAsync(int entry)
        {
            var ordenes = await _repoOrden.ObtenerTodoAsync();
            return await ordenes
                .Where(o => o.MotoEntry == entry)
                .OrderByDescending(o => o.FechaDoc).ThenByDescending(o => o.Entry)
                .ToListAsync();
        }
        #endregion

        private async Task NormalizarYValidarAsync(Motocicleta obj, int? entryActual)
        {
            obj.Placa = (obj.Placa ?? string.Empty).Trim().ToUpperInvariant();
            if (obj.Placa.Length == 0)
                throw new Exception("La placa es requerida.");

            obj.CodigoSn = (obj.CodigoSn ?? string.Empty).Trim();
            if (obj.CodigoSn.Length == 0)
                throw new Exception("El cliente de la motocicleta es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");
            if (socio.TipoSn != TipoSocioCliente)
                throw new Exception("El socio de negocio debe ser un cliente.");
            if (socio.Activo == "N")
                throw new Exception("El socio de negocio está inactivo.");

            if (obj.Anio.HasValue && (obj.Anio < 1900 || obj.Anio > 2100))
                throw new Exception("El año de la motocicleta no es válido.");

            if (string.IsNullOrWhiteSpace(obj.Activo))
                obj.Activo = "S";
            else if (obj.Activo != "S" && obj.Activo != "N")
                throw new Exception("El campo Activo debe ser 'S' o 'N'.");

            var motos = await _repoMoto.ObtenerTodoAsync();
            var placa = obj.Placa;
            var excluir = entryActual ?? 0;
            if (await motos.AnyAsync(m => m.Placa == placa && m.Entry != excluir))
                throw new Exception($"Ya existe una motocicleta con la placa: {placa}");
        }

        private async Task<bool> TieneOrdenesAsync(int entry)
        {
            var ordenes = await _repoOrden.ObtenerTodoAsync();
            return await ordenes.AnyAsync(o => o.MotoEntry == entry);
        }
    }
}
```

- [ ] **Step 4: DTOs, aplicación, controlador, mapeo y DI**

`API.Application.DTO/motocicleta/MotocicletaDTO.cs`:
```csharp
namespace API.Application.DTO.motocicleta
{
    public class MotocicletaDTO
    {
        public int Entry { get; set; }
        public string CodigoSn { get; set; } = null!;
        public string? NombreSn { get; set; }
        public string Placa { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public short? Anio { get; set; }
        public string? Color { get; set; }
        public string? Vin { get; set; }
        public string? Comentario { get; set; }
        public string Activo { get; set; } = null!;
    }
}
```
`MotocicletaCrearDTO.cs` (mismo namespace):
```csharp
namespace API.Application.DTO.motocicleta
{
    public class MotocicletaCrearDTO
    {
        public string CodigoSn { get; set; } = null!;
        public string Placa { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public short? Anio { get; set; }
        public string? Color { get; set; }
        public string? Vin { get; set; }
        public string? Comentario { get; set; }
        public string? Activo { get; set; }
    }
}
```
`MotocicletaActualizarDTO.cs`: idéntico a `MotocicletaCrearDTO` con la clase `MotocicletaActualizarDTO`.

`API.Application.Interface/IMotocicletaApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.motocicleta;

namespace API.Application.Interface
{
    public interface IMotocicletaApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(MotocicletaCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, MotocicletaActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry);
        Task<Respuesta<MotocicletaDTO>> ObtenerAsync(int entry);
        Task<Respuesta<IEnumerable<MotocicletaDTO>>> BuscarAsync(string? codigoSn, string? placa);
        #endregion
    }
}
```

`API.Application.Main/MotocicletaApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.motocicleta;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class MotocicletaApplication : IMotocicletaApplication
    {
        private readonly IMotocicletaDomain _domain;
        private readonly IMapper _mapper;

        public MotocicletaApplication(IMotocicletaDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(MotocicletaCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<Motocicleta>(obj));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, MotocicletaActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(entry, _mapper.Map<Motocicleta>(obj));
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro actualizado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int entry)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.EliminarAsync(entry);
                respuesta.Resultado = respuesta.Dato;
                if (respuesta.Dato)
                    respuesta.Mensaje = "Registro desactivado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<MotocicletaDTO>> ObtenerAsync(int entry)
        {
            var respuesta = new Respuesta<MotocicletaDTO>();
            try
            {
                respuesta.Dato = _mapper.Map<MotocicletaDTO>(await _domain.ObtenerAsync(entry));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<MotocicletaDTO>>> BuscarAsync(string? codigoSn, string? placa)
        {
            var respuesta = new Respuesta<IEnumerable<MotocicletaDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<MotocicletaDTO>>(await _domain.BuscarAsync(codigoSn, placa));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
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

`API.Service.WebApi/Controllers/MotocicletaController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.motocicleta;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/Motocicleta")]
    public class MotocicletaController : ControllerBase
    {
        private readonly IMotocicletaApplication _application;

        public MotocicletaController(IMotocicletaApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Motocicletas", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<MotocicletaDTO>>>> Buscar([FromQuery] string? codigoSn, [FromQuery] string? placa)
        {
            var motos = await _application.BuscarAsync(codigoSn, placa);

            if (!motos.Resultado)
                return BadRequest(motos);

            return Ok(motos);
        }

        [RequierePermiso("Motocicletas", Accion.Ver)]
        [HttpGet("{entry:int}")]
        public async Task<ActionResult<Respuesta<MotocicletaDTO>>> Obtener([FromRoute] int entry)
        {
            var moto = await _application.ObtenerAsync(entry);

            if (!moto.Resultado)
                return BadRequest(moto);

            if (moto.Dato == null)
            {
                moto.Resultado = false;
                moto.Mensaje = "La motocicleta no se encontró.";
                return NotFound(moto);
            }

            return Ok(moto);
        }

        [RequierePermiso("Motocicletas", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> Crear([FromBody] MotocicletaCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("Motocicletas", Accion.Actualizar)]
        [HttpPut("{entry:int}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] int entry, [FromBody] MotocicletaActualizarDTO obj)
        {
            var moto = await _application.ObtenerAsync(entry);

            if (moto.Dato == null)
            {
                moto.Resultado = false;
                moto.Mensaje = "La motocicleta no se encontró.";
                return NotFound(moto);
            }

            var actualizar = await _application.ActualizarAsync(entry, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("Motocicletas", Accion.Eliminar)]
        [HttpDelete("{entry:int}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] int entry)
        {
            var moto = await _application.ObtenerAsync(entry);

            if (moto.Dato == null)
            {
                moto.Resultado = false;
                moto.Mensaje = "La motocicleta no se encontró.";
                return NotFound(moto);
            }

            var eliminar = await _application.EliminarAsync(entry);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
```

Mapeo en `PerfilMapeo.cs` — `using API.Application.DTO.motocicleta;` y, tras el bloque `// Mecanico`:
```csharp
            // Motocicleta
            CreateMap<Motocicleta, MotocicletaDTO>()
                .ForMember(d => d.NombreSn, o => o.MapFrom(s => s.CodigoSnNavigation != null ? s.CodigoSnNavigation.Nombre : null));
            CreateMap<MotocicletaCrearDTO, Motocicleta>();
            CreateMap<MotocicletaActualizarDTO, Motocicleta>();
```

DI en `Startup.cs`:
```csharp
            services.AddTransient<IMotocicletaDomain, MotocicletaDomain>();
            services.AddTransient<IMotocicletaApplication, MotocicletaApplication>();
```

- [ ] **Step 5: Pruebas del controlador** — crear `API.Service.WebApi.Tests/Controllers/MotocicletaControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.motocicleta;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class MotocicletaControllerTests
    {
        private readonly Mock<IMotocicletaApplication> _app = new();

        private MotocicletaController Crear() => new(_app.Object);

        [Fact]
        public async Task Buscar_PasaLosFiltrosYDevuelveOk()
        {
            var buena = new Respuesta<IEnumerable<MotocicletaDTO>> { Resultado = true, Dato = new[] { new MotocicletaDTO() } };
            _app.Setup(a => a.BuscarAsync("CL-1", "M12")).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Buscar("CL-1", "M12")).Result).Value);
        }

        [Fact]
        public async Task Buscar_BadRequest_CuandoResultadoEsFalso()
        {
            var mala = new Respuesta<IEnumerable<MotocicletaDTO>> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.BuscarAsync(null, null)).ReturnsAsync(mala);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Buscar(null, null)).Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = null! });

            var notFound = Assert.IsType<NotFoundObjectResult>((await Crear().Obtener(9)).Result);
            Assert.Equal("La motocicleta no se encontró.", Assert.IsType<Respuesta<MotocicletaDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 7 };
            var dto = new MotocicletaCrearDTO { CodigoSn = "CL-1", Placa = "M123ABC" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_YOk()
        {
            var dto = new MotocicletaActualizarDTO { CodigoSn = "CL-1", Placa = "M123ABC" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.ObtenerAsync(5))
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = new MotocicletaDTO { Entry = 5 } });
            _app.Setup(a => a.ActualizarAsync(5, dto)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Actualizar(5, dto)).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Actualizar(5, dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_NotFound_YOk()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.ObtenerAsync(5))
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<MotocicletaDTO> { Resultado = true, Dato = new MotocicletaDTO { Entry = 5 } });
            _app.Setup(a => a.EliminarAsync(5)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Eliminar(5)).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Eliminar(5)).Result).Value);
        }
    }
}
```

- [ ] **Step 6: Verificar** — `dotnet test … --filter "FullyQualifiedName~Motocicleta"` → todas pasando (18 de dominio + 6 de controlador); `dotnet build API.sln …` → 0 errores.

- [ ] **Step 7: Commit**

```bash
git add API.Application.DTO/motocicleta API.Domain.Interface/IMotocicletaDomain.cs API.Domain.Core/MotocicletaDomain.cs API.Application.Interface/IMotocicletaApplication.cs API.Application.Main/MotocicletaApplication.cs API.Service.WebApi/Controllers/MotocicletaController.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/MotocicletaDomainTests.cs API.Service.WebApi.Tests/Controllers/MotocicletaControllerTests.cs
git commit -m "feat(taller): catalogo de motocicletas por cliente con placa unica, baja logica e historial en el dominio

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---
## Fase 3 — Orden de servicio: líneas, documento base y dominio (API)

### Task 5: `OrdenServicioLineaService` (validación, montos del servidor e inventario de las líneas)

**Precondición:** Tasks 1 y 2 completas. `grep -n "IOrdenServicioLineaService" -r API.Domain.Interface` sin resultados.

**Files:**
- Create: `API.Domain.Interface/IOrdenServicioLineaService.cs`, `API.Domain.Core/OrdenServicioLineaService.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/OrdenServicioLineaServiceTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<Articulo, string>`, `<Impuesto, string>`, `<Mecanico, string>`, `IInventarioAsientoService.AsentarAsync` (y `MovimientoRequest`).
- Produces:
  - `Task IOrdenServicioLineaService.PrepararAsync(IList<OrdenServicioDetalle> lineas, int numeroInicial = 1)` — valida cada línea y completa lo que fija el servidor (`CodArticulo` recortado, `Descripcion`, `CodAlmacen` por defecto del artículo, `EsInsumo`, `TipoObjeto="70"`, `PrctjeDesc`, `Impuesto`, `TotalLinea`); `numeroInicial` solo sirve para los mensajes `Línea n`.
  - `Task IOrdenServicioLineaService.AsentarSalidaAsync(int entry, DateTime fecha, IEnumerable<OrdenServicioDetalle> lineas)` — salida de inventario de los insumos con cantidad > 0 (`TipoDoc "70"`, `DocLinea = NoLinea`, `permitirNegativo: false`).
  - `void IOrdenServicioLineaService.RecalcularCabecera(OrdenServicio orden, IEnumerable<OrdenServicioDetalle> lineas)` — `TotalBruto`, `TotalDesc`, `TotalImp`, `TotalDoc`.

- [ ] **Step 1: Pruebas (fallan)** — crear `API.Service.WebApi.Tests/Domain/OrdenServicioLineaServiceTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class OrdenServicioLineaServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<Mecanico, string>> _repoMecanico = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly OrdenServicioLineaService _svc;
        private readonly List<MovimientoRequest> _movimientos = new();

        public OrdenServicioLineaServiceTests()
        {
            _svc = new OrdenServicioLineaService(_repoArticulo.Object, _repoImpuesto.Object, _repoMecanico.Object, _asiento.Object);

            // Valores por defecto válidos: cada prueba rompe solo lo que quiere probar.
            _repoArticulo.Setup(r => r.ObtenerAsync("ACE-1")).ReturnsAsync(new Articulo
            {
                Codigo = "ACE-1", Nombre = "Aceite 20W50", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "S", AlmacenDefecto = "01"
            });
            _repoArticulo.Setup(r => r.ObtenerAsync("MO-1")).ReturnsAsync(new Articulo
            {
                Codigo = "MO-1", Nombre = "Mano de obra", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "N"
            });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA", Tasa = 12m });
            _repoMecanico.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _movimientos.AddRange(ms))
                .Returns(Task.CompletedTask);
        }

        private static OrdenServicioDetalle Insumo(Action<OrdenServicioDetalle>? ajustar = null)
        {
            var l = new OrdenServicioDetalle { CodArticulo = "ACE-1", Cantidad = 2m, Precio = 100m, PrctjeDesc = 10m, CodigoImpuesto = "IVA" };
            ajustar?.Invoke(l);
            return l;
        }

        private static OrdenServicioDetalle Servicio(Action<OrdenServicioDetalle>? ajustar = null)
        {
            var l = new OrdenServicioDetalle { CodArticulo = "MO-1", Cantidad = 1m, Precio = 50m, CodigoImpuesto = "IVA" };
            ajustar?.Invoke(l);
            return l;
        }

        private async Task<string> MensajeAlPreparar(OrdenServicioDetalle linea, int numeroInicial = 1)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea }, numeroInicial));
            return ex.Message;
        }

        // ---------------- PrepararAsync: camino feliz ----------------

        [Fact]
        public async Task Preparar_Insumo_CompletaDatosYCalculaMontosEnElServidor()
        {
            var linea = Insumo(l => { l.Impuesto = 999m; l.TotalLinea = 999m; });   // lo que mande el cliente se ignora

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("S", linea.EsInsumo);
            Assert.Equal("70", linea.TipoObjeto);
            Assert.Equal("01", linea.CodAlmacen);                 // almacén por defecto del artículo
            Assert.Equal("Aceite 20W50", linea.Descripcion);      // descripción por defecto = nombre del artículo
            // 2 x 100 = 200; desc 10% = 20; base 180; IVA 12% = 21.60; total 201.60
            Assert.Equal(21.60m, linea.Impuesto);
            Assert.Equal(201.60m, linea.TotalLinea);
        }

        [Fact]
        public async Task Preparar_Insumo_ConservaAlmacenYDescripcionIndicados()
        {
            var linea = Insumo(l => { l.CodAlmacen = " 02 "; l.Descripcion = "  Aceite sintético "; });

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("02", linea.CodAlmacen);
            Assert.Equal("Aceite sintético", linea.Descripcion);
        }

        [Fact]
        public async Task Preparar_Servicio_NoEsInsumoYNoExigeAlmacen()
        {
            var linea = Servicio();

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("N", linea.EsInsumo);
            Assert.Null(linea.CodAlmacen);
            Assert.Equal(0m, linea.PrctjeDesc);
            Assert.Equal(6.00m, linea.Impuesto);
            Assert.Equal(56.00m, linea.TotalLinea);
        }

        [Fact]
        public async Task Preparar_ServicioConAlmacenPorDefectoEnElArticulo_LoCopia()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("MO-2")).ReturnsAsync(new Articulo
            {
                Codigo = "MO-2", Nombre = "Afinación", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "N", AlmacenDefecto = "01"
            });
            var linea = Servicio(l => l.CodArticulo = "MO-2");

            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { linea });

            Assert.Equal("N", linea.EsInsumo);
            Assert.Equal("01", linea.CodAlmacen);
        }

        [Fact]
        public async Task Preparar_ConMecanicoValido_Pasa()
        {
            await _svc.PrepararAsync(new List<OrdenServicioDetalle> { Servicio(l => l.CodMecanico = "M01") });
        }

        // ---------------- PrepararAsync: validaciones ----------------

        [Fact]
        public async Task Preparar_ArticuloVacio_Lanza()
        {
            Assert.Equal("Línea 1: el artículo es requerido.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "  ")));
        }

        [Fact]
        public async Task Preparar_ArticuloInexistente_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("XX")).ReturnsAsync((Articulo?)null);

            Assert.Equal("Línea 1: el artículo 'XX' no existe.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "XX")));
        }

        [Fact]
        public async Task Preparar_ArticuloInactivo_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("OLD")).ReturnsAsync(new Articulo { Codigo = "OLD", Activo = "N", ArticuloVenta = "S", ArticuloInventario = "S" });

            Assert.Equal("Línea 1: el artículo 'OLD' está inactivo.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "OLD")));
        }

        [Fact]
        public async Task Preparar_ArticuloNoVendible_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("COMP")).ReturnsAsync(new Articulo { Codigo = "COMP", Activo = "S", ArticuloVenta = "N", ArticuloInventario = "S" });

            Assert.Equal("Línea 1: el artículo 'COMP' no está habilitado para la venta.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "COMP")));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Preparar_CantidadNoPositiva_Lanza(double cantidad)
        {
            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlPreparar(Insumo(l => l.Cantidad = (decimal)cantidad)));
        }

        [Fact]
        public async Task Preparar_CantidadNula_Lanza()
        {
            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlPreparar(Insumo(l => l.Cantidad = null)));
        }

        [Fact]
        public async Task Preparar_PrecioNulo_Lanza()
        {
            Assert.Equal("Línea 1: el precio es requerido.", await MensajeAlPreparar(Insumo(l => l.Precio = null)));
        }

        [Fact]
        public async Task Preparar_PrecioNegativo_Lanza()
        {
            Assert.Equal("Línea 1: el precio no puede ser negativo.", await MensajeAlPreparar(Insumo(l => l.Precio = -1m)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public async Task Preparar_DescuentoFueraDeRango_Lanza(double descuento)
        {
            Assert.Equal("Línea 1: el descuento debe estar entre 0 y 100.", await MensajeAlPreparar(Insumo(l => l.PrctjeDesc = (decimal)descuento)));
        }

        [Fact]
        public async Task Preparar_ImpuestoVacio_Lanza()
        {
            Assert.Equal("Línea 1: el impuesto es requerido.", await MensajeAlPreparar(Insumo(l => l.CodigoImpuesto = " ")));
        }

        [Fact]
        public async Task Preparar_ImpuestoInexistente_Lanza()
        {
            _repoImpuesto.Setup(r => r.ObtenerAsync("ZZ")).ReturnsAsync((Impuesto?)null);

            Assert.Equal("Línea 1: el impuesto 'ZZ' no existe.", await MensajeAlPreparar(Insumo(l => l.CodigoImpuesto = "ZZ")));
        }

        [Fact]
        public async Task Preparar_InsumoSinAlmacenNiAlmacenPorDefecto_Lanza()
        {
            _repoArticulo.Setup(r => r.ObtenerAsync("SIN")).ReturnsAsync(new Articulo
            {
                Codigo = "SIN", Nombre = "Sin almacén", Activo = "S", ArticuloVenta = "S", ArticuloInventario = "S", AlmacenDefecto = null
            });

            Assert.Equal("Línea 1: el almacén es requerido para un artículo de inventario.", await MensajeAlPreparar(Insumo(l => l.CodArticulo = "SIN")));
        }

        [Fact]
        public async Task Preparar_DescripcionLarga_Lanza()
        {
            Assert.Equal("Línea 1: la descripción no puede superar 200 caracteres.", await MensajeAlPreparar(Insumo(l => l.Descripcion = new string('x', 201))));
        }

        [Fact]
        public async Task Preparar_MecanicoInexistente_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.Equal("Línea 1: el mecánico 'M99' no existe.", await MensajeAlPreparar(Servicio(l => l.CodMecanico = "M99")));
        }

        [Fact]
        public async Task Preparar_MecanicoInactivo_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M02")).ReturnsAsync(new Mecanico { Codigo = "M02", Nombre = "Pedro", Activo = "N" });

            Assert.Equal("Línea 1: el mecánico 'M02' está inactivo.", await MensajeAlPreparar(Servicio(l => l.CodMecanico = "M02")));
        }

        [Fact]
        public async Task Preparar_NumeroInicial_AjustaElNumeroDeLineaEnLosMensajes()
        {
            var lineas = new List<OrdenServicioDetalle> { Servicio(), Insumo(l => l.Cantidad = 0m) };

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.PrepararAsync(lineas, numeroInicial: 4));

            Assert.Equal("Línea 5: la cantidad debe ser mayor que 0.", ex.Message);
        }

        // ---------------- RecalcularCabecera ----------------

        [Fact]
        public async Task RecalcularCabecera_SumaLasLineas()
        {
            var lineas = new List<OrdenServicioDetalle> { Insumo(), Servicio() };
            await _svc.PrepararAsync(lineas);
            var orden = new OrdenServicio();

            _svc.RecalcularCabecera(orden, lineas);

            // bruto 200 + 50; descuento 20 + 0; impuesto 21.60 + 6.00; total 201.60 + 56.00
            Assert.Equal(250m, orden.TotalBruto);
            Assert.Equal(20m, orden.TotalDesc);
            Assert.Equal(27.60m, orden.TotalImp);
            Assert.Equal(257.60m, orden.TotalDoc);
        }

        [Fact]
        public void RecalcularCabecera_SinLineas_DejaTodoEnCero()
        {
            var orden = new OrdenServicio { TotalBruto = 5m, TotalDesc = 5m, TotalImp = 5m, TotalDoc = 5m };

            _svc.RecalcularCabecera(orden, new List<OrdenServicioDetalle>());

            Assert.Equal((0m, 0m, 0m, 0m), (orden.TotalBruto, orden.TotalDesc, orden.TotalImp, orden.TotalDoc));
        }

        // ---------------- AsentarSalidaAsync ----------------

        [Fact]
        public async Task AsentarSalida_SoloLosInsumosConCantidad_ComoSalidaDeLaOrden()
        {
            var insumo = Insumo(l => { l.Entry = 77; l.NoLinea = 3; l.EsInsumo = "S"; l.CodAlmacen = "01"; });
            var servicio = Servicio(l => { l.Entry = 77; l.NoLinea = 4; l.EsInsumo = "N"; });
            var fecha = new DateTime(2026, 10, 22);

            await _svc.AsentarSalidaAsync(77, fecha, new[] { insumo, servicio });

            var m = Assert.Single(_movimientos);
            Assert.Equal(new MovimientoRequest("70", 77, 3, "ACE-1", "01", -2m, 100m, fecha), m);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), false), Times.Once);
        }

        [Fact]
        public async Task AsentarSalida_SoloServicios_NoLlamaAlInventario()
        {
            await _svc.AsentarSalidaAsync(77, DateTime.Now, new[] { Servicio(l => { l.NoLinea = 1; l.EsInsumo = "N"; }) });

            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }
    }
}
```

- [ ] **Step 2: Ejecutarlas y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~OrdenServicioLineaServiceTests"` → no compila.

- [ ] **Step 3: Interfaz** — `API.Domain.Interface/IOrdenServicioLineaService.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>
    /// Reglas comunes de las líneas de una orden de servicio: validación, montos calculados en el servidor y
    /// salida de inventario de los insumos. Lo usan OrdenServicioDomain (alta con líneas) y
    /// OrdenServicioDetalleDomain (alta/edición/baja de una línea).
    /// </summary>
    public interface IOrdenServicioLineaService
    {
        /// <summary>
        /// Valida las líneas y completa lo que fija el servidor (descripción, almacén por defecto, EsInsumo,
        /// TipoObjeto, Impuesto y TotalLinea). numeroInicial = número de línea de la primera, solo para los
        /// mensajes "Línea n". Lanza Exception con mensaje de usuario.
        /// </summary>
        Task PrepararAsync(IList<OrdenServicioDetalle> lineas, int numeroInicial = 1);

        /// <summary>
        /// Asienta la salida de inventario de los insumos con cantidad &gt; 0 (TipoDoc "70", bloqueo duro de stock
        /// negativo). Los servicios no mueven inventario. Las líneas ya deben tener Entry y NoLinea.
        /// </summary>
        Task AsentarSalidaAsync(int entry, DateTime fecha, IEnumerable<OrdenServicioDetalle> lineas);

        /// <summary>Recalcula TotalBruto, TotalDesc, TotalImp y TotalDoc de la cabecera a partir de las líneas.</summary>
        void RecalcularCabecera(OrdenServicio orden, IEnumerable<OrdenServicioDetalle> lineas);
    }
}
```

- [ ] **Step 4: Implementación** — `API.Domain.Core/OrdenServicioLineaService.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;

namespace API.Domain.Core
{
    public class OrdenServicioLineaService : IOrdenServicioLineaService
    {
        // CHECK constraint de la tabla: TipoObjeto='70'. Se fuerza siempre en el servidor.
        private const string TipoObjetoOrden = "70";
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Mecanico, string> _repoMecanico;
        private readonly IInventarioAsientoService _asiento;

        public OrdenServicioLineaService(
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<Mecanico, string> repoMecanico,
            IInventarioAsientoService asiento)
        {
            _repoArticulo = repoArticulo;
            _repoImpuesto = repoImpuesto;
            _repoMecanico = repoMecanico;
            _asiento = asiento;
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        public async Task PrepararAsync(IList<OrdenServicioDetalle> lineas, int numeroInicial = 1)
        {
            var tasas = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lineas.Count; i++)
            {
                var n = numeroInicial + i;
                var linea = lineas[i];

                var codArticulo = (linea.CodArticulo ?? string.Empty).Trim();
                if (codArticulo.Length == 0)
                    throw new Exception($"Línea {n}: el artículo es requerido.");

                var articulo = await _repoArticulo.ObtenerAsync(codArticulo)
                    ?? throw new Exception($"Línea {n}: el artículo '{codArticulo}' no existe.");
                if (articulo.Activo == "N")
                    throw new Exception($"Línea {n}: el artículo '{codArticulo}' está inactivo.");
                if (articulo.ArticuloVenta != "S")
                    throw new Exception($"Línea {n}: el artículo '{codArticulo}' no está habilitado para la venta.");

                if ((linea.Cantidad ?? 0m) <= 0m)
                    throw new Exception($"Línea {n}: la cantidad debe ser mayor que 0.");

                if (linea.Precio is null)
                    throw new Exception($"Línea {n}: el precio es requerido.");
                if (linea.Precio < 0m)
                    throw new Exception($"Línea {n}: el precio no puede ser negativo.");

                var descuento = linea.PrctjeDesc ?? 0m;
                if (descuento < 0m || descuento > 100m)
                    throw new Exception($"Línea {n}: el descuento debe estar entre 0 y 100.");

                var codImpuesto = (linea.CodigoImpuesto ?? string.Empty).Trim();
                if (codImpuesto.Length == 0)
                    throw new Exception($"Línea {n}: el impuesto es requerido.");
                if (!tasas.TryGetValue(codImpuesto, out var tasa))
                {
                    var impuesto = await _repoImpuesto.ObtenerAsync(codImpuesto)
                        ?? throw new Exception($"Línea {n}: el impuesto '{codImpuesto}' no existe.");
                    tasa = impuesto.Tasa ?? 0m;
                    tasas[codImpuesto] = tasa;
                }

                var descripcion = (linea.Descripcion ?? string.Empty).Trim();
                if (descripcion.Length == 0)
                    descripcion = articulo.Nombre ?? codArticulo;
                if (descripcion.Length > LongitudMaximaDescripcion)
                    throw new Exception($"Línea {n}: la descripción no puede superar {LongitudMaximaDescripcion} caracteres.");

                var esInsumo = articulo.ArticuloInventario == "S";
                var codAlmacen = (linea.CodAlmacen ?? string.Empty).Trim();
                if (codAlmacen.Length == 0)
                    codAlmacen = (articulo.AlmacenDefecto ?? string.Empty).Trim();
                if (esInsumo && codAlmacen.Length == 0)
                    throw new Exception($"Línea {n}: el almacén es requerido para un artículo de inventario.");

                var codMecanico = (linea.CodMecanico ?? string.Empty).Trim();
                if (codMecanico.Length > 0)
                {
                    var mecanico = await _repoMecanico.ObtenerAsync(codMecanico)
                        ?? throw new Exception($"Línea {n}: el mecánico '{codMecanico}' no existe.");
                    if (mecanico.Activo == "N")
                        throw new Exception($"Línea {n}: el mecánico '{codMecanico}' está inactivo.");
                }

                // Todo validado: recién ahora se escriben los datos que fija el servidor.
                linea.CodArticulo = codArticulo;
                linea.CodigoImpuesto = codImpuesto;
                linea.Descripcion = descripcion;
                linea.CodAlmacen = codAlmacen.Length == 0 ? null : codAlmacen;
                linea.CodMecanico = codMecanico.Length == 0 ? null : codMecanico;
                linea.EsInsumo = esInsumo ? "S" : "N";
                linea.TipoObjeto = TipoObjetoOrden;
                linea.PrctjeDesc = descuento;

                var bruto = Redondear(linea.Cantidad!.Value * linea.Precio!.Value);
                var montoDesc = Redondear(bruto * descuento / 100m);
                var subtotal = bruto - montoDesc;
                var montoImpuesto = Redondear(subtotal * tasa / 100m);
                linea.Impuesto = montoImpuesto;
                linea.TotalLinea = subtotal + montoImpuesto;
            }
        }

        public async Task AsentarSalidaAsync(int entry, DateTime fecha, IEnumerable<OrdenServicioDetalle> lineas)
        {
            var movimientos = lineas
                .Where(l => l.EsInsumo == "S" && (l.Cantidad ?? 0m) > 0m)
                .Select(l => new MovimientoRequest(
                    TipoDoc: TipoObjetoOrden,
                    DocEntry: entry,
                    DocLinea: l.NoLinea,
                    CodArticulo: l.CodArticulo!,
                    CodAlmacen: l.CodAlmacen!,
                    Cantidad: -(l.Cantidad!.Value),   // negativo = salida de stock
                    PrecioUnitario: l.Precio ?? 0m,
                    Fecha: fecha))
                .ToList();

            if (movimientos.Count == 0)
                return;

            await _asiento.AsentarAsync(movimientos);   // permitirNegativo: false -> StockInsuficienteException
        }

        public void RecalcularCabecera(OrdenServicio orden, IEnumerable<OrdenServicioDetalle> lineas)
        {
            decimal totalBruto = 0m, totalDesc = 0m, totalImp = 0m, totalDoc = 0m;

            foreach (var l in lineas)
            {
                var bruto = Redondear((l.Cantidad ?? 0m) * (l.Precio ?? 0m));
                var desc = Redondear(bruto * (l.PrctjeDesc ?? 0m) / 100m);
                totalBruto += bruto;
                totalDesc += desc;
                totalImp += l.Impuesto ?? 0m;
                totalDoc += l.TotalLinea ?? 0m;
            }

            orden.TotalBruto = totalBruto;
            orden.TotalDesc = totalDesc;
            orden.TotalImp = totalImp;
            orden.TotalDoc = totalDoc;
        }
    }
}
```

- [ ] **Step 5: DI** en `Startup.cs` (junto al registro de `IMotocicletaDomain`):
```csharp
            services.AddTransient<IOrdenServicioLineaService, OrdenServicioLineaService>();
```

- [ ] **Step 6: Verificar** — `dotnet test … --filter "FullyQualifiedName~OrdenServicioLineaServiceTests"` → todas pasando (27); `dotnet build API.sln …` → 0 errores.

- [ ] **Step 7: Commit**

```bash
git add API.Domain.Interface/IOrdenServicioLineaService.cs API.Domain.Core/OrdenServicioLineaService.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/OrdenServicioLineaServiceTests.cs
git commit -m "feat(taller): servicio de lineas de la orden con validacion, montos del servidor y salida de inventario de los insumos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: La orden de servicio como origen (70 → Factura) en `DocumentoBaseService`

**Precondición:** Task 1 completa. `grep -n "OrdenServicio" API.Domain.Interface/IDocumentoBaseService.cs API.Domain.Core/DocumentoBaseService.cs` sin resultados. Antes de editar `DocumentoBaseService.cs`: `file API.Domain.Core/DocumentoBaseService.cs` para conservar el fin de línea.

**Files:**
- Modify: `API.Domain.Interface/IDocumentoBaseService.cs`, `API.Domain.Core/DocumentoBaseService.cs`, `API.Service.WebApi/Controllers/DocumentoBaseController.cs`
- Test: `API.Service.WebApi.Tests/Domain/DocumentoBaseContratosTests.cs`, `DocumentoBaseServiceTests.cs`, `API.Service.WebApi.Tests/Controllers/DocumentoBaseControllerTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<OrdenServicio, int>` y `<OrdenServicioDetalle, (int Entry, int NoLinea)>` (Task 1).
- Produces: `TipoDocumentoVenta.OrdenServicio = 70` (`Nombre(70) = "Orden de servicio"`, `DestinosDe(70) = [Factura]`); `DocumentoBaseMensajes.OrdenNoFacturable = "La orden de servicio debe estar Terminada o Entregada para facturarse."`; `DocumentoBaseService` con constructor de **10** repositorios (los 8 actuales + `orden`, `ordenDet` al final); `GET api/DocumentoBase/70/{entry}/Abierto?destino=6` con permisos `OrdenesServicio:Ver` + `Facturas:Agregar`.

- [ ] **Step 1: Pruebas de contratos (fallan)** — en `DocumentoBaseContratosTests.cs`:

a) En el `[Theory]` de `EsCombinacionValida_SoloLaCadenaHaciaAdelante`, agregar estas filas `InlineData` junto a las existentes:
```csharp
        [InlineData(70, 6, true)]
        [InlineData(70, 5, false)]
        [InlineData(70, 4, false)]
        [InlineData(70, 3, false)]
        [InlineData(6, 70, false)]
        [InlineData(3, 70, false)]
```
b) En el `[Theory]` de `Nombre_DevuelveElNombreEnEspanol`, agregar `[InlineData(70, "Orden de servicio")]`.
c) Agregar el test:
```csharp
        [Fact]
        public void DestinosDe_OrdenDeServicio_EsSoloFactura()
        {
            Assert.Equal(new[] { 6 }, TipoDocumentoVenta.DestinosDe(TipoDocumentoVenta.OrdenServicio));
            Assert.Equal(70, TipoDocumentoVenta.OrdenServicio);
        }
```

- [ ] **Step 2: Pruebas del servicio (fallan)** — en `DocumentoBaseServiceTests.cs`:

a) Campos (junto a los mocks existentes) y listas:
```csharp
        private readonly Mock<IRepositorioGenerico<OrdenServicio, int>> _ord = new();
        private readonly Mock<IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>> _ordDet = new();
        private readonly List<OrdenServicio> _ordenes = new();
        private readonly List<OrdenServicioDetalle> _ordLineas = new();
```
b) En el constructor, junto a los otros `Setup`, y reemplazando la construcción de `_svc`:
```csharp
            _ord.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _ordenes.FirstOrDefault(x => x.Entry == id)!);
            _ordDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _ordLineas.AsAsyncQueryable());

            _svc = new DocumentoBaseService(_cot.Object, _cotDet.Object, _ped.Object, _pedDet.Object,
                _ent.Object, _entDet.Object, _fac.Object, _facDet.Object, _ord.Object, _ordDet.Object);
```
c) Fábricas (junto a `Fac`/`LineaFac`):
```csharp
        private OrdenServicio Ord(int entry, int numDoc, string sn = "CL-1", string estado = "T", string cancelado = "N")
        {
            var o = new OrdenServicio { Entry = entry, NumDoc = numDoc, CodigoSn = sn, NombreSn = "Cliente", Direccion = "Dir", MonedaDoc = "QTZ",
                Comentario = "nota", EstadoServicio = estado, Cancelado = cancelado };
            _ordenes.Add(o);
            return o;
        }

        private void LineaOrd(int entry, int noLinea, string art, decimal cant, string? alm = "01", string esInsumo = "S") =>
            _ordLineas.Add(new OrdenServicioDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Descripcion = "Desc " + art, Cantidad = cant,
                Precio = 10m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = cant * 10m * 0.12m, CodAlmacen = alm, EsInsumo = esInsumo });
```
d) Pruebas nuevas (al final de la clase, antes del `}` de cierre):
```csharp
        // ---------- Orden de servicio como origen ----------

        [Theory]
        [InlineData("T")]
        [InlineData("E")]
        public async Task Borrador_OrdenTerminadaOEntregada_CopiaLasLineasAbiertas(string estado)
        {
            Ord(1, 5, estado: estado);
            LineaOrd(1, 1, "ACE-1", 2m);
            LineaOrd(1, 2, "MO-1", 1m, alm: null, esInsumo: "N");

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.OrdenServicio, 1, TipoDocumentoVenta.Factura);

            Assert.Equal((70, 1, 5), (b.OrigenTipo, b.OrigenEntry, b.OrigenNumDoc));
            Assert.Equal(("CL-1", "Cliente", "Dir", "QTZ"), (b.Cabecera.CodigoSn, b.Cabecera.NombreSn, b.Cabecera.Direccion, b.Cabecera.MonedaDoc));
            Assert.Null(b.Cabecera.PrctjeDesc);
            Assert.Equal(new[] { "ACE-1", "MO-1" }, b.Lineas.Select(l => l.CodArticulo));
            Assert.All(b.Lineas, l => Assert.Equal(70, l.BaseTipo));
            Assert.Null(b.Lineas[1].CodAlmacen);
        }

        [Theory]
        [InlineData("R")]
        [InlineData("P")]
        public async Task Borrador_OrdenSinTerminar_Lanza(string estado)
        {
            Ord(1, 5, estado: estado);
            LineaOrd(1, 1, "ACE-1", 2m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ObtenerBorradorAsync(TipoDocumentoVenta.OrdenServicio, 1, TipoDocumentoVenta.Factura));
            Assert.Equal("La orden de servicio debe estar Terminada o Entregada para facturarse.", ex.Message);
        }

        [Fact]
        public async Task Borrador_OrdenCancelada_Lanza()
        {
            Ord(1, 5, estado: "T", cancelado: "S");
            LineaOrd(1, 1, "ACE-1", 2m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ObtenerBorradorAsync(TipoDocumentoVenta.OrdenServicio, 1, TipoDocumentoVenta.Factura));
            Assert.Equal("El documento origen está cancelado.", ex.Message);
        }

        [Fact]
        public async Task Borrador_OrdenParcialmenteFacturada_MuestraLoQueFalta()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);
            Fac(40, 2);
            LineaFac(40, 1, "ACE-1", 3m, baseTipo: 70, baseEntry: 1, baseLinea: 1);

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.OrdenServicio, 1, TipoDocumentoVenta.Factura);

            Assert.Equal(1m, b.Lineas.Single().CantidadAbierta);
        }

        [Fact]
        public async Task Borrador_OrdenConFacturaCancelada_LiberaLaCantidad()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);
            Fac(40, 2, cancelado: "S");
            LineaFac(40, 1, "ACE-1", 4m, baseTipo: 70, baseEntry: 1, baseLinea: 1);

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.OrdenServicio, 1, TipoDocumentoVenta.Factura);

            Assert.Equal(4m, b.Lineas.Single().CantidadAbierta);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public async Task Borrador_OrdenAOtroDestino_Lanza(int destino)
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 2m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ObtenerBorradorAsync(TipoDocumentoVenta.OrdenServicio, 1, destino));
            Assert.Equal($"No se puede copiar Orden de servicio a {TipoDocumentoVenta.Nombre(destino)}.", ex.Message);
        }

        [Fact]
        public async Task Borrador_OrdenInexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ObtenerBorradorAsync(TipoDocumentoVenta.OrdenServicio, 99, TipoDocumentoVenta.Factura));
            Assert.Equal(DocumentoBaseMensajes.OrigenNoExiste, ex.Message);
        }

        [Fact]
        public async Task Validar_FacturaDeOrdenTerminada_ParcialPasa()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);

            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(70, 1, 1, "ACE-1", 3m) });
        }

        [Fact]
        public async Task Validar_FacturaDeOrdenEnProceso_Lanza()
        {
            Ord(1, 5, estado: "P");
            LineaOrd(1, 1, "ACE-1", 4m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(70, 1, 1, "ACE-1", 1m) }));
            Assert.Equal("La orden de servicio debe estar Terminada o Entregada para facturarse.", ex.Message);
        }

        [Fact]
        public async Task Validar_FacturaDeOrden_CantidadMayorQueLaAbierta_Lanza()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);
            Fac(40, 2);
            LineaFac(40, 1, "ACE-1", 3m, baseTipo: 70, baseEntry: 1, baseLinea: 1);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(70, 1, 1, "ACE-1", 2m) }));
            Assert.Equal("La cantidad de la línea 1 de Orden de servicio 5 excede la cantidad abierta (1).", ex.Message);
        }

        [Fact]
        public async Task Validar_FacturaDeOrden_OtroCliente_Lanza()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-2", new[] { Req(70, 1, 1, "ACE-1", 1m) }));
            Assert.Equal("El socio del documento debe ser el del documento base Orden de servicio 5 (CL-1).", ex.Message);
        }

        [Fact]
        public async Task OrigenModificable_OrdenConFactura_NombraLaFactura()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);
            Fac(40, 2);
            LineaFac(40, 1, "ACE-1", 1m, baseTipo: 70, baseEntry: 1, baseLinea: 1);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 1, "cancelar el documento"));
            Assert.Equal("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos.", ex.Message);
        }

        [Fact]
        public async Task OrigenModificable_OrdenSinFacturas_NoLanza()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);

            await _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 1, "cancelar el documento");
        }

        [Fact]
        public async Task LineaModificable_OrdenSoloBloqueaLaLineaFacturada()
        {
            Ord(1, 5);
            LineaOrd(1, 1, "ACE-1", 4m);
            LineaOrd(1, 2, "MO-1", 1m, alm: null, esInsumo: "N");
            Fac(40, 2);
            LineaFac(40, 1, "ACE-1", 1m, baseTipo: 70, baseEntry: 1, baseLinea: 1);

            await _svc.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, 1, 2, "modificar");

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, 1, 1, "eliminar"));
            Assert.Equal("No se puede eliminar la línea 1: la usan Factura 2. Cancele primero esos documentos.", ex.Message);
        }
```

- [ ] **Step 3: Pruebas del controlador (fallan)** — en `DocumentoBaseControllerTests.cs` agregar dentro de la clase:

```csharp
        [Fact]
        public async Task Abierto_DesdeOrdenDeServicio_ConVerEnOrdenesYAgregarEnFacturas_DevuelveOk()
        {
            var (c, app) = Crear(Ambos("OrdenesServicio:V", "Facturas:A"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true, Dato = new DocumentoBaseBorradorDTO() };
            app.Setup(a => a.ObtenerBorradorAsync(70, 5, 6)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(70, 5, 6);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_DesdeOrdenDeServicio_SinVerEnOrdenes_Devuelve403()
        {
            var (c, app) = Crear(Ambos("Facturas:A", "Facturas:V"));

            var r = await c.ObtenerAbiertoAsync(70, 5, 6);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_DesdeOrdenDeServicio_SinAgregarEnFacturas_Devuelve403()
        {
            var (c, _) = Crear(Ambos("OrdenesServicio:V", "Facturas:V"));

            var r = await c.ObtenerAbiertoAsync(70, 5, 6);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
        }
```

- [ ] **Step 4: Ejecutar y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~DocumentoBase"` → no compila (`TipoDocumentoVenta.OrdenServicio` y el constructor de 10 repos no existen).

- [ ] **Step 5: `IDocumentoBaseService.cs`** — (a) tras `public const int Factura = 6;` agregar `public const int OrdenServicio = 70;`; (b) en `Nombre`, antes del `_ =>`: `OrdenServicio => "Orden de servicio",`; (c) en `DestinosDe`, antes del `_ =>`: `OrdenServicio => new[] { Factura },`; (d) en `DocumentoBaseMensajes`:

```csharp
    public static class DocumentoBaseMensajes
    {
        public const string OrigenNoExiste = "El documento origen no existe.";
        public const string OrdenNoFacturable = "La orden de servicio debe estar Terminada o Entregada para facturarse.";
    }
```

- [ ] **Step 6: `DocumentoBaseService.cs`** (conservar EOL):

a) Campos tras `_facturaDet`:
```csharp
        private readonly IRepositorioGenerico<OrdenServicio, int> _orden;
        private readonly IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> _ordenDet;
```
b) Constructor: agregar al final de la lista de parámetros `, IRepositorioGenerico<OrdenServicio, int> orden, IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> ordenDet` y, en el cuerpo, `_orden = orden; _ordenDet = ordenDet;`.
c) El record `Cab` gana un último parámetro opcional (la cabecera de la orden no tiene `EstadoDoc`; su estado de servicio decide si se puede facturar):
```csharp
        private sealed record Cab(int Entry, int NumDoc, string? CodigoSn, string? NombreSn, string? Direccion, string? MonedaDoc,
            decimal? PrctjeDesc, decimal? PrctjeImpuesto, string? Comentario, string? Cancelado, string? EstadoDoc, string? EstadoServicio = null)
```
(conservar el cuerpo del record: `Activo` y `Etiqueta`.)
d) Junto a los `Mapear` existentes:
```csharp
        private static Cab Mapear(OrdenServicio h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, null, null, h.Comentario, h.Cancelado, null, h.EstadoServicio);

        private static Lin Mapear(OrdenServicioDetalle d) => new(d.Entry, d.NoLinea, null, null, null, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, null, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);

        /// <summary>Una orden de servicio solo se factura cuando está Terminada o Entregada.</summary>
        private static void ValidarOrdenFacturable(int tipoOrigen, Cab cab)
        {
            if (tipoOrigen == TipoDocumentoVenta.OrdenServicio && cab.EstadoServicio is not ("T" or "E"))
                throw new Exception(DocumentoBaseMensajes.OrdenNoFacturable);
        }
```
e) En `CabeceraAsync`, antes del `default:`:
```csharp
                case TipoDocumentoVenta.OrdenServicio:
                    { var h = await _orden.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
```
f) En `LineasDeAsync`, antes del `default:`:
```csharp
                case TipoDocumentoVenta.OrdenServicio:
                    return (await (await _ordenDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
```
g) En `ObtenerBorradorAsync`, justo después del bloque `if (!cab.Activo) throw new Exception("El documento origen está cancelado.");` agregar `ValidarOrdenFacturable(tipoOrigen, cab);`. En `ValidarLineasAsync`, justo después del bloque `if (!cab.Activo) throw new Exception($"El documento base {etiqueta} está cancelado.");` agregar `ValidarOrdenFacturable(tipoOrigen, cab);`.

- [ ] **Step 7: `DocumentoBaseController.cs`** — en `ModuloDe`, antes del `_ => null`: `TipoDocumentoVenta.OrdenServicio => "OrdenesServicio",`.

- [ ] **Step 8: Verificar** — `dotnet test … --filter "FullyQualifiedName~DocumentoBase"` → todas pasando (las anteriores + las nuevas); `dotnet build API.sln …` → 0 errores. (Cualquier otra clase que construya `DocumentoBaseService` a mano además de `DocumentoBaseServiceTests` falla al compilar: no hay otra, `grep -rn "new DocumentoBaseService(" --include=*.cs .`.)

- [ ] **Step 9: Commit**

```bash
git add API.Domain.Interface/IDocumentoBaseService.cs API.Domain.Core/DocumentoBaseService.cs API.Service.WebApi/Controllers/DocumentoBaseController.cs API.Service.WebApi.Tests/Domain/DocumentoBaseContratosTests.cs API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs API.Service.WebApi.Tests/Controllers/DocumentoBaseControllerTests.cs
git commit -m "feat(taller): la orden de servicio es origen de Copiar a Factura (tipo 70) solo en estado Terminada o Entregada

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `FacturaDomain` no descuenta inventario de las líneas con base en una orden de servicio

**Precondición:** Task 6 completa. `grep -n "BaseTipo != TipoDocumentoVenta.Entrega" API.Domain.Core/FacturaDomain.cs` encuentra exactamente una coincidencia.

**Files:**
- Modify: `API.Domain.Core/FacturaDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs`

**Interfaces:**
- Consumes: `TipoDocumentoVenta.OrdenServicio` (Task 6).
- Produces: en `FacturaDomain.InsertarAsync`, el asiento de inventario cubre las líneas con cantidad > 0 y `BaseTipo` distinto de `Entrega` (5) **y** de `OrdenServicio` (70).

- [ ] **Step 1: Prueba (falla)** — en `FacturaDomainTests.cs`, junto a `InsertarAsync_Mezcla_SoloLaLineaQueNoVieneDeEntregaGeneraMovimiento`, agregar:

```csharp
        [Fact]
        public async Task InsertarAsync_LineaConBaseOrdenDeServicio_NoGeneraMovimiento_PeroSiSeInsertaLinea()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { LineaDe("ACE-1", "01", 2m, 100m, baseTipo: 70, baseEntry: 500, baseLinea: 1) });

            Assert.Empty(_movimientosAsentados);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.BaseTipo == 70)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_MezclaOrdenEntregaYSuelta_SoloLaSueltaGeneraMovimiento()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[]
            {
                LineaDe("ACE-1", "01", 2m, 100m, baseTipo: 70, baseEntry: 500, baseLinea: 1),
                LineaDe("ART1", "01", 10m, 25m, baseTipo: 5, baseEntry: 501, baseLinea: 1),
                LineaDe("ART2", "01", 4m, 30m, baseTipo: null, baseEntry: null, baseLinea: null),
            });

            var m = Assert.Single(_movimientosAsentados);
            Assert.Equal("ART2", m.CodArticulo);
            Assert.Equal(3, m.DocLinea);
            Assert.Equal(-4m, m.Cantidad);
        }
```

- [ ] **Step 2: Ejecutar y ver que falla** — `dotnet test … --filter "FullyQualifiedName~FacturaDomainTests"` → `InsertarAsync_LineaConBaseOrdenDeServicio_…` falla (genera un movimiento).

- [ ] **Step 3: Cambio** — en `FacturaDomain.cs`, reemplazar la línea del filtro:

```csharp
                    .Where(l => (l.Cantidad ?? 0m) > 0m && l.BaseTipo != TipoDocumentoVenta.Entrega)   // BaseTipo 5: esa mercancía ya la movió su Entrega; con base Cotización o Pedido nadie entregó todavía
```
por:
```csharp
                    .Where(l => (l.Cantidad ?? 0m) > 0m
                        && l.BaseTipo != TipoDocumentoVenta.Entrega            // BaseTipo 5: esa mercancía ya la movió su Entrega
                        && l.BaseTipo != TipoDocumentoVenta.OrdenServicio)     // BaseTipo 70: los insumos ya salieron al agregarse a la orden; con base Cotización o Pedido nadie entregó todavía
```

- [ ] **Step 4: Verificar** — `dotnet test … --filter "FullyQualifiedName~FacturaDomainTests"` → todas pasando; `dotnet build API.sln …` → 0 errores.

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/FacturaDomain.cs API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs
git commit -m "feat(taller): la factura no vuelve a descontar inventario de lineas que vienen de una orden de servicio

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---
### Task 8: `OrdenServicioDomain` (alta con líneas, edición de cabecera, estados y cancelación)

**Precondición:** Tasks 1, 2, 5 y 6 completas. `grep -n "IOrdenServicioDomain" -r API.Domain.Interface` sin resultados.

**Files:**
- Create: `API.Domain.Interface/IOrdenServicioDomain.cs`, `API.Domain.Core/OrdenServicioDomain.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/OrdenServicioDomainTests.cs`

**Interfaces:**
- Consumes: `IOrdenServicioLineaService` (Task 5), `IInventarioAsientoService.RevertirAsync` (existente), `IDocumentoBaseService.ValidarOrigenModificableAsync(70, …)` (Task 6), `IEjecutorTransaccion.EjecutarAsync` (`Func<Task<int>>` y `Func<Task<bool>>`), repositorios de Task 1.
- Produces: `EstadoOrdenServicio` (constantes `Recibida="R"`, `EnProceso="P"`, `Terminada="T"`, `Entregada="E"`, `EsValido`, `Orden`, `Nombre`) y `IOrdenServicioDomain { Task<int> InsertarAsync(OrdenServicio obj, IEnumerable<OrdenServicioDetalle> lineas); Task<bool> ActualizarAsync(int id, OrdenServicio obj); Task<bool> CambiarEstadoAsync(int id, string nuevoEstado); Task<bool> CancelarAsync(int id); Task<OrdenServicio?> ObtenerAsync(int id); Task<IQueryable<OrdenServicio>> ObtenerTodoAsync(); Task<IEnumerable<OrdenServicioDetalle>> ObtenerLineasAsync(int id); }`. Los `bool` devuelven `false` si la orden no existe; las reglas de negocio lanzan `Exception` con el mensaje de usuario.

- [ ] **Step 1: Pruebas (fallan)** — crear `API.Service.WebApi.Tests/Domain/OrdenServicioDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class OrdenServicioDomainTests
    {
        private readonly Mock<IRepositorioGenerico<OrdenServicio, int>> _repoOrden = new();
        private readonly Mock<IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<Motocicleta, int>> _repoMoto = new();
        private readonly Mock<IRepositorioGenerico<Mecanico, string>> _repoMecanico = new();
        private readonly Mock<IOrdenServicioLineaService> _lineas = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly List<string> _secuencia = new();
        private readonly OrdenServicioDomain _domain;

        public OrdenServicioDomainTests()
        {
            _domain = new OrdenServicioDomain(_repoOrden.Object, _repoDetalle.Object, _repoNumeracion.Object, _repoSocio.Object,
                _repoMoneda.Object, _repoMoto.Object, _repoMecanico.Object, _lineas.Object, _asiento.Object, _documentoBase.Object, _tx.Object);

            // El doble del ejecutor corre el Func directo (sin transacción ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoOrden.Setup(r => r.InsertarAsync(It.IsAny<OrdenServicio>())).ReturnsAsync((OrdenServicio o) => { o.Entry = 99; return o; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>())).Returns(Task.CompletedTask);

            // Valores por defecto válidos: cada prueba rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Cliente Uno", TipoSn = "C", Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            _repoMoto.Setup(r => r.ObtenerAsync(10)).ReturnsAsync(new Motocicleta { Entry = 10, CodigoSn = "SN1", Placa = "M123ABC", Activo = "S" });
            _repoMecanico.Setup(r => r.ObtenerAsync("M01")).ReturnsAsync(new Mecanico { Codigo = "M01", Nombre = "Juan", Activo = "S" });
        }

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string codigoObj = "70") => new()
        {
            CodigoObj = codigoObj, Serie = 4, NombreSerie = "Primario", SigNumero = sig, FinNumero = fin,
            Bloqueado = bloqueado, Manual = manual, SubTipoDoc = "--", TipoSerie = "D"
        };

        private static OrdenServicio Orden(Action<OrdenServicio>? ajustar = null)
        {
            var o = new OrdenServicio { Serie = 4, CodigoSn = "SN1", MonedaDoc = "QTZ", MotoEntry = 10, CodMecanico = "M01" };
            ajustar?.Invoke(o);
            return o;
        }

        private OrdenServicio Existente(Action<OrdenServicio>? ajustar = null)
        {
            var o = new OrdenServicio
            {
                Entry = 50, NumDoc = 8, Serie = 4, CodigoSn = "SN1", NombreSn = "Cliente Uno", MonedaDoc = "QTZ", MotoEntry = 10,
                EstadoServicio = "R", Cancelado = "N", Comentario = "original"
            };
            ajustar?.Invoke(o);
            _repoOrden.Setup(r => r.ObtenerAsync(50)).ReturnsAsync(o);
            return o;
        }

        private async Task<string> MensajeAlInsertar(OrdenServicio orden, params OrdenServicioDetalle[] lineas)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(orden, lineas));
            return ex.Message;
        }

        // ---------------- InsertarAsync ----------------

        [Fact]
        public async Task InsertarAsync_Valida_NumeraGuardaEncabezadoYLineasYAsienta()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var obj = Orden();
            var l1 = new OrdenServicioDetalle { CodArticulo = "ACE-1" };
            var l2 = new OrdenServicioDetalle { CodArticulo = "MO-1" };

            var entry = await _domain.InsertarAsync(obj, new[] { l1, l2 });

            Assert.Equal(99, entry);
            Assert.Equal("70", obj.TipoObjeto);
            Assert.Equal("N", obj.Cancelado);
            Assert.Equal("R", obj.EstadoServicio);
            Assert.Equal(5, obj.NumDoc);
            Assert.Equal(6, serie.SigNumero);                  // arrancaba en 5; debe haber avanzado
            Assert.Equal("Cliente Uno", obj.NombreSn);         // el servidor completa el nombre del socio
            Assert.NotNull(obj.FechaDoc);
            Assert.Equal(obj.FechaDoc, obj.FechaIngreso);
            Assert.Equal((99, 1), (l1.Entry, l1.NoLinea));
            Assert.Equal((99, 2), (l2.Entry, l2.NoLinea));
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(l1), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(l2), Times.Once);
            _lineas.Verify(l => l.PrepararAsync(It.Is<IList<OrdenServicioDetalle>>(x => x.Count == 2), 1), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(obj, It.IsAny<IEnumerable<OrdenServicioDetalle>>()), Times.Once);
            _lineas.Verify(l => l.AsentarSalidaAsync(99, obj.FechaDoc!.Value, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 2)), Times.Once);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinLineas_EsValido()
        {
            var entry = await _domain.InsertarAsync(Orden(), new List<OrdenServicioDetalle>());

            Assert.Equal(99, entry);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_IgnoraEstadoYCancelacionQueMandeElCliente()
        {
            var obj = Orden(o => { o.EstadoServicio = "E"; o.Cancelado = "S"; o.FechaCancelado = DateTime.Now; o.FechaEntrega = DateTime.Now; o.TipoObjeto = "6"; });

            await _domain.InsertarAsync(obj, new List<OrdenServicioDetalle>());

            Assert.Equal(("R", "N", "70"), (obj.EstadoServicio, obj.Cancelado, obj.TipoObjeto));
            Assert.Null(obj.FechaCancelado);
            Assert.Null(obj.FechaEntrega);
        }

        [Fact]
        public async Task InsertarAsync_ConservaNombreYFechasEnviadas()
        {
            var fecha = new DateTime(2026, 10, 20);
            var obj = Orden(o => { o.NombreSn = "Nombre del formulario"; o.FechaDoc = fecha; o.FechaIngreso = fecha.AddDays(-1); });

            await _domain.InsertarAsync(obj, new List<OrdenServicioDetalle>());

            Assert.Equal("Nombre del formulario", obj.NombreSn);
            Assert.Equal(fecha, obj.FechaDoc);
            Assert.Equal(fecha.AddDays(-1), obj.FechaIngreso);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            Assert.Equal("La serie no existe.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SerieDeOtroDocumento_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(codigoObj: "6"));

            Assert.Equal("La serie no corresponde a órdenes de servicio.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(bloqueado: "S"));

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar órdenes de servicio.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(manual: "S"));

            Assert.Equal("El número de documento es requerido para series manuales.", await MensajeAlInsertar(Orden(o => o.NumDoc = 0)));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualConNumero_ConservaElNumeroYNoAvanzaElConsecutivo()
        {
            var serie = CrearSerie(sig: 5, manual: "S");
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var obj = Orden(o => o.NumDoc = 777);

            await _domain.InsertarAsync(obj, new List<OrdenServicioDetalle>());

            Assert.Equal(777, obj.NumDoc);
            Assert.Equal("S", obj.NumManual);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: 11, fin: 10));

            Assert.Equal("Se agotó la numeración disponible en esta serie.", await MensajeAlInsertar(Orden()));
        }

        [Fact]
        public async Task InsertarAsync_SinSocio_Lanza()
        {
            Assert.Equal("El socio de negocio es requerido.", await MensajeAlInsertar(Orden(o => o.CodigoSn = " ")));
        }

        [Fact]
        public async Task InsertarAsync_SocioInexistente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN9")).ReturnsAsync((SocioNegocio?)null);

            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Orden(o => o.CodigoSn = "SN9")));
        }

        [Fact]
        public async Task InsertarAsync_SocioQueNoEsCliente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("PR1")).ReturnsAsync(new SocioNegocio { Codigo = "PR1", TipoSn = "P", Activo = "S" });

            Assert.Equal("El socio de negocio debe ser un cliente.", await MensajeAlInsertar(Orden(o => o.CodigoSn = "PR1")));
        }

        [Fact]
        public async Task InsertarAsync_SocioInactivo_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN2")).ReturnsAsync(new SocioNegocio { Codigo = "SN2", TipoSn = "C", Activo = "N" });

            Assert.Equal("El socio de negocio está inactivo.", await MensajeAlInsertar(Orden(o => o.CodigoSn = "SN2")));
        }

        [Fact]
        public async Task InsertarAsync_SinMoneda_Lanza()
        {
            Assert.Equal("La moneda es requerida.", await MensajeAlInsertar(Orden(o => o.MonedaDoc = "")));
        }

        [Fact]
        public async Task InsertarAsync_MonedaInexistente_Lanza()
        {
            _repoMoneda.Setup(r => r.ObtenerAsync("XXX")).ReturnsAsync((Monedum?)null);

            Assert.Equal("La moneda no existe.", await MensajeAlInsertar(Orden(o => o.MonedaDoc = "XXX")));
        }

        [Fact]
        public async Task InsertarAsync_SinMoto_Lanza()
        {
            Assert.Equal("La motocicleta es requerida.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 0)));
        }

        [Fact]
        public async Task InsertarAsync_MotoInexistente_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(77)).ReturnsAsync((Motocicleta?)null);

            Assert.Equal("La motocicleta no existe.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 77)));
        }

        [Fact]
        public async Task InsertarAsync_MotoInactiva_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(11)).ReturnsAsync(new Motocicleta { Entry = 11, CodigoSn = "SN1", Placa = "M1", Activo = "N" });

            Assert.Equal("La motocicleta está inactiva.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 11)));
        }

        [Fact]
        public async Task InsertarAsync_MotoDeOtroCliente_Lanza()
        {
            _repoMoto.Setup(r => r.ObtenerAsync(12)).ReturnsAsync(new Motocicleta { Entry = 12, CodigoSn = "SN7", Placa = "M2", Activo = "S" });

            Assert.Equal("La motocicleta no pertenece a este cliente.", await MensajeAlInsertar(Orden(o => o.MotoEntry = 12)));
        }

        [Fact]
        public async Task InsertarAsync_MecanicoInexistente_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M99")).ReturnsAsync((Mecanico?)null);

            Assert.Equal("El mecánico no existe.", await MensajeAlInsertar(Orden(o => o.CodMecanico = "M99")));
        }

        [Fact]
        public async Task InsertarAsync_MecanicoInactivo_Lanza()
        {
            _repoMecanico.Setup(r => r.ObtenerAsync("M02")).ReturnsAsync(new Mecanico { Codigo = "M02", Nombre = "Pedro", Activo = "N" });

            Assert.Equal("El mecánico está inactivo.", await MensajeAlInsertar(Orden(o => o.CodMecanico = "M02")));
        }

        [Fact]
        public async Task InsertarAsync_SinMecanico_EsValido()
        {
            await _domain.InsertarAsync(Orden(o => o.CodMecanico = null), new List<OrdenServicioDetalle>());
        }

        [Fact]
        public async Task InsertarAsync_KilometrajeNegativo_Lanza()
        {
            Assert.Equal("El kilometraje de ingreso no puede ser negativo.", await MensajeAlInsertar(Orden(o => o.KmIngreso = -5)));
        }

        [Fact]
        public async Task InsertarAsync_LineaInvalida_NoNumeraNiGuarda()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Línea 1: la cantidad debe ser mayor que 0."));

            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlInsertar(Orden(), new OrdenServicioDetalle { CodArticulo = "ACE-1" }));

            Assert.Equal(5, serie.SigNumero);   // el consecutivo no avanza ante un error de validación
            _repoOrden.Verify(r => r.InsertarAsync(It.IsAny<OrdenServicio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_StockInsuficiente_PropagaLaExcepcion()
        {
            _lineas.Setup(l => l.AsentarSalidaAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new StockInsuficienteException("ACE-1", "01", 1m, 5m));

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.InsertarAsync(Orden(), new[] { new OrdenServicioDetalle { CodArticulo = "ACE-1" } }));
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalso()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            Assert.False(await _domain.ActualizarAsync(99, Orden()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelada_Lanza()
        {
            Existente(o => o.Cancelado = "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, Orden()));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_Entregada_SoloCambiaElComentario()
        {
            var existente = Existente(o => o.EstadoServicio = "E");

            var ok = await _domain.ActualizarAsync(50, Orden(o => { o.Comentario = "nuevo"; o.Diagnostico = "no debe cambiar"; o.CodigoSn = "OTRO"; }));

            Assert.True(ok);
            Assert.Equal("nuevo", existente.Comentario);
            Assert.Null(existente.Diagnostico);
            Assert.Equal("SN1", existente.CodigoSn);
            _repoSocio.Verify(r => r.ObtenerAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_Edicion_CopiaLosCamposEditables()
        {
            var existente = Existente(o => o.EstadoServicio = "P");
            var fecha = new DateTime(2026, 10, 25);
            var obj = Orden(o =>
            {
                o.Direccion = "Zona 1"; o.CodMecanico = "M01"; o.KmIngreso = 12000; o.FechaPromesa = fecha;
                o.FallaReportada = "No enciende"; o.Diagnostico = "Bujía"; o.Observaciones = "Cliente espera"; o.Comentario = "editado";
            });

            Assert.True(await _domain.ActualizarAsync(50, obj));

            Assert.Equal(("Zona 1", "M01", 12000, fecha), (existente.Direccion, existente.CodMecanico, existente.KmIngreso, existente.FechaPromesa));
            Assert.Equal(("No enciende", "Bujía", "Cliente espera", "editado"),
                (existente.FallaReportada, existente.Diagnostico, existente.Observaciones, existente.Comentario));
            Assert.Equal("P", existente.EstadoServicio);   // el estado no se cambia por aquí
            Assert.Equal(8, existente.NumDoc);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeCliente_ValidaQueNoTengaFacturas()
        {
            Existente();
            _repoSocio.Setup(r => r.ObtenerAsync("SN3")).ReturnsAsync(new SocioNegocio { Codigo = "SN3", Nombre = "Cliente Tres", TipoSn = "C", Activo = "S" });
            _repoMoto.Setup(r => r.ObtenerAsync(13)).ReturnsAsync(new Motocicleta { Entry = 13, CodigoSn = "SN3", Placa = "M3", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync(50, Orden(o => { o.CodigoSn = "SN3"; o.MotoEntry = 13; })));

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cambiar el cliente"), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarDeClienteConFacturas_LanzaYNoCambia()
        {
            var existente = Existente();
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cambiar el cliente"))
                .ThrowsAsync(new Exception("No se puede cambiar el cliente: lo usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, Orden(o => o.CodigoSn = "SN3")));

            Assert.Equal("No se puede cambiar el cliente: lo usan Factura 2. Cancele primero esos documentos.", ex.Message);
            Assert.Equal("SN1", existente.CodigoSn);
        }

        [Fact]
        public async Task ActualizarAsync_MotoDeOtroCliente_Lanza()
        {
            Existente();
            _repoMoto.Setup(r => r.ObtenerAsync(12)).ReturnsAsync(new Motocicleta { Entry = 12, CodigoSn = "SN7", Placa = "M2", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, Orden(o => o.MotoEntry = 12)));
            Assert.Equal("La motocicleta no pertenece a este cliente.", ex.Message);
        }

        // ---------------- CambiarEstadoAsync ----------------

        [Theory]
        [InlineData("R", "P")]
        [InlineData("P", "T")]
        [InlineData("R", "T")]      // se puede saltar hacia adelante
        [InlineData("T", "E")]
        public async Task CambiarEstadoAsync_Avanza(string desde, string hacia)
        {
            var existente = Existente(o => o.EstadoServicio = desde);

            Assert.True(await _domain.CambiarEstadoAsync(50, hacia));

            Assert.Equal(hacia, existente.EstadoServicio);
        }

        [Fact]
        public async Task CambiarEstadoAsync_AEntregada_RegistraLaFechaDeEntrega()
        {
            var existente = Existente(o => o.EstadoServicio = "T");

            await _domain.CambiarEstadoAsync(50, "E");

            Assert.NotNull(existente.FechaEntrega);
        }

        [Fact]
        public async Task CambiarEstadoAsync_ATerminada_NoRegistraFechaDeEntrega()
        {
            var existente = Existente(o => o.EstadoServicio = "P");

            await _domain.CambiarEstadoAsync(50, "T");

            Assert.Null(existente.FechaEntrega);
        }

        [Theory]
        [InlineData("T", "P", "No se puede pasar de Terminada a En proceso: la orden solo avanza.")]
        [InlineData("E", "T", "No se puede pasar de Entregada a Terminada: la orden solo avanza.")]
        [InlineData("P", "R", "No se puede pasar de En proceso a Recibida: la orden solo avanza.")]
        public async Task CambiarEstadoAsync_Retroceder_Lanza(string desde, string hacia, string mensaje)
        {
            Existente(o => o.EstadoServicio = desde);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, hacia));
            Assert.Equal(mensaje, ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_MismoEstado_Lanza()
        {
            Existente(o => o.EstadoServicio = "P");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, "P"));
            Assert.Equal("La orden ya está en estado En proceso.", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_EstadoInvalido_Lanza()
        {
            Existente();

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, "X"));
            Assert.Equal("El estado de la orden no es válido.", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_Cancelada_Lanza()
        {
            Existente(o => o.Cancelado = "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CambiarEstadoAsync(50, "P"));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }

        [Fact]
        public async Task CambiarEstadoAsync_Inexistente_DevuelveFalso()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            Assert.False(await _domain.CambiarEstadoAsync(99, "P"));
        }

        // ---------------- CancelarAsync ----------------

        [Fact]
        public async Task CancelarAsync_ValidaDerivadosLuegoRevierteElInventarioYCancela()
        {
            var existente = Existente(o => o.EstadoServicio = "T");
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cancelar el documento"))
                .Callback(() => _secuencia.Add("validar")).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.RevertirAsync("70", 50)).Callback(() => _secuencia.Add("revertir")).Returns(Task.CompletedTask);

            Assert.True(await _domain.CancelarAsync(50));

            Assert.Equal(new[] { "validar", "revertir" }, _secuencia);
            Assert.Equal("S", existente.Cancelado);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("original", existente.Comentario);
        }

        [Fact]
        public async Task CancelarAsync_ConFacturasActivas_LanzaYNoRevierte()
        {
            var existente = Existente();
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, "cancelar el documento"))
                .ThrowsAsync(new Exception("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(50));

            Assert.Equal("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            Assert.Equal("N", existente.Cancelado);
        }

        [Fact]
        public async Task CancelarAsync_Entregada_Lanza()
        {
            Existente(o => o.EstadoServicio = "E");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(50));
            Assert.Equal("Una orden entregada no se puede cancelar.", ex.Message);
        }

        [Fact]
        public async Task CancelarAsync_YaCancelada_Lanza()
        {
            Existente(o => o.Cancelado = "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(50));
            Assert.Equal("La orden de servicio ya está cancelada.", ex.Message);
        }

        [Fact]
        public async Task CancelarAsync_Inexistente_DevuelveFalso()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            Assert.False(await _domain.CancelarAsync(99));
        }

        // ---------------- Consultas ----------------

        [Fact]
        public async Task ObtenerLineasAsync_DevuelveLasLineasDeLaOrdenOrdenadasPorNumero()
        {
            var todas = new List<OrdenServicioDetalle>
            {
                new() { Entry = 50, NoLinea = 2 },
                new() { Entry = 51, NoLinea = 1 },
                new() { Entry = 50, NoLinea = 1 },
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(todas.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerLineasAsync(50)).Select(l => l.NoLinea).ToList();

            Assert.Equal(new[] { 1, 2 }, lineas);
        }
    }
}
```

- [ ] **Step 2: Ejecutarlas y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~OrdenServicioDomainTests"` → no compila.

- [ ] **Step 3: Interfaz y estados** — `API.Domain.Interface/IOrdenServicioDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Estados de una orden de servicio (columna EstadoServicio). Solo avanzan: R &lt; P &lt; T &lt; E.</summary>
    public static class EstadoOrdenServicio
    {
        public const string Recibida = "R";
        public const string EnProceso = "P";
        public const string Terminada = "T";
        public const string Entregada = "E";

        public static bool EsValido(string? estado) => estado is Recibida or EnProceso or Terminada or Entregada;

        public static int Orden(string? estado) => estado switch
        {
            Recibida => 0,
            EnProceso => 1,
            Terminada => 2,
            Entregada => 3,
            _ => -1
        };

        public static string Nombre(string? estado) => estado switch
        {
            Recibida => "Recibida",
            EnProceso => "En proceso",
            Terminada => "Terminada",
            Entregada => "Entregada",
            _ => estado ?? string.Empty
        };
    }

    public interface IOrdenServicioDomain
    {
        #region async methods
        /// <summary>Registra la orden con sus líneas en una transacción: numera, guarda y asienta la salida de los insumos. Devuelve el Entry.</summary>
        Task<int> InsertarAsync(OrdenServicio obj, IEnumerable<OrdenServicioDetalle> lineas);
        /// <summary>Edita la cabecera (en una orden Entregada solo el comentario). Devuelve false si la orden no existe.</summary>
        Task<bool> ActualizarAsync(int id, OrdenServicio obj);
        /// <summary>Avanza el estado (R, P, T, E); nunca retrocede. Devuelve false si la orden no existe.</summary>
        Task<bool> CambiarEstadoAsync(int id, string nuevoEstado);
        /// <summary>Cancela la orden y devuelve al inventario todos sus insumos. Devuelve false si la orden no existe.</summary>
        Task<bool> CancelarAsync(int id);
        Task<OrdenServicio?> ObtenerAsync(int id);
        Task<IQueryable<OrdenServicio>> ObtenerTodoAsync();
        Task<IEnumerable<OrdenServicioDetalle>> ObtenerLineasAsync(int id);
        #endregion
    }
}
```

- [ ] **Step 4: Dominio** — `API.Domain.Core/OrdenServicioDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class OrdenServicioDomain : IOrdenServicioDomain
    {
        // CHECK constraint de la tabla: TipoObjeto='70'. Se fuerza siempre en el servidor.
        private const string TipoObjetoOrden = "70";
        private const string TipoSocioCliente = "C";
        private const string MsgCancelada = "La orden de servicio está cancelada y no se puede modificar.";

        private readonly IRepositorioGenerico<OrdenServicio, int> _repoOrden;
        private readonly IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<Motocicleta, int> _repoMoto;
        private readonly IRepositorioGenerico<Mecanico, string> _repoMecanico;
        private readonly IOrdenServicioLineaService _lineas;
        private readonly IInventarioAsientoService _asiento;
        private readonly IDocumentoBaseService _documentoBase;
        private readonly IEjecutorTransaccion _tx;

        public OrdenServicioDomain(
            IRepositorioGenerico<OrdenServicio, int> repoOrden,
            IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Monedum, string> repoMoneda,
            IRepositorioGenerico<Motocicleta, int> repoMoto,
            IRepositorioGenerico<Mecanico, string> repoMecanico,
            IOrdenServicioLineaService lineas,
            IInventarioAsientoService asiento,
            IDocumentoBaseService documentoBase,
            IEjecutorTransaccion tx)
        {
            _repoOrden = repoOrden;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoMoneda = repoMoneda;
            _repoMoto = repoMoto;
            _repoMecanico = repoMecanico;
            _lineas = lineas;
            _asiento = asiento;
            _documentoBase = documentoBase;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(OrdenServicio obj, IEnumerable<OrdenServicioDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<OrdenServicioDetalle>();

            obj.TipoObjeto = TipoObjetoOrden;
            obj.Cancelado = "N";                  // un documento recién registrado nunca nace cancelado
            obj.FechaCancelado = null;
            obj.FechaEntrega = null;
            obj.EstadoServicio = EstadoOrdenServicio.Recibida;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaIngreso ??= obj.FechaDoc;

            var serie = await ValidarSerieAsync(obj);
            await ValidarClienteYMonedaAsync(obj);
            await ValidarMotoYMecanicoAsync(obj);
            await _lineas.PrepararAsync(lineasList);   // antes de numerar: un error de línea no debe avanzar el consecutivo
            _lineas.RecalcularCabecera(obj, lineasList);

            AsignarNumero(obj, serie);

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoOrden.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                await _lineas.AsentarSalidaAsync(obj.Entry, obj.FechaDoc ?? DateTime.Now, lineasList);

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + inventario) + Commit
        }

        public async Task<bool> ActualizarAsync(int id, OrdenServicio obj)
        {
            var existente = await _repoOrden.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception(MsgCancelada);

            // Orden entregada: solo el comentario (replace-semantics: enviar vacío lo borra).
            if (existente.EstadoServicio == EstadoOrdenServicio.Entregada)
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await Task.CompletedTask;
                    existente.Comentario = obj.Comentario;
                    return true;
                });
            }

            // Una orden con facturas activas no cambia de cliente (primero se cancelan esas facturas).
            if (!string.Equals(existente.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, id, "cambiar el cliente");

            await ValidarClienteYMonedaAsync(obj);
            await ValidarMotoYMecanicoAsync(obj);

            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.CodigoSn = obj.CodigoSn;
                existente.NombreSn = obj.NombreSn;
                existente.Direccion = obj.Direccion;
                existente.MonedaDoc = obj.MonedaDoc;
                existente.MotoEntry = obj.MotoEntry;
                existente.CodMecanico = obj.CodMecanico;
                existente.KmIngreso = obj.KmIngreso;
                existente.FechaDoc = obj.FechaDoc ?? existente.FechaDoc;
                existente.FechaIngreso = obj.FechaIngreso;
                existente.FechaPromesa = obj.FechaPromesa;
                existente.FallaReportada = obj.FallaReportada;
                existente.Diagnostico = obj.Diagnostico;
                existente.Observaciones = obj.Observaciones;
                existente.Comentario = obj.Comentario;
                return true;
            });
        }

        public async Task<bool> CambiarEstadoAsync(int id, string nuevoEstado)
        {
            if (!EstadoOrdenServicio.EsValido(nuevoEstado))
                throw new Exception("El estado de la orden no es válido.");

            var existente = await _repoOrden.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception(MsgCancelada);

            var actual = existente.EstadoServicio;
            if (nuevoEstado == actual)
                throw new Exception($"La orden ya está en estado {EstadoOrdenServicio.Nombre(actual)}.");
            if (EstadoOrdenServicio.Orden(nuevoEstado) < EstadoOrdenServicio.Orden(actual))
                throw new Exception($"No se puede pasar de {EstadoOrdenServicio.Nombre(actual)} a {EstadoOrdenServicio.Nombre(nuevoEstado)}: la orden solo avanza.");

            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.EstadoServicio = nuevoEstado;
                if (nuevoEstado == EstadoOrdenServicio.Entregada)
                    existente.FechaEntrega = DateTime.Now;
                return true;
            });
        }

        public async Task<bool> CancelarAsync(int id)
        {
            var existente = await _repoOrden.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("La orden de servicio ya está cancelada.");
            if (existente.EstadoServicio == EstadoOrdenServicio.Entregada)
                throw new Exception("Una orden entregada no se puede cancelar.");

            // Una orden que ya se facturó (total o parcialmente) no se cancela: primero se cancela la factura.
            await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.OrdenServicio, id, "cancelar el documento");

            return await _tx.EjecutarAsync(async () =>
            {
                await _asiento.RevertirAsync(TipoObjetoOrden, id);   // devuelve al inventario todos los insumos
                existente.Cancelado = "S";
                existente.FechaCancelado = DateTime.Now;
                return true;
            });
        }

        public async Task<OrdenServicio?> ObtenerAsync(int id)
        {
            return await _repoOrden.ObtenerAsync(id);
        }

        public async Task<IQueryable<OrdenServicio>> ObtenerTodoAsync()
        {
            return await _repoOrden.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<OrdenServicioDetalle>> ObtenerLineasAsync(int id)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }
        #endregion

        #region validaciones
        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(OrdenServicio obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoOrden)
                throw new Exception("La serie no corresponde a órdenes de servicio.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar órdenes de servicio.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeración automática se asigna al final (después de validar todo) para no avanzar el
        // consecutivo de una serie rastreada si el documento resulta inválido.
        private static void AsignarNumero(OrdenServicio obj, NumeracionDocumentoDet serie)
        {
            if (serie.Manual == "S")
            {
                obj.NumManual = "S";
                return;
            }

            if (serie.SigNumero == null)
                throw new Exception("La serie no tiene configurado el número siguiente.");
            if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                throw new Exception("Se agotó la numeración disponible en esta serie.");

            obj.NumManual = "N";
            obj.NumDoc = serie.SigNumero.Value;
            serie.SigNumero = serie.SigNumero.Value + 1; // rastreada por el mismo contexto; se persiste en el Save de la transacción
        }

        private async Task ValidarClienteYMonedaAsync(OrdenServicio obj)
        {
            if (string.IsNullOrWhiteSpace(obj.CodigoSn))
                throw new Exception("El socio de negocio es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");

            if (socio.TipoSn != TipoSocioCliente)
                throw new Exception("El socio de negocio debe ser un cliente.");

            if (socio.Activo == "N")
                throw new Exception("El socio de negocio está inactivo.");

            if (string.IsNullOrWhiteSpace(obj.NombreSn))
                obj.NombreSn = socio.Nombre;

            if (string.IsNullOrWhiteSpace(obj.MonedaDoc))
                throw new Exception("La moneda es requerida.");

            if (await _repoMoneda.ObtenerAsync(obj.MonedaDoc) is null)
                throw new Exception("La moneda no existe.");
        }

        private async Task ValidarMotoYMecanicoAsync(OrdenServicio obj)
        {
            if (obj.MotoEntry <= 0)
                throw new Exception("La motocicleta es requerida.");

            var moto = await _repoMoto.ObtenerAsync(obj.MotoEntry)
                ?? throw new Exception("La motocicleta no existe.");
            if (moto.Activo == "N")
                throw new Exception("La motocicleta está inactiva.");
            if (!string.Equals(moto.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                throw new Exception("La motocicleta no pertenece a este cliente.");

            if (!string.IsNullOrWhiteSpace(obj.CodMecanico))
            {
                var mecanico = await _repoMecanico.ObtenerAsync(obj.CodMecanico)
                    ?? throw new Exception("El mecánico no existe.");
                if (mecanico.Activo == "N")
                    throw new Exception("El mecánico está inactivo.");
            }

            if (obj.KmIngreso < 0)
                throw new Exception("El kilometraje de ingreso no puede ser negativo.");
        }
        #endregion
    }
}
```

- [ ] **Step 5: DI** en `Startup.cs` (junto a `IOrdenServicioLineaService`):
```csharp
            services.AddTransient<IOrdenServicioDomain, OrdenServicioDomain>();
```

- [ ] **Step 6: Verificar** — `dotnet test … --filter "FullyQualifiedName~OrdenServicioDomainTests"` → todas pasando (52); `dotnet build API.sln …` → 0 errores.

- [ ] **Step 7: Commit**

```bash
git add API.Domain.Interface/IOrdenServicioDomain.cs API.Domain.Core/OrdenServicioDomain.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/OrdenServicioDomainTests.cs
git commit -m "feat(taller): dominio de la orden de servicio con alta e inventario, edicion de cabecera, estados y cancelacion

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `OrdenServicioDetalleDomain` (agregar, cambiar y quitar líneas con su inventario)

**Precondición:** Task 8 completa. `grep -n "IOrdenServicioDetalleDomain" -r API.Domain.Interface` sin resultados.

**Files:**
- Create: `API.Domain.Interface/IOrdenServicioDetalleDomain.cs`, `API.Domain.Core/OrdenServicioDetalleDomain.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/OrdenServicioDetalleDomainTests.cs`

**Interfaces:**
- Consumes: `IOrdenServicioLineaService` (Task 5), `IInventarioAsientoService.RevertirLineaAsync` (Task 2), `IDocumentoBaseService.ValidarLineaModificableAsync(70, …)` (Task 6), `EstadoOrdenServicio` (Task 8).
- Produces: `IOrdenServicioDetalleDomain { Task<int> InsertarAsync(OrdenServicioDetalle obj); Task<bool> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalle obj); Task<bool> EliminarAsync(int entry, int noLinea); }` — `InsertarAsync` devuelve el `NoLinea` asignado y requiere `obj.Entry` (la orden); `ActualizarAsync`/`EliminarAsync` devuelven `false` si la línea no existe. Todas validan que la orden exista, no esté cancelada ni entregada, mueven el inventario y recalculan los totales de la cabecera dentro de la misma transacción.

- [ ] **Step 1: Pruebas (fallan)** — crear `API.Service.WebApi.Tests/Domain/OrdenServicioDetalleDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class OrdenServicioDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<OrdenServicio, int>> _repoOrden = new();
        private readonly Mock<IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IOrdenServicioLineaService> _lineas = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly List<OrdenServicioDetalle> _guardadas = new();
        private readonly List<string> _secuencia = new();
        private readonly OrdenServicioDetalleDomain _domain;

        public OrdenServicioDetalleDomainTests()
        {
            _domain = new OrdenServicioDetalleDomain(_repoOrden.Object, _repoDetalle.Object, _lineas.Object, _asiento.Object, _documentoBase.Object, _tx.Object);

            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _guardadas.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>())).Returns(Task.CompletedTask);
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoDetalle.Setup(r => r.ObtenerAsync(It.IsAny<(int Entry, int NoLinea)>()))
                .ReturnsAsync(((int Entry, int NoLinea) k) => _guardadas.FirstOrDefault(l => l.Entry == k.Entry && l.NoLinea == k.NoLinea));
        }

        private OrdenServicio Orden(string estado = "R", string cancelado = "N")
        {
            var o = new OrdenServicio { Entry = 50, EstadoServicio = estado, Cancelado = cancelado };
            _repoOrden.Setup(r => r.ObtenerAsync(50)).ReturnsAsync(o);
            return o;
        }

        private OrdenServicioDetalle Guardada(int noLinea, string art = "ACE-1", decimal cant = 2m, string esInsumo = "S", string? alm = "01")
        {
            var l = new OrdenServicioDetalle
            {
                Entry = 50, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 100m, EsInsumo = esInsumo, CodAlmacen = alm, CodigoImpuesto = "IVA"
            };
            _guardadas.Add(l);
            return l;
        }

        private static OrdenServicioDetalle Nueva(Action<OrdenServicioDetalle>? ajustar = null)
        {
            var l = new OrdenServicioDetalle { Entry = 50, CodArticulo = "ACE-1", Cantidad = 1m, Precio = 100m, CodigoImpuesto = "IVA" };
            ajustar?.Invoke(l);
            return l;
        }

        // ---------------- InsertarAsync ----------------

        [Fact]
        public async Task InsertarAsync_PrimeraLinea_AsignaNumero1_AsientaYRecalculaConTodasLasLineas()
        {
            var orden = Orden();
            var nueva = Nueva();

            var noLinea = await _domain.InsertarAsync(nueva);

            Assert.Equal(1, noLinea);
            Assert.Equal(1, nueva.NoLinea);
            _lineas.Verify(l => l.PrepararAsync(It.Is<IList<OrdenServicioDetalle>>(x => x.Count == 1 && x[0] == nueva), 1), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(nueva), Times.Once);
            _lineas.Verify(l => l.AsentarSalidaAsync(50, It.IsAny<DateTime>(), It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Single() == nueva)), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 1)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_ConLineasPrevias_UsaElSiguienteNumeroAunqueHayaHuecos()
        {
            var orden = Orden();
            Guardada(1);
            Guardada(3);
            var nueva = Nueva();

            var noLinea = await _domain.InsertarAsync(nueva);

            Assert.Equal(4, noLinea);
            _lineas.Verify(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), 4), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 3)), Times.Once);
        }

        [Theory]
        [InlineData("R")]
        [InlineData("P")]
        [InlineData("T")]
        public async Task InsertarAsync_EnEstadosEditables_Permite(string estado)
        {
            Orden(estado);

            Assert.Equal(1, await _domain.InsertarAsync(Nueva()));
        }

        [Fact]
        public async Task InsertarAsync_OrdenInexistente_Lanza()
        {
            _repoOrden.Setup(r => r.ObtenerAsync(77)).ReturnsAsync((OrdenServicio?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva(l => l.Entry = 77)));
            Assert.Equal("La orden de servicio no existe.", ex.Message);
        }

        [Fact]
        public async Task InsertarAsync_OrdenCancelada_Lanza()
        {
            Orden(cancelado: "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva()));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }

        [Fact]
        public async Task InsertarAsync_OrdenEntregada_Lanza()
        {
            Orden("E");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva()));
            Assert.Equal("La orden de servicio ya fue entregada: solo se puede modificar el comentario.", ex.Message);
        }

        [Fact]
        public async Task InsertarAsync_LineaInvalida_NoEntraALaTransaccion()
        {
            Orden();
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Línea 1: la cantidad debe ser mayor que 0."));

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nueva()));

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.IsAny<OrdenServicioDetalle>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_StockInsuficiente_Propaga()
        {
            Orden();
            _lineas.Setup(l => l.AsentarSalidaAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new StockInsuficienteException("ACE-1", "01", 0m, 1m));

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.InsertarAsync(Nueva()));
        }

        // ---------------- ActualizarAsync ----------------

        [Fact]
        public async Task ActualizarAsync_RevierteLaLineaLuegoAsientaLaNuevaYRecalcula()
        {
            var orden = Orden();
            var existente = Guardada(1, cant: 2m);
            var otra = Guardada(2, art: "MO-1", esInsumo: "N", alm: null);
            _asiento.Setup(a => a.RevertirLineaAsync("70", 50, 1)).Callback(() => _secuencia.Add("revertir")).Returns(Task.CompletedTask);
            _lineas.Setup(l => l.AsentarSalidaAsync(50, It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .Callback(() => _secuencia.Add("asentar")).Returns(Task.CompletedTask);
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .Callback<IList<OrdenServicioDetalle>, int>((lista, _) =>
                {
                    lista[0].EsInsumo = "S";
                    lista[0].CodAlmacen = "01";
                    lista[0].Impuesto = 36m;
                    lista[0].TotalLinea = 336m;
                    lista[0].TipoObjeto = "70";
                }).Returns(Task.CompletedTask);

            var ok = await _domain.ActualizarAsync(50, 1, Nueva(l => { l.Cantidad = 3m; l.Precio = 100m; l.Descripcion = "Aceite"; }));

            Assert.True(ok);
            Assert.Equal(new[] { "revertir", "asentar" }, _secuencia);
            Assert.Equal((3m, 100m, "Aceite", 36m, 336m, "S", "01"),
                (existente.Cantidad, existente.Precio, existente.Descripcion, existente.Impuesto, existente.TotalLinea, existente.EsInsumo, existente.CodAlmacen));
            _lineas.Verify(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), 1), Times.Once);
            _lineas.Verify(l => l.AsentarSalidaAsync(50, It.IsAny<DateTime>(), It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Single() == existente)), Times.Once);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Count() == 2 && x.Contains(existente) && x.Contains(otra))), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_ValidaQueLaLineaNoEsteFacturada()
        {
            Orden();
            Guardada(1);
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, 1, "modificar"))
                .ThrowsAsync(new Exception("No se puede modificar la línea 1: la usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, 1, Nueva()));

            Assert.Equal("No se puede modificar la línea 1: la usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirLineaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_LineaInexistente_DevuelveFalso()
        {
            Orden();

            Assert.False(await _domain.ActualizarAsync(50, 9, Nueva()));
        }

        [Fact]
        public async Task ActualizarAsync_OrdenEntregada_Lanza()
        {
            Orden("E");
            Guardada(1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, 1, Nueva()));
            Assert.Equal("La orden de servicio ya fue entregada: solo se puede modificar el comentario.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_LineaInvalida_NoRevierteElInventario()
        {
            Orden();
            Guardada(1);
            _lineas.Setup(l => l.PrepararAsync(It.IsAny<IList<OrdenServicioDetalle>>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Línea 1: el artículo 'XX' no existe."));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(50, 1, Nueva(l => l.CodArticulo = "XX")));

            _asiento.Verify(a => a.RevertirLineaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_StockInsuficienteAlReasentar_Propaga()
        {
            Orden();
            Guardada(1);
            _lineas.Setup(l => l.AsentarSalidaAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new StockInsuficienteException("ACE-1", "01", 1m, 9m));

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.ActualizarAsync(50, 1, Nueva(l => l.Cantidad = 9m)));
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task EliminarAsync_RevierteLaLineaLaBorraYRecalculaConLasRestantes()
        {
            var orden = Orden();
            Guardada(1);
            var restante = Guardada(2, art: "MO-1", esInsumo: "N", alm: null);
            _asiento.Setup(a => a.RevertirLineaAsync("70", 50, 1)).Callback(() => _secuencia.Add("revertir")).Returns(Task.CompletedTask);
            _repoDetalle.Setup(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 50 && k.NoLinea == 1)))
                .Callback(() => _secuencia.Add("borrar")).ReturnsAsync(true);

            Assert.True(await _domain.EliminarAsync(50, 1));

            Assert.Equal(new[] { "revertir", "borrar" }, _secuencia);
            _lineas.Verify(l => l.RecalcularCabecera(orden, It.Is<IEnumerable<OrdenServicioDetalle>>(x => x.Single() == restante)), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_ValidaQueLaLineaNoEsteFacturada()
        {
            Orden();
            Guardada(1);
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, 50, 1, "eliminar"))
                .ThrowsAsync(new Exception("No se puede eliminar la línea 1: la usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(50, 1));

            Assert.Equal("No se puede eliminar la línea 1: la usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirLineaAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            _repoDetalle.Verify(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_LineaInexistente_DevuelveFalso()
        {
            Orden();

            Assert.False(await _domain.EliminarAsync(50, 9));
        }

        [Fact]
        public async Task EliminarAsync_OrdenCancelada_Lanza()
        {
            Orden(cancelado: "S");
            Guardada(1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(50, 1));
            Assert.Equal("La orden de servicio está cancelada y no se puede modificar.", ex.Message);
        }
    }
}
```

- [ ] **Step 2: Ejecutarlas y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~OrdenServicioDetalleDomainTests"` → no compila.

- [ ] **Step 3: Interfaz** — `API.Domain.Interface/IOrdenServicioDetalleDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IOrdenServicioDetalleDomain
    {
        #region async methods
        /// <summary>Agrega una línea a una orden existente (obj.Entry) y devuelve su NoLinea. Los insumos descuentan inventario.</summary>
        Task<int> InsertarAsync(OrdenServicioDetalle obj);
        /// <summary>Cambia una línea (revierte su inventario y vuelve a asentarla). Devuelve false si la línea no existe.</summary>
        Task<bool> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalle obj);
        /// <summary>Quita una línea y devuelve su inventario. Devuelve false si la línea no existe.</summary>
        Task<bool> EliminarAsync(int entry, int noLinea);
        #endregion
    }
}
```

- [ ] **Step 4: Dominio** — `API.Domain.Core/OrdenServicioDetalleDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class OrdenServicioDetalleDomain : IOrdenServicioDetalleDomain
    {
        private const string TipoObjetoOrden = "70";

        private readonly IRepositorioGenerico<OrdenServicio, int> _repoOrden;
        private readonly IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IOrdenServicioLineaService _lineas;
        private readonly IInventarioAsientoService _asiento;
        private readonly IDocumentoBaseService _documentoBase;
        private readonly IEjecutorTransaccion _tx;

        public OrdenServicioDetalleDomain(
            IRepositorioGenerico<OrdenServicio, int> repoOrden,
            IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)> repoDetalle,
            IOrdenServicioLineaService lineas,
            IInventarioAsientoService asiento,
            IDocumentoBaseService documentoBase,
            IEjecutorTransaccion tx)
        {
            _repoOrden = repoOrden;
            _repoDetalle = repoDetalle;
            _lineas = lineas;
            _asiento = asiento;
            _documentoBase = documentoBase;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(OrdenServicioDetalle obj)
        {
            var orden = await OrdenEditableAsync(obj.Entry);
            var existentes = await LineasAsync(obj.Entry);

            obj.NoLinea = existentes.Count == 0 ? 1 : existentes.Max(l => l.NoLinea) + 1;
            var nuevas = new List<OrdenServicioDetalle> { obj };
            await _lineas.PrepararAsync(nuevas, obj.NoLinea);   // valida y calcula antes de abrir la transacción

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoDetalle.AgregarSinGuardarAsync(obj);
                await _lineas.AsentarSalidaAsync(orden.Entry, DateTime.Now, nuevas);
                _lineas.RecalcularCabecera(orden, existentes.Concat(nuevas));
                return obj.NoLinea;
            });
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalle obj)
        {
            var orden = await OrdenEditableAsync(entry);
            var existente = await _repoDetalle.ObtenerAsync((entry, noLinea));
            if (existente is null)
                return false;

            // Una línea que ya se facturó (total o parcialmente) no se modifica: primero se cancela la factura.
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, entry, noLinea, "modificar");

            var otras = (await LineasAsync(entry)).Where(l => l.NoLinea != noLinea).ToList();

            // La línea candidata se valida y se calcula aparte; recién dentro de la transacción reemplaza a la guardada.
            var candidata = new OrdenServicioDetalle
            {
                Entry = entry, NoLinea = noLinea,
                CodArticulo = obj.CodArticulo, Descripcion = obj.Descripcion, CodAlmacen = obj.CodAlmacen,
                Cantidad = obj.Cantidad, Precio = obj.Precio, PrctjeDesc = obj.PrctjeDesc,
                CodigoImpuesto = obj.CodigoImpuesto, CodMecanico = obj.CodMecanico
            };
            var lista = new List<OrdenServicioDetalle> { candidata };
            await _lineas.PrepararAsync(lista, noLinea);

            return await _tx.EjecutarAsync(async () =>
            {
                await _asiento.RevertirLineaAsync(TipoObjetoOrden, entry, noLinea);   // devuelve lo que tenía la línea

                existente.CodArticulo = candidata.CodArticulo;
                existente.Descripcion = candidata.Descripcion;
                existente.CodAlmacen = candidata.CodAlmacen;
                existente.EsInsumo = candidata.EsInsumo;
                existente.Cantidad = candidata.Cantidad;
                existente.Precio = candidata.Precio;
                existente.PrctjeDesc = candidata.PrctjeDesc;
                existente.CodigoImpuesto = candidata.CodigoImpuesto;
                existente.Impuesto = candidata.Impuesto;
                existente.TotalLinea = candidata.TotalLinea;
                existente.CodMecanico = candidata.CodMecanico;
                existente.TipoObjeto = candidata.TipoObjeto;

                await _lineas.AsentarSalidaAsync(entry, DateTime.Now, new[] { existente });   // bloquea si no alcanza el stock
                _lineas.RecalcularCabecera(orden, otras.Append(existente));
                return true;
            });
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            var orden = await OrdenEditableAsync(entry);
            var existente = await _repoDetalle.ObtenerAsync((entry, noLinea));
            if (existente is null)
                return false;

            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.OrdenServicio, entry, noLinea, "eliminar");

            var restantes = (await LineasAsync(entry)).Where(l => l.NoLinea != noLinea).ToList();

            return await _tx.EjecutarAsync(async () =>
            {
                await _asiento.RevertirLineaAsync(TipoObjetoOrden, entry, noLinea);   // el insumo vuelve al almacén
                await _repoDetalle.EliminarAsync((entry, noLinea));
                _lineas.RecalcularCabecera(orden, restantes);
                return true;
            });
        }
        #endregion

        /// <summary>La orden debe existir, no estar cancelada y no haber sido entregada.</summary>
        private async Task<OrdenServicio> OrdenEditableAsync(int entry)
        {
            var orden = await _repoOrden.ObtenerAsync(entry)
                ?? throw new Exception("La orden de servicio no existe.");

            if (orden.Cancelado == "S")
                throw new Exception("La orden de servicio está cancelada y no se puede modificar.");
            if (orden.EstadoServicio == EstadoOrdenServicio.Entregada)
                throw new Exception("La orden de servicio ya fue entregada: solo se puede modificar el comentario.");

            return orden;
        }

        private async Task<List<OrdenServicioDetalle>> LineasAsync(int entry)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == entry).OrderBy(d => d.NoLinea).ToListAsync();
        }
    }
}
```

- [ ] **Step 5: DI** en `Startup.cs`:
```csharp
            services.AddTransient<IOrdenServicioDetalleDomain, OrdenServicioDetalleDomain>();
```

- [ ] **Step 6: Verificar** — `dotnet test … --filter "FullyQualifiedName~OrdenServicioDetalleDomainTests"` → todas pasando (20); `dotnet build API.sln …` → 0 errores.

- [ ] **Step 7: Commit**

```bash
git add API.Domain.Interface/IOrdenServicioDetalleDomain.cs API.Domain.Core/OrdenServicioDetalleDomain.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/OrdenServicioDetalleDomainTests.cs
git commit -m "feat(taller): lineas de la orden de servicio con inventario por linea (agregar, cambiar y quitar)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---
### Task 10: DTOs, aplicación, controladores y mapeo de la orden de servicio (más el historial de la moto)

**Precondición:** Tasks 4, 8 y 9 completas. `grep -n "IOrdenServicioApplication" -r API.Application.Interface` sin resultados.

**Files:**
- Create: `API.Application.DTO/ordenServicio/OrdenServicioDTO.cs`, `OrdenServicioCrearDTO.cs`, `OrdenServicioActualizarDTO.cs`, `OrdenServicioEstadoDTO.cs`, `OrdenServicioDetalleDTO.cs`, `OrdenServicioDetalleCrearDTO.cs`, `OrdenServicioDetalleActualizarDTO.cs`
- Create: `API.Application.Interface/IOrdenServicioApplication.cs`, `IOrdenServicioDetalleApplication.cs`; `API.Application.Main/OrdenServicioApplication.cs`, `OrdenServicioDetalleApplication.cs`
- Create: `API.Service.WebApi/Controllers/OrdenServicioController.cs`, `OrdenServicioDetalleController.cs`
- Modify: `API.Application.Interface/IMotocicletaApplication.cs`, `API.Application.Main/MotocicletaApplication.cs`, `API.Service.WebApi/Controllers/MotocicletaController.cs`, `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Application/OrdenServicioApplicationTests.cs`, `OrdenServicioDetalleApplicationTests.cs`, `MotocicletaApplicationTests.cs`; `API.Service.WebApi.Tests/Controllers/OrdenServicioControllerTests.cs`, `OrdenServicioDetalleControllerTests.cs`, `TallerPermisosTests.cs`; modificar `Controllers/MotocicletaControllerTests.cs`

**Interfaces:**
- Consumes: `IOrdenServicioDomain` (Task 8), `IOrdenServicioDetalleDomain` (Task 9), `IMotocicletaDomain.ObtenerHistorialAsync` (Task 4).
- Produces (rutas, todas `[Authorize]`; módulo `OrdenesServicio`):
  - `GET api/OrdenServicio?estado=` (Ver) → `Respuesta<IEnumerable<OrdenServicioDTO>>`, de la más nueva a la más antigua; `GET api/OrdenServicio/{id}` (Ver) → `Respuesta<OrdenServicioDTO>` (404 si no existe); `GET api/OrdenServicio/{id}/Lineas` (Ver) → `Respuesta<IEnumerable<OrdenServicioDetalleDTO>>`.
  - `POST api/OrdenServicio` (Agregar, cuerpo `OrdenServicioCrearDTO` con `Lineas`) → `Respuesta<int>` (Entry); `PUT api/OrdenServicio/{id}` (Actualizar, `OrdenServicioActualizarDTO`); `POST api/OrdenServicio/{id}/Estado` (Actualizar, `{ "estado": "P" }`); `POST api/OrdenServicio/{id}/Cancelar` (Eliminar).
  - `POST api/OrdenServicioDetalle` (Actualizar, `OrdenServicioDetalleCrearDTO` con `Entry`) → `Respuesta<int>` (NoLinea); `PUT api/OrdenServicioDetalle/{entry}/{noLinea}` y `DELETE …/{entry}/{noLinea}` (Actualizar).
  - `GET api/Motocicleta/{entry}/Historial` (Ver en `Motocicletas` **y** Ver en `OrdenesServicio`) → `Respuesta<IEnumerable<OrdenServicioDTO>>`.
  - `OrdenServicioDTO` incluye `Placa`, `MotoDescripcion` ("Marca Modelo") y `NombreMecanico`.

- [ ] **Step 1: DTOs** (namespace `API.Application.DTO.ordenServicio`; archivos nuevos).

`OrdenServicioDTO.cs`:
```csharp
namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? Placa { get; set; }
        public string? MotoDescripcion { get; set; }
        public string? CodMecanico { get; set; }
        public string? NombreMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string EstadoServicio { get; set; } = null!;
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`OrdenServicioCrearDTO.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual": para series autogeneradas el servidor
        // calcula el siguiente número (ver OrdenServicioDomain.InsertarAsync).
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? CodMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string? Comentario { get; set; }

        /// <summary>Líneas (insumos y servicios) que se registran junto con la orden. Los montos los calcula el servidor.</summary>
        public List<OrdenServicioDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
```

`OrdenServicioActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioActualizarDTO
    {
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? CodMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`OrdenServicioEstadoDTO.cs`:
```csharp
namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioEstadoDTO
    {
        /// <summary>Nuevo estado: P (En proceso), T (Terminada) o E (Entregada).</summary>
        public string Estado { get; set; } = null!;
    }
}
```

`OrdenServicioDetalleDTO.cs`:
```csharp
namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public string? CodAlmacen { get; set; }
        public string? EsInsumo { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? CodMecanico { get; set; }
        public string? TipoObjeto { get; set; }
    }
}
```

`OrdenServicioDetalleCrearDTO.cs`:
```csharp
namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioDetalleCrearDTO
    {
        // Solo lo usa POST api/OrdenServicioDetalle (la orden a la que se agrega); dentro de
        // OrdenServicioCrearDTO.Lineas lo asigna el servidor. Impuesto y TotalLinea no se reciben: los calcula el servidor.
        public int Entry { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public string? CodAlmacen { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public string? CodMecanico { get; set; }
    }
}
```

`OrdenServicioDetalleActualizarDTO.cs`:
```csharp
namespace API.Application.DTO.ordenServicio
{
    public class OrdenServicioDetalleActualizarDTO
    {
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public string? CodAlmacen { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public string? CodMecanico { get; set; }
    }
}
```

- [ ] **Step 2: Mapeo** — en `PerfilMapeo.cs` agregar `using API.Application.DTO.ordenServicio;` y, tras el bloque `// Motocicleta`:

```csharp
            // OrdenServicio
            CreateMap<OrdenServicio, OrdenServicioDTO>()
                .ForMember(d => d.Placa, o => o.MapFrom(s => s.MotoNavigation != null ? s.MotoNavigation.Placa : null))
                .ForMember(d => d.MotoDescripcion, o => o.MapFrom(s => s.MotoNavigation != null
                    ? ((s.MotoNavigation.Marca ?? "") + " " + (s.MotoNavigation.Modelo ?? "")).Trim()
                    : null))
                .ForMember(d => d.NombreMecanico, o => o.MapFrom(s => s.MecanicoNavigation != null ? s.MecanicoNavigation.Nombre : null));
            CreateMap<OrdenServicioCrearDTO, OrdenServicio>();
            CreateMap<OrdenServicioActualizarDTO, OrdenServicio>();

            // OrdenServicioDetalle
            CreateMap<OrdenServicioDetalle, OrdenServicioDetalleDTO>();
            CreateMap<OrdenServicioDetalleCrearDTO, OrdenServicioDetalle>();
            CreateMap<OrdenServicioDetalleActualizarDTO, OrdenServicioDetalle>();
```

- [ ] **Step 3: Pruebas de aplicación (fallan)**.

`API.Service.WebApi.Tests/Application/OrdenServicioApplicationTests.cs`:
```csharp
using API.Application.DTO.ordenServicio;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class OrdenServicioApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IOrdenServicioDomain> _domain = new();
        private readonly OrdenServicioApplication _app;

        public OrdenServicioApplicationTests()
        {
            _app = new OrdenServicioApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaCabeceraYLineas_YDevuelveElEntry()
        {
            OrdenServicio? recibida = null;
            List<OrdenServicioDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicio>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .Callback<OrdenServicio, IEnumerable<OrdenServicioDetalle>>((o, l) => { recibida = o; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(42);
            var dto = new OrdenServicioCrearDTO
            {
                Serie = 33, CodigoSn = "SN1", MonedaDoc = "QTZ", MotoEntry = 10, CodMecanico = "M01", KmIngreso = 12000, FallaReportada = "No enciende",
                Lineas =
                {
                    new OrdenServicioDetalleCrearDTO { CodArticulo = "ACE-1", Cantidad = 2, Precio = 85m, CodigoImpuesto = "IVA", CodAlmacen = "01" },
                    new OrdenServicioDetalleCrearDTO { CodArticulo = "MO-1", Cantidad = 1, Precio = 150m, CodigoImpuesto = "IVA" }
                }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal(42, respuesta.Dato);
            Assert.Equal("Registro agregado correctamente.", respuesta.Mensaje);
            Assert.Equal(("SN1", 10, "M01", 12000, "No enciende", 33), (recibida!.CodigoSn, recibida.MotoEntry, recibida.CodMecanico, recibida.KmIngreso, recibida.FallaReportada, recibida.Serie));
            Assert.Equal(new[] { "ACE-1", "MO-1" }, lineasRecibidas!.Select(l => l.CodArticulo));
            Assert.Equal("01", lineasRecibidas![0].CodAlmacen);
        }

        [Fact]
        public async Task Insertar_ErrorDelDominio_DevuelveResultadoFalseConElMensaje()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicio>(), It.IsAny<IEnumerable<OrdenServicioDetalle>>()))
                .ThrowsAsync(new Exception("La serie no existe."));

            var respuesta = await _app.InsertarAsync(new OrdenServicioCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La serie no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Obtener_MapeaPlacaMotoYMecanico()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new OrdenServicio
            {
                Entry = 5, EstadoServicio = "P",
                MotoNavigation = new Motocicleta { Placa = "M123ABC", Marca = "Honda", Modelo = "CB125" },
                MecanicoNavigation = new Mecanico { Nombre = "Juan" }
            });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal(("M123ABC", "Honda CB125", "Juan", "P"),
                (respuesta.Dato.Placa, respuesta.Dato.MotoDescripcion, respuesta.Dato.NombreMecanico, respuesta.Dato.EstadoServicio));
        }

        [Fact]
        public async Task Obtener_SinMotoNiMecanicoCargados_DejaLosNombresEnNulo()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new OrdenServicio { Entry = 5, EstadoServicio = "R" });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.Null(respuesta.Dato.Placa);
            Assert.Null(respuesta.Dato.MotoDescripcion);
            Assert.Null(respuesta.Dato.NombreMecanico);
        }

        [Fact]
        public async Task Obtener_Inexistente_DevuelveDatoNulo()
        {
            _domain.Setup(d => d.ObtenerAsync(99)).ReturnsAsync((OrdenServicio?)null);

            var respuesta = await _app.ObtenerAsync(99);

            Assert.True(respuesta.Resultado);
            Assert.Null(respuesta.Dato);
        }

        [Fact]
        public async Task ObtenerTodo_FiltraPorEstadoYOrdenaDeLaMasNuevaALaMasAntigua()
        {
            var ordenes = new List<OrdenServicio>
            {
                new() { Entry = 1, EstadoServicio = "T" },
                new() { Entry = 2, EstadoServicio = "R" },
                new() { Entry = 3, EstadoServicio = "T" },
            };
            _domain.Setup(d => d.ObtenerTodoAsync()).ReturnsAsync(ordenes.AsAsyncQueryable());

            var terminadas = await _app.ObtenerTodoAsync("t");
            var todas = await _app.ObtenerTodoAsync(null);

            Assert.Equal(new[] { 3, 1 }, terminadas.Dato.Select(o => o.Entry));
            Assert.Equal(new[] { 3, 2, 1 }, todas.Dato.Select(o => o.Entry));
        }

        [Fact]
        public async Task Actualizar_OrdenInexistente_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.ActualizarAsync(99, It.IsAny<OrdenServicio>())).ReturnsAsync(false);

            var respuesta = await _app.ActualizarAsync(99, new OrdenServicioActualizarDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La orden de servicio no se encontró.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_Ok()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.Is<OrdenServicio>(o => o.Diagnostico == "Bujía"))).ReturnsAsync(true);

            var respuesta = await _app.ActualizarAsync(5, new OrdenServicioActualizarDTO { Diagnostico = "Bujía" });

            Assert.True(respuesta.Resultado);
            Assert.Equal("Registro actualizado correctamente.", respuesta.Mensaje);
        }

        [Fact]
        public async Task CambiarEstado_Ok_YConErrorDelDominio()
        {
            _domain.Setup(d => d.CambiarEstadoAsync(5, "P")).ReturnsAsync(true);
            _domain.Setup(d => d.CambiarEstadoAsync(5, "R")).ThrowsAsync(new Exception("La orden ya está en estado Recibida."));

            var ok = await _app.CambiarEstadoAsync(5, "P");
            var mala = await _app.CambiarEstadoAsync(5, "R");

            Assert.True(ok.Resultado);
            Assert.Equal("Estado actualizado correctamente.", ok.Mensaje);
            Assert.False(mala.Resultado);
            Assert.Equal("La orden ya está en estado Recibida.", mala.Mensaje);
        }

        [Fact]
        public async Task Cancelar_Ok_YConErrorDelDominio()
        {
            _domain.SetupSequence(d => d.CancelarAsync(5))
                .ReturnsAsync(true)
                .ThrowsAsync(new Exception("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos."));

            var ok = await _app.CancelarAsync(5);
            var mala = await _app.CancelarAsync(5);

            Assert.True(ok.Resultado);
            Assert.Equal("Orden cancelada. El inventario de los insumos fue devuelto.", ok.Mensaje);
            Assert.False(mala.Resultado);
            Assert.StartsWith("No se puede cancelar el documento", mala.Mensaje);
        }

        [Fact]
        public async Task ObtenerLineas_MapeaLasLineas()
        {
            _domain.Setup(d => d.ObtenerLineasAsync(5)).ReturnsAsync(new[]
            {
                new OrdenServicioDetalle { Entry = 5, NoLinea = 1, CodArticulo = "ACE-1", EsInsumo = "S", Cantidad = 2m, TotalLinea = 190.4m }
            });

            var respuesta = await _app.ObtenerLineasAsync(5);

            var linea = Assert.Single(respuesta.Dato);
            Assert.Equal(("ACE-1", "S", 190.4m), (linea.CodArticulo, linea.EsInsumo, linea.TotalLinea));
        }
    }
}
```

`OrdenServicioDetalleApplicationTests.cs`:
```csharp
using API.Application.DTO.ordenServicio;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class OrdenServicioDetalleApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IOrdenServicioDetalleDomain> _domain = new();
        private readonly OrdenServicioDetalleApplication _app;

        public OrdenServicioDetalleApplicationTests()
        {
            _app = new OrdenServicioDetalleApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaYDevuelveElNumeroDeLinea()
        {
            OrdenServicioDetalle? recibida = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicioDetalle>()))
                .Callback<OrdenServicioDetalle>(l => recibida = l).ReturnsAsync(3);

            var respuesta = await _app.InsertarAsync(new OrdenServicioDetalleCrearDTO { Entry = 50, CodArticulo = "ACE-1", Cantidad = 2, Precio = 85m, CodigoImpuesto = "IVA" });

            Assert.True(respuesta.Resultado);
            Assert.Equal(3, respuesta.Dato);
            Assert.Equal("Línea agregada correctamente.", respuesta.Mensaje);
            Assert.Equal((50, "ACE-1", 2m), (recibida!.Entry, recibida.CodArticulo, recibida.Cantidad));
        }

        [Fact]
        public async Task Insertar_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<OrdenServicioDetalle>())).ThrowsAsync(new Exception("La orden de servicio no existe."));

            var respuesta = await _app.InsertarAsync(new OrdenServicioDetalleCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La orden de servicio no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_LineaInexistente_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.ActualizarAsync(50, 9, It.IsAny<OrdenServicioDetalle>())).ReturnsAsync(false);

            var respuesta = await _app.ActualizarAsync(50, 9, new OrdenServicioDetalleActualizarDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La línea no se encontró.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_Ok()
        {
            _domain.Setup(d => d.ActualizarAsync(50, 1, It.Is<OrdenServicioDetalle>(l => l.Cantidad == 3m))).ReturnsAsync(true);

            var respuesta = await _app.ActualizarAsync(50, 1, new OrdenServicioDetalleActualizarDTO { Cantidad = 3m });

            Assert.True(respuesta.Resultado);
            Assert.Equal("Línea actualizada correctamente.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Eliminar_Ok_Inexistente_YError()
        {
            _domain.Setup(d => d.EliminarAsync(50, 1)).ReturnsAsync(true);
            _domain.Setup(d => d.EliminarAsync(50, 9)).ReturnsAsync(false);
            _domain.Setup(d => d.EliminarAsync(50, 2)).ThrowsAsync(new Exception("No se puede eliminar la línea 2: la usan Factura 2. Cancele primero esos documentos."));

            var ok = await _app.EliminarAsync(50, 1);
            var noExiste = await _app.EliminarAsync(50, 9);
            var error = await _app.EliminarAsync(50, 2);

            Assert.True(ok.Resultado);
            Assert.Equal("Línea eliminada correctamente.", ok.Mensaje);
            Assert.False(noExiste.Resultado);
            Assert.Equal("La línea no se encontró.", noExiste.Mensaje);
            Assert.False(error.Resultado);
            Assert.StartsWith("No se puede eliminar la línea 2", error.Mensaje);
        }
    }
}
```

`MotocicletaApplicationTests.cs`:
```csharp
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class MotocicletaApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IMotocicletaDomain> _domain = new();
        private readonly MotocicletaApplication _app;

        public MotocicletaApplicationTests()
        {
            _app = new MotocicletaApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Obtener_MapeaElNombreDelCliente()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new Motocicleta
            {
                Entry = 5, CodigoSn = "CL-1", Placa = "M123ABC", Activo = "S",
                CodigoSnNavigation = new SocioNegocio { Codigo = "CL-1", Nombre = "Cliente Uno" }
            });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.Equal(("M123ABC", "Cliente Uno"), (respuesta.Dato.Placa, respuesta.Dato.NombreSn));
        }

        [Fact]
        public async Task ObtenerHistorial_MapeaLasOrdenesDeLaMoto()
        {
            _domain.Setup(d => d.ObtenerHistorialAsync(5)).ReturnsAsync(new[]
            {
                new OrdenServicio { Entry = 3, NumDoc = 30, EstadoServicio = "T", MotoEntry = 5, MecanicoNavigation = new Mecanico { Nombre = "Juan" } },
                new OrdenServicio { Entry = 1, NumDoc = 10, EstadoServicio = "E", MotoEntry = 5 }
            });

            var respuesta = await _app.ObtenerHistorialAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal(new[] { 30, 10 }, respuesta.Dato.Select(o => o.NumDoc));
            Assert.Equal("Juan", respuesta.Dato.First().NombreMecanico);
        }

        [Fact]
        public async Task ObtenerHistorial_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ObtenerHistorialAsync(5)).ThrowsAsync(new Exception("falla"));

            var respuesta = await _app.ObtenerHistorialAsync(5);

            Assert.False(respuesta.Resultado);
            Assert.Equal("falla", respuesta.Mensaje);
        }
    }
}
```

- [ ] **Step 4: Ejecutarlas y ver que fallan** — `dotnet test … --filter "FullyQualifiedName~OrdenServicioApplicationTests|FullyQualifiedName~OrdenServicioDetalleApplicationTests|FullyQualifiedName~MotocicletaApplicationTests"` → no compila.

- [ ] **Step 5: Interfaces y aplicaciones**.

`API.Application.Interface/IOrdenServicioApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;

namespace API.Application.Interface
{
    public interface IOrdenServicioApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(OrdenServicioCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, OrdenServicioActualizarDTO obj);
        Task<Respuesta<bool>> CambiarEstadoAsync(int id, string estado);
        Task<Respuesta<bool>> CancelarAsync(int id);
        Task<Respuesta<OrdenServicioDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerTodoAsync(string? estado);
        Task<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>> ObtenerLineasAsync(int id);
        #endregion
    }
}
```

`API.Application.Interface/IOrdenServicioDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;

namespace API.Application.Interface
{
    public interface IOrdenServicioDetalleApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(OrdenServicioDetalleCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalleActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
        #endregion
    }
}
```

`API.Application.Main/OrdenServicioApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class OrdenServicioApplication : IOrdenServicioApplication
    {
        private readonly IOrdenServicioDomain _domain;
        private readonly IMapper _mapper;

        public OrdenServicioApplication(IOrdenServicioDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(OrdenServicioCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var orden = _mapper.Map<OrdenServicio>(obj);
                var lineas = _mapper.Map<IEnumerable<OrdenServicioDetalle>>(obj.Lineas);
                respuesta.Dato = await _domain.InsertarAsync(orden, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, OrdenServicioActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(id, _mapper.Map<OrdenServicio>(obj));
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Registro actualizado correctamente." : "La orden de servicio no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> CambiarEstadoAsync(int id, string estado)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.CambiarEstadoAsync(id, estado);
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Estado actualizado correctamente." : "La orden de servicio no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> CancelarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.CancelarAsync(id);
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato
                    ? "Orden cancelada. El inventario de los insumos fue devuelto."
                    : "La orden de servicio no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<OrdenServicioDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<OrdenServicioDTO>();
            try
            {
                respuesta.Dato = _mapper.Map<OrdenServicioDTO>(await _domain.ObtenerAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerTodoAsync(string? estado)
        {
            var respuesta = new Respuesta<IEnumerable<OrdenServicioDTO>>();
            try
            {
                var consulta = await _domain.ObtenerTodoAsync();
                if (!string.IsNullOrWhiteSpace(estado))
                {
                    var filtro = estado.Trim().ToUpperInvariant();
                    consulta = consulta.Where(x => x.EstadoServicio == filtro);
                }
                var ordenes = await consulta.OrderByDescending(x => x.Entry).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<OrdenServicioDTO>>(ordenes);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<OrdenServicioDetalleDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<OrdenServicioDetalleDTO>>(await _domain.ObtenerLineasAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
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

`API.Application.Main/OrdenServicioDetalleApplication.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;

namespace API.Application.Main
{
    public class OrdenServicioDetalleApplication : IOrdenServicioDetalleApplication
    {
        private const string LineaNoEncontrada = "La línea no se encontró.";

        private readonly IOrdenServicioDetalleDomain _domain;
        private readonly IMapper _mapper;

        public OrdenServicioDetalleApplication(IOrdenServicioDetalleDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(OrdenServicioDetalleCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<OrdenServicioDetalle>(obj));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Línea agregada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalleActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(entry, noLinea, _mapper.Map<OrdenServicioDetalle>(obj));
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Línea actualizada correctamente." : LineaNoEncontrada;
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
                respuesta.Dato = await _domain.EliminarAsync(entry, noLinea);
                respuesta.Resultado = respuesta.Dato;
                respuesta.Mensaje = respuesta.Dato ? "Línea eliminada correctamente." : LineaNoEncontrada;
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

Historial de la moto — en `IMotocicletaApplication.cs` agregar `using API.Application.DTO.ordenServicio;` y, tras `BuscarAsync`:
```csharp
        Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerHistorialAsync(int entry);
```
y en `MotocicletaApplication.cs` (con `using API.Application.DTO.ordenServicio;`), dentro de `#region async methods`:
```csharp
        public async Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerHistorialAsync(int entry)
        {
            var respuesta = new Respuesta<IEnumerable<OrdenServicioDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<OrdenServicioDTO>>(await _domain.ObtenerHistorialAsync(entry));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
```

- [ ] **Step 6: Controladores**.

`API.Service.WebApi/Controllers/OrdenServicioController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/OrdenServicio")]
    public class OrdenServicioController : ControllerBase
    {
        private const string NoEncontrada = "La orden de servicio no se encontró.";

        private readonly IOrdenServicioApplication _application;

        public OrdenServicioController(IOrdenServicioApplication application)
        {
            _application = application;
        }

        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<OrdenServicioDTO>>> Obtener([FromRoute] int id)
        {
            var orden = await _application.ObtenerAsync(id);

            if (!orden.Resultado)
                return BadRequest(orden);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            return Ok(orden);
        }

        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<OrdenServicioDTO>>>> ObtenerTodoAsync([FromQuery] string? estado)
        {
            var ordenes = await _application.ObtenerTodoAsync(estado);

            if (!ordenes.Resultado)
                return BadRequest(ordenes);

            return Ok(ordenes);
        }

        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        [HttpGet("{id:int}/Lineas")]
        public async Task<ActionResult<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>>> ObtenerLineas([FromRoute] int id)
        {
            var lineas = await _application.ObtenerLineasAsync(id);

            if (!lineas.Resultado)
                return BadRequest(lineas);

            return Ok(lineas);
        }

        [RequierePermiso("OrdenesServicio", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] OrdenServicioCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] OrdenServicioActualizarDTO obj)
        {
            var orden = await _application.ObtenerAsync(id);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            var actualizar = await _application.ActualizarAsync(id, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPost("{id:int}/Estado")]
        public async Task<ActionResult<Respuesta<bool>>> CambiarEstadoAsync([FromRoute] int id, [FromBody] OrdenServicioEstadoDTO obj)
        {
            var orden = await _application.ObtenerAsync(id);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            var cambio = await _application.CambiarEstadoAsync(id, obj.Estado);

            if (!cambio.Resultado)
                return BadRequest(cambio);

            return Ok(cambio);
        }

        // Cancelar una orden equivale a anularla: exige Accion.Eliminar (convención: 'cancelar documento -> Eliminar').
        [RequierePermiso("OrdenesServicio", Accion.Eliminar)]
        [HttpPost("{id:int}/Cancelar")]
        public async Task<ActionResult<Respuesta<bool>>> CancelarAsync([FromRoute] int id)
        {
            var orden = await _application.ObtenerAsync(id);

            if (orden.Dato == null)
            {
                orden.Resultado = false;
                orden.Mensaje = NoEncontrada;
                return NotFound(orden);
            }

            var cancelar = await _application.CancelarAsync(id);

            if (!cancelar.Resultado)
                return BadRequest(cancelar);

            return Ok(cancelar);
        }
    }
}
```
(En `ActualizarAsync`/`CambiarEstadoAsync`/`CancelarAsync`, `NotFound(orden)` devuelve `Respuesta<OrdenServicioDTO>`; el tipo de retorno `ActionResult<Respuesta<bool>>` lo acepta porque `NotFound(...)` es un `ActionResult`, igual que en `NotaCreditoController`.)

`API.Service.WebApi/Controllers/OrdenServicioDetalleController.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    // Agregar, cambiar o quitar una línea es editar la orden: los tres endpoints exigen Actualizar sobre OrdenesServicio.
    [Authorize]
    [ApiController]
    [Route("api/OrdenServicioDetalle")]
    public class OrdenServicioDetalleController : ControllerBase
    {
        private readonly IOrdenServicioDetalleApplication _application;

        public OrdenServicioDetalleController(IOrdenServicioDetalleApplication application)
        {
            _application = application;
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] OrdenServicioDetalleCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpPut("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int entry, [FromRoute] int noLinea, [FromBody] OrdenServicioDetalleActualizarDTO obj)
        {
            var actualizar = await _application.ActualizarAsync(entry, noLinea, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        [HttpDelete("{entry:int}/{noLinea:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int entry, [FromRoute] int noLinea)
        {
            var eliminar = await _application.EliminarAsync(entry, noLinea);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
```

Historial en `MotocicletaController.cs` (agregar `using API.Application.DTO.ordenServicio;` y, tras la acción `Obtener`):
```csharp
        // Muestra órdenes de servicio: además de ver motocicletas exige ver órdenes de servicio.
        [RequierePermiso("Motocicletas", Accion.Ver)]
        [HttpGet("{entry:int}/Historial")]
        public async Task<ActionResult<Respuesta<IEnumerable<OrdenServicioDTO>>>> ObtenerHistorial([FromRoute] int entry)
        {
            if (!PermisoUsuario.Tiene(User, "OrdenesServicio", Accion.Ver))
                return PermisoUsuario.Denegado();

            var historial = await _application.ObtenerHistorialAsync(entry);

            if (!historial.Resultado)
                return BadRequest(historial);

            return Ok(historial);
        }
```

DI en `Startup.cs`:
```csharp
            services.AddTransient<IOrdenServicioApplication, OrdenServicioApplication>();
            services.AddTransient<IOrdenServicioDetalleApplication, OrdenServicioDetalleApplication>();
```

- [ ] **Step 7: Pruebas de controladores**.

`API.Service.WebApi.Tests/Controllers/OrdenServicioControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class OrdenServicioControllerTests
    {
        private readonly Mock<IOrdenServicioApplication> _app = new();

        private OrdenServicioController Crear() => new(_app.Object);

        private void Existe(int id) =>
            _app.Setup(a => a.ObtenerAsync(id)).ReturnsAsync(new Respuesta<OrdenServicioDTO> { Resultado = true, Dato = new OrdenServicioDTO { Entry = id } });

        private void NoExiste(int id) =>
            _app.Setup(a => a.ObtenerAsync(id)).ReturnsAsync(new Respuesta<OrdenServicioDTO> { Resultado = true, Dato = null! });

        [Fact]
        public async Task Obtener_BadRequest_NotFound_YOk()
        {
            var mala = new Respuesta<OrdenServicioDTO> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<OrdenServicioDTO> { Resultado = true, Dato = new OrdenServicioDTO { Entry = 1 } };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(mala);
            _app.Setup(a => a.ObtenerAsync(2)).ReturnsAsync(buena);
            NoExiste(3);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Obtener(1)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Obtener(2)).Result).Value);
            var notFound = Assert.IsType<NotFoundObjectResult>((await Crear().Obtener(3)).Result);
            Assert.Equal("La orden de servicio no se encontró.", Assert.IsType<Respuesta<OrdenServicioDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task ObtenerTodo_PasaElFiltroDeEstado_YDevuelveBadRequestOOk()
        {
            var mala = new Respuesta<IEnumerable<OrdenServicioDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<OrdenServicioDTO>> { Resultado = true, Dato = new[] { new OrdenServicioDTO() } };
            _app.Setup(a => a.ObtenerTodoAsync("T")).ReturnsAsync(buena);
            _app.Setup(a => a.ObtenerTodoAsync(null)).ReturnsAsync(mala);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodoAsync("T")).Result).Value);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodoAsync(null)).Result).Value);
        }

        [Fact]
        public async Task ObtenerLineas_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<OrdenServicioDetalleDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<OrdenServicioDetalleDTO>> { Resultado = true, Dato = new[] { new OrdenServicioDetalleDTO() } };
            _app.SetupSequence(a => a.ObtenerLineasAsync(5)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
        }

        [Fact]
        public async Task Insertar_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 42 };
            var dto = new OrdenServicioCrearDTO { Serie = 3 };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_BadRequest_YOk()
        {
            var dto = new OrdenServicioActualizarDTO { MotoEntry = 10 };
            NoExiste(1);
            Existe(2);
            Existe(3);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.ActualizarAsync(2, dto)).ReturnsAsync(mala);
            _app.Setup(a => a.ActualizarAsync(3, dto)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().ActualizarAsync(1, dto)).Result);
            _app.Verify(a => a.ActualizarAsync(1, It.IsAny<OrdenServicioActualizarDTO>()), Times.Never);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ActualizarAsync(2, dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ActualizarAsync(3, dto)).Result).Value);
        }

        [Fact]
        public async Task CambiarEstado_NotFound_BadRequest_YOk()
        {
            NoExiste(1);
            Existe(2);
            Existe(3);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "No se puede pasar de Terminada a En proceso: la orden solo avanza." };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.CambiarEstadoAsync(2, "P")).ReturnsAsync(mala);
            _app.Setup(a => a.CambiarEstadoAsync(3, "T")).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().CambiarEstadoAsync(1, new OrdenServicioEstadoDTO { Estado = "P" })).Result);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().CambiarEstadoAsync(2, new OrdenServicioEstadoDTO { Estado = "P" })).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().CambiarEstadoAsync(3, new OrdenServicioEstadoDTO { Estado = "T" })).Result).Value);
        }

        [Fact]
        public async Task Cancelar_NotFound_BadRequest_YOk()
        {
            NoExiste(1);
            Existe(2);
            Existe(3);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos." };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.CancelarAsync(2)).ReturnsAsync(mala);
            _app.Setup(a => a.CancelarAsync(3)).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().CancelarAsync(1)).Result);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().CancelarAsync(2)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().CancelarAsync(3)).Result).Value);
        }
    }
}
```

`OrdenServicioDetalleControllerTests.cs`:
```csharp
using API.Application.DTO;
using API.Application.DTO.ordenServicio;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class OrdenServicioDetalleControllerTests
    {
        private readonly Mock<IOrdenServicioDetalleApplication> _app = new();

        private OrdenServicioDetalleController Crear() => new(_app.Object);

        [Fact]
        public async Task Insertar_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 3 };
            var dto = new OrdenServicioDetalleCrearDTO { Entry = 50, CodArticulo = "ACE-1" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().InsertarAsync(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new OrdenServicioDetalleActualizarDTO { Cantidad = 2m };
            _app.SetupSequence(a => a.ActualizarAsync(50, 1, dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ActualizarAsync(50, 1, dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ActualizarAsync(50, 1, dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.EliminarAsync(50, 1)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().EliminarAsync(50, 1)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().EliminarAsync(50, 1)).Result).Value);
        }
    }
}
```

En `MotocicletaControllerTests.cs`, agregar `using System.Security.Claims;`, `using API.Application.DTO.ordenServicio;`, `using Microsoft.AspNetCore.Http;` y, dentro de la clase:
```csharp
        private MotocicletaController CrearConPermisos(params Claim[] claims) => new(_app.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };

        [Fact]
        public async Task ObtenerHistorial_SinVerOrdenesDeServicio_Devuelve403()
        {
            var c = CrearConPermisos(new Claim("SuperUsuario", "N"), new Claim("perm", "Motocicletas:V"));

            var r = await c.ObtenerHistorial(5);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            _app.Verify(a => a.ObtenerHistorialAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerHistorial_ConVerOrdenesDeServicio_DevuelveOk()
        {
            var c = CrearConPermisos(new Claim("SuperUsuario", "N"), new Claim("perm", "Motocicletas:V"), new Claim("perm", "OrdenesServicio:V"));
            var buena = new Respuesta<IEnumerable<OrdenServicioDTO>> { Resultado = true, Dato = new[] { new OrdenServicioDTO() } };
            _app.Setup(a => a.ObtenerHistorialAsync(5)).ReturnsAsync(buena);

            var r = await c.ObtenerHistorial(5);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task ObtenerHistorial_BadRequest_CuandoResultadoEsFalso()
        {
            var c = CrearConPermisos(new Claim("SuperUsuario", "S"));
            var mala = new Respuesta<IEnumerable<OrdenServicioDTO>> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.ObtenerHistorialAsync(5)).ReturnsAsync(mala);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await c.ObtenerHistorial(5)).Result).Value);
        }
```

`API.Service.WebApi.Tests/Controllers/TallerPermisosTests.cs` (tabla de permisos por reflexión, igual que `NotaCreditoPermisosTests`):
```csharp
using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión la tabla de permisos de los controllers del taller.</summary>
    public class TallerPermisosTests
    {
        private static readonly FieldInfo CampoModulo =
            typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo CampoAccion =
            typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static IEnumerable<MethodInfo> Acciones(Type controller) =>
            controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Any());

        private static (string Modulo, Accion Accion) Permiso(MethodInfo metodo)
        {
            var attr = metodo.GetCustomAttribute<RequierePermisoAttribute>();
            Assert.NotNull(attr);
            return ((string)CampoModulo.GetValue(attr)!, (Accion)CampoAccion.GetValue(attr)!);
        }

        public static IEnumerable<object[]> Casos()
        {
            yield return new object[] { typeof(MecanicoController), "Mecanicos", new Dictionary<string, Accion>
            {
                ["ObtenerTodo"] = Accion.Ver, ["ObtenerPorCodigo"] = Accion.Ver, ["Crear"] = Accion.Agregar,
                ["Actualizar"] = Accion.Actualizar, ["Eliminar"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(MotocicletaController), "Motocicletas", new Dictionary<string, Accion>
            {
                ["Buscar"] = Accion.Ver, ["Obtener"] = Accion.Ver, ["ObtenerHistorial"] = Accion.Ver, ["Crear"] = Accion.Agregar,
                ["Actualizar"] = Accion.Actualizar, ["Eliminar"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(OrdenServicioController), "OrdenesServicio", new Dictionary<string, Accion>
            {
                ["Obtener"] = Accion.Ver, ["ObtenerTodoAsync"] = Accion.Ver, ["ObtenerLineas"] = Accion.Ver,
                ["InsertarAsync"] = Accion.Agregar, ["ActualizarAsync"] = Accion.Actualizar,
                ["CambiarEstadoAsync"] = Accion.Actualizar, ["CancelarAsync"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(OrdenServicioDetalleController), "OrdenesServicio", new Dictionary<string, Accion>
            {
                ["InsertarAsync"] = Accion.Actualizar, ["ActualizarAsync"] = Accion.Actualizar, ["EliminarAsync"] = Accion.Actualizar
            } };
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void TablaDePermisos_EsLaEsperada(Type controller, string modulo, Dictionary<string, Accion> esperado)
        {
            var real = Acciones(controller).ToDictionary(m => m.Name, m => Permiso(m));

            Assert.Equal(esperado.Keys.OrderBy(k => k), real.Keys.OrderBy(k => k));
            foreach (var (nombre, accion) in esperado)
            {
                Assert.Equal(modulo, real[nombre].Modulo);
                Assert.True(accion == real[nombre].Accion, $"{controller.Name}.{nombre}: se esperaba {accion} y es {real[nombre].Accion}");
            }
        }

        [Theory]
        [InlineData(typeof(MecanicoController))]
        [InlineData(typeof(MotocicletaController))]
        [InlineData(typeof(OrdenServicioController))]
        [InlineData(typeof(OrdenServicioDetalleController))]
        public void LosControllersDelTaller_ExigenAutenticacion(Type controller)
        {
            Assert.NotNull(controller.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        }
    }
}
```

- [ ] **Step 8: Verificar** — `dotnet test … --filter "FullyQualifiedName~OrdenServicio|FullyQualifiedName~Motocicleta|FullyQualifiedName~TallerPermisos"` → todas pasando; `dotnet build API.sln …` → 0 errores.

- [ ] **Step 9: Commit**

```bash
git add API.Application.DTO/ordenServicio API.Application.Interface/IOrdenServicioApplication.cs API.Application.Interface/IOrdenServicioDetalleApplication.cs API.Application.Interface/IMotocicletaApplication.cs API.Application.Main/OrdenServicioApplication.cs API.Application.Main/OrdenServicioDetalleApplication.cs API.Application.Main/MotocicletaApplication.cs API.Service.WebApi/Controllers/OrdenServicioController.cs API.Service.WebApi/Controllers/OrdenServicioDetalleController.cs API.Service.WebApi/Controllers/MotocicletaController.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Application/OrdenServicioApplicationTests.cs API.Service.WebApi.Tests/Application/OrdenServicioDetalleApplicationTests.cs API.Service.WebApi.Tests/Application/MotocicletaApplicationTests.cs API.Service.WebApi.Tests/Controllers/OrdenServicioControllerTests.cs API.Service.WebApi.Tests/Controllers/OrdenServicioDetalleControllerTests.cs API.Service.WebApi.Tests/Controllers/MotocicletaControllerTests.cs API.Service.WebApi.Tests/Controllers/TallerPermisosTests.cs
git commit -m "feat(taller): API de ordenes de servicio, sus lineas e historial de la motocicleta con permisos del modulo Taller

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Suite completa, script completo de la base y prueba de humo contra SQL Server real

**Precondición:** Tasks 1–10 completas y commiteadas. `git status --short` solo muestra `API.Service.WebApi/appsettings.json`.

**Files:**
- Modify: `sql/crear-api-db-test-completo.sql` (se anexa el DDL del taller al final)
- La prueba de humo vive en el scratchpad y no se commitea.

**Interfaces:** ninguna nueva.

- [ ] **Step 1: Compilación y suite completa**

Run (PowerShell, desde la raíz de la API): `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
Expected: 0 errores de compilación y **0 fallos**; total = 1573 + las pruebas nuevas del plan. Reportar la cifra exacta.

- [ ] **Step 2: Anexar el DDL del taller al script completo** — así una base nueva creada con `crear-api-db-test-completo.sql` ya trae las tablas, la serie 70 y los módulos:

```bash
grep -n "@ck" sql/crear-api-db-test-completo.sql    # debe estar vacío; si no, renombrar la variable del bloque anexado
file sql/crear-api-db-test-completo.sql
```
Anexar al final del archivo (conservando su fin de línea) una línea en blanco, el comentario
```sql
-------------------------------------------------------------------------------------------------
-- TALLER: ordenes de servicio de motocicletas, mecanicos y motocicletas (equivale a
-- sql/2026-10-22-ordenes-servicio-taller.sql). Idempotente; amplia el CHECK de FacturaDetalle.BaseTipo a 70.
-------------------------------------------------------------------------------------------------
```
y el contenido completo de `sql/2026-10-22-ordenes-servicio-taller.sql` **sin** su primera línea de comentarios de cabecera ni `SET NOCOUNT ON;` (ya están al inicio del script completo). Verificar que el archivo sigue siendo UTF-8 (los acentos de `'Mecánicos'`, `'Órdenes de servicio'`).

- [ ] **Step 3: Base temporal y API** (PowerShell, misma sesión; `$SP` = ruta del scratchpad):

```powershell
$ErrorActionPreference = 'Stop'
$SP = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
$repo = 'C:\Users\migue\source\repos\angelm0508'
$cfg = Get-Content "$repo\API\API.Service.WebApi\appsettings.json" -Raw
$pw = [regex]::Match($cfg, 'Password=([^;"]*)').Groups[1].Value
if (-not $pw) { throw 'sin contraseña en appsettings' }
$db = 'API_DB_TALLER_A'
if (((sqlcmd -S localhost -U sa -P $pw -C -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='$db'") -join '').Trim() -ne '0') { throw "$db ya existe" }
$sql = (Get-Content "$repo\API\sql\crear-api-db-test-completo.sql" -Raw -Encoding UTF8) -replace 'API_DB_TEST', $db
if ($sql -match 'API_DB_TEST') { throw 'guard: API_DB_TEST sigue presente' }
$tmp = "$SP\$db.sql"; $sql | Set-Content $tmp -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i $tmp          # debe terminar sin errores

Set-Location "$repo\API"
dotnet build API.Service.WebApi\API.Service.WebApi.csproj -p:BaseOutputPath="$SP\apismoke\"
$dll = Get-ChildItem "$SP\apismoke" -Recurse -Filter API.Service.WebApi.dll | Select-Object -First 1
$env:ConnectionStrings__API_DB = "Server=localhost;Database=$db;User Id=sa;Password=$pw;TrustServerCertificate=True;"
$env:ASPNETCORE_URLS = 'http://localhost:5199'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$api = Start-Process dotnet -ArgumentList "`"$($dll.FullName)`"" -WorkingDirectory "$repo\API\API.Service.WebApi" -PassThru -WindowStyle Hidden -RedirectStandardOutput "$SP\api-taller.log" -RedirectStandardError "$SP\api-taller.err"
1..30 | ForEach-Object { try { Invoke-WebRequest http://localhost:5199/swagger -UseBasicParsing -SkipHttpErrorCheck | Out-Null; break } catch { Start-Sleep 1 } }
```
Expected: el script completo corre sin errores (incluido el bloque del taller), el build termina en 0 errores y la API responde en 5199. `-WorkingDirectory` **debe** ser `API.Service.WebApi` (si no, no carga `appsettings.json` y todo responde 500). Comprobar que `$SP\api-taller.log` muestra `API_DB_TALLER_A` y **nunca** `API_DB_TEST`. La contraseña no se imprime. Verificar también en la base: `SELECT name FROM sys.tables WHERE name IN ('Mecanico','Motocicleta','OrdenServicio','OrdenServicioDetalle')` (4 filas), `SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.FacturaDetalle') AND definition LIKE '%BaseTipo%'` (contiene `70`) y `SELECT Codigo FROM Modulo WHERE Grupo='Taller'` (3 filas).

- [ ] **Step 4: Prueba de humo** — crear `$SP\smoke-taller.ps1` con este contenido y correrlo: `pwsh -File "$SP\smoke-taller.ps1" -Base http://localhost:5199`.

```powershell
param([string]$Base = 'http://localhost:5199')
$ErrorActionPreference = 'Stop'
$script:Fallos = 0

function Esperar([string]$nombre, [bool]$ok, [string]$detalle = '') {
    if ($ok) { Write-Host "OK    $nombre" -ForegroundColor Green }
    else { Write-Host "FALLA $nombre $detalle" -ForegroundColor Red; $script:Fallos++ }
}

function Api([string]$metodo, [string]$ruta, $cuerpo = $null, [switch]$SinToken) {
    $h = @{}
    if (-not $SinToken) { $h.Authorization = "Bearer $script:Token" }
    $p = @{ Method = $metodo; Uri = "$Base/$ruta"; Headers = $h; SkipHttpErrorCheck = $true; UseBasicParsing = $true }
    if ($null -ne $cuerpo) { $p.Body = ($cuerpo | ConvertTo-Json -Depth 10); $p.ContentType = 'application/json; charset=utf-8' }
    $r = Invoke-WebRequest @p
    $o = $null; try { $o = $r.Content | ConvertFrom-Json } catch { }
    [pscustomobject]@{ Status = [int]$r.StatusCode; Body = $o; Dato = $o.dato; Mensaje = $o.mensaje }
}

function Serie([string]$obj, [string]$sub = '--', [string]$manual = 'N') {
    (Api GET "api/NumeracionDocumentoDet/PorDocumento/$obj").Dato |
        Where-Object { $_.subTipoDoc -eq $sub -and $_.manual -eq $manual } | Select-Object -First 1 -ExpandProperty serie
}

function Existencia([string]$cod) {
    $q = 0; foreach ($x in (Api GET "api/Existencia/PorArticulo/$cod").Dato) { $q += [decimal]$x.disponible }; $q
}

function Linea($cod, $desc, $cant, $precio, $alm = '01', $tasa = 12) {
    $sub = [decimal]$cant * [decimal]$precio; $imp = [Math]::Round($sub * $tasa / 100, 2)
    @{ Entry = 0; CodArticulo = $cod; Descripcion = $desc; Cantidad = $cant; Precio = $precio; PrecioBruto = $precio; PrctjeDesc = 0
       CodigoImpuesto = 'IVA'; Impuesto = $imp; TotalLinea = $sub + $imp; CodAlmacen = $alm }
}
function LineaBase($cod, $desc, $cant, $precio, $baseTipo, $baseEntry, $baseLinea) {
    $l = Linea $cod $desc $cant $precio; $l.BaseTipo = $baseTipo; $l.BaseEntry = $baseEntry; $l.BaseLinea = $baseLinea; $l
}
function Cab($serie, $sn, $nombre, $lineas) {
    $br = 0; $im = 0; $tot = 0
    foreach ($l in $lineas) { $br += [decimal]$l.Cantidad * [decimal]$l.Precio; $im += [decimal]$l.Impuesto; $tot += [decimal]$l.TotalLinea }
    $now = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss')
    @{ Serie = $serie; Cancelado = 'N'; NumManual = 'N'; Imprimido = 'N'; EstadoDoc = 'A'; FechaDoc = $now; FechaEmision = $now
       CodigoSn = $sn; NombreSn = $nombre; Direccion = 'Sede'; MonedaDoc = 'QTZ'; BaseTipo = -1; PrctjeImpuesto = 12
       TotalImp = $im; PrctjeDesc = 0; TotalDesc = 0; TotalBruto = $br; TotalDoc = $tot; Comentario = $null; Lineas = @($lineas) }
}
# Líneas de la orden: los montos los calcula el servidor, solo viajan los datos de entrada.
function LineaOrden($cod, $cant, $precio) { @{ CodArticulo = $cod; Cantidad = $cant; Precio = $precio; PrctjeDesc = 0; CodigoImpuesto = 'IVA' } }
function CabOrden($serie, $moto, $lineas) {
    @{ Serie = $serie; FechaDoc = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss'); CodigoSn = 'CL-0001'; NombreSn = 'Cliente Smoke'; Direccion = 'Sede'
       MonedaDoc = 'QTZ'; MotoEntry = $moto; CodMecanico = 'M01'; KmIngreso = 12000; FallaReportada = 'No enciende'; Lineas = @($lineas) }
}

# ---------------------------------------------------------------- sesión
$login = Api POST 'api/Auth/login' @{ usuario = 'admin'; contrasena = 'Admin123!' } -SinToken
$script:Token = $login.Dato.token
Esperar 'login' ($login.Status -eq 200 -and [bool]$script:Token)
Esperar 'sin token -> 401' ((Api GET 'api/OrdenServicio' -SinToken).Status -eq 401)

# ---------------------------------------------------------------- datos de prueba
function GrupoSn([string]$tipo) {
    $g = (Api GET 'api/GrupoSN').Dato | Where-Object { $_.tipoGrupo -eq $tipo } | Select-Object -First 1
    if ($g) { return $g.entry }
    (Api POST 'api/GrupoSN' @{ Nombre = "Smoke $tipo"; TipoGrupo = $tipo }).Dato
}
$grupoArt = ((Api GET 'api/GrupoArticulo').Dato | Select-Object -First 1).codigo
if (-not $grupoArt) { $grupoArt = (Api POST 'api/GrupoArticulo' @{ Nombre = 'Smoke' }).Dato }

$null = Api POST 'api/SocioNegocio' @{ Codigo = 'CL-0001'; Nombre = 'Cliente Smoke'; TipoSn = 'C'; GrupoSn = (GrupoSn 'C'); Activo = 'S'; Serie = (Serie '1' 'C' 'S') }
$null = Api POST 'api/SocioNegocio' @{ Codigo = 'PR-0001'; Nombre = 'Proveedor Smoke'; TipoSn = 'P'; GrupoSn = (GrupoSn 'P'); Activo = 'S'; Serie = (Serie '1' 'P' 'S') }
$null = Api POST 'api/Articulo' @{ Codigo = 'ACE-001'; Nombre = 'Aceite 20W50'; CodigoGrupo = $grupoArt; Activo = 'S'; ArticuloCompra = 'S'; ArticuloVenta = 'S'
    ArticuloInventario = 'S'; PrecioUnitario = 0; CantDisponible = 0; CantConfirmada = 0; CantPedida = 0; AlmacenDefecto = '01'; Serie = (Serie '2' '--' 'S') }
$null = Api POST 'api/Articulo' @{ Codigo = 'MO-001'; Nombre = 'Mano de obra'; CodigoGrupo = $grupoArt; Activo = 'S'; ArticuloCompra = 'N'; ArticuloVenta = 'S'
    ArticuloInventario = 'N'; PrecioUnitario = 0; CantDisponible = 0; CantConfirmada = 0; CantPedida = 0; AlmacenDefecto = '01'; Serie = (Serie '2' '--' 'S') }
$compra = Api POST 'api/FacturaCompra' (Cab (Serie '13') 'PR-0001' 'Proveedor Smoke' @((Linea 'ACE-001' 'Aceite' 20 50)))
Esperar 'compra inicial (stock 20)' ($compra.Status -eq 200 -and (Existencia 'ACE-001') -eq 20) $compra.Mensaje

$mec = Api POST 'api/Mecanico' @{ Codigo = 'M01'; Nombre = 'Juan Pérez'; Telefono = '5555-1111' }
Esperar 'mecánico creado' ($mec.Status -eq 200) $mec.Mensaje
$motoRes = Api POST 'api/Motocicleta' @{ CodigoSn = 'CL-0001'; Placa = 'm123abc'; Marca = 'Honda'; Modelo = 'CB125'; Anio = 2022 }
$moto = $motoRes.Dato
Esperar 'motocicleta creada (placa normalizada)' ($motoRes.Status -eq 200 -and $moto -gt 0 -and (Api GET "api/Motocicleta/$moto").Dato.placa -eq 'M123ABC') $motoRes.Mensaje
Esperar 'placa duplicada rechazada' ((Api POST 'api/Motocicleta' @{ CodigoSn = 'CL-0001'; Placa = 'M123ABC' }).Status -eq 400)

$serie = Serie '70'
Esperar 'la serie 70 existe (sembrada por el script)' ([bool]$serie)

# ---------------------------------------------------------------- A. alta con insumo y servicio
$ordA = Api POST 'api/OrdenServicio' (CabOrden $serie $moto @((LineaOrden 'ACE-001' 4 85), (LineaOrden 'MO-001' 1 150)))
$ord1 = $ordA.Dato
Esperar 'orden 1 creada con un insumo (4) y un servicio' ($ordA.Status -eq 200 -and $ord1 -gt 0) $ordA.Mensaje
Esperar 'el insumo descontó: 20 -> 16' ((Existencia 'ACE-001') -eq 16)
$o1 = (Api GET "api/OrdenServicio/$ord1").Dato
Esperar 'totales del servidor: 548.80, estado R, NumDoc 1, placa y mecánico' ($o1.totalDoc -eq 548.8 -and $o1.estadoServicio -eq 'R' -and $o1.numDoc -eq 1 -and $o1.placa -eq 'M123ABC' -and $o1.nombreMecanico -eq 'Juan Pérez') "$($o1 | ConvertTo-Json -Compress)"
$lin = (Api GET "api/OrdenServicio/$ord1/Lineas").Dato
Esperar 'línea 1 es insumo con almacén y la 2 es servicio' ($lin[0].esInsumo -eq 'S' -and $lin[0].codAlmacen -eq '01' -and $lin[1].esInsumo -eq 'N')

# ---------------------------------------------------------------- B. stock insuficiente
$exceso = Api POST 'api/OrdenServicio' (CabOrden $serie $moto @((LineaOrden 'ACE-001' 100 85)))
Esperar 'stock insuficiente rechazado (400)' ($exceso.Status -eq 400) $exceso.Mensaje
Esperar 'el rechazo no dejó movimientos: stock 16 y una sola orden' ((Existencia 'ACE-001') -eq 16 -and @((Api GET 'api/OrdenServicio').Dato).Count -eq 1)
$ord2Res = Api POST 'api/OrdenServicio' (CabOrden $serie $moto @((LineaOrden 'MO-001' 1 100)))
$ord2 = $ord2Res.Dato
Esperar 'el rechazo no consumió consecutivo (la siguiente es la 2)' ($ord2Res.Status -eq 200 -and (Api GET "api/OrdenServicio/$ord2").Dato.numDoc -eq 2) $ord2Res.Mensaje

# ---------------------------------------------------------------- C. líneas sueltas con inventario
$nl = (Api POST 'api/OrdenServicioDetalle' @{ Entry = $ord1; CodArticulo = 'ACE-001'; Cantidad = 2; Precio = 85; PrctjeDesc = 0; CodigoImpuesto = 'IVA' }).Dato
Esperar 'agregar insumo x2: 16 -> 14' ($nl -eq 3 -and (Existencia 'ACE-001') -eq 14)
$r = Api PUT "api/OrdenServicioDetalle/$ord1/$nl" @{ CodArticulo = 'ACE-001'; Cantidad = 3; Precio = 85; PrctjeDesc = 0; CodigoImpuesto = 'IVA' }
Esperar 'cambiar a x3: 14 -> 13' ($r.Status -eq 200 -and (Existencia 'ACE-001') -eq 13) $r.Mensaje
$r = Api PUT "api/OrdenServicioDetalle/$ord1/$nl" @{ CodArticulo = 'ACE-001'; Cantidad = 999; Precio = 85; PrctjeDesc = 0; CodigoImpuesto = 'IVA' }
Esperar 'cambiar a x999 rechazado y la reversa se deshace: sigue en 13' ($r.Status -eq 400 -and (Existencia 'ACE-001') -eq 13) $r.Mensaje
$r = Api DELETE "api/OrdenServicioDetalle/$ord1/$nl"
Esperar 'quitar la línea devuelve el insumo: 13 -> 16' ($r.Status -eq 200 -and (Existencia 'ACE-001') -eq 16) $r.Mensaje
Esperar 'los totales vuelven a 548.80' ((Api GET "api/OrdenServicio/$ord1").Dato.totalDoc -eq 548.8)

# ---------------------------------------------------------------- D. estados
$r = Api POST "api/OrdenServicio/$ord1/Estado" @{ Estado = 'P' }
Esperar 'R -> P' ($r.Status -eq 200) $r.Mensaje
$r = Api POST "api/OrdenServicio/$ord1/Estado" @{ Estado = 'R' }
Esperar 'P -> R rechazado ("solo avanza")' ($r.Status -eq 400 -and $r.Mensaje -like '*la orden solo avanza*') $r.Mensaje

# ---------------------------------------------------------------- E. facturar (Copiar a)
$b = Api GET "api/DocumentoBase/70/$ord2/Abierto?destino=6"
Esperar 'una orden Recibida no se factura (400)' ($b.Status -eq 400 -and $b.Mensaje -like '*debe estar Terminada o Entregada*') $b.Mensaje
$r = Api POST "api/OrdenServicio/$ord1/Estado" @{ Estado = 'T' }
Esperar 'P -> T' ($r.Status -eq 200) $r.Mensaje
$b = Api GET "api/DocumentoBase/70/$ord1/Abierto?destino=6"
Esperar 'borrador de la orden Terminada: 2 líneas abiertas (4 y 1)' ($b.Status -eq 200 -and $b.Dato.lineas.Count -eq 2 -and $b.Dato.lineas[0].cantidadAbierta -eq 4 -and $b.Dato.lineas[1].cantidadAbierta -eq 1) $b.Mensaje
Esperar 'orden -> pedido no es una copia válida (400)' ((Api GET "api/DocumentoBase/70/$ord1/Abierto?destino=4").Status -eq 400)

$fac = Api POST 'api/Factura' (Cab (Serie '6') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ACE-001' 'Aceite' 4 85 70 $ord1 1), (LineaBase 'MO-001' 'Mano de obra' 1 150 70 $ord1 2)))
Esperar 'factura desde la orden: NO vuelve a descontar (sigue en 16)' ($fac.Status -eq 200 -and (Existencia 'ACE-001') -eq 16) $fac.Mensaje
$exceso = Api POST 'api/Factura' (Cab (Serie '6') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ACE-001' 'Aceite' 1 85 70 $ord1 1)))
Esperar 'copiar de más rechazado (excede la cantidad abierta (0))' ($exceso.Status -eq 400 -and $exceso.Mensaje -like '*excede la cantidad abierta (0)*') $exceso.Mensaje
$b = Api GET "api/DocumentoBase/70/$ord1/Abierto?destino=6"
Esperar 'la orden ya no tiene líneas abiertas' ($b.Status -eq 400 -and $b.Mensaje -like '*no tiene líneas abiertas*') $b.Mensaje

# ---------------------------------------------------------------- F. integridad del origen
$r = Api POST "api/OrdenServicio/$ord1/Cancelar"
Esperar 'cancelar la orden facturada = 400 (nombra la factura)' ($r.Status -eq 400 -and $r.Mensaje -like '*lo usan Factura*') $r.Mensaje
$r = Api PUT "api/OrdenServicioDetalle/$ord1/1" @{ CodArticulo = 'ACE-001'; Cantidad = 1; Precio = 85; PrctjeDesc = 0; CodigoImpuesto = 'IVA' }
Esperar 'modificar la línea 1 facturada = 400' ($r.Status -eq 400 -and $r.Mensaje -like 'No se puede modificar la línea 1*') $r.Mensaje
$r = Api DELETE "api/OrdenServicioDetalle/$ord1/1"
Esperar 'quitar la línea 1 facturada = 400' ($r.Status -eq 400 -and $r.Mensaje -like 'No se puede eliminar la línea 1*') $r.Mensaje
$nuevaLinea = (Api POST 'api/OrdenServicioDetalle' @{ Entry = $ord1; CodArticulo = 'ACE-001'; Cantidad = 1; Precio = 85; PrctjeDesc = 0; CodigoImpuesto = 'IVA' }).Dato
$b = Api GET "api/DocumentoBase/70/$ord1/Abierto?destino=6"
Esperar 'agregar una línea nueva a la orden facturada es válido y queda abierta (descuenta: 15)' ($nuevaLinea -eq 3 -and (Existencia 'ACE-001') -eq 15 -and $b.Status -eq 200 -and $b.Dato.lineas.Count -eq 1 -and $b.Dato.lineas[0].baseLinea -eq 3)
$r = Api DELETE "api/OrdenServicioDetalle/$ord1/$nuevaLinea"
Esperar 'quitar esa línea (aún sin facturar) devuelve el insumo: 16' ($r.Status -eq 200 -and (Existencia 'ACE-001') -eq 16) $r.Mensaje

# ---------------------------------------------------------------- G. liberar al cancelar la factura y cancelar la orden
$r = Api PUT "api/Factura/$($fac.Dato)" @{ Cancelado = 'S' }
Esperar 'cancelar la factura = 200 y el stock no cambia (16)' ($r.Status -eq 200 -and (Existencia 'ACE-001') -eq 16) $r.Mensaje
$b = Api GET "api/DocumentoBase/70/$ord1/Abierto?destino=6"
Esperar 'la cantidad vuelve a quedar abierta (4 y 1)' ($b.Status -eq 200 -and $b.Dato.lineas[0].cantidadAbierta -eq 4 -and $b.Dato.lineas[1].cantidadAbierta -eq 1)
$r = Api POST "api/OrdenServicio/$ord1/Cancelar"
Esperar 'ahora la orden se cancela y devuelve los insumos: 20' ($r.Status -eq 200 -and (Existencia 'ACE-001') -eq 20 -and (Api GET "api/OrdenServicio/$ord1").Dato.cancelado -eq 'S') $r.Mensaje
$r = Api POST "api/OrdenServicioDetalle" @{ Entry = $ord1; CodArticulo = 'ACE-001'; Cantidad = 1; Precio = 85; CodigoImpuesto = 'IVA' }
Esperar 'una orden cancelada no acepta líneas' ($r.Status -eq 400 -and $r.Mensaje -like '*cancelada*') $r.Mensaje

# ---------------------------------------------------------------- H. entregar sin facturar
$r = Api POST "api/OrdenServicio/$ord2/Estado" @{ Estado = 'E' }
Esperar 'R -> E (se entrega sin facturar)' ($r.Status -eq 200 -and (Api GET "api/OrdenServicio/$ord2").Dato.fechaEntrega) $r.Mensaje
$r = Api POST 'api/OrdenServicioDetalle' @{ Entry = $ord2; CodArticulo = 'MO-001'; Cantidad = 1; Precio = 10; CodigoImpuesto = 'IVA' }
Esperar 'una orden entregada no acepta líneas' ($r.Status -eq 400 -and $r.Mensaje -like '*ya fue entregada*') $r.Mensaje
$r = Api POST "api/OrdenServicio/$ord2/Cancelar"
Esperar 'una orden entregada no se cancela' ($r.Status -eq 400 -and $r.Mensaje -like '*entregada no se puede cancelar*') $r.Mensaje

# ---------------------------------------------------------------- I. historial y filtros
$h = Api GET "api/Motocicleta/$moto/Historial"
Esperar 'historial de la moto: 2 órdenes, la más nueva primero' ($h.Status -eq 200 -and $h.Dato.Count -eq 2 -and $h.Dato[0].entry -eq $ord2)
Esperar 'filtro por estado' (@((Api GET 'api/OrdenServicio?estado=E').Dato).Count -eq 1)

Write-Host ''
if ($script:Fallos -gt 0) { Write-Host "$script:Fallos verificación(es) fallaron" -ForegroundColor Red; exit 1 }
Write-Host 'Todas las verificaciones pasaron' -ForegroundColor Green
```
Expected: todas las líneas `OK`. Cualquier `FALLA` se diagnostica y se corrige en la tarea que corresponda (no se da por buena la verificación con fallas abiertas). Nota: si el mensaje de `StockInsuficienteException` o la forma exacta de una respuesta difiere de lo asumido en una comprobación `-like`, ajustar solo esa comprobación del script (no el código) tras verificar el comportamiento real.

- [ ] **Step 5: Limpieza (siempre)** — detener la API por su PID (`Stop-Process -Id $api.Id`), borrar las variables de entorno de la sesión (`Remove-Item Env:ConnectionStrings__API_DB, Env:ASPNETCORE_URLS, Env:ASPNETCORE_ENVIRONMENT`), eliminar la base (`ALTER DATABASE API_DB_TALLER_A SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE API_DB_TALLER_A;`) y comprobar que `SELECT name FROM sys.databases WHERE name LIKE 'API_DB%'` devuelve solo `API_DB_TEST`, que el puerto 5199 no tiene listener y que `git status --short` solo muestra `appsettings.json` y `sql/crear-api-db-test-completo.sql`.

- [ ] **Step 6: Commit** (solo el script completo):

```bash
git add sql/crear-api-db-test-completo.sql
git commit -m "docs(db): script completo de creacion de API_DB_TEST incluye el modulo de taller

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---
## Fase 4 — Web: menú Taller, catálogos y órdenes de servicio

> La Web no tiene proyecto de pruebas: cada tarea se verifica con `dotnet build Web.slnx` (0 errores y 0 advertencias **nuevas**; las 4 advertencias de los archivos ajenos no cuentan), `node --check` de cada JS tocado y, al final, el recorrido en el navegador de la Task 17. Los roles que usen órdenes necesitan además `Ver` en `Mecanicos` y `Motocicletas` (y `Agregar` en `Motocicletas` para registrar una moto desde la orden): la Web llama a la API con el token del usuario y la API exige el permiso de cada módulo.

### Task 12: Clientes tipados, DTOs y ajustes de `site.js` del taller

**Precondición:** Task 11 completa (la API del taller está lista). En la Web: `git status --short` solo con los 4 archivos ajenos; `grep -n "Mecanico\|Motocicleta\|OrdenServicio" Web.UI/Program.cs` sin resultados. Antes de editar `Web.UI/wwwroot/js/site.js` y `Web.UI/Program.cs`: `file` de cada uno para conservar el fin de línea.

**Files:**
- Create: `Web.ApiClient/Dtos/Mecanico/{MecanicoDTO,MecanicoCrearDTO,MecanicoActualizarDTO}.cs`
- Create: `Web.ApiClient/Dtos/Motocicleta/{MotocicletaDTO,MotocicletaCrearDTO,MotocicletaActualizarDTO}.cs`
- Create: `Web.ApiClient/Dtos/OrdenServicio/{OrdenServicioDTO,OrdenServicioCrearDTO,OrdenServicioActualizarDTO,OrdenServicioEstadoDTO,OrdenServicioDetalleDTO,OrdenServicioDetalleCrearDTO,OrdenServicioDetalleActualizarDTO}.cs`
- Create: `Web.ApiClient/Clientes/{IMecanicoApiClient,MecanicoApiClient,IMotocicletaApiClient,MotocicletaApiClient,IOrdenServicioApiClient,OrdenServicioApiClient,IOrdenServicioDetalleApiClient,OrdenServicioDetalleApiClient}.cs`
- Modify: `Web.UI/Program.cs`, `Web.UI/wwwroot/js/site.js`

**Interfaces:**
- Produces: `IMecanicoApiClient { ObtenerTodoAsync(); ObtenerAsync(string codigo); InsertarAsync(MecanicoCrearDTO); ActualizarAsync(string codigo, MecanicoActualizarDTO); EliminarAsync(string codigo) }`; `IMotocicletaApiClient { BuscarAsync(string? codigoSn, string? placa); ObtenerAsync(int entry); InsertarAsync(MotocicletaCrearDTO) → Respuesta<int>; ActualizarAsync(int, MotocicletaActualizarDTO); EliminarAsync(int); ObtenerHistorialAsync(int) → IEnumerable<OrdenServicioDTO> }`; `IOrdenServicioApiClient { ObtenerTodoAsync(string? estado = null); ObtenerAsync(int); InsertarAsync(OrdenServicioCrearDTO) → Respuesta<int>; ActualizarAsync(int, OrdenServicioActualizarDTO); CambiarEstadoAsync(int, string); CancelarAsync(int); ObtenerLineasAsync(int) }`; `IOrdenServicioDetalleApiClient { InsertarAsync(OrdenServicioDetalleCrearDTO) → Respuesta<int>; ActualizarAsync(int entry, int noLinea, OrdenServicioDetalleActualizarDTO); EliminarAsync(int, int) }`; en `site.js`: `App.autocompletar` acepta `opciones.parametros: () => object` (parámetros extra de cada búsqueda) y `App.etiquetaOrigen` / `App.lineasDesdeBorrador` nombran el tipo 70 como "Orden de servicio".

- [ ] **Step 1: DTOs** (namespaces `Web.ApiClient.Dtos.Mecanico`, `…Motocicleta`, `…OrdenServicio`; archivos nuevos). Los nombres de propiedad coinciden con los de la API (JSON camelCase, deserialización sin distinguir mayúsculas).

`Mecanico/MecanicoDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Mecanico
{
    public class MecanicoDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string Activo { get; set; } = null!;
    }
}
```
`Mecanico/MecanicoCrearDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Mecanico
{
    public class MecanicoCrearDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Activo { get; set; }
    }
}
```
`Mecanico/MecanicoActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Mecanico
{
    public class MecanicoActualizarDTO
    {
        public string Nombre { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Activo { get; set; }
    }
}
```

`Motocicleta/MotocicletaDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Motocicleta
{
    public class MotocicletaDTO
    {
        public int Entry { get; set; }
        public string CodigoSn { get; set; } = null!;
        public string? NombreSn { get; set; }
        public string Placa { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public short? Anio { get; set; }
        public string? Color { get; set; }
        public string? Vin { get; set; }
        public string? Comentario { get; set; }
        public string Activo { get; set; } = null!;
    }
}
```
`Motocicleta/MotocicletaCrearDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.Motocicleta
{
    public class MotocicletaCrearDTO
    {
        public string CodigoSn { get; set; } = null!;
        public string Placa { get; set; } = null!;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public short? Anio { get; set; }
        public string? Color { get; set; }
        public string? Vin { get; set; }
        public string? Comentario { get; set; }
        public string? Activo { get; set; }
    }
}
```
`Motocicleta/MotocicletaActualizarDTO.cs`: idéntica a `MotocicletaCrearDTO` con la clase `MotocicletaActualizarDTO`.

`OrdenServicio/OrdenServicioDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.OrdenServicio
{
    public class OrdenServicioDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? Placa { get; set; }
        public string? MotoDescripcion { get; set; }
        public string? CodMecanico { get; set; }
        public string? NombreMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string EstadoServicio { get; set; } = null!;
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```
`OrdenServicio/OrdenServicioCrearDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.OrdenServicio
{
    public class OrdenServicioCrearDTO
    {
        // Requerido solo para series "Manual": para series autogeneradas la API calcula el siguiente número.
        public int? NumDoc { get; set; }
        public int Serie { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? CodMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string? Comentario { get; set; }
        public List<OrdenServicioDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
```
`OrdenServicio/OrdenServicioActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.OrdenServicio
{
    public class OrdenServicioActualizarDTO
    {
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaPromesa { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int MotoEntry { get; set; }
        public string? CodMecanico { get; set; }
        public int? KmIngreso { get; set; }
        public string? FallaReportada { get; set; }
        public string? Diagnostico { get; set; }
        public string? Observaciones { get; set; }
        public string? Comentario { get; set; }
    }
}
```
`OrdenServicio/OrdenServicioEstadoDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.OrdenServicio
{
    public class OrdenServicioEstadoDTO
    {
        public string Estado { get; set; } = null!;
    }
}
```
`OrdenServicio/OrdenServicioDetalleDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.OrdenServicio
{
    public class OrdenServicioDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public string? CodAlmacen { get; set; }
        public string? EsInsumo { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? CodMecanico { get; set; }
        public string? TipoObjeto { get; set; }
    }
}
```
`OrdenServicio/OrdenServicioDetalleCrearDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.OrdenServicio
{
    public class OrdenServicioDetalleCrearDTO
    {
        // Entry solo lo usa POST api/OrdenServicioDetalle; dentro de OrdenServicioCrearDTO.Lineas lo asigna el servidor.
        public int Entry { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public string? CodAlmacen { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public string? CodMecanico { get; set; }
    }
}
```
`OrdenServicio/OrdenServicioDetalleActualizarDTO.cs`:
```csharp
namespace Web.ApiClient.Dtos.OrdenServicio
{
    public class OrdenServicioDetalleActualizarDTO
    {
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public string? CodAlmacen { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public string? CodMecanico { get; set; }
    }
}
```

- [ ] **Step 2: Clientes tipados** (patrón de `AlmacenApiClient`; archivos nuevos).

`Clientes/IMecanicoApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Mecanico;

namespace Web.ApiClient.Clientes
{
    public interface IMecanicoApiClient
    {
        Task<Respuesta<IEnumerable<MecanicoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<MecanicoDTO>> ObtenerAsync(string codigo);
        Task<Respuesta<bool>> InsertarAsync(MecanicoCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, MecanicoActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
    }
}
```
`Clientes/MecanicoApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Mecanico;

namespace Web.ApiClient.Clientes
{
    public class MecanicoApiClient : ApiClientBase, IMecanicoApiClient
    {
        private const string Recurso = "api/Mecanico";

        public MecanicoApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<MecanicoDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<MecanicoDTO>>(Recurso);

        public Task<Respuesta<MecanicoDTO>> ObtenerAsync(string codigo) =>
            GetAsync<MecanicoDTO>($"{Recurso}/{Uri.EscapeDataString(codigo)}");

        public Task<Respuesta<bool>> InsertarAsync(MecanicoCrearDTO dto) =>
            PostAsync<bool>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(string codigo, MecanicoActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{Uri.EscapeDataString(codigo)}", dto);

        public Task<Respuesta<bool>> EliminarAsync(string codigo) =>
            DeleteAsync<bool>($"{Recurso}/{Uri.EscapeDataString(codigo)}");
    }
}
```
`Clientes/IMotocicletaApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Motocicleta;
using Web.ApiClient.Dtos.OrdenServicio;

namespace Web.ApiClient.Clientes
{
    public interface IMotocicletaApiClient
    {
        Task<Respuesta<IEnumerable<MotocicletaDTO>>> BuscarAsync(string? codigoSn, string? placa);
        Task<Respuesta<MotocicletaDTO>> ObtenerAsync(int entry);
        Task<Respuesta<int>> InsertarAsync(MotocicletaCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, MotocicletaActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry);
        Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerHistorialAsync(int entry);
    }
}
```
`Clientes/MotocicletaApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Motocicleta;
using Web.ApiClient.Dtos.OrdenServicio;

namespace Web.ApiClient.Clientes
{
    public class MotocicletaApiClient : ApiClientBase, IMotocicletaApiClient
    {
        private const string Recurso = "api/Motocicleta";

        public MotocicletaApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<MotocicletaDTO>>> BuscarAsync(string? codigoSn, string? placa)
        {
            var consulta = new List<string>();
            if (!string.IsNullOrWhiteSpace(codigoSn)) consulta.Add($"codigoSn={Uri.EscapeDataString(codigoSn)}");
            if (!string.IsNullOrWhiteSpace(placa)) consulta.Add($"placa={Uri.EscapeDataString(placa)}");
            var url = consulta.Count == 0 ? Recurso : $"{Recurso}?{string.Join("&", consulta)}";
            return GetAsync<IEnumerable<MotocicletaDTO>>(url);
        }

        public Task<Respuesta<MotocicletaDTO>> ObtenerAsync(int entry) =>
            GetAsync<MotocicletaDTO>($"{Recurso}/{entry}");

        public Task<Respuesta<int>> InsertarAsync(MotocicletaCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, MotocicletaActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry) =>
            DeleteAsync<bool>($"{Recurso}/{entry}");

        public Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerHistorialAsync(int entry) =>
            GetAsync<IEnumerable<OrdenServicioDTO>>($"{Recurso}/{entry}/Historial");
    }
}
```
`Clientes/IOrdenServicioApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.OrdenServicio;

namespace Web.ApiClient.Clientes
{
    public interface IOrdenServicioApiClient
    {
        Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerTodoAsync(string? estado = null);
        Task<Respuesta<OrdenServicioDTO>> ObtenerAsync(int entry);
        Task<Respuesta<int>> InsertarAsync(OrdenServicioCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, OrdenServicioActualizarDTO dto);
        Task<Respuesta<bool>> CambiarEstadoAsync(int entry, string estado);
        Task<Respuesta<bool>> CancelarAsync(int entry);
        Task<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>> ObtenerLineasAsync(int entry);
    }
}
```
`Clientes/OrdenServicioApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.OrdenServicio;

namespace Web.ApiClient.Clientes
{
    public class OrdenServicioApiClient : ApiClientBase, IOrdenServicioApiClient
    {
        private const string Recurso = "api/OrdenServicio";

        public OrdenServicioApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<OrdenServicioDTO>>> ObtenerTodoAsync(string? estado = null) =>
            GetAsync<IEnumerable<OrdenServicioDTO>>(string.IsNullOrWhiteSpace(estado) ? Recurso : $"{Recurso}?estado={Uri.EscapeDataString(estado)}");

        public Task<Respuesta<OrdenServicioDTO>> ObtenerAsync(int entry) =>
            GetAsync<OrdenServicioDTO>($"{Recurso}/{entry}");

        public Task<Respuesta<int>> InsertarAsync(OrdenServicioCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, OrdenServicioActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}", dto);

        public Task<Respuesta<bool>> CambiarEstadoAsync(int entry, string estado) =>
            PostAsync<bool>($"{Recurso}/{entry}/Estado", new OrdenServicioEstadoDTO { Estado = estado });

        public Task<Respuesta<bool>> CancelarAsync(int entry) =>
            PostAsync<bool>($"{Recurso}/{entry}/Cancelar", new { });

        public Task<Respuesta<IEnumerable<OrdenServicioDetalleDTO>>> ObtenerLineasAsync(int entry) =>
            GetAsync<IEnumerable<OrdenServicioDetalleDTO>>($"{Recurso}/{entry}/Lineas");
    }
}
```
`Clientes/IOrdenServicioDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.OrdenServicio;

namespace Web.ApiClient.Clientes
{
    public interface IOrdenServicioDetalleApiClient
    {
        Task<Respuesta<int>> InsertarAsync(OrdenServicioDetalleCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalleActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea);
    }
}
```
`Clientes/OrdenServicioDetalleApiClient.cs`:
```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.OrdenServicio;

namespace Web.ApiClient.Clientes
{
    public class OrdenServicioDetalleApiClient : ApiClientBase, IOrdenServicioDetalleApiClient
    {
        private const string Recurso = "api/OrdenServicioDetalle";

        public OrdenServicioDetalleApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<int>> InsertarAsync(OrdenServicioDetalleCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, int noLinea, OrdenServicioDetalleActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}/{noLinea}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry, int noLinea) =>
            DeleteAsync<bool>($"{Recurso}/{entry}/{noLinea}");
    }
}
```

- [ ] **Step 3: Registro en `Web.UI/Program.cs`** — tras las líneas de `IDocumentoBaseApiClient`:
```csharp
builder.Services.AddHttpClient<IMecanicoApiClient, MecanicoApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IMotocicletaApiClient, MotocicletaApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IOrdenServicioApiClient, OrdenServicioApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IOrdenServicioDetalleApiClient, OrdenServicioDetalleApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 4: `site.js`** (conservar su fin de línea):

a) En `etiquetaOrigen`, el mapa pasa a `const nombres = { 3: 'Cotización', 4: 'Pedido', 5: 'Entrega', 70: 'Orden de servicio' };`.

b) En `lineasDesdeBorrador`, el mapa pasa a `const nombres = { 3: 'Cotización', 4: 'Pedido', 5: 'Entrega', 6: 'Factura', 70: 'Orden de servicio' };`.

c) En `autocompletar`: en la documentación de parámetros agregar
```js
     * @param {() => object} [opciones.parametros] - parámetros extra que se envían en cada búsqueda además de `texto`
     *        (p. ej. { tipo: 'I' }); se evalúa en cada petición, así que puede depender del estado del formulario.
```
y tras `const requerido = opciones.requerido ?? true;` agregar `const parametros = opciones.parametros || (() => ({}));`; en `buscar`, reemplazar `const respuesta = await $.get(endpoint, { texto });` por:
```js
                const respuesta = await $.get(endpoint, { texto, ...parametros() });
```

- [ ] **Step 5: Verificar** — `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` (PowerShell) → 0 errores; `node --check Web.UI/wwwroot/js/site.js` → sin salida.

- [ ] **Step 6: Commit**

```bash
git add Web.ApiClient/Dtos/Mecanico Web.ApiClient/Dtos/Motocicleta Web.ApiClient/Dtos/OrdenServicio Web.ApiClient/Clientes/IMecanicoApiClient.cs Web.ApiClient/Clientes/MecanicoApiClient.cs Web.ApiClient/Clientes/IMotocicletaApiClient.cs Web.ApiClient/Clientes/MotocicletaApiClient.cs Web.ApiClient/Clientes/IOrdenServicioApiClient.cs Web.ApiClient/Clientes/OrdenServicioApiClient.cs Web.ApiClient/Clientes/IOrdenServicioDetalleApiClient.cs Web.ApiClient/Clientes/OrdenServicioDetalleApiClient.cs Web.UI/Program.cs Web.UI/wwwroot/js/site.js
git commit -m "feat(taller): clientes de la API y DTOs de mecanicos, motocicletas y ordenes de servicio en la Web

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Menú Taller y pantalla de Mecánicos

**Precondición:** Task 12 completa. `grep -n "Taller" Web.UI/Views/Shared/_Layout.cshtml` sin resultados. `file` de `_Layout.cshtml` antes de editarlo.

**Files:**
- Create: `Web.UI/Controllers/MecanicosController.cs`, `Web.UI/Views/Mecanicos/Index.cshtml`, `Web.UI/Views/Mecanicos/_Form.cshtml`, `Web.UI/wwwroot/js/mecanicos.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `IMecanicoApiClient`, `App.*` (site.js), `RequierePermiso`/`User.TienePermiso` (módulo `Mecanicos`).
- Produces: pantalla `/Mecanicos` (listado, alta, edición y desactivación) y el grupo de menú **Taller** (con el enlace de Mecánicos; las Tasks 14 y 15 agregan los otros dos).

- [ ] **Step 1: Controlador** — `Web.UI/Controllers/MecanicosController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.Mecanico;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class MecanicosController : Controller
    {
        private readonly IMecanicoApiClient _mecanicos;

        public MecanicosController(IMecanicoApiClient mecanicos)
        {
            _mecanicos = mecanicos;
        }

        [RequierePermiso("Mecanicos", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("Mecanicos", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _mecanicos.ObtenerTodoAsync();
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("Mecanicos", Accion.Agregar)]
        public IActionResult FormularioCrear()
        {
            ViewBag.EsEdicion = false;
            return PartialView("_Form", new MecanicoCrearDTO { Activo = "S" });
        }

        [HttpGet]
        [RequierePermiso("Mecanicos", Accion.Actualizar)]
        public async Task<IActionResult> FormularioEditar(string codigo)
        {
            var respuesta = await _mecanicos.ObtenerAsync(codigo);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            ViewBag.EsEdicion = true;
            var dto = new MecanicoCrearDTO
            {
                Codigo = respuesta.Dato.Codigo,
                Nombre = respuesta.Dato.Nombre,
                Telefono = respuesta.Dato.Telefono,
                Activo = respuesta.Dato.Activo
            };
            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("Mecanicos", Accion.Agregar)]
        public async Task<IActionResult> Crear([FromBody] MecanicoCrearDTO dto)
        {
            var respuesta = await _mecanicos.InsertarAsync(dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("Mecanicos", Accion.Actualizar)]
        public async Task<IActionResult> Editar(string codigo, [FromBody] MecanicoCrearDTO dto)
        {
            var actualizar = new MecanicoActualizarDTO { Nombre = dto.Nombre, Telefono = dto.Telefono, Activo = dto.Activo };
            var respuesta = await _mecanicos.ActualizarAsync(codigo, actualizar);
            return Json(respuesta);
        }

        // Baja lógica: el mecánico pasa a inactivo (puede tener órdenes de servicio).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("Mecanicos", Accion.Eliminar)]
        public async Task<IActionResult> Eliminar(string codigo)
        {
            var respuesta = await _mecanicos.EliminarAsync(codigo);
            return Json(respuesta);
        }
    }
}
```

- [ ] **Step 2: Vistas**.

`Web.UI/Views/Mecanicos/Index.cshtml`:
```razor
@{
    ViewData["Title"] = "Mecánicos";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Mecánicos</h3>
    @if (User.TienePermiso("Mecanicos", Accion.Agregar))
    {
        <button type="button" class="btn btn-primary" id="btnNuevo">
            <i class="fa-solid fa-plus me-1"></i>Nuevo
        </button>
    }
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblMecanicos" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>Código</th>
                        <th>Nombre</th>
                        <th>Teléfono</th>
                        <th>Activo</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalFormulario" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-dialog-scrollable">
        <div class="modal-content" id="contenidoModal">
            <!-- se carga por AJAX -->
        </div>
    </div>
</div>

<script id="datosPermisosMecanicos" type="application/json">
    { "actualizar": @(User.TienePermiso("Mecanicos", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("Mecanicos", Accion.Eliminar) ? "true" : "false") }
</script>

@section Scripts {
    <script src="~/js/mecanicos.js" asp-append-version="true"></script>
}
```

`Web.UI/Views/Mecanicos/_Form.cshtml`:
```razor
@model Web.ApiClient.Dtos.Mecanico.MecanicoCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? "Editar mecánico" : "Nuevo mecánico")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formMecanico" novalidate>
        <div class="row g-3">
            <div class="col-md-4">
                <label asp-for="Codigo" class="form-label"></label>
                <input asp-for="Codigo" class="form-control" readonly="@esEdicion" maxlength="10" />
            </div>
            <div class="col-md-8">
                <label asp-for="Nombre" class="form-label"></label>
                <input asp-for="Nombre" class="form-control" maxlength="100" />
            </div>
            <div class="col-md-8">
                <label asp-for="Telefono" class="form-label">Teléfono</label>
                <input asp-for="Telefono" class="form-control" maxlength="30" />
            </div>
            <div class="col-md-4">
                <label asp-for="Activo" class="form-label"></label>
                <select asp-for="Activo" class="form-select">
                    <option value="S">Sí</option>
                    <option value="N">No</option>
                </select>
            </div>
        </div>
    </form>
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
    <button type="button" class="btn btn-primary" id="btnGuardarMecanico" data-edicion="@esEdicion.ToString().ToLower()" data-codigo="@Model.Codigo">
        <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
    </button>
</div>
```

- [ ] **Step 3: JS** — `Web.UI/wwwroot/js/mecanicos.js`:

```js
$(function () {
    const datosPermisosEl = document.getElementById('datosPermisosMecanicos');
    const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true };
    const tabla = $('#tblMecanicos').DataTable({
        ajax: { url: '/Mecanicos/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'codigo' },
            { data: 'nombre' },
            { data: 'telefono', render: d => d || '' },
            { data: 'activo', render: d => d === 'S' ? '<span class="badge text-bg-success">Sí</span>' : '<span class="badge text-bg-secondary">No</span>' },
            {
                data: 'codigo', orderable: false, searchable: false, className: 'text-end',
                render: (codigo, type, row) => `
                    ${permisos.actualizar ? `<button class="btn btn-sm btn-outline-primary btn-editar" data-codigo="${codigo}"><i class="fa-solid fa-pen"></i></button>` : ''}
                    ${permisos.eliminar && row.activo === 'S' ? `<button class="btn btn-sm btn-outline-danger btn-desactivar" data-codigo="${codigo}" title="Desactivar"><i class="fa-solid fa-user-slash"></i></button>` : ''}
                `
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    function abrirModal(html) {
        $('#contenidoModal').html(html);
        new bootstrap.Modal('#modalFormulario').show();
    }

    $('#btnNuevo').on('click', async function () {
        const html = await $.get('/Mecanicos/FormularioCrear');
        abrirModal(html);
    });

    $('#tblMecanicos').on('click', '.btn-editar', async function () {
        const codigo = $(this).data('codigo');
        const html = await $.get('/Mecanicos/FormularioEditar', { codigo });
        abrirModal(html);
    });

    $('#tblMecanicos').on('click', '.btn-desactivar', async function () {
        const codigo = $(this).data('codigo');
        const confirmado = await Swal.fire({
            icon: 'warning',
            title: '¿Desactivar mecánico?',
            text: `El mecánico "${codigo}" dejará de ofrecerse en las órdenes de servicio nuevas. Sus órdenes anteriores no se pierden.`,
            showCancelButton: true,
            confirmButtonText: 'Desactivar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: '#dc3545'
        }).then(r => r.isConfirmed);
        if (!confirmado) return;

        const respuesta = await App.eliminar(`/Mecanicos/Eliminar?codigo=${encodeURIComponent(codigo)}`);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Mecánico desactivado correctamente.');
        recargarTabla();
    });

    $(document).on('click', '#btnGuardarMecanico', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const codigo = $boton.data('codigo');

        const datos = App.recolectarFormulario('#formMecanico');
        const url = esEdicion ? `/Mecanicos/Editar?codigo=${encodeURIComponent(codigo)}` : '/Mecanicos/Crear';

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.enviarJson(url, 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            App.mostrarExito(esEdicion ? 'Mecánico actualizado correctamente.' : 'Mecánico creado correctamente.');
            recargarTabla();
        } finally {
            $boton.prop('disabled', false);
        }
    });
});
```

- [ ] **Step 4: Menú Taller en `_Layout.cshtml`**.

a) En el bloque `@{ … }` del inicio, junto a `EsActivoCompras` y `MuestraCompras`, agregar:
```razor
    bool EsActivoTaller = new[] { "Mecanicos", "Motocicletas", "OrdenesServicio" }.Any(EsActivo);
    bool MuestraTaller = new[] { "Mecanicos", "Motocicletas", "OrdenesServicio" }.Any(Puede);
```
b) Justo **antes** del bloque `@if (MuestraAdministracion)` (después del `}` que cierra el bloque de Compras), agregar:
```razor
                    @if (MuestraTaller)
                    {
                        <a class="nav-link nav-link-toggle @(EsActivoTaller ? "active" : "")" data-bs-toggle="collapse" href="#submenuTaller" role="button" aria-expanded="@(EsActivoTaller ? "true" : "false")" aria-controls="submenuTaller">
                            <i class="fa-solid fa-screwdriver-wrench"></i><span>Taller</span>
                            <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
                        </a>
                        <div class="collapse @(EsActivoTaller ? "show" : "")" id="submenuTaller">
                            @if (Puede("Mecanicos"))
                            {
                                <a class="nav-link nav-sublink @(EsActivo("Mecanicos") ? "active" : "")" asp-controller="Mecanicos" asp-action="Index">
                                    <i class="fa-solid fa-user-gear"></i><span>Mecánicos</span>
                                </a>
                            }
                        </div>
                    }
```

- [ ] **Step 5: Verificar** — `dotnet build Web.slnx …` → 0 errores; `node --check Web.UI/wwwroot/js/mecanicos.js`.

- [ ] **Step 6: Commit**

```bash
git add Web.UI/Controllers/MecanicosController.cs Web.UI/Views/Mecanicos/Index.cshtml Web.UI/Views/Mecanicos/_Form.cshtml Web.UI/wwwroot/js/mecanicos.js Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(taller): menu Taller y pantalla de mecanicos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Pantalla de Motocicletas (con historial)

**Precondición:** Task 13 completa. `grep -n "Motocicletas" Web.UI/Views/Shared/_Layout.cshtml` solo muestra las listas de la Task 13 (no hay enlace).

**Files:**
- Create: `Web.UI/Controllers/MotocicletasController.cs`, `Web.UI/Views/Motocicletas/Index.cshtml`, `Web.UI/Views/Motocicletas/_Form.cshtml`, `Web.UI/wwwroot/js/motocicletas.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `IMotocicletaApiClient` (Task 12), `ISocioNegocioApiClient.ObtenerTodoAsync("C")`/`ObtenerContenganNombreAsync(texto, "C")` (existentes), `App.autocompletar`.
- Produces: `/Motocicletas` (listado con búsqueda, alta, edición, baja lógica e **Historial** de órdenes de la moto); `GET /Motocicletas/BuscarSocios?texto=`.

- [ ] **Step 1: Controlador** — `Web.UI/Controllers/MotocicletasController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.Motocicleta;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class MotocicletasController : Controller
    {
        private readonly IMotocicletaApiClient _motos;
        private readonly ISocioNegocioApiClient _socios;

        public MotocicletasController(IMotocicletaApiClient motos, ISocioNegocioApiClient socios)
        {
            _motos = motos;
            _socios = socios;
        }

        [RequierePermiso("Motocicletas", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("Motocicletas", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _motos.BuscarAsync(null, null);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("Motocicletas", Accion.Agregar)]
        public IActionResult FormularioCrear()
        {
            ViewBag.EsEdicion = false;
            return PartialView("_Form", new MotocicletaCrearDTO { Activo = "S" });
        }

        [HttpGet]
        [RequierePermiso("Motocicletas", Accion.Actualizar)]
        public async Task<IActionResult> FormularioEditar(int entry)
        {
            var respuesta = await _motos.ObtenerAsync(entry);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            ViewBag.EsEdicion = true;
            ViewBag.EntryActual = entry;
            ViewBag.NombreCliente = respuesta.Dato.NombreSn;
            var dto = new MotocicletaCrearDTO
            {
                CodigoSn = respuesta.Dato.CodigoSn,
                Placa = respuesta.Dato.Placa,
                Marca = respuesta.Dato.Marca,
                Modelo = respuesta.Dato.Modelo,
                Anio = respuesta.Dato.Anio,
                Color = respuesta.Dato.Color,
                Vin = respuesta.Dato.Vin,
                Comentario = respuesta.Dato.Comentario,
                Activo = respuesta.Dato.Activo
            };
            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("Motocicletas", Accion.Agregar)]
        public async Task<IActionResult> Crear([FromBody] MotocicletaCrearDTO dto)
        {
            var respuesta = await _motos.InsertarAsync(dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("Motocicletas", Accion.Actualizar)]
        public async Task<IActionResult> Editar(int entry, [FromBody] MotocicletaCrearDTO dto)
        {
            var actualizar = new MotocicletaActualizarDTO
            {
                CodigoSn = dto.CodigoSn, Placa = dto.Placa, Marca = dto.Marca, Modelo = dto.Modelo, Anio = dto.Anio,
                Color = dto.Color, Vin = dto.Vin, Comentario = dto.Comentario, Activo = dto.Activo
            };
            var respuesta = await _motos.ActualizarAsync(entry, actualizar);
            return Json(respuesta);
        }

        // Baja lógica: la motocicleta pasa a inactiva (puede tener órdenes de servicio).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("Motocicletas", Accion.Eliminar)]
        public async Task<IActionResult> Eliminar(int entry)
        {
            var respuesta = await _motos.EliminarAsync(entry);
            return Json(respuesta);
        }

        // La API exige además Ver en OrdenesServicio (el historial lista órdenes).
        [HttpGet]
        [RequierePermiso("Motocicletas", Accion.Ver)]
        public async Task<IActionResult> ObtenerHistorial(int entry)
        {
            var respuesta = await _motos.ObtenerHistorialAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("Motocicletas", Accion.Ver)]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync("C")
                : await _socios.ObtenerContenganNombreAsync(texto, "C");
            return Json(respuesta);
        }
    }
}
```

- [ ] **Step 2: Vistas**.

`Web.UI/Views/Motocicletas/Index.cshtml`:
```razor
@{
    ViewData["Title"] = "Motocicletas";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Motocicletas</h3>
    @if (User.TienePermiso("Motocicletas", Accion.Agregar))
    {
        <button type="button" class="btn btn-primary" id="btnNuevo">
            <i class="fa-solid fa-plus me-1"></i>Nuevo
        </button>
    }
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblMotocicletas" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>Placa</th>
                        <th>Cliente</th>
                        <th>Marca / modelo</th>
                        <th>Año</th>
                        <th>Color</th>
                        <th>Activo</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalFormulario" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content" id="contenidoModal">
            <!-- se carga por AJAX -->
        </div>
    </div>
</div>

<div class="modal fade" id="modalHistorial" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title" id="tituloHistorial">Historial de servicios</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <div class="table-responsive">
                    <table class="table table-sm align-middle w-100">
                        <thead>
                            <tr>
                                <th>No. orden</th>
                                <th>Fecha</th>
                                <th>Estado</th>
                                <th>Mecánico</th>
                                <th>Falla reportada</th>
                                <th class="text-end">Total</th>
                            </tr>
                        </thead>
                        <tbody id="cuerpoHistorial"></tbody>
                    </table>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>
            </div>
        </div>
    </div>
</div>

<script id="datosPermisosMotocicletas" type="application/json">
    { "actualizar": @(User.TienePermiso("Motocicletas", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("Motocicletas", Accion.Eliminar) ? "true" : "false"),
      "historial": @(User.TienePermiso("OrdenesServicio", Accion.Ver) ? "true" : "false") }
</script>

@section Scripts {
    <script src="~/js/motocicletas.js" asp-append-version="true"></script>
}
```

`Web.UI/Views/Motocicletas/_Form.cshtml`:
```razor
@model Web.ApiClient.Dtos.Motocicleta.MotocicletaCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? "Editar motocicleta" : "Nueva motocicleta")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formMotocicleta" novalidate>
        <div class="row g-3">
            <div class="col-md-8 position-relative">
                <label class="form-label">Cliente</label>
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       value="@(!string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {ViewBag.NombreCliente}" : "")" />
                <input type="hidden" asp-for="CodigoSn" />
                <div class="invalid-feedback" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
            </div>
            <div class="col-md-4">
                <label asp-for="Placa" class="form-label"></label>
                <input asp-for="Placa" class="form-control text-uppercase" maxlength="15" />
            </div>
            <div class="col-md-4">
                <label asp-for="Marca" class="form-label"></label>
                <input asp-for="Marca" class="form-control" maxlength="50" />
            </div>
            <div class="col-md-4">
                <label asp-for="Modelo" class="form-label"></label>
                <input asp-for="Modelo" class="form-control" maxlength="50" />
            </div>
            <div class="col-md-4">
                <label asp-for="Anio" class="form-label">Año</label>
                <input asp-for="Anio" type="number" min="1900" max="2100" class="form-control" />
            </div>
            <div class="col-md-4">
                <label asp-for="Color" class="form-label"></label>
                <input asp-for="Color" class="form-control" maxlength="30" />
            </div>
            <div class="col-md-5">
                <label asp-for="Vin" class="form-label">VIN / chasis</label>
                <input asp-for="Vin" class="form-control" maxlength="30" />
            </div>
            <div class="col-md-3">
                <label asp-for="Activo" class="form-label"></label>
                <select asp-for="Activo" class="form-select">
                    <option value="S">Sí</option>
                    <option value="N">No</option>
                </select>
            </div>
            <div class="col-12">
                <label asp-for="Comentario" class="form-label"></label>
                <textarea asp-for="Comentario" class="form-control" rows="2" maxlength="254"></textarea>
            </div>
        </div>
    </form>
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
    <button type="button" class="btn btn-primary" id="btnGuardarMotocicleta" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
        <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
    </button>
</div>
```

- [ ] **Step 3: JS** — `Web.UI/wwwroot/js/motocicletas.js`:

```js
$(function () {
    const datosPermisosEl = document.getElementById('datosPermisosMotocicletas');
    const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true, historial: true };
    const tabla = $('#tblMotocicletas').DataTable({
        ajax: { url: '/Motocicletas/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'placa' },
            { data: 'nombreSn', render: (d, t, row) => d || row.codigoSn || '' },
            { data: 'marca', render: (d, t, row) => [d, row.modelo].filter(Boolean).join(' ') },
            { data: 'anio', render: d => d ?? '' },
            { data: 'color', render: d => d || '' },
            { data: 'activo', render: d => d === 'S' ? '<span class="badge text-bg-success">Sí</span>' : '<span class="badge text-bg-secondary">No</span>' },
            {
                data: 'entry', orderable: false, searchable: false, className: 'text-end',
                render: (entry, type, row) => `
                    ${permisos.historial ? `<button class="btn btn-sm btn-outline-secondary btn-historial" data-entry="${entry}" data-placa="${row.placa}" title="Historial de servicios"><i class="fa-solid fa-clock-rotate-left"></i></button>` : ''}
                    ${permisos.actualizar ? `<button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"><i class="fa-solid fa-pen"></i></button>` : ''}
                    ${permisos.eliminar && row.activo === 'S' ? `<button class="btn btn-sm btn-outline-danger btn-desactivar" data-entry="${entry}" data-placa="${row.placa}" title="Desactivar"><i class="fa-solid fa-ban"></i></button>` : ''}
                `
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    function abrirModal(html) {
        $('#contenidoModal').html(html);
        $('#modalFormulario').off('.autocompletar'); // limpia los listeners de la apertura anterior
        new bootstrap.Modal('#modalFormulario').show();
        inicializarBuscadorCliente();
    }

    function inicializarBuscadorCliente() {
        if ($('#CodigoSnTexto').length === 0) return;
        App.autocompletar({
            texto: $('#CodigoSnTexto'),
            oculto: $('#CodigoSn'),
            lista: $('#CodigoSnResultados'),
            error: $('#CodigoSnError'),
            endpoint: '/Motocicletas/BuscarSocios',
            obtenerCodigo: s => s.codigo ?? s.Codigo,
            obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`
        });
    }

    $('#btnNuevo').on('click', async function () {
        const html = await $.get('/Motocicletas/FormularioCrear');
        abrirModal(html);
    });

    $('#tblMotocicletas').on('click', '.btn-editar', async function () {
        const entry = $(this).data('entry');
        const html = await $.get('/Motocicletas/FormularioEditar', { entry });
        abrirModal(html);
    });

    $('#tblMotocicletas').on('click', '.btn-desactivar', async function () {
        const entry = $(this).data('entry');
        const placa = $(this).data('placa');
        const confirmado = await Swal.fire({
            icon: 'warning',
            title: '¿Desactivar motocicleta?',
            text: `La motocicleta ${placa} dejará de ofrecerse en las órdenes de servicio nuevas. Su historial no se pierde.`,
            showCancelButton: true,
            confirmButtonText: 'Desactivar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: '#dc3545'
        }).then(r => r.isConfirmed);
        if (!confirmado) return;

        const respuesta = await App.eliminar(`/Motocicletas/Eliminar?entry=${entry}`);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Motocicleta desactivada correctamente.');
        recargarTabla();
    });

    $(document).on('click', '#btnGuardarMotocicleta', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const entry = $boton.data('entry');

        const datos = App.recolectarFormulario('#formMotocicleta');
        if (!datos.CodigoSn) {
            App.mostrarError('Selecciona el cliente de la motocicleta.');
            return;
        }
        const url = esEdicion ? `/Motocicletas/Editar?entry=${entry}` : '/Motocicletas/Crear';

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.enviarJson(url, 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            App.mostrarExito(esEdicion ? 'Motocicleta actualizada correctamente.' : 'Motocicleta creada correctamente.');
            recargarTabla();
        } finally {
            $boton.prop('disabled', false);
        }
    });

    // --- Historial de servicios de la motocicleta ---

    const ESTADOS = { R: 'Recibida', P: 'En proceso', T: 'Terminada', E: 'Entregada' };

    $('#tblMotocicletas').on('click', '.btn-historial', async function () {
        const entry = $(this).data('entry');
        const placa = $(this).data('placa');
        $('#tituloHistorial').text(`Historial de servicios - ${placa}`);
        const $cuerpo = $('#cuerpoHistorial').empty();

        const respuesta = await $.get('/Motocicletas/ObtenerHistorial', { entry });
        const ordenes = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        if (ordenes.length === 0) {
            $cuerpo.append($('<tr>').append($('<td colspan="6" class="text-center text-muted">').text('Esta motocicleta no tiene órdenes de servicio.')));
        }
        ordenes.forEach(o => {
            const estado = o.cancelado === 'S' ? 'Cancelada' : (ESTADOS[o.estadoServicio] ?? o.estadoServicio);
            $cuerpo.append($('<tr>')
                .append($('<td>').text(o.numDoc))
                .append($('<td>').text(o.fechaDoc ? new Date(o.fechaDoc).toLocaleDateString() : ''))
                .append($('<td>').text(estado))
                .append($('<td>').text(o.nombreMecanico || ''))
                .append($('<td>').text(o.fallaReportada || ''))
                .append($('<td class="text-end">').text(o.totalDoc != null ? Number(o.totalDoc).toFixed(2) : '')));
        });
        new bootstrap.Modal('#modalHistorial').show();
    });
});
```

- [ ] **Step 4: Enlace en el menú Taller** — en `_Layout.cshtml`, dentro de `<div class="collapse … id="submenuTaller">`, tras el enlace de Mecánicos:
```razor
                            @if (Puede("Motocicletas"))
                            {
                                <a class="nav-link nav-sublink @(EsActivo("Motocicletas") ? "active" : "")" asp-controller="Motocicletas" asp-action="Index">
                                    <i class="fa-solid fa-motorcycle"></i><span>Motocicletas</span>
                                </a>
                            }
```

- [ ] **Step 5: Verificar** — `dotnet build Web.slnx …` → 0 errores; `node --check Web.UI/wwwroot/js/motocicletas.js`.

- [ ] **Step 6: Commit**

```bash
git add Web.UI/Controllers/MotocicletasController.cs Web.UI/Views/Motocicletas/Index.cshtml Web.UI/Views/Motocicletas/_Form.cshtml Web.UI/wwwroot/js/motocicletas.js Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(taller): pantalla de motocicletas por cliente con baja logica e historial de servicios

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Órdenes de servicio — controlador y listado (estados, cancelación y "Facturar")

**Precondición:** Task 14 completa. `grep -n "OrdenesServicio" Web.UI/Controllers/*.cs` sin resultados.

**Files:**
- Create: `Web.UI/Controllers/OrdenesServicioController.cs`, `Web.UI/Views/OrdenesServicio/Index.cshtml`, `Web.UI/wwwroot/js/ordenesservicio.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `IOrdenServicioApiClient`, `IOrdenServicioDetalleApiClient`, `IMotocicletaApiClient`, `IMecanicoApiClient`, `ISocioNegocioApiClient`, `IMonedaApiClient`, `IArticuloApiClient`, `IAlmacenApiClient`, `IImpuestoApiClient`, `INumeracionDocumentoDetApiClient`, `INumeracionDocumentoApiClient` (los de Entregas más los del taller); `App.botonCopiarA(70, entry, destinos)`.
- Produces (todas bajo `/OrdenesServicio`): `Index`, `ObtenerTodos?estado=`, `ObtenerUno?entry=`, `FormularioCrear`, `FormularioEditar?entry=`, `Crear`, `Editar?entry=`, `CambiarEstado?entry=&estado=`, `Cancelar?entry=`, `ObtenerDetalle?entry=`, `CrearLinea`, `EditarLinea?entry=&noLinea=`, `EliminarLinea?entry=&noLinea=`, `BuscarSocios`, `BuscarArticulos?texto=&tipo=`, `BuscarArticulosPorCodigo?texto=&tipo=`, `BuscarAlmacenes`, `BuscarImpuestos`, `ObtenerAlmacenPorCodigo`, `ObtenerImpuestoPorCodigo`, `MotosDeCliente?codigoSn=`, `CrearMoto`. Las acciones de formulario devuelven la vista parcial `_Form` que crea la Task 16 (hasta entonces "Nuevo" y "Editar" no responden: el listado de esta tarea solo cubre estados, cancelar y facturar).

- [ ] **Step 1: Controlador completo** — `Web.UI/Controllers/OrdenesServicioController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Articulo;
using Web.ApiClient.Dtos.Motocicleta;
using Web.ApiClient.Dtos.OrdenServicio;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class OrdenesServicioController : Controller
    {
        private readonly IOrdenServicioApiClient _ordenes;
        private readonly IOrdenServicioDetalleApiClient _detalles;
        private readonly IMotocicletaApiClient _motos;
        private readonly IMecanicoApiClient _mecanicos;
        private readonly ISocioNegocioApiClient _socios;
        private readonly IMonedaApiClient _monedas;
        private readonly IArticuloApiClient _articulos;
        private readonly IAlmacenApiClient _almacenes;
        private readonly IImpuestoApiClient _impuestos;
        private readonly INumeracionDocumentoDetApiClient _series;
        private readonly INumeracionDocumentoApiClient _numeracion;

        // CodigoObj de NumeracionDocumento que identifica a "Órdenes de servicio" como tipo de objeto.
        private const string CodigoObjOrden = "70";
        private const string SubTipoDocOrden = "--";

        public OrdenesServicioController(
            IOrdenServicioApiClient ordenes,
            IOrdenServicioDetalleApiClient detalles,
            IMotocicletaApiClient motos,
            IMecanicoApiClient mecanicos,
            ISocioNegocioApiClient socios,
            IMonedaApiClient monedas,
            IArticuloApiClient articulos,
            IAlmacenApiClient almacenes,
            IImpuestoApiClient impuestos,
            INumeracionDocumentoDetApiClient series,
            INumeracionDocumentoApiClient numeracion)
        {
            _ordenes = ordenes;
            _detalles = detalles;
            _motos = motos;
            _mecanicos = mecanicos;
            _socios = socios;
            _monedas = monedas;
            _articulos = articulos;
            _almacenes = almacenes;
            _impuestos = impuestos;
            _series = series;
            _numeracion = numeracion;
        }

        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos(string? estado)
        {
            var respuesta = await _ordenes.ObtenerTodoAsync(estado);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> ObtenerUno(int entry)
        {
            var respuesta = await _ordenes.ObtenerAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Agregar)]
        public async Task<IActionResult> FormularioCrear()
        {
            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjOrden);
            ViewBag.SeriesOrden = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocOrden);

            // Serie preseleccionada: la configurada como "por defecto" para este objeto en "Numeración de documentos".
            var numeraciones = await _numeracion.ObtenerTodoAsync();
            var numeracionActual = (numeraciones.Dato ?? []).FirstOrDefault(n => n.CodigoObj == CodigoObjOrden && n.SubTipoDoc == SubTipoDocOrden);
            ViewBag.SerieDefecto = numeracionActual?.SerieDfct;

            ViewBag.EsEdicion = false;
            // Las fechas nacen con el día de hoy: la orden se abre en el momento en que ingresa la moto.
            var dto = new OrdenServicioCrearDTO { FechaDoc = DateTime.Today, FechaIngreso = DateTime.Today };
            return PartialView("_Form", dto);
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> FormularioEditar(int entry)
        {
            var respuesta = await _ordenes.ObtenerAsync(entry);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            var o = respuesta.Dato;
            await CargarDropdownsAsync();
            ViewBag.EsEdicion = true;
            ViewBag.EntryActual = entry;
            ViewBag.NumDocActual = o.NumDoc;
            ViewBag.EstadoServicio = o.EstadoServicio;
            ViewBag.Cancelado = o.Cancelado ?? "N";
            ViewBag.TotalBruto = o.TotalBruto;
            ViewBag.TotalImp = o.TotalImp;
            ViewBag.TotalDoc = o.TotalDoc;

            var serieInfo = await _series.ObtenerAsync(o.Serie);
            ViewBag.NombreSerieActual = serieInfo.Resultado ? serieInfo.Dato?.NombreSerie : null;

            // Motos del cliente de la orden (la inactiva de la orden se conserva aunque ya no se ofrezca a otras).
            var motos = await _motos.BuscarAsync(o.CodigoSn, null);
            ViewBag.MotosCliente = (motos.Dato ?? []).Where(m => m.Activo == "S" || m.Entry == o.MotoEntry).ToList();

            var dto = new OrdenServicioCrearDTO
            {
                NumDoc = o.NumDoc,
                Serie = o.Serie,
                FechaDoc = o.FechaDoc,
                FechaIngreso = o.FechaIngreso,
                FechaPromesa = o.FechaPromesa,
                CodigoSn = o.CodigoSn,
                NombreSn = o.NombreSn,
                Direccion = o.Direccion,
                MonedaDoc = o.MonedaDoc,
                MotoEntry = o.MotoEntry,
                CodMecanico = o.CodMecanico,
                KmIngreso = o.KmIngreso,
                FallaReportada = o.FallaReportada,
                Diagnostico = o.Diagnostico,
                Observaciones = o.Observaciones,
                Comentario = o.Comentario
            };
            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("OrdenesServicio", Accion.Agregar)]
        public async Task<IActionResult> Crear([FromBody] OrdenServicioCrearDTO dto)
        {
            var respuesta = await _ordenes.InsertarAsync(dto);
            if (!respuesta.Resultado)
                return Json(respuesta);

            var creada = await _ordenes.ObtenerAsync(respuesta.Dato);
            return Json(new { respuesta.Resultado, respuesta.Mensaje, dato = respuesta.Dato, numDoc = creada.Dato?.NumDoc });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        public async Task<IActionResult> Editar(int entry, [FromBody] OrdenServicioActualizarDTO dto)
        {
            var respuesta = await _ordenes.ActualizarAsync(entry, dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        public async Task<IActionResult> CambiarEstado(int entry, string estado)
        {
            var respuesta = await _ordenes.CambiarEstadoAsync(entry, estado);
            return Json(respuesta);
        }

        // Cancelar una orden es anularla: requiere Eliminar (convención: 'cancelar documento -> Eliminar').
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("OrdenesServicio", Accion.Eliminar)]
        public async Task<IActionResult> Cancelar(int entry)
        {
            var respuesta = await _ordenes.CancelarAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> ObtenerDetalle(int entry)
        {
            var respuesta = await _ordenes.ObtenerLineasAsync(entry);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        public async Task<IActionResult> CrearLinea([FromBody] OrdenServicioDetalleCrearDTO dto)
        {
            var respuesta = await _detalles.InsertarAsync(dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        public async Task<IActionResult> EditarLinea(int entry, int noLinea, [FromBody] OrdenServicioDetalleActualizarDTO dto)
        {
            var respuesta = await _detalles.ActualizarAsync(entry, noLinea, dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("OrdenesServicio", Accion.Actualizar)]
        public async Task<IActionResult> EliminarLinea(int entry, int noLinea)
        {
            var respuesta = await _detalles.EliminarAsync(entry, noLinea);
            return Json(respuesta);
        }

        // ---------------------------------------------------------------- buscadores del formulario

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync("C")
                : await _socios.ObtenerContenganNombreAsync(texto, "C");
            return Json(respuesta);
        }

        /// <summary>tipo: "I" = solo insumos (artículos de inventario), "S" = solo servicios; sin tipo, ambos. Siempre activos y de venta.</summary>
        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> BuscarArticulos(string texto, string? tipo)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(FiltrarArticulos(respuesta, tipo));
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> BuscarArticulosPorCodigo(string texto, string? tipo)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganCodigoAsync(texto);
            return Json(FiltrarArticulos(respuesta, tipo));
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> BuscarAlmacenes(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> BuscarImpuestos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _impuestos.ObtenerTodoAsync()
                : await _impuestos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> ObtenerAlmacenPorCodigo(string codigo)
        {
            var respuesta = await _almacenes.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> ObtenerImpuestoPorCodigo(string codigo)
        {
            var respuesta = await _impuestos.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        /// <summary>Motocicletas activas de un cliente, para el selector de la orden.</summary>
        [HttpGet]
        [RequierePermiso("OrdenesServicio", Accion.Ver)]
        public async Task<IActionResult> MotosDeCliente(string codigoSn)
        {
            var respuesta = await _motos.BuscarAsync(codigoSn, null);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return Json(respuesta);

            return Json(new Respuesta<IEnumerable<MotocicletaDTO>>
            {
                Resultado = true,
                Mensaje = respuesta.Mensaje,
                Dato = respuesta.Dato.Where(m => m.Activo == "S").ToList()
            });
        }

        /// <summary>Registro rápido de una motocicleta desde el formulario de la orden.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("Motocicletas", Accion.Agregar)]
        public async Task<IActionResult> CrearMoto([FromBody] MotocicletaCrearDTO dto)
        {
            var respuesta = await _motos.InsertarAsync(dto);
            return Json(respuesta);
        }

        private static Respuesta<IEnumerable<ArticuloDTO>> FiltrarArticulos(Respuesta<IEnumerable<ArticuloDTO>> origen, string? tipo)
        {
            if (!origen.Resultado || origen.Dato is null)
                return origen;

            var lista = origen.Dato.Where(a => a.Activo != "N" && a.ArticuloVenta == "S");
            lista = tipo switch
            {
                "I" => lista.Where(a => a.ArticuloInventario == "S"),
                "S" => lista.Where(a => a.ArticuloInventario != "S"),
                _ => lista
            };
            return new Respuesta<IEnumerable<ArticuloDTO>> { Resultado = true, Mensaje = origen.Mensaje, Dato = lista.ToList() };
        }

        private async Task CargarDropdownsAsync()
        {
            // Socio, artículo, almacén e impuesto se consultan bajo demanda con el buscador (BuscarSocios,
            // BuscarArticulos, …). Moneda y mecánico son <select> normales.
            var monedas = await _monedas.ObtenerTodoAsync();
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");

            var mecanicos = await _mecanicos.ObtenerTodoAsync();
            ViewBag.Mecanicos = new SelectList((mecanicos.Dato ?? []).Where(m => m.Activo == "S"), "Codigo", "Nombre");
        }
    }
}
```
(Si algún nombre de método del cliente existente difiere —`ObtenerContenganNombreAsync`, `ObtenerContenganCodigoAsync`, `ObtenerAsync` de `IAlmacenApiClient`/`IImpuestoApiClient`— usar los nombres reales de `EntregasController`, que ya los llama igual.)

- [ ] **Step 2: Vista del listado** — `Web.UI/Views/OrdenesServicio/Index.cshtml`:

```razor
@{
    ViewData["Title"] = "Órdenes de servicio";
    string PuedeCopiar(string modulo) => User.TienePermiso(modulo, Accion.Ver) && User.TienePermiso(modulo, Accion.Agregar) ? "true" : "false";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Órdenes de servicio</h3>
    <div class="d-flex gap-2">
        <select id="filtroEstado" class="form-select" style="width: auto;">
            <option value="">Todos los estados</option>
            <option value="R">Recibidas</option>
            <option value="P">En proceso</option>
            <option value="T">Terminadas</option>
            <option value="E">Entregadas</option>
        </select>
        @if (User.TienePermiso("OrdenesServicio", Accion.Agregar))
        {
            <button type="button" class="btn btn-primary" id="btnNuevo">
                <i class="fa-solid fa-plus me-1"></i>Nuevo
            </button>
        }
    </div>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblOrdenesServicio" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>No. orden</th>
                        <th>Fecha</th>
                        <th>Cliente</th>
                        <th>Motocicleta</th>
                        <th>Mecánico</th>
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

<script id="datosPermisosOrdenesServicio" type="application/json">
    { "actualizar": @(User.TienePermiso("OrdenesServicio", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("OrdenesServicio", Accion.Eliminar) ? "true" : "false"),
      "copiarA": { "factura": @PuedeCopiar("Facturas") } }
</script>

@section Scripts {
    <script src="~/js/ordenesservicio.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: JS del listado** — `Web.UI/wwwroot/js/ordenesservicio.js` (archivo nuevo; la Task 16 lo reemplaza por la versión completa que incluye este mismo código):

```js
$(function () {
    const datosPermisosEl = document.getElementById('datosPermisosOrdenesServicio');
    const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true, copiarA: {} };
    const destinosCopia = [
        permisos.copiarA && permisos.copiarA.factura && { nombre: 'Factura de venta', url: '/Facturas' }
    ].filter(Boolean);

    // Estados de la orden y a cuál avanza cada uno.
    const ESTADOS = {
        R: { nombre: 'Recibida', clase: 'text-bg-secondary', siguiente: 'P' },
        P: { nombre: 'En proceso', clase: 'text-bg-primary', siguiente: 'T' },
        T: { nombre: 'Terminada', clase: 'text-bg-warning', siguiente: 'E' },
        E: { nombre: 'Entregada', clase: 'text-bg-success', siguiente: null }
    };

    function insigniaEstado(row) {
        if (row && row.cancelado === 'S') return '<span class="badge text-bg-danger">Cancelada</span>';
        const e = ESTADOS[row && row.estadoServicio];
        return e ? `<span class="badge ${e.clase}">${e.nombre}</span>` : '';
    }

    const tabla = $('#tblOrdenesServicio').DataTable({
        ajax: { url: '/OrdenesServicio/ObtenerTodos', dataSrc: App.dataSrcTabla, data: d => { d.estado = $('#filtroEstado').val() || ''; } },
        order: [[0, 'desc']],
        columns: [
            { data: 'numDoc' },
            { data: 'fechaDoc', render: d => d ? new Date(d).toLocaleDateString() : '' },
            { data: 'nombreSn', render: (d, t, row) => d || row.codigoSn || '' },
            {
                data: 'placa',
                render: (d, t, row) => d ? `${d}${row.motoDescripcion ? ` <span class="text-muted small">${row.motoDescripcion}</span>` : ''}` : ''
            },
            { data: 'nombreMecanico', render: d => d || '' },
            { data: 'estadoServicio', render: (d, t, row) => insigniaEstado(row) },
            { data: 'totalDoc', render: d => d != null ? Number(d).toFixed(2) : '' },
            {
                data: 'entry', orderable: false, searchable: false, className: 'text-end',
                render: (entry, t, row) => {
                    const cancelada = !row || row.cancelado === 'S';
                    const estado = ESTADOS[row && row.estadoServicio];
                    const facturable = !cancelada && (row.estadoServicio === 'T' || row.estadoServicio === 'E');
                    const siguiente = estado && estado.siguiente ? ESTADOS[estado.siguiente] : null;
                    const puedeAvanzar = permisos.actualizar && !cancelada && siguiente;
                    return `
                        ${facturable ? App.botonCopiarA(70, entry, destinosCopia) : ''}
                        ${puedeAvanzar ? `<button class="btn btn-sm btn-outline-success btn-avanzar" data-entry="${entry}" data-estado="${estado.siguiente}" title="Pasar a ${siguiente.nombre}"><i class="fa-solid fa-forward-step"></i></button>` : ''}
                        <button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"${permisos.actualizar ? '' : ' title="Ver"'}><i class="fa-solid ${permisos.actualizar ? 'fa-pen' : 'fa-eye'}"></i></button>
                    `;
                }
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    $('#filtroEstado').on('change', recargarTabla);

    $('#tblOrdenesServicio').on('click', '.btn-avanzar', async function () {
        const entry = $(this).data('entry');
        const estado = $(this).data('estado');
        const confirmado = await Swal.fire({
            icon: 'question',
            title: '¿Cambiar el estado de la orden?',
            text: `La orden pasará a "${ESTADOS[estado].nombre}". No se puede volver a un estado anterior.`,
            showCancelButton: true,
            confirmButtonText: 'Cambiar',
            cancelButtonText: 'Cancelar'
        }).then(r => r.isConfirmed);
        if (!confirmado) return;

        const respuesta = await App.enviarJson(`/OrdenesServicio/CambiarEstado?entry=${entry}&estado=${estado}`, 'POST', {});
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito(`Orden ${ESTADOS[estado].nombre.toLowerCase()}.`);
        recargarTabla();
    });
});
```

- [ ] **Step 4: Enlace en el menú Taller** — en `_Layout.cshtml`, dentro de `id="submenuTaller"`, **antes** del enlace de Mecánicos (las órdenes son lo principal del grupo):
```razor
                            @if (Puede("OrdenesServicio"))
                            {
                                <a class="nav-link nav-sublink @(EsActivo("OrdenesServicio") ? "active" : "")" asp-controller="OrdenesServicio" asp-action="Index">
                                    <i class="fa-solid fa-clipboard-list"></i><span>Órdenes de servicio</span>
                                </a>
                            }
```

- [ ] **Step 5: Verificar** — `dotnet build Web.slnx …` → 0 errores; `node --check Web.UI/wwwroot/js/ordenesservicio.js`.

- [ ] **Step 6: Commit**

```bash
git add Web.UI/Controllers/OrdenesServicioController.cs Web.UI/Views/OrdenesServicio/Index.cshtml Web.UI/wwwroot/js/ordenesservicio.js Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(taller): listado de ordenes de servicio con estados, facturar desde la orden y controlador completo

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---
### Task 16: Formulario de la orden de servicio (alta, edición con líneas en vivo y registro rápido de moto)

**Precondición:** Task 15 completa. `grep -n "_Form" Web.UI/Controllers/OrdenesServicioController.cs` muestra las dos vistas parciales (`FormularioCrear` y `FormularioEditar`); `Web.UI/Views/OrdenesServicio/_Form.cshtml` no existe todavía. `file Web.UI/wwwroot/js/ordenesservicio.js` para conservar el fin de línea del archivo (se reemplaza completo).

**Files:**
- Create: `Web.UI/Views/OrdenesServicio/_Form.cshtml`
- Modify (reemplazo completo): `Web.UI/wwwroot/js/ordenesservicio.js`

**Interfaces:**
- Consumes: las acciones de `OrdenesServicioController` (Task 15), `App.autocompletar` con `parametros` (Task 12), `App.resolverPrecioLista`/`obtenerListaDeSocio`, `App.sinCamposInternos`, `App.enviarJson`, `App.eliminar`.
- Produces: el modal "Nueva orden de servicio" / "Orden de servicio N" con:
  - **alta:** cabecera + líneas locales que viajan en una sola petición (`POST /OrdenesServicio/Crear`);
  - **edición (estados R, P, T):** cabecera editable con `POST /OrdenesServicio/Editar`; cada línea agregada, cambiada o quitada se guarda en el acto (`CrearLinea`/`EditarLinea`/`EliminarLinea`) y los totales se refrescan del servidor;
  - **orden entregada:** solo el comentario; **cancelada:** solo lectura; botón "Cancelar orden" (permiso Eliminar);
  - **registro rápido de moto** en el propio formulario (permiso `Motocicletas:Agregar`).

- [ ] **Step 1: Vista del formulario** — `Web.UI/Views/OrdenesServicio/_Form.cshtml`:

```razor
@using System.Text.Json
@using Web.ApiClient.Dtos.Motocicleta
@model Web.ApiClient.Dtos.OrdenServicio.OrdenServicioCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
    string estado = ViewBag.EstadoServicio ?? "R";
    bool cancelada = esEdicion && (ViewBag.Cancelado ?? "N") == "S";
    bool entregada = esEdicion && !cancelada && estado == "E";
    bool puedeEscribir = esEdicion ? User.TienePermiso("OrdenesServicio", Accion.Actualizar) : User.TienePermiso("OrdenesServicio", Accion.Agregar);
    bool cabeceraBloqueada = cancelada || entregada || !puedeEscribir;           // la cabecera completa solo se edita en R, P, T
    bool comentarioBloqueado = cancelada || !puedeEscribir;                        // la orden entregada acepta el comentario
    bool lineasEditables = !cancelada && !entregada && puedeEscribir;
    var opcionesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var motos = (ViewBag.MotosCliente as IEnumerable<MotocicletaDTO>) ?? Enumerable.Empty<MotocicletaDTO>();
    string nombreEstado = estado switch { "R" => "Recibida", "P" => "En proceso", "T" => "Terminada", "E" => "Entregada", _ => estado };
    string textoEstado = cancelada ? "Cancelada" : nombreEstado;
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? $"Orden de servicio {ViewBag.NumDocActual} - {textoEstado}" : "Nueva orden de servicio")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formOrden" novalidate>
        <div class="row g-3">
            @if (!esEdicion)
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <select id="selectSerieOrden" class="form-select" data-serie-defecto="@ViewBag.SerieDefecto"></select>
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
            </div>
            <div class="col-md-3">
                <label class="form-label">Estado</label>
                <input class="form-control" value="@(esEdicion ? textoEstado : "Recibida")" disabled />
            </div>
            <div class="col-md-3">
                <label class="form-label">Fecha documento</label>
                <input type="date" name="FechaDoc" id="FechaDoc" class="form-control" value="@Model.FechaDoc?.ToString("yyyy-MM-dd")" readonly="@cabeceraBloqueada" />
            </div>

            <div class="col-md-5 position-relative">
                <label class="form-label">Cliente</label>
                @if (cabeceraBloqueada)
                {
                    <input class="form-control" value="@(!string.IsNullOrEmpty(Model.NombreSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : Model.CodigoSn)" disabled />
                    <input type="hidden" asp-for="CodigoSn" />
                }
                else
                {
                    <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                           value="@(esEdicion && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
                    <input type="hidden" asp-for="CodigoSn" />
                    <div class="invalid-feedback" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
                }
            </div>
            <div class="col-md-4">
                <label asp-for="NombreSn" class="form-label">Nombre</label>
                <input asp-for="NombreSn" class="form-control" readonly />
            </div>
            <div class="col-md-3">
                <label asp-for="MonedaDoc" class="form-label">Moneda</label>
                <select asp-for="MonedaDoc" class="form-select" asp-items="ViewBag.Monedas" disabled="@cabeceraBloqueada">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>

            <div class="col-md-5">
                <label class="form-label">Motocicleta</label>
                <div class="input-group">
                    <select name="MotoEntry" id="MotoEntry" class="form-select" disabled="@cabeceraBloqueada">
                        <option value="">-- Seleccione --</option>
                        @foreach (var m in motos)
                        {
                            var detalleMoto = $"{m.Marca} {m.Modelo}".Trim();
                            <option value="@m.Entry" selected="@(m.Entry == Model.MotoEntry)">@(detalleMoto.Length > 0 ? $"{m.Placa} - {detalleMoto}" : m.Placa)</option>
                        }
                    </select>
                    @if (!cabeceraBloqueada && User.TienePermiso("Motocicletas", Accion.Agregar))
                    {
                        <button type="button" class="btn btn-outline-secondary" id="btnNuevaMoto" title="Registrar una motocicleta nueva del cliente">
                            <i class="fa-solid fa-plus"></i>
                        </button>
                    }
                </div>
            </div>
            <div class="col-md-4">
                <label asp-for="CodMecanico" class="form-label">Mecánico</label>
                <select asp-for="CodMecanico" class="form-select" asp-items="ViewBag.Mecanicos" disabled="@cabeceraBloqueada">
                    <option value="">-- Sin asignar --</option>
                </select>
            </div>
            <div class="col-md-3">
                <label asp-for="KmIngreso" class="form-label">Kilometraje de ingreso</label>
                <input asp-for="KmIngreso" type="number" min="0" class="form-control" readonly="@cabeceraBloqueada" />
            </div>

            <div class="col-md-4">
                <label asp-for="Direccion" class="form-label"></label>
                <input asp-for="Direccion" class="form-control" readonly="@cabeceraBloqueada" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha de ingreso</label>
                <input type="date" name="FechaIngreso" id="FechaIngreso" class="form-control" value="@Model.FechaIngreso?.ToString("yyyy-MM-dd")" readonly="@cabeceraBloqueada" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha promesa de entrega</label>
                <input type="date" name="FechaPromesa" id="FechaPromesa" class="form-control" value="@Model.FechaPromesa?.ToString("yyyy-MM-dd")" readonly="@cabeceraBloqueada" />
            </div>

            <div class="col-md-6">
                <label asp-for="FallaReportada" class="form-label">Falla reportada por el cliente</label>
                <textarea asp-for="FallaReportada" class="form-control" rows="2" maxlength="500" readonly="@cabeceraBloqueada"></textarea>
            </div>
            <div class="col-md-6">
                <label asp-for="Diagnostico" class="form-label">Diagnóstico del mecánico</label>
                <textarea asp-for="Diagnostico" class="form-control" rows="2" maxlength="500" readonly="@cabeceraBloqueada"></textarea>
            </div>
            <div class="col-md-6">
                <label asp-for="Observaciones" class="form-label"></label>
                <textarea asp-for="Observaciones" class="form-control" rows="2" maxlength="500" readonly="@cabeceraBloqueada"></textarea>
            </div>
            <div class="col-md-6">
                <label asp-for="Comentario" class="form-label"></label>
                <textarea asp-for="Comentario" class="form-control" rows="2" maxlength="254" readonly="@comentarioBloqueado"></textarea>
            </div>

            <div class="col-md-4">
                <label class="form-label">Total bruto</label>
                <input id="TotalBruto" class="form-control" value="@ViewBag.TotalBruto" disabled />
            </div>
            <div class="col-md-4">
                <label class="form-label">Total impuesto</label>
                <input id="TotalImp" class="form-control" value="@ViewBag.TotalImp" disabled />
            </div>
            <div class="col-md-4">
                <label class="form-label">Total documento</label>
                <input id="TotalDoc" class="form-control" value="@ViewBag.TotalDoc" disabled />
            </div>
        </div>
    </form>

    <div id="panelMotoRapida" class="border rounded p-3 mt-3 d-none">
        <h6 class="mb-2">Registrar motocicleta del cliente</h6>
        <div class="row g-2">
            <div class="col-md-3">
                <label class="form-label">Placa</label>
                <input id="mrPlaca" class="form-control text-uppercase" maxlength="15" />
            </div>
            <div class="col-md-3">
                <label class="form-label">Marca</label>
                <input id="mrMarca" class="form-control" maxlength="50" />
            </div>
            <div class="col-md-3">
                <label class="form-label">Modelo</label>
                <input id="mrModelo" class="form-control" maxlength="50" />
            </div>
            <div class="col-md-1">
                <label class="form-label">Año</label>
                <input id="mrAnio" type="number" min="1900" max="2100" class="form-control" />
            </div>
            <div class="col-md-2">
                <label class="form-label">Color</label>
                <input id="mrColor" class="form-control" maxlength="30" />
            </div>
        </div>
        <div class="text-end mt-2">
            <button type="button" class="btn btn-sm btn-secondary" id="btnCancelarMoto">Cancelar</button>
            <button type="button" class="btn btn-sm btn-primary" id="btnGuardarMoto">Guardar motocicleta</button>
        </div>
    </div>

    <hr />
    <div class="d-flex justify-content-between align-items-center mb-2">
        <h6 class="mb-0">Insumos y servicios</h6>
        @if (lineasEditables)
        {
            <div class="d-flex gap-2">
                <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevoInsumo">
                    <i class="fa-solid fa-box-open me-1"></i>Agregar insumo
                </button>
                <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevoServicio">
                    <i class="fa-solid fa-screwdriver-wrench me-1"></i>Agregar servicio
                </button>
            </div>
        }
    </div>

    @if (!esEdicion)
    {
        <p class="text-muted small">Los insumos descuentan del inventario al guardar la orden. Los servicios (mano de obra) no mueven inventario.</p>
    }
    else if (lineasEditables)
    {
        <p class="text-muted small">Cada insumo que agregues, cambies o quites se guarda de inmediato y mueve el inventario. Las líneas ya facturadas no se pueden modificar.</p>
    }
    else if (entregada)
    {
        <p class="text-muted small">La orden ya fue entregada: solo se puede modificar el comentario.</p>
    }
    else if (cancelada)
    {
        <p class="text-muted small">La orden está cancelada; sus insumos volvieron al inventario.</p>
    }

    <div class="table-responsive">
        <table id="tblDetalleOrden" class="table table-sm table-hover align-middle w-100"
               data-entry="@ViewBag.EntryActual" data-es-edicion="@esEdicion.ToString().ToLower()" data-lineas-editables="@lineasEditables.ToString().ToLower()">
            <thead>
                <tr>
                    <th>Tipo</th>
                    <th>Artículo</th>
                    <th>Descripción</th>
                    <th>Almacén</th>
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

    @if (lineasEditables)
    {
        <div id="panelLineaDetalle" class="border rounded p-3 mb-2 d-none">
            <h6 id="tituloPanelLinea" class="mb-2">Insumo del inventario</h6>
            <form id="formLineaDetalle">
                <input type="hidden" id="detNoLineaOriginal" value="" />
                <div class="row g-2">
                    <div class="col-md-4 position-relative">
                        <label class="form-label">Código de artículo</label>
                        <input type="text" id="detArtCodTexto" class="form-control" placeholder="Buscar por código..." autocomplete="off" />
                        <div class="invalid-feedback" id="detArtCodError">Selecciona una opción de la lista o borra el texto.</div>
                        <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detArtCodResultados"></ul>
                    </div>
                    <div class="col-md-4 position-relative">
                        <label class="form-label">Descripción de artículo</label>
                        <input type="text" id="detArtDescTexto" class="form-control" placeholder="Buscar por descripción..." autocomplete="off" />
                        <div class="invalid-feedback" id="detArtDescError">Selecciona una opción de la lista o borra el texto.</div>
                        <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detArtDescResultados"></ul>
                        <input type="hidden" id="detCodArticulo" />
                    </div>
                    <div class="col-md-4 position-relative" id="grupoAlmacen">
                        <label class="form-label">Almacén</label>
                        <input type="text" id="detCodAlmacenTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                        <input type="hidden" name="CodAlmacen" id="detCodAlmacen" />
                        <div class="invalid-feedback" id="detCodAlmacenError">Selecciona una opción de la lista o borra el texto.</div>
                        <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodAlmacenResultados"></ul>
                    </div>
                    <div class="col-md-4 position-relative">
                        <label class="form-label">Impuesto</label>
                        <input type="text" id="detCodigoImpuestoTexto" class="form-control" placeholder="Buscar por nombre..." autocomplete="off" />
                        <input type="hidden" id="detCodigoImpuesto" />
                        <div class="invalid-feedback" id="detCodigoImpuestoError">Selecciona una opción de la lista o borra el texto.</div>
                        <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodigoImpuestoResultados"></ul>
                    </div>

                    <div class="col-md-8">
                        <label class="form-label">Descripción</label>
                        <input name="Descripcion" id="detDescripcion" class="form-control" maxlength="200" />
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
    }

    @if (!esEdicion)
    {
        <script id="datosSeriesOrden" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(ViewBag.SeriesOrden, opcionesJson).Replace("</", "<\\/"))
        </script>
    }
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>
    @if (esEdicion && !cancelada && !entregada && User.TienePermiso("OrdenesServicio", Accion.Eliminar))
    {
        <button type="button" class="btn btn-outline-danger" id="btnCancelarOrden" data-entry="@ViewBag.EntryActual">
            <i class="fa-solid fa-ban me-1"></i>Cancelar orden
        </button>
    }
    @if (!cancelada && puedeEscribir)
    {
        <button type="button" class="btn btn-primary" id="btnGuardarOrden" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
            <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
        </button>
    }
</div>
```

- [ ] **Step 2: JS completo** — **reemplazar todo** `Web.UI/wwwroot/js/ordenesservicio.js` por (la primera parte es el listado de la Task 15, sin cambios):

```js
$(function () {
    // ================================================================ listado

    const datosPermisosEl = document.getElementById('datosPermisosOrdenesServicio');
    const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true, copiarA: {} };
    const destinosCopia = [
        permisos.copiarA && permisos.copiarA.factura && { nombre: 'Factura de venta', url: '/Facturas' }
    ].filter(Boolean);

    // Estados de la orden y a cuál avanza cada uno.
    const ESTADOS = {
        R: { nombre: 'Recibida', clase: 'text-bg-secondary', siguiente: 'P' },
        P: { nombre: 'En proceso', clase: 'text-bg-primary', siguiente: 'T' },
        T: { nombre: 'Terminada', clase: 'text-bg-warning', siguiente: 'E' },
        E: { nombre: 'Entregada', clase: 'text-bg-success', siguiente: null }
    };

    function insigniaEstado(row) {
        if (row && row.cancelado === 'S') return '<span class="badge text-bg-danger">Cancelada</span>';
        const e = ESTADOS[row && row.estadoServicio];
        return e ? `<span class="badge ${e.clase}">${e.nombre}</span>` : '';
    }

    const tabla = $('#tblOrdenesServicio').DataTable({
        ajax: { url: '/OrdenesServicio/ObtenerTodos', dataSrc: App.dataSrcTabla, data: d => { d.estado = $('#filtroEstado').val() || ''; } },
        order: [[0, 'desc']],
        columns: [
            { data: 'numDoc' },
            { data: 'fechaDoc', render: d => d ? new Date(d).toLocaleDateString() : '' },
            { data: 'nombreSn', render: (d, t, row) => d || row.codigoSn || '' },
            {
                data: 'placa',
                render: (d, t, row) => d ? `${d}${row.motoDescripcion ? ` <span class="text-muted small">${row.motoDescripcion}</span>` : ''}` : ''
            },
            { data: 'nombreMecanico', render: d => d || '' },
            { data: 'estadoServicio', render: (d, t, row) => insigniaEstado(row) },
            { data: 'totalDoc', render: d => d != null ? Number(d).toFixed(2) : '' },
            {
                data: 'entry', orderable: false, searchable: false, className: 'text-end',
                render: (entry, t, row) => {
                    const cancelada = !row || row.cancelado === 'S';
                    const estado = ESTADOS[row && row.estadoServicio];
                    const facturable = !cancelada && (row.estadoServicio === 'T' || row.estadoServicio === 'E');
                    const siguiente = estado && estado.siguiente ? ESTADOS[estado.siguiente] : null;
                    const puedeAvanzar = permisos.actualizar && !cancelada && siguiente;
                    return `
                        ${facturable ? App.botonCopiarA(70, entry, destinosCopia) : ''}
                        ${puedeAvanzar ? `<button class="btn btn-sm btn-outline-success btn-avanzar" data-entry="${entry}" data-estado="${estado.siguiente}" title="Pasar a ${siguiente.nombre}"><i class="fa-solid fa-forward-step"></i></button>` : ''}
                        <button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"${permisos.actualizar ? '' : ' title="Ver"'}><i class="fa-solid ${permisos.actualizar ? 'fa-pen' : 'fa-eye'}"></i></button>
                    `;
                }
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    $('#filtroEstado').on('change', recargarTabla);

    $('#tblOrdenesServicio').on('click', '.btn-avanzar', async function () {
        const entry = $(this).data('entry');
        const estado = $(this).data('estado');
        const confirmado = await Swal.fire({
            icon: 'question',
            title: '¿Cambiar el estado de la orden?',
            text: `La orden pasará a "${ESTADOS[estado].nombre}". No se puede volver a un estado anterior.`,
            showCancelButton: true,
            confirmButtonText: 'Cambiar',
            cancelButtonText: 'Cancelar'
        }).then(r => r.isConfirmed);
        if (!confirmado) return;

        const respuesta = await App.enviarJson(`/OrdenesServicio/CambiarEstado?entry=${entry}&estado=${estado}`, 'POST', {});
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito(`Orden ${ESTADOS[estado].nombre.toLowerCase()}.`);
        recargarTabla();
    });

    // ================================================================ formulario

    function abrirModal(html) {
        $('#contenidoModal').html(html);
        $('#modalFormulario').off('.autocompletar'); // limpia los listeners de la apertura anterior antes de que los buscadores registren los suyos
        new bootstrap.Modal('#modalFormulario').show();
        inicializarSerieOrden();
        inicializarBuscadorCliente();
        inicializarDetalle();
    }

    $('#btnNuevo').on('click', async function () {
        const html = await $.get('/OrdenesServicio/FormularioCrear');
        abrirModal(html);
    });

    $('#tblOrdenesServicio').on('click', '.btn-editar', async function () {
        const entry = $(this).data('entry');
        const html = await $.get('/OrdenesServicio/FormularioEditar', { entry });
        abrirModal(html);
    });

    // --- Cliente y motocicleta ---

    async function cargarMotos(codigoSn, seleccionar) {
        const $sel = $('#MotoEntry');
        $sel.empty().append($('<option>').val('').text('-- Seleccione --'));
        if (!codigoSn) return;
        try {
            const r = await $.get('/OrdenesServicio/MotosDeCliente', { codigoSn });
            const motos = (r.resultado && r.dato) ? r.dato : [];
            motos.forEach(m => {
                const detalle = [m.marca, m.modelo].filter(Boolean).join(' ');
                $sel.append($('<option>').val(m.entry).text(detalle ? `${m.placa} - ${detalle}` : m.placa));
            });
            if (seleccionar) $sel.val(String(seleccionar));
        } catch (e) { /* sin motos que ofrecer */ }
    }

    function inicializarBuscadorCliente() {
        if ($('#CodigoSnTexto').length === 0) return;
        App.autocompletar({
            texto: $('#CodigoSnTexto'),
            oculto: $('#CodigoSn'),
            lista: $('#CodigoSnResultados'),
            error: $('#CodigoSnError'),
            endpoint: '/OrdenesServicio/BuscarSocios',
            obtenerCodigo: s => s.codigo ?? s.Codigo,
            obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
            onSeleccion: async s => {
                $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '');
                await cargarMotos(s ? (s.codigo ?? s.Codigo) : null);
                const nueva = s ? (s.numLstPrecio ?? s.NumLstPrecio ?? null) : null;
                if (nueva === listaPrecioSocio) return;
                listaPrecioSocio = nueva;
                await ofrecerRecalcularLineas();
            }
        });
    }

    $(document).on('click', '#btnNuevaMoto', function () {
        if (!$('#CodigoSn').val()) {
            App.mostrarError('Selecciona primero el cliente.');
            return;
        }
        $('#panelMotoRapida').removeClass('d-none');
        $('#mrPlaca').trigger('focus');
    });

    $(document).on('click', '#btnCancelarMoto', function () {
        $('#panelMotoRapida').addClass('d-none');
    });

    $(document).on('click', '#btnGuardarMoto', async function () {
        const $boton = $(this);
        const codigoSn = $('#CodigoSn').val();
        if (!codigoSn) {
            App.mostrarError('Selecciona primero el cliente.');
            return;
        }
        const datos = {
            CodigoSn: codigoSn,
            Placa: $('#mrPlaca').val(),
            Marca: $('#mrMarca').val() || null,
            Modelo: $('#mrModelo').val() || null,
            Anio: $('#mrAnio').val() ? Number($('#mrAnio').val()) : null,
            Color: $('#mrColor').val() || null,
            Activo: 'S'
        };

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.enviarJson('/OrdenesServicio/CrearMoto', 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            await cargarMotos(codigoSn, respuesta.dato);
            $('#panelMotoRapida').addClass('d-none').find('input').val('');
            App.mostrarExito('Motocicleta registrada.');
        } finally {
            $boton.prop('disabled', false);
        }
    });

    // --- Serie de numeración (solo aplica al crear) ---

    function inicializarSerieOrden() {
        const $sel = $('#selectSerieOrden');
        if ($sel.length === 0) return;

        const datosEl = document.getElementById('datosSeriesOrden');
        const series = datosEl ? (JSON.parse(datosEl.textContent) || []) : [];

        $sel.empty();
        series.forEach(s => {
            const sigNumero = s.sigNumero ?? s.SigNumero;
            $sel.append($('<option>')
                .val(s.serie ?? s.Serie)
                .attr('data-manual', s.manual ?? s.Manual)
                .attr('data-sig-numero', sigNumero ?? '')
                .text(s.nombreSerie ?? s.NombreSerie));
        });

        // Preselecciona la serie "por defecto" configurada en "Numeración de documentos"; si no está
        // entre las opciones cae en la primera disponible.
        const serieDefecto = $sel.data('serie-defecto');
        const tieneSerieDefecto = serieDefecto !== undefined && serieDefecto !== '' &&
            $sel.find(`option[value="${serieDefecto}"]`).length > 0;
        if (tieneSerieDefecto) {
            $sel.val(String(serieDefecto));
        } else if ($sel.find('option').length > 0) {
            $sel.prop('selectedIndex', 0);
        }

        actualizarNumDocSegunSerie();
    }

    function esSerieManual() {
        const $sel = $('#selectSerieOrden');
        if ($sel.length === 0 || !$sel.val()) return true;
        return $sel.find('option:selected').data('manual') === 'S';
    }

    function actualizarNumDocSegunSerie() {
        const $numDoc = $('#NumDoc');
        if ($numDoc.length === 0 || $('#selectSerieOrden').length === 0) return;

        if (esSerieManual()) {
            $numDoc.val('').prop('disabled', false).attr('placeholder', '');
        } else {
            const sigNumero = $('#selectSerieOrden').find('option:selected').data('sig-numero');
            $numDoc.val(sigNumero ?? '').prop('disabled', true).attr('placeholder', 'Se generará al guardar');
        }
    }

    $(document).on('change', '#selectSerieOrden', actualizarNumDocSegunSerie);

    // --- Guardar y cancelar la orden ---

    $(document).on('click', '#btnGuardarOrden', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const entry = $boton.data('entry');

        // El número de documento no se solicita aquí para series no manuales: el servidor lo calcula y avanza
        // el consecutivo al registrar la orden. Para series Manual el campo #NumDoc está habilitado y viaja normal.
        const datos = App.recolectarFormulario('#formOrden');

        if (!esEdicion) {
            const serie = $('#selectSerieOrden').val();
            if (!serie) {
                App.mostrarError('Debes seleccionar una serie.');
                return;
            }
            datos.Serie = serie;
            if (!datos.CodigoSn) {
                App.mostrarError('Selecciona el cliente.');
                return;
            }
            if (!datos.MotoEntry) {
                App.mostrarError('Selecciona la motocicleta del cliente.');
                return;
            }
            if (!datos.MonedaDoc) {
                App.mostrarError('Selecciona la moneda.');
                return;
            }
            datos.Lineas = lineasLocales.map(App.sinCamposInternos);

            // Guardar queda deshabilitado mientras dura la petición: un doble clic no debe crear dos órdenes
            // ni descontar dos veces el inventario.
            $boton.prop('disabled', true);
            try {
                const respuesta = await App.enviarJson('/OrdenesServicio/Crear', 'POST', datos);
                if (!respuesta.resultado) {
                    App.mostrarError(respuesta.mensaje);
                    return;
                }
                const sufijoNumDoc = respuesta.numDoc != null ? ` No. documento: ${respuesta.numDoc}.` : '';
                await App.mostrarExito(`Orden de servicio creada correctamente.${sufijoNumDoc}`);
                bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
                recargarTabla();
            } finally {
                $boton.prop('disabled', false);
            }
            return;
        }

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.enviarJson(`/OrdenesServicio/Editar?entry=${entry}`, 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            App.mostrarExito('Orden de servicio actualizada correctamente.');
            recargarTabla();
        } finally {
            $boton.prop('disabled', false);
        }
    });

    $(document).on('click', '#btnCancelarOrden', async function () {
        const entry = $(this).data('entry');
        const confirmado = await Swal.fire({
            icon: 'warning',
            title: '¿Cancelar la orden de servicio?',
            text: 'Los insumos de la orden volverán al inventario. Esta acción no se puede deshacer.',
            showCancelButton: true,
            confirmButtonText: 'Cancelar orden',
            cancelButtonText: 'Volver',
            confirmButtonColor: '#dc3545'
        }).then(r => r.isConfirmed);
        if (!confirmado) return;

        const $btn = $(this).prop('disabled', true);
        try {
            const respuesta = await App.enviarJson(`/OrdenesServicio/Cancelar?entry=${entry}`, 'POST', {});
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            App.mostrarExito(respuesta.mensaje || 'Orden cancelada.');
            recargarTabla();
        } finally {
            $btn.prop('disabled', false);
        }
    });

    // ================================================================ líneas (insumos y servicios)
    // En el alta las líneas se administran localmente y viajan con la orden; en la edición cada operación
    // se guarda en vivo contra la API (con su movimiento de inventario).

    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let noLineaOriginalEnEdicion = null;
    let tasaImpuestoSeleccionado = 0;
    let buscadorArticuloCod, buscadorArticuloDesc, buscadorAlmacen, buscadorImpuesto;
    let sincronizandoArticulo = false;
    let listaPrecioSocio = null;
    let tipoLineaActual = 'I';   // 'I' = insumo (inventario), 'S' = servicio: decide qué artículos ofrece el buscador y si pide almacén

    function esEdicionDetalle() {
        const v = $('#tblDetalleOrden').data('es-edicion');
        return v === true || v === 'true';
    }

    function lineasEditables() {
        const v = $('#tblDetalleOrden').data('lineas-editables');
        return v === true || v === 'true';
    }

    function entryActual() {
        return $('#tblDetalleOrden').data('entry');
    }

    function esInsumoLinea(linea) {
        return (linea.esInsumo ?? linea.EsInsumo) === 'S' || linea._esInsumo === true;
    }

    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;
        tipoLineaActual = 'I';

        listaPrecioSocio = null;
        const codSnActual = $('#CodigoSn').val();
        if (codSnActual) {
            App.obtenerListaDeSocio(codSnActual).then(n => { listaPrecioSocio = n; });
        }

        const $tabla = $('#tblDetalleOrden');
        if ($tabla.length === 0) return;

        // Si la orden no se puede editar (entregada, cancelada o sin permiso) no hay panel de línea.
        if ($('#detArtCodTexto').length > 0) {
            buscadorArticuloCod = App.autocompletar({
                texto: $('#detArtCodTexto'), oculto: $('#detCodArticulo'),
                lista: $('#detArtCodResultados'), error: $('#detArtCodError'),
                endpoint: '/OrdenesServicio/BuscarArticulosPorCodigo',
                parametros: () => ({ tipo: tipoLineaActual }),
                obtenerCodigo: a => a.codigo ?? a.Codigo,
                obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
                onSeleccion: a => aplicarArticuloSeleccionado(a, 'cod')
            });
            buscadorArticuloDesc = App.autocompletar({
                texto: $('#detArtDescTexto'), oculto: $('#detCodArticulo'),
                lista: $('#detArtDescResultados'), error: $('#detArtDescError'),
                endpoint: '/OrdenesServicio/BuscarArticulos',
                parametros: () => ({ tipo: tipoLineaActual }),
                obtenerCodigo: a => a.codigo ?? a.Codigo,
                obtenerEtiqueta: a => `${a.nombre ?? a.Nombre} (${a.codigo ?? a.Codigo})`,
                onSeleccion: a => aplicarArticuloSeleccionado(a, 'desc')
            });
            buscadorAlmacen = App.autocompletar({
                texto: $('#detCodAlmacenTexto'), oculto: $('#detCodAlmacen'),
                lista: $('#detCodAlmacenResultados'), error: $('#detCodAlmacenError'),
                endpoint: '/OrdenesServicio/BuscarAlmacenes',
                obtenerCodigo: al => al.codigo ?? al.Codigo,
                obtenerEtiqueta: al => `${al.codigo ?? al.Codigo} - ${al.nombre ?? al.Nombre}`
            });
            buscadorImpuesto = App.autocompletar({
                texto: $('#detCodigoImpuestoTexto'), oculto: $('#detCodigoImpuesto'),
                lista: $('#detCodigoImpuestoResultados'), error: $('#detCodigoImpuestoError'),
                endpoint: '/OrdenesServicio/BuscarImpuestos',
                obtenerCodigo: i => i.codigo ?? i.Codigo,
                obtenerEtiqueta: i => `${i.nombre ?? i.Nombre} (${i.tasa ?? i.Tasa ?? 0}%)`,
                minCaracteres: 0,
                onSeleccion: i => {
                    tasaImpuestoSeleccionado = i ? Number(i.tasa ?? i.Tasa ?? 0) : 0;
                    recalcularLinea();
                }
            });
        }

        if (esEdicionDetalle()) {
            cargarDetalleRemoto();
        } else {
            pintarDetalle();
        }
    }

    async function cargarDetalleRemoto() {
        const respuesta = await $.get('/OrdenesServicio/ObtenerDetalle', { entry: entryActual() });
        lineasRemotas = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        pintarDetalle();
    }

    // En la edición los totales de la cabecera son los del servidor (él redondea por línea).
    async function refrescarTotalesServidor() {
        const r = await $.get('/OrdenesServicio/ObtenerUno', { entry: entryActual() });
        if (r.resultado && r.dato) {
            $('#TotalBruto').val(Number(r.dato.totalBruto ?? 0).toFixed(2));
            $('#TotalImp').val(Number(r.dato.totalImp ?? 0).toFixed(2));
            $('#TotalDoc').val(Number(r.dato.totalDoc ?? 0).toFixed(2));
        }
        recargarTabla();
    }

    function calcularTotalesDesdeLineas(lista) {
        let totalBruto = 0, totalImp = 0, totalDoc = 0;
        lista.forEach(l => {
            const cantidad = Number(l.cantidad ?? l.Cantidad ?? 0);
            const precio = Number(l.precio ?? l.Precio ?? 0);
            const prctjeDesc = Number(l.prctjeDesc ?? l.PrctjeDesc ?? 0);
            const impuesto = Number(l.impuesto ?? l.Impuesto ?? 0);
            const bruto = cantidad * precio;
            const desc = bruto * (prctjeDesc / 100);
            totalBruto += bruto;
            totalImp += impuesto;
            totalDoc += (bruto - desc + impuesto);
        });
        return { totalBruto: totalBruto.toFixed(2), totalImp: totalImp.toFixed(2), totalDoc: totalDoc.toFixed(2) };
    }

    // Al cambiar el cliente en el alta, ofrece recalcular los precios de las líneas ya cargadas.
    async function ofrecerRecalcularLineas() {
        if (esEdicionDetalle()) return;                 // en la edición las líneas ya guardadas conservan su precio
        if (!lineasLocales.length || listaPrecioSocio == null) return;

        const ok = await Swal.fire({
            icon: 'question',
            title: '¿Actualizar precios?',
            text: 'Se recalcularán los precios de las líneas según la lista de precio del cliente seleccionado.',
            showCancelButton: true,
            confirmButtonText: 'Actualizar',
            cancelButtonText: 'Mantener'
        }).then(r => r.isConfirmed);
        if (!ok) return;

        for (const l of lineasLocales) {
            const nuevo = await App.resolverPrecioLista(l.CodArticulo, listaPrecioSocio);
            if (nuevo == null) continue;
            const viejo = Number(l.Precio ?? 0);
            if (!viejo) continue;   // sin precio previo no se puede reescalar el impuesto en proporción
            const cant = Number(l.Cantidad ?? 0);
            const pd = Number(l.PrctjeDesc ?? 0);
            const impNuevo = Number((Number(l.Impuesto ?? 0) * (nuevo / viejo)).toFixed(2));
            const bruto = cant * nuevo;
            l.Precio = nuevo;
            l.Impuesto = impNuevo;
            l.TotalLinea = Number((bruto - bruto * (pd / 100) + impNuevo).toFixed(2));
        }
        pintarDetalle();
    }

    function pintarDetalle() {
        const $tbody = $('#tblDetalleOrden tbody');
        if ($tbody.length === 0) return;

        const edicion = esEdicionDetalle();
        const lista = edicion ? lineasRemotas : lineasLocales;

        // En el alta los totales se muestran con lo local (el servidor los recalcula al guardar).
        if (!edicion) {
            const totales = calcularTotalesDesdeLineas(lista);
            $('#TotalBruto').val(totales.totalBruto);
            $('#TotalImp').val(totales.totalImp);
            $('#TotalDoc').val(totales.totalDoc);
        }

        if (lista.length === 0) {
            $tbody.html('<tr><td colspan="10" class="text-center text-muted">Sin líneas</td></tr>');
            return;
        }

        const editables = lineasEditables();
        $tbody.empty();
        lista.forEach(linea => {
            const noLinea = linea.noLinea ?? linea.NoLinea;
            const clave = edicion ? noLinea : linea._id;
            const precio = linea.precio ?? linea.Precio;
            const impuesto = linea.impuesto ?? linea.Impuesto;
            const totalLinea = linea.totalLinea ?? linea.TotalLinea;
            const insumo = esInsumoLinea(linea);

            const $fila = $('<tr>')
                .append($('<td>').html(insumo ? '<span class="badge text-bg-primary">Insumo</span>' : '<span class="badge text-bg-info">Servicio</span>'))
                .append($('<td>').text(linea.codArticulo ?? linea.CodArticulo ?? ''))
                .append($('<td>').text(linea.descripcion ?? linea.Descripcion ?? ''))
                .append($('<td>').text(linea.codAlmacen ?? linea.CodAlmacen ?? ''))
                .append($('<td>').text(linea.cantidad ?? linea.Cantidad ?? ''))
                .append($('<td>').text(precio != null ? Number(precio).toFixed(2) : ''))
                .append($('<td>').text(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0))
                .append($('<td>').text(impuesto != null ? Number(impuesto).toFixed(2) : '0.00'))
                .append($('<td>').text(totalLinea != null ? Number(totalLinea).toFixed(2) : ''));
            const $acciones = $('<td class="text-end">');
            if (editables) {
                $acciones.html(`
                    <button type="button" class="btn btn-sm btn-outline-primary btn-editar-linea" data-clave="${clave}"><i class="fa-solid fa-pen"></i></button>
                    <button type="button" class="btn btn-sm btn-outline-danger btn-eliminar-linea" data-clave="${clave}"><i class="fa-solid fa-trash"></i></button>`);
            }
            $tbody.append($fila.append($acciones));
        });
    }

    // Sincroniza las dos cajas de artículo sin re-disparar onSeleccion (establecer() lo haría).
    function setArticuloEnAmbasCajas(item) {
        sincronizandoArticulo = true;
        try {
            buscadorArticuloCod.establecer(item || null);
            buscadorArticuloDesc.establecer(item || null);
            $('#detCodArticulo').val(item ? (item.codigo ?? item.Codigo) : '');
        } finally {
            sincronizandoArticulo = false;
        }
    }

    // Handler de selección del usuario en cualquiera de las dos cajas.
    function aplicarArticuloSeleccionado(a, origen) {
        if (sincronizandoArticulo) return; // corta la recursión de establecer()
        sincronizandoArticulo = true;
        try {
            $('#detCodArticulo').val(a ? (a.codigo ?? a.Codigo) : '');
            if (origen !== 'cod')  buscadorArticuloCod.establecer(a || null);
            if (origen !== 'desc') buscadorArticuloDesc.establecer(a || null);
        } finally {
            sincronizandoArticulo = false;
        }
        if (!a) { buscadorAlmacen.establecer(null); return; }
        $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
        aplicarPrecioDeArticulo(a);
        if (tipoLineaActual === 'I') aplicarAlmacenDefectoDeArticulo(a);
        recalcularLinea();
    }

    // Coloca en #detPrecio el precio de lista del cliente, o el PrecioUnitario base si no hay lista.
    async function aplicarPrecioDeArticulo(a) {
        const base = a.precioUnitario ?? a.PrecioUnitario ?? 0;
        const pLista = await App.resolverPrecioLista(a.codigo ?? a.Codigo, listaPrecioSocio);
        $('#detPrecio').val(pLista != null ? pLista : base);
        recalcularLinea();
    }

    async function aplicarAlmacenDefectoDeArticulo(a) {
        const cod = a.almacenDefecto ?? a.AlmacenDefecto ?? '';
        if (!cod) { buscadorAlmacen.establecer(null); return; }
        const r = await $.get('/OrdenesServicio/ObtenerAlmacenPorCodigo', { codigo: cod });
        buscadorAlmacen.establecer(r.resultado && r.dato ? r.dato : { codigo: cod, nombre: cod });
    }

    function limpiarPanelLinea() {
        $('#detNoLineaOriginal').val('');
        setArticuloEnAmbasCajas(null);
        buscadorAlmacen.establecer(null);
        buscadorImpuesto.establecer(null);
        tasaImpuestoSeleccionado = 0;
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        noLineaOriginalEnEdicion = null;
    }

    function mostrarPanelLinea() {
        $('#grupoAlmacen').toggleClass('d-none', tipoLineaActual === 'S');
        $('#tituloPanelLinea').text(tipoLineaActual === 'S' ? 'Servicio / mano de obra' : 'Insumo del inventario');
        $('#panelLineaDetalle').removeClass('d-none');
    }

    /** Recalcula el monto de impuesto y el total de la línea con base en los campos actuales del panel. */
    function recalcularLinea() {
        const cantidad = Number($('#detCantidad').val()) || 0;
        const precio = Number($('#detPrecio').val()) || 0;
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;
        const tasa = tasaImpuestoSeleccionado || 0;

        const bruto = cantidad * precio;
        const subtotal = bruto - bruto * (prctjeDesc / 100);
        const impuesto = subtotal * (tasa / 100);

        $('#detImpuestoMonto').val(impuesto.toFixed(2));
        $('#detTotalLinea').val((subtotal + impuesto).toFixed(2));
    }

    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc', recalcularLinea);

    $(document).on('click', '#btnNuevoInsumo', function () {
        tipoLineaActual = 'I';
        limpiarPanelLinea();
        mostrarPanelLinea();
    });

    $(document).on('click', '#btnNuevoServicio', function () {
        tipoLineaActual = 'S';
        limpiarPanelLinea();
        mostrarPanelLinea();
    });

    $(document).on('click', '#btnCancelarLinea', function () {
        $('#panelLineaDetalle').addClass('d-none');
    });

    $(document).on('click', '.btn-editar-linea', async function () {
        const clave = $(this).data('clave');
        const edicion = esEdicionDetalle();
        const linea = edicion
            ? lineasRemotas.find(l => (l.noLinea ?? l.NoLinea) === clave)
            : lineasLocales.find(l => l._id === clave);
        if (!linea) return;

        tipoLineaActual = esInsumoLinea(linea) ? 'I' : 'S';
        limpiarPanelLinea();
        noLineaOriginalEnEdicion = clave;

        const codArticulo = linea.codArticulo ?? linea.CodArticulo ?? '';
        const codAlmacen = linea.codAlmacen ?? linea.CodAlmacen ?? '';
        const codigoImpuesto = linea.codigoImpuesto ?? linea.CodigoImpuesto ?? '';

        $('#detNoLineaOriginal').val(clave);
        setArticuloEnAmbasCajas(codArticulo ? { codigo: codArticulo, nombre: linea.descripcion ?? linea.Descripcion ?? '' } : null);

        if (codAlmacen && tipoLineaActual === 'I') {
            const r = await $.get('/OrdenesServicio/ObtenerAlmacenPorCodigo', { codigo: codAlmacen });
            buscadorAlmacen.establecer(r.resultado && r.dato ? r.dato : { codigo: codAlmacen, nombre: codAlmacen });
        } else {
            buscadorAlmacen.establecer(null);
        }

        if (codigoImpuesto) {
            const r = await $.get('/OrdenesServicio/ObtenerImpuestoPorCodigo', { codigo: codigoImpuesto });
            buscadorImpuesto.establecer(r.resultado && r.dato ? r.dato : { codigo: codigoImpuesto, nombre: codigoImpuesto, tasa: 0 });
        } else {
            buscadorImpuesto.establecer(null);
        }

        $('#detDescripcion').val(linea.descripcion ?? linea.Descripcion ?? '');
        $('#detCantidad').val(linea.cantidad ?? linea.Cantidad ?? 1);
        $('#detPrecio').val(linea.precio ?? linea.Precio ?? '');
        $('#detPrctjeDesc').val(linea.prctjeDesc ?? linea.PrctjeDesc ?? 0);

        recalcularLinea();
        mostrarPanelLinea();
    });

    $(document).on('click', '.btn-eliminar-linea', async function () {
        const clave = $(this).data('clave');

        const confirmado = await App.confirmarEliminar(esEdicionDetalle()
            ? 'Se quitará la línea de la orden y, si es un insumo, volverá al inventario.'
            : 'Se quitará la línea de la orden.');
        if (!confirmado) return;

        if (esEdicionDetalle()) {
            const respuesta = await App.eliminar(`/OrdenesServicio/EliminarLinea?entry=${entryActual()}&noLinea=${clave}`);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            App.mostrarExito('Línea eliminada correctamente.');
            await cargarDetalleRemoto();
            await refrescarTotalesServidor();
        } else {
            lineasLocales = lineasLocales.filter(l => l._id !== clave);
            pintarDetalle();
        }
    });

    $(document).on('click', '#btnGuardarLinea', async function () {
        const $boton = $(this);
        const datosForm = App.recolectarFormulario('#formLineaDetalle');
        datosForm.CodArticulo = $('#detCodArticulo').val() || null;
        datosForm.CodigoImpuesto = $('#detCodigoImpuesto').val() || null;

        if (!datosForm.CodArticulo) {
            App.mostrarError('Selecciona un artículo.');
            return;
        }
        if (!(Number(datosForm.Cantidad) > 0)) {
            App.mostrarError('La cantidad debe ser mayor que cero.');
            return;
        }
        if (datosForm.Precio == null || !(Number(datosForm.Precio) >= 0)) {
            App.mostrarError('Indica un precio válido.');
            return;
        }
        if (!datosForm.CodigoImpuesto) {
            App.mostrarError('Selecciona el impuesto.');
            return;
        }
        if (tipoLineaActual === 'I' && !datosForm.CodAlmacen) {
            App.mostrarError('Selecciona el almacén del insumo.');
            return;
        }
        if (tipoLineaActual === 'S') {
            datosForm.CodAlmacen = null;   // el servidor usa el almacén por defecto del artículo, si lo tiene
        }

        if (esEdicionDetalle()) {
            const esEdicionLinea = noLineaOriginalEnEdicion !== null;
            const url = esEdicionLinea
                ? `/OrdenesServicio/EditarLinea?entry=${entryActual()}&noLinea=${noLineaOriginalEnEdicion}`
                : '/OrdenesServicio/CrearLinea';
            const datos = {
                CodArticulo: datosForm.CodArticulo,
                Descripcion: datosForm.Descripcion,
                CodAlmacen: datosForm.CodAlmacen,
                Cantidad: Number(datosForm.Cantidad),
                Precio: Number(datosForm.Precio),
                PrctjeDesc: Number(datosForm.PrctjeDesc) || 0,
                CodigoImpuesto: datosForm.CodigoImpuesto
            };
            if (!esEdicionLinea) datos.Entry = entryActual();

            $boton.prop('disabled', true);
            try {
                const respuesta = await App.enviarJson(url, 'POST', datos);
                if (!respuesta.resultado) {
                    App.mostrarError(respuesta.mensaje);
                    return;
                }
                App.mostrarExito(esEdicionLinea ? 'Línea actualizada correctamente.' : 'Línea agregada correctamente.');
                $('#panelLineaDetalle').addClass('d-none');
                await cargarDetalleRemoto();
                await refrescarTotalesServidor();
            } finally {
                $boton.prop('disabled', false);
            }
        } else {
            if (noLineaOriginalEnEdicion !== null) {
                lineasLocales = lineasLocales.map(l => l._id === noLineaOriginalEnEdicion
                    ? { ...datosForm, _id: l._id, _esInsumo: l._esInsumo }
                    : l);
            } else {
                lineasLocales.push({ ...datosForm, _id: proximoIdLocal++, _esInsumo: tipoLineaActual === 'I' });
            }
            $('#panelLineaDetalle').addClass('d-none');
            pintarDetalle();
        }
    });
});
```

- [ ] **Step 3: Verificar** — `node --check Web.UI/wwwroot/js/ordenesservicio.js`; `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` → 0 errores y 0 advertencias nuevas.

- [ ] **Step 4: Commit**

```bash
git add Web.UI/Views/OrdenesServicio/_Form.cshtml Web.UI/wwwroot/js/ordenesservicio.js
git commit -m "feat(taller): formulario de la orden de servicio con insumos y servicios, edicion con lineas en vivo y registro rapido de moto

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 5 — Verificación integral

### Task 17: Verificación integral (API + Web contra una base temporal, con recorrido en el navegador)

**Precondición:** Tasks 1–16 completas; ambos repos sin cambios sin commit salvo `appsettings.json` (API) y los 4 archivos ajenos (Web).

**Files:** ninguno (verificación en el scratchpad; no se commitea nada).

- [ ] **Step 1: Builds finales** — PowerShell: `dotnet test API.sln -p:BaseOutputPath="…\apitest\"` en la API → 0 fallos (igual que la Task 11); `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` en la Web → **0 errores** y sin advertencias nuevas (las 4 de los archivos ajenos no cuentan).

- [ ] **Step 2: Entorno temporal con Web** — repetir el Step 3 de la Task 11 con la base `API_DB_TALLER_B` y la API en `http://localhost:5199`; luego compilar y levantar la Web en `http://localhost:5200` apuntando a esa API (`$env:ApiSettings__BaseUrl = 'http://localhost:5199'`, `$env:ASPNETCORE_URLS = 'http://localhost:5200'`, `dotnet build Web.UI\Web.UI.csproj -p:BaseOutputPath="$SP\webcheck\"` y ejecutar la DLL resultante con `-WorkingDirectory "$repo\Web\Web.UI"`). Cargar los datos de partida corriendo la primera parte de `smoke-taller.ps1` (hasta crear mecánico y motocicleta: cliente `CL-0001`, proveedor, artículos `ACE-001` —insumo— y `MO-001` —servicio—, compra inicial de 20 `ACE-001`, mecánico `M01`, moto `M123ABC`), o creándolos por la API.

- [ ] **Step 3: Recorrido en el navegador** (Browser pane, sesión `admin` / `Admin123!`, ventana de 1440×900) — verificar y anotar cada resultado:

1. **Menú:** aparece el grupo **Taller** con "Órdenes de servicio", "Motocicletas" y "Mecánicos".
2. **Mecánicos:** crear `M02 – Pedro Gómez` → aparece en el listado; desactivarlo → el listado lo muestra con "No" y ya no aparece como opción en una orden nueva.
3. **Motocicletas:** crear una moto para `CL-0001` con placa en minúsculas (`p456def`) → el listado la muestra en mayúsculas; intentar repetir la placa → mensaje "Ya existe una motocicleta con la placa: P456DEF"; el botón **Historial** de `M123ABC` abre el modal (vacío al inicio: "Esta motocicleta no tiene órdenes de servicio.").
4. **Órdenes → Nuevo:** el modal abre con la serie preseleccionada, el número "Se generará al guardar", las fechas de documento e ingreso con el día de hoy (sin el error crudo de fecha nula). Elegir el cliente `CL-0001` → el selector de moto se llena con sus motos activas; el botón "+" registra una moto nueva (`q789ghi`, Honda, 2023) y la deja seleccionada.
5. **Insumos y servicios:** "Agregar insumo" ofrece solo `ACE-001` (no `MO-001`), trae precio y almacén por defecto; "Agregar servicio" ofrece solo `MO-001` y **no** muestra el almacén. Las dos líneas aparecen con la insignia "Insumo"/"Servicio" y los totales del formulario cambian. Guardar → "Orden de servicio creada correctamente. No. documento: N." y la fila aparece con placa, moto y mecánico. Comprobar en la base (`SELECT SUM(Disponible) FROM ExistenciaArticulo WHERE CodArticulo='ACE-001'`) que el stock bajó por la cantidad del insumo.
6. **Stock insuficiente:** una orden nueva con `ACE-001` × 999 → el mensaje de stock insuficiente se muestra, el modal sigue abierto y no se crea la orden ni baja el stock; la orden siguiente conserva el consecutivo (no se saltó ningún número).
7. **Edición en vivo (orden Recibida):** abrir la orden → cabecera editable; cambiar la cantidad del insumo (el stock se ajusta en la base), pasarla a una cantidad imposible (error y el stock queda igual), quitar la línea (el stock vuelve) y agregar otra; el "Total documento" se refresca en cada operación.
8. **Estados:** en el listado, el botón de avanzar lleva R → En proceso → Terminada; el botón "Copiar a" aparece **solo** en órdenes Terminadas o Entregadas; no hay forma de retroceder.
9. **Facturar:** "Copiar a → Factura de venta" desde la orden Terminada abre el formulario de Factura precargado (cliente, líneas con el texto "Orden de servicio N · abierto: X", almacén ya asignado también para el servicio); guardar → "Factura N creada desde Orden de servicio M"; el stock de `ACE-001` **no** vuelve a bajar. Abrir de nuevo "Copiar a" desde la misma orden → "El documento no tiene líneas abiertas."
10. **Integridad:** en la orden facturada, editar o quitar la línea facturada → mensaje "No se puede modificar la línea 1: la usan Factura …"; "Cancelar orden" → "…lo usan Factura …"; agregar un insumo nuevo es válido y queda abierto para una segunda factura (el botón "Copiar a" lo ofrece).
11. **Cancelaciones:** cancelar la factura (pantalla de Facturas) → la cantidad vuelve a estar abierta y el stock no cambia; luego "Cancelar orden" → éxito, la orden queda "Cancelada", el formulario en solo lectura y el stock vuelve al valor inicial.
12. **Entregada sin facturar:** avanzar otra orden hasta Entregada → al abrirla solo el comentario es editable y se guarda; no ofrece agregar líneas ni "Cancelar orden".
13. **Historial:** el botón de la moto `M123ABC` lista sus órdenes, la más reciente primero, con estado y total.
14. **Permisos:** con un usuario sin `Agregar` en `Facturas` el listado de órdenes no ofrece "Copiar a"; sin `Eliminar` en `OrdenesServicio` el formulario no ofrece "Cancelar orden"; sin `Agregar` en `Motocicletas` no aparece el "+" de moto (crear el usuario y su plantilla desde Administración; si no es viable, verificar el comportamiento con un usuario de la plantilla Vendedor del recorrido anterior y anotar la limitación).
15. **Búsqueda del listado:** escribir "factura" o "pedido" en la búsqueda de la tabla no marca todas las filas (la columna de acciones no participa en la búsqueda).
16. **Consola** del navegador sin errores de JavaScript durante todo el recorrido (`read_console_messages`; la consola se reinicia al navegar: revisarla en los puntos clave y al final).

Expected: todos los puntos se comportan como se describe; cualquier desvío se reporta con captura y se corrige en la tarea que corresponda (no se da por buena la verificación con desvíos abiertos).

- [ ] **Step 4: Limpieza (siempre)** — detener la API y la Web (por PID propio), borrar las variables de entorno de la sesión, eliminar `API_DB_TALLER_B` (`ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE …`) y comprobar que `SELECT name FROM sys.databases WHERE name LIKE 'API_DB%'` devuelve solo `API_DB_TEST`, que los puertos 5199 y 5200 no tienen listener y que `git status --short` de ambos repos solo muestra `appsettings.json` (API) y los 4 archivos ajenos (Web).

- [ ] **Step 5: Sin commit.** Reportar el resultado de cada punto del recorrido y la confirmación de limpieza.

---

## Cierre

Tras la Task 17: revisión final de rama (API y Web) con el skill `superpowers:requesting-code-review` (lentes sugeridos: coherencia de nombres entre capas y JSON camelCase/PascalCase; reglas de estado y de edición según estado; que ningún camino de la orden mueva inventario sin su asiento o devuelva de más —cambio de línea, cancelación, facturas canceladas—; permisos por módulo; XSS en los fragmentos HTML del listado y del formulario) y, si queda limpia, `superpowers:finishing-a-development-branch` (integración a decidir por el usuario; **sin push**).

Pendientes que quedan para el usuario al terminar: (1) ejecutar `sql/2026-10-22-ordenes-servicio-taller.sql` en `API_DB_TEST` (junto con las migraciones anteriores que aún tenga pendientes); (2) asignar los módulos del grupo **Taller** (`Mecanicos`, `Motocicletas`, `OrdenesServicio`) a las plantillas de permisos que correspondan (p. ej. un mecánico o recepcionista del taller necesita además `Ver` en Articulos/Almacenes/Impuestos y `Ver` en `Mecanicos` y `Motocicletas`); (3) fuera de alcance de esta versión y listados en el spec: impresión de la orden, presupuesto previo, comisiones, recordatorios.
