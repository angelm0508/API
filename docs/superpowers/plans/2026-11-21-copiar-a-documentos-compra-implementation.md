# Copiar a… en la cadena de compras Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Poder crear una Entrega de compra o una Factura de compra a partir de un Pedido de compra (y una Factura de compra a partir de una Entrega de compra) con un botón «Copiar a» en el listado del origen, con cantidad abierta, validación de las líneas copiadas, inventario correcto y origen protegido, igual que ya existe en ventas.

**Architecture:** Un servicio paralelo `DocumentoBaseCompraService` (Domain.Core) con la misma forma que `DocumentoBaseService` de ventas (que no se toca) calcula la cantidad abierta en vivo, arma el borrador y valida las líneas con base y la integridad del origen; un endpoint `GET api/DocumentoBaseCompra/{tipoOrigen}/{entry}/Abierto?destino=` entrega el borrador; los dominios de Entrega y Factura de compra validan las líneas antes de numerar (y la factura solo asienta inventario de las líneas que no vienen de una entrega); los dominios de Pedido y Entrega de compra bloquean cancelar/eliminar/cambiar proveedor/editar líneas copiadas mientras tengan destinos activos; la Web agrega «Copiar a» en los listados y formularios precargados.

**Tech Stack:** .NET 7 / EF Core 7 / xUnit + Moq (API); .NET 8 MVC + jQuery + SweetAlert2 (Web).

**Spec:** `API/docs/superpowers/specs/2026-11-21-copiar-a-documentos-compra-design.md` (léelo antes de empezar). Referencia funcional ya implementada en ventas: `API/docs/superpowers/specs/2026-10-18-copiar-a-documentos-venta-design.md`, `API.Domain.Core/DocumentoBaseService.cs` y los commits Web `63f932f`, `ff40108`, `9167f36`, `9e05597`, `4bbdb90`, `41bbb87`.

## Global Constraints

- **Repos y ramas:** API `C:\Users\migue\source\repos\angelm0508\API` en `desarrollo` (HEAD al planificar: `eeac079`); Web `C:\Users\migue\source\repos\angelm0508\Web` en `main` (HEAD `5df66a1`). Trabajo directo en esas ramas, sin worktree. Lee el `AGENTS.md` de cada repo.
- **Git:** commits con rutas explícitas (`git add <archivo>`), **nunca** `git add -A` ni `git add .`. **Nunca** commitear `API.Service.WebApi/appsettings.json`, `sql/crear-api-db-completo*.sql`, `sql/temporal-*.sql` ni `.superpowers/`. **No hacer `push`.** Mensajes `tipo(modulo): descripcion` en español sin tildes. Todo commit termina con la línea `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (exactamente «Sonnet 5.5»). Conserva el fin de línea de cada archivo (autocrlf). `git status` de la API lista ~25 `*CrearDTO.cs` modificados solo por fin de línea: no los toques ni los commitees.
- **Capas:** `Application.*` depende solo de `Domain.Interface`; los contratos compartidos van en `Domain.Interface`. Respuestas siempre `Respuesta<T>`; la capa Application atrapa excepciones de negocio y devuelve `Resultado = false` con `ex.Message`. **Sin parámetros opcionales en interfaces de dominio** (rompen Moq, CS0854).
- **Ventas no se toca:** `DocumentoBaseService`, sus controlador/aplicación y los dominios de ventas no se modifican (el único cambio compartido es `DocumentoDerivado.Etiqueta`, ver Tarea 1). La suite de ventas debe seguir verde sin cambios de aserciones.
- **Mensajes de negocio:** literales en español; las pruebas los verifican. Validar **antes de numerar** (un rechazo no consume consecutivo).
- **Pruebas:** TDD (prueba primero y verla fallar), xUnit + Moq (`API.Service.WebApi.Tests`, patrón de `Mock<IRepositorioGenerico<…>>` + `.AsAsyncQueryable()` de `TestHelpers`, como `DocumentoBaseServiceTests`). Cada tarea cierra con build sin errores y suite completa en verde. **Línea base: 3918.** Si una API/Web en ejecución bloquea `bin`, compila/prueba con `-p:BaseOutputPath="$env:TEMP\gescom-build-cc\"` (con la barra final).
- **Constructores de dominios existentes:** al agregar `IDocumentoBaseCompraService` a un dominio, ponlo como **último** parámetro del constructor (sin valor por defecto) y actualiza **todas** las construcciones (grep `new <Dominio>(` en todo el repo, pruebas incluidas) con `Mock.Of<IDocumentoBaseCompraService>()`; verifica que ningún mock quede en una posición equivocada.
- **«Comprometido y Pedido»:** los dominios de compras ya avisan a `IExistenciaCompromisoService` **después de confirmar** y como último paso, con envoltorios que nunca lanzan (spec `2026-11-20-comprometido-y-pedido-*`). Las validaciones de esta función van **antes** de cualquier lectura previa (`ObtenerParesAsync`) y de cualquier cambio; un rechazo no avisa. No cambies esos avisos.
- **SQL real:** pruebas de humo **solo** en bases temporales `API_DB_EVID<n>` creadas con el script completo y eliminadas al terminar (scripts en `<scratchpad>`: `evid-up.ps1`, `evid-up-api.ps1`, `evid-down-api.ps1`, `pos-up-api.ps1`, `cv-comun.ps1`, `cp-*.ps1`; requieren `pwsh -ExecutionPolicy Bypass -File`; `evid-down-api.ps1` **elimina la base**; con `sqlcmd` usa `-I` para DML sobre `Articulo`). **Jamás** contra `API_DB_TEST`, `API_DB` ni una base del usuario. La contraseña de `sa` y la clave JWT **nunca** se imprimen, copian ni commitean; el usuario escribe las contraseñas en el navegador.
- **`<scratchpad>`** = `C:\Users\migue\AppData\Local\Temp\claude\9f8881082ead4f04\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad`.
- **Web:** escapa texto dinámico; `App.enviarJson`/antiforgery en POST; sin `console.log`; verifica con `dotnet build Web.slnx -p:BaseOutputPath="$env:TEMP\gescom-build-web\"` (con barra final) y `node --check` de cada JS tocado; la Web no tiene proyecto de pruebas: di qué no pudiste verificar en navegador.

## Rulings (decisiones tomadas al planificar)

