# Stock mínimo, máximo y bloqueo por artículo y almacén Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que cada artículo tenga, por almacén, stock mínimo, stock máximo y una marca de bloqueo (tabla de la ficha como en SAP B1), y que la API rechace en el servidor cualquier movimiento nuevo de un artículo en un almacén donde está bloqueado.

**Architecture:** Tabla nueva `ArticuloAlmacen(CodArticulo, CodAlmacen, Minimo, Maximo, Bloqueado)` independiente de `ExistenciaArticulo`. Un dominio `IArticuloAlmacenDomain` calcula las filas de la ficha (stock, disponible, nivel necesario, estado) y guarda la configuración reemplazándola completa dentro de `IEjecutorTransaccion`. El bloqueo se hace cumplir en el único punto por el que pasan todos los movimientos de stock: `InventarioAsientoService.AplicarMovimientoAsync` (las reversas nunca se bloquean). La Web agrega la sección «Datos de inventario» a la ficha y al alta, columnas en Existencias y el aviso por almacén en el Home.

**Tech Stack:** .NET 7 API (N-capas, EF Core 7 DB-first, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server (solo bases temporales), PowerShell/node (puppeteer) para verificación.

**Spec:** `docs/superpowers/specs/2026-11-06-stock-por-almacen-design.md` (repo API, commit `b8ff607`).

**Cómo usar este plan (importante):** hay módulos verificados que son la **plantilla autoritativa** (Fase C límite de crédito y Fase B para controladores, DTOs, pruebas de permisos por reflexión y smoke; `ExistenciaArticulo` para entidad con clave compuesta; `PermisoDomain.GuardarMatrizUsuarioAsync` para reemplazo atómico con `IEjecutorTransaccion`). Cada tarea nombra su plantilla, los archivos exactos, el código clave y las reglas. Quien implementa abre la plantilla real del repositorio y la adapta. Los mensajes indicados se verifican literalmente en pruebas.

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`, HEAD `b8ff607` o posterior. `git status --short` muestra solo `API.Service.WebApi/appsettings.json` modificado (NUNCA se commitea) y dos archivos **sin seguimiento del usuario**: `sql/crear-api-db-completo.sql` y `sql/crear-api-db-completo - copia.sql` (NO se stagean ni se commitean; el segundo ni se toca; el primero sí se actualiza en disco con el mismo bloque SQL de la Tarea 1, solo en disco). Línea base de pruebas: **2596** pasando, 0 fallando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `bcd145c` o posterior. `git status --short` muestra **dos archivos modificados propios de una petición anterior del usuario** (quitar «-- Seleccione --» de los selects): `Web.UI/Views/Articulos/Crear.cshtml` y `Web.UI/Views/Articulos/_Form.cshtml`. La Tarea 5 los commitea primero, por ruta, en su propio commit.
- Build/test (PowerShell; scratchpad de la sesión: `C:\Users\migue\AppData\Local\Temp\claude\9f8881082ead4f04\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad`, si cambia usar el que indique el entorno):
  - API: `dotnet build API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj` y `dotnet test API.Service.WebApi.Tests --nologo -v q` (`--filter "FullyQualifiedName~Clase"` para una clase).
  - Web: `dotnet build Web.slnx` (si `bin` está bloqueado por un Web.UI en ejecución: `-p:BaseOutputPath="<scratchpad>\webbuild\"`; los errores MSB3021/MSB3027 de copia por bloqueo no son errores de compilación). La Web no tiene proyecto de pruebas: se verifica con build y recorrido en navegador.
- SQL de pruebas: SOLO bases temporales (`API_DB_EVID<n>` vía `evid-up.ps1` / `evid-down.ps1` del scratchpad, que reemplazan `API_DB_TEST` en `sql/crear-api-db-test-completo.sql`); **jamás** `API_DB_TEST`. La contraseña de `sa` se extrae de `API.Service.WebApi\appsettings.json` a una variable de proceso, sin imprimirla ni escribirla. Las bases temporales se eliminan al terminar. Scripts existentes en el scratchpad para usar de plantilla: `smoke-limite-credito.ps1` (138), `smoke-pagos-final.ps1` (54), `smoke-pagos-efectuados.ps1` (51), `smoke-vencimientos.ps1` (122); puppeteer en `<scratchpad>\pupmod`.

## Decisiones del plan que ajustan el spec (verificadas en el código)

1. **Controlador nuevo, rutas del spec.** Los endpoints viven en `ArticuloAlmacenController` con `[Route("api/Articulo")]` (no se toca `ArticuloController`, cuyo constructor construyen sus pruebas): `GET {codigo}/Almacenes`, `PUT {codigo}/Almacenes` y `GET Almacenes/BajoMinimo` (este último alimenta el Home; dos segmentos literales, sin choque con `{codigo}/Almacenes` ni con los demás).
2. **Cero es «sin valor».** Al guardar, `Minimo = 0` y `Maximo = 0` se normalizan a `NULL` (como SAP B1: 0 = sin nivel). El `CHECK` de la tabla sí admite 0. Una fila sin mínimo, sin máximo y sin bloqueo se elimina. `StockAlmacenCalculadora` también ignora 0 (`> 0`) por si hay datos cargados a mano.
3. **Reemplazo atómico** con `IEjecutorTransaccion.EjecutarAsync` (patrón `PermisoDomain`): dentro de la transacción se actualizan con `ActualizarAsync`/eliminan con `EliminarAsync` las filas existentes y se agregan las nuevas con `AgregarSinGuardarAsync`; el ejecutor hace el `SaveChanges` final y el commit.
4. **Sin AutoMapper para esta fase.** El dominio devuelve `record`s (`ArticuloAlmacenFila`, `AlmacenBajoMinimo`) y la aplicación proyecta a DTOs a mano. Solo `ExistenciaArticuloDTO` (mapeado con AutoMapper) gana campos: se ignoran en el mapa y los llena `ExistenciaApplication` (lección de fases anteriores).
5. **Constructor del asiento.** `InventarioAsientoService` gana un sexto parámetro `IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>`; la única construcción manual está en `InventarioAsientoServiceTests` y se actualiza (el mock suelto devuelve `null` = sin configuración).
6. **Alta de artículo.** `PUT .../Almacenes` exige `Articulos`/Actualizar. En la página de alta la sección solo se muestra si el usuario tiene también `Articulos`/Actualizar; el modal de edición ya exige Actualizar, así que no hace falta una variante de solo lectura.
7. **Comprometido y Pedido** de `ExistenciaArticulo` no los escribe ningún documento (verificado): se muestran tal cual (0 hoy). No se cambia.

## Global Constraints

Cada tarea las incluye implícitamente.

- **Datos:** tabla `ArticuloAlmacen`: `CodArticulo nvarchar(15) NOT NULL` FK `Articulo(Codigo)`, `CodAlmacen nvarchar(8) NOT NULL` FK `Almacen(Codigo)`, `Minimo decimal(19,6) NULL`, `Maximo decimal(19,6) NULL`, `Bloqueado nvarchar(1) NOT NULL DEFAULT 'N'`; PK `(CodArticulo, CodAlmacen)`; `CHECK Bloqueado IN ('S','N')`, `CHECK (Minimo IS NULL OR Minimo >= 0)`, `CHECK (Maximo IS NULL OR Maximo >= 0)`, `CHECK (Minimo IS NULL OR Maximo IS NULL OR Maximo >= Minimo)`. Sin migración de datos; `Articulo.Minimo/Maximo` no cambian.
- **Fila de la ficha** (`GET api/Articulo/{codigo}/Almacenes`): `codAlmacen`, `nombreAlmacen`, `bloqueado`, `enStock` (= `ExistenciaArticulo.Disponible`, 0 sin fila), `comprometido`, `pedido`, `disponible = enStock − comprometido + pedido`, `minimo`, `maximo`, `nivelNecesario = max(0, maximo − disponible)` si hay máximo (> 0) y si no nulo, `estado`: `BAJO_MINIMO` si mínimo > 0 y `enStock < minimo`; `SOBRE_MAXIMO` si máximo > 0 y `enStock > maximo`; si no `OK`. Filas: todos los almacenes activos (`Almacen.Activo = 'S'`) más los inactivos con configuración o existencias del artículo; orden por código de almacén. Artículo inexistente → 404.
- **Mensajes (verificados literalmente en pruebas):** «El artículo {cod} no existe.» (ya existe), «El almacén {cod} no existe.» (ya existe), «El almacén {cod} está repetido en la lista.», «El stock mínimo del almacén {cod} no puede ser negativo.», «El stock máximo del almacén {cod} no puede ser negativo.», «El stock mínimo del almacén {cod} admite como máximo 6 decimales.», «El stock máximo del almacén {cod} admite como máximo 6 decimales.», «El stock máximo del almacén {cod} no puede ser menor que el mínimo.», «El artículo {codArticulo} está bloqueado en el almacén {codAlmacen}.».
- **Bloqueo:** se evalúa en `AplicarMovimientoAsync` después de comprobar que el artículo es de inventario y que el almacén existe, y solo si `movReversaDe == null`; `ArticuloAlmacenBloqueadoException` → 400 por el manejo existente de la capa de aplicación (`Respuesta` con el mensaje). Un documento rechazado no consume consecutivo. `RevertirAsync`/`RevertirLineaAsync` nunca se bloquean.
- **Permisos:** `GET .../Almacenes` y `GET Almacenes/BajoMinimo`: `Articulos`/Ver. `PUT .../Almacenes`: `Articulos`/Actualizar. Sin módulos de permisos nuevos y sin cambios en `PUT api/Articulo/{codigo}`.
- **Avisos:** solo avisan, nunca bloquean. Fila roja = `BAJO_MINIMO`, amarilla = `SOBRE_MAXIMO`.
- **Compatibilidad:** artículos sin configuración se comportan exactamente como hoy; ninguna prueba existente cambia de resultado salvo la línea del constructor del asiento.
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` y los archivos `sql/crear-api-db-completo*.sql` NUNCA se commitean. **Sin `git push`.**
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (`git ls-files --eol`), archivos nuevos en LF, sin churn. Comentarios y mensajes en español, estilo del repo. Todo texto dinámico en la Web se escapa.
- **Verificación al cerrar cada tarea de la API:** `dotnet build` sin errores y pruebas de la tarea en verde; suite completa donde se indique.

## File Structure

**API:** `sql/2026-11-06-stock-por-almacen.sql`; entidad `ArticuloAlmacen`, `ApiDbTestContext`, `ArticuloAlmacenRepositorio`, `Startup`; `API.Domain.Interface/{IArticuloAlmacenDomain.cs}` (con los `record`s); `API.Domain.Core/{StockAlmacenCalculadora,ArticuloAlmacenDomain}.cs` y `Inventario/ExcepcionesInventario.cs`, `InventarioAsientoService.cs`; `API.Application.DTO/articulo/almacen/*`, `IArticuloAlmacenApplication`, `ArticuloAlmacenApplication`, `ExistenciaApplication`, `ExistenciaArticuloDTO`, `PerfilMapeo`; `ArticuloAlmacenController`.

**Web:** `Web.ApiClient` (DTOs, `IArticuloApiClient`/`ArticuloApiClient` o cliente nuevo, `ExistenciaArticuloDTO`); `ArticulosController`, `ExistenciasController`, `HomeController` y `DashboardViewModel`; vistas `Articulos/_InventarioAlmacenes.cshtml` (parcial), `_Form.cshtml`, `Crear.cshtml`, `Existencias/Index.cshtml`, `Home/Index.cshtml`; JS `articulo-almacenes.js` (nuevo, módulo compartido), `articulos.js`, `existencias.js`.

---

## Fase 1 — API

### Task 1: Script SQL, entidad y repositorio

**Plantilla:** `ExistenciaArticulo` (entidad con clave compuesta, `ApiDbTestContext` línea ~1542, `ExistenciaArticuloRepositorio`, registro en `Startup.cs:314`), commit `da9f889` (script SQL idempotente + bloque duplicado + prueba de modelo `ModeloLimiteCreditoTests`).

**Precondición:** `git log --oneline -1` es `b8ff607` o posterior; existen `API.Domain.Entity/Models/ExistenciaArticulo.cs` y `API.Service.WebApi.Tests/Domain/ModeloLimiteCreditoTests.cs`.

**Files:** Create `sql/2026-11-06-stock-por-almacen.sql`, `API.Domain.Entity/Models/ArticuloAlmacen.cs`, `API.Infraestructure.Repository/ArticuloAlmacenRepositorio.cs`, `API.Service.WebApi.Tests/Domain/ModeloStockAlmacenTests.cs`; Modify `API.Domain.Entity/Models/ApiDbTestContext.cs`, `API.Service.WebApi/Startup.cs` (una línea de DI junto a la de `ExistenciaArticulo`), `sql/crear-api-db-test-completo.sql` (mismo bloque al final; se commitea), `sql/crear-api-db-completo.sql` (mismo bloque al final; solo en disco, NO se stagea).

**Interfaces — Produces:** `ArticuloAlmacen { string CodArticulo; string CodAlmacen; decimal? Minimo; decimal? Maximo; string Bloqueado = "N"; Articulo CodArticuloNavigation; Almacen CodAlmacenNavigation }`; `IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>` registrado (transient, como el de `ExistenciaArticulo`).

- [ ] **Step 1: Prueba de modelo (falla).** `ModeloStockAlmacenTests` (patrón `ModeloLimiteCreditoTests`): tabla `ArticuloAlmacen`, clave `(CodArticulo, CodAlmacen)`, longitudes (`CodArticulo` 15, `CodAlmacen` 8, `Bloqueado` 1), `Minimo`/`Maximo` `decimal(19,6)` nulables, default `'N'` de `Bloqueado`, FK a `Articulo` y a `Almacen` sin colecciones inversas.
- [ ] **Step 2: Script SQL idempotente** `sql/2026-11-06-stock-por-almacen.sql` con este contenido (el mismo bloque, sin la guarda `THROW`, va al final de los dos scripts completos):

```sql
-- Stock minimo, maximo y bloqueo por articulo y almacen
IF OBJECT_ID('dbo.Articulo', 'U') IS NULL OR OBJECT_ID('dbo.Almacen', 'U') IS NULL
    THROW 50000, 'Faltan las tablas Articulo y Almacen: cree primero la base con el script completo.', 1;

IF OBJECT_ID('dbo.ArticuloAlmacen', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArticuloAlmacen (
        CodArticulo nvarchar(15)  NOT NULL,
        CodAlmacen  nvarchar(8)   NOT NULL,
        Minimo      decimal(19,6) NULL,
        Maximo      decimal(19,6) NULL,
        Bloqueado   nvarchar(1)   NOT NULL CONSTRAINT DF_ArticuloAlmacen_Bloqueado DEFAULT ('N'),
        CONSTRAINT pk_articulo_almacen PRIMARY KEY (CodArticulo, CodAlmacen),
        CONSTRAINT fk_articulo_almacen_articulo FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_articulo_almacen_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo),
        CONSTRAINT CK_ArticuloAlmacen_Bloqueado CHECK (Bloqueado IN ('S','N')),
        CONSTRAINT CK_ArticuloAlmacen_Minimo CHECK (Minimo IS NULL OR Minimo >= 0),
        CONSTRAINT CK_ArticuloAlmacen_Maximo CHECK (Maximo IS NULL OR Maximo >= 0),
        CONSTRAINT CK_ArticuloAlmacen_MaxMin CHECK (Minimo IS NULL OR Maximo IS NULL OR Maximo >= Minimo)
    );
END
GO
PRINT 'Stock por almacen aplicado.';
SELECT name AS Tabla FROM sys.tables WHERE name = 'ArticuloAlmacen';
```

  Verifica el tipo y longitud reales de `Articulo.Codigo` y `Almacen.Codigo` en `sql/crear-api-db-test-completo.sql` antes de fijar las columnas.
- [ ] **Step 3: Entidad, contexto, repositorio y DI.** Entidad como en «Produces» (parcial, `Bloqueado` por defecto `"N"`); bloque `modelBuilder.Entity<ArticuloAlmacen>` copiado de `ExistenciaArticulo` (clave compuesta, `HasMaxLength`, `HasColumnType("decimal(19, 6)")`, default `'N'` con `HasDefaultValueSql("('N')")`, `HasOne(...).WithMany()` sin colección inversa, nombres de FK del script) y `DbSet<ArticuloAlmacen> ArticuloAlmacens`; `ArticuloAlmacenRepositorio : RepositorioGenericoEfCore<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>` con `ObtenerAsync` por `FindAsync(id.CodArticulo, id.CodAlmacen)` (clon de `ExistenciaArticuloRepositorio`); una línea `services.AddTransient<IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>, ArticuloAlmacenRepositorio>();`.
- [ ] **Step 4: Verificar.** Build y `ModeloStockAlmacenTests` en verde; modelos previos sin regresión. En una base temporal creada con `crear-api-db-test-completo.sql` **sin** el bloque nuevo (o con la base de la sesión anterior): ejecutar el script individual **dos veces** (idempotente), comprobar tabla, PK, FKs y los cuatro `CHECK` (insertar `Minimo = -1`, `Maximo < Minimo`, `Bloqueado = 'X'` falla; `Minimo = 0`, `Maximo = 0` pasa); y que una base creada con el script completo ya incluye la tabla. Eliminar la base.
- [ ] **Step 5: Commit** `feat(stock-almacen): script SQL, entidad y repositorio de configuracion de stock por articulo y almacen` (los archivos de la lista, por ruta; el script completo de TEST sí, el `crear-api-db-completo.sql` no).

### Task 2: Calculadora y dominio `ArticuloAlmacenDomain`

**Plantilla:** `PermisoDomain` (reemplazo atómico con `_tx.EjecutarAsync`), `ExistenciaDomain` (consultas) y sus pruebas (`PermisoDomainTests`, `InventarioAsientoServiceTests` para el estilo de mocks).

**Precondición:** Task 1 hecha.

**Files:** Create `API.Domain.Interface/IArticuloAlmacenDomain.cs`, `API.Domain.Core/StockAlmacenCalculadora.cs`, `API.Domain.Core/ArticuloAlmacenDomain.cs`, `API.Service.WebApi.Tests/Domain/StockAlmacenCalculadoraTests.cs`, `API.Service.WebApi.Tests/Domain/ArticuloAlmacenDomainTests.cs`; Modify `API.Service.WebApi/Startup.cs` (DI `IArticuloAlmacenDomain` → `ArticuloAlmacenDomain`, junto a `IExistenciaDomain`).

**Interfaces — Produces (en `API.Domain.Interface`):**

```csharp
public sealed record ArticuloAlmacenFila(
    string CodAlmacen, string NombreAlmacen, bool Bloqueado,
    decimal EnStock, decimal Comprometido, decimal Pedido, decimal Disponible,
    decimal? Minimo, decimal? Maximo, decimal? NivelNecesario, string Estado);

public sealed record ArticuloAlmacenConfig(string CodAlmacen, decimal? Minimo, decimal? Maximo, bool Bloqueado);

public sealed record AlmacenBajoMinimo(
    string CodArticulo, string NombreArticulo, string CodAlmacen, string NombreAlmacen,
    decimal EnStock, decimal Minimo);

public interface IArticuloAlmacenDomain
{
    /// <summary>Filas de la ficha. Lanza ArticuloNoExisteException si el artículo no existe.</summary>
    Task<IReadOnlyList<ArticuloAlmacenFila>> ObtenerFilasAsync(string codArticulo);
    /// <summary>Reemplaza TODA la configuración del artículo (atómico). Valida y lanza Exception con el mensaje del contrato.</summary>
    Task GuardarAsync(string codArticulo, IReadOnlyList<ArticuloAlmacenConfig> filas);
    /// <summary>Configuración existente por (artículo, almacén), filtrable; para enriquecer Existencias.</summary>
    Task<IReadOnlyDictionary<(string CodArticulo, string CodAlmacen), ArticuloAlmacenConfig>> ObtenerConfiguracionAsync(string? articulo, string? almacen);
    /// <summary>Artículos de inventario activos bajo su mínimo por almacén (mínimo > 0, en stock < mínimo).</summary>
    Task<IReadOnlyList<AlmacenBajoMinimo>> ObtenerBajoMinimoAsync();
}
```

`StockAlmacenCalculadora` (estática, en `API.Domain.Core`):

```csharp
public static class StockAlmacenCalculadora
{
    public const string Ok = "OK";
    public const string BajoMinimo = "BAJO_MINIMO";
    public const string SobreMaximo = "SOBRE_MAXIMO";

    public static decimal Disponible(decimal enStock, decimal comprometido, decimal pedido) =>
        enStock - comprometido + pedido;

    public static decimal? NivelNecesario(decimal? maximo, decimal disponible) =>
        maximo is > 0 ? Math.Max(0m, maximo.Value - disponible) : null;

    public static string Estado(decimal enStock, decimal? minimo, decimal? maximo) =>
        minimo is > 0 && enStock < minimo.Value ? BajoMinimo
        : maximo is > 0 && enStock > maximo.Value ? SobreMaximo
        : Ok;
}
```

- [ ] **Step 1: Pruebas de la calculadora (fallan).** Disponible con y sin comprometido/pedido; `NivelNecesario` sin máximo (null), con máximo 0 (null), `disponible` igual al máximo (0), sobre el máximo (0, nunca negativo), por debajo (diferencia exacta con 6 decimales); `Estado`: sin mínimo/máximo → `OK`; `enStock == minimo` → `OK`; `enStock < minimo` → `BAJO_MINIMO`; mínimo 0 nunca marca bajo; `enStock == maximo` → `OK`; `enStock > maximo` → `SOBRE_MAXIMO`; ambos configurados con stock dentro del rango → `OK`.
- [ ] **Step 2: Pruebas del dominio (fallan)** con mocks de `IRepositorioGenerico<ArticuloAlmacen, ...>`, `<Articulo, string>`, `<Almacen, string>`, `<ExistenciaArticulo, ...>` y `IEjecutorTransaccion` (que ejecuta la operación): (a) `ObtenerFilasAsync` de artículo inexistente → `ArticuloNoExisteException` («El artículo X no existe.»); (b) lista todos los almacenes activos aunque no haya existencias ni configuración (stock 0, `OK`); (c) incluye un almacén inactivo solo si tiene configuración o existencias; orden por código; (d) cálculo de `disponible`, `nivelNecesario` y `estado` por fila a partir de existencias y configuración; (e) `GuardarAsync` crea filas nuevas con `AgregarSinGuardarAsync`, actualiza las existentes con `ActualizarAsync`, elimina con `EliminarAsync` las que ya no vienen o quedan vacías; (f) fila con mínimo 0, máximo 0 y sin bloqueo **no se guarda**; mínimo 0/máximo 0 con bloqueo se guarda como `NULL`/`NULL`/`'S'`; (g) cada validación con su mensaje literal: almacén inexistente, repetido, mínimo negativo, máximo negativo, más de 6 decimales (mínimo y máximo), `maximo < minimo`; (h) ante cualquier error no se llama a `AgregarSinGuardarAsync`/`ActualizarAsync`/`EliminarAsync` (se valida todo **antes** de tocar nada); (i) `ObtenerBajoMinimoAsync`: solo artículos `ArticuloInventario = 'S'` y `Activo = 'S'` con mínimo > 0, almacén activo, `enStock < minimo` (sin fila de existencia cuenta como 0); un artículo bloqueado en el almacén igual aparece (el bloqueo no afecta al aviso); (j) `ObtenerConfiguracionAsync` filtra por artículo y almacén.
- [ ] **Step 3: Implementar** la calculadora, el dominio y la línea de DI. `GuardarAsync`: normaliza (0 → null), valida todo, luego `_tx.EjecutarAsync(async () => { ...; return true; })` con el patrón de `PermisoDomain.GuardarMatrizUsuarioAsync`. Las consultas leen con `ObtenerTodoAsync()` + `Where` + `ToListAsync()` (cargas agrupadas: una consulta por tabla, no una por almacén).
- [ ] **Step 4: Pruebas en verde y suite completa** (línea base 2596 + las nuevas, 0 fallando).
- [ ] **Step 5: Commit** `feat(stock-almacen): dominio de configuracion de stock por almacen y calculo de avisos`.

### Task 3: Bloqueo en el asiento de inventario

**Plantilla:** `InventarioAsientoService.AplicarMovimientoAsync` y `StockInsuficienteException`; pruebas en `InventarioAsientoServiceTests`.

**Precondición:** Task 1 hecha (la entidad y el repositorio existen).

**Files:** Modify `API.Domain.Core/Inventario/ExcepcionesInventario.cs`, `API.Domain.Core/InventarioAsientoService.cs`, `API.Service.WebApi.Tests/Domain/InventarioAsientoServiceTests.cs`.

**Interfaces — Produces:** `ArticuloAlmacenBloqueadoException(string codArticulo, string codAlmacen)` en `API.Domain.Core.Inventario`, mensaje «El artículo {codArticulo} está bloqueado en el almacén {codAlmacen}.»; el constructor de `InventarioAsientoService` recibe como **sexto** parámetro `IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)> repoArticuloAlmacen`.

- [ ] **Step 1: Pruebas (fallan).** En `InventarioAsientoServiceTests`: agregar el mock `_repoArtAlm` y pasarlo como sexto argumento en el constructor del `setup` (sin configuración devuelve `null`); luego: (a) entrada (cantidad > 0) con `Bloqueado = 'S'` → `ArticuloAlmacenBloqueadoException` con el mensaje literal y **sin** llamar `AgregarSinGuardarAsync` de movimiento ni existencia; (b) salida con `Bloqueado = 'S'` → igual; (c) `Bloqueado = 'N'` o sin configuración → asienta normalmente; (d) bloqueado en otro almacén del mismo artículo no afecta; (e) **reversa** (`RevertirAsync` de un documento previo con el artículo bloqueado ahora) pasa y asienta la reversa; (f) `RevertirLineaAsync` ídem; (g) artículo no de inventario (`ArticuloInventario = 'N'`) con bloqueo configurado se ignora como hoy; (h) un lote de movimientos donde el segundo está bloqueado lanza al llegar a él (el primero ya quedó adjunto en memoria: no es persistido porque el caller aborta; documentar con la prueba que la excepción se propaga).
- [ ] **Step 2: Implementar.** En `AplicarMovimientoAsync`, justo después de la comprobación `AlmacenNoExisteException`:

```csharp
// Bloqueo del artículo en este almacén: solo aplica a movimientos nuevos; una reversa nunca se bloquea.
if (movReversaDe is null)
{
    var configuracion = await _repoArticuloAlmacen.ObtenerAsync((codArticulo, codAlmacen));
    if (configuracion?.Bloqueado == "S")
        throw new ArticuloAlmacenBloqueadoException(codArticulo, codAlmacen);
}
```

  más el campo, el parámetro del constructor y la excepción nueva.
- [ ] **Step 3: Pruebas en verde y suite completa** (el cambio de constructor no debe romper nada más: `grep` confirma una sola construcción manual).
- [ ] **Step 4: Commit** `feat(stock-almacen): el asiento de inventario rechaza movimientos de articulos bloqueados en un almacen`.

### Task 4: Aplicación, DTOs, controlador y enriquecimiento de Existencias

**Plantilla:** `CreditoClienteApplication` + `CreditoClienteController` (+ `CreditoClienteControllerTests`, `CreditoClientePermisosTests` para permisos por reflexión) y `ExistenciaApplication`.

**Precondición:** Tasks 1–3 hechas.

**Files:** Create `API.Application.DTO/articulo/almacen/{ArticuloAlmacenDTO,ArticuloAlmacenGuardarDTO,AlmacenBajoMinimoDTO}.cs`, `API.Application.Interface/IArticuloAlmacenApplication.cs`, `API.Application.Main/ArticuloAlmacenApplication.cs`, `API.Service.WebApi/Controllers/ArticuloAlmacenController.cs`, `API.Service.WebApi.Tests/Application/ArticuloAlmacenApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/ArticuloAlmacenControllerTests.cs`, `API.Service.WebApi.Tests/Controllers/ArticuloAlmacenPermisosTests.cs`, `API.Service.WebApi.Tests/Application/ExistenciaApplicationTests.cs`; Modify `API.Application.DTO/inventario/ExistenciaArticuloDTO.cs`, `API.Application.Main/ExistenciaApplication.cs`, `API.Transversal.Mapper/PerfilMapeo.cs` (ignorar los campos nuevos en `CreateMap<ExistenciaArticulo, ExistenciaArticuloDTO>`), `API.Service.WebApi/Startup.cs` (DI de la aplicación).

**Interfaces — Produces:**
- `ArticuloAlmacenDTO { string CodAlmacen; string NombreAlmacen; bool Bloqueado; decimal EnStock; decimal Comprometido; decimal Pedido; decimal Disponible; decimal? Minimo; decimal? Maximo; decimal? NivelNecesario; string Estado }`.
- `ArticuloAlmacenGuardarDTO { string CodAlmacen; decimal? Minimo; decimal? Maximo; bool Bloqueado }`.
- `AlmacenBajoMinimoDTO { string CodArticulo; string NombreArticulo; string CodAlmacen; string NombreAlmacen; decimal EnStock; decimal Minimo }`.
- `IArticuloAlmacenApplication`: `Task<Respuesta<IEnumerable<ArticuloAlmacenDTO>>> ObtenerAsync(string codigo)`, `Task<Respuesta<bool>> GuardarAsync(string codigo, IEnumerable<ArticuloAlmacenGuardarDTO>? filas)` (nulo o vacío = borra toda la configuración), `Task<Respuesta<IEnumerable<AlmacenBajoMinimoDTO>>> ObtenerBajoMinimoAsync()`. Cada método captura la excepción y devuelve `Resultado = false` con `ex.Message` (como `ExistenciaApplication`).
- `ExistenciaArticuloDTO` gana `decimal? Minimo`, `decimal? Maximo`, `bool Bloqueado` y `string Estado` (`OK`/`BAJO_MINIMO`/`SOBRE_MAXIMO`).
- Controlador `ArticuloAlmacenController` (`[Authorize] [ApiController] [Route("api/Articulo")]`): `[RequierePermiso("Articulos", Accion.Ver)] [HttpGet("{codigo}/Almacenes")]` → `Ok` / `NotFound` si `!Resultado` y el mensaje empieza por «El artículo» y contiene «no existe» (patrón de `ArticuloController.Precio`) / `BadRequest`; `[RequierePermiso("Articulos", Accion.Actualizar)] [HttpPut("{codigo}/Almacenes")]` con `[FromBody] List<ArticuloAlmacenGuardarDTO>` (mismo 404/400); `[RequierePermiso("Articulos", Accion.Ver)] [HttpGet("Almacenes/BajoMinimo")]`.

- [ ] **Step 1: Pruebas (fallan).** Aplicación (mocks del dominio): proyección exacta a los DTOs; captura de excepciones a `Resultado = false` con el mensaje; `GuardarAsync(null)` y lista vacía llaman al dominio con lista vacía; `ExistenciaApplication` llena `Minimo`, `Maximo`, `Bloqueado` y `Estado` de cada fila desde `ObtenerConfiguracionAsync` (sin configuración: nulos, `false`, `OK`) con una sola llamada al dominio por petición. Controlador: 200, 404 (artículo inexistente), 400. Permisos por reflexión (plantilla `CreditoClientePermisosTests`): Ver en los dos GET, Actualizar en el PUT, sin otros métodos públicos sin atributo. Mapeo: la prueba de configuración de AutoMapper existente (si hay) sigue verde.
- [ ] **Step 2: Implementar** DTOs, aplicación, controlador, enriquecimiento de `ExistenciaApplication` (inyecta `IArticuloAlmacenDomain`; actualizar su constructor y todas sus construcciones manuales en pruebas), `Ignore()` en el mapa y DI (`AddTransient<IArticuloAlmacenApplication, ArticuloAlmacenApplication>()`).
- [ ] **Step 3: Pruebas en verde y suite completa.**
- [ ] **Step 4: Commit** `feat(stock-almacen): API de configuracion de stock por almacen y avisos en existencias`.

### Task 5: Prueba de humo contra SQL Server real y regresión

**Plantilla:** `<scratchpad>\smoke-limite-credito.ps1` (levanta la API contra la base temporal, usuario `admin`/`Admin123!`, contadores OK/FALLO, evidencias en `<scratchpad>\evid-*.txt`).

**Precondición:** Tasks 1–4 hechas y suite verde.

**Files:** Create `<scratchpad>\smoke-stock-almacen.ps1` (no se commitea). Ningún archivo del repositorio cambia en esta tarea salvo defectos de producto que se corrijan con su propio commit.

- [ ] **Step 1: Base y API temporales.** `evid-up.ps1 API_DB_EVID<n>` (usa `sql/crear-api-db-test-completo.sql` ya con el bloque nuevo); API en el puerto de la plantilla.
- [ ] **Step 2: Escenarios (≥ 35 comprobaciones).** Migración: script individual idempotente (dos veces) sobre una segunda base; tabla y CHECKs. Lectura: `GET .../Almacenes` de un artículo recién creado trae todos los almacenes activos con stock 0 y `OK`; artículo inexistente → 404. Guardado: mínimo/máximo/bloqueo persisten y se leen; 0 → nulo; fila vacía se elimina; reemplazo completo (omitir una fila la borra); cada validación → 400 con el mensaje literal y sin cambios parciales; sin permiso → 403 (usuario sin `Articulos`/Actualizar). Cálculo: tras una entrada de mercancías de 10 unidades, `enStock` 10, `disponible` 10, `BAJO_MINIMO` con mínimo 15, `nivelNecesario` correcto con máximo 40; `SOBRE_MAXIMO` con máximo 5. **Bloqueo:** con el artículo bloqueado en el almacén `01`: entrada de mercancías → 400 con el mensaje literal; salida de mercancías → 400; entrega de venta y factura de venta del artículo → 400 **sin consumir consecutivo** (`SigNumero` de la serie igual antes y después) y sin movimientos de kardex; factura de compra → 400; el mismo artículo en otro almacén no bloqueado → 200; **cancelar** un documento con movimientos previos al bloqueo funciona (reversa permitida) y deja el kardex coherente; desbloquear y repetir → 200. Existencias: `GET api/Existencia` trae `minimo`, `maximo`, `bloqueado`, `estado`. Home: `GET api/Articulo/Almacenes/BajoMinimo` lista el artículo bajo mínimo con su almacén y deja de listarlo al subir el stock o quitar el mínimo; un servicio (`ArticuloInventario = 'N'`) no aparece.
- [ ] **Step 3: Regresión** en bases temporales propias: `smoke-limite-credito.ps1` (138), `smoke-pagos-final.ps1` (54), `smoke-pagos-efectuados.ps1` (51), `smoke-vencimientos.ps1` (122). Cualquier fallo de producto se corrige (commit propio) antes de seguir; los defectos del script de prueba se arreglan sin commit.
- [ ] **Step 4: Limpieza.** `evid-down.ps1` de cada base y proceso; confirmar sin bases `API_DB_EVID*` ni puertos abiertos. Guardar la evidencia en `<scratchpad>\evid-stock-almacen-<fecha>.txt`.

---

## Fase 2 — Web

### Task 6: Clientes tipados y acciones del controlador de artículos

**Plantilla:** `ArticuloApiClient`/`IArticuloApiClient`, `ExistenciaApiClient`, y las acciones JSON de `ArticulosController` (`ObtenerTodos`, `Editar`).

**Precondición:** Tasks 1–5 hechas; `dotnet build Web.slnx` compila; `git status --short` de la Web solo con `Web.UI/Views/Articulos/Crear.cshtml` y `_Form.cshtml` modificados.

**Files:** Create `Web.ApiClient/Dtos/Articulo/{ArticuloAlmacenDTO,ArticuloAlmacenGuardarDTO,AlmacenBajoMinimoDTO}.cs`; Modify `Web.ApiClient/Clientes/{IArticuloApiClient,ArticuloApiClient}.cs`, `Web.ApiClient/Dtos/Existencia/ExistenciaArticuloDTO.cs`, `Web.UI/Controllers/ArticulosController.cs`.

**Interfaces — Produces:** DTOs de la Web espejo de los de la API (mismos nombres y tipos, namespace `Web.ApiClient.Dtos.Articulo`); `IArticuloApiClient.ObtenerAlmacenesAsync(string codigo)` → `Respuesta<IEnumerable<ArticuloAlmacenDTO>>` (`GET api/Articulo/{codigo}/Almacenes`), `GuardarAlmacenesAsync(string codigo, IEnumerable<ArticuloAlmacenGuardarDTO> filas)` → `Respuesta<bool>` (`PUT`, vía `PutAsync<bool>`), `ObtenerAlmacenesBajoMinimoAsync()` → `Respuesta<IEnumerable<AlmacenBajoMinimoDTO>>`; `ExistenciaArticuloDTO` con `Minimo`, `Maximo`, `Bloqueado`, `Estado`; acciones de `ArticulosController`: `GET /Articulos/Almacenes?codigo=` (`Articulos`/Ver, `Json(respuesta)`), `GET /Articulos/AlmacenesNuevo` (`Articulos`/Agregar; arma filas con stock 0 desde `_almacenes.ObtenerTodoAsync()` solo con almacenes activos, ordenadas por código), `POST /Articulos/GuardarAlmacenes?codigo=` (`[ValidateAntiForgeryToken]`, `Articulos`/Actualizar, `[FromBody] List<ArticuloAlmacenGuardarDTO>`, devuelve `Json(respuesta)`).

- [ ] **Step 1: Commit previo de la petición del usuario.** Stagear **solo** `Web.UI/Views/Articulos/Crear.cshtml` y `Web.UI/Views/Articulos/_Form.cshtml` y commitear `fix(articulos): quita la opcion -- Seleccione -- de fabricante, grupo y grupo de unidad de medida` antes de cualquier otro cambio.
- [ ] **Step 2: Implementar** los DTOs, los métodos de cliente y las tres acciones (verificar que `Almacen` de la Web expone `Activo`; si no, filtrar con lo que tenga y dejar la nota).
- [ ] **Step 3: Verificar.** `dotnet build Web.slnx` sin errores de compilación ni advertencias nuevas.
- [ ] **Step 4: Commit** `feat(stock-almacen): clientes tipados y acciones de la ficha para stock por almacen` (rutas explícitas).

### Task 7: Sección «Datos de inventario» en la ficha y el alta

**Plantilla:** `Web.UI/Views/Articulos/_Form.cshtml` y `Crear.cshtml`, `wwwroot/js/articulos.js` (guardado del modal y de la página «Nuevo»), `App.enviarJson`, `App.mostrarError`, helpers `esc` de `existencias.js`.

**Precondición:** Task 6 hecha.

**Files:** Create `Web.UI/Views/Articulos/_InventarioAlmacenes.cshtml`, `Web.UI/wwwroot/js/articulo-almacenes.js`; Modify `Web.UI/Views/Articulos/_Form.cshtml`, `Web.UI/Views/Articulos/Crear.cshtml`, `Web.UI/Views/Articulos/Index.cshtml` y la vista/`@section Scripts` donde se incluye `articulos.js` (incluir también `articulo-almacenes.js`), `Web.UI/wwwroot/js/articulos.js`.

**Interfaces — Produces:** módulo global `ArticuloAlmacenes` con `cargar(contenedor, codigoOUrl)` (pide las filas por AJAX y pinta la tabla), `recolectar()` (devuelve `[{codAlmacen, minimo, maximo, bloqueado}]` de todas las filas), `validar()` (marca errores por fila y devuelve `true/false`) y `guardar(codigo)` (omite la llamada si no hay nada configurado en el alta; `POST /Articulos/GuardarAlmacenes?codigo=`; devuelve la `Respuesta` o un `{resultado:true}` sin llamada).

- [ ] **Step 1: Parcial y módulo JS.** Tabla de SAP B1: `#`, Código de almacén, Nombre del almacén, Bloqueado (casilla), En stock, Comprometido, Pedido, Disponible, Stock mínimo, Stock máximo, Nivel de stock necesario. Las columnas de stock y el nivel son solo lectura con fondo gris (`table-secondary`/clase propia); Bloqueado, mínimo y máximo son editables (`type="number" step="0.000001" min="0"`). Totales de En stock, Comprometido, Pedido y Disponible en el pie. Al editar mínimo o máximo se recalculan en el navegador el **nivel necesario** y el color de la fila con las mismas fórmulas que `StockAlmacenCalculadora` (rojo `BAJO_MINIMO`, amarillo `SOBRE_MAXIMO`, `OK` sin color). Validación en el navegador: no negativos, `máximo >= mínimo` por fila, mensajes por fila; la API revalida. Todo texto dinámico escapado.
- [ ] **Step 2: Ficha (edición).** `_Form.cshtml` incluye el parcial; al abrirse el modal (`abrirModal` en `articulos.js`) se llama `ArticuloAlmacenes.cargar` con `/Articulos/Almacenes?codigo=`. Guardar: `#btnGuardarArticulo` valida las filas, guarda el artículo como hoy y, si resultó bien, `ArticuloAlmacenes.guardar(codigo)`; si el artículo se guardó y la configuración falla, mostrar el error de la configuración **sin** cerrar el modal ni deshacer el artículo.
- [ ] **Step 3: Alta.** `Crear.cshtml` incluye el parcial **solo** si `User.TienePermiso("Articulos", Accion.Actualizar)` y carga `/Articulos/AlmacenesNuevo`; `#btnGuardarArticuloPagina` valida, crea el artículo y con el `codigo` devuelto llama a `ArticuloAlmacenes.guardar(codigo)` (omitida si no se configuró nada); si la configuración falla, mostrar el error y dejar al usuario en la ficha del artículo recién creado (redirigir a `/Articulos` con aviso de que debe reintentar desde «Editar»).
- [ ] **Step 4: Verificar.** Build sin errores. Recorrido rápido con el navegador integrado contra la API temporal (la prueba completa es la Tarea 9).
- [ ] **Step 5: Commit** `feat(stock-almacen): seccion Datos de inventario en la ficha y el alta de articulos`.

### Task 8: Columnas en Existencias y aviso por almacén en el Home

**Plantilla:** `Web.UI/wwwroot/js/existencias.js`, `Views/Existencias/Index.cshtml`, `HomeController.Index`, `DashboardViewModel`, `Views/Home/Index.cshtml` (tarjeta «Stock bajo», líneas ~64–70 y ~183–195).

**Precondición:** Task 6 hecha.

**Files:** Modify `Web.UI/Views/Existencias/Index.cshtml`, `Web.UI/wwwroot/js/existencias.js`, `Web.UI/Controllers/HomeController.cs`, `Web.UI/Models/Home/DashboardViewModel.cs`, `Web.UI/Views/Home/Index.cshtml`.

- [ ] **Step 1: Existencias.** Columnas Mínimo, Máximo y Bloqueado (insignia «Bloqueado») en la tabla; fila marcada en rojo si `estado = 'BAJO_MINIMO'` y en amarillo si `SOBRE_MAXIMO` (con `createdRow` de DataTables); texto escapado.
- [ ] **Step 2: Home.** `HomeController.Index` consulta `ObtenerAlmacenesBajoMinimoAsync()` y degrada en silencio (lista vacía) si falla o no hay permiso; `DashboardViewModel` gana `List<AlmacenBajoMinimoViewModel> AlmacenesBajoMinimo` (código y nombre del artículo, código y nombre del almacén, en stock, mínimo) y `TotalArticulosStockBajo` pasa a ser (artículos bajo mínimo general) + (filas por almacén). La tarjeta lista primero los del mínimo general (como hoy) y luego las filas por almacén con la leyenda «{artículo} · Almacén {nombre}: en stock X · Mínimo Y»; sigue mostrando solo 3 en total y «Todo en orden» si no hay ninguno.
- [ ] **Step 3: Verificar.** Build sin errores.
- [ ] **Step 4: Commit** `feat(stock-almacen): columnas de minimo, maximo y bloqueo en existencias y aviso por almacen en el inicio`.

### Task 9: Recorrido en navegador, documentación y limpieza

**Plantilla:** recorridos puppeteer de las fases B y C (`<scratchpad>\pupmod`, capturas en `<scratchpad>\docfigs\`).

**Precondición:** Tasks 1–8 hechas; API y Web compilan; suite verde.

**Files:** capturas en el scratchpad; sin cambios en el repositorio salvo defectos de producto (cada uno con su commit).

- [ ] **Step 1: Entorno.** `evid-up.ps1` (API + Web contra la base temporal), con una segunda sucursal (`02`) y un artículo de inventario con existencias.
- [ ] **Step 2: Recorrido con capturas reales revisadas** (sin errores de consola de producto): ficha con la tabla de almacenes; editar mínimo/máximo y ver el recálculo del nivel y el color; guardar y reabrir la ficha con los valores persistidos; marcar bloqueado el almacén `01` y facturar el artículo → diálogo de error con el mensaje del bloqueo y sin factura creada; cancelar un documento previo con el artículo bloqueado funciona; desbloquear y facturar; alta de un artículo nuevo con la sección y valores guardados; usuario sin `Articulos`/Actualizar no ve la sección en el alta ni puede abrir la edición; Existencias con columnas y color; Home con el aviso por almacén. Validaciones por fila (máximo < mínimo, negativos).
- [ ] **Step 3: Limpieza.** `evid-down.ps1`; sin bases `API_DB_EVID*` ni puertos abiertos; capturas finales guardadas.
- [ ] **Step 4: Notas de despliegue** en el informe final (no en el repositorio): ejecutar `sql/2026-11-06-stock-por-almacen.sql` en la base; desplegar API y Web juntas; sin cambios de permisos.
