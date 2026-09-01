# Asiento de inventario en documentos de venta (INV-3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enganchar `IInventarioAsientoService` (INV-1) en el registro y la cancelación de `Entrega` de venta (`TipoObjeto="5"`) y `Factura` de venta (`TipoObjeto="6"`): al registrar (encabezado + líneas en una transacción) **sale** stock; al poner `Cancelado='S'` el stock **reingresa**. El stock negativo se bloquea de forma dura. Editar un documento asentado solo permite cambiar `Comentario`.

**Architecture:** Espejo exacto de INV-2 (compras), ya en `main`. La cantidad del `MovimientoRequest` va en **negativo** (salida) y `AsentarAsync` se llama con `permitirNegativo: false` (default) para que rechace stock insuficiente con `StockInsuficienteException` y rollback total vía `IEjecutorTransaccion`. `FacturaDomain` agrega un único filtro extra: una línea con `BaseEntry != null` no genera movimiento (esa mercancía ya la movió su Entrega). Web: el formulario de creación manda encabezado + líneas en una petición; el de edición muestra líneas en solo lectura, encabezado read-only y botón "Cancelar documento".

**Tech Stack:** C# / .NET 7 (API) y .NET 8 (Web), EF Core (SQL Server), AutoMapper, xUnit + Moq, jQuery + Bootstrap.

**Spec:** `API/docs/superpowers/specs/2026-08-31-inventario-asiento-ventas-design.md`

## Global Constraints