1. **Servicio paralelo, sin tocar ventas.** `DocumentoBaseCompraService` repite la estructura de `DocumentoBaseService` (spec, enfoque B). Los registros compartidos (`LineaBaseRequest`, `LineaAbiertaVenta`, `CabeceraBorrador`, `BorradorDocumentoBase`, `DocumentoDerivado`, `LineasBase`, `DocumentoBaseMensajes`) se reutilizan.
2. **Sin parámetro opcional.** `IDocumentoBaseCompraService.ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea)` **no** lleva `= null` (el de ventas sí; aquí se evita CS0854). Quien no filtra por línea pasa `null`.
3. **«Activo».** Origen activo: `Cancelado != "S"` y `EstadoDoc != "C"`. Destino que consume: solo `Cancelado != "S"` (igual que `PedidoCompraDomain.ObtenerAbiertosAsync` y el recálculo de «Pedido»). Las facturas de compra que consumen son solo las de `DocSubType == "--"`.
4. **Cabecera del destino.** `EntregaCompraDomain` y `FacturaCompraDomain` fuerzan `BaseTipo = -1` y `BaseEntry = null` en la cabecera al insertar (la referencia vive solo en las líneas; `CK_FacturaCompra_Base` exige `-1` para facturas `--`).
5. **Entrega de compra como origen solo se valida al cancelar** (eliminar ya exige cancelarla antes; una entrega cancelada no puede tener derivados activos). **Pedido de compra** se valida al cancelar/cerrar, al cambiar de proveedor, al eliminar y al editar/quitar una línea copiada.
6. **El borrador reutiliza `DocumentoBaseBorradorDTO`** (Application.DTO) y duplica su mapeo en `DocumentoBaseCompraApplication` (~40 líneas): no se toca la aplicación de ventas.
7. **Inventario de la Factura de compra:** se asientan las líneas con `Cantidad > 0` y `BaseTipo != 12` (`BaseTipo` nulo o `-1` cuenta como «no viene de una entrega»).

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `API.Domain.Interface/IDocumentoBaseCompraService.cs` | `TipoDocumentoCompra`, `NombreDocumento`, `IDocumentoBaseCompraService` |
| `API.Domain.Interface/IDocumentoBaseService.cs` | solo `DocumentoDerivado.Etiqueta` → `NombreDocumento.De` |
| `API.Domain.Core/DocumentoBaseCompraService.cs` | cantidad abierta, borrador, validación de líneas, derivados, integridad |
| `API.Application.Interface/IDocumentoBaseCompraApplication.cs`, `API.Application.Main/DocumentoBaseCompraApplication.cs`, `API.Service.WebApi/Controllers/DocumentoBaseCompraController.cs`, `Startup.cs` | endpoint y DI |
| `API.Domain.Core/{EntregaCompra,FacturaCompra}Domain.cs` | validar líneas con base, cabecera `-1`, inventario `BaseTipo != 12` |
| `API.Domain.Core/{PedidoCompra,PedidoCompraDetalle,EntregaCompra}Domain.cs` | integridad del origen |
| `API.Service.WebApi.Tests/...` | pruebas de cada pieza |
| Web: `Web.ApiClient/Clientes/{IDocumentoBaseCompraApiClient,DocumentoBaseCompraApiClient}.cs`, `Web.UI/Program.cs`, `wwwroot/js/site.js`, `Controllers/{EntregasCompra,FacturasCompra}Controller.cs`, `Views/{EntregasCompra,FacturasCompra}/_Form.cshtml`, `Views/{PedidosCompra,EntregasCompra}/Index.cshtml`, `wwwroot/js/{entregascompra,facturascompra,pedidoscompra}.js`, `AGENTS.md` | cliente, formularios precargados, botón «Copiar a» |

---

### Task 1: Tipos compartidos y `DocumentoBaseCompraService`

**Files:**
- Create: `API.Domain.Interface/IDocumentoBaseCompraService.cs`, `API.Domain.Core/DocumentoBaseCompraService.cs`
- Modify: `API.Domain.Interface/IDocumentoBaseService.cs` (solo `DocumentoDerivado.Etiqueta`)
- Test: `API.Service.WebApi.Tests/Domain/DocumentoBaseCompraServiceTests.cs`; amplía `API.Service.WebApi.Tests/Domain/DocumentoBaseContratosTests.cs`

**Interfaces (produce; las usan las Tareas 2 a 4):**

```csharp
namespace API.Domain.Interface
{
    /// <summary>Tipos de objeto de la cadena de compras (coinciden con NumeracionDocumento.CodigoObj / TipoObjeto y con el BaseTipo de las líneas).</summary>
    public static class TipoDocumentoCompra
    {
        public const int PedidoCompra = 11;
        public const int EntregaCompra = 12;
        public const int FacturaCompra = 13;

        public static string Nombre(int tipo) => tipo switch
        {
            PedidoCompra => "Pedido de compra",
            EntregaCompra => "Entrega de compra",
            FacturaCompra => "Factura de compra",
            _ => $"Documento {tipo}"
        };

        public static IReadOnlyList<int> DestinosDe(int origen) => origen switch
        {
            PedidoCompra => new[] { EntregaCompra, FacturaCompra },
            EntregaCompra => new[] { FacturaCompra },
            _ => Array.Empty<int>()
        };

        public static bool EsCombinacionValida(int origen, int destino) => DestinosDe(origen).Contains(destino);
    }

    /// <summary>Nombre de un documento de ventas o de compras (los códigos no se solapan: ventas 3, 4, 5, 6, 70; compras 11, 12, 13).</summary>
    public static class NombreDocumento
    {
        public static string De(int tipo) =>
            tipo is TipoDocumentoCompra.PedidoCompra or TipoDocumentoCompra.EntregaCompra or TipoDocumentoCompra.FacturaCompra
                ? TipoDocumentoCompra.Nombre(tipo)
                : TipoDocumentoVenta.Nombre(tipo);
    }

    public interface IDocumentoBaseCompraService
    {
        /// <summary>Borrador del destino con las líneas abiertas del origen. Lanza Exception con mensaje de usuario.</summary>
        Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino);

        /// <summary>Valida las líneas con base de un destino que se va a crear (antes de numerar). No hace nada si no hay líneas.</summary>
        Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas);

        /// <summary>Documentos destino activos que usan el origen (o solo una línea de él si noLinea no es null).</summary>
        Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea);

        /// <summary>Lanza si el origen tiene derivados activos. accion: "cancelar el documento", "eliminar el documento", etc.</summary>
        Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion);

        /// <summary>Lanza si la línea del origen ya fue copiada a un documento activo. accion: "modificar" o "eliminar".</summary>
        Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion);
    }
}
```

Cambio en `IDocumentoBaseService.cs` (única modificación ahí): en el record `DocumentoDerivado`, `public string Etiqueta => $"{NombreDocumento.De(Tipo)} {NumDoc}";`.

**`DocumentoBaseCompraService`** (`API.Domain.Core`, copia la forma de `DocumentoBaseService`; este es el código completo):

