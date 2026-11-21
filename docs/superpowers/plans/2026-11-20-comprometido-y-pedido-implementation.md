# Comprometido y Pedido Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que `ExistenciaArticulo.Comprometido` (unidades de pedidos de venta aún no entregadas ni facturadas) y `ExistenciaArticulo.Pedido` (unidades de pedidos de compra aún no recibidas ni facturadas) se mantengan solas, como en SAP B1, con un recálculo total para el relleno inicial y la reparación.

**Architecture:** Un servicio `IExistenciaCompromisoService` (Domain.Core) recalcula ambas columnas desde los pedidos activos (fuente de verdad) para los (artículo, almacén) afectados; los dominios de pedidos, entregas y facturas (ventas y compras) lo invocan **después de confirmar** cada operación mediante envoltorios que nunca fallan; un endpoint `POST api/Existencia/RecalcularCompromisos` y un botón en la Web recalculan todo. La lógica numérica pura vive en `CompromisoCalculadora`.

**Tech Stack:** .NET 7 / EF Core 7 / xUnit + Moq (API); .NET 8 MVC + jQuery + SweetAlert2 (Web).

**Spec:** `API/docs/superpowers/specs/2026-11-20-comprometido-y-pedido-design.md` (léelo antes de empezar: este plan es su argumento).

## Global Constraints

- **Repos y ramas:** API `C:\Users\migue\source\repos\angelm0508\API` en `desarrollo`; Web `C:\Users\migue\source\repos\angelm0508\Web` en `main`. Trabajo directo en esas ramas, sin worktree. Lee el `AGENTS.md` de cada repo.
- **Git:** commits con rutas explícitas (`git add <archivo>`), **nunca** `git add -A` ni `git add .`. **Nunca** commitear `API.Service.WebApi/appsettings.json`, `sql/crear-api-db-completo*.sql`, `sql/temporal-*.sql` ni `.superpowers/`. **No hacer `push`.** Mensajes `tipo(modulo): descripcion` en español sin tildes. Todo commit termina con la línea `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Conserva el fin de línea de cada archivo (autocrlf). `git status` de la API lista ~25 `*CrearDTO.cs` modificados solo por fin de línea: no los toques ni los commitees.
- **Capas:** `Application.*` depende solo de `Domain.Interface`; los contratos compartidos van en `Domain.Interface`. Respuestas siempre `Respuesta<T>`; la capa Application atrapa excepciones de negocio. Sin parámetros opcionales en interfaces de dominio (rompen Moq, CS0854).
- **Nunca fallar la operación del usuario por el recálculo:** el recálculo corre **después de confirmar** el documento y, en las integraciones, a través de los envoltorios seguros (`NotificarDocumentoAsync`/`NotificarParesAsync`), que atrapan y registran cualquier excepción.
- **Solo informativo:** no se agregan validaciones ni avisos por disponibilidad; `Disponible` (stock) y los movimientos de inventario no se tocan.
- **Mensajes de negocio:** literales en español; las pruebas los verifican.
- **Pruebas:** TDD (prueba primero y verla fallar), xUnit + Moq (`API.Service.WebApi.Tests`, patrón de `Mock<IRepositorioGenerico<…>>` + `.AsAsyncQueryable()` de `TestHelpers`). Cada tarea cierra con build sin errores y suite completa en verde. **Línea base: 3725** (confírmala y anótala). Si una API/Web en ejecución bloquea `bin`, compila/prueba con `-p:BaseOutputPath="$env:TEMP\gescom-build-comp\"` (con la barra final).
- **Constructores de dominios existentes:** al agregar `IExistenciaCompromisoService` a un dominio, ponlo como **último** parámetro del constructor y actualiza los constructores en las pruebas con `Mock.Of<IExistenciaCompromisoService>()` (Moq devuelve `Task` completadas por defecto); no reescribas pruebas existentes más de lo necesario.
- **SQL real:** pruebas de humo **solo** en bases temporales `API_DB_EVID<n>` creadas con el script completo y eliminadas al terminar (scripts `evid-up.ps1`, `evid-up-api.ps1`, `evid-down-api.ps1`, `cv-comun.ps1` y los `pos-*.ps1` en `<scratchpad>`; requieren `pwsh -ExecutionPolicy Bypass -File`; con `sqlcmd` usa `-I` para DML sobre `Articulo`). **Jamás** contra `API_DB_TEST`, `API_DB` ni una base del usuario. La contraseña de `sa` y la clave JWT **nunca** se imprimen, copian ni commitean.
- **`<scratchpad>`** = `C:\Users\migue\AppData\Local\Temp\claude\9f8881082ead4f04\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad`.
- **Web:** escapa texto dinámico; `App.enviarJson`/antiforgery en POST; sin `console.log`; verifica con `dotnet build Web.slnx` y `node --check`; di qué no pudiste verificar en navegador.

## Rulings (decisiones tomadas al planificar)