- **Repos y ramas:** API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`); Web en `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad git `panchoman08`. Sin push hasta aprobación final del usuario.
- **Build/test a carpeta externa:** `-p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"` (y `...\apitest\`, `...\webbuild\`).
- **No hay .NET 7 SDK**; el SDK 9/10 compila `net7.0`. No añadir `global.json`.
- **`dotnet test` de la suite completa de la API en verde** antes de terminar cualquier tarea que toque la API. **Baseline actual: 666 pruebas, 0 fallos.**
- **`appsettings.json` (`API.Service.WebApi`) y `appsettings*.json` (Web) pueden aparecer modificados localmente con un connection string real — NUNCA commitearlos.** Usar `git add` con rutas explícitas, nunca `git add -A`.
- **`TipoObjeto`:** `Entrega` venta = `"5"`, `Factura` venta = `"6"` (CHECK constraint de la tabla; ya son los defaults). Se fuerza en el servidor.
- **Flags de las tablas de venta** (ya existen, mismos defaults que compras): `EstadoDoc` `'A'`/`'C'` (INV-3 no lo toca); `Cancelado` `'S'`/`'N'` (`'S'` dispara el reversó); `EstadoInv` `'A'`/`'C'` (asentado / revertido). **No se necesita script SQL.**
- **`Startup.cs` NO cambia.** `IRepositorioGenerico<Entrega,int>`, `<EntregaDetalle,(int,int)>`, `<Factura,int>`, `<FacturaDetalle,(int,int)>`, `<NumeracionDocumentoDet,int>`, `IEjecutorTransaccion`, `IInventarioAsientoService` ya están registrados.
- **Contrato de INV-1 (no romper):** `IInventarioAsientoService.AsentarAsync(IEnumerable<MovimientoRequest>, bool permitirNegativo = false)` / `RevertirAsync(string tipoDoc, int docEntry)` **nunca** llaman `SaveChangesAsync`. `AgregarSinGuardarAsync` hace `DbSet.AddAsync` sin guardar. `MovimientoRequest(string TipoDoc, int DocEntry, int DocLinea, string CodArticulo, string CodAlmacen, decimal Cantidad, decimal PrecioUnitario, DateTime Fecha)` — **`Cantidad < 0` = salida**. Si `Disponible + Cantidad < 0` y `!permitirNegativo` → lanza `StockInsuficienteException`.
- **`IEjecutorTransaccion.EjecutarAsync<T>(Func<Task<T>>)`:** al retornar sin excepción hace `SaveChangesAsync` + `Commit`; si lanza, `Rollback` + `ChangeTracker.Clear()` + repropaga. Los domains **nunca** llaman `SaveChangesAsync` directo.
- **Edición post-asiento:** solo `Comentario` (replace-semantics: enviar vacío/`null` lo borra). Se ignoran cambios a socio/fechas/moneda/totales/serie/`NumDoc`/`EstadoDoc`.
- **Estilo de plan: por transformación (DRY).** El código canónico ya vive en el repo (archivos `*Compra`, mergeados en INV-2 con su fix wave). Cada tarea referencia el archivo canónico en HEAD + una tabla de sustitución + los deltas específicos de ventas. Los deltas de ventas van escritos completos en el plan.
- **Fuera de alcance:** chaining (solo se deja el hook `BaseEntry`), Cotización/Pedido de venta, Entrada/Salida de mercancías (INV-4), traslados, reserva, reintento por concurrencia, descancelar, reprocesar documentos de venta previos.

---

## File Structure

**API (rama `desarrollo`):**

| Archivo | Responsabilidad | Cambio |
|---|---|---|
| `API.Application.DTO/entrega/EntregaCrearDTO.cs` | DTO de alta de Entrega venta | + campo `Lineas` |
| `API.Application.DTO/factura/FacturaCrearDTO.cs` | DTO de alta de Factura venta | + campo `Lineas` |
| `API.Domain.Interface/IEntregaDomain.cs` | contrato del dominio Entrega | firma de `InsertarAsync` → 2 args |
| `API.Domain.Interface/IFacturaDomain.cs` | contrato del dominio Factura | firma de `InsertarAsync` → 2 args |
| `API.Domain.Core/EntregaDomain.cs` | lógica de Entrega venta | reescritura = `EntregaCompraDomain` sustituido + salida |
| `API.Domain.Core/FacturaDomain.cs` | lógica de Factura venta | reescritura = `EntregaDomain` sustituido + guard `BaseEntry` |
| `API.Domain.Core/EntregaDetalleDomain.cs` | líneas de Entrega venta | reescritura = `EntregaCompraDetalleDomain` sustituido |
| `API.Domain.Core/FacturaDetalleDomain.cs` | líneas de Factura venta | reescritura = `EntregaDetalleDomain` sustituido |
| `API.Application.Main/EntregaApplication.cs` | orquestación Entrega venta | `InsertarAsync` mapea `obj.Lineas` |
| `API.Application.Main/FacturaApplication.cs` | orquestación Factura venta | `InsertarAsync` mapea `obj.Lineas` |
| `API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs` | tests de `EntregaDomain` | reescritura = `EntregaCompraDomainTests` sustituido + tests de salida |
| `API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs` | tests de `FacturaDomain` | reescritura + tests `BaseEntry` |
| `API.Service.WebApi.Tests/Domain/EntregaDetalleDomainTests.cs` | tests de `EntregaDetalleDomain` | **nuevo** = `EntregaCompraDetalleDomainTests` sustituido |
| `API.Service.WebApi.Tests/Domain/FacturaDetalleDomainTests.cs` | tests de `FacturaDetalleDomain` | **nuevo** = `EntregaDetalleDomainTests` sustituido |

**Web (rama `main`):**

| Archivo | Cambio |
|---|---|
| `Web.ApiClient/Dtos/Entrega/EntregaCrearDTO.cs` | + campo `Lineas` |
| `Web.ApiClient/Dtos/Factura/FacturaCrearDTO.cs` | + campo `Lineas` |
| `Web.UI/Controllers/EntregasController.cs` | `Editar` + `FormularioEditar` reenvían `Cancelado` |
| `Web.UI/Controllers/FacturasController.cs` | idem |
| `Web.UI/wwwroot/js/entregas.js` | reemplazo = `entregascompra.js` sustituido |
| `Web.UI/wwwroot/js/facturas.js` | reemplazo = `entregas.js` sustituido |
| `Web.UI/Views/Entregas/_Form.cshtml` | reemplazo/merge = `EntregasCompra/_Form.cshtml` sustituido |
| `Web.UI/Views/Facturas/_Form.cshtml` | reemplazo/merge = `Entregas/_Form.cshtml` sustituido |

**No se tocan:** `Startup.cs`, `InventarioAsientoService.cs`, `EjecutorTransaccion.cs`, `PerfilMapeo.cs` (los mapas `CreateMap<EntregaDetalleCrearDTO, EntregaDetalle>()` y `CreateMap<FacturaDetalleCrearDTO, FacturaDetalle>()` ya existen), ni ningún archivo de Cotización/Pedido/`*Compra`.

---

## Task 1: `EntregaDomain` — asiento de salida atómico + cancelación + guardas de detalle

**Files:**
- Modify: `API.Application.DTO/entrega/EntregaCrearDTO.cs`
- Modify: `API.Domain.Interface/IEntregaDomain.cs`
- Modify: `API.Domain.Core/EntregaDomain.cs` (reescritura)
- Modify: `API.Domain.Core/EntregaDetalleDomain.cs` (reescritura)
- Modify: `API.Application.Main/EntregaApplication.cs`
- Modify: `API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs` (reescritura)
- Create: `API.Service.WebApi.Tests/Domain/EntregaDetalleDomainTests.cs`

**Interfaces:**
- Consumes (de fases previas, ya en `main`): `IEjecutorTransaccion.EjecutarAsync<T>(Func<Task<T>>)`; `IInventarioAsientoService.AsentarAsync(IEnumerable<MovimientoRequest>, bool = false)` y `RevertirAsync(string, int)`; `MovimientoRequest(string TipoDoc, int DocEntry, int DocLinea, string CodArticulo, string CodAlmacen, decimal Cantidad, decimal PrecioUnitario, DateTime Fecha)`; `IRepositorioGenerico<T,TKey>.AgregarSinGuardarAsync(T)`.
- Produces: `IEntregaDomain.InsertarAsync(Entrega obj, IEnumerable<EntregaDetalle> lineas) -> Task<int>`; `EntregaCrearDTO.Lineas : List<EntregaDetalleCrearDTO>`; `EntregaDetalleDomain(IRepositorioGenerico<EntregaDetalle,(int,int)>, IRepositorioGenerico<Entrega,int>)` (detalle primero, encabezado segundo).

### Precondición (verificar antes de empezar)

`EntregaDomain.cs` y `EntregaDetalleDomain.cs` hoy tienen exactamente la forma que tenían
`EntregaCompraDomain.cs` / `EntregaCompraDetalleDomain.cs` **antes** de INV-2 Task 2:
`EntregaDomain` con ctor de 3 repos (`<Entrega,int>`, `<EntregaDetalle,(int,int)>`,
`<NumeracionDocumentoDet,int>`), `InsertarAsync(Entrega obj)` de 1 argumento que fuerza
`TipoObjeto` y numera, `ActualizarAsync` que fuerza `TipoObjeto` y llama `_repo.ActualizarAsync`,
`EliminarAsync` que borra líneas a mano; `EntregaDetalleDomain` con ctor de 1 repo, sin repo de
encabezado y sin guardas, `InsertarAsync` que calcula `NoLinea = max+1`.
Si difieren de eso de forma relevante, **PARAR** y reportar NEEDS_CONTEXT con el diff.

### Tabla de sustitución (canónico → Task 1)

Canónico: `API.Domain.Core/EntregaCompraDomain.cs`, `API.Domain.Core/EntregaCompraDetalleDomain.cs`,
`API.Domain.Interface/IEntregaCompraDomain.cs`, `API.Application.Main/EntregaCompraApplication.cs`,
`API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs`,
`API.Service.WebApi.Tests/Domain/EntregaCompraDetalleDomainTests.cs` — todos en HEAD de `desarrollo`.

| Canónico (`EntregaCompra`) | Task 1 (`Entrega`) |
|---|---|
| `EntregaCompra` / `EntregaCompraDetalle` (tipos, DTO ns `entregaCompra`) | `Entrega` / `EntregaDetalle` (ns `entrega`) |
| `IEntregaCompraDomain` / `IEntregaCompraDetalleDomain` | `IEntregaDomain` / `IEntregaDetalleDomain` |
| `EntregaCompraApplication` | `EntregaApplication` |
| `EntregaCompraDomainTests` / `EntregaCompraDetalleDomainTests` | `EntregaDomainTests` / `EntregaDetalleDomainTests` |
| campo `_repoEntregaCompra` | `_repoEntrega` |
| `TipoObjetoEntregaCompra` (const) | `TipoObjetoEntrega` (const) |
| `"12"` (valor de la const + `CodigoObj` del helper de test + asserts) | `"5"` |
| `ObtenerPorEntregaCompraAsync` (detalle domain) | `ObtenerPorEntregaAsync` |
| `RevertirAsync(TipoObjetoEntregaCompra, id)` | `RevertirAsync(TipoObjetoEntrega, id)` |
| "entregas de compra" / "entrega de compra" (mensajes) | "entregas" / "entrega" |

### Delta de ventas (lo que NO es sustitución)

**D1 — El `MovimientoRequest` lleva `Cantidad` en negativo (salida).** En el canónico
`EntregaCompraDomain.InsertarAsync` la línea del `Select` dice:
```csharp
                        Cantidad: l.Cantidad!.Value,
```
En `EntregaDomain.InsertarAsync` debe decir:
```csharp
                        Cantidad: -(l.Cantidad!.Value),   // negativo = salida de stock
```
Todo lo demás del `MovimientoRequest` queda igual (`PrecioUnitario: l.Precio ?? 0m` — precio de
venta, solo referencia). El filtro `.Where(l => (l.Cantidad ?? 0m) > 0m)` NO cambia en Task 1
(se filtra por cantidad de la línea, que es positiva; el signo se aplica al construir el request).

**D2 — Test nuevo de stock insuficiente** (no existe en compras porque la entrada no se bloquea).
Añadir a `EntregaDomainTests` (ver Step 7).

**D3 — El texto del `_asiento` mock en `EntregaDomainTests`** queda igual que en el canónico
(`AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>())`).

### Steps

- [ ] **Step 1: `EntregaCrearDTO` (API) gana `Lineas`**

En `API.Application.DTO/entrega/EntregaCrearDTO.cs` añadir la propiedad (el
`EntregaDetalleCrearDTO` está en el namespace `API.Application.DTO.entrega`, no hace falta
`using` extra — confirmar; si estuviera en otro ns, añadir el `using`):
```csharp
        public List<EntregaDetalleCrearDTO> Lineas { get; set; } = new();
