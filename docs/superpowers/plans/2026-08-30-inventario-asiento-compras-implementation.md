# Asiento de inventario en documentos de compra (INV-2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enganchar `IInventarioAsientoService` (INV-1) en el registro y la cancelación de `EntregaCompra` y `FacturaCompra`: al registrar (encabezado + líneas en una sola transacción) suma stock; al poner `Cancelado='S'` lo revierte. Editar un documento asentado solo permite cambiar `Comentario`; las líneas son inmutables.

**Architecture:** API N-capas (.NET 7). Nuevo `IEjecutorTransaccion` envuelve `BeginTransaction`/`SaveChanges`/commit/rollback para que los domains queden 100% mockeables. `EntregaCompraDomain.InsertarAsync(obj, lineas)` guarda el encabezado (→ `Entry`), añade las líneas con `AgregarSinGuardarAsync`, arma los `MovimientoRequest` y llama `AsentarAsync`; el `EjecutorTransaccion` hace el `SaveChangesAsync` final y el commit. `AsentarAsync`/`RevertirAsync` mutan el `ChangeTracker` sin guardar (contrato de INV-1). Web: el formulario de creación manda encabezado + líneas en una petición; el de edición muestra líneas en solo-lectura y ofrece "Cancelar documento".

**Tech Stack:** C# / .NET 7 (API) y .NET 8 (Web), EF Core (SQL Server), AutoMapper, xUnit + Moq, jQuery + Bootstrap.

**Spec:** `API/docs/superpowers/specs/2026-08-30-inventario-asiento-compras-design.md`

## Global Constraints

