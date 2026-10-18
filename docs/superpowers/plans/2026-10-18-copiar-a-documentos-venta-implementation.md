# Copiar a… en la cadena de ventas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Crear un Pedido, una Entrega o una Factura de venta a partir de un documento origen de la cadena de ventas (Cotización → Pedido/Entrega/Factura, Pedido → Entrega/Factura, Entrega → Factura) con un botón "Copiar a", copia parcial por cantidad abierta, validación en el servidor y bloqueo de cancelar/eliminar/editar un origen que ya tiene derivados, igual que el "Copy To" de SAP B1.

**Architecture:** Un servicio de dominio `DocumentoBaseService` (compartido por los tres destinos) calcula la cantidad abierta en vivo, arma el borrador del destino y valida las líneas copiadas antes de numerar; los dominios destino lo invocan y `FacturaDomain` cambia su regla de inventario a `BaseTipo != 5`. Los dominios origen y de detalle lo usan para bloquear cambios sobre lo ya copiado. Un endpoint `GET api/DocumentoBase/{tipoOrigen}/{entry}/Abierto?destino=` entrega el borrador; la Web agrega el botón "Copiar a" en los listados de origen y abre el formulario del destino precargado. Sin cambios de esquema.

**Tech Stack:** .NET 7 API (N-capas, EF Core 7 DB-first, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server (solo bases temporales para la prueba de humo), bash/node para parches.

**Spec:** `docs/superpowers/specs/2026-10-18-copiar-a-documentos-venta-design.md` (repo API, commit `94ce3e0`). Referencia de patrón: `docs/superpowers/specs/2026-09-13-documentos-base-compras-design.md` (mismo mecanismo para compras, sin implementar).

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. Verificar antes de empezar: `git log --oneline -3` debe mostrar `94ce3e0` (spec) o posterior; `git status --short` solo con `API.Service.WebApi/appsettings.json` modificado. Línea base de pruebas: **1443** pasando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `1b1ff25` (o posterior). `git status --short` muestra **4 archivos ajenos** sin commit que NO se tocan ni se stagean: `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`. Línea base: `dotnet build Web.slnx` con 0 errores.
- Build/test con `BaseOutputPath` en el scratchpad (el API de desarrollo del usuario puede estar corriendo y bloquear `bin\`; ejecutar **desde PowerShell**, en bash la barra final rompe el quoting):
  - API: `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`
  - Convención de abreviatura: en los comandos del plan, `-p:BaseOutputPath="…apitest\"` y `…webbuild` significan la ruta completa del scratchpad de arriba, y `dotnet test … --filter "…"` significa `dotnet test API.Service.WebApi.TestsAPI.Service.WebApi.Tests.csproj -p:BaseOutputPath="<ruta>apitest\" --filter "…"`.
  - Para una sola clase de pruebas: `dotnet test API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="…\apitest\" --filter "FullyQualifiedName~NombreDeClase"`.
- SQL de pruebas: SOLO bases temporales (`API_DB_COPIA_A`, `API_DB_COPIA_B`); **jamás** `API_DB_TEST`. La contraseña de `sa` se extrae de `API.Service.WebApi\appsettings.json` (`[regex]::Match($cfg,'Password=([^;"]*)')`) a una variable de proceso y no se imprime ni se escribe en archivos. Toda base temporal se elimina al terminar (`ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE …`).

## Global Constraints

Cada tarea las incluye implícitamente.

- **Tipos de objeto de venta:** Cotización `3`, Pedido `4`, Entrega `5`, Factura `6` (solo filas `DocSubType = '--'`; las notas de débito `'ND'` no participan).
- **Combinaciones válidas (origen → destino):** 3→4, 3→5, 3→6, 4→5, 4→6, 5→6. Cualquier otra se rechaza con `No se puede copiar {Origen} a {Destino}.`
- **Esquema (sin DDL):** `CHECK` de `BaseTipo` en detalle: `PedidoDetalle {-1,3}`, `EntregaDetalle {-1,3,4}`, `FacturaDetalle {-1,3,4,5}`, `CotizacionDetalle {-1}`. Las cabeceras `Pedido`/`Entrega`/`Factura` solo admiten `BaseTipo = -1`: el servidor la fija siempre en `-1` y `BaseEntry = null`. `TipoDocDestino`/`DocDestinoEntry` no se usan.
- **Documento activo:** `Cancelado != 'S'` **y** `EstadoDoc != 'C'`. **Cantidad abierta** de una línea origen = `Cantidad` − Σ `Cantidad` de las líneas de documentos destino **activos** con `BaseTipo = tipoOrigen`, `BaseEntry = entryOrigen`, `BaseLinea = noLinea`; se calcula siempre en vivo (sin contadores guardados).
- **Validación de línea con base** (en `ValidarLineasAsync`, antes de numerar y antes de abrir la transacción): combinación válida; origen existente y activo; mismo socio que el destino; línea origen existente; mismo `CodArticulo`; `Cantidad > 0`; Σ cantidades por línea origen ≤ abierta. `Precio`, `PrctjeDesc`, `CodigoImpuesto` y `CodAlmacen` quedan editables. Un error rechaza el documento completo y no consume el consecutivo.
- **Inventario:** Cotización y Pedido nunca mueven; Entrega descuenta todas sus líneas con cantidad > 0 (sin cambio); Factura descuenta las líneas con cantidad > 0 y `BaseTipo != 5` (reemplaza la condición `BaseEntry == null`).
- **Integridad del origen:** con ≥ 1 línea con destinos activos, un origen (Cotización/Pedido/Entrega) no se puede eliminar ni cancelar (`Cancelado='S'` / `EstadoDoc='C'`), no puede cambiar de `CodigoSn`, y sus líneas consumidas no se pueden modificar ni eliminar (agregar líneas nuevas sí).
- **Mensajes en español exactamente como aparecen** en los bloques de código del plan (las pruebas los verifican literalmente). `Etiqueta` de un documento = `"{Nombre} {NumDoc}"` (ej. `Cotización 12`); lista de etiquetas = `"A"`, `"A y B"`, `"A, B y C"`.
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` puede aparecer modificado: NUNCA commitearlo. Los 4 archivos ajenos de la Web (arriba) NUNCA se stagean ni se modifican. **Sin `git push`** (el usuario lo aprueba aparte).
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (CRLF/LF); los archivos nuevos en LF; sin churn de EOL (`git diff --stat` de una edición pequeña no puede mostrar cientos de líneas). Los comentarios y mensajes siguen el estilo del repo (español).
- **Verificación al cerrar cada tarea de la API:** `dotnet build` sin errores y las pruebas de la tarea en verde; la suite completa (1443 + las nuevas) se corre en la Tarea 10.

## Ajustes respecto al spec (decididos al planificar, con el código leído)

1. **`Impuesto` en la línea del borrador.** `LineaAbiertaVenta` (nombre propio: ya existe un `LineaAbierta` del lado de compras en `API.Domain.Interface/DocumentoAbierto.cs`) y `DocumentoBaseLineaDTO` incluyen `Impuesto` (monto en la cantidad del origen) para que el formulario calcule el impuesto de la cantidad abierta (`impuesto × abierta ÷ cantidadOrigen`) sin conocer la tasa. El spec no lo listaba.
2. **`EntregaDetalleDomain` no se modifica.** Sus métodos `ActualizarAsync`/`EliminarAsync` ya lanzan siempre (las líneas de una entrega no se tocan después de crearla): es una regla más estricta que la del spec. La integridad de líneas se agrega solo en Cotización y Pedido.
3. **Cotización y Pedido se "cancelan" editando `EstadoDoc` a `'C'`** (no tienen acción de cancelar); `ValidarOrigenModificableAsync` se invoca en `ActualizarAsync` cuando el estado pasa a `'C'` (o `Cancelado` a `'S'`) o cambia `CodigoSn`, y en `EliminarAsync`.
4. **Punto de entrada en la Web:** el botón "Copiar a" navega al listado del destino con `?baseTipo=&baseEntry=` y ese listado abre su formulario precargado (`App.abrirCopiaSiCorresponde`). Es la misma experiencia que "abre el formulario del destino" sin incrustar los tres modales en cada listado.
5. **Moneda bloqueada** en el formulario copiado con `pointer-events: none` (un `select` deshabilitado no viajaría en el formulario); socio, nombre y dirección con `readonly`.

## File Structure

**API — dominio**
- Crear `API.Domain.Interface/IDocumentoBaseService.cs` (constantes `TipoDocumentoVenta`, records, `LineasBase`, interfaz).
- Crear `API.Domain.Core/DocumentoBaseService.cs`.
- Modificar `API.Domain.Core/FacturaDomain.cs`, `EntregaDomain.cs`, `PedidoDomain.cs` (destinos) y `CotizacionDomain.cs`, `PedidoDomain.cs`, `EntregaDomain.cs`, `CotizacionDetalleDomain.cs`, `PedidoDetalleDomain.cs`, `EntregaDetalleDomain.cs` (integridad del origen); `API.Domain.Interface/IPedidoDomain.cs`.

**API — aplicación / servicio web**
- Crear `API.Application.DTO/documentoBase/DocumentoBaseBorradorDTO.cs`, `API.Application.Interface/IDocumentoBaseApplication.cs`, `API.Application.Main/DocumentoBaseApplication.cs`, `API.Service.WebApi/Controllers/DocumentoBaseController.cs`.
- Modificar `API.Application.DTO/pedido/PedidoCrearDTO.cs` (`Lineas` opcional), `API.Application.Main/PedidoApplication.cs`, `API.Service.WebApi/Startup.cs` (DI).

**API — pruebas** (`API.Service.WebApi.Tests`)
- Crear `Domain/DocumentoBaseContratosTests.cs`, `Domain/DocumentoBaseServiceTests.cs`, `Domain/CotizacionDetalleDomainTests.cs`, `Domain/PedidoDetalleDomainTests.cs`, `Application/DocumentoBaseApplicationTests.cs`, `Controllers/DocumentoBaseControllerTests.cs`.
- Modificar `Domain/FacturaDomainTests.cs`, `EntregaDomainTests.cs`, `PedidoDomainTests.cs`, `CotizacionDomainTests.cs`, `EntregaDetalleDomainTests.cs` (constructores y casos nuevos).

**Web** (`Web.ApiClient`, `Web.UI`)
- Crear `Web.ApiClient/Dtos/DocumentoBase/DocumentoBaseBorradorDTO.cs`, `Web.ApiClient/Clientes/IDocumentoBaseApiClient.cs`, `DocumentoBaseApiClient.cs`.
- Modificar `Web.ApiClient/Dtos/Pedido/PedidoCrearDTO.cs`, `Web.UI/Program.cs`, `Web.UI/wwwroot/js/site.js`, `Controllers/PedidosController.cs`, `EntregasController.cs`, `FacturasController.cs`, `Views/{Pedidos,Entregas,Facturas}/_Form.cshtml`, `Views/{Cotizaciones,Pedidos,Entregas}/Index.cshtml`, `wwwroot/js/{cotizaciones,pedidos,entregas,facturas}.js`.

---

## Fase 1 — Servicio de documento base (API)

### Task 1: Contratos del dominio (`TipoDocumentoVenta`, records, `LineasBase`, `IDocumentoBaseService`)

**Precondición:** `git log --oneline -1` en la API muestra `94ce3e0` o posterior; el repo compila: `dotnet build API.sln -p:BaseOutputPath="…\apitest\"` (PowerShell) → 0 errores.

**Files:**
- Create: `API.Domain.Interface/IDocumentoBaseService.cs`
- Test: `API.Service.WebApi.Tests/Domain/DocumentoBaseContratosTests.cs`

**Interfaces:**
- Produces (namespace `API.Domain.Interface`): `TipoDocumentoVenta` (constantes `Cotizacion=3, Pedido=4, Entrega=5, Factura=6`; `Nombre(int)`, `DestinosDe(int)`, `EsCombinacionValida(int,int)`); records `LineaBaseRequest`, `LineaAbiertaVenta`, `CabeceraBorrador`, `BorradorDocumentoBase`, `DocumentoDerivado`; `DocumentoBaseMensajes.OrigenNoExiste`; `LineasBase.Extraer(...)`; `IDocumentoBaseService`. Las tareas 2–9 usan estos nombres exactos.

- [ ] **Step 1: Escribir las pruebas que fallan**

`API.Service.WebApi.Tests/Domain/DocumentoBaseContratosTests.cs`:

```csharp
using API.Domain.Interface;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class DocumentoBaseContratosTests
    {
        [Theory]
        [InlineData(3, 4, true)]
        [InlineData(3, 5, true)]
        [InlineData(3, 6, true)]
        [InlineData(4, 5, true)]
        [InlineData(4, 6, true)]
        [InlineData(5, 6, true)]
        [InlineData(4, 3, false)]
        [InlineData(5, 4, false)]
        [InlineData(6, 6, false)]
        [InlineData(6, 3, false)]
        [InlineData(3, 3, false)]
        [InlineData(9, 6, false)]
        public void EsCombinacionValida_SoloLaCadenaHaciaAdelante(int origen, int destino, bool esperado)
        {
            Assert.Equal(esperado, TipoDocumentoVenta.EsCombinacionValida(origen, destino));
        }

        [Fact]
        public void DestinosDe_Cotizacion_EsPedidoEntregaFactura()
        {
            Assert.Equal(new[] { 4, 5, 6 }, TipoDocumentoVenta.DestinosDe(TipoDocumentoVenta.Cotizacion));
            Assert.Empty(TipoDocumentoVenta.DestinosDe(TipoDocumentoVenta.Factura));
        }

        [Theory]
        [InlineData(3, "Cotización")]
        [InlineData(4, "Pedido")]
        [InlineData(5, "Entrega")]
        [InlineData(6, "Factura")]
        [InlineData(99, "Documento 99")]
        public void Nombre_DevuelveElNombreEnEspanol(int tipo, string esperado)
        {
            Assert.Equal(esperado, TipoDocumentoVenta.Nombre(tipo));
        }

        [Fact]
        public void DocumentoDerivado_Etiqueta_EsNombreYNumero()
        {
            Assert.Equal("Pedido 3", new DocumentoDerivado(4, 77, 3, 1).Etiqueta);
        }

        [Fact]
        public void Extraer_IgnoraLineasSinBase()
        {
            var lineas = new (int?, int?, int?, string?, decimal?)[]
            {
                (null, null, null, "A", 1m),
                (-1, null, null, "B", 2m),
                (0, 5, 1, "C", 3m),
            };

            Assert.Empty(LineasBase.Extraer(lineas));
        }

        [Fact]
        public void Extraer_ConvierteLasLineasConBase()
        {
            var lineas = new (int?, int?, int?, string?, decimal?)[]
            {
                (-1, null, null, "A", 1m),
                (3, 10, 2, "ART1", 4m),
                (4, 11, 1, "ART2", null),
            };

            var r = LineasBase.Extraer(lineas);

            Assert.Equal(2, r.Count);
            Assert.Equal(new LineaBaseRequest(3, 10, 2, "ART1", 4m), r[0]);
            Assert.Equal(new LineaBaseRequest(4, 11, 1, "ART2", 0m), r[1]);
        }

        [Theory]
        [InlineData(3, null, 1)]
        [InlineData(3, 10, null)]
        public void Extraer_BaseSinEntryOLinea_Lanza(int baseTipo, int? baseEntry, int? baseLinea)
        {
            var lineas = new (int?, int?, int?, string?, decimal?)[] { (baseTipo, baseEntry, baseLinea, "ART1", 1m) };

            var ex = Assert.Throws<Exception>(() => LineasBase.Extraer(lineas));
            Assert.Equal("La línea con documento base debe indicar BaseEntry y BaseLinea.", ex.Message);
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que falla (no compila)**

Run (PowerShell): `dotnet test API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="…\apitest\" --filter "FullyQualifiedName~DocumentoBaseContratosTests"`
Expected: error de compilación (`TipoDocumentoVenta`/`LineasBase` no existen).

- [ ] **Step 3: Implementar**

`API.Domain.Interface/IDocumentoBaseService.cs`:

```csharp
namespace API.Domain.Interface
{
    /// <summary>
    /// Tipos de objeto de la cadena de ventas y qué origen puede copiarse a qué destino. Los números coinciden con
    /// NumeracionDocumento.CodigoObj / TipoObjeto de cada tabla y con el BaseTipo de las líneas.
    /// </summary>
    public static class TipoDocumentoVenta
    {
        public const int Cotizacion = 3;
        public const int Pedido = 4;
        public const int Entrega = 5;
        public const int Factura = 6;

        public static string Nombre(int tipo) => tipo switch
        {
            Cotizacion => "Cotización",
            Pedido => "Pedido",
            Entrega => "Entrega",
            Factura => "Factura",
            _ => $"Documento {tipo}"
        };

        public static IReadOnlyList<int> DestinosDe(int origen) => origen switch
        {
            Cotizacion => new[] { Pedido, Entrega, Factura },
            Pedido => new[] { Entrega, Factura },
            Entrega => new[] { Factura },
            _ => Array.Empty<int>()
        };

        public static bool EsCombinacionValida(int origen, int destino) => DestinosDe(origen).Contains(destino);
    }

    public static class DocumentoBaseMensajes
    {
        public const string OrigenNoExiste = "El documento origen no existe.";
    }

    /// <summary>Línea de un documento destino que apunta a una línea de su documento origen.</summary>
    public sealed record LineaBaseRequest(int BaseTipo, int BaseEntry, int BaseLinea, string? CodArticulo, decimal Cantidad);

    /// <summary>Línea abierta de un origen, lista para cargarse en el formulario del destino.</summary>
    public sealed record LineaAbiertaVenta(
        int BaseTipo, int BaseEntry, int BaseLinea, string? CodArticulo, string? Descripcion,
        decimal CantidadOrigen, decimal CantidadAbierta, decimal? Precio, decimal? PrecioBruto, decimal? PrctjeDesc,
        string? CodigoImpuesto, decimal? Impuesto, string? CodAlmacen);

    public sealed record CabeceraBorrador(
        string? CodigoSn, string? NombreSn, string? Direccion, string? MonedaDoc,
        decimal? PrctjeDesc, decimal? PrctjeImpuesto, string? Comentario);

    public sealed record BorradorDocumentoBase(
        int OrigenTipo, int OrigenEntry, int OrigenNumDoc, CabeceraBorrador Cabecera, IReadOnlyList<LineaAbiertaVenta> Lineas);

    /// <summary>Documento destino activo que consume (en todo o en parte) un documento origen.</summary>
    public sealed record DocumentoDerivado(int Tipo, int Entry, int NumDoc, int BaseLinea)
    {
        public string Etiqueta => $"{TipoDocumentoVenta.Nombre(Tipo)} {NumDoc}";
    }

    public static class LineasBase
    {
        /// <summary>
        /// Toma las líneas de un destino que traen documento base (BaseTipo &gt; 0) y las convierte en solicitudes de
        /// validación. BaseTipo nulo o &lt;= 0 (-1 = ninguno) significa línea libre y se ignora.
        /// </summary>
        public static IReadOnlyList<LineaBaseRequest> Extraer(
            IEnumerable<(int? BaseTipo, int? BaseEntry, int? BaseLinea, string? CodArticulo, decimal? Cantidad)> lineas)
        {
            var resultado = new List<LineaBaseRequest>();
            foreach (var l in lineas)
            {
                if (l.BaseTipo is null or <= 0) continue;
                if (l.BaseEntry is null || l.BaseLinea is null)
                    throw new Exception("La línea con documento base debe indicar BaseEntry y BaseLinea.");
                resultado.Add(new LineaBaseRequest(l.BaseTipo.Value, l.BaseEntry.Value, l.BaseLinea.Value, l.CodArticulo, l.Cantidad ?? 0m));
            }
            return resultado;
        }
    }

    public interface IDocumentoBaseService
    {
        /// <summary>Borrador del destino con las líneas abiertas del origen. Lanza Exception con mensaje de usuario.</summary>
        Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);

        /// <summary>
        /// Valida las líneas con base de un destino que se va a crear (antes de numerar). No hace nada si no hay líneas.
        /// Lanza Exception con mensaje de usuario ante cualquier incumplimiento.
        /// </summary>
        Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas);

        /// <summary>Documentos destino activos que usan el origen (o solo una línea de él si se indica noLinea).</summary>
        Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea = null);

        /// <summary>Lanza si el origen tiene derivados activos. accion: "cancelar el documento", "eliminar el documento", etc.</summary>
        Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion);

        /// <summary>Lanza si la línea del origen ya fue copiada a un documento activo. accion: "modificar" o "eliminar".</summary>
        Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion);
    }
}
```

- [ ] **Step 4: Correr y comprobar que pasa**

Run: el mismo comando del Step 2.
Expected: PASS (23 casos).

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Interface/IDocumentoBaseService.cs API.Service.WebApi.Tests/Domain/DocumentoBaseContratosTests.cs
git commit -m "feat(documento-base): contratos del servicio y combinaciones validas de la cadena de ventas

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `DocumentoBaseService` — lectura de documentos y derivados

**Precondición:** Task 1 completa (`IDocumentoBaseService` compila). `grep -n "Impuesto" API.Domain.Entity/Models/{Cotizacion,Pedido,Entrega,Factura}Detalle.cs` muestra la propiedad `Impuesto` (decimal?) en las cuatro (los nombres de propiedades de las cuatro familias son idénticos; si alguna difiere, ajustar solo los métodos `Mapear` de esta tarea).

**Files:**
- Create: `API.Domain.Core/DocumentoBaseService.cs` (en esta tarea solo `ObtenerDerivadosAsync`; los demás métodos lanzan `NotImplementedException` y se reemplazan en las tareas 3 y 4)
- Test: `API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs`

**Interfaces:**
- Consumes: `TipoDocumentoVenta`, `DocumentoDerivado`, `IDocumentoBaseService` (Task 1); `IRepositorioGenerico<T,K>` (`ObtenerAsync(K)`, `ObtenerTodoAsync()`).
- Produces: clase `DocumentoBaseService` con constructor de 8 repositorios en este orden: `Cotizacion, CotizacionDetalle, Pedido, PedidoDetalle, Entrega, EntregaDetalle, Factura, FacturaDetalle`; helpers privados `CabeceraAsync`, `LineasDeAsync`, `ConsumosAsync`, `Mapear`, `Listar`, `Fmt` que usan las tareas 3 y 4.

- [ ] **Step 1: Escribir las pruebas que fallan**

`API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs` (esta clase se amplía en las tareas 3 y 4; aquí solo la base y los casos de derivados):

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
    public class DocumentoBaseServiceTests
    {
        private readonly Mock<IRepositorioGenerico<Cotizacion, int>> _cot = new();
        private readonly Mock<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>> _cotDet = new();
        private readonly Mock<IRepositorioGenerico<Pedido, int>> _ped = new();
        private readonly Mock<IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)>> _pedDet = new();
        private readonly Mock<IRepositorioGenerico<Entrega, int>> _ent = new();
        private readonly Mock<IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)>> _entDet = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _fac = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _facDet = new();

        private readonly List<Cotizacion> _cotizaciones = new();
        private readonly List<CotizacionDetalle> _cotLineas = new();
        private readonly List<Pedido> _pedidos = new();
        private readonly List<PedidoDetalle> _pedLineas = new();
        private readonly List<Entrega> _entregas = new();
        private readonly List<EntregaDetalle> _entLineas = new();
        private readonly List<Factura> _facturas = new();
        private readonly List<FacturaDetalle> _facLineas = new();

        private readonly DocumentoBaseService _svc;

        public DocumentoBaseServiceTests()
        {
            _cot.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _cotizaciones.FirstOrDefault(x => x.Entry == id)!);
            _ped.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _pedidos.FirstOrDefault(x => x.Entry == id)!);
            _ent.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _entregas.FirstOrDefault(x => x.Entry == id)!);
            _fac.Setup(r => r.ObtenerAsync(It.IsAny<int>())).ReturnsAsync((int id) => _facturas.FirstOrDefault(x => x.Entry == id)!);
            _cotDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _cotLineas.AsAsyncQueryable());
            _pedDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _pedLineas.AsAsyncQueryable());
            _entDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _entLineas.AsAsyncQueryable());
            _facDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _facLineas.AsAsyncQueryable());

            _svc = new DocumentoBaseService(_cot.Object, _cotDet.Object, _ped.Object, _pedDet.Object,
                _ent.Object, _entDet.Object, _fac.Object, _facDet.Object);
        }

        // ---------- fábricas de datos ----------

        private Cotizacion Cot(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var c = new Cotizacion { Entry = entry, NumDoc = numDoc, CodigoSn = sn, NombreSn = "Cliente", Direccion = "Dir", MonedaDoc = "QTZ",
                PrctjeDesc = 0m, PrctjeImpuesto = 12m, Comentario = "nota", EstadoDoc = estadoDoc, Cancelado = cancelado };
            _cotizaciones.Add(c);
            return c;
        }

        private void LineaCot(int entry, int noLinea, string art, decimal cant, decimal precio = 10m, string? alm = "01") =>
            _cotLineas.Add(new CotizacionDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Descripcion = "Desc " + art,
                Cantidad = cant, Precio = precio, PrecioBruto = precio, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = cant * precio * 0.12m, CodAlmacen = alm });

        private Pedido Ped(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var p = new Pedido { Entry = entry, NumDoc = numDoc, CodigoSn = sn, EstadoDoc = estadoDoc, Cancelado = cancelado };
            _pedidos.Add(p);
            return p;
        }

        private void LineaPed(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _pedLineas.Add(new PedidoDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private Entrega Ent(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N")
        {
            var e = new Entrega { Entry = entry, NumDoc = numDoc, CodigoSn = sn, EstadoDoc = estadoDoc, Cancelado = cancelado };
            _entregas.Add(e);
            return e;
        }

        private void LineaEnt(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _entLineas.Add(new EntregaDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        private Factura Fac(int entry, int numDoc, string sn = "CL-1", string? estadoDoc = "A", string? cancelado = "N", string subTipo = "--")
        {
            var f = new Factura { Entry = entry, NumDoc = numDoc, CodigoSn = sn, EstadoDoc = estadoDoc, Cancelado = cancelado, DocSubType = subTipo };
            _facturas.Add(f);
            return f;
        }

        private void LineaFac(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            _facLineas.Add(new FacturaDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
                BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });

        // ---------- ObtenerDerivadosAsync ----------

        [Fact]
        public async Task ObtenerDerivados_SinConsumidores_DevuelveVacio()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Empty(r);
        }

        [Fact]
        public async Task ObtenerDerivados_PedidoYFacturaActivos_LosListaOrdenadosPorTipo()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Fac(40, 2);
            LineaFac(40, 1, "ART1", 3m, baseTipo: 3, baseEntry: 1, baseLinea: 1);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, baseTipo: 3, baseEntry: 1, baseLinea: 1);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Equal(new[] { "Pedido 3", "Factura 2" }, r.Select(d => d.Etiqueta));
        }

        [Fact]
        public async Task ObtenerDerivados_IgnoraCanceladosYNotasDeDebito()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3, estadoDoc: "C");                          // pedido cancelado por EstadoDoc
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);
            Ent(31, 4, cancelado: "S");                          // entrega cancelada por Cancelado
            LineaEnt(31, 1, "ART1", 2m, 3, 1, 1);
            Fac(40, 9, subTipo: "ND");                           // nota de débito: no es factura
            LineaFac(40, 1, "ART1", 1m, 3, 1, 1);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Empty(r);
        }

        [Fact]
        public async Task ObtenerDerivados_PorLinea_SoloLosQueUsanEsaLinea()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            LineaCot(1, 2, "ART2", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);
            Ped(31, 5);
            LineaPed(31, 1, "ART2", 4m, 3, 1, 2);

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1, noLinea: 2);

            Assert.Equal(new[] { "Pedido 5" }, r.Select(d => d.Etiqueta));
        }

        [Fact]
        public async Task ObtenerDerivados_IgnoraLineasDeOtroOrigenYLasDeCantidadCero()
        {
            Cot(1, 12);
            Cot(2, 13);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 2, 1);   // base: otra cotización
            Ped(31, 4);
            LineaPed(31, 1, "ART1", 0m, 3, 1, 1);   // cantidad cero: no consume

            var r = await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Cotizacion, 1);

            Assert.Empty(r);
        }

        [Fact]
        public async Task ObtenerDerivados_OrigenFactura_NoTieneDestinos()
        {
            Fac(40, 2);

            Assert.Empty(await _svc.ObtenerDerivadosAsync(TipoDocumentoVenta.Factura, 40));
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que falla (no compila: `DocumentoBaseService` no existe)**

Run: `dotnet test API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="…\apitest\" --filter "FullyQualifiedName~DocumentoBaseServiceTests"`
Expected: error de compilación.

- [ ] **Step 3: Implementar**

`API.Domain.Core/DocumentoBaseService.cs`:

```csharp
using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// "Copiar a" de la cadena de ventas (Cotización → Pedido → Entrega → Factura), estilo SAP B1: cantidad abierta
    /// calculada en vivo, validación de las líneas copiadas e integridad del origen. Ver el spec
    /// 2026-10-18-copiar-a-documentos-venta-design.md.
    /// </summary>
    public class DocumentoBaseService : IDocumentoBaseService
    {
        private readonly IRepositorioGenerico<Cotizacion, int> _cotizacion;
        private readonly IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> _cotizacionDet;
        private readonly IRepositorioGenerico<Pedido, int> _pedido;
        private readonly IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> _pedidoDet;
        private readonly IRepositorioGenerico<Entrega, int> _entrega;
        private readonly IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> _entregaDet;
        private readonly IRepositorioGenerico<Factura, int> _factura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _facturaDet;

        public DocumentoBaseService(
            IRepositorioGenerico<Cotizacion, int> cotizacion,
            IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> cotizacionDet,
            IRepositorioGenerico<Pedido, int> pedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> pedidoDet,
            IRepositorioGenerico<Entrega, int> entrega,
            IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> entregaDet,
            IRepositorioGenerico<Factura, int> factura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> facturaDet)
        {
            _cotizacion = cotizacion;
            _cotizacionDet = cotizacionDet;
            _pedido = pedido;
            _pedidoDet = pedidoDet;
            _entrega = entrega;
            _entregaDet = entregaDet;
            _factura = factura;
            _facturaDet = facturaDet;
        }

        // ---------------------------------------------------------------------------------------------
        // Proyecciones comunes: las cuatro familias de tablas tienen las mismas columnas relevantes.
        // ---------------------------------------------------------------------------------------------

        private sealed record Cab(int Entry, int NumDoc, string? CodigoSn, string? NombreSn, string? Direccion, string? MonedaDoc,
            decimal? PrctjeDesc, decimal? PrctjeImpuesto, string? Comentario, string? Cancelado, string? EstadoDoc)
        {
            // Un documento está activo si no está cancelado ni por Cancelado ni por EstadoDoc.
            public bool Activo => Cancelado != "S" && EstadoDoc != "C";
            public string Etiqueta(int tipo) => $"{TipoDocumentoVenta.Nombre(tipo)} {NumDoc}";
        }

        private sealed record Lin(int Entry, int NoLinea, int? BaseTipo, int? BaseEntry, int? BaseLinea, string? CodArticulo, string? Descripcion,
            decimal? Cantidad, decimal? Precio, decimal? PrecioBruto, decimal? PrctjeDesc, string? CodigoImpuesto, decimal? Impuesto, string? CodAlmacen);

        private sealed record Consumo(int TipoDestino, int EntryDestino, int NumDocDestino, int BaseLinea, decimal Cantidad);

        private static Cab Mapear(Cotizacion h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);
        private static Cab Mapear(Pedido h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);
        private static Cab Mapear(Entrega h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);
        private static Cab Mapear(Factura h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc);

        private static Lin Mapear(CotizacionDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);
        private static Lin Mapear(PedidoDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);
        private static Lin Mapear(EntregaDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);
        private static Lin Mapear(FacturaDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen);

        // ---------------------------------------------------------------------------------------------
        // Lectura
        // ---------------------------------------------------------------------------------------------

        /// <summary>Cabecera del documento; null si no existe (o si una "factura" es en realidad una nota de débito).</summary>
        private async Task<Cab?> CabeceraAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoVenta.Cotizacion:
                    { var h = await _cotizacion.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoVenta.Pedido:
                    { var h = await _pedido.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoVenta.Entrega:
                    { var h = await _entrega.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoVenta.Factura:
                    { var h = await _factura.ObtenerAsync(entry); return h is null || h.DocSubType != "--" ? null : Mapear(h); }
                default:
                    return null;
            }
        }

        /// <summary>Líneas de un documento, ordenadas por NoLinea.</summary>
        private async Task<List<Lin>> LineasDeAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoVenta.Cotizacion:
                    return (await (await _cotizacionDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoVenta.Pedido:
                    return (await (await _pedidoDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoVenta.Entrega:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoVenta.Factura:
                    return (await (await _facturaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Líneas de las tablas destino cuyo BaseTipo/BaseEntry apuntan al origen indicado.</summary>
        private async Task<List<Lin>> LineasConBaseAsync(int tipoDestino, int tipoOrigen, int entryOrigen)
        {
            switch (tipoDestino)
            {
                case TipoDocumentoVenta.Pedido:
                    return (await (await _pedidoDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                case TipoDocumentoVenta.Entrega:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                case TipoDocumentoVenta.Factura:
                    return (await (await _facturaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Consumos (cantidad copiada por línea origen) de los documentos destino ACTIVOS.</summary>
        private async Task<List<Consumo>> ConsumosAsync(int tipoOrigen, int entry)
        {
            var consumos = new List<Consumo>();
            foreach (var destino in TipoDocumentoVenta.DestinosDe(tipoOrigen))
            {
                var lineas = await LineasConBaseAsync(destino, tipoOrigen, entry);
                foreach (var grupo in lineas.GroupBy(l => l.Entry))
                {
                    var cab = await CabeceraAsync(destino, grupo.Key);
                    if (cab is null || !cab.Activo) continue;
                    foreach (var l in grupo)
                        consumos.Add(new Consumo(destino, cab.Entry, cab.NumDoc, l.BaseLinea ?? 0, l.Cantidad ?? 0m));
                }
            }
            return consumos;
        }

        private static decimal Consumido(IEnumerable<Consumo> consumos, int noLinea) =>
            consumos.Where(c => c.BaseLinea == noLinea).Sum(c => c.Cantidad);

        private static string Fmt(decimal valor) => valor.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Listar(IEnumerable<string> partes)
        {
            var lista = partes.ToList();
            return lista.Count <= 1
                ? string.Concat(lista)
                : string.Join(", ", lista.Take(lista.Count - 1)) + " y " + lista[^1];
        }

        // ---------------------------------------------------------------------------------------------
        // API del servicio
        // ---------------------------------------------------------------------------------------------

        public async Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea = null)
        {
            var consumos = await ConsumosAsync(tipoOrigen, entry);
            return consumos
                .Where(c => c.Cantidad > 0m && (noLinea is null || c.BaseLinea == noLinea))
                .GroupBy(c => (c.TipoDestino, c.EntryDestino))
                .Select(g => new DocumentoDerivado(g.Key.TipoDestino, g.Key.EntryDestino, g.First().NumDocDestino, g.First().BaseLinea))
                .OrderBy(d => d.Tipo).ThenBy(d => d.Entry)
                .ToList();
        }

        public Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino) =>
            throw new NotImplementedException("Task 3");

        public Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas) =>
            throw new NotImplementedException("Task 3");

        public Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion) =>
            throw new NotImplementedException("Task 4");

        public Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion) =>
            throw new NotImplementedException("Task 4");
    }
}
```

- [ ] **Step 4: Correr y comprobar que pasa**

Run: el mismo comando del Step 2.
Expected: PASS (6 pruebas). Si alguna propiedad de entidad no existe (compilación), ajustar solo los `Mapear` y reportar cuál difería.

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/DocumentoBaseService.cs API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs
git commit -m "feat(documento-base): lectura de documentos y derivados activos por origen y por linea

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `DocumentoBaseService` — borrador y validación de líneas

**Precondición:** Task 2 completa y `DocumentoBaseServiceTests` (6 casos) en verde.

**Files:**
- Modify: `API.Domain.Core/DocumentoBaseService.cs` (reemplaza los dos `NotImplementedException` de `ObtenerBorradorAsync` y `ValidarLineasAsync`)
- Modify: `API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs` (agregar casos al final de la clase)

**Interfaces:**
- Consumes: helpers privados de la Task 2 (`CabeceraAsync`, `LineasDeAsync`, `ConsumosAsync`, `Consumido`, `Fmt`) y las fábricas de prueba (`Cot`, `LineaCot`, `Ped`, `LineaPed`, `Ent`, `LineaEnt`, `Fac`, `LineaFac`).
- Produces: `ObtenerBorradorAsync` / `ValidarLineasAsync` con los mensajes exactos de abajo.

- [ ] **Step 1: Agregar las pruebas que fallan** (dentro de la clase `DocumentoBaseServiceTests`, antes de la llave de cierre de la clase)

```csharp
        // ---------- ObtenerBorradorAsync ----------

        [Fact]
        public async Task Borrador_CopiaCabeceraYLineasAbiertas_ConCantidadAbierta()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m, precio: 25m);
            LineaCot(1, 2, "ART2", 5m, precio: 8m, alm: null);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, baseTipo: 3, baseEntry: 1, baseLinea: 1);   // consume 4 de las 10

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Factura);

            Assert.Equal((3, 1, 12), (b.OrigenTipo, b.OrigenEntry, b.OrigenNumDoc));
            Assert.Equal(("CL-1", "Cliente", "Dir", "QTZ", 12m, "nota"),
                (b.Cabecera.CodigoSn, b.Cabecera.NombreSn, b.Cabecera.Direccion, b.Cabecera.MonedaDoc, b.Cabecera.PrctjeImpuesto, b.Cabecera.Comentario));
            Assert.Equal(2, b.Lineas.Count);
            Assert.Equal((3, 1, 1, "ART1", 10m, 6m, 25m, "01"),
                (b.Lineas[0].BaseTipo, b.Lineas[0].BaseEntry, b.Lineas[0].BaseLinea, b.Lineas[0].CodArticulo,
                 b.Lineas[0].CantidadOrigen, b.Lineas[0].CantidadAbierta, b.Lineas[0].Precio, b.Lineas[0].CodAlmacen));
            Assert.Equal(("ART2", 5m, 5m), (b.Lineas[1].CodArticulo, b.Lineas[1].CantidadOrigen, b.Lineas[1].CantidadAbierta));
            Assert.Null(b.Lineas[1].CodAlmacen);
        }

        [Fact]
        public async Task Borrador_OmiteLasLineasAgotadas()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            LineaCot(1, 2, "ART2", 5m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 10m, 3, 1, 1);   // agota la línea 1

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Pedido);

            Assert.Equal(new[] { "ART2" }, b.Lineas.Select(l => l.CodArticulo));
        }

        [Fact]
        public async Task Borrador_UnDestinoCanceladoLiberaLaCantidad()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3, estadoDoc: "C");
            LineaPed(30, 1, "ART1", 10m, 3, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Entrega);

            Assert.Equal(10m, b.Lineas.Single().CantidadAbierta);
        }

        [Fact]
        public async Task Borrador_SumaLosConsumosDeVariosDestinos()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 2m, 3, 1, 1);
            Fac(40, 7);
            LineaFac(40, 1, "ART1", 3m, 3, 1, 1);

            var b = await _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Entrega);

            Assert.Equal(5m, b.Lineas.Single().CantidadAbierta);
        }

        [Theory]
        [InlineData(4, 3)]    // Pedido -> Cotización (hacia atrás)
        [InlineData(5, 4)]
        [InlineData(6, 6)]
        public async Task Borrador_CombinacionInvalida_Lanza(int origen, int destino)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(origen, 1, destino));
            Assert.Equal($"No se puede copiar {TipoDocumentoVenta.Nombre(origen)} a {TipoDocumentoVenta.Nombre(destino)}.", ex.Message);
        }

        [Fact]
        public async Task Borrador_OrigenInexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 99, TipoDocumentoVenta.Pedido));
            Assert.Equal(DocumentoBaseMensajes.OrigenNoExiste, ex.Message);
        }

        [Theory]
        [InlineData("C", "N")]
        [InlineData("A", "S")]
        public async Task Borrador_OrigenInactivo_Lanza(string estadoDoc, string cancelado)
        {
            Cot(1, 12, estadoDoc: estadoDoc, cancelado: cancelado);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Pedido));
            Assert.Equal("El documento origen está cancelado.", ex.Message);
        }

        [Fact]
        public async Task Borrador_SinLineasAbiertas_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 10m, 3, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() => _svc.ObtenerBorradorAsync(TipoDocumentoVenta.Cotizacion, 1, TipoDocumentoVenta.Pedido));
            Assert.Equal("El documento no tiene líneas abiertas.", ex.Message);
        }

        // ---------- ValidarLineasAsync ----------

        private static LineaBaseRequest Req(int baseTipo, int baseEntry, int baseLinea, string art, decimal cant) =>
            new(baseTipo, baseEntry, baseLinea, art, cant);

        [Fact]
        public async Task Validar_SinLineas_NoHaceNadaNiLeeNada()
        {
            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", Array.Empty<LineaBaseRequest>());

            _cot.Verify(r => r.ObtenerAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Validar_LineasCorrectasConCantidadParcial_Pasa()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", 10m) });
            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Entrega, "cl-1", new[] { Req(3, 1, 1, "ART1", 0.5m) });   // socio sin distinguir mayúsculas
        }

        [Fact]
        public async Task Validar_CombinacionInvalida_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(5, 1, 1, "ART1", 1m) }));
            Assert.Equal("No se puede copiar Entrega a Pedido.", ex.Message);
        }

        [Fact]
        public async Task Validar_OrigenInexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 99, 1, "ART1", 1m) }));
            Assert.Equal("El documento base Cotización 99 no existe.", ex.Message);
        }

        [Fact]
        public async Task Validar_OrigenCancelado_Lanza()
        {
            Cot(1, 12, estadoDoc: "C");
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", 1m) }));
            Assert.Equal("El documento base Cotización 12 está cancelado.", ex.Message);
        }

        [Fact]
        public async Task Validar_SocioDistinto_Lanza()
        {
            Cot(1, 12, sn: "CL-1");
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-2", new[] { Req(3, 1, 1, "ART1", 1m) }));
            Assert.Equal("El socio del documento debe ser el del documento base Cotización 12 (CL-1).", ex.Message);
        }

        [Fact]
        public async Task Validar_LineaOrigenInexistente_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 7, "ART1", 1m) }));
            Assert.Equal("La línea 7 de Cotización 12 no existe.", ex.Message);
        }

        [Fact]
        public async Task Validar_ArticuloDistinto_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "OTRO", 1m) }));
            Assert.Equal("El artículo de la línea 1 de Cotización 12 no se puede cambiar.", ex.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public async Task Validar_CantidadNoPositiva_Lanza(double cantidad)
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", (decimal)cantidad) }));
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 debe ser mayor que cero.", ex.Message);
        }

        [Fact]
        public async Task Validar_CantidadMayorQueLaAbierta_Lanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);   // abierta = 6

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Entrega, "CL-1", new[] { Req(3, 1, 1, "ART1", 6.5m) }));
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6).", ex.Message);
        }

        [Fact]
        public async Task Validar_VariasLineasContraLaMismaLineaOrigen_SeSuman()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1", new[] { Req(3, 1, 1, "ART1", 6m), Req(3, 1, 1, "ART1", 5m) }));
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (10).", ex.Message);
        }

        [Fact]
        public async Task Validar_DestinoCanceladoNoCuentaComoConsumo()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ent(31, 4, cancelado: "S");
            LineaEnt(31, 1, "ART1", 10m, 3, 1, 1);

            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(3, 1, 1, "ART1", 10m) });
        }

        [Fact]
        public async Task Validar_EntregaAFactura_ValidaContraLaEntrega()
        {
            Ent(31, 4);
            LineaEnt(31, 1, "ART1", 5m);

            await _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(5, 31, 1, "ART1", 5m) });

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1", new[] { Req(5, 31, 1, "ART1", 5.01m) }));
            Assert.Equal("La cantidad de la línea 1 de Entrega 4 excede la cantidad abierta (5).", ex.Message);
        }
```

- [ ] **Step 2: Correr y comprobar que fallan** (`NotImplementedException`)

Run: `dotnet test … --filter "FullyQualifiedName~DocumentoBaseServiceTests"` → FAIL en los casos nuevos.

- [ ] **Step 3: Implementar** — en `DocumentoBaseService.cs` reemplazar el bloque de los dos métodos `NotImplementedException("Task 3")` por:

```csharp
        public async Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)
        {
            if (!TipoDocumentoVenta.EsCombinacionValida(tipoOrigen, tipoDestino))
                throw new Exception($"No se puede copiar {TipoDocumentoVenta.Nombre(tipoOrigen)} a {TipoDocumentoVenta.Nombre(tipoDestino)}.");

            var cab = await CabeceraAsync(tipoOrigen, entry)
                ?? throw new Exception(DocumentoBaseMensajes.OrigenNoExiste);

            if (!cab.Activo)
                throw new Exception("El documento origen está cancelado.");

            var lineas = await LineasDeAsync(tipoOrigen, entry);
            var consumos = await ConsumosAsync(tipoOrigen, entry);

            var abiertas = new List<LineaAbiertaVenta>();
            foreach (var l in lineas)
            {
                var abierta = (l.Cantidad ?? 0m) - Consumido(consumos, l.NoLinea);
                if (abierta <= 0m) continue;
                abiertas.Add(new LineaAbiertaVenta(tipoOrigen, entry, l.NoLinea, l.CodArticulo, l.Descripcion,
                    l.Cantidad ?? 0m, abierta, l.Precio, l.PrecioBruto, l.PrctjeDesc, l.CodigoImpuesto, l.Impuesto, l.CodAlmacen));
            }

            if (abiertas.Count == 0)
                throw new Exception("El documento no tiene líneas abiertas.");

            return new BorradorDocumentoBase(tipoOrigen, entry, cab.NumDoc,
                new CabeceraBorrador(cab.CodigoSn, cab.NombreSn, cab.Direccion, cab.MonedaDoc, cab.PrctjeDesc, cab.PrctjeImpuesto, cab.Comentario),
                abiertas);
        }

        public async Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas)
        {
            foreach (var documento in lineas.GroupBy(l => (l.BaseTipo, l.BaseEntry)))
            {
                var (tipoOrigen, entryOrigen) = documento.Key;

                if (!TipoDocumentoVenta.EsCombinacionValida(tipoOrigen, tipoDestino))
                    throw new Exception($"No se puede copiar {TipoDocumentoVenta.Nombre(tipoOrigen)} a {TipoDocumentoVenta.Nombre(tipoDestino)}.");

                var cab = await CabeceraAsync(tipoOrigen, entryOrigen)
                    ?? throw new Exception($"El documento base {TipoDocumentoVenta.Nombre(tipoOrigen)} {entryOrigen} no existe.");
                var etiqueta = cab.Etiqueta(tipoOrigen);

                if (!cab.Activo)
                    throw new Exception($"El documento base {etiqueta} está cancelado.");

                if (!string.Equals(cab.CodigoSn, codigoSnDestino, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"El socio del documento debe ser el del documento base {etiqueta} ({cab.CodigoSn}).");

                var origenLineas = await LineasDeAsync(tipoOrigen, entryOrigen);
                var consumos = await ConsumosAsync(tipoOrigen, entryOrigen);

                foreach (var porLinea in documento.GroupBy(l => l.BaseLinea))
                {
                    var origen = origenLineas.FirstOrDefault(x => x.NoLinea == porLinea.Key)
                        ?? throw new Exception($"La línea {porLinea.Key} de {etiqueta} no existe.");

                    foreach (var l in porLinea)
                    {
                        if (!string.Equals(l.CodArticulo, origen.CodArticulo, StringComparison.Ordinal))
                            throw new Exception($"El artículo de la línea {porLinea.Key} de {etiqueta} no se puede cambiar.");
                        if (l.Cantidad <= 0m)
                            throw new Exception($"La cantidad de la línea {porLinea.Key} de {etiqueta} debe ser mayor que cero.");
                    }

                    var abierta = (origen.Cantidad ?? 0m) - Consumido(consumos, porLinea.Key);
                    if (porLinea.Sum(l => l.Cantidad) > abierta)
                        throw new Exception($"La cantidad de la línea {porLinea.Key} de {etiqueta} excede la cantidad abierta ({Fmt(abierta)}).");
                }
            }
        }
```

- [ ] **Step 4: Correr y comprobar que pasa**

Run: el comando del Step 2 → PASS (`DocumentoBaseServiceTests`: 6 de la Task 2 + 25 nuevos = 31).

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/DocumentoBaseService.cs API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs
git commit -m "feat(documento-base): borrador de lineas abiertas y validacion de lineas copiadas

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `DocumentoBaseService` — integridad del origen

**Precondición:** Task 3 completa (`DocumentoBaseServiceTests` en verde).

**Files:**
- Modify: `API.Domain.Core/DocumentoBaseService.cs` (reemplaza los dos `NotImplementedException("Task 4")`)
- Modify: `API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs` (casos al final de la clase)

**Interfaces:**
- Produces: `ValidarOrigenModificableAsync(tipoOrigen, entry, accion)` → mensaje `No se puede {accion}: lo usan {lista}. Cancele primero esos documentos.`; `ValidarLineaModificableAsync(tipoOrigen, entry, noLinea, accion)` → `No se puede {accion} la línea {noLinea}: la usan {lista}. Cancele primero esos documentos.`

- [ ] **Step 1: Agregar las pruebas que fallan** (al final de la clase)

```csharp
        // ---------- Integridad del origen ----------

        [Fact]
        public async Task OrigenModificable_SinDerivados_NoLanza()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);

            await _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cancelar el documento");
        }

        [Fact]
        public async Task OrigenModificable_ConDerivados_NombraLosDocumentos()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);
            Fac(40, 2);
            LineaFac(40, 1, "ART1", 3m, 3, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cancelar el documento"));
            Assert.Equal("No se puede cancelar el documento: lo usan Pedido 3 y Factura 2. Cancele primero esos documentos.", ex.Message);
        }

        [Fact]
        public async Task OrigenModificable_TresDerivados_UsaComasYY()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 1m, 3, 1, 1);
            Ent(31, 4);
            LineaEnt(31, 1, "ART1", 1m, 3, 1, 1);
            Fac(40, 2);
            LineaFac(40, 1, "ART1", 1m, 3, 1, 1);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "eliminar el documento"));
            Assert.Equal("No se puede eliminar el documento: lo usan Pedido 3, Entrega 4 y Factura 2. Cancele primero esos documentos.", ex.Message);
        }

        [Fact]
        public async Task OrigenModificable_DerivadoCancelado_NoBloquea()
        {
            Ent(31, 4);
            LineaEnt(31, 1, "ART1", 5m);
            Fac(40, 2, cancelado: "S");
            LineaFac(40, 1, "ART1", 5m, 5, 31, 1);

            await _svc.ValidarOrigenModificableAsync(TipoDocumentoVenta.Entrega, 31, "cancelar el documento");
        }

        [Fact]
        public async Task LineaModificable_SoloBloqueaLaLineaConsumida()
        {
            Cot(1, 12);
            LineaCot(1, 1, "ART1", 10m);
            LineaCot(1, 2, "ART2", 10m);
            Ped(30, 3);
            LineaPed(30, 1, "ART1", 4m, 3, 1, 1);

            await _svc.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 1, 2, "modificar");   // la 2 está libre

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _svc.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 1, 1, "eliminar"));
            Assert.Equal("No se puede eliminar la línea 1: la usan Pedido 3. Cancele primero esos documentos.", ex.Message);
        }
```

- [ ] **Step 2: Correr y comprobar que fallan** → FAIL (`NotImplementedException`).

- [ ] **Step 3: Implementar** — reemplazar los dos métodos `NotImplementedException("Task 4")` por:

```csharp
        public async Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion)
        {
            var derivados = await ObtenerDerivadosAsync(tipoOrigen, entry);
            if (derivados.Count == 0) return;

            throw new Exception($"No se puede {accion}: lo usan {Listar(derivados.Select(d => d.Etiqueta))}. Cancele primero esos documentos.");
        }

        public async Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion)
        {
            var derivados = await ObtenerDerivadosAsync(tipoOrigen, entry, noLinea);
            if (derivados.Count == 0) return;

            throw new Exception($"No se puede {accion} la línea {noLinea}: la usan {Listar(derivados.Select(d => d.Etiqueta))}. Cancele primero esos documentos.");
        }
```

- [ ] **Step 4: Correr y comprobar que pasa** → PASS (`DocumentoBaseServiceTests` completo: 6 + 25 + 5 = 36).

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/DocumentoBaseService.cs API.Service.WebApi.Tests/Domain/DocumentoBaseServiceTests.cs
git commit -m "feat(documento-base): bloqueo de cancelar, eliminar y editar lineas de un origen con derivados

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 2 — Endpoint y dominios destino (API)

### Task 5: Endpoint `GET api/DocumentoBase/{tipoOrigen}/{entry}/Abierto` (DTOs, aplicación, controlador, DI)

**Precondición:** Task 4 completa; `dotnet test … --filter "FullyQualifiedName~DocumentoBase"` → 59 pruebas en verde (23 + 36).

**Files:**
- Create: `API.Application.DTO/documentoBase/DocumentoBaseBorradorDTO.cs`
- Create: `API.Application.Interface/IDocumentoBaseApplication.cs`
- Create: `API.Application.Main/DocumentoBaseApplication.cs`
- Create: `API.Service.WebApi/Controllers/DocumentoBaseController.cs`
- Modify: `API.Service.WebApi/Startup.cs` (dos registros DI junto a `IInventarioAsientoService`, línea ~274)
- Test: `API.Service.WebApi.Tests/Application/DocumentoBaseApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/DocumentoBaseControllerTests.cs`

**Interfaces:**
- Consumes: `IDocumentoBaseService.ObtenerBorradorAsync`, `BorradorDocumentoBase`, `LineaAbiertaVenta`, `DocumentoBaseMensajes.OrigenNoExiste`, `TipoDocumentoVenta` (tareas 1–3); `PermisoUsuario.Tiene` / `PermisoUsuario.Denegado()`, `Accion` (existentes).
- Produces: `IDocumentoBaseApplication.ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino) : Task<Respuesta<DocumentoBaseBorradorDTO>>`; endpoint con respuestas 200 / 400 / 401 / 403 / 404; DTOs `DocumentoBaseBorradorDTO`, `DocumentoBaseCabeceraDTO`, `DocumentoBaseLineaDTO` (JSON camelCase en el cable: `origenTipo`, `origenEntry`, `origenNumDoc`, `cabecera{codigoSn,nombreSn,direccion,monedaDoc,prctjeDesc,prctjeImpuesto,comentario}`, `lineas[{baseTipo,baseEntry,baseLinea,codArticulo,descripcion,cantidadOrigen,cantidadAbierta,precio,precioBruto,prctjeDesc,codigoImpuesto,impuesto,codAlmacen}]`) que consume la Web en la Task 11.

- [ ] **Step 1: Escribir las pruebas que fallan**

`API.Service.WebApi.Tests/Application/DocumentoBaseApplicationTests.cs`:

```csharp
using API.Application.Main;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class DocumentoBaseApplicationTests
    {
        private readonly Mock<IDocumentoBaseService> _servicio = new();
        private readonly DocumentoBaseApplication _app;

        public DocumentoBaseApplicationTests()
        {
            _app = new DocumentoBaseApplication(_servicio.Object);
        }

        [Fact]
        public async Task ObtenerBorrador_MapeaCabeceraYLineas()
        {
            _servicio.Setup(s => s.ObtenerBorradorAsync(3, 10, 4)).ReturnsAsync(new BorradorDocumentoBase(
                3, 10, 12,
                new CabeceraBorrador("CL-1", "Cliente", "Dir", "QTZ", 5m, 12m, "nota"),
                new[] { new LineaAbiertaVenta(3, 10, 2, "ART1", "Desc", 10m, 6m, 25m, 25m, 0m, "IVA", 30m, "01") }));

            var r = await _app.ObtenerBorradorAsync(3, 10, 4);

            Assert.True(r.Resultado);
            Assert.Equal((3, 10, 12), (r.Dato.OrigenTipo, r.Dato.OrigenEntry, r.Dato.OrigenNumDoc));
            Assert.Equal(("CL-1", "Cliente", "Dir", "QTZ", 5m, 12m, "nota"),
                (r.Dato.Cabecera.CodigoSn, r.Dato.Cabecera.NombreSn, r.Dato.Cabecera.Direccion, r.Dato.Cabecera.MonedaDoc,
                 r.Dato.Cabecera.PrctjeDesc, r.Dato.Cabecera.PrctjeImpuesto, r.Dato.Cabecera.Comentario));
            var l = Assert.Single(r.Dato.Lineas);
            Assert.Equal((3, 10, 2, "ART1", "Desc", 10m, 6m, 25m, 25m, 0m, "IVA", 30m, "01"),
                (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Descripcion, l.CantidadOrigen, l.CantidadAbierta,
                 l.Precio, l.PrecioBruto, l.PrctjeDesc, l.CodigoImpuesto, l.Impuesto, l.CodAlmacen));
        }

        [Fact]
        public async Task ObtenerBorrador_ConError_DevuelveElMensajeDelServicio()
        {
            _servicio.Setup(s => s.ObtenerBorradorAsync(4, 1, 3)).ThrowsAsync(new Exception("No se puede copiar Pedido a Cotización."));

            var r = await _app.ObtenerBorradorAsync(4, 1, 3);

            Assert.False(r.Resultado);
            Assert.Equal("No se puede copiar Pedido a Cotización.", r.Mensaje);
            Assert.Null(r.Dato);
        }
    }
}
```

`API.Service.WebApi.Tests/Controllers/DocumentoBaseControllerTests.cs`:

```csharp
using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.documentoBase;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class DocumentoBaseControllerTests
    {
        private static ControllerContext Contexto(params Claim[] claims) => new()
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };

        private (DocumentoBaseController Controller, Mock<IDocumentoBaseApplication> App) Crear(params Claim[] claims)
        {
            var app = new Mock<IDocumentoBaseApplication>();
            return (new DocumentoBaseController(app.Object) { ControllerContext = Contexto(claims) }, app);
        }

        private static Claim[] Permisos(string perm) => new[] { new Claim("SuperUsuario", "N"), new Claim("perm", perm) };

        private static Claim[] Ambos(string a, string b) => new[] { new Claim("SuperUsuario", "N"), new Claim("perm", a), new Claim("perm", b) };

        [Fact]
        public async Task Abierto_ConPermisos_DevuelveOk()
        {
            var (c, app) = Crear(Ambos("Cotizaciones:V", "Pedidos:A"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true, Dato = new DocumentoBaseBorradorDTO() };
            app.Setup(a => a.ObtenerBorradorAsync(3, 10, 4)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(3, 10, 4);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_SinVerEnElOrigen_Devuelve403()
        {
            var (c, app) = Crear(Ambos("Cotizaciones:A", "Pedidos:A"));

            var r = await c.ObtenerAbiertoAsync(3, 10, 4);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_SinAgregarEnElDestino_Devuelve403()
        {
            var (c, app) = Crear(Ambos("Cotizaciones:V", "Pedidos:V"));

            var r = await c.ObtenerAbiertoAsync(3, 10, 4);

            Assert.Equal(403, Assert.IsType<ObjectResult>(r.Result).StatusCode);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_SuperUsuario_Pasa()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            app.Setup(a => a.ObtenerBorradorAsync(5, 1, 6)).ReturnsAsync(new Respuesta<DocumentoBaseBorradorDTO> { Resultado = true });

            var r = await c.ObtenerAbiertoAsync(5, 1, 6);

            Assert.IsType<OkObjectResult>(r.Result);
        }

        [Theory]
        [InlineData(9, 4)]
        [InlineData(3, 9)]
        public async Task Abierto_TipoDesconocido_Devuelve400(int origen, int destino)
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));

            var r = await c.ObtenerAbiertoAsync(origen, 1, destino);

            Assert.IsType<BadRequestObjectResult>(r.Result);
            app.Verify(a => a.ObtenerBorradorAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task Abierto_OrigenInexistente_Devuelve404()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = false, Mensaje = DocumentoBaseMensajes.OrigenNoExiste };
            app.Setup(a => a.ObtenerBorradorAsync(3, 99, 4)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(3, 99, 4);

            Assert.Same(respuesta, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Abierto_OtroError_Devuelve400ConElMensaje()
        {
            var (c, app) = Crear(new Claim("SuperUsuario", "S"));
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO> { Resultado = false, Mensaje = "El documento no tiene líneas abiertas." };
            app.Setup(a => a.ObtenerBorradorAsync(3, 1, 4)).ReturnsAsync(respuesta);

            var r = await c.ObtenerAbiertoAsync(3, 1, 4);

            Assert.Same(respuesta, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que fallan (no compila)**

Run: `dotnet test … --filter "FullyQualifiedName~DocumentoBaseApplicationTests|FullyQualifiedName~DocumentoBaseControllerTests"` → error de compilación (no existen los tipos).

- [ ] **Step 3: Implementar**

`API.Application.DTO/documentoBase/DocumentoBaseBorradorDTO.cs`:

```csharp
namespace API.Application.DTO.documentoBase
{
    public class DocumentoBaseBorradorDTO
    {
        public int OrigenTipo { get; set; }
        public int OrigenEntry { get; set; }
        public int OrigenNumDoc { get; set; }
        public DocumentoBaseCabeceraDTO Cabecera { get; set; } = new();
        public List<DocumentoBaseLineaDTO> Lineas { get; set; } = new();
    }

    public class DocumentoBaseCabeceraDTO
    {
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public string? Comentario { get; set; }
    }

    public class DocumentoBaseLineaDTO
    {
        public int BaseTipo { get; set; }
        public int BaseEntry { get; set; }
        public int BaseLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal CantidadOrigen { get; set; }
        public decimal CantidadAbierta { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

`API.Application.Interface/IDocumentoBaseApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.documentoBase;

namespace API.Application.Interface
{
    public interface IDocumentoBaseApplication
    {
        Task<Respuesta<DocumentoBaseBorradorDTO>> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);
    }
}
```

`API.Application.Main/DocumentoBaseApplication.cs` (mapeo manual: los records del dominio no pasan por AutoMapper):

```csharp
using API.Application.DTO;
using API.Application.DTO.documentoBase;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class DocumentoBaseApplication : IDocumentoBaseApplication
    {
        private readonly IDocumentoBaseService _documentoBase;

        public DocumentoBaseApplication(IDocumentoBaseService documentoBase)
        {
            _documentoBase = documentoBase;
        }

        public async Task<Respuesta<DocumentoBaseBorradorDTO>> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)
        {
            var respuesta = new Respuesta<DocumentoBaseBorradorDTO>();
            try
            {
                var b = await _documentoBase.ObtenerBorradorAsync(tipoOrigen, entry, tipoDestino);
                respuesta.Dato = new DocumentoBaseBorradorDTO
                {
                    OrigenTipo = b.OrigenTipo,
                    OrigenEntry = b.OrigenEntry,
                    OrigenNumDoc = b.OrigenNumDoc,
                    Cabecera = new DocumentoBaseCabeceraDTO
                    {
                        CodigoSn = b.Cabecera.CodigoSn,
                        NombreSn = b.Cabecera.NombreSn,
                        Direccion = b.Cabecera.Direccion,
                        MonedaDoc = b.Cabecera.MonedaDoc,
                        PrctjeDesc = b.Cabecera.PrctjeDesc,
                        PrctjeImpuesto = b.Cabecera.PrctjeImpuesto,
                        Comentario = b.Cabecera.Comentario
                    },
                    Lineas = b.Lineas.Select(l => new DocumentoBaseLineaDTO
                    {
                        BaseTipo = l.BaseTipo,
                        BaseEntry = l.BaseEntry,
                        BaseLinea = l.BaseLinea,
                        CodArticulo = l.CodArticulo,
                        Descripcion = l.Descripcion,
                        CantidadOrigen = l.CantidadOrigen,
                        CantidadAbierta = l.CantidadAbierta,
                        Precio = l.Precio,
                        PrecioBruto = l.PrecioBruto,
                        PrctjeDesc = l.PrctjeDesc,
                        CodigoImpuesto = l.CodigoImpuesto,
                        Impuesto = l.Impuesto,
                        CodAlmacen = l.CodAlmacen
                    }).ToList()
                };
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
    }
}
```

`API.Service.WebApi/Controllers/DocumentoBaseController.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.documentoBase;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/DocumentoBase")]
    public class DocumentoBaseController : ControllerBase
    {
        private readonly IDocumentoBaseApplication _documentoBaseApplication;

        public DocumentoBaseController(IDocumentoBaseApplication documentoBaseApplication)
        {
            _documentoBaseApplication = documentoBaseApplication;
        }

        // Módulo de permisos de cada tipo de documento de venta.
        private static string? ModuloDe(int tipo) => tipo switch
        {
            TipoDocumentoVenta.Cotizacion => "Cotizaciones",
            TipoDocumentoVenta.Pedido => "Pedidos",
            TipoDocumentoVenta.Entrega => "Entregas",
            TipoDocumentoVenta.Factura => "Facturas",
            _ => null
        };

        /// <summary>
        /// Borrador del documento destino con las líneas abiertas del origen ("Copiar a"). Exige Ver en el módulo del
        /// origen y Agregar en el del destino; se evalúa aquí (y no con el atributo) porque los módulos dependen de
        /// los parámetros.
        /// </summary>
        [HttpGet("{tipoOrigen:int}/{entry:int}/Abierto")]
        public async Task<ActionResult<Respuesta<DocumentoBaseBorradorDTO>>> ObtenerAbiertoAsync(
            [FromRoute] int tipoOrigen, [FromRoute] int entry, [FromQuery] int destino)
        {
            var moduloOrigen = ModuloDe(tipoOrigen);
            var moduloDestino = ModuloDe(destino);
            if (moduloOrigen is null || moduloDestino is null)
                return BadRequest(new Respuesta<DocumentoBaseBorradorDTO> { Mensaje = "El tipo de documento no es válido." });

            if (!PermisoUsuario.Tiene(User, moduloOrigen, Accion.Ver) || !PermisoUsuario.Tiene(User, moduloDestino, Accion.Agregar))
                return PermisoUsuario.Denegado();

            var respuesta = await _documentoBaseApplication.ObtenerBorradorAsync(tipoOrigen, entry, destino);
            if (respuesta.Resultado)
                return Ok(respuesta);

            return respuesta.Mensaje == DocumentoBaseMensajes.OrigenNoExiste ? NotFound(respuesta) : BadRequest(respuesta);
        }
    }
}
```

`API.Service.WebApi/Startup.cs` — junto a la línea `services.AddTransient<IInventarioAsientoService, InventarioAsientoService>();` agregar:

```csharp
            services.AddTransient<IDocumentoBaseService, DocumentoBaseService>();
            services.AddTransient<IDocumentoBaseApplication, DocumentoBaseApplication>();
```

(los `using` de `API.Domain.Core`, `API.Domain.Interface`, `API.Application.Main` y `API.Application.Interface` ya existen en `Startup.cs`; si falta alguno, agregarlo.)

- [ ] **Step 4: Correr y comprobar que pasa**

Run: `dotnet build API.sln -p:BaseOutputPath="…\apitest\"` → 0 errores; luego el comando del Step 2 → PASS (2 + 8 = 10 pruebas).

- [ ] **Step 5: Commit**

```bash
git add API.Application.DTO/documentoBase/DocumentoBaseBorradorDTO.cs API.Application.Interface/IDocumentoBaseApplication.cs API.Application.Main/DocumentoBaseApplication.cs API.Service.WebApi/Controllers/DocumentoBaseController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Application/DocumentoBaseApplicationTests.cs API.Service.WebApi.Tests/Controllers/DocumentoBaseControllerTests.cs
git commit -m "feat(documento-base): endpoint GET api/DocumentoBase/{tipo}/{entry}/Abierto con permisos por modulo

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `FacturaDomain` — validar líneas con base y regla de inventario `BaseTipo != 5`

**Precondición:** Task 5 completa; `grep -n "BaseEntry == null" API.Domain.Core/FacturaDomain.cs` muestra exactamente 1 línea (la condición de inventario a reemplazar).

**Files:**
- Modify: `API.Domain.Core/FacturaDomain.cs`
- Modify: `API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs`

**Interfaces:**
- Consumes: `IDocumentoBaseService.ValidarLineasAsync`, `LineasBase.Extraer`, `TipoDocumentoVenta` (Task 1).
- Produces: `FacturaDomain(repoFactura, repoDetalle, repoNumeracion, tx, asiento, IDocumentoBaseService documentoBase)` — el parámetro nuevo va **al final**.

- [ ] **Step 1: Actualizar y ampliar las pruebas** (`FacturaDomainTests.cs`)

a) Campos y constructor — agregar el campo y pasarlo al constructor:

```csharp
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
```
y cambiar la construcción a:
```csharp
            _domain = new FacturaDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, _documentoBase.Object);
```

b) Agregar el helper (junto a `LineaBase`):

```csharp
        private static FacturaDetalle LineaDe(string art, string alm, decimal? cant, decimal? precio, int? baseTipo, int? baseEntry, int? baseLinea) =>
            new() { CodArticulo = art, CodAlmacen = alm, Cantidad = cant, Precio = precio, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };
```

c) Las dos pruebas existentes que modelaban "línea que viene de una Entrega" solo con `BaseEntry` deben declarar `BaseTipo = 5`. Reemplazar su cuerpo así:

```csharp
        [Fact]
        public async Task InsertarAsync_LineaConBaseEntrega_NoGeneraMovimiento_PeroSiSeInsertaLinea()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { LineaDe("ART1", "01", 10m, 25m, baseTipo: 5, baseEntry: 500, baseLinea: 1) });

            Assert.Empty(_movimientosAsentados);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l => l.Entry == 99 && l.NoLinea == 1)), Times.Once);
        }
