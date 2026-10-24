# Pagos recibidos (cobros a clientes) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Registrar los cobros de clientes (objeto 24) sobre facturas y notas de débito de cliente, con catálogo de formas de pago (efectivo, cheque, transferencia, tarjeta + referencia), saldo y estado de pago por factura calculados de los documentos, y botón "Registrar pago" en los listados de Facturas y Notas de débito.

**Architecture:** Documento propio `PagoRecibido` con dos tablas hijas (`PagoRecibidoFactura`: a qué documentos se aplica, `PagoRecibidoMedio`: con qué se pagó). Un servicio sin estado, `SaldoFacturaService`, calcula saldo = total − notas de crédito activas con esa factura base − pagos activos, y el estado Pendiente/Parcial/Pagada; `Factura` no cambia. `PagoRecibidoDomain` valida y guarda dentro de una transacción `Serializable` (dos pagos simultáneos no pueden exceder el saldo). Cancelar un pago solo lo marca; el saldo se recupera solo. `FacturaDomain`/`NotaDebitoDomain` rechazan cancelar o eliminar un documento con pagos activos y `NotaCreditoDomain` descuenta los pagos de su límite acreditable. La Web agrega Formas de pago, Pagos recibidos (listado, detalle, cancelar y página de alta estilo SAP) y columnas Pagado/Saldo/estado + botón "Registrar pago" en Facturas y Notas de débito.

**Tech Stack:** .NET 7 API (N-capas, EF Core 7 DB-first, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server (solo bases temporales para las pruebas reales), PowerShell/node para parches.

**Spec:** `docs/superpowers/specs/2026-10-23-pagos-recibidos-design.md` (repo API, commit `adae904`). Referencias de patrón en el código: `NotaCreditoDomain` (serie, validación de socio, transacción `Serializable` del límite acreditable, `Redondear`), `OrdenServicioDomain` (serie/`AsignarNumero`/cancelación dentro de `IEjecutorTransaccion`), `MecanicoDomain`/`MecanicoController`/`MecanicoApplication` (catálogo con baja lógica), `MecanicosController`/`mecanicos.js` (catálogo en modal), `ArticulosController.Crear` + `Views/Articulos/Crear.cshtml` (página de alta), `App.autocompletar` en `site.js`.

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. Antes de empezar: `git log --oneline -1` muestra `adae904` (spec) o posterior; `git status --short` solo con `API.Service.WebApi/appsettings.json` modificado. Línea base de pruebas: **1800** pasando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `315e15f` (o posterior). `git status --short` muestra **9 archivos ajenos** sin commit que NO se tocan ni se stagean: `Web.ApiClient/Dtos/Almacen/AlmacenActualizarDTO.cs`, `Web.ApiClient/Dtos/Almacen/AlmacenCrearDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloActualizarDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioActualizarDTO.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioCrearDTO.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`. Línea base: `dotnet build Web.slnx` con 0 errores.
- Build/test con `BaseOutputPath` en el scratchpad (el API de desarrollo del usuario puede estar corriendo y bloquear `bin\`; ejecutar **desde PowerShell**, en bash la barra final rompe el quoting):
  - API: `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`
  - Una sola clase de pruebas: `dotnet test API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="…\apitest\" --filter "FullyQualifiedName~NombreDeClase"` (`…\apitest\` = la ruta completa del scratchpad de arriba).
- SQL de pruebas: SOLO bases temporales (`API_DB_PAGOS_A`, `API_DB_PAGOS_B`); **jamás** `API_DB_TEST`. La contraseña de `sa` se extrae de `API.Service.WebApi\appsettings.json` (`[regex]::Match($cfg,'Password=([^;"]*)')`) a una variable de proceso y no se imprime ni se escribe en archivos. Toda base temporal se elimina al terminar (`ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE …`).

## Global Constraints

Cada tarea las incluye implícitamente.

- **Código de objeto:** pago recibido = `24` (`TipoObjeto='24'`, `NumeracionDocumento.CodigoObj='24'`, `SubTipoDoc='--'`). Los ocupados no se tocan: 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 13, 15, 59, 60, 70.
- **Módulos de permisos (grupo `Ventas`):** `PagosRecibidos` y `FormasPago`. Acciones `Ver`/`Agregar`/`Actualizar`/`Eliminar` (claims `Modulo:VAUE`). Cancelar un pago = `Eliminar` sobre `PagosRecibidos`.
- **Documentos cobrables:** filas de `Factura` con `DocSubType` `'--'` (factura) o `'ND'` (nota de débito de cliente), `Cancelado != 'S'`, del cliente del pago. `Factura.EstadoDoc` **no** se toca nunca.
- **Saldo:** `Saldo = Redondear(Total − Acreditado − Pagado)` (nunca negativo), con `Acreditado` = suma de `NotaCredito.TotalDoc` con `BaseTipo=6`, `BaseEntry=entry`, `Cancelado<>'S'` y `Pagado` = suma de `PagoRecibidoFactura.MontoAplicado` de pagos con `Cancelado<>'S'`. Estado: `"Pagada"` si `Saldo == 0`; `"Parcial"` si `Pagado > 0` y `Saldo > 0`; `"Pendiente"` en otro caso. `Redondear(x) = Math.Round(x, 2, MidpointRounding.AwayFromZero)`.
- **Pago:** al menos un documento y al menos una forma de pago; `Σ MontoAplicado == Σ Monto` y ese valor es `TotalPago` (lo calcula el servidor, el cliente no lo manda); cada `MontoAplicado` y cada `Monto` > 0 y con a lo más 2 decimales; `MontoAplicado <= Saldo` vigente (se valida **dentro** de la transacción `Serializable`); sin documentos repetidos; el cliente debe existir y ser de tipo `C` pero **no** se exige que esté activo.
- **Formas de pago (`FormaPago.TipoDato`):** `N` sin dato; `C` cheque (referencia = nº de cheque y banco obligatorios, `FechaCheque` opcional); `T` transferencia (referencia obligatoria); `J` tarjeta (referencia = autorización o últimos 4 dígitos, obligatoria). Para `N` se descartan referencia/banco/fecha; `FechaCheque` solo se conserva en `C`. Baja lógica (`DELETE` pone `Activo='N'`); `TipoDato` no puede cambiar si algún `PagoRecibidoMedio` usa la forma.
- **Cancelar un pago:** solo marca `Cancelado='S'` y `FechaCancelado`; un pago ya cancelado no se vuelve a cancelar; los pagos no se editan ni se eliminan.
- **Guardas sobre documentos existentes:** cancelar o eliminar una factura/nota de débito con pagos activos se rechaza con `"El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago."`; el límite de una nota de crédito con factura base pasa a `acreditado + nota <= total − pagado`.
- **Mensajes en español exactamente como aparecen** en los bloques de código del plan (las pruebas los verifican literalmente).
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` puede aparecer modificado: NUNCA commitearlo. Los 9 archivos ajenos de la Web (arriba) NUNCA se stagean ni se modifican. **Sin `git push`.**
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (CRLF/LF; comprobar con `file`); los archivos nuevos en LF; sin churn de EOL (`git diff --stat` de una edición pequeña no puede mostrar cientos de líneas). Comentarios y mensajes en español, estilo del repo.
- **Verificación al cerrar cada tarea de la API:** `dotnet build` sin errores y las pruebas de la tarea en verde; la suite completa (1800 + las nuevas) se corre en la Tarea 9.

## Ajustes respecto al spec (decididos al planificar, con el código leído)

1. **`Saldos` en un controlador aparte y por POST.** En vez de agregar acciones a `FacturaController` y `NotaDebitoController` (cambiaría sus constructores y pruebas), un `SaldoFacturaController` expone `POST api/Factura/Saldos` (Ver `Facturas`) y `POST api/NotaDebito/Saldos` (Ver `NotasDebito`) con rutas absolutas por acción y el cuerpo JSON `[1,2,3]`. El spec decía GET con `entries=…`, pero un listado grande no cabe en la URL (límite de ~8 KB de Kestrel). La Web los consume con un cliente tipado nuevo (`ISaldoFacturaApiClient`), sin tocar `IFacturaApiClient`/`INotaDebitoApiClient`.
2. **Guarda de pagos compartida** (`GuardaPagos.ValidarSinPagosAsync`) para no duplicar el mensaje entre `FacturaDomain` y `NotaDebitoDomain`. Se aplica en cancelar **y** en eliminar.
3. **Nota de crédito:** `FacturaAcreditable` y `FacturaBaseCredito` ganan `TotalPagado` (parámetro con valor por defecto 0, no rompe llamadas existentes) y el `Disponible` del selector "Basado en factura" pasa a `max(0, total − acreditado − pagado)`.
4. **`Registrar pago` desde el listado** abre `PagosRecibidos/Crear?codigoSn=…&facturaEntry=…` (el listado ya trae el cliente de la fila; el servidor re-valida todo al guardar).
5. **Alta de pago en página completa** (como Nuevo artículo) y detalle/cancelación en modal (como Mecánicos).

## File Structure

**API — entidades y datos**
- Crear `sql/2026-10-23-pagos-recibidos.sql`.
- Crear `API.Domain.Entity/Models/{FormaPago,PagoRecibido,PagoRecibidoFactura,PagoRecibidoMedio}.cs`; modificar `API.Domain.Entity/Models/ApiDbTestContext.cs`.
- Crear `API.Infraestructure.Repository/{FormaPago,PagoRecibido,PagoRecibidoFactura,PagoRecibidoMedio}Repositorio.cs`.

**API — dominio**
- Crear `API.Domain.Interface/{IFormaPagoDomain,ISaldoFacturaService,IPagoRecibidoDomain}.cs`.
- Crear `API.Domain.Core/{FormaPagoDomain,SaldoFacturaService,GuardaPagos,PagoRecibidoDomain}.cs`.
- Modificar `API.Domain.Core/{FacturaDomain,NotaDebitoDomain,NotaCreditoDomain}.cs`, `API.Domain.Interface/FacturaBaseCredito.cs`.

**API — aplicación / servicio web**
- Crear `API.Application.DTO/formaPago/…`, `API.Application.DTO/pagoRecibido/…`, `API.Application.Interface/{IFormaPago,IPagoRecibido,ISaldoFactura}Application.cs`, `API.Application.Main/{FormaPago,PagoRecibido,SaldoFactura}Application.cs`.
- Crear `API.Service.WebApi/Controllers/{FormaPago,PagoRecibido,SaldoFactura}Controller.cs`.
- Modificar `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`, `API.Application.Main/NotaCreditoApplication.cs`, `API.Application.DTO/notaCredito/{NotaCreditoFacturaBaseResumenDTO,NotaCreditoFacturaBaseDTO}.cs`, `sql/crear-api-db-test-completo.sql`.

**API — pruebas** (`API.Service.WebApi.Tests`)
- Crear `Domain/{ModeloPagosTests,FormaPagoDomainTests,SaldoFacturaServiceTests,PagoRecibidoDomainTests,GuardaPagosTests}.cs`, `Controllers/{FormaPagoControllerTests,PagoRecibidoControllerTests,SaldoFacturaControllerTests,PagosPermisosTests}.cs`, `Application/PagoRecibidoApplicationTests.cs`.
- Modificar `Domain/{FacturaDomainTests,NotaDebitoDomainTests,NotaCreditoDomainTests}.cs`, `Application/NotaCreditoApplicationTests.cs`.

**Web** (`Web.ApiClient`, `Web.UI`)
- Crear `Web.ApiClient/Dtos/{FormaPago,PagoRecibido,SaldoFactura}/…` y `Web.ApiClient/Clientes/{IFormaPago,IPagoRecibido,ISaldoFactura}ApiClient.cs` + implementaciones.
- Crear `Web.UI/Controllers/{FormasPago,PagosRecibidos}Controller.cs`, `Web.UI/Views/{FormasPago,PagosRecibidos}/…`, `Web.UI/wwwroot/js/{formaspago,pagosrecibidos,pagosrecibidos-crear}.js` (`site.js` no cambia: su mapa de etiquetas es solo de la cadena "Copiar a").
- Modificar `Web.UI/Program.cs`, `Web.UI/Views/Shared/_Layout.cshtml`, `Web.UI/Controllers/{Facturas,NotasDebito}Controller.cs`, `Web.UI/Views/{Facturas,NotasDebito}/Index.cshtml`, `Web.UI/wwwroot/js/{facturas,notasdebito}.js`, `Web.ApiClient/Dtos/{Factura/FacturaDTO,NotaDebito/NotaDebitoDTO}.cs`.

---

## Fase 1 — Base de datos, entidades y catálogo de formas de pago (API)

### Task 1: Script SQL, entidades EF, mapeo y repositorios de pagos

**Precondición:** `git log --oneline -1` en la API muestra `adae904` o posterior; `dotnet build API.sln -p:BaseOutputPath="…\apitest\"` (PowerShell) → 0 errores; `grep -n "FormaPago\|PagoRecibido" API.Domain.Entity/Models/ApiDbTestContext.cs` sin resultados.

**Files:**
- Create: `sql/2026-10-23-pagos-recibidos.sql`
- Create: `API.Domain.Entity/Models/FormaPago.cs`, `PagoRecibido.cs`, `PagoRecibidoFactura.cs`, `PagoRecibidoMedio.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs`
- Create: `API.Infraestructure.Repository/FormaPagoRepositorio.cs`, `PagoRecibidoRepositorio.cs`, `PagoRecibidoFacturaRepositorio.cs`, `PagoRecibidoMedioRepositorio.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/ModeloPagosTests.cs`

**Interfaces:**
- Produces (entidades, namespace `API.Domain.Entity.Models`): `FormaPago { string Codigo; string Nombre; string TipoDato; string Activo }`; `PagoRecibido { int Entry; int NumDoc; int Serie; string? Cancelado; string? NumManual; string? TipoObjeto; DateTime? FechaDoc; DateTime? FechaCancelado; string? CodigoSn; string? NombreSn; decimal TotalPago; string? Comentario; SocioNegocio? CodigoSnNavigation; NumeracionDocumentoDet? SerieNavigation }`; `PagoRecibidoFactura { int Entry; int NoLinea; int FacturaEntry; decimal MontoAplicado; Factura? FacturaNavigation }`; `PagoRecibidoMedio { int Entry; int NoLinea; string CodFormaPago; decimal Monto; string? Referencia; string? Banco; DateTime? FechaCheque; FormaPago? CodFormaPagoNavigation }`.
- Produces (repositorios): `IRepositorioGenerico<FormaPago, string>`, `<PagoRecibido, int>`, `<PagoRecibidoFactura, (int Entry, int NoLinea)>`, `<PagoRecibidoMedio, (int Entry, int NoLinea)>` registrados en DI. `PagoRecibidoFacturaRepositorio` incluye `FacturaNavigation` y `PagoRecibidoMedioRepositorio` incluye `CodFormaPagoNavigation` en `ObtenerTodoAsync`.
- Produces (SQL): tablas `FormaPago` (con 4 semillas), `PagoRecibido`, `PagoRecibidoFactura`, `PagoRecibidoMedio`; serie "Primario" y fila padre de numeración del objeto `'24'`; módulos `PagosRecibidos` y `FormasPago` (grupo Ventas).

- [ ] **Step 1: Escribir la prueba del modelo (fallará)** — crear `API.Service.WebApi.Tests/Domain/ModeloPagosTests.cs`:

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las entidades de pagos sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloPagosTests
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
        public void FormaPago_MapeaTablaYClave()
        {
            var et = Modelo().FindEntityType(typeof(FormaPago))!;
            Assert.Equal("FormaPago", et.GetTableName());
            Assert.Equal(new[] { "Codigo" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
        }

        [Fact]
        public void PagoRecibido_MapeaTablaClaveYColumnasDeSocio()
        {
            var et = Modelo().FindEntityType(typeof(PagoRecibido))!;
            Assert.Equal("PagoRecibido", et.GetTableName());
            Assert.Equal(new[] { "Entry" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("CodigoSN", et.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal("NombreSN", et.FindProperty("NombreSn")!.GetColumnName());
            Assert.Equal("decimal(19, 6)", et.FindProperty("TotalPago")!.GetColumnType());
        }

        [Fact]
        public void PagoRecibidoFactura_ClaveCompuestaYRelacionConFactura()
        {
            var et = Modelo().FindEntityType(typeof(PagoRecibidoFactura))!;
            Assert.Equal("PagoRecibidoFactura", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "FacturaEntry");
            Assert.Equal(typeof(Factura), fk.PrincipalEntityType.ClrType);
            Assert.Equal("decimal(19, 6)", et.FindProperty("MontoAplicado")!.GetColumnType());
        }

        [Fact]
        public void PagoRecibidoMedio_ClaveCompuestaYRelacionConFormaPago()
        {
            var et = Modelo().FindEntityType(typeof(PagoRecibidoMedio))!;
            Assert.Equal("PagoRecibidoMedio", et.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, et.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray());
            var fk = et.GetForeignKeys().Single(f => f.Properties.Single().Name == "CodFormaPago");
            Assert.Equal(typeof(FormaPago), fk.PrincipalEntityType.ClrType);
            Assert.Equal("decimal(19, 6)", et.FindProperty("Monto")!.GetColumnType());
        }
    }
}
```

- [ ] **Step 2: Correr la prueba y comprobar que falla** — PowerShell: `dotnet test API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="…\apitest\" --filter "FullyQualifiedName~ModeloPagosTests"`. Esperado: error de compilación (`FormaPago`/`PagoRecibido…` no existen).

- [ ] **Step 3: Crear las entidades** (namespace de archivo, igual que `Mecanico.cs`; LF):

`API.Domain.Entity/Models/FormaPago.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class FormaPago
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    /// <summary>N = sin dato, C = cheque, T = transferencia, J = tarjeta.</summary>
    public string TipoDato { get; set; } = "N";

    public string Activo { get; set; } = "S";
}
```

`API.Domain.Entity/Models/PagoRecibido.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class PagoRecibido
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public decimal TotalPago { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual NumeracionDocumentoDet? SerieNavigation { get; set; }
}
```

`API.Domain.Entity/Models/PagoRecibidoFactura.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class PagoRecibidoFactura
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    /// <summary>Entry de Factura: una factura (DocSubType '--') o una nota de débito de cliente ('ND').</summary>
    public int FacturaEntry { get; set; }

    public decimal MontoAplicado { get; set; }

    public virtual Factura? FacturaNavigation { get; set; }
}
```

`API.Domain.Entity/Models/PagoRecibidoMedio.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class PagoRecibidoMedio
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public string CodFormaPago { get; set; } = null!;

    public decimal Monto { get; set; }

    public string? Referencia { get; set; }

    public string? Banco { get; set; }

    public DateTime? FechaCheque { get; set; }

    public virtual FormaPago? CodFormaPagoNavigation { get; set; }
}
```

- [ ] **Step 4: Mapear en `ApiDbTestContext.cs`** (comprobar EOL con `file`). (a) Tras la línea `public virtual DbSet<Mecanico> Mecanicos { get; set; }` (≈ línea 20) agregar:

```csharp
    public virtual DbSet<FormaPago> FormaPagos { get; set; }

    public virtual DbSet<PagoRecibido> PagoRecibidos { get; set; }

    public virtual DbSet<PagoRecibidoFactura> PagoRecibidoFacturas { get; set; }

    public virtual DbSet<PagoRecibidoMedio> PagoRecibidoMedios { get; set; }
```

(b) Justo antes de `OnModelCreatingPartial(modelBuilder);` (al final de `OnModelCreating`, después del bloque `modelBuilder.Entity<OrdenServicioDetalle>`) agregar:

```csharp
        modelBuilder.Entity<FormaPago>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("pk_forma_pago");

            entity.ToTable("FormaPago");

            entity.Property(e => e.Codigo).HasMaxLength(10);
            entity.Property(e => e.Activo)
                .HasMaxLength(1)
                .HasDefaultValueSql("('S')");
            entity.Property(e => e.Nombre).HasMaxLength(50);
            entity.Property(e => e.TipoDato)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
        });

        modelBuilder.Entity<PagoRecibido>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_pago_recibido");

            entity.ToTable("PagoRecibido");

            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('24')");
            entity.Property(e => e.TotalPago).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany()
                .HasForeignKey(d => d.CodigoSn)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pago_recibido_sn");

            entity.HasOne(d => d.SerieNavigation).WithMany()
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pago_recibido_serie");
        });

        modelBuilder.Entity<PagoRecibidoFactura>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_pago_recibido_factura");

            entity.ToTable("PagoRecibidoFactura");

            entity.Property(e => e.MontoAplicado).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.FacturaNavigation).WithMany()
                .HasForeignKey(d => d.FacturaEntry)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pago_recibido_factura_factura");
        });

        modelBuilder.Entity<PagoRecibidoMedio>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_pago_recibido_medio");

            entity.ToTable("PagoRecibidoMedio");

            entity.Property(e => e.Banco).HasMaxLength(60);
            entity.Property(e => e.CodFormaPago).HasMaxLength(10);
            entity.Property(e => e.FechaCheque).HasColumnType("datetime");
            entity.Property(e => e.Monto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Referencia).HasMaxLength(50);

            entity.HasOne(d => d.CodFormaPagoNavigation).WithMany()
                .HasForeignKey(d => d.CodFormaPago)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_pago_recibido_medio_forma");
        });

```

- [ ] **Step 5: Crear los repositorios** (namespace `API.Infraestructure.Repository`, LF):

`FormaPagoRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class FormaPagoRepositorio : RepositorioGenericoEfCore<FormaPago, string>
    {
        public FormaPagoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`PagoRecibidoRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class PagoRecibidoRepositorio : RepositorioGenericoEfCore<PagoRecibido, int>
    {
        public PagoRecibidoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`PagoRecibidoFacturaRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class PagoRecibidoFacturaRepositorio : RepositorioGenericoEfCore<PagoRecibidoFactura, (int Entry, int NoLinea)>
    {
        public PagoRecibidoFacturaRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea), en el orden de HasKey en ApiDbTestContext.
        public override async Task<PagoRecibidoFactura?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }

        // El detalle del pago muestra el número y el tipo del documento cobrado.
        public override Task<IQueryable<PagoRecibidoFactura>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.FacturaNavigation).AsQueryable());
        }
    }
}
```

`PagoRecibidoMedioRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class PagoRecibidoMedioRepositorio : RepositorioGenericoEfCore<PagoRecibidoMedio, (int Entry, int NoLinea)>
    {
        public PagoRecibidoMedioRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        public override async Task<PagoRecibidoMedio?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }

        // El detalle del pago muestra el nombre y el tipo de dato de la forma de pago.
        public override Task<IQueryable<PagoRecibidoMedio>> ObtenerTodoAsync()
        {
            return Task.FromResult(DbSet.Include(x => x.CodFormaPagoNavigation).AsQueryable());
        }
    }
}
```

- [ ] **Step 6: DI** — en `API.Service.WebApi/Startup.cs`, junto a los repositorios del taller (tras la línea `services.AddTransient<IRepositorioGenerico<OrdenServicioDetalle, (int Entry, int NoLinea)>, OrdenServicioDetalleRepositorio>();`) agregar:

```csharp
            services.AddTransient<IRepositorioGenerico<FormaPago, string>, FormaPagoRepositorio>();
            services.AddTransient<IRepositorioGenerico<PagoRecibido, int>, PagoRecibidoRepositorio>();
            services.AddTransient<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>, PagoRecibidoFacturaRepositorio>();
            services.AddTransient<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>, PagoRecibidoMedioRepositorio>();
```

- [ ] **Step 7: Correr las pruebas del modelo** — mismo comando del Step 2. Esperado: 4 pruebas en verde.

- [ ] **Step 8: Escribir el script SQL** — crear `sql/2026-10-23-pagos-recibidos.sql` (UTF-8, LF):

```sql
-- Pagos recibidos (objeto 24): cobros a clientes sobre facturas y notas de debito de cliente.
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

-- ===== FormaPago (catalogo) =====
IF OBJECT_ID('dbo.FormaPago', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FormaPago (
        Codigo   nvarchar(10) NOT NULL,
        Nombre   nvarchar(50) NOT NULL,
        TipoDato nvarchar(1)  NOT NULL CONSTRAINT DF_FormaPago_TipoDato DEFAULT ('N') CONSTRAINT CK_FormaPago_TipoDato CHECK (TipoDato IN ('N','C','T','J')), -- N sin dato, C cheque, T transferencia, J tarjeta
        Activo   nvarchar(1)  NOT NULL CONSTRAINT DF_FormaPago_Activo DEFAULT ('S') CONSTRAINT CK_FormaPago_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_forma_pago PRIMARY KEY (Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'EFE') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('EFE', 'Efectivo', 'N');
IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'CHQ') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('CHQ', 'Cheque', 'C');
IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'TRF') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('TRF', 'Transferencia', 'T');
IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'TJT') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('TJT', 'Tarjeta', 'J');

-- ===== PagoRecibido (ObjType 24) =====
IF OBJECT_ID('dbo.PagoRecibido', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoRecibido (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_PagoRecibido_Cancelado DEFAULT ('N') CONSTRAINT CK_PagoRecibido_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_PagoRecibido_NumManual DEFAULT ('N') CONSTRAINT CK_PagoRecibido_NumManual CHECK (NumManual IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_PagoRecibido_TipoObjeto DEFAULT ('24') CONSTRAINT CK_PagoRecibido_TipoObjeto CHECK (TipoObjeto IN ('24')),
        FechaDoc       datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        TotalPago      decimal(19,6) NOT NULL CONSTRAINT CK_PagoRecibido_Total CHECK (TotalPago > 0),
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_pago_recibido PRIMARY KEY (Entry),
        CONSTRAINT fk_pago_recibido_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_pago_recibido_sn    FOREIGN KEY (CodigoSN) REFERENCES dbo.SocioNegocio(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PagoRecibido_CodigoSN' AND object_id = OBJECT_ID('dbo.PagoRecibido'))
    CREATE NONCLUSTERED INDEX IX_PagoRecibido_CodigoSN ON dbo.PagoRecibido (CodigoSN) INCLUDE (Cancelado);

-- ===== PagoRecibidoFactura (a que documentos se aplica; FK a Factura cubre facturas y notas de debito) =====
IF OBJECT_ID('dbo.PagoRecibidoFactura', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoRecibidoFactura (
        Entry         int           NOT NULL,
        NoLinea       int           NOT NULL,
        FacturaEntry  int           NOT NULL,
        MontoAplicado decimal(19,6) NOT NULL CONSTRAINT CK_PagoRecibidoFactura_Monto CHECK (MontoAplicado > 0),
        CONSTRAINT pk_pago_recibido_factura PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT uq_pago_recibido_factura_doc UNIQUE (Entry, FacturaEntry),
        CONSTRAINT fk_pago_recibido_factura_pago    FOREIGN KEY (Entry)        REFERENCES dbo.PagoRecibido(Entry),
        CONSTRAINT fk_pago_recibido_factura_factura FOREIGN KEY (FacturaEntry) REFERENCES dbo.Factura(Entry)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PagoRecibidoFactura_Factura' AND object_id = OBJECT_ID('dbo.PagoRecibidoFactura'))
    CREATE NONCLUSTERED INDEX IX_PagoRecibidoFactura_Factura ON dbo.PagoRecibidoFactura (FacturaEntry) INCLUDE (MontoAplicado);

-- ===== PagoRecibidoMedio (con que se pago) =====
IF OBJECT_ID('dbo.PagoRecibidoMedio', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoRecibidoMedio (
        Entry        int           NOT NULL,
        NoLinea      int           NOT NULL,
        CodFormaPago nvarchar(10)  NOT NULL,
        Monto        decimal(19,6) NOT NULL CONSTRAINT CK_PagoRecibidoMedio_Monto CHECK (Monto > 0),
        Referencia   nvarchar(50)  NULL,
        Banco        nvarchar(60)  NULL,
        FechaCheque  datetime      NULL,
        CONSTRAINT pk_pago_recibido_medio PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_pago_recibido_medio_pago  FOREIGN KEY (Entry)        REFERENCES dbo.PagoRecibido(Entry),
        CONSTRAINT fk_pago_recibido_medio_forma FOREIGN KEY (CodFormaPago) REFERENCES dbo.FormaPago(Codigo)
    );
END

-- ===== Numeracion (serie "Primario" y fila padre para la pantalla "Numeracion de documentos") =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '24')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('24', (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet), 'Primario', 'N', '--', 'D', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '24' AND SubTipoDoc = '--')
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SubTipoDoc, DocAlias, SerieDfct)
    SELECT '24', '--', 'Pago recibido', MIN(Serie) FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '24';

-- ===== Catalogo de permisos (grupo Ventas) =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('PagosRecibidos', 'Pagos recibidos', 'Ventas'),
    ('FormasPago', 'Formas de pago', 'Ventas')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Pagos recibidos DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('FormaPago','PagoRecibido','PagoRecibidoFactura','PagoRecibidoMedio') ORDER BY name;
SELECT Codigo, TipoDato FROM dbo.FormaPago ORDER BY Codigo;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '24';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('PagosRecibidos','FormasPago');
```

- [ ] **Step 9: Validar el script contra una base temporal real** (PowerShell; no imprime la contraseña; elimina la base al terminar):

```powershell
$ErrorActionPreference = 'Stop'
$SP = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
$repo = 'C:\Users\migue\source\repos\angelm0508\API'
$cfg = Get-Content "$repo\API.Service.WebApi\appsettings.json" -Raw
$pw = [regex]::Match($cfg, 'Password=([^;"]*)').Groups[1].Value
$db = 'API_DB_PAGOS_A'
if (((sqlcmd -S localhost -U sa -P $pw -C -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='$db'") -join '').Trim() -ne '0') { throw "$db ya existe" }
$sql = (Get-Content "$repo\sql\crear-api-db-test-completo.sql" -Raw -Encoding UTF8) -replace 'API_DB_TEST', $db
if ($sql -match 'API_DB_TEST') { throw 'guard: API_DB_TEST sigue presente' }
$tmp = "$SP\$db.sql"; $sql | Set-Content $tmp -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i $tmp | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'fallo el script completo' }
1..2 | ForEach-Object {
    sqlcmd -S localhost -U sa -P $pw -C -d $db -b -f 65001 -i "$repo\sql\2026-10-23-pagos-recibidos.sql" | Select-Object -Last 14
    if ($LASTEXITCODE -ne 0) { throw "fallo la corrida $_ del script de pagos" }
}
sqlcmd -S localhost -U sa -P $pw -C -d $db -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM FormaPago; SELECT COUNT(*) FROM Modulo WHERE Codigo IN ('PagosRecibidos','FormasPago'); SELECT COUNT(*) FROM NumeracionDocumentoDet WHERE CodigoObj='24';"
sqlcmd -S localhost -U sa -P $pw -C -Q "ALTER DATABASE $db SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $db;"
```

Esperado: la segunda corrida no falla; los conteos son `4`, `2` y `1` (idempotente: no duplica semillas ni serie); la base queda eliminada. Si la corrida falla, corregir el script (no la prueba) y repetir.

- [ ] **Step 10: Build y commit**

```bash
git add sql/2026-10-23-pagos-recibidos.sql API.Domain.Entity/Models/FormaPago.cs API.Domain.Entity/Models/PagoRecibido.cs API.Domain.Entity/Models/PagoRecibidoFactura.cs API.Domain.Entity/Models/PagoRecibidoMedio.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Infraestructure.Repository/FormaPagoRepositorio.cs API.Infraestructure.Repository/PagoRecibidoRepositorio.cs API.Infraestructure.Repository/PagoRecibidoFacturaRepositorio.cs API.Infraestructure.Repository/PagoRecibidoMedioRepositorio.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ModeloPagosTests.cs
git commit -m "feat(pagos): script SQL, entidades EF, mapeo y repositorios de pagos recibidos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Catálogo de formas de pago (dominio, aplicación, controlador, DTOs, mapeo, DI)

**Precondición:** Task 1 completa (`git log --oneline -1` muestra su commit; `ModeloPagosTests` en verde).

**Files:**
- Create: `API.Domain.Interface/IFormaPagoDomain.cs`, `API.Domain.Core/FormaPagoDomain.cs`
- Create: `API.Application.DTO/formaPago/FormaPagoDTO.cs`, `FormaPagoCrearDTO.cs`, `FormaPagoActualizarDTO.cs`
- Create: `API.Application.Interface/IFormaPagoApplication.cs`, `API.Application.Main/FormaPagoApplication.cs`
- Create: `API.Service.WebApi/Controllers/FormaPagoController.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/FormaPagoDomainTests.cs`, `API.Service.WebApi.Tests/Controllers/FormaPagoControllerTests.cs`

**Interfaces:**
- Consumes (Task 1): `FormaPago`, `PagoRecibidoMedio`, `IRepositorioGenerico<FormaPago, string>`, `IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>`.
- Produces: `IFormaPagoDomain { Task<bool> InsertarAsync(FormaPago); Task<bool> ActualizarAsync(string codigo, FormaPago); Task<bool> EliminarAsync(string codigo); Task<FormaPago?> ObtenerPorCodigoAsync(string); Task<IQueryable<FormaPago>> ObtenerTodoAsync() }`; `IFormaPagoApplication` (mismos métodos con `Respuesta<…>`: `InsertarAsync(FormaPagoCrearDTO)`, `ActualizarAsync(string, FormaPagoActualizarDTO)`, `EliminarAsync(string)`, `ObtenerPorCodigoAsync(string)`, `ObtenerAsync()`); endpoint `api/FormaPago` (módulo `FormasPago`).

- [ ] **Step 1: Escribir las pruebas del dominio (fallarán)** — crear `API.Service.WebApi.Tests/Domain/FormaPagoDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FormaPagoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repo = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>> _repoMedio = new();
        private readonly FormaPagoDomain _domain;

        public FormaPagoDomainTests()
        {
            _domain = new FormaPagoDomain(_repo.Object, _repoMedio.Object);
            _repo.Setup(r => r.InsertarAsync(It.IsAny<FormaPago>())).ReturnsAsync((FormaPago f) => f);
            _repo.Setup(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>())).ReturnsAsync(true);
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoMedio>().AsAsyncQueryable());
        }

        private void MedioUsaForma(string codigo) =>
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoMedio>
            {
                new() { Entry = 1, NoLinea = 1, CodFormaPago = codigo, Monto = 10m }
            }.AsAsyncQueryable());

        private async Task<string> MensajeAlInsertar(FormaPago f)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(f));
            return ex.Message;
        }

        [Fact]
        public async Task InsertarAsync_Valida_GuardaConTipoYActivoPorDefecto()
        {
            var ok = await _domain.InsertarAsync(new FormaPago { Codigo = " EFE ", Nombre = " Efectivo " });

            Assert.True(ok);
            _repo.Verify(r => r.InsertarAsync(It.Is<FormaPago>(f => f.Codigo == "EFE" && f.Nombre == "Efectivo" && f.TipoDato == "N" && f.Activo == "S")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinCodigo_Lanza()
        {
            Assert.Equal("El código de la forma de pago es requerido.", await MensajeAlInsertar(new FormaPago { Codigo = "  ", Nombre = "Efectivo" }));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<FormaPago>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SinNombre_Lanza()
        {
            Assert.Equal("El nombre de la forma de pago es requerido.", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "" }));
        }

        [Fact]
        public async Task InsertarAsync_TipoDatoInvalido_Lanza()
        {
            Assert.Equal("El tipo de dato debe ser 'N', 'C', 'T' o 'J'.", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", TipoDato = "X" }));
        }

        [Fact]
        public async Task InsertarAsync_ActivoInvalido_Lanza()
        {
            Assert.Equal("El campo Activo debe ser 'S' o 'N'.", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", Activo = "X" }));
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(new FormaPago { Codigo = "EFE", Nombre = "Otro", TipoDato = "N", Activo = "S" });

            Assert.Equal("Ya existe un registro con el código: EFE", await MensajeAlInsertar(new FormaPago { Codigo = "EFE", Nombre = "Efectivo" }));
        }

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            Assert.False(await _domain.ActualizarAsync("NOPE", new FormaPago { Nombre = "X" }));
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SinNombre_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(new FormaPago { Codigo = "EFE", Nombre = "Efectivo", TipoDato = "N", Activo = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("EFE", new FormaPago { Nombre = " " }));

            Assert.Equal("El nombre de la forma de pago es requerido.", ex.Message);
        }

        [Fact]
        public async Task ActualizarAsync_TipoDatoNulo_ConservaElActual()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });

            await _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque bancario", TipoDato = null!, Activo = "S" });

            _repo.Verify(r => r.ActualizarAsync("CHQ", It.Is<FormaPago>(f => f.Nombre == "Cheque bancario" && f.TipoDato == "C")), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_CambiaTipoDatoConPagosQueLaUsan_Lanza()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });
            MedioUsaForma("CHQ");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque", TipoDato = "T", Activo = "S" }));

            Assert.Equal("No se puede cambiar el tipo de dato: la forma de pago ya se usó en pagos.", ex.Message);
            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiaTipoDatoSinPagos_Actualiza()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });

            Assert.True(await _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque", TipoDato = "T", Activo = "S" }));
        }

        [Fact]
        public async Task ActualizarAsync_MismoTipoDatoConPagos_Actualiza()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });
            MedioUsaForma("CHQ");

            Assert.True(await _domain.ActualizarAsync("CHQ", new FormaPago { Nombre = "Cheque bancario", TipoDato = "C", Activo = "S" }));
        }

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            Assert.False(await _domain.EliminarAsync("NOPE"));
        }

        [Fact]
        public async Task EliminarAsync_BajaLogica_ConservaNombreYTipo()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "S" });

            Assert.True(await _domain.EliminarAsync("CHQ"));

            _repo.Verify(r => r.ActualizarAsync("CHQ", It.Is<FormaPago>(f => f.Activo == "N" && f.Nombre == "Cheque" && f.TipoDato == "C")), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_YaInactiva_NoVuelveAActualizar()
        {
            _repo.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C", Activo = "N" });

            Assert.True(await _domain.EliminarAsync("CHQ"));

            _repo.Verify(r => r.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPago>()), Times.Never);
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que falla** — `… --filter "FullyQualifiedName~FormaPagoDomainTests"`. Esperado: error de compilación (`FormaPagoDomain` no existe).

- [ ] **Step 3: Interfaz y dominio**

`API.Domain.Interface/IFormaPagoDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IFormaPagoDomain
    {
        #region async methods
        Task<bool> InsertarAsync(FormaPago obj);
        Task<bool> ActualizarAsync(string codigo, FormaPago obj);
        /// <summary>Baja lógica: la forma de pago pasa a Activo='N' (nunca se borra, puede tener pagos).</summary>
        Task<bool> EliminarAsync(string codigo);
        Task<FormaPago?> ObtenerPorCodigoAsync(string codigo);
        Task<IQueryable<FormaPago>> ObtenerTodoAsync();
        #endregion
    }
}
```

`API.Domain.Core/FormaPagoDomain.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class FormaPagoDomain : IFormaPagoDomain
    {
        private readonly IRepositorioGenerico<FormaPago, string> _repo;
        private readonly IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> _repoMedio;

        public FormaPagoDomain(
            IRepositorioGenerico<FormaPago, string> repo,
            IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> repoMedio)
        {
            _repo = repo;
            _repoMedio = repoMedio;
        }

        #region async methods
        public async Task<bool> InsertarAsync(FormaPago obj)
        {
            obj.Codigo = (obj.Codigo ?? string.Empty).Trim();
            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Codigo.Length == 0)
                throw new Exception("El código de la forma de pago es requerido.");
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre de la forma de pago es requerido.");
            obj.TipoDato = TipoDatoValido(obj.TipoDato);
            obj.Activo = ActivoValido(obj.Activo);

            if (await _repo.ObtenerAsync(obj.Codigo) != null)
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");

            await _repo.InsertarAsync(obj);
            return true;
        }

        public async Task<bool> ActualizarAsync(string codigo, FormaPago obj)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            obj.Nombre = (obj.Nombre ?? string.Empty).Trim();
            if (obj.Nombre.Length == 0)
                throw new Exception("El nombre de la forma de pago es requerido.");
            obj.TipoDato = string.IsNullOrWhiteSpace(obj.TipoDato) ? existente.TipoDato : TipoDatoValido(obj.TipoDato);
            obj.Activo = ActivoValido(obj.Activo);

            // El tipo de dato fija qué referencia exige cada pago ya registrado: no puede cambiar si la forma se usó.
            if (obj.TipoDato != existente.TipoDato && await TieneMediosAsync(codigo))
                throw new Exception("No se puede cambiar el tipo de dato: la forma de pago ya se usó en pagos.");

            return await _repo.ActualizarAsync(codigo, obj);
        }

        public async Task<bool> EliminarAsync(string codigo)
        {
            var existente = await _repo.ObtenerAsync(codigo);
            if (existente is null)
                return false;

            if (existente.Activo == "N")
                return true;

            return await _repo.ActualizarAsync(codigo, new FormaPago
            {
                Nombre = existente.Nombre,
                TipoDato = existente.TipoDato,
                Activo = "N"
            });
        }

        public async Task<FormaPago?> ObtenerPorCodigoAsync(string codigo)
        {
            return await _repo.ObtenerAsync(codigo);
        }

        public async Task<IQueryable<FormaPago>> ObtenerTodoAsync()
        {
            return await _repo.ObtenerTodoAsync();
        }
        #endregion

        private async Task<bool> TieneMediosAsync(string codigo)
        {
            var medios = await _repoMedio.ObtenerTodoAsync();
            return await medios.AnyAsync(m => m.CodFormaPago == codigo);
        }

        private static string TipoDatoValido(string? tipo)
        {
            if (string.IsNullOrWhiteSpace(tipo))
                return "N";
            if (tipo != "N" && tipo != "C" && tipo != "T" && tipo != "J")
                throw new Exception("El tipo de dato debe ser 'N', 'C', 'T' o 'J'.");
            return tipo;
        }

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

- [ ] **Step 4: DTOs** (namespace `API.Application.DTO.formaPago`):

`FormaPagoDTO.cs`:

```csharp
namespace API.Application.DTO.formaPago
{
    public class FormaPagoDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        /// <summary>N = sin dato, C = cheque, T = transferencia, J = tarjeta.</summary>
        public string TipoDato { get; set; } = "N";
        public string Activo { get; set; } = "S";
    }
}
```

`FormaPagoCrearDTO.cs`:

```csharp
namespace API.Application.DTO.formaPago
{
    public class FormaPagoCrearDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; }
        public string? Activo { get; set; }
    }
}
```

`FormaPagoActualizarDTO.cs`:

```csharp
namespace API.Application.DTO.formaPago
{
    public class FormaPagoActualizarDTO
    {
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; }
        public string? Activo { get; set; }
    }
}
```

- [ ] **Step 5: Aplicación y controlador**

`API.Application.Interface/IFormaPagoApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.formaPago;