- **Repos y ramas:** API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`); Web en `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad git `panchoman08`. Sin push hasta aprobación final del usuario.
- **Build/test a carpeta externa:** `-p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"` (y `...\apitest\`, `...\webbuild\`).
- **No hay .NET 7 SDK**; el SDK 9/10 compila `net7.0`. No añadir `global.json`.
- **`dotnet test` de la suite completa de la API en verde** antes de terminar cualquier tarea que toque la API. Baseline actual: **644 pruebas, 0 fallos.**
- **`appsettings.json` (`API.Service.WebApi`) puede aparecer modificado localmente con un connection string real — NUNCA commitearlo.** Usar `git add` con rutas explícitas, nunca `git add -A`.
- **`TipoObjeto` / `TipoDoc`:** `EntregaCompra` = `"12"`, `FacturaCompra` = `"13"` (CHECK constraint). Se fuerza en el servidor.
- **Flags de las tablas de compra:** `EstadoDoc` `'A'`/`'C'` (abierto/cerrado — INV-2 no lo toca); `Cancelado` `'S'`/`'N'` (anulación — `'S'` dispara el reversó); `EstadoInv` `'A'`/`'C'` (asentado / revertido).
- **Contrato de INV-1 (no romper):** `IInventarioAsientoService.AsentarAsync` / `RevertirAsync` **nunca** llaman `SaveChangesAsync`. `IRepositorioGenerico.AgregarSinGuardarAsync` hace `DbSet.AddAsync` sin guardar. Signatura: `MovimientoRequest(string TipoDoc, int DocEntry, int DocLinea, string CodArticulo, string CodAlmacen, decimal Cantidad, decimal PrecioUnitario, DateTime Fecha)` — `Cantidad > 0` entrada.
- **`IEjecutorTransaccion.EjecutarAsync<T>(Func<Task<T>>)`:** al retornar sin excepción hace `SaveChangesAsync` + `Commit`; si lanza, `Rollback` y repropaga. Los domains **nunca** llaman `SaveChangesAsync` directo — todo va dentro de `EjecutarAsync`.
- **Edición post-asiento:** solo `Comentario` (confirmado contra SAP B1). Se ignoran cambios a socio/fechas/moneda/totales/serie/`NumDoc`/`EstadoDoc`.
- **Fuera de alcance:** ventas (INV-3), documentos Entrada/Salida de mercancías (INV-4), traslados, reserva (`Comprometido`/`Pedido`), reintento por `DbUpdateConcurrencyException`, descancelar, reprocesar documentos de compra ya existentes.

---

## Task 1: `IEjecutorTransaccion`, excepciones tipadas y validación de almacén en el asiento

**Files:**
- Create: `API.Domain.Interface/IEjecutorTransaccion.cs`
- Create: `API.Infraestructure.Repository/EjecutorTransaccion.cs`
- Create: `API.Domain.Core/Inventario/ExcepcionesInventario.cs`
- Modify: `API.Domain.Core/InventarioAsientoService.cs`
- Modify: `API.Service.WebApi/Startup.cs` (registrar `IEjecutorTransaccion`)
- Modify: `API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs`
- Test: `API.Service.WebApi.Tests/Infraestructure/EjecutorTransaccionTests.cs` (opcional, ver Step 8)

**Interfaces:**
- Produces:
  - `IEjecutorTransaccion.EjecutarAsync<T>(Func<Task<T>> operacion)` → `Task<T>`.
  - `ArticuloNoExisteException(string codArticulo)`, `AlmacenNoExisteException(string codAlmacen)`, `StockInsuficienteException(string codArticulo, string codAlmacen, decimal disponible, decimal requerido)` en `API.Domain.Core.Inventario`.
  - `InventarioAsientoService` gana ctor param `IRepositorioGenerico<Almacen, string> repoAlmacen` (al final de la lista) y lanza las excepciones tipadas.

- [ ] **Step 1: `IEjecutorTransaccion`**

`API.Domain.Interface/IEjecutorTransaccion.cs`:

```csharp
namespace API.Domain.Interface
{
    /// <summary>
    /// Ejecuta una operación dentro de una transacción de base de datos. Al terminar sin
    /// excepción: SaveChangesAsync (flushea todo lo que la operación dejó pendiente en el
    /// ChangeTracker) + Commit. Si la operación lanza: Rollback y se repropaga la excepción.
    /// Permite que los domains orquesten "guardar encabezado -> obtener Entry -> añadir
    /// líneas + inventario -> guardar" de forma atómica sin llamar SaveChangesAsync
    /// directamente (quedan 100% mockeables).
    /// </summary>
    public interface IEjecutorTransaccion
    {
        Task<T> EjecutarAsync<T>(Func<Task<T>> operacion);
    }
}
```

- [ ] **Step 2: `EjecutorTransaccion` (impl)**

`API.Infraestructure.Repository/EjecutorTransaccion.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class EjecutorTransaccion : IEjecutorTransaccion
    {
        private readonly ApiDbTestContext _context;

        public EjecutorTransaccion(ApiDbTestContext context)
        {
            _context = context;
        }

        public async Task<T> EjecutarAsync<T>(Func<Task<T>> operacion)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var resultado = await operacion();
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return resultado;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}
```

- [ ] **Step 3: Excepciones tipadas**

`API.Domain.Core/Inventario/ExcepcionesInventario.cs`:

```csharp
namespace API.Domain.Core.Inventario
{
    public class ArticuloNoExisteException : Exception
    {
        public ArticuloNoExisteException(string codArticulo)
            : base($"El artículo {codArticulo} no existe.") { }
    }

    public class AlmacenNoExisteException : Exception
    {
        public AlmacenNoExisteException(string codAlmacen)
            : base($"El almacén {codAlmacen} no existe.") { }
    }

    public class StockInsuficienteException : Exception
    {
        public StockInsuficienteException(string codArticulo, string codAlmacen, decimal disponible, decimal requerido)
            : base($"Stock insuficiente en el almacén {codAlmacen} para el artículo {codArticulo}: disponible {disponible}, requerido {requerido}.") { }
    }
}
```

- [ ] **Step 4: Actualizar `InventarioAsientoService`**

En `API.Domain.Core/InventarioAsientoService.cs`:

- Añadir `using API.Domain.Core.Inventario;`.
- Campo + ctor param nuevos:

```csharp
        private readonly IRepositorioGenerico<Almacen, string> _repoAlmacen;
```

Ctor: añadir `IRepositorioGenerico<Almacen, string> repoAlmacen` **al final** de la lista de parámetros y `_repoAlmacen = repoAlmacen;`.

- En `AplicarMovimientoAsync`, cambiar el throw de artículo inexistente:

```csharp
            var articulo = await _repoArticulo.ObtenerAsync(codArticulo)
                ?? throw new ArticuloNoExisteException(codArticulo);
```

- Justo después del chequeo `if (articulo.ArticuloInventario != "S") return;`, añadir la validación de almacén:

```csharp
            if (await _repoAlmacen.ObtenerAsync(codAlmacen) is null)
                throw new AlmacenNoExisteException(codAlmacen);
```

- Cambiar el throw de stock negativo:

```csharp
            if (nuevaDisponible < 0m && !permitirNegativo)
                throw new StockInsuficienteException(codArticulo, codAlmacen, existencia.Disponible, -cantidad);
```

- [ ] **Step 5: DI**

En `API.Service.WebApi/Startup.cs`, junto a los demás `AddTransient` (p. ej. cerca de los servicios de inventario):

```csharp
            services.AddTransient<IEjecutorTransaccion, EjecutorTransaccion>();
```

(El repo genérico de `Almacen` ya está registrado; `InventarioAsientoService` sigue registrado igual.)

- [ ] **Step 6: Actualizar `InventarioAsientoServiceTests`**

En `API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs`:

- Añadir `using API.Domain.Core.Inventario;`.
- Nuevo mock de repo de almacén + pasarlo al ctor:

```csharp
        private readonly Mock<IRepositorioGenerico<Almacen, string>> _repoAlmacen = new();
```

En el constructor del test, cambiar la construcción del servicio a:

```csharp
            _svc = new InventarioAsientoService(_repoArt.Object, _repoExist.Object, _repoMov.Object, new ValuacionInventario(), _repoAlmacen.Object);
            // ... setups existentes ...
            _repoAlmacen.Setup(r => r.ObtenerAsync(It.IsAny<string>())).ReturnsAsync(new Almacen { Codigo = "01" });
```

- `AsentarAsync_SalidaQueDejaNegativo_Lanza`: cambiar `Assert.ThrowsAsync<Exception>` → `Assert.ThrowsAsync<StockInsuficienteException>`.
- Si algún test verifica "artículo no existe", cambiar a `Assert.ThrowsAsync<ArticuloNoExisteException>` (si no existe ese test, no añadir uno nuevo por él).
- Nuevo test:

```csharp
        [Fact]
        public async Task AsentarAsync_AlmacenInexistente_Lanza()
        {
            ArticuloDeInventario("ART1");
            SinExistenciaPrevia();
            _repoAlmacen.Setup(r => r.ObtenerAsync("99")).ReturnsAsync((Almacen?)null);

            await Assert.ThrowsAsync<AlmacenNoExisteException>(
                () => _svc.AsentarAsync(new[] { new MovimientoRequest("11", 100, 1, "ART1", "99", 10m, 25m, new DateTime(2026, 8, 30)) }));
            Assert.Empty(_movAgregados);
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
Expected: ~645 passed (644 + 1 nuevo test de almacén), 0 fallos.

- [ ] **Step 8: (opcional) `EjecutorTransaccionTests`**

`IEjecutorTransaccion` real necesita un proveedor EF para ejercitarse; el proyecto de test no lo tiene. **No añadir un proveedor EF solo para esto.** Su corrección (commit al retornar, rollback al lanzar) se valida en la prueba manual del navegador (Task 6). Si el implementador quiere una prueba barata, puede añadir una que verifique que un doble simple del `Func` se ejecuta — pero no aporta cobertura real; **omitir** salvo criterio contrario, y anotarlo en el reporte.

- [ ] **Step 9: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Domain.Interface/IEjecutorTransaccion.cs API.Infraestructure.Repository/EjecutorTransaccion.cs API.Domain.Core/Inventario/ API.Domain.Core/InventarioAsientoService.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs
git commit -m "feat(api): IEjecutorTransaccion + excepciones tipadas de inventario + validacion de almacen en el asiento"
```

---

## Task 2: `EntregaCompra` — asiento atómico, cancelación, y bloqueo de edición

**Files:**
- Modify: `API.Application.DTO/entregaCompra/EntregaCompraCrearDTO.cs` (campo `Lineas`)
- Modify: `API.Domain.Interface/IEntregaCompraDomain.cs` (firma `InsertarAsync`)
- Modify: `API.Domain.Core/EntregaCompraDomain.cs` (reescritura)
- Modify: `API.Domain.Core/EntregaCompraDetalleDomain.cs` (guardas)
- Modify: `API.Application.Main/EntregaCompraApplication.cs` (mapear líneas)
- Modify: `API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs` (reescritura)
- Modify: `API.Service.WebApi.Tests/Controllers/EntregaCompraDetalleControllerTests.cs` — **solo si** rompen por la nueva guarda; ver Step 8.
- Create: `API.Service.WebApi.Tests/Domain/EntregaCompraDetalleDomainTests.cs`

**Interfaces:**
- Consumes: `IEjecutorTransaccion` (Task 1), `IInventarioAsientoService` (INV-1), `IRepositorioGenerico<EntregaCompra, int>`, `<EntregaCompraDetalle, (int Entry, int NoLinea)>`, `<NumeracionDocumentoDet, int>`, `MovimientoRequest`.
- Produces:
  - `IEntregaCompraDomain.InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas)` → `Task<int>` (reemplaza la de 1 parámetro).
  - `EntregaCompraCrearDTO.Lineas` (`List<EntregaCompraDetalleCrearDTO>`).

- [ ] **Step 1: `EntregaCompraCrearDTO` gana `Lineas`**

En `API.Application.DTO/entregaCompra/EntregaCompraCrearDTO.cs`, añadir el `using` y la propiedad:

```csharp
using API.Application.DTO.entregaCompra; // (ya está el namespace propio; el DetalleCrearDTO vive en el mismo)
```

```csharp
        /// <summary>
        /// Líneas del documento. Con INV-2 el documento se registra con sus líneas en una
        /// sola petición (y una sola transacción). El `Entry` de cada línea se ignora aquí
        /// (lo asigna el servidor al crear el encabezado).
        /// </summary>
        public List<EntregaCompraDetalleCrearDTO> Lineas { get; set; } = new();
```

(`EntregaCompraDetalleCrearDTO` está en `API.Application.DTO.entregaCompra`, mismo namespace — no hace falta `using` extra.)

- [ ] **Step 2: `IEntregaCompraDomain.InsertarAsync` cambia de firma**

`API.Domain.Interface/IEntregaCompraDomain.cs`:

```csharp
        Task<int> InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas);