```

- [ ] **Step 2: `IEntregaDomain.InsertarAsync` → 2 argumentos**

En `API.Domain.Interface/IEntregaDomain.cs`, reemplazar
`Task<int> InsertarAsync(Entrega obj);` por:
```csharp
        Task<int> InsertarAsync(Entrega obj, IEnumerable<EntregaDetalle> lineas);
```
(se elimina la sobrecarga de 1 argumento).

- [ ] **Step 3: Reescribir `EntregaDomain.cs`**

Tomar `API.Domain.Core/EntregaCompraDomain.cs` en HEAD, aplicarle la tabla de sustitución de
arriba y el **Delta D1** (signo negativo en `Cantidad`). El resultado: ctor de 5 dependencias
(`IRepositorioGenerico<Entrega,int>`, `<EntregaDetalle,(int,int)>`, `<NumeracionDocumentoDet,int>`,
`IEjecutorTransaccion`, `IInventarioAsientoService`); `InsertarAsync(Entrega obj, IEnumerable<EntregaDetalle> lineas)`
fuerza `TipoObjeto="5"` + `EstadoInv="A"`, numera antes de la transacción, y dentro de
`_tx.EjecutarAsync`: inserta encabezado → `obj.Entry`, añade líneas con `AgregarSinGuardarAsync`
(Entry/NoLinea 1..n), arma `MovimientoRequest` por línea con `Cantidad > 0` **con cantidad
negativa**, llama `_asiento.AsentarAsync(movimientos)` (sin segundo argumento → `permitirNegativo`
default `false`), `return obj.Entry`. `ActualizarAsync` idéntico al canónico (cancelación →
`RevertirAsync("5", id)` + flags + `Comentario` con guard `!= null`; edición inocua → `Comentario`
incondicional). `EliminarAsync` idéntico (guarda + borrado dentro de `_tx.EjecutarAsync`).
`ObtenerAsync` / `ObtenerTodoAsync` sin cambios.

- [ ] **Step 4: Reescribir `EntregaDetalleDomain.cs`**

Tomar `API.Domain.Core/EntregaCompraDetalleDomain.cs` en HEAD y aplicar la sustitución. El
resultado: ctor `(IRepositorioGenerico<EntregaDetalle,(int Entry,int NoLinea)> repoGenericoDet,
IRepositorioGenerico<Entrega,int> repoEncabezado)` — **detalle primero, encabezado segundo**;
`InsertarAsync` rechaza incondicionalmente (`await Task.CompletedTask; throw new Exception("Las líneas se definen al crear el documento y no se pueden agregar después.");`);
`ActualizarAsync` / `EliminarAsync` llaman `await LanzarSiElDocumentoExisteAsync(entry);` al inicio;
`ObtenerPorEntregaAsync` (renombrado de `ObtenerPorEntregaCompraAsync`) y los demás `Obtener*`
sin cambios; `LanzarSiElDocumentoExisteAsync` idéntico al canónico.

- [ ] **Step 5: `EntregaApplication.InsertarAsync` mapea `obj.Lineas`**

En `API.Application.Main/EntregaApplication.cs`, en `InsertarAsync`, entre el `_mapper.Map<Entrega>(obj)`
y la llamada al dominio, añadir el mapeo de líneas y pasar el segundo argumento (igual que
`EntregaCompraApplication.InsertarAsync`):
```csharp
                var entrega = _mapper.Map<Entrega>(obj);
                var lineas = _mapper.Map<IEnumerable<EntregaDetalle>>(obj.Lineas);
                respuesta.Dato = await _entregaDomain.InsertarAsync(entrega, lineas);
