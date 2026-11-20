# POS de facturación rápida Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un módulo POS para facturar en mostrador: la API registra en una sola transacción una factura de contado más su cobro y devuelve el ticket; la Web ofrece una pantalla rápida (buscador/lector, artículos rápidos, cobro con cambio, ticket de 80 mm, ventas del día) y la configuración que la habilita.

**Architecture:** Endpoint atómico `POST api/Pos/Venta` en un `PosDomain` (Domain.Core) que reutiliza `IFacturaDomain.InsertarAsync` y `IPagoRecibidoDomain.InsertarAsync` dentro de una transacción exterior; para eso `EjecutorTransaccion` pasa a admitir anidamiento. La venta **es una factura normal** (serie POS, `Factura.Comentario` con la marca `Venta POS - Cajero: <usuario>`) y el cobro un pago recibido, así que inventario, costo y reportes no cambian. Contratos compartidos en `API.Domain.Interface/Pos`; la lógica numérica pura vive en `PosCalculadora`.

**Tech Stack:** .NET 7 / EF Core 7 / xUnit + Moq (API, solo SQL Server real para el humo); .NET 8 MVC + jQuery + Bootstrap 5 + SweetAlert2 (Web, sin proyecto de pruebas).

**Spec:** `API/docs/superpowers/specs/2026-11-19-pos-facturacion-rapida-design.md` (léelo antes de empezar: este plan es su argumento; la enmienda de `SeriePago` y de la marca POS ya está incorporada).

## Global Constraints

- **Repos y ramas:** API `C:\Users\migue\source\repos\angelm0508\API` en `desarrollo`; Web `C:\Users\migue\source\repos\angelm0508\Web` en `main`. Trabajo directo en esas ramas (dos repos separados, convención del proyecto). Lee el `AGENTS.md` de cada repo.
- **Git:** commits con rutas explícitas (`git add <archivo>`), **nunca** `git add -A` ni `git add .`. **Nunca** commitear `API.Service.WebApi/appsettings.json`, `sql/crear-api-db-completo*.sql`, `sql/temporal-*.sql` ni `.superpowers/`. **No hacer `push`** (el usuario lo pide aparte). Mensajes `tipo(modulo): descripcion` en español sin tildes. Todo commit termina con la línea `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Conserva el fin de línea de cada archivo (autocrlf activo). `git status` de la API lista ~25 `*CrearDTO.cs` como modificados solo por fin de línea: no los toques ni los commitees (los 11 DTO de documentos que ya llevan `Nit` ya están commiteados).
- **Capas (API):** `Service.WebApi` → `Application.Main` → `Domain.Core` → `Infraestructure.Repository`. `Application.*` depende solo de `Domain.Interface`. Los tipos compartidos (records, interfaces) van en `Domain.Interface`. Respuestas siempre `Respuesta<T> { Resultado, Mensaje, Dato }`; `Application` atrapa las excepciones de negocio y devuelve `Resultado = false` con `ex.Message`; el controlador responde 400 (404 si el mensaje indica que no existe). Evita parámetros opcionales en interfaces de dominio (rompen Moq, CS0854).
- **Permisos:** `[RequierePermiso("Modulo", Accion.X)]` en cada acción (API y Web). Módulos nuevos: `Pos` y `PosConfiguracion` (grupo `POS`).
- **Validar antes de numerar:** ningún rechazo puede consumir un consecutivo. La venta es todo o nada.
- **Mensajes de negocio:** literales en español; las pruebas los verifican. Los de este plan son definitivos.
- **Montos:** 2 decimales, redondeo `MidpointRounding.AwayFromZero`; texto de montos en mensajes con `ToString("0.00", CultureInfo.InvariantCulture)`.
- **Pruebas:** TDD (prueba primero y verla fallar), xUnit + Moq (`API.Service.WebApi.Tests`, patrón de `Mock<IRepositorioGenerico<…>>` + `.AsAsyncQueryable()` de `TestHelpers`). Cada tarea cierra con build sin errores y suite completa en verde. **Línea base al empezar: 3436** (confírmala y anótala). Si una API/Web en ejecución bloquea `bin`, compila/prueba con `-p:BaseOutputPath="$env:TEMP\gescom-build-pos\"` (con la barra final; los errores MSB3021/MSB3027 son de bloqueo de archivos).
- **SQL real:** pruebas de humo **solo** en bases temporales `API_DB_EVID<n>` creadas con el script completo y eliminadas al terminar (scripts `evid-up.ps1`, `evid-up-api.ps1`, `evid-down-api.ps1` y los `cv-*.ps1`/`t12-*.ps1` en `<scratchpad>`). **Jamás** contra `API_DB_TEST`, `API_DB` ni una base del usuario. La contraseña de `sa` y la clave JWT **nunca** se imprimen, copian ni commitean (si el `sa` de `appsettings.json` no autentica, detente y pregunta).
- **`<scratchpad>`** = `C:\Users\migue\AppData\Local\Temp\claude\9f8881082ead4f04\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad` (scripts y evidencias; nada de eso se commitea).
- **Web:** **escapa siempre** el texto dinámico que se inserta en HTML desde JS; `App.enviarJson` para POST; sin `Html.Raw` con datos; sin `console.log` de depuración. Verifica con `dotnet build Web.slnx` (0 errores) y `node --check` de cada JS tocado; di qué no pudiste verificar en navegador.
- **Dependencias nuevas:** ninguna en producción. Solo si la Tarea 2 lo necesita, un paquete de **pruebas** `Microsoft.EntityFrameworkCore.Sqlite` con la misma versión mayor de EF Core que use el proyecto (7.x), en `API.Service.WebApi.Tests`.

## Rulings (decisiones tomadas al planificar)

1. **`SeriePago` en la configuración.** `PagoRecibidoDomain` exige una serie de pagos (`CodigoObj = '24'`); se agrega `SeriePago` a `PosConfiguracion` (spec §3, ya enmendado).
2. **Cajero y marca POS.** `Factura.Comentario = "Venta POS - Cajero: " + usuario`. Una factura es venta POS si su `Comentario` empieza con `Venta POS - Cajero: ` (constante `PosMarca.Prefijo`). No hay columna nueva en `Factura`.
3. **Contratos en `Domain.Interface/Pos`** viajan sin DTO espejo por Application y el controlador (como la reportería). La Web tiene sus DTO propios que los reflejan en `Web.ApiClient/Dtos/Pos`.
4. **Transacciones anidadas.** `EjecutorTransaccion` ejecuta la operación dentro de la transacción activa si ya hay una (sin confirmar ni revertir; lo hace el nivel exterior). El nivel de aislamiento solicitado por una operación anidada (p. ej. `Serializable` en el pago) **se ignora** y rige el del exterior (por defecto el de la conexión). La venta POS usa una transacción exterior `Serializable` (la que exige `PagoRecibidoDomain` para su chequeo de saldo), con un candado `UPDLOCK,HOLDLOCK` sobre las series de factura y de pagos (`IBloqueoSeries`, tarea 6c).
7. **Cheque.** El cobro admite las formas de pago activas. Si `PagoRecibidoDomain.ValidarFormasDePagoAsync` exige datos extra según `FormaPago.TipoDato` (p. ej. banco y fecha de cheque), el contrato `VentaPosPago` los trae (`Banco`, `FechaCheque`) y la pantalla los pide; no se inventan reglas nuevas.
4. **Existencias en la búsqueda.** Las existencias que muestra la búsqueda son `ExistenciaArticulo.Disponible` del almacén POS (0 si no hay fila); es informativo: la validación real de stock la hace `FacturaDomain` al cobrar.
5. **Unidad.** Las líneas POS van sin `UomEntry` (`FacturaDomain` resuelve la unidad base y el factor).
6. **Sin precio = no vendible.** Un artículo sin precio > 0 en la lista del cliente no aparece en la búsqueda ni en los rápidos y la venta lo rechaza.
8. **Nombre de la empresa en el ticket.** `PosDomain` no tiene configuración: arma `TicketPos` con `Empresa = string.Empty` y `PosController` (que ya puede inyectar `IConfiguration`, como `ReporteController`) lo reemplaza con `ticket with { Empresa = _config["Reportes:Empresa"] ?? "Sistema de Gestión" }` en `POST Venta` y `GET Ticket`.

## Estructura de archivos

**API (rama `desarrollo`)**

