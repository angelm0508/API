# Generar documentos de compra en base a otro documento — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir generar una Entrega de Compra en base a uno o varios Pedidos de Compra
abiertos, y una Factura de Compra en base a una o varias Entregas de Compra o Pedidos de Compra
abiertos — sin repostear inventario cuando la línea de Factura de Compra viene de una Entrega.

**Architecture:** Cantidad abierta calculada siempre en vivo (nunca guardada) sumando el consumo
de las tablas detalle que ya usan `BaseTipo`/`BaseEntry`/`BaseLinea`. Dos endpoints de lectura
nuevos (`Abiertos`) alimentan un picker Web "Copiar desde..." que agrega líneas al mismo arreglo
`lineasLocales` que ya arma cada documento antes de guardarlo. Validación server-side reutilizable
en `EntregaCompraDomain`/`FacturaCompraDomain` antes de asentar; un solo cambio de una línea de
código en el filtro de inventario de `FacturaCompraDomain`.

**Tech Stack:** .NET 7 (API, EF Core 7, AutoMapper, xUnit + Moq), .NET 8 (Web MVC), SQL Server
(`API_DB_TEST`), jQuery + Bootstrap 5 + SweetAlert2 (Web).

**Spec:** `docs/superpowers/specs/2026-09-13-documentos-base-compras-design.md`

## Global Constraints

- Repos: API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`), Web en
  `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad de git: `panchoman08`.
- **Sin push hasta aprobación final del usuario.**
- `git add` con rutas de archivo explícitas — **nunca** `git add -A` ni `git add .`.
- `API.Service.WebApi/appsettings.json` puede aparecer modificado localmente (connection string
  real) — **nunca** commitearlo.
- Compilar siempre con output redirigido fuera del árbol para no chocar con locks de Visual
  Studio: API con
  `-p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\{apibuild|apitest}\"`,
  Web con `...\webbuild\`.
- Sin cambios de esquema (DDL) — `BaseTipo`/`BaseEntry`/`BaseLinea` ya existen en
  `PedidoCompraDetalle`, `EntregaCompraDetalle`, `FacturaCompraDetalle`.
- `CodigoObj` / `TipoObjeto` (string) de cada documento: Pedido Compra = `"11"`, Entrega Compra =
  `"12"`, Factura Compra = `"13"`. `BaseTipo` (int, en la línea destino) usa los mismos números:
  `11` = base Pedido Compra, `12` = base Entrega Compra. Oferta Compra (`10`) queda fuera de
  alcance en todo este plan.
- Cada tarea termina con `dotnet build` y la suite de tests en verde antes de pasar a la
  siguiente. Verificar el número exacto de tests en la baseline (`dotnet test` sobre `HEAD` antes
  de la Tarea 1) — no asumir un número fijo, puede haber cambiado desde que se escribió este plan.
- Cada tarea de API es un commit en `desarrollo`; cada tarea de Web es un commit en `main`.

---

## Fase 1 — API: cantidad abierta

### Task 1: Tipos de dominio compartidos y `PedidoCompraDomain.ObtenerAbiertosAsync`

**Files:**
- Create: `API.Domain.Interface/DocumentoAbierto.cs`
- Modify: `API.Domain.Interface/IPedidoCompraDomain.cs`
- Modify: `API.Domain.Core/PedidoCompraDomain.cs`
- Modify: `API.Service.WebApi/Startup.cs` (nuevas dependencias del constructor de `PedidoCompraDomain`)
- Test: `API.Service.WebApi.Tests/Domain/PedidoCompraDomainTests.cs`

**Interfaces:**
- Produces: `record LineaAbierta(int NoLinea, string CodArticulo, string? Descripcion, decimal
  Cantidad, decimal CantidadAbierta, decimal? Precio, string? CodAlmacen, string? CodigoImpuesto)`;
  `record DocumentoAbierto(int Entry, int NumDoc, int Serie, DateTime? FechaDoc, IReadOnlyList<LineaAbierta> Lineas)`;
  `IPedidoCompraDomain.ObtenerAbiertosAsync(string codigoSn) : Task<IEnumerable<DocumentoAbierto>>`.

- [ ] **Step 1: Crear los tipos de dominio compartidos**

`API.Domain.Interface/DocumentoAbierto.cs`:

```csharp
namespace API.Domain.Interface
{
    /// <summary>
    /// Una línea de un documento origen (Pedido de Compra o Entrega de Compra) con cantidad
    /// pendiente de copiar a un documento destino ("Copiar desde..."). CantidadAbierta se calcula
    /// siempre en el momento -- nunca se persiste -- restando lo ya consumido por líneas activas
    /// (encabezado no cancelado) de los documentos destino posibles.
    /// </summary>
    public record LineaAbierta(
        int NoLinea,
        string CodArticulo,
        string? Descripcion,
        decimal Cantidad,
        decimal CantidadAbierta,
        decimal? Precio,
        string? CodAlmacen,
        string? CodigoImpuesto);

    /// <summary>Un documento origen con al menos una línea de cantidad abierta > 0.</summary>
    public record DocumentoAbierto(
        int Entry,
        int NumDoc,
        int Serie,
        DateTime? FechaDoc,
        IReadOnlyList<LineaAbierta> Lineas);
}
```

- [ ] **Step 2: Escribir el test que falla**

Agregar a `API.Service.WebApi.Tests/Domain/PedidoCompraDomainTests.cs` (después de la clase, junto
a los demás `[Fact]`; ampliar el constructor de la clase de test primero -- ver Step 2b):

```csharp
[Fact]
public async Task ObtenerAbiertosAsync_SinConsumo_DevuelveCantidadTotalComoAbierta()
{
    var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N", FechaDoc = new DateTime(2026, 9, 1) };
    var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Descripcion = "Articulo 1", Cantidad = 10m, Precio = 25m, CodAlmacen = "01", CodigoImpuesto = "IVA" };
    _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
    _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
    _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra>().AsAsyncQueryable());
    _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable());
    _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

    Assert.Single(resultado);
    Assert.Equal(1, resultado[0].Entry);
    Assert.Single(resultado[0].Lineas);
    Assert.Equal(10m, resultado[0].Lineas[0].CantidadAbierta);
}

[Fact]
public async Task ObtenerAbiertosAsync_ConEntregaYFacturaDirectaActivas_RestaAmbasDelMismoPool()
{
    var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N" };
    var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
    var entregaHeader = new EntregaCompra { Entry = 50, Cancelado = "N" };
    var entregaLinea = new EntregaCompraDetalle { Entry = 50, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 4m };
    var facturaHeader = new FacturaCompra { Entry = 60, Cancelado = "N" };
    var facturaLinea = new FacturaCompraDetalle { Entry = 60, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 3m };
    _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
    _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
    _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entregaHeader }.AsAsyncQueryable());
    _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { entregaLinea }.AsAsyncQueryable());
    _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra> { facturaHeader }.AsAsyncQueryable());
    _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle> { facturaLinea }.AsAsyncQueryable());

    var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

    Assert.Equal(3m, resultado[0].Lineas[0].CantidadAbierta); // 10 - 4 (entrega) - 3 (factura directa)
}

[Fact]
public async Task ObtenerAbiertosAsync_EntregaCancelada_SeIgnoraElConsumo()
{
    var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N" };
    var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
    var entregaHeader = new EntregaCompra { Entry = 50, Cancelado = "S" };
    var entregaLinea = new EntregaCompraDetalle { Entry = 50, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 4m };
    _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
    _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
    _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entregaHeader }.AsAsyncQueryable());
    _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { entregaLinea }.AsAsyncQueryable());
    _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

    Assert.Equal(10m, resultado[0].Lineas[0].CantidadAbierta);
}

[Fact]
public async Task ObtenerAbiertosAsync_LineaSinCantidadAbierta_NoAparece()
{
    var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "N" };
    var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
    var entregaHeader = new EntregaCompra { Entry = 50, Cancelado = "N" };
    var entregaLinea = new EntregaCompraDetalle { Entry = 50, NoLinea = 1, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1, Cantidad = 10m };
    _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
    _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
    _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entregaHeader }.AsAsyncQueryable());
    _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { entregaLinea }.AsAsyncQueryable());
    _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

    Assert.Empty(resultado);
}

[Fact]
public async Task ObtenerAbiertosAsync_PedidoDeOtroSocio_NoAparece()
{
    var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0002", Cancelado = "N" };
    var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
    _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
    _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
    _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra>().AsAsyncQueryable());
    _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable());
    _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var resultado = await _domain.ObtenerAbiertosAsync("PV0001");

    Assert.Empty(resultado);
}

[Fact]
public async Task ObtenerAbiertosAsync_PedidoCancelado_NoAparece()
{
    var pedido = new PedidoCompra { Entry = 1, NumDoc = 100, Serie = 4, CodigoSn = "PV0001", Cancelado = "S" };
    var linea = new PedidoCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 10m };
    _repoPedidoCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { pedido }.AsAsyncQueryable());
    _repoDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { linea }.AsAsyncQueryable());
    _repoEntregaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra>().AsAsyncQueryable());
    _repoEntregaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable());
    _repoFacturaCompraMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalleMock.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var resultado = await _domain.ObtenerAbiertosAsync("PV0001");

    Assert.Empty(resultado);
}
```

- [ ] **Step 2b: Ampliar el constructor de test con los 4 mocks nuevos**

En `PedidoCompraDomainTests.cs`, agregar los campos y ampliar el constructor:

```csharp
private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoEntregaCompraMock;
private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoEntregaCompraDetalleMock;
private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturaCompraMock;
private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoFacturaCompraDetalleMock;

public PedidoCompraDomainTests()
{
    _repoPedidoCompraMock = new Mock<IRepositorioGenerico<PedidoCompra, int>>();
    _repoDetalleMock = new Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>>();
    _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
    _repoEntregaCompraMock = new Mock<IRepositorioGenerico<EntregaCompra, int>>();
    _repoEntregaCompraDetalleMock = new Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>>();
    _repoFacturaCompraMock = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
    _repoFacturaCompraDetalleMock = new Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>>();
    _domain = new PedidoCompraDomain(
        _repoPedidoCompraMock.Object, _repoDetalleMock.Object, _repoNumeracionMock.Object,
        _repoEntregaCompraMock.Object, _repoEntregaCompraDetalleMock.Object,
        _repoFacturaCompraMock.Object, _repoFacturaCompraDetalleMock.Object);
}
```

Agregar `using API.Service.WebApi.Tests.TestHelpers;` al inicio del archivo (para `AsAsyncQueryable()`).

- [ ] **Step 3: Ejecutar la suite y verificar que los 6 tests nuevos fallan por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"
```
Esperado: `error CS1729` (el constructor de `PedidoCompraDomain` no acepta esos parámetros) o
`error CS7036` — falla de compilación, no de test todavía.

- [ ] **Step 4: Ampliar `IPedidoCompraDomain`**

`API.Domain.Interface/IPedidoCompraDomain.cs` — agregar el método a la interfaz:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IPedidoCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(PedidoCompra obj);
        Task<bool> ActualizarAsync(int id, PedidoCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<PedidoCompra> ObtenerAsync(int id);
        Task<IQueryable<PedidoCompra>> ObtenerTodoAsync();
        Task<IEnumerable<DocumentoAbierto>> ObtenerAbiertosAsync(string codigoSn);
        #endregion
    }
}
```

- [ ] **Step 5: Implementar `PedidoCompraDomain.ObtenerAbiertosAsync`**

`API.Domain.Core/PedidoCompraDomain.cs` — ampliar el constructor y agregar el método:

```csharp
private readonly IRepositorioGenerico<EntregaCompra, int> _repoEntregaCompra;
private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _repoEntregaCompraDetalle;
private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _repoFacturaCompraDetalle;

public PedidoCompraDomain(
    IRepositorioGenerico<PedidoCompra, int> repoGenericoPedidoCompra,
    IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> repoGenericoDetalle,
    IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion,
    IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
    IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoEntregaCompraDetalle,
    IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
    IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoFacturaCompraDetalle)
{
    _repoGenericoPedidoCompra = repoGenericoPedidoCompra;
    _repoGenericoDetalle = repoGenericoDetalle;
    _repoGenericoNumeracion = repoGenericoNumeracion;
    _repoEntregaCompra = repoEntregaCompra;
    _repoEntregaCompraDetalle = repoEntregaCompraDetalle;
    _repoFacturaCompra = repoFacturaCompra;
    _repoFacturaCompraDetalle = repoFacturaCompraDetalle;
}
```

Y el método (dentro de `#region async methods`, después de `ObtenerTodoAsync`):