```

- [ ] **Step 6: Reescribir `EntregaDomainTests.cs`**

Tomar `API.Service.WebApi.Tests/Domain/EntregaCompraDomainTests.cs` en HEAD, aplicar la
sustitución. **Ajuste en los asserts del `MovimientoRequest` por el signo negativo:** en el
canónico `InsertarAsync_ConLineas_...` el assert dice:
```csharp
            Assert.Equal(("12", 99, 1, "ART1", "01", 10m, 25m), (
                _movimientosAsentados[0].TipoDoc, _movimientosAsentados[0].DocEntry, _movimientosAsentados[0].DocLinea,
                _movimientosAsentados[0].CodArticulo, _movimientosAsentados[0].CodAlmacen,
                _movimientosAsentados[0].Cantidad, _movimientosAsentados[0].PrecioUnitario));
```
En `EntregaDomainTests` debe ser (cantidad **-10m**, TipoDoc **"5"**):
```csharp
            Assert.Equal(("5", 99, 1, "ART1", "01", -10m, 25m), (
                _movimientosAsentados[0].TipoDoc, _movimientosAsentados[0].DocEntry, _movimientosAsentados[0].DocLinea,
                _movimientosAsentados[0].CodArticulo, _movimientosAsentados[0].CodAlmacen,
                _movimientosAsentados[0].Cantidad, _movimientosAsentados[0].PrecioUnitario));
```
El helper `SerieAuto` usa `CodigoObj = "5"`. Los asserts `Assert.Equal("12", obj.TipoObjeto)` →
`Assert.Equal("5", obj.TipoObjeto)`. `_asiento.Verify(a => a.RevertirAsync("12", 7), ...)` →
`RevertirAsync("5", 7)`. Todos los demás tests se transforman 1:1.

- [ ] **Step 7: Añadir el test de stock insuficiente a `EntregaDomainTests.cs`**

Añadir este test (Delta D2 — verifica que una `StockInsuficienteException` de `AsentarAsync`
se propaga y no deja documento):
```csharp
        [Fact]
        public async Task InsertarAsync_StockInsuficiente_Propaga()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .ThrowsAsync(new StockInsuficienteException("ART1", "01", 3m, 10m));

            var obj = new Entrega { Serie = 4 };
            var lineas = new[] { Linea("ART1", "01", 10m, 25m) };

            await Assert.ThrowsAsync<StockInsuficienteException>(() => _domain.InsertarAsync(obj, lineas));
        }
```
`StockInsuficienteException` está en `API.Domain.Core.Inventario` — añadir
`using API.Domain.Core.Inventario;` al archivo de test si no está.
Nota: el `_tx` doble corre el `Func`, así que la excepción de `AsentarAsync` sale por
`EjecutarAsync` tal cual; no hace falta configurar rollback en el mock.

- [ ] **Step 8: Crear `EntregaDetalleDomainTests.cs`**

Tomar `API.Service.WebApi.Tests/Domain/EntregaCompraDetalleDomainTests.cs` en HEAD, aplicar la
sustitución (`EntregaCompra`→`Entrega`, `EntregaCompraDetalle`→`EntregaDetalle`,
`EntregaCompraDetalleDomain`→`EntregaDetalleDomain`, clase `EntregaCompraDetalleDomainTests`→
`EntregaDetalleDomainTests`). Los 4 tests (`InsertarAsync_DocumentoExiste_Lanza`,
`InsertarAsync_DocumentoNoExiste_Lanza`, `ActualizarAsync_DocumentoExiste_Lanza`,
`EliminarAsync_DocumentoExiste_Lanza`) se transforman 1:1. El ctor del test es
`new EntregaDetalleDomain(_repoDet.Object, _repoHeader.Object)`.

- [ ] **Step 9: Revisar callers de `IEntregaDetalleDomain.InsertarAsync`**

`grep -rn "IEntregaDetalleDomain\|_entregaDetalleDomain" API.Application.Main API.Service.WebApi`.
Si algún controller/Application construye `EntregaDetalleDomain` directamente o llama a su
`InsertarAsync` esperando éxito, ajustarlo (la capa Application ya atrapa `Exception` y devuelve
`Respuesta` fallida — igual que pasó en INV-2, donde no hizo falta tocar nada). Documentar lo
que se encontró.

- [ ] **Step 10: Build**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 11: Suite completa**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: **0 fallos**. Baseline 666 + ~17 nuevos (13 de `EntregaDomainTests` reescritos +
1 de stock insuficiente + 4 de `EntregaDetalleDomainTests`) − los viejos de `EntregaDomainTests`
(si los había: revisar el archivo actual). Los nuevos de venta en verde.

- [ ] **Step 12: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Application.DTO/entrega/EntregaCrearDTO.cs API.Domain.Interface/IEntregaDomain.cs API.Domain.Core/EntregaDomain.cs API.Domain.Core/EntregaDetalleDomain.cs API.Application.Main/EntregaApplication.cs API.Service.WebApi.Tests/Domain/EntregaDomainTests.cs API.Service.WebApi.Tests/Domain/EntregaDetalleDomainTests.cs
git commit -m "feat(api): Entrega de venta descuenta inventario al registrar (salida atomica) y lo reingresa al cancelar"
```
(añadir el trailer `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`)

---

## Task 2: `FacturaDomain` — transformación de Task 1 + guard `BaseEntry`

**Files:**
- Modify: `API.Application.DTO/factura/FacturaCrearDTO.cs`
- Modify: `API.Domain.Interface/IFacturaDomain.cs`
- Modify: `API.Domain.Core/FacturaDomain.cs` (reescritura)
- Modify: `API.Domain.Core/FacturaDetalleDomain.cs` (reescritura)
- Modify: `API.Application.Main/FacturaApplication.cs`
- Modify: `API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs` (reescritura)
- Create: `API.Service.WebApi.Tests/Domain/FacturaDetalleDomainTests.cs`

