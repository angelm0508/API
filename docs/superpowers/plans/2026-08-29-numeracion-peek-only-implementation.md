# Numeración de solo consulta + generación atómica en Artículos/Socios de Negocio — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hacer que `NumeracionDocumentoDetDomain.GenerarCodigoAsync` sea de solo consulta (nunca incrementa/persiste el consecutivo por sí solo), y que Artículos y Socios de Negocio generen y avancen el consecutivo de su serie de forma atómica dentro de su propio `InsertarAsync`, igual que ya hace `CotizacionDomain`.

**Architecture:** Se elimina la mutación/persistencia del consecutivo dentro de `GenerarCodigoAsync` y se extrae su lógica de formateo a un helper estático reutilizable. `ArticuloDomain` y `SocioNegocioDomain` ganan una dependencia al repo genérico de `NumeracionDocumentoDet` y replican, dentro de su propio `InsertarAsync`, el mismo patrón ya usado por `CotizacionDomain`: buscar la serie, validar, y — para series no manuales — calcular el código y avanzar `SigNumero` **solo en memoria** (sin `ActualizarAsync` explícito), confiando en que ambos repos comparten el mismo `DbContext` scoped por request. Como el código generado ahora solo se conoce después de insertar, `InsertarAsync` pasa de devolver `bool` a devolver el `Codigo` real (`string`), propagándose ese cambio de tipo por Domain → Application → Controller (API) y por ApiClient → Controller Web → JS.

**Tech Stack:** .NET 7 (API), .NET 8 (Web), EF Core, AutoMapper, xUnit + Moq (API), jQuery + DataTables (Web).

**Spec:** [docs/superpowers/specs/2026-08-29-numeracion-peek-only-design.md](../specs/2026-08-29-numeracion-peek-only-design.md)

## Global Constraints

- El consecutivo de una serie (`SigNumero`) solo avanza cuando el documento/registro correspondiente se registra de verdad — nunca por el solo hecho de generar/previsualizar un código.
- Series manuales (`Manual == "S"`) no cambian de comportamiento: el valor lo sigue escribiendo el cliente.
- No se agrega bloqueo de fila ni transacción explícita a nivel de base de datos (el riesgo de colisión entre altas simultáneas sobre la misma serie no manual queda en el mismo nivel que ya acepta `CotizacionDomain` hoy).
- No se agrega lógica de "crear a partir del documento anterior" en ningún módulo.
- Todo cambio en API debe dejar `dotnet test` en verde antes de pasar al siguiente task.

---

### Task 1: `NumeracionDocumentoDetDomain.GenerarCodigoAsync` de solo consulta

**Files:**
- Modify: `API.Domain.Core/NumeracionDocumentoDetDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/NumeracionDocumentoDetDomainTests.cs` (crear — no existe hoy)

**Interfaces:**
- Produces: `NumeracionDocumentoDetDomain.FormatearCodigo(NumeracionDocumentoDet linea)` — método estático público, usado por los Tasks 2 y 3.
- `GenerarCodigoAsync(int serie)` sigue devolviendo `Task<string>`, sin cambios de firma.

- [ ] **Step 1: Escribir las pruebas que fallan**

Crear `API.Service.WebApi.Tests/Domain/NumeracionDocumentoDetDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NumeracionDocumentoDetDomainTests
    {
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoMock;
        private readonly NumeracionDocumentoDetDomain _domain;

        public NumeracionDocumentoDetDomainTests()
        {
            _repoMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new NumeracionDocumentoDetDomain(_repoMock.Object);
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "3",
            Serie = 5,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N",
            IniCadena = "COT-",
            CantDigitos = 4,
            FinCadena = ""
        };

        [Fact]
        public async Task GenerarCodigoAsync_NoIncrementaNiPersisteElConsecutivo()
        {
            var serie = SerieAutogenerada(sigNumero: 1);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            var codigo = await _domain.GenerarCodigoAsync(5);

            Assert.Equal("COT-0001", codigo);
            Assert.Equal(1, serie.SigNumero);
            _repoMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task GenerarCodigoAsync_LlamadoDosVecesSeguidas_DevuelveElMismoCodigo()
        {
            var serie = SerieAutogenerada(sigNumero: 1);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            var primero = await _domain.GenerarCodigoAsync(5);
            var segundo = await _domain.GenerarCodigoAsync(5);

            Assert.Equal(primero, segundo);
            Assert.Equal("COT-0001", segundo);
        }

        [Fact]
        public async Task GenerarCodigoAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public async Task GenerarCodigoAsync_SerieInexistente_Lanza()
        {
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NumeracionDocumentoDet?)null);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public async Task GenerarCodigoAsync_SinSigNumero_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: null);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public async Task GenerarCodigoAsync_NumeracionAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoMock.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.GenerarCodigoAsync(5));
        }

        [Fact]
        public void FormatearCodigo_ArmaElCodigoConPaddingDeCeros()
        {
            var serie = SerieAutogenerada(sigNumero: 7);

            var codigo = NumeracionDocumentoDetDomain.FormatearCodigo(serie);

            Assert.Equal("COT-0007", codigo);
        }
    }
}
```