```csharp
// BaseTipo con el que una Entrega de Compra o una Factura de Compra directa apuntan a una
// línea de Pedido de Compra -- mismo número que el TipoObjeto/CodigoObj del Pedido de Compra.
private const int BaseTipoPedidoCompra = 11;

public async Task<IEnumerable<DocumentoAbierto>> ObtenerAbiertosAsync(string codigoSn)
{
    var pedidos = await (await _repoGenericoPedidoCompra.ObtenerTodoAsync())
        .Where(p => p.CodigoSn == codigoSn && p.Cancelado != "S")
        .ToListAsync();
    if (pedidos.Count == 0) return Enumerable.Empty<DocumentoAbierto>();

    var entriesPedidos = pedidos.Select(p => p.Entry).ToHashSet();
    var lineasPedido = await (await _repoGenericoDetalle.ObtenerTodoAsync())
        .Where(d => entriesPedidos.Contains(d.Entry))
        .ToListAsync();

    // Consumo por Entrega de Compra: líneas activas (encabezado no cancelado) con BaseTipo=Pedido.
    var entregasCanceladas = (await (await _repoEntregaCompra.ObtenerTodoAsync())
        .Where(e => e.Cancelado == "S")
        .Select(e => e.Entry)
        .ToListAsync()).ToHashSet();
    var consumoPorEntrega = (await (await _repoEntregaCompraDetalle.ObtenerTodoAsync())
        .Where(d => d.BaseTipo == BaseTipoPedidoCompra && d.BaseEntry != null && d.BaseLinea != null)
        .ToListAsync())
        .Where(d => !entregasCanceladas.Contains(d.Entry))
        .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
        .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

    // Consumo por Factura de Compra directa (sin Entrega de por medio): mismo BaseTipo=Pedido,
    // mismo pool que el consumo por Entrega -- ver spec §1, "un solo pool compartido".
    var facturasCanceladas = (await (await _repoFacturaCompra.ObtenerTodoAsync())
        .Where(f => f.Cancelado == "S")
        .Select(f => f.Entry)
        .ToListAsync()).ToHashSet();
    var consumoPorFacturaDirecta = (await (await _repoFacturaCompraDetalle.ObtenerTodoAsync())
        .Where(d => d.BaseTipo == BaseTipoPedidoCompra && d.BaseEntry != null && d.BaseLinea != null)
        .ToListAsync())
        .Where(d => !facturasCanceladas.Contains(d.Entry))
        .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
        .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

    var resultado = new List<DocumentoAbierto>();
    foreach (var pedido in pedidos)
    {
        var lineasAbiertas = new List<LineaAbierta>();
        foreach (var linea in lineasPedido.Where(l => l.Entry == pedido.Entry))
        {
            var clave = (linea.Entry, linea.NoLinea);
            var consumido = consumoPorEntrega.GetValueOrDefault(clave, 0m) + consumoPorFacturaDirecta.GetValueOrDefault(clave, 0m);
            var abierta = (linea.Cantidad ?? 0m) - consumido;
            if (abierta <= 0m) continue;

            lineasAbiertas.Add(new LineaAbierta(
                linea.NoLinea, linea.CodArticulo!, linea.Descripcion, linea.Cantidad ?? 0m,
                abierta, linea.Precio, linea.CodAlmacen, linea.CodigoImpuesto));
        }
        if (lineasAbiertas.Count == 0) continue;

        resultado.Add(new DocumentoAbierto(pedido.Entry, pedido.NumDoc, pedido.Serie, pedido.FechaDoc, lineasAbiertas));
    }
    return resultado;
}
```

Agregar `using API.Domain.Interface;` ya está presente en el archivo (por `IPedidoCompraDomain`);
`DocumentoAbierto`/`LineaAbierta` viven en el mismo namespace, no requiere `using` adicional.

- [ ] **Step 6: Registrar las 4 dependencias nuevas en `Startup.cs`**

`API.Service.WebApi/Startup.cs` no cambia sus líneas de registro (los 4 repos genéricos ya están
registrados para sus propios dominios) — el contenedor de DI resuelve automáticamente los
parámetros adicionales del constructor de `PedidoCompraDomain` porque `IRepositorioGenerico<EntregaCompra,
int>`, `IRepositorioGenerico<EntregaCompraDetalle, (int,int)>`, `IRepositorioGenerico<FacturaCompra,
int>` e `IRepositorioGenerico<FacturaCompraDetalle, (int,int)>` ya están en el contenedor. Sin
cambios en este archivo para este paso.

- [ ] **Step 7: Ejecutar la suite y verificar que los 6 tests nuevos pasan**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\" --filter "FullyQualifiedName~PedidoCompraDomainTests"
```
Esperado: todos los tests de `PedidoCompraDomainTests` en verde (los existentes + los 6 nuevos).

- [ ] **Step 8: Commit**

```bash
git add API.Domain.Interface/DocumentoAbierto.cs API.Domain.Interface/IPedidoCompraDomain.cs API.Domain.Core/PedidoCompraDomain.cs API.Service.WebApi.Tests/Domain/PedidoCompraDomainTests.cs
git commit -m "feat(compras): PedidoCompraDomain.ObtenerAbiertosAsync (cantidad abierta por linea)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 2: `EntregaCompraDomain.ObtenerAbiertosAsync`

**Files:**
- Modify: `API.Domain.Interface/IEntregaCompraDomain.cs`
- Modify: `API.Domain.Core/EntregaCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs`

**Interfaces:**
- Consumes: `LineaAbierta`/`DocumentoAbierto` (Task 1, `API.Domain.Interface`).
- Produces: `IEntregaCompraDomain.ObtenerAbiertosAsync(string codigoSn) : Task<IEnumerable<DocumentoAbierto>>`.

- [ ] **Step 1: Escribir los tests que fallan**

`EntregaCompraDomainTests.cs` ya tiene estos campos (verificar antes de tocar nada): `_repoHeader`
(el `EntregaCompra`), `_repoDetalle`, `_repoNumeracion`, `_tx`, `_asiento` — **sin** sufijo
`Mock`. Los tests nuevos usan `_repoHeader`/`_repoDetalle` para lo que ya existe, y agregan 2
campos nuevos con el mismo estilo (sin sufijo). Ampliar la clase de test:

```csharp
private readonly Mock<IRepositorioGenerico<FacturaCompra, int>> _repoFacturaCompra;
private readonly Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>> _repoFacturaCompraDetalle;
```

Y reemplazar la línea del constructor de test:

```csharp
_domain = new EntregaCompraDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object);
```

por:

```csharp
_repoFacturaCompra = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
_repoFacturaCompraDetalle = new Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>>();
_domain = new EntregaCompraDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, _repoFacturaCompra.Object, _repoFacturaCompraDetalle.Object);
```

```csharp
[Fact]
public async Task ObtenerAbiertosAsync_SinConsumo_DevuelveCantidadTotalComoAbierta()
{
    var entrega = new EntregaCompra { Entry = 1, NumDoc = 200, Serie = 6, CodigoSn = "PV0001", Cancelado = "N", FechaDoc = new DateTime(2026, 9, 1) };
    var linea = new EntregaCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Descripcion = "Articulo 1", Cantidad = 8m, Precio = 25m, CodAlmacen = "01", CodigoImpuesto = "IVA" };
    _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entrega }.AsAsyncQueryable());
    _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { linea }.AsAsyncQueryable());
    _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

    Assert.Single(resultado);
    Assert.Equal(8m, resultado[0].Lineas[0].CantidadAbierta);
}

[Fact]
public async Task ObtenerAbiertosAsync_ConFacturaActiva_RestaLoConsumido()
{
    var entrega = new EntregaCompra { Entry = 1, NumDoc = 200, Serie = 6, CodigoSn = "PV0001", Cancelado = "N" };
    var linea = new EntregaCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 8m };
    var facturaHeader = new FacturaCompra { Entry = 70, Cancelado = "N" };
    var facturaLinea = new FacturaCompraDetalle { Entry = 70, NoLinea = 1, BaseTipo = 12, BaseEntry = 1, BaseLinea = 1, Cantidad = 6m };
    _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entrega }.AsAsyncQueryable());
    _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { linea }.AsAsyncQueryable());
    _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra> { facturaHeader }.AsAsyncQueryable());
    _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle> { facturaLinea }.AsAsyncQueryable());

    var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

    Assert.Equal(2m, resultado[0].Lineas[0].CantidadAbierta);
}

[Fact]
public async Task ObtenerAbiertosAsync_FacturaCancelada_SeIgnoraElConsumo()
{
    var entrega = new EntregaCompra { Entry = 1, NumDoc = 200, Serie = 6, CodigoSn = "PV0001", Cancelado = "N" };
    var linea = new EntregaCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 8m };
    var facturaHeader = new FacturaCompra { Entry = 70, Cancelado = "S" };
    var facturaLinea = new FacturaCompraDetalle { Entry = 70, NoLinea = 1, BaseTipo = 12, BaseEntry = 1, BaseLinea = 1, Cantidad = 6m };
    _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entrega }.AsAsyncQueryable());
    _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { linea }.AsAsyncQueryable());
    _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra> { facturaHeader }.AsAsyncQueryable());
    _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle> { facturaLinea }.AsAsyncQueryable());

    var resultado = (await _domain.ObtenerAbiertosAsync("PV0001")).ToList();

    Assert.Equal(8m, resultado[0].Lineas[0].CantidadAbierta);
}

[Fact]
public async Task ObtenerAbiertosAsync_EntregaCancelada_NoAparece()
{
    var entrega = new EntregaCompra { Entry = 1, NumDoc = 200, Serie = 6, CodigoSn = "PV0001", Cancelado = "S" };
    var linea = new EntregaCompraDetalle { Entry = 1, NoLinea = 1, CodArticulo = "ART1", Cantidad = 8m };
    _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { entrega }.AsAsyncQueryable());
    _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { linea }.AsAsyncQueryable());
    _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var resultado = await _domain.ObtenerAbiertosAsync("PV0001");

    Assert.Empty(resultado);
}
```

- [ ] **Step 2: Ejecutar la suite y verificar que fallan por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
```
Esperado: error de compilación (constructor de `EntregaCompraDomain` no coincide todavía).

- [ ] **Step 3: Ampliar `IEntregaCompraDomain` e implementar el método**

`API.Domain.Interface/IEntregaCompraDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface IEntregaCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas);
        Task<bool> ActualizarAsync(int id, EntregaCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<EntregaCompra> ObtenerAsync(int id);
        Task<IQueryable<EntregaCompra>> ObtenerTodoAsync();
        Task<IEnumerable<DocumentoAbierto>> ObtenerAbiertosAsync(string codigoSn);
        #endregion
    }
}
```

`API.Domain.Core/EntregaCompraDomain.cs` — ampliar el constructor y agregar el método:

```csharp
private readonly IRepositorioGenerico<FacturaCompra, int> _repoFacturaCompra;
private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _repoFacturaCompraDetalle;

public EntregaCompraDomain(
    IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
    IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
    IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
    IEjecutorTransaccion tx,
    IInventarioAsientoService asiento,
    IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
    IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoFacturaCompraDetalle)
{
    _repoEntregaCompra = repoEntregaCompra;
    _repoDetalle = repoDetalle;
    _repoNumeracion = repoNumeracion;
    _tx = tx;
    _asiento = asiento;
    _repoFacturaCompra = repoFacturaCompra;
    _repoFacturaCompraDetalle = repoFacturaCompraDetalle;
}
```

```csharp
// BaseTipo con el que una Factura de Compra apunta a una línea de Entrega de Compra -- mismo
// número que TipoObjetoEntregaCompra.
private const int BaseTipoEntregaCompra = 12;