**Interfaces:**
- Consumes: lo mismo que Task 1.
- Produces: `IFacturaDomain.InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas) -> Task<int>`; `FacturaCrearDTO.Lineas : List<FacturaDetalleCrearDTO>`; `FacturaDetalleDomain(IRepositorioGenerico<FacturaDetalle,(int,int)>, IRepositorioGenerico<Factura,int>)`.

### Precondición

`FacturaDomain.cs` / `FacturaDetalleDomain.cs` hoy son clones pre-INV-2 (misma forma que
`EntregaDomain` / `EntregaDetalleDomain` **antes** de Task 1: ctor de 3 / 1 repos,
`InsertarAsync` de 1 arg, `EliminarAsync` borra líneas a mano, detalle sin guardas). Si no,
PARAR y reportar NEEDS_CONTEXT.

### Cómo hacerlo

Task 1 ya dejó `EntregaDomain.cs` etc. en el estado destino. **Transformar los archivos
`Entrega*` (commiteados por Task 1) a `Factura*`** con esta tabla, más el delta del guard:

| Task 1 (`Entrega`) | Task 2 (`Factura`) |
|---|---|
| `Entrega` / `EntregaDetalle` (tipos, DTO ns `entrega`) | `Factura` / `FacturaDetalle` (ns `factura`) |
| `TipoObjetoEntrega` / `"5"` | `TipoObjetoFactura` / `"6"` |
| `IEntregaDomain` / `IEntregaDetalleDomain` | `IFacturaDomain` / `IFacturaDetalleDomain` |
| `EntregaApplication` | `FacturaApplication` |
| `_repoEntrega` | `_repoFactura` |
| `ObtenerPorEntregaAsync` | `ObtenerPorFacturaAsync` |
| `RevertirAsync("5", id)` | `RevertirAsync("6", id)` |
| `EntregaDomainTests` / `EntregaDetalleDomainTests` | `FacturaDomainTests` / `FacturaDetalleDomainTests` |
| "entregas" / "entrega" (mensajes) | "facturas" / "factura" |

### Delta del guard `BaseEntry` (lo único que NO es sustitución de Task 1)

En `FacturaDomain.InsertarAsync`, el filtro de `movimientos` gana una segunda cláusula. En Task 1
(`EntregaDomain`) la línea es:
```csharp
                var movimientos = lineasList
                    .Where(l => (l.Cantidad ?? 0m) > 0m)
```
En `FacturaDomain` debe ser:
```csharp
                var movimientos = lineasList
                    .Where(l => (l.Cantidad ?? 0m) > 0m && l.BaseEntry == null)   // BaseEntry != null -> esa mercancia ya la movio su Entrega
```
Todo lo demás del `Select` queda igual que Task 1 (incluida `Cantidad: -(l.Cantidad!.Value)` y
`TipoDoc: TipoObjetoFactura`).

### Delta de tests `BaseEntry` (añadir a `FacturaDomainTests`)

El helper `Linea` del canónico no setea `BaseEntry`. Añadir una sobrecarga o un helper y estos
tests:
```csharp
        private static FacturaDetalle LineaBase(string art, string alm, decimal? cant, decimal? precio, int? baseEntry) =>
            new() { CodArticulo = art, CodAlmacen = alm, Cantidad = cant, Precio = precio, BaseEntry = baseEntry };

        [Fact]
        public async Task InsertarAsync_LineaConBaseEntry_NoGeneraMovimiento_PeroSiSeInsertaLinea()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { LineaBase("ART1", "01", 10m, 25m, baseEntry: 500) });

            Assert.Empty(_movimientosAsentados);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l => l.Entry == 99 && l.NoLinea == 1)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinBaseEntry_GeneraMovimiento()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[] { LineaBase("ART1", "01", 10m, 25m, baseEntry: null) });

            Assert.Single(_movimientosAsentados);
            Assert.Equal(-10m, _movimientosAsentados[0].Cantidad);
        }

        [Fact]
        public async Task InsertarAsync_Mezcla_SoloLaLineaSinBaseEntryGeneraMovimiento()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4 };

            await _domain.InsertarAsync(obj, new[]
            {
                LineaBase("ART1", "01", 10m, 25m, baseEntry: 500),
                LineaBase("ART2", "01", 4m, 30m, baseEntry: null),
            });

            Assert.Single(_movimientosAsentados);
            Assert.Equal("ART2", _movimientosAsentados[0].CodArticulo);
            Assert.Equal(2, _movimientosAsentados[0].DocLinea);
        }
```

### Steps

- [ ] **Step 1:** `FacturaCrearDTO` (API) gana `public List<FacturaDetalleCrearDTO> Lineas { get; set; } = new();` (como Task 1 Step 1, con `factura`).
- [ ] **Step 2:** `IFacturaDomain.InsertarAsync` → `Task<int> InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas);` (elimina la sobrecarga de 1 arg).
- [ ] **Step 3:** Reescribir `FacturaDomain.cs` = `EntregaDomain.cs` (post-Task 1) sustituido + el **delta del guard `BaseEntry`** de arriba.
- [ ] **Step 4:** Reescribir `FacturaDetalleDomain.cs` = `EntregaDetalleDomain.cs` (post-Task 1) sustituido. Ctor `(repoDet, repoEncabezado<Factura,int>)`. `InsertarAsync` rechaza siempre.
- [ ] **Step 5:** `FacturaApplication.InsertarAsync` mapea `obj.Lineas` → `IEnumerable<FacturaDetalle>` y pasa el 2º argumento (como Task 1 Step 5).
- [ ] **Step 6:** Reescribir `FacturaDomainTests.cs` = `EntregaDomainTests.cs` (post-Task 1) sustituido. Asserts del `MovimientoRequest`: `TipoDoc` `"6"`, cantidad negativa. `SerieAuto` con `CodigoObj = "6"`. Conservar el test `InsertarAsync_StockInsuficiente_Propaga` transformado.
- [ ] **Step 7:** Añadir los 3 tests `BaseEntry` de arriba a `FacturaDomainTests.cs`.
- [ ] **Step 8:** Crear `FacturaDetalleDomainTests.cs` = `EntregaDetalleDomainTests.cs` (post-Task 1) sustituido.
- [ ] **Step 9:** `grep -rn "IFacturaDetalleDomain\|_facturaDetalleDomain" API.Application.Main API.Service.WebApi` — ajustar callers si esperan éxito de `InsertarAsync`; documentar.
- [ ] **Step 10: Build** — `dotnet build API.sln -p:BaseOutputPath=".../apibuild/"` → `0 Errores`.
- [ ] **Step 11: Suite completa** — `dotnet test API.sln -p:BaseOutputPath=".../apitest/"` → **0 fallos**; los nuevos de `Factura` en verde.
- [ ] **Step 12: Commit**
```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Application.DTO/factura/FacturaCrearDTO.cs API.Domain.Interface/IFacturaDomain.cs API.Domain.Core/FacturaDomain.cs API.Domain.Core/FacturaDetalleDomain.cs API.Application.Main/FacturaApplication.cs API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs API.Service.WebApi.Tests/Domain/FacturaDetalleDomainTests.cs
git commit -m "feat(api): Factura de venta descuenta inventario al registrar (salvo lineas con BaseEntry) y lo reingresa al cancelar"
```
(trailer `Co-Authored-By`)