- [ ] **Step 2: Ejecutar las pruebas y confirmar que fallan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~NumeracionDocumentoDetDomainTests"
```

Esperado: `GenerarCodigoAsync_NoIncrementaNiPersisteElConsecutivo` y `GenerarCodigoAsync_LlamadoDosVecesSeguidas_DevuelveElMismoCodigo` fallan (el código actual sí incrementa/persiste); `FormatearCodigo_ArmaElCodigoConPaddingDeCeros` falla con error de compilación (`FormatearCodigo` no existe todavía) — es esperado, forma parte de este mismo paso.

- [ ] **Step 3: Implementar el cambio**

En `API.Domain.Core/NumeracionDocumentoDetDomain.cs`, reemplazar el método `GenerarCodigoAsync` completo por:

```csharp
        public async Task<string> GenerarCodigoAsync(int serie)
        {
            var linea = await ObtenerAsync(serie);
            if (linea == null)
            {
                throw new Exception("La serie no existe.");
            }

            if (linea.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para generar códigos.");
            }

            if (linea.SigNumero == null)
            {
                throw new Exception("La serie no tiene configurado el número siguiente.");
            }

            if (linea.FinNumero.HasValue && linea.SigNumero.Value > linea.FinNumero.Value)
            {
                throw new Exception("Se agotó la numeración disponible en esta serie.");
            }

            // Solo consulta: NO se incrementa ni se persiste el consecutivo aquí. El consecutivo
            // real solo avanza cuando el documento se registra de verdad (ver CotizacionDomain,
            // ArticuloDomain, SocioNegocioDomain) -- llamar a este método varias veces sin registrar
            // nada debe devolver siempre el mismo código.
            return FormatearCodigo(linea);
        }

        public static string FormatearCodigo(NumeracionDocumentoDet linea)
        {
            var numeroFormateado = linea.SigNumero!.Value.ToString().PadLeft(linea.CantDigitos ?? 0, '0');
            return $"{linea.IniCadena}{numeroFormateado}{linea.FinCadena}";
        }
```

- [ ] **Step 4: Ejecutar las pruebas y confirmar que pasan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~NumeracionDocumentoDetDomainTests"
```

Esperado: 7/7 PASS.

- [ ] **Step 5: Commit**

```bash
git add API.Domain.Core/NumeracionDocumentoDetDomain.cs API.Service.WebApi.Tests/Domain/NumeracionDocumentoDetDomainTests.cs
git commit -m "fix: GenerarCodigoAsync deja de incrementar/persistir el consecutivo (solo consulta)"
```

---

### Task 2: `ArticuloDomain` genera y avanza el código de forma atómica al insertar

**Files:**
- Create: `API.Service.WebApi.Tests/TestHelpers/TestAsyncQueryable.cs`
- Modify: `API.Domain.Interface/IArticuloDomain.cs`
- Modify: `API.Domain.Core/ArticuloDomain.cs`
- Modify: `API.Application.Interface/IArticuloApplication.cs`
- Modify: `API.Application.Main/ArticuloApplication.cs`
- Modify: `API.Service.WebApi/Controllers/ArticuloController.cs`
- Modify: `API.Application.DTO/articulo/articulo/ArticuloCrearDTO.cs`
- Modify: `API.Service.WebApi.Tests/Controllers/ArticuloControllerTests.cs`
- Test: `API.Service.WebApi.Tests/Domain/ArticuloDomainTests.cs` (crear — no existe hoy)

**Interfaces:**
- Consumes: `NumeracionDocumentoDetDomain.FormatearCodigo(NumeracionDocumentoDet)` (Task 1).
- Produces: `IArticuloDomain.InsertarAsync(Articulo obj)` ahora devuelve `Task<string>` (el `Codigo` real, ya sea el generado o el manual) en vez de `Task<bool>`; `IArticuloApplication.InsertarAsync(ArticuloCrearDTO obj)` ahora devuelve `Task<Respuesta<string>>`. Estos dos cambios de tipo los consume el Task 4 (Web ApiClient).

- [ ] **Step 1: Crear el helper de pruebas para `IQueryable` async**

`ObtenerPorCodigoAsync` (ya existente, sin cambios) usa `FirstOrDefaultAsync` de EF Core sobre el resultado de `ObtenerTodoAsync()`. Un `List<T>.AsQueryable()` normal no soporta esos operadores async y lanza en tiempo de ejecución ("IQueryable object... does not implement IAsyncEnumerable"). Se necesita este helper para poder probar `InsertarAsync` (que ahora invoca `ObtenerPorCodigoAsync` igual que antes). Crear `API.Service.WebApi.Tests/TestHelpers/TestAsyncQueryable.cs`:

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace API.Service.WebApi.Tests.TestHelpers
{
    // Helper mínimo (patrón documentado por Microsoft para probar código EF Core con mocks) que
    // permite que un IQueryable<T> en memoria soporte los operadores async de EF Core
    // (FirstOrDefaultAsync, etc.). Sin esto, cualquier prueba que llegue a un código que use esos
    // operadores sobre un repo mockeado lanza en tiempo de ejecución.
    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;
        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
        public T Current => _inner.Current;
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());
        public ValueTask DisposeAsync() { _inner.Dispose(); return ValueTask.CompletedTask; }
    }

    internal class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;
        public TestAsyncQueryProvider(IQueryable<T> source) => _inner = source.Provider;

        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
        public object? Execute(Expression expression) => _inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, new[] { typeof(Expression) })!
                .MakeGenericMethod(expectedResultType)
                .Invoke(this, new object[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    internal static class QueryableAsyncExtensions
    {
        public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source) => new TestAsyncEnumerable<T>(source);
    }
}
```

- [ ] **Step 2: Escribir las pruebas que fallan**

Crear `API.Service.WebApi.Tests/Domain/ArticuloDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ArticuloDomainTests
    {
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticuloMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly ArticuloDomain _domain;

        public ArticuloDomainTests()
        {
            _repoArticuloMock = new Mock<IRepositorioGenerico<Articulo, string>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new ArticuloDomain(_repoArticuloMock.Object, _repoNumeracionMock.Object);

            _repoArticuloMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<Articulo>().AsAsyncQueryable());
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "2",
            Serie = 7,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "--",
            TipoSerie = "N",
            IniCadena = "ART-",
            CantDigitos = 4,
            FinCadena = ""
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_GeneraCodigoYAvanzaSigNumero()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.InsertarAsync(It.IsAny<Articulo>())).ReturnsAsync((Articulo a) => a);

            var obj = new Articulo { Serie = 7 };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("ART-0005", codigo);
            Assert.Equal("ART-0005", obj.Codigo);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaCodigoDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.InsertarAsync(It.IsAny<Articulo>())).ReturnsAsync((Articulo a) => a);

            var obj = new Articulo { Serie = 7, Codigo = "MANUAL-1" };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("MANUAL-1", codigo);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinCodigo_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);

            var obj = new Articulo { Serie = 7, Codigo = "" };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);

            var obj = new Articulo { Serie = 7 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);

            var obj = new Articulo { Serie = 7 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new Articulo { Serie = 7 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(serie);
            _repoArticuloMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<Articulo> { new() { Codigo = "ART-0005" } }.AsAsyncQueryable());

            var obj = new Articulo { Serie = 7 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoArticuloMock.Verify(r => r.InsertarAsync(It.IsAny<Articulo>()), Times.Never);
        }
    }
}
```

- [ ] **Step 3: Ejecutar las pruebas y confirmar que fallan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~ArticuloDomainTests"
```