```
(renombra `InsertarAsync_LineaConBaseEntry_NoGeneraMovimiento_PeroSiSeInsertaLinea`) y

```csharp
        [Fact]
        public async Task InsertarAsync_Mezcla_SoloLaLineaQueNoVieneDeEntregaGeneraMovimiento()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[]
            {
                LineaDe("ART1", "01", 10m, 25m, baseTipo: 5, baseEntry: 500, baseLinea: 1),
                LineaDe("ART2", "01", 4m, 30m, baseTipo: null, baseEntry: null, baseLinea: null),
            });

            Assert.Single(_movimientosAsentados);
            Assert.Equal("ART2", _movimientosAsentados[0].CodArticulo);
            Assert.Equal(2, _movimientosAsentados[0].DocLinea);
        }
```
(renombra `InsertarAsync_Mezcla_SoloLaLineaSinBaseEntryGeneraMovimiento`). La prueba `InsertarAsync_LineaSinBaseEntry_GeneraMovimiento` se conserva sin cambios.

d) Casos nuevos (al final de la clase):

```csharp
        [Theory]
        [InlineData(3)]   // base Cotización
        [InlineData(4)]   // base Pedido
        public async Task InsertarAsync_LineaConBaseCotizacionOPedido_SiGeneraMovimiento(int baseTipo)
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { LineaDe("ART1", "01", 10m, 25m, baseTipo: baseTipo, baseEntry: 500, baseLinea: 1) });

            var m = Assert.Single(_movimientosAsentados);
            Assert.Equal(-10m, m.Cantidad);
        }

        [Fact]
        public async Task InsertarAsync_LineasConBase_ValidaConElServicioAntesDeNumerar()
        {
            var serie = SerieAuto(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var obj = new Factura { Serie = 4, CodigoSn = "CL-1" };

            await _domain.InsertarAsync(obj, new[]
            {
                LineaDe("ART1", "01", 10m, 25m, baseTipo: 3, baseEntry: 500, baseLinea: 2),
                LineaDe("ART2", "01", 1m, 25m, baseTipo: null, baseEntry: null, baseLinea: null),
            });

            _documentoBase.Verify(d => d.ValidarLineasAsync(TipoDocumentoVenta.Factura, "CL-1",
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(3, 500, 2, "ART1", 10m))), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinLineasConBase_NoLlamaAlServicio()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));

            await _domain.InsertarAsync(new Factura { Serie = 4 }, new[] { Linea("ART1", "01", 10m, 25m) });

            _documentoBase.Verify(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ValidacionDeBaseFalla_NoNumeraNiGuardaNiAsienta()
        {
            var serie = SerieAuto(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ThrowsAsync(new Exception("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6)."));
            var obj = new Factura { Serie = 4 };

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(obj, new[] { LineaDe("ART1", "01", 10m, 25m, baseTipo: 3, baseEntry: 500, baseLinea: 1) }));

            Assert.Equal("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6).", ex.Message);
            Assert.Equal(5, serie.SigNumero);   // el consecutivo no avanzó
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
            Assert.Empty(_movimientosAsentados);
        }

        [Fact]
        public async Task InsertarAsync_CabeceraSiempreConBaseTipoMenosUno()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4, BaseTipo = 5, BaseEntry = 77 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 1m, 1m) });

            Assert.Equal(-1, obj.BaseTipo);
            Assert.Null(obj.BaseEntry);
        }
