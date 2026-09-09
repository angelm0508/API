# Lista de precio en la creación de documentos — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Al elegir un artículo en la línea de un documento comercial, colocar el precio resuelto según la lista de precio del socio de negocio del documento (`Articulo.PrecioUnitario` × producto de `Factor` de la cadena `Base`, con redondeo configurable de la lista asignada), en vez del `PrecioUnitario` base.

**Architecture:** API N-capas (.NET 7): helper puro `RedondeoCalculadora` + `PrecioListaDomain` (recorre la cadena `Base`, detecta ciclos, multiplica factores, redondea) + `PrecioListaApplication` + endpoint nuevo `GET api/Articulo/{codigo}/Precio?listaPrecio={entry}`. Web MVC (.NET 8): DTO + método en `IArticuloApiClient`, un `PreciosController` proxy, helpers en `site.js`, y el mismo cambio en los 7 JS de documento (variable `listaPrecioSocio`, `onSeleccion` del socio, resolución del precio al elegir artículo, recálculo de líneas al cambiar el socio).

**Tech Stack:** C# / .NET 7 (API) y .NET 8 (Web), EF Core, AutoMapper, xUnit + Moq, jQuery + Bootstrap + SweetAlert2.

**Spec:** `API/docs/superpowers/specs/2026-09-09-lista-de-precio-en-documentos-design.md`

## Global Constraints

- **Repos y ramas:** API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`); Web en `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad git `panchoman08`. **Sin push hasta aprobación final del usuario.**
- **Build/test a carpeta externa** (no ensuciar el árbol): `-p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\"` para build de API; `...\apitest\` para `dotnet test`; `...\webbuild\` para build de Web.
- **`appsettings.json` de `API.Service.WebApi` puede aparecer modificado localmente con un connection string real — NUNCA commitearlo.** Hacer `git add` con rutas explícitas de archivo; **nunca `git add -A` ni `git add .`**.
- **Baseline suite API:** el spec anterior (`2026-09-02-ajustes-documentos`) cerró en **745 pruebas, 0 fallos**. Correr `dotnet test API.sln` una vez antes de la Task 1 y anotar el número real `B`. Cada tarea de API cierra en `B + (tests nuevos de esa tarea)`, **0 fallos**. Gate de Web: `dotnet build Web.slnx` → `0 Errores` (no hay suite de tests en el repo Web).
- **Los 7 documentos comerciales:** Cotización, Pedido, Entrega, Factura, Pedido de compra, Entrega de compra, Factura de compra. Los documentos de mercancía (Entrada/Salida) **no** entran.
- **Sin cambios de esquema. Sin `.sql`. Sin migración de datos.** La lista "Venta" y las demás se crean con la pantalla existente `Web /ListadosPrecio`.
- **`ArticuloNoExisteException`** ya existe en `API.Domain.Core/Inventario/ExcepcionesInventario.cs` (`public class ArticuloNoExisteException : Exception`, ctor `(string codArticulo)`, mensaje `"El artículo {codArticulo} no existe."`). Se **reutiliza**.
- **Redondeo:** taxonomía fija (definida hoy en `Web.UI/Controllers/ListadosPrecioController.cs`): `MetodoRedondeo` `0`/null=ninguno · `1`=1 · `2`=0.5 · `3`=5 · `4`=10 · `5`=25 (el múltiplo). `ReglaRedondeo` `"F"`=abajo · `"C"`=arriba · `"R"`/null=al más cercano. Se aplica la config de **la lista asignada** (`cadena[0]`), no de las bases. Siempre se cierra a 2 decimales.
- **`Factor`/`MetodoRedondeo`/`ReglaRedondeo` nulos → neutros** (`1`, sin múltiplo). **`PrecioUnitario` nulo → base `0`.**
- **`App.resolverPrecioLista` nunca bloquea:** ante lista nula, artículo inexistente, o error de la API, devuelve `null` y el llamador cae al `PrecioUnitario` del artículo.

---

## File Structure

### API (repo `angelm0508/API`, rama `desarrollo`)

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `API.Domain.Core/Precio/RedondeoCalculadora.cs` | Crear | Helper `static` puro: `Redondear(decimal, short?, string?) -> decimal`. Sin dependencias. |
| `API.Domain.Core/Precio/ExcepcionesPrecio.cs` | Crear | `PrecioListaInvalidaException : Exception` (lista inexistente / cadena circular / demasiado profunda). |
| `API.Domain.Interface/IPrecioListaDomain.cs` | Crear | `IPrecioListaDomain.ResolverAsync(string, int?) -> Task<PrecioResuelto>` + `record PrecioResuelto(string Codigo, decimal PrecioBase, decimal FactorEfectivo, decimal PrecioLista)`. |
| `API.Domain.Core/Precio/PrecioListaDomain.cs` | Crear | Implementa `IPrecioListaDomain`: carga artículo (`IArticuloDomain`), recorre cadena `Base` (`IListadoPrecioDomain`), multiplica factores, aplica `RedondeoCalculadora`. |
| `API.Application.DTO/precio/PrecioResueltoDTO.cs` | Crear | DTO de salida del endpoint. |
| `API.Application.Interface/IPrecioListaApplication.cs` | Crear | `ResolverAsync(string, int?) -> Task<Respuesta<PrecioResueltoDTO>>`. |
| `API.Application.Main/PrecioListaApplication.cs` | Crear | Llama al domain, mapea `PrecioResuelto -> PrecioResueltoDTO` (manual), `try/catch` → `Respuesta`. |
| `API.Service.WebApi/Controllers/ArticuloController.cs` | Modificar | + ctor `IPrecioListaApplication`; + `[HttpGet("{codigo}/Precio")]`. |
| `API.Service.WebApi/Startup.cs` | Modificar | + 2 `AddTransient` (`IPrecioListaDomain`, `IPrecioListaApplication`). |
| `API.Service.WebApi.Tests/Domain/RedondeoCalculadoraTests.cs` | Crear | `[Theory]` con la tabla del spec §2. |
| `API.Service.WebApi.Tests/Domain/PrecioListaDomainTests.cs` | Crear | Casos del spec §2 con `IArticuloDomain` / `IListadoPrecioDomain` mockeados. |
| `API.Service.WebApi.Tests/Controllers/ArticuloControllerTests.cs` | Modificar | + casos `Precio` 200 / 404 / 400 / sin `listaPrecio`. |

### Web (repo `angelm0508/Web`, rama `main`)

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `Web.ApiClient/Dtos/Precio/PrecioResueltoDTO.cs` | Crear | Espejo del DTO de la API. |
| `Web.ApiClient/Clientes/IArticuloApiClient.cs` | Modificar | + `ObtenerPrecioAsync(string, int?)`. |
| `Web.ApiClient/Clientes/ArticuloApiClient.cs` | Modificar | Implementa `ObtenerPrecioAsync` (`GetAsync<PrecioResueltoDTO>`). |
| `Web.UI/Controllers/PreciosController.cs` | Crear | Proxy: `Articulo(codigo, listaPrecio?)` y `ListaDeSocio(codigo)`. |
| `Web.UI/wwwroot/js/site.js` | Modificar | + `App.resolverPrecioLista(codigo, listaPrecio)` y `App.obtenerListaDeSocio(codigoSn)`. |
| `Web.UI/wwwroot/js/cotizaciones.js` | Modificar | Canónico: `listaPrecioSocio`, `onSeleccion` socio, `aplicarPrecioDeArticulo`, `ofrecerRecalcularLineas`, precarga en edición. |
| `Web.UI/wwwroot/js/pedidos.js` · `entregas.js` · `facturas.js` | Modificar | Misma receta (Task 6). |
| `Web.UI/wwwroot/js/pedidoscompra.js` · `entregascompra.js` · `facturascompra.js` | Modificar | Misma receta (Task 7). |

**Nota de decomposición:** el spec §4 bosquejó 10 tareas (cotización canónica + 6 transformaciones individuales + verificación). Este plan agrupa las 6 transformaciones en 2 lotes de 3 (venta / compra), como en el plan de `ajustes-documentos`. Total: 8 tareas.

---

## Task 1: `RedondeoCalculadora` (API, helper puro)

**Files:**
- Create: `API/API.Domain.Core/Precio/RedondeoCalculadora.cs`
- Test: `API/API.Service.WebApi.Tests/Domain/RedondeoCalculadoraTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `API.Domain.Core.Precio.RedondeoCalculadora.Redondear(decimal valor, short? metodo, string? regla) -> decimal`. `metodo`: `0`/null sin múltiplo, `1`→1, `2`→0.5, `3`→5, `4`→10, `5`→25. `regla`: `"F"` floor, `"C"` ceil, `"R"`/null/otro → nearest (AwayFromZero). Siempre `Math.Round(resultado, 2, MidpointRounding.AwayFromZero)` al final.