public async Task<IEnumerable<DocumentoAbierto>> ObtenerAbiertosAsync(string codigoSn)
{
    var entregas = await (await _repoEntregaCompra.ObtenerTodoAsync())
        .Where(e => e.CodigoSn == codigoSn && e.Cancelado != "S")
        .ToListAsync();
    if (entregas.Count == 0) return Enumerable.Empty<DocumentoAbierto>();

    var entriesEntregas = entregas.Select(e => e.Entry).ToHashSet();
    var lineasEntrega = await (await _repoDetalle.ObtenerTodoAsync())
        .Where(d => entriesEntregas.Contains(d.Entry))
        .ToListAsync();

    var facturasCanceladas = (await (await _repoFacturaCompra.ObtenerTodoAsync())
        .Where(f => f.Cancelado == "S")
        .Select(f => f.Entry)
        .ToListAsync()).ToHashSet();
    var consumoPorFactura = (await (await _repoFacturaCompraDetalle.ObtenerTodoAsync())
        .Where(d => d.BaseTipo == BaseTipoEntregaCompra && d.BaseEntry != null && d.BaseLinea != null)
        .ToListAsync())
        .Where(d => !facturasCanceladas.Contains(d.Entry))
        .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
        .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

    var resultado = new List<DocumentoAbierto>();
    foreach (var entrega in entregas)
    {
        var lineasAbiertas = new List<LineaAbierta>();
        foreach (var linea in lineasEntrega.Where(l => l.Entry == entrega.Entry))
        {
            var consumido = consumoPorFactura.GetValueOrDefault((linea.Entry, linea.NoLinea), 0m);
            var abierta = (linea.Cantidad ?? 0m) - consumido;
            if (abierta <= 0m) continue;

            lineasAbiertas.Add(new LineaAbierta(
                linea.NoLinea, linea.CodArticulo!, linea.Descripcion, linea.Cantidad ?? 0m,
                abierta, linea.Precio, linea.CodAlmacen, linea.CodigoImpuesto));
        }
        if (lineasAbiertas.Count == 0) continue;

        resultado.Add(new DocumentoAbierto(entrega.Entry, entrega.NumDoc, entrega.Serie, entrega.FechaDoc, lineasAbiertas));
    }
    return resultado;
}
```

- [ ] **Step 4: Ejecutar la suite y verificar que pasan**

```powershell
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\" --filter "FullyQualifiedName~EntregaCompraDomainTests"
```

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Interface/IEntregaCompraDomain.cs API.Domain.Core/EntregaCompraDomain.cs API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs
git commit -m "feat(compras): EntregaCompraDomain.ObtenerAbiertosAsync (cantidad abierta por linea)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 3: DTOs y capa de aplicación (`ObtenerAbiertosAsync`)

**Files:**
- Create: `API.Application.DTO/documentoAbierto/LineaAbiertaDTO.cs`
- Create: `API.Application.DTO/documentoAbierto/DocumentoAbiertoDTO.cs`
- Modify: `API.Application.Interface/IPedidoCompraApplication.cs`
- Modify: `API.Application.Interface/IEntregaCompraApplication.cs`
- Modify: `API.Application.Main/PedidoCompraApplication.cs`
- Modify: `API.Application.Main/EntregaCompraApplication.cs`
- Test: `API.Service.WebApi.Tests/Application/PedidoCompraApplicationTests.cs` (crear si no existe)
- Test: `API.Service.WebApi.Tests/Application/EntregaCompraApplicationTests.cs` (crear si no existe)

**Interfaces:**
- Consumes: `IPedidoCompraDomain.ObtenerAbiertosAsync`/`IEntregaCompraDomain.ObtenerAbiertosAsync`
  (Tasks 1-2); `DocumentoAbierto`/`LineaAbierta` (Task 1).
- Produces: `DocumentoAbiertoDTO`/`LineaAbiertaDTO`;
  `IPedidoCompraApplication.ObtenerAbiertosAsync(string) : Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>`;
  `IEntregaCompraApplication.ObtenerAbiertosAsync(string) : Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>`.

- [ ] **Step 1: Crear los DTOs**

`API.Application.DTO/documentoAbierto/LineaAbiertaDTO.cs`:

```csharp
namespace API.Application.DTO.documentoAbierto
{
    public class LineaAbiertaDTO
    {
        public int NoLinea { get; set; }
        public string CodArticulo { get; set; } = null!;
        public string? Descripcion { get; set; }
        public decimal Cantidad { get; set; }
        public decimal CantidadAbierta { get; set; }
        public decimal? Precio { get; set; }
        public string? CodAlmacen { get; set; }
        public string? CodigoImpuesto { get; set; }
    }
}
```

`API.Application.DTO/documentoAbierto/DocumentoAbiertoDTO.cs`:

```csharp
namespace API.Application.DTO.documentoAbierto
{
    public class DocumentoAbiertoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public DateTime? FechaDoc { get; set; }
        public List<LineaAbiertaDTO> Lineas { get; set; } = new();
    }
}
```

- [ ] **Step 2: Escribir los tests de aplicación que fallan**

Ambos archivos son nuevos, mismo patrón de test que `PrecioListaApplicationTests.cs` (mock del
dominio, sin AutoMapper real). Crear `API.Service.WebApi.Tests/Application/PedidoCompraApplicationTests.cs`:

```csharp
using API.Application.DTO.documentoAbierto;
using API.Application.DTO.pedidoCompra;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class PedidoCompraApplicationTests
    {
        private readonly Mock<IPedidoCompraDomain> _domain = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly PedidoCompraApplication _application;

        public PedidoCompraApplicationTests()
        {
            _application = new PedidoCompraApplication(_domain.Object, _mapper.Object);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_Ok_MapeaLineasYEncabezado()
        {
            var lineas = new List<LineaAbierta> { new(1, "ART1", "Articulo 1", 10m, 6m, 25m, "01", "IVA") };
            var documentos = new List<DocumentoAbierto> { new(1, 100, 4, new DateTime(2026, 9, 1), lineas) };
            _domain.Setup(d => d.ObtenerAbiertosAsync("PV0001")).ReturnsAsync(documentos);

            var respuesta = await _application.ObtenerAbiertosAsync("PV0001");

            Assert.True(respuesta.Resultado);
            var doc = Assert.Single(respuesta.Dato!);
            Assert.Equal(1, doc.Entry);
            Assert.Equal(100, doc.NumDoc);
            var linea = Assert.Single(doc.Lineas);
            Assert.Equal("ART1", linea.CodArticulo);
            Assert.Equal(6m, linea.CantidadAbierta);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_ExcepcionDelDominio_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.ObtenerAbiertosAsync(It.IsAny<string>())).ThrowsAsync(new Exception("fallo"));

            var respuesta = await _application.ObtenerAbiertosAsync("PV0001");

            Assert.False(respuesta.Resultado);
            Assert.Equal("fallo", respuesta.Mensaje);
        }
    }
}
```

Crear `API.Service.WebApi.Tests/Application/EntregaCompraApplicationTests.cs`:

```csharp
using API.Application.DTO.documentoAbierto;
using API.Application.DTO.entregaCompra;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class EntregaCompraApplicationTests
    {
        private readonly Mock<IEntregaCompraDomain> _domain = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly EntregaCompraApplication _application;

        public EntregaCompraApplicationTests()
        {
            _application = new EntregaCompraApplication(_domain.Object, _mapper.Object);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_Ok_MapeaLineasYEncabezado()
        {
            var lineas = new List<LineaAbierta> { new(1, "ART1", "Articulo 1", 8m, 4m, 25m, "01", "IVA") };
            var documentos = new List<DocumentoAbierto> { new(1, 200, 6, new DateTime(2026, 9, 1), lineas) };
            _domain.Setup(d => d.ObtenerAbiertosAsync("PV0001")).ReturnsAsync(documentos);

            var respuesta = await _application.ObtenerAbiertosAsync("PV0001");

            Assert.True(respuesta.Resultado);
            var doc = Assert.Single(respuesta.Dato!);
            Assert.Equal(1, doc.Entry);
            Assert.Equal(200, doc.NumDoc);
            var linea = Assert.Single(doc.Lineas);
            Assert.Equal("ART1", linea.CodArticulo);
            Assert.Equal(4m, linea.CantidadAbierta);
        }

        [Fact]
        public async Task ObtenerAbiertosAsync_ExcepcionDelDominio_DevuelveResultadoFalse()
        {
            _domain.Setup(d => d.ObtenerAbiertosAsync(It.IsAny<string>())).ThrowsAsync(new Exception("fallo"));

            var respuesta = await _application.ObtenerAbiertosAsync("PV0001");

            Assert.False(respuesta.Resultado);
            Assert.Equal("fallo", respuesta.Mensaje);
        }
    }
}
```

- [ ] **Step 3: Ejecutar la suite y verificar que fallan por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
```
Esperado: error — `IPedidoCompraApplication`/`IEntregaCompraApplication` todavía no tienen
`ObtenerAbiertosAsync`.

- [ ] **Step 4: Ampliar las interfaces y las clases de aplicación**

`API.Application.Interface/IPedidoCompraApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.documentoAbierto;
using API.Application.DTO.pedidoCompra;

namespace API.Application.Interface
{
    public interface IPedidoCompraApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(PedidoCompraCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, PedidoCompraActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<PedidoCompraDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<PedidoCompraDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn);
        #endregion
    }
}
```

`API.Application.Main/PedidoCompraApplication.cs` — agregar `using API.Application.DTO.documentoAbierto;`
y `using API.Domain.Interface;`, y el método (mapeo manual, mismo patrón que `PrecioListaApplication`):

```csharp
public async Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn)
{
    var respuesta = new Respuesta<IEnumerable<DocumentoAbiertoDTO>>();
    try
    {
        var documentos = await _pedidoCompraDomain.ObtenerAbiertosAsync(codigoSn);
        respuesta.Dato = documentos.Select(MapearDocumento).ToList();
        respuesta.Resultado = true;
    }
    catch (Exception ex)
    {
        respuesta.Mensaje = ex.Message;
    }
    return respuesta;
}

private static DocumentoAbiertoDTO MapearDocumento(DocumentoAbierto d) => new()
{
    Entry = d.Entry,
    NumDoc = d.NumDoc,
    Serie = d.Serie,
    FechaDoc = d.FechaDoc,
    Lineas = d.Lineas.Select(l => new LineaAbiertaDTO
    {
        NoLinea = l.NoLinea,
        CodArticulo = l.CodArticulo,
        Descripcion = l.Descripcion,
        Cantidad = l.Cantidad,
        CantidadAbierta = l.CantidadAbierta,
        Precio = l.Precio,
        CodAlmacen = l.CodAlmacen,
        CodigoImpuesto = l.CodigoImpuesto
    }).ToList()
};
```

`API.Application.Interface/IEntregaCompraApplication.cs` — agregar `using API.Application.DTO.documentoAbierto;`
y el método:

```csharp
Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn);
```

`API.Application.Main/EntregaCompraApplication.cs` — agregar `using API.Application.DTO.documentoAbierto;`
y `using API.Domain.Interface;`, y:

```csharp
public async Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn)
{
    var respuesta = new Respuesta<IEnumerable<DocumentoAbiertoDTO>>();
    try
    {
        var documentos = await _entregaCompraDomain.ObtenerAbiertosAsync(codigoSn);
        respuesta.Dato = documentos.Select(MapearDocumento).ToList();
        respuesta.Resultado = true;
    }
    catch (Exception ex)
    {
        respuesta.Mensaje = ex.Message;
    }
    return respuesta;
}

private static DocumentoAbiertoDTO MapearDocumento(DocumentoAbierto d) => new()
{
    Entry = d.Entry,
    NumDoc = d.NumDoc,
    Serie = d.Serie,
    FechaDoc = d.FechaDoc,
    Lineas = d.Lineas.Select(l => new LineaAbiertaDTO
    {
        NoLinea = l.NoLinea,
        CodArticulo = l.CodArticulo,
        Descripcion = l.Descripcion,
        Cantidad = l.Cantidad,
        CantidadAbierta = l.CantidadAbierta,
        Precio = l.Precio,
        CodAlmacen = l.CodAlmacen,
        CodigoImpuesto = l.CodigoImpuesto
    }).ToList()
};
```

- [ ] **Step 5: Ejecutar la suite y verificar que los tests nuevos pasan**

```powershell
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\" --filter "FullyQualifiedName~PedidoCompraApplicationTests|FullyQualifiedName~EntregaCompraApplicationTests"
```

- [ ] **Step 6: Commit**