Esperado: error de compilación (`ArticuloDomain` no tiene un constructor con dos parámetros; `InsertarAsync` devuelve `bool`, no `string`).

- [ ] **Step 4: Implementar el cambio en Domain**

`API.Domain.Interface/IArticuloDomain.cs` — cambiar la línea:
```csharp
Task<bool> InsertarAsync(Articulo obj);
```
por:
```csharp
Task<string> InsertarAsync(Articulo obj);
```

`API.Domain.Core/ArticuloDomain.cs` — reemplazar el constructor y `InsertarAsync`:

```csharp
        private readonly IRepositorioGenerico<Articulo, string> _repoGenericoArticulo;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public ArticuloDomain(
            IRepositorioGenerico<Articulo, string> repoGenericoArticulo,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoGenericoArticulo = repoGenericoArticulo;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<string> InsertarAsync(Articulo obj)
        {
            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar artículos.");
            }

            if (serie.Manual == "S")
            {
                // Serie manual: el código lo escribe el usuario, el consecutivo automático no aplica.
                if (string.IsNullOrWhiteSpace(obj.Codigo))
                {
                    throw new Exception("El código es requerido para series manuales.");
                }
            }
            else
            {
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el artículo --
                // no al solo consultar/previsualizar el código (NumeracionDocumentoDetDomain.GenerarCodigoAsync
                // es de solo lectura).
                if (serie.SigNumero == null)
                {
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                }

                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                {
                    throw new Exception("Se agotó la numeración disponible en esta serie.");
                }

                obj.Codigo = NumeracionDocumentoDetDomain.FormatearCodigo(serie);
                serie.SigNumero = serie.SigNumero.Value + 1;
                // Sin ActualizarAsync explícito -- "serie" ya está rastreada por el mismo DbContext
                // que usa _repoGenericoArticulo; el incremento se persiste junto con el INSERT.
            }

            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            await _repoGenericoArticulo.InsertarAsync(obj);
            return obj.Codigo;
        }
```

(El resto de los métodos de la clase — `ActualizarAsync`, `EliminarAsync`, `ObtenerPorCodigoAsync`, etc. — no cambian.)

- [ ] **Step 5: Propagar el cambio de tipo por Application y Controller**

`API.Application.DTO/articulo/articulo/ArticuloCrearDTO.cs` — quitar el atributo `[Required]` de `Codigo` y hacerlo opcional:
```csharp
        public string? Codigo { get; set; }
```

`API.Application.Interface/IArticuloApplication.cs` — cambiar:
```csharp
Task<Respuesta<bool>> InsertarAsync(ArticuloCrearDTO obj);
```
por:
```csharp
Task<Respuesta<string>> InsertarAsync(ArticuloCrearDTO obj);
```

`API.Application.Main/ArticuloApplication.cs` — reemplazar `InsertarAsync`:

```csharp
        public async Task<Respuesta<string>> InsertarAsync(ArticuloCrearDTO obj)
        {
            var respuseta = new Respuesta<string>();

            try
            {
                var productoo = _mapper.Map<Articulo>(obj);

                respuseta.Dato = await _productoDomain.InsertarAsync(productoo);
                respuseta.Resultado = true;
                respuseta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuseta.Mensaje = $"{ex.Message} / {ex.InnerException}";
            }
            return respuseta;
        }
```

`API.Service.WebApi/Controllers/ArticuloController.cs` — cambiar la firma del endpoint `Post`:
```csharp
        [HttpPost]
        public async Task<ActionResult<Respuesta<string>>> Post([FromBody] ArticuloCrearDTO producto)
        {
            var insertar = await _articuloApplication.InsertarAsync(producto);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }
```

- [ ] **Step 6: Actualizar las pruebas existentes del Controller**

En `API.Service.WebApi.Tests/Controllers/ArticuloControllerTests.cs`, en `Post_DevuelveBadRequest_CuandoResultadoEsFalso` cambiar:
```csharp
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
```
por:
```csharp
            var respuesta = new Respuesta<string> { Resultado = false, Mensaje = "error" };
```