---

## Task 3: Web — `Entregas` (crear con líneas embebidas, editar solo comentario, cancelar)

**Files:**
- Modify: `Web.ApiClient/Dtos/Entrega/EntregaCrearDTO.cs`
- Modify: `Web.UI/Controllers/EntregasController.cs`
- Modify: `Web.UI/wwwroot/js/entregas.js` (reemplazo)
- Modify: `Web.UI/Views/Entregas/_Form.cshtml` (reemplazo/merge)

**Interfaces:**
- Consumes: `api/Entrega` (Task 1) — `Crear` acepta `dto.Lineas`; `Editar` reacciona a `Cancelado='S'`.
- Produces: nada para tareas posteriores.

### Cómo hacerlo

Canónico: `Web.UI/wwwroot/js/entregascompra.js`, `Web.UI/Views/EntregasCompra/_Form.cshtml`,
`Web.UI/Controllers/EntregasCompraController.cs`, `Web.ApiClient/Dtos/EntregaCompra/EntregaCompraCrearDTO.cs`
— todos en HEAD de `main` (incluyen el fix wave de INV-2: badge "Cancelado", guards cliente,
encabezado read-only en edición, `finally` en el handler de cancelar).

`entregas.js` y `Entregas/_Form.cshtml` hoy son los clones **pre-INV-2** (creación en dos pasos
con bucle `/CrearLinea`, sin botón cancelar, sin badge, encabezado editable en edición). El
resultado neto de esta tarea es **reemplazar `entregas.js` por `entregascompra.js` sustituido** y
**hacer que `Entregas/_Form.cshtml` quede igual a `EntregasCompra/_Form.cshtml` sustituido**
(mismo patrón que INV-2 Task 5 hizo con `facturascompra.js` ← `entregascompra.js`, donde el
reviewer verificó "byte-idéntico módulo sustitución").

### Tabla de sustitución

| Canónico (`EntregasCompra`) | Task 3 (`Entregas`) |
|---|---|
| `EntregasCompra` (controller, rutas `/EntregasCompra/...`, carpeta de vistas, `#tblEntregasCompra`, `#tblDetalleEntregaCompra`) | `Entregas` (`/Entregas/...`, `#tblEntregas`, `#tblDetalleEntrega`) |
| `entregascompra` (nombre de archivo js, `datosSeriesEntregaCompra`, `selectSerieEntregaCompra`) | `entregas` (`datosSeriesEntrega`, `selectSerieEntrega`) |
| `EntregaCompra` / `EntregaCompraDetalle` (tipos DTO) | `Entrega` / `EntregaDetalle` |
| `entregaCompra` (ns DTO Web) | `entrega` |
| `#btnGuardarEntregaCompra` | `#btnGuardarEntrega` |
| `#btnCancelarDocEntregaCompra` | `#btnCancelarDocEntrega` |
| `inicializarSerieEntregaCompra` / `esSerieManualEntregaCompra` | `inicializarSerieEntrega` / `esSerieManualEntrega` |
| `EntregaCompraCrearDTO` / `EntregaCompraActualizarDTO` | `EntregaCrearDTO` / `EntregaActualizarDTO` |
| "entrega de compra" / "Entrega de compra" / "entregas de compra" (textos UI) | "entrega" / "Entrega" / "entregas" |

`#btnNuevaLinea` NO lleva sufijo en ninguno de los dos (queda igual). `ViewBag.EntryActual` —
misma clave. `App.*` helpers — sin cambios.

**Sin lógica extra en Web:** el guard `BaseEntry` de la Factura es 100% servidor; aquí no
aplica (además esta tarea es Entrega, no Factura).

### Steps

- [ ] **Step 1: `EntregaCrearDTO` (Web) gana `Lineas`**

En `Web.ApiClient/Dtos/Entrega/EntregaCrearDTO.cs` añadir `using Web.ApiClient.Dtos.EntregaDetalle;`
y `public List<EntregaDetalleCrearDTO> Lineas { get; set; } = new();` (igual que
`EntregaCompraCrearDTO` con la sustitución). Verificar que `EntregaDetalleCrearDTO` (Web) existe
en `Web.ApiClient/Dtos/EntregaDetalle/` — si no, PARAR y reportar.

- [ ] **Step 2: `EntregasController` reenvía `Cancelado`**

En `Web.UI/Controllers/EntregasController.cs`:
- En `FormularioEditar`, en la construcción del `EntregaCrearDTO` para la vista, añadir
  `Cancelado = respuesta.Dato.Cancelado,` (como `EntregasCompraController:94`). Verificar que
  ya se setea `ViewBag.EntryActual = entry;` (como `EntregasCompraController:85`); si no, añadirlo.