```

- [ ] **Step 2: Correr y comprobar que fallan**

Run: `dotnet test … --filter "FullyQualifiedName~FacturaDomainTests"` → error de compilación (constructor de 6 parámetros) y, tras compilar, fallos de la regla de inventario.

- [ ] **Step 3: Implementar** (`API.Domain.Core/FacturaDomain.cs`)

a) Campo + constructor:

```csharp
        private readonly IDocumentoBaseService _documentoBase;

        public FacturaDomain(
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento,
            IDocumentoBaseService documentoBase)
        {
            _repoFactura = repoFactura;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _tx = tx;
            _asiento = asiento;
            _documentoBase = documentoBase;
        }
```

b) En `InsertarAsync`, justo después de `obj.FechaCancelado = null;` agregar:

```csharp
            // CK_Factura_Base: una factura ('--') solo admite BaseTipo = -1 en la cabecera. La referencia al documento
            // base (copiar a) vive únicamente en las líneas.
            obj.BaseTipo = -1;
            obj.BaseEntry = null;
```

c) Justo después de la línea `ValidarLineas(lineasList); // antes de numerar…` agregar:

```csharp
            // Líneas copiadas de una cotización, un pedido o una entrega: se validan antes de numerar, así un error no
            // avanza el consecutivo (ver DocumentoBaseService.ValidarLineasAsync).
            var basadas = LineasBase.Extraer(lineasList.Select(l => (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Cantidad)));
            if (basadas.Count > 0)
                await _documentoBase.ValidarLineasAsync(TipoDocumentoVenta.Factura, obj.CodigoSn, basadas);
```