En `Post_DevuelveOk_CuandoResultadoEsExitoso` cambiar:
```csharp
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
```
por:
```csharp
            var respuesta = new Respuesta<string> { Resultado = true, Dato = "A1" };
```

- [ ] **Step 7: Ejecutar las pruebas y confirmar que pasan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~ArticuloDomainTests|FullyQualifiedName~ArticuloControllerTests"
```

Esperado: todas PASS (7 de `ArticuloDomainTests` + las existentes de `ArticuloControllerTests`).

- [ ] **Step 8: Build completo y suite completa**

```bash
dotnet build API.sln
dotnet test API.Service.WebApi.Tests
```

Esperado: build sin errores; toda la suite en verde (confirma que ningún otro archivo referenciaba `IArticuloDomain.InsertarAsync`/`IArticuloApplication.InsertarAsync` con el tipo viejo).

- [ ] **Step 9: Commit**

```bash
git add API.Domain.Interface/IArticuloDomain.cs API.Domain.Core/ArticuloDomain.cs \
        API.Application.Interface/IArticuloApplication.cs API.Application.Main/ArticuloApplication.cs \
        API.Service.WebApi/Controllers/ArticuloController.cs \
        API.Application.DTO/articulo/articulo/ArticuloCrearDTO.cs \
        API.Service.WebApi.Tests/Controllers/ArticuloControllerTests.cs \
        API.Service.WebApi.Tests/Domain/ArticuloDomainTests.cs \
        API.Service.WebApi.Tests/TestHelpers/TestAsyncQueryable.cs
git commit -m "feat: ArticuloDomain genera y avanza el codigo de la serie de forma atomica al insertar"
```

---

### Task 3: `SocioNegocioDomain` genera y avanza el código de forma atómica al insertar

**Files:**
- Modify: `API.Domain.Interface/ISocioNegocioDomain.cs`
- Modify: `API.Domain.Core/SocioNegocioDomain.cs`
- Modify: `API.Application.Interface/ISocioNegocioApplication.cs`
- Modify: `API.Application.Main/SocioNegocioApplication.cs`
- Modify: `API.Service.WebApi/Controllers/SocioNegocioController.cs`
- Modify: `API.Application.DTO/socioNegocio/SocioNegocioCrearDTO.cs`
- Modify: `API.Service.WebApi.Tests/Controllers/SocioNegocioControllerTests.cs`
- Test: `API.Service.WebApi.Tests/Domain/SocioNegocioDomainTests.cs` (crear — no existe hoy)

**Interfaces:**
- Consumes: `NumeracionDocumentoDetDomain.FormatearCodigo` (Task 1); `TestAsyncEnumerable<T>`/`AsAsyncQueryable` (Task 2, mismo namespace `API.Service.WebApi.Tests.TestHelpers`).
- Produces: `ISocioNegocioDomain.InsertarAsync(SocioNegocio obj)` ahora devuelve `Task<string>`; `ISocioNegocioApplication.InsertarAsync(SocioNegocioCrearDTO obj)` ahora devuelve `Task<Respuesta<string>>`. Lo consume el Task 4.

Mismo patrón exacto que el Task 2, cambiando `Articulo`→`SocioNegocio` y el mensaje de error de serie bloqueada.

- [ ] **Step 1: Escribir las pruebas que fallan**

Crear `API.Service.WebApi.Tests/Domain/SocioNegocioDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class SocioNegocioDomainTests
    {
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocioMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly SocioNegocioDomain _domain;

        public SocioNegocioDomainTests()
        {
            _repoSocioMock = new Mock<IRepositorioGenerico<SocioNegocio, string>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new SocioNegocioDomain(_repoSocioMock.Object, _repoNumeracionMock.Object);

            _repoSocioMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<SocioNegocio>().AsAsyncQueryable());
        }

        private static NumeracionDocumentoDet SerieAutogenerada(int? sigNumero = 5, int? finNumero = null, string bloqueado = "N") => new()
        {
            CodigoObj = "1",
            Serie = 9,
            NombreSerie = "Primario",
            SigNumero = sigNumero,
            FinNumero = finNumero,
            Bloqueado = bloqueado,
            Manual = "N",
            SubTipoDoc = "C",
            TipoSerie = "N",
            IniCadena = "CLI-",
            CantDigitos = 4,
            FinCadena = ""
        };

        [Fact]
        public async Task InsertarAsync_SerieAutogenerada_GeneraCodigoYAvanzaSigNumero()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);

            var obj = new SocioNegocio { Serie = 9 };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("CLI-0005", codigo);
            Assert.Equal("CLI-0005", obj.Codigo);
            Assert.Equal(6, serie.SigNumero);
            _repoNumeracionMock.Verify(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<NumeracionDocumentoDet>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieManual_RespetaCodigoDelCliente()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.InsertarAsync(It.IsAny<SocioNegocio>())).ReturnsAsync((SocioNegocio s) => s);

            var obj = new SocioNegocio { Serie = 9, Codigo = "MANUAL-1" };
            var codigo = await _domain.InsertarAsync(obj);

            Assert.Equal("MANUAL-1", codigo);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinCodigo_Lanza()
        {
            var serie = SerieAutogenerada();
            serie.Manual = "S";
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);

            var obj = new SocioNegocio { Serie = 9, Codigo = "" };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            var serie = SerieAutogenerada(bloqueado: "S");
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);

            var obj = new SocioNegocio { Serie = 9 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 10, finNumero: 9);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);

            var obj = new SocioNegocio { Serie = 9 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync((NumeracionDocumentoDet?)null);

            var obj = new SocioNegocio { Serie = 9 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_CodigoDuplicado_Lanza()
        {
            var serie = SerieAutogenerada(sigNumero: 5);
            _repoNumeracionMock.Setup(r => r.ObtenerAsync(9)).ReturnsAsync(serie);
            _repoSocioMock.Setup(r => r.ObtenerTodoAsync())
                .ReturnsAsync(new List<SocioNegocio> { new() { Codigo = "CLI-0005" } }.AsAsyncQueryable());

            var obj = new SocioNegocio { Serie = 9 };

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(obj));
            _repoSocioMock.Verify(r => r.InsertarAsync(It.IsAny<SocioNegocio>()), Times.Never);
        }
    }
}
```

- [ ] **Step 2: Ejecutar las pruebas y confirmar que fallan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~SocioNegocioDomainTests"
```