- En `Editar`, en la construcción del `EntregaActualizarDTO`, añadir `Cancelado = dto.Cancelado,`
  (como `EntregasCompraController:139`). `EntregaActualizarDTO` (Web) ya tiene `Cancelado`.

- [ ] **Step 3: Reemplazar `Web.UI/wwwroot/js/entregas.js`**

Sustituir el contenido completo por `Web.UI/wwwroot/js/entregascompra.js` (HEAD) con la tabla
de sustitución aplicada. Verificar tras el reemplazo: un solo `POST /Entregas/Crear` con
`datos.Lineas`; sin bucle `/CrearLinea` en el camino de creación; los dos guards cliente
(≥1 línea; `Cantidad > 0` sin `CodAlmacen`); handler `#btnCancelarDocEntrega` con
`$btn.prop('disabled', true)` + `finally`; columna de estado con badge `row.cancelado === 'S'`;
`pintarDetalle()` sin botones por fila en edición.

- [ ] **Step 4: `Web.UI/Views/Entregas/_Form.cshtml` = `EntregasCompra/_Form.cshtml` sustituido**

Hacer que `Entregas/_Form.cshtml` quede equivalente a `EntregasCompra/_Form.cshtml` (HEAD) con
la sustitución: `@if (!esEdicion)` alrededor de "Agregar línea"; botón "Cancelar documento" en el
`modal-footer` visible solo si `esEdicion && (Model.Cancelado ?? "N") != "S"` con
`data-entry="@ViewBag.EntryActual"`; **todos los inputs de encabezado salvo el textarea
`Comentario`** con `readonly="@esEdicion"` (inputs) / `disabled="@esEdicion"` (selects); el
autocomplete de `CodigoSN` renderizado como display de solo lectura en edición (patrón de `Serie`);
"Guardar" oculto cuando `Model.Cancelado == "S"`. Conservar cualquier campo específico de la
vista de venta que exista y no en compras (comparar los dos `_Form` antes de reemplazar; si hay
divergencia real de campos, aplicar los cambios de INV-2 sobre el `_Form` de venta en vez de
reemplazar a ciegas, y documentarlo).

- [ ] **Step 5: Build Web**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 6: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/Dtos/Entrega/EntregaCrearDTO.cs Web.UI/Controllers/EntregasController.cs Web.UI/wwwroot/js/entregas.js Web.UI/Views/Entregas/_Form.cshtml
git commit -m "feat(web): Entregas de venta crea con lineas embebidas; edicion solo comentario; boton Cancelar documento"
```
(trailer `Co-Authored-By`)

---

## Task 4: Web — `Facturas` (transformación de Task 3)

**Files:**
- Modify: `Web.ApiClient/Dtos/Factura/FacturaCrearDTO.cs`
- Modify: `Web.UI/Controllers/FacturasController.cs`
- Modify: `Web.UI/wwwroot/js/facturas.js` (reemplazo)
- Modify: `Web.UI/Views/Facturas/_Form.cshtml` (reemplazo/merge)

**Interfaces:**
- Consumes: `api/Factura` (Task 2) — `Crear` acepta `dto.Lineas`; `Editar` reacciona a `Cancelado='S'`.

### Precondición

`facturas.js` / `Facturas/_Form.cshtml` hoy son clones pre-INV-2 de `entregas.js` /
`Entregas/_Form.cshtml`. Si divergen de forma relevante de lo que Task 3 dejó en `Entregas`,
PARAR y reportar.

### Cómo hacerlo

Transformar los archivos `Entregas` que dejó Task 3 (commiteados) a `Facturas` con esta tabla:

| Task 3 (`Entregas`) | Task 4 (`Facturas`) |
|---|---|
| `Entregas` (controller, `/Entregas/...`, carpeta vistas, `#tblEntregas`, `#tblDetalleEntrega`) | `Facturas` (`/Facturas/...`, `#tblFacturas`, `#tblDetalleFactura`) |
| `entregas` (archivo js, `datosSeriesEntrega`, `selectSerieEntrega`) | `facturas` (`datosSeriesFactura`, `selectSerieFactura`) |
| `Entrega` / `EntregaDetalle` (tipos DTO) | `Factura` / `FacturaDetalle` |
| `entrega` (ns DTO Web) | `factura` |
| `#btnGuardarEntrega` / `#btnCancelarDocEntrega` | `#btnGuardarFactura` / `#btnCancelarDocFactura` |
| `inicializarSerieEntrega` / `esSerieManualEntrega` | `inicializarSerieFactura` / `esSerieManualFactura` |
| `EntregaCrearDTO` / `EntregaActualizarDTO` | `FacturaCrearDTO` / `FacturaActualizarDTO` |
| "entrega" / "Entrega" / "entregas" (textos UI) | "factura" / "Factura" / "facturas" |

### Steps