1. **Quién consume un pedido.** Solo las líneas de **Entrega y Factura de venta con `BaseTipo = 4`** (`BaseEntry`, `BaseLinea`) y, en compras, de **Entrega de compra y Factura de compra con `BaseTipo = 11`**. Una factura creada sobre una entrega (`BaseTipo = 5`) no cambia el pedido (la entrega ya lo consumió). Por eso `ObtenerParesAsync` solo remonta una vez.
2. **«Activo».** Pedido de venta o de compra activo: `Cancelado != "S"` **y** `EstadoDoc != "C"`. Consumidores de **ventas**: el mismo criterio que `DocumentoBaseService` (`Cabecera.Activo` = no cancelado y `EstadoDoc != "C"`). Consumidores de **compras**: `Cancelado != "S"` (como `PedidoCompraDomain.ObtenerAbiertosAsync`).
3. **Unidad.** `abierto = máx(0, Cantidad de la línea − consumido)` en la unidad de la línea; `abiertoBase = abierto × FactorUom` (`FactorUom ≤ 0` se trata como 1). El consumo suma `Cantidad` de las líneas consumidoras (igual que `DocumentoBaseService`/`PedidoCompraDomain`).
4. **Envoltorios seguros.** Además de las tres operaciones del spec, el contrato incluye `ObtenerParesAsync`, `NotificarDocumentoAsync` y `NotificarParesAsync` (estos dos nunca lanzan: atrapan y registran con `ILogger`). Es una ampliación menor del contrato del spec (§3). Por la misma razón los pares se expresan con el record `ParExistencia(CodArticulo, CodAlmacen)` en lugar de tuplas (Moq y los diccionarios los manejan mejor; el significado es idéntico).
5. **Sin llamada cuando no hay base.** `RecalcularPorDocumentoAsync` para entrega/factura (5, 6, 12, 13) solo hace trabajo si alguna línea apunta a un pedido (`BaseTipo` 4 en ventas, 11 en compras); las ventas POS y las facturas directas no pagan el costo.
6. **Solo artículos de inventario con almacén.** Se ignoran líneas sin artículo o sin almacén y artículos con `ArticuloInventario != "S"`.

## Estructura de archivos (todo en `API/` salvo la Web)

| Archivo | Responsabilidad |
|---|---|
| `API.Domain.Interface/Existencias/{Contratos,IExistenciaCompromisoService}.cs` | Contratos: `ParExistencia`, `ResultadoRecalculo`, `TipoDocCompromiso`, interfaz |
| `API.Domain.Core/Existencias/CompromisoCalculadora.cs` | Cálculo puro (abierto, base, suma por par) |
| `API.Domain.Core/Existencias/ExistenciaCompromisoService.cs` | Servicio: lectura de pedidos y consumos, recálculo, escritura, envoltorios |
| `API.Application.Interface/IExistenciaApplication.cs`, `API.Application.Main/ExistenciaApplication.cs`, `API.Service.WebApi/Controllers/ExistenciaController.cs`, `Startup.cs` | Endpoint `RecalcularCompromisos` y DI |
| `API.Domain.Core/{Pedido,PedidoDetalle,Entrega,Factura,PedidoCompra,PedidoCompraDetalle,EntregaCompra,FacturaCompra}Domain.cs` | Invocación tras confirmar |
| `API.Service.WebApi.Tests/...` | Pruebas de cada pieza |
| Web: `Web.ApiClient/Clientes/IExistenciaApiClient.cs`/`ExistenciaApiClient.cs`, `Web.UI/Controllers/ExistenciasController.cs`, `Views/Existencias/Index.cshtml`, `wwwroot/js/existencias.js`, `AGENTS.md` | Botón «Recalcular comprometido y pedido» |

---

### Task 1: Contratos y calculadora pura

**Files:**
- Create: `API.Domain.Interface/Existencias/Contratos.cs`, `API.Domain.Interface/Existencias/IExistenciaCompromisoService.cs`, `API.Domain.Core/Existencias/CompromisoCalculadora.cs`
- Test: `API.Service.WebApi.Tests/Domain/CompromisoCalculadoraTests.cs`

**Interfaces (produce; las usan las Tareas 2 a 9):**

```csharp
namespace API.Domain.Interface.Existencias
{
    public sealed record ParExistencia(string CodArticulo, string CodAlmacen);

    public sealed record ResultadoRecalculo(int Filas, int Corregidas);

    /// <summary>Códigos de objeto de los documentos que mueven el comprometido (ventas) y el pedido (compras).</summary>
    public static class TipoDocCompromiso
    {
        public const int Pedido = 4;
        public const int Entrega = 5;
        public const int Factura = 6;
        public const int PedidoCompra = 11;
        public const int EntregaCompra = 12;
        public const int FacturaCompra = 13;
    }

    public interface IExistenciaCompromisoService
    {
        /// <summary>Recalcula Comprometido y Pedido de esos (artículo, almacén). Idempotente; deja en 0 los que no tienen pedidos abiertos.</summary>
        Task RecalcularAsync(IReadOnlyCollection<ParExistencia> pares);

        /// <summary>Recalcula los pares que toca un documento (ver ObtenerParesAsync). Puede lanzar.</summary>
        Task RecalcularPorDocumentoAsync(int tipoDoc, int entry);

        /// <summary>Recalcula todos los pares con pedidos abiertos y deja en 0 los demás. Devuelve filas revisadas y filas cambiadas.</summary>
        Task<ResultadoRecalculo> RecalcularTodoAsync();

        /// <summary>
        /// Pares (artículo, almacén) que toca un documento: pedido (4, 11) = los de sus líneas; entrega/factura (5, 6, 12, 13) =
        /// los de sus propias líneas más los de las líneas de los pedidos base (BaseTipo 4 en ventas, 11 en compras).
        /// Úsalo ANTES de borrar un documento.
        /// </summary>
        Task<IReadOnlyList<ParExistencia>> ObtenerParesAsync(int tipoDoc, int entry);

        /// <summary>Envoltorio seguro de RecalcularPorDocumentoAsync: NUNCA lanza (atrapa y registra).</summary>
        Task NotificarDocumentoAsync(int tipoDoc, int entry);

        /// <summary>Envoltorio seguro de RecalcularAsync: NUNCA lanza (atrapa y registra).</summary>
        Task NotificarParesAsync(IReadOnlyCollection<ParExistencia> pares);
    }
}
```