Esperado: error de compilación (mismo motivo que el Task 2).

- [ ] **Step 3: Implementar el cambio en Domain**

`API.Domain.Interface/ISocioNegocioDomain.cs` — cambiar:
```csharp
Task<bool> InsertarAsync(SocioNegocio obj);
```
por:
```csharp
Task<string> InsertarAsync(SocioNegocio obj);
```

`API.Domain.Core/SocioNegocioDomain.cs` — reemplazar el constructor y `InsertarAsync`:

```csharp
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocioNegocio;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoGenericoNumeracion;

        public SocioNegocioDomain(
            IRepositorioGenerico<SocioNegocio, string> repoSocioNegocio,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoGenericoNumeracion)
        {
            _repoSocioNegocio = repoSocioNegocio;
            _repoGenericoNumeracion = repoGenericoNumeracion;
        }

        #region async methods
        public async Task<string> InsertarAsync(SocioNegocio obj)
        {
            var serie = await _repoGenericoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.Bloqueado == "S")
            {
                throw new Exception("La serie está bloqueada y no se puede usar para registrar socios de negocio.");
            }

            if (serie.Manual == "S")
            {
                // Serie manual: el código lo escribe el usuario, el consecutivo automático no aplica.
                if (string.IsNullOrWhiteSpace(obj.Codigo))
                {
                    throw new Exception("El código es requerido para series manuales.");
                }
            }
            else
            {
                // Serie autogenerada: el consecutivo solo avanza aquí, al registrar el socio -- no
                // al solo consultar/previsualizar el código (NumeracionDocumentoDetDomain.GenerarCodigoAsync
                // es de solo lectura).
                if (serie.SigNumero == null)
                {
                    throw new Exception("La serie no tiene configurado el número siguiente.");
                }

                if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                {
                    throw new Exception("Se agotó la numeración disponible en esta serie.");
                }

                obj.Codigo = NumeracionDocumentoDetDomain.FormatearCodigo(serie);
                serie.SigNumero = serie.SigNumero.Value + 1;
                // Sin ActualizarAsync explícito -- "serie" ya está rastreada por el mismo DbContext
                // que usa _repoSocioNegocio; el incremento se persiste junto con el INSERT.
            }

            if (await ObtenerPorCodigoAsync(obj.Codigo) != null)
            {
                throw new Exception($"Ya existe un registro con el código: {obj.Codigo}");
            }

            await _repoSocioNegocio.InsertarAsync(obj);
            return obj.Codigo;
        }
```

- [ ] **Step 4: Propagar el cambio de tipo por Application y Controller**

`API.Application.DTO/socioNegocio/SocioNegocioCrearDTO.cs` — quitar `[Required]` de `Codigo`:
```csharp
        public string? Codigo { get; set; }
```

`API.Application.Interface/ISocioNegocioApplication.cs` — cambiar:
```csharp
Task<Respuesta<bool>> InsertarAsync(SocioNegocioCrearDTO obj);
```
por:
```csharp
Task<Respuesta<string>> InsertarAsync(SocioNegocioCrearDTO obj);
```

`API.Application.Main/SocioNegocioApplication.cs` — reemplazar `InsertarAsync`:

```csharp
        public async Task<Respuesta<string>> InsertarAsync(SocioNegocioCrearDTO obj)
        {
            var respuesta = new Respuesta<string>();
            try
            {
                var socioNegocio = _mapper.Map<SocioNegocio>(obj);
                respuesta.Dato = await _socioNegocioDomain.InsertarAsync(socioNegocio);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = $"{ex.Message} / {ex.InnerException}";
            }
            return respuesta;
        }
```

`API.Service.WebApi/Controllers/SocioNegocioController.cs` — cambiar la firma del endpoint `Crear`:
```csharp
        [HttpPost]
        public async Task<ActionResult<Respuesta<string>>> Crear([FromBody] SocioNegocioCrearDTO obj)
        {
            var insertar = await _socioNegocioApplication.InsertarAsync(obj);

            if (!insertar.Resultado)
                return BadRequest(insertar);

            return Ok(insertar);
        }
```

- [ ] **Step 5: Actualizar las pruebas existentes del Controller**

En `API.Service.WebApi.Tests/Controllers/SocioNegocioControllerTests.cs`, en `Crear_DevuelveBadRequest_CuandoResultadoEsFalso` cambiar:
```csharp
            var respuesta = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
```
por:
```csharp
            var respuesta = new Respuesta<string> { Resultado = false, Mensaje = "error" };
```

En `Crear_DevuelveOk_CuandoResultadoEsExitoso` cambiar:
```csharp
            var respuesta = new Respuesta<bool> { Resultado = true, Dato = true };
```
por:
```csharp
            var respuesta = new Respuesta<string> { Resultado = true, Dato = "SN1" };
```