```csharp
using System.Globalization;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// "Copiar a" de la cadena de compras (Pedido de compra → Entrega de compra → Factura de compra): cantidad abierta calculada
    /// en vivo, validación de las líneas copiadas e integridad del origen. Ver el spec 2026-11-21-copiar-a-documentos-compra-design.md.
    /// </summary>
    public class DocumentoBaseCompraService : IDocumentoBaseCompraService
    {
        private readonly IRepositorioGenerico<PedidoCompra, int> _pedido;
        private readonly IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> _pedidoDet;
        private readonly IRepositorioGenerico<EntregaCompra, int> _entrega;
        private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _entregaDet;
        private readonly IRepositorioGenerico<FacturaCompra, int> _factura;
        private readonly IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> _facturaDet;

        public DocumentoBaseCompraService(
            IRepositorioGenerico<PedidoCompra, int> pedido,
            IRepositorioGenerico<PedidoCompraDetalle, (int Entry, int NoLinea)> pedidoDet,
            IRepositorioGenerico<EntregaCompra, int> entrega,
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> entregaDet,
            IRepositorioGenerico<FacturaCompra, int> factura,
            IRepositorioGenerico<FacturaCompraDetalle, (int Entry, int NoLinea)> facturaDet)
        {
            _pedido = pedido;
            _pedidoDet = pedidoDet;
            _entrega = entrega;
            _entregaDet = entregaDet;
            _factura = factura;
            _facturaDet = facturaDet;
        }

        private sealed record Cab(int Entry, int NumDoc, string? CodigoSn, string? NombreSn, string? Direccion, string? MonedaDoc,
            decimal? PrctjeDesc, decimal? PrctjeImpuesto, string? Comentario, string? Cancelado, string? EstadoDoc, string? Nit)
        {
            /// <summary>Origen activo: ni cancelado ni cerrado.</summary>
            public bool Activo => Cancelado != "S" && EstadoDoc != "C";
            /// <summary>Un destino consume cantidad del origen mientras no esté cancelado (mismo criterio que «Pedido» de existencias).</summary>
            public bool Consume => Cancelado != "S";
            public string Etiqueta(int tipo) => $"{TipoDocumentoCompra.Nombre(tipo)} {NumDoc}";
        }

        private sealed record Lin(int Entry, int NoLinea, int? BaseTipo, int? BaseEntry, int? BaseLinea, string? CodArticulo, string? Descripcion,
            decimal? Cantidad, decimal? Precio, decimal? PrecioBruto, decimal? PrctjeDesc, string? CodigoImpuesto, decimal? Impuesto, string? CodAlmacen,
            int? UomEntry = null, string? UomCodigo = null, decimal FactorUom = 1m, decimal? CantidadBase = null);

        private sealed record Consumo(int TipoDestino, int EntryDestino, int NumDocDestino, int BaseLinea, decimal Cantidad);

        private static Cab Mapear(PedidoCompra h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc, h.Nit);
        private static Cab Mapear(EntregaCompra h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc, h.Nit);
        private static Cab Mapear(FacturaCompra h) => new(h.Entry, h.NumDoc, h.CodigoSn, h.NombreSn, h.Direccion, h.MonedaDoc, h.PrctjeDesc, h.PrctjeImpuesto, h.Comentario, h.Cancelado, h.EstadoDoc, h.Nit);

        private static Lin Mapear(PedidoCompraDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen, d.UomEntry, d.UomEntryNavigation?.Codigo, d.FactorUom, d.CantidadBase);
        private static Lin Mapear(EntregaCompraDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen, d.UomEntry, d.UomEntryNavigation?.Codigo, d.FactorUom, d.CantidadBase);
        private static Lin Mapear(FacturaCompraDetalle d) => new(d.Entry, d.NoLinea, d.BaseTipo, d.BaseEntry, d.BaseLinea, d.CodArticulo, d.Descripcion, d.Cantidad, d.Precio, d.PrecioBruto, d.PrctjeDesc, d.CodigoImpuesto, d.Impuesto, d.CodAlmacen, d.UomEntry, d.UomEntryNavigation?.Codigo, d.FactorUom, d.CantidadBase);

        /// <summary>Cabecera del documento; null si no existe (o si una «factura» es en realidad una nota de débito).</summary>
        private async Task<Cab?> CabeceraAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoCompra.PedidoCompra:
                    { var h = await _pedido.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoCompra.EntregaCompra:
                    { var h = await _entrega.ObtenerAsync(entry); return h is null ? null : Mapear(h); }
                case TipoDocumentoCompra.FacturaCompra:
                    { var h = await _factura.ObtenerAsync(entry); return h is null || h.DocSubType != "--" ? null : Mapear(h); }
                default:
                    return null;
            }
        }

        /// <summary>Líneas de un origen (pedido o entrega de compra), ordenadas por NoLinea.</summary>
        private async Task<List<Lin>> LineasDeAsync(int tipo, int entry)
        {
            switch (tipo)
            {
                case TipoDocumentoCompra.PedidoCompra:
                    return (await (await _pedidoDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                case TipoDocumentoCompra.EntregaCompra:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.Entry == entry).ToListAsync()).Select(d => Mapear(d)).OrderBy(l => l.NoLinea).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Líneas de las tablas destino cuyo BaseTipo/BaseEntry apuntan al origen indicado.</summary>
        private async Task<List<Lin>> LineasConBaseAsync(int tipoDestino, int tipoOrigen, int entryOrigen)
        {
            switch (tipoDestino)
            {
                case TipoDocumentoCompra.EntregaCompra:
                    return (await (await _entregaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                case TipoDocumentoCompra.FacturaCompra:
                    return (await (await _facturaDet.ObtenerTodoAsync()).Where(d => d.BaseTipo == tipoOrigen && d.BaseEntry == entryOrigen).ToListAsync()).Select(d => Mapear(d)).ToList();
                default:
                    return new List<Lin>();
            }
        }

        /// <summary>Consumos (cantidad copiada por línea origen) de los documentos destino que consumen (no cancelados).</summary>
        private async Task<List<Consumo>> ConsumosAsync(int tipoOrigen, int entry)
        {
            var consumos = new List<Consumo>();
            foreach (var destino in TipoDocumentoCompra.DestinosDe(tipoOrigen))
            {
                var lineas = await LineasConBaseAsync(destino, tipoOrigen, entry);
                foreach (var grupo in lineas.GroupBy(l => l.Entry))
                {
                    var cab = await CabeceraAsync(destino, grupo.Key);
                    if (cab is null || !cab.Consume) continue;
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

        public async Task<IReadOnlyList<DocumentoDerivado>> ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea)
        {
            var consumos = await ConsumosAsync(tipoOrigen, entry);
            return consumos
                .Where(c => c.Cantidad > 0m && (noLinea is null || c.BaseLinea == noLinea))
                .GroupBy(c => (c.TipoDestino, c.EntryDestino))
                .Select(g => new DocumentoDerivado(g.Key.TipoDestino, g.Key.EntryDestino, g.First().NumDocDestino, g.First().BaseLinea))
                .OrderBy(d => d.Tipo).ThenBy(d => d.Entry)
                .ToList();
        }

        public async Task<BorradorDocumentoBase> ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)
        {
            if (!TipoDocumentoCompra.EsCombinacionValida(tipoOrigen, tipoDestino))
                throw new Exception($"No se puede copiar {TipoDocumentoCompra.Nombre(tipoOrigen)} a {TipoDocumentoCompra.Nombre(tipoDestino)}.");

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
                var factor = l.FactorUom > 0m ? l.FactorUom : 1m;
                abiertas.Add(new LineaAbiertaVenta(tipoOrigen, entry, l.NoLinea, l.CodArticulo, l.Descripcion,
                    l.Cantidad ?? 0m, abierta, l.Precio, l.PrecioBruto, l.PrctjeDesc, l.CodigoImpuesto, l.Impuesto, l.CodAlmacen,
                    l.UomEntry, l.UomCodigo, factor, UnidadesLinea.CantidadBase(abierta, factor)));
            }

            if (abiertas.Count == 0)
                throw new Exception("El documento no tiene líneas abiertas.");

            return new BorradorDocumentoBase(tipoOrigen, entry, cab.NumDoc,
                new CabeceraBorrador(cab.CodigoSn, cab.NombreSn, cab.Direccion, cab.MonedaDoc, cab.PrctjeDesc, cab.PrctjeImpuesto, cab.Comentario, cab.Nit),
                abiertas);
        }

        public async Task ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas)
        {
            foreach (var documento in lineas.GroupBy(l => (l.BaseTipo, l.BaseEntry)))
            {
                var (tipoOrigen, entryOrigen) = documento.Key;

                if (!TipoDocumentoCompra.EsCombinacionValida(tipoOrigen, tipoDestino))
                    throw new Exception($"No se puede copiar {TipoDocumentoCompra.Nombre(tipoOrigen)} a {TipoDocumentoCompra.Nombre(tipoDestino)}.");

                var cab = await CabeceraAsync(tipoOrigen, entryOrigen)
                    ?? throw new Exception($"El documento base {TipoDocumentoCompra.Nombre(tipoOrigen)} {entryOrigen} no existe.");
                var etiqueta = cab.Etiqueta(tipoOrigen);

                if (!cab.Activo)
                    throw new Exception($"El documento base {etiqueta} está cancelado.");

                if (!string.Equals(cab.CodigoSn, codigoSnDestino, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"El proveedor del documento debe ser el del documento base {etiqueta} ({cab.CodigoSn}).");

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

        public async Task ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion)
        {
            var derivados = await ObtenerDerivadosAsync(tipoOrigen, entry, null);
            if (derivados.Count == 0) return;

            throw new Exception($"No se puede {accion}: lo usan {Listar(derivados.Select(d => d.Etiqueta))}. Cancele primero esos documentos.");
        }

        public async Task ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion)
        {
            var derivados = await ObtenerDerivadosAsync(tipoOrigen, entry, noLinea);
            if (derivados.Count == 0) return;

            throw new Exception($"No se puede {accion} la línea {noLinea}: la usan {Listar(derivados.Select(d => d.Etiqueta))}. Cancele primero esos documentos.");
        }
    }
}
```