**`CompromisoCalculadora`** (`API.Domain.Core/Existencias`, estática, pura):

```csharp
using API.Domain.Interface.Existencias;

namespace API.Domain.Core.Existencias
{
    /// <summary>Una línea de pedido activa con su consumo ya sumado (en la unidad de la línea).</summary>
    public sealed record LineaPedidoAbierta(string CodArticulo, string CodAlmacen, decimal CantidadLinea, decimal Consumido, decimal FactorUom);

    public static class CompromisoCalculadora
    {
        /// <summary>Compara pares sin distinguir mayúsculas (la base ignora mayúsculas).</summary>
        public static readonly IEqualityComparer<ParExistencia> Comparador = new ParComparador();

        /// <summary>Cantidad abierta en unidad base: máx(0, cantidad − consumido) × factor (factor ≤ 0 se trata como 1).</summary>
        public static decimal AbiertoBase(decimal cantidadLinea, decimal consumido, decimal factorUom)
        {
            var abierto = cantidadLinea - consumido;
            if (abierto <= 0m) return 0m;
            var factor = factorUom <= 0m ? 1m : factorUom;
            return abierto * factor;
        }

        /// <summary>Suma lo abierto por (artículo, almacén). Solo incluye pares con cantidad mayor que 0.</summary>
        public static Dictionary<ParExistencia, decimal> SumarPorPar(IEnumerable<LineaPedidoAbierta> lineas)
        {
            var resultado = new Dictionary<ParExistencia, decimal>(Comparador);
            foreach (var l in lineas)
            {
                var abierto = AbiertoBase(l.CantidadLinea, l.Consumido, l.FactorUom);
                if (abierto <= 0m) continue;
                var par = new ParExistencia(l.CodArticulo.Trim(), l.CodAlmacen.Trim());
                resultado[par] = resultado.TryGetValue(par, out var actual) ? actual + abierto : abierto;
            }
            return resultado;
        }

        private sealed class ParComparador : IEqualityComparer<ParExistencia>
        {
            public bool Equals(ParExistencia? x, ParExistencia? y) =>
                x is not null && y is not null
                && string.Equals(x.CodArticulo, y.CodArticulo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CodAlmacen, y.CodAlmacen, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(ParExistencia p) =>
                HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(p.CodArticulo), StringComparer.OrdinalIgnoreCase.GetHashCode(p.CodAlmacen));
        }
    }
}
```

- [ ] **Step 1: Pruebas (fallan)** `CompromisoCalculadoraTests` con números exactos: `AbiertoBase(10, 0, 1) = 10`; `AbiertoBase(10, 4, 1) = 6`; `AbiertoBase(10, 10, 1) = 0`; consumo mayor que la cantidad (`AbiertoBase(10, 12, 1) = 0`); caja de 12 (`AbiertoBase(3, 1, 12) = 24`); factor 0 o negativo (`AbiertoBase(5, 0, 0) = 5`); `SumarPorPar` suma dos líneas del mismo par («A1», «01»: 10 y 6 → 16), mantiene pares distintos separados, ignora líneas con abierto 0, trata «a1»/«A1» y «01 »/«01» como el mismo par, y devuelve vacío sin líneas.
- [ ] **Step 2:** `dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~CompromisoCalculadoraTests" --nologo -v q` → falla (no compila).
- [ ] **Step 3:** Crea los tres archivos con el código de arriba. `Contratos.cs` agrupa los records y `TipoDocCompromiso`; `IExistenciaCompromisoService.cs` la interfaz (mismo namespace).
- [ ] **Step 4:** pruebas verdes, build de `API.sln` 0 errores, suite completa verde (anota la línea base y el conteo).
- [ ] **Step 5: Commit** (rutas explícitas): `feat(inventario): contratos y calculadora de comprometido y pedido`.

---

### Task 2: Servicio de recálculo

**Files:**
- Create: `API.Domain.Core/Existencias/ExistenciaCompromisoService.cs`
- Modify: `API.Service.WebApi/Startup.cs` (registro: `services.AddTransient<IExistenciaCompromisoService, ExistenciaCompromisoService>();`)
- Test: `API.Service.WebApi.Tests/Domain/ExistenciaCompromisoServiceTests.cs`