```

(Se elimina `Task<int> InsertarAsync(EntregaCompra obj);`.)

- [ ] **Step 3: Reescribir `EntregaCompraDomain`**

`API.Domain.Core/EntregaCompraDomain.cs` completo:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class EntregaCompraDomain : IEntregaCompraDomain
    {
        // CHECK constraint de la tabla: TipoObjeto='12'. Se fuerza siempre en el servidor.
        private const string TipoObjetoEntregaCompra = "12";

        private readonly IRepositorioGenerico<EntregaCompra, int> _repoEntregaCompra;
        private readonly IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IEjecutorTransaccion _tx;
        private readonly IInventarioAsientoService _asiento;

        public EntregaCompraDomain(
            IRepositorioGenerico<EntregaCompra, int> repoEntregaCompra,
            IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento)
        {
            _repoEntregaCompra = repoEntregaCompra;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _tx = tx;
            _asiento = asiento;
        }

        #region async methods
        public async Task<int> InsertarAsync(EntregaCompra obj, IEnumerable<EntregaCompraDetalle> lineas)
        {
            obj.TipoObjeto = TipoObjetoEntregaCompra;
            obj.EstadoInv = "A";

            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar entregas de compra.");

            if (serie.Manual == "S")
            {
                if (obj.NumDoc <= 0)
                    throw new Exception("El número de documento es requerido para series manuales.");
            }
            else
            {
                if (serie.SigNumero == null)
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                    throw new Exception("Se agotó la numeración disponible en esta serie.");

                obj.NumDoc = serie.SigNumero.Value;
                serie.SigNumero = serie.SigNumero.Value + 1; // rastreada por el mismo contexto; se persiste en el Save de la transacción
            }

            var lineasList = lineas?.ToList() ?? new List<EntregaCompraDetalle>();

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoEntregaCompra.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                var movimientos = lineasList
                    .Where(l => (l.Cantidad ?? 0m) > 0m)
                    .Select(l => new MovimientoRequest(
                        TipoDoc: TipoObjetoEntregaCompra,
                        DocEntry: obj.Entry,
                        DocLinea: l.NoLinea,
                        CodArticulo: l.CodArticulo!,
                        CodAlmacen: l.CodAlmacen!,
                        Cantidad: l.Cantidad!.Value,
                        PrecioUnitario: l.Precio ?? 0m,
                        Fecha: obj.FechaDoc ?? DateTime.Now))
                    .ToList();

                await _asiento.AsentarAsync(movimientos);

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (líneas + inventario) + Commit
        }

        public async Task<bool> ActualizarAsync(int id, EntregaCompra obj)
        {
            var existente = await _repoEntregaCompra.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelación: Cancelado pasa a 'S'.
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await _asiento.RevertirAsync(TipoObjetoEntregaCompra, id);
                    existente.Cancelado = "S";
                    existente.EstadoInv = "C";
                    existente.FechaCancelado = DateTime.Now;
                    existente.Comentario = obj.Comentario;
                    return true;
                });
            }

            // Edición inocua: solo el comentario.
            return await _tx.EjecutarAsync(async () =>
            {
                existente.Comentario = obj.Comentario;
                return true;
            });
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var existente = await _repoEntregaCompra.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.EstadoInv == "A" && existente.Cancelado != "S")
                throw new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo.");

            // Documento cancelado (inventario ya revertido) o sin asiento: borrar líneas y encabezado.
            var detalles = await _repoDetalle.ObtenerTodoAsync();
            var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
            foreach (var linea in lineas)
                await _repoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));

            return await _repoEntregaCompra.EliminarAsync(id);
        }

        public async Task<EntregaCompra> ObtenerAsync(int id)
        {
            return await _repoEntregaCompra.ObtenerAsync(id);
        }

        public async Task<IQueryable<EntregaCompra>> ObtenerTodoAsync()
        {
            return await _repoEntregaCompra.ObtenerTodoAsync();
        }
        #endregion
    }
}
```