- [ ] **Step 1: Pruebas de contratos (fallan)** en `DocumentoBaseContratosTests`: `TipoDocumentoCompra.DestinosDe(11)` = `{12, 13}`; `DestinosDe(12)` = `{13}`; `DestinosDe(13)` y `DestinosDe(4)` vacíos; `EsCombinacionValida(11,12)` y `(11,13)` y `(12,13)` verdaderas, `(12,12)`, `(13,12)`, `(12,11)` falsas; `Nombre(11)` «Pedido de compra», `Nombre(12)` «Entrega de compra», `Nombre(13)` «Factura de compra»; `NombreDocumento.De(4)` «Pedido», `De(6)` «Factura», `De(11)` «Pedido de compra»; `new DocumentoDerivado(6, 1, 2, 1).Etiqueta` = «Factura 2» (ventas sin cambio) y `new DocumentoDerivado(13, 1, 2, 1).Etiqueta` = «Factura de compra 2».
- [ ] **Step 2: Pruebas del servicio (fallan)** en `DocumentoBaseCompraServiceTests`. Copia el esqueleto de `DocumentoBaseServiceTests` (listas en memoria + `Mock<IRepositorioGenerico<…>>` con `ObtenerAsync` y `ObtenerTodoAsync().AsAsyncQueryable()`), con estas fábricas (artículo `A1`, proveedor `P1` por defecto):

```csharp
private readonly List<PedidoCompra> _pedidos = new();
private readonly List<PedidoCompraDetalle> _pedLineas = new();
private readonly List<EntregaCompra> _entregas = new();
private readonly List<EntregaCompraDetalle> _entLineas = new();
private readonly List<FacturaCompra> _facturas = new();
private readonly List<FacturaCompraDetalle> _facLineas = new();
private readonly DocumentoBaseCompraService _svc;
// ctor: _svc = new DocumentoBaseCompraService(_ped.Object, _pedDet.Object, _ent.Object, _entDet.Object, _fac.Object, _facDet.Object);

private PedidoCompra Ped(int entry, int numDoc, string sn = "P1", string? estadoDoc = "A", string? cancelado = "N")
{
    var p = new PedidoCompra { Entry = entry, NumDoc = numDoc, CodigoSn = sn, NombreSn = "Proveedor", Direccion = "Dir", Nit = "1234567-8",
        MonedaDoc = "QTZ", PrctjeDesc = 0m, PrctjeImpuesto = 12m, Comentario = "nota", EstadoDoc = estadoDoc, Cancelado = cancelado };
    _pedidos.Add(p);
    return p;
}
private void LineaPed(int entry, int noLinea, string art, decimal cant, decimal factor = 1m, int? uom = null) =>
    _pedLineas.Add(new PedidoCompraDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Descripcion = "Desc " + art, Cantidad = cant,
        Precio = 10m, PrecioBruto = 10m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = cant * 1.2m, CodAlmacen = "01", FactorUom = factor, UomEntry = uom, BaseTipo = -1 });
private EntregaCompra Ent(int entry, int numDoc, string sn = "P1", string? estadoDoc = "A", string? cancelado = "N") { /* igual, EntregaCompra */ }
private void LineaEnt(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) =>
    _entLineas.Add(new EntregaCompraDetalle { Entry = entry, NoLinea = noLinea, CodArticulo = art, Cantidad = cant, Precio = 10m,
        BaseTipo = baseTipo ?? -1, BaseEntry = baseEntry, BaseLinea = baseLinea });
private FacturaCompra Fac(int entry, int numDoc, string sn = "P1", string? estadoDoc = "A", string? cancelado = "N", string subTipo = "--") { /* igual, FacturaCompra con DocSubType = subTipo */ }
private void LineaFac(int entry, int noLinea, string art, decimal cant, int? baseTipo = null, int? baseEntry = null, int? baseLinea = null) { /* igual, FacturaCompraDetalle */ }
```

  Casos (todos con los números exactos de la tabla; `Ped(1, 5)` + `LineaPed(1, 1, "A1", 10m)` salvo que se indique):