**Interfaces:**
- Consumes: `IExistenciaCompromisoService`, `ParExistencia`, `ResultadoRecalculo`, `TipoDocCompromiso` (Tarea 1); `CompromisoCalculadora`, `LineaPedidoAbierta`.
- Consumes (entidades/repos, verifica claves y propiedades en `API.Domain.Entity/Models`): `IRepositorioGenerico<Pedido,int>`, `<PedidoDetalle,(int Entry,int NoLinea)>`, `<Entrega,int>`, `<EntregaDetalle,(int,int)>`, `<Factura,int>`, `<FacturaDetalle,(int,int)>`, `<PedidoCompra,int>`, `<PedidoCompraDetalle,(int,int)>`, `<EntregaCompra,int>`, `<EntregaCompraDetalle,(int,int)>`, `<FacturaCompra,int>`, `<FacturaCompraDetalle,(int,int)>`, `<Articulo,string>`, `<ExistenciaArticulo,(string CodArticulo,string CodAlmacen)>`, y `ILogger<ExistenciaCompromisoService>` (`Microsoft.Extensions.Logging`; verifica que `Domain.Core` lo referencie; si no, usa `ILoggerFactory`/`ILogger` de `Microsoft.Extensions.Logging.Abstractions`).
- Produces: la implementación completa de `IExistenciaCompromisoService`.

**Reglas (spec §2 y Rulings 1-6):**
- **Líneas abiertas de ventas** para un conjunto de pares (o todas): líneas de `PedidoDetalle` cuyo `Pedido` es activo (`Cancelado != "S"` y `EstadoDoc != "C"`), con `CodArticulo` y `CodAlmacen` no vacíos y artículo de inventario (`ArticuloInventario == "S"`). El consumo por línea = suma de `Cantidad` de líneas de `EntregaDetalle` y `FacturaDetalle` con `BaseTipo == 4`, `BaseEntry == pedido.Entry`, `BaseLinea == linea.NoLinea` cuyo encabezado es activo (`Cancelado != "S"` y `EstadoDoc != "C"`).
- **Líneas abiertas de compras**: igual con `PedidoCompraDetalle` y consumo de `EntregaCompraDetalle`/`FacturaCompraDetalle` con `BaseTipo == 11` cuyo encabezado tiene `Cancelado != "S"`; el pedido de compra activo = `Cancelado != "S"` y `EstadoDoc != "C"`.
- **Consultas acotadas:** filtra primero los pedidos activos que contienen líneas de los pares pedidos (o todos los activos en `RecalcularTodoAsync`) y trae el consumo **solo de esos pedidos** (por lotes de 1,000 entries con `Contains(List<int>)`); proyecta solo las columnas necesarias; nada de cargar tablas completas ni una consulta por línea.
- **Escritura:** para cada par pedido en `RecalcularAsync`, `Comprometido` = suma de ventas del par (0 si no hay) y `Pedido` = suma de compras (0 si no hay). Si no existe la fila `ExistenciaArticulo` y el valor calculado es > 0 en alguna columna, se crea con `Disponible = 0`, `Comprometido`, `Pedido` y `FechaActualizacion = DateTime.Now`; si no existe y ambos son 0, no se crea. Si existe y el valor cambió, se actualiza (y `FechaActualizacion`); si no cambió no se escribe. Usa el patrón de los repositorios existentes (`ActualizarAsync` guarda por sí mismo; `AgregarSinGuardarAsync` + un solo `SaveChanges` al final si prefieres; mira `InventarioAsientoService`).
- **`RecalcularTodoAsync`:** calcula todos los pares con pedidos abiertos (ventas y compras), además revisa **todas** las filas de `ExistenciaArticulo` con `Comprometido != 0` o `Pedido != 0` y deja en 0 las que no tienen pedidos abiertos. `Filas` = filas de existencia revisadas (las que tenían valor o recibieron valor); `Corregidas` = las que cambiaron.
- **`ObtenerParesAsync`:** pedido (4, 11): pares de sus líneas con artículo, almacén e inventario. Entrega/factura (5, 6 ventas; 12, 13 compras): pares de sus propias líneas más los pares de las líneas de pedido a las que apuntan (`BaseTipo` 4 en ventas, 11 en compras; busca la línea de pedido por `BaseEntry`/`BaseLinea`). Un documento inexistente devuelve lista vacía.
- **`RecalcularPorDocumentoAsync`:** para 4 y 11 recalcula los pares de sus líneas; para 5, 6, 12, 13 solo trabaja si alguna línea apunta a un pedido (Ruling 5); si no, retorna sin consultar más.
- **Envoltorios seguros:** `NotificarDocumentoAsync`/`NotificarParesAsync` llaman a los anteriores dentro de `try/catch (Exception)` y registran con `_logger.LogError(ex, "...")` (mensaje en español con tipo y entry); nunca relanzan ni propagan; si `pares` es nulo o vacío no hacen nada.