```bash
git add API.Application.DTO/documentoAbierto/LineaAbiertaDTO.cs API.Application.DTO/documentoAbierto/DocumentoAbiertoDTO.cs API.Application.Interface/IPedidoCompraApplication.cs API.Application.Interface/IEntregaCompraApplication.cs API.Application.Main/PedidoCompraApplication.cs API.Application.Main/EntregaCompraApplication.cs API.Service.WebApi.Tests/Application/PedidoCompraApplicationTests.cs API.Service.WebApi.Tests/Application/EntregaCompraApplicationTests.cs
git commit -m "feat(compras): DocumentoAbiertoDTO y ObtenerAbiertosAsync en la capa de aplicacion

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 4: Endpoints `GET .../Abiertos`

**Files:**
- Modify: `API.Service.WebApi/Controllers/PedidoCompraController.cs`
- Modify: `API.Service.WebApi/Controllers/EntregaCompraController.cs`
- Test: `API.Service.WebApi.Tests/Controllers/PedidoCompraControllerTests.cs`
- Test: `API.Service.WebApi.Tests/Controllers/EntregaCompraControllerTests.cs`

**Interfaces:**
- Consumes: `IPedidoCompraApplication.ObtenerAbiertosAsync`/`IEntregaCompraApplication.ObtenerAbiertosAsync` (Task 3).
- Produces: `GET api/PedidoCompra/Abiertos?codigoSn=...`, `GET api/EntregaCompra/Abiertos?codigoSn=...`.

- [ ] **Step 1: Escribir los tests de controller que fallan**

Agregar a `API.Service.WebApi.Tests/Controllers/PedidoCompraControllerTests.cs` (campos existentes
del archivo: `_applicationMock` de tipo `Mock<IPedidoCompraApplication>`, `_controller`; agregar
`using API.Application.DTO.documentoAbierto;` al inicio):

```csharp
[Fact]
public async Task Abiertos_Ok_DevuelveOk200()
{
    var documentos = new List<DocumentoAbiertoDTO> { new() { Entry = 1, NumDoc = 100, Serie = 4 } };
    _applicationMock.Setup(a => a.ObtenerAbiertosAsync("PV0001"))
        .ReturnsAsync(new Respuesta<IEnumerable<DocumentoAbiertoDTO>> { Resultado = true, Dato = documentos });

    var resultado = await _controller.Abiertos("PV0001");

    var ok = Assert.IsType<OkObjectResult>(resultado.Result);
    var respuesta = Assert.IsType<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>(ok.Value);
    Assert.Single(respuesta.Dato!);
}

[Fact]
public async Task Abiertos_SinCodigoSn_Devuelve400()
{
    var resultado = await _controller.Abiertos("");

    Assert.IsType<BadRequestObjectResult>(resultado.Result);
    _applicationMock.Verify(a => a.ObtenerAbiertosAsync(It.IsAny<string>()), Times.Never);
}
```

Agregar a `API.Service.WebApi.Tests/Controllers/EntregaCompraControllerTests.cs` (mismos nombres
de campo: `_applicationMock` de tipo `Mock<IEntregaCompraApplication>`, `_controller`; agregar
`using API.Application.DTO.documentoAbierto;` al inicio):

```csharp
[Fact]
public async Task Abiertos_Ok_DevuelveOk200()
{
    var documentos = new List<DocumentoAbiertoDTO> { new() { Entry = 1, NumDoc = 200, Serie = 6 } };
    _applicationMock.Setup(a => a.ObtenerAbiertosAsync("PV0001"))
        .ReturnsAsync(new Respuesta<IEnumerable<DocumentoAbiertoDTO>> { Resultado = true, Dato = documentos });

    var resultado = await _controller.Abiertos("PV0001");

    var ok = Assert.IsType<OkObjectResult>(resultado.Result);
    var respuesta = Assert.IsType<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>(ok.Value);
    Assert.Single(respuesta.Dato!);
}

[Fact]
public async Task Abiertos_SinCodigoSn_Devuelve400()
{
    var resultado = await _controller.Abiertos("");

    Assert.IsType<BadRequestObjectResult>(resultado.Result);
    _applicationMock.Verify(a => a.ObtenerAbiertosAsync(It.IsAny<string>()), Times.Never);
}
```

- [ ] **Step 2: Ejecutar la suite y verificar que fallan por compilación**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
```
Esperado: `Abiertos` no existe todavía en ninguno de los dos controllers.

- [ ] **Step 3: Agregar las acciones**

`API.Service.WebApi/Controllers/PedidoCompraController.cs` — agregar `using API.Application.DTO.documentoAbierto;`
y, después de `ObtenerTodoAsync`:

```csharp
[HttpGet("Abiertos")]
public async Task<ActionResult<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>> Abiertos([FromQuery] string codigoSn)
{
    if (string.IsNullOrWhiteSpace(codigoSn))
    {
        return BadRequest(new Respuesta<IEnumerable<DocumentoAbiertoDTO>> { Mensaje = "codigoSn es requerido." });
    }

    var res = await _pedidoCompraApplication.ObtenerAbiertosAsync(codigoSn);
    return res.Resultado ? Ok(res) : BadRequest(res);
}
```

`API.Service.WebApi/Controllers/EntregaCompraController.cs` — agregar `using
API.Application.DTO.documentoAbierto;` y:

```csharp
[HttpGet("Abiertos")]
public async Task<ActionResult<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>> Abiertos([FromQuery] string codigoSn)
{
    if (string.IsNullOrWhiteSpace(codigoSn))
    {
        return BadRequest(new Respuesta<IEnumerable<DocumentoAbiertoDTO>> { Mensaje = "codigoSn es requerido." });
    }

    var res = await _entregaCompraApplication.ObtenerAbiertosAsync(codigoSn);
    return res.Resultado ? Ok(res) : BadRequest(res);
}
```

- [ ] **Step 4: Ejecutar la suite completa y verificar baseline + 4**

```powershell
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\"
```
Esperado: `0` errores, todos los tests en verde (baseline de este plan + 6 de Task 1 + 4 de Task 2
+ 4 de Task 3 + 4 de este task).

- [ ] **Step 5: Commit**

```bash
git add API.Service.WebApi/Controllers/PedidoCompraController.cs API.Service.WebApi/Controllers/EntregaCompraController.cs API.Service.WebApi.Tests/Controllers/PedidoCompraControllerTests.cs API.Service.WebApi.Tests/Controllers/EntregaCompraControllerTests.cs
git commit -m "feat(compras): endpoints GET Abiertos en PedidoCompra y EntregaCompra

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 2 — API: validación e inventario al guardar

### Task 5: Validación de líneas con base en `EntregaCompraDomain.InsertarAsync`

**Files:**
- Modify: `API.Domain.Core/EntregaCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs`

**Interfaces:**
- Consumes: `ObtenerAbiertosAsync` (Task 2, ya en la misma clase) — se reutiliza para recalcular
  la cantidad abierta al validar.
- Produces: `EntregaCompraDomain.InsertarAsync` ahora valida líneas con `BaseTipo == 11` antes de
  ejecutar la transacción; sin cambio de firma.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar a `EntregaCompraDomainTests.cs`:

```csharp
[Fact]
public async Task InsertarAsync_LineaConBaseSinEntryOLinea_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 5m, BaseTipo = 11, BaseEntry = null, BaseLinea = null } };

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("BaseEntry/BaseLinea", ex.Message);
}

[Fact]
public async Task InsertarAsync_LineaConBaseTipoInvalido_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    // 12 (Entrega de Compra) no es una base valida para una Entrega de Compra -- solo Pedido (11).
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 5m, BaseTipo = 12, BaseEntry = 1, BaseLinea = 1 } };

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("BaseTipo", ex.Message);
}

[Fact]
public async Task InsertarAsync_LineaBaseInexistente_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 5m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 } };
    _repoPedidoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra>().AsAsyncQueryable());
    _repoPedidoCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle>().AsAsyncQueryable());

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("no existe", ex.Message);
}

[Fact]
public async Task InsertarAsync_LineaBaseDeOtroSocio_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 5m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 } };
    _repoPedidoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { new() { Entry = 1, CodigoSn = "PV0002", Cancelado = "N" } }.AsAsyncQueryable());
    _repoPedidoCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("otro proveedor", ex.Message);
}

[Fact]
public async Task InsertarAsync_LineaBaseCancelada_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 5m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 } };
    _repoPedidoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "S" } }.AsAsyncQueryable());
    _repoPedidoCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("cancelado", ex.Message);
}

[Fact]
public async Task InsertarAsync_CantidadExcedeLoAbierto_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 11m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 } };
    _repoPedidoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "N" } }.AsAsyncQueryable());
    _repoPedidoCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());
    _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("excede lo pendiente", ex.Message);
}

[Fact]
public async Task InsertarAsync_CantidadIgualALoAbierto_Inserta()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 10m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 } };
    _repoPedidoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "N" } }.AsAsyncQueryable());
    _repoPedidoCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());
    _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    var entry = await _domain.InsertarAsync(obj, lineas);

    Assert.Equal(99, entry);
}

[Fact]
public async Task InsertarAsync_DosLineasMismaBase_SumanParaValidar()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    // 6 + 6 = 12 > 10 abiertos -- debe rechazar aunque cada linea por separado parezca valida.
    var lineas = new[]
    {
        new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 6m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 },
        new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 6m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 },
    };
    _repoPedidoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "N" } }.AsAsyncQueryable());
    _repoPedidoCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());
    _repoFacturaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompra>().AsAsyncQueryable());
    _repoFacturaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaCompraDetalle>().AsAsyncQueryable());

    await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
}

[Fact]
public async Task InsertarAsync_LineaSinBaseTipo_NoValidaNada()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new EntregaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new EntregaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 5m } };

    var entry = await _domain.InsertarAsync(obj, lineas);

    Assert.Equal(99, entry);
}
```

Ampliar la clase de test con los 2 campos de Pedido de Compra (además de `_repoFacturaCompra`/
`_repoFacturaCompraDetalle` ya agregados en Task 2 -- mismo estilo, sin sufijo `Mock`):

```csharp
private readonly Mock<IRepositorioGenerico<PedidoCompra, int>> _repoPedidoCompra;
private readonly Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>> _repoPedidoCompraDetalle;
```

Y reemplazar la línea del constructor de test (la que Task 2 ya dejó así):

```csharp
_repoFacturaCompra = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
_repoFacturaCompraDetalle = new Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>>();
_domain = new EntregaCompraDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object, _repoFacturaCompra.Object, _repoFacturaCompraDetalle.Object);
```

por:

```csharp
_repoFacturaCompra = new Mock<IRepositorioGenerico<FacturaCompra, int>>();
_repoFacturaCompraDetalle = new Mock<IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)>>();
_repoPedidoCompra = new Mock<IRepositorioGenerico<PedidoCompra, int>>();
_repoPedidoCompraDetalle = new Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>>();
_domain = new EntregaCompraDomain(
    _repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object,
    _repoFacturaCompra.Object, _repoFacturaCompraDetalle.Object,
    _repoPedidoCompra.Object, _repoPedidoCompraDetalle.Object);
```

(mismo orden que el nuevo constructor de `EntregaCompraDomain` del Step 3 de este task).

- [ ] **Step 2: Ejecutar la suite y verificar que fallan**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\" --filter "FullyQualifiedName~EntregaCompraDomainTests"
```
Esperado: fallo de compilación (constructor no coincide) o, si compila, los tests de validación
fallan porque ninguna línea con base es rechazada todavía.

- [ ] **Step 3: Implementar la validación**

`API.Domain.Core/EntregaCompraDomain.cs` — agregar los dos repos de Pedido de Compra al
constructor (además de los de Factura de Compra agregados en Task 2):

```csharp
private readonly IRepositorioGenerico<PedidoCompra, int> _repoPedidoCompra;
private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _repoPedidoCompraDetalle;

public EntregaCompraDomain(
    IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
    IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
    IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
    IEjecutorTransaccion tx,
    IInventarioAsientoService asiento,
    IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
    IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoFacturaCompraDetalle,
    IRepositorioGenerico<PedidoCompra, int> repoPedidoCompra,
    IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> repoPedidoCompraDetalle)
{
    _repoEntregaCompra = repoEntregaCompra;
    _repoDetalle = repoDetalle;
    _repoNumeracion = repoNumeracion;
    _tx = tx;
    _asiento = asiento;
    _repoFacturaCompra = repoFacturaCompra;
    _repoFacturaCompraDetalle = repoFacturaCompraDetalle;
    _repoPedidoCompra = repoPedidoCompra;
    _repoPedidoCompraDetalle = repoPedidoCompraDetalle;
}
```

Agregar, antes del `return await _tx.EjecutarAsync(...)` de `InsertarAsync` (después de armar
`lineasList`):