d) Reemplazar la condición de inventario:

```csharp
                var movimientos = lineasList
                    .Where(l => (l.Cantidad ?? 0m) > 0m && l.BaseTipo != TipoDocumentoVenta.Entrega)   // BaseTipo 5: esa mercancía ya la movió su Entrega; con base Cotización o Pedido nadie entregó todavía
```

- [ ] **Step 4: Correr y comprobar que pasa**

Run: `dotnet test … --filter "FullyQualifiedName~FacturaDomainTests"` → PASS (los existentes + 6 nuevos: 2 del Theory + 4).

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/FacturaDomain.cs API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs
git commit -m "feat(documento-base): FacturaDomain valida lineas con base y descuenta inventario salvo las que vienen de una Entrega

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `EntregaDomain` — validar líneas con base (antes de numerar)

**Precondición:** Task 6 completa; en `EntregaDomain.InsertarAsync` la línea `var lineasList = lineas?.ToList() ?? new List<EntregaDetalle>();` aparece **después** del bloque de numeración (a mover).

**Files:**
- Modify: `API.Domain.Core/EntregaDomain.cs`
- Modify: `API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs`

**Interfaces:**
- Produces: `EntregaDomain(repoEntrega, repoDetalle, repoNumeracion, tx, asiento, IDocumentoBaseService documentoBase)` (parámetro nuevo al final). La Entrega sigue asentando **todas** sus líneas con cantidad > 0, tengan base o no.

- [ ] **Step 1: Actualizar y ampliar las pruebas** (`EntregaDomainTests.cs`)

a) Campo y constructor:

```csharp
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
```
```csharp
            _domain = new EntregaDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, _documentoBase.Object);
```

b) Helper (junto a `Linea`):

```csharp
        private static EntregaDetalle LineaDe(string art, string alm, decimal? cant, decimal? precio, int? baseTipo, int? baseEntry, int? baseLinea) =>
            new() { CodArticulo = art, CodAlmacen = alm, Cantidad = cant, Precio = precio, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };
```

c) Casos nuevos (al final de la clase):

```csharp
        [Fact]
        public async Task InsertarAsync_LineaConBase_SigueAsentandoInventario()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));

            await _domain.InsertarAsync(new Entrega { Serie = 4 }, new[] { LineaDe("ART1", "01", 10m, 25m, baseTipo: 4, baseEntry: 500, baseLinea: 1) });

            var m = Assert.Single(_movimientosAsentados);
            Assert.Equal(-10m, m.Cantidad);
        }

        [Fact]
        public async Task InsertarAsync_LineasConBase_ValidaConElServicio()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Entrega { Serie = 4, CodigoSn = "CL-1" };

            await _domain.InsertarAsync(obj, new[]
            {
                LineaDe("ART1", "01", 10m, 25m, baseTipo: 3, baseEntry: 500, baseLinea: 2),
                LineaDe("ART2", "01", 1m, 25m, baseTipo: null, baseEntry: null, baseLinea: null),
            });

            _documentoBase.Verify(d => d.ValidarLineasAsync(TipoDocumentoVenta.Entrega, "CL-1",
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(3, 500, 2, "ART1", 10m))), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_SinLineasConBase_NoLlamaAlServicio()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));

            await _domain.InsertarAsync(new Entrega { Serie = 4 }, new[] { Linea("ART1", "01", 10m, 25m) });

            _documentoBase.Verify(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_ValidacionDeBaseFalla_NoNumeraNiGuardaNiAsienta()
        {
            var serie = SerieAuto(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ThrowsAsync(new Exception("El socio del documento debe ser el del documento base Pedido 3 (CL-1)."));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Entrega { Serie = 4 }, new[] { LineaDe("ART1", "01", 10m, 25m, baseTipo: 4, baseEntry: 500, baseLinea: 1) }));

            Assert.Equal("El socio del documento debe ser el del documento base Pedido 3 (CL-1).", ex.Message);
            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<Entrega>()), Times.Never);
            Assert.Empty(_movimientosAsentados);
        }

        [Fact]
        public async Task InsertarAsync_CabeceraSiempreConBaseTipoMenosUno()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Entrega { Serie = 4, BaseTipo = 4, BaseEntry = 77 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 1m, 1m) });

            Assert.Equal(-1, obj.BaseTipo);
            Assert.Null(obj.BaseEntry);
        }
```

- [ ] **Step 2: Correr y comprobar que fallan** — `dotnet test … --filter "FullyQualifiedName~EntregaDomainTests"` → error de compilación (constructor).

- [ ] **Step 3: Implementar** (`API.Domain.Core/EntregaDomain.cs`)

a) Campo + constructor (parámetro nuevo al final, igual que en `FacturaDomain`):

```csharp
        private readonly IDocumentoBaseService _documentoBase;

        public EntregaDomain(
            IRepositorioGenerico<Entrega, int> repoEntrega,
            IRepositorioGenerico<EntregaDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento,
            IDocumentoBaseService documentoBase)
        {
            _repoEntrega = repoEntrega;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _tx = tx;
            _asiento = asiento;
            _documentoBase = documentoBase;
        }
```

b) En `InsertarAsync`: después de `obj.FechaCancelado = null;` agregar el bloque de cabecera:

```csharp
            // CK de la cabecera: solo BaseTipo = -1. La referencia al documento base vive únicamente en las líneas.
            obj.BaseTipo = -1;
            obj.BaseEntry = null;
```

c) **Mover** la línea `var lineasList = lineas?.ToList() ?? new List<EntregaDetalle>();` (hoy después de la numeración) a justo después del `if (serie.Bloqueado == "S") throw …;` y antes de `if (serie.Manual == "S")`, y agregar debajo de ella la validación:

```csharp
            var lineasList = lineas?.ToList() ?? new List<EntregaDetalle>();

            // Líneas copiadas de una cotización o de un pedido: se validan antes de numerar, así un error no avanza el
            // consecutivo (ver DocumentoBaseService.ValidarLineasAsync).
            var basadas = LineasBase.Extraer(lineasList.Select(l => (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Cantidad)));
            if (basadas.Count > 0)
                await _documentoBase.ValidarLineasAsync(TipoDocumentoVenta.Entrega, obj.CodigoSn, basadas);
```

(la línea original de `lineasList` después de la numeración se elimina; el resto del método no cambia: la Entrega asienta todas las líneas con cantidad > 0.)

- [ ] **Step 4: Correr y comprobar que pasa** → PASS (existentes + 5 nuevos).

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/EntregaDomain.cs API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs
git commit -m "feat(documento-base): EntregaDomain valida lineas con base antes de numerar

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `PedidoDomain` con líneas en una sola transacción + `PedidoCrearDTO.Lineas`

**Precondición:** Task 7 completa; `grep -n "IEjecutorTransaccion" API.Domain.Core/PedidoDomain.cs` sin resultados (el dominio aún no tiene transacción).

**Files:**
- Modify: `API.Domain.Interface/IPedidoDomain.cs`, `API.Domain.Core/PedidoDomain.cs`
- Modify: `API.Application.DTO/pedido/PedidoCrearDTO.cs`, `API.Application.Main/PedidoApplication.cs`
- Modify: `API.Service.WebApi.Tests/Domain/PedidoDomainTests.cs`
- Create: `API.Service.WebApi.Tests/Application/PedidoApplicationTests.cs`

**Interfaces:**
- Consumes: `IEjecutorTransaccion` (existente), `IDocumentoBaseService`, `LineasBase`, `TipoDocumentoVenta`.
- Produces: `IPedidoDomain.InsertarAsync(Pedido obj, IEnumerable<PedidoDetalle> lineas) : Task<int>` (nueva sobrecarga; la antigua `InsertarAsync(Pedido)` se conserva); `PedidoDomain(repoPedido, repoDetalle, repoNumeracion, IEjecutorTransaccion tx, IDocumentoBaseService documentoBase)`; `PedidoCrearDTO.Lineas : List<PedidoDetalleCrearDTO>` (opcional, vacía por defecto).

- [ ] **Step 1: Actualizar y ampliar las pruebas**

a) `PedidoDomainTests.cs` — agregar `using API.Domain.Interface;`, los campos y el constructor:

```csharp
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
```
```csharp
            _domain = new PedidoDomain(_repoPedidoMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object, _tx.Object, _documentoBase.Object);
            // El doble del ejecutor corre el Func directo (sin transacción ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _repoDetalleMock.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<PedidoDetalle>())).Returns(Task.CompletedTask);
```
(`using Moq;` y `API.Infraestructure.Interface` ya están; `IEjecutorTransaccion` vive en `API.Infraestructure.Interface` o `API.Domain.Interface`: copiar el `using` que usa `FacturaDomainTests`.)

b) Casos nuevos al final de la clase:

```csharp
        private static PedidoDetalle LineaPedido(string art, decimal? cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
            new() { CodArticulo = art, Cantidad = cant, Precio = 10m, BaseTipo = baseTipo, BaseEntry = baseEntry, BaseLinea = baseLinea };

        [Fact]
        public async Task InsertarConLineas_NumeraGuardaCabeceraYLineasEnUnaTransaccion()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });
            var obj = new Pedido { Serie = 4, TipoObjeto = "x", BaseTipo = 3, BaseEntry = 8 };

            var entry = await _domain.InsertarAsync(obj, new[] { LineaPedido("ART1", 2m), LineaPedido("ART2", 3m) });

            Assert.Equal(99, entry);
            Assert.Equal((5, "4", -1), (obj.NumDoc, obj.TipoObjeto, obj.BaseTipo));
            Assert.Null(obj.BaseEntry);
            Assert.Equal(6, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
            _repoDetalleMock.Verify(r => r.AgregarSinGuardarAsync(It.Is<PedidoDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.CodArticulo == "ART1")), Times.Once);
            _repoDetalleMock.Verify(r => r.AgregarSinGuardarAsync(It.Is<PedidoDetalle>(l => l.Entry == 99 && l.NoLinea == 2 && l.CodArticulo == "ART2")), Times.Once);
        }

        [Fact]
        public async Task InsertarConLineas_LineasConBase_ValidaConElServicioAntesDeNumerar()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 99; return c; });
            var obj = new Pedido { Serie = 4, CodigoSn = "CL-1" };

            await _domain.InsertarAsync(obj, new[] { LineaPedido("ART1", 4m, baseTipo: 3, baseEntry: 12, baseLinea: 1), LineaPedido("ART2", 1m) });

            _documentoBase.Verify(d => d.ValidarLineasAsync(TipoDocumentoVenta.Pedido, "CL-1",
                It.Is<IReadOnlyList<LineaBaseRequest>>(l => l.Count == 1 && l[0] == new LineaBaseRequest(3, 12, 1, "ART1", 4m))), Times.Once);
        }

        [Fact]
        public async Task InsertarConLineas_ValidacionFalla_NoNumeraNiGuarda()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            _documentoBase.Setup(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()))
                .ThrowsAsync(new Exception("El documento base Cotización 12 está cancelado."));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Pedido { Serie = 4 }, new[] { LineaPedido("ART1", 1m, baseTipo: 3, baseEntry: 12, baseLinea: 1) }));

            Assert.Equal("El documento base Cotización 12 está cancelado.", ex.Message);
            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoPedidoMock.Verify(r => r.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task InsertarConLineas_SerieBloqueada_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAutogenerada(bloqueado: "S"));

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new Pedido { Serie = 4 }, new[] { LineaPedido("ART1", 1m) }));
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
        }

        [Fact]
        public async Task InsertarSinLineas_CaminoAntiguo_NoUsaTransaccionNiServicio()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAutogenerada(sigNumero: 5));
            _repoPedidoMock.Setup(r => r.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync((Pedido c) => { c.Entry = 7; return c; });

            await _domain.InsertarAsync(new Pedido { Serie = 4 });

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _documentoBase.Verify(d => d.ValidarLineasAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<LineaBaseRequest>>()), Times.Never);
        }
```

c) `Application/PedidoApplicationTests.cs`:

```csharp
using API.Application.DTO.pedido;
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
    public class PedidoApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<IPedidoDomain> _domain = new();
        private readonly PedidoApplication _app;

        public PedidoApplicationTests()
        {
            _app = new PedidoApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_ConLineas_UsaLaSobrecargaTransaccionalYMapeaLasLineas()
        {
            Pedido? recibido = null;
            List<PedidoDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>()))
                .Callback<Pedido, IEnumerable<PedidoDetalle>>((p, l) => { recibido = p; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(42);
            var dto = new PedidoCrearDTO
            {
                Serie = 7, CodigoSn = "CL-1",
                Lineas = { new PedidoDetalleCrearDTO { CodArticulo = "ART1", Cantidad = 4m, Precio = 10m, BaseTipo = 3, BaseEntry = 12, BaseLinea = 1 } }
            };

            var r = await _app.InsertarAsync(dto);

            Assert.True(r.Resultado);
            Assert.Equal(42, r.Dato);
            Assert.Equal("CL-1", recibido!.CodigoSn);
            var l = Assert.Single(lineasRecibidas!);
            Assert.Equal(("ART1", 4m, 3, 12, 1), (l.CodArticulo, l.Cantidad, l.BaseTipo, l.BaseEntry, l.BaseLinea));
            _domain.Verify(d => d.InsertarAsync(It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_SinLineas_UsaElCaminoAntiguo()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>())).ReturnsAsync(9);

            var r = await _app.InsertarAsync(new PedidoCrearDTO { Serie = 7 });

            Assert.True(r.Resultado);
            Assert.Equal(9, r.Dato);
            _domain.Verify(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_ConError_DevuelveElMensajeDelDominio()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Pedido>(), It.IsAny<IEnumerable<PedidoDetalle>>()))
                .ThrowsAsync(new Exception("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6)."));

            var r = await _app.InsertarAsync(new PedidoCrearDTO { Serie = 7, Lineas = { new PedidoDetalleCrearDTO { CodArticulo = "A", Cantidad = 9m } } });

            Assert.False(r.Resultado);
            Assert.Equal("La cantidad de la línea 1 de Cotización 12 excede la cantidad abierta (6).", r.Mensaje);
        }
    }
}
```

- [ ] **Step 2: Correr y comprobar que fallan** — `dotnet test … --filter "FullyQualifiedName~PedidoDomainTests|FullyQualifiedName~PedidoApplicationTests"` → error de compilación.

- [ ] **Step 3: Implementar**

a) `API.Domain.Interface/IPedidoDomain.cs` — agregar bajo `Task<int> InsertarAsync(Pedido obj);`:

```csharp
        /// <summary>Alta atómica: cabecera + líneas (con o sin documento base) en una sola transacción.</summary>
        Task<int> InsertarAsync(Pedido obj, IEnumerable<PedidoDetalle> lineas);
```

b) `API.Application.DTO/pedido/PedidoCrearDTO.cs` — agregar al final de la clase:

```csharp
        /// <summary>
        /// Líneas del pedido (opcional). Si vienen, el pedido se guarda con sus líneas en una sola petición y una sola
        /// transacción (lo usa "Copiar a"); si no, se mantiene el flujo anterior de cabecera y luego cada línea.
        /// </summary>
        public List<PedidoDetalleCrearDTO> Lineas { get; set; } = new();
```

c) `API.Application.Main/PedidoApplication.cs` — reemplazar el cuerpo del `try` de `InsertarAsync`:

```csharp
                var pedido = _mapper.Map<Pedido>(obj);
                if (obj.Lineas is { Count: > 0 })
                {
                    var lineas = _mapper.Map<IEnumerable<PedidoDetalle>>(obj.Lineas);
                    respuesta.Dato = await _pedidoDomain.InsertarAsync(pedido, lineas);
                }
                else
                {
                    respuesta.Dato = await _pedidoDomain.InsertarAsync(pedido);
                }
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
```

d) `API.Domain.Core/PedidoDomain.cs` — agregar `using` si falta; campos y constructor (parámetros nuevos al final):

```csharp
        private readonly IEjecutorTransaccion _tx;
        private readonly IDocumentoBaseService _documentoBase;

        public PedidoDomain(
            IRepositorioGenerico<Pedido, int> repoGenericoPedido,
            IRepositorioGenerico<PedidoDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion,
            IEjecutorTransaccion tx,
            IDocumentoBaseService documentoBase)
        {
            _repoGenericoPedido = repoGenericoPedido;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
            _tx = tx;
            _documentoBase = documentoBase;
        }
```
(`IEjecutorTransaccion` está en el namespace que importan `FacturaDomain`/`EntregaDomain`: copiar su `using`.)

Reemplazar **todo** el método `InsertarAsync(Pedido obj)` actual (desde `public async Task<int> InsertarAsync(Pedido obj)` hasta su `}` de cierre, antes de `ActualizarAsync`) por estos tres miembros (el bloque de numeración se extrae sin cambiar su lógica):

```csharp
        public async Task<int> InsertarAsync(Pedido obj)
        {
            obj.TipoObjeto = TipoObjetoPedido;
            await AsignarNumeroAsync(obj);

            var creado = await _repoGenericoPedido.InsertarAsync(obj);
            return creado.Entry;
        }

        public async Task<int> InsertarAsync(Pedido obj, IEnumerable<PedidoDetalle> lineas)
        {
            obj.TipoObjeto = TipoObjetoPedido;
            // CK de la cabecera: solo BaseTipo = -1. La referencia al documento base vive únicamente en las líneas.
            obj.BaseTipo = -1;
            obj.BaseEntry = null;

            var lineasList = lineas?.ToList() ?? new List<PedidoDetalle>();

            // Líneas copiadas de una cotización: se validan antes de numerar, así un error no avanza el consecutivo.
            var basadas = LineasBase.Extraer(lineasList.Select(l => (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Cantidad)));
            if (basadas.Count > 0)
                await _documentoBase.ValidarLineasAsync(TipoDocumentoVenta.Pedido, obj.CodigoSn, basadas);

            await AsignarNumeroAsync(obj);

            return await _tx.EjecutarAsync(async () =>
            {
                var creado = await _repoGenericoPedido.InsertarAsync(obj); // Save #1: asigna Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = creado.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoGenericoDetalle.AgregarSinGuardarAsync(linea);
                }

                return creado.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + consecutivo) + Commit
        }

        /// <summary>Valida la serie y asigna el número de documento (manual o el consecutivo de la serie).</summary>
        private async Task AsignarNumeroAsync(Pedido obj)
        {
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

                // No se llama a _repoGenericoNumeracion.ActualizarAsync a propósito: "serie" ya es una entidad
                // rastreada por el mismo ApiDbTestContext que usan los otros repos genéricos (mismo scope de la
                // petición), así que el cambio queda pendiente y se guarda junto con el INSERT en el mismo Save --
                // si el INSERT falla, el incremento del consecutivo tampoco se guarda.
                serie.SigNumero = serie.SigNumero.Value + 1;
            }
        }
```

- [ ] **Step 4: Correr y comprobar que pasa**

Run: `dotnet build API.sln …` → 0 errores; `dotnet test … --filter "FullyQualifiedName~PedidoDomainTests|FullyQualifiedName~PedidoApplicationTests"` → PASS (existentes + 5 de dominio + 3 de aplicación).

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Interface/IPedidoDomain.cs API.Domain.Core/PedidoDomain.cs API.Application.DTO/pedido/PedidoCrearDTO.cs API.Application.Main/PedidoApplication.cs API.Service.WebApi.Tests/Domain/PedidoDomainTests.cs API.Service.WebApi.Tests/Application/PedidoApplicationTests.cs
git commit -m "feat(documento-base): Pedido acepta lineas en una sola transaccion y valida las copiadas de una cotizacion

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Integridad del origen en Cotización, Pedido, Entrega y sus líneas

**Precondición:** Task 8 completa. Nota de diseño verificada en el código: `EntregaDetalleDomain.ActualizarAsync`/`EliminarAsync` ya lanzan siempre (`LanzarSiElDocumentoExisteAsync`: "Las líneas se definen al crear el documento y no se pueden modificar después."), una regla más estricta que la del spec; por eso **no** se modifica `EntregaDetalleDomain` y `EntregaDetalleDomainTests` queda intacto. La integridad de líneas se agrega solo en Cotización y Pedido.

**Files:**
- Modify: `API.Domain.Core/CotizacionDomain.cs`, `PedidoDomain.cs`, `EntregaDomain.cs`, `CotizacionDetalleDomain.cs`, `PedidoDetalleDomain.cs`
- Modify: `API.Service.WebApi.Tests/Domain/CotizacionDomainTests.cs`, `PedidoDomainTests.cs`, `EntregaDomainTests.cs`
- Create: `API.Service.WebApi.Tests/Domain/CotizacionDetalleDomainTests.cs`, `PedidoDetalleDomainTests.cs`

**Interfaces:**
- Consumes: `IDocumentoBaseService.ValidarOrigenModificableAsync` / `ValidarLineaModificableAsync`, `TipoDocumentoVenta` (Task 1).
- Produces: `CotizacionDomain(repoCot, repoDetalle, repoNumeracion, IDocumentoBaseService documentoBase)`; `PedidoDomain(…, tx, documentoBase)` (ya tiene `_documentoBase` desde la Task 8); `CotizacionDetalleDomain(repoDet, IDocumentoBaseService documentoBase)`; `PedidoDetalleDomain(repoDet, IDocumentoBaseService documentoBase)`. Las `accion` usadas: `"eliminar el documento"`, `"cancelar el documento"`, `"cambiar el socio de negocio"`, y `"modificar"`/`"eliminar"` para líneas.

- [ ] **Step 1: Escribir las pruebas que fallan**

a) `CotizacionDomainTests.cs`: agregar `using API.Domain.Interface;`, el campo `private readonly Mock<IDocumentoBaseService> _documentoBase = new();` y cambiar la construcción a `new CotizacionDomain(_repoCotizacionMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object, _documentoBase.Object)`. Casos nuevos al final de la clase:

```csharp
        [Fact]
        public async Task EliminarAsync_ConDerivados_Lanza_YNoBorraNada()
        {
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "eliminar el documento"))
                .ThrowsAsync(new Exception("No se puede eliminar el documento: lo usan Pedido 3. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(1));

            Assert.Equal("No se puede eliminar el documento: lo usan Pedido 3. Cancele primero esos documentos.", ex.Message);
            _repoDetalleMock.Verify(r => r.EliminarAsync(It.IsAny<(int, int)>()), Times.Never);
            _repoCotizacionMock.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_PasarAEstadoCancelado_ValidaElOrigen()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _repoCotizacionMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Cotizacion>())).ReturnsAsync(true);
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cancelar el documento"))
                .ThrowsAsync(new Exception("bloqueado"));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "C", CodigoSn = "CL-1" }));

            _repoCotizacionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarElSocio_ValidaElOrigen()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, 1, "cambiar el socio de negocio"))
                .ThrowsAsync(new Exception("bloqueado"));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "A", CodigoSn = "CL-2" }));

            _repoCotizacionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Cotizacion>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_SinCancelarNiCambiarSocio_NoValida()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _repoCotizacionMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Cotizacion>())).ReturnsAsync(true);

            var ok = await _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "A", CodigoSn = "cl-1", Comentario = "x" });

            Assert.True(ok);
            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_YaCancelada_NoRevalida()
        {
            _repoCotizacionMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Cotizacion { Entry = 1, EstadoDoc = "C", CodigoSn = "CL-1" });
            _repoCotizacionMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Cotizacion>())).ReturnsAsync(true);

            await _domain.ActualizarAsync(1, new Cotizacion { EstadoDoc = "C", CodigoSn = "CL-1" });

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
```

b) `PedidoDomainTests.cs` — casos equivalentes con `TipoDocumentoVenta.Pedido` (`ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, 1, …)`, `_repoPedidoMock`, entidad `Pedido`):

```csharp
        [Fact]
        public async Task EliminarAsync_ConDerivados_Lanza_YNoBorraNada()
        {
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, 1, "eliminar el documento"))
                .ThrowsAsync(new Exception("No se puede eliminar el documento: lo usan Entrega 4. Cancele primero esos documentos."));

            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(1));

            _repoDetalleMock.Verify(r => r.EliminarAsync(It.IsAny<(int, int)>()), Times.Never);
            _repoPedidoMock.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_PasarAEstadoCancelado_ValidaElOrigen()
        {
            _repoPedidoMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Pedido { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, 1, "cancelar el documento"))
                .ThrowsAsync(new Exception("bloqueado"));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "C", CodigoSn = "CL-1" }));

            _repoPedidoMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<Pedido>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CambiarElSocio_ValidaElOrigen()
        {
            _repoPedidoMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Pedido { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Pedido, 1, "cambiar el socio de negocio"))
                .ThrowsAsync(new Exception("bloqueado"));

            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-2" }));
        }

        [Fact]
        public async Task ActualizarAsync_SinCancelarNiCambiarSocio_NoValida()
        {
            _repoPedidoMock.Setup(r => r.ObtenerAsync(1)).ReturnsAsync(new Pedido { Entry = 1, EstadoDoc = "A", CodigoSn = "CL-1" });
            _repoPedidoMock.Setup(r => r.ActualizarAsync(1, It.IsAny<Pedido>())).ReturnsAsync(true);

            await _domain.ActualizarAsync(1, new Pedido { EstadoDoc = "A", CodigoSn = "CL-1" });

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
```

c) `EntregaDomainTests.cs` — dos casos (revisar cómo el archivo arma una Entrega existente en sus pruebas de cancelación y reutilizar esa misma preparación `_repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A" })`):

```csharp
        [Fact]
        public async Task ActualizarAsync_Cancelar_ConDerivados_Lanza_YNoRevierteInventario()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A" });
            _documentoBase.Setup(d => d.ValidarOrigenModificableAsync(TipoDocumentoVenta.Entrega, 7, "cancelar el documento"))
                .ThrowsAsync(new Exception("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, new Entrega { Cancelado = "S" }));

            Assert.Equal("No se puede cancelar el documento: lo usan Factura 2. Cancele primero esos documentos.", ex.Message);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_EdicionDeComentario_NoValidaElOrigen()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Entrega { Entry = 7, Cancelado = "N", EstadoInv = "A" });

            await _domain.ActualizarAsync(7, new Entrega { Comentario = "x" });

            _documentoBase.Verify(d => d.ValidarOrigenModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
```

d) `CotizacionDetalleDomainTests.cs` (nuevo) y `PedidoDetalleDomainTests.cs` (nuevo; mismo texto cambiando `Cotizacion`→`Pedido`, `CotizacionDetalle`→`PedidoDetalle` y `TipoDocumentoVenta.Cotizacion`→`TipoDocumentoVenta.Pedido`). Para Cotización:

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
    public class CotizacionDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IDocumentoBaseService> _documentoBase = new();
        private readonly CotizacionDetalleDomain _domain;

        public CotizacionDetalleDomainTests()
        {
            _domain = new CotizacionDetalleDomain(_repoDet.Object, _documentoBase.Object);
        }

        [Fact]
        public async Task ActualizarAsync_LineaSinDerivados_Actualiza()
        {
            _repoDet.Setup(r => r.ActualizarAsync((5, 2), It.IsAny<CotizacionDetalle>())).ReturnsAsync(true);

            var ok = await _domain.ActualizarAsync(5, 2, new CotizacionDetalle { Entry = 5, NoLinea = 2 });

            Assert.True(ok);
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "modificar"), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_LineaConsumida_Lanza_YNoActualiza()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "modificar"))
                .ThrowsAsync(new Exception("No se puede modificar la línea 2: la usan Pedido 3. Cancele primero esos documentos."));

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, 2, new CotizacionDetalle()));

            Assert.Equal("No se puede modificar la línea 2: la usan Pedido 3. Cancele primero esos documentos.", ex.Message);
            _repoDet.Verify(r => r.ActualizarAsync(It.IsAny<(int, int)>(), It.IsAny<CotizacionDetalle>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_LineaSinDerivados_Elimina()
        {
            _repoDet.Setup(r => r.EliminarAsync((5, 2))).ReturnsAsync(true);

            Assert.True(await _domain.EliminarAsync(5, 2));
            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "eliminar"), Times.Once);
        }

        [Fact]
        public async Task EliminarAsync_LineaConsumida_Lanza_YNoElimina()
        {
            _documentoBase.Setup(d => d.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, 5, 2, "eliminar"))
                .ThrowsAsync(new Exception("No se puede eliminar la línea 2: la usan Pedido 3. Cancele primero esos documentos."));

            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5, 2));

            _repoDet.Verify(r => r.EliminarAsync(It.IsAny<(int, int)>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_AgregarLineaNueva_NoSeBloquea()
        {
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<CotizacionDetalle>().AsAsyncQueryable());
            _repoDet.Setup(r => r.InsertarAsync(It.IsAny<CotizacionDetalle>())).ReturnsAsync((CotizacionDetalle l) => l);

            await _domain.InsertarAsync(new CotizacionDetalle { Entry = 5 });

            _documentoBase.Verify(d => d.ValidarLineaModificableAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }
    }
}
```
(`InsertarAsync` calcula el `NoLinea` con `ObtenerPorCotizacionAsync`, que usa `ToListAsync`: por eso la prueba usa `AsAsyncQueryable()` del helper `TestHelpers`.)

- [ ] **Step 2: Correr y comprobar que fallan** — `dotnet test … --filter "FullyQualifiedName~CotizacionDomainTests|FullyQualifiedName~PedidoDomainTests|FullyQualifiedName~EntregaDomainTests|FullyQualifiedName~CotizacionDetalleDomainTests|FullyQualifiedName~PedidoDetalleDomainTests"` → error de compilación (constructores).

- [ ] **Step 3: Implementar**

a) `CotizacionDomain.cs` — campo + constructor (parámetro nuevo al final):

```csharp
        private readonly IDocumentoBaseService _documentoBase;

        public CotizacionDomain(
            IRepositorioGenerico<Cotizacion, int> repoGenericoCotizacion,
            IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion,
            IDocumentoBaseService documentoBase)
        {
            _repoGenericoCotizacion = repoGenericoCotizacion;
            _repoGenericoDetalle = repoGenericoDetalle;
            _repoGenericoNumeracion = repoGenericoNumeracion;
            _documentoBase = documentoBase;
        }