- [ ] **Step 6: Ejecutar las pruebas y confirmar que pasan**

```bash
dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~SocioNegocioDomainTests|FullyQualifiedName~SocioNegocioControllerTests"
```

Esperado: todas PASS.

- [ ] **Step 7: Build completo y suite completa**

```bash
dotnet build API.sln
dotnet test API.Service.WebApi.Tests
```

Esperado: build sin errores; toda la suite en verde.

- [ ] **Step 8: Commit**

```bash
git add API.Domain.Interface/ISocioNegocioDomain.cs API.Domain.Core/SocioNegocioDomain.cs \
        API.Application.Interface/ISocioNegocioApplication.cs API.Application.Main/SocioNegocioApplication.cs \
        API.Service.WebApi/Controllers/SocioNegocioController.cs \
        API.Application.DTO/socioNegocio/SocioNegocioCrearDTO.cs \
        API.Service.WebApi.Tests/Controllers/SocioNegocioControllerTests.cs \
        API.Service.WebApi.Tests/Domain/SocioNegocioDomainTests.cs
git commit -m "feat: SocioNegocioDomain genera y avanza el codigo de la serie de forma atomica al insertar"
```

---

### Task 4: Web.ApiClient — DTOs y clientes de Articulo/SocioNegocio devuelven el código real

**Files:**
- Modify: `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`
- Modify: `Web.ApiClient/Clientes/IArticuloApiClient.cs`
- Modify: `Web.ApiClient/Clientes/ArticuloApiClient.cs`
- Modify: `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioCrearDTO.cs`
- Modify: `Web.ApiClient/Clientes/ISocioNegocioApiClient.cs`
- Modify: `Web.ApiClient/Clientes/SocioNegocioApiClient.cs`

**Interfaces:**
- Consumes: la API ahora responde al `POST` de creación de Artículo/Socio de Negocio con `Respuesta<string>` (Tasks 2 y 3) en vez de `Respuesta<bool>`.
- Produces: `IArticuloApiClient.InsertarAsync(ArticuloCrearDTO)` y `ISocioNegocioApiClient.InsertarAsync(SocioNegocioCrearDTO)` devuelven `Task<Respuesta<string>>` — el Task 5 y el Task 6 usan `respuesta.Dato` como el `Codigo` real creado.

No hay proyecto de pruebas en el repo Web (confirmado: no existe ningún `*.Tests.csproj`); la verificación de esta parte es el build y, al final del plan (Task 7), la prueba manual en navegador.

- [ ] **Step 1: `ArticuloCrearDTO` — Código opcional**

En `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, cambiar:
```csharp
        [Required(ErrorMessage = "El código es requerido.")]
        [Display(Name = "Código")]
        public string Codigo { get; set; } = null!;
```
por:
```csharp
        [Display(Name = "Código")]
        public string? Codigo { get; set; }
```

- [ ] **Step 2: `IArticuloApiClient`/`ArticuloApiClient` — `InsertarAsync` devuelve `string`**

En `Web.ApiClient/Clientes/IArticuloApiClient.cs`, cambiar:
```csharp
        Task<Respuesta<bool>> InsertarAsync(ArticuloCrearDTO dto);
```
por:
```csharp
        Task<Respuesta<string>> InsertarAsync(ArticuloCrearDTO dto);
```

En `Web.ApiClient/Clientes/ArticuloApiClient.cs`, cambiar:
```csharp
        public Task<Respuesta<bool>> InsertarAsync(ArticuloCrearDTO dto) =>
            PostAsync<bool>(Recurso, dto);
```
por:
```csharp
        public Task<Respuesta<string>> InsertarAsync(ArticuloCrearDTO dto) =>
            PostAsync<string>(Recurso, dto);
```

- [ ] **Step 3: `SocioNegocioCrearDTO` — Código opcional**

En `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioCrearDTO.cs`, cambiar:
```csharp
        [Required(ErrorMessage = "El código es requerido.")]
        [Display(Name = "Código")]
        public string Codigo { get; set; } = null!;
```
por:
```csharp
        [Display(Name = "Código")]
        public string? Codigo { get; set; }
```

- [ ] **Step 4: `ISocioNegocioApiClient`/`SocioNegocioApiClient` — `InsertarAsync` devuelve `string`**

En `Web.ApiClient/Clientes/ISocioNegocioApiClient.cs`, cambiar:
```csharp
        Task<Respuesta<bool>> InsertarAsync(SocioNegocioCrearDTO dto);
```
por:
```csharp
        Task<Respuesta<string>> InsertarAsync(SocioNegocioCrearDTO dto);
```

En `Web.ApiClient/Clientes/SocioNegocioApiClient.cs`, cambiar:
```csharp
        public Task<Respuesta<bool>> InsertarAsync(SocioNegocioCrearDTO dto) =>
            PostAsync<bool>(Recurso, dto);
```
por:
```csharp
        public Task<Respuesta<string>> InsertarAsync(SocioNegocioCrearDTO dto) =>
            PostAsync<string>(Recurso, dto);
```

- [ ] **Step 5: Build**

```bash
dotnet build Web.slnx
```

Esperado: falla en `Web.UI` (Tasks 5 y 6 corrigen esos usos) — es esperado en este punto; `Web.ApiClient` en sí debe compilar sin errores.

- [ ] **Step 6: Commit**

```bash
git add Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs Web.ApiClient/Clientes/IArticuloApiClient.cs Web.ApiClient/Clientes/ArticuloApiClient.cs \
        Web.ApiClient/Dtos/SocioNegocio/SocioNegocioCrearDTO.cs Web.ApiClient/Clientes/ISocioNegocioApiClient.cs Web.ApiClient/Clientes/SocioNegocioApiClient.cs