| Archivo | Responsabilidad |
|---|---|
| `sql/2026-11-19-pos.sql` (+ bloque en `sql/crear-api-db-test-completo.sql` y en el local sin seguimiento `sql/crear-api-db-completo.sql`) | `PosConfiguracion`, columnas de `Articulo`, módulos de permiso |
| `API.Domain.Entity/Models/PosConfiguracion.cs`, `Articulo.cs`, `ApiDbTestContext.cs` | Entidad y mapeo EF |
| `API.Infraestructure.Repository/EjecutorTransaccion.cs` | Transacción anidable |
| `API.Domain.Interface/Pos/*.cs` | Contratos: configuración, catálogo, venta, ticket, `IPosDomain`, `PosMarca` |
| `API.Domain.Core/Pos/PosCalculadora.cs`, `PosDomain.cs` | Cálculo puro; casos de uso |
| `API.Application.Interface/IPosApplication.cs`, `API.Application.Main/PosApplication.cs` | Casos de uso con `Respuesta<T>` |
| `API.Service.WebApi/Controllers/PosController.cs`, `Startup.cs` | Endpoints y DI |
| `API.Application.DTO/articulo/articulo/*.cs`, `API.Transversal.Mapper/PerfilMapeo.cs` | `CodigoBarras` y `MostrarEnPos` en artículos |
| `API.Service.WebApi.Tests/...` | Pruebas de cada pieza |

**Web (rama `main`)**

| Archivo | Responsabilidad |
|---|---|
| `Web.ApiClient/Dtos/Pos/*.cs`, `Clientes/IPosApiClient.cs`, `PosApiClient.cs`, `Dtos/Articulo/*` | Cliente tipado y DTO |
| `Web.UI/Controllers/PosController.cs`, `PosConfiguracionController.cs`, `Program.cs` | Páginas y proxys JSON |
| `Web.UI/Views/Pos/Index.cshtml`, `_Ticket.cshtml`, `Views/PosConfiguracion/Index.cshtml`, `Views/Shared/_Layout.cshtml` | Pantallas y menú |
| `Web.UI/wwwroot/js/pos.js`, `pos-configuracion.js`, `wwwroot/css/pos.css` | Lógica y estilos |
| `Web.UI/Views/Articulos/Crear.cshtml`, `_Form.cshtml`, `wwwroot/js/articulos.js`, `Controllers/ArticulosController.cs` | Campos de la ficha del artículo |

---

## Fase 1 — Base de datos y API

### Task 1: Datos — script SQL, entidades y campos del artículo

**Files:**
- Create: `sql/2026-11-19-pos.sql`, `API.Domain.Entity/Models/PosConfiguracion.cs`
- Modify: `sql/crear-api-db-test-completo.sql` (bloque al final), `sql/crear-api-db-completo.sql` (local, sin commit), `API.Domain.Entity/Models/Articulo.cs`, `API.Domain.Entity/Models/ApiDbTestContext.cs`, `API.Application.DTO/articulo/articulo/{ArticuloCrearDTO,ArticuloActualizarDTO,ArticuloDTO}.cs` (verifica los nombres reales), `API.Transversal.Mapper/PerfilMapeo.cs` (si algún mapeo de artículo es explícito)
- Test: `API.Service.WebApi.Tests/Domain/ModeloPosTests.cs`, `API.Service.WebApi.Tests/Application/ArticuloPosMapeoTests.cs`

**Interfaces:**
- Produces: entidad `PosConfiguracion { int Id; int? SeriePago; int? SerieFactura; string? CodAlmacen; string? CodigoImpuesto; string? ClienteDefecto; string? FormaPagoEfectivo; string? MonedaDoc }`; en `Articulo`: `string? CodigoBarras`, `string MostrarEnPos = "N"`; las mismas dos propiedades en los DTO de creación, actualización y consulta de artículo.

- [ ] **Step 1: Lee** las tablas referenciadas para fijar tipos y longitudes: `SocioNegocio.Codigo`, `Almacen.Codigo`, `Impuesto.Codigo`, `FormaPago.Codigo`, `Moneda.Codigo`, `NumeracionDocumentoDet.Serie` (en `sql/crear-api-db-test-completo.sql` y `ApiDbTestContext.cs`), y el estilo de scripts idempotentes `sql/2026-11-08-reporteria.sql` y `sql/2026-10-26-condiciones-pago-vencimientos.sql` (uso de `EXEC('...')` cuando un `CHECK`/`DEFAULT`/`UPDATE` referencia una columna agregada en el mismo lote).
- [ ] **Step 2: Pruebas (fallan).** `ModeloPosTests`: las propiedades `Articulo.CodigoBarras` (string?) y `Articulo.MostrarEnPos` (por defecto `"N"`) y `PosConfiguracion` existen con los tipos del contrato (reflexión), y el modelo EF define `CodigoBarras` con `HasMaxLength(50)`, `MostrarEnPos` con `HasMaxLength(1)` y `PosConfiguracion` como entidad (construye `ApiDbTestContext` con `UseInMemoryDatabase` y consulta `context.Model`). `ArticuloPosMapeoTests` (patrón de `VencimientoMapeoTests`: `MapperConfiguration` con `PerfilMapeo`): los tres mapeos de artículo (crear→entidad, actualizar→entidad, entidad→consulta DTO) propagan `CodigoBarras` y `MostrarEnPos`; un `ActualizarDTO` sin código de barras **sí lo borra** (el `PUT` reemplaza columnas, así que la Web debe enviar siempre los valores actuales: lo cubre la Tarea 9).
- [ ] **Step 3:** `dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~ModeloPosTests|FullyQualifiedName~ArticuloPosMapeoTests" --nologo -v q` → fallan.
- [ ] **Step 4: Script SQL** `sql/2026-11-19-pos.sql` (idempotente):
  - `IF OBJECT_ID('dbo.PosConfiguracion','U') IS NULL CREATE TABLE dbo.PosConfiguracion (Id int NOT NULL CONSTRAINT PK_PosConfiguracion PRIMARY KEY, SeriePago int NULL, SerieFactura int NULL, CodAlmacen <tipo> NULL, CodigoImpuesto <tipo> NULL, ClienteDefecto <tipo> NULL, FormaPagoEfectivo <tipo> NULL, MonedaDoc <tipo> NULL, CONSTRAINT CK_PosConfiguracion_Id CHECK (Id = 1))` y las FK a `NumeracionDocumentoDet(Serie)` (×2), `Almacen`, `Impuesto`, `SocioNegocio`, `FormaPago` y `Moneda`, cada una creada solo si no existe (`IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = ...)`).
  - Fila inicial `Id = 1` con `FormaPagoEfectivo = 'EFE'` si existe esa forma de pago (`IF NOT EXISTS (SELECT 1 FROM dbo.PosConfiguracion) INSERT ...`).
  - `Articulo`: `IF COL_LENGTH('dbo.Articulo','CodigoBarras') IS NULL ALTER TABLE dbo.Articulo ADD CodigoBarras NVARCHAR(50) NULL;`, `IF COL_LENGTH('dbo.Articulo','MostrarEnPos') IS NULL ALTER TABLE dbo.Articulo ADD MostrarEnPos CHAR(1) NOT NULL CONSTRAINT DF_Articulo_MostrarEnPos DEFAULT 'N';`, `CHECK (MostrarEnPos IN ('S','N'))` (con `EXEC`), e índice único filtrado `UX_Articulo_CodigoBarras ON dbo.Articulo(CodigoBarras) WHERE CodigoBarras IS NOT NULL` (con `EXEC` por la regla del lote).
  - Módulos de permiso con `MERGE dbo.Modulo` como en `2026-11-08-reporteria.sql`: `Pos` («Punto de venta») y `PosConfiguracion` («Configuración del POS»), `Grupo = 'POS'`.
  - Cierra con los `SELECT` de verificación habituales. Agrega **el mismo bloque** al final de `sql/crear-api-db-test-completo.sql` y del local `sql/crear-api-db-completo.sql`.
- [ ] **Step 5: Entidades y mapeo EF.** `PosConfiguracion.cs` (partial, estilo de las demás), propiedades en `Articulo.cs`, `DbSet<PosConfiguracion>` y `modelBuilder.Entity<PosConfiguracion>` con `HasKey(e => e.Id)`, `ToTable("PosConfiguracion")`, `ValueGeneratedNever()` en `Id` y longitudes según el Step 1; en `Articulo` `CodigoBarras` (`HasMaxLength(50)`) y `MostrarEnPos` (`HasMaxLength(1)`, `HasDefaultValueSql("('N')")`). Agrega las propiedades a los tres DTO de artículo (`MostrarEnPos` con valor por defecto `"N"` en el de creación) y ajusta `PerfilMapeo.cs` si algún mapeo es explícito.
- [ ] **Step 6:** pruebas verdes, build de `API.sln` 0 errores, suite completa verde (anota el conteo).
- [ ] **Step 7: Commit** (rutas explícitas; **sin** `sql/crear-api-db-completo.sql`): `feat(pos): tabla de configuracion y campos de articulo`.