namespace API.Application.Interface
{
    public interface IFormaPagoApplication
    {
        #region async methods
        Task<Respuesta<bool>> InsertarAsync(FormaPagoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, FormaPagoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
        Task<Respuesta<FormaPagoDTO>> ObtenerPorCodigoAsync(string codigo);
        Task<Respuesta<IEnumerable<FormaPagoDTO>>> ObtenerAsync();
        #endregion
    }
}
```

`API.Application.Main/FormaPagoApplication.cs` (mismo esquema que `MecanicoApplication`):

```csharp
using API.Application.DTO;
using API.Application.DTO.formaPago;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class FormaPagoApplication : IFormaPagoApplication
    {
        private readonly IFormaPagoDomain _domain;
        private readonly IMapper _mapper;

        public FormaPagoApplication(IFormaPagoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<bool>> InsertarAsync(FormaPagoCrearDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.InsertarAsync(_mapper.Map<FormaPago>(obj));
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

        public async Task<Respuesta<bool>> ActualizarAsync(string codigo, FormaPagoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.ActualizarAsync(codigo, _mapper.Map<FormaPago>(obj));
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

        public async Task<Respuesta<FormaPagoDTO>> ObtenerPorCodigoAsync(string codigo)
        {
            var respuesta = new Respuesta<FormaPagoDTO>();
            try
            {
                var forma = await _domain.ObtenerPorCodigoAsync(codigo);
                respuesta.Dato = _mapper.Map<FormaPagoDTO>(forma);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<FormaPagoDTO>>> ObtenerAsync()
        {
            var respuesta = new Respuesta<IEnumerable<FormaPagoDTO>>();
            try
            {
                var formas = await (await _domain.ObtenerTodoAsync()).OrderBy(x => x.Nombre).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<FormaPagoDTO>>(formas);
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

`API.Service.WebApi/Controllers/FormaPagoController.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.formaPago;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/FormaPago")]
    public class FormaPagoController : ControllerBase
    {
        private const string NoEncontrada = "Código de forma de pago no encontrado.";

        private readonly IFormaPagoApplication _application;

        public FormaPagoController(IFormaPagoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("FormasPago", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<FormaPagoDTO>>>> ObtenerTodo()
        {
            var formas = await _application.ObtenerAsync();

            if (!formas.Resultado)
                return BadRequest(formas);

            return Ok(formas);
        }

        [RequierePermiso("FormasPago", Accion.Ver)]
        [HttpGet("{codigo}")]
        public async Task<ActionResult<Respuesta<FormaPagoDTO>>> ObtenerPorCodigo([FromRoute] string codigo)
        {
            var forma = await _application.ObtenerPorCodigoAsync(codigo);

            if (!forma.Resultado)
                return BadRequest(forma);

            if (forma.Dato == null)
            {
                forma.Resultado = false;
                forma.Mensaje = NoEncontrada;
                return NotFound(forma);
            }

            return Ok(forma);
        }

        [RequierePermiso("FormasPago", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<bool>>> Crear([FromBody] FormaPagoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        [RequierePermiso("FormasPago", Accion.Actualizar)]
        [HttpPut("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Actualizar([FromRoute] string codigo, [FromBody] FormaPagoActualizarDTO obj)
        {
            var forma = await _application.ObtenerPorCodigoAsync(codigo);

            if (forma.Dato == null)
            {
                forma.Resultado = false;
                forma.Mensaje = NoEncontrada;
                return NotFound(forma);
            }

            var actualizar = await _application.ActualizarAsync(codigo, obj);

            if (!actualizar.Resultado)
                return BadRequest(actualizar);

            return Ok(actualizar);
        }

        [RequierePermiso("FormasPago", Accion.Eliminar)]
        [HttpDelete("{codigo}")]
        public async Task<ActionResult<Respuesta<bool>>> Eliminar([FromRoute] string codigo)
        {
            var forma = await _application.ObtenerPorCodigoAsync(codigo);

            if (forma.Dato == null)
            {
                forma.Resultado = false;
                forma.Mensaje = NoEncontrada;
                return NotFound(forma);
            }

            var eliminar = await _application.EliminarAsync(codigo);

            if (!eliminar.Resultado)
                return BadRequest(eliminar);

            return Ok(eliminar);
        }
    }
}
```

- [ ] **Step 6: Mapeo y DI**
  - `API.Transversal.Mapper/PerfilMapeo.cs`: agregar `using API.Application.DTO.formaPago;` junto a los `using` de DTO (el archivo empieza con BOM; conservarlo) y, tras el bloque `// Mecanico`, agregar:

```csharp
            // FormaPago
            CreateMap<FormaPago, FormaPagoDTO>();
            CreateMap<FormaPagoCrearDTO, FormaPago>();
            CreateMap<FormaPagoActualizarDTO, FormaPago>();
```

  - `API.Service.WebApi/Startup.cs`, junto a las líneas de repositorios de pagos de la Task 1, agregar:

```csharp
            services.AddTransient<IFormaPagoDomain, FormaPagoDomain>();
            services.AddTransient<IFormaPagoApplication, FormaPagoApplication>();
```

- [ ] **Step 7: Pruebas del controlador** — crear `API.Service.WebApi.Tests/Controllers/FormaPagoControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.formaPago;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class FormaPagoControllerTests
    {
        private readonly Mock<IFormaPagoApplication> _app = new();

        private FormaPagoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<FormaPagoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<FormaPagoDTO>> { Resultado = true, Dato = new[] { new FormaPagoDTO() } };
            _app.SetupSequence(a => a.ObtenerAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodo()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodo()).Result).Value);
        }

        [Fact]
        public async Task ObtenerPorCodigo_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("XXX")).ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ObtenerPorCodigo("XXX");

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("Código de forma de pago no encontrado.", Assert.IsType<Respuesta<FormaPagoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new FormaPagoCrearDTO { Codigo = "EFE", Nombre = "Efectivo" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerPorCodigoAsync("XXX")).ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Actualizar("XXX", new FormaPagoActualizarDTO { Nombre = "X" });

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<string>(), It.IsAny<FormaPagoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_Ok_CuandoExiste()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            var dto = new FormaPagoActualizarDTO { Nombre = "Efectivo caja" };
            _app.Setup(a => a.ObtenerPorCodigoAsync("EFE")).ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = new FormaPagoDTO { Codigo = "EFE" } });
            _app.Setup(a => a.ActualizarAsync("EFE", dto)).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Actualizar("EFE", dto)).Result).Value);
        }

        [Fact]
        public async Task Eliminar_NotFoundYOk()
        {
            _app.SetupSequence(a => a.ObtenerPorCodigoAsync("EFE"))
                .ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = null! })
                .ReturnsAsync(new Respuesta<FormaPagoDTO> { Resultado = true, Dato = new FormaPagoDTO { Codigo = "EFE" } });
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.EliminarAsync("EFE")).ReturnsAsync(buena);

            Assert.IsType<NotFoundObjectResult>((await Crear().Eliminar("EFE")).Result);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Eliminar("EFE")).Result).Value);
        }
    }
}
```

- [ ] **Step 8: Correr** — `… --filter "FullyQualifiedName~FormaPago"` (dominio + controlador). Esperado: todo en verde. Luego `dotnet build API.sln` sin errores.

- [ ] **Step 9: Commit**

```bash
git add API.Domain.Interface/IFormaPagoDomain.cs API.Domain.Core/FormaPagoDomain.cs API.Application.DTO/formaPago API.Application.Interface/IFormaPagoApplication.cs API.Application.Main/FormaPagoApplication.cs API.Service.WebApi/Controllers/FormaPagoController.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/FormaPagoDomainTests.cs API.Service.WebApi.Tests/Controllers/FormaPagoControllerTests.cs
git commit -m "feat(pagos): catalogo de formas de pago (dominio, aplicacion, controlador y pruebas)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 2 — Saldo y documento de pago (API)

### Task 3: `SaldoFacturaService` (saldo y estado de pago de facturas)

**Precondición:** Task 2 completa; `git log --oneline -1` muestra su commit.

**Files:**
- Create: `API.Domain.Interface/ISaldoFacturaService.cs`, `API.Domain.Core/SaldoFacturaService.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/SaldoFacturaServiceTests.cs`

**Interfaces:**
- Consumes (Task 1): `IRepositorioGenerico<PagoRecibido, int>`, `IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>`; existentes: `IRepositorioGenerico<Factura, int>`, `IRepositorioGenerico<NotaCredito, int>` (`NotaCredito.BaseTipo`, `BaseEntry`, `TotalDoc`, `Cancelado`).
- Produces (namespace `API.Domain.Interface`): `record SaldoFactura(int FacturaEntry, decimal Total, decimal Acreditado, decimal Pagado, decimal Saldo, string Estado)`; `record PagoAplicado(int PagoEntry, int PagoNumDoc, decimal Monto)`; `static class EstadoPagoFactura { const string Pendiente = "Pendiente"; const string Parcial = "Parcial"; const string Pagada = "Pagada"; }`; `interface ISaldoFacturaService { Task<IReadOnlyDictionary<int, SaldoFactura>> ObtenerAsync(IEnumerable<int> facturaEntries); Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaEntry); }`. Una factura inexistente no aparece en el diccionario.

- [ ] **Step 1: Escribir las pruebas (fallarán)** — crear `API.Service.WebApi.Tests/Domain/SaldoFacturaServiceTests.cs`:

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
    public class SaldoFacturaServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _repoNota = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibido, int>> _repoPago = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>> _repoPagoFactura = new();
        private readonly SaldoFacturaService _servicio;

        private readonly List<Factura> _facturas = new();
        private readonly List<NotaCredito> _notas = new();
        private readonly List<PagoRecibido> _pagos = new();
        private readonly List<PagoRecibidoFactura> _lineas = new();

        public SaldoFacturaServiceTests()
        {
            _servicio = new SaldoFacturaService(_repoFactura.Object, _repoNota.Object, _repoPago.Object, _repoPagoFactura.Object);
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facturas.AsAsyncQueryable());
            _repoNota.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _notas.AsAsyncQueryable());
            _repoPago.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pagos.AsAsyncQueryable());
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _lineas.AsAsyncQueryable());
        }

        private void Factura(int entry, decimal total) => _facturas.Add(new Factura { Entry = entry, NumDoc = entry * 10, TotalDoc = total });

        private void Nota(int baseEntry, decimal total, string cancelado = "N", int baseTipo = 6) =>
            _notas.Add(new NotaCredito { Entry = _notas.Count + 1, BaseTipo = baseTipo, BaseEntry = baseEntry, TotalDoc = total, Cancelado = cancelado });

        private void Pago(int pagoEntry, int numDoc, int facturaEntry, decimal monto, string cancelado = "N")
        {
            if (_pagos.All(p => p.Entry != pagoEntry))
                _pagos.Add(new PagoRecibido { Entry = pagoEntry, NumDoc = numDoc, Cancelado = cancelado });
            _lineas.Add(new PagoRecibidoFactura { Entry = pagoEntry, NoLinea = _lineas.Count + 1, FacturaEntry = facturaEntry, MontoAplicado = monto });
        }

        [Fact]
        public async Task ObtenerAsync_SinPagosNiNotas_SaldoEsElTotalYEstadoPendiente()
        {
            Factura(1, 100m);

            var r = await _servicio.ObtenerAsync(new[] { 1 });

            var s = r[1];
            Assert.Equal(100m, s.Total);
            Assert.Equal(0m, s.Acreditado);
            Assert.Equal(0m, s.Pagado);
            Assert.Equal(100m, s.Saldo);
            Assert.Equal("Pendiente", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoParcial_EstadoParcial()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 40m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(40m, s.Pagado);
            Assert.Equal(60m, s.Saldo);
            Assert.Equal("Parcial", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoTotal_EstadoPagada()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 100m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_PagoCancelado_NoCuenta()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 100m, cancelado: "S");

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Pagado);
            Assert.Equal(100m, s.Saldo);
            Assert.Equal("Pendiente", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoActivaDescuentaElSaldo_PeroNoLaCancelada()
        {
            Factura(1, 100m);
            Nota(1, 30m);
            Nota(1, 50m, cancelado: "S");

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(30m, s.Acreditado);
            Assert.Equal(70m, s.Saldo);
            Assert.Equal("Pendiente", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoDeOtroTipoBase_NoCuenta()
        {
            Factura(1, 100m);
            Nota(1, 30m, baseTipo: 5);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Acreditado);
            Assert.Equal(100m, s.Saldo);
        }

        [Fact]
        public async Task ObtenerAsync_NotaDeCreditoPorElTotal_SaldoCeroYPagada()
        {
            Factura(1, 100m);
            Nota(1, 100m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_NotaYPago_RestanAmbos()
        {
            Factura(1, 100m);
            Nota(1, 20m);
            Pago(1, 7, 1, 30m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(50m, s.Saldo);
            Assert.Equal("Parcial", s.Estado);
        }

        [Fact]
        public async Task ObtenerAsync_VariasFacturas_CadaUnaConSuSaldo()
        {
            Factura(1, 100m);
            Factura(2, 200m);
            Factura(3, 300m);
            Pago(1, 7, 1, 100m);
            Pago(2, 8, 2, 50m);

            var r = await _servicio.ObtenerAsync(new[] { 1, 2, 3 });

            Assert.Equal(3, r.Count);
            Assert.Equal("Pagada", r[1].Estado);
            Assert.Equal(150m, r[2].Saldo);
            Assert.Equal(300m, r[3].Saldo);
        }

        [Fact]
        public async Task ObtenerAsync_SoloDevuelveLasPedidas()
        {
            Factura(1, 100m);
            Factura(2, 200m);

            var r = await _servicio.ObtenerAsync(new[] { 2 });

            Assert.Single(r);
            Assert.True(r.ContainsKey(2));
        }

        [Fact]
        public async Task ObtenerAsync_FacturaInexistente_NoApareceEnElDiccionario()
        {
            Factura(1, 100m);

            var r = await _servicio.ObtenerAsync(new[] { 1, 99 });

            Assert.Single(r);
            Assert.False(r.ContainsKey(99));
        }

        [Fact]
        public async Task ObtenerAsync_ListaVacia_DevuelveVacioSinConsultar()
        {
            var r = await _servicio.ObtenerAsync(Array.Empty<int>());

            Assert.Empty(r);
            _repoFactura.Verify(x => x.ObtenerTodoAsync(), Times.Never);
        }

        [Fact]
        public async Task ObtenerAsync_RedondeaADosDecimales()
        {
            Factura(1, 100.005m);
            Pago(1, 7, 1, 0.004m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(100.01m, s.Saldo);   // 100.005 -> 100.01 y 0.004 -> 0.00: cada componente se redondea antes de restar
        }

        [Fact]
        public async Task ObtenerAsync_SaldoNegativoPorDatosExternos_SeInformaCero()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 120m);

            var s = (await _servicio.ObtenerAsync(new[] { 1 }))[1];

            Assert.Equal(0m, s.Saldo);
            Assert.Equal("Pagada", s.Estado);
        }

        [Fact]
        public async Task ObtenerPagosActivosAsync_SoloLosNoCancelados_ConNumeroYMonto()
        {
            Factura(1, 100m);
            Pago(1, 7, 1, 30m);
            Pago(2, 8, 1, 20m, cancelado: "S");
            Pago(3, 9, 1, 10m);
            Pago(4, 10, 2, 99m);

            var pagos = await _servicio.ObtenerPagosActivosAsync(1);

            Assert.Equal(new[] { 7, 9 }, pagos.Select(p => p.PagoNumDoc).ToArray());
            Assert.Equal(new[] { 30m, 10m }, pagos.Select(p => p.Monto).ToArray());
        }

        [Fact]
        public async Task ObtenerPagosActivosAsync_SinPagos_DevuelveListaVacia()
        {
            Factura(1, 100m);

            Assert.Empty(await _servicio.ObtenerPagosActivosAsync(1));
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que falla** — `… --filter "FullyQualifiedName~SaldoFacturaServiceTests"`. Esperado: error de compilación.

- [ ] **Step 3: Interfaz** — `API.Domain.Interface/ISaldoFacturaService.cs`:

```csharp
namespace API.Domain.Interface
{
    /// <summary>Saldo de una factura o nota de débito de cliente: total − notas de crédito activas − pagos activos.</summary>
    public sealed record SaldoFactura(int FacturaEntry, decimal Total, decimal Acreditado, decimal Pagado, decimal Saldo, string Estado);

    /// <summary>Un pago activo aplicado a un documento (para el mensaje de "cancele primero el pago").</summary>
    public sealed record PagoAplicado(int PagoEntry, int PagoNumDoc, decimal Monto);

    public static class EstadoPagoFactura
    {
        public const string Pendiente = "Pendiente";
        public const string Parcial = "Parcial";
        public const string Pagada = "Pagada";
    }

    public interface ISaldoFacturaService
    {
        /// <summary>
        /// Saldo y estado de pago de las facturas pedidas, con tres consultas agrupadas (no una por factura).
        /// Una factura inexistente no aparece en el diccionario.
        /// </summary>
        Task<IReadOnlyDictionary<int, SaldoFactura>> ObtenerAsync(IEnumerable<int> facturaEntries);

        /// <summary>Pagos no cancelados aplicados a un documento, del más antiguo al más reciente.</summary>
        Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaEntry);
    }
}
```

- [ ] **Step 4: Implementación** — `API.Domain.Core/SaldoFacturaService.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class SaldoFacturaService : ISaldoFacturaService
    {
        // NotaCredito.BaseTipo de una nota basada en una factura de venta.
        private const int TipoBaseFactura = 6;

        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<NotaCredito, int> _repoNota;
        private readonly IRepositorioGenerico<PagoRecibido, int> _repoPago;
        private readonly IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> _repoPagoFactura;

        public SaldoFacturaService(
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<NotaCredito, int> repoNota,
            IRepositorioGenerico<PagoRecibido, int> repoPago,
            IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> repoPagoFactura)
        {
            _repoFactura = repoFactura;
            _repoNota = repoNota;
            _repoPago = repoPago;
            _repoPagoFactura = repoPagoFactura;
        }

        public async Task<IReadOnlyDictionary<int, SaldoFactura>> ObtenerAsync(IEnumerable<int> facturaEntries)
        {
            var resultado = new Dictionary<int, SaldoFactura>();
            var ids = (facturaEntries ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (ids.Count == 0)
                return resultado;

            var facturas = await (await _repoFactura.ObtenerTodoAsync())
                .Where(f => ids.Contains(f.Entry))
                .Select(f => new { f.Entry, Total = f.TotalDoc ?? 0m })
                .ToListAsync();
            if (facturas.Count == 0)
                return resultado;

            var notas = await (await _repoNota.ObtenerTodoAsync())
                .Where(n => n.BaseTipo == TipoBaseFactura && n.Cancelado != "S" && n.BaseEntry != null && ids.Contains(n.BaseEntry.Value))
                .Select(n => new { Base = n.BaseEntry, Total = n.TotalDoc ?? 0m })
                .ToListAsync();
            var acreditado = notas
                .GroupBy(n => n.Base!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(n => n.Total));

            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            var pagos = await _repoPago.ObtenerTodoAsync();
            var aplicados = await (from pf in lineas
                                   join p in pagos on pf.Entry equals p.Entry
                                   where ids.Contains(pf.FacturaEntry) && p.Cancelado != "S"
                                   select new { pf.FacturaEntry, pf.MontoAplicado }).ToListAsync();
            var pagado = aplicados
                .GroupBy(a => a.FacturaEntry)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.MontoAplicado));

            foreach (var f in facturas)
            {
                var total = Redondear(f.Total);
                var acred = Redondear(acreditado.TryGetValue(f.Entry, out var a) ? a : 0m);
                var pag = Redondear(pagado.TryGetValue(f.Entry, out var p) ? p : 0m);
                var saldo = Math.Max(0m, Redondear(total - acred - pag));
                var estado = saldo == 0m
                    ? EstadoPagoFactura.Pagada
                    : pag > 0m ? EstadoPagoFactura.Parcial : EstadoPagoFactura.Pendiente;
                resultado[f.Entry] = new SaldoFactura(f.Entry, total, acred, pag, saldo, estado);
            }
            return resultado;
        }

        public async Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaEntry)
        {
            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            var pagos = await _repoPago.ObtenerTodoAsync();
            var filas = await (from pf in lineas
                               join p in pagos on pf.Entry equals p.Entry
                               where pf.FacturaEntry == facturaEntry && p.Cancelado != "S"
                               orderby p.Entry
                               select new { p.Entry, p.NumDoc, pf.MontoAplicado }).ToListAsync();
            return filas.Select(f => new PagoAplicado(f.Entry, f.NumDoc, f.MontoAplicado)).ToList();
        }

        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
```

- [ ] **Step 5: DI** — en `Startup.cs`, junto a las líneas de pagos agregar: `services.AddTransient<ISaldoFacturaService, SaldoFacturaService>();`

- [ ] **Step 6: Correr** — `… --filter "FullyQualifiedName~SaldoFacturaServiceTests"`. Esperado: 16 pruebas en verde.

- [ ] **Step 7: Commit**

```bash
git add API.Domain.Interface/ISaldoFacturaService.cs API.Domain.Core/SaldoFacturaService.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/SaldoFacturaServiceTests.cs
git commit -m "feat(pagos): servicio de saldo y estado de pago de facturas

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `PagoRecibidoDomain` (alta con validaciones y transacción serializable, cancelación, consultas)

**Precondición:** Task 3 completa; `git log --oneline -1` muestra su commit; `SaldoFacturaServiceTests` en verde.

**Files:**
- Create: `API.Domain.Interface/IPagoRecibidoDomain.cs`, `API.Domain.Core/PagoRecibidoDomain.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/PagoRecibidoDomainTests.cs`

**Interfaces:**
- Consumes: Task 1 (`PagoRecibido`, `PagoRecibidoFactura`, `PagoRecibidoMedio`, `FormaPago` y sus repositorios), Task 3 (`ISaldoFacturaService`, `SaldoFactura`); existentes: `IRepositorioGenerico<NumeracionDocumentoDet, int>`, `<SocioNegocio, string>` (`TipoSn`, `Nombre`), `<Factura, int>` (`CodigoSn`, `Cancelado`, `DocSubType`, `NumDoc`, `FechaDoc`), `IEjecutorTransaccion` (`EjecutarAsync<T>(Func<Task<T>>)` y `EjecutarAsync<T>(Func<Task<T>>, IsolationLevel)`).
- Produces (namespace `API.Domain.Interface`): `record DocumentoPorCobrar(Factura Documento, SaldoFactura Saldo)`; `interface IPagoRecibidoDomain { Task<int> InsertarAsync(PagoRecibido obj, IEnumerable<PagoRecibidoFactura> facturas, IEnumerable<PagoRecibidoMedio> medios); Task<bool> CancelarAsync(int id); Task<PagoRecibido?> ObtenerAsync(int id); Task<IQueryable<PagoRecibido>> ObtenerTodoAsync(); Task<IEnumerable<PagoRecibidoFactura>> ObtenerFacturasAsync(int id); Task<IEnumerable<PagoRecibidoMedio>> ObtenerMediosAsync(int id); Task<IReadOnlyList<DocumentoPorCobrar>> ObtenerDocumentosPorCobrarAsync(string codigoSn); }`. `InsertarAsync` devuelve el `Entry` del pago.

**Orden de validación de `InsertarAsync`** (cada paso lanza `Exception` con el mensaje exacto del código de abajo): serie → cliente → documentos → formas de pago → cuadre → número → transacción `Serializable` { saldos vigentes → cabecera → líneas }. Toda validación ocurre **antes** de numerar, así un error no avanza el consecutivo de la serie.

- [ ] **Step 1: Escribir las pruebas (fallarán)** — crear `API.Service.WebApi.Tests/Domain/PagoRecibidoDomainTests.cs`:

```csharp
using System.Data;
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PagoRecibidoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<PagoRecibido, int>> _repoPago = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)>> _repoPagoFactura = new();
        private readonly Mock<IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)>> _repoMedio = new();
        private readonly Mock<IRepositorioGenerico<FormaPago, string>> _repoForma = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<ISaldoFacturaService> _saldos = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly PagoRecibidoDomain _domain;

        public PagoRecibidoDomainTests()
        {
            _domain = new PagoRecibidoDomain(
                _repoPago.Object, _repoPagoFactura.Object, _repoMedio.Object, _repoForma.Object,
                _repoNumeracion.Object, _repoSocio.Object, _repoFactura.Object, _saldos.Object, _tx.Object);

            // El doble del ejecutor corre el Func directo (sin transaccion ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()))
                .Returns<Func<Task<int>>, IsolationLevel>((f, _) => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoPago.Setup(r => r.InsertarAsync(It.IsAny<PagoRecibido>()))
                .ReturnsAsync((PagoRecibido p) => { p.Entry = 99; return p; });
            _repoPagoFactura.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PagoRecibidoFactura>())).Returns(Task.CompletedTask);
            _repoMedio.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PagoRecibidoMedio>())).Returns(Task.CompletedTask);
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoFactura>().AsAsyncQueryable());
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoMedio>().AsAsyncQueryable());

            // Valores por defecto validos: cada test rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Cliente SN1", TipoSn = "C", Activo = "S" });
            _repoFactura.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(Doc(1, 10));
            _repoFactura.Setup(r => r.ObtenerAsync(2)).ReturnsAsync(Doc(2, 20, "ND"));
            _repoForma.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(Forma("EFE", "Efectivo", "N"));
            _repoForma.Setup(r => r.ObtenerAsync("CHQ")).ReturnsAsync(Forma("CHQ", "Cheque", "C"));
            _repoForma.Setup(r => r.ObtenerAsync("TRF")).ReturnsAsync(Forma("TRF", "Transferencia", "T"));
            _repoForma.Setup(r => r.ObtenerAsync("TJT")).ReturnsAsync(Forma("TJT", "Tarjeta", "J"));
            SaldoDisponible(100m);
        }

        private void SaldoDisponible(decimal saldo) =>
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) => (IReadOnlyDictionary<int, SaldoFactura>)ids.Distinct()
                    .ToDictionary(i => i, i => new SaldoFactura(i, 100m, 0m, 100m - saldo, saldo, "Pendiente")));

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string? codigoObj = null) => new()
        {
            CodigoObj = codigoObj ?? "24", Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "D"
        };

        private static Factura Doc(int entry, int numDoc, string subTipo = "--", string cliente = "SN1", string cancelado = "N") => new()
        {
            Entry = entry, NumDoc = numDoc, CodigoSn = cliente, Cancelado = cancelado, DocSubType = subTipo,
            FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 100m
        };

        private static FormaPago Forma(string codigo, string nombre, string tipo, string activo = "S") =>
            new() { Codigo = codigo, Nombre = nombre, TipoDato = tipo, Activo = activo };

        private static PagoRecibido Pago(Action<PagoRecibido>? ajustar = null)
        {
            var p = new PagoRecibido { Serie = 4, CodigoSn = "SN1", FechaDoc = new DateTime(2026, 10, 23) };
            ajustar?.Invoke(p);
            return p;
        }

        private static PagoRecibidoFactura Aplicar(int facturaEntry, decimal monto) => new() { FacturaEntry = facturaEntry, MontoAplicado = monto };

        private static PagoRecibidoMedio Medio(string forma, decimal monto, string? referencia = null, string? banco = null, DateTime? fechaCheque = null) =>
            new() { CodFormaPago = forma, Monto = monto, Referencia = referencia, Banco = banco, FechaCheque = fechaCheque };

        private async Task<string> MensajeAlInsertar(PagoRecibido pago, PagoRecibidoFactura[] facturas, PagoRecibidoMedio[] medios)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(pago, facturas, medios));
            return ex.Message;
        }

        // Escenario valido minimo: factura 1 por 100 pagada en efectivo.
        private Task<int> InsertarValido(PagoRecibido? pago = null) =>
            _domain.InsertarAsync(pago ?? Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) });

        // ---------------- InsertarAsync: camino feliz ----------------

        [Fact]
        public async Task InsertarAsync_Valido_NumeraGuardaCabeceraFacturasYMedios()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var pago = Pago();

            var entry = await InsertarValido(pago);

            Assert.Equal(99, entry);
            Assert.Equal("24", pago.TipoObjeto);
            Assert.Equal(5, pago.NumDoc);
            Assert.Equal(6, serie.SigNumero);
            Assert.Equal("N", pago.NumManual);
            Assert.Equal("N", pago.Cancelado);
            Assert.Equal(100m, pago.TotalPago);
            Assert.Equal("Cliente SN1", pago.NombreSn);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable), Times.Once);
            _repoPagoFactura.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoRecibidoFactura>(l => l.Entry == 99 && l.NoLinea == 1 && l.FacturaEntry == 1 && l.MontoAplicado == 100m)), Times.Once);
            _repoMedio.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoRecibidoMedio>(m => m.Entry == 99 && m.NoLinea == 1 && m.CodFormaPago == "EFE" && m.Monto == 100m)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_VariosDocumentosYMedios_NoLineasConsecutivosYTotalEsLaSuma()
        {
            var pago = Pago();

            await _domain.InsertarAsync(pago,
                new[] { Aplicar(1, 60m), Aplicar(2, 40m) },
                new[] { Medio("EFE", 70m), Medio("CHQ", 30m, "00123", "Banco Industrial") });

            Assert.Equal(100m, pago.TotalPago);
            _repoPagoFactura.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoRecibidoFactura>(l => l.NoLinea == 1 && l.FacturaEntry == 1)), Times.Once);
            _repoPagoFactura.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoRecibidoFactura>(l => l.NoLinea == 2 && l.FacturaEntry == 2)), Times.Once);
            _repoMedio.Verify(r => r.AgregarSinGuardarAsync(It.Is<PagoRecibidoMedio>(m => m.NoLinea == 2 && m.CodFormaPago == "CHQ")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_ForzaCanceladoNoYFechaCanceladoNulo()
        {
            var pago = Pago(p => { p.Cancelado = "S"; p.FechaCancelado = DateTime.Now; p.TipoObjeto = "99"; });

            await InsertarValido(pago);

            Assert.Equal("N", pago.Cancelado);
            Assert.Null(pago.FechaCancelado);
            Assert.Equal("24", pago.TipoObjeto);
        }

        [Fact]
        public async Task InsertarAsync_SinFecha_UsaLaFechaActual()
        {
            var pago = Pago(p => p.FechaDoc = null);

            await InsertarValido(pago);

            Assert.NotNull(pago.FechaDoc);
        }

        [Fact]
        public async Task InsertarAsync_ClienteInactivo_SePermite()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Cliente SN1", TipoSn = "C", Activo = "N" });

            Assert.Equal(99, await InsertarValido());
        }

        [Fact]
        public async Task InsertarAsync_NotaDeDebito_SePuedeCobrar()
        {
            var entry = await _domain.InsertarAsync(Pago(), new[] { Aplicar(2, 50m) }, new[] { Medio("EFE", 50m) });

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_UsaElNumeroIndicadoYNoAvanzaElConsecutivo()
        {
            var serie = CrearSerie(sig: 5, manual: "S");
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var pago = Pago(p => p.NumDoc = 77);

            await InsertarValido(pago);

            Assert.Equal("S", pago.NumManual);
            Assert.Equal(77, pago.NumDoc);
            Assert.Equal(5, serie.SigNumero);
        }

        // ---------------- InsertarAsync: serie ----------------

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            Assert.Equal("La serie no existe.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieDeOtroObjeto_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(codigoObj: "6"));

            Assert.Equal("La serie no corresponde a pagos recibidos.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(bloqueado: "S"));

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar pagos recibidos.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(manual: "S"));

            Assert.Equal("El número de documento es requerido para series manuales.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieSinSiguienteNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null));

            Assert.Equal("La serie no tiene configurado el número siguiente.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: 11, fin: 10));

            Assert.Equal("Se agotó la numeración disponible en esta serie.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        // ---------------- InsertarAsync: cliente ----------------

        [Fact]
        public async Task InsertarAsync_SinCliente_Lanza()
        {
            Assert.Equal("El socio de negocio es requerido.", await MensajeAlInsertar(Pago(p => p.CodigoSn = " "), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_ClienteInexistente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN9")).ReturnsAsync((SocioNegocio?)null);

            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Pago(p => p.CodigoSn = "SN9"), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_SocioQueNoEsCliente_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", Nombre = "Prov", TipoSn = "P", Activo = "S" });

            Assert.Equal("El socio de negocio debe ser un cliente.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        // ---------------- InsertarAsync: documentos ----------------

        [Fact]
        public async Task InsertarAsync_SinDocumentos_Lanza()
        {
            Assert.Equal("Indique al menos un documento a pagar.", await MensajeAlInsertar(Pago(), Array.Empty<PagoRecibidoFactura>(), new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoRepetido_Lanza()
        {
            Assert.Equal("El documento 1 está repetido en el pago.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 50m), Aplicar(1, 50m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoInexistente_Lanza()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(9)).ReturnsAsync((Factura?)null);

            Assert.Equal("El documento 9 no existe.", await MensajeAlInsertar(Pago(), new[] { Aplicar(9, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoDeOtroCliente_Lanza()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(Doc(1, 10, cliente: "SN2"));

            Assert.Equal("Factura 10 pertenece a otro socio de negocio.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_DocumentoCancelado_Lanza()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(2)).ReturnsAsync(Doc(2, 20, "ND", cancelado: "S"));

            Assert.Equal("Nota de débito 20 está cancelada.", await MensajeAlInsertar(Pago(), new[] { Aplicar(2, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task InsertarAsync_MontoAplicadoNoPositivo_Lanza(int monto)
        {
            Assert.Equal("El monto a pagar de Factura 10 debe ser mayor que 0.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, monto) }, new[] { Medio("EFE", 10m) }));
        }

        [Fact]
        public async Task InsertarAsync_MontoAplicadoConMasDeDosDecimales_Lanza()
        {
            Assert.Equal("El monto a pagar de Factura 10 no puede tener más de 2 decimales.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 10.005m) }, new[] { Medio("EFE", 10.005m) }));
        }

        [Fact]
        public async Task InsertarAsync_MontoMayorQueElSaldo_LanzaDentroDeLaTransaccionSerializable()
        {
            SaldoDisponible(60m);

            var mensaje = await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) });

            Assert.Equal("El monto a pagar de Factura 10 (100.00) excede su saldo (60.00).", mensaje);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), IsolationLevel.Serializable), Times.Once);
            _repoPago.Verify(r => r.InsertarAsync(It.IsAny<PagoRecibido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_MontoIgualAlSaldo_Pasa()
        {
            SaldoDisponible(100m);

            Assert.Equal(99, await InsertarValido());
        }

        // ---------------- InsertarAsync: formas de pago ----------------

        [Fact]
        public async Task InsertarAsync_SinFormasDePago_Lanza()
        {
            Assert.Equal("Indique al menos una forma de pago.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, Array.Empty<PagoRecibidoMedio>()));
        }

        [Fact]
        public async Task InsertarAsync_FormaInexistente_Lanza()
        {
            _repoForma.Setup(r => r.ObtenerAsync("XXX")).ReturnsAsync((FormaPago?)null);

            Assert.Equal("La forma de pago XXX no existe.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("XXX", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_FormaInactiva_Lanza()
        {
            _repoForma.Setup(r => r.ObtenerAsync("EFE")).ReturnsAsync(Forma("EFE", "Efectivo", "N", activo: "N"));

            Assert.Equal("La forma de pago Efectivo está inactiva.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100m) }));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task InsertarAsync_MontoDeFormaNoPositivo_Lanza(int monto)
        {
            Assert.Equal("El monto de la forma de pago Efectivo debe ser mayor que 0.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", monto) }));
        }

        [Fact]
        public async Task InsertarAsync_MontoDeFormaConMasDeDosDecimales_Lanza()
        {
            Assert.Equal("El monto de la forma de pago Efectivo no puede tener más de 2 decimales.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 100.001m) }));
        }

        [Fact]
        public async Task InsertarAsync_ChequeSinNumero_Lanza()
        {
            Assert.Equal("La forma de pago Cheque requiere el número de cheque.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("CHQ", 100m, referencia: " ", banco: "Industrial") }));
        }

        [Fact]
        public async Task InsertarAsync_ChequeSinBanco_Lanza()
        {
            Assert.Equal("La forma de pago Cheque requiere el banco.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("CHQ", 100m, referencia: "123") }));
        }

        [Fact]
        public async Task InsertarAsync_TransferenciaSinReferencia_Lanza()
        {
            Assert.Equal("La forma de pago Transferencia requiere la referencia de la transferencia.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("TRF", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_TarjetaSinAutorizacion_Lanza()
        {
            Assert.Equal("La forma de pago Tarjeta requiere la autorización o los últimos 4 dígitos de la tarjeta.", await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("TJT", 100m) }));
        }

        [Fact]
        public async Task InsertarAsync_EfectivoDescartaReferenciaBancoYFecha()
        {
            PagoRecibidoMedio? guardado = null;
            _repoMedio.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PagoRecibidoMedio>()))
                .Callback<PagoRecibidoMedio>(m => guardado = m).Returns(Task.CompletedTask);

            await _domain.InsertarAsync(Pago(), new[] { Aplicar(1, 100m) },
                new[] { Medio("EFE", 100m, "basura", "basura", new DateTime(2026, 10, 30)) });

            Assert.Null(guardado!.Referencia);
            Assert.Null(guardado.Banco);
            Assert.Null(guardado.FechaCheque);
        }

        [Fact]
        public async Task InsertarAsync_ChequeConservaReferenciaBancoYFecha_RecortandoEspacios()
        {
            PagoRecibidoMedio? guardado = null;
            _repoMedio.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PagoRecibidoMedio>()))
                .Callback<PagoRecibidoMedio>(m => guardado = m).Returns(Task.CompletedTask);

            await _domain.InsertarAsync(Pago(), new[] { Aplicar(1, 100m) },
                new[] { Medio("CHQ", 100m, " 00123 ", " Banco Industrial ", new DateTime(2026, 10, 30)) });

            Assert.Equal("00123", guardado!.Referencia);
            Assert.Equal("Banco Industrial", guardado.Banco);
            Assert.Equal(new DateTime(2026, 10, 30), guardado.FechaCheque);
        }

        [Fact]
        public async Task InsertarAsync_TransferenciaDescartaLaFechaDeCheque()
        {
            PagoRecibidoMedio? guardado = null;
            _repoMedio.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PagoRecibidoMedio>()))
                .Callback<PagoRecibidoMedio>(m => guardado = m).Returns(Task.CompletedTask);

            await _domain.InsertarAsync(Pago(), new[] { Aplicar(1, 100m) },
                new[] { Medio("TRF", 100m, "REF-9", null, new DateTime(2026, 10, 30)) });

            Assert.Equal("REF-9", guardado!.Referencia);
            Assert.Null(guardado.FechaCheque);
        }

        // ---------------- InsertarAsync: cuadre ----------------

        [Fact]
        public async Task InsertarAsync_TotalAplicadoDistintoDeLosMedios_Lanza()
        {
            Assert.Equal("El total aplicado a los documentos (100.00) no coincide con el total de las formas de pago (90.00).",
                await MensajeAlInsertar(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 90m) }));
        }

        [Fact]
        public async Task InsertarAsync_ErrorDeValidacion_NoAvanzaElConsecutivoNiAbreTransaccion()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Pago(), new[] { Aplicar(1, 100m) }, new[] { Medio("EFE", 90m) }));

            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>(), It.IsAny<IsolationLevel>()), Times.Never);
        }

        // ---------------- CancelarAsync ----------------

        [Fact]
        public async Task CancelarAsync_MarcaCanceladoConFechaDentroDeTransaccion()
        {
            var pago = new PagoRecibido { Entry = 7, Cancelado = "N" };
            _repoPago.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(pago);

            Assert.True(await _domain.CancelarAsync(7));

            Assert.Equal("S", pago.Cancelado);
            Assert.NotNull(pago.FechaCancelado);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>()), Times.Once);
        }

        [Fact]
        public async Task CancelarAsync_Inexistente_DevuelveFalse()
        {
            _repoPago.Setup(r => r.ObtenerAsync(7)).ReturnsAsync((PagoRecibido?)null);

            Assert.False(await _domain.CancelarAsync(7));
        }

        [Fact]
        public async Task CancelarAsync_YaCancelado_Lanza()
        {
            _repoPago.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new PagoRecibido { Entry = 7, Cancelado = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.CancelarAsync(7));

            Assert.Equal("El pago ya está cancelado.", ex.Message);
        }

        // ---------------- consultas ----------------

        [Fact]
        public async Task ObtenerFacturasAsync_DevuelveLasLineasDelPagoOrdenadas()
        {
            _repoPagoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoFactura>
            {
                new() { Entry = 7, NoLinea = 2, FacturaEntry = 2, MontoAplicado = 40m },
                new() { Entry = 7, NoLinea = 1, FacturaEntry = 1, MontoAplicado = 60m },
                new() { Entry = 8, NoLinea = 1, FacturaEntry = 1, MontoAplicado = 5m }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerFacturasAsync(7)).ToList();

            Assert.Equal(new[] { 1, 2 }, lineas.Select(l => l.NoLinea).ToArray());
        }

        [Fact]
        public async Task ObtenerMediosAsync_DevuelveLosMediosDelPagoOrdenados()
        {
            _repoMedio.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PagoRecibidoMedio>
            {
                new() { Entry = 7, NoLinea = 2, CodFormaPago = "CHQ", Monto = 30m },
                new() { Entry = 7, NoLinea = 1, CodFormaPago = "EFE", Monto = 70m },
                new() { Entry = 8, NoLinea = 1, CodFormaPago = "EFE", Monto = 5m }
            }.AsAsyncQueryable());

            var medios = (await _domain.ObtenerMediosAsync(7)).ToList();

            Assert.Equal(new[] { "EFE", "CHQ" }, medios.Select(m => m.CodFormaPago).ToArray());
        }

        [Fact]
        public async Task ObtenerDocumentosPorCobrarAsync_SoloDelClienteNoCanceladosYConSaldo_OrdenadosPorFecha()
        {
            var docs = new List<Factura>
            {
                Doc(1, 10), Doc(2, 20, "ND"), Doc(3, 30, cliente: "SN2"), Doc(4, 40, cancelado: "S"), Doc(5, 50)
            };
            docs[1].FechaDoc = new DateTime(2026, 9, 1);   // la ND es la mas antigua
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(docs.AsAsyncQueryable());
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) => (IReadOnlyDictionary<int, SaldoFactura>)ids.Distinct().ToDictionary(
                    i => i,
                    i => new SaldoFactura(i, 100m, 0m, i == 5 ? 100m : 0m, i == 5 ? 0m : 100m, i == 5 ? "Pagada" : "Pendiente")));

            var abiertos = await _domain.ObtenerDocumentosPorCobrarAsync("SN1");

            Assert.Equal(new[] { 2, 1 }, abiertos.Select(a => a.Documento.Entry).ToArray());   // 3 (otro cliente), 4 (cancelado) y 5 (saldo 0) quedan fuera
            Assert.All(abiertos, a => Assert.True(a.Saldo.Saldo > 0m));
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que falla** — `… --filter "FullyQualifiedName~PagoRecibidoDomainTests"`. Esperado: error de compilación (`PagoRecibidoDomain`/`IPagoRecibidoDomain` no existen).

- [ ] **Step 3: Interfaz** — `API.Domain.Interface/IPagoRecibidoDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Factura o nota de débito de un cliente con saldo por cobrar.</summary>
    public sealed record DocumentoPorCobrar(Factura Documento, SaldoFactura Saldo);

    public interface IPagoRecibidoDomain
    {
        #region async methods
        /// <summary>Registra un pago aplicado a documentos del cliente; devuelve el Entry. Lanza Exception con mensaje de usuario.</summary>
        Task<int> InsertarAsync(PagoRecibido obj, IEnumerable<PagoRecibidoFactura> facturas, IEnumerable<PagoRecibidoMedio> medios);
        /// <summary>Cancela el pago (el saldo de los documentos se recupera solo). false si no existe.</summary>
        Task<bool> CancelarAsync(int id);
        Task<PagoRecibido?> ObtenerAsync(int id);
        Task<IQueryable<PagoRecibido>> ObtenerTodoAsync();
        Task<IEnumerable<PagoRecibidoFactura>> ObtenerFacturasAsync(int id);
        Task<IEnumerable<PagoRecibidoMedio>> ObtenerMediosAsync(int id);
        /// <summary>Facturas y notas de débito del cliente, no canceladas y con saldo mayor que 0, de la más antigua a la más reciente.</summary>
        Task<IReadOnlyList<DocumentoPorCobrar>> ObtenerDocumentosPorCobrarAsync(string codigoSn);
        #endregion
    }
}
```

- [ ] **Step 4: Dominio** — `API.Domain.Core/PagoRecibidoDomain.cs`:

```csharp
using System.Data;
using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class PagoRecibidoDomain : IPagoRecibidoDomain
    {
        // CHECK constraint de la tabla: TipoObjeto='24'. Se fuerza siempre en el servidor.
        private const string TipoObjetoPago = "24";
        private const string TipoSocioCliente = "C";
        private const string SubTipoNotaDebito = "ND";

        private readonly IRepositorioGenerico<PagoRecibido, int> _repoPago;
        private readonly IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> _repoPagoFactura;
        private readonly IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> _repoMedio;
        private readonly IRepositorioGenerico<FormaPago, string> _repoForma;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly ISaldoFacturaService _saldos;
        private readonly IEjecutorTransaccion _tx;

        public PagoRecibidoDomain(
            IRepositorioGenerico<PagoRecibido, int> repoPago,
            IRepositorioGenerico<PagoRecibidoFactura, (int Entry, int NoLinea)> repoPagoFactura,
            IRepositorioGenerico<PagoRecibidoMedio, (int Entry, int NoLinea)> repoMedio,
            IRepositorioGenerico<FormaPago, string> repoForma,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Factura, int> repoFactura,
            ISaldoFacturaService saldos,
            IEjecutorTransaccion tx)
        {
            _repoPago = repoPago;
            _repoPagoFactura = repoPagoFactura;
            _repoMedio = repoMedio;
            _repoForma = repoForma;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoFactura = repoFactura;
            _saldos = saldos;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(PagoRecibido obj, IEnumerable<PagoRecibidoFactura> facturas, IEnumerable<PagoRecibidoMedio> medios)
        {
            var facturasList = facturas?.ToList() ?? new List<PagoRecibidoFactura>();
            var mediosList = medios?.ToList() ?? new List<PagoRecibidoMedio>();

            obj.TipoObjeto = TipoObjetoPago;
            obj.Cancelado = "N";                  // un documento recién registrado nunca nace cancelado
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;

            var serie = await ValidarSerieAsync(obj);
            await ValidarClienteAsync(obj);
            var documentos = await ValidarDocumentosAsync(obj.CodigoSn!, facturasList);
            await ValidarFormasDePagoAsync(mediosList);

            var totalAplicado = Redondear(facturasList.Sum(f => f.MontoAplicado));
            var totalMedios = Redondear(mediosList.Sum(m => m.Monto));
            if (totalAplicado != totalMedios)
                throw new Exception($"El total aplicado a los documentos ({Formato(totalAplicado)}) no coincide con el total de las formas de pago ({Formato(totalMedios)}).");
            obj.TotalPago = totalAplicado;

            // La numeración automática se asigna al final (después de validar todo) para no avanzar el
            // consecutivo de una serie rastreada si el documento resulta inválido.
            AsignarNumero(obj, serie);

            // Serializable: el chequeo del saldo y el insert son una unidad; dos pagos simultáneos sobre la misma
            // factura no pueden exceder el saldo entre los dos.
            return await _tx.EjecutarAsync(async () =>
            {
                await ValidarSaldosAsync(facturasList, documentos);

                await _repoPago.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in facturasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoPagoFactura.AgregarSinGuardarAsync(linea);
                }

                noLinea = 1;
                foreach (var medio in mediosList)
                {
                    medio.Entry = obj.Entry;
                    medio.NoLinea = noLinea++;
                    await _repoMedio.AgregarSinGuardarAsync(medio);
                }

                return obj.Entry;
            }, IsolationLevel.Serializable);
            // EjecutarAsync: Save #2 (líneas) + Commit. No hay asiento de inventario ni contable.
        }

        public async Task<bool> CancelarAsync(int id)
        {
            var existente = await _repoPago.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El pago ya está cancelado.");

            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.Cancelado = "S";
                existente.FechaCancelado = DateTime.Now;
                return true;
            });
        }

        public async Task<PagoRecibido?> ObtenerAsync(int id)
        {
            return await _repoPago.ObtenerAsync(id);
        }

        public async Task<IQueryable<PagoRecibido>> ObtenerTodoAsync()
        {
            return await _repoPago.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<PagoRecibidoFactura>> ObtenerFacturasAsync(int id)
        {
            var lineas = await _repoPagoFactura.ObtenerTodoAsync();
            return await lineas.Where(l => l.Entry == id).OrderBy(l => l.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<PagoRecibidoMedio>> ObtenerMediosAsync(int id)
        {
            var medios = await _repoMedio.ObtenerTodoAsync();
            return await medios.Where(m => m.Entry == id).OrderBy(m => m.NoLinea).ToListAsync();
        }

        public async Task<IReadOnlyList<DocumentoPorCobrar>> ObtenerDocumentosPorCobrarAsync(string codigoSn)
        {
            var queryable = await _repoFactura.ObtenerTodoAsync();
            var documentos = await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S")
                .OrderBy(f => f.FechaDoc).ThenBy(f => f.Entry)
                .ToListAsync();
            if (documentos.Count == 0)
                return new List<DocumentoPorCobrar>();

            var saldos = await _saldos.ObtenerAsync(documentos.Select(d => d.Entry));
            return documentos
                .Where(d => saldos.TryGetValue(d.Entry, out var s) && s.Saldo > 0m)
                .Select(d => new DocumentoPorCobrar(d, saldos[d.Entry]))
                .ToList();
        }
        #endregion

        #region validaciones y calculos
        private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        private static string Formato(decimal valor) => valor.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Etiqueta(Factura f) => (f.DocSubType == SubTipoNotaDebito ? "Nota de débito " : "Factura ") + f.NumDoc;

        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(PagoRecibido obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoPago)
                throw new Exception("La serie no corresponde a pagos recibidos.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar pagos recibidos.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // Un cliente dado de baja con deuda debe poder pagarla: no se exige que esté activo.
        private async Task ValidarClienteAsync(PagoRecibido obj)
        {
            if (string.IsNullOrWhiteSpace(obj.CodigoSn))
                throw new Exception("El socio de negocio es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");

            if (socio.TipoSn != TipoSocioCliente)
                throw new Exception("El socio de negocio debe ser un cliente.");

            obj.NombreSn = socio.Nombre;
        }

        private async Task<Dictionary<int, Factura>> ValidarDocumentosAsync(string codigoSn, List<PagoRecibidoFactura> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("Indique al menos un documento a pagar.");

            var repetido = lineas.GroupBy(l => l.FacturaEntry).FirstOrDefault(g => g.Count() > 1);
            if (repetido != null)
                throw new Exception($"El documento {repetido.Key} está repetido en el pago.");

            var documentos = new Dictionary<int, Factura>();
            foreach (var linea in lineas)
            {
                var factura = await _repoFactura.ObtenerAsync(linea.FacturaEntry)
                    ?? throw new Exception($"El documento {linea.FacturaEntry} no existe.");
                var etiqueta = Etiqueta(factura);

                if (!string.Equals(factura.CodigoSn, codigoSn, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"{etiqueta} pertenece a otro socio de negocio.");

                if (factura.Cancelado == "S")
                    throw new Exception($"{etiqueta} está cancelada.");

                if (linea.MontoAplicado <= 0m)
                    throw new Exception($"El monto a pagar de {etiqueta} debe ser mayor que 0.");

                if (Redondear(linea.MontoAplicado) != linea.MontoAplicado)
                    throw new Exception($"El monto a pagar de {etiqueta} no puede tener más de 2 decimales.");

                documentos[linea.FacturaEntry] = factura;
            }
            return documentos;
        }

        private async Task ValidarFormasDePagoAsync(List<PagoRecibidoMedio> medios)
        {
            if (medios.Count == 0)
                throw new Exception("Indique al menos una forma de pago.");

            foreach (var medio in medios)
            {
                medio.CodFormaPago = (medio.CodFormaPago ?? string.Empty).Trim();
                var forma = await _repoForma.ObtenerAsync(medio.CodFormaPago)
                    ?? throw new Exception($"La forma de pago {medio.CodFormaPago} no existe.");

                if (forma.Activo != "S")
                    throw new Exception($"La forma de pago {forma.Nombre} está inactiva.");

                if (medio.Monto <= 0m)
                    throw new Exception($"El monto de la forma de pago {forma.Nombre} debe ser mayor que 0.");

                if (Redondear(medio.Monto) != medio.Monto)
                    throw new Exception($"El monto de la forma de pago {forma.Nombre} no puede tener más de 2 decimales.");

                medio.Referencia = Limpiar(medio.Referencia);
                medio.Banco = Limpiar(medio.Banco);

                switch (forma.TipoDato)
                {
                    case "C":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere el número de cheque.");
                        if (medio.Banco is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere el banco.");
                        break;
                    case "T":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere la referencia de la transferencia.");
                        break;
                    case "J":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere la autorización o los últimos 4 dígitos de la tarjeta.");
                        break;
                    default:        // 'N': sin dato
                        medio.Referencia = null;
                        medio.Banco = null;
                        break;
                }

                if (forma.TipoDato != "C")
                    medio.FechaCheque = null;
            }
        }

        private static string? Limpiar(string? texto)
        {
            var t = texto?.Trim();
            return string.IsNullOrEmpty(t) ? null : t;
        }

        /// <summary>Corre dentro de la transaccion Serializable del insert.</summary>
        private async Task ValidarSaldosAsync(List<PagoRecibidoFactura> lineas, Dictionary<int, Factura> documentos)
        {
            var saldos = await _saldos.ObtenerAsync(lineas.Select(l => l.FacturaEntry));
            foreach (var linea in lineas)
            {
                var disponible = saldos.TryGetValue(linea.FacturaEntry, out var s) ? s.Saldo : 0m;
                if (linea.MontoAplicado > disponible)
                    throw new Exception($"El monto a pagar de {Etiqueta(documentos[linea.FacturaEntry])} ({Formato(linea.MontoAplicado)}) excede su saldo ({Formato(disponible)}).");
            }
        }

        private static void AsignarNumero(PagoRecibido obj, NumeracionDocumentoDet serie)
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
        #endregion
    }
}
```

- [ ] **Step 5: DI** — en `Startup.cs`, junto a las líneas de pagos agregar: `services.AddTransient<IPagoRecibidoDomain, PagoRecibidoDomain>();`

- [ ] **Step 6: Correr** — `… --filter "FullyQualifiedName~PagoRecibidoDomainTests"`. Esperado: todo en verde. Si `InsertarAsync_ErrorDeValidacion_NoAvanzaElConsecutivoNiAbreTransaccion` falla porque el total de medios (90) también activa antes la validación de saldos, no: el cuadre se evalúa antes de la transacción; revisar el orden en `InsertarAsync`. Notas: (a) los textos con comillas tipográficas/acentos deben coincidir carácter por carácter; (b) `SaldoDisponible` devuelve el diccionario con `Pagado = 100 − saldo` solo para dar datos coherentes.

- [ ] **Step 7: Commit**

```bash
git add API.Domain.Interface/IPagoRecibidoDomain.cs API.Domain.Core/PagoRecibidoDomain.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/PagoRecibidoDomainTests.cs
git commit -m "feat(pagos): dominio de pagos recibidos con saldo validado en transaccion serializable

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Aplicación, DTOs, controlador, mapeo y permisos de pagos recibidos

**Precondición:** Task 4 completa; `PagoRecibidoDomainTests` en verde.

**Files:**
- Create: `API.Application.DTO/pagoRecibido/PagoRecibidoCrearDTO.cs`, `PagoRecibidoFacturaCrearDTO.cs`, `PagoRecibidoMedioCrearDTO.cs`, `PagoRecibidoDTO.cs`, `PagoRecibidoFacturaDTO.cs`, `PagoRecibidoMedioDTO.cs`, `DocumentoPorCobrarDTO.cs`
- Create: `API.Application.Interface/IPagoRecibidoApplication.cs`, `API.Application.Main/PagoRecibidoApplication.cs`
- Create: `API.Service.WebApi/Controllers/PagoRecibidoController.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`, `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Application/PagoRecibidoApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/PagoRecibidoControllerTests.cs`, `API.Service.WebApi.Tests/Controllers/PagosPermisosTests.cs`

**Interfaces:**
- Consumes (Task 4): `IPagoRecibidoDomain`, `DocumentoPorCobrar`.
- Produces: `IPagoRecibidoApplication { Task<Respuesta<int>> InsertarAsync(PagoRecibidoCrearDTO); Task<Respuesta<bool>> CancelarAsync(int id); Task<Respuesta<PagoRecibidoDTO>> ObtenerAsync(int id); Task<Respuesta<IEnumerable<PagoRecibidoDTO>>> ObtenerTodoAsync(); Task<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>> ObtenerFacturasAsync(int id); Task<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>> ObtenerMediosAsync(int id); Task<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>> ObtenerDocumentosPorCobrarAsync(string codigoSn) }`; endpoints `api/PagoRecibido…` (módulo `PagosRecibidos`).

- [ ] **Step 1: DTOs** (namespace `API.Application.DTO.pagoRecibido`):

`PagoRecibidoCrearDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    /// <summary>Alta de un pago recibido. El total lo calcula el servidor; no se acepta del cliente.</summary>
    public class PagoRecibidoCrearDTO
    {
        public int Serie { get; set; }
        /// <summary>Solo para series manuales.</summary>
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public string? CodigoSn { get; set; }
        public string? Comentario { get; set; }
        public List<PagoRecibidoFacturaCrearDTO> Facturas { get; set; } = new();
        public List<PagoRecibidoMedioCrearDTO> Medios { get; set; } = new();
    }
}
```

`PagoRecibidoFacturaCrearDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoFacturaCrearDTO
    {
        /// <summary>Entry de la factura o nota de débito de cliente.</summary>
        public int FacturaEntry { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
```

`PagoRecibidoMedioCrearDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoMedioCrearDTO
    {
        public string? CodFormaPago { get; set; }
        public decimal Monto { get; set; }
        public string? Referencia { get; set; }
        public string? Banco { get; set; }
        public DateTime? FechaCheque { get; set; }
    }
}
```

`PagoRecibidoDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public decimal TotalPago { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`PagoRecibidoFacturaDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoFacturaDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int FacturaEntry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura; "ND" = nota de débito de cliente.</summary>
        public string? DocSubType { get; set; }
        public decimal? TotalDoc { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
```

`PagoRecibidoMedioDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    public class PagoRecibidoMedioDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string CodFormaPago { get; set; } = null!;
        public string? NombreFormaPago { get; set; }
        public string? TipoDato { get; set; }
        public decimal Monto { get; set; }
        public string? Referencia { get; set; }
        public string? Banco { get; set; }
        public DateTime? FechaCheque { get; set; }
    }
}
```

`DocumentoPorCobrarDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    /// <summary>Factura o nota de débito del cliente con su saldo, para el formulario de nuevo pago.</summary>
    public class DocumentoPorCobrarDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura; "ND" = nota de débito de cliente.</summary>
        public string? DocSubType { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal Total { get; set; }
        public decimal Acreditado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Saldo { get; set; }
        public string Estado { get; set; } = null!;
    }
}
```

- [ ] **Step 2: Mapeo** — en `PerfilMapeo.cs` agregar `using API.Application.DTO.pagoRecibido;` y, tras el bloque `// FormaPago` de la Task 2:

```csharp
            // PagoRecibido
            CreateMap<PagoRecibido, PagoRecibidoDTO>();
            CreateMap<PagoRecibidoCrearDTO, PagoRecibido>();
            CreateMap<PagoRecibidoFacturaCrearDTO, PagoRecibidoFactura>();
            CreateMap<PagoRecibidoMedioCrearDTO, PagoRecibidoMedio>();
            CreateMap<PagoRecibidoFactura, PagoRecibidoFacturaDTO>()
                .ForMember(d => d.NumDoc, o => o.MapFrom(s => s.FacturaNavigation != null ? s.FacturaNavigation.NumDoc : 0))
                .ForMember(d => d.DocSubType, o => o.MapFrom(s => s.FacturaNavigation != null ? s.FacturaNavigation.DocSubType : null))
                .ForMember(d => d.TotalDoc, o => o.MapFrom(s => s.FacturaNavigation != null ? s.FacturaNavigation.TotalDoc : null));
            CreateMap<PagoRecibidoMedio, PagoRecibidoMedioDTO>()
                .ForMember(d => d.NombreFormaPago, o => o.MapFrom(s => s.CodFormaPagoNavigation != null ? s.CodFormaPagoNavigation.Nombre : null))
                .ForMember(d => d.TipoDato, o => o.MapFrom(s => s.CodFormaPagoNavigation != null ? s.CodFormaPagoNavigation.TipoDato : null));
```

- [ ] **Step 3: Aplicación** — `API.Application.Interface/IPagoRecibidoApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;

namespace API.Application.Interface
{
    public interface IPagoRecibidoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PagoRecibidoCrearDTO obj);
        Task<Respuesta<bool>> CancelarAsync(int id);
        Task<Respuesta<PagoRecibidoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PagoRecibidoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>> ObtenerFacturasAsync(int id);
        Task<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>> ObtenerMediosAsync(int id);
        Task<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>> ObtenerDocumentosPorCobrarAsync(string codigoSn);
        #endregion
    }
}
```

`API.Application.Main/PagoRecibidoApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class PagoRecibidoApplication : IPagoRecibidoApplication
    {
        private readonly IPagoRecibidoDomain _domain;
        private readonly IMapper _mapper;

        public PagoRecibidoApplication(IPagoRecibidoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(PagoRecibidoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var pago = _mapper.Map<PagoRecibido>(obj);
                var facturas = _mapper.Map<IEnumerable<PagoRecibidoFactura>>(obj.Facturas ?? new());
                var medios = _mapper.Map<IEnumerable<PagoRecibidoMedio>>(obj.Medios ?? new());
                respuesta.Dato = await _domain.InsertarAsync(pago, facturas, medios);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Pago registrado correctamente.";
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
                respuesta.Mensaje = respuesta.Dato ? "Pago cancelado correctamente." : "El pago recibido no se encontró.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<PagoRecibidoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<PagoRecibidoDTO>();
            try
            {
                respuesta.Dato = _mapper.Map<PagoRecibidoDTO>(await _domain.ObtenerAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoRecibidoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<PagoRecibidoDTO>>();
            try
            {
                var pagos = await (await _domain.ObtenerTodoAsync()).OrderByDescending(p => p.Entry).ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<PagoRecibidoDTO>>(pagos);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>> ObtenerFacturasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<PagoRecibidoFacturaDTO>>(await _domain.ObtenerFacturasAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>> ObtenerMediosAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<PagoRecibidoMedioDTO>>();
            try
            {
                respuesta.Dato = _mapper.Map<IEnumerable<PagoRecibidoMedioDTO>>(await _domain.ObtenerMediosAsync(id));
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>> ObtenerDocumentosPorCobrarAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<DocumentoPorCobrarDTO>>();
            try
            {
                var documentos = await _domain.ObtenerDocumentosPorCobrarAsync(codigoSn);
                respuesta.Dato = documentos.Select(d => new DocumentoPorCobrarDTO
                {
                    Entry = d.Documento.Entry,
                    NumDoc = d.Documento.NumDoc,
                    DocSubType = d.Documento.DocSubType,
                    FechaDoc = d.Documento.FechaDoc,
                    Total = d.Saldo.Total,
                    Acreditado = d.Saldo.Acreditado,
                    Pagado = d.Saldo.Pagado,
                    Saldo = d.Saldo.Saldo,
                    Estado = d.Saldo.Estado
                }).ToList();
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

- [ ] **Step 4: Controlador** — `API.Service.WebApi/Controllers/PagoRecibidoController.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/PagoRecibido")]
    public class PagoRecibidoController : ControllerBase
    {
        private const string NoEncontrado = "El pago recibido no se encontró.";

        private readonly IPagoRecibidoApplication _application;

        public PagoRecibidoController(IPagoRecibidoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoRecibidoDTO>>>> ObtenerTodo()
        {
            var pagos = await _application.ObtenerTodoAsync();

            if (!pagos.Resultado)
                return BadRequest(pagos);

            return Ok(pagos);
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<PagoRecibidoDTO>>> Obtener([FromRoute] int id)
        {
            var pago = await _application.ObtenerAsync(id);

            if (!pago.Resultado)
                return BadRequest(pago);

            if (pago.Dato == null)
            {
                pago.Resultado = false;
                pago.Mensaje = NoEncontrado;
                return NotFound(pago);
            }

            return Ok(pago);
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet("{id:int}/Facturas")]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>>> ObtenerFacturas([FromRoute] int id)
        {
            var facturas = await _application.ObtenerFacturasAsync(id);

            if (!facturas.Resultado)
                return BadRequest(facturas);

            return Ok(facturas);
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        [HttpGet("{id:int}/Medios")]
        public async Task<ActionResult<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>>> ObtenerMedios([FromRoute] int id)
        {
            var medios = await _application.ObtenerMediosAsync(id);

            if (!medios.Resultado)
                return BadRequest(medios);

            return Ok(medios);
        }

        // Solo lo usa el formulario de nuevo pago: exige Agregar.
        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        [HttpGet("FacturasAbiertas/{codigoSn}")]
        public async Task<ActionResult<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>>> ObtenerDocumentosPorCobrar([FromRoute] string codigoSn)
        {
            var documentos = await _application.ObtenerDocumentosPorCobrarAsync(codigoSn);

            if (!documentos.Resultado)
                return BadRequest(documentos);

            return Ok(documentos);
        }

        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> Crear([FromBody] PagoRecibidoCrearDTO obj)
        {
            var insertar = await _application.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }

        // Cancelar un pago equivale a anularlo: exige Accion.Eliminar (convención: 'cancelar documento -> Eliminar').
        [RequierePermiso("PagosRecibidos", Accion.Eliminar)]
        [HttpPost("{id:int}/Cancelar")]
        public async Task<ActionResult<Respuesta<bool>>> Cancelar([FromRoute] int id)
        {
            var pago = await _application.ObtenerAsync(id);

            if (pago.Dato == null)
            {
                pago.Resultado = false;
                pago.Mensaje = NoEncontrado;
                return NotFound(pago);
            }

            var cancelar = await _application.CancelarAsync(id);

            if (!cancelar.Resultado)
                return BadRequest(cancelar);

            return Ok(cancelar);
        }
    }
}
```

- [ ] **Step 5: DI** — en `Startup.cs`, junto a las líneas de pagos agregar: `services.AddTransient<IPagoRecibidoApplication, PagoRecibidoApplication>();`

- [ ] **Step 6: Pruebas de aplicación** — crear `API.Service.WebApi.Tests/Application/PagoRecibidoApplicationTests.cs`:

```csharp
using API.Application.DTO.pagoRecibido;
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
    public class PagoRecibidoApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IPagoRecibidoDomain> _domain = new();
        private readonly PagoRecibidoApplication _app;

        public PagoRecibidoApplicationTests()
        {
            _app = new PagoRecibidoApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaCabeceraFacturasYMedios_YDevuelveElEntry()
        {
            PagoRecibido? recibido = null;
            List<PagoRecibidoFactura>? facturasRecibidas = null;
            List<PagoRecibidoMedio>? mediosRecibidos = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<PagoRecibido>(), It.IsAny<IEnumerable<PagoRecibidoFactura>>(), It.IsAny<IEnumerable<PagoRecibidoMedio>>()))
                .Callback<PagoRecibido, IEnumerable<PagoRecibidoFactura>, IEnumerable<PagoRecibidoMedio>>((p, f, m) => { recibido = p; facturasRecibidas = f.ToList(); mediosRecibidos = m.ToList(); })
                .ReturnsAsync(42);
            var dto = new PagoRecibidoCrearDTO
            {
                Serie = 40, CodigoSn = "SN1", Comentario = "abono",
                Facturas = { new PagoRecibidoFacturaCrearDTO { FacturaEntry = 7, MontoAplicado = 60m } },
                Medios = { new PagoRecibidoMedioCrearDTO { CodFormaPago = "CHQ", Monto = 60m, Referencia = "123", Banco = "Industrial" } }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal(42, respuesta.Dato);
            Assert.Equal("Pago registrado correctamente.", respuesta.Mensaje);
            Assert.Equal("SN1", recibido!.CodigoSn);
            Assert.Equal(40, recibido.Serie);
            var factura = Assert.Single(facturasRecibidas!);
            Assert.Equal(7, factura.FacturaEntry);
            Assert.Equal(60m, factura.MontoAplicado);
            var medio = Assert.Single(mediosRecibidos!);
            Assert.Equal("CHQ", medio.CodFormaPago);
            Assert.Equal("Industrial", medio.Banco);
        }

        [Fact]
        public async Task Insertar_ErrorDeDominio_DevuelveElMensajeSinLanzar()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<PagoRecibido>(), It.IsAny<IEnumerable<PagoRecibidoFactura>>(), It.IsAny<IEnumerable<PagoRecibidoMedio>>()))
                .ThrowsAsync(new Exception("Indique al menos un documento a pagar."));

            var respuesta = await _app.InsertarAsync(new PagoRecibidoCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("Indique al menos un documento a pagar.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Cancelar_Exito_YNoEncontrado()
        {
            _domain.SetupSequence(d => d.CancelarAsync(7)).ReturnsAsync(true).ReturnsAsync(false);

            var ok = await _app.CancelarAsync(7);
            var noExiste = await _app.CancelarAsync(7);

            Assert.True(ok.Resultado);
            Assert.Equal("Pago cancelado correctamente.", ok.Mensaje);
            Assert.False(noExiste.Resultado);
            Assert.Equal("El pago recibido no se encontró.", noExiste.Mensaje);
        }

        [Fact]
        public async Task Cancelar_YaCancelado_DevuelveElMensajeDelDominio()
        {
            _domain.Setup(d => d.CancelarAsync(7)).ThrowsAsync(new Exception("El pago ya está cancelado."));

            var respuesta = await _app.CancelarAsync(7);

            Assert.False(respuesta.Resultado);
            Assert.Equal("El pago ya está cancelado.", respuesta.Mensaje);
        }

        [Fact]
        public async Task ObtenerFacturas_MapeaNumeroTipoYTotalDelDocumento()
        {
            _domain.Setup(d => d.ObtenerFacturasAsync(7)).ReturnsAsync(new List<PagoRecibidoFactura>
            {
                new() { Entry = 7, NoLinea = 1, FacturaEntry = 3, MontoAplicado = 60m,
                        FacturaNavigation = new Factura { Entry = 3, NumDoc = 30, DocSubType = "ND", TotalDoc = 150m } }
            });

            var respuesta = await _app.ObtenerFacturasAsync(7);

            var fila = Assert.Single(respuesta.Dato);
            Assert.Equal(30, fila.NumDoc);
            Assert.Equal("ND", fila.DocSubType);
            Assert.Equal(150m, fila.TotalDoc);
            Assert.Equal(60m, fila.MontoAplicado);
        }

        [Fact]
        public async Task ObtenerMedios_MapeaNombreYTipoDeLaFormaDePago()
        {
            _domain.Setup(d => d.ObtenerMediosAsync(7)).ReturnsAsync(new List<PagoRecibidoMedio>
            {
                new() { Entry = 7, NoLinea = 1, CodFormaPago = "CHQ", Monto = 60m, Referencia = "123", Banco = "Industrial",
                        CodFormaPagoNavigation = new FormaPago { Codigo = "CHQ", Nombre = "Cheque", TipoDato = "C" } }
            });

            var respuesta = await _app.ObtenerMediosAsync(7);

            var fila = Assert.Single(respuesta.Dato);
            Assert.Equal("Cheque", fila.NombreFormaPago);
            Assert.Equal("C", fila.TipoDato);
            Assert.Equal("123", fila.Referencia);
        }

        [Fact]
        public async Task ObtenerDocumentosPorCobrar_MapeaDocumentoYSaldo()
        {
            var factura = new Factura { Entry = 3, NumDoc = 30, DocSubType = "--", FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 100m };
            _domain.Setup(d => d.ObtenerDocumentosPorCobrarAsync("SN1")).ReturnsAsync(new List<DocumentoPorCobrar>
            {
                new(factura, new SaldoFactura(3, 100m, 10m, 20m, 70m, "Parcial"))
            });

            var respuesta = await _app.ObtenerDocumentosPorCobrarAsync("SN1");

            var fila = Assert.Single(respuesta.Dato);
            Assert.Equal(3, fila.Entry);
            Assert.Equal(30, fila.NumDoc);
            Assert.Equal(100m, fila.Total);
            Assert.Equal(10m, fila.Acreditado);
            Assert.Equal(20m, fila.Pagado);
            Assert.Equal(70m, fila.Saldo);
            Assert.Equal("Parcial", fila.Estado);
        }
    }
}
```

- [ ] **Step 7: Pruebas de controlador y de permisos** — crear `API.Service.WebApi.Tests/Controllers/PagoRecibidoControllerTests.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class PagoRecibidoControllerTests
    {
        private readonly Mock<IPagoRecibidoApplication> _app = new();

        private PagoRecibidoController Crear() => new(_app.Object);

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<PagoRecibidoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<PagoRecibidoDTO>> { Resultado = true, Dato = new[] { new PagoRecibidoDTO() } };
            _app.SetupSequence(a => a.ObtenerTodoAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodo()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodo()).Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Obtener(9);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            Assert.Equal("El pago recibido no se encontró.", Assert.IsType<Respuesta<PagoRecibidoDTO>>(notFound.Value).Mensaje);
        }

        [Fact]
        public async Task Obtener_Ok_CuandoExiste()
        {
            var buena = new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = new PagoRecibidoDTO { Entry = 9 } };
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(buena);

            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Obtener(9)).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturasYMedios_BadRequestYOk()
        {
            var malaF = new Respuesta<IEnumerable<PagoRecibidoFacturaDTO>> { Resultado = false, Mensaje = "error" };
            var buenaF = new Respuesta<IEnumerable<PagoRecibidoFacturaDTO>> { Resultado = true, Dato = new[] { new PagoRecibidoFacturaDTO() } };
            _app.SetupSequence(a => a.ObtenerFacturasAsync(9)).ReturnsAsync(malaF).ReturnsAsync(buenaF);
            var malaM = new Respuesta<IEnumerable<PagoRecibidoMedioDTO>> { Resultado = false, Mensaje = "error" };
            var buenaM = new Respuesta<IEnumerable<PagoRecibidoMedioDTO>> { Resultado = true, Dato = new[] { new PagoRecibidoMedioDTO() } };
            _app.SetupSequence(a => a.ObtenerMediosAsync(9)).ReturnsAsync(malaM).ReturnsAsync(buenaM);

            Assert.Same(malaF, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturas(9)).Result).Value);
            Assert.Same(buenaF, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturas(9)).Result).Value);
            Assert.Same(malaM, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerMedios(9)).Result).Value);
            Assert.Same(buenaM, Assert.IsType<OkObjectResult>((await Crear().ObtenerMedios(9)).Result).Value);
        }

        [Fact]
        public async Task ObtenerDocumentosPorCobrar_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<DocumentoPorCobrarDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<DocumentoPorCobrarDTO>> { Resultado = true, Dato = new[] { new DocumentoPorCobrarDTO() } };
            _app.SetupSequence(a => a.ObtenerDocumentosPorCobrarAsync("SN1")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerDocumentosPorCobrar("SN1")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerDocumentosPorCobrar("SN1")).Result).Value);
        }

        [Fact]
        public async Task Crear_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 5 };
            var dto = new PagoRecibidoCrearDTO { Serie = 40, CodigoSn = "SN1" };
            _app.SetupSequence(a => a.InsertarAsync(dto)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Crear(dto)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Crear(dto)).Result).Value);
        }

        [Fact]
        public async Task Cancelar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Cancelar(9);

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
            _app.Verify(a => a.CancelarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Cancelar_BadRequestYOk()
        {
            _app.Setup(a => a.ObtenerAsync(9)).ReturnsAsync(new Respuesta<PagoRecibidoDTO> { Resultado = true, Dato = new PagoRecibidoDTO { Entry = 9 } });
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "El pago ya está cancelado." };
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.SetupSequence(a => a.CancelarAsync(9)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().Cancelar(9)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().Cancelar(9)).Result).Value);
        }
    }
}
```

Crear `API.Service.WebApi.Tests/Controllers/PagosPermisosTests.cs` (misma técnica por reflexión de `TallerPermisosTests`; la Task 8 le agrega el `SaldoFacturaController`):

```csharp
using System.Reflection;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión la tabla de permisos de los controllers de pagos.</summary>
    public class PagosPermisosTests
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
            yield return new object[] { typeof(FormaPagoController), "FormasPago", new Dictionary<string, Accion>
            {
                ["ObtenerTodo"] = Accion.Ver, ["ObtenerPorCodigo"] = Accion.Ver, ["Crear"] = Accion.Agregar,
                ["Actualizar"] = Accion.Actualizar, ["Eliminar"] = Accion.Eliminar
            } };
            yield return new object[] { typeof(PagoRecibidoController), "PagosRecibidos", new Dictionary<string, Accion>
            {
                ["ObtenerTodo"] = Accion.Ver, ["Obtener"] = Accion.Ver, ["ObtenerFacturas"] = Accion.Ver, ["ObtenerMedios"] = Accion.Ver,
                ["ObtenerDocumentosPorCobrar"] = Accion.Agregar, ["Crear"] = Accion.Agregar, ["Cancelar"] = Accion.Eliminar
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
        [InlineData(typeof(FormaPagoController))]
        [InlineData(typeof(PagoRecibidoController))]
        public void ElControllerExigeAutenticacion(Type controller)
        {
            Assert.NotNull(controller.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        }
    }
}
```

(Si en `TallerPermisosTests` los nombres de los campos privados de `RequierePermisoAttribute` difieren de `_codigoModulo`/`_accion`, copiar exactamente los que usa ese archivo.)

- [ ] **Step 8: Correr** — `… --filter "FullyQualifiedName~PagoRecibido|FullyQualifiedName~PagosPermisos"`. Esperado: todo en verde. Luego `dotnet build API.sln` sin errores.

- [ ] **Step 9: Commit**

```bash
git add API.Application.DTO/pagoRecibido API.Application.Interface/IPagoRecibidoApplication.cs API.Application.Main/PagoRecibidoApplication.cs API.Service.WebApi/Controllers/PagoRecibidoController.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Application/PagoRecibidoApplicationTests.cs API.Service.WebApi.Tests/Controllers/PagoRecibidoControllerTests.cs API.Service.WebApi.Tests/Controllers/PagosPermisosTests.cs
git commit -m "feat(pagos): aplicacion, DTOs, controlador y permisos de pagos recibidos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 3 — Guardas sobre documentos existentes, saldos y script completo (API)

### Task 6: Guarda de pagos al cancelar o eliminar facturas y notas de débito

**Precondición:** Task 5 completa; `git log --oneline -1` muestra su commit; `dotnet build API.sln` → 0 errores.

**Files:**
- Create: `API.Domain.Core/GuardaPagos.cs`
- Modify: `API.Domain.Core/FacturaDomain.cs`, `API.Domain.Core/NotaDebitoDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/GuardaPagosTests.cs` (nuevo), `API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs`, `API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs`

**Interfaces:**
- Consumes (Task 3): `ISaldoFacturaService.ObtenerPagosActivosAsync(int) → Task<IReadOnlyList<PagoAplicado>>`, `PagoAplicado(PagoEntry, PagoNumDoc, Monto)`.
- Produces: `static class GuardaPagos { static Task ValidarSinPagosAsync(ISaldoFacturaService saldos, int facturaEntry) }` — lanza `Exception("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.")` si hay pagos activos. `FacturaDomain` y `NotaDebitoDomain` reciben `ISaldoFacturaService saldos` como **último** parámetro del constructor.

- [ ] **Step 1: Escribir las pruebas (fallarán)**

(a) Crear `API.Service.WebApi.Tests/Domain/GuardaPagosTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class GuardaPagosTests
    {
        private readonly Mock<ISaldoFacturaService> _saldos = new();

        [Fact]
        public async Task ValidarSinPagosAsync_SinPagos_NoLanza()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado>());

            await GuardaPagos.ValidarSinPagosAsync(_saldos.Object, 7);
        }

        [Fact]
        public async Task ValidarSinPagosAsync_UnPago_LoNombra()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m) });

            var ex = await Assert.ThrowsAsync<Exception>(() => GuardaPagos.ValidarSinPagosAsync(_saldos.Object, 7));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3). Cancele primero el pago.", ex.Message);
        }

        [Fact]
        public async Task ValidarSinPagosAsync_VariosPagos_LosListaPorNumero()
        {
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(7)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m), new(2, 5, 60m) });

            var ex = await Assert.ThrowsAsync<Exception>(() => GuardaPagos.ValidarSinPagosAsync(_saldos.Object, 7));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.", ex.Message);
        }
    }
}
```

(b) `FacturaDomainTests.cs` (conservar su EOL): agregar el campo `private readonly Mock<ISaldoFacturaService> _saldos = new();` junto a los demás mocks; en el constructor cambiar la creación del dominio a

```csharp
            _domain = new FacturaDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, _documentoBase.Object, _saldos.Object);
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(It.IsAny<int>())).ReturnsAsync(new List<PagoAplicado>());
```

y agregar estas pruebas junto a las de cancelar/eliminar:

```csharp
        private void ConPagosActivos(int entry) =>
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(entry)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m), new(2, 5, 60m) });

        [Fact]
        public async Task ActualizarAsync_Cancelado_S_ConPagosActivos_LanzaYNoRevierteNiCancela()
        {
            var existente = new Factura { Entry = 7, Cancelado = "N", EstadoInv = "A" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);
            ConPagosActivos(7);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, new Factura { Cancelado = "S" }));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.", ex.Message);
            Assert.Equal("N", existente.Cancelado);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SoloComentario_ConPagosActivos_SePermite()
        {
            var existente = new Factura { Entry = 7, Cancelado = "N", EstadoInv = "A", Comentario = "viejo" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);
            ConPagosActivos(7);

            Assert.True(await _domain.ActualizarAsync(7, new Factura { Comentario = "nuevo" }));

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("N", existente.Cancelado);
        }

        [Fact]
        public async Task EliminarAsync_ConPagosActivos_LanzaYNoBorra()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, EstadoInv = "C", Cancelado = "S" });
            ConPagosActivos(7);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.", ex.Message);
            _repoHeader.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }
```

(c) `NotaDebitoDomainTests.cs`: agregar el campo `private readonly Mock<ISaldoFacturaService> _saldos = new();`; en el constructor agregar `_saldos.Object` como último argumento de `new NotaDebitoDomain(...)` (tras `_asiento.Object`) y, junto a los `Setup` por defecto, `_saldos.Setup(s => s.ObtenerPagosActivosAsync(It.IsAny<int>())).ReturnsAsync(new List<PagoAplicado>());`. Agregar (usando `PrepararNota`, `SubTipoNota` y `CodigoObj` que ya existen en el archivo):

```csharp
        private void ConPagosActivos(int entry) =>
            _saldos.Setup(s => s.ObtenerPagosActivosAsync(entry)).ReturnsAsync(new List<PagoAplicado> { new(1, 3, 40m), new(2, 5, 60m) });

        [Fact]
        public async Task ActualizarAsync_Cancelar_ConPagosActivos_LanzaYNoCancela()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "I", Cancelado = "N", EstadoInv = "A", EstadoDoc = "A" };
            PrepararNota(existente);
            ConPagosActivos(5);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, new Factura { Cancelado = "S" }));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.", ex.Message);
            Assert.Equal("N", existente.Cancelado);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SoloComentario_ConPagosActivos_SePermite()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "S", Cancelado = "N", Comentario = "viejo" };
            PrepararNota(existente);
            ConPagosActivos(5);

            Assert.True(await _domain.ActualizarAsync(5, new Factura { Comentario = "nuevo" }));

            Assert.Equal("nuevo", existente.Comentario);
        }

        [Fact]
        public async Task EliminarAsync_ConPagosActivos_LanzaYNoBorra()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, Cancelado = "S" });
            ConPagosActivos(5);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5));

            Assert.Equal("El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago.", ex.Message);
            _repoFactura.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }
```

- [ ] **Step 2: Correr y comprobar que falla** — `… --filter "FullyQualifiedName~GuardaPagosTests|FullyQualifiedName~FacturaDomainTests|FullyQualifiedName~NotaDebitoDomainTests"`. Esperado: error de compilación (`GuardaPagos` no existe; constructores).

- [ ] **Step 3: Implementar** — crear `API.Domain.Core/GuardaPagos.cs`:

```csharp
using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>
    /// Regla compartida por factura y nota de débito de cliente: un documento con pagos activos no se cancela ni
    /// se elimina; primero hay que cancelar el pago.
    /// </summary>
    public static class GuardaPagos
    {
        public static async Task ValidarSinPagosAsync(ISaldoFacturaService saldos, int facturaEntry)
        {
            var pagos = await saldos.ObtenerPagosActivosAsync(facturaEntry);
            if (pagos.Count == 0)
                return;

            var etiquetas = string.Join(", ", pagos.Select(p => $"Pago {p.PagoNumDoc}"));
            throw new Exception($"El documento tiene pagos aplicados ({etiquetas}). Cancele primero el pago.");
        }
    }
}
```

`FacturaDomain.cs`: (i) campo `private readonly ISaldoFacturaService _saldos;`; (ii) constructor con `ISaldoFacturaService saldos` como último parámetro y `_saldos = saldos;`; (iii) en `ActualizarAsync`, dentro de `if (obj.Cancelado == "S")`, **antes** de `return await _tx.EjecutarAsync(...)` agregar `await GuardaPagos.ValidarSinPagosAsync(_saldos, id);`; (iv) en `EliminarAsync`, justo después de `if (existente is null) return false;` agregar `await GuardaPagos.ValidarSinPagosAsync(_saldos, id);`.

`NotaDebitoDomain.cs`: igual — campo `_saldos`, parámetro `ISaldoFacturaService saldos` al final del constructor, `await GuardaPagos.ValidarSinPagosAsync(_saldos, id);` dentro de `if (obj.Cancelado == "S")` antes del `return await _tx.EjecutarAsync(...)`, y en `EliminarAsync` tras `if (existente is null) return false;`.

(La DI no cambia: `ISaldoFacturaService` ya está registrado y los dominios se resuelven por constructor.)

- [ ] **Step 4: Correr** — mismo filtro del Step 2 más `ModeloPagosTests`. Esperado: todo en verde (las pruebas existentes de `FacturaDomainTests`/`NotaDebitoDomainTests` siguen pasando porque el mock por defecto devuelve lista vacía). Luego `dotnet build API.sln` sin errores (ningún otro sitio del código construye estos dominios).

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/GuardaPagos.cs API.Domain.Core/FacturaDomain.cs API.Domain.Core/NotaDebitoDomain.cs API.Service.WebApi.Tests/Domain/GuardaPagosTests.cs API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs
git commit -m "feat(pagos): una factura o nota de debito con pagos activos no se cancela ni se elimina

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: El límite de la nota de crédito descuenta los pagos aplicados

**Precondición:** Task 6 completa; `git log --oneline -1` muestra su commit.

**Files:**
- Modify: `API.Domain.Interface/FacturaBaseCredito.cs`, `API.Domain.Core/NotaCreditoDomain.cs`, `API.Application.Main/NotaCreditoApplication.cs`, `API.Application.DTO/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs`, `API.Application.DTO/notaCredito/NotaCreditoFacturaBaseDTO.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs`, `API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs`

**Interfaces:**
- Consumes (Task 3): `ISaldoFacturaService.ObtenerAsync(IEnumerable<int>) → IReadOnlyDictionary<int, SaldoFactura>` (usa `SaldoFactura.Pagado`).
- Produces: `record FacturaAcreditable(Factura Documento, decimal TotalAcreditado, decimal TotalPagado = 0m)`; `record FacturaBaseCredito(Factura Documento, IReadOnlyList<FacturaDetalle> Lineas, decimal TotalAcreditado, decimal TotalPagado = 0m)`; `NotaCreditoDomain` recibe `ISaldoFacturaService saldos` como **último** parámetro del constructor; `NotaCreditoFacturaBaseResumenDTO.TotalPagado` y `NotaCreditoFacturaBaseDTO.TotalPagado` (decimal); `Disponible = max(0, total − acreditado − pagado)`.

- [ ] **Step 1: Escribir las pruebas (fallarán)**

(a) `NotaCreditoDomainTests.cs`: agregar `private readonly Mock<ISaldoFacturaService> _saldos = new();`; en el constructor pasar `_saldos.Object` como último argumento de `new NotaCreditoDomain(...)` (tras `_tx.Object`) y, junto a los `Setup` por defecto, agregar:

```csharp
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, SaldoFactura>());
```

Agregar la constante `private const string MsgExcesoPagada = "Las notas de crédito ({0}) exceden lo que queda por acreditar de la factura ({1} = total menos pagos aplicados). Cancele primero los pagos si necesita acreditarla por completo.";` junto a `MsgExceso`, y estas pruebas (usan `PrepararFactura`, `NotaConBase`, `NotasPrevias`, `Previa`, `Linea()` = 112, que ya existen):

```csharp
        private static string ExcesoPagada(string acreditado, string limite) =>
            string.Format(CultureInfo.InvariantCulture, MsgExcesoPagada, acreditado, limite);

        private void ConPagado(decimal pagado) =>
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync((IEnumerable<int> ids) => (IReadOnlyDictionary<int, SaldoFactura>)ids.Distinct()
                    .ToDictionary(i => i, i => new SaldoFactura(i, 200m, 0m, pagado, 0m, "Parcial")));

        [Fact]
        public async Task InsertarAsync_FacturaConPagos_LaNotaNoPuedeExcederTotalMenosPagado()
        {
            PrepararFactura(total: 200m);
            ConPagado(100m);                                   // limite = 200 - 100 = 100; la nota es 112

            Assert.Equal(ExcesoPagada("112.00", "100.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaCredito>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_FacturaConPagos_NotaJustoEnElLimite_EsValida()
        {
            PrepararFactura(total: 212m);
            ConPagado(100m);                                   // limite = 112 = la nota

            Assert.Equal(99, await _domain.InsertarAsync(NotaConBase(), new[] { Linea() }));
        }

        [Fact]
        public async Task InsertarAsync_FacturaConPagosYNotasPrevias_SumanContraElLimite()
        {
            PrepararFactura(total: 300m);
            ConPagado(100m);                                   // limite = 200
            NotasPrevias(Previa(12, 90m));                     // 90 + 112 = 202 > 200

            Assert.Equal(ExcesoPagada("202.00", "200.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SinPagos_ConservaElMensajeDeExcesoOriginal()
        {
            PrepararFactura(total: 100m);

            Assert.Equal(Exceso("112.00", "100.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task ObtenerFacturasBaseAsync_IncluyeLoPagadoDeCadaFactura()
        {
            var facturas = new List<Factura>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 112m },
                new() { Entry = 4, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 200m }
            };
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            NotasPrevias(Previa(1, 30m));
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, SaldoFactura> { [1] = new SaldoFactura(1, 112m, 30m, 20m, 62m, "Parcial") });

            var resultado = (await _domain.ObtenerFacturasBaseAsync("SN1")).ToList();

            Assert.Equal(new[] { 4, 1 }, resultado.Select(f => f.Documento.Entry));
            Assert.Equal(0m, resultado[0].TotalPagado);
            Assert.Equal(20m, resultado[1].TotalPagado);
            Assert.Equal(30m, resultado[1].TotalAcreditado);
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_IncluyeLoPagado()
        {
            PrepararFactura(entry: 12, total: 112m);
            _repoFacturaDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle>().AsAsyncQueryable());
            NotasPrevias();
            _saldos.Setup(s => s.ObtenerAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, SaldoFactura> { [12] = new SaldoFactura(12, 112m, 0m, 40m, 72m, "Parcial") });

            var resultado = await _domain.ObtenerFacturaBaseAsync(12);

            Assert.Equal(40m, resultado.TotalPagado);
        }
```

(b) `NotaCreditoApplicationTests.cs`: agregar

```csharp
        [Fact]
        public async Task ObtenerFacturasBase_ElDisponibleDescuentaLoPagado()
        {
            _domain.Setup(d => d.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(new List<FacturaAcreditable>
            {
                new(new Factura { Entry = 12, NumDoc = 100, TotalDoc = 112m, MonedaDoc = "QTZ", CodigoSn = "SN1" }, 30m, 20m)
            });

            var fila = Assert.Single((await _app.ObtenerFacturasBaseAsync("SN1")).Dato);

            Assert.Equal(30m, fila.TotalAcreditado);
            Assert.Equal(20m, fila.TotalPagado);
            Assert.Equal(62m, fila.Disponible);
        }

        [Fact]
        public async Task ObtenerFacturaBase_ElDisponibleDescuentaLoPagado()
        {
            var factura = new Factura { Entry = 12, NumDoc = 100, CodigoSn = "SN1", MonedaDoc = "QTZ", TotalDoc = 112m };
            _domain.Setup(d => d.ObtenerFacturaBaseAsync(12)).ReturnsAsync(new FacturaBaseCredito(factura, new List<FacturaDetalle>(), 12m, 40m));

            var dato = (await _app.ObtenerFacturaBaseAsync(12)).Dato;

            Assert.Equal(40m, dato.TotalPagado);
            Assert.Equal(60m, dato.Disponible);
        }
```

- [ ] **Step 2: Correr y comprobar que falla** — `… --filter "FullyQualifiedName~NotaCreditoDomainTests|FullyQualifiedName~NotaCreditoApplicationTests"`. Esperado: error de compilación (constructor, `TotalPagado`).

- [ ] **Step 3: Implementar**

`API.Domain.Interface/FacturaBaseCredito.cs`: cambiar las dos primeras declaraciones a

```csharp
    /// <summary>Factura de venta candidata a base de una nota de credito de cliente, con lo ya acreditado y lo ya pagado.</summary>
    public record FacturaAcreditable(Factura Documento, decimal TotalAcreditado, decimal TotalPagado = 0m);

    /// <summary>Factura de venta con sus lineas, lo ya acreditado y lo ya pagado: base de una nota de credito de cliente.</summary>
    public record FacturaBaseCredito(Factura Documento, IReadOnlyList<FacturaDetalle> Lineas, decimal TotalAcreditado, decimal TotalPagado = 0m);
```

(las dos de compra no cambian).

`API.Domain.Core/NotaCreditoDomain.cs`:
1. Agregar `private const string MsgExcesoPagada = "Las notas de crédito ({0}) exceden lo que queda por acreditar de la factura ({1} = total menos pagos aplicados). Cancele primero los pagos si necesita acreditarla por completo.";` junto a `MsgExceso`.
2. Campo `private readonly ISaldoFacturaService _saldos;` y parámetro `ISaldoFacturaService saldos` **al final** del constructor (`_saldos = saldos;`).
3. En `ObtenerFacturasBaseAsync`, después de calcular `acreditadoPorFactura` y antes del `return`, y cambiando el `return`:

```csharp
            var saldos = await _saldos.ObtenerAsync(entries);

            return facturas
                .Select(f => new FacturaAcreditable(
                    f,
                    acreditadoPorFactura.TryGetValue(f.Entry, out var a) ? a : 0m,
                    saldos.TryGetValue(f.Entry, out var s) ? s.Pagado : 0m))
                .ToList();
```

4. En `ObtenerFacturaBaseAsync`, reemplazar las dos últimas líneas por:

```csharp
            var acreditado = await ObtenerAcreditadoAsync(entry);
            var pagado = await ObtenerPagadoAsync(entry);
            return new FacturaBaseCredito(factura, lineas, acreditado, pagado);
```

5. Agregar el método auxiliar (junto a `ObtenerAcreditadoAsync`):

```csharp
        private async Task<decimal> ObtenerPagadoAsync(int facturaEntry)
        {
            var saldos = await _saldos.ObtenerAsync(new[] { facturaEntry });
            return saldos.TryGetValue(facturaEntry, out var s) ? s.Pagado : 0m;
        }
```

6. Reemplazar `ValidarLimiteAcreditableAsync` por:

```csharp
        /// <summary>Corre dentro de la transaccion Serializable del insert.</summary>
        private async Task ValidarLimiteAcreditableAsync(Factura factura, NotaCredito obj)
        {
            var acreditado = await ObtenerAcreditadoAsync(factura.Entry);
            var totalConEstaNota = Redondear(acreditado + (obj.TotalDoc ?? 0m));
            var totalFactura = Redondear(factura.TotalDoc ?? 0m);

            var pagado = await ObtenerPagadoAsync(factura.Entry);
            if (pagado > 0m)
            {
                // Con pagos aplicados solo se puede acreditar lo que todavia no se cobro: total - pagado.
                var limite = Redondear(totalFactura - pagado);
                if (totalConEstaNota > limite)
                    throw new Exception(string.Format(CultureInfo.InvariantCulture, MsgExcesoPagada,
                        totalConEstaNota.ToString("0.00", CultureInfo.InvariantCulture),
                        limite.ToString("0.00", CultureInfo.InvariantCulture)));
                return;
            }

            if (totalConEstaNota > totalFactura)
                throw new Exception(string.Format(CultureInfo.InvariantCulture, MsgExceso,
                    totalConEstaNota.ToString("0.00", CultureInfo.InvariantCulture),
                    totalFactura.ToString("0.00", CultureInfo.InvariantCulture)));
        }
```

`API.Application.DTO/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs` y `NotaCreditoFacturaBaseDTO.cs`: agregar `/// <summary>Suma de los pagos activos aplicados a esta factura.</summary>` + `public decimal TotalPagado { get; set; }` justo después de `TotalAcreditado`, y en el resumen cambiar el comentario de `Disponible` a `max(0, TotalDoc - TotalAcreditado - TotalPagado): lo que todavia se puede acreditar.`

`API.Application.Main/NotaCreditoApplication.cs`: en `ObtenerFacturasBaseAsync` agregar `TotalPagado = f.TotalPagado,` tras `TotalAcreditado = f.TotalAcreditado,` y cambiar `Disponible = Disponible(f.Documento.TotalDoc, f.TotalAcreditado)` por `Disponible = Disponible(f.Documento.TotalDoc, f.TotalAcreditado + f.TotalPagado)`; en `ObtenerFacturaBaseAsync` agregar `TotalPagado = baseDoc.TotalPagado,` tras `TotalAcreditado = baseDoc.TotalAcreditado,` y cambiar el `Disponible` a `Disponible(baseDoc.Documento.TotalDoc, baseDoc.TotalAcreditado + baseDoc.TotalPagado)`. Actualizar el comentario del método auxiliar `Disponible` a `max(0, total - acreditado - pagado)` (el parámetro `acreditado` pasa a significar "ya descontado").

- [ ] **Step 4: Correr** — filtro del Step 2. Esperado: todo en verde, incluidas las pruebas anteriores de `Exceso(...)` (mensaje original sin pagos). Luego `dotnet build API.sln` sin errores.

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Interface/FacturaBaseCredito.cs API.Domain.Core/NotaCreditoDomain.cs API.Application.Main/NotaCreditoApplication.cs API.Application.DTO/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs API.Application.DTO/notaCredito/NotaCreditoFacturaBaseDTO.cs API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs
git commit -m "feat(pagos): el limite acreditable de una nota de credito descuenta los pagos aplicados

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Endpoints de saldos de facturas y notas de débito

**Precondición:** Task 7 completa; `git log --oneline -1` muestra su commit.

**Files:**
- Create: `API.Application.DTO/pagoRecibido/SaldoFacturaDTO.cs`, `API.Application.Interface/ISaldoFacturaApplication.cs`, `API.Application.Main/SaldoFacturaApplication.cs`, `API.Service.WebApi/Controllers/SaldoFacturaController.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Controllers/SaldoFacturaControllerTests.cs`

**Interfaces:**
- Consumes (Task 3): `ISaldoFacturaService`, `SaldoFactura`.
- Produces: `ISaldoFacturaApplication { Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerAsync(IEnumerable<int> entries) }`; `SaldoFacturaDTO { int FacturaEntry; decimal Total; decimal Acreditado; decimal Pagado; decimal Saldo; string Estado }`; endpoints `POST api/Factura/Saldos` (Ver `Facturas`) y `POST api/NotaDebito/Saldos` (Ver `NotasDebito`) con body JSON `[1,2,3]` (POST y no GET: un listado grande desbordaría la longitud de la URL).

- [ ] **Step 1: Escribir las pruebas (fallarán)** — crear `API.Service.WebApi.Tests/Controllers/SaldoFacturaControllerTests.cs`:

```csharp
using System.Reflection;
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class SaldoFacturaControllerTests
    {
        private readonly Mock<ISaldoFacturaApplication> _app = new();

        private SaldoFacturaController Crear() => new(_app.Object);

        [Fact]
        public async Task SaldosFacturas_BadRequestYOk()
        {
            var ids = new List<int> { 1, 2 };
            var mala = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = true, Dato = new[] { new SaldoFacturaDTO { FacturaEntry = 1 } } };
            _app.SetupSequence(a => a.ObtenerAsync(ids)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().SaldosFacturas(ids)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().SaldosFacturas(ids)).Result).Value);
        }

        [Fact]
        public async Task SaldosNotasDebito_BadRequestYOk()
        {
            var ids = new List<int> { 5 };
            var mala = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = true, Dato = new[] { new SaldoFacturaDTO { FacturaEntry = 5 } } };
            _app.SetupSequence(a => a.ObtenerAsync(ids)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().SaldosNotasDebito(ids)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().SaldosNotasDebito(ids)).Result).Value);
        }

        [Fact]
        public async Task SaldosFacturas_CuerpoNulo_SeTrataComoListaVacia()
        {
            var vacia = new Respuesta<IEnumerable<SaldoFacturaDTO>> { Resultado = true, Dato = Array.Empty<SaldoFacturaDTO>() };
            _app.Setup(a => a.ObtenerAsync(It.Is<IEnumerable<int>>(e => !e.Any()))).ReturnsAsync(vacia);

            Assert.Same(vacia, Assert.IsType<OkObjectResult>((await Crear().SaldosFacturas(null)).Result).Value);
        }

        [Theory]
        [InlineData("SaldosFacturas", "Facturas", "api/Factura/Saldos")]
        [InlineData("SaldosNotasDebito", "NotasDebito", "api/NotaDebito/Saldos")]
        public void CadaAccion_ExigeVerEnSuModuloYSeExponeComoPost(string accion, string modulo, string ruta)
        {
            var campoModulo = typeof(RequierePermisoAttribute).GetField("_codigoModulo", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var campoAccion = typeof(RequierePermisoAttribute).GetField("_accion", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var metodo = typeof(SaldoFacturaController).GetMethod(accion)!;

            var permiso = metodo.GetCustomAttribute<RequierePermisoAttribute>()!;
            Assert.Equal(modulo, (string)campoModulo.GetValue(permiso)!);
            Assert.Equal(Accion.Ver, (Accion)campoAccion.GetValue(permiso)!);
            var http = Assert.IsAssignableFrom<HttpMethodAttribute>(metodo.GetCustomAttributes(typeof(HttpMethodAttribute), true).Single());
            Assert.Equal(new[] { "POST" }, http.HttpMethods.ToArray());
            Assert.Equal(ruta, http.Template);
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que falla** — `… --filter "FullyQualifiedName~SaldoFacturaControllerTests"`. Esperado: error de compilación.

- [ ] **Step 3: Implementar**

`API.Application.DTO/pagoRecibido/SaldoFacturaDTO.cs`:

```csharp
namespace API.Application.DTO.pagoRecibido
{
    /// <summary>Saldo y estado de pago de una factura o nota de débito de cliente.</summary>
    public class SaldoFacturaDTO
    {
        public int FacturaEntry { get; set; }
        public decimal Total { get; set; }
        public decimal Acreditado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Saldo { get; set; }
        /// <summary>Pendiente, Parcial o Pagada.</summary>
        public string Estado { get; set; } = null!;
    }
}
```

`API.Application.Interface/ISaldoFacturaApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;

namespace API.Application.Interface
{
    public interface ISaldoFacturaApplication
    {
        #region async methods
        Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerAsync(IEnumerable<int> entries);
        #endregion
    }
}
```

`API.Application.Main/SaldoFacturaApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class SaldoFacturaApplication : ISaldoFacturaApplication
    {
        private readonly ISaldoFacturaService _saldos;

        public SaldoFacturaApplication(ISaldoFacturaService saldos)
        {
            _saldos = saldos;
        }

        #region async methods
        public async Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerAsync(IEnumerable<int> entries)
        {
            var respuesta = new Respuesta<IEnumerable<SaldoFacturaDTO>>();
            try
            {
                var saldos = await _saldos.ObtenerAsync(entries ?? Enumerable.Empty<int>());
                respuesta.Dato = saldos.Values.Select(s => new SaldoFacturaDTO
                {
                    FacturaEntry = s.FacturaEntry,
                    Total = s.Total,
                    Acreditado = s.Acreditado,
                    Pagado = s.Pagado,
                    Saldo = s.Saldo,
                    Estado = s.Estado
                }).ToList();
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

`API.Service.WebApi/Controllers/SaldoFacturaController.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.pagoRecibido;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Saldo y estado de pago de facturas y notas de débito de cliente, para los listados. Es POST porque un listado
    /// grande no cabe en la URL. Las rutas son absolutas por acción para no tocar FacturaController/NotaDebitoController.
    /// </summary>
    [Authorize]
    [ApiController]
    public class SaldoFacturaController : ControllerBase
    {
        private readonly ISaldoFacturaApplication _application;

        public SaldoFacturaController(ISaldoFacturaApplication application)
        {
            _application = application;
        }

        [RequierePermiso("Facturas", Accion.Ver)]
        [HttpPost("api/Factura/Saldos")]
        public Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaDTO>>>> SaldosFacturas([FromBody] List<int>? entries) => Saldos(entries);

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpPost("api/NotaDebito/Saldos")]
        public Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaDTO>>>> SaldosNotasDebito([FromBody] List<int>? entries) => Saldos(entries);

        private async Task<ActionResult<Respuesta<IEnumerable<SaldoFacturaDTO>>>> Saldos(List<int>? entries)
        {
            var saldos = await _application.ObtenerAsync(entries ?? new List<int>());

            if (!saldos.Resultado)
                return BadRequest(saldos);

            return Ok(saldos);
        }
    }
}
```

`Startup.cs`: junto a las líneas de pagos agregar `services.AddTransient<ISaldoFacturaApplication, SaldoFacturaApplication>();`.

- [ ] **Step 4: Correr** — `… --filter "FullyQualifiedName~SaldoFacturaControllerTests"`. Esperado: 5 pruebas (2 + 1 + 2 de la Theory) en verde. Comprobar además que no haya ambigüedad de rutas con `FacturaController`/`NotaDebitoController` (`dotnet build` y, si es posible, arrancar la API contra una base temporal en la Task 9 y llamar `POST api/Factura/Saldos`).

- [ ] **Step 5: Commit**

```bash
git add API.Application.DTO/pagoRecibido/SaldoFacturaDTO.cs API.Application.Interface/ISaldoFacturaApplication.cs API.Application.Main/SaldoFacturaApplication.cs API.Service.WebApi/Controllers/SaldoFacturaController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Controllers/SaldoFacturaControllerTests.cs
git commit -m "feat(pagos): endpoints de saldo y estado de pago para facturas y notas de debito

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Script completo, suite de la API y verificación de SQL real

**Precondición:** Tasks 1–8 completas; `dotnet build API.sln` → 0 errores.

**Files:**
- Modify: `sql/crear-api-db-test-completo.sql`

- [ ] **Step 1: Agregar el bloque de pagos al script completo.** Al **final** de `sql/crear-api-db-test-completo.sql` (después de las consultas finales del bloque de taller), agregar una línea `GO` y luego un banner `-- ===== Pagos recibidos (objeto 24): cobros a clientes =====` seguido del **cuerpo** de `sql/2026-10-23-pagos-recibidos.sql` (desde `-- ===== FormaPago (catalogo) =====` hasta su último `SELECT`), sin las líneas de cabecera del archivo standalone (`-- Pagos recibidos (objeto 24)…`, `-- Idempotente…`, `-- Archivo UTF-8…`, `SET NOCOUNT ON;`). Conservar el EOL y la codificación del archivo (comprobar con `file`; el script completo es UTF-8, escribir con node/PowerShell `-Encoding utf8` sin alterar el resto: `git diff --stat` solo debe mostrar líneas agregadas al final).

- [ ] **Step 2: Verificar el script completo en una base nueva y el standalone sobre el esquema anterior** (PowerShell; no imprime la contraseña; elimina ambas bases al terminar):

```powershell
$ErrorActionPreference = 'Stop'
$SP = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
$repo = 'C:\Users\migue\source\repos\angelm0508\API'
$cfg = Get-Content "$repo\API.Service.WebApi\appsettings.json" -Raw
$pw = [regex]::Match($cfg, 'Password=([^;"]*)').Groups[1].Value
function Q($db, $q) { sqlcmd -S localhost -U sa -P $pw -C -d $db -h -1 -W -b -Q "SET NOCOUNT ON; $q" }

# A) script completo NUEVO sobre una base vacia
$dbA = 'API_DB_PAGOS_A'
$sqlA = (Get-Content "$repo\sql\crear-api-db-test-completo.sql" -Raw -Encoding UTF8) -replace 'API_DB_TEST', $dbA
if ($sqlA -match 'API_DB_TEST') { throw 'guard A' }
$sqlA | Set-Content "$SP\$dbA.sql" -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$SP\$dbA.sql" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'fallo el script completo nuevo' }
'A) FormaPago / Modulo / Serie24:'; Q $dbA "SELECT COUNT(*) FROM FormaPago; SELECT COUNT(*) FROM Modulo WHERE Codigo IN ('PagosRecibidos','FormasPago'); SELECT COUNT(*) FROM NumeracionDocumentoDet WHERE CodigoObj='24';"

# B) script standalone dos veces sobre una base construida con el script completo ANTERIOR (adae904)
$dbB = 'API_DB_PAGOS_B'
$viejo = git -C $repo show adae904:sql/crear-api-db-test-completo.sql
$sqlB = ($viejo -join "`n") -replace 'API_DB_TEST', $dbB
if ($sqlB -match 'API_DB_TEST') { throw 'guard B' }
$sqlB | Set-Content "$SP\$dbB.sql" -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$SP\$dbB.sql" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'fallo el script completo anterior' }
1..2 | ForEach-Object {
    sqlcmd -S localhost -U sa -P $pw -C -d $dbB -b -f 65001 -i "$repo\sql\2026-10-23-pagos-recibidos.sql" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "fallo la corrida $_ del standalone" }
}
'B) tras 2 corridas:'; Q $dbB "SELECT COUNT(*) FROM FormaPago; SELECT COUNT(*) FROM Modulo WHERE Codigo IN ('PagosRecibidos','FormasPago'); SELECT COUNT(*) FROM NumeracionDocumentoDet WHERE CodigoObj='24'; SELECT COUNT(*) FROM NumeracionDocumento WHERE CodigoObj='24';"

foreach ($db in $dbA, $dbB) {
    sqlcmd -S localhost -U sa -P $pw -C -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE $db SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $db; END"
}
```

Esperado: A) `4`, `2`, `1`; B) `4`, `2`, `1`, `1`; ambas bases eliminadas (`SELECT name FROM sys.databases WHERE name LIKE 'API_DB_PAGOS%'` sin filas). Si falla, corregir el SQL (en el standalone **y** en el bloque agregado, deben ser idénticos) y repetir.

- [ ] **Step 3: Suite completa** — PowerShell: `dotnet test API.sln -p:BaseOutputPath="…\apitest\"`. Esperado: 0 fallos; el total es 1800 + las pruebas nuevas de las Tasks 1–8 (anotar el número exacto en el reporte). Si alguna prueba preexistente falla por un constructor cambiado (`FacturaDomain`, `NotaDebitoDomain`, `NotaCreditoDomain`) en un archivo que la tarea correspondiente no actualizó, corregir **la prueba** (agregar el mock) y mencionarlo.

- [ ] **Step 4: Commit**

```bash
git add sql/crear-api-db-test-completo.sql
git commit -m "docs(db): script completo de creacion de API_DB_TEST incluye pagos recibidos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 4 — Web: clientes tipados, formas de pago, pagos recibidos y saldos en los listados

### Task 10: DTOs y clientes tipados de la Web

**Precondición:** Task 9 completa (API); en la Web `git log --oneline -1` muestra `315e15f` o posterior y `git status --short` solo muestra los 9 archivos ajenos; `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` (PowerShell) → 0 errores.

**Files (Web):**
- Create: `Web.ApiClient/Dtos/FormaPago/{FormaPagoDTO,FormaPagoCrearDTO,FormaPagoActualizarDTO}.cs`
- Create: `Web.ApiClient/Dtos/PagoRecibido/{PagoRecibidoDTO,PagoRecibidoFacturaDTO,PagoRecibidoMedioDTO,PagoRecibidoCrearDTO,PagoRecibidoFacturaCrearDTO,PagoRecibidoMedioCrearDTO,DocumentoPorCobrarDTO}.cs`
- Create: `Web.ApiClient/Dtos/SaldoFactura/SaldoFacturaDTO.cs`
- Create: `Web.ApiClient/Clientes/{IFormaPagoApiClient,FormaPagoApiClient,IPagoRecibidoApiClient,PagoRecibidoApiClient,ISaldoFacturaApiClient,SaldoFacturaApiClient}.cs`
- Modify: `Web.UI/Program.cs`, `Web.ApiClient/Dtos/Factura/FacturaDTO.cs`, `Web.ApiClient/Dtos/NotaDebito/NotaDebitoDTO.cs`

**Interfaces:**
- Consumes: endpoints de las Tasks 2, 5 y 8 (`api/FormaPago`, `api/PagoRecibido…`, `POST api/Factura/Saldos`, `POST api/NotaDebito/Saldos`); `ApiClientBase` (`GetAsync/PostAsync/PutAsync/DeleteAsync`), `Respuesta<T>`.
- Produces (namespaces `Web.ApiClient.Dtos.FormaPago`, `.PagoRecibido`, `.SaldoFactura`): los DTOs de abajo (mismas propiedades que los de la API, en PascalCase; el JSON viaja en camelCase). Clientes: `IFormaPagoApiClient { ObtenerTodoAsync(); ObtenerAsync(string); InsertarAsync(FormaPagoCrearDTO); ActualizarAsync(string, FormaPagoActualizarDTO); EliminarAsync(string) }`; `IPagoRecibidoApiClient { ObtenerTodoAsync(); ObtenerAsync(int); ObtenerFacturasAsync(int); ObtenerMediosAsync(int); ObtenerDocumentosPorCobrarAsync(string codigoSn); InsertarAsync(PagoRecibidoCrearDTO) → Respuesta<int>; CancelarAsync(int) → Respuesta<bool> }`; `ISaldoFacturaApiClient { ObtenerSaldosFacturasAsync(IEnumerable<int>); ObtenerSaldosNotasDebitoAsync(IEnumerable<int>) }`. `FacturaDTO` y `NotaDebitoDTO` ganan `decimal? Pagado`, `decimal? Saldo`, `string? EstadoPago` (la API no los envía; la Web los llena en la Task 14).

No hay pruebas automáticas de la Web; la verificación de esta tarea es la compilación.

- [ ] **Step 1: DTOs de formas de pago** (un archivo por clase, LF):

`FormaPagoDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.FormaPago
{
    public class FormaPagoDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        /// <summary>N = sin dato, C = cheque, T = transferencia, J = tarjeta.</summary>
        public string TipoDato { get; set; } = "N";
        public string Activo { get; set; } = "S";
    }
}
```

`FormaPagoCrearDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.FormaPago
{
    public class FormaPagoCrearDTO
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; } = "N";
        public string? Activo { get; set; } = "S";
    }
}
```

`FormaPagoActualizarDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.FormaPago
{
    public class FormaPagoActualizarDTO
    {
        public string Nombre { get; set; } = null!;
        public string? TipoDato { get; set; }
        public string? Activo { get; set; }
    }
}
```

- [ ] **Step 2: DTOs de pagos recibidos** (namespace `Web.ApiClient.Dtos.PagoRecibido`):

`PagoRecibidoDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.PagoRecibido
{
    public class PagoRecibidoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public decimal TotalPago { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`PagoRecibidoFacturaDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.PagoRecibido
{
    public class PagoRecibidoFacturaDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public int FacturaEntry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura; "ND" = nota de débito de cliente.</summary>
        public string? DocSubType { get; set; }
        public decimal? TotalDoc { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
```

`PagoRecibidoMedioDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.PagoRecibido
{
    public class PagoRecibidoMedioDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string CodFormaPago { get; set; } = null!;
        public string? NombreFormaPago { get; set; }
        public string? TipoDato { get; set; }
        public decimal Monto { get; set; }
        public string? Referencia { get; set; }
        public string? Banco { get; set; }
        public DateTime? FechaCheque { get; set; }
    }
}
```

`PagoRecibidoCrearDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.PagoRecibido
{
    public class PagoRecibidoCrearDTO
    {
        public int Serie { get; set; }
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public string? CodigoSn { get; set; }
        public string? Comentario { get; set; }
        public List<PagoRecibidoFacturaCrearDTO> Facturas { get; set; } = new();
        public List<PagoRecibidoMedioCrearDTO> Medios { get; set; } = new();
    }
}
```

`PagoRecibidoFacturaCrearDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.PagoRecibido
{
    public class PagoRecibidoFacturaCrearDTO
    {
        public int FacturaEntry { get; set; }
        public decimal MontoAplicado { get; set; }
    }
}
```

`PagoRecibidoMedioCrearDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.PagoRecibido
{
    public class PagoRecibidoMedioCrearDTO
    {
        public string? CodFormaPago { get; set; }
        public decimal Monto { get; set; }
        public string? Referencia { get; set; }
        public string? Banco { get; set; }
        public DateTime? FechaCheque { get; set; }
    }
}
```

`DocumentoPorCobrarDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.PagoRecibido
{
    public class DocumentoPorCobrarDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        /// <summary>"--" = factura; "ND" = nota de débito de cliente.</summary>
        public string? DocSubType { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal Total { get; set; }
        public decimal Acreditado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Saldo { get; set; }
        public string Estado { get; set; } = null!;
    }
}
```

`Web.ApiClient/Dtos/SaldoFactura/SaldoFacturaDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.SaldoFactura
{
    public class SaldoFacturaDTO
    {
        public int FacturaEntry { get; set; }
        public decimal Total { get; set; }
        public decimal Acreditado { get; set; }
        public decimal Pagado { get; set; }
        public decimal Saldo { get; set; }
        /// <summary>Pendiente, Parcial o Pagada.</summary>
        public string Estado { get; set; } = null!;
    }
}
```

- [ ] **Step 3: Clientes tipados** (namespace `Web.ApiClient.Clientes`):

`IFormaPagoApiClient.cs` y `FormaPagoApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.FormaPago;

namespace Web.ApiClient.Clientes
{
    public interface IFormaPagoApiClient
    {
        Task<Respuesta<IEnumerable<FormaPagoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<FormaPagoDTO>> ObtenerAsync(string codigo);
        Task<Respuesta<bool>> InsertarAsync(FormaPagoCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(string codigo, FormaPagoActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(string codigo);
    }
}
```

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.FormaPago;

namespace Web.ApiClient.Clientes
{
    public class FormaPagoApiClient : ApiClientBase, IFormaPagoApiClient
    {
        private const string Recurso = "api/FormaPago";

        public FormaPagoApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<FormaPagoDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<FormaPagoDTO>>(Recurso);

        public Task<Respuesta<FormaPagoDTO>> ObtenerAsync(string codigo) =>
            GetAsync<FormaPagoDTO>($"{Recurso}/{Uri.EscapeDataString(codigo)}");

        public Task<Respuesta<bool>> InsertarAsync(FormaPagoCrearDTO dto) =>
            PostAsync<bool>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(string codigo, FormaPagoActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{Uri.EscapeDataString(codigo)}", dto);

        public Task<Respuesta<bool>> EliminarAsync(string codigo) =>
            DeleteAsync<bool>($"{Recurso}/{Uri.EscapeDataString(codigo)}");
    }
}
```

`IPagoRecibidoApiClient.cs` y `PagoRecibidoApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.PagoRecibido;

namespace Web.ApiClient.Clientes
{
    public interface IPagoRecibidoApiClient
    {
        Task<Respuesta<IEnumerable<PagoRecibidoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<PagoRecibidoDTO>> ObtenerAsync(int entry);
        Task<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>> ObtenerFacturasAsync(int entry);
        Task<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>> ObtenerMediosAsync(int entry);
        Task<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>> ObtenerDocumentosPorCobrarAsync(string codigoSn);
        Task<Respuesta<int>> InsertarAsync(PagoRecibidoCrearDTO dto);
        Task<Respuesta<bool>> CancelarAsync(int entry);
    }
}
```

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.PagoRecibido;

namespace Web.ApiClient.Clientes
{
    public class PagoRecibidoApiClient : ApiClientBase, IPagoRecibidoApiClient
    {
        private const string Recurso = "api/PagoRecibido";

        public PagoRecibidoApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<PagoRecibidoDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<PagoRecibidoDTO>>(Recurso);

        public Task<Respuesta<PagoRecibidoDTO>> ObtenerAsync(int entry) =>
            GetAsync<PagoRecibidoDTO>($"{Recurso}/{entry}");

        public Task<Respuesta<IEnumerable<PagoRecibidoFacturaDTO>>> ObtenerFacturasAsync(int entry) =>
            GetAsync<IEnumerable<PagoRecibidoFacturaDTO>>($"{Recurso}/{entry}/Facturas");

        public Task<Respuesta<IEnumerable<PagoRecibidoMedioDTO>>> ObtenerMediosAsync(int entry) =>
            GetAsync<IEnumerable<PagoRecibidoMedioDTO>>($"{Recurso}/{entry}/Medios");

        public Task<Respuesta<IEnumerable<DocumentoPorCobrarDTO>>> ObtenerDocumentosPorCobrarAsync(string codigoSn) =>
            GetAsync<IEnumerable<DocumentoPorCobrarDTO>>($"{Recurso}/FacturasAbiertas/{Uri.EscapeDataString(codigoSn)}");

        public Task<Respuesta<int>> InsertarAsync(PagoRecibidoCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> CancelarAsync(int entry) =>
            PostAsync<bool>($"{Recurso}/{entry}/Cancelar", new { });
    }
}
```

`ISaldoFacturaApiClient.cs` y `SaldoFacturaApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.SaldoFactura;

namespace Web.ApiClient.Clientes
{
    public interface ISaldoFacturaApiClient
    {
        Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerSaldosFacturasAsync(IEnumerable<int> entries);
        Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerSaldosNotasDebitoAsync(IEnumerable<int> entries);
    }
}
```

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.SaldoFactura;

namespace Web.ApiClient.Clientes
{
    public class SaldoFacturaApiClient : ApiClientBase, ISaldoFacturaApiClient
    {
        public SaldoFacturaApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerSaldosFacturasAsync(IEnumerable<int> entries) =>
            PostAsync<IEnumerable<SaldoFacturaDTO>>("api/Factura/Saldos", entries.ToList());

        public Task<Respuesta<IEnumerable<SaldoFacturaDTO>>> ObtenerSaldosNotasDebitoAsync(IEnumerable<int> entries) =>
            PostAsync<IEnumerable<SaldoFacturaDTO>>("api/NotaDebito/Saldos", entries.ToList());
    }
}
```

- [ ] **Step 4: Registrar en `Web.UI/Program.cs`** — junto al registro de `IOrdenServicioApiClient` (conservar EOL) agregar:

```csharp
builder.Services.AddHttpClient<IFormaPagoApiClient, FormaPagoApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<IPagoRecibidoApiClient, PagoRecibidoApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<ISaldoFacturaApiClient, SaldoFacturaApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 5: Campos de saldo en los DTO de listado** — `Web.ApiClient/Dtos/Factura/FacturaDTO.cs` y `Web.ApiClient/Dtos/NotaDebito/NotaDebitoDTO.cs`: antes de la llave de cierre de la clase agregar:

```csharp
        /// <summary>Suma de pagos activos aplicados. La llena el controlador de la Web (no viene del API de facturas).</summary>
        public decimal? Pagado { get; set; }
        /// <summary>Saldo por cobrar (total − notas de crédito − pagos).</summary>
        public decimal? Saldo { get; set; }
        /// <summary>Pendiente, Parcial o Pagada.</summary>
        public string? EstadoPago { get; set; }
```

- [ ] **Step 6: Compilar** — PowerShell: `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"`. Esperado: 0 errores.

- [ ] **Step 7: Commit** (Web)

```bash
git add Web.ApiClient/Dtos/FormaPago Web.ApiClient/Dtos/PagoRecibido Web.ApiClient/Dtos/SaldoFactura Web.ApiClient/Clientes/IFormaPagoApiClient.cs Web.ApiClient/Clientes/FormaPagoApiClient.cs Web.ApiClient/Clientes/IPagoRecibidoApiClient.cs Web.ApiClient/Clientes/PagoRecibidoApiClient.cs Web.ApiClient/Clientes/ISaldoFacturaApiClient.cs Web.ApiClient/Clientes/SaldoFacturaApiClient.cs Web.UI/Program.cs Web.ApiClient/Dtos/Factura/FacturaDTO.cs Web.ApiClient/Dtos/NotaDebito/NotaDebitoDTO.cs
git commit -m "feat(pagos): DTOs y clientes tipados de formas de pago, pagos recibidos y saldos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Pantalla de formas de pago y menú

**Precondición:** Task 10 completa (Web); build en 0 errores.

**Files (Web):**
- Create: `Web.UI/Controllers/FormasPagoController.cs`, `Web.UI/Views/FormasPago/Index.cshtml`, `Web.UI/Views/FormasPago/_Form.cshtml`, `Web.UI/wwwroot/js/formaspago.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes (Task 10): `IFormaPagoApiClient`, `FormaPagoCrearDTO`, `FormaPagoActualizarDTO`.
- Produces: rutas `/FormasPago` (Index), `/FormasPago/ObtenerTodos`, `FormularioCrear`, `FormularioEditar`, `Crear`, `Editar`, `Eliminar`; módulo de permisos `FormasPago`; entrada "Formas de pago" en el submenú Ventas.

- [ ] **Step 1: Controlador** — `Web.UI/Controllers/FormasPagoController.cs` (mismo esquema que `MecanicosController`):

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.FormaPago;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class FormasPagoController : Controller
    {
        private readonly IFormaPagoApiClient _formas;

        public FormasPagoController(IFormaPagoApiClient formas)
        {
            _formas = formas;
        }

        [RequierePermiso("FormasPago", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("FormasPago", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _formas.ObtenerTodoAsync();
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("FormasPago", Accion.Agregar)]
        public IActionResult FormularioCrear()
        {
            ViewBag.EsEdicion = false;
            return PartialView("_Form", new FormaPagoCrearDTO { TipoDato = "N", Activo = "S" });
        }

        [HttpGet]
        [RequierePermiso("FormasPago", Accion.Actualizar)]
        public async Task<IActionResult> FormularioEditar(string codigo)
        {
            var respuesta = await _formas.ObtenerAsync(codigo);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            ViewBag.EsEdicion = true;
            var dto = new FormaPagoCrearDTO
            {
                Codigo = respuesta.Dato.Codigo,
                Nombre = respuesta.Dato.Nombre,
                TipoDato = respuesta.Dato.TipoDato,
                Activo = respuesta.Dato.Activo
            };
            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("FormasPago", Accion.Agregar)]
        public async Task<IActionResult> Crear([FromBody] FormaPagoCrearDTO dto)
        {
            var respuesta = await _formas.InsertarAsync(dto);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("FormasPago", Accion.Actualizar)]
        public async Task<IActionResult> Editar(string codigo, [FromBody] FormaPagoCrearDTO dto)
        {
            var actualizar = new FormaPagoActualizarDTO { Nombre = dto.Nombre, TipoDato = dto.TipoDato, Activo = dto.Activo };
            var respuesta = await _formas.ActualizarAsync(codigo, actualizar);
            return Json(respuesta);
        }

        // Baja lógica: la forma de pago pasa a inactiva (puede tener pagos registrados).
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("FormasPago", Accion.Eliminar)]
        public async Task<IActionResult> Eliminar(string codigo)
        {
            var respuesta = await _formas.EliminarAsync(codigo);
            return Json(respuesta);
        }
    }
}
```

- [ ] **Step 2: Vistas** —

`Web.UI/Views/FormasPago/Index.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Formas de pago";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Formas de pago</h3>
    @if (User.TienePermiso("FormasPago", Accion.Agregar))
    {
        <button type="button" class="btn btn-primary" id="btnNuevo">
            <i class="fa-solid fa-plus me-1"></i>Nuevo
        </button>
    }
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblFormasPago" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>Código</th>
                        <th>Nombre</th>
                        <th>Dato adicional</th>
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

<script id="datosPermisosFormasPago" type="application/json">
    { "actualizar": @(User.TienePermiso("FormasPago", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("FormasPago", Accion.Eliminar) ? "true" : "false") }
</script>

@section Scripts {
    <script src="~/js/formaspago.js" asp-append-version="true"></script>
}
```

`Web.UI/Views/FormasPago/_Form.cshtml`:

```cshtml
@model Web.ApiClient.Dtos.FormaPago.FormaPagoCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? "Editar forma de pago" : "Nueva forma de pago")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formFormaPago" novalidate>
        <div class="row g-3">
            <div class="col-md-4">
                <label asp-for="Codigo" class="form-label">Código</label>
                <input asp-for="Codigo" class="form-control" readonly="@esEdicion" maxlength="10" />
            </div>
            <div class="col-md-8">
                <label asp-for="Nombre" class="form-label">Nombre</label>
                <input asp-for="Nombre" class="form-control" maxlength="50" />
            </div>
            <div class="col-md-8">
                <label asp-for="TipoDato" class="form-label">Dato adicional que se pide al cobrar</label>
                <select asp-for="TipoDato" class="form-select">
                    <option value="N">Ninguno (efectivo)</option>
                    <option value="C">Cheque (número y banco)</option>
                    <option value="T">Transferencia (referencia)</option>
                    <option value="J">Tarjeta (autorización o últimos 4 dígitos)</option>
                </select>
                @if (esEdicion)
                {
                    <span class="form-text">No se puede cambiar si la forma de pago ya se usó en algún pago.</span>
                }
            </div>
            <div class="col-md-4">
                <label asp-for="Activo" class="form-label">Activo</label>
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
    <button type="button" class="btn btn-primary" id="btnGuardarFormaPago" data-edicion="@esEdicion.ToString().ToLower()" data-codigo="@Model.Codigo">
        <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
    </button>
</div>
```

- [ ] **Step 3: JavaScript** — `Web.UI/wwwroot/js/formaspago.js`:

```javascript
$(function () {
    const datosPermisosEl = document.getElementById('datosPermisosFormasPago');
    const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true };
    // Escapa texto de usuario para interpolarlo en HTML y en atributos entre comillas dobles.
    const escapar = v => $('<div>').text(v ?? '').html().replace(/"/g, '&quot;');
    // Escapa solo al pintar; para buscar y ordenar devuelve el texto plano.
    const textoSeguro = (d, t) => t === 'display' ? escapar(d) : (d || '');
    const tiposDato = { N: 'Ninguno', C: 'Cheque', T: 'Transferencia', J: 'Tarjeta' };

    const tabla = $('#tblFormasPago').DataTable({
        ajax: { url: '/FormasPago/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'codigo', render: textoSeguro },
            { data: 'nombre', render: textoSeguro },
            { data: 'tipoDato', render: d => tiposDato[d] || '' },
            { data: 'activo', render: d => d === 'S' ? '<span class="badge text-bg-success">Sí</span>' : '<span class="badge text-bg-secondary">No</span>' },
            {
                data: 'codigo', orderable: false, searchable: false, className: 'text-end',
                render: (codigo, type, row) => `
                    ${permisos.actualizar ? `<button class="btn btn-sm btn-outline-primary btn-editar" data-codigo="${escapar(codigo)}"><i class="fa-solid fa-pen"></i></button>` : ''}
                    ${permisos.eliminar && row.activo === 'S' ? `<button class="btn btn-sm btn-outline-danger btn-desactivar" data-codigo="${escapar(codigo)}" title="Desactivar"><i class="fa-solid fa-ban"></i></button>` : ''}
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
        const html = await $.get('/FormasPago/FormularioCrear');
        abrirModal(html);
    });

    $('#tblFormasPago').on('click', '.btn-editar', async function () {
        const codigo = $(this).data('codigo');
        const html = await $.get('/FormasPago/FormularioEditar', { codigo });
        abrirModal(html);
    });

    $('#tblFormasPago').on('click', '.btn-desactivar', async function () {
        const codigo = $(this).data('codigo');
        const confirmado = await Swal.fire({
            icon: 'warning',
            title: '¿Desactivar forma de pago?',
            text: `La forma de pago "${codigo}" dejará de ofrecerse en los pagos nuevos. Los pagos anteriores no se pierden.`,
            showCancelButton: true,
            confirmButtonText: 'Desactivar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: '#dc3545'
        }).then(r => r.isConfirmed);
        if (!confirmado) return;

        const respuesta = await App.eliminar(`/FormasPago/Eliminar?codigo=${encodeURIComponent(codigo)}`);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Forma de pago desactivada correctamente.');
        recargarTabla();
    });

    $(document).on('click', '#btnGuardarFormaPago', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const codigo = $boton.data('codigo');

        const datos = App.recolectarFormulario('#formFormaPago');
        const url = esEdicion ? `/FormasPago/Editar?codigo=${encodeURIComponent(codigo)}` : '/FormasPago/Crear';

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.enviarJson(url, 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            App.mostrarExito(esEdicion ? 'Forma de pago actualizada correctamente.' : 'Forma de pago creada correctamente.');
            recargarTabla();
        } finally {
            $boton.prop('disabled', false);
        }
    });
});
```

- [ ] **Step 4: Menú** — en `Web.UI/Views/Shared/_Layout.cshtml` (comprobar EOL): (a) en las líneas de `EsActivoVentas` y `MuestraVentas` agregar `"PagosRecibidos", "FormasPago"` al arreglo de nombres (después de `"NotasCredito"`); (b) dentro del submenú Ventas (`id="submenuVentas"`), justo después del bloque `@if (Puede("NotasCredito")) { … }` y antes del `</div>` que cierra el submenú, agregar:

```cshtml
                            @if (Puede("FormasPago"))
                            {
                                <a class="nav-link nav-sublink @(EsActivo("FormasPago") ? "active" : "")" asp-controller="FormasPago" asp-action="Index">
                                    <i class="fa-solid fa-credit-card"></i><span>Formas de pago</span>
                                </a>
                            }
```

- [ ] **Step 5: Compilar y verificar sintaxis** — PowerShell: `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` (0 errores) y `node --check Web.UI\wwwroot\js\formaspago.js` (sin salida = OK).

- [ ] **Step 6: Commit** (Web)

```bash
git add Web.UI/Controllers/FormasPagoController.cs Web.UI/Views/FormasPago Web.UI/wwwroot/js/formaspago.js Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(pagos): pantalla de formas de pago y entrada en el menu Ventas

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Listado de pagos recibidos, detalle y cancelación

**Precondición:** Task 11 completa (Web); build en 0 errores.

**Files (Web):**
- Create: `Web.UI/Controllers/PagosRecibidosController.cs`, `Web.UI/Views/PagosRecibidos/Index.cshtml`, `Web.UI/Views/PagosRecibidos/_Detalle.cshtml`, `Web.UI/wwwroot/js/pagosrecibidos.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes (Task 10): `IPagoRecibidoApiClient`, DTOs de pagos.
- Produces: rutas `/PagosRecibidos` (Index), `/PagosRecibidos/ObtenerTodos`, `/PagosRecibidos/Detalle?entry=`, `POST /PagosRecibidos/Cancelar?entry=`; la Task 13 agrega `Crear`, `BuscarSocios`, `DocumentosPorCobrar` al mismo controlador y cambia su constructor.

- [ ] **Step 1: Controlador** — `Web.UI/Controllers/PagosRecibidosController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class PagosRecibidosController : Controller
    {
        private readonly IPagoRecibidoApiClient _pagos;

        public PagosRecibidosController(IPagoRecibidoApiClient pagos)
        {
            _pagos = pagos;
        }

        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _pagos.ObtenerTodoAsync();
            return Json(respuesta);
        }

        // Detalle de solo lectura (modal): cabecera, documentos aplicados y formas de pago.
        [HttpGet]
        [RequierePermiso("PagosRecibidos", Accion.Ver)]
        public async Task<IActionResult> Detalle(int entry)
        {
            var pago = await _pagos.ObtenerAsync(entry);
            if (!pago.Resultado || pago.Dato is null)
                return NotFound();

            var facturas = await _pagos.ObtenerFacturasAsync(entry);
            var medios = await _pagos.ObtenerMediosAsync(entry);
            ViewBag.Facturas = facturas.Dato ?? [];
            ViewBag.Medios = medios.Dato ?? [];
            return PartialView("_Detalle", pago.Dato);
        }

        // Cancelar un pago es anularlo: requiere Eliminar (convención: 'cancelar documento -> Eliminar').
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("PagosRecibidos", Accion.Eliminar)]
        public async Task<IActionResult> Cancelar(int entry)
        {
            var respuesta = await _pagos.CancelarAsync(entry);
            return Json(respuesta);
        }
    }
}
```

- [ ] **Step 2: Vistas** —

`Web.UI/Views/PagosRecibidos/Index.cshtml`:

```cshtml
@{
    ViewData["Title"] = "Pagos recibidos";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Pagos recibidos</h3>
    <div class="d-flex gap-2">
        <select id="filtroEstado" class="form-select" style="width: auto;">
            <option value="">Todos</option>
            <option value="Vigente">Vigentes</option>
            <option value="Cancelado">Cancelados</option>
        </select>
        @if (User.TienePermiso("PagosRecibidos", Accion.Agregar))
        {
            <a asp-action="Crear" class="btn btn-primary">
                <i class="fa-solid fa-plus me-1"></i>Nuevo pago
            </a>
        }
    </div>
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblPagosRecibidos" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>No. pago</th>
                        <th>Fecha</th>
                        <th>Cliente</th>
                        <th class="text-end">Total</th>
                        <th>Estado</th>
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

@section Scripts {
    <script src="~/js/pagosrecibidos.js" asp-append-version="true"></script>
}
```

(El botón "Nuevo pago" apunta a `Crear`, que agrega la Task 13: hasta entonces el enlace no se usa; el proyecto compila porque `asp-action` solo genera la URL.)

`Web.UI/Views/PagosRecibidos/_Detalle.cshtml`:

```cshtml
@model Web.ApiClient.Dtos.PagoRecibido.PagoRecibidoDTO
@{
    var facturas = (IEnumerable<Web.ApiClient.Dtos.PagoRecibido.PagoRecibidoFacturaDTO>)ViewBag.Facturas;
    var medios = (IEnumerable<Web.ApiClient.Dtos.PagoRecibido.PagoRecibidoMedioDTO>)ViewBag.Medios;
    bool cancelado = Model.Cancelado == "S";
}

<div class="modal-header">
    <h5 class="modal-title">
        Pago recibido @Model.NumDoc
        @if (cancelado)
        {
            <span class="badge text-bg-danger ms-2">Cancelado</span>
        }
        else
        {
            <span class="badge text-bg-success ms-2">Vigente</span>
        }
    </h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <div class="row g-3 mb-3">
        <div class="col-md-4"><div class="small text-muted">Fecha</div>@Model.FechaDoc?.ToString("dd/MM/yyyy")</div>
        <div class="col-md-5"><div class="small text-muted">Cliente</div>@Model.CodigoSn - @Model.NombreSn</div>
        <div class="col-md-3"><div class="small text-muted">Total</div><strong>@Model.TotalPago.ToString("0.00")</strong></div>
        @if (cancelado)
        {
            <div class="col-md-4"><div class="small text-muted">Cancelado el</div>@Model.FechaCancelado?.ToString("dd/MM/yyyy HH:mm")</div>
        }
        @if (!string.IsNullOrWhiteSpace(Model.Comentario))
        {
            <div class="col-12"><div class="small text-muted">Comentario</div>@Model.Comentario</div>
        }
    </div>

    <h6>Documentos pagados</h6>
    <table class="table table-sm align-middle">
        <thead>
            <tr><th>Documento</th><th class="text-end">Total del documento</th><th class="text-end">Monto aplicado</th></tr>
        </thead>
        <tbody>
            @foreach (var f in facturas)
            {
                <tr>
                    <td>@(f.DocSubType == "ND" ? "Nota de débito " : "Factura ")@f.NumDoc</td>
                    <td class="text-end">@(f.TotalDoc?.ToString("0.00"))</td>
                    <td class="text-end">@f.MontoAplicado.ToString("0.00")</td>
                </tr>
            }
        </tbody>
    </table>

    <h6 class="mt-3">Formas de pago</h6>
    <table class="table table-sm align-middle">
        <thead>
            <tr><th>Forma</th><th>Referencia</th><th>Banco</th><th>Fecha del cheque</th><th class="text-end">Monto</th></tr>
        </thead>
        <tbody>
            @foreach (var m in medios)
            {
                <tr>
                    <td>@(m.NombreFormaPago ?? m.CodFormaPago)</td>
                    <td>@m.Referencia</td>
                    <td>@m.Banco</td>
                    <td>@m.FechaCheque?.ToString("dd/MM/yyyy")</td>
                    <td class="text-end">@m.Monto.ToString("0.00")</td>
                </tr>
            }
        </tbody>
    </table>
</div>
<div class="modal-footer">
    @if (!cancelado && User.TienePermiso("PagosRecibidos", Accion.Eliminar))
    {
        <button type="button" class="btn btn-outline-danger me-auto" id="btnCancelarPago" data-entry="@Model.Entry" data-numdoc="@Model.NumDoc">
            <i class="fa-solid fa-ban me-1"></i>Cancelar pago
        </button>
    }
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>
</div>
```

- [ ] **Step 3: JavaScript** — `Web.UI/wwwroot/js/pagosrecibidos.js`:

```javascript
$(function () {
    // Escapa texto de usuario para interpolarlo en HTML. Solo al pintar; para buscar y ordenar se devuelve el texto plano.
    const escapar = v => $('<div>').text(v ?? '').html().replace(/"/g, '&quot;');
    const textoSeguro = (d, t) => t === 'display' ? escapar(d) : (d || '');

    const tabla = $('#tblPagosRecibidos').DataTable({
        ajax: { url: '/PagosRecibidos/ObtenerTodos', dataSrc: App.dataSrcTabla },
        order: [[0, 'desc']],
        columns: [
            { data: 'numDoc' },
            { data: 'fechaDoc', render: (d, t) => d ? (t === 'display' ? new Date(d).toLocaleDateString() : d) : '' },
            { data: 'nombreSn', render: (d, t, row) => textoSeguro(d || row.codigoSn || '', t) },
            { data: 'totalPago', className: 'text-end', render: (d, t) => d != null ? (t === 'display' ? Number(d).toFixed(2) : d) : '' },
            {
                data: 'cancelado',
                render: (d, t) => t === 'display'
                    ? (d === 'S' ? '<span class="badge text-bg-danger">Cancelado</span>' : '<span class="badge text-bg-success">Vigente</span>')
                    : (d === 'S' ? 'Cancelado' : 'Vigente')
            },
            {
                data: 'entry', orderable: false, searchable: false, className: 'text-end',
                render: entry => `<button class="btn btn-sm btn-outline-primary btn-ver" data-entry="${entry}" title="Ver detalle"><i class="fa-solid fa-eye"></i></button>`
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    $('#filtroEstado').on('change', function () {
        const valor = this.value;
        tabla.column(4).search(valor ? `^${valor}$` : '', true, false).draw();
    });

    async function abrirDetalle(entry) {
        try {
            const html = await $.get('/PagosRecibidos/Detalle', { entry });
            $('#contenidoModal').html(html);
            bootstrap.Modal.getOrCreateInstance(document.getElementById('modalFormulario')).show();
        } catch (e) {
            App.mostrarError('No se pudo cargar el detalle del pago.');
        }
    }

    $('#tblPagosRecibidos').on('click', '.btn-ver', function () {
        abrirDetalle($(this).data('entry'));
    });

    $('#tblPagosRecibidos tbody').on('dblclick', 'tr', function () {
        const fila = tabla.row(this).data();
        if (fila) abrirDetalle(fila.entry);
    });

    $(document).on('click', '#btnCancelarPago', async function () {
        const $boton = $(this);
        const entry = $boton.data('entry');
        const numDoc = $boton.data('numdoc');
        const confirmado = await Swal.fire({
            icon: 'warning',
            title: '¿Cancelar el pago?',
            text: `El pago ${numDoc} se marcará como cancelado y el saldo de las facturas que cubría volverá a estar pendiente. Esta acción no se puede deshacer.`,
            showCancelButton: true,
            confirmButtonText: 'Cancelar pago',
            cancelButtonText: 'Volver',
            confirmButtonColor: '#dc3545'
        }).then(r => r.isConfirmed);
        if (!confirmado) return;

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.eliminar(`/PagosRecibidos/Cancelar?entry=${encodeURIComponent(entry)}`);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            App.mostrarExito('Pago cancelado correctamente.');
            recargarTabla();
        } finally {
            $boton.prop('disabled', false);
        }
    });
});
```

- [ ] **Step 4: Menú** — en `_Layout.cshtml`, dentro del submenú Ventas, **antes** del bloque `@if (Puede("FormasPago"))` agregado en la Task 11, agregar:

```cshtml
                            @if (Puede("PagosRecibidos"))
                            {
                                <a class="nav-link nav-sublink @(EsActivo("PagosRecibidos") ? "active" : "")" asp-controller="PagosRecibidos" asp-action="Index">
                                    <i class="fa-solid fa-hand-holding-dollar"></i><span>Pagos recibidos</span>
                                </a>
                            }
```

- [ ] **Step 5: Compilar y verificar sintaxis** — `dotnet build Web.slnx …` (0 errores) y `node --check Web.UI\wwwroot\js\pagosrecibidos.js`.

- [ ] **Step 6: Commit** (Web)

```bash
git add Web.UI/Controllers/PagosRecibidosController.cs Web.UI/Views/PagosRecibidos Web.UI/wwwroot/js/pagosrecibidos.js Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(pagos): listado, detalle y cancelacion de pagos recibidos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Alta de un pago recibido (página estilo SAP)

**Precondición:** Task 12 completa (Web); build en 0 errores.

**Files (Web):**
- Modify: `Web.UI/Controllers/PagosRecibidosController.cs`
- Create: `Web.UI/Views/PagosRecibidos/Crear.cshtml`, `Web.UI/wwwroot/js/pagosrecibidos-crear.js`

**Interfaces:**
- Consumes (Task 10): `IPagoRecibidoApiClient.ObtenerDocumentosPorCobrarAsync/InsertarAsync`, `IFormaPagoApiClient.ObtenerTodoAsync`; existentes: `ISocioNegocioApiClient.ObtenerTodoAsync(string? tipo)` / `ObtenerContenganNombreAsync(string nombre, string? tipo)` / `ObtenerAsync(string codigo)`, `INumeracionDocumentoDetApiClient.ObtenerPorDocumentoAsync(string)` (`NumeracionDocumentoDetDTO.Serie/NombreSerie/Manual/Bloqueado/SubTipoDoc`), `INumeracionDocumentoApiClient.ObtenerTodoAsync()` (`CodigoObj`, `SubTipoDoc`, `SerieDfct`), `App.autocompletar`.
- Produces: `GET /PagosRecibidos/Crear?codigoSn=&facturaEntry=` (página), `GET /PagosRecibidos/BuscarSocios?texto=`, `GET /PagosRecibidos/DocumentosPorCobrar?codigoSn=`, `POST /PagosRecibidos/Crear` (JSON `PagoRecibidoCrearDTO`).

- [ ] **Step 1: Controlador** — en `PagosRecibidosController.cs`: (a) agregar `using System.Text.Json;`, `using Web.ApiClient.Dtos.PagoRecibido;`; (b) cambiar el constructor y los campos a:

```csharp
        private const string CodigoObjPago = "24";
        private const string SubTipoDocPago = "--";
        private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

        private readonly IPagoRecibidoApiClient _pagos;
        private readonly IFormaPagoApiClient _formas;
        private readonly ISocioNegocioApiClient _socios;
        private readonly INumeracionDocumentoDetApiClient _series;
        private readonly INumeracionDocumentoApiClient _numeracion;

        public PagosRecibidosController(
            IPagoRecibidoApiClient pagos,
            IFormaPagoApiClient formas,
            ISocioNegocioApiClient socios,
            INumeracionDocumentoDetApiClient series,
            INumeracionDocumentoApiClient numeracion)
        {
            _pagos = pagos;
            _formas = formas;
            _socios = socios;
            _series = series;
            _numeracion = numeracion;
        }
```

(c) agregar estas acciones (antes de `Detalle`):

```csharp
        // Página de alta. codigoSn + facturaEntry (opcionales) llegan desde el botón "Registrar pago" de los listados:
        // precargan el cliente y marcan ese documento por su saldo completo. El servidor revalida todo al guardar.
        [HttpGet]
        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        public async Task<IActionResult> Crear(string? codigoSn = null, int? facturaEntry = null)
        {
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjPago);
            ViewBag.Series = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocPago && s.Bloqueado != "S").ToList();

            // Serie preseleccionada: la configurada como "por defecto" en "Numeración de documentos".
            var numeraciones = await _numeracion.ObtenerTodoAsync();
            ViewBag.SerieDefecto = (numeraciones.Dato ?? [])
                .FirstOrDefault(n => n.CodigoObj == CodigoObjPago && n.SubTipoDoc == SubTipoDocPago)?.SerieDfct;

            var formas = await _formas.ObtenerTodoAsync();
            var formasActivas = (formas.Dato ?? [])
                .Where(f => f.Activo == "S")
                .OrderBy(f => f.Nombre)
                .Select(f => new { codigo = f.Codigo, nombre = f.Nombre, tipoDato = f.TipoDato })
                .ToList();

            string? nombreSn = null;
            if (!string.IsNullOrWhiteSpace(codigoSn))
            {
                var socio = await _socios.ObtenerAsync(codigoSn);
                nombreSn = socio.Dato?.Nombre;
            }

            ViewBag.DatosInicio = JsonSerializer.Serialize(new { formas = formasActivas, codigoSn, nombreSn, facturaEntry }, JsonWeb)
                .Replace("</", "<\\/");
            return View();
        }

        [HttpGet]
        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync("C")
                : await _socios.ObtenerContenganNombreAsync(texto, "C");
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        public async Task<IActionResult> DocumentosPorCobrar(string codigoSn)
        {
            var respuesta = await _pagos.ObtenerDocumentosPorCobrarAsync(codigoSn);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("PagosRecibidos", Accion.Agregar)]
        public async Task<IActionResult> Crear([FromBody] PagoRecibidoCrearDTO dto)
        {
            var respuesta = await _pagos.InsertarAsync(dto);
            return Json(respuesta);
        }
```

- [ ] **Step 2: Vista** — `Web.UI/Views/PagosRecibidos/Crear.cshtml`:

```cshtml
@using Web.ApiClient.Dtos.NumeracionDocumentoDet
@{
    ViewData["Title"] = "Nuevo pago recibido";
    var series = (IEnumerable<NumeracionDocumentoDetDTO>)ViewBag.Series;
    int? serieDefecto = ViewBag.SerieDefecto;
}

<nav class="small text-muted mb-2">
    <a asp-controller="PagosRecibidos" asp-action="Index">Pagos recibidos</a>
    <i class="fa-solid fa-chevron-right mx-1" style="font-size:.65rem"></i>
    <span>Nuevo pago</span>
</nav>

<div class="d-flex align-items-start justify-content-between flex-wrap gap-3 mb-3">
    <div>
        <h1 class="h4 mb-1">Nuevo pago recibido</h1>
        <p class="text-muted small mb-0">Elige el cliente, indica cuánto pagas a cada documento y con qué formas de pago. El total aplicado debe ser igual al total de las formas de pago.</p>
    </div>
    <div class="d-flex gap-2">
        <a asp-controller="PagosRecibidos" asp-action="Index" class="btn btn-outline-secondary btn-sm">Cancelar</a>
        <button type="button" class="btn btn-primary btn-sm" id="btnGuardarPago" disabled>
            <i class="fa-solid fa-check me-1"></i>Guardar pago
        </button>
    </div>
</div>

<div class="card mb-3">
    <div class="card-body">
        <h2 class="h6 mb-3">Datos del pago</h2>
        <div class="row g-3">
            <div class="col-md-3">
                <label for="Serie" class="form-label">Serie <span class="text-danger">*</span></label>
                <select id="Serie" class="form-select">
                    @foreach (var s in series)
                    {
                        <option value="@s.Serie" data-manual="@s.Manual" selected="@(serieDefecto == s.Serie)">@s.NombreSerie</option>
                    }
                </select>
            </div>
            <div class="col-md-2 d-none" id="grupoNumDoc">
                <label for="NumDoc" class="form-label">No. de documento</label>
                <input id="NumDoc" type="number" min="1" class="form-control" />
            </div>
            <div class="col-md-3">
                <label for="FechaDoc" class="form-label">Fecha</label>
                <input id="FechaDoc" type="date" class="form-control" value="@DateTime.Today.ToString("yyyy-MM-dd")" />
            </div>
            <div class="col-md-6 position-relative">
                <label for="CodigoSnTexto" class="form-label">Cliente <span class="text-danger">*</span></label>
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar cliente por nombre..." autocomplete="off" />
                <input type="hidden" id="CodigoSn" />
                <div class="invalid-feedback d-none" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
            </div>
            <div class="col-12">
                <label for="Comentario" class="form-label">Comentario</label>
                <textarea id="Comentario" class="form-control" rows="2" maxlength="254"></textarea>
            </div>
        </div>
    </div>
</div>

<div class="card mb-3">
    <div class="card-body">
        <div class="d-flex justify-content-between align-items-center mb-2">
            <h2 class="h6 mb-0">Documentos del cliente con saldo</h2>
            <button type="button" class="btn btn-outline-secondary btn-sm" id="btnPagarTodo"><i class="fa-solid fa-list-check me-1"></i>Pagar todo</button>
        </div>
        <p class="text-muted small d-none" id="sinDocumentos"></p>
        <div class="table-responsive">
            <table class="table table-sm align-middle d-none" id="tblDocumentos">
                <thead>
                    <tr>
                        <th style="width:2.5rem"></th>
                        <th>Documento</th>
                        <th>Fecha</th>
                        <th class="text-end">Total</th>
                        <th class="text-end">Notas de crédito</th>
                        <th class="text-end">Pagado</th>
                        <th class="text-end">Saldo</th>
                        <th class="text-end">Monto a pagar</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="card mb-3">
    <div class="card-body">
        <div class="d-flex justify-content-between align-items-center mb-2">
            <h2 class="h6 mb-0">Formas de pago</h2>
            <button type="button" class="btn btn-outline-secondary btn-sm" id="btnAgregarMedio"><i class="fa-solid fa-plus me-1"></i>Agregar forma de pago</button>
        </div>
        <div class="table-responsive">
            <table class="table table-sm align-middle" id="tblMedios">
                <thead>
                    <tr>
                        <th>Forma</th>
                        <th class="text-end">Monto</th>
                        <th>Referencia</th>
                        <th>Banco</th>
                        <th>Fecha del cheque</th>
                        <th style="width:2.5rem"></th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="card">
    <div class="card-body d-flex flex-wrap gap-4 justify-content-end">
        <div>Total aplicado: <strong id="totalAplicado">0.00</strong></div>
        <div>Total formas de pago: <strong id="totalMedios">0.00</strong></div>
        <div>Diferencia: <strong id="diferencia">0.00</strong></div>
    </div>
</div>

<script id="datosPagoCrear" type="application/json">
    @Html.Raw((string)ViewBag.DatosInicio)
</script>

@section Scripts {
    <script src="~/js/pagosrecibidos-crear.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: JavaScript** — `Web.UI/wwwroot/js/pagosrecibidos-crear.js`:

```javascript
$(function () {
    const datos = JSON.parse(document.getElementById('datosPagoCrear').textContent);
    const formas = datos.formas || [];
    let facturaInicial = datos.facturaEntry ?? null;

    const r2 = n => Math.round((Number(n) + Number.EPSILON) * 100) / 100;
    const dinero = n => r2(n).toFixed(2);
    const escapar = v => $('<div>').text(v ?? '').html().replace(/"/g, '&quot;');
    const etiqueta = d => (d.docSubType === 'ND' ? 'Nota de débito ' : 'Factura ') + d.numDoc;

    let documentos = [];        // documentos con saldo del cliente elegido
    const aplicado = new Map(); // facturaEntry -> monto a pagar
    let medioAuto = true;       // mientras haya una sola forma y no se edite, su monto sigue al total aplicado
    let cargaId = 0;            // descarta respuestas de una carga anterior si el cliente cambió

    // ---- serie ----
    function actualizarNumeroManual() {
        const manual = $('#Serie option:selected').data('manual') === 'S';
        $('#grupoNumDoc').toggleClass('d-none', !manual);
        if (!manual) $('#NumDoc').val('');
    }
    $('#Serie').on('change', actualizarNumeroManual);
    actualizarNumeroManual();

    // ---- cliente ----
    const buscadorCliente = App.autocompletar({
        texto: $('#CodigoSnTexto'),
        oculto: $('#CodigoSn'),
        lista: $('#CodigoSnResultados'),
        error: $('#CodigoSnError'),
        endpoint: '/PagosRecibidos/BuscarSocios',
        obtenerCodigo: s => s.codigo ?? s.Codigo,
        obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
        onSeleccion: s => cargarDocumentos(s ? (s.codigo ?? s.Codigo) : null)
    });

    async function cargarDocumentos(codigoSn) {
        const id = ++cargaId;
        documentos = [];
        aplicado.clear();
        if (!codigoSn) {
            pintarDocumentos();
            recalcular();
            return;
        }
        let respuesta;
        try {
            respuesta = await $.get('/PagosRecibidos/DocumentosPorCobrar', { codigoSn });
        } catch (e) {
            respuesta = { resultado: false, mensaje: 'No se pudieron cargar los documentos del cliente.' };
        }
        if (id !== cargaId) return;
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
        } else {
            documentos = respuesta.dato || [];
            if (facturaInicial != null) {
                const d = documentos.find(x => x.entry === facturaInicial);
                if (d) aplicado.set(d.entry, r2(d.saldo));
            }
        }
        facturaInicial = null;
        pintarDocumentos();
        recalcular();
    }

    // ---- documentos por cobrar ----
    function pintarDocumentos() {
        const $tbody = $('#tblDocumentos tbody').empty();
        const hayCliente = !!$('#CodigoSn').val();
        if (documentos.length === 0) {
            $('#tblDocumentos').addClass('d-none');
            $('#sinDocumentos').removeClass('d-none')
                .text(hayCliente ? 'El cliente no tiene facturas ni notas de débito con saldo.' : 'Elige un cliente para ver sus documentos con saldo.');
            return;
        }
        $('#sinDocumentos').addClass('d-none');
        $('#tblDocumentos').removeClass('d-none');
        documentos.forEach(d => {
            const monto = aplicado.get(d.entry);
            const marcado = monto != null;
            $tbody.append(`
                <tr data-entry="${d.entry}">
                    <td class="text-center"><input type="checkbox" class="form-check-input chk-pagar"${marcado ? ' checked' : ''}></td>
                    <td>${escapar(etiqueta(d))}</td>
                    <td>${d.fechaDoc ? new Date(d.fechaDoc).toLocaleDateString() : ''}</td>
                    <td class="text-end">${dinero(d.total)}</td>
                    <td class="text-end">${dinero(d.acreditado)}</td>
                    <td class="text-end">${dinero(d.pagado)}</td>
                    <td class="text-end fw-semibold">${dinero(d.saldo)}</td>
                    <td style="width:9rem"><input type="number" class="form-control form-control-sm text-end inp-monto" min="0" max="${r2(d.saldo)}" step="0.01" value="${marcado ? r2(monto) : ''}"${marcado ? '' : ' disabled'}></td>
                </tr>`);
        });
    }

    function documentoDeFila($fila) {
        const entry = Number($fila.data('entry'));
        return documentos.find(x => x.entry === entry);
    }

    $('#tblDocumentos').on('change', '.chk-pagar', function () {
        const $fila = $(this).closest('tr');
        const d = documentoDeFila($fila);
        const $monto = $fila.find('.inp-monto');
        if (this.checked) {
            aplicado.set(d.entry, r2(d.saldo));
            $monto.prop('disabled', false).val(r2(d.saldo));
        } else {
            aplicado.delete(d.entry);
            $monto.prop('disabled', true).val('');
        }
        recalcular();
    });

    $('#tblDocumentos').on('input', '.inp-monto', function () {
        const d = documentoDeFila($(this).closest('tr'));
        let valor = parseFloat($(this).val());
        if (isNaN(valor) || valor < 0) valor = 0;
        if (valor > d.saldo) {           // nunca más que el saldo del documento
            valor = d.saldo;
            $(this).val(r2(valor));
        }
        aplicado.set(d.entry, r2(valor));
        recalcular();
    });

    $('#btnPagarTodo').on('click', function () {
        documentos.forEach(d => aplicado.set(d.entry, r2(d.saldo)));
        pintarDocumentos();
        recalcular();
    });

    // ---- formas de pago ----
    function opcionesFormas() {
        return formas.map(f => `<option value="${escapar(f.codigo)}" data-tipo="${escapar(f.tipoDato)}">${escapar(f.nombre)}</option>`).join('');
    }

    function ajustarCamposMedio($fila) {
        const tipo = $fila.find('.sel-forma option:selected').data('tipo');
        const usaReferencia = tipo === 'C' || tipo === 'T' || tipo === 'J';
        const usaBanco = tipo === 'C' || tipo === 'T';
        const usaFecha = tipo === 'C';
        const marcas = { C: 'Nº de cheque', T: 'Referencia', J: 'Autorización / últimos 4' };
        $fila.find('.inp-ref').prop('disabled', !usaReferencia).attr('placeholder', marcas[tipo] || '');
        $fila.find('.inp-banco').prop('disabled', !usaBanco).attr('placeholder', tipo === 'C' ? 'Banco' : (tipo === 'T' ? 'Banco (opcional)' : ''));
        $fila.find('.inp-fecha-cheque').prop('disabled', !usaFecha);
        if (!usaReferencia) $fila.find('.inp-ref').val('');
        if (!usaBanco) $fila.find('.inp-banco').val('');
        if (!usaFecha) $fila.find('.inp-fecha-cheque').val('');
    }

    function agregarMedio() {
        if (formas.length === 0) {
            App.mostrarError('No hay formas de pago activas. Crea una en "Formas de pago".');
            return;
        }
        if ($('#tblMedios tbody tr').length >= 1) medioAuto = false;
        const $fila = $(`
            <tr>
                <td><select class="form-select form-select-sm sel-forma">${opcionesFormas()}</select></td>
                <td style="width:9rem"><input type="number" class="form-control form-control-sm text-end inp-medio-monto" min="0" step="0.01"></td>
                <td><input type="text" class="form-control form-control-sm inp-ref" maxlength="50"></td>
                <td><input type="text" class="form-control form-control-sm inp-banco" maxlength="60"></td>
                <td style="width:10rem"><input type="date" class="form-control form-control-sm inp-fecha-cheque"></td>
                <td class="text-end"><button type="button" class="btn btn-sm btn-outline-danger btn-quitar-medio" title="Quitar"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`);
        $('#tblMedios tbody').append($fila);
        ajustarCamposMedio($fila);
        recalcular();
    }

    $('#btnAgregarMedio').on('click', agregarMedio);

    $('#tblMedios').on('change', '.sel-forma', function () {
        ajustarCamposMedio($(this).closest('tr'));
    });

    $('#tblMedios').on('input', '.inp-medio-monto', function () {
        medioAuto = false;
        recalcular();
    });

    $('#tblMedios').on('click', '.btn-quitar-medio', function () {
        $(this).closest('tr').remove();
        recalcular();
    });

    // ---- totales y cuadre ----
    function totalAplicado() {
        let total = 0;
        aplicado.forEach(v => { total += v; });
        return r2(total);
    }

    function totalMedios() {
        let total = 0;
        $('#tblMedios .inp-medio-monto').each(function () { total += parseFloat($(this).val()) || 0; });
        return r2(total);
    }

    function recalcular() {
        const aplicadoTotal = totalAplicado();
        const $filas = $('#tblMedios tbody tr');
        if (medioAuto && $filas.length === 1) {
            $filas.find('.inp-medio-monto').val(aplicadoTotal > 0 ? aplicadoTotal : '');
        }
        const mediosTotal = totalMedios();
        const diferencia = r2(aplicadoTotal - mediosTotal);
        $('#totalAplicado').text(dinero(aplicadoTotal));
        $('#totalMedios').text(dinero(mediosTotal));
        $('#diferencia').text(dinero(diferencia))
            .toggleClass('text-success', diferencia === 0 && aplicadoTotal > 0)
            .toggleClass('text-danger', diferencia !== 0);
        $('#btnGuardarPago').prop('disabled', !(aplicadoTotal > 0 && diferencia === 0 && $('#CodigoSn').val()));
    }

    // ---- guardar ----
    $('#btnGuardarPago').on('click', async function () {
        const $boton = $(this);
        const manual = $('#Serie option:selected').data('manual') === 'S';

        const facturas = [...aplicado.entries()]
            .filter(([, monto]) => monto > 0)
            .map(([entry, monto]) => ({ FacturaEntry: entry, MontoAplicado: monto }));

        const medios = [];
        $('#tblMedios tbody tr').each(function () {
            const $fila = $(this);
            const monto = parseFloat($fila.find('.inp-medio-monto').val());
            if (!(monto > 0)) return;
            medios.push({
                CodFormaPago: $fila.find('.sel-forma').val(),
                Monto: r2(monto),
                Referencia: ($fila.find('.inp-ref').val() || '').trim() || null,
                Banco: ($fila.find('.inp-banco').val() || '').trim() || null,
                FechaCheque: $fila.find('.inp-fecha-cheque').val() || null
            });
        });

        const pago = {
            Serie: Number($('#Serie').val()),
            NumDoc: manual ? Number($('#NumDoc').val() || 0) : 0,
            FechaDoc: $('#FechaDoc').val() || null,
            CodigoSn: $('#CodigoSn').val(),
            Comentario: ($('#Comentario').val() || '').trim() || null,
            Facturas: facturas,
            Medios: medios
        };

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.enviarJson('/PagosRecibidos/Crear', 'POST', pago);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            await App.mostrarExito('Pago registrado correctamente.');
            window.location.href = '/PagosRecibidos';
        } finally {
            recalcular();
        }
    });

    // ---- arranque ----
    agregarMedio();
    if (datos.codigoSn) {
        buscadorCliente.establecer({ codigo: datos.codigoSn, nombre: datos.nombreSn || datos.codigoSn });
    } else {
        pintarDocumentos();
    }
    recalcular();
});
```

- [ ] **Step 4: Compilar y verificar sintaxis** — `dotnet build Web.slnx …` (0 errores) y `node --check Web.UI\wwwroot\js\pagosrecibidos-crear.js`.

- [ ] **Step 5: Commit** (Web)

```bash
git add Web.UI/Controllers/PagosRecibidosController.cs Web.UI/Views/PagosRecibidos/Crear.cshtml Web.UI/wwwroot/js/pagosrecibidos-crear.js
git commit -m "feat(pagos): pagina de alta de pagos recibidos con documentos, formas de pago y cuadre

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Saldo, estado de pago y botón "Registrar pago" en Facturas y Notas de débito

**Precondición:** Task 13 completa (Web); build en 0 errores.

**Files (Web):**
- Modify: `Web.UI/Controllers/FacturasController.cs`, `Web.UI/Controllers/NotasDebitoController.cs`
- Modify: `Web.UI/Views/Facturas/Index.cshtml`, `Web.UI/Views/NotasDebito/Index.cshtml`
- Modify: `Web.UI/wwwroot/js/facturas.js`, `Web.UI/wwwroot/js/notasdebito.js`

**Interfaces:**
- Consumes (Task 10): `ISaldoFacturaApiClient`, `SaldoFacturaDTO`, `FacturaDTO/NotaDebitoDTO.Pagado/Saldo/EstadoPago`; (Task 13) ruta `/PagosRecibidos/Crear?codigoSn=&facturaEntry=`.
- Produces: columnas **Pagado**, **Saldo** y **Pago** (insignia) en ambos listados; botón "Registrar pago" en las filas no canceladas con saldo > 0 para quien tenga Agregar en `PagosRecibidos`.

- [ ] **Step 1: Controladores** —

`FacturasController.cs` (conservar EOL): (a) `using Web.ApiClient.Dtos.SaldoFactura;` no es necesario (solo se usa el cliente); agregar el campo `private readonly ISaldoFacturaApiClient _saldos;`; (b) agregar `ISaldoFacturaApiClient saldos` como **último** parámetro del constructor y `_saldos = saldos;`; (c) reemplazar la acción `ObtenerTodos` por:

```csharp
        [HttpGet]
        [RequierePermiso("Facturas", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _facturas.ObtenerTodoAsync();
            if (respuesta.Resultado && respuesta.Dato is not null)
            {
                // Saldo y estado de pago de todo el listado en una sola llamada; si falla, el listado sale sin esas columnas.
                var lista = respuesta.Dato.ToList();
                var saldos = await _saldos.ObtenerSaldosFacturasAsync(lista.Select(f => f.Entry));
                if (saldos.Resultado && saldos.Dato is not null)
                {
                    var porEntry = saldos.Dato.ToDictionary(s => s.FacturaEntry);
                    foreach (var f in lista)
                    {
                        if (!porEntry.TryGetValue(f.Entry, out var s)) continue;
                        f.Pagado = s.Pagado;
                        f.Saldo = s.Saldo;
                        f.EstadoPago = s.Estado;
                    }
                }
                respuesta.Dato = lista;
            }
            return Json(respuesta);
        }
```

`NotasDebitoController.cs`: lo mismo con `_notas.ObtenerTodoAsync()` y `_saldos.ObtenerSaldosNotasDebitoAsync(...)` (campo `_saldos`, parámetro `ISaldoFacturaApiClient saldos` al final del constructor), módulo `"NotasDebito"`.

- [ ] **Step 2: Vistas** — en `Views/Facturas/Index.cshtml` y `Views/NotasDebito/Index.cshtml`: (a) en `<thead>`, después de `<th>Total</th>` agregar `<th class="text-end">Pagado</th><th class="text-end">Saldo</th><th>Pago</th>` (cada `<th>` en su línea, con la misma sangría); (b) en el `<script id="datosPermisos…" type="application/json">` agregar la propiedad `"pagar"`:

```cshtml
    { "actualizar": @(User.TienePermiso("Facturas", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("Facturas", Accion.Eliminar) ? "true" : "false"),
      "pagar": @(User.TienePermiso("PagosRecibidos", Accion.Agregar) ? "true" : "false") }
```

(en Notas de débito el módulo de las dos primeras es `"NotasDebito"`; `pagar` siempre es `PagosRecibidos`/`Agregar`).

- [ ] **Step 3: JavaScript** — en `wwwroot/js/facturas.js` y `wwwroot/js/notasdebito.js` (conservar EOL): (a) en `permisos` el valor por defecto cuando falta el JSON pasa a `{ actualizar: true, eliminar: true, pagar: false }`; (b) en el arreglo `columns`, **después** de la columna `{ data: 'totalDoc', … },` agregar:

```javascript
            { data: 'pagado', className: 'text-end', render: (d, t, row) => (row.cancelado === 'S' || d == null) ? '' : Number(d).toFixed(2) },
            { data: 'saldo', className: 'text-end', render: (d, t, row) => (row.cancelado === 'S' || d == null) ? '' : Number(d).toFixed(2) },
            {
                data: 'estadoPago',
                render: (d, t, row) => {
                    if (row.cancelado === 'S' || !d) return '';
                    if (t !== 'display') return d;
                    const clase = d === 'Pagada' ? 'success' : (d === 'Parcial' ? 'warning' : 'secondary');
                    return `<span class="badge text-bg-${clase}">${d}</span>`;
                }
            },
```

(c) en la columna de acciones (`data: 'entry', orderable: false, …`) cambiar `render: entry => \`` por `render: (entry, type, row) => \`` y, como **primer** elemento dentro de la plantilla, agregar:

```javascript
                    ${permisos.pagar && row.cancelado !== 'S' && row.saldo > 0 ? `<a class="btn btn-sm btn-outline-success" href="/PagosRecibidos/Crear?codigoSn=${encodeURIComponent(row.codigoSn || '')}&facturaEntry=${entry}" title="Registrar pago"><i class="fa-solid fa-hand-holding-dollar"></i></a>` : ''}
```

Comprobar que ningún otro código del archivo dependa de la posición de las columnas (`tabla.column(n)`, `order`); si lo hay, ajustar los índices.

- [ ] **Step 4: Compilar y verificar sintaxis** — `dotnet build Web.slnx …` (0 errores) y `node --check` de `facturas.js` y `notasdebito.js`.

- [ ] **Step 5: Commit** (Web)

```bash
git add Web.UI/Controllers/FacturasController.cs Web.UI/Controllers/NotasDebitoController.cs Web.UI/Views/Facturas/Index.cshtml Web.UI/Views/NotasDebito/Index.cshtml Web.UI/wwwroot/js/facturas.js Web.UI/wwwroot/js/notasdebito.js
git commit -m "feat(pagos): saldo, estado de pago y boton Registrar pago en facturas y notas de debito

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 5 — Verificación integral

### Task 15: Prueba de humo contra SQL Server real y recorrido en navegador

**Precondición:** Tasks 1–14 completas; `git log --oneline` muestra los commits de cada tarea en ambos repos; `dotnet test API.sln` verde (Task 9) y `dotnet build Web.slnx` con 0 errores.

**Files:** ninguno del repositorio (solo scripts y bases temporales en el scratchpad). Si la verificación descubre un defecto, se corrige con su prueba (TDD) en un commit aparte y se anota en el reporte.

**Interfaces:** consume todo lo anterior. Modelo de scripts en el scratchpad (`C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad`): `smoke-taller.ps1` (helpers `Api`, `Serie`, `Existencia`, `Linea`, `Cab`, `Esperar`), `t17-up.ps1` (levanta base + API + Web) y `t17-seed.ps1` (login y datos). Reutilizar sus funciones copiándolas, no modificando los originales.

- [ ] **Step 1: Levantar el entorno temporal.** Copiar `t17-up.ps1` a `pagos-up.ps1` en el scratchpad con estos cambios: base `API_DB_PAGOS_C`; **quitar** la línea `pwsh -File "$SP\t17-seed.ps1" …` (los datos los crea el smoke); salidas a `api-pagos.log/.err`, `web-pagos.log/.err`; `t17-pids.txt` → `pagos-pids.txt`. Lanzar API (`http://localhost:5199`, variable `ConnectionStrings__API_DB` con la base temporal, `-WorkingDirectory API.Service.WebApi`) y Web (`http://localhost:5200`, `ApiSettings__BaseUrl=http://localhost:5199`, `-WorkingDirectory Web.UI`) con `Start-Process -WindowStyle Hidden` (sin bloquear la sesión; no ejecutar el script en primer plano si tarda: usar `run_in_background`). Esperar con bucles `Invoke-WebRequest … -SkipHttpErrorCheck` de hasta 30 s. Comprobar `login`: `POST api/Auth/login {usuario:'admin', contrasena:'Admin123!'}` → 200 (el usuario `admin` viene en el script SQL completo). Comprobar que el log de la API **no** menciona `API_DB_TEST`.

- [ ] **Step 2: Escribir y correr `smoke-pagos.ps1`** (en el scratchpad; contra `http://localhost:5199`; mismo formato `OK`/`FALLA` y código de salida que `smoke-taller.ps1`). Datos: cliente `CL-0001`; artículos de **servicio** (`ArticuloInventario='N'`, sin stock) para facturar sin inventario; serie de Factura (`Serie '6'`), de Nota de crédito (`'8'`), de Nota de débito (`'6' 'ND'`) y de Pago (`'24'`) obtenidas como en `smoke-taller.ps1` (`Serie <obj> <sub>`). Comprobaciones (cada una con su mensaje esperado literal):

  1. `login` 200; `GET api/PagoRecibido` sin token → 401.
  2. **Formas de pago:** `GET api/FormaPago` devuelve las 4 semillas (EFE, CHQ, TRF, TJT); `POST` de `DEP`/`Depósito`/`T` → 200; duplicado → 400 `Ya existe un registro con el código: DEP`; `PUT DEP` cambiando `TipoDato` a `J` antes de usarla → 200; `DELETE DEP` → 200 y queda `Activo='N'`.
  3. **Facturas de prueba:** F1 por 1000.00 y F2 por 500.00 del cliente (líneas de servicio, impuesto 0 o el que use el helper `Linea`; usar los totales reales que devuelva la API, no asumir el IVA) y F3 por 100.00. Los importes siguientes se expresan sobre los totales reales: usar variables, no literales.
  4. `POST api/Factura/Saldos [F1,F2,F3]` → las tres `Pendiente`, `Saldo == Total`, `Pagado == 0`.
  5. `GET api/PagoRecibido/FacturasAbiertas/CL-0001` → incluye F1, F2 y F3 con su saldo.
  6. **Pago parcial:** pagar 400 de F1 en `EFE` → 200 (Entry > 0); `Saldos` → F1 `Parcial`, `Pagado 400`, `Saldo = Total − 400`.
  7. **Pago mixto a dos documentos:** pagar el resto de F1 y 100 de F2 con `EFE` 300 + `CHQ` (resto, referencia `00123`, banco `Industrial`) → 200; `Saldos` → F1 `Pagada` (`Saldo 0`), F2 `Parcial`; `GET api/PagoRecibido/{id}/Facturas` y `/Medios` devuelven 2 y 2 filas con `nombreFormaPago` y `numDoc`.
  8. **Sobrepago:** pagar a F2 más que su saldo → 400 con `excede su saldo`.
  9. **Validaciones:** cheque sin referencia → 400 `La forma de pago Cheque requiere el número de cheque.`; cheque sin banco → 400 `…requiere el banco.`; total aplicado ≠ total de formas → 400 `…no coincide con el total de las formas de pago…`; documento repetido → 400; documento de otro cliente (crear `CL-0002` y una factura suya) → 400 `pertenece a otro socio de negocio.`; cliente proveedor → 400 `El socio de negocio debe ser un cliente.`; cada rechazo **no** avanza el consecutivo (el `SigNumero` de la serie 24 no cambia).
  10. **Guarda de cancelación:** cancelar F1 (`PUT api/Factura/{id}` con `{Cancelado:'S'}`) → 400 `El documento tiene pagos aplicados (Pago n, Pago m). Cancele primero el pago.`
  11. **Límite de la nota de crédito:** nota de crédito basada en F1 (pagada por completo) por cualquier monto > 0 → 400 que contiene `exceden lo que queda por acreditar`; nota de crédito basada en F2 por un monto ≤ `Total − Pagado` → 200 y `Saldos` de F2 baja en ese monto; una nota de crédito sobre F3 (sin pagos) mayor que su total → 400 con el mensaje original `excede el total de la factura base`.
  12. **Cancelar un pago:** cancelar el pago mixto → 200 y `Saldos` de F1 y F2 recuperan lo aplicado (F1 `Parcial` con los 400 del primer pago); cancelar otra vez → 400 `El pago ya está cancelado.`; `GET api/PagoRecibido/{id}` muestra `cancelado='S'` con `fechaCancelado`.
  13. **Nota de débito cobrable:** crear una nota de débito de cliente por concepto (`POST api/NotaDebito`, como en `smoke-taller`/`smoke-copiar-a`) de 150.00 → aparece en `FacturasAbiertas`; pagarla completa → `POST api/NotaDebito/Saldos` la muestra `Pagada`; cancelarla con el pago activo → 400 con la guarda.
  14. **Concurrencia (transacción Serializable):** sobre F3 (saldo 100) lanzar **dos pagos simultáneos** de 100.00 (`ForEach-Object -Parallel -ThrottleLimit 2` con `Invoke-WebRequest`, o dos `Start-ThreadJob`) → exactamente **uno** responde 200 y el otro 400 con `excede su saldo`; `Saldos` de F3 `Pagada` y un solo pago activo.
  15. **Cierre del ciclo:** cancelar el pago ganador de F3, cancelar F3 (200 ahora que no tiene pagos activos) y comprobar que ya no aparece en `FacturasAbiertas`.
  16. **Cliente inactivo:** dar de baja a `CL-0001` (`PUT api/SocioNegocio/CL-0001` con `Activo='N'`) y registrar un pago sobre F2 → 200 (permitido); reactivarlo.
  17. **Taller:** orden de servicio → "Copiar a Factura" (`POST api/Factura` con líneas base 70, como en `smoke-taller.ps1`) → pagarla → `Saldos` `Pagada`.

  Esperado: todas en `OK`. Cualquier `FALLA` es un defecto a corregir con su prueba unitaria antes de seguir.

- [ ] **Step 3: Recorrido en el navegador** (herramientas del navegador integrado; Web en `http://localhost:5200`, sesión `admin`/`Admin123!`; pasar el recorrido con una ventana de al menos 1200 px de ancho). Anotar PASS/FALLA de cada punto:

  1. Menú Ventas muestra **Pagos recibidos** y **Formas de pago**.
  2. **Formas de pago:** listado con las 4 semillas; crear `DEP`; editar nombre; intentar cambiar el tipo de una forma ya usada en un pago (después del punto 7) → mensaje `No se puede cambiar el tipo de dato…`; desactivar `DEP`.
  3. **Facturas:** el listado muestra las columnas Pagado, Saldo y la insignia (Pendiente/Parcial/Pagada); las canceladas no muestran saldo; cada fila con saldo tiene el botón verde "Registrar pago".
  4. **Registrar pago desde la factura:** el botón abre `Pagos recibidos > Nuevo pago` con el cliente y la factura ya marcada por su saldo; la barra de cuadre muestra total aplicado = total de la forma de pago y **Guardar** habilitado.
  5. **Pago mixto:** quitar la marca, pagar solo una parte editando "Monto a pagar" (no puede superar el saldo: el campo se limita), agregar una segunda forma de pago (`Cheque`: aparecen y se exigen Referencia y Banco; `Efectivo` los deshabilita); mientras `Diferencia ≠ 0` Guardar está deshabilitado; al cuadrar se habilita; guardar → aviso de éxito y regreso al listado.
  6. **Listado de pagos:** aparece el pago como Vigente; el filtro "Cancelados" lo oculta; el botón de ojo abre el detalle con documentos aplicados y formas de pago (referencia/banco/fecha del cheque).
  7. **Cancelar el pago** desde el detalle (confirmación) → pasa a Cancelado y la factura vuelve a mostrar el saldo anterior.
  8. **Facturas con pagos:** el botón eliminar/cancelar de una factura con pagos activos muestra el mensaje `El documento tiene pagos aplicados…`.
  9. **Notas de débito:** el listado tiene las mismas columnas y el botón "Registrar pago".
  10. **Nuevo pago directo** (sin factura): elegir cliente con el buscador carga sus documentos; "Pagar todo" marca todos; cambiar de cliente limpia la tabla; sin documentos muestra el texto de ayuda.
  11. **Serie manual:** si existe una serie manual para el objeto 24, al elegirla aparece el campo "No. de documento"; con series automáticas no aparece.
  12. **Permisos:** con un usuario sin `PagosRecibidos` ni `FormasPago` el menú no muestra esas entradas y `/PagosRecibidos` responde acceso denegado; con `PagosRecibidos` Ver pero sin Agregar el listado no muestra "Nuevo pago" ni el botón "Registrar pago" en facturas (crear el usuario/plantilla con la API o la pantalla de Usuarios/Plantillas y borrarlos al final si se creó directamente en la base temporal).
  13. **XSS:** una forma de pago llamada `<img src=x onerror=alert(1)>` y un cliente con nombre similar se ven como texto en los listados y en el formulario (no se ejecuta).
  14. Sin errores en la consola del navegador (`read_console_messages`) durante el recorrido, salvo los 400 esperados de las validaciones.

- [ ] **Step 4: Limpieza.** Detener los procesos de `pagos-pids.txt` y los que escuchen en 5199/5200; eliminar `API_DB_PAGOS_C` (`ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE …`) y confirmar que `SELECT name FROM sys.databases WHERE name LIKE 'API_DB_PAGOS%'` no devuelve filas y que `API_DB_TEST` sigue intacta (no se tocó).

- [ ] **Step 5: Reporte.** Escribir el resultado (cada comprobación OK/FALLA con la evidencia, defectos encontrados y commits de corrección) en el reporte de la tarea. Esta tarea no crea commits salvo correcciones de defectos.

---

## Notas de despliegue (para quien integra, fuera de las tareas)

- Ejecutar `sql/2026-10-23-pagos-recibidos.sql` en `API_DB_TEST` (idempotente, UTF-8: `sqlcmd -f 65001`) y asignar los módulos `PagosRecibidos` y `FormasPago` a las plantillas de permisos. Para el cobro hace falta además Ver en `Facturas`/`NotasDebito` (listados con saldo) y en `SociosNegocio` (buscador de clientes); para que "Registrar pago" aparezca, Agregar en `PagosRecibidos`.
- No hay migración de datos: las facturas existentes quedan con saldo = total y estado Pendiente.
- Los saldos se calculan con tres consultas agrupadas por listado; si el listado de facturas creciera mucho, se puede materializar después sin cambiar la API.
- Riesgos aceptados (ver spec §6): un pago puede cancelarse sin restricción de fecha; sin anticipos, un cliente que pagó de más no tiene dónde registrarlo.