- [ ] **Step 1:** `FacturaCrearDTO` (Web) gana `using Web.ApiClient.Dtos.FacturaDetalle;` + `public List<FacturaDetalleCrearDTO> Lineas { get; set; } = new();`.
- [ ] **Step 2:** `FacturasController` — `FormularioEditar` añade `Cancelado = respuesta.Dato.Cancelado,` (+ `ViewBag.EntryActual` si falta); `Editar` añade `Cancelado = dto.Cancelado,`.
- [ ] **Step 3:** Reemplazar `Web.UI/wwwroot/js/facturas.js` por `Web.UI/wwwroot/js/entregas.js` (post-Task 3) sustituido.
- [ ] **Step 4:** `Web.UI/Views/Facturas/_Form.cshtml` = `Web.UI/Views/Entregas/_Form.cshtml` (post-Task 3) sustituido (misma advertencia de Task 3 Step 4 sobre comparar campos antes de reemplazar).
- [ ] **Step 5: Build Web** — `dotnet build Web.slnx -p:BaseOutputPath=".../webbuild/"` → `0 Errores`.
- [ ] **Step 6: Commit**
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/Dtos/Factura/FacturaCrearDTO.cs Web.UI/Controllers/FacturasController.cs Web.UI/wwwroot/js/facturas.js Web.UI/Views/Facturas/_Form.cshtml
git commit -m "feat(web): Facturas de venta crea con lineas embebidas; edicion solo comentario; boton Cancelar documento"
```
(trailer `Co-Authored-By`)

---

## Task 5: Verificación final conjunta

**Files:** ninguno nuevo.

- [ ] **Step 1: Build completo de la API**
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 2: Suite completa de la API**
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: **0 fallos**.

- [ ] **Step 3: Build completo de la Web**
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 4: Prueba manual en el navegador (para el usuario)**

Levantar API + Web, iniciar sesión. Necesita al menos un artículo con `ArticuloInventario='S'`
**con existencia positiva** en un almacén, y un socio cliente.

1. **Entrega de venta**: menú Ventas → Entregas → Nuevo. Serie por defecto, socio cliente,
   2 líneas del mismo artículo (cantidades distintas dentro del stock disponible) → Guardar.
   - Inventario → Existencias: el disponible del almacén **bajó** por la suma de las 2 cantidades.
   - Kardex del artículo: 2 movimientos con `TipoDoc=5`, `CantidadSale` = cantidad de cada
     línea, `CostoUnitario` = costo promedio móvil (COGS), `PrecioUnitario` = precio de venta,
     saldos corridos.
2. Editar esa entrega: cambiar el comentario → se guarda; **vaciar** el comentario y guardar →
   queda vacío. Confirmar que no hay "Agregar línea", ni botones editar/eliminar por fila, y que
   los campos de encabezado están deshabilitados.
3. En edición, "Cancelar documento" → confirmar.
   - Kardex: movimientos inversos; el disponible **vuelve** al valor previo; `EstadoInv='C'`;
     la fila de la lista muestra el badge rojo "Cancelado".
   - Intentar eliminar una entrega asentada no cancelada → error "Cancele el documento…".
     Eliminar una ya cancelada → se borra.
4. **Stock negativo**: crear una Entrega de venta con una cantidad **mayor** al disponible del
   almacén → la respuesta es error ("stock insuficiente"/`StockInsuficienteException`) y **no**
   queda documento, ni líneas, ni movimientos, ni avanza `SigNumero`.
5. Repetir 1-4 para **Factura de venta** (`TipoDoc=6`). Además: como hoy nada llena `BaseEntry`,
   toda línea de la Factura descuenta (el guard `BaseEntry == null` solo se nota cuando exista
   el flujo de chaining).

- [ ] **Step 5: Recordatorio para el usuario**

Imprimir:
- Reiniciar las sesiones de depuración de Visual Studio (API y Web.UI) — cambiaron los
  constructores de `EntregaDomain` / `FacturaDomain` / sus detalle domains.
- Los documentos de venta creados **antes** de INV-3 tienen `EstadoInv='A'` pero sin
  movimientos de inventario; no se reprocesan.
- Deuda conocida (heredada del diseño): cancelar una venta reingresa stock y **re-mezcla** el
  promedio móvil al costo con que salió (una entrada sí recalcula promedio). Restaura exacto si
  nada cambió entre medias.
- Siguiente fase: **INV-4** (documentos Entrada / Salida de mercancías estilo SAP B1).

- [ ] **Step 6: Commit final (si quedó algo suelto)**
```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add docs/ && git commit -m "chore: cierre INV-3" || echo "nada que commitear"
```

---

## Notas de auto-revisión (cobertura del spec)

- **§1 `EntregaDomain`**: DTO `Lineas`, `InsertarAsync(obj, lineas)` atómico vía `EjecutorTransaccion`,
  `TipoObjeto="5"` + `EstadoInv="A"`, `MovimientoRequest` por línea con `Cantidad > 0` y **cantidad
  negativa**, `PrecioUnitario = l.Precio`, `AsentarAsync` sin `permitirNegativo` (bloqueo duro),
  cancelación por `Cancelado='S'` → `RevertirAsync("5", id)` + `EstadoInv='C'` + `FechaCancelado`,
  edición inocua solo `Comentario` (replace-semantics), `Eliminar` bloqueado si asentado-no-cancelado
  y atómico, guardas en el detalle domain (`InsertarAsync` rechaza siempre) → **Task 1**.
- **§2 `FacturaDomain`** idéntico con `"6"` + filtro `l.BaseEntry == null` → **Task 2**.
- **§3 Web**: DTO `Lineas`, crear en una petición (sin loop `CrearLinea`), `Editar` reenvía
  `Cancelado`, `_Form` edición solo comentario + líneas read-only + encabezado read-only + botón
  "Cancelar documento", badge "Cancelado" en la lista, guards cliente → **Tasks 3 y 4**.
- **§4 Semántica** (cancelación, edición, eliminar, errores tipados, rollback total) → cubierta
  por la transformación en Tasks 1-2 + verificación en Task 5.
- **§5 Pruebas**: `EntregaDomainTests` / `FacturaDomainTests` (numeración, asiento salida con
  cantidad negativa capturada, `Cantidad<=0` sin movimiento, `_tx` `Times.Once`, `SigNumero`
  avanzó, stock insuficiente propaga, cancelar/recancelar, edición inocua incl. null borra,
  eliminar), `FacturaDomainTests` extra `BaseEntry` (3), `*DetalleDomainTests` (4 c/u),
  `InventarioAsientoServiceTests` sin cambios → **Tasks 1-2**; verificación conjunta + manual → **Task 5**.
- **§6 Riesgos** (`EjecutorTransaccion` sin test unitario → validado en Task 5; re-mezcla del
  promedio al cancelar → recordatorio Task 5; race de doble cancelación → mitigación cliente en
  la transformación Web; `DbUpdateConcurrencyException` cruda → fuera de alcance) → anotados.
- **Fuera de alcance** (chaining salvo el hook, Cotización/Pedido, mercancías, traslados, reserva,
  reintento, descancelar, reprocesar) → Global Constraints; sin tareas.