| # | Escenario | Esperado |
|---|---|---|
| B1 | `ObtenerBorradorAsync(11, 1, 12)` sin consumo | 1 línea: `CantidadOrigen 10`, `CantidadAbierta 10`, `BaseTipo 11`, `BaseEntry 1`, `BaseLinea 1`, `CantidadBase 10`; `OrigenNumDoc 5`; cabecera `CodigoSn "P1"`, `Nit "1234567-8"`, `MonedaDoc "QTZ"` |
| B2 | Entrega `Ent(2,7)` con `LineaEnt(2,1,"A1",4m,11,1,1)` | `CantidadAbierta 6` |
| B3 | B2 + `Fac(3,9)` con `LineaFac(3,1,"A1",6m,11,1,1)` | `ObtenerBorradorAsync(11,1,13)` lanza «El documento no tiene líneas abiertas.» |
| B4 | La entrega de B2 con `Cancelado = "S"` | `CantidadAbierta 10` (libera) |
| B5 | La factura destino con `Cancelado = "S"` | no consume |
| B6 | Entrega destino con `EstadoDoc = "C"` y `Cancelado = "N"` | **sí consume** (regla de compras: solo `Cancelado`) → `CantidadAbierta 6` |
| B7 | Factura `Fac(3, 9, subTipo: "DN")` con línea `BaseTipo 11` apuntando al pedido | no consume (nota de débito) |
| B8 | Origen `Cancelado = "S"` | lanza «El documento origen está cancelado.» |
| B9 | Origen `EstadoDoc = "C"` | lanza «El documento origen está cancelado.» |
| B10 | Origen inexistente (`ObtenerBorradorAsync(11, 99, 12)`) | lanza `DocumentoBaseMensajes.OrigenNoExiste` |
| B11 | `ObtenerBorradorAsync(12, 1, 12)` | lanza «No se puede copiar Entrega de compra a Entrega de compra.» |
| B12 | Unidad: `LineaPed(1, 1, "A1", 3m, factor: 12m, uom: 5)` y una entrega con 1 | `CantidadAbierta 2`, `FactorUom 12`, `CantidadBase 24` |
| B13 | Dos líneas, una agotada | solo la línea con abierta > 0 en el borrador |
| B14 | Entrega como origen: `Ent(2,7)` con `LineaEnt(2,1,"A1",8m)`; factura `LineaFac(3,1,"A1",3m,12,2,1)` | `ObtenerBorradorAsync(12,2,13)` → `CantidadAbierta 5`, `BaseTipo 12` |
| V1 | `ValidarLineasAsync(12, "P1", [(11,1,1,"A1",6m)])` con abierta 10 | no lanza |
| V2 | Proveedor `"P2"` | lanza «El proveedor del documento debe ser el del documento base Pedido de compra 5 (P1).» |
| V3 | `BaseEntry 99` | lanza «El documento base Pedido de compra 99 no existe.» |
| V4 | Origen cancelado | lanza «El documento base Pedido de compra 5 está cancelado.» |
| V5 | `BaseLinea 9` | lanza «La línea 9 de Pedido de compra 5 no existe.» |
| V6 | Artículo `"A2"` | lanza «El artículo de la línea 1 de Pedido de compra 5 no se puede cambiar.» |
| V7 | Cantidad `0` | lanza «La cantidad de la línea 1 de Pedido de compra 5 debe ser mayor que cero.» |
| V8 | Abierta 6 (consumo 4 de 10), pide `7` | lanza «La cantidad de la línea 1 de Pedido de compra 5 excede la cantidad abierta (6).» |
| V9 | Abierta 6, pide exactamente `6` | no lanza |
| V10 | Dos líneas del payload contra la misma línea origen: `3` y `4` con abierta 6 | lanza «…excede la cantidad abierta (6).» |
| V11 | `ValidarLineasAsync(12, "P1", [(12,...)])` (entrega → entrega) | lanza «No se puede copiar Entrega de compra a Entrega de compra.» |
| V12 | Lista vacía | no lanza |
| I1 | `ObtenerDerivadosAsync(11, 1, null)` con `Ent(3,…)` y `Fac(2,…)` consumiendo | `["Entrega de compra 3", "Factura de compra 2"]` (orden por tipo y entry), `BaseLinea` correcta |
| I2 | `ValidarOrigenModificableAsync(11, 1, "cancelar el documento")` con esos derivados | lanza «No se puede cancelar el documento: lo usan Entrega de compra 3 y Factura de compra 2. Cancele primero esos documentos.» |
| I3 | Sin derivados | no lanza |
| I4 | `ValidarLineaModificableAsync(11, 1, 1, "modificar")` con la línea 1 copiada a la entrega 3 | lanza «No se puede modificar la línea 1: la usan Entrega de compra 3. Cancele primero esos documentos.» |
| I5 | `ValidarLineaModificableAsync(11, 1, 2, "eliminar")` con solo la línea 1 copiada | no lanza |
| I6 | Derivado cancelado | no bloquea |

- [ ] **Step 3:** `dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~DocumentoBaseContratosTests|FullyQualifiedName~DocumentoBaseCompraServiceTests" --nologo -v q` → falla (no compila).
- [ ] **Step 4:** Crea `IDocumentoBaseCompraService.cs` y `DocumentoBaseCompraService.cs` con el código de arriba y el cambio de `DocumentoDerivado.Etiqueta`.
- [ ] **Step 5:** pruebas verdes, build `API.sln` 0 errores, suite completa verde (las pruebas de ventas sin cambios; anota baseline y conteo).
- [ ] **Step 6: Commit** (rutas explícitas): `feat(compras): servicio de documento base para copiar a en compras`.

---

### Task 2: Aplicación, controlador y DI del endpoint

**Files:**
- Create: `API.Application.Interface/IDocumentoBaseCompraApplication.cs`, `API.Application.Main/DocumentoBaseCompraApplication.cs`, `API.Service.WebApi/Controllers/DocumentoBaseCompraController.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Application/DocumentoBaseCompraApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/DocumentoBaseCompraControllerTests.cs` (mira `DocumentoBaseApplicationTests` y `DocumentoBaseControllerTests` y copia su forma)

**Interfaces:**
- Consumes: `IDocumentoBaseCompraService.ObtenerBorradorAsync`, `TipoDocumentoCompra`, `DocumentoBaseMensajes.OrigenNoExiste` (Tarea 1); `DocumentoBaseBorradorDTO` (ya existe en `API.Application.DTO.documentoBase`).
- Produces: `IDocumentoBaseCompraApplication.ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)` → `Task<Respuesta<DocumentoBaseBorradorDTO>>`; `GET api/DocumentoBaseCompra/{tipoOrigen:int}/{entry:int}/Abierto?destino={int}`.

La aplicación copia el mapeo de `DocumentoBaseApplication` (mismo DTO), cambiando solo la dependencia a `IDocumentoBaseCompraService`; no se toca la de ventas. El controlador copia `DocumentoBaseController` con la ruta `[Route("api/DocumentoBaseCompra")]` y este `ModuloDe`:

```csharp
private static string? ModuloDe(int tipo) => tipo switch
{
    TipoDocumentoCompra.PedidoCompra => "PedidosCompra",
    TipoDocumentoCompra.EntregaCompra => "EntregasCompra",
    TipoDocumentoCompra.FacturaCompra => "FacturasCompra",
    _ => null
};
```

Misma lógica: `moduloOrigen`/`moduloDestino` nulo → 400 «El tipo de documento no es válido.»; sin `Ver` en el origen o sin `Agregar` en el destino (`PermisoUsuario.Tiene`) → `PermisoUsuario.Denegado()`; `Resultado` → 200; `Mensaje == DocumentoBaseMensajes.OrigenNoExiste` → 404; si no, 400. Registro en `Startup.cs` junto al de ventas: `services.AddTransient<IDocumentoBaseCompraService, DocumentoBaseCompraService>();` y `services.AddTransient<IDocumentoBaseCompraApplication, DocumentoBaseCompraApplication>();`.