```csharp
await ValidarLineasConBaseAsync(obj.CodigoSn, lineasList);
```

Y el método privado nuevo (única base válida para Entrega de Compra: Pedido de Compra = `11`):

```csharp
private async Task ValidarLineasConBaseAsync(string? codigoSnDestino, List<EntregaCompraDetalle> lineas)
{
    var conBase = lineas.Where(l => l.BaseTipo != null && l.BaseTipo != -1).ToList();
    if (conBase.Count == 0) return;

    foreach (var l in conBase)
    {
        if (l.BaseEntry == null || l.BaseLinea == null)
            throw new Exception($"La línea con artículo '{l.CodArticulo}' indica BaseTipo sin BaseEntry/BaseLinea.");
        if (l.BaseTipo != BaseTipoPedidoCompra)
            throw new Exception($"BaseTipo {l.BaseTipo} no es válido para una Entrega de Compra (solo se puede basar en un Pedido de Compra).");
    }

    var pedidos = (await (await _repoPedidoCompra.ObtenerTodoAsync()).ToListAsync())
        .ToDictionary(p => p.Entry);
    var lineasPedido = (await (await _repoPedidoCompraDetalle.ObtenerTodoAsync()).ToListAsync())
        .ToDictionary(d => (d.Entry, d.NoLinea));

    // Cantidad ya abierta de cada línea base, recalculada ahora mismo (no lo que mandó el navegador).
    var abiertos = (await ObtenerAbiertosAsync(codigoSnDestino ?? string.Empty))
        .SelectMany(d => d.Lineas.Select(l => ((d.Entry, l.NoLinea), l.CantidadAbierta)))
        .ToDictionary(x => x.Item1, x => x.CantidadAbierta);

    // Si el propio documento trae mas de una linea apuntando a la misma base, sus cantidades
    // se suman antes de comparar contra lo abierto -- ver spec §1.
    foreach (var grupo in conBase.GroupBy(l => (l.BaseEntry!.Value, l.BaseLinea!.Value)))
    {
        var (baseEntry, baseLinea) = grupo.Key;
        if (!lineasPedido.ContainsKey((baseEntry, baseLinea)))
            throw new Exception($"La línea base {baseEntry}/{baseLinea} no existe.");
        if (!pedidos.TryGetValue(baseEntry, out var pedido))
            throw new Exception($"La línea base {baseEntry}/{baseLinea} no existe.");
        if (pedido.CodigoSn != codigoSnDestino)
            throw new Exception($"El documento base {baseEntry} pertenece a otro proveedor.");
        if (pedido.Cancelado == "S")
            throw new Exception($"El documento base {baseEntry} está cancelado.");

        var solicitado = grupo.Sum(l => l.Cantidad ?? 0m);
        var abierta = abiertos.GetValueOrDefault((baseEntry, baseLinea), 0m);
        if (solicitado > abierta)
            throw new Exception($"La cantidad solicitada para la línea base {baseEntry}/{baseLinea} excede lo pendiente ({abierta} disponible).");
    }
}
```

- [ ] **Step 4: Ejecutar la suite y verificar que pasan**

```powershell
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\" --filter "FullyQualifiedName~EntregaCompraDomainTests"
```

- [ ] **Step 5: Ejecutar la suite completa (nada más debe romperse)**

```powershell
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\"
```

- [ ] **Step 6: Commit**

```bash
git add API.Domain.Core/EntregaCompraDomain.cs API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs
git commit -m "feat(compras): valida lineas con BaseTipo=Pedido al insertar una Entrega de Compra

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 6: Validación de líneas con base + filtro de inventario en `FacturaCompraDomain.InsertarAsync`

**Files:**
- Modify: `API.Domain.Core/FacturaCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/FacturaCompraDomainTests.cs`

**Interfaces:**
- Consumes: `IEntregaCompraDomain.ObtenerAbiertosAsync` (Task 2, inyectado);
  `IPedidoCompraDomain.ObtenerAbiertosAsync` (Task 1, inyectado).
- Produces: `FacturaCompraDomain.InsertarAsync` valida líneas con `BaseTipo` 11 o 12, y ya no
  postea inventario para líneas con `BaseTipo == 12`; sin cambio de firma.

- [ ] **Step 1: Escribir los tests que fallan**

Agregar a `FacturaCompraDomainTests.cs`:

```csharp
[Fact]
public async Task InsertarAsync_LineaBaseEntrega_NoSeAsientaEnInventario()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new FacturaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new FacturaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 4m, Precio = 25m, BaseTipo = 12, BaseEntry = 1, BaseLinea = 1 } };
    _repoEntregaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "N" } }.AsAsyncQueryable());
    _repoEntregaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());

    await _domain.InsertarAsync(obj, lineas);

    Assert.Empty(_movimientosAsentados);
}

[Fact]
public async Task InsertarAsync_LineaBasePedidoDirecto_SiSeAsientaEnInventario()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new FacturaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new FacturaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 4m, Precio = 25m, BaseTipo = 11, BaseEntry = 1, BaseLinea = 1 } };
    _repoPedidoCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "N" } }.AsAsyncQueryable());
    _repoPedidoCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<PedidoCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());
    _repoEntregaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra>().AsAsyncQueryable());
    _repoEntregaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable());

    await _domain.InsertarAsync(obj, lineas);

    Assert.Single(_movimientosAsentados);
}

[Fact]
public async Task InsertarAsync_MezclaDeLineas_SoloAsientaLasQueNoSonBaseEntrega()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new FacturaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[]
    {
        new FacturaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 3m, Precio = 25m }, // sin base
        new FacturaCompraDetalle { CodArticulo = "ART2", CodAlmacen = "01", Cantidad = 4m, Precio = 30m, BaseTipo = 12, BaseEntry = 1, BaseLinea = 1 },
    };
    _repoEntregaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "N" } }.AsAsyncQueryable());
    _repoEntregaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());

    await _domain.InsertarAsync(obj, lineas);

    Assert.Single(_movimientosAsentados);
    Assert.Equal("ART1", _movimientosAsentados[0].CodArticulo);
}

[Fact]
public async Task InsertarAsync_LineaBaseTipoInvalido_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new FacturaCompra { Serie = 4, CodigoSn = "PV0001" };
    // 10 (Oferta de Compra) esta fuera de alcance -- no es una base valida acá.
    var lineas = new[] { new FacturaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 4m, BaseTipo = 10, BaseEntry = 1, BaseLinea = 1 } };

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("BaseTipo", ex.Message);
}

[Fact]
public async Task InsertarAsync_CantidadExcedeLoAbiertoDeLaEntrega_Lanza()
{
    var serie = SerieAuto(sig: 5);
    _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
    var obj = new FacturaCompra { Serie = 4, CodigoSn = "PV0001" };
    var lineas = new[] { new FacturaCompraDetalle { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 11m, BaseTipo = 12, BaseEntry = 1, BaseLinea = 1 } };
    _repoEntregaCompra.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompra> { new() { Entry = 1, CodigoSn = "PV0001", Cancelado = "N" } }.AsAsyncQueryable());
    _repoEntregaCompraDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<EntregaCompraDetalle> { new() { Entry = 1, NoLinea = 1, Cantidad = 10m } }.AsAsyncQueryable());

    var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj, lineas));
    Assert.Contains("excede lo pendiente", ex.Message);
}
```

`FacturaCompraDomainTests.cs` ya tiene `_repoHeader` (el `FacturaCompra`), `_repoDetalle`,
`_repoNumeracion`, `_tx`, `_asiento` — sin sufijo `Mock`. Ampliar la clase de test con los 4 campos
nuevos, mismo estilo:

```csharp
private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoEntregaCompra;
private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoEntregaCompraDetalle;
private readonly Mock<IRepositorioGenerico<PedidoCompra, int>> _repoPedidoCompra;
private readonly Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>> _repoPedidoCompraDetalle;
```

Y reemplazar la línea del constructor de test:

```csharp
_domain = new FacturaCompraDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object);
```

por:

```csharp
_repoEntregaCompra = new Mock<IRepositorioGenerico<EntregaCompra, int>>();
_repoEntregaCompraDetalle = new Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>>();
_repoPedidoCompra = new Mock<IRepositorioGenerico<PedidoCompra, int>>();
_repoPedidoCompraDetalle = new Mock<IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)>>();
_domain = new FacturaCompraDomain(
    _repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object,
    _repoEntregaCompra.Object, _repoEntregaCompraDetalle.Object,
    _repoPedidoCompra.Object, _repoPedidoCompraDetalle.Object);
```

(mismo orden que el nuevo constructor de `FacturaCompraDomain` del Step 3 de este task).

- [ ] **Step 2: Ejecutar la suite y verificar que fallan**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
```
Esperado: fallo de compilación (constructor no coincide todavía).

- [ ] **Step 3: Implementar el filtro de inventario y la validación**

`API.Domain.Core/FacturaCompraDomain.cs` — ampliar el constructor:

```csharp
private readonly IRepositorioGenerico<EntregaCompra, int> _repoEntregaCompra;
private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _repoEntregaCompraDetalle;
private readonly IRepositorioGenerico<PedidoCompra, int> _repoPedidoCompra;
private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _repoPedidoCompraDetalle;

public FacturaCompraDomain(
    IRepositorioGenerico<FacturaCompra, int> repoFacturaCompra,
    IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
    IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
    IEjecutorTransaccion tx,
    IInventarioAsientoService asiento,
    IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
    IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoEntregaCompraDetalle,
    IRepositorioGenerico<PedidoCompra, int> repoPedidoCompra,
    IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> repoPedidoCompraDetalle)
{
    _repoFacturaCompra = repoFacturaCompra;
    _repoDetalle = repoDetalle;
    _repoNumeracion = repoNumeracion;
    _tx = tx;
    _asiento = asiento;
    _repoEntregaCompra = repoEntregaCompra;
    _repoEntregaCompraDetalle = repoEntregaCompraDetalle;
    _repoPedidoCompra = repoPedidoCompra;
    _repoPedidoCompraDetalle = repoPedidoCompraDetalle;
}

private const int BaseTipoPedidoCompra = 11;
private const int BaseTipoEntregaCompra = 12;
```

Cambiar, dentro de `InsertarAsync`, el filtro de movimientos (línea ya existente):

```csharp
var movimientos = lineasList
    .Where(l => (l.Cantidad ?? 0m) > 0m && l.BaseTipo != BaseTipoEntregaCompra)
    .Select(l => new MovimientoRequest(
```

Y agregar, antes del `return await _tx.EjecutarAsync(...)` (después de armar `lineasList`):

```csharp
await ValidarLineasConBaseAsync(obj.CodigoSn, lineasList);
```

El método privado nuevo (dos bases válidas: Entrega de Compra=`12` y Pedido de Compra=`11`,
resolviendo la cantidad abierta contra el dominio correspondiente en cada caso):