- [ ] **Step 1: Establecer baseline de la suite**

Run: `cd "C:\Users\migue\source\repos\angelm0508\API" && dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
Expected: `Con error: 0`. Anotar el total (`B`, se espera 745). Este número se usa en todas las tareas de API.

- [ ] **Step 2: Escribir el test (falla)**

Crear `API/API.Service.WebApi.Tests/Domain/RedondeoCalculadoraTests.cs`.
**`[InlineData]` no admite `decimal`** (no es constante de atributo válida en C#) — usar
`TheoryData` vía `[MemberData]`:

```csharp
using API.Domain.Core.Precio;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class RedondeoCalculadoraTests
    {
        public static TheoryData<decimal, short?, string?, decimal> Casos => new()
        {
            { 26.4m, 0,    "R", 26.40m },  // sin múltiplo
            { 26.4m, null, "R", 26.40m },  // método nulo
            { 26.4m, 1,    "R", 26.00m },  // a la unidad, al más cercano
            { 26.4m, 1,    "C", 27.00m },  // a la unidad, arriba
            { 26.4m, 1,    "F", 26.00m },  // a la unidad, abajo
            { 26.4m, 2,    "R", 26.50m },  // a 0.5
            { 26.4m, 3,    "R", 25.00m },  // a 5, al más cercano
            { 26.4m, 3,    "C", 30.00m },  // a 5, arriba
            { 26.4m, 3,    "F", 25.00m },  // a 5, abajo
            { 12.5m, 2,    "R", 12.50m },  // ya es múltiplo de 0.5
            { 24.0m, 5,    "C", 25.00m },  // a 25, arriba
            { 26.4m, 0,    null, 26.40m }, // regla nula con método 0
        };

        [Theory]
        [MemberData(nameof(Casos))]
        public void Redondear_SegunMetodoYRegla(decimal valor, short? metodo, string? regla, decimal esperado)
        {
            Assert.Equal(esperado, RedondeoCalculadora.Redondear(valor, metodo, regla));
        }
    }
}
```

(El literal `0` / `1` en `TheoryData<..., short?, ...>` convierte implícitamente a `short?`.)

- [ ] **Step 3: Ejecutar el test — debe fallar por compilación**

Run: `cd "C:\Users\migue\source\repos\angelm0508\API" && dotnet test API.sln --filter "FullyQualifiedName~RedondeoCalculadoraTests" -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
Expected: FAIL — `RedondeoCalculadora` no existe (error de compilación).

- [ ] **Step 4: Implementar `RedondeoCalculadora`**

Crear `API/API.Domain.Core/Precio/RedondeoCalculadora.cs`:

```csharp
namespace API.Domain.Core.Precio
{
    /// <summary>
    /// Redondeo de precios según la configuración de una lista de precio.
    /// <para>metodo: 0/null = sin múltiplo, 1 = 1, 2 = 0.5, 3 = 5, 4 = 10, 5 = 25.</para>
    /// <para>regla: "F" hacia abajo, "C" hacia arriba, "R"/null/otro = al más cercano.</para>
    /// </summary>
    public static class RedondeoCalculadora
    {
        public static decimal Redondear(decimal valor, short? metodo, string? regla)
        {
            decimal paso = (metodo ?? 0) switch
            {
                1 => 1m,
                2 => 0.5m,
                3 => 5m,
                4 => 10m,
                5 => 25m,
                _ => 0m
            };

            decimal resultado;
            if (paso == 0m)
            {
                resultado = valor;
            }
            else
            {
                var q = valor / paso;
                var qRedondeado = (regla ?? "R").Trim().ToUpperInvariant() switch
                {
                    "F" => Math.Floor(q),
                    "C" => Math.Ceiling(q),
                    _ => Math.Round(q, MidpointRounding.AwayFromZero)
                };
                resultado = qRedondeado * paso;
            }

            return Math.Round(resultado, 2, MidpointRounding.AwayFromZero);
        }
    }
}
```

- [ ] **Step 5: Ejecutar el test — debe pasar**

Run: `cd "C:\Users\migue\source\repos\angelm0508\API" && dotnet test API.sln --filter "FullyQualifiedName~RedondeoCalculadoraTests" -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
Expected: PASS (12 casos).

- [ ] **Step 6: Suite completa en verde**

Run: `dotnet test API.sln -p:BaseOutputPath="...\apitest\"`
Expected: `Con error: 0`, total `B + 12`.

- [ ] **Step 7: Commit**

```bash
cd "C:\Users\migue\source\repos\angelm0508\API"
git add API.Domain.Core/Precio/RedondeoCalculadora.cs API.Service.WebApi.Tests/Domain/RedondeoCalculadoraTests.cs
git commit -m "feat(precio): RedondeoCalculadora para listas de precio"
```

---

## Task 2: `PrecioListaDomain` (API, resolución de precio)

**Files:**
- Create: `API/API.Domain.Interface/IPrecioListaDomain.cs`
- Create: `API/API.Domain.Core/Precio/ExcepcionesPrecio.cs`
- Create: `API/API.Domain.Core/Precio/PrecioListaDomain.cs`
- Test: `API/API.Service.WebApi.Tests/Domain/PrecioListaDomainTests.cs`

**Interfaces:**
- Consumes:
  - `API.Domain.Core.Precio.RedondeoCalculadora.Redondear(decimal, short?, string?)` (Task 1).
  - `API.Domain.Interface.IArticuloDomain.ObtenerPorCodigoAsync(string sku) -> Task<Articulo>` (devuelve `null` si no existe). `Articulo.PrecioUnitario` es `decimal?`.
  - `API.Domain.Interface.IListadoPrecioDomain.ObtenerAsync(int id) -> Task<ListadoPrecio>` (devuelve `null` si no existe). `ListadoPrecio` tiene `Entry` (int), `Base` (`int?`), `Factor` (`decimal?`), `MetodoRedondeo` (`short?`), `ReglaRedondeo` (`string?`).
  - `ArticuloNoExisteException(string codArticulo)` de `API.Domain.Core.Inventario`.