- [ ] **Step 4: Guardas en `EntregaCompraDetalleDomain`**

`API.Domain.Core/EntregaCompraDetalleDomain.cs`: inyectar el repo del encabezado y rechazar mutaciones sobre documentos existentes.

- Ctor: añadir `IRepositorioGenerico<EntregaCompra, int> repoEncabezado` y el campo `_repoEncabezado`.
- Método privado:

```csharp
        private async Task LanzarSiElDocumentoExisteAsync(int entry)
        {
            if (await _repoEncabezado.ObtenerAsync(entry) is not null)
                throw new Exception("Las líneas se definen al crear el documento y no se pueden modificar después.");
        }
```

- Al inicio de `InsertarAsync(EntregaCompraDetalle obj)`: `await LanzarSiElDocumentoExisteAsync(obj.Entry);`
- Al inicio de `ActualizarAsync(int entry, int noLinea, ...)`: `await LanzarSiElDocumentoExisteAsync(entry);`
- Al inicio de `EliminarAsync(int entry, int noLinea)`: `await LanzarSiElDocumentoExisteAsync(entry);`
- Los `Obtener*` no cambian.

- [ ] **Step 5: `EntregaCompraApplication` mapea las líneas**

En `API.Application.Main/EntregaCompraApplication.cs`, método `InsertarAsync`:

```csharp
        public async Task<Respuesta<int>> InsertarAsync(EntregaCompraCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var entregaCompra = _mapper.Map<EntregaCompra>(obj);
                var lineas = _mapper.Map<IEnumerable<EntregaCompraDetalle>>(obj.Lineas);
                respuesta.Dato = await _entregaCompraDomain.InsertarAsync(entregaCompra, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
```

(`ActualizarAsync` / `EliminarAsync` / los `Obtener*` no cambian.) El `CreateMap<EntregaCompraDetalleCrearDTO, EntregaCompraDetalle>()` ya existe en `PerfilMapeo.cs` (módulo de compra); AutoMapper mapea `List<X>` → `IEnumerable<Y>` automáticamente. Confirmar en el build.

- [ ] **Step 6: Reescribir `EntregaCompraDomainTests`**