```
y reemplazar `ActualizarAsync` y el inicio de `EliminarAsync`:

```csharp
        public async Task<bool> ActualizarAsync(int id, Cotizacion obj)
        {
            obj.TipoObjeto = TipoObjetoCotizacion;

            // Un origen con documentos derivados activos no se cancela ni cambia de socio (se calcula antes de actualizar:
            // la entidad existente está rastreada y el update la pisa).
            var existente = await _repoGenericoCotizacion.ObtenerAsync(id);
            if (existente is not null)
            {
                var seCancela = existente.EstadoDoc != "C" && existente.Cancelado != "S" && (obj.EstadoDoc == "C" || obj.Cancelado == "S");
                var cambiaSocio = !string.Equals(existente.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase);
                if (seCancela)
                    await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, id, "cancelar el documento");
                else if (cambiaSocio)
                    await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, id, "cambiar el socio de negocio");
            }

            return await _repoGenericoCotizacion.ActualizarAsync(id, obj);
        }

        public async Task<bool> EliminarAsync(int id)
        {
            await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.Cotizacion, id, "eliminar el documento");

            // No existe FK/cascada entre CotizacionDetalle.Entry y Cotizacion.Entry en la base de
            // datos, así que las líneas de detalle se borran a mano antes que el encabezado.
            var detalles = await _repoGenericoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
            {
                await _repoGenericoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));
            }

            return await _repoGenericoCotizacion.EliminarAsync(id);
        }
```

b) `PedidoDomain.cs` — mismos dos reemplazos usando `_repoGenericoPedido`, `Pedido` y `TipoDocumentoVenta.Pedido` (el constructor y `_documentoBase` ya existen desde la Task 8).

c) `EntregaDomain.cs` — en `ActualizarAsync`, dentro del `if (obj.Cancelado == "S")` y **antes** de `return await _tx.EjecutarAsync(…)`:

```csharp
                // Una entrega que ya se facturó (total o parcialmente) no se cancela: primero se cancela la factura.
                await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoVenta.Entrega, id, "cancelar el documento");
```

d) `CotizacionDetalleDomain.cs` y `PedidoDetalleDomain.cs` — constructor con el servicio y las dos validaciones (ejemplo Cotización; Pedido idéntico con `PedidoDetalle`/`TipoDocumentoVenta.Pedido`):

```csharp
        private readonly IDocumentoBaseService _documentoBase;

        public CotizacionDetalleDomain(
            IRepositorioGenerico<CotizacionDetalle, (int Entry, int NoLinea)> repoGenericoDet,
            IDocumentoBaseService documentoBase)
        {
            _repoGenericoDet = repoGenericoDet;
            _documentoBase = documentoBase;
        }

        public async Task<bool> ActualizarAsync(int entry, int noLinea, CotizacionDetalle obj)
        {
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, entry, noLinea, "modificar");
            return await _repoGenericoDet.ActualizarAsync((entry, noLinea), obj);
        }

        public async Task<bool> EliminarAsync(int entry, int noLinea)
        {
            await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoVenta.Cotizacion, entry, noLinea, "eliminar");
            return await _repoGenericoDet.EliminarAsync((entry, noLinea));
        }
```

- [ ] **Step 4: Correr y comprobar que pasa** — el comando del Step 2 → PASS (existentes + 5 + 4 + 2 + 5 + 5 nuevos). `dotnet build API.sln …` → 0 errores.

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/CotizacionDomain.cs API.Domain.Core/PedidoDomain.cs API.Domain.Core/EntregaDomain.cs API.Domain.Core/CotizacionDetalleDomain.cs API.Domain.Core/PedidoDetalleDomain.cs API.Service.WebApi.Tests/Domain/CotizacionDomainTests.cs API.Service.WebApi.Tests/Domain/PedidoDomainTests.cs API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs API.Service.WebApi.Tests/Domain/CotizacionDetalleDomainTests.cs API.Service.WebApi.Tests/Domain/PedidoDetalleDomainTests.cs
git commit -m "feat(documento-base): un origen con derivados activos no se cancela, elimina ni cambia de socio, y sus lineas copiadas no se tocan

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 3 — Verificación de la API

### Task 10: Compilación, suite completa y prueba de humo contra SQL Server real

**Precondición:** Tasks 1–9 completas y commiteadas (`git log --oneline -9` en la API muestra los 9 commits de la fase). `git status --short` solo muestra `API.Service.WebApi/appsettings.json`.

**Files:** ninguno del repo (la prueba de humo vive en el scratchpad y no se commitea).

- [ ] **Step 1: Compilación y suite completa**

Run (PowerShell, desde la raíz de la API): `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
Expected: 0 errores de compilación y **0 fallos**; total = 1443 + las pruebas nuevas del plan (23 + 36 + 10 + 6 + 5 + 8 + 21 = 109) = **1552**. Si el total difiere en unas pocas pruebas (por casos de `Theory`), lo que debe cumplirse es 0 fallos y `1443 + nuevas`; reportar la cifra exacta.

- [ ] **Step 2: Crear una base temporal y levantar la API contra ella**

Todo en PowerShell, en una misma sesión (las variables de entorno solo viven en ese proceso). Variable `$SP` = ruta del scratchpad (`C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad`).

```powershell
$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\migue\source\repos\angelm0508'
$cfg = Get-Content "$repo\API\API.Service.WebApi\appsettings.json" -Raw
$pw = [regex]::Match($cfg, 'Password=([^;"]*)').Groups[1].Value
if (-not $pw) { throw 'sin contraseña en appsettings' }
$db = 'API_DB_COPIA_A'
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
$api = Start-Process dotnet -ArgumentList "`"$($dll.FullName)`"" -PassThru -WindowStyle Hidden -RedirectStandardOutput "$SP\api-copia.log" -RedirectStandardError "$SP\api-copia.err"
# esperar a que escuche
1..30 | ForEach-Object { try { Invoke-WebRequest http://localhost:5199/swagger -UseBasicParsing -SkipHttpErrorCheck | Out-Null; break } catch { Start-Sleep 1 } }
```
Expected: el script SQL corre sin errores, el build termina en 0 errores y la API responde en el puerto 5199 (comprobar que `$SP\api-copia.log` muestra la base `API_DB_COPIA_A` y **nunca** `API_DB_TEST`). La contraseña no se imprime en ningún paso.

- [ ] **Step 3: Escribir y correr la prueba de humo**

Crear `$SP\smoke-copiar-a.ps1` con este contenido y correrlo: `pwsh -File "$SP\smoke-copiar-a.ps1" -Base http://localhost:5199`.

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

# ---------------------------------------------------------------- sesión
$login = Api POST 'api/Auth/login' @{ usuario = 'admin'; contrasena = 'Admin123!' } -SinToken
$script:Token = $login.Dato.token
Esperar 'login' ($login.Status -eq 200 -and [bool]$script:Token)
Esperar 'sin token -> 401' ((Api GET 'api/DocumentoBase/3/1/Abierto?destino=4' -SinToken).Status -eq 401)

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
foreach ($a in 'ART-001', 'ART-002') {
    $null = Api POST 'api/Articulo' @{ Codigo = $a; Nombre = "Articulo $a"; CodigoGrupo = $grupoArt; Activo = 'S'; ArticuloCompra = 'S'; ArticuloVenta = 'S'
        ArticuloInventario = 'S'; PrecioUnitario = 0; CantDisponible = 0; CantConfirmada = 0; CantPedida = 0; AlmacenDefecto = '01'; Serie = (Serie '2' '--' 'S') }
}
$compra = Api POST 'api/FacturaCompra' (Cab (Serie '13') 'PR-0001' 'Proveedor Smoke' @((Linea 'ART-001' 'Art 1' 100 50), (Linea 'ART-002' 'Art 2' 50 20)))
Esperar 'compra inicial (stock 100 y 50)' ($compra.Status -eq 200 -and (Existencia 'ART-001') -eq 100 -and (Existencia 'ART-002') -eq 50) $compra.Mensaje

# cotización con 2 líneas: ART-001 x10 y ART-002 x5
$cotRes = Api POST 'api/Cotizacion' ((Cab (Serie '3') 'CL-0001' 'Cliente Smoke' @()) | ForEach-Object { $_.Remove('Lineas'); $_ })
$cot = $cotRes.Dato
foreach ($l in @((Linea 'ART-001' 'Art 1' 10 85), (Linea 'ART-002' 'Art 2' 5 30))) { $l.Entry = $cot; $null = Api POST 'api/CotizacionDetalle' $l }
Esperar 'cotización creada con 2 líneas' ($cotRes.Status -eq 200 -and $cot -gt 0)

# ---------------------------------------------------------------- borrador y copia parcial
$b = Api GET "api/DocumentoBase/3/$cot/Abierto?destino=4"
Esperar 'borrador cot -> pedido: 2 líneas abiertas (10 y 5)' ($b.Status -eq 200 -and $b.Dato.lineas.Count -eq 2 -and $b.Dato.lineas[0].cantidadAbierta -eq 10 -and $b.Dato.lineas[1].cantidadAbierta -eq 5)
Esperar 'combinación inválida (4 -> 3) = 400' ((Api GET "api/DocumentoBase/4/1/Abierto?destino=3").Status -eq 400)
Esperar 'origen inexistente = 404' ((Api GET 'api/DocumentoBase/3/99999/Abierto?destino=4').Status -eq 404)

$pedA = Api POST 'api/Pedido' (Cab (Serie '4') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ART-001' 'Art 1' 4 85 3 $cot 1)))
Esperar 'pedido A desde la cotización (4 de 10)' ($pedA.Status -eq 200) $pedA.Mensaje
$b = Api GET "api/DocumentoBase/3/$cot/Abierto?destino=4"
Esperar 'línea 1 queda con 6 abiertas' ($b.Dato.lineas[0].cantidadAbierta -eq 6)

$exceso = Api POST 'api/Pedido' (Cab (Serie '4') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ART-001' 'Art 1' 7 85 3 $cot 1)))
Esperar 'exceso (7 > 6) rechazado' ($exceso.Status -eq 400 -and $exceso.Mensaje -like '*excede la cantidad abierta (6)*') $exceso.Mensaje
$pedB = Api POST 'api/Pedido' (Cab (Serie '4') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ART-001' 'Art 1' 1 85 3 $cot 1)))
$numA = (Api GET "api/Pedido/$($pedA.Dato)").Dato.numDoc; $numB = (Api GET "api/Pedido/$($pedB.Dato)").Dato.numDoc
Esperar 'el rechazo no consumió consecutivo (B = A + 1)' ($pedB.Status -eq 200 -and $numB -eq $numA + 1) "A=$numA B=$numB"

# ---------------------------------------------------------------- inventario
$ent = Api POST 'api/Entrega' (Cab (Serie '5') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ART-001' 'Art 1' 3 85 4 $pedA.Dato 1)))
Esperar 'entrega desde el pedido A (3): descuenta -> 97' ($ent.Status -eq 200 -and (Existencia 'ART-001') -eq 97) $ent.Mensaje
$facE = Api POST 'api/Factura' (Cab (Serie '6') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ART-001' 'Art 1' 3 85 5 $ent.Dato 1)))
Esperar 'factura desde la entrega (3): NO vuelve a descontar -> 97' ($facE.Status -eq 200 -and (Existencia 'ART-001') -eq 97) $facE.Mensaje
$facD = Api POST 'api/Factura' (Cab (Serie '6') 'CL-0001' 'Cliente Smoke' @((LineaBase 'ART-002' 'Art 2' 5 30 3 $cot 2)))
Esperar 'factura directa desde la cotización (línea 2 x5): SÍ descuenta -> 45' ($facD.Status -eq 200 -and (Existencia 'ART-002') -eq 45) $facD.Mensaje

# ---------------------------------------------------------------- integridad del origen
$h = (Api GET "api/Cotizacion/$cot").Dato; $h | Add-Member -NotePropertyName estadoDoc -NotePropertyValue 'C' -Force
$r = Api PUT "api/Cotizacion/$cot" $h
Esperar 'cancelar la cotización con derivados = 400' ($r.Status -eq 400 -and $r.Mensaje -like 'No se puede cancelar el documento: lo usan*') $r.Mensaje
$r = Api DELETE "api/Cotizacion/$cot"
Esperar 'eliminar la cotización con derivados = 400' ($r.Status -eq 400 -and $r.Mensaje -like 'No se puede eliminar el documento: lo usan*') $r.Mensaje
$lin = (Api GET "api/CotizacionDetalle/$cot/1").Dato
$r = Api PUT "api/CotizacionDetalle/$cot/1" $lin
Esperar 'modificar la línea 1 consumida = 400' ($r.Status -eq 400 -and $r.Mensaje -like 'No se puede modificar la línea 1*') $r.Mensaje
$r = Api PUT "api/Entrega/$($ent.Dato)" @{ Cancelado = 'S' }
Esperar 'cancelar la entrega ya facturada = 400' ($r.Status -eq 400 -and $r.Mensaje -like '*lo usan Factura*') $r.Mensaje

# ---------------------------------------------------------------- liberar cantidad al cancelar destinos
$r = Api PUT "api/Factura/$($facE.Dato)" @{ Cancelado = 'S' }
Esperar 'cancelar la factura (de la entrega) = 200' ($r.Status -eq 200) $r.Mensaje
$r = Api PUT "api/Entrega/$($ent.Dato)" @{ Cancelado = 'S' }
Esperar 'ahora la entrega se cancela y devuelve stock -> 100' ($r.Status -eq 200 -and (Existencia 'ART-001') -eq 100) $r.Mensaje
$hp = (Api GET "api/Pedido/$($pedA.Dato)").Dato; $hp | Add-Member -NotePropertyName estadoDoc -NotePropertyValue 'C' -Force
$r = Api PUT "api/Pedido/$($pedA.Dato)" $hp
Esperar 'cancelar el pedido A (ya sin derivados) = 200' ($r.Status -eq 200) $r.Mensaje
$b = Api GET "api/DocumentoBase/3/$cot/Abierto?destino=4"
Esperar 'borrador final: solo la línea 1 con 9 abiertas (10 - 1 del pedido B)' ($b.Status -eq 200 -and $b.Dato.lineas.Count -eq 1 -and $b.Dato.lineas[0].baseLinea -eq 1 -and $b.Dato.lineas[0].cantidadAbierta -eq 9)

Write-Host ''
if ($script:Fallos -gt 0) { Write-Host "$script:Fallos verificación(es) fallaron" -ForegroundColor Red; exit 1 }
Write-Host 'Todas las verificaciones pasaron' -ForegroundColor Green
```

Expected: todas las líneas `OK`, exit code 0. Cualquier `FALLA` se investiga (no se ajusta la expectativa sin entender la causa); si falla por el **contrato real** de un endpoint (ruta o campo distinto), corregir el script, no el producto, y reportar qué difería.

- [ ] **Step 4: Limpieza (siempre, aunque la prueba haya fallado)**

```powershell
Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.CommandLine -like '*apismoke*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Remove-Item Env:\ConnectionStrings__API_DB, Env:\ASPNETCORE_URLS, Env:\ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
sqlcmd -S localhost -U sa -P $pw -C -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END"
sqlcmd -S localhost -U sa -P $pw -C -h -1 -W -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name LIKE 'API_DB%'"
```
Expected: la última consulta devuelve **solo `API_DB_TEST`**; puerto 5199 sin listener. `git status --short` en la API: solo `appsettings.json` modificado (la prueba de humo no deja archivos en el repo).

- [ ] **Step 5: Sin commit** (esta tarea solo verifica). Reportar: total de pruebas, resultado de cada línea del humo, y la confirmación de limpieza.

---

## Fase 4 — Web

> La Web no tiene proyecto de pruebas: cada tarea se verifica con `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` (0 errores) y la Task 17 cubre el recorrido manual completo. **Los 4 archivos ajenos** (`ArticuloCrearDTO.cs`, `Respuesta.cs`, `ArticulosController.cs`, `articulos.js`) no se tocan ni se stagean.

### Task 11: Cliente de la API y DTOs (`Web.ApiClient`) + registro

**Precondición:** Task 10 completa; `git -C C:\Users\migue\source\repos\angelm0508\Web status --short` muestra solo los 4 archivos ajenos; `dotnet build Web.slnx` → 0 errores.

**Files:**
- Create: `Web.ApiClient/Dtos/DocumentoBase/DocumentoBaseBorradorDTO.cs`, `Web.ApiClient/Clientes/IDocumentoBaseApiClient.cs`, `Web.ApiClient/Clientes/DocumentoBaseApiClient.cs`
- Modify: `Web.ApiClient/Dtos/Pedido/PedidoCrearDTO.cs`, `Web.UI/Program.cs`

**Interfaces:**
- Consumes: contrato JSON del endpoint de la Task 5; `ApiClientBase.GetAsync<T>`; `Respuesta<T>` (`Web.ApiClient.Dtos`).
- Produces: `IDocumentoBaseApiClient.ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino) : Task<Respuesta<DocumentoBaseBorradorDTO>>`; `Web.ApiClient.Dtos.Pedido.PedidoCrearDTO.Lineas : List<PedidoDetalleCrearDTO>`.

- [ ] **Step 1: Crear los DTOs** — `Web.ApiClient/Dtos/DocumentoBase/DocumentoBaseBorradorDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.DocumentoBase
{
    public class DocumentoBaseBorradorDTO
    {
        public int OrigenTipo { get; set; }
        public int OrigenEntry { get; set; }
        public int OrigenNumDoc { get; set; }
        public DocumentoBaseCabeceraDTO Cabecera { get; set; } = new();
        public List<DocumentoBaseLineaDTO> Lineas { get; set; } = new();
    }

    public class DocumentoBaseCabeceraDTO
    {
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public string? Comentario { get; set; }
    }

    public class DocumentoBaseLineaDTO
    {
        public int BaseTipo { get; set; }
        public int BaseEntry { get; set; }
        public int BaseLinea { get; set; }
        public string? CodArticulo { get; set; }
        public string? Descripcion { get; set; }
        public decimal CantidadOrigen { get; set; }
        public decimal CantidadAbierta { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public string? CodAlmacen { get; set; }
    }
}
```

- [ ] **Step 2: Cliente** — `Web.ApiClient/Clientes/IDocumentoBaseApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.DocumentoBase;

namespace Web.ApiClient.Clientes
{
    public interface IDocumentoBaseApiClient
    {
        /// <summary>Borrador del documento destino con las líneas abiertas del origen ("Copiar a").</summary>
        Task<Respuesta<DocumentoBaseBorradorDTO>> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);
    }
}
```

`Web.ApiClient/Clientes/DocumentoBaseApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.DocumentoBase;

namespace Web.ApiClient.Clientes
{
    public class DocumentoBaseApiClient : ApiClientBase, IDocumentoBaseApiClient
    {
        private const string Recurso = "api/DocumentoBase";

        public DocumentoBaseApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<DocumentoBaseBorradorDTO>> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino) =>
            GetAsync<DocumentoBaseBorradorDTO>($"{Recurso}/{tipoOrigen}/{entry}/Abierto?destino={tipoDestino}");
    }
}
```

- [ ] **Step 3: `PedidoCrearDTO.Lineas`** — en `Web.ApiClient/Dtos/Pedido/PedidoCrearDTO.cs` agregar al final de la clase (usar `using Web.ApiClient.Dtos.PedidoDetalle;` si el archivo no lo tiene):

```csharp
        /// <summary>Líneas del pedido (opcional): si vienen, la API guarda cabecera y líneas en una sola transacción ("Copiar a").</summary>
        public List<PedidoDetalleCrearDTO> Lineas { get; set; } = new();
```

- [ ] **Step 4: Registrar el cliente** — en `Web.UI/Program.cs`, junto al registro de `IExistenciaApiClient` (línea ~109), agregar:

```csharp
builder.Services.AddHttpClient<IDocumentoBaseApiClient, DocumentoBaseApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

- [ ] **Step 5: Verificar y commitear**

Run (PowerShell): `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` → 0 errores.

```bash
git add Web.ApiClient/Dtos/DocumentoBase/DocumentoBaseBorradorDTO.cs Web.ApiClient/Clientes/IDocumentoBaseApiClient.cs Web.ApiClient/Clientes/DocumentoBaseApiClient.cs Web.ApiClient/Dtos/Pedido/PedidoCrearDTO.cs Web.UI/Program.cs
git commit -m "feat(documento-base): cliente de la API para el borrador de Copiar a y lineas en PedidoCrearDTO

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Helpers compartidos en `site.js`

**Precondición:** Task 11 completa. `grep -n "obtenerListaDeSocio\|autocompletar:" Web.UI/wwwroot/js/site.js` muestra ambos miembros del objeto `App` (los helpers se insertan **antes** de `autocompletar:`).

**Files:** Modify: `Web.UI/wwwroot/js/site.js`

**Interfaces:**
- Produces (todos en el objeto `App`): `App.leerBorrador()`, `App.lineasDesdeBorrador(borrador, idInicial)` → `{ lineas, proximoId }`, `App.sinCamposInternos(linea)`, `App.abrirCopiaSiCorresponde(abrir)`, `App.botonCopiarA(tipoOrigen, entry, destinos)`; manejador delegado de `.btn-copiar-a`. Las tareas 13–16 usan estos nombres.

- [ ] **Step 1: Agregar los helpers** — insertar dentro del objeto `App`, justo antes de la propiedad `autocompletar: function (opciones) {` (respetando las comas entre miembros):

```js
    // ---- "Copiar a": líneas con documento base (compartido por pedidos.js, entregas.js y facturas.js) ----

    /** Borrador del documento base que el servidor embebe en #datosBorrador; null si el formulario no viene de "Copiar a". */
    leerBorrador: function () {
        const el = document.getElementById('datosBorrador');
        if (!el) return null;
        try { return JSON.parse(el.textContent); } catch (e) { return null; }
    },

    /**
     * Convierte las líneas abiertas del borrador en líneas locales del formulario (mismas propiedades que arma el panel
     * de línea). Los campos con prefijo "_" son solo de la interfaz y se quitan antes de enviar (sinCamposInternos).
     */
    lineasDesdeBorrador: function (borrador, idInicial) {
        const nombres = { 3: 'Cotización', 4: 'Pedido', 5: 'Entrega', 6: 'Factura' };
        let id = idInicial;
        const lineas = (borrador.lineas || []).map(l => {
            const cantidad = Number(l.cantidadAbierta);
            const origen = Number(l.cantidadOrigen);
            const proporcion = origen > 0 ? cantidad / origen : 1;
            const impuesto = l.impuesto != null ? Number((Number(l.impuesto) * proporcion).toFixed(2)) : 0;
            const precio = Number(l.precio ?? 0);
            const prctjeDesc = Number(l.prctjeDesc ?? 0);
            const bruto = cantidad * precio;
            const total = Number((bruto - bruto * (prctjeDesc / 100) + impuesto).toFixed(2));
            return {
                _id: id++,
                _abierta: cantidad,
                _baseTexto: `${nombres[l.baseTipo] ?? 'Documento'} ${borrador.origenNumDoc} · abierto: ${cantidad}`,
                BaseTipo: l.baseTipo, BaseEntry: l.baseEntry, BaseLinea: l.baseLinea,
                CodArticulo: l.codArticulo, Descripcion: l.descripcion,
                Cantidad: cantidad, Precio: l.precio, PrecioBruto: l.precioBruto, PrctjeDesc: l.prctjeDesc,
                CodigoImpuesto: l.codigoImpuesto, CodAlmacen: l.codAlmacen,
                Impuesto: impuesto, TotalLinea: total
            };
        });
        return { lineas, proximoId: id };
    },

    /** Quita los campos internos (prefijo "_") antes de enviar una línea a la API. */
    sinCamposInternos: function (linea) {
        return Object.fromEntries(Object.entries(linea).filter(([clave]) => !clave.startsWith('_')));
    },

    /** Si la URL trae ?baseTipo=&baseEntry= (viene de "Copiar a"), limpia la URL y llama a abrir(baseTipo, baseEntry). */
    abrirCopiaSiCorresponde: function (abrir) {
        const q = new URLSearchParams(window.location.search);
        const baseTipo = q.get('baseTipo');
        const baseEntry = q.get('baseEntry');
        if (!baseTipo || !baseEntry) return;
        history.replaceState(null, '', window.location.pathname);
        abrir(baseTipo, baseEntry);
    },

    /** Botón desplegable "Copiar a" de una fila de un listado. destinos: [{ nombre, url }]; '' si no hay destinos. */
    botonCopiarA: function (tipoOrigen, entry, destinos) {
        if (!destinos || destinos.length === 0) return '';
        const items = destinos.map(d =>
            `<li><a class="dropdown-item btn-copiar-a" href="#" data-url="${d.url}" data-tipo="${tipoOrigen}" data-entry="${entry}">${d.nombre}</a></li>`).join('');
        return `<div class="btn-group">
            <button class="btn btn-sm btn-outline-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown" aria-expanded="false" title="Copiar a"><i class="fa-solid fa-copy"></i></button>
            <ul class="dropdown-menu dropdown-menu-end">${items}</ul>
        </div>`;
    },

```

- [ ] **Step 2: Agregar el manejador delegado** — al final de `site.js` (después de la definición completa de `App`), asegurando que jQuery ya esté cargado (comprobar el orden de `<script>` en `Views/Shared/_Layout.cshtml`: jQuery antes que `site.js`; si `site.js` se carga antes, poner este bloque en un `$(function () { … })` del mismo archivo que ya espere a jQuery):

```js
// "Copiar a": navega al listado del destino, que abre su formulario precargado (App.abrirCopiaSiCorresponde).
$(document).on('click', '.btn-copiar-a', function (e) {
    e.preventDefault();
    window.location.href = `${$(this).data('url')}?baseTipo=${$(this).data('tipo')}&baseEntry=${$(this).data('entry')}`;
});
```

- [ ] **Step 3: Verificar** — `node --check Web.UI/wwwroot/js/site.js` → sin errores de sintaxis; `dotnet build Web.slnx …` → 0 errores.

- [ ] **Step 4: Commit**

```bash
git add Web.UI/wwwroot/js/site.js
git commit -m "feat(documento-base): helpers de Copiar a en site.js (borrador, lineas con base y boton)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Destino **Pedidos** — controlador, formulario y JS

**Precondición:** Task 12 completa. `grep -n "baseTipo" Web.UI/Controllers/PedidosController.cs` sin resultados.

**Files:**
- Modify: `Web.UI/Controllers/PedidosController.cs`, `Web.UI/Views/Pedidos/_Form.cshtml`, `Web.UI/wwwroot/js/pedidos.js`

**Interfaces:**
- Consumes: `IDocumentoBaseApiClient`, `DocumentoBaseBorradorDTO` (Task 11); helpers `App.*` (Task 12); `PedidoCrearDTO.Lineas`.
- Produces: `GET /Pedidos/FormularioCrear?baseTipo=&baseEntry=` (400 con texto plano si el borrador falla); formulario con `#datosBorrador`; `pedidos.js` que abre la copia automáticamente al llegar con `?baseTipo=&baseEntry=` y guarda cabecera + líneas en **una** petición.

- [ ] **Step 1: Controlador** — en `PedidosController.cs`:

a) `using Web.ApiClient.Dtos.DocumentoBase;`; campo y parámetro de constructor (al final de la lista):

```csharp
        private readonly IDocumentoBaseApiClient _documentoBase;
        // Tipo de objeto de Pedido (destino de "Copiar a").
        private const int TipoDestinoPedido = 4;
```
```csharp
            INumeracionDocumentoApiClient numeracion,
            IDocumentoBaseApiClient documentoBase)
        {
            // (asignaciones existentes sin cambio)
            _numeracion = numeracion;
            _documentoBase = documentoBase;
        }
```

b) Reemplazar `FormularioCrear`:

```csharp
        [HttpGet]
        [RequierePermiso("Pedidos", Accion.Agregar)]
        public async Task<IActionResult> FormularioCrear(int? baseTipo = null, int? baseEntry = null)
        {
            // "Copiar a": el borrador trae la cabecera y las líneas abiertas del documento origen.
            DocumentoBaseBorradorDTO? borrador = null;
            if (baseTipo.HasValue && baseEntry.HasValue)
            {
                var copia = await _documentoBase.ObtenerBorradorAsync(baseTipo.Value, baseEntry.Value, TipoDestinoPedido);
                if (!copia.Resultado || copia.Dato is null)
                    return BadRequest(copia.Mensaje);   // el JS lo muestra con App.mostrarError
                borrador = copia.Dato;
            }
            ViewBag.Borrador = borrador;

            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjPedido);
            ViewBag.SeriesPedido = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocPedido);

            // Serie preseleccionada: la que está configurada como "por defecto" para este objeto en
            // la pantalla "Numeración de documentos" (NumeracionDocumento.SerieDfct).
            var numeraciones = await _numeracion.ObtenerTodoAsync();
            var numeracionActual = (numeraciones.Dato ?? []).FirstOrDefault(n => n.CodigoObj == CodigoObjPedido && n.SubTipoDoc == SubTipoDocPedido);
            ViewBag.SerieDefecto = numeracionActual?.SerieDfct;

            ViewBag.EsEdicion = false;
            var dto = new PedidoCrearDTO { EstadoDoc = "A", TipoObjeto = "4" };
            if (borrador is not null)
            {
                dto.CodigoSn = borrador.Cabecera.CodigoSn;
                dto.NombreSn = borrador.Cabecera.NombreSn;
                dto.Direccion = borrador.Cabecera.Direccion;
                dto.MonedaDoc = borrador.Cabecera.MonedaDoc;
                dto.PrctjeDesc = borrador.Cabecera.PrctjeDesc;
                dto.PrctjeImpuesto = borrador.Cabecera.PrctjeImpuesto;
                dto.Comentario = borrador.Cabecera.Comentario;
            }
            return PartialView("_Form", dto);
        }
```

- [ ] **Step 2: Vista `Views/Pedidos/_Form.cshtml`**

a) Arriba (junto a `esEdicion` y `opcionesJson`) agregar `@using Web.ApiClient.Dtos.DocumentoBase` y:

```razor
@{
    var borrador = ViewBag.Borrador as DocumentoBaseBorradorDTO;
    bool conBase = borrador is not null;
}
```
b) Título del modal: `@(esEdicion ? "Editar pedido" : conBase ? $"Nuevo pedido (copia del documento {borrador!.OrigenNumDoc})" : "Nuevo pedido")`.

c) Campo del socio: el `value` del texto debe mostrarse también al copiar y el campo quedar de solo lectura:

```razor
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       readonly="@conBase"
                       value="@((esEdicion || conBase) && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
```
y en `NombreSn` y `Direccion` agregar `readonly="@conBase"` a sus `<input asp-for=… />`.

d) Junto al `<script id="datosSeriesPedido">` (dentro del `@if (!esEdicion)`), agregar el borrador:

```razor
    @if (conBase)
    {
        <script id="datosBorrador" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(borrador, opcionesJson).Replace("</", "<\\/"))
        </script>
    }
```

- [ ] **Step 3: `pedidos.js`** (cinco cambios)

1) Variable de estado, junto a las demás del bloque "Detalle" (`let lineasLocales = []; …`):

```js
    let borradorCopia = null;   // borrador de "Copiar a" cuando el formulario nace de otro documento
```

2) En `inicializarDetalle()`, justo **antes** de `if (esEdicionDetalle()) { cargarDetalleRemoto(); } else { pintarDetalle(); }`:

```js
        // "Copiar a": se cargan las líneas abiertas del documento origen y se bloquea la moneda.
        borradorCopia = esEdicionDetalle() ? null : App.leerBorrador();
        if (borradorCopia) {
            const copia = App.lineasDesdeBorrador(borradorCopia, proximoIdLocal);
            lineasLocales = copia.lineas;
            proximoIdLocal = copia.proximoId;
            $('#MonedaDoc').css('pointer-events', 'none').attr('tabindex', '-1');
        }
```

3) En `pintarDetalle()`, la celda de descripción pasa a mostrar la referencia al documento base:

```js
                    <td>${descripcion ?? ''}${linea._baseTexto ? `<div class="small text-muted">${linea._baseTexto}</div>` : ''}</td>
```
(reemplaza `<td>${descripcion ?? ''}</td>` en la plantilla de la fila).

4) En el guardado, al inicio de `if (!esEdicion) {` (antes de `const respuestaCabecera = await App.enviarJson('/Pedidos/Crear', …)`), agregar el camino de copia (una sola petición):

```js
            if (borradorCopia) {
                if (lineasLocales.length === 0) {
                    App.mostrarError('Agrega al menos una línea al documento.');
                    return;
                }
                datos.Lineas = lineasLocales.map(App.sinCamposInternos);
                const respuestaCopia = await App.enviarJson('/Pedidos/Crear', 'POST', datos);
                if (!respuestaCopia.resultado) {
                    App.mostrarError(respuestaCopia.mensaje);
                    return;
                }
                const sufijoCopia = respuestaCopia.numDoc != null ? ` No. documento: ${respuestaCopia.numDoc}.` : '';
                await App.mostrarExito(`Pedido creado correctamente.${sufijoCopia}`);
                bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
                recargarTabla();
                return;
            }
```

5) En el handler `#btnGuardarLinea`, rama local (`else { if (noLineaOriginalEnEdicion !== null) { … } else { … } … }`), reemplazar el bloque de edición local por:

```js
            if (noLineaOriginalEnEdicion !== null) {
                const actual = lineasLocales.find(l => l._id === noLineaOriginalEnEdicion);
                if (actual && actual.BaseTipo != null && actual.BaseTipo > 0) {
                    // línea copiada de otro documento: el artículo no cambia y la cantidad no pasa de lo abierto
                    if (datosForm.CodArticulo !== actual.CodArticulo) {
                        App.mostrarError('El artículo de una línea copiada no se puede cambiar.');
                        return;
                    }
                    if (!(Number(datosForm.Cantidad) > 0)) {
                        App.mostrarError('La cantidad debe ser mayor que cero.');
                        return;
                    }
                    if (Number(datosForm.Cantidad) > actual._abierta) {
                        App.mostrarError(`La cantidad no puede superar la cantidad abierta (${actual._abierta}).`);
                        return;
                    }
                    lineasLocales = lineasLocales.map(l => l._id === actual._id ? { ...actual, ...datosForm, _id: actual._id } : l);
                } else {
                    lineasLocales = lineasLocales.map(l => l._id === noLineaOriginalEnEdicion ? { ...datosForm, _id: l._id } : l);
                }
            } else {
                lineasLocales.push({ ...datosForm, _id: proximoIdLocal++ });
            }
```
(El `else` interno de "línea nueva" conserva su comportamiento anterior.)

6) Al **final** del callback `$(function () { … });` (después de todos los handlers), abrir la copia si la URL la trae:

```js
    // "Copiar a": llega desde otro listado con ?baseTipo=&baseEntry= y abre el formulario precargado.
    App.abrirCopiaSiCorresponde(async (baseTipo, baseEntry) => {
        try {
            const html = await $.get('/Pedidos/FormularioCrear', { baseTipo, baseEntry });
            abrirModal(html);
        } catch (xhr) {
            App.mostrarError((xhr && xhr.responseText) || 'No se pudo copiar el documento.');
        }
    });
```

- [ ] **Step 4: Verificar** — `node --check Web.UI/wwwroot/js/pedidos.js`; `dotnet build Web.slnx …` → 0 errores y 0 advertencias nuevas.

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/PedidosController.cs Web.UI/Views/Pedidos/_Form.cshtml Web.UI/wwwroot/js/pedidos.js
git commit -m "feat(documento-base): formulario de Pedido precargado desde Copiar a con guardado en una sola peticion

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Destino **Entregas** — controlador, formulario y JS

**Precondición:** Task 13 completa. `grep -n "baseTipo" Web.UI/Controllers/EntregasController.cs` sin resultados; `grep -n "datos.Lineas = lineasLocales.map" Web.UI/wwwroot/js/entregas.js` encuentra la asignación de líneas del alta (se reemplaza en el Step 3).

**Files:**
- Modify: `Web.UI/Controllers/EntregasController.cs`, `Web.UI/Views/Entregas/_Form.cshtml`, `Web.UI/wwwroot/js/entregas.js`

**Interfaces:**
- Consumes: `IDocumentoBaseApiClient`, `DocumentoBaseBorradorDTO` (Task 11); helpers `App.*` (Task 12).
- Produces: `GET /Entregas/FormularioCrear?baseTipo=&baseEntry=` (400 con texto plano si el borrador falla); formulario con `#datosBorrador`; `entregas.js` que abre la copia automáticamente al llegar con `?baseTipo=&baseEntry=` y guarda cabecera y líneas (ya iban en una sola petición) con las referencias al documento base.

- [ ] **Step 1: Controlador** — en `EntregasController.cs`:

a) `using Web.ApiClient.Dtos.DocumentoBase;`; campo, constante y parámetro de constructor (al final de la lista):

```csharp
        private readonly IDocumentoBaseApiClient _documentoBase;
        // Tipo de objeto de Entrega (destino de "Copiar a").
        private const int TipoDestinoEntrega = 5;
```
```csharp
            INumeracionDocumentoApiClient numeracion,
            IDocumentoBaseApiClient documentoBase)
        {
            // (asignaciones existentes sin cambio)
            _documentoBase = documentoBase;
        }
```

b) Reemplazar `FormularioCrear`:

```csharp
        [HttpGet]
        [RequierePermiso("Entregas", Accion.Agregar)]
        public async Task<IActionResult> FormularioCrear(int? baseTipo = null, int? baseEntry = null)
        {
            // "Copiar a": el borrador trae la cabecera y las líneas abiertas del documento origen.
            DocumentoBaseBorradorDTO? borrador = null;
            if (baseTipo.HasValue && baseEntry.HasValue)
            {
                var copia = await _documentoBase.ObtenerBorradorAsync(baseTipo.Value, baseEntry.Value, TipoDestinoEntrega);
                if (!copia.Resultado || copia.Dato is null)
                    return BadRequest(copia.Mensaje);   // el JS lo muestra con App.mostrarError
                borrador = copia.Dato;
            }
            ViewBag.Borrador = borrador;

            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjEntrega);
            ViewBag.SeriesEntrega = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocEntrega);

            // Serie preseleccionada: la que está configurada como "por defecto" para este objeto en
            // la pantalla "Numeración de documentos" (NumeracionDocumento.SerieDfct).
            var numeraciones = await _numeracion.ObtenerTodoAsync();
            var numeracionActual = (numeraciones.Dato ?? []).FirstOrDefault(n => n.CodigoObj == CodigoObjEntrega && n.SubTipoDoc == SubTipoDocEntrega);
            ViewBag.SerieDefecto = numeracionActual?.SerieDfct;

            ViewBag.EsEdicion = false;
            var dto = new EntregaCrearDTO { EstadoDoc = "A", TipoObjeto = "5" };
            if (borrador is not null)
            {
                dto.CodigoSn = borrador.Cabecera.CodigoSn;
                dto.NombreSn = borrador.Cabecera.NombreSn;
                dto.Direccion = borrador.Cabecera.Direccion;
                dto.MonedaDoc = borrador.Cabecera.MonedaDoc;
                dto.PrctjeDesc = borrador.Cabecera.PrctjeDesc;
                dto.PrctjeImpuesto = borrador.Cabecera.PrctjeImpuesto;
                dto.Comentario = borrador.Cabecera.Comentario;
            }
            return PartialView("_Form", dto);
        }
```
(si el `FormularioCrear` actual de este archivo difiere en algún nombre de constante o de `ViewBag`, conservar los nombres propios del archivo: solo se agregan el bloque del borrador al inicio y el rellenado del `dto` al final.)

- [ ] **Step 2: Vista `Views/Entregas/_Form.cshtml`**

a) Arriba (junto a `esEdicion` y `opcionesJson`) agregar `@using Web.ApiClient.Dtos.DocumentoBase` y:

```razor
@{
    var borrador = ViewBag.Borrador as DocumentoBaseBorradorDTO;
    bool conBase = borrador is not null;
}
```
b) Título del modal: `@(esEdicion ? "Editar entrega" : conBase ? $"Nueva entrega (copia del documento {borrador!.OrigenNumDoc})" : "Nueva entrega")`.

c) Campo del socio: el `value` del texto debe mostrarse también al copiar y el campo quedar de solo lectura:

```razor
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       readonly="@conBase"
                       value="@((esEdicion || conBase) && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
```
y en `NombreSn` y `Direccion` agregar `readonly="@conBase"` a sus `<input asp-for=… />`.

d) Junto al `<script id="datosSeriesEntrega">` (dentro del `@if (!esEdicion)`), agregar el borrador:

```razor
    @if (conBase)
    {
        <script id="datosBorrador" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(borrador, opcionesJson).Replace("</", "<\\/"))
        </script>
    }
```

- [ ] **Step 3: `entregas.js`** (seis cambios)

1) Variable de estado, junto a las demás del bloque "Detalle" (`let lineasLocales = []; …`):

```js
    let borradorCopia = null;   // borrador de "Copiar a" cuando el formulario nace de otro documento
```

2) En `inicializarDetalle()`, justo **antes** de `if (esEdicionDetalle()) { cargarDetalleRemoto(); } else { pintarDetalle(); }`:

```js
        // "Copiar a": se cargan las líneas abiertas del documento origen y se bloquea la moneda.
        borradorCopia = esEdicionDetalle() ? null : App.leerBorrador();
        if (borradorCopia) {
            const copia = App.lineasDesdeBorrador(borradorCopia, proximoIdLocal);
            lineasLocales = copia.lineas;
            proximoIdLocal = copia.proximoId;
            $('#MonedaDoc').css('pointer-events', 'none').attr('tabindex', '-1');
        }
```