- Produces:
  - `record PrecioResuelto(string Codigo, decimal PrecioBase, decimal FactorEfectivo, decimal PrecioLista)` en `API.Domain.Interface`.
  - `IPrecioListaDomain.ResolverAsync(string codigoArticulo, int? listaPrecioEntry) -> Task<PrecioResuelto>`.
  - `PrecioListaInvalidaException : Exception` en `API.Domain.Core.Precio` — mensajes: `"La cadena de listas de precio es circular."`, `"La cadena de listas de precio es demasiado profunda."`, `"La lista de precio {entry} no está registrada."`. **Ninguno contiene el substring `"El artículo"`** (el controller lo usa para distinguir 404 de artículo vs 400 de lista).

- [ ] **Step 1: Crear la interfaz + record**

Crear `API/API.Domain.Interface/IPrecioListaDomain.cs`:

```csharp
namespace API.Domain.Interface
{
    /// <summary>Resultado de resolver el precio de un artículo contra una lista de precio.</summary>
    public record PrecioResuelto(string Codigo, decimal PrecioBase, decimal FactorEfectivo, decimal PrecioLista);

    public interface IPrecioListaDomain
    {
        /// <summary>
        /// Devuelve el precio del artículo aplicando la lista de precio indicada (cadena Base
        /// recursiva de factores + redondeo de la lista asignada). Si <paramref name="listaPrecioEntry"/>
        /// es null, devuelve el PrecioUnitario base redondeado a 2 decimales.
        /// Lanza <see cref="API.Domain.Core.Inventario.ArticuloNoExisteException"/> si el artículo no existe.
        /// </summary>
        Task<PrecioResuelto> ResolverAsync(string codigoArticulo, int? listaPrecioEntry);
    }
}
```

- [ ] **Step 2: Crear la excepción**

Crear `API/API.Domain.Core/Precio/ExcepcionesPrecio.cs`:

```csharp
namespace API.Domain.Core.Precio
{
    public class PrecioListaInvalidaException : Exception
    {
        public PrecioListaInvalidaException(string mensaje) : base(mensaje) { }

        public static PrecioListaInvalidaException Circular() =>
            new("La cadena de listas de precio es circular.");

        public static PrecioListaInvalidaException DemasiadoProfunda() =>
            new("La cadena de listas de precio es demasiado profunda.");

        public static PrecioListaInvalidaException NoExiste(int entry) =>
            new($"La lista de precio {entry} no está registrada.");
    }
}
```

- [ ] **Step 3: Escribir los tests (fallan)**

Crear `API/API.Service.WebApi.Tests/Domain/PrecioListaDomainTests.cs`:

```csharp
using API.Domain.Core.Inventario;
using API.Domain.Core.Precio;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class PrecioListaDomainTests
    {
        private readonly Mock<IArticuloDomain> _articulos = new();
        private readonly Mock<IListadoPrecioDomain> _listas = new();
        private readonly PrecioListaDomain _sut;

        public PrecioListaDomainTests()
        {
            _sut = new PrecioListaDomain(_articulos.Object, _listas.Object);
        }

        private void Articulo(string cod, decimal? precio) =>
            _articulos.Setup(a => a.ObtenerPorCodigoAsync(cod))
                .ReturnsAsync(new Articulo { Codigo = cod, PrecioUnitario = precio });

        private void Lista(int entry, decimal? factor, int? bas = null, short? metodo = null, string? regla = null) =>
            _listas.Setup(l => l.ObtenerAsync(entry))
                .ReturnsAsync(new ListadoPrecio { Entry = entry, Factor = factor, Base = bas, MetodoRedondeo = metodo, ReglaRedondeo = regla });

        [Fact]
        public async Task SinLista_DevuelvePrecioBaseRedondeado()
        {
            Articulo("A", 22m);
            var r = await _sut.ResolverAsync("A", null);
            Assert.Equal(22.00m, r.PrecioLista);
            Assert.Equal(1m, r.FactorEfectivo);
            Assert.Equal(22m, r.PrecioBase);
        }

        [Fact]
        public async Task ListaConFactor_MultiplicaYRedondea()
        {
            Articulo("0003-0002", 22m);
            Lista(1, 1.20m);
            var r = await _sut.ResolverAsync("0003-0002", 1);
            Assert.Equal(26.40m, r.PrecioLista);
            Assert.Equal(1.20m, r.FactorEfectivo);
        }

        [Fact]
        public async Task CadenaBase_MultiplicaLosFactores()
        {
            Articulo("A", 100m);
            Lista(10, 1.10m, bas: 20);  // asignada
            Lista(20, 1.20m, bas: null); // raíz
            var r = await _sut.ResolverAsync("A", 10);
            Assert.Equal(1.32m, r.FactorEfectivo);
            Assert.Equal(132.00m, r.PrecioLista);
        }

        [Fact]
        public async Task Redondeo_UsaConfigDeLaListaAsignada()
        {
            Articulo("0003-0002", 22m);
            Lista(1, 1.20m, metodo: 3, regla: "C"); // a 5, arriba → 26.4 → 30
            var r = await _sut.ResolverAsync("0003-0002", 1);
            Assert.Equal(30.00m, r.PrecioLista);
        }

        [Fact]
        public async Task FactorNulo_SeTrataComoUno()
        {
            Articulo("A", 50m);
            Lista(1, null);
            var r = await _sut.ResolverAsync("A", 1);
            Assert.Equal(50.00m, r.PrecioLista);
            Assert.Equal(1m, r.FactorEfectivo);
        }

        [Fact]
        public async Task PrecioUnitarioNulo_BaseCero()
        {
            Articulo("A", null);
            Lista(1, 1.20m);
            var r = await _sut.ResolverAsync("A", 1);
            Assert.Equal(0m, r.PrecioBase);
            Assert.Equal(0.00m, r.PrecioLista);
        }

        [Fact]
        public async Task ArticuloInexistente_LanzaArticuloNoExiste()
        {
            _articulos.Setup(a => a.ObtenerPorCodigoAsync("NOPE")).ReturnsAsync((Articulo)null!);
            await Assert.ThrowsAsync<ArticuloNoExisteException>(() => _sut.ResolverAsync("NOPE", null));
        }

        [Fact]
        public async Task ListaInexistente_LanzaPrecioListaInvalida()
        {
            Articulo("A", 10m);
            _listas.Setup(l => l.ObtenerAsync(999)).ReturnsAsync((ListadoPrecio)null!);
            await Assert.ThrowsAsync<PrecioListaInvalidaException>(() => _sut.ResolverAsync("A", 999));
        }

        [Fact]
        public async Task CicloDirecto_LanzaPrecioListaInvalida()
        {
            Articulo("A", 10m);
            Lista(5, 1.1m, bas: 5); // se apunta a sí misma
            await Assert.ThrowsAsync<PrecioListaInvalidaException>(() => _sut.ResolverAsync("A", 5));
        }

        [Fact]
        public async Task CadenaDe11Niveles_LanzaPrecioListaInvalida()
        {
            Articulo("A", 10m);
            for (int i = 1; i <= 12; i++)
                Lista(i, 1.0m, bas: i < 12 ? i + 1 : (int?)null);
            await Assert.ThrowsAsync<PrecioListaInvalidaException>(() => _sut.ResolverAsync("A", 1));
        }
    }
}
```

- [ ] **Step 4: Ejecutar los tests — fallan por compilación**

Run: `dotnet test API.sln --filter "FullyQualifiedName~PrecioListaDomainTests" -p:BaseOutputPath="...\apitest\"`
Expected: FAIL — `PrecioListaDomain` no existe.