`API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs` completo:

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
    public class EntregaCompraDomainTests
    {
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoHeader = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly EntregaCompraDomain _domain;

        private readonly List<MovimientoRequest> _movimientosAsentados = new();

        public EntregaCompraDomainTests()
        {
            _domain = new EntregaCompraDomain(_repoHeader.Object, _repoDetalle.Object, _repoNumeracion.Object, _tx.Object, _asiento.Object);
            // El doble del ejecutor corre el Func directo (sin transacción ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoHeader.Setup(r => r.InsertarAsync(It.IsAny<EntregaCompra>()))
                .ReturnsAsync((EntregaCompra c) => { c.Entry = 99; return c; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<EntregaCompraDetalle>())).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _movimientosAsentados.AddRange(ms))
                .Returns(Task.CompletedTask);
        }

        private static NumeracionDocumentoDet SerieAuto(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N") => new()
        {
            CodigoObj = "12", Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "N"
        };

        private static EntregaCompraDetalle Linea(string art, string alm, decimal? cant, decimal? precio) =>
            new() { CodArticulo = art, CodAlmacen = alm, Cantidad = cant, Precio = precio };

        [Fact]
        public async Task InsertarAsync_ConLineas_NumeraAsientaYMarcaEstadoInv()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new EntregaCompra { Serie = 4, FechaDoc = new DateTime(2026, 8, 30) };
            var lineas = new[] { Linea("ART1", "01", 10m, 25m), Linea("ART2", "01", 5m, 30m) };

            var entry = await _domain.InsertarAsync(obj, lineas);

            Assert.Equal(99, entry);
            Assert.Equal("12", obj.TipoObjeto);
            Assert.Equal("A", obj.EstadoInv);
            Assert.Equal(5, obj.NumDoc);
            // 2 líneas con Entry/NoLinea asignados
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<EntregaCompraDetalle>(l => l.Entry == 99 && l.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<EntregaCompraDetalle>(l => l.Entry == 99 && l.NoLinea == 2)), Times.Once);
            // 2 MovimientoRequest correctos
            Assert.Equal(2, _movimientosAsentados.Count);
            Assert.Equal(("12", 99, 1, "ART1", "01", 10m, 25m), (
                _movimientosAsentados[0].TipoDoc, _movimientosAsentados[0].DocEntry, _movimientosAsentados[0].DocLinea,
                _movimientosAsentados[0].CodArticulo, _movimientosAsentados[0].CodAlmacen,
                _movimientosAsentados[0].Cantidad, _movimientosAsentados[0].PrecioUnitario));
            Assert.Equal(2, _movimientosAsentados[1].DocLinea);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinCantidad_NoGeneraMovimiento()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new EntregaCompra { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", null, 25m), Linea("ART2", "01", 0m, 10m) });

            Assert.Empty(_movimientosAsentados);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza_YNoInserta()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(bloqueado: "S"));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4 }, new List<EntregaCompraDetalle>()));
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<EntregaCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 10, fin: 9));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4 }, new List<EntregaCompraDetalle>()));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(manual: "S"));
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4, NumDoc = 0 }, new List<EntregaCompraDetalle>()));
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompra { Serie = 4 }, new List<EntregaCompraDetalle>()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelado_S_RevierteYMarcaEstadoInvC()
        {
            var existente = new EntregaCompra { Entry = 7, Cancelado = "N", EstadoInv = "A" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(7, new EntregaCompra { Cancelado = "S", Comentario = "anulado" });

            Assert.True(ok);
            _asiento.Verify(a => a.RevertirAsync("12", 7), Times.Once);
            Assert.Equal("S", existente.Cancelado);
            Assert.Equal("C", existente.EstadoInv);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("anulado", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_YaCancelado_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7, Cancelado = "S" });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, new EntregaCompra { Comentario = "x" }));
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_Inocua_SoloCopiaComentario()
        {
            var existente = new EntregaCompra { Entry = 7, Cancelado = "N", EstadoInv = "A", CodigoSn = "SN-ORIG", MonedaDoc = "GTQ", Comentario = "viejo" };
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(7, new EntregaCompra { Comentario = "nuevo", CodigoSn = "SN-HACK", MonedaDoc = "USD" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("SN-ORIG", existente.CodigoSn);
            Assert.Equal("GTQ", existente.MonedaDoc);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_AsentadoNoCancelado_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7, EstadoInv = "A", Cancelado = "N" });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7));
            _repoHeader.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLineasYEncabezado()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7, EstadoInv = "C", Cancelado = "S" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<EntregaCompraDetalle>().AsAsyncQueryable()); // helper de TestHelpers: soporta ToListAsync
            _repoHeader.Setup(r => r.EliminarAsync(7)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(7);

            Assert.True(ok);
            _repoHeader.Verify(r => r.EliminarAsync(7), Times.Once);
        }
    }
}
```

- [ ] **Step 7: `EntregaCompraDetalleDomainTests` (nuevo)**

`API.Service.WebApi.Tests/Domain/EntregaCompraDetalleDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class EntregaCompraDetalleDomainTests
    {
        private readonly Mock<IRepositorioGenerico<EntregaCompraDetalle, (int Entry, int NoLinea)>> _repoDet = new();
        private readonly Mock<IRepositorioGenerico<EntregaCompra, int>> _repoHeader = new();
        private readonly EntregaCompraDetalleDomain _domain;

        public EntregaCompraDetalleDomainTests()
        {
            _domain = new EntregaCompraDetalleDomain(_repoDet.Object, _repoHeader.Object);
        }

        [Fact]
        public async Task InsertarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(new EntregaCompraDetalle { Entry = 7 }));
            _repoDet.Verify(r => r.InsertarAsync(It.IsAny<EntregaCompraDetalle>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(7, 1, new EntregaCompraDetalle()));
        }

        [Fact]
        public async Task EliminarAsync_DocumentoExiste_Lanza()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new EntregaCompra { Entry = 7 });
            await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(7, 1));
        }
    }
}
```

**Nota:** el ctor de `EntregaCompraDetalleDomain` cambió (Step 4). Verificar el orden real
de parámetros al escribir el test (repo de detalle primero, repo de encabezado segundo,
según Step 4).

- [ ] **Step 8: Build + arreglar tests que rompan por firma/ctor**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`. Si rompe:
- `EntregaCompraControllerTests` / `EntregaCompraDetalleControllerTests` (INV-1/compra) mockean `IEntregaCompraApplication` / `IEntregaCompraDetalleApplication`, **no** el dominio — no deberían romper por el cambio de firma del dominio. Si alguno construye `EntregaCompraDomain` directamente, ajustar el ctor.
- Cualquier test que llame `_domain.InsertarAsync(obj)` con 1 argumento → pasar a 2 (`obj, lineas`).
- Registro DI en `Startup.cs`: `EntregaCompraDomain` y `EntregaCompraDetalleDomain` siguen registrados con `AddTransient<IEntregaCompraDomain, EntregaCompraDomain>()` etc.; solo cambian los ctors (todas las deps ya registradas: `IEjecutorTransaccion` en Task 1, `IInventarioAsientoService` en INV-1, repos genéricos ya existen). **No** hace falta tocar `Startup.cs` en esta tarea.

- [ ] **Step 9: Suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 0 fallos. El conteo cambia (se reescriben ~7 tests de `EntregaCompraDomainTests` y se añaden 3 de `EntregaCompraDetalleDomainTests` + los nuevos de dominio); lo que importa: **0 fallos** y que los tests nuevos de EntregaCompra pasen.