git commit -m "feat: ApiClient de Articulo/SocioNegocio refleja que Insertar devuelve el codigo real"
```

---

### Task 5: `ArticulosController`/`articulos.js` — usar el código real devuelto por la API

**Files:**
- Modify: `Web.UI/Controllers/ArticulosController.cs`
- Modify: `Web.UI/wwwroot/js/articulos.js`

**Interfaces:**
- Consumes: `IArticuloApiClient.InsertarAsync` devuelve `Task<Respuesta<string>>` (Task 4).

- [ ] **Step 1: `ArticulosController.Crear` — devolver el código real en la respuesta JSON**

En `Web.UI/Controllers/ArticulosController.cs`, cambiar:
```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromBody] ArticuloCrearDTO dto)
        {
            var respuesta = await _articulos.InsertarAsync(dto);
            return Json(respuesta);
        }
```
por:
```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromBody] ArticuloCrearDTO dto)
        {
            var respuesta = await _articulos.InsertarAsync(dto);
            // "respuesta.Dato" ya es el código real (para series no manuales, el que calculó la
            // API al momento de registrar -- no el de la vista previa mostrada antes de guardar).
            return Json(new { respuesta.Resultado, respuesta.Mensaje, codigo = respuesta.Dato });
        }
```

- [ ] **Step 2: `articulos.js` — dejar de enviar el código de la vista previa y usar el real**

En `Web.UI/wwwroot/js/articulos.js`, dentro de `$(document).on('click', '#btnGuardarArticuloPagina', ...)`, cambiar:
```javascript
        let codigoGenerado = null;
        if (!esSerieManualArticulo()) {
            const respuestaSerie = await App.enviarJson(`/Articulos/GenerarCodigoSerie?serie=${serieSeleccionada}`, 'POST', {});
            if (!respuestaSerie.resultado) {
                App.mostrarError(respuestaSerie.mensaje);
                return;
            }
            codigoGenerado = respuestaSerie.dato;
        }

        const datos = App.recolectarFormulario('#formArticuloCrear');
        // El campo Código queda deshabilitado cuando el código se genera automáticamente, así que
        // no viaja en el serializeArray del formulario -- hay que agregarlo a mano.
        if (codigoGenerado !== null) {
            datos.Codigo = codigoGenerado;
        }
        datos.Serie = serieSeleccionada;

        const respuesta = await App.enviarJson('/Articulos/Crear', 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }

        await App.mostrarExito('Artículo creado correctamente.');
        window.location.href = '/Articulos';
```
por:
```javascript
        // La vista previa del campo Código (deshabilitado cuando la serie no es manual) es solo
        // cosmética -- el código real lo calcula y asigna la API al momento de guardar, así que ya
        // no se envía nada calculado aquí para series no manuales.
        const datos = App.recolectarFormulario('#formArticuloCrear');
        datos.Serie = serieSeleccionada;

        const respuesta = await App.enviarJson('/Articulos/Crear', 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }

        await App.mostrarExito(`Artículo "${respuesta.codigo}" creado correctamente.`);
        window.location.href = '/Articulos';
```

- [ ] **Step 3: Quitar la acción `GenerarCodigoSerie`, ya sin ningún llamador**

Tras el Step 2, ningún JS del repo Web llama a `/Articulos/GenerarCodigoSerie`. En `Web.UI/Controllers/ArticulosController.cs`, eliminar por completo el método:
```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarCodigoSerie(int serie)
        {
            var respuesta = await _series.GenerarCodigoAsync(serie);
            return Json(respuesta);
        }
```

(El campo `_series`/`INumeracionDocumentoDetApiClient` sigue usándose en `Crear`/`FormularioEditar` para cargar las series y mostrar el nombre de la serie actual -- no se toca esa parte.)

- [ ] **Step 4: Build**

```bash
dotnet build Web.slnx
```

Esperado: sin errores.

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/ArticulosController.cs Web.UI/wwwroot/js/articulos.js
git commit -m "feat: alta de Articulo usa el codigo real devuelto por la API en vez de la vista previa"
```

---

### Task 6: `SociosNegocioController`/`sociosNegocio.js` — usar el código real, incluida la creación de direcciones

**Files:**
- Modify: `Web.UI/Controllers/SociosNegocioController.cs`
- Modify: `Web.UI/wwwroot/js/sociosNegocio.js`

**Interfaces:**
- Consumes: `ISocioNegocioApiClient.InsertarAsync` devuelve `Task<Respuesta<string>>` (Task 4).

- [ ] **Step 1: `SociosNegocioController.Crear` — devolver el código real en la respuesta JSON**

En `Web.UI/Controllers/SociosNegocioController.cs`, cambiar:
```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromBody] SocioNegocioCrearDTO dto)
        {
            var respuesta = await _socios.InsertarAsync(dto);
            return Json(respuesta);
        }
```
por:
```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([FromBody] SocioNegocioCrearDTO dto)
        {
            var respuesta = await _socios.InsertarAsync(dto);
            // "respuesta.Dato" ya es el código real (para series no manuales, el que calculó la
            // API al momento de registrar -- no el de la vista previa mostrada antes de guardar).
            // El JS lo necesita para crear las direcciones acumuladas con el CodigoSn correcto.
            return Json(new { respuesta.Resultado, respuesta.Mensaje, codigo = respuesta.Dato });
        }
```