---

### Task 2: Transacciones anidables

**Files:**
- Modify: `API.Infraestructure.Repository/EjecutorTransaccion.cs`
- Test: `API.Service.WebApi.Tests/Infraestructure/EjecutorTransaccionAnidadoTests.cs`

**Interfaces:**
- Produces: `public static async Task<T> EjecutarConAnidamientoAsync<T>(DbContext context, Func<Task<T>> operacion, System.Data.IsolationLevel? nivel)` en `EjecutorTransaccion`. `IEjecutorTransaccion` no cambia; las dos sobrecargas de instancia delegan en ella con su `_context`.

**Código** (reemplaza el cuerpo de la clase):

```csharp
using Microsoft.EntityFrameworkCore;
using API.Infraestructure.Interface;

namespace API.Infraestructure.Repository
{
    public class EjecutorTransaccion : IEjecutorTransaccion
    {
        private readonly ApiDbTestContext _context;

        public EjecutorTransaccion(ApiDbTestContext context)
        {
            _context = context;
        }

        public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion)
        {
            // Nivel por defecto de la conexión (READ COMMITTED): comportamiento histórico.
            return EjecutarConAnidamientoAsync(_context, operacion, null);
        }

        public Task<T> EjecutarAsync<T>(Func<Task<T>> operacion, System.Data.IsolationLevel nivel)
        {
            return EjecutarConAnidamientoAsync(_context, operacion, nivel);
        }

        /// <summary>
        /// Ejecuta la operación en una transacción. Si ya hay una activa en el contexto (p. ej. la venta POS, que compone
        /// factura y cobro), la operación corre DENTRO de ella: se guardan sus cambios pero no se confirma ni se revierte
        /// aquí; eso lo hace el nivel exterior. El nivel de aislamiento pedido por una operación anidada se ignora: rige el
        /// de la transacción exterior.
        /// </summary>
        public static async Task<T> EjecutarConAnidamientoAsync<T>(DbContext context, Func<Task<T>> operacion, System.Data.IsolationLevel? nivel)
        {
            if (context.Database.CurrentTransaction is not null)
            {
                var anidado = await operacion();
                await context.SaveChangesAsync();
                return anidado;
            }

            await using var tx = nivel.HasValue
                ? await context.Database.BeginTransactionAsync(nivel.Value)
                : await context.Database.BeginTransactionAsync();
            try
            {
                var resultado = await operacion();
                await context.SaveChangesAsync();
                await tx.CommitAsync();
                return resultado;
            }
            catch
            {
                await tx.RollbackAsync();
                context.ChangeTracker.Clear(); // el contexto scoped queda con mutaciones parciales tras el rollback
                throw;
            }
        }
    }
}
```

Conserva los `using` y el namespace que ya tiene el archivo (si difieren de lo anterior, mantén los reales).

- [ ] **Step 1: Pruebas (fallan).** Primero mira si `API.Service.WebApi.Tests` ya referencia `Microsoft.EntityFrameworkCore.Sqlite`; si no, agrégalo como paquete de pruebas (misma versión mayor de EF Core). Usa un `DbContext` de prueba mínimo (clase privada con una entidad `Fila { int Id; string Texto }`) sobre una conexión SQLite **abierta** en memoria (`new SqliteConnection("DataSource=:memory:")`, `Open()`), porque `EjecutarConAnidamientoAsync` recibe `DbContext`. Casos:
  1. *Sin transacción activa confirma:* una operación que agrega una fila y devuelve 7 → devuelve 7, la fila existe en un contexto nuevo sobre la misma conexión.
  2. *Sin transacción activa revierte:* la operación agrega y lanza `InvalidOperationException("x")` → la excepción se propaga, la fila **no** existe y el `ChangeTracker` queda vacío.
  3. *Anidada comparte la transacción:* dentro de una exterior, la interior agrega una fila; al terminar la interior **no** hay commit (la fila no es visible desde otro contexto sobre otra conexión… con SQLite en memoria usa `context.Database.CurrentTransaction` para afirmar que la transacción exterior sigue activa y que `Database.CurrentTransaction.TransactionId` es el mismo antes y después de la interior).
  4. *Rollback del exterior deshace lo anidado:* exterior agrega A, interior (anidada) agrega B, el exterior lanza → ni A ni B quedan.
  5. *Fallo de la anidada deshace todo:* exterior agrega A, interior agrega B y lanza → la excepción llega al llamador del exterior y ni A ni B quedan.
  6. *Aislamiento anidado ignorado:* la interior pide `IsolationLevel.Serializable` dentro de una exterior sin nivel → no lanza.
- [ ] **Step 2:** `dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~EjecutorTransaccionAnidadoTests" --nologo -v q` → falla (el método no existe).
- [ ] **Step 3:** Reemplaza `EjecutorTransaccion.cs` con el código de arriba.
- [ ] **Step 4:** pruebas verdes. Build de `API.sln` y suite completa verde (**toda la suite existente debe seguir pasando**: el comportamiento sin transacción activa no cambia).
- [ ] **Step 5: Commit**: `feat(pos): ejecutor de transacciones anidable` (incluye el `.csproj` de pruebas si agregaste el paquete).

---

### Task 3: Contratos y calculadora de venta y cobro

**Files:**
- Create: `API.Domain.Interface/Pos/{PosMarca,Contratos,IPosDomain}.cs`, `API.Domain.Core/Pos/PosCalculadora.cs`
- Test: `API.Service.WebApi.Tests/Domain/PosCalculadoraTests.cs`

**Interfaces (produce; las usan las Tareas 4 a 10):**

```csharp
namespace API.Domain.Interface.Pos
{
    public static class PosMarca
    {
        public const string Prefijo = "Venta POS - Cajero: ";
        public static string Comentario(string cajero) => Prefijo + cajero;
        public static bool EsVentaPos(string? comentario) => comentario != null && comentario.StartsWith(Prefijo, StringComparison.Ordinal);
        public static string Cajero(string? comentario) => EsVentaPos(comentario) ? comentario!.Substring(Prefijo.Length) : string.Empty;
    }

    public sealed record PosConfiguracionDato(
        int? SerieFactura, int? SeriePago, string? CodAlmacen, string? CodigoImpuesto,
        string? ClienteDefecto, string? FormaPagoEfectivo, string? MonedaDoc);

    public sealed record PosConfiguracionVista(PosConfiguracionDato Configuracion, IReadOnlyList<string> Faltantes);

    public sealed record ArticuloPos(
        string Codigo, string? CodigoBarras, string Nombre, decimal Precio, decimal Existencias, bool Inventariable);

    public sealed record VentaPosLinea(string CodArticulo, decimal Cantidad);

    public sealed record VentaPosPago(string CodFormaPago, decimal Monto, string? Referencia, string? Banco, DateTime? FechaCheque);

    public sealed record VentaPosSolicitud(
        string? CodigoCliente, string? Nit, List<VentaPosLinea> Lineas, List<VentaPosPago> Pagos);

    public sealed record TicketPosLinea(string CodArticulo, string Descripcion, decimal Cantidad, decimal PrecioNeto, decimal Total);

    public sealed record TicketPosPago(string CodFormaPago, string Forma, decimal Monto);

    public sealed record TicketPos(
        int FacturaEntry, int Serie, string NombreSerie, int NumDoc, DateTime Fecha, string Cajero,
        string CodigoCliente, string NombreCliente, string Nit,
        IReadOnlyList<TicketPosLinea> Lineas, decimal Subtotal, decimal Impuesto, decimal Total,
        IReadOnlyList<TicketPosPago> Pagos, decimal Pagado, decimal Cambio, string Moneda, string Empresa);

    public sealed record VentaPosDelDia(int FacturaEntry, int Serie, int NumDoc, DateTime Fecha, string CodigoCliente, string NombreCliente, decimal Total, bool Cancelada);

    public interface IPosDomain
    {
        Task<PosConfiguracionVista> ObtenerConfiguracionAsync();
        Task GuardarConfiguracionAsync(PosConfiguracionDato dato);
        Task<IReadOnlyList<ArticuloPos>> BuscarArticulosAsync(string? texto, string? codigoCliente);
        Task<ArticuloPos?> ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente);
        Task<IReadOnlyList<ArticuloPos>> ObtenerRapidosAsync();
        Task<TicketPos> VenderAsync(VentaPosSolicitud solicitud, string cajero);
        Task<TicketPos> ObtenerTicketAsync(int facturaEntry);
        Task<IReadOnlyList<VentaPosDelDia>> ObtenerVentasDelDiaAsync();
    }
}
```