- [ ] **Step 1: Pruebas (fallan)** `ExistenciaCompromisoServiceTests` con repos mock y números exactos (artículo `A1` inventariable, almacén `01`):
  1. pedido de venta activo de 10 → `Comprometido 10`.
  2. + entrega con línea de 4 (`BaseTipo 4`) → 6; + factura con línea de 6 (`BaseTipo 4`) → 0.
  3. entrega cancelada (`Cancelado "S"`) no consume → vuelve a 10; entrega con `EstadoDoc "C"` no consume en ventas.
  4. pedido cancelado o con `EstadoDoc "C"` → 0 (y la fila previa con 10 queda en 0).
  5. dos pedidos del mismo par (10 y 5) → 15; pares distintos (`01` y `02`) independientes; entrega que descuenta de otro almacén no cambia el par del pedido.
  6. unidad con factor 12 (3 cajas, consumo 1) → 24.
  7. artículo no inventariable ignorado; línea sin almacén ignorada.
  8. compras: pedido de compra de 20, entrega de compra de 5 (`BaseTipo 11`) → `Pedido 15`; factura de compra de 15 → 0; entrega de compra cancelada no consume.
  9. el mismo par con ventas y compras: `Comprometido` y `Pedido` se calculan por separado.
  10. crea la fila de existencias si falta (stock 0) y no la crea si ambos valores son 0; no escribe si el valor no cambió.
  11. `RecalcularTodoAsync`: deja en 0 los pares que ya no tienen pedidos abiertos, devuelve `Filas`/`Corregidas` exactos con un caso de 3 filas (1 sin cambio, 1 corregida, 1 puesta en 0).
  12. `ObtenerParesAsync` de una entrega con base de pedido devuelve sus pares más los del pedido (sin duplicados); de un documento inexistente devuelve vacío; de una factura sin base (POS) devuelve solo sus propias líneas y `RecalcularPorDocumentoAsync` no consulta pedidos.
  13. envoltorios seguros: con un repo que lanza, `NotificarDocumentoAsync` y `NotificarParesAsync` no lanzan y registran (verifica con un `ILogger` falso).
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~ExistenciaCompromisoServiceTests"` → falla.
- [ ] **Step 3: Implementa** el servicio y regístralo en `Startup.cs`.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(inventario): servicio de recalculo de comprometido y pedido`.

---

### Task 3: Endpoint de recálculo total

**Files:**
- Modify: `API.Application.Interface/IExistenciaApplication.cs`, `API.Application.Main/ExistenciaApplication.cs`, `API.Service.WebApi/Controllers/ExistenciaController.cs`
- Test: `API.Service.WebApi.Tests/Application/ExistenciaApplicationTests.cs` (amplía el existente), `API.Service.WebApi.Tests/Controllers/ExistenciaControllerTests.cs` (amplía o crea según exista)

**Interfaces:**
- Consumes: `IExistenciaCompromisoService.RecalcularTodoAsync()` y `ResultadoRecalculo` (Tareas 1-2).
- Produces: `IExistenciaApplication.RecalcularCompromisosAsync()` → `Task<Respuesta<ResultadoRecalculo>>` y `POST api/Existencia/RecalcularCompromisos` con `[RequierePermiso("Existencias", Accion.Actualizar)]`; 200 con `Respuesta<ResultadoRecalculo>` o 400 con el mensaje.

- [ ] **Step 1: Pruebas (fallan):** Application: éxito devuelve `Resultado = true` con `Dato.Filas`/`Corregidas`; una excepción del servicio devuelve `Resultado = false` con `ex.Message`. Controlador: ruta y verbo (`POST RecalcularCompromisos`), atributo `[RequierePermiso("Existencias", Accion.Actualizar)]` (reflexión), 200 y 400. Si `IExistenciaApplication` no recibe aún el servicio, agrega la dependencia como último parámetro del constructor y actualiza las pruebas.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~ExistenciaApplicationTests|FullyQualifiedName~ExistenciaControllerTests"` → falla.
- [ ] **Step 3: Implementa** `RecalcularCompromisosAsync` en Application y la acción en el controlador.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(inventario): endpoint para recalcular comprometido y pedido`.

---

### Task 4: Integración en ventas A — Pedido y línea de pedido

**Files:**
- Modify: `API.Domain.Core/PedidoDomain.cs`, `API.Domain.Core/PedidoDetalleDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/PedidoDomainTests.cs`, `PedidoDetalleDomainTests.cs` (o los archivos equivalentes existentes; verifica sus nombres), más una clase nueva `PedidoCompromisoTests.cs` para las pruebas de invocación

**Interfaces:**
- Consumes: `IExistenciaCompromisoService` (`NotificarDocumentoAsync`, `ObtenerParesAsync`, `NotificarParesAsync`) y `TipoDocCompromiso.Pedido` (Tareas 1-2).
- Produces: `PedidoDomain` y `PedidoDetalleDomain` con `IExistenciaCompromisoService` como último parámetro del constructor, invocándolo tras confirmar.

**Dónde invocar (lee cada método primero; la llamada va DESPUÉS de que el documento quedó guardado/confirmado, nunca dentro de la operación transaccional):**
- `PedidoDomain.InsertarAsync(Pedido obj, IEnumerable<PedidoDetalle> lineas)` (la sobrecarga con líneas que usa `_tx.EjecutarAsync`): tras el `await _tx.EjecutarAsync(...)`, `await _compromisos.NotificarDocumentoAsync(TipoDocCompromiso.Pedido, entry)`. La sobrecarga `InsertarAsync(Pedido obj)` sin líneas no compromete nada (no se invoca).
- `PedidoDomain.ActualizarAsync`: tras guardar, `NotificarDocumentoAsync(Pedido, id)` cuando cambió el estado (cancelar, cerrar, reabrir: mira cómo calcula `seCancela`/`seReabre`/`sigueActivo`); si hay otras actualizaciones que no tocan el estado, también es seguro invocarlo (idempotente), pero no lo invoques en las ramas que lanzan.
- `PedidoDomain.EliminarAsync`: `var pares = await _compromisos.ObtenerParesAsync(Pedido, id)` **antes** de borrar y `NotificarParesAsync(pares)` después de borrar con éxito.
- `PedidoDetalleDomain.InsertarAsync`, `ActualizarAsync` y `EliminarAsync`: tras guardar (o borrar) la línea, `NotificarDocumentoAsync(Pedido, entry)` (en `EliminarAsync` el documento sigue existiendo; en el caso de que la línea borrada fuera la única, el recálculo la deja en 0).
- Las ramas que lanzan excepción de negocio o devuelven `false` no invocan.

- [ ] **Step 1: Pruebas (fallan)** en `PedidoCompromisoTests` con `Mock<IExistenciaCompromisoService>` (`Strict` solo donde afirmes que NO se llama): (a) `InsertarAsync` con líneas invoca `NotificarDocumentoAsync(4, entryCreado)` exactamente una vez y **después** de `_tx.EjecutarAsync` (usa una secuencia con un doble que registra el orden); (b) cancelar/cerrar/reabrir invoca `NotificarDocumentoAsync(4, id)`; (c) `EliminarAsync` llama `ObtenerParesAsync` antes del borrado y `NotificarParesAsync` con esos pares después; (d) cada operación de `PedidoDetalleDomain` invoca `NotificarDocumentoAsync(4, entry)`; (e) cuando la operación lanza o devuelve `false`, NO se invoca; (f) un servicio que lanza **no** hace fallar la operación (con el mock configurado para lanzar `Exception`, el dominio devuelve su resultado normal: como el envoltorio real nunca lanza, esta prueba usa el servicio real con un repo que lanza, o verifica que el dominio no atrapa nada adicional y deja la garantía en el servicio; documenta cuál elegiste). Actualiza los constructores en las pruebas existentes de estos dominios con `Mock.Of<IExistenciaCompromisoService>()`.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~PedidoCompromisoTests"` → falla.
- [ ] **Step 3: Implementa** los cambios en los dos dominios.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde (todas las pruebas existentes de pedido siguen en verde).
- [ ] **Step 5: Commit**: `feat(inventario): pedido de venta actualiza el comprometido`.