- [ ] **Step 10: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Application.DTO/entregaCompra/ API.Domain.Interface/IEntregaCompraDomain.cs API.Domain.Core/EntregaCompraDomain.cs API.Domain.Core/EntregaCompraDetalleDomain.cs API.Application.Main/EntregaCompraApplication.cs API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs API.Service.WebApi.Tests/Domain/EntregaCompraDetalleDomainTests.cs
git commit -m "feat(api): EntregaCompra asienta inventario al registrar (encabezado+lineas atomico) y revierte al cancelar"
```

---

## Task 3: `FacturaCompra` — igual que Task 2 con sustitución

Aplicar **exactamente los mismos cambios de Task 2** a los archivos de `FacturaCompra`,
con esta tabla de sustitución:

| Task 2 (`EntregaCompra`) | Task 3 (`FacturaCompra`) |
|---|---|
| `EntregaCompra` (tipo, clase, variables) | `FacturaCompra` |
| `EntregaCompraDetalle` | `FacturaCompraDetalle` |
| `entregaCompra` (namespace DTO, carpeta) | `facturaCompra` |
| `TipoObjetoEntregaCompra` / `"12"` | `TipoObjetoFacturaCompra` / `"13"` |
| `IEntregaCompraDomain` / `IEntregaCompraDetalleDomain` | `IFacturaCompraDomain` / `IFacturaCompraDetalleDomain` |
| `EntregaCompraApplication` | `FacturaCompraApplication` |
| `ObtenerPorEntregaCompraAsync` (en el detalle domain) | `ObtenerPorFacturaCompraAsync` |
| "entregas de compra" / "entrega de compra" (mensajes) | "facturas de compra" / "factura de compra" |
| `EntregaCompraDomainTests` / `EntregaCompraDetalleDomainTests` | `FacturaCompraDomainTests` / `FacturaCompraDetalleDomainTests` |
| helper `SerieAuto` con `CodigoObj = "12"` | `CodigoObj = "13"` |
| aserciones `Assert.Equal("12", ...)` | `Assert.Equal("13", ...)` |

**Files:**
- Modify: `API.Application.DTO/facturaCompra/FacturaCompraCrearDTO.cs`
- Modify: `API.Domain.Interface/IFacturaCompraDomain.cs`
- Modify: `API.Domain.Core/FacturaCompraDomain.cs`
- Modify: `API.Domain.Core/FacturaCompraDetalleDomain.cs`
- Modify: `API.Application.Main/FacturaCompraApplication.cs`
- Modify: `API.Service.WebApi.Tests/Domain/FacturaCompraDomainTests.cs`
- Create: `API.Service.WebApi.Tests/Domain/FacturaCompraDetalleDomainTests.cs`

**Interfaces:**
- Produces: `IFacturaCompraDomain.InsertarAsync(FacturaCompra obj, IEnumerable<FacturaCompraDetalle> lineas)`; `FacturaCompraCrearDTO.Lineas`.

- [ ] **Step 1:** `FacturaCompraCrearDTO` gana `Lineas` (`List<FacturaCompraDetalleCrearDTO>`), como Task 2 Step 1.
- [ ] **Step 2:** `IFacturaCompraDomain.InsertarAsync` → 2 parámetros, como Task 2 Step 2.
- [ ] **Step 3:** Reescribir `FacturaCompraDomain` aplicando la tabla de sustitución al código completo de Task 2 Step 3. Verificar: `FacturaCompraDomain` hoy tiene la misma forma que `EntregaCompraDomain` (numeración idéntica, `EliminarAsync` borra líneas a mano) — la reescritura es 1:1.
- [ ] **Step 4:** Guardas en `FacturaCompraDetalleDomain` como Task 2 Step 4 (inyectar `IRepositorioGenerico<FacturaCompra, int>`).
- [ ] **Step 5:** `FacturaCompraApplication.InsertarAsync` mapea `obj.Lineas`, como Task 2 Step 5.
- [ ] **Step 6:** Reescribir `FacturaCompraDomainTests` aplicando la sustitución al código de Task 2 Step 6.
- [ ] **Step 7:** Crear `FacturaCompraDetalleDomainTests` aplicando la sustitución al código de Task 2 Step 7.
- [ ] **Step 8: Build**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 9: Suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: **0 fallos**; los tests nuevos de FacturaCompra presentes y en verde.

- [ ] **Step 10: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Application.DTO/facturaCompra/ API.Domain.Interface/IFacturaCompraDomain.cs API.Domain.Core/FacturaCompraDomain.cs API.Domain.Core/FacturaCompraDetalleDomain.cs API.Application.Main/FacturaCompraApplication.cs API.Service.WebApi.Tests/Domain/FacturaCompraDomainTests.cs API.Service.WebApi.Tests/Domain/FacturaCompraDetalleDomainTests.cs
git commit -m "feat(api): FacturaCompra asienta inventario al registrar y revierte al cancelar"
```

---

## Task 4: Web — `EntregasCompra` (crear con líneas embebidas, editar solo comentario, cancelar)

**Files:**
- Modify: `Web.ApiClient/Dtos/EntregaCompra/EntregaCompraCrearDTO.cs` (campo `Lineas`)
- Modify: `Web.UI/Controllers/EntregasCompraController.cs` (`Editar` reenvía `Cancelado`)
- Modify: `Web.UI/wwwroot/js/entregascompra.js` (crear en una petición; editar solo comentario; botón cancelar; líneas read-only en edición)
- Modify: `Web.UI/Views/EntregasCompra/_Form.cshtml` (ocultar edición de líneas en modo edición; botón "Cancelar documento")

**Interfaces:**
- Consumes: `api/EntregaCompra` (Task 2) — `Crear` ahora acepta `dto.Lineas`; `Editar` reacciona a `Cancelado='S'`.
- Produces: nada para tareas posteriores.

- [ ] **Step 1: `EntregaCompraCrearDTO` (Web) gana `Lineas`**

En `Web.ApiClient/Dtos/EntregaCompra/EntregaCompraCrearDTO.cs`, añadir:

```csharp
using Web.ApiClient.Dtos.EntregaCompraDetalle;
```
y la propiedad:
```csharp
        public List<EntregaCompraDetalleCrearDTO> Lineas { get; set; } = new();
```