**`PosCalculadora`** (`API.Domain.Core/Pos`, estática, pura):

```csharp
using System.Globalization;
using API.Domain.Interface.Pos;

namespace API.Domain.Core.Pos
{
    public sealed record LineaPosCalculada(string CodArticulo, decimal Cantidad, decimal PrecioNeto, decimal Bruto, decimal Impuesto, decimal TotalLinea);

    public sealed record TotalesPos(IReadOnlyList<LineaPosCalculada> Lineas, decimal Bruto, decimal Impuesto, decimal Total);

    public sealed record PagoPosRegistrado(string CodFormaPago, decimal Monto, string? Referencia, string? Banco, DateTime? FechaCheque);

    public sealed record ResultadoPagosPos(decimal Pagado, decimal Cambio, IReadOnlyList<PagoPosRegistrado> Medios);

    public static class PosCalculadora
    {
        public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        public static string Formato(decimal monto) => monto.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Una línea: bruto = cantidad × precio neto; impuesto sobre el bruto sin redondear (como la Web de facturas).</summary>
        public static LineaPosCalculada Linea(string codArticulo, decimal cantidad, decimal precioNeto, decimal tasaImpuesto)
        {
            var brutoSinRedondear = cantidad * precioNeto;
            var bruto = Redondear(brutoSinRedondear);
            var impuesto = Redondear(brutoSinRedondear * tasaImpuesto / 100m);
            return new LineaPosCalculada(codArticulo, cantidad, precioNeto, bruto, impuesto, bruto + impuesto);
        }

        public static TotalesPos Totales(IReadOnlyList<LineaPosCalculada> lineas) =>
            new(lineas, lineas.Sum(l => l.Bruto), lineas.Sum(l => l.Impuesto), lineas.Sum(l => l.TotalLinea));

        /// <summary>
        /// Valida los pagos contra el total y calcula el cambio. El cambio solo puede salir de la forma de pago efectivo y se
        /// resta de esos pagos en orden (los que quedan en cero se descartan): el cobro registrado es exactamente el total.
        /// </summary>
        public static ResultadoPagosPos ValidarPagos(decimal total, IReadOnlyList<VentaPosPago> pagos, string? formaEfectivo)
        {
            if (pagos == null || pagos.Count == 0)
                throw new Exception("Indica al menos una forma de pago.");
            if (pagos.Any(p => string.IsNullOrWhiteSpace(p.CodFormaPago)))
                throw new Exception("La forma de pago es requerida.");
            if (pagos.Any(p => Redondear(p.Monto) <= 0m))
                throw new Exception("El monto de cada forma de pago debe ser mayor que cero.");

            var montos = pagos.Select(p => Redondear(p.Monto)).ToList();
            var pagado = montos.Sum();
            if (pagado < total)
                throw new Exception($"El pago ({Formato(pagado)}) no cubre el total ({Formato(total)}).");

            var cambio = pagado - total;
            var efectivo = string.IsNullOrWhiteSpace(formaEfectivo)
                ? 0m
                : pagos.Select((p, i) => (p, i)).Where(x => x.p.CodFormaPago == formaEfectivo).Sum(x => montos[x.i]);
            if (cambio > efectivo)
                throw new Exception($"El cambio ({Formato(cambio)}) solo puede darse con efectivo.");

            var restante = cambio;
            var medios = new List<PagoPosRegistrado>();
            for (var i = 0; i < pagos.Count; i++)
            {
                var monto = montos[i];
                if (restante > 0m && pagos[i].CodFormaPago == formaEfectivo)
                {
                    var descuento = Math.Min(restante, monto);
                    monto -= descuento;
                    restante -= descuento;
                }
                if (monto > 0m)
                    medios.Add(new PagoPosRegistrado(pagos[i].CodFormaPago, monto, pagos[i].Referencia, pagos[i].Banco, pagos[i].FechaCheque));
            }
            return new ResultadoPagosPos(pagado, cambio, medios);
        }
    }
}
```

- [ ] **Step 1: Pruebas (fallan)** en `PosCalculadoraTests` con estos números exactos:
  - `Linea("A1", 2, 45.00m, 12m)` → bruto 90.00, impuesto 10.80, total 100.80. `Linea("A2", 3, 33.333m, 12m)` → bruto 100.00, impuesto 12.00 (11.99988 redondeado), total 112.00. `Linea("A3", 1, 0.005m, 0m)` → bruto 0.01 (medio hacia arriba). `Linea` con tasa 0 → impuesto 0.
  - `Totales` suma bruto, impuesto y total de varias líneas (45×2 + 22×1 con 12 % → bruto 112.00, impuesto 13.44, total 125.44).
  - `ValidarPagos(112.00, [EFE 200], "EFE")` → pagado 200, cambio 88.00, medios `[EFE 112.00]`.
  - `ValidarPagos(112.00, [TJT 50, EFE 100], "EFE")` → cambio 38.00, medios `[TJT 50.00, EFE 62.00]` (orden conservado).
  - `ValidarPagos(112.00, [EFE 112], "EFE")` → cambio 0, medios `[EFE 112.00]`.
  - `ValidarPagos(100.00, [EFE 10, EFE 200], "EFE")` → cambio 110.00, medios `[EFE 100.00]` (el primer efectivo queda en 0 y se descarta).
  - Errores (mensaje literal): `[]` → «Indica al menos una forma de pago.»; forma vacía → «La forma de pago es requerida.»; monto 0 o negativo → «El monto de cada forma de pago debe ser mayor que cero.»; `ValidarPagos(112, [EFE 100], "EFE")` → «El pago (100.00) no cubre el total (112.00).»; `ValidarPagos(112, [TJT 150], "EFE")` → «El cambio (38.00) solo puede darse con efectivo.»; `ValidarPagos(112, [TJT 150], null)` → mismo error (sin forma de efectivo configurada no hay cambio); `ValidarPagos(112, [TJT 112], null)` → OK, cambio 0.
  - `PosMarca`: `Comentario("ana")` = «Venta POS - Cajero: ana»; `EsVentaPos` verdadero/falso/nulo; `Cajero("Venta POS - Cajero: ana")` = «ana»; `Cajero("otro")` = vacío.