- [ ] **Step 5: Implementar `PrecioListaDomain`**

Crear `API/API.Domain.Core/Precio/PrecioListaDomain.cs`:

```csharp
using API.Domain.Core.Inventario;
using API.Domain.Entity.Models;
using API.Domain.Interface;

namespace API.Domain.Core.Precio
{
    public class PrecioListaDomain : IPrecioListaDomain
    {
        private const int MaxProfundidad = 10;

        private readonly IArticuloDomain _articulos;
        private readonly IListadoPrecioDomain _listas;

        public PrecioListaDomain(IArticuloDomain articulos, IListadoPrecioDomain listas)
        {
            _articulos = articulos;
            _listas = listas;
        }

        public async Task<PrecioResuelto> ResolverAsync(string codigoArticulo, int? listaPrecioEntry)
        {
            var articulo = await _articulos.ObtenerPorCodigoAsync(codigoArticulo)
                ?? throw new ArticuloNoExisteException(codigoArticulo);

            decimal precioBase = articulo.PrecioUnitario ?? 0m;

            if (listaPrecioEntry is null)
                return new PrecioResuelto(codigoArticulo, precioBase, 1m,
                    RedondeoCalculadora.Redondear(precioBase, null, null));

            var cadena = new List<ListadoPrecio>();
            var visitados = new HashSet<int>();
            int? actual = listaPrecioEntry;

            while (actual is not null)
            {
                if (!visitados.Add(actual.Value))
                    throw PrecioListaInvalidaException.Circular();
                if (cadena.Count >= MaxProfundidad)
                    throw PrecioListaInvalidaException.DemasiadoProfunda();

                var lp = await _listas.ObtenerAsync(actual.Value)
                    ?? throw PrecioListaInvalidaException.NoExiste(actual.Value);

                cadena.Add(lp);
                actual = lp.Base;
            }

            decimal factorEfectivo = 1m;
            foreach (var lp in cadena)
                factorEfectivo *= lp.Factor ?? 1m;

            var asignada = cadena[0];
            decimal precioLista = RedondeoCalculadora.Redondear(
                precioBase * factorEfectivo, asignada.MetodoRedondeo, asignada.ReglaRedondeo);

            return new PrecioResuelto(codigoArticulo, precioBase, factorEfectivo, precioLista);
        }
    }
}
```

- [ ] **Step 6: Ejecutar los tests — pasan**

Run: `dotnet test API.sln --filter "FullyQualifiedName~PrecioListaDomainTests" -p:BaseOutputPath="...\apitest\"`
Expected: PASS (10 casos).

- [ ] **Step 7: Suite completa**

Run: `dotnet test API.sln -p:BaseOutputPath="...\apitest\"`
Expected: `Con error: 0`, total `B + 22`.

- [ ] **Step 8: Commit**

```bash
cd "C:\Users\migue\source\repos\angelm0508\API"
git add API.Domain.Interface/IPrecioListaDomain.cs API.Domain.Core/Precio/ExcepcionesPrecio.cs API.Domain.Core/Precio/PrecioListaDomain.cs API.Service.WebApi.Tests/Domain/PrecioListaDomainTests.cs
git commit -m "feat(precio): PrecioListaDomain (cadena Base + factor + redondeo)"
```

---

## Task 3: `PrecioListaApplication` + endpoint + DI (API)

**Files:**
- Create: `API/API.Application.DTO/precio/PrecioResueltoDTO.cs`
- Create: `API/API.Application.Interface/IPrecioListaApplication.cs`
- Create: `API/API.Application.Main/PrecioListaApplication.cs`
- Modify: `API/API.Service.WebApi/Controllers/ArticuloController.cs`
- Modify: `API/API.Service.WebApi/Startup.cs`
- Test: `API/API.Service.WebApi.Tests/Controllers/ArticuloControllerTests.cs`

**Interfaces:**
- Consumes:
  - `IPrecioListaDomain.ResolverAsync(string, int?) -> Task<PrecioResuelto>` (Task 2). `PrecioResuelto` = `record (string Codigo, decimal PrecioBase, decimal FactorEfectivo, decimal PrecioLista)`.
  - `ArticuloNoExisteException` (mensaje contiene `"no existe"`), `PrecioListaInvalidaException`.
  - `Respuesta<T>` de `API.Application.DTO` (`bool Resultado`, `string Mensaje`, `T Dato`).
- Produces:
  - `PrecioResueltoDTO { string Codigo; decimal PrecioBase; decimal FactorEfectivo; decimal PrecioLista; }`.
  - `IPrecioListaApplication.ResolverAsync(string, int?) -> Task<Respuesta<PrecioResueltoDTO>>`.
  - Endpoint `GET api/Articulo/{codigo}/Precio?listaPrecio={int?}` → `200 Respuesta<PrecioResueltoDTO>` OK; `404` si el mensaje contiene `"El artículo"` (viene de `ArticuloNoExisteException`); `400` en cualquier otro error de negocio (lista inexistente / ciclo / profundidad).

- [ ] **Step 1: Crear el DTO**

Crear `API/API.Application.DTO/precio/PrecioResueltoDTO.cs`:

```csharp
namespace API.Application.DTO.precio
{
    public class PrecioResueltoDTO
    {
        public string Codigo { get; set; } = null!;
        public decimal PrecioBase { get; set; }
        public decimal FactorEfectivo { get; set; }
        public decimal PrecioLista { get; set; }
    }
}
```

- [ ] **Step 2: Crear la interfaz de Application**

Crear `API/API.Application.Interface/IPrecioListaApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.precio;

namespace API.Application.Interface
{
    public interface IPrecioListaApplication
    {
        Task<Respuesta<PrecioResueltoDTO>> ResolverAsync(string codigoArticulo, int? listaPrecioEntry);
    }
}
```

- [ ] **Step 3: Crear la Application**

Crear `API/API.Application.Main/PrecioListaApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.precio;
using API.Application.Interface;
using API.Domain.Interface;

namespace API.Application.Main
{
    public class PrecioListaApplication : IPrecioListaApplication
    {
        private readonly IPrecioListaDomain _domain;

        public PrecioListaApplication(IPrecioListaDomain domain)
        {
            _domain = domain;
        }

        public async Task<Respuesta<PrecioResueltoDTO>> ResolverAsync(string codigoArticulo, int? listaPrecioEntry)
        {
            var respuesta = new Respuesta<PrecioResueltoDTO>();
            try
            {
                var r = await _domain.ResolverAsync(codigoArticulo, listaPrecioEntry);
                respuesta.Dato = new PrecioResueltoDTO
                {
                    Codigo = r.Codigo,
                    PrecioBase = r.PrecioBase,
                    FactorEfectivo = r.FactorEfectivo,
                    PrecioLista = r.PrecioLista
                };
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Resultado = false;
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
    }
}
```

> Verificar en `Respuesta<T>` los nombres reales de las propiedades (`Resultado` / `Mensaje` / `Dato`) y si el patrón del proyecto pone `respuesta.Resultado = true` explícito o ya viene `true` por defecto — mirar otra Application (p. ej. `ListadoPrecioApplication` o `ArticuloApplication`) y copiar el estilo exacto.

- [ ] **Step 4: Registrar en DI**

En `API/API.Service.WebApi/Startup.cs`, junto a las líneas de `IListadoPrecioDomain` / `IListadoPrecioApplication` (buscar `AddTransient<IListadoPrecioDomain`):

```csharp
services.AddTransient<IPrecioListaDomain, PrecioListaDomain>();
services.AddTransient<IPrecioListaApplication, PrecioListaApplication>();
```