```csharp
private async Task ValidarLineasConBaseAsync(string? codigoSnDestino, List<FacturaCompraDetalle> lineas)
{
    var conBase = lineas.Where(l => l.BaseTipo != null && l.BaseTipo != -1).ToList();
    if (conBase.Count == 0) return;

    foreach (var l in conBase)
    {
        if (l.BaseEntry == null || l.BaseLinea == null)
            throw new Exception($"La línea con artículo '{l.CodArticulo}' indica BaseTipo sin BaseEntry/BaseLinea.");
        if (l.BaseTipo != BaseTipoEntregaCompra && l.BaseTipo != BaseTipoPedidoCompra)
            throw new Exception($"BaseTipo {l.BaseTipo} no es válido para una Factura de Compra (solo Entrega de Compra o Pedido de Compra).");
    }

    var deEntrega = conBase.Where(l => l.BaseTipo == BaseTipoEntregaCompra).ToList();
    if (deEntrega.Count > 0)
    {
        await ValidarContraAsync(codigoSnDestino, deEntrega, "el documento de Entrega de Compra",
            async () => await ObtenerAbiertosEntregaAsync(codigoSnDestino ?? string.Empty));
    }

    var dePedido = conBase.Where(l => l.BaseTipo == BaseTipoPedidoCompra).ToList();
    if (dePedido.Count > 0)
    {
        await ValidarContraAsync(codigoSnDestino, dePedido, "el documento de Pedido de Compra",
            async () => await ObtenerAbiertosPedidoAsync(codigoSnDestino ?? string.Empty));
    }
}

// Reutiliza la misma consulta de cantidad abierta que expone el endpoint "Abiertos" -- ver
// ObtenerAbiertosAsync en PedidoCompraDomain/EntregaCompraDomain (misma logica, duplicada aquí
// porque FacturaCompraDomain necesita validar contra AMBOS tipos de base, y los dos dominios de
// origen no se inyectan como IPedidoCompraDomain/IEntregaCompraDomain para no crear una
// dependencia circular entre dominios -- se consulta directo contra los repos genericos).
private async Task<Dictionary<(int Entry, int NoLinea), decimal>> ObtenerAbiertosEntregaAsync(string codigoSn)
{
    var entregas = (await (await _repoEntregaCompra.ObtenerTodoAsync())
        .Where(e => e.CodigoSn == codigoSn && e.Cancelado != "S")
        .ToListAsync()).ToDictionary(e => e.Entry);
    var lineasEntrega = (await (await _repoEntregaCompraDetalle.ObtenerTodoAsync()).ToListAsync())
        .Where(d => entregas.ContainsKey(d.Entry));
    var facturasCanceladas = (await (await _repoFacturaCompra.ObtenerTodoAsync())
        .Where(f => f.Cancelado == "S").Select(f => f.Entry).ToListAsync()).ToHashSet();
    var consumo = (await (await _repoDetalle.ObtenerTodoAsync())
        .Where(d => d.BaseTipo == BaseTipoEntregaCompra && d.BaseEntry != null && d.BaseLinea != null)
        .ToListAsync())
        .Where(d => !facturasCanceladas.Contains(d.Entry))
        .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
        .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

    return lineasEntrega.ToDictionary(
        l => (l.Entry, l.NoLinea),
        l => (l.Cantidad ?? 0m) - consumo.GetValueOrDefault((l.Entry, l.NoLinea), 0m));
}

private async Task<Dictionary<(int Entry, int NoLinea), decimal>> ObtenerAbiertosPedidoAsync(string codigoSn)
{
    var pedidos = (await (await _repoPedidoCompra.ObtenerTodoAsync())
        .Where(p => p.CodigoSn == codigoSn && p.Cancelado != "S")
        .ToListAsync()).ToDictionary(p => p.Entry);
    var lineasPedido = (await (await _repoPedidoCompraDetalle.ObtenerTodoAsync()).ToListAsync())
        .Where(d => pedidos.ContainsKey(d.Entry));

    var entregasCanceladas = (await (await _repoEntregaCompra.ObtenerTodoAsync())
        .Where(e => e.Cancelado == "S").Select(e => e.Entry).ToListAsync()).ToHashSet();
    var consumoEntrega = (await (await _repoEntregaCompraDetalle.ObtenerTodoAsync())
        .Where(d => d.BaseTipo == BaseTipoPedidoCompra && d.BaseEntry != null && d.BaseLinea != null)
        .ToListAsync())
        .Where(d => !entregasCanceladas.Contains(d.Entry))
        .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
        .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

    var facturasCanceladas = (await (await _repoFacturaCompra.ObtenerTodoAsync())
        .Where(f => f.Cancelado == "S").Select(f => f.Entry).ToListAsync()).ToHashSet();
    var consumoFacturaDirecta = (await (await _repoDetalle.ObtenerTodoAsync())
        .Where(d => d.BaseTipo == BaseTipoPedidoCompra && d.BaseEntry != null && d.BaseLinea != null)
        .ToListAsync())
        .Where(d => !facturasCanceladas.Contains(d.Entry))
        .GroupBy(d => (d.BaseEntry!.Value, d.BaseLinea!.Value))
        .ToDictionary(g => g.Key, g => g.Sum(d => d.Cantidad ?? 0m));

    return lineasPedido.ToDictionary(
        l => (l.Entry, l.NoLinea),
        l => (l.Cantidad ?? 0m) - consumoEntrega.GetValueOrDefault((l.Entry, l.NoLinea), 0m) - consumoFacturaDirecta.GetValueOrDefault((l.Entry, l.NoLinea), 0m));
}

private async Task ValidarContraAsync(
    string? codigoSnDestino, List<FacturaCompraDetalle> lineasDeEsaBase, string nombreDocumentoBase,
    Func<Task<Dictionary<(int Entry, int NoLinea), decimal>>> obtenerAbiertos)
{
    var abiertos = await obtenerAbiertos();
    var headersOrigen = lineasDeEsaBase.Select(l => l.BaseEntry!.Value).ToHashSet();

    // Validar pertenencia al mismo socio y estado de cancelacion del encabezado origen. El
    // repositorio a consultar depende de si la base es Entrega o Pedido de Compra.
    var esEntrega = lineasDeEsaBase[0].BaseTipo == BaseTipoEntregaCompra;
    if (esEntrega)
    {
        var entregas = (await (await _repoEntregaCompra.ObtenerTodoAsync()).ToListAsync())
            .Where(e => headersOrigen.Contains(e.Entry)).ToDictionary(e => e.Entry);
        foreach (var entry in headersOrigen)
        {
            if (!entregas.TryGetValue(entry, out var h))
                throw new Exception($"El documento base {entry} no existe.");
            if (h.CodigoSn != codigoSnDestino)
                throw new Exception($"El documento base {entry} pertenece a otro proveedor.");
            if (h.Cancelado == "S")
                throw new Exception($"El documento base {entry} está cancelado.");
        }
    }
    else
    {
        var pedidos = (await (await _repoPedidoCompra.ObtenerTodoAsync()).ToListAsync())
            .Where(p => headersOrigen.Contains(p.Entry)).ToDictionary(p => p.Entry);
        foreach (var entry in headersOrigen)
        {
            if (!pedidos.TryGetValue(entry, out var h))
                throw new Exception($"El documento base {entry} no existe.");
            if (h.CodigoSn != codigoSnDestino)
                throw new Exception($"El documento base {entry} pertenece a otro proveedor.");
            if (h.Cancelado == "S")
                throw new Exception($"El documento base {entry} está cancelado.");
        }
    }

    foreach (var grupo in lineasDeEsaBase.GroupBy(l => (l.BaseEntry!.Value, l.BaseLinea!.Value)))
    {
        if (!abiertos.TryGetValue(grupo.Key, out var abierta))
            throw new Exception($"La línea base {grupo.Key.Item1}/{grupo.Key.Item2} no existe.");

        var solicitado = grupo.Sum(l => l.Cantidad ?? 0m);
        if (solicitado > abierta)
            throw new Exception($"La cantidad solicitada para la línea base {grupo.Key.Item1}/{grupo.Key.Item2} excede lo pendiente ({abierta} disponible).");
    }
}
```

- [ ] **Step 4: Ejecutar la suite y verificar que pasan**

```powershell
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\" --filter "FullyQualifiedName~FacturaCompraDomainTests"
```

- [ ] **Step 5: Ejecutar la suite completa**

```powershell
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\"
```
Esperado: `0` errores, todos en verde (baseline + todos los tests agregados en Tasks 1-6).

- [ ] **Step 6: Commit**

```bash
git add API.Domain.Core/FacturaCompraDomain.cs API.Service.WebApi.Tests/Domain/FacturaCompraDomainTests.cs
git commit -m "fix(compras): FacturaCompraDomain no repostea inventario si la linea viene de una Entrega

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Fase 3 — Web: plomería y picker "Copiar desde"

### Task 7: `Web.ApiClient` y proxy MVC (`Abiertos`)

**Files:**
- Create: `Web.ApiClient/Dtos/DocumentoAbierto/LineaAbiertaDTO.cs`
- Create: `Web.ApiClient/Dtos/DocumentoAbierto/DocumentoAbiertoDTO.cs`
- Modify: `Web.ApiClient/Clientes/IPedidoCompraApiClient.cs`
- Modify: `Web.ApiClient/Clientes/PedidoCompraApiClient.cs`
- Modify: `Web.ApiClient/Clientes/IEntregaCompraApiClient.cs`
- Modify: `Web.ApiClient/Clientes/EntregaCompraApiClient.cs`
- Modify: `Web.UI/Controllers/PedidosCompraController.cs`
- Modify: `Web.UI/Controllers/EntregasCompraController.cs`

**Interfaces:**
- Produces: `IPedidoCompraApiClient.ObtenerAbiertosAsync(string codigoSn) : Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>>`;
  `IEntregaCompraApiClient.ObtenerAbiertosAsync(string codigoSn)` (mismo shape);
  `GET /PedidosCompra/Abiertos?codigoSn=...`, `GET /EntregasCompra/Abiertos?codigoSn=...` (Json).

- [ ] **Step 1: Crear los DTOs espejo**

`Web.ApiClient/Dtos/DocumentoAbierto/LineaAbiertaDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.DocumentoAbierto
{
    public class LineaAbiertaDTO
    {
        public int NoLinea { get; set; }
        public string CodArticulo { get; set; } = null!;
        public string? Descripcion { get; set; }
        public decimal Cantidad { get; set; }
        public decimal CantidadAbierta { get; set; }
        public decimal? Precio { get; set; }
        public string? CodAlmacen { get; set; }
        public string? CodigoImpuesto { get; set; }
    }
}
```

`Web.ApiClient/Dtos/DocumentoAbierto/DocumentoAbiertoDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.DocumentoAbierto
{
    public class DocumentoAbiertoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public DateTime? FechaDoc { get; set; }
        public List<LineaAbiertaDTO> Lineas { get; set; } = new();
    }
}
```

- [ ] **Step 2: Ampliar `IPedidoCompraApiClient`/`PedidoCompraApiClient`**

`Web.ApiClient/Clientes/IPedidoCompraApiClient.cs` — agregar `using Web.ApiClient.Dtos.DocumentoAbierto;`
y el método:

```csharp
Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn);
```

`Web.ApiClient/Clientes/PedidoCompraApiClient.cs` — agregar `using Web.ApiClient.Dtos.DocumentoAbierto;`
y:

```csharp
public Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn) =>
    GetAsync<IEnumerable<DocumentoAbiertoDTO>>($"{Recurso}/Abiertos?codigoSn={Uri.EscapeDataString(codigoSn)}");
```

(Agregar `using System;` si el archivo no lo tiene, para `Uri.EscapeDataString`.)

- [ ] **Step 3: Ampliar `IEntregaCompraApiClient`/`EntregaCompraApiClient`**

`Web.ApiClient/Clientes/IEntregaCompraApiClient.cs` — agregar `using Web.ApiClient.Dtos.DocumentoAbierto;`
y el método:

```csharp
Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn);
```

`Web.ApiClient/Clientes/EntregaCompraApiClient.cs` — agregar `using Web.ApiClient.Dtos.DocumentoAbierto;`
(y `using System;` si no está) y:

```csharp
public Task<Respuesta<IEnumerable<DocumentoAbiertoDTO>>> ObtenerAbiertosAsync(string codigoSn) =>
    GetAsync<IEnumerable<DocumentoAbiertoDTO>>($"{Recurso}/Abiertos?codigoSn={Uri.EscapeDataString(codigoSn)}");
```

- [ ] **Step 4: Agregar la acción proxy en `PedidosCompraController`**

`Web.UI/Controllers/PedidosCompraController.cs` — agregar `using Web.ApiClient.Dtos.DocumentoAbierto;`
si hace falta (el tipo de retorno lo trae `Json(...)` sin anotar, así que no es estrictamente
necesario, pero se agrega por claridad) y, junto a `BuscarSocios`:

```csharp
[HttpGet]
public async Task<IActionResult> Abiertos(string codigoSn)
{
    if (string.IsNullOrWhiteSpace(codigoSn)) return Json(new Respuesta<IEnumerable<DocumentoAbiertoDTO>> { Mensaje = "codigoSn es requerido." });
    var respuesta = await _pedidosCompra.ObtenerAbiertosAsync(codigoSn);
    return Json(respuesta);
}
```

(`_pedidosCompra` es el campo `IPedidoCompraApiClient` ya inyectado en el constructor existente de
este controller.)

- [ ] **Step 5: Agregar la acción proxy en `EntregasCompraController`**

`Web.UI/Controllers/EntregasCompraController.cs` — agregar `using
Web.ApiClient.Dtos.DocumentoAbierto;` si hace falta, y junto a `BuscarSocios`:

```csharp
[HttpGet]
public async Task<IActionResult> Abiertos(string codigoSn)
{
    if (string.IsNullOrWhiteSpace(codigoSn)) return Json(new Respuesta<IEnumerable<DocumentoAbiertoDTO>> { Mensaje = "codigoSn es requerido." });
    var respuesta = await _entregasCompra.ObtenerAbiertosAsync(codigoSn);
    return Json(respuesta);
}
```

(`_entregasCompra` es el campo `IEntregaCompraApiClient` ya inyectado en el constructor existente
de este controller.)

- [ ] **Step 6: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"
```
Esperado: `0` Errores.