- [ ] **Step 1: Pruebas (fallan).** Aplicación: éxito mapea cabecera (incluye `Nit`) y líneas (todos los campos, `UomEntry`, `UomCodigo`, `FactorUom`, `CantidadBase`) y `Resultado = true`; una excepción del servicio devuelve `Resultado = false` con `ex.Message`. Controlador: tipo inválido (`tipoOrigen = 4` o `destino = 5`) → 400 «El tipo de documento no es válido.»; sin `Ver` en el origen → 403; sin `Agregar` en el destino → 403; con ambos → 200 con el `Respuesta`; `OrigenNoExiste` → 404; otro mensaje → 400; el atributo de ruta es `api/DocumentoBaseCompra` y la acción `{tipoOrigen:int}/{entry:int}/Abierto`.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~DocumentoBaseCompraApplicationTests|FullyQualifiedName~DocumentoBaseCompraControllerTests"` → falla.
- [ ] **Step 3: Implementa** aplicación, controlador y registros.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(compras): endpoint del borrador de copiar a en compras`.

---

### Task 3: Dominios destino — Entrega y Factura de compra

**Files:**
- Modify: `API.Domain.Core/EntregaCompraDomain.cs`, `API.Domain.Core/FacturaCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/EntregaCompraDocumentoBaseTests.cs`, `FacturaCompraDocumentoBaseTests.cs` + constructores de las pruebas existentes (`EntregaCompraDomainTests`, `FacturaCompraDomainTests`, `EntregaCompraCompromisoTests`, `FacturaCompraCompromisoTests`, `UnidadesMedidaDocumentosTests` y cualquier otra: grep `new EntregaCompraDomain(` / `new FacturaCompraDomain(`)

**Interfaces:**
- Consumes: `IDocumentoBaseCompraService.ValidarLineasAsync(int, string?, IReadOnlyList<LineaBaseRequest>)`, `LineasBase.Extraer`, `TipoDocumentoCompra.EntregaCompra/FacturaCompra` (Tarea 1).
- Produces: ambos dominios con `IDocumentoBaseCompraService documentoBase` como **último** parámetro del constructor.

**Qué hacer (mira `EntregaDomain.cs:62-78` y `FacturaDomain.cs:75-97` como referencia exacta de ventas):**
- **Cabecera:** en `InsertarAsync` de ambos dominios, al inicio: `obj.BaseTipo = -1; obj.BaseEntry = null;` con el comentario «CK de la cabecera: la referencia al documento base vive únicamente en las líneas».
- **Validación:** después de armar `lineasList` (y, en la factura, después de `ValidarLineas(lineasList)`), **antes** de resolver unidades y de numerar: `var basadas = LineasBase.Extraer(lineasList.Select(l => (l.BaseTipo, l.BaseEntry, l.BaseLinea, l.CodArticulo, l.Cantidad))); if (basadas.Count > 0) await _documentoBase.ValidarLineasAsync(TipoDocumentoCompra.EntregaCompra /* o FacturaCompra */, obj.CodigoSn, basadas);`.
- **Inventario de la factura:** en la consulta de movimientos de `FacturaCompraDomain.InsertarAsync`, agrega `&& l.BaseTipo != TipoDocumentoCompra.EntregaCompra` al `.Where(l => (l.Cantidad ?? 0m) > 0m …)` (`BaseTipo` nulo o `-1` sigue asentando). `EntregaCompraDomain` asienta todo como hoy.
- No cambies los avisos de «Comprometido y Pedido» ni su posición (siguen después de la transacción, último paso).

- [ ] **Step 1: Pruebas (fallan)**, con `Mock<IDocumentoBaseCompraService>`: (a) con líneas `BaseTipo 11` se llama a `ValidarLineasAsync(12|13, proveedor, basadas)` exactamente una vez y **antes** de numerar (registra el orden en una lista compartida: `"validar"` antes de `"tx"`); (b) si `ValidarLineasAsync` lanza, el dominio propaga el mensaje, **no** se ejecuta la transacción y `serie.SigNumero` no cambia; (c) sin líneas con base (todas `BaseTipo -1`/nulo) **no** se llama; (d) la cabecera insertada tiene `BaseTipo == -1` y `BaseEntry == null` aunque el DTO traiga `BaseTipo 11`/`BaseEntry 5`; (e) factura: con líneas `BaseTipo 12` (sin asentar), `11`, `-1` y nulo, el `_asiento` recibe movimientos solo de las de `11`, `-1` y nulo (exactamente esos `DocLinea`), y con todas `12` no se llama a asentar o se llama con lista vacía según la implementación existente (verifica cómo se comporta hoy con lista vacía y asierta eso); (f) entrega: asienta todas las líneas con cantidad > 0 aunque tengan `BaseTipo 11`; (g) un rechazo de `ValidarLineasAsync` no dispara avisos a `IExistenciaCompromisoService` (`Times.Never`).
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~EntregaCompraDocumentoBaseTests|FullyQualifiedName~FacturaCompraDocumentoBaseTests"` → falla.
- [ ] **Step 3: Implementa** los cambios y actualiza todos los constructores.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(compras): entrega y factura de compra validan las lineas con documento base`.

---

### Task 4: Dominios origen — integridad de Pedido y Entrega de compra

**Files:**
- Modify: `API.Domain.Core/PedidoCompraDomain.cs`, `API.Domain.Core/PedidoCompraDetalleDomain.cs`, `API.Domain.Core/EntregaCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/PedidoCompraIntegridadTests.cs`, `PedidoCompraDetalleIntegridadTests.cs`, `EntregaCompraIntegridadTests.cs` + constructores de las pruebas existentes de `PedidoCompraDomain` y `PedidoCompraDetalleDomain` (`PedidoCompraDomainTests`, `PedidoCompraCompromisoTests`, y las que haga falta; `EntregaCompraDomain` ya recibe el servicio desde la Tarea 3)

**Interfaces:**
- Consumes: `IDocumentoBaseCompraService.ValidarOrigenModificableAsync(int, int, string)` y `ValidarLineaModificableAsync(int, int, int, string)`, `TipoDocumentoCompra` (Tarea 1); el parámetro `IDocumentoBaseCompraService` de `EntregaCompraDomain` (Tarea 3).
- Produces: `PedidoCompraDomain` y `PedidoCompraDetalleDomain` con `IDocumentoBaseCompraService documentoBase` como **último** parámetro del constructor.

**Dónde validar (referencia de ventas: `PedidoDomain.cs:176-184, 259`, `PedidoDetalleDomain.cs:54, 102`, `EntregaDomain.cs:165`). La validación va SIEMPRE antes de cualquier lectura previa de «Comprometido y Pedido» (`ObtenerParesAsync`) y de cualquier cambio; un rechazo no avisa:**
- `PedidoCompraDomain.ActualizarAsync(id, obj)`: ya lee `existente`. Si no es nulo, después de fijar `obj.Nit = existente.Nit`: `var seCancela = existente.EstadoDoc != "C" && existente.Cancelado != "S" && (obj.EstadoDoc == "C" || obj.Cancelado == "S"); var cambiaProveedor = !string.Equals(existente.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase); if (seCancela) await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoCompra.PedidoCompra, id, "cancelar el documento"); else if (cambiaProveedor) await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoCompra.PedidoCompra, id, "cambiar el proveedor");`. Todo antes de `_repoGenericoPedidoCompra.ActualizarAsync`.
- `PedidoCompraDomain.EliminarAsync(id)`: primera instrucción `await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoCompra.PedidoCompra, id, "eliminar el documento");`, antes de `ObtenerParesAsync`.
- `PedidoCompraDetalleDomain.ActualizarAsync(entry, noLinea, obj)`: primera instrucción `await _documentoBase.ValidarLineaModificableAsync(TipoDocumentoCompra.PedidoCompra, entry, noLinea, "modificar");` (antes de resolver unidades y de `ObtenerParesAsync`). `EliminarAsync(entry, noLinea)`: primera instrucción `ValidarLineaModificableAsync(..., "eliminar")`, antes de `ObtenerParesAsync`. `InsertarAsync` (agregar línea nueva) **no** valida: sigue permitido.
- `EntregaCompraDomain.ActualizarAsync`: dentro de `if (obj.Cancelado == "S")`, **antes** de `_tx.EjecutarAsync`: `await _documentoBase.ValidarOrigenModificableAsync(TipoDocumentoCompra.EntregaCompra, id, "cancelar el documento");`. No se valida al eliminar (ya exige cancelar antes) ni en la edición del comentario.

- [ ] **Step 1: Pruebas (fallan)**, con `Mock<IDocumentoBaseCompraService>` y la lista de orden compartida: (a) Pedido: cancelar por `EstadoDoc = "C"` y por `Cancelado = "S"` llama a `ValidarOrigenModificableAsync(11, id, "cancelar el documento")` una vez; cambiar `CodigoSn` llama con «cambiar el proveedor»; editar otros campos sin cancelar ni cambiar proveedor **no** valida; un pedido ya cancelado que se vuelve a guardar cancelado **no** valida; (b) si la validación lanza, el mensaje se propaga, **no** se llama a `_repo.ActualizarAsync`, ni a `ObtenerParesAsync`, ni a ningún aviso (`Times.Never`); (c) Eliminar: valida con «eliminar el documento» antes de `ObtenerParesAsync` (orden en la lista) y, si lanza, no se borra nada ni se avisa; (d) Línea: `Actualizar` y `Eliminar` llaman a `ValidarLineaModificableAsync(11, entry, noLinea, "modificar"|"eliminar")` **antes** de `ObtenerParesAsync`; si lanzan, no se guarda ni se avisa; `Insertar` no valida; (e) Entrega: cancelar llama a `ValidarOrigenModificableAsync(12, id, "cancelar el documento")` antes de la transacción y, si lanza, `_tx` no corre ni se avisa; editar solo el comentario no valida; (f) los mensajes del servicio se propagan tal cual (sin envolver).
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~PedidoCompraIntegridadTests|FullyQualifiedName~PedidoCompraDetalleIntegridadTests|FullyQualifiedName~EntregaCompraIntegridadTests"` → falla.
- [ ] **Step 3: Implementa** los cambios y actualiza los constructores de `PedidoCompraDomain` y `PedidoCompraDetalleDomain` en todas las pruebas.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(compras): pedido y entrega de compra protegidos mientras tengan destinos activos`.

---

### Task 5: Web — cliente, ayudantes y Entrega de compra precargada

**Files** (repo Web):
- Create: `Web.ApiClient/Clientes/IDocumentoBaseCompraApiClient.cs`, `Web.ApiClient/Clientes/DocumentoBaseCompraApiClient.cs`
- Modify: `Web.UI/Program.cs`, `Web.UI/wwwroot/js/site.js`, `Web.UI/Controllers/EntregasCompraController.cs`, `Web.UI/Views/EntregasCompra/_Form.cshtml`, `Web.UI/wwwroot/js/entregascompra.js`

**Interfaces:**
- Consumes: `GET api/DocumentoBaseCompra/{tipoOrigen}/{entry}/Abierto?destino=` → `Respuesta<DocumentoBaseBorradorDTO>` (Tarea 2). El DTO web `Web.ApiClient/Dtos/DocumentoBase/DocumentoBaseBorradorDTO.cs` ya existe (mismo contrato).
- Produces: `IDocumentoBaseCompraApiClient.ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)`; `/EntregasCompra/FormularioCrear?baseTipo=&baseEntry=`; el nombre de documento en `App.lineasDesdeBorrador` reconoce 11, 12 y 13.

**Qué hacer (modelo exacto: `git show 63f932f 9e05597 ff40108`; copia además el estado ACTUAL de `Web.UI/Controllers/EntregasController.cs`, `Views/Entregas/_Form.cshtml` y `wwwroot/js/entregas.js`, que ya incluyen el `Nit` del borrador y otros ajustes posteriores):**
1. **Cliente:** `IDocumentoBaseCompraApiClient`/`DocumentoBaseCompraApiClient` copian `IDocumentoBaseApiClient`/`DocumentoBaseApiClient` con la ruta `api/DocumentoBaseCompra/...`; registro en `Program.cs` como el de `IDocumentoBaseApiClient`.
2. **`site.js`:** en `lineasDesdeBorrador`, el mapa `nombres` pasa a `{ 3: 'Cotización', 4: 'Pedido', 5: 'Entrega', 6: 'Factura', 11: 'Pedido de compra', 12: 'Entrega de compra', 13: 'Factura de compra' }`. No cambies nada más de los ayudantes.
3. **`EntregasCompraController.FormularioCrear(int? baseTipo = null, int? baseEntry = null)`:** igual que `EntregasController.FormularioCrear` con `IDocumentoBaseCompraApiClient` y `TipoDestinoEntregaCompra = 12`; si hay borrador, rellena `CodigoSn`, `NombreSn`, `Direccion`, `Nit`, `MonedaDoc`, `PrctjeDesc`, `PrctjeImpuesto`, `Comentario` del DTO de creación (usa los nombres reales de `EntregaCompraCrearDTO`); si la API rechaza, `BadRequest(mensaje)`.
4. **`_Form.cshtml`:** `@using Web.ApiClient.Dtos.DocumentoBase`, `borrador`/`conBase`, título «Nueva entrega de compra (copia del documento N)», proveedor/nombre/dirección de solo lectura con base y el `<script id="datosBorrador" type="application/json">` con `.Replace("</", "<\\/")`, exactamente como en la Entrega de ventas.
5. **`entregascompra.js`:** `let borradorCopia = null;`, carga de las líneas del borrador al abrir (si no es edición), moneda bloqueada, texto «Base: …» debajo de la descripción, tope de cantidad (`_abierta`) y artículo fijo al editar una línea copiada, y `App.abrirCopiaSiCorresponde(... '/EntregasCompra/FormularioCrear' ...)` al final. Usa los identificadores reales de este archivo (`lineasLocales`, `proximoIdLocal`, `esEdicionDetalle()`, `abrirModal`…); `datos.Lineas` ya usa `App.sinCamposInternos`.
6. **Mensaje al guardar:** si el formulario viene de «Copiar a», el éxito dice «Entrega de compra N creada desde Pedido de compra M» como en ventas (si ventas no lo hace de forma especial, deja el mensaje normal y dilo en el informe).

- [ ] **Step 1:** cliente, registro y `site.js`. **Step 2:** controlador, vista y JS de la Entrega de compra. **Step 3:** `dotnet build Web.slnx -p:BaseOutputPath="$env:TEMP\gescom-build-web\"` 0 errores y `node --check` de `site.js` y `entregascompra.js`. **Step 4: Commit** (Web, rutas explícitas): `feat(compras): formulario de entrega de compra precargado desde copiar a`.