---

### Task 5: Integración en ventas B — Entrega y Factura

**Files:**
- Modify: `API.Domain.Core/EntregaDomain.cs`, `API.Domain.Core/FacturaDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/EntregaCompromisoTests.cs`, `FacturaCompromisoTests.cs` + constructores de las pruebas existentes

**Interfaces:**
- Consumes: `IExistenciaCompromisoService` y `TipoDocCompromiso.Entrega`/`Factura` (Tareas 1-2).
- Produces: `EntregaDomain` y `FacturaDomain` con `IExistenciaCompromisoService` como último parámetro del constructor.

**Dónde invocar (tras confirmar; lee cada método):**
- `EntregaDomain.InsertarAsync` (la que usa `_tx.EjecutarAsync` con líneas): tras la transacción, `NotificarDocumentoAsync(Entrega, entry)`.
- `EntregaDomain.ActualizarAsync`: en la rama de cancelar/reabrir (donde llama `RevertirAsync` del inventario o reaplica) y cuando cambie el estado: tras guardar, `NotificarDocumentoAsync(Entrega, id)`.
- `EntregaDomain.EliminarAsync` (si existe): `ObtenerParesAsync(Entrega, id)` antes de borrar y `NotificarParesAsync` después.
- Lo mismo para `FacturaDomain.InsertarAsync` (tras la transacción; **la venta POS y las facturas sin base no deben pagar el costo**: lo garantiza `RecalcularPorDocumentoAsync`, pero la invocación sigue siendo un solo `await` barato), `ActualizarAsync` (cancelar/reabrir) y `EliminarAsync`.
- **Anidamiento:** `FacturaDomain.InsertarAsync` corre anidada dentro de la transacción exterior de `PosDomain.VenderAsync`: el envoltorio seguro se ejecuta con el documento ya guardado por el `SaveChanges` anidado y, al no haber base de pedido, retorna sin hacer nada. No agregues `try/catch` propios alrededor de las llamadas anidadas (regla del POS).