`IArticuloDomain` (dependencia de `PrecioListaDomain`) e `IListadoPrecioDomain` ya están
registrados en `Startup.cs` — no agregar nada más. Agregar los `using` necesarios si el archivo
no usa `using` global (`API.Domain.Core.Precio`, `API.Application.Main` ya deberían estar por las
otras líneas).

- [ ] **Step 5: Agregar el endpoint al `ArticuloController`**

En `API/API.Service.WebApi/Controllers/ArticuloController.cs`:

- Al ctor: agregar parámetro `IPrecioListaApplication precioListaApplication` y campo `private readonly IPrecioListaApplication _precioListaApplication;` (asignarlo). Agregar `using API.Application.DTO.precio;`.
- Después del `[HttpGet("{codigo}")]` `Articulo(...)` existente, agregar:

```csharp
        [HttpGet("{codigo}/Precio")]
        public async Task<ActionResult<Respuesta<PrecioResueltoDTO>>> Precio(
            [FromRoute] string codigo, [FromQuery] int? listaPrecio)
        {
            var res = await _precioListaApplication.ResolverAsync(codigo, listaPrecio);
            if (res.Resultado)
                return Ok(res);

            // "El artículo X no existe." viene de ArticuloNoExisteException -> 404.
            // Lista inexistente / ciclo / profundidad -> 400.
            return res.Mensaje != null &&
                   res.Mensaje.Contains("El artículo", StringComparison.OrdinalIgnoreCase)
                ? NotFound(res)
                : BadRequest(res);
        }
```

- [ ] **Step 6: Escribir los tests de controller (fallan)**

En `API/API.Service.WebApi.Tests/Controllers/ArticuloControllerTests.cs` — revisar cómo está construido el `ArticuloController` en el `ctor` de la clase de tests (qué mocks recibe) y **agregar** `Mock<IPrecioListaApplication>` a esa construcción. Luego agregar:

```csharp
        [Fact]
        public async Task Precio_Ok_DevuelveOk200()
        {
            _precioListaApp
                .Setup(a => a.ResolverAsync("0003-0002", 1))
                .ReturnsAsync(new Respuesta<PrecioResueltoDTO>
                {
                    Resultado = true,
                    Dato = new PrecioResueltoDTO { Codigo = "0003-0002", PrecioBase = 22m, FactorEfectivo = 1.20m, PrecioLista = 26.40m }
                });

            var r = await _controller.Precio("0003-0002", 1);

            var ok = Assert.IsType<OkObjectResult>(r.Result);
            var payload = Assert.IsType<Respuesta<PrecioResueltoDTO>>(ok.Value);
            Assert.Equal(26.40m, payload.Dato!.PrecioLista);
        }

        [Fact]
        public async Task Precio_ArticuloNoExiste_Devuelve404()
        {
            _precioListaApp
                .Setup(a => a.ResolverAsync("NOPE", null))
                .ReturnsAsync(new Respuesta<PrecioResueltoDTO> { Resultado = false, Mensaje = "El artículo NOPE no existe." });

            var r = await _controller.Precio("NOPE", null);

            Assert.IsType<NotFoundObjectResult>(r.Result);
        }

        [Fact]
        public async Task Precio_ListaInvalida_Devuelve400()
        {
            _precioListaApp
                .Setup(a => a.ResolverAsync("0003-0002", 999))
                .ReturnsAsync(new Respuesta<PrecioResueltoDTO> { Resultado = false, Mensaje = "La lista de precio 999 no está registrada." });

            var r = await _controller.Precio("0003-0002", 999);

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }
```

(Ajustar los nombres `_precioListaApp` / `_controller` a como estén nombrados los campos en esa clase de tests. Añadir `using API.Application.DTO.precio;`.)

- [ ] **Step 7: Ejecutar los tests — fallan, luego build y pasan**

Run: `dotnet test API.sln --filter "FullyQualifiedName~ArticuloControllerTests" -p:BaseOutputPath="...\apitest\"`
Expected: primero FAIL (compilación / mock faltante), tras completar Steps 3-6 → PASS.

- [ ] **Step 8: Suite completa**

Run: `dotnet test API.sln -p:BaseOutputPath="...\apitest\"`
Expected: `Con error: 0`, total `B + 25`.

- [ ] **Step 9: Commit**

```bash
cd "C:\Users\migue\source\repos\angelm0508\API"
git add API.Application.DTO/precio/PrecioResueltoDTO.cs API.Application.Interface/IPrecioListaApplication.cs API.Application.Main/PrecioListaApplication.cs API.Service.WebApi/Controllers/ArticuloController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Controllers/ArticuloControllerTests.cs
git commit -m "feat(precio): endpoint GET api/Articulo/{codigo}/Precio y DI"
```

---

## Task 4: Plomería Web (ApiClient + `PreciosController` + helpers `site.js`)

**Files:**
- Create: `Web/Web.ApiClient/Dtos/Precio/PrecioResueltoDTO.cs`
- Modify: `Web/Web.ApiClient/Clientes/IArticuloApiClient.cs`
- Modify: `Web/Web.ApiClient/Clientes/ArticuloApiClient.cs`
- Create: `Web/Web.UI/Controllers/PreciosController.cs`
- Modify: `Web/Web.UI/wwwroot/js/site.js`

**Interfaces:**
- Consumes:
  - Endpoint API `GET api/Articulo/{codigo}/Precio?listaPrecio={int?}` (Task 3).
  - `ApiClientBase.GetAsync<T>(string path) -> Task<Respuesta<T>>` (patrón de `ArticuloApiClient`).
  - `Respuesta<T>` en `Web.ApiClient.Dtos` (`resultado` / `mensaje` / `dato` al serializar a JSON camelCase).
  - `ISocioNegocioApiClient` — método de "obtener por código" (verificar el nombre real; en `ArticuloApiClient` es `ObtenerAsync(string codigo)`).
- Produces:
  - `Web.ApiClient.Dtos.Precio.PrecioResueltoDTO` (`Codigo`, `PrecioBase`, `FactorEfectivo`, `PrecioLista`).
  - `IArticuloApiClient.ObtenerPrecioAsync(string codigo, int? listaPrecio) -> Task<Respuesta<PrecioResueltoDTO>>`.
  - MVC: `GET /Precios/Articulo?codigo=&listaPrecio=` → `Json(Respuesta<PrecioResueltoDTO>)`. `GET /Precios/ListaDeSocio?codigo=` → `Json(Respuesta<SocioNegocioDTO>)`.
  - JS: `App.resolverPrecioLista(codigo, listaPrecio) -> Promise<number|null>` (el `precioLista`, o `null`). `App.obtenerListaDeSocio(codigoSn) -> Promise<number|null>` (el `numLstPrecio`, o `null`).

- [ ] **Step 1: DTO Web**

Crear `Web/Web.ApiClient/Dtos/Precio/PrecioResueltoDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.Precio
{
    public class PrecioResueltoDTO
    {
        public string Codigo { get; set; } = null!;
        public decimal PrecioBase { get; set; }
        public decimal FactorEfectivo { get; set; }
        public decimal PrecioLista { get; set; }
    }
}
```

- [ ] **Step 2: Método en el ApiClient**

En `Web/Web.ApiClient/Clientes/IArticuloApiClient.cs` agregar `using Web.ApiClient.Dtos.Precio;` y a la interfaz:

```csharp
        Task<Respuesta<PrecioResueltoDTO>> ObtenerPrecioAsync(string codigo, int? listaPrecio);
```

En `Web/Web.ApiClient/Clientes/ArticuloApiClient.cs` agregar `using Web.ApiClient.Dtos.Precio;` y:

```csharp
        public Task<Respuesta<PrecioResueltoDTO>> ObtenerPrecioAsync(string codigo, int? listaPrecio)
        {
            var ruta = $"{Recurso}/{Uri.EscapeDataString(codigo)}/Precio";
            if (listaPrecio.HasValue) ruta += $"?listaPrecio={listaPrecio.Value}";
            return GetAsync<PrecioResueltoDTO>(ruta);
        }
```

- [ ] **Step 3: `PreciosController`**

Verificar en `Web/Web.ApiClient/Clientes/ISocioNegocioApiClient.cs` el nombre del método "obtener por código" (se asume `ObtenerAsync(string codigo) -> Task<Respuesta<SocioNegocioDTO>>`). Crear `Web/Web.UI/Controllers/PreciosController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;

namespace Web.UI.Controllers
{
    [Authorize]
    public class PreciosController : Controller
    {
        private readonly IArticuloApiClient _articulos;
        private readonly ISocioNegocioApiClient _socios;

        public PreciosController(IArticuloApiClient articulos, ISocioNegocioApiClient socios)
        {
            _articulos = articulos;
            _socios = socios;
        }

        [HttpGet]
        public async Task<IActionResult> Articulo(string codigo, int? listaPrecio)
            => Json(await _articulos.ObtenerPrecioAsync(codigo, listaPrecio));

        [HttpGet]
        public async Task<IActionResult> ListaDeSocio(string codigo)
            => Json(await _socios.ObtenerAsync(codigo));
    }
}
```

(Si `ISocioNegocioApiClient` no está registrado en DI o el método se llama distinto, ajustar. `ISocioNegocioApiClient` ya se usa en `SociosNegocioController` — copiar de ahí el nombre real.)

- [ ] **Step 4: Helpers en `site.js`**

En `Web/Web.UI/wwwroot/js/site.js`, dentro del objeto `App` (después de `eliminar:` o donde encaje), agregar:

```javascript
    /**
     * Resuelve el precio de un artículo según una lista de precio.
     * Devuelve Number(precioLista) o null (sin lista, artículo inexistente, o error de la API);
     * el llamador cae entonces al PrecioUnitario del artículo.
     */
    resolverPrecioLista: async function (codigo, listaPrecio) {
        if (!codigo || listaPrecio == null || listaPrecio === '') return null;
        try {
            const r = await $.get('/Precios/Articulo', { codigo, listaPrecio });
            if (r && r.resultado && r.dato && r.dato.precioLista != null) {
                return Number(r.dato.precioLista);
            }
        } catch (e) { /* cae a null */ }
        return null;
    },

    /** Devuelve el NumLstPrecio de un socio (o null). Para precargar la lista en edición. */
    obtenerListaDeSocio: async function (codigoSn) {
        if (!codigoSn) return null;
        try {
            const r = await $.get('/Precios/ListaDeSocio', { codigo: codigoSn });
            const n = r && r.resultado && r.dato ? (r.dato.numLstPrecio ?? null) : null;
            return n != null ? Number(n) : null;
        } catch (e) { return null; }
    },
```

- [ ] **Step 5: Build de ambos repos**

Run: `cd "C:\Users\migue\source\repos\angelm0508\Web" && dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`
Expected: `0 Errores`.

- [ ] **Step 6: Commits (uno por repo)**

```bash
cd "C:\Users\migue\source\repos\angelm0508\Web"
git add Web.ApiClient/Dtos/Precio/PrecioResueltoDTO.cs Web.ApiClient/Clientes/IArticuloApiClient.cs Web.ApiClient/Clientes/ArticuloApiClient.cs Web.UI/Controllers/PreciosController.cs Web.UI/wwwroot/js/site.js
git commit -m "feat(precio): ApiClient, PreciosController y helpers de resolución de precio de lista"
```

---

## Task 5: `cotizaciones.js` — pantalla canónica

**Files:**
- Modify: `Web/Web.UI/wwwroot/js/cotizaciones.js`

**Interfaces:**
- Consumes: `App.resolverPrecioLista(codigo, listaPrecio)`, `App.obtenerListaDeSocio(codigoSn)` (Task 4). `Swal.fire` (SweetAlert2, ya cargado globalmente).
- Produces: la receta de 5 puntos que las Tasks 6 y 7 replican en los otros 6 JS.

**Precondición a verificar en el archivo (debe cumplirse tal cual):**
- `function inicializarBuscadorSocio()` con `onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')`.
- Bloque de `let` de módulo que incluye `let lineasLocales = [];`, `let lineasRemotas = [];`, `let sincronizandoArticulo = false;` — **al nivel del `$(function () {` de arriba**, no dentro de `inicializarDetalle`.
- `function esEdicionDetalle()` y `function pintarDetalle()`.
- `function inicializarDetalle()` que arranca con `lineasLocales = []; lineasRemotas = [];`.
- `function aplicarArticuloSeleccionado(a, origen)` que contiene, tras `if (!a) { buscadorAlmacen.establecer(null); return; }`, la línea `$('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);` seguida de `aplicarAlmacenDefectoDeArticulo(a);` y `recalcularLinea();`.

Si algo difiere, adaptar los nombres locales en los pasos siguientes (no abortar).

- [ ] **Step 1: Declarar la variable de módulo**

Junto a `let sincronizandoArticulo = false;` (bloque de `let` de módulo, ~línea 217) agregar:

```javascript
    let listaPrecioSocio = null;
```

- [ ] **Step 2: `onSeleccion` del buscador de socio**

En `inicializarBuscadorSocio()`, reemplazar:

```javascript
            onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')
```

por:

```javascript
            onSeleccion: async s => {
                $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '');
                const nueva = s ? (s.numLstPrecio ?? s.NumLstPrecio ?? null) : null;
                if (nueva === listaPrecioSocio) return;
                listaPrecioSocio = nueva;
                await ofrecerRecalcularLineas();
            }
```

- [ ] **Step 3: Reset + precarga de `listaPrecioSocio` en `inicializarDetalle`**

En `inicializarDetalle()`, donde hoy dice:

```javascript
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        noLineaOriginalEnEdicion = null;
```

agregar debajo:

```javascript
        listaPrecioSocio = null;
        const codSnActual = $('#CodigoSn').val();
        if (codSnActual) {
            App.obtenerListaDeSocio(codSnActual).then(n => { listaPrecioSocio = n; });
        }
```

- [ ] **Step 4: Resolver el precio de lista al elegir artículo**

En `aplicarArticuloSeleccionado(a, origen)`, reemplazar la línea:

```javascript
        $('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);
```

por:

```javascript
        aplicarPrecioDeArticulo(a);
```

y agregar, inmediatamente después de la función `aplicarArticuloSeleccionado` (antes de `aplicarAlmacenDefectoDeArticulo`):

```javascript
    // Coloca en #detPrecio el precio de lista del socio, o el PrecioUnitario base si no hay lista.
    async function aplicarPrecioDeArticulo(a) {
        const base = a.precioUnitario ?? a.PrecioUnitario ?? 0;
        const codArt = a.codigo ?? a.Codigo;
        const pLista = await App.resolverPrecioLista(codArt, listaPrecioSocio);
        $('#detPrecio').val(pLista != null ? pLista : base);
        recalcularLinea();
    }
```