- [ ] **Step 7: Commit**

```bash
git add Web.ApiClient/Dtos/DocumentoAbierto/LineaAbiertaDTO.cs Web.ApiClient/Dtos/DocumentoAbierto/DocumentoAbiertoDTO.cs Web.ApiClient/Clientes/IPedidoCompraApiClient.cs Web.ApiClient/Clientes/PedidoCompraApiClient.cs Web.ApiClient/Clientes/IEntregaCompraApiClient.cs Web.ApiClient/Clientes/EntregaCompraApiClient.cs Web.UI/Controllers/PedidosCompraController.cs Web.UI/Controllers/EntregasCompraController.cs
git commit -m "feat(compras): ApiClient y proxy MVC para el endpoint Abiertos

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 8: Picker "Copiar desde Pedido de Compra" en `entregascompra.js`

**Files:**
- Modify: `Web.UI/Views/EntregasCompra/Index.cshtml`
- Modify: `Web.UI/Views/EntregasCompra/_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/entregascompra.js`

**Interfaces:**
- Consumes: `GET /PedidosCompra/Abiertos?codigoSn=...` (Task 7).
- Produces: líneas agregadas a `lineasLocales` con `BaseTipo=11`, `BaseEntry`, `BaseLinea`; badge
  visual en `pintarDetalle()`.

**Precondición verificable:** `Web.UI/wwwroot/js/entregascompra.js` tiene hoy, en la misma forma
que `facturascompra.js` (verificado en el spec): `let lineasLocales = [];`, `function
pintarDetalle()`, `function aplicarArticuloSeleccionado(a, origen)`, un botón `#btnNuevaLinea` en
`_Form.cshtml`, y el guardado arma `datos.Lineas = lineasLocales.map(({ _id, ...linea }) =>
linea);` antes de `POST /EntregasCompra/Crear`. Confirmar con
`grep -n "lineasLocales\|pintarDetalle\|aplicarArticuloSeleccionado" Web.UI/wwwroot/js/entregascompra.js`
antes de tocar nada; si algún nombre difiere, adaptar los pasos de abajo a los nombres reales
encontrados (no renombrar nada existente).

- [ ] **Step 1: Agregar el modal del picker en `Index.cshtml`**

`Web.UI/Views/EntregasCompra/Index.cshtml` — agregar, después del `<div class="modal fade"
id="modalFormulario" ...>` existente y antes de `@section Scripts`:

```html
<div class="modal fade" id="modalCopiarDesde" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title" id="tituloCopiarDesde">Copiar desde Pedido de Compra</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <div class="table-responsive">
                    <table class="table table-sm table-hover align-middle">
                        <thead>
                            <tr>
                                <th></th>
                                <th>Documento</th>
                                <th>Artículo</th>
                                <th>Descripción</th>
                                <th>Cant. abierta</th>
                                <th>Cant. a copiar</th>
                            </tr>
                        </thead>
                        <tbody id="tblCopiarDesdeBody">
                            <tr><td colspan="6" class="text-center text-muted">Sin líneas abiertas</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
                <button type="button" class="btn btn-primary" id="btnAgregarCopiadas">Agregar seleccionadas</button>
            </div>
        </div>
    </div>
</div>
```

- [ ] **Step 2: Agregar el botón "Copiar desde Pedido de Compra" en `_Form.cshtml`**

`Web.UI/Views/EntregasCompra/_Form.cshtml` — en el bloque `!esEdicion` que ya tiene
`#btnNuevaLinea` (mismo `<div class="d-flex justify-content-between align-items-center mb-2">`
que en `FacturasCompra/_Form.cshtml`), agregar el botón nuevo junto a él:

```html
@if (!esEdicion)
{
    <button type="button" class="btn btn-sm btn-outline-secondary me-2" id="btnCopiarDesdePedido" disabled>
        <i class="fa-solid fa-copy me-1"></i>Copiar desde Pedido de Compra
    </button>
    <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevaLinea">
        <i class="fa-solid fa-plus me-1"></i>Agregar línea
    </button>
}
```

(Reemplaza el bloque `@if (!esEdicion) { <button ... id="btnNuevaLinea"> ... }` existente por este,
manteniendo el botón `#btnNuevaLinea` igual y agregando el nuevo antes.)

- [ ] **Step 3: Habilitar el botón solo con socio seleccionado**

`Web.UI/wwwroot/js/entregascompra.js` — en `inicializarBuscadorSocio()`, dentro de `onSeleccion`
(mismo bloque que ya actualiza `listaPrecioSocio`), agregar la habilitación/deshabilitación:

```javascript
onSeleccion: async s => {
    $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '');
    $('#btnCopiarDesdePedido').prop('disabled', !s);
    const nueva = s ? (s.numLstPrecio ?? s.NumLstPrecio ?? null) : null;
    if (nueva === listaPrecioSocio) return;
    listaPrecioSocio = nueva;
    await ofrecerRecalcularLineas();
}
```

- [ ] **Step 4: Implementar el picker (traer, pintar, agregar)**

`Web.UI/wwwroot/js/entregascompra.js` — agregar, después de `pintarDetalle()`:

```javascript
// --- Picker "Copiar desde..." ---

let candidatosCopiarDesde = []; // aplanado: una fila por linea abierta, con el doc al que pertenece

async function abrirPickerCopiarDesde() {
    const codigoSn = $('#CodigoSn').val();
    if (!codigoSn) return;

    const respuesta = await $.get('/PedidosCompra/Abiertos', { codigoSn });
    const documentos = (respuesta && respuesta.resultado && respuesta.dato) ? respuesta.dato : [];

    candidatosCopiarDesde = [];
    documentos.forEach(doc => {
        (doc.lineas ?? doc.Lineas ?? []).forEach(l => {
            candidatosCopiarDesde.push({
                docEntry: doc.entry ?? doc.Entry,
                docNumDoc: doc.numDoc ?? doc.NumDoc,
                noLinea: l.noLinea ?? l.NoLinea,
                codArticulo: l.codArticulo ?? l.CodArticulo,
                descripcion: l.descripcion ?? l.Descripcion,
                cantidadAbierta: Number(l.cantidadAbierta ?? l.CantidadAbierta ?? 0),
                precio: l.precio ?? l.Precio ?? 0,
                codAlmacen: l.codAlmacen ?? l.CodAlmacen ?? null,
                codigoImpuesto: l.codigoImpuesto ?? l.CodigoImpuesto ?? null
            });
        });
    });

    pintarPickerCopiarDesde();
    new bootstrap.Modal('#modalCopiarDesde').show();
}

function pintarPickerCopiarDesde() {
    const $tbody = $('#tblCopiarDesdeBody');
    if (candidatosCopiarDesde.length === 0) {
        $tbody.html('<tr><td colspan="6" class="text-center text-muted">Sin líneas abiertas</td></tr>');
        return;
    }
    $tbody.html(candidatosCopiarDesde.map((c, i) => `
        <tr>
            <td><input type="checkbox" class="chkCopiarLinea" data-idx="${i}"></td>
            <td>Pedido #${c.docNumDoc}</td>
            <td>${c.codArticulo}</td>
            <td>${c.descripcion ?? ''}</td>
            <td>${c.cantidadAbierta}</td>
            <td><input type="number" class="form-control form-control-sm inputCantidadCopiar" data-idx="${i}" min="0.01" max="${c.cantidadAbierta}" step="0.01" value="${c.cantidadAbierta}"></td>
        </tr>
    `).join(''));
}

$(document).on('click', '#btnCopiarDesdePedido', abrirPickerCopiarDesde);

$(document).on('click', '#btnAgregarCopiadas', function () {
    $('.chkCopiarLinea:checked').each(function () {
        const idx = Number($(this).data('idx'));
        const c = candidatosCopiarDesde[idx];
        const cantidad = Math.min(Number($(`.inputCantidadCopiar[data-idx="${idx}"]`).val()) || 0, c.cantidadAbierta);
        if (cantidad <= 0) return;

        const prctjeDesc = 0;
        const impuesto = 0; // sin tasa conocida en el picker; el usuario puede editar la linea despues de agregarla
        const totalLinea = Number((cantidad * c.precio).toFixed(2));

        lineasLocales.push({
            _id: proximoIdLocal++,
            CodArticulo: c.codArticulo, Descripcion: c.descripcion,
            Cantidad: cantidad, Precio: c.precio, PrecioBruto: c.precio,
            PrctjeDesc: prctjeDesc, CodigoImpuesto: c.codigoImpuesto, Impuesto: impuesto, TotalLinea: totalLinea,
            CodAlmacen: c.codAlmacen,
            BaseTipo: 11, BaseEntry: c.docEntry, BaseLinea: c.noLinea, baseDocNumDoc: c.docNumDoc
        });
    });
    bootstrap.Modal.getInstance(document.getElementById('modalCopiarDesde')).hide();
    pintarDetalle();
});
```

- [ ] **Step 5: Marca visual en `pintarDetalle()`**

`Web.UI/wwwroot/js/entregascompra.js` — en `pintarDetalle()`, dentro del `.map(linea => ...)`,
agregar la lectura de `baseEntry`/`baseDocNumDoc` y el badge en la celda de artículo:

```javascript
const baseEntry = linea.baseEntry ?? linea.BaseEntry;
const baseDocNumDoc = linea.baseDocNumDoc;
const badgeBase = baseEntry ? ` <span class="badge text-bg-info">Pedido #${baseDocNumDoc ?? baseEntry}</span>` : '';
```

y cambiar la celda `<td>${codArticulo ?? ''}</td>` por:

```javascript
<td>${codArticulo ?? ''}${badgeBase}</td>
```

- [ ] **Step 6: Verificación manual**

No hay pruebas automatizadas de JS. Verificar en el navegador (requiere la API corriendo y un
Pedido de Compra abierto para algún proveedor — ver checklist completo en el spec §3, Task 10 lo
repite):

1. Crear un Pedido de Compra a un proveedor con una línea de cantidad 10 (sin generar nada más
   sobre él).
2. Abrir "Nuevo" en Entregas de Compra, elegir ese proveedor → el botón "Copiar desde Pedido de
   Compra" se habilita.
3. Click → aparece el picker con esa línea, cantidad abierta = 10.
4. Tildarla, dejar cantidad en 4, "Agregar seleccionadas" → aparece en el grid con el badge
   "Pedido #<numDoc>".
5. Guardar → la Entrega se crea con esa línea (`BaseTipo=11`).

- [ ] **Step 7: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="...\scratchpad\webbuild\"
```

- [ ] **Step 8: Commit**

```bash
git add Web.UI/Views/EntregasCompra/Index.cshtml Web.UI/Views/EntregasCompra/_Form.cshtml Web.UI/wwwroot/js/entregascompra.js
git commit -m "feat(compras): picker Copiar desde Pedido de Compra en Entregas de Compra

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 9: Picker "Copiar desde Pedido/Entrega de Compra" en `facturascompra.js`

**Files:**
- Modify: `Web.UI/Views/FacturasCompra/Index.cshtml`
- Modify: `Web.UI/Views/FacturasCompra/_Form.cshtml`
- Modify: `Web.UI/wwwroot/js/facturascompra.js`

**Interfaces:**
- Consumes: `GET /PedidosCompra/Abiertos?codigoSn=...`, `GET /EntregasCompra/Abiertos?codigoSn=...`
  (Task 7).
- Produces: líneas agregadas a `lineasLocales` con `BaseTipo` 11 o 12 según el origen elegido.

**Precondición verificable:** mismo chequeo que en Task 8 pero sobre `facturascompra.js` (el
archivo completo ya se leyó para este plan — confirmado: `lineasLocales`, `pintarDetalle`,
`aplicarArticuloSeleccionado`, botón `#btnNuevaLinea`, guardado con `datos.Lineas =
lineasLocales.map(...)` antes de `POST /FacturasCompra/Crear`).

- [ ] **Step 1: Agregar el modal del picker en `Index.cshtml`**

`Web.UI/Views/FacturasCompra/Index.cshtml` — agregar, después del `<div class="modal fade"
id="modalFormulario" ...>` existente y antes de `@section Scripts` (un solo modal, reusado por los
dos botones — el título y los datos se parametrizan en JS, ver Step 4):

```html
<div class="modal fade" id="modalCopiarDesde" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-lg modal-dialog-scrollable">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title" id="tituloCopiarDesde">Copiar desde Pedido de Compra</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <div class="table-responsive">
                    <table class="table table-sm table-hover align-middle">
                        <thead>
                            <tr>
                                <th></th>
                                <th>Documento</th>
                                <th>Artículo</th>
                                <th>Descripción</th>
                                <th>Cant. abierta</th>
                                <th>Cant. a copiar</th>
                            </tr>
                        </thead>
                        <tbody id="tblCopiarDesdeBody">
                            <tr><td colspan="6" class="text-center text-muted">Sin líneas abiertas</td></tr>
                        </tbody>
                    </table>
                </div>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
                <button type="button" class="btn btn-primary" id="btnAgregarCopiadas">Agregar seleccionadas</button>
            </div>
        </div>
    </div>