---

### Task 6: Web — Factura de compra precargada

**Files** (repo Web): Modify `Web.UI/Controllers/FacturasCompraController.cs`, `Web.UI/Views/FacturasCompra/_Form.cshtml`, `Web.UI/wwwroot/js/facturascompra.js`

**Interfaces:**
- Consumes: `IDocumentoBaseCompraApiClient` y el `site.js` de la Tarea 5.
- Produces: `/FacturasCompra/FormularioCrear?baseTipo=&baseEntry=` con el borrador de un Pedido de compra (11) o una Entrega de compra (12).

**Qué hacer:** igual que la Tarea 5 para la Factura de compra; modelo: `git show 4bbdb90` y el estado ACTUAL de `FacturasController.cs`, `Views/Facturas/_Form.cshtml` y `facturas.js`. `TipoDestinoFacturaCompra = 13`. Detalles propios de la factura de compra: la condición de pago y la fecha de vencimiento **no** vienen del borrador (siguen su comportamiento normal por proveedor); si una línea del borrador no trae almacén, se elige en el formulario (la validación de almacén ya existe); el proveedor, dirección y moneda quedan de solo lectura; el recálculo por proveedor no corre. Mensaje al guardar como en la Tarea 5.

- [ ] **Step 1:** controlador, vista y JS. **Step 2:** `dotnet build Web.slnx …` 0 errores y `node --check wwwroot/js/facturascompra.js`. **Step 3: Commit**: `feat(compras): formulario de factura de compra precargado desde copiar a`.