- [ ] **Step 5: `ofrecerRecalcularLineas`**

Agregar como `function` (al nivel del `$(function () {` de módulo, junto a `aplicarPrecioDeArticulo` o cerca de `pintarDetalle`):

```javascript
    // Al cambiar el socio en creación, ofrece recalcular los precios de las líneas ya cargadas.
    async function ofrecerRecalcularLineas() {
        if (esEdicionDetalle()) return;                 // en edición el encabezado es de solo lectura
        if (!lineasLocales.length || listaPrecioSocio == null) return;

        const ok = await Swal.fire({
            icon: 'question',
            title: '¿Actualizar precios?',
            text: 'Se recalcularán los precios de las líneas según la lista de precio del socio seleccionado.',
            showCancelButton: true,
            confirmButtonText: 'Actualizar',
            cancelButtonText: 'Mantener'
        }).then(r => r.isConfirmed);
        if (!ok) return;

        for (const l of lineasLocales) {
            const cod = l.codArticulo ?? l.CodArticulo ?? l.codigo ?? l.Codigo;
            const p = await App.resolverPrecioLista(cod, listaPrecioSocio);
            if (p != null) { l.precio = p; l.Precio = p; }
        }
        pintarDetalle();
    }
```

> Verificar con qué clave se guarda el código de artículo en `lineasLocales` (mirar cómo se arma `datosForm` al agregar una línea — en `cotizaciones.js` el hidden es `#detCodArticulo` y el objeto usa `CodArticulo`). Ajustar el `l.codArticulo ?? l.CodArticulo ?? ...` si hace falta.

- [ ] **Step 6: Build Web**

Run: `cd "C:\Users\migue\source\repos\angelm0508\Web" && dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"`
Expected: `0 Errores` (JS no compila, pero valida que no se rompió ninguna vista/Razor).

- [ ] **Step 7: Smoke manual (para el ejecutor o el usuario)**

Con el sitio corriendo:
- Crear una lista `Venta` (Factor `1.20`, sin redondeo) en `ListadosPrecio` y asignarla a un cliente en `SociosNegocio`.
- Nueva cotización → elegir ese cliente → agregar línea, elegir `0003-0002` → `#detPrecio` = `26.40`.
- Cliente sin lista → `#detPrecio` = `22.00`.
- Con 1-2 líneas, cambiar a un cliente con lista distinta → aparece el `Swal`; "Actualizar" recalcula, "Mantener" no.
- Editar una cotización existente de un cliente con lista → agregar una línea nueva → toma el precio de lista.

- [ ] **Step 8: Commit**

```bash
cd "C:\Users\migue\source\repos\angelm0508\Web"
git add Web.UI/wwwroot/js/cotizaciones.js
git commit -m "feat(precio): cotizaciones aplica la lista de precio del socio al elegir artículo"
```

---

## Task 6: Transformación a `pedidos.js`, `entregas.js`, `facturas.js` (venta)

**Files:**
- Modify: `Web/Web.UI/wwwroot/js/pedidos.js`
- Modify: `Web/Web.UI/wwwroot/js/entregas.js`
- Modify: `Web/Web.UI/wwwroot/js/facturas.js`

**Interfaces:**
- Consumes: idéntico a Task 5.
- Produces: nada nuevo (aplica la misma receta).

- [ ] **Step 1: Verificar la precondición en los 3 archivos**

Para cada uno, confirmar que existe: `inicializarBuscadorSocio()` con `onSeleccion: s => $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '')`; el bloque de `let` de módulo con `lineasLocales` / `lineasRemotas` / `sincronizandoArticulo`; `esEdicionDetalle()`; la función de re-render (esperada `pintarDetalle`; si se llama distinto —p. ej. `renderizarDetalle`— anotarlo y usar ese nombre); `inicializarDetalle()` con `lineasLocales = []; lineasRemotas = [];`; `aplicarArticuloSeleccionado(a, origen)` con la línea `$('#detPrecio').val(a.precioUnitario ?? a.PrecioUnitario ?? 0);`.

Si algún archivo NO cumple, detener y reportar la diferencia antes de editarlo.

- [ ] **Step 2: Aplicar los 5 cambios de Task 5 a cada archivo**

Misma receta que Task 5 Steps 1-5, adaptando solo el nombre de la función de re-render si difiere:

1. `let listaPrecioSocio = null;` en el bloque de `let` de módulo.
2. `onSeleccion` del socio → versión `async` que setea `listaPrecioSocio` y llama `ofrecerRecalcularLineas()`.
3. En `inicializarDetalle()`, tras el reset de `lineasLocales`/`lineasRemotas`: `listaPrecioSocio = null;` + precarga con `App.obtenerListaDeSocio($('#CodigoSn').val())`.
4. En `aplicarArticuloSeleccionado`: la línea de `$('#detPrecio').val(...)` → `aplicarPrecioDeArticulo(a);` + agregar la función `aplicarPrecioDeArticulo` (idéntica a Task 5 Step 4).
5. Agregar `ofrecerRecalcularLineas` (idéntica a Task 5 Step 5, con `<funcion de re-render>` = la real de ese archivo).

- [ ] **Step 3: Build Web**

Run: `cd "C:\Users\migue\source\repos\angelm0508\Web" && dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"`
Expected: `0 Errores`.

- [ ] **Step 4: Smoke manual**

Repetir el smoke de Task 5 Step 7 en **Factura de venta** (la más crítica). Verificar rápido en Pedido y Entrega que al elegir artículo con cliente-con-lista el precio sale multiplicado.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\migue\source\repos\angelm0508\Web"
git add Web.UI/wwwroot/js/pedidos.js Web.UI/wwwroot/js/entregas.js Web.UI/wwwroot/js/facturas.js
git commit -m "feat(precio): pedidos, entregas y facturas de venta aplican la lista de precio del socio"
```

---

## Task 7: Transformación a `pedidoscompra.js`, `entregascompra.js`, `facturascompra.js` (compra)

**Files:**
- Modify: `Web/Web.UI/wwwroot/js/pedidoscompra.js`
- Modify: `Web/Web.UI/wwwroot/js/entregascompra.js`
- Modify: `Web/Web.UI/wwwroot/js/facturascompra.js`

**Interfaces:**
- Consumes: idéntico a Task 5.
- Produces: nada nuevo.

- [ ] **Step 1: Verificar la precondición en los 3 archivos** — igual que Task 6 Step 1. En compra el socio del `onSeleccion` es el **proveedor**; el modelo (`SocioNegocio.NumLstPrecio`) no distingue tipo, así que la receta es la misma.

- [ ] **Step 2: Aplicar los 5 cambios** — igual que Task 6 Step 2.

- [ ] **Step 3: Build Web**

Run: `cd "C:\Users\migue\source\repos\angelm0508\Web" && dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"`
Expected: `0 Errores`.

- [ ] **Step 4: Smoke manual**

Crear una lista y asignarla a un **proveedor**. Nueva factura de compra a ese proveedor → elegir artículo → `#detPrecio` sale con el factor aplicado. Proveedor sin lista → precio base.

- [ ] **Step 5: Commit**

```bash
cd "C:\Users\migue\source\repos\angelm0508\Web"
git add Web.UI/wwwroot/js/pedidoscompra.js Web.UI/wwwroot/js/entregascompra.js Web.UI/wwwroot/js/facturascompra.js
git commit -m "feat(precio): pedidos, entregas y facturas de compra aplican la lista de precio del socio"
```

---

## Task 8: Verificación conjunta