- [ ] **Step 2: `EntregasCompraController.Editar` reenvía `Cancelado`**

En `Web.UI/Controllers/EntregasCompraController.cs`, en la construcción de
`EntregaCompraActualizarDTO` dentro de `Editar`, añadir la línea:

```csharp
                Cancelado = dto.Cancelado,
```

(El resto de campos se dejan como están: el dominio de la API ignora todo salvo
`Comentario` y `Cancelado`.)

- [ ] **Step 3: `entregascompra.js` — camino de crear en una sola petición**

En `Web.UI/wwwroot/js/entregascompra.js`, dentro del handler `#btnGuardarEntregaCompra`,
reemplazar **todo el bloque `if (!esEdicion) { ... return; }`** (el que hoy hace
`POST /Crear` y luego el `for (const linea of lineasLocales) POST /CrearLinea`) por:

```javascript
        if (!esEdicion) {
            datos.Lineas = lineasLocales.map(({ _id, ...linea }) => linea);

            const respuesta = await App.enviarJson('/EntregasCompra/Crear', 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }

            const sufijoNumDoc = respuesta.numDoc != null ? ` No. documento: ${respuesta.numDoc}.` : '';
            await App.mostrarExito(`Entrega de compra creada correctamente.${sufijoNumDoc}`);
            bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
            recargarTabla();
            return;
        }
```

(Se elimina el conteo "líneas guardadas: X de Y" y toda referencia a
`/EntregasCompra/CrearLinea` en el camino de creación.)

- [ ] **Step 4: `entregascompra.js` — edición: solo comentario, líneas bloqueadas, botón cancelar**

- El camino de edición (después del `return` del bloque de creación) que hace
  `POST /EntregasCompra/Editar?entry=${entry}` con `datos`: dejarlo — el dominio de la API
  ya ignora todo salvo `Comentario`. Cambiar solo el mensaje de éxito si hace falta.
- Añadir, dentro del `$(function () { ... })` del archivo, un handler para el botón de
  cancelar documento:

```javascript
    $(document).on('click', '#btnCancelarDocEntregaCompra', async function () {
        const entry = $(this).data('entry');
        const confirmado = await App.confirmarEliminar('Se cancelará este documento y se revertirá el inventario que ingresó. Esta acción no se puede deshacer.');
        if (!confirmado) return;

        const respuesta = await App.enviarJson(`/EntregasCompra/Editar?entry=${entry}`, 'POST', { Cancelado: 'S' });
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        bootstrap.Modal.getInstance(document.getElementById('modalFormulario')).hide();
        App.mostrarExito('Documento cancelado. El inventario fue revertido.');
        recargarTabla();
    });
```

- En modo edición, deshabilitar el grid de líneas: al cargar el formulario de edición
  (donde el JS hoy detecta `esEdicionDetalle()` / renderiza `lineasRemotas`), ocultar el
  botón "Agregar línea" y los botones de editar/eliminar de cada fila. La forma más simple
  y robusta: en `_Form.cshtml` (Step 5) envolver esos controles en `@if (!esEdicion) { ... }`
  o marcarlos con una clase y en el JS, si `esEdicion`, hacer
  `$('#btnNuevaLineaEntregaCompra, .btn-editar-linea, .btn-eliminar-linea').addClass('d-none');`
  El implementador elige; documentar cuál.

- [ ] **Step 5: `EntregasCompra/_Form.cshtml`**

En `Web.UI/Views/EntregasCompra/_Form.cshtml` (`bool esEdicion = ViewBag.EsEdicion ?? false;`
ya existe):

- El botón "Agregar línea" del detalle: envolver en `@if (!esEdicion) { ... }` (en
  edición no se agregan líneas).
- Las celdas de acciones por fila de detalle se pintan desde el JS; en edición el JS las
  oculta (Step 4). Alternativamente, pasar `data-es-edicion="@esEdicion.ToString().ToLower()"`
  al `<table>` del detalle y que el JS lo lea (ya se hace en otros `_Form`).
- Añadir el botón de cancelar en el `modal-footer` (o junto a "Guardar"), visible solo en
  edición y si el documento no está cancelado:

```html
    @if (esEdicion && (Model.Cancelado ?? "N") != "S")
    {
        <button type="button" class="btn btn-outline-danger" id="btnCancelarDocEntregaCompra" data-entry="@ViewBag.EntryActual">
            <i class="fa-solid fa-ban me-1"></i>Cancelar documento
        </button>
    }
```

- `FormularioEditar` del controller ya arma un `EntregaCompraCrearDTO`; añadirle
  `Cancelado = respuesta.Dato.Cancelado,` para que la vista pueda decidir. (Editar
  `Web.UI/Controllers/EntregasCompraController.cs` → `FormularioEditar`.)
- Si `Model.Cancelado == "S"`: el `_Form` puede además marcar los inputs del encabezado
  como `readonly`/`disabled` y ocultar "Guardar" — opcional pero recomendado; el
  implementador decide y documenta.

- [ ] **Step 6: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 7: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/Dtos/EntregaCompra/ Web.UI/Controllers/EntregasCompraController.cs Web.UI/wwwroot/js/entregascompra.js Web.UI/Views/EntregasCompra/_Form.cshtml
git commit -m "feat(web): EntregasCompra crea con lineas embebidas; edicion solo comentario; boton Cancelar documento"
```

---

## Task 5: Web — `FacturasCompra` (igual que Task 4 con sustitución)

Aplicar los mismos cambios de Task 4 con: `EntregaCompra`→`FacturaCompra`,
`EntregasCompra`→`FacturasCompra`, `entregascompra`→`facturascompra`,
`#btnGuardarEntregaCompra`→`#btnGuardarFacturaCompra`,
`#btnCancelarDocEntregaCompra`→`#btnCancelarDocFacturaCompra`, `/EntregasCompra/`→`/FacturasCompra/`,
`#btnNuevaLineaEntregaCompra`→`#btnNuevaLineaFacturaCompra`, textos "entrega de compra"→"factura de compra".