</div>
```

- [ ] **Step 2: Agregar los dos botones en `_Form.cshtml`**

`Web.UI/Views/FacturasCompra/_Form.cshtml` — reemplazar el bloque `@if (!esEdicion) { <button ...
id="btnNuevaLinea"> ... }` por:

```html
@if (!esEdicion)
{
    <button type="button" class="btn btn-sm btn-outline-secondary me-2" id="btnCopiarDesdePedido" disabled>
        <i class="fa-solid fa-copy me-1"></i>Copiar desde Pedido de Compra
    </button>
    <button type="button" class="btn btn-sm btn-outline-secondary me-2" id="btnCopiarDesdeEntrega" disabled>
        <i class="fa-solid fa-copy me-1"></i>Copiar desde Entrega de Compra
    </button>
    <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevaLinea">
        <i class="fa-solid fa-plus me-1"></i>Agregar línea
    </button>
}
```

- [ ] **Step 3: Habilitar los botones solo con socio seleccionado**

`Web.UI/wwwroot/js/facturascompra.js` — en `inicializarBuscadorSocio()`, dentro de `onSeleccion`:

```javascript
onSeleccion: async s => {
    $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '');
    $('#btnCopiarDesdePedido, #btnCopiarDesdeEntrega').prop('disabled', !s);
    const nueva = s ? (s.numLstPrecio ?? s.NumLstPrecio ?? null) : null;
    if (nueva === listaPrecioSocio) return;
    listaPrecioSocio = nueva;
    await ofrecerRecalcularLineas();
}
```

- [ ] **Step 4: Implementar el picker parametrizado por origen**

`Web.UI/wwwroot/js/facturascompra.js` — agregar, después de `pintarDetalle()` (mismo cuerpo que
Task 8 Step 4, pero parametrizado por `origen`: `'pedido'` → endpoint `/PedidosCompra/Abiertos` y
`BaseTipo=11`; `'entrega'` → endpoint `/EntregasCompra/Abiertos` y `BaseTipo=12`):

```javascript
// --- Picker "Copiar desde..." ---

let candidatosCopiarDesde = [];
let origenCopiarDesde = null; // 'pedido' | 'entrega'

async function abrirPickerCopiarDesde(origen) {
    const codigoSn = $('#CodigoSn').val();
    if (!codigoSn) return;
    origenCopiarDesde = origen;

    const endpoint = origen === 'entrega' ? '/EntregasCompra/Abiertos' : '/PedidosCompra/Abiertos';
    const etiqueta = origen === 'entrega' ? 'Entrega' : 'Pedido';
    $('#tituloCopiarDesde').text(`Copiar desde ${etiqueta} de Compra`);

    const respuesta = await $.get(endpoint, { codigoSn });
    const documentos = (respuesta && respuesta.resultado && respuesta.dato) ? respuesta.dato : [];

    candidatosCopiarDesde = [];
    documentos.forEach(doc => {
        (doc.lineas ?? doc.Lineas ?? []).forEach(l => {
            candidatosCopiarDesde.push({
                docEntry: doc.entry ?? doc.Entry,
                docNumDoc: doc.numDoc ?? doc.NumDoc,
                noLinea: l.noLinea ?? l.NoLinea,
                codArticulo: l.codArticulo ?? l.CodArticulo,
                descripcion: l.descripcion ?? l.Descripcion,
                cantidadAbierta: Number(l.cantidadAbierta ?? l.CantidadAbierta ?? 0),
                precio: l.precio ?? l.Precio ?? 0,
                codAlmacen: l.codAlmacen ?? l.CodAlmacen ?? null,
                codigoImpuesto: l.codigoImpuesto ?? l.CodigoImpuesto ?? null
            });
        });
    });

    pintarPickerCopiarDesde(etiqueta);
    new bootstrap.Modal('#modalCopiarDesde').show();
}

function pintarPickerCopiarDesde(etiqueta) {
    const $tbody = $('#tblCopiarDesdeBody');
    if (candidatosCopiarDesde.length === 0) {
        $tbody.html('<tr><td colspan="6" class="text-center text-muted">Sin líneas abiertas</td></tr>');
        return;
    }
    $tbody.html(candidatosCopiarDesde.map((c, i) => `
        <tr>
            <td><input type="checkbox" class="chkCopiarLinea" data-idx="${i}"></td>
            <td>${etiqueta} #${c.docNumDoc}</td>
            <td>${c.codArticulo}</td>
            <td>${c.descripcion ?? ''}</td>
            <td>${c.cantidadAbierta}</td>
            <td><input type="number" class="form-control form-control-sm inputCantidadCopiar" data-idx="${i}" min="0.01" max="${c.cantidadAbierta}" step="0.01" value="${c.cantidadAbierta}"></td>
        </tr>
    `).join(''));
}

$(document).on('click', '#btnCopiarDesdePedido', () => abrirPickerCopiarDesde('pedido'));
$(document).on('click', '#btnCopiarDesdeEntrega', () => abrirPickerCopiarDesde('entrega'));

$(document).on('click', '#btnAgregarCopiadas', function () {
    const baseTipo = origenCopiarDesde === 'entrega' ? 12 : 11;
    const etiqueta = origenCopiarDesde === 'entrega' ? 'Entrega' : 'Pedido';

    $('.chkCopiarLinea:checked').each(function () {
        const idx = Number($(this).data('idx'));
        const c = candidatosCopiarDesde[idx];
        const cantidad = Math.min(Number($(`.inputCantidadCopiar[data-idx="${idx}"]`).val()) || 0, c.cantidadAbierta);
        if (cantidad <= 0) return;

        const totalLinea = Number((cantidad * c.precio).toFixed(2));

        lineasLocales.push({
            _id: proximoIdLocal++,
            CodArticulo: c.codArticulo, Descripcion: c.descripcion,
            Cantidad: cantidad, Precio: c.precio, PrecioBruto: c.precio,
            PrctjeDesc: 0, CodigoImpuesto: c.codigoImpuesto, Impuesto: 0, TotalLinea: totalLinea,
            CodAlmacen: c.codAlmacen,
            BaseTipo: baseTipo, BaseEntry: c.docEntry, BaseLinea: c.noLinea,
            baseDocNumDoc: c.docNumDoc, baseEtiqueta: etiqueta
        });
    });
    bootstrap.Modal.getInstance(document.getElementById('modalCopiarDesde')).hide();
    pintarDetalle();
});
```

- [ ] **Step 5: Marca visual en `pintarDetalle()`**

`Web.UI/wwwroot/js/facturascompra.js` — mismo cambio que Task 8 Step 5, pero usando la etiqueta
guardada en la línea (Pedido o Entrega, según de dónde vino):

```javascript
const baseEntry = linea.baseEntry ?? linea.BaseEntry;
const baseDocNumDoc = linea.baseDocNumDoc;
const baseEtiqueta = linea.baseEtiqueta ?? 'Doc';
const badgeBase = baseEntry ? ` <span class="badge text-bg-info">${baseEtiqueta} #${baseDocNumDoc ?? baseEntry}</span>` : '';
```

y cambiar `<td>${codArticulo ?? ''}</td>` por `<td>${codArticulo ?? ''}${badgeBase}</td>`.

- [ ] **Step 6: Verificación manual**

Checklist completo en el spec §3 (repetido en Task 10). Puntualmente para este task: repetir los
pasos 1-5 de Task 8 pero para Factura de Compra con origen "Pedido de Compra" (verificar que sí
mueve inventario) y con origen "Entrega de Compra" (verificar que NO mueve inventario).

- [ ] **Step 7: Build**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="...\scratchpad\webbuild\"
```

- [ ] **Step 8: Commit**

```bash
git add Web.UI/Views/FacturasCompra/Index.cshtml Web.UI/Views/FacturasCompra/_Form.cshtml Web.UI/wwwroot/js/facturascompra.js
git commit -m "feat(compras): picker Copiar desde Pedido/Entrega de Compra en Facturas de Compra

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 10: Verificación conjunta y checklist manual

**Files:** ninguno (task de verificación, sin cambios de código).

- [ ] **Step 1: Build y suite completa de la API**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
dotnet build API.sln -p:BaseOutputPath="...\scratchpad\apibuild\"
dotnet test API.sln -p:BaseOutputPath="...\scratchpad\apitest\"
```
Esperado: `0` Errores de build; suite completa en verde (baseline del inicio de este plan + todos
los tests de las Tasks 1-6).

- [ ] **Step 2: Build de la Web**

```powershell
cd C:\Users\migue\source\repos\angelm0508\Web
dotnet build Web.slnx -p:BaseOutputPath="...\scratchpad\webbuild\"
```
Esperado: `0` Errores, `0` Advertencias nuevas.

- [ ] **Step 3: Checklist manual (navegador, API + Web corriendo contra `API_DB_TEST`)**

1. Pedido de Compra a un proveedor, 1 línea `Cantidad = 10`. Crear una Entrega de Compra nueva
   para ese proveedor, "Copiar desde Pedido de Compra" → la línea aparece con abierta = 10;
   copiar 4 → la Entrega se guarda con `BaseTipo=11`. Volver a abrir el picker (otra Entrega
   nueva) sobre el mismo proveedor → la misma línea ahora muestra abierta = 6.
2. Con esa Entrega de 4 unidades ya creada: crear una Factura de Compra, "Copiar desde Entrega de
   Compra" → aparece la línea con abierta = 4; copiarla completa → la Factura se guarda. Verificar
   con una consulta a `Articulo.CantDisponible` (o la pantalla de Artículos) que **no** cambió al
   guardar la Factura (sí había cambiado al guardar la Entrega).
3. Sobre el mismo Pedido (6 unidades restantes): crear una Factura de Compra, "Copiar desde
   Pedido de Compra" directo → copiar las 6 → la Factura se guarda y el stock del artículo **sí**
   sube en 6 esta vez.
4. Repetir el picker sobre ese Pedido → ya no aparece (cantidad abierta = 0 en su única línea).
5. Multi-base: dos Pedidos de Compra abiertos al mismo proveedor → abrir el picker al crear una
   Entrega → aparecen líneas de ambos documentos en la misma tabla → tildar una de cada uno →
   ambas se agregan al grid con su `BaseEntry` distinto → guardar → la Entrega queda con dos
   líneas, cada una vinculada a su Pedido de origen.
6. Forzar una cantidad mayor a la abierta vía la API directamente (con una herramienta como
   Postman/curl, saltando el límite del input `max` del navegador) → el servidor rechaza con
   "excede lo pendiente".
7. Cancelar la Entrega de 4 unidades del paso 1 → volver a abrir el picker de Entrega sobre el
   Pedido original → la línea vuelve a mostrar abierta = 4 (se liberó automáticamente, sin tocar
   nada en el Pedido).

- [ ] **Step 4: Confirmar que no queda nada sin commitear salvo `appsettings.json`**

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
git status --porcelain
cd C:\Users\migue\source\repos\angelm0508\Web
git status --porcelain
```
Esperado: API muestra únicamente ` M API.Service.WebApi/appsettings.json` (si el usuario tiene su
connection string local); Web, árbol limpio.

- [ ] **Step 5: Reporte final**

No hay commit en este task — es de verificación. Usar
`superpowers:finishing-a-development-branch` para presentar las opciones de integración al
usuario una vez completadas las 9 tareas anteriores.