3) En `pintarDetalle()`, la celda de descripción pasa a mostrar la referencia al documento base:

```js
                    <td>${descripcion ?? ''}${linea._baseTexto ? `<div class="small text-muted">${linea._baseTexto}</div>` : ''}</td>
```
(reemplaza `<td>${descripcion ?? ''}</td>` en la plantilla de la fila).

4) En el guardado del alta, reemplazar la asignación de líneas para quitar los campos internos:

```js
            datos.Lineas = lineasLocales.map(App.sinCamposInternos);
```
(sustituye a `datos.Lineas = lineasLocales.map(({ _id, ...linea }) => linea);`; la validación de almacén por línea que ya existe se conserva.)

5) En el handler `#btnGuardarLinea`, rama local (`else { if (noLineaOriginalEnEdicion !== null) { … } else { … } … }`), reemplazar el bloque de edición local por:

```js
            if (noLineaOriginalEnEdicion !== null) {
                const actual = lineasLocales.find(l => l._id === noLineaOriginalEnEdicion);
                if (actual && actual.BaseTipo != null && actual.BaseTipo > 0) {
                    // línea copiada de otro documento: el artículo no cambia y la cantidad no pasa de lo abierto
                    if (datosForm.CodArticulo !== actual.CodArticulo) {
                        App.mostrarError('El artículo de una línea copiada no se puede cambiar.');
                        return;
                    }
                    if (!(Number(datosForm.Cantidad) > 0)) {
                        App.mostrarError('La cantidad debe ser mayor que cero.');
                        return;
                    }
                    if (Number(datosForm.Cantidad) > actual._abierta) {
                        App.mostrarError(`La cantidad no puede superar la cantidad abierta (${actual._abierta}).`);
                        return;
                    }
                    lineasLocales = lineasLocales.map(l => l._id === actual._id ? { ...actual, ...datosForm, _id: actual._id } : l);
                } else {
                    lineasLocales = lineasLocales.map(l => l._id === noLineaOriginalEnEdicion ? { ...datosForm, _id: l._id } : l);
                }
            } else {
                lineasLocales.push({ ...datosForm, _id: proximoIdLocal++ });
            }
```
(el `else` interno de "línea nueva" conserva su comportamiento anterior.)

6) Al **final** del callback `$(function () { … });` (después de todos los handlers), abrir la copia si la URL la trae:

```js
    // "Copiar a": llega desde otro listado con ?baseTipo=&baseEntry= y abre el formulario precargado.
    App.abrirCopiaSiCorresponde(async (baseTipo, baseEntry) => {
        try {
            const html = await $.get('/Entregas/FormularioCrear', { baseTipo, baseEntry });
            abrirModal(html);
        } catch (xhr) {
            App.mostrarError((xhr && xhr.responseText) || 'No se pudo copiar el documento.');
        }
    });
```

- [ ] **Step 4: Verificar** — `node --check Web.UI/wwwroot/js/entregas.js`; `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` → 0 errores y 0 advertencias nuevas.

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/EntregasController.cs Web.UI/Views/Entregas/_Form.cshtml Web.UI/wwwroot/js/entregas.js
git commit -m "feat(documento-base): formulario de Entrega precargado desde Copiar a

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Destino **Facturas** — controlador, formulario y JS

**Precondición:** Task 14 completa. `grep -n "baseTipo" Web.UI/Controllers/FacturasController.cs` sin resultados; `grep -n "datos.Lineas = lineasLocales.map" Web.UI/wwwroot/js/facturas.js` encuentra la asignación de líneas del alta (se reemplaza en el Step 3).

**Files:**
- Modify: `Web.UI/Controllers/FacturasController.cs`, `Web.UI/Views/Facturas/_Form.cshtml`, `Web.UI/wwwroot/js/facturas.js`

**Interfaces:**
- Consumes: `IDocumentoBaseApiClient`, `DocumentoBaseBorradorDTO` (Task 11); helpers `App.*` (Task 12).
- Produces: `GET /Facturas/FormularioCrear?baseTipo=&baseEntry=` (400 con texto plano si el borrador falla); formulario con `#datosBorrador`; `facturas.js` que abre la copia automáticamente al llegar con `?baseTipo=&baseEntry=` y guarda cabecera y líneas (ya iban en una sola petición) con las referencias al documento base.

- [ ] **Step 1: Controlador** — en `FacturasController.cs`:

a) `using Web.ApiClient.Dtos.DocumentoBase;`; campo, constante y parámetro de constructor (al final de la lista):

```csharp
        private readonly IDocumentoBaseApiClient _documentoBase;
        // Tipo de objeto de Factura (destino de "Copiar a").
        private const int TipoDestinoFactura = 6;
```
```csharp
            INumeracionDocumentoApiClient numeracion,
            IDocumentoBaseApiClient documentoBase)
        {
            // (asignaciones existentes sin cambio)
            _documentoBase = documentoBase;
        }
```

b) Reemplazar `FormularioCrear`:

```csharp
        [HttpGet]
        [RequierePermiso("Facturas", Accion.Agregar)]
        public async Task<IActionResult> FormularioCrear(int? baseTipo = null, int? baseEntry = null)
        {
            // "Copiar a": el borrador trae la cabecera y las líneas abiertas del documento origen.
            DocumentoBaseBorradorDTO? borrador = null;
            if (baseTipo.HasValue && baseEntry.HasValue)
            {
                var copia = await _documentoBase.ObtenerBorradorAsync(baseTipo.Value, baseEntry.Value, TipoDestinoFactura);
                if (!copia.Resultado || copia.Dato is null)
                    return BadRequest(copia.Mensaje);   // el JS lo muestra con App.mostrarError
                borrador = copia.Dato;
            }
            ViewBag.Borrador = borrador;

            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjFactura);
            ViewBag.SeriesFactura = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocFactura);

            // Serie preseleccionada: la que está configurada como "por defecto" para este objeto en
            // la pantalla "Numeración de documentos" (NumeracionDocumento.SerieDfct).
            var numeraciones = await _numeracion.ObtenerTodoAsync();
            var numeracionActual = (numeraciones.Dato ?? []).FirstOrDefault(n => n.CodigoObj == CodigoObjFactura && n.SubTipoDoc == SubTipoDocFactura);
            ViewBag.SerieDefecto = numeracionActual?.SerieDfct;

            ViewBag.EsEdicion = false;
            var dto = new FacturaCrearDTO { EstadoDoc = "A", TipoObjeto = "6" };
            if (borrador is not null)
            {
                dto.CodigoSn = borrador.Cabecera.CodigoSn;
                dto.NombreSn = borrador.Cabecera.NombreSn;
                dto.Direccion = borrador.Cabecera.Direccion;
                dto.MonedaDoc = borrador.Cabecera.MonedaDoc;
                dto.PrctjeDesc = borrador.Cabecera.PrctjeDesc;
                dto.PrctjeImpuesto = borrador.Cabecera.PrctjeImpuesto;
                dto.Comentario = borrador.Cabecera.Comentario;
            }
            return PartialView("_Form", dto);
        }
```
(si el `FormularioCrear` actual de este archivo difiere en algún nombre de constante o de `ViewBag`, conservar los nombres propios del archivo: solo se agregan el bloque del borrador al inicio y el rellenado del `dto` al final.)

- [ ] **Step 2: Vista `Views/Facturas/_Form.cshtml`**

a) Arriba (junto a `esEdicion` y `opcionesJson`) agregar `@using Web.ApiClient.Dtos.DocumentoBase` y:

```razor
@{
    var borrador = ViewBag.Borrador as DocumentoBaseBorradorDTO;
    bool conBase = borrador is not null;
}
```
b) Título del modal: `@(esEdicion ? "Editar factura" : conBase ? $"Nueva factura (copia del documento {borrador!.OrigenNumDoc})" : "Nueva factura")`.

c) Campo del socio: el `value` del texto debe mostrarse también al copiar y el campo quedar de solo lectura:

```razor
                <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off"
                       readonly="@conBase"
                       value="@((esEdicion || conBase) && !string.IsNullOrEmpty(Model.CodigoSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : "")" />
```
y en `NombreSn` y `Direccion` agregar `readonly="@conBase"` a sus `<input asp-for=… />`.

d) Junto al `<script id="datosSeriesFactura">` (dentro del `@if (!esEdicion)`), agregar el borrador:

```razor
    @if (conBase)
    {
        <script id="datosBorrador" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(borrador, opcionesJson).Replace("</", "<\\/"))
        </script>
    }
```

- [ ] **Step 3: `facturas.js`** (seis cambios)

1) Variable de estado, junto a las demás del bloque "Detalle" (`let lineasLocales = []; …`):

```js
    let borradorCopia = null;   // borrador de "Copiar a" cuando el formulario nace de otro documento
```

2) En `inicializarDetalle()`, justo **antes** de `if (esEdicionDetalle()) { cargarDetalleRemoto(); } else { pintarDetalle(); }`:

```js
        // "Copiar a": se cargan las líneas abiertas del documento origen y se bloquea la moneda.
        borradorCopia = esEdicionDetalle() ? null : App.leerBorrador();
        if (borradorCopia) {
            const copia = App.lineasDesdeBorrador(borradorCopia, proximoIdLocal);
            lineasLocales = copia.lineas;
            proximoIdLocal = copia.proximoId;
            $('#MonedaDoc').css('pointer-events', 'none').attr('tabindex', '-1');
        }
```

3) En `pintarDetalle()`, la celda de descripción pasa a mostrar la referencia al documento base:

```js
                    <td>${descripcion ?? ''}${linea._baseTexto ? `<div class="small text-muted">${linea._baseTexto}</div>` : ''}</td>
```
(reemplaza `<td>${descripcion ?? ''}</td>` en la plantilla de la fila).

4) En el guardado del alta, reemplazar la asignación de líneas para quitar los campos internos:

```js
            datos.Lineas = lineasLocales.map(App.sinCamposInternos);
```
(sustituye a `datos.Lineas = lineasLocales.map(({ _id, ...linea }) => linea);`; la validación de almacén por línea que ya existe se conserva.)

5) En el handler `#btnGuardarLinea`, rama local (`else { if (noLineaOriginalEnEdicion !== null) { … } else { … } … }`), reemplazar el bloque de edición local por:

```js
            if (noLineaOriginalEnEdicion !== null) {
                const actual = lineasLocales.find(l => l._id === noLineaOriginalEnEdicion);
                if (actual && actual.BaseTipo != null && actual.BaseTipo > 0) {
                    // línea copiada de otro documento: el artículo no cambia y la cantidad no pasa de lo abierto
                    if (datosForm.CodArticulo !== actual.CodArticulo) {
                        App.mostrarError('El artículo de una línea copiada no se puede cambiar.');
                        return;
                    }
                    if (!(Number(datosForm.Cantidad) > 0)) {
                        App.mostrarError('La cantidad debe ser mayor que cero.');
                        return;
                    }
                    if (Number(datosForm.Cantidad) > actual._abierta) {
                        App.mostrarError(`La cantidad no puede superar la cantidad abierta (${actual._abierta}).`);
                        return;
                    }
                    lineasLocales = lineasLocales.map(l => l._id === actual._id ? { ...actual, ...datosForm, _id: actual._id } : l);
                } else {
                    lineasLocales = lineasLocales.map(l => l._id === noLineaOriginalEnEdicion ? { ...datosForm, _id: l._id } : l);
                }
            } else {
                lineasLocales.push({ ...datosForm, _id: proximoIdLocal++ });
            }
```
(el `else` interno de "línea nueva" conserva su comportamiento anterior.)

6) Al **final** del callback `$(function () { … });` (después de todos los handlers), abrir la copia si la URL la trae:

```js
    // "Copiar a": llega desde otro listado con ?baseTipo=&baseEntry= y abre el formulario precargado.
    App.abrirCopiaSiCorresponde(async (baseTipo, baseEntry) => {
        try {
            const html = await $.get('/Facturas/FormularioCrear', { baseTipo, baseEntry });
            abrirModal(html);
        } catch (xhr) {
            App.mostrarError((xhr && xhr.responseText) || 'No se pudo copiar el documento.');
        }
    });
```

- [ ] **Step 4: Verificar** — `node --check Web.UI/wwwroot/js/facturas.js`; `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` → 0 errores y 0 advertencias nuevas.

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/FacturasController.cs Web.UI/Views/Facturas/_Form.cshtml Web.UI/wwwroot/js/facturas.js
git commit -m "feat(documento-base): formulario de Factura precargado desde Copiar a

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 16: Botón **"Copiar a"** en los listados de Cotizaciones, Pedidos y Entregas

**Precondición:** Task 15 completa. En cada `Views/{Cotizaciones,Pedidos,Entregas}/Index.cshtml` existe un `<script id="datosPermisos…" type="application/json">` (verificar con grep) y en su JS la columna de acciones se arma con `render: entry => …`.

**Files:**
- Modify: `Web.UI/Views/Cotizaciones/Index.cshtml`, `Views/Pedidos/Index.cshtml`, `Views/Entregas/Index.cshtml`
- Modify: `Web.UI/wwwroot/js/cotizaciones.js`, `pedidos.js`, `entregas.js`

**Interfaces:**
- Consumes: `App.botonCopiarA(tipoOrigen, entry, destinos)` (Task 12); las rutas `/Pedidos`, `/Entregas`, `/Facturas` (Tasks 13–15).
- Produces: en cada fila no cancelada de Cotizaciones (origen 3: Pedido, Entrega, Factura de venta), Pedidos (origen 4: Entrega, Factura de venta) y Entregas (origen 5: Factura de venta) un desplegable "Copiar a" con solo los destinos para los que el usuario tiene **Ver y Agregar**.

- [ ] **Step 1: Permisos en las vistas** — en cada `Index.cshtml` agregar al JSON de permisos existente una propiedad `copiarA` con los destinos que corresponden a ese origen (ejemplo para **Cotizaciones**, que ofrece los tres; Pedidos incluye solo `entrega` y `factura`; Entregas solo `factura`):

```razor
@{
    string PuedeCopiar(string modulo) => User.TienePermiso(modulo, Accion.Ver) && User.TienePermiso(modulo, Accion.Agregar) ? "true" : "false";
}
```
(al inicio del archivo, tras `ViewData["Title"]`) y en el JSON, tras `"eliminar": …`:

```
      "copiarA": { "pedido": @PuedeCopiar("Pedidos"), "entrega": @PuedeCopiar("Entregas"), "factura": @PuedeCopiar("Facturas") }
```
(añadir la coma que corresponda detrás de la propiedad anterior.)

- [ ] **Step 2: JS de cada listado** — en `cotizaciones.js`, `pedidos.js` y `entregas.js`:

a) Después de leer `permisos`, definir los destinos del origen (ejemplo Cotizaciones; en `pedidos.js` quitar `pedido`; en `entregas.js` dejar solo `factura`):

```js
    const destinosCopia = [
        permisos.copiarA && permisos.copiarA.pedido && { nombre: 'Pedido', url: '/Pedidos' },
        permisos.copiarA && permisos.copiarA.entrega && { nombre: 'Entrega', url: '/Entregas' },
        permisos.copiarA && permisos.copiarA.factura && { nombre: 'Factura de venta', url: '/Facturas' }
    ].filter(Boolean);
```
b) En la columna de acciones, cambiar `render: entry => \`…\`` por `render: (entry, t, row) => \`…\`` y agregar al inicio del contenido el botón (tipo de origen: Cotizaciones `3`, Pedidos `4`, Entregas `5`):

```js
                    ${row.estadoDoc === 'C' || row.cancelado === 'S' ? '' : App.botonCopiarA(3, entry, destinosCopia)}
```

- [ ] **Step 3: Verificar** — `node --check` de los tres JS; `dotnet build Web.slnx …` → 0 errores.

- [ ] **Step 4: Commit**

```bash
git add Web.UI/Views/Cotizaciones/Index.cshtml Web.UI/Views/Pedidos/Index.cshtml Web.UI/Views/Entregas/Index.cshtml Web.UI/wwwroot/js/cotizaciones.js Web.UI/wwwroot/js/pedidos.js Web.UI/wwwroot/js/entregas.js
git commit -m "feat(documento-base): boton Copiar a en los listados de Cotizaciones, Pedidos y Entregas

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Verificación integral (API + Web contra una base temporal)

**Precondición:** Tasks 1–16 completas; ambos repos sin cambios sin commit salvo `appsettings.json` (API) y los 4 archivos ajenos (Web).

**Files:** ninguno (verificación en el scratchpad; no se commitea nada).

- [ ] **Step 1: Builds finales**

PowerShell: `dotnet test API.sln -p:BaseOutputPath="…\apitest\"` en la API → 0 fallos (igual que la Task 10); `dotnet build Web.slnx -p:BaseOutputPath="…\webbuild\"` en la Web → **0 errores y 0 advertencias**.

- [ ] **Step 2: Entorno temporal con Web**

Repetir el Step 2 de la Task 10 con la base `API_DB_COPIA_B` y la API en `http://localhost:5199`; luego compilar y levantar la Web en `http://localhost:5200` apuntando a esa API (`$env:ApiSettings__BaseUrl = 'http://localhost:5199'`, `$env:ASPNETCORE_URLS = 'http://localhost:5200'`, `dotnet build Web.UI\Web.UI.csproj -p:BaseOutputPath="$SP\webcheck\"` y ejecutar la DLL resultante). Cargar los datos de la prueba de humo (clientes, artículos con existencias y una cotización de 2 líneas) corriendo la primera mitad de `smoke-copiar-a.ps1` (hasta crear la cotización) o creándolos por la API.

- [ ] **Step 3: Recorrido en el navegador** (Browser pane, sesión `admin`/`Admin123!`, ventana de 1440×900) — verificar y anotar cada resultado:

1. **Cotizaciones**: cada fila no cancelada muestra el botón "Copiar a" con "Pedido", "Entrega" y "Factura de venta".
2. "Copiar a → Pedido": navega a `/Pedidos` y se abre el modal "Nuevo pedido (copia del documento N)" con socio, nombre, dirección y moneda bloqueados, y las líneas abiertas con su texto "Cotización N · abierto: X"; la URL queda limpia (sin `baseTipo`).
3. Bajar la cantidad de una línea a 4, quitar la otra línea y guardar → "Pedido creado correctamente" y el pedido aparece en el listado con las líneas correctas.
4. Volver a "Copiar a → Pedido" desde la misma cotización: la línea muestra la cantidad abierta restante (10 − 4 = 6). Intentar editar la línea a 7 → error "La cantidad no puede superar la cantidad abierta (6)". Cambiar el artículo de una línea copiada → error "El artículo de una línea copiada no se puede cambiar."
5. Desde el **Pedido** creado: "Copiar a → Entrega" (y luego "Entrega → Factura de venta"): cada formulario abre precargado; una línea sin almacén exige elegirlo; al guardar la Entrega la existencia del artículo baja (Existencias) y al guardar la Factura desde la Entrega **no** vuelve a bajar.
6. Desde la **Cotización**: "Copiar a → Factura de venta" por la otra línea: al guardar, la existencia de ese artículo baja.
7. Intentar **eliminar** la cotización (botón papelera) → mensaje "No se puede eliminar el documento: lo usan …"; intentar editar el socio de la cotización o poner su estado en Cancelado → mensaje equivalente. Editar una línea consumida → "No se puede modificar la línea N: la usan …".
8. **Cancelar** la entrega ya facturada → mensaje "…lo usan Factura …"; cancelar primero la factura y luego la entrega → ambas se cancelan y la existencia vuelve al valor anterior.
9. Con un usuario de plantilla **Vendedor** (sin Agregar en Pedidos): "Copiar a" no ofrece "Pedido".
10. Consola del navegador sin errores de JavaScript durante todo el recorrido (`read_console_messages`).

Expected: todos los puntos se comportan como se describe; cualquier desvío se reporta con captura y se corrige en la tarea que corresponda (no se da por buena la verificación con desvíos abiertos).

- [ ] **Step 4: Limpieza (siempre)** — detener la API y la Web (por PID propio), borrar variables de entorno de la sesión, eliminar `API_DB_COPIA_B` (`ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE …`) y comprobar que `SELECT name FROM sys.databases WHERE name LIKE 'API_DB%'` devuelve solo `API_DB_TEST`, que los puertos 5199 y 5200 no tienen listener y que `git status --short` de ambos repos solo muestra `appsettings.json` (API) y los 4 archivos ajenos (Web).

- [ ] **Step 5: Sin commit.** Reportar el resultado de cada punto del recorrido y la confirmación de limpieza.

---

## Cierre

Tras la Task 17: revisión final de rama (API y Web) con el skill `superpowers:requesting-code-review` y, si queda limpia, `superpowers:finishing-a-development-branch` (integración a decidir por el usuario; **sin push**). El plan documentos-base-compras (pausado) puede reutilizar `DocumentoBaseService` como patrón, pero queda fuera de este plan.