---

### Task 7: Web — botón «Copiar a» en los listados

**Files** (repo Web): Modify `Web.UI/Views/PedidosCompra/Index.cshtml`, `Web.UI/Views/EntregasCompra/Index.cshtml`, `Web.UI/wwwroot/js/pedidoscompra.js`, `Web.UI/wwwroot/js/entregascompra.js`, `Web/AGENTS.md`

**Interfaces:**
- Consumes: `App.botonCopiarA(tipoOrigen, entry, destinos)` y el manejador `.btn-copiar-a` de `site.js`; `/EntregasCompra` y `/FacturasCompra` abren su formulario precargado con `?baseTipo=&baseEntry=` (Tareas 5-6).
- Produces: botón desplegable «Copiar a» en cada fila no cancelada de Pedidos de compra (destinos: Entrega de compra, Factura de compra) y de Entregas de compra (destino: Factura de compra), solo con los destinos para los que el usuario tiene `Ver` y `Agregar`.

**Qué hacer (modelo exacto: `git show 41bbb87` y `Views/Pedidos/Index.cshtml` línea 3 `PuedeCopiar` y línea 47 `copiarA`):** en cada `Index.cshtml` agrega la función `PuedeCopiar(modulo)` (`User.TienePermiso(modulo, Accion.Ver) && User.TienePermiso(modulo, Accion.Agregar)`) y el objeto `copiarA` al JSON de permisos que el listado ya emite (`datosPermisos`): Pedidos de compra → `{ "entrega": PuedeCopiar("EntregasCompra"), "factura": PuedeCopiar("FacturasCompra") }`; Entregas de compra → `{ "factura": PuedeCopiar("FacturasCompra") }`. En cada JS, el valor por defecto de `permisos` (cuando no hay el elemento) incluye `copiarA` vacío, y en la columna de acciones se antepone `App.botonCopiarA(<tipoOrigen 11|12>, entry, destinos)` donde `destinos` se arma con `{ nombre: 'Entrega de compra', url: '/EntregasCompra' }` / `{ nombre: 'Factura de compra', url: '/FacturasCompra' }` filtrados por `permisos.copiarA`, y **solo** cuando la fila no está cancelada ni cerrada (`row.cancelado !== 'S'` y `row.estadoDoc !== 'C'`; usa los nombres reales de las propiedades del listado). Añade a `Web/AGENTS.md` una viñeta breve de «Copiar a» en compras (botón, destinos, permisos, endpoint `api/DocumentoBaseCompra`).

- [ ] **Step 1:** vistas y JS de los dos listados. **Step 2:** `dotnet build Web.slnx …` 0 errores y `node --check` de `pedidoscompra.js` y `entregascompra.js`. **Step 3: Commit** (Web): `feat(compras): boton copiar a en pedidos y entregas de compra`.

---

### Task 8: Humo con SQL real, documentación y cierre

**Files:** `<scratchpad>` (nada se commitea salvo correcciones con su prueba), `API/AGENTS.md`, `API/docs/traspaso-2026-11-07.md`

- [ ] **Step 1:** Crea una base temporal `API_DB_EVID<n>` (usa `220`) con `<scratchpad>\evid-up.ps1`/`evid-up-api.ps1` y levanta la API en el puerto 5199 con `pos-up-api.ps1` (lee antes `cv-comun.ps1` y los `cp-*.ps1` para el login como `admin` y el armado: almacenes `01` y `02`, impuesto, proveedor `P1` y `P2`, series de pedido/entrega/factura de compra, artículo `A1` inventariable, artículo no inventariable; **nunca** imprimas secretos).
- [ ] **Step 2: Flujos por la API con números a mano** (`GET api/Existencia/{articulo}/{almacen}` para existencias y SQL de solo lectura donde haga falta): (a) pedido de compra de 20 de `A1` → borrador `GET api/DocumentoBaseCompra/11/{entry}/Abierto?destino=12` con abierta 20; (b) entrega de compra copiada por 5 → stock +5, abierta 15, columna **Pedido** de Existencias 15; (c) factura de compra copiada de **esa entrega** por 5 → stock **sin cambio** (no se suma dos veces); (d) factura de compra copiada directamente del pedido por 15 → stock +15, Pedido 0; (e) copia que excede lo abierto (pide 100) → rechazada y el consecutivo de la serie no avanza; (f) cancelar la factura destino → la cantidad vuelve al pedido (abierta 15) y el stock baja 15; (g) intentar cancelar/eliminar el pedido con destinos → rechazado con «lo usan Entrega de compra N y Factura de compra M…»; intentar editar/quitar una línea copiada → rechazado con «la usan …»; agregar una línea nueva al pedido → permitido; (h) cancelar la entrega con una factura copiada de ella → rechazado; cancelar primero la factura y luego la entrega → permitido; (i) proveedor distinto en el destino → rechazado con el mensaje del proveedor; (j) combinación inválida (`11 → 11`, `13 → 12`) → 400; origen inexistente → 404; usuario sin `Agregar` en el destino o sin `Ver` en el origen → 403, sin token → 401; (k) unidad de medida (caja de 12) si el seed lo permite; (l) pedido con `EstadoDoc = 'C'` → el borrador lo rechaza; (m) ventas sin regresión: un «Copiar a» de ventas (Pedido → Entrega) sigue funcionando; (n) 0 errores 500 y ninguna advertencia de EF de no traducción ni «No se pudo recalcular» en el log.
- [ ] **Step 3:** Corrige con prueba xUnit de regresión (TDD) lo que no cuadre (`fix(compras): <qué>`); evidencia en `<scratchpad>\evid-copiar-a-compras-2026-11-21.txt` (escenario, esperado, obtenido, PASS/FAIL; sin secretos).
- [ ] **Step 4: Documentación.** `API/AGENTS.md`: cambia «Compras análogas (factura de compra = 13)» en la viñeta de documentos de venta para apuntar a la nueva viñeta y agrega **Copiar a en compras** (cadena 11→12/13 y 12→13, `DocumentoBaseCompraService`, endpoint y permisos, reglas de activo del origen vs del destino que consume, inventario de la factura `BaseTipo != 12`, origen protegido y qué dominios lo validan, cabecera `-1`, relación con «Pedido» de existencias, límites: concurrencia, `PedidoCompraDomain.ObtenerAbiertosAsync` sin uso) y actualiza la línea base de la suite. `docs/traspaso-2026-11-07.md`: sección «Copiar a en compras» (qué se hizo, cómo se verificó, pendientes: «Copiar desde» con varios orígenes, retirar `ObtenerAbiertosAsync`).
- [ ] **Step 5: Cierre.** Deja la base y la API vivas si habrá recorrido en navegador con el usuario (anota nombre, puerto y usuario de pruebas; sin contraseñas); si no, elimínalas con `evid-down-api.ps1` (léelo antes: solo afecta bases `API_DB_EVID<n>`) y verifica que no queda ninguna ni procesos en los puertos usados.
- [ ] **Step 6: Commit** (API, rutas explícitas): `docs(compras): copiar a en compras en agents y traspaso`.