- [ ] **Step 1: Pruebas (fallan):** por cada dominio, el mismo patrón de la Tarea 4: alta invoca `NotificarDocumentoAsync(5|6, entry)` una vez **después** de la transacción; cancelar y reabrir invocan; eliminar usa `ObtenerParesAsync` antes y `NotificarParesAsync` después; cuando la operación lanza o devuelve `false` no se invoca; una entrega con líneas de base de pedido y una factura sin base hacen una sola llamada cada una. Verifica además con el **servicio real** (repos mock) el caso de integración: pedido de 10, entrega de 4 con `BaseTipo 4` creada con `EntregaDomain` → `Comprometido 6` (usa el servicio real de la Tarea 2 con los mismos repos mock compartidos si es viable; si no, deja ese caso para el humo de la Tarea 9 y dilo).
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~EntregaCompromisoTests|FullyQualifiedName~FacturaCompromisoTests"` → falla.
- [ ] **Step 3: Implementa** los cambios en ambos dominios.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(inventario): entrega y factura de venta actualizan el comprometido`.

---

### Task 6: Integración en compras A — Pedido de compra y línea

**Files:**
- Modify: `API.Domain.Core/PedidoCompraDomain.cs`, `API.Domain.Core/PedidoCompraDetalleDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/PedidoCompraCompromisoTests.cs` + constructores de las pruebas existentes

**Interfaces:**
- Consumes: `IExistenciaCompromisoService` y `TipoDocCompromiso.PedidoCompra` (Tareas 1-2).
- Produces: ambos dominios con `IExistenciaCompromisoService` como último parámetro del constructor.

**Dónde invocar:** igual que la Tarea 4 pero con `TipoDocCompromiso.PedidoCompra` (11):
- `PedidoCompraDomain.InsertarAsync(PedidoCompra)` crea solo la cabecera (las líneas llegan por `PedidoCompraDetalleDomain`): no invoca; la invocación va en el detalle.
- `ActualizarAsync` (cancelar/cerrar/reabrir y cualquier actualización guardada con éxito): tras guardar, `NotificarDocumentoAsync(PedidoCompra, id)`.
- `EliminarAsync`: `ObtenerParesAsync` **antes** de borrar líneas y cabecera; `NotificarParesAsync` después.
- `PedidoCompraDetalleDomain.InsertarAsync`/`ActualizarAsync`/`EliminarAsync`: tras guardar, `NotificarDocumentoAsync(PedidoCompra, entry)`.
- **Atención:** `PedidoCompraDomain.ActualizarAsync` reemplaza columnas y conserva `Nit`; la llamada nueva va al final y no altera esa lógica.

- [ ] **Step 1: Pruebas (fallan):** el mismo patrón de la Tarea 4 con tipo 11: cada operación del detalle invoca `NotificarDocumentoAsync(11, entry)`; la actualización invoca solo si se guardó; eliminar usa pares antes y después; sin llamada si la operación lanza o devuelve `false`; la creación de la cabecera sola no invoca.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~PedidoCompraCompromisoTests"` → falla.
- [ ] **Step 3: Implementa** los cambios.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(inventario): pedido de compra actualiza el pedido de existencias`.

---

### Task 7: Integración en compras B — Entrega de compra y Factura de compra

**Files:**
- Modify: `API.Domain.Core/EntregaCompraDomain.cs`, `API.Domain.Core/FacturaCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/EntregaCompraCompromisoTests.cs`, `FacturaCompraCompromisoTests.cs` + constructores existentes

**Interfaces:**
- Consumes: `IExistenciaCompromisoService` y `TipoDocCompromiso.EntregaCompra`/`FacturaCompra` (Tareas 1-2).
- Produces: ambos dominios con `IExistenciaCompromisoService` como último parámetro del constructor.

**Dónde invocar:** igual que la Tarea 5 con los tipos 12 y 13: alta (tras la transacción), cancelar/reabrir, eliminar (pares antes, notificar después). Las notas de débito de compra y las de crédito **no** se integran (no consumen pedidos).

- [ ] **Step 1: Pruebas (fallan):** el patrón de la Tarea 5 con tipos 12 y 13, incluyendo «alta con líneas con base de pedido de compra hace una llamada» y «alta sin base (directa) hace una llamada y el servicio retorna sin trabajar».
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~EntregaCompraCompromisoTests|FullyQualifiedName~FacturaCompraCompromisoTests"` → falla.
- [ ] **Step 3: Implementa** los cambios.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(inventario): entrega y factura de compra actualizan el pedido de existencias`.

---

### Task 8: Web — botón «Recalcular comprometido y pedido»

**Files:**
- Create: `Web.ApiClient/Clientes/IExistenciaApiClient.cs` y `ExistenciaApiClient.cs` **solo si no existen** (verifica: ya hay un cliente de existencias que usa `ExistenciasController`; amplíalo en lugar de crear uno nuevo), `Web.ApiClient/Dtos/Existencia/ResultadoRecalculoDTO.cs` (`Filas`, `Corregidas`)
- Modify: `Web.UI/Controllers/ExistenciasController.cs`, `Web.UI/Views/Existencias/Index.cshtml`, `Web.UI/wwwroot/js/existencias.js`, `Web/AGENTS.md`

**Interfaces:**
- Consumes: `POST api/Existencia/RecalcularCompromisos` → `Respuesta<ResultadoRecalculo>` (`{ filas, corregidas }`) (Tarea 3).
- Produces: método `RecalcularCompromisosAsync()` en el cliente de existencias; acción `POST /Existencias/RecalcularCompromisos` (`[ValidateAntiForgeryToken]`, `[RequierePermiso("Existencias", Accion.Actualizar)]`, devuelve el `Respuesta` tal cual); botón visible solo con `User.TienePermiso("Existencias", Accion.Actualizar)`.

**Qué hace:** en la pantalla de existencias un botón «Recalcular comprometido y pedido» (con ícono) que pide confirmación (SweetAlert: «Se recalcularán el comprometido y el pedido de todos los artículos a partir de los pedidos abiertos. ¿Continuar?»), se deshabilita mientras corre y muestra el resultado («N filas revisadas, M corregidas») y recarga la tabla. Errores con `App.mostrarError`. Sin `console.log`; escapa texto dinámico.

- [ ] **Step 1:** cliente, acción y vista. **Step 2:** JS con candado contra doble clic. **Step 3:** `dotnet build Web.slnx -p:BaseOutputPath="$env:TEMP\gescom-build-web\"` 0 errores y `node --check Web.UI/wwwroot/js/existencias.js`. **Step 4:** viñeta breve en `Web/AGENTS.md` (botón, permiso `Existencias` Actualizar, y que los números los mantiene la API). **Step 5: Commit** (Web, rutas explícitas): `feat(inventario): boton para recalcular comprometido y pedido`.