**Files:** ninguno nuevo.

- [ ] **Step 1: Build + suite API**

Run: `cd "C:\Users\migue\source\repos\angelm0508\API" && dotnet build API.sln -p:BaseOutputPath="...\apibuild\"` → `0 Errores`.
Run: `dotnet test API.sln -p:BaseOutputPath="...\apitest\"` → `Con error: 0`, total `B + 25`.

- [ ] **Step 2: Build Web**

Run: `cd "C:\Users\migue\source\repos\angelm0508\Web" && dotnet build Web.slnx -p:BaseOutputPath="...\webbuild\"` → `0 Errores`.

- [ ] **Step 3: `git status` en ambos repos**

Confirmar que **no** quedó `appsettings.json` staged ni cambios sueltos fuera de lo commiteado. `git log --oneline -6` en cada repo debe mostrar los commits de este plan.

- [ ] **Step 4: Checklist manual (para el usuario)**

Preparación: en `ListadosPrecio` crear `Venta` (Factor `1.20`, `MetodoRedondeo` = Ninguno) y `Mayorista` (Factor `1.35`, Base = `Venta`, `MetodoRedondeo` = "Redondear a 5", `ReglaRedondeo` = "Al más cercano"). Asignar `Venta` a un cliente y `Mayorista` a otro; asignar `Venta` a un proveedor.

- **Cliente con `Venta`** → nueva cotización → línea con `0003-0002` (Q22) → `#detPrecio` = `26.40`.
- **Cliente con `Mayorista`** (cadena Base `Mayorista→Venta`, factor `1.35 × 1.20 = 1.62`, `22 × 1.62 = 35.64` → redondeo a 5 al más cercano → `35.00`) → `#detPrecio` = `35.00`.
- **Cliente sin lista** → `#detPrecio` = `22.00`.
- **Editar precio a mano** tras el autollenado → se respeta al guardar.
- **Cambiar de cliente** con 2 líneas cargadas → `Swal` de confirmación; "Actualizar" recalcula ambas; "Mantener" las deja; borrar el cliente → líneas intactas, líneas nuevas vuelven al precio base.
- **Editar** una cotización existente de un cliente con lista → agregar una línea nueva → toma el precio de lista.
- Repetir el "camino feliz" en **Pedido, Entrega y Factura de venta**, y en **Factura de compra** con un proveedor con lista.
- **API caída / lista borrada:** con la lista asignada a un cliente pero borrada de la tabla → elegir artículo → cae al precio base, sin bloquear ni error visible molesto.

- [ ] **Step 5: Recordatorio para el usuario**

- Reiniciar las sesiones de depuración de Visual Studio (API y Web.UI) — `ArticuloController` cambió de constructor (nuevo parámetro `IPrecioListaApplication`).
- Nada de esquema cambió; no hay `.sql` que aplicar.
- Las listas de precio y su asignación a socios se administran con las pantallas existentes (`ListadosPrecio`, `SociosNegocio`).

- [ ] **Step 6: Commit de cierre (si quedó algo suelto, p. ej. docs)**

```bash
cd "C:\Users\migue\source\repos\angelm0508\API"
git add docs/ && git commit -m "docs: cierre lista de precio en documentos" || echo "nada que commitear"
```

---

## Notas de auto-revisión (cobertura del spec)

- **Spec §1 (algoritmo):** `RedondeoCalculadora` → Task 1. Recorrido de cadena `Base` + ciclos + profundidad + producto de factores + redondeo de `cadena[0]` → Task 2 (`PrecioListaDomain`). `PrecioResuelto` record en `API.Domain.Interface` (espeja `MovimientoRequest`). `PrecioResueltoDTO` + `IPrecioListaApplication` + `PrecioListaApplication` + endpoint `GET api/Articulo/{codigo}/Precio` + DI → Task 3.
- **Spec §2 (pruebas API):** tabla de redondeo → Task 1 Step 2 (`[Theory]`, 12 casos, incluye los 9 del spec + 3 extra). Casos de dominio (sin lista, factor simple 26.40, cadena L0×L1 = 1.32, ciclo, profundidad 11, artículo/factor/precio nulos, redondeo real método 3 regla C → 30) → Task 2 Step 3 (10 casos). Controller 200/404/400 → Task 3 Step 6.
- **Spec §3 (Web):** DTO + `IArticuloApiClient.ObtenerPrecioAsync` + `PreciosController` (`Articulo` + `ListaDeSocio`) + `App.resolverPrecioLista` + `App.obtenerListaDeSocio` → Task 4. Los 7 JS: variable `listaPrecioSocio`, `onSeleccion` del socio (setea + `ofrecerRecalcularLineas`), `aplicarPrecioDeArticulo` en `aplicarArticuloSeleccionado`, `ofrecerRecalcularLineas` con `Swal`, precarga en edición vía `App.obtenerListaDeSocio` → Task 5 (canónico) + Task 6 (3 venta) + Task 7 (3 compra). Borrado del socio → `listaPrecioSocio = null`, sin recálculo (cubierto por la guarda `listaPrecioSocio == null` en `ofrecerRecalcularLineas` y `resolverPrecioLista`).
- **Spec §4 (estructura):** este plan agrupa las 6 transformaciones en 2 lotes (Task 6 venta, Task 7 compra) en vez de 6 tareas sueltas — mismo criterio que el plan de `ajustes-documentos`. Task 8 = verificación.
- **Decisión 404 vs 400:** el controller distingue por substring `"El artículo"` del mensaje de `ArticuloNoExisteException` (`"El artículo {x} no existe."`, bajo nuestro control). Los mensajes de `PrecioListaInvalidaException` (`"...circular."`, `"...demasiado profunda."`, `"La lista de precio {n} no está registrada."`) **no** contienen ese substring → 400. El spec §1 lo dejó abierto ("el contrato es artículo → 404, lista/ciclo → 400") — cumplido. Funcionalmente el front cae al precio base ante cualquier no-`resultado`, así que 404 vs 400 no afecta la UX. **(Ajuste de pre-flight: el borrador original usaba `"no existe"` como discriminador, que colisionaba con el mensaje de lista-no-registrada.)**
- **Redondeo de listas base:** se usa solo `cadena[0]` (lista asignada) — Task 2 Step 5, `var asignada = cadena[0];`. Coincide con el spec y con SAP B1.
- **Fuera de alcance (spec):** precios por artículo dentro de una lista, `SocioNegocio.Descuento`, recálculo de documentos guardados, `ExtMonto`/`RndFrmtInt`/`RndFrmtDec`, seed de datos, documentos de mercancía. Ninguna tarea los toca.
- **Placeholder scan:** sin "TBD"/"TODO". "`B`" es el baseline real que la Task 1 Step 1 fija (se espera 745); todos los totales de suite son `B + N` con `N` explícito por tarea (12, 22, 25). Los "verificar el nombre real" (`Respuesta<T>` props, método de `ISocioNegocioApiClient`, función de re-render de cada JS) son chequeos de precondición con acción definida, no huecos.
- **Consistencia de tipos:** `PrecioResuelto` (record, 4 campos) ↔ `PrecioResueltoDTO` (API) ↔ `PrecioResueltoDTO` (Web) ↔ `r.dato.precioLista` (JS camelCase) — nombres alineados. `ResolverAsync(string, int?)` mismo nombre en `IPrecioListaDomain` y `IPrecioListaApplication`. `App.resolverPrecioLista` / `App.obtenerListaDeSocio` usados con esos nombres exactos en Tasks 5-7.