- [ ] **Step 2:** `dotnet test API.Service.WebApi.Tests --filter "FullyQualifiedName~PosCalculadoraTests" --nologo -v q` → falla (no compila).
- [ ] **Step 3:** Crea los tres archivos de `Domain.Interface/Pos` y `PosCalculadora.cs` con el código de arriba (`Contratos.cs` agrupa los records y `IPosDomain`; `PosMarca.cs` la clase estática; separa `IPosDomain` en su archivo si prefieres, con el mismo namespace). Verifica que `API.Domain.Core` pueda ver `API.Domain.Interface.Pos` (ya referencia `Domain.Interface`).
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(pos): contratos y calculadora de venta y cobro`.

---

### Task 4: Configuración del POS (dominio, aplicación y endpoints)

**Files:**
- Create: `API.Domain.Core/Pos/PosDomain.cs` (parcial: configuración; las Tareas 5 y 6 agregan métodos), `API.Application.Interface/IPosApplication.cs`, `API.Application.Main/PosApplication.cs`, `API.Service.WebApi/Controllers/PosController.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/PosConfiguracionDomainTests.cs`, `API.Service.WebApi.Tests/Application/PosApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/PosControllerTests.cs`

**Interfaces:**
- Consumes: `IPosDomain` y contratos (Tarea 3); `PosConfiguracion` (Tarea 1).
- Produces: `IPosApplication` con **todos** los métodos de la API (cada uno `Task<Respuesta<T>>`): `ObtenerConfiguracionAsync()`, `GuardarConfiguracionAsync(PosConfiguracionDato)`, `BuscarArticulosAsync(string? texto, string? codigoCliente)`, `ObtenerArticuloPorCodigoAsync(string codigo, string? codigoCliente)`, `ObtenerRapidosAsync()`, `VenderAsync(VentaPosSolicitud, string cajero)`, `ObtenerTicketAsync(int)`, `ObtenerVentasDelDiaAsync()`. En esta tarea el dominio implementa solo la configuración; los demás métodos de `IPosDomain` lanzan `NotImplementedException` hasta las Tareas 5 y 6 (no se registran endpoints para ellos todavía).
- Produces (controlador `api/Pos`): `GET Configuracion` (`Pos.Ver` **o** `PosConfiguracion.Ver`; evalúa con `PermisoUsuario.Tiene(User, "Pos", Accion.Ver) || PermisoUsuario.Tiene(User, "PosConfiguracion", Accion.Ver)` dentro de la acción, como `ReporteController`, y 403 si ninguno) y `PUT Configuracion` (`[RequierePermiso("PosConfiguracion", Accion.Actualizar)]`).

**Reglas de `ObtenerConfiguracionAsync`:** devuelve la fila `Id = 1` (si no existe, una vacía) y `Faltantes`: lista de nombres para el usuario de lo que está sin definir, en este orden y con estos textos: «Serie de factura POS», «Serie de pagos», «Almacén POS», «Impuesto por defecto», «Cliente por defecto», «Forma de pago en efectivo», «Moneda».

**Reglas de `GuardarConfiguracionAsync`** (valida todo antes de guardar; mensajes literales; cada campo nulo se permite = «sin definir»):
- `SerieFactura`: debe existir, ser `CodigoObj = '6'` y `SubTipoDoc = '--'`, `Manual != 'S'` y `Bloqueado != 'S'`; si no: «La serie de factura POS no es válida: debe ser una serie automática y activa de facturas.»
- `SeriePago`: existir, `CodigoObj = '24'`, no manual ni bloqueada; si no: «La serie de pagos no es válida: debe ser una serie automática y activa de pagos recibidos.»
- `CodAlmacen` existe: «El almacén no existe.» · `CodigoImpuesto` existe: «El impuesto no existe.» · `ClienteDefecto` existe y `TipoSn = 'C'`: «El cliente por defecto no existe o no es un cliente.» · `FormaPagoEfectivo` existe y activa: «La forma de pago en efectivo no existe o está inactiva.» · `MonedaDoc` existe: «La moneda no existe.»
- Guarda en la fila `Id = 1` (la crea si falta).

- [ ] **Step 1: Pruebas (fallan).** `PosConfiguracionDomainTests` (repos mock): fila inexistente → todos los faltantes en el orden dado; configuración completa → faltantes vacíos; cada regla de `Guardar` con su mensaje (serie manual, bloqueada, de otro documento, subtipo distinto de `--`, cliente de tipo proveedor, forma de pago inactiva…); guardar válido persiste los siete valores; nulos permitidos. `PosApplicationTests`: éxito → `Resultado = true`; excepción → `Resultado = false` con el mensaje. `PosControllerTests` (patrón de `ReporteControllerTests`: controlador con `ClaimsPrincipal` de prueba): `GET Configuracion` con `Pos.Ver` → 200, con `PosConfiguracion.Ver` → 200, sin ninguno → 403; `PUT` sin permiso se bloquea por el atributo (verifica con reflexión que lleva `RequierePermiso("PosConfiguracion", Actualizar)`), éxito → 200, error → 400.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~PosConfiguracionDomainTests|FullyQualifiedName~PosApplicationTests|FullyQualifiedName~PosControllerTests"` → fallan.
- [ ] **Step 3: Implementa** `PosDomain` (inyecta `IRepositorioGenerico<PosConfiguracion,int>`, `<NumeracionDocumentoDet,int>`, `<Almacen,string>`, `<Impuesto,string>`, `<SocioNegocio,string>`, `<FormaPago,string>`, `<Moneda,string>` — verifica los tipos de clave reales), `PosApplication` y `PosController`. En `Startup.cs`: `services.AddTransient<IPosDomain, PosDomain>(); services.AddTransient<IPosApplication, PosApplication>();`.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(pos): configuracion del pos (dominio, aplicacion y endpoints)`.

---

### Task 5: Catálogo — búsqueda, código exacto y artículos rápidos

**Files:**
- Modify: `API.Domain.Core/Pos/PosDomain.cs`, `API.Application.Main/PosApplication.cs`, `API.Service.WebApi/Controllers/PosController.cs`
- Test: `API.Service.WebApi.Tests/Domain/PosCatalogoDomainTests.cs`, ampliar `PosApplicationTests.cs` y `PosControllerTests.cs`

**Interfaces:**
- Consumes: `IPrecioListaDomain.ResolverAsync(string codigoArticulo, int? listaPrecioEntry)` → `PrecioResuelto(Codigo, PrecioBase, FactorEfectivo, PrecioLista)` (el precio neto por unidad es `PrecioLista`); `SocioNegocio.NumLstPrecio`; `Articulo`, `ExistenciaArticulo` (clave `(string CodArticulo, string CodAlmacen)`), `PosConfiguracion`; `ConsultaLineasVenta` (`API.Domain.Core/Reportes/Compartido`) para los más vendidos.
- Produces: `PosDomain.BuscarArticulosAsync`, `ObtenerArticuloPorCodigoAsync`, `ObtenerRapidosAsync`; endpoints `GET api/Pos/Articulos?texto=&cliente=`, `GET api/Pos/Articulos/porCodigo/{codigo}?cliente=` (404 si es null), `GET api/Pos/Rapidos`, todos `[RequierePermiso("Pos", Accion.Ver)]`.

**Reglas:**
- **Cliente para el precio:** `codigoCliente` o, si es nulo, `ClienteDefecto` de la configuración; la lista de precios es su `NumLstPrecio` (nula = precio base). Sin configuración de cliente ni parámetro: lista nula.
- **Vendible:** `Activo == "S"`, `ArticuloVenta == "S"` y `PrecioLista > 0`. Los que no cumplen se omiten (Ruling 6).
- **`BuscarArticulosAsync`:** texto vacío o de menos de 2 caracteres → lista vacía. Coincidencia por `Codigo`, `CodigoBarras` o `Nombre` que **contenga** el texto (sin distinguir mayúsculas), ordenada: coincidencia exacta de código/código de barras primero, luego por nombre; máximo 20 filas **después** de filtrar los vendibles (el precio se resuelve para los candidatos, tope razonable de 100 candidatos antes de recortar).
- **`ObtenerArticuloPorCodigoAsync`:** coincidencia **exacta** (sin distinguir mayúsculas) por `CodigoBarras` primero y luego por `Codigo`; null si no existe o no es vendible.
- **`Existencias`:** `ExistenciaArticulo.Disponible` del `CodAlmacen` de la configuración (0 si no hay fila o no hay almacén); `Inventariable = ArticuloInventario == "S"`.
- **`ObtenerRapidosAsync`:** artículos vendibles con `MostrarEnPos == "S"`, por nombre, máximo 24. Si **no hay ninguno marcado**, los 12 más vendidos de los últimos 30 días: usa `ConsultaLineasVenta.ObtenerAsync(new FiltroLineasVenta(Desde: hoy − 30 días, HastaExclusiva: hoy + 1 día, null, null, null, null))` y agrupa por `CodArticulo` sumando `CantidadBase` (desempate por código); se filtran los vendibles y se toman 12.

- [ ] **Step 1: Pruebas (fallan)** `PosCatalogoDomainTests` con números concretos: búsqueda por código, por código de barras y por nombre; coincidencia exacta primero; texto de 1 carácter → vacío; artículo inactivo, no de venta y sin precio omitidos; máximo 20 con 30 candidatos; precio de la lista del cliente (mockea `IPrecioListaDomain` devolviendo 50 para la lista 3 y 40 para nula); existencias del almacén POS (12 y 0 sin fila); `porCodigo` prefiere código de barras sobre código; `Rapidos` marcados (24 máx., por nombre) y respaldo de más vendidos (3 artículos con cantidades 10/30/20 → orden 30, 20, 10; solo los vendibles). Application y controlador: éxito/404/permiso.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~PosCatalogoDomainTests"` → falla.
- [ ] **Step 3: Implementa** los tres métodos y sus endpoints. Evita `Contains` sobre listas grandes y evaluación en cliente de tablas completas (consulta con filtro `Where` en SQL y `Take`; **la traducción a SQL se verifica en la Tarea 8**).
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(pos): busqueda de articulos, codigo exacto y articulos rapidos`.

---

### Task 6: La venta atómica, el ticket y las ventas del día

**Files:**
- Modify: `API.Domain.Core/Pos/PosDomain.cs`, `API.Application.Main/PosApplication.cs`, `API.Service.WebApi/Controllers/PosController.cs`
- Test: `API.Service.WebApi.Tests/Domain/PosVentaDomainTests.cs`, ampliar `PosApplicationTests.cs` y `PosControllerTests.cs`

**Interfaces:**
- Consumes: `PosCalculadora` (Tarea 3), `IFacturaDomain.InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas)` → entry, `IPagoRecibidoDomain.InsertarAsync(PagoRecibido, IEnumerable<PagoRecibidoFactura>, IEnumerable<PagoRecibidoMedio>)` → entry, `IEjecutorTransaccion.EjecutarAsync` (anidable, Tarea 2), `IPrecioListaDomain`, y las entidades `Factura`, `FacturaDetalle`, `PagoRecibido`, `PagoRecibidoFactura`, `PagoRecibidoMedio`, `Impuesto.Tasa`.
- Produces: `PosDomain.VenderAsync(VentaPosSolicitud, string cajero)`, `ObtenerTicketAsync(int)`, `ObtenerVentasDelDiaAsync()` (el ticket sale con `Empresa = string.Empty`; ver Ruling 8: el controlador lo completa y una prueba del controlador lo verifica con `IConfiguration` falso, incluido el valor por defecto «Sistema de Gestión»); endpoints `POST api/Pos/Venta` (`[RequierePermiso("Pos", Accion.Agregar)]`; el controlador toma `cajero = User.FindFirst(ClaimTypes.Name)?.Value`, y si es nulo o vacío responde 400 «No se pudo identificar al cajero.»), `GET api/Pos/Ticket/{facturaEntry}` y `GET api/Pos/VentasDelDia` (`Pos.Ver`; el ticket inexistente o que no es venta POS → 404 «La venta no existe.»).

**Algoritmo de `VenderAsync`** (orden obligatorio; todo lo que se valida va **antes** de numerar):
1. Configuración completa; si falta algo: «El POS no está configurado: falta <Faltantes unidos por ", ">.»
2. Cliente = `solicitud.CodigoCliente` o `ClienteDefecto`; debe existir y ser `TipoSn = 'C'` activo: «El cliente no existe o no es un cliente.»
3. Líneas: al menos una («Agrega al menos un artículo a la venta.»); cantidad > 0 en cada una («La cantidad de <CodArticulo> debe ser mayor que cero.»); los artículos repetidos se **suman** (misma `CodArticulo`, comparación sin distinguir mayúsculas; conserva el orden de primera aparición); cada artículo debe existir, estar activo, ser de venta y tener precio > 0 en la lista del cliente: «El artículo <CodArticulo> no se puede vender en el POS.»
4. Precio: `IPrecioListaDomain.ResolverAsync(cod, socio.NumLstPrecio).PrecioLista` (se ignora cualquier precio del cliente). Tasa = `Impuesto.Tasa ?? 0` del impuesto de la configuración. `PosCalculadora.Linea(...)` por línea y `Totales`.
5. Pagos: `PosCalculadora.ValidarPagos(total, solicitud.Pagos, config.FormaPagoEfectivo)`.
6. NIT: `NitDocumento.Normalizar(solicitud.Nit)` (ya existe en `Domain.Core`); si queda nulo, el `Nit` del socio; si también es nulo, `"CF"`.
7. Todo lo siguiente dentro de `_tx.EjecutarAsync(async () => { ... })` (la transacción **exterior**):
   - `Factura`: `Serie = config.SerieFactura`, `CodigoSn`, `NombreSn`, `FechaDoc = FechaEmision = hoy`, `MonedaDoc = config.MonedaDoc`, `Nit`, `CondicionPago = "CON"`, `Comentario = PosMarca.Comentario(cajero)`, `PrctjeImpuesto = tasa`, `PrctjeDesc = 0`, `TotalBruto/TotalDesc (0)/TotalImp/TotalDoc` de los totales, `EstadoDoc = "A"`, `PagoInmediato = total`. **Lee `FacturaDomain.InsertarAsync` y `FacturaCrearDTO`/el mapeo de creación para asignar exactamente los campos que éste espera** (p. ej. `Direccion` = dirección de facturación del cliente o vacía, `NumManual`, `DocType`). Líneas `FacturaDetalle`: `CodArticulo`, `Descripcion = Articulo.Nombre`, `Cantidad`, `Precio = PrecioNeto`, `PrecioBruto = PrecioNeto`, `PrctjeDesc = 0`, `CodigoImpuesto`, `Impuesto`, `TotalLinea`, `CodAlmacen = config.CodAlmacen`, `TipoObjeto = "6"`, sin `UomEntry` ni `Base*` (Ruling 5).
   - `facturaEntry = await _facturaDomain.InsertarAsync(factura, lineas)`.
   - `PagoRecibido`: `Serie = config.SeriePago`, `CodigoSn`, `NombreSn`, `FechaDoc = hoy`, `Comentario = PosMarca.Comentario(cajero)`; un `PagoRecibidoFactura { FacturaEntry, MontoAplicado = total }`; un `PagoRecibidoMedio` por cada `PagoPosRegistrado` (`CodFormaPago`, `Monto`, `Referencia`, `Banco`, `FechaCheque`). `await _pagoDomain.InsertarAsync(...)`.
   - Devuelve el ticket construido con los datos ya calculados.
8. Si **cualquier** paso lanza, nada queda (la transacción exterior revierte factura, inventario, consecutivos y pago).

**`ObtenerTicketAsync(entry)`:** lee la factura y debe ser venta POS (`PosMarca.EsVentaPos(Comentario)`) y `Cancelado != "S"` no se exige (un ticket de una venta cancelada se puede consultar, con `Total` visible); si no: lanza «La venta no existe.» (la capa de aplicación/controlador lo traduce a 404). Reconstruye líneas (`FacturaDetalle` con `Descripcion`, `Cantidad`, `Precio`, `TotalLinea`), medios (`PagoRecibidoMedio` del pago aplicado a esa factura, con el nombre de `FormaPago`), `Pagado = Σ medios` y `Cambio = 0` (el cambio no se persiste: el ticket reimpreso lo muestra en 0 y la Web lo omite si es 0), `Moneda = MonedaDoc`, `NombreSerie` de la serie. `ObtenerVentasDelDiaAsync`: facturas con marca POS y `FechaDoc` de hoy (rango del día), orden por `Entry` descendente, máximo 200.

- [ ] **Step 1: Pruebas (fallan)** `PosVentaDomainTests` con repos y dominios mockeados (`Mock<IFacturaDomain>`, `Mock<IPagoRecibidoDomain>`, `IEjecutorTransaccion` falso que ejecuta la operación y propaga excepciones) y números concretos:
  - **Feliz:** 2 × A1 (precio de lista 45) y 1 × A2 (22), impuesto 12 %, pagos `[EFE 200]` → factura con `TotalBruto 112.00`, `TotalImp 13.44`, `TotalDoc 125.44`; **la factura recibida por `InsertarAsync`** lleva `Serie = config.SerieFactura`, `CondicionPago "CON"`, `PagoInmediato 125.44`, `Comentario "Venta POS - Cajero: ana"`, `Nit` (solicitado → del socio → «CF»), 2 líneas con `CodAlmacen = config.CodAlmacen` y `UomEntry` nulo; **el pago** lleva `Serie = config.SeriePago`, un `PagoRecibidoFactura` por 125.44 y un medio EFE por 125.44 (cambio 74.56 descontado del efectivo); el ticket trae `Pagado 200.00`, `Cambio 74.56`.
  - Precio enviado por el cliente ignorado (la solicitud no lo tiene: verifica que el precio sale de `IPrecioListaDomain`).
  - Artículo repetido sumado (A1 ×1 y A1 ×2 → una línea de 3).
  - Cada validación con su mensaje y **sin llamar** a `InsertarAsync` de factura ni de pago (`Times.Never`): sin configuración, cliente inexistente, cliente proveedor, sin líneas, cantidad 0, artículo no vendible/sin precio, pagos insuficientes, cambio sin efectivo.
  - **Todo o nada:** si `IPagoRecibidoDomain.InsertarAsync` lanza, la excepción sale de `VenderAsync` (el ejecutor falso propaga; la reversa real la prueba el humo de la Tarea 8) y no se devuelve ticket.
  - `ObtenerTicketAsync` de una factura sin la marca POS → «La venta no existe.»; con la marca → líneas, medios y cajero reconstruidos. `ObtenerVentasDelDiaAsync` solo trae marcadas de hoy, más recientes primero.
  - Controlador: cajero sin claim → 400; `Pos.Agregar` requerido (reflexión sobre el atributo); 404 de ticket inexistente.
- [ ] **Step 2:** `dotnet test … --filter "FullyQualifiedName~PosVentaDomainTests"` → fallan.
- [ ] **Step 3: Implementa** `VenderAsync`, `ObtenerTicketAsync`, `ObtenerVentasDelDiaAsync`, los métodos de `PosApplication` (traduce «La venta no existe.» y «… no existe» a no-éxito con ese mensaje; el controlador responde 404 cuando el mensaje contiene «no existe» y es el endpoint de ticket) y los tres endpoints.
- [ ] **Step 4:** pruebas verdes, build 0 errores, suite completa verde.
- [ ] **Step 5: Commit**: `feat(pos): venta atomica, ticket y ventas del dia`.

---

### Task 7: Documentación de la API y cierre de la fase 1

**Files:**
- Modify: `AGENTS.md`, `docs/traspaso-2026-11-07.md`

- [ ] **Step 1:** En `AGENTS.md` (reglas de negocio) agrega una viñeta «**POS**» (módulo `Pos`/`PosConfiguracion`; `POST api/Pos/Venta` atómico con factura de contado + pago recibido; marca `Venta POS - Cajero: <usuario>` en `Factura.Comentario`; `PosConfiguracion` con `SerieFactura`, `SeriePago`, `CodAlmacen`, `CodigoImpuesto`, `ClienteDefecto`, `FormaPagoEfectivo`, `MonedaDoc`; `EjecutorTransaccion` anidable: la operación anidada corre en la transacción exterior y su aislamiento se ignora) y actualiza la línea base de la suite.
- [ ] **Step 2:** En el traspaso agrega la sección «POS (fase 1: API)» con lo hecho, los endpoints, la marca POS, el script `sql/2026-11-19-pos.sql` (debe correrse en la base real) y los pendientes (fases 2 y 3 en la Web).
- [ ] **Step 3:** Build `API.sln` 0 errores y suite completa verde (anota el conteo). 
- [ ] **Step 4: Commit**: `docs(pos): pos en AGENTS.md y traspaso`.

---

### Task 8: Prueba de humo con SQL real (API)

**Files:** solo `<scratchpad>` (nada se commitea salvo correcciones con su prueba).

- [ ] **Step 1:** Crea una base temporal `API_DB_EVID<n>` con `<scratchpad>\evid-up.ps1` (script completo, que ya incluye el bloque POS) y levanta la API con `evid-up-api.ps1` (lee antes los scripts `cv-comun.ps1` y `t12-*.ps1` para el login como `admin` y la creación de documentos por la API; **nunca** imprimas secretos). Ejecuta además `sql/2026-11-19-pos.sql` **dos veces** (idempotencia).
- [ ] **Step 2: Datos por la API:** una serie de factura POS (`CodigoObj 6`, subtipo `--`, automática, desde 1) y una serie de pagos (`CodigoObj 24`), un almacén, un impuesto IVA 12 %, el cliente CF (tipo C), formas de pago `EFE`/`TJT`, tres artículos inventariables con precio y existencias (ART-1 precio 45 con 10 en stock, ART-2 precio 22 con 5, ART-3 sin precio), uno con `CodigoBarras` y otro con `MostrarEnPos = 'S'`; `PUT api/Pos/Configuracion` con todo.
- [ ] **Step 3: Verifica** (compara cada número con el cálculo a mano): `GET Configuracion` sin faltantes; búsqueda por texto, por código y por código de barras exacto (ART-3 no aparece); rápidos con el marcado y, desmarcándolo, con los más vendidos; **venta** 2×ART-1 + 1×ART-2, IVA 12 %, pago `[EFE 200]` → total 125.44, cambio 74.56, factura con serie POS y comentario con el cajero, pago recibido aplicado por 125.44, existencias descontadas (ART-1 8, ART-2 4); venta con pago mixto `[TJT 100, EFE 50]` y cambio 24.56 (si el total fuese 125.44, ajusta); **todo o nada**: venta de 20 × ART-1 (stock insuficiente) → 400 y **no** avanza `SigNumero` de ninguna serie, no hay factura, pago ni movimiento; venta con pago que no cubre → 400 sin consumir consecutivo; cliente proveedor → 400; `Ticket/{entry}` y `VentasDelDia` devuelven la venta; ticket de una factura normal (sin marca) → 404; código de barras duplicado al guardar un artículo → error de la base traducido (anota el comportamiento real); comprobar que los reportes `ventas-por-articulo`, `utilidad-por-periodo` (costo del movimiento, venta neta sin IVA) y `facturas-por-cobrar` (la venta POS sale **pagada**, saldo 0) cuadran con la venta POS; que las traducciones a SQL de la búsqueda, los rápidos (más vendidos) y la venta no generan advertencias de EF en el log de la API; 0 errores 500.
- [ ] **Step 4:** Corrige con prueba xUnit de regresión (TDD) lo que no traduzca o no cuadre (`fix(pos): <qué>`); evidencia en `<scratchpad>\evid-pos-api-2026-11-19.txt`.
- [ ] **Step 5: Cierre:** elimina la base temporal y detén la API con `evid-down-api.ps1` (léelo antes: solo debe afectar bases `API_DB_EVID<n>`), y verifica que no quedan bases `API_DB_EVID*`. **Si vas a seguir con la Fase 2 sobre la misma base, no la elimines todavía y anota nombre y puerto en el ledger**; en el cierre del plan (Tarea 11) se elimina.

---

## Fase 2 — Web: configuración y ficha del artículo

### Task 9: Configuración del POS y campos del artículo (Web)

**Files:**
- Create: `Web.ApiClient/Dtos/Pos/*.cs` (reflejan los contratos: `PosConfiguracionDato`, `PosConfiguracionVista`, `ArticuloPos`, `VentaPosLinea`, `VentaPosPago`, `VentaPosSolicitud`, `TicketPos` y sus filas, `VentaPosDelDia`), `Web.ApiClient/Clientes/IPosApiClient.cs`, `PosApiClient.cs`, `Web.UI/Controllers/PosConfiguracionController.cs`, `Web.UI/Views/PosConfiguracion/Index.cshtml`, `Web.UI/wwwroot/js/pos-configuracion.js`
- Modify: `Web.UI/Program.cs` (registro del cliente tipado), `Web.UI/Views/Shared/_Layout.cshtml` (menú «POS» con «Configuración POS»), `Web.ApiClient/Dtos/Articulo/{ArticuloCrearDTO,ArticuloActualizarDTO,ArticuloDTO}.cs`, `Web.UI/Views/Articulos/Crear.cshtml`, `Web.UI/Views/Articulos/_Form.cshtml`, `Web.UI/wwwroot/js/articulos.js`, `Web.UI/Controllers/ArticulosController.cs`

**Interfaces:**
- Consumes: endpoints de la API de las Tareas 4 a 6 (rutas y contratos de arriba).
- Produces: `IPosApiClient` con métodos `ObtenerConfiguracionAsync`, `GuardarConfiguracionAsync(PosConfiguracionDato)`, `BuscarArticulosAsync(string texto, string? cliente)`, `ObtenerArticuloPorCodigoAsync(string codigo, string? cliente)`, `ObtenerRapidosAsync`, `VenderAsync(VentaPosSolicitud)`, `ObtenerTicketAsync(int)`, `ObtenerVentasDelDiaAsync` (todos `Task<Respuesta<T>>`, siguiendo `ApiClientBase`; mira `ReporteApiClient`/`NumeracionDocumentoApiClient` como patrón). El menú **POS** (visible con `Pos.Ver` o `PosConfiguracion.Ver`) tiene los hijos «Punto de venta» (`Pos.Ver`, lo usa la Tarea 10) y «Configuración POS» (`PosConfiguracion.Ver`).

**Qué hace:**
1. **Pantalla «Configuración POS»** (`PosConfiguracion/Index`): formulario con los siete campos: selects de series (facturas automáticas del subtipo `--` y pagos recibidos), almacén, impuesto, moneda y formas de pago activas; buscador de cliente (autocompletado de clientes, `App.autocompletar`) para el cliente por defecto. Muestra los «Faltantes» de `ObtenerConfiguracionAsync` como aviso. Guardar → `PUT` (requiere `PosConfiguracion.Actualizar`; con solo `Ver` los campos van de solo lectura). Las series se obtienen con los clientes existentes (`INumeracionDocumentoDetApiClient.ObtenerPorDocumentoAsync("6")` y `("24")`; filtra `SubTipoDoc == "--"` en facturas y `Manual != "S"`/`Bloqueado != "S"`); usa los demás clientes existentes para los catálogos (almacenes, impuestos, monedas, formas de pago). El usuario necesita `Ver` en esos catálogos (mismo comportamiento que los demás filtros del sistema).
2. **Ficha del artículo:** campo «Código de barras» (`maxlength="50"`) y casilla «Mostrar en el POS» (`MostrarEnPos` `S`/`N`) en `Crear.cshtml` y `_Form.cshtml` (edición). **El `PUT` de artículo reemplaza todas las columnas**: el formulario de edición **debe enviar siempre los valores actuales** de ambos campos (revisa cómo `ArticulosController.FormularioEditar` arma el DTO campo por campo y cómo `articulos.js` serializa con `App.recolectarFormulario`; agrega los dos campos en ambos sitios) y la casilla debe viajar como `S`/`N` (no `on`).
3. Registra el cliente en `Program.cs` como los demás (`AddHttpClient<IPosApiClient, PosApiClient>`).

- [ ] **Step 1:** DTO y cliente tipado (sin lógica). **Step 2:** configuración (controlador, vista, JS, menú). **Step 3:** campos del artículo (DTO de la Web, vistas, controlador, JS). **Step 4:** `dotnet build Web.slnx` 0 errores (`-p:BaseOutputPath="$env:TEMP\gescom-build-web\"` si hace falta) y `node --check` de `pos-configuracion.js` y `articulos.js`. **Step 5:** con la API de la Tarea 8 viva (si sigue levantada), recorre en el navegador integrado: guardar la configuración, ver los faltantes, crear un artículo con código de barras y casilla marcada, **editarlo y confirmar que no se pierden** ambos valores, y código de barras duplicado → mensaje comprensible; si no puedes verificar algo en navegador, dilo en el reporte. **Step 6: Commit** (Web, rutas explícitas): `feat(pos): configuracion del pos y campos del articulo`.

---

## Fase 3 — Web: la pantalla POS

### Task 10: Pantalla POS (buscador, lista, rápidos, cobro, ticket y ventas del día)

**Files:**
- Create: `Web.UI/Controllers/PosController.cs`, `Web.UI/Views/Pos/Index.cshtml`, `Web.UI/Views/Pos/_Ticket.cshtml`, `Web.UI/wwwroot/js/pos.js`, `Web.UI/wwwroot/css/pos.css`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml` (si hace falta incluir el CSS por vista con `@section Styles` según el patrón existente)

**Interfaces:**
- Consumes: `IPosApiClient` (Tarea 9) y los endpoints de la API.
- Produces (controlador `Pos`, proxys JSON): `GET /Pos` (vista, `[RequierePermiso("Pos", Accion.Ver)]`; pasa a la vista la configuración y sus faltantes y las formas de pago activas), `GET /Pos/Buscar?texto=&cliente=`, `GET /Pos/PorCodigo?codigo=&cliente=`, `GET /Pos/Rapidos`, `GET /Pos/Clientes?texto=` (clientes para «Cambiar», reutiliza `ISocioNegocioApiClient` filtrando `TipoSn == "C"`), `POST /Pos/Vender` (`[ValidateAntiForgeryToken]`, `[RequierePermiso("Pos", Accion.Agregar)]`), `GET /Pos/Ticket?factura=` (parcial `_Ticket`) y `GET /Pos/VentasDelDia`. Las respuestas JSON siguen `{ resultado, mensaje, dato }`; ante error de la API el controlador devuelve el `Respuesta` (nunca un 500 de la Web).

**Comportamiento de la pantalla** (spec §2; sin lógica de negocio en el navegador: precios, impuesto y totales finales los fija la API; el navegador solo **muestra** un total estimado con los precios de la búsqueda y la tasa del impuesto que le entrega el controlador):
- **Buscador** con foco permanente (`autofocus`, y al terminar cualquier acción vuelve el foco). Enter: llama `PorCodigo`; si hay coincidencia exacta agrega 1 unidad (si ya está, suma 1); si no, muestra sugerencias de `Buscar` (≥ 2 caracteres, con `debounce` de 250 ms y descarte de respuestas viejas) navegables con ↑/↓ y elegibles con Enter o clic. Un escáner que «escribe» el código y envía Enter funciona igual.
- **Lista:** `+`/`-`/`x` por línea, cantidades decimales no (v1 solo enteros ≥ 1); el artículo sin existencias suficientes muestra un aviso visual pero **no** bloquea (lo valida el cobro). Total estimado = Σ(precio × cantidad) + IVA (tasa informada), redondeado a 2 decimales como el servidor.
- **Rápidos:** botones grandes (táctiles, mín. 64 px) que agregan 1 unidad.
- **Cliente:** por defecto el de la configuración (nombre y NIT mostrados); «Cambiar» (F4) abre un buscador de clientes; el campo NIT se edita (máx. 15) y viaja en la solicitud.
- **Cobrar (F9):** modal con una fila por forma de pago activa (importe, referencia opcional; si la forma exige banco/fecha de cheque, los pide); el total viene precargado en la forma de efectivo; botones de billetes rápidos (10, 20, 50, 100, 200) que **suman** al efectivo; muestra «Pagado», «Falta» o «Cambio» en vivo. «Confirmar» deshabilitado si falta dinero. Envía `Vender` (botón deshabilitado mientras se envía). Éxito → modal/panel de **ticket** (`_Ticket`) con «Imprimir» (`window.print()` con `@media print` que oculta todo salvo el ticket, ancho 80 mm, fuente monoespaciada) y «Nueva venta» (Enter), que limpia la lista, deja el cliente por defecto y devuelve el foco al buscador. Error → SweetAlert con el mensaje y **se conserva** la venta.
- **Ventas del día:** botón que abre una lista (hora, serie-número, cliente, total, «Cancelada» si aplica) con «Reimprimir» (carga `Ticket`).
- **Atajos:** F2 foco en buscador, F4 cambiar cliente, F9 cobrar, Esc cierra modal/nueva venta (con confirmación si hay líneas). Los atajos usan `keydown` con `preventDefault` para F-teclas.
- **Sin configuración completa:** en vez de la pantalla, un aviso con la lista de faltantes y, si el usuario tiene `PosConfiguracion.Ver`, el enlace a la configuración.
- **Escapa** todo texto dinámico al insertarlo en HTML (nombres de artículo/cliente, referencias). Sin `Html.Raw` con datos. Sin `console.log`.

- [ ] **Step 1:** controlador y proxys (con manejo de errores). **Step 2:** vista, `pos.css` (pantalla completa; el ticket de 80 mm) y `_Ticket`. **Step 3:** `pos.js` por módulos internos (`Buscador`, `Lista`, `Cobro`, `Ticket`, `VentasDelDia`, `Atajos`) en un solo IIFE. **Step 4:** `dotnet build Web.slnx` 0 errores y `node --check Web.UI/wwwroot/js/pos.js`. **Step 5: recorrido en navegador** con API + base de la Tarea 8 (o una nueva base temporal creada igual): venta con lector simulado (teclear código + Enter), búsqueda por nombre, rápidos, cambio de cliente y NIT, cobro mixto con cambio, ticket e impresión (vista previa), «Ventas del día» y reimpresión, atajos, venta con stock insuficiente (error y la lista se conserva), usuario sin `Pos.Agregar` (no puede cobrar) y sin configuración (aviso). Si no puedes verificar algo en navegador, dilo. **Step 6: Commit** (Web, rutas explícitas): `feat(pos): pantalla de punto de venta`.

---

### Task 11: Documentación y cierre del módulo

**Files:**
- Modify: `API/AGENTS.md`, `API/docs/traspaso-2026-11-07.md`, `Web/AGENTS.md`

- [ ] **Step 1:** `Web/AGENTS.md`: viñeta «**POS**» (pantalla `Pos/Index` + `wwwroot/js/pos.js`, configuración en `PosConfiguracion`, cliente `IPosApiClient`, el navegador no calcula precios finales: los fija la API, atajos F2/F4/F9/Esc, ticket de 80 mm por CSS de impresión, permisos `Pos` y `PosConfiguracion`).
- [ ] **Step 2:** En el traspaso: sección «POS (fases 2 y 3: Web)» con lo hecho, la evidencia (archivos de evidencia del scratchpad), qué se verificó en navegador y qué no, y los pendientes del spec (crédito, descuentos, caja y turnos, devoluciones por notas de crédito, idempotencia, ventas en espera) más la instrucción de **correr `sql/2026-11-19-pos.sql` en la base real, asignar los módulos `Pos`/`PosConfiguracion` a las plantillas y crear la serie POS, la serie de pagos y el cliente «Consumidor final» antes de usar el POS**.
- [ ] **Step 3:** Elimina la base temporal y detén la API de humo (`evid-down-api.ps1`), y verifica que no quedan bases `API_DB_EVID*` ni la API en el puerto de humo.
- [ ] **Step 4:** Build de `API.sln` y `Web.slnx` con 0 errores; suite de la API en verde (anota el conteo final en `AGENTS.md`).
- [ ] **Step 5: Commits** (rutas explícitas; uno por repo): API `docs(pos): cierre del modulo pos`, Web `docs(pos): pos en AGENTS.md`.