---

### Task 9: Humo con SQL real, documentación y cierre

**Files:** `<scratchpad>` (nada se commitea salvo correcciones con su prueba), `API/AGENTS.md`, `API/docs/traspaso-2026-11-07.md`

- [ ] **Step 1:** Crea una base temporal `API_DB_EVID<n>` con `<scratchpad>\evid-up.ps1` y levanta la API (`evid-up-api.ps1`/`pos-up-api.ps1`; lee antes `cv-comun.ps1` y `pos-final.ps1` para el login como `admin` y el armado del escenario: almacén, impuesto, cliente, proveedor, series, artículos con stock; **nunca** imprimas secretos).
- [ ] **Step 2: Flujos con números a mano** (artículo `A1` con 100 en stock, almacén `01`): (a) pedido de venta de 10 → `Comprometido 10` (`GET api/Existencia/A1/01`), `Disponible` (stock) intacto en 100; (b) entrega de 4 copiando del pedido → 6; factura de 6 copiando del pedido → 0; (c) cancelar la factura → 6; cancelar la entrega → 10; (d) cancelar el pedido → 0; (e) reabrir → 10; (f) editar la línea del pedido a 15 → 15; borrar la línea → 0; (g) entrega desde otro almacén (`02`) copiando el pedido de `01` → libera `01`; (h) pedido de compra de 20 → `Pedido 20`; entrega de compra de 5 copiada → 15; factura de compra de 15 → 0; cancelar la entrega de compra → 5; (i) una venta POS y una factura directa no cambian nada; (j) pedido con artículo no inventariable y línea sin almacén no comprometen. (k) **Relleno inicial:** pon a mano `Comprometido`/`Pedido` en valores erróneos con `UPDATE` (usa `sqlcmd -I`), llama `POST api/Existencia/RecalcularCompromisos` y verifica que las vuelve a dejar correctas, devuelve `Filas`/`Corregidas` coherentes y una segunda llamada devuelve `Corregidas = 0` (idempotente); crea además pedidos abiertos y luego borra las columnas a 0 para simular datos previos y repáralos con el recálculo. (l) Un usuario sin `Existencias` Actualizar recibe 403 en el endpoint. (m) Que `existencias-valor` y `stock-bajo-minimo` muestran los nuevos valores. (n) 0 errores 500 y ninguna advertencia de EF de no traducción en el log; mide el tiempo del recálculo total con ~500 pedidos abiertos (siémbralos por la API o por `INSERT` en la base temporal).
- [ ] **Step 3:** Corrige con prueba xUnit de regresión (TDD) lo que no cuadre (`fix(inventario): <qué>`); evidencia en `<scratchpad>\evid-comprometido-2026-11-20.txt`.
- [ ] **Step 4: Documentación.** `API/AGENTS.md`: reescribe la frase «`Comprometido`/`Pedido` hoy no los mantiene ningún documento» y agrega una viñeta **Comprometido y Pedido** (qué son, quién los mantiene: `IExistenciaCompromisoService`, regla de cálculo, post-confirmación con envoltorios seguros, endpoint `RecalcularCompromisos` y permiso, **ejecutarlo una vez al desplegar**, límites) y la línea base de la suite. `docs/traspaso-2026-11-07.md`: sección «Comprometido y Pedido» (qué se hizo, cómo se verificó, pasos para la base real: correr el recálculo una vez, límites y pendientes).
- [ ] **Step 5: Cierre.** Deja la base y la API vivas si habrá recorrido en navegador con el usuario (anota nombre, puerto y usuario de pruebas; sin contraseñas); si no, elimínalas con `evid-down-api.ps1` (léelo antes: solo afecta bases `API_DB_EVID<n>`) y verifica que no queda ninguna.
- [ ] **Step 6: Commit** (API, rutas explícitas): `docs(inventario): comprometido y pedido en agents y traspaso`.