- [ ] **Step 2: `sociosNegocio.js` — dejar de enviar el código de la vista previa y usar el real para las direcciones**

En `Web.UI/wwwroot/js/sociosNegocio.js`, dentro de `$(document).on('click', '#btnGuardarSocioNegocioPagina', ...)`, cambiar:
```javascript
        let codigoGenerado = null;
        if (!esSerieManualSocioNegocio()) {
            const respuestaSerie = await App.enviarJson(`/SociosNegocio/GenerarCodigoSerie?serie=${serieSeleccionada}`, 'POST', {});
            if (!respuestaSerie.resultado) {
                App.mostrarError(respuestaSerie.mensaje);
                return;
            }
            codigoGenerado = respuestaSerie.dato;
        }
```
por (se elimina el bloque completo -- la generación previa ya no aporta el código real, solo el `#Codigo` deshabilitado sigue mostrando la vista previa cosmética que ya poblaba `actualizarCodigoSegunSerie`):
```javascript
        // La vista previa del campo Código (deshabilitado cuando la serie no es manual) es solo
        // cosmética -- el código real lo calcula y asigna la API al momento de guardar.
```

Luego, más abajo, cambiar:
```javascript
        const datos = App.recolectarFormulario('#formSocioNegocioCrear');
        // El campo Código queda deshabilitado cuando el código se genera automáticamente, así que
        // no viaja en el serializeArray del formulario -- hay que agregarlo a mano.
        if (codigoGenerado !== null) {
            datos.Codigo = codigoGenerado;
        }
        datos.Serie = serieSeleccionada;

        const respuesta = await App.enviarJson('/SociosNegocio/Crear', 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }

        const codigoCreado = datos.Codigo;
```
por:
```javascript
        const datos = App.recolectarFormulario('#formSocioNegocioCrear');
        datos.Serie = serieSeleccionada;

        const respuesta = await App.enviarJson('/SociosNegocio/Crear', 'POST', datos);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }

        // El código real lo devuelve la API en la respuesta -- para series no manuales puede no
        // coincidir con la vista previa que se mostraba en el campo deshabilitado.
        const codigoCreado = respuesta.codigo;
```

(El resto del bloque que usa `codigoCreado` para crear las direcciones en secuencia, líneas debajo de este punto, no cambia: sigue funcionando igual porque `codigoCreado` ahora contiene el valor correcto.)

- [ ] **Step 3: Quitar la acción `GenerarCodigoSerie`, ya sin ningún llamador**

Tras el Step 2, ningún JS del repo Web llama a `/SociosNegocio/GenerarCodigoSerie`. En `Web.UI/Controllers/SociosNegocioController.cs`, eliminar por completo el método:
```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarCodigoSerie(int serie)
        {
            var respuesta = await _series.GenerarCodigoAsync(serie);
            return Json(respuesta);
        }
```

(El campo `_series`/`INumeracionDocumentoDetApiClient` sigue usándose en `Crear`/`FormularioEditar`/`CargarSeriesAsync` para cargar las series y mostrar el nombre de la serie actual -- no se toca esa parte.)

- [ ] **Step 4: Build**

```bash
dotnet build Web.slnx
```

Esperado: sin errores.

- [ ] **Step 5: Commit**

```bash
git add Web.UI/Controllers/SociosNegocioController.cs Web.UI/wwwroot/js/sociosNegocio.js
git commit -m "feat: alta de SocioNegocio usa el codigo real devuelto por la API, incluidas las direcciones acumuladas"
```

---

### Task 7: Verificación final

**Files:** ninguno (solo verificación).

- [ ] **Step 1: Suite completa de API**

```bash
dotnet test API.Service.WebApi.Tests
```

Esperado: 100% PASS (incluye las pruebas nuevas de los Tasks 1-3 y todas las preexistentes).

- [ ] **Step 2: Build completo de ambos repos**

```bash
dotnet build API.sln
dotnet build Web.slnx
```

Esperado: 0 errores en ambos.

- [ ] **Step 3: Verificación manual en navegador**

Con la API y Web corriendo (build aislado en puertos que no choquen con la sesión de Visual Studio del usuario, igual que en verificaciones anteriores de esta sesión), con sesión iniciada como `admin`/`Admin123!`:

1. Ir a `POST api/NumeracionDocumentoDet/GenerarCodigo/{serie}` (una serie autogenerada real) dos veces seguidas sin crear ningún documento entre medio -- confirmar que ambas llamadas devuelven el mismo código.
2. Crear un Artículo nuevo con una serie autogenerada -- confirmar que el mensaje de éxito muestra el código real y que el artículo queda guardado con ese código exacto en el listado.
3. Repetir el paso 1 para esa misma serie -- confirmar que ahora el código devuelto avanzó en uno respecto al que se usó en el paso 2 (el consecutivo sí avanzó porque el artículo se registró).
4. Crear un Socio de Negocio nuevo con serie autogenerada y 2 direcciones acumuladas en el formulario -- confirmar que el socio se crea con el código real y que ambas direcciones quedan guardadas con el `CodigoSn` correcto (no vacío, no el de una vista previa vieja).
5. Crear un Artículo y un Socio de Negocio con una serie **manual** -- confirmar que el código escrito a mano se respeta tal cual.

- [ ] **Step 4: Recordatorio final**

Avisar al usuario que reinicie las sesiones de depuración de Visual Studio (API y Web.UI) para recoger los cambios de esta sesión, y ofrecer la skill `finishing-a-development-branch` para decidir qué hacer con las ramas de ambos repos.