**Files:**
- Modify: `Web.ApiClient/Dtos/FacturaCompra/FacturaCompraCrearDTO.cs`
- Modify: `Web.UI/Controllers/FacturasCompraController.cs`
- Modify: `Web.UI/wwwroot/js/facturascompra.js`
- Modify: `Web.UI/Views/FacturasCompra/_Form.cshtml`

- [ ] **Step 1:** `FacturaCompraCrearDTO` (Web) gana `Lineas` (`List<FacturaCompraDetalleCrearDTO>`), como Task 4 Step 1.
- [ ] **Step 2:** `FacturasCompraController.Editar` añade `Cancelado = dto.Cancelado,`.
- [ ] **Step 3:** `facturascompra.js` — camino de crear en una petición, como Task 4 Step 3.
- [ ] **Step 4:** `facturascompra.js` — botón `#btnCancelarDocFacturaCompra` + líneas read-only en edición, como Task 4 Step 4.
- [ ] **Step 5:** `FacturasCompra/_Form.cshtml` + `FacturasCompraController.FormularioEditar` (`Cancelado`), como Task 4 Step 5.
- [ ] **Step 6: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 7: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/Dtos/FacturaCompra/ Web.UI/Controllers/FacturasCompraController.cs Web.UI/wwwroot/js/facturascompra.js Web.UI/Views/FacturasCompra/_Form.cshtml
git commit -m "feat(web): FacturasCompra crea con lineas embebidas; edicion solo comentario; boton Cancelar documento"
```

---

## Task 6: Verificación final conjunta

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
Expected: **0 fallos**.

- [ ] **Step 3: Build completo de la Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 4: Prueba manual en el navegador (para el usuario)**

Levantar API + Web, iniciar sesión. Necesita al menos un artículo con `ArticuloInventario='S'`
y un almacén.

1. **EntregaCompra**: menú Compras → Entregas de compra → Nuevo. Serie por defecto, socio
   proveedor, 2 líneas del mismo artículo (cantidades y precios distintos) → Guardar.
   - En Inventario → Existencias: el disponible del almacén subió por la suma de las 2
     cantidades.
   - Kardex del artículo: 2 movimientos con `TipoDoc=12`, saldos corridos, y
     `Articulo.CostoPromedio` = promedio móvil ponderado.
2. Editar esa entrega: cambiar el comentario → se guarda. Confirmar que **no** hay botón
   "Agregar línea" ni se pueden editar/eliminar filas del detalle.
3. En el formulario de edición, "Cancelar documento" → confirmar.
   - Kardex: aparecen los movimientos inversos; el disponible vuelve al valor previo;
     `EstadoInv='C'`.
   - Intentar eliminar la entrega **antes** de cancelar (en otra creada) → error "Cancele
     el documento…". Eliminar una ya cancelada → se borra.
4. Repetir 1-3 para **FacturaCompra** (`TipoDoc=13`).
5. Forzar un error: crear una EntregaCompra con una línea cuyo `CodAlmacen` no exista (si
   la UI lo permite; si no, vía API) → la respuesta es error y **no** queda documento ni
   movimiento (rollback).

- [ ] **Step 5: Recordatorio para el usuario**

Imprimir:
- Reiniciar las sesiones de depuración de Visual Studio (API y Web.UI).
- Los documentos de compra creados **antes** de INV-2 tienen `EstadoInv='A'` pero sin
  movimientos de inventario; no se reprocesan. Para sembrar inventario a partir de ellos,
  usar el documento Entrada de Mercancías de INV-4 (o un script).
- Siguiente fase: **INV-3** (asiento en documentos de venta — salida de stock con bloqueo
  de negativo).

- [ ] **Step 6: Commit final (si quedó algo suelto)**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add docs/ && git commit -m "chore: cierre INV-2" || echo "nada que commitear"
```

---

## Notas de auto-revisión (cobertura del spec)

- **§0 `IEjecutorTransaccion`** (interfaz + impl + DI + contrato commit/rollback) → Task 1 Steps 1-2, 5.
- **§1 Excepciones tipadas + validación de almacén** en `InventarioAsientoService` + tests → Task 1 Steps 3-4, 6.
- **§2 `EntregaCompra`**: DTO `Lineas`, `InsertarAsync(obj, lineas)` atómico vía `EjecutarAsync`, `EstadoInv='A'`, `MovimientoRequest` por línea con `Cantidad>0`, cancelación por `Cancelado='S'` → `RevertirAsync` + `EstadoInv='C'` + `FechaCancelado`, edición inocua solo `Comentario`, `Eliminar` bloqueado si asentado-no-cancelado, guardas en el detalle domain → Task 2.
- **§3 `FacturaCompra`** idéntico con `"13"` → Task 3.
- **Web §**: DTO `Lineas`, crear en una petición (sin loop `CrearLinea`), `Editar` reenvía `Cancelado`, `_Form` edición solo comentario + líneas read-only + botón "Cancelar documento" → Tasks 4 y 5.
- **Pruebas** (dominio de ambos documentos + detalle domains + asiento service actualizado) → Tasks 1-3; verificación conjunta + manual → Task 6.
- **Riesgo `EjecutorTransaccion` sin test unitario** → Task 1 Step 8 (omitido a propósito; validado en Task 6 Step 4).
- **Migración: documentos previos no se reprocesan** → Task 6 Step 5 (recordatorio).
- **Fuera de alcance** (ventas, mercancías, traslados, reserva, reintento concurrencia) → Global Constraints; sin tareas.
