# Reportería dinámica (fases 1 a 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un motor de reportes genérico en la API (filtros, columnas, orden, agrupación con subtotales, exportación a CSV/Excel/PDF, vistas guardadas) y su pantalla genérica en la Web (tabla, gráfico, impresión), con 2 reportes de muestra: *Ventas por documento* y *Existencias y valor por almacén*.

**Architecture:** Contratos en `API.Domain.Interface/Reportes`; motor y definiciones (una clase `IReporte` por reporte, consulta EF/LINQ sin SQL libre) en `API.Domain.Core/Reportes`; casos de uso en `API.Application.Main/ReporteApplication`; exportadores detrás de `IExportadorReporte` en un proyecto nuevo `API.Infraestructure.Exportacion`; `ReporteController` (`api/Reporte`) con permiso por módulo del reporte evaluado dentro de la acción. La Web tiene `ReportesController`, un cliente tipado y **una sola pantalla** que se arma desde la definición que devuelve la API.

**Tech Stack:** .NET 7 / EF Core 7 / xUnit 2.4 + Moq 4.20 (API); .NET 8 MVC + jQuery + DataTables + Bootstrap 5 (Web); ClosedXML (Excel), QuestPDF Community (PDF), Chart.js (gráfico, empaquetado en `wwwroot/lib`).

**Spec:** `API/docs/superpowers/specs/2026-11-08-reporteria-dinamica-design.md` (commit `95a00f2`). Léelo antes de empezar: este plan es su argumento.

**Alcance de este plan.** Cubre las fases 1, 2 y 3 del spec §5 con los 2 reportes de muestra. **La fase 4 (los otros 16 reportes del catálogo §4) va en un plan aparte** (`2026-11-08-reporteria-catalogo-implementation.md`), que se escribe cuando este plan esté cerrado y el patrón de definiciones, probado con SQL real. Las definiciones nuevas solo añaden clases `IReporte` y su registro de DI.

## Global Constraints

- **Repos y ramas:** API `C:\Users\migue\source\repos\angelm0508\API` en rama `desarrollo`; Web `C:\Users\migue\source\repos\angelm0508\Web` en rama `main`. Trabajo directo en esas ramas, sin worktree (dos repos separados, convención del proyecto).
- **Git:** commits con rutas explícitas (`git add <archivo>`), **nunca** `git add -A` ni `git add .`. **Nunca** commitear `API.Service.WebApi/appsettings.json` ni `sql/crear-api-db-completo*.sql`. **No hacer `push`.** Mensajes `tipo(modulo): descripcion` en español sin tildes. Todo commit termina con la línea `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. Conserva el fin de línea de cada archivo (autocrlf activo; los avisos «LF will be replaced by CRLF» son normales).
- **Capas (spec §1):** `Application.*` depende solo de `Domain.Interface`, nunca de `Domain.Core`. Los tipos compartidos van en `Domain.Interface`. Los exportadores usan librerías de archivos y viven en `API.Infraestructure.Exportacion`, que referencia solo `Domain.Interface`.
- **Respuestas:** siempre `Respuesta<T> { Resultado, Mensaje, Dato }`. La capa Application atrapa excepciones de negocio y devuelve `Resultado = false` con el mensaje; el controlador responde 400 (404 si el reporte no existe; 403 sin permiso).
- **Límites (spec §2):** tope de **50,000** filas por consulta (el motor pide 50,001 para detectar el truncado); agrupación de **hasta 2** niveles; orden por **hasta 3** criterios; **50** vistas guardadas por usuario; gráfico PNG de **máximo 2 MB**.
- **Permisos (spec §3):** un módulo por familia (`ReportesVentas`, `ReportesCompras`, `ReportesInventario`, `ReportesCobrosPagos`, grupo `Reportes`). `Accion.Ver` permite ejecutar, exportar y gestionar las vistas propias. **El permiso se evalúa dentro de la acción** con `PermisoUsuario.Tiene(User, definicion.Modulo, Accion.Ver)` (el módulo depende del reporte, así que no sirve el atributo `[RequierePermiso]`). Los métodos públicos del controlador **no** llevan `[RequierePermiso]`.
- **Sin SQL libre:** solo se aceptan filtros, columnas, campos de orden y de agrupación declarados en la definición; lo demás es 400.
- **Dependencias nuevas (spec §1 y §7):** ClosedXML (MIT) y QuestPDF (licencia *Community*, se declara `QuestPDF.Settings.License = LicenseType.Community`), solo en `API.Infraestructure.Exportacion`; Chart.js (MIT) con versión fija en `Web.UI/wwwroot/lib/chartjs/`, **sin CDN**. Versiones fijas: `ClosedXML` 0.102.3, `QuestPDF` 2024.3.10, `chart.js` 4.4.1.
- **Descargas:** descargar `chart.umd.min.js` (≈200 KB, `https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js`) exige **confirmación del usuario en el chat antes de bajarlo** (Tarea 15). Los paquetes NuGet se restauran con `dotnet restore` como parte normal del build.
- **SQL real:** pruebas de humo **solo** en bases temporales `API_DB_EVID<n>` creadas con `sql/crear-api-db-test-completo.sql` y eliminadas al terminar. **Jamás** contra `API_DB_TEST` ni una base del usuario. La contraseña de `sa` y la clave JWT **nunca** se imprimen, copian ni commitean (si el `sa` de `appsettings.json` no autentica, detenerte y pedirle al usuario cómo ejecutar los scripts).
- **Pruebas:** TDD (prueba primero y verla fallar). xUnit + Moq en `API.Service.WebApi.Tests`. Cada tarea cierra con build sin errores y suite completa en verde. Si hay una API/Web en ejecución que bloquea `bin`, compila/prueba con `-p:BaseOutputPath="<carpeta temporal>\"` (los errores MSB3021/MSB3027 son de bloqueo de archivos, no de compilación). **Al empezar, corre la suite completa y anota la línea base** (AGENTS.md dice 2713; el último conteo conocido es 2745).
- **Rutas auxiliares:** `<scratchpad>` = `C:\Users\migue\AppData\Local\Temp\claude\9f8881082ead4f04\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad` (scripts de bases temporales, evidencias y capturas; nada de eso se commitea). `<carpeta temporal>` para `-p:BaseOutputPath` = `$env:TEMP\gescom-build-reporteria\` (con la barra final).
- **Mensajes de error de negocio:** literales en español; las pruebas los verifican. Los de este plan son definitivos.
- **Web:** **escapa siempre** el texto dinámico que se inserta en HTML desde JS (títulos de columna, valores de filas, nombres de vistas, etiquetas de grupos); usa `App.enviarJson` para POST (envía el token antiforgery). Sin `Html.Raw` con datos. Sin `console.log` de depuración.

## Rulings (decisiones tomadas al planificar)

1. **`TotalFilas`.** El spec nombra `TotalFilasEncontradas`; el plan fija `ResultadoReporte.TotalFilas` = filas que quedaron en el resultado (≤ 50,000). Es lo que necesita la pantalla; el aviso de truncado lo da `Truncado`.
2. **Tipos de contrato en `Domain.Interface`.** `SolicitudReporte`, `ResultadoReporte`, `ReporteDefinicion`, `ExportarReporteRequest` y `ReporteVistaDato` viajan sin DTO espejo por la Application y el controlador (AGENTS.md permite compartir tipos en `Domain.Interface`). Solo los tipos que no pertenecen al dominio (`ReporteResumenDTO`, `ArchivoReporteDTO`, `GuardarVistaDTO`, `ReporteVistaDTO`) están en `Application.DTO`. Para esto `API.Application.Interface` agrega una referencia a `API.Domain.Interface`.
3. **Enums en JSON como texto.** Los enums de contrato llevan `[JsonConverter(typeof(JsonStringEnumConverter))]` (no se cambia la configuración global de JSON de la API).
4. **Mensajes con título.** Los mensajes de validación que nombran un filtro, columna o campo usan su **título** visible (p. ej. «El filtro 'Cliente' es obligatorio.»); si el campo ni siquiera existe, se usa el nombre recibido.
5. **404 por reporte inexistente.** Solo el mensaje «El reporte 'x' no existe.» produce 404; los de filtros y columnas usan «no es válido para este reporte» para no confundirse con él.
6. **Fechas del filtro.** Formato `yyyy-MM-dd`. El límite superior del rango es inclusivo para el usuario y se implementa como «menor que el día siguiente» (`FechaHastaExclusiva`).
7. **CSV.** Datos planos para importar: sin fila de total cuando no hay agrupación; con agrupación se agregan columnas iniciales `Grupo 1` y `Grupo 2` y una fila por detalle (o por grupo hoja con sus subtotales si `SoloResumen`); no se escriben filas de subtotal ni total general. El spec dice «las filas tal como se ven»: aquí se afina a «las filas de datos tal como se ven».
8. **Vistas guardadas.** Se devuelve `configuracion` como objeto JSON (no como texto escapado). El máximo de 50 es por usuario en total (no por reporte). Guardar con un nombre existente (sin distinguir mayúsculas) **reemplaza** esa vista.
9. **Fase 4 aparte.** Ver «Alcance de este plan».

## Estructura de archivos

**API (rama `desarrollo`)**

| Archivo | Responsabilidad |
|---|---|
| `sql/2026-11-08-reporteria.sql` (+ bloque en `sql/crear-api-db-test-completo.sql` y en el local `sql/crear-api-db-completo.sql`) | Tabla `ReporteVista` y módulos de permiso |
| `API.Domain.Entity/Models/ReporteVista.cs`, `ApiDbTestContext.cs` | Entidad y mapeo EF |
| `API.Infraestructure.Repository/ReporteVistaRepositorio.cs` | Repositorio genérico de vistas |
| `API.Domain.Interface/Reportes/*.cs` | Contratos: enums, definición, solicitud, resultado, `IReporte`, `IMotorReportes`, `ICatalogoReportes`, `IExportadorReporte`, `IReporteVistaDomain`, `AplanadorResultado`, excepciones |
| `API.Domain.Core/Reportes/MotorReportes.cs`, `CatalogoReportes.cs`, `ReporteVistaDomain.cs` | Motor, catálogo y vistas |
| `API.Domain.Core/Reportes/Definiciones/VentasPorDocumentoReporte.cs`, `ExistenciasValorReporte.cs` | Reportes de muestra |
| `API.Application.DTO/reporte/*.cs`, `API.Application.Interface/IReporteApplication.cs`, `API.Application.Main/ReporteApplication.cs` | Casos de uso |
| `API.Infraestructure.Exportacion/` (proyecto nuevo) | `CsvExportador`, `ExcelExportador`, `PdfExportador` |
| `API.Service.WebApi/Controllers/ReporteController.cs`, `Startup.cs` | Endpoints y DI |
| `API.Service.WebApi.Tests/...` | Pruebas de cada pieza |

**Web (rama `main`)**

| Archivo | Responsabilidad |
|---|---|
| `Web.ApiClient/Dtos/Reporte/ReporteResumenDTO.cs`, `Clientes/IReporteApiClient.cs`, `ReporteApiClient.cs` | Cliente tipado |
| `Web.UI/Controllers/ReportesController.cs`, `Program.cs` | Páginas y proxys JSON/archivo |
| `Web.UI/Views/Reportes/Index.cshtml`, `Ver.cshtml`, `Views/Shared/_Layout.cshtml` | Catálogo, pantalla genérica y menú |
| `Web.UI/wwwroot/js/reportes.js`, `wwwroot/css/reportes.css`, `wwwroot/lib/chartjs/chart.umd.min.js` | Lógica de la pantalla, estilos y gráfico |

---

## Fase 1 — Núcleo

### Task 1: Base de datos de reportería (tabla `ReporteVista` y módulos de permiso)

**Files:**
- Create: `sql/2026-11-08-reporteria.sql`
- Modify: `sql/crear-api-db-test-completo.sql` (bloque al final), `sql/crear-api-db-completo.sql` (local, **no se commitea**)
- Create: `API.Domain.Entity/Models/ReporteVista.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs` (DbSet y mapeo)
- Create: `API.Infraestructure.Repository/ReporteVistaRepositorio.cs`
- Modify: `API.Service.WebApi/Startup.cs` (una línea de DI)
- Test: `API.Service.WebApi.Tests/Domain/ModeloReporteriaTests.cs`

**Interfaces:**
- Produces: entidad `API.Domain.Entity.Models.ReporteVista { int Entry; int UsuarioId; string CodigoReporte; string Nombre; string Configuracion; DateTime FechaCreacion; DateTime FechaActualizacion }`; `DbSet<ReporteVista> ReporteVistas`; `IRepositorioGenerico<ReporteVista, int>` registrado; módulos `ReportesVentas`, `ReportesCompras`, `ReportesInventario`, `ReportesCobrosPagos` (grupo `Reportes`) en `Modulo`.

- [ ] **Step 1: Línea base.** Desde `C:\Users\migue\source\repos\angelm0508\API`, `git status --short` (solo deben aparecer `appsettings.json`, los `sql/crear-api-db-completo*.sql` y el script temporal de limpieza, sin seguimiento) y `dotnet test API.Service.WebApi.Tests --nologo -v q`. Anota el número de pruebas pasando (línea base) en el ledger.

- [ ] **Step 2: Escribir la prueba que falla** `API.Service.WebApi.Tests/Domain/ModeloReporteriaTests.cs`:

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    // Valida el mapeo EF de las vistas guardadas de reportes sin tocar la base de datos (el modelo se construye en memoria).
    public class ModeloReporteriaTests
    {
        private static readonly DbContextOptions<ApiDbTestContext> _opciones =
            new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=localhost;Database=API_DB_TEST;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

        private static IModel Modelo()
        {
            using var ctx = new ApiDbTestContext(_opciones);
            return ctx.Model;
        }

        [Fact]
        public void ReporteVista_MapeaTablaClaveYLongitudes()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;
            Assert.Equal("ReporteVista", et.GetTableName());

            var clave = et.FindPrimaryKey()!;
            Assert.Equal("pk_reporte_vista", clave.GetName());
            Assert.Equal(new[] { "Entry" }, clave.Properties.Select(p => p.Name).ToArray());

            Assert.Equal(50, et.FindProperty("CodigoReporte")!.GetMaxLength());
            Assert.Equal(100, et.FindProperty("Nombre")!.GetMaxLength());
            Assert.False(et.FindProperty("Configuracion")!.IsNullable);
        }

        [Fact]
        public void ReporteVista_TieneIndiceUnicoPorUsuarioReporteYNombre()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;
            var indice = et.GetIndexes().Single(i => i.IsUnique);

            Assert.Equal(new[] { "UsuarioId", "CodigoReporte", "Nombre" }, indice.Properties.Select(p => p.Name).ToArray());
            Assert.Equal("uq_reporte_vista", indice.GetDatabaseName());
        }

        [Fact]
        public void ReporteVista_LasFechasTienenDefaultGetdate()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;

            foreach (var nombre in new[] { "FechaCreacion", "FechaActualizacion" })
            {
                var p = et.FindProperty(nombre)!;
                Assert.Equal(typeof(DateTime), p.ClrType);
                Assert.Contains("getdate", p.GetDefaultValueSql()!, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ReporteVista_LlaveForaneaAUsuarioConCascada()
        {
            var et = Modelo().FindEntityType(typeof(ReporteVista))!;
            var fk = et.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Usuario));

            Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
            Assert.Equal("fk_reporte_vista_usuario", fk.GetConstraintName());
            Assert.Equal("UsuarioId", fk.Properties.Single().Name);
        }
    }
}
```

- [ ] **Step 3: Ver que falla.** `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~ModeloReporteriaTests"` → error de compilación (`ReporteVista` no existe).

- [ ] **Step 4: Entidad** `API.Domain.Entity/Models/ReporteVista.cs`:

```csharp
using System;

namespace API.Domain.Entity.Models;

// Vista guardada de un reporte (filtros, columnas, orden, agrupacion y grafico) de un usuario. Configuracion es JSON.
public partial class ReporteVista
{
    public int Entry { get; set; }

    public int UsuarioId { get; set; }

    public string CodigoReporte { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string Configuracion { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime FechaActualizacion { get; set; }
}
```

- [ ] **Step 5: Contexto.** En `API.Domain.Entity/Models/ApiDbTestContext.cs`:
  1. Junto a `public virtual DbSet<ArticuloAlmacen> ArticuloAlmacens { get; set; }` (≈ línea 128) agrega `public virtual DbSet<ReporteVista> ReporteVistas { get; set; }`.
  2. Justo **después** del cierre del bloque `modelBuilder.Entity<ArticuloAlmacen>(entity => { ... });` (≈ línea 1641 en adelante) agrega:

```csharp
        modelBuilder.Entity<ReporteVista>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_reporte_vista");

            entity.ToTable("ReporteVista");

            entity.HasIndex(e => new { e.UsuarioId, e.CodigoReporte, e.Nombre }, "uq_reporte_vista").IsUnique();

            entity.Property(e => e.CodigoReporte).HasMaxLength(50);
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.FechaCreacion)
                .HasColumnType("datetime")
                .HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FechaActualizacion)
                .HasColumnType("datetime")
                .HasDefaultValueSql("(getdate())");

            entity.HasOne<Usuario>().WithMany()
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_reporte_vista_usuario");
        });
```

- [ ] **Step 6: Repositorio** `API.Infraestructure.Repository/ReporteVistaRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class ReporteVistaRepositorio : RepositorioGenericoEfCore<ReporteVista, int>
    {
        public ReporteVistaRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

- [ ] **Step 7: DI.** En `API.Service.WebApi/Startup.cs`, justo después de la línea `services.AddTransient<IRepositorioGenerico<ArticuloAlmacen, (string CodArticulo, string CodAlmacen)>, ArticuloAlmacenRepositorio>();` agrega:

```csharp
            services.AddTransient<IRepositorioGenerico<ReporteVista, int>, ReporteVistaRepositorio>();
```

- [ ] **Step 8: Script SQL** `sql/2026-11-08-reporteria.sql` (idempotente, ASCII):

```sql
-- Reporteria dinamica: vistas guardadas por usuario y modulos de permiso de los reportes.
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

IF OBJECT_ID('dbo.ReporteVista', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReporteVista (
        Entry              int identity(1,1) NOT NULL,
        UsuarioId          int           NOT NULL,
        CodigoReporte      nvarchar(50)  NOT NULL,
        Nombre             nvarchar(100) NOT NULL,
        Configuracion      nvarchar(max) NOT NULL,
        FechaCreacion      datetime      NOT NULL CONSTRAINT DF_ReporteVista_FechaCreacion DEFAULT (GETDATE()),
        FechaActualizacion datetime      NOT NULL CONSTRAINT DF_ReporteVista_FechaActualizacion DEFAULT (GETDATE()),
        CONSTRAINT pk_reporte_vista PRIMARY KEY (Entry),
        CONSTRAINT fk_reporte_vista_usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario(ID) ON DELETE CASCADE,
        CONSTRAINT CK_ReporteVista_Configuracion CHECK (ISJSON(Configuracion) = 1),
        CONSTRAINT uq_reporte_vista UNIQUE (UsuarioId, CodigoReporte, Nombre)
    );
END

-- Catalogo de permisos: un modulo por familia de reportes
MERGE dbo.Modulo AS target
USING (VALUES
    ('ReportesVentas',      'Reportes de ventas',         'Reportes'),
    ('ReportesCompras',     'Reportes de compras',        'Reportes'),
    ('ReportesInventario',  'Reportes de inventario',     'Reportes'),
    ('ReportesCobrosPagos', 'Reportes de cobros y pagos', 'Reportes')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT name FROM sys.tables WHERE name = 'ReporteVista';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Grupo = 'Reportes' ORDER BY Codigo;
```

- [ ] **Step 9: Mismo bloque en los scripts completos.** Desde la carpeta del repo API (bash):

```bash
for f in sql/crear-api-db-test-completo.sql sql/crear-api-db-completo.sql; do
  { printf '\n-- ===== Reporteria dinamica (sql/2026-11-08-reporteria.sql) =====\nGO\n'; cat sql/2026-11-08-reporteria.sql; printf '\nGO\n'; } >> "$f"
done
tail -n 8 sql/crear-api-db-test-completo.sql
```

- [ ] **Step 10: Scripts auxiliares de bases temporales** (en la carpeta de scratchpad de la sesión, **no** en el repo). Crea `<scratchpad>\evid-up.ps1`:

```powershell
param([Parameter(Mandatory)][string]$Base)
if ($Base -notmatch '^API_DB_EVID\d+$') { throw 'Solo se permiten bases API_DB_EVID<n>.' }
$cs = (Get-Content 'C:\Users\migue\source\repos\angelm0508\API\API.Service.WebApi\appsettings.json' -Raw | ConvertFrom-Json).ConnectionStrings.API_DB
$env:SQLCMDPASSWORD = [regex]::Match($cs, 'Password=([^;]*)').Groups[1].Value
$sql = (Get-Content 'C:\Users\migue\source\repos\angelm0508\API\sql\crear-api-db-test-completo.sql' -Raw -Encoding UTF8) -replace 'API_DB_TEST', $Base
$tmp = Join-Path $env:TEMP "$Base.sql"
[IO.File]::WriteAllText($tmp, $sql, (New-Object Text.UTF8Encoding($true)))
sqlcmd -S localhost -U sa -C -b -f 65001 -i $tmp
$codigo = $LASTEXITCODE
Remove-Item $tmp -ErrorAction SilentlyContinue
exit $codigo
```

y `<scratchpad>\evid-down.ps1`:

```powershell
param([Parameter(Mandatory)][string]$Base)
if ($Base -notmatch '^API_DB_EVID\d+$') { throw 'Solo se permiten bases API_DB_EVID<n>.' }
$cs = (Get-Content 'C:\Users\migue\source\repos\angelm0508\API\API.Service.WebApi\appsettings.json' -Raw | ConvertFrom-Json).ConnectionStrings.API_DB
$env:SQLCMDPASSWORD = [regex]::Match($cs, 'Password=([^;]*)').Groups[1].Value
sqlcmd -S localhost -U sa -C -b -Q "IF DB_ID('$Base') IS NOT NULL BEGIN ALTER DATABASE [$Base] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Base]; END"
```

Si `sqlcmd` no autentica con `sa`, detente y pide al usuario cómo ejecutar los scripts (regla de SQL real de las Global Constraints). No imprimas la contraseña.

- [ ] **Step 11: Verificar el script contra una base temporal.**

```powershell
& '<scratchpad>\evid-up.ps1' API_DB_EVID1
$env:SQLCMDPASSWORD = ([regex]::Match(((Get-Content API.Service.WebApi\appsettings.json -Raw | ConvertFrom-Json).ConnectionStrings.API_DB), 'Password=([^;]*)').Groups[1].Value)
sqlcmd -S localhost -U sa -C -b -d API_DB_EVID1 -f 65001 -i sql\2026-11-08-reporteria.sql   # segunda corrida: debe ser idempotente
sqlcmd -S localhost -U sa -C -d API_DB_EVID1 -h -1 -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name='ReporteVista'; SELECT COUNT(*) FROM dbo.Modulo WHERE Grupo='Reportes'"
& '<scratchpad>\evid-down.ps1' API_DB_EVID1
```
Esperado: `1` y `4`, sin errores en ninguna corrida. Al final no debe quedar ninguna base `API_DB_EVID*` (`sqlcmd ... -Q "SELECT name FROM sys.databases WHERE name LIKE 'API_DB_EVID%'"` devuelve vacío).

- [ ] **Step 12: Ver que pasa.** `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~ModeloReporteriaTests"` → 4 pasando. Luego la suite completa: línea base + 4, 0 fallando; `dotnet build API.sln` sin errores.

- [ ] **Step 13: Commit.**

```bash
git add sql/2026-11-08-reporteria.sql sql/crear-api-db-test-completo.sql API.Domain.Entity/Models/ReporteVista.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Infraestructure.Repository/ReporteVistaRepositorio.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ModeloReporteriaTests.cs
git commit -m "feat(reporteria): tabla ReporteVista y modulos de permiso de reportes

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Contratos de reportería en `Domain.Interface`

**Files:**
- Create (todos en `API.Domain.Interface/Reportes/`): `TiposReporte.cs`, `SolicitudReporte.cs`, `ResultadoReporte.cs`, `FiltrosReporte.cs`, `Contratos.cs`, `ExcepcionesReporte.cs`
- Test: `API.Service.WebApi.Tests/Domain/ContratosReporteTests.cs`, `API.Service.WebApi.Tests/Domain/FiltrosReporteTests.cs`

**Interfaces:**
- Produces (namespace `API.Domain.Interface.Reportes`; lo consumen todas las tareas siguientes, copiar los nombres tal cual):
  - Enums (serializan como texto): `TipoColumna { Texto, Entero, Cantidad, Dinero, Fecha }`, `Totalizacion { Ninguna, Suma, Promedio, Conteo }`, `TipoFiltro { Fecha, RangoFechas, Texto, Numero, Lista, Socio, Articulo, Almacen }`, `ParteFecha { Dia, Mes, Anio }`.
  - Records: `OpcionFiltro(string Valor, string Titulo)`; `ColumnaReporte(string Campo, string Titulo, TipoColumna Tipo, bool Agrupable = false, Totalizacion Totaliza = Totalizacion.Ninguna, bool VisiblePorDefecto = true)`; `FiltroReporte(string Campo, string Titulo, TipoFiltro Tipo, bool Obligatorio = false, IReadOnlyList<OpcionFiltro>? Opciones = null, string? ValorPorDefecto = null, string? SubTipo = null)` (`SubTipo` en filtros `Socio`: `"C"` cliente, `"P"` proveedor, `null` ambos); `ReporteDefinicion(string Codigo, string Nombre, string Descripcion, string Familia, string Modulo, IReadOnlyList<FiltroReporte> Filtros, IReadOnlyList<ColumnaReporte> Columnas)`.
  - Clases de solicitud: `FiltroValor { string? Valor; string? Desde; string? Hasta }`, `OrdenReporte { string Campo; bool Descendente }`, `AgrupacionReporte { string Campo; ParteFecha? Parte }`, `SolicitudReporte { Dictionary<string, FiltroValor> Filtros; List<string> Columnas; List<OrdenReporte> Orden; List<AgrupacionReporte> Agrupar; bool SoloResumen }`, `ExportarReporteRequest { SolicitudReporte Solicitud; string? GraficoPng }`.
  - Resultado: `FilaReporte : Dictionary<string, object?>`; `GrupoReporte { string Campo; string Titulo; string Clave; string Etiqueta; int Conteo; Dictionary<string, decimal?> Subtotales; List<GrupoReporte> Hijos; List<FilaReporte> Filas }`; `ResultadoReporte { List<ColumnaReporte> Columnas; List<FilaReporte> Filas; List<GrupoReporte> Grupos; Dictionary<string, decimal?> Totales; bool Truncado; int TotalFilas; List<string> Avisos }`.
  - `FiltrosReporte` (acceso tipado): `Texto(campo) → string?`, `Numero(campo) → decimal?`, `Fecha(campo) → DateTime?`, `FechaDesde(campo) → DateTime?`, `FechaHastaExclusiva(campo) → DateTime?`, estático `ParseFecha(string?) → DateTime?`, `FiltrosReporte.Vacio`, constructor `FiltrosReporte(IReadOnlyDictionary<string, FiltroValor> valores)`.
  - Interfaces: `IReporte { ReporteDefinicion Definicion { get; } Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite); }`; `IMotorReportes { Task<ResultadoReporte> EjecutarAsync(IReporte reporte, SolicitudReporte solicitud); }`; `ICatalogoReportes { IReadOnlyList<IReporte> Todos { get; } IReporte? Buscar(string codigo); }`; `IExportadorReporte { string Formato { get; } string ContentType { get; } string Extension { get; } byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto); }`; `record ContextoExportacion(string Empresa, string Usuario, DateTime Fecha, string FiltrosTexto, byte[]? GraficoPng)`; `record ReporteVistaDato(int Entry, string Nombre, string Configuracion, DateTime FechaActualizacion)`; `IReporteVistaDomain { Task<IReadOnlyList<ReporteVistaDato>> ObtenerAsync(int usuarioId, string codigoReporte); Task<ReporteVistaDato> GuardarAsync(int usuarioId, string codigoReporte, string nombre, string configuracionJson); Task<bool> EliminarAsync(int usuarioId, string codigoReporte, int entry); }`.
  - Excepciones: `ReporteInvalidoException : Exception`, `ReporteNoExisteException : Exception` (mensaje «El reporte '{codigo}' no existe.»).

- [ ] **Step 1: Pruebas que fallan.**

`API.Service.WebApi.Tests/Domain/FiltrosReporteTests.cs`:

```csharp
using API.Domain.Interface.Reportes;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class FiltrosReporteTests
    {
        private static FiltrosReporte Con(string campo, FiltroValor valor) =>
            new(new Dictionary<string, FiltroValor> { [campo] = valor });

        [Fact]
        public void Texto_RecortaYDevuelveNuloSiEstaVacioOAusente()
        {
            Assert.Equal("C1", Con("c", new FiltroValor { Valor = "  C1 " }).Texto("c"));
            Assert.Null(Con("c", new FiltroValor { Valor = "   " }).Texto("c"));
            Assert.Null(FiltrosReporte.Vacio.Texto("c"));
        }

        [Fact]
        public void Numero_UsaCulturaInvarianteYDevuelveNuloSiNoEsNumero()
        {
            Assert.Equal(12.5m, Con("n", new FiltroValor { Valor = "12.5" }).Numero("n"));
            Assert.Null(Con("n", new FiltroValor { Valor = "abc" }).Numero("n"));
            Assert.Null(FiltrosReporte.Vacio.Numero("n"));
        }

        [Fact]
        public void Fecha_AceptaSoloAnioMesDia()
        {
            Assert.Equal(new DateTime(2026, 1, 31), Con("f", new FiltroValor { Valor = "2026-01-31" }).Fecha("f"));
            Assert.Null(Con("f", new FiltroValor { Valor = "31/01/2026" }).Fecha("f"));
            Assert.Null(FiltrosReporte.ParseFecha(null));
        }

        [Fact]
        public void Rango_DesdeEsElDiaYHastaEsExclusivoElDiaSiguiente()
        {
            var f = Con("r", new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" });

            Assert.Equal(new DateTime(2026, 1, 1), f.FechaDesde("r"));
            Assert.Equal(new DateTime(2026, 2, 1), f.FechaHastaExclusiva("r"));
        }

        [Fact]
        public void Rango_SinExtremosDevuelveNulo()
        {
            var f = Con("r", new FiltroValor());

            Assert.Null(f.FechaDesde("r"));
            Assert.Null(f.FechaHastaExclusiva("r"));
            Assert.Null(FiltrosReporte.Vacio.FechaDesde("r"));
        }
    }
}
```

`API.Service.WebApi.Tests/Domain/ContratosReporteTests.cs`:

```csharp
using System.Text.Json;
using API.Domain.Interface.Reportes;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ContratosReporteTests
    {
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        [Fact]
        public void LosEnumsSeSerializanComoTexto()
        {
            var json = JsonSerializer.Serialize(new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma), Web);

            Assert.Contains("\"tipo\":\"Dinero\"", json);
            Assert.Contains("\"totaliza\":\"Suma\"", json);
        }

        [Fact]
        public void LaSolicitudSeLeeDesdeJsonConEnumsComoTexto()
        {
            const string json = """
                { "filtros": { "fechas": { "desde": "2026-01-01", "hasta": "2026-01-31" }, "cliente": { "valor": "C1" } },
                  "columnas": ["fecha","total"],
                  "orden": [ { "campo": "total", "descendente": true } ],
                  "agrupar": [ { "campo": "fecha", "parte": "Mes" } ],
                  "soloResumen": true }
                """;

            var s = JsonSerializer.Deserialize<SolicitudReporte>(json, Web)!;

            Assert.Equal("C1", s.Filtros["cliente"].Valor);
            Assert.Equal("2026-01-31", s.Filtros["fechas"].Hasta);
            Assert.Equal(new[] { "fecha", "total" }, s.Columnas);
            Assert.True(s.Orden[0].Descendente);
            Assert.Equal(ParteFecha.Mes, s.Agrupar[0].Parte);
            Assert.True(s.SoloResumen);
        }

        [Fact]
        public void LaSolicitudVaciaTieneColeccionesNoNulas()
        {
            var s = new SolicitudReporte();

            Assert.Empty(s.Filtros);
            Assert.Empty(s.Columnas);
            Assert.Empty(s.Orden);
            Assert.Empty(s.Agrupar);
            Assert.False(s.SoloResumen);
        }

        [Fact]
        public void LaFilaSerializaSusValoresPorTipoEnTiempoDeEjecucion()
        {
            var fila = new FilaReporte { ["fecha"] = new DateTime(2026, 1, 5), ["total"] = 12.5m, ["cliente"] = "C1", ["nota"] = null };

            var json = JsonSerializer.Serialize(fila, Web);

            Assert.Contains("\"total\":12.5", json);
            Assert.Contains("\"cliente\":\"C1\"", json);
            Assert.Contains("\"nota\":null", json);
        }

        [Fact]
        public void ReporteNoExiste_LlevaElMensajeConElCodigo()
        {
            Assert.Equal("El reporte 'x' no existe.", new ReporteNoExisteException("x").Message);
        }
    }
}
```

- [ ] **Step 2: Ver que fallan** (no compila: faltan los tipos): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~FiltrosReporteTests|FullyQualifiedName~ContratosReporteTests"`.

- [ ] **Step 3: Implementar.** `API.Domain.Interface/Reportes/TiposReporte.cs`:

```csharp
using System.Text.Json.Serialization;

namespace API.Domain.Interface.Reportes
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoColumna { Texto, Entero, Cantidad, Dinero, Fecha }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Totalizacion { Ninguna, Suma, Promedio, Conteo }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoFiltro { Fecha, RangoFechas, Texto, Numero, Lista, Socio, Articulo, Almacen }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ParteFecha { Dia, Mes, Anio }

    public sealed record OpcionFiltro(string Valor, string Titulo);

    public sealed record ColumnaReporte(
        string Campo,
        string Titulo,
        TipoColumna Tipo,
        bool Agrupable = false,
        Totalizacion Totaliza = Totalizacion.Ninguna,
        bool VisiblePorDefecto = true);

    /// <summary>SubTipo: en filtros de tipo Socio, "C" = solo clientes, "P" = solo proveedores, null = ambos.</summary>
    public sealed record FiltroReporte(
        string Campo,
        string Titulo,
        TipoFiltro Tipo,
        bool Obligatorio = false,
        IReadOnlyList<OpcionFiltro>? Opciones = null,
        string? ValorPorDefecto = null,
        string? SubTipo = null);

    public sealed record ReporteDefinicion(
        string Codigo,
        string Nombre,
        string Descripcion,
        string Familia,
        string Modulo,
        IReadOnlyList<FiltroReporte> Filtros,
        IReadOnlyList<ColumnaReporte> Columnas);
}
```

`SolicitudReporte.cs`:

```csharp
namespace API.Domain.Interface.Reportes
{
    /// <summary>Valor de un filtro. Valor sirve a los filtros simples; Desde y Hasta al de rango de fechas (yyyy-MM-dd).</summary>
    public sealed class FiltroValor
    {
        public string? Valor { get; set; }
        public string? Desde { get; set; }
        public string? Hasta { get; set; }
    }

    public sealed class OrdenReporte
    {
        public string Campo { get; set; } = "";
        public bool Descendente { get; set; }
    }

    public sealed class AgrupacionReporte
    {
        public string Campo { get; set; } = "";
        /// <summary>Solo para columnas de fecha; sin valor se agrupa por día.</summary>
        public ParteFecha? Parte { get; set; }
    }

    public sealed class SolicitudReporte
    {
        public Dictionary<string, FiltroValor> Filtros { get; set; } = new();
        public List<string> Columnas { get; set; } = new();
        public List<OrdenReporte> Orden { get; set; } = new();
        public List<AgrupacionReporte> Agrupar { get; set; } = new();
        public bool SoloResumen { get; set; }
    }

    public sealed class ExportarReporteRequest
    {
        public SolicitudReporte Solicitud { get; set; } = new();
        /// <summary>Imagen PNG del gráfico en base64 (con o sin el prefijo data:image/png;base64,). Máximo 2 MB.</summary>
        public string? GraficoPng { get; set; }
    }
}
```

`ResultadoReporte.cs`:

```csharp
namespace API.Domain.Interface.Reportes
{
    /// <summary>Una fila de un reporte: campo → valor. Las filas de un reporte dinámico no tienen forma fija.</summary>
    public sealed class FilaReporte : Dictionary<string, object?> { }

    public sealed class GrupoReporte
    {
        public string Campo { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Clave { get; set; } = "";
        public string Etiqueta { get; set; } = "";
        public int Conteo { get; set; }
        public Dictionary<string, decimal?> Subtotales { get; set; } = new();
        public List<GrupoReporte> Hijos { get; set; } = new();
        public List<FilaReporte> Filas { get; set; } = new();
    }

    public sealed class ResultadoReporte
    {
        public List<ColumnaReporte> Columnas { get; set; } = new();
        /// <summary>Filas de detalle cuando no hay agrupación.</summary>
        public List<FilaReporte> Filas { get; set; } = new();
        /// <summary>Árbol de grupos cuando hay agrupación (las filas de detalle cuelgan de las hojas, salvo SoloResumen).</summary>
        public List<GrupoReporte> Grupos { get; set; } = new();
        public Dictionary<string, decimal?> Totales { get; set; } = new();
        public bool Truncado { get; set; }
        /// <summary>Filas que quedaron en el resultado (como máximo el tope del motor).</summary>
        public int TotalFilas { get; set; }
        public List<string> Avisos { get; set; } = new();
    }
}
```

`FiltrosReporte.cs`:

```csharp
using System.Globalization;

namespace API.Domain.Interface.Reportes
{
    /// <summary>Acceso tipado a los valores de los filtros ya validados por el motor.</summary>
    public sealed class FiltrosReporte
    {
        private readonly IReadOnlyDictionary<string, FiltroValor> _valores;

        public FiltrosReporte(IReadOnlyDictionary<string, FiltroValor> valores)
        {
            _valores = valores;
        }

        public static FiltrosReporte Vacio { get; } = new(new Dictionary<string, FiltroValor>());

        public string? Texto(string campo)
        {
            var v = Obtener(campo)?.Valor;
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }

        public decimal? Numero(string campo) =>
            decimal.TryParse(Obtener(campo)?.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

        public DateTime? Fecha(string campo) => ParseFecha(Obtener(campo)?.Valor);

        public DateTime? FechaDesde(string campo) => ParseFecha(Obtener(campo)?.Desde);

        /// <summary>
        /// Límite superior EXCLUSIVO de un rango: las 00:00 del día siguiente a "hasta". Para el usuario el rango es
        /// inclusivo en ambos extremos; en las consultas se usa «menor que» este valor.
        /// </summary>
        public DateTime? FechaHastaExclusiva(string campo) => ParseFecha(Obtener(campo)?.Hasta)?.AddDays(1);

        public static DateTime? ParseFecha(string? texto) =>
            DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f.Date : null;

        private FiltroValor? Obtener(string campo) => _valores.TryGetValue(campo, out var v) ? v : null;
    }
}
```

`Contratos.cs`:

```csharp
namespace API.Domain.Interface.Reportes
{
    public interface IReporte
    {
        ReporteDefinicion Definicion { get; }

        /// <summary>
        /// Devuelve a lo sumo "limite" filas, en el orden natural del reporte. El motor pide el tope + 1 para detectar el
        /// truncado. Cada fila trae TODAS las columnas de la definición (clave = Campo).
        /// </summary>
        Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite);
    }

    public interface IMotorReportes
    {
        Task<ResultadoReporte> EjecutarAsync(IReporte reporte, SolicitudReporte solicitud);
    }

    public interface ICatalogoReportes
    {
        IReadOnlyList<IReporte> Todos { get; }
        IReporte? Buscar(string codigo);
    }

    public sealed record ContextoExportacion(string Empresa, string Usuario, DateTime Fecha, string FiltrosTexto, byte[]? GraficoPng);

    public interface IExportadorReporte
    {
        /// <summary>"xlsx", "pdf" o "csv".</summary>
        string Formato { get; }
        string ContentType { get; }
        string Extension { get; }
        byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto);
    }

    public sealed record ReporteVistaDato(int Entry, string Nombre, string Configuracion, DateTime FechaActualizacion);

    public interface IReporteVistaDomain
    {
        Task<IReadOnlyList<ReporteVistaDato>> ObtenerAsync(int usuarioId, string codigoReporte);

        /// <summary>Crea la vista o, si el usuario ya tiene una con ese nombre para ese reporte, la reemplaza.</summary>
        Task<ReporteVistaDato> GuardarAsync(int usuarioId, string codigoReporte, string nombre, string configuracionJson);

        /// <summary>false si la vista no existe o no es del usuario.</summary>
        Task<bool> EliminarAsync(int usuarioId, string codigoReporte, int entry);
    }
}
```

`ExcepcionesReporte.cs`:

```csharp
namespace API.Domain.Interface.Reportes
{
    /// <summary>Solicitud de reporte inválida (filtro, columna, orden o agrupación no permitidos, vista inválida, etc.).</summary>
    public sealed class ReporteInvalidoException : Exception
    {
        public ReporteInvalidoException(string mensaje) : base(mensaje) { }
    }

    public sealed class ReporteNoExisteException : Exception
    {
        public ReporteNoExisteException(string codigo) : base($"El reporte '{codigo}' no existe.") { }
    }
}
```

- [ ] **Step 4: Ver que pasan.** El mismo `dotnet test ... --filter` → 10 pasando. Suite completa en verde; `dotnet build API.sln` sin errores.

- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Interface/Reportes API.Service.WebApi.Tests/Domain/ContratosReporteTests.cs API.Service.WebApi.Tests/Domain/FiltrosReporteTests.cs
git commit -m "feat(reporteria): contratos del motor de reportes en Domain.Interface

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Motor de reportes — validación, columnas, orden, totales y truncado

**Files:**
- Create: `API.Domain.Core/Reportes/MotorReportes.cs`
- Create: `API.Service.WebApi.Tests/TestHelpers/ReporteFalso.cs`
- Test: `API.Service.WebApi.Tests/Domain/MotorReportesTests.cs`

**Interfaces:**
- Consumes: todo lo de la Tarea 2.
- Produces: `API.Domain.Core.Reportes.MotorReportes : IMotorReportes` con constantes públicas `LimiteFilas = 50000`, `MaxCriteriosOrden = 3`, `MaxNivelesAgrupacion = 2`; clase interna `ComparadorValores` (`Instancia`, estático `EsNumero(object)`). Los mensajes de validación quedan fijos (los verifican las pruebas, ver Rulings 4 y 5). `ReporteFalso` (helper de pruebas) con `FiltrosRecibidos`, `LimiteRecibido` y el estático `Fila(params (string Campo, object? Valor)[] pares)`.

- [ ] **Step 1: Helper de pruebas** `API.Service.WebApi.Tests/TestHelpers/ReporteFalso.cs`:

```csharp
using API.Domain.Interface.Reportes;

namespace API.Service.WebApi.Tests.TestHelpers
{
    /// <summary>Reporte de prueba: devuelve las filas dadas (respetando el límite) y registra lo que recibió.</summary>
    internal sealed class ReporteFalso : IReporte
    {
        private readonly IEnumerable<FilaReporte> _filas;

        public ReporteFalso(ReporteDefinicion definicion, IEnumerable<FilaReporte> filas)
        {
            Definicion = definicion;
            _filas = filas;
        }

        public ReporteDefinicion Definicion { get; }
        public FiltrosReporte? FiltrosRecibidos { get; private set; }
        public int? LimiteRecibido { get; private set; }

        public Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            FiltrosRecibidos = filtros;
            LimiteRecibido = limite;
            return Task.FromResult<IReadOnlyList<FilaReporte>>(_filas.Take(limite).ToList());
        }

        public static FilaReporte Fila(params (string Campo, object? Valor)[] pares)
        {
            var fila = new FilaReporte();
            foreach (var (campo, valor) in pares) fila[campo] = valor;
            return fila;
        }
    }
}
```

- [ ] **Step 2: Pruebas que fallan** `API.Service.WebApi.Tests/Domain/MotorReportesTests.cs`:

```csharp
using API.Domain.Core.Reportes;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class MotorReportesTests
    {
        internal static readonly ReporteDefinicion Def = new(
            Codigo: "t01", Nombre: "Prueba", Descripcion: "d", Familia: "Pruebas", Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, Obligatorio: true),
                new FiltroReporte("tipo", "Tipo", TipoFiltro.Lista, Opciones: new[] { new OpcionFiltro("FAC", "Factura"), new OpcionFiltro("NC", "Nota de crédito") }),
                new FiltroReporte("serie", "Serie", TipoFiltro.Numero)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("cliente", "Cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("cantidad", "Cantidad", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("promedio", "Promedio", TipoColumna.Dinero, Totaliza: Totalizacion.Promedio),
                new ColumnaReporte("cuenta", "Cuenta", TipoColumna.Entero, Totaliza: Totalizacion.Conteo),
                new ColumnaReporte("nota", "Nota", TipoColumna.Texto, VisiblePorDefecto: false)
            });

        internal static FilaReporte F(string cliente, DateTime fecha, decimal cantidad, decimal importe, decimal promedio = 0m, string moneda = "QTZ", string? nota = null) =>
            ReporteFalso.Fila(("fecha", fecha), ("cliente", cliente), ("moneda", moneda), ("cantidad", cantidad), ("importe", importe),
                ("promedio", promedio), ("cuenta", 1), ("nota", nota));

        internal static SolicitudReporte Req(Action<SolicitudReporte>? ajustar = null)
        {
            var s = new SolicitudReporte();
            s.Filtros["cliente"] = new FiltroValor { Valor = "C1" };
            ajustar?.Invoke(s);
            return s;
        }

        private static readonly DateTime D1 = new(2026, 1, 10);
        private static readonly DateTime D2 = new(2026, 1, 20);
        private static readonly DateTime D3 = new(2026, 2, 5);

        private readonly MotorReportes _motor = new();

        private static async Task<string> MensajeAsync(Task tarea) =>
            (await Assert.ThrowsAsync<ReporteInvalidoException>(() => tarea)).Message;

        // ---------- filtros ----------

        [Fact]
        public async Task Ejecutar_FiltroDesconocido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["zzz"] = new FiltroValor { Valor = "1" });

            Assert.Equal("El filtro 'zzz' no es válido para este reporte.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_FiltroObligatorioAusente_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("El filtro 'Cliente' es obligatorio.", await MensajeAsync(_motor.EjecutarAsync(r, new SolicitudReporte())));
        }

        [Fact]
        public async Task Ejecutar_FiltroObligatorioVacio_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["cliente"] = new FiltroValor { Valor = "   " });

            Assert.Equal("El filtro 'Cliente' es obligatorio.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_FechaConFormatoInvalido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["fechas"] = new FiltroValor { Desde = "01/02/2026" });

            Assert.Equal("El valor del filtro 'Fechas' no es válido.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_RangoInvertido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["fechas"] = new FiltroValor { Desde = "2026-02-01", Hasta = "2026-01-01" });

            Assert.Equal("El rango del filtro 'Fechas' no es válido: la fecha inicial es posterior a la final.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_ListaFueraDeLasOpciones_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["tipo"] = new FiltroValor { Valor = "XX" });

            Assert.Equal("El valor del filtro 'Tipo' no es válido.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_NumeroInvalido_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["serie"] = new FiltroValor { Valor = "abc" });

            Assert.Equal("El valor del filtro 'Serie' no es válido.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_FiltroOpcionalVacio_SeIgnora()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Filtros["tipo"] = new FiltroValor { Valor = "" });

            await _motor.EjecutarAsync(r, s);

            Assert.Null(r.FiltrosRecibidos!.Texto("tipo"));
        }

        [Fact]
        public async Task Ejecutar_PasaAlReporteLosFiltrosTipadosYElLimiteMasUno()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x =>
            {
                x.Filtros["fechas"] = new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" };
                x.Filtros["serie"] = new FiltroValor { Valor = "7" };
            });

            await _motor.EjecutarAsync(r, s);

            Assert.Equal(MotorReportes.LimiteFilas + 1, r.LimiteRecibido);
            Assert.Equal("C1", r.FiltrosRecibidos!.Texto("cliente"));
            Assert.Equal(new DateTime(2026, 1, 1), r.FiltrosRecibidos.FechaDesde("fechas"));
            Assert.Equal(new DateTime(2026, 2, 1), r.FiltrosRecibidos.FechaHastaExclusiva("fechas"));
            Assert.Equal(7m, r.FiltrosRecibidos.Numero("serie"));
        }

        // ---------- columnas ----------

        [Fact]
        public async Task Ejecutar_SinColumnasPedidas_UsaLasVisiblesPorDefectoEnElOrdenDeLaDefinicion()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 1, 10) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Equal(new[] { "fecha", "cliente", "cantidad", "importe", "promedio", "cuenta" }, res.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(new[] { "fecha", "cliente", "cantidad", "importe", "promedio", "cuenta" }, res.Filas[0].Keys.ToArray());
        }

        [Fact]
        public async Task Ejecutar_ColumnasPedidas_RespetanElOrdenPedidoYSoloEllas()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 1, 10, nota: "x") });

            var res = await _motor.EjecutarAsync(r, Req(s => s.Columnas.AddRange(new[] { "importe", "fecha", "nota" })));

            Assert.Equal(new[] { "importe", "fecha", "nota" }, res.Columnas.Select(c => c.Campo).ToArray());
            Assert.Equal(new[] { "importe", "fecha", "nota" }, res.Filas[0].Keys.ToArray());
            Assert.Equal("x", res.Filas[0]["nota"]);
        }

        [Fact]
        public async Task Ejecutar_ColumnaDesconocida_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("La columna 'zzz' no es válida para este reporte.",
                await MensajeAsync(_motor.EjecutarAsync(r, Req(s => s.Columnas.Add("zzz")))));
        }

        // ---------- orden ----------

        [Fact]
        public async Task Ejecutar_OrdenDescendentePorImporte()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 1, 10), F("C1", D2, 1, 30), F("C1", D3, 1, 20) });

            var res = await _motor.EjecutarAsync(r, Req(s => s.Orden.Add(new OrdenReporte { Campo = "importe", Descendente = true })));

            Assert.Equal(new object?[] { 30m, 20m, 10m }, res.Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenMultiple_UsaElSegundoCriterioParaDesempatar()
        {
            var r = new ReporteFalso(Def, new[] { F("B", D1, 1, 5), F("A", D2, 1, 7), F("A", D1, 1, 9) });

            var res = await _motor.EjecutarAsync(r, Req(s =>
            {
                s.Orden.Add(new OrdenReporte { Campo = "cliente" });
                s.Orden.Add(new OrdenReporte { Campo = "importe", Descendente = true });
            }));

            Assert.Equal(new object?[] { 9m, 7m, 5m }, res.Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_SinOrdenPedido_ConservaElOrdenNaturalDelReporte()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D2, 1, 2), F("C1", D1, 1, 1) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Equal(new object?[] { 2m, 1m }, res.Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenPorColumnaDesconocida_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Orden.Add(new OrdenReporte { Campo = "zzz" }));

            Assert.Equal("No se puede ordenar por 'zzz': la columna no es válida para este reporte.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        [Fact]
        public async Task Ejecutar_MasDeTresCriteriosDeOrden_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());
            var s = Req(x => x.Orden.AddRange(new[] { "fecha", "cliente", "importe", "cantidad" }.Select(c => new OrdenReporte { Campo = c })));

            Assert.Equal("Solo se permiten hasta 3 criterios de orden.", await MensajeAsync(_motor.EjecutarAsync(r, s)));
        }

        // ---------- truncado ----------

        private static IEnumerable<FilaReporte> Filas(int n) =>
            Enumerable.Range(1, n).Select(i => F("C1", D1, 1, 1));

        [Fact]
        public async Task Ejecutar_ExactamenteElTope_NoSeTrunca()
        {
            var res = await _motor.EjecutarAsync(new ReporteFalso(Def, Filas(MotorReportes.LimiteFilas)), Req());

            Assert.False(res.Truncado);
            Assert.Equal(MotorReportes.LimiteFilas, res.TotalFilas);
            Assert.Equal(MotorReportes.LimiteFilas, res.Filas.Count);
        }

        [Fact]
        public async Task Ejecutar_UnaFilaMasQueElTope_SeTruncaAlTope()
        {
            var res = await _motor.EjecutarAsync(new ReporteFalso(Def, Filas(MotorReportes.LimiteFilas + 5)), Req());

            Assert.True(res.Truncado);
            Assert.Equal(MotorReportes.LimiteFilas, res.TotalFilas);
            Assert.Equal(MotorReportes.LimiteFilas, res.Filas.Count);
        }

        // ---------- totales ----------

        [Fact]
        public async Task Ejecutar_Totales_SumaPromedioYConteo()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10, promedio: 1), F("C1", D2, 3, 20, promedio: 2), F("C1", D3, 5, 30, promedio: 6) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Equal(10m, res.Totales["cantidad"]);
            Assert.Equal(60m, res.Totales["importe"]);
            Assert.Equal(3m, res.Totales["promedio"]);
            Assert.Equal(3m, res.Totales["cuenta"]);
            Assert.False(res.Totales.ContainsKey("fecha"));
        }

        [Fact]
        public async Task Ejecutar_Totales_SoloParaColumnasVisibles()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10) });

            var res = await _motor.EjecutarAsync(r, Req(s => s.Columnas.AddRange(new[] { "fecha", "cliente" })));

            Assert.Empty(res.Totales);
        }

        [Fact]
        public async Task Ejecutar_SinFilas_SumaCeroPromedioNuloYConteoCero()
        {
            var res = await _motor.EjecutarAsync(new ReporteFalso(Def, new List<FilaReporte>()), Req());

            Assert.Equal(0m, res.Totales["importe"]);
            Assert.Null(res.Totales["promedio"]);
            Assert.Equal(0m, res.Totales["cuenta"]);
            Assert.Equal(0, res.TotalFilas);
            Assert.Empty(res.Filas);
        }

        // ---------- moneda ----------

        [Fact]
        public async Task Ejecutar_VariasMonedas_AgregaLaColumnaMonedaNoSumaImportesYAvisa()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10, moneda: "QTZ"), F("C1", D2, 3, 20, moneda: "USD") });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.Contains(res.Columnas, c => c.Campo == "moneda");
            Assert.Null(res.Totales["importe"]);
            Assert.Null(res.Totales["promedio"]);
            Assert.Equal(5m, res.Totales["cantidad"]);
            Assert.Contains("El resultado incluye más de una moneda: los importes no se suman.", res.Avisos);
        }

        [Fact]
        public async Task Ejecutar_UnaSolaMoneda_NoAgregaLaColumnaNiAviso()
        {
            var r = new ReporteFalso(Def, new[] { F("C1", D1, 2, 10), F("C1", D2, 3, 20) });

            var res = await _motor.EjecutarAsync(r, Req());

            Assert.DoesNotContain(res.Columnas, c => c.Campo == "moneda");
            Assert.Equal(30m, res.Totales["importe"]);
            Assert.Empty(res.Avisos);
        }
    }
}
```

- [ ] **Step 3: Ver que fallan** (no compila: falta `MotorReportes`): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~MotorReportesTests"`.

- [ ] **Step 4: Implementar** `API.Domain.Core/Reportes/MotorReportes.cs`:

```csharp
using System.Globalization;
using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes
{
    /// <summary>
    /// Motor único de todos los reportes: valida la solicitud contra la definición, ejecuta, ordena y totaliza.
    /// Solo acepta filtros, columnas y campos de orden que la definición declara (no hay SQL libre).
    /// </summary>
    public sealed class MotorReportes : IMotorReportes
    {
        public const int LimiteFilas = 50000;
        public const int MaxCriteriosOrden = 3;
        public const int MaxNivelesAgrupacion = 2;

        private const string CampoMoneda = "moneda";
        private const int MaxLongitudTexto = 100;

        public async Task<ResultadoReporte> EjecutarAsync(IReporte reporte, SolicitudReporte solicitud)
        {
            if (solicitud is null)
                throw new ReporteInvalidoException("La solicitud del reporte es obligatoria.");

            var definicion = reporte.Definicion;
            var filtros = ValidarFiltros(definicion, solicitud.Filtros);
            var visibles = ResolverColumnas(definicion, solicitud.Columnas);
            ValidarOrden(definicion, solicitud.Orden);

            var filas = (await reporte.EjecutarAsync(filtros, LimiteFilas + 1)).ToList();
            var truncado = filas.Count > LimiteFilas;
            if (truncado) filas = filas.Take(LimiteFilas).ToList();

            filas = Ordenar(filas, solicitud.Orden);

            var resultado = new ResultadoReporte { Truncado = truncado, TotalFilas = filas.Count };

            var sumarImportes = true;
            if (VariasMonedas(filas))
            {
                sumarImportes = false;
                var colMoneda = definicion.Columnas.FirstOrDefault(c => c.Campo == CampoMoneda);
                if (colMoneda is not null && !visibles.Contains(colMoneda)) visibles.Add(colMoneda);
                resultado.Avisos.Add("El resultado incluye más de una moneda: los importes no se suman.");
            }

            resultado.Columnas = visibles;
            resultado.Totales = Totalizar(filas, visibles, sumarImportes);
            resultado.Filas = Proyectar(filas, visibles);
            return resultado;
        }

        // ---------- validación ----------

        private static FiltrosReporte ValidarFiltros(ReporteDefinicion definicion, Dictionary<string, FiltroValor>? recibidos)
        {
            var limpios = new Dictionary<string, FiltroValor>(StringComparer.Ordinal);

            foreach (var (campo, valor) in recibidos ?? new Dictionary<string, FiltroValor>())
            {
                var filtro = definicion.Filtros.FirstOrDefault(f => f.Campo == campo)
                    ?? throw new ReporteInvalidoException($"El filtro '{campo}' no es válido para este reporte.");

                if (valor is null || EstaVacio(filtro, valor)) continue;   // vacío = filtro no aplicado
                ValidarValor(filtro, valor);
                limpios[campo] = valor;
            }

            foreach (var filtro in definicion.Filtros.Where(f => f.Obligatorio))
                if (!limpios.ContainsKey(filtro.Campo))
                    throw new ReporteInvalidoException($"El filtro '{filtro.Titulo}' es obligatorio.");

            return new FiltrosReporte(limpios);
        }

        private static bool EstaVacio(FiltroReporte filtro, FiltroValor valor) =>
            filtro.Tipo == TipoFiltro.RangoFechas
                ? string.IsNullOrWhiteSpace(valor.Desde) && string.IsNullOrWhiteSpace(valor.Hasta)
                : string.IsNullOrWhiteSpace(valor.Valor);

        private static void ValidarValor(FiltroReporte filtro, FiltroValor valor)
        {
            var invalido = new ReporteInvalidoException($"El valor del filtro '{filtro.Titulo}' no es válido.");

            switch (filtro.Tipo)
            {
                case TipoFiltro.RangoFechas:
                    var desde = FiltrosReporte.ParseFecha(valor.Desde);
                    var hasta = FiltrosReporte.ParseFecha(valor.Hasta);
                    if ((!string.IsNullOrWhiteSpace(valor.Desde) && desde is null) || (!string.IsNullOrWhiteSpace(valor.Hasta) && hasta is null))
                        throw invalido;
                    if (desde is not null && hasta is not null && desde > hasta)
                        throw new ReporteInvalidoException($"El rango del filtro '{filtro.Titulo}' no es válido: la fecha inicial es posterior a la final.");
                    break;
                case TipoFiltro.Fecha:
                    if (FiltrosReporte.ParseFecha(valor.Valor) is null) throw invalido;
                    break;
                case TipoFiltro.Numero:
                    if (!decimal.TryParse(valor.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) throw invalido;
                    break;
                case TipoFiltro.Lista:
                    if (filtro.Opciones is null || !filtro.Opciones.Any(o => o.Valor == valor.Valor)) throw invalido;
                    break;
                default:   // Texto, Socio, Articulo, Almacen
                    if (valor.Valor!.Length > MaxLongitudTexto) throw invalido;
                    break;
            }
        }

        private static List<ColumnaReporte> ResolverColumnas(ReporteDefinicion definicion, List<string>? pedidas)
        {
            if (pedidas is null || pedidas.Count == 0)
                return definicion.Columnas.Where(c => c.VisiblePorDefecto).ToList();

            var resultado = new List<ColumnaReporte>();
            foreach (var campo in pedidas)
            {
                var columna = definicion.Columnas.FirstOrDefault(c => c.Campo == campo)
                    ?? throw new ReporteInvalidoException($"La columna '{campo}' no es válida para este reporte.");
                if (!resultado.Contains(columna)) resultado.Add(columna);
            }
            return resultado;
        }

        private static void ValidarOrden(ReporteDefinicion definicion, List<OrdenReporte>? orden)
        {
            if (orden is null || orden.Count == 0) return;
            if (orden.Count > MaxCriteriosOrden)
                throw new ReporteInvalidoException($"Solo se permiten hasta {MaxCriteriosOrden} criterios de orden.");

            foreach (var o in orden)
                if (!definicion.Columnas.Any(c => c.Campo == o.Campo))
                    throw new ReporteInvalidoException($"No se puede ordenar por '{o.Campo}': la columna no es válida para este reporte.");
        }

        // ---------- cálculo ----------

        private static List<FilaReporte> Ordenar(List<FilaReporte> filas, List<OrdenReporte>? orden)
        {
            if (orden is null || orden.Count == 0) return filas;

            IOrderedEnumerable<FilaReporte>? ordenado = null;
            foreach (var o in orden)
            {
                var campo = o.Campo;
                object? Clave(FilaReporte f) => f.TryGetValue(campo, out var v) ? v : null;

                ordenado = (ordenado, o.Descendente) switch
                {
                    (null, false) => filas.OrderBy(f => Clave(f), ComparadorValores.Instancia),
                    (null, true) => filas.OrderByDescending(f => Clave(f), ComparadorValores.Instancia),
                    (_, false) => ordenado!.ThenBy(f => Clave(f), ComparadorValores.Instancia),
                    (_, true) => ordenado!.ThenByDescending(f => Clave(f), ComparadorValores.Instancia)
                };
            }
            return ordenado!.ToList();
        }

        private static bool VariasMonedas(List<FilaReporte> filas) =>
            filas.Select(f => f.TryGetValue(CampoMoneda, out var m) ? m as string : null)
                 .Where(m => !string.IsNullOrEmpty(m))
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .Count() > 1;

        private static Dictionary<string, decimal?> Totalizar(IReadOnlyCollection<FilaReporte> filas, IEnumerable<ColumnaReporte> visibles, bool sumarImportes)
        {
            var totales = new Dictionary<string, decimal?>();
            foreach (var col in visibles.Where(c => c.Totaliza != Totalizacion.Ninguna))
            {
                if (!sumarImportes && col.Tipo == TipoColumna.Dinero)
                {
                    totales[col.Campo] = null;
                    continue;
                }

                var valores = filas.Select(f => f.TryGetValue(col.Campo, out var v) ? v : null).Where(v => v is not null).ToList();
                totales[col.Campo] = col.Totaliza switch
                {
                    Totalizacion.Suma => (decimal?)valores.Sum(v => ADecimal(v!)),
                    Totalizacion.Promedio => valores.Count == 0 ? null : (decimal?)valores.Average(v => ADecimal(v!)),
                    Totalizacion.Conteo => (decimal?)valores.Count,
                    _ => null
                };
            }
            return totales;
        }

        private static List<FilaReporte> Proyectar(IEnumerable<FilaReporte> filas, IReadOnlyList<ColumnaReporte> visibles) =>
            filas.Select(f =>
            {
                var nueva = new FilaReporte();
                foreach (var c in visibles) nueva[c.Campo] = f.TryGetValue(c.Campo, out var v) ? v : null;
                return nueva;
            }).ToList();

        private static decimal ADecimal(object valor) => Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
    }

    /// <summary>Compara valores de celdas: nulos primero, números como decimal, fechas como fecha y el resto como texto sin distinguir mayúsculas.</summary>
    internal sealed class ComparadorValores : IComparer<object?>
    {
        public static readonly ComparadorValores Instancia = new();

        public int Compare(object? x, object? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            if (EsNumero(x) && EsNumero(y))
                return Convert.ToDecimal(x, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(y, CultureInfo.InvariantCulture));
            if (x is DateTime dx && y is DateTime dy) return dx.CompareTo(dy);
            return StringComparer.CurrentCultureIgnoreCase.Compare(x.ToString(), y.ToString());
        }

        public static bool EsNumero(object valor) =>
            valor is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}
```

- [ ] **Step 5: Ver que pasan.** `dotnet test ... --filter "FullyQualifiedName~MotorReportesTests"` → todas en verde (las de truncado generan 50 mil filas: tardan un par de segundos). Suite completa en verde; `dotnet build API.sln` sin errores.

- [ ] **Step 6: Commit.**

```bash
git add API.Domain.Core/Reportes/MotorReportes.cs API.Service.WebApi.Tests/TestHelpers/ReporteFalso.cs API.Service.WebApi.Tests/Domain/MotorReportesTests.cs
git commit -m "feat(reporteria): motor de reportes con validacion, columnas, orden, totales y truncado

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Motor de reportes — agrupación y subtotales (spec §2.5)

**Files:**
- Modify: `API.Domain.Core/Reportes/MotorReportes.cs`
- Test: `API.Service.WebApi.Tests/Domain/MotorReportesAgrupacionTests.cs` (reutiliza `MotorReportesTests.Def`, `F` y `Req`, que son `internal static`)

**Interfaces:**
- Consumes: `MotorReportes` de la Tarea 3 y los helpers de prueba de `MotorReportesTests`.
- Produces: con `SolicitudReporte.Agrupar` no vacío, `ResultadoReporte.Grupos` trae el árbol (`GrupoReporte`) y `Filas` queda vacío; cada grupo lleva `Conteo` y `Subtotales`; las hojas llevan `Filas` salvo `SoloResumen`. Etiquetas: texto tal cual, `(Sin valor)` para nulos, números como `0.######`, fechas `yyyy-MM-dd` (Dia), `yyyy-MM` (Mes) o `yyyy` (Anio). Los grupos salen ordenados por su clave. Mensajes de validación nuevos: «Solo se permiten hasta 2 niveles de agrupación.», «No se puede agrupar por '{campo}': la columna no es válida para este reporte.», «No se puede agrupar por '{título}': la columna no es agrupable.», «La parte de fecha solo aplica a columnas de tipo fecha.».

- [ ] **Step 1: Pruebas que fallan** `API.Service.WebApi.Tests/Domain/MotorReportesAgrupacionTests.cs`:

```csharp
using API.Domain.Core.Reportes;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;
using static API.Service.WebApi.Tests.Domain.MotorReportesTests;

namespace API.Service.WebApi.Tests.Domain
{
    public class MotorReportesAgrupacionTests
    {
        private static readonly DateTime E10 = new(2026, 1, 10);
        private static readonly DateTime E20 = new(2026, 1, 20);
        private static readonly DateTime F5 = new(2026, 2, 5);

        private readonly MotorReportes _motor = new();

        private static SolicitudReporte Agrupado(Action<SolicitudReporte> ajustar) => Req(ajustar);

        private static AgrupacionReporte Por(string campo, ParteFecha? parte = null) => new() { Campo = campo, Parte = parte };

        private static async Task<string> MensajeAsync(Task tarea) =>
            (await Assert.ThrowsAsync<ReporteInvalidoException>(() => tarea)).Message;

        [Fact]
        public async Task AgruparPorTexto_DaUnGrupoPorValorConSubtotalesYFilas()
        {
            var r = new ReporteFalso(Def, new[] { F("B", E10, 1, 5), F("A", E10, 2, 10), F("A", E20, 3, 20) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente"))));

            Assert.Empty(res.Filas);
            Assert.Equal(new[] { "A", "B" }, res.Grupos.Select(g => g.Etiqueta).ToArray());
            var a = res.Grupos[0];
            Assert.Equal("cliente", a.Campo);
            Assert.Equal("Cliente", a.Titulo);
            Assert.Equal(2, a.Conteo);
            Assert.Equal(30m, a.Subtotales["importe"]);
            Assert.Equal(5m, a.Subtotales["cantidad"]);
            Assert.Equal(2, a.Filas.Count);
            Assert.Equal(35m, res.Totales["importe"]);
        }

        [Fact]
        public async Task SoloResumen_DejaLosGruposSinFilas()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10), F("A", E20, 1, 20) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => { s.Agrupar.Add(Por("cliente")); s.SoloResumen = true; }));

            Assert.Single(res.Grupos);
            Assert.Empty(res.Grupos[0].Filas);
            Assert.Equal(30m, res.Grupos[0].Subtotales["importe"]);
            Assert.Equal(2, res.Grupos[0].Conteo);
        }

        [Fact]
        public async Task DosNiveles_ClienteYLuegoMes_FormaElArbolYLasFilasCuelganDeLasHojas()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10), F("A", E20, 1, 20), F("A", F5, 1, 40), F("B", E10, 1, 5) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => { s.Agrupar.Add(Por("cliente")); s.Agrupar.Add(Por("fecha", ParteFecha.Mes)); }));

            var a = res.Grupos.Single(g => g.Etiqueta == "A");
            Assert.Empty(a.Filas);
            Assert.Equal(new[] { "2026-01", "2026-02" }, a.Hijos.Select(h => h.Etiqueta).ToArray());
            Assert.Equal(30m, a.Hijos[0].Subtotales["importe"]);
            Assert.Equal(2, a.Hijos[0].Filas.Count);
            Assert.Equal(70m, a.Subtotales["importe"]);
        }

        [Fact]
        public async Task FechaPorAnio_YPorDia_UsanSusFormatos()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 1), F("A", F5, 1, 1) });

            var porAnio = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("fecha", ParteFecha.Anio))));
            var porDia = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("fecha"))));

            Assert.Equal(new[] { "2026" }, porAnio.Grupos.Select(g => g.Etiqueta).ToArray());
            Assert.Equal(new[] { "2026-01-10", "2026-02-05" }, porDia.Grupos.Select(g => g.Etiqueta).ToArray());
        }

        [Fact]
        public async Task ValorNulo_VaEnUnGrupoSinValorAntesQueLosDemas()
        {
            var sinCliente = ReporteFalso.Fila(("fecha", E10), ("cliente", null), ("moneda", "QTZ"), ("cantidad", 1m), ("importe", 5m), ("promedio", 0m), ("cuenta", 1), ("nota", null));
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 1), sinCliente });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente"))));

            Assert.Equal(new[] { "(Sin valor)", "A" }, res.Grupos.Select(g => g.Etiqueta).ToArray());
        }

        [Fact]
        public async Task CampoDeAgrupacionNoVisible_IgualAgrupa()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10), F("B", E10, 1, 5) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => { s.Columnas.Add("importe"); s.Agrupar.Add(Por("cliente")); }));

            Assert.Equal(new[] { "A", "B" }, res.Grupos.Select(g => g.Etiqueta).ToArray());
            Assert.Equal(new[] { "importe" }, res.Grupos[0].Filas[0].Keys.ToArray());
        }

        [Fact]
        public async Task LasFilasDeUnGrupoRespetanElOrdenPedido()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 5), F("A", E20, 1, 9), F("A", F5, 1, 7) });

            var res = await _motor.EjecutarAsync(r, Agrupado(s =>
            {
                s.Agrupar.Add(Por("cliente"));
                s.Orden.Add(new OrdenReporte { Campo = "importe", Descendente = true });
            }));

            Assert.Equal(new object?[] { 9m, 7m, 5m }, res.Grupos[0].Filas.Select(f => f["importe"]).ToArray());
        }

        [Fact]
        public async Task ColumnaNoAgrupable_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("No se puede agrupar por 'Nota': la columna no es agrupable.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("nota"))))));
        }

        [Fact]
        public async Task ColumnaDesconocida_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("No se puede agrupar por 'zzz': la columna no es válida para este reporte.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("zzz"))))));
        }

        [Fact]
        public async Task MasDeDosNiveles_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("Solo se permiten hasta 2 niveles de agrupación.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => { s.Agrupar.Add(Por("cliente")); s.Agrupar.Add(Por("moneda")); s.Agrupar.Add(Por("fecha")); }))));
        }

        [Fact]
        public async Task ParteDeFechaEnColumnaQueNoEsFecha_Lanza()
        {
            var r = new ReporteFalso(Def, new List<FilaReporte>());

            Assert.Equal("La parte de fecha solo aplica a columnas de tipo fecha.",
                await MensajeAsync(_motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente", ParteFecha.Mes))))));
        }

        [Fact]
        public async Task VariasMonedas_NoSumaImportesEnLosSubtotales()
        {
            var r = new ReporteFalso(Def, new[] { F("A", E10, 1, 10, moneda: "QTZ"), F("A", E20, 1, 20, moneda: "USD") });

            var res = await _motor.EjecutarAsync(r, Agrupado(s => s.Agrupar.Add(Por("cliente"))));

            Assert.Null(res.Grupos[0].Subtotales["importe"]);
            Assert.Equal(2m, res.Grupos[0].Subtotales["cantidad"]);
        }
    }
}
```


- [ ] **Step 2: Ver que fallan:** `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~MotorReportesAgrupacionTests"` (el motor aún ignora `Agrupar`: las pruebas de grupos fallan y las de validación no lanzan).

- [ ] **Step 3: Implementar.** En `API.Domain.Core/Reportes/MotorReportes.cs`:

  1. En `EjecutarAsync`, tras `ValidarOrden(definicion, solicitud.Orden);` agrega `ValidarAgrupacion(definicion, solicitud.Agrupar);`.
  2. Reemplaza estas dos líneas del final de `EjecutarAsync`:

```csharp
            resultado.Totales = Totalizar(filas, visibles, sumarImportes);
            resultado.Filas = Proyectar(filas, visibles);
            return resultado;
```

por:

```csharp
            resultado.Totales = Totalizar(filas, visibles, sumarImportes);
            if (solicitud.Agrupar is { Count: > 0 })
                resultado.Grupos = Agrupar(filas, solicitud.Agrupar, 0, definicion, visibles, solicitud.SoloResumen, sumarImportes);
            else
                resultado.Filas = Proyectar(filas, visibles);
            return resultado;
```

  3. Agrega a la clase `MotorReportes` (antes de `ADecimal`) estos miembros, y la constante `private const string SinValor = "(Sin valor)";` junto a las otras constantes:

```csharp
        private static void ValidarAgrupacion(ReporteDefinicion definicion, List<AgrupacionReporte>? agrupar)
        {
            if (agrupar is null || agrupar.Count == 0) return;
            if (agrupar.Count > MaxNivelesAgrupacion)
                throw new ReporteInvalidoException($"Solo se permiten hasta {MaxNivelesAgrupacion} niveles de agrupación.");

            foreach (var a in agrupar)
            {
                var columna = definicion.Columnas.FirstOrDefault(c => c.Campo == a.Campo)
                    ?? throw new ReporteInvalidoException($"No se puede agrupar por '{a.Campo}': la columna no es válida para este reporte.");
                if (!columna.Agrupable)
                    throw new ReporteInvalidoException($"No se puede agrupar por '{columna.Titulo}': la columna no es agrupable.");
                if (a.Parte is not null && columna.Tipo != TipoColumna.Fecha)
                    throw new ReporteInvalidoException("La parte de fecha solo aplica a columnas de tipo fecha.");
            }
        }

        private static List<GrupoReporte> Agrupar(
            List<FilaReporte> filas, IReadOnlyList<AgrupacionReporte> niveles, int nivel,
            ReporteDefinicion definicion, IReadOnlyList<ColumnaReporte> visibles, bool soloResumen, bool sumarImportes)
        {
            var agrupacion = niveles[nivel];
            var columna = definicion.Columnas.First(c => c.Campo == agrupacion.Campo);

            return filas
                .GroupBy(f => EtiquetaGrupo(f, columna, agrupacion.Parte))
                .OrderBy(g => ValorOrdenGrupo(g.First(), columna, agrupacion.Parte), ComparadorValores.Instancia)
                .Select(g =>
                {
                    var lista = g.ToList();
                    var grupo = new GrupoReporte
                    {
                        Campo = columna.Campo,
                        Titulo = columna.Titulo,
                        Clave = g.Key,
                        Etiqueta = g.Key,
                        Conteo = lista.Count,
                        Subtotales = Totalizar(lista, visibles, sumarImportes)
                    };
                    if (nivel + 1 < niveles.Count)
                        grupo.Hijos = Agrupar(lista, niveles, nivel + 1, definicion, visibles, soloResumen, sumarImportes);
                    else if (!soloResumen)
                        grupo.Filas = Proyectar(lista, visibles);
                    return grupo;
                })
                .ToList();
        }

        private static string EtiquetaGrupo(FilaReporte fila, ColumnaReporte columna, ParteFecha? parte)
        {
            fila.TryGetValue(columna.Campo, out var valor);
            if (valor is null) return SinValor;

            if (columna.Tipo == TipoColumna.Fecha && valor is DateTime fecha)
                return (parte ?? ParteFecha.Dia) switch
                {
                    ParteFecha.Anio => fecha.ToString("yyyy", CultureInfo.InvariantCulture),
                    ParteFecha.Mes => fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    _ => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                };

            if (ComparadorValores.EsNumero(valor))
                return ADecimal(valor).ToString("0.######", CultureInfo.InvariantCulture);

            return valor.ToString() ?? SinValor;
        }

        // Los números se ordenan como números y los demás por su etiqueta (las fechas con formato yyyy-MM-dd ordenan cronológicamente).
        private static object? ValorOrdenGrupo(FilaReporte fila, ColumnaReporte columna, ParteFecha? parte)
        {
            fila.TryGetValue(columna.Campo, out var valor);
            if (valor is null) return null;
            if (columna.Tipo == TipoColumna.Fecha) return EtiquetaGrupo(fila, columna, parte);
            return ComparadorValores.EsNumero(valor) ? ADecimal(valor) : valor.ToString();
        }
```

- [ ] **Step 4: Ver que pasan.** `dotnet test ... --filter "FullyQualifiedName~MotorReportes"` (ambas clases) en verde; suite completa en verde; `dotnet build API.sln` sin errores.

- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Core/Reportes/MotorReportes.cs API.Service.WebApi.Tests/Domain/MotorReportesAgrupacionTests.cs
git commit -m "feat(reporteria): agrupacion en 1 y 2 niveles con subtotales y partes de fecha

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Reporte «Ventas por documento»

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/VentasPorDocumentoReporte.cs`
- Test: `API.Service.WebApi.Tests/Domain/VentasPorDocumentoReporteTests.cs`

**Interfaces:**
- Consumes: `IReporte`, `FiltrosReporte`, contratos de la Tarea 2; `IRepositorioGenerico<Factura, int>` y `IRepositorioGenerico<NotaCredito, int>` (`API.Infraestructure.Interface`); entidades `Factura` (campos `Cancelado`, `DocSubType` `'--'`/`'ND'`, `FechaDoc`, `Serie`, `NumDoc`, `CodigoSn`, `NombreSn`, `MonedaDoc`, `TotalBruto`, `TotalDesc`, `TotalImp`, `TotalDoc`, `Entry`) y `NotaCredito` (mismos campos, sin `DocSubType`).
- Produces: `VentasPorDocumentoReporte : IReporte` con `public const string CodigoReporte = "ventas-por-documento"`; constructor `(IRepositorioGenerico<Factura, int> facturas, IRepositorioGenerico<NotaCredito, int> notasCredito)`. Filas con las claves `fecha, tipo, serie, numDoc, codigoSn, nombreSn, moneda, bruto, descuento, impuesto, total`. Tipos: `"Factura"`, `"Nota de débito"`, `"Nota de crédito"`; las notas de crédito llevan importes negativos.

- [ ] **Step 1: Pruebas que fallan** `API.Service.WebApi.Tests/Domain/VentasPorDocumentoReporteTests.cs`:

```csharp
using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class VentasPorDocumentoReporteTests
    {
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFacturas = new();
        private readonly Mock<IRepositorioGenerico<NotaCredito, int>> _repoNotas = new();

        private VentasPorDocumentoReporte Crear(Factura[] facturas, NotaCredito[] notas)
        {
            _repoFacturas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());
            _repoNotas.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(notas.AsAsyncQueryable());
            return new VentasPorDocumentoReporte(_repoFacturas.Object, _repoNotas.Object);
        }

        private static Factura Fac(int entry, DateTime fecha, string cliente = "C1", string sub = "--", string cancelado = "N",
            int serie = 1, decimal bruto = 100m, decimal desc = 0m, decimal imp = 12m, decimal total = 112m, string moneda = "QTZ") =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = serie, Cancelado = cancelado, DocSubType = sub, FechaDoc = fecha,
                CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = moneda,
                TotalBruto = bruto, TotalDesc = desc, TotalImp = imp, TotalDoc = total
            };

        private static NotaCredito Nc(int entry, DateTime fecha, string cliente = "C1", string cancelado = "N",
            int serie = 2, decimal bruto = 50m, decimal imp = 6m, decimal total = 56m) =>
            new()
            {
                Entry = entry, NumDoc = entry, Serie = serie, Cancelado = cancelado, FechaDoc = fecha,
                CodigoSn = cliente, NombreSn = "Cliente " + cliente, MonedaDoc = "QTZ",
                TotalBruto = bruto, TotalDesc = 0m, TotalImp = imp, TotalDoc = total
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        private static readonly DateTime Dia1 = new(2026, 1, 10, 9, 30, 0);
        private static readonly DateTime Dia2 = new(2026, 1, 31, 14, 0, 0);
        private static readonly DateTime Dia3 = new(2026, 2, 1, 8, 0, 0);

        [Fact]
        public void Definicion_EsDeVentasConSusFiltrosYColumnas()
        {
            var d = Crear(Array.Empty<Factura>(), Array.Empty<NotaCredito>()).Definicion;

            Assert.Equal("ventas-por-documento", d.Codigo);
            Assert.Equal("ReportesVentas", d.Modulo);
            Assert.Equal("Ventas", d.Familia);
            Assert.Equal(new[] { "fechas", "cliente", "tipo", "serie" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal("C", d.Filtros.Single(f => f.Campo == "cliente").SubTipo);
            Assert.Equal(new[] { "FAC", "ND", "NC" }, d.Filtros.Single(f => f.Campo == "tipo").Opciones!.Select(o => o.Valor).ToArray());
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "total").Totaliza);
            Assert.True(d.Columnas.Single(c => c.Campo == "fecha").Agrupable);
            Assert.False(d.Columnas.Single(c => c.Campo == "serie").VisiblePorDefecto);
        }

        [Fact]
        public async Task Ejecutar_FacturasNotasDeDebitoYNotasDeCredito_ConSignoEnLasNotasDeCredito()
        {
            var r = Crear(
                new[] { Fac(1, Dia1), Fac(2, Dia1, sub: "ND", total: 56m, bruto: 50m, imp: 6m) },
                new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(3, filas.Count);
            var factura = filas.Single(f => (string)f["tipo"]! == "Factura");
            Assert.Equal(112m, factura["total"]);
            Assert.Equal(100m, factura["bruto"]);
            Assert.Equal(12m, factura["impuesto"]);
            Assert.Equal("C1", factura["codigoSn"]);
            Assert.Equal("Cliente C1", factura["nombreSn"]);
            Assert.Equal("QTZ", factura["moneda"]);
            Assert.Equal(56m, filas.Single(f => (string)f["tipo"]! == "Nota de débito")["total"]);
            var nota = filas.Single(f => (string)f["tipo"]! == "Nota de crédito");
            Assert.Equal(-56m, nota["total"]);
            Assert.Equal(-50m, nota["bruto"]);
            Assert.Equal(-6m, nota["impuesto"]);
            Assert.Equal(0m, nota["descuento"]);
        }

        [Fact]
        public async Task Ejecutar_ExcluyeLosCancelados()
        {
            var r = Crear(new[] { Fac(1, Dia1), Fac(2, Dia1, cancelado: "S") }, new[] { Nc(1, Dia1, cancelado: "S") });

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Single(filas);
            Assert.Equal("Factura", filas[0]["tipo"]);
        }

        [Theory]
        [InlineData("FAC", new[] { "Factura" })]
        [InlineData("ND", new[] { "Nota de débito" })]
        [InlineData("NC", new[] { "Nota de crédito" })]
        public async Task Ejecutar_FiltroTipo(string tipo, string[] esperados)
        {
            var r = Crear(new[] { Fac(1, Dia1), Fac(2, Dia1, sub: "ND") }, new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(d => d["tipo"] = new FiltroValor { Valor = tipo }), 100);

            Assert.Equal(esperados, filas.Select(f => (string)f["tipo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_FiltroClienteYSerie()
        {
            var r = Crear(new[] { Fac(1, Dia1, "C1", serie: 1), Fac(2, Dia1, "C2", serie: 1), Fac(3, Dia1, "C1", serie: 9) }, new[] { Nc(1, Dia1, "C1", serie: 2) });

            var porCliente = await r.EjecutarAsync(Filtros(d => d["cliente"] = new FiltroValor { Valor = "C1" }), 100);
            var porSerie = await r.EjecutarAsync(Filtros(d => d["serie"] = new FiltroValor { Valor = "9" }), 100);

            Assert.Equal(3, porCliente.Count);
            Assert.All(porCliente, f => Assert.Equal("C1", f["codigoSn"]));
            Assert.Single(porSerie);
            Assert.Equal(9, porSerie[0]["serie"]);
        }

        [Fact]
        public async Task Ejecutar_RangoDeFechas_EsInclusivoEnAmbosExtremos()
        {
            var r = Crear(new[] { Fac(1, Dia1), Fac(2, Dia2), Fac(3, Dia3) }, new[] { Nc(1, Dia3) });

            var filas = await r.EjecutarAsync(Filtros(d => d["fechas"] = new FiltroValor { Desde = "2026-01-10", Hasta = "2026-01-31" }), 100);

            Assert.Equal(new[] { 1, 2 }, filas.Select(f => (int)f["numDoc"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenNaturalPorFechaYAplicaElLimite()
        {
            var r = Crear(new[] { Fac(1, Dia2), Fac(2, Dia1) }, new[] { Nc(1, Dia1) });

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(2, filas.Count);
            Assert.Equal(new[] { "Factura", "Nota de crédito" }, filas.Select(f => (string)f["tipo"]!).ToArray());
            Assert.All(filas, f => Assert.Equal(Dia1, f["fecha"]));
        }

        [Fact]
        public async Task Ejecutar_ImportesNulosCuentanComoCero()
        {
            var factura = Fac(1, Dia1);
            factura.TotalBruto = null; factura.TotalDesc = null; factura.TotalImp = null; factura.TotalDoc = null;
            var r = Crear(new[] { factura }, Array.Empty<NotaCredito>());

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(0m, filas[0]["total"]);
            Assert.Equal(0m, filas[0]["bruto"]);
        }
    }
}
```

- [ ] **Step 2: Ver que fallan** (no compila: falta la clase): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~VentasPorDocumentoReporteTests"`.

- [ ] **Step 3: Implementar** `API.Domain.Core/Reportes/Definiciones/VentasPorDocumentoReporte.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>
    /// Ventas por documento: facturas y notas de débito (suman) y notas de crédito (restan), sin cancelados.
    /// La nota de débito vive en Factura con DocSubType 'ND'.
    /// </summary>
    public sealed class VentasPorDocumentoReporte : IReporte
    {
        public const string CodigoReporte = "ventas-por-documento";

        private readonly IRepositorioGenerico<Factura, int> _facturas;
        private readonly IRepositorioGenerico<NotaCredito, int> _notasCredito;

        public VentasPorDocumentoReporte(IRepositorioGenerico<Factura, int> facturas, IRepositorioGenerico<NotaCredito, int> notasCredito)
        {
            _facturas = facturas;
            _notasCredito = notasCredito;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Ventas por documento",
            Descripcion: "Facturas y notas de débito menos notas de crédito, sin documentos cancelados.",
            Familia: "Ventas",
            Modulo: "ReportesVentas",
            Filtros: new[]
            {
                new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas),
                new FiltroReporte("cliente", "Cliente", TipoFiltro.Socio, SubTipo: "C"),
                new FiltroReporte("tipo", "Tipo", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("FAC", "Factura"),
                    new OpcionFiltro("ND", "Nota de débito"),
                    new OpcionFiltro("NC", "Nota de crédito")
                }),
                new FiltroReporte("serie", "Serie", TipoFiltro.Numero)
            },
            Columnas: new[]
            {
                new ColumnaReporte("fecha", "Fecha", TipoColumna.Fecha, Agrupable: true),
                new ColumnaReporte("tipo", "Tipo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("serie", "Serie", TipoColumna.Entero, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("numDoc", "No.", TipoColumna.Entero),
                new ColumnaReporte("codigoSn", "Código cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nombreSn", "Cliente", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("moneda", "Moneda", TipoColumna.Texto, Agrupable: true, VisiblePorDefecto: false),
                new ColumnaReporte("bruto", "Bruto", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("descuento", "Descuento", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("impuesto", "Impuesto", TipoColumna.Dinero, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("total", "Total", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var desde = filtros.FechaDesde("fechas");
            var hasta = filtros.FechaHastaExclusiva("fechas");
            var cliente = filtros.Texto("cliente");
            var tipo = filtros.Texto("tipo");
            var serie = filtros.Numero("serie") is { } s ? (int?)s : null;

            var filas = new List<FilaReporte>();

            if (tipo is null or "FAC" or "ND")
            {
                var q = (await _facturas.ObtenerTodoAsync()).Where(f => f.Cancelado != "S");
                q = tipo switch
                {
                    "FAC" => q.Where(f => f.DocSubType == "--"),
                    "ND" => q.Where(f => f.DocSubType == "ND"),
                    _ => q.Where(f => f.DocSubType == "--" || f.DocSubType == "ND")
                };
                if (desde is { } d1) q = q.Where(f => f.FechaDoc >= d1);
                if (hasta is { } h1) q = q.Where(f => f.FechaDoc < h1);
                if (cliente is not null) q = q.Where(f => f.CodigoSn == cliente);
                if (serie is { } s1) q = q.Where(f => f.Serie == s1);

                var documentos = await q.OrderBy(f => f.FechaDoc).ThenBy(f => f.Entry).Take(limite)
                    .Select(f => new { f.FechaDoc, f.DocSubType, f.Serie, f.NumDoc, f.CodigoSn, f.NombreSn, f.MonedaDoc, f.TotalBruto, f.TotalDesc, f.TotalImp, f.TotalDoc })
                    .ToListAsync();

                filas.AddRange(documentos.Select(x => Fila(x.FechaDoc, x.DocSubType == "ND" ? "Nota de débito" : "Factura", 1m,
                    x.Serie, x.NumDoc, x.CodigoSn, x.NombreSn, x.MonedaDoc, x.TotalBruto, x.TotalDesc, x.TotalImp, x.TotalDoc)));
            }

            if (tipo is null or "NC")
            {
                var q = (await _notasCredito.ObtenerTodoAsync()).Where(n => n.Cancelado != "S");
                if (desde is { } d2) q = q.Where(n => n.FechaDoc >= d2);
                if (hasta is { } h2) q = q.Where(n => n.FechaDoc < h2);
                if (cliente is not null) q = q.Where(n => n.CodigoSn == cliente);
                if (serie is { } s2) q = q.Where(n => n.Serie == s2);

                var notas = await q.OrderBy(n => n.FechaDoc).ThenBy(n => n.Entry).Take(limite)
                    .Select(n => new { n.FechaDoc, n.Serie, n.NumDoc, n.CodigoSn, n.NombreSn, n.MonedaDoc, n.TotalBruto, n.TotalDesc, n.TotalImp, n.TotalDoc })
                    .ToListAsync();

                filas.AddRange(notas.Select(x => Fila(x.FechaDoc, "Nota de crédito", -1m,
                    x.Serie, x.NumDoc, x.CodigoSn, x.NombreSn, x.MonedaDoc, x.TotalBruto, x.TotalDesc, x.TotalImp, x.TotalDoc)));
            }

            return filas
                .OrderBy(f => (DateTime?)f["fecha"])
                .ThenBy(f => (string)f["tipo"]!, StringComparer.Ordinal)
                .Take(limite)
                .ToList();
        }

        private static FilaReporte Fila(DateTime? fecha, string tipo, decimal signo, int serie, int numDoc, string? codigoSn, string? nombreSn,
            string? moneda, decimal? bruto, decimal? descuento, decimal? impuesto, decimal? total) =>
            new()
            {
                ["fecha"] = fecha,
                ["tipo"] = tipo,
                ["serie"] = serie,
                ["numDoc"] = numDoc,
                ["codigoSn"] = codigoSn,
                ["nombreSn"] = nombreSn,
                ["moneda"] = moneda,
                ["bruto"] = signo * (bruto ?? 0m),
                ["descuento"] = signo * (descuento ?? 0m),
                ["impuesto"] = signo * (impuesto ?? 0m),
                ["total"] = signo * (total ?? 0m)
            };
    }
}
```

- [ ] **Step 4: Ver que pasan.** El mismo `--filter` en verde; suite completa en verde; `dotnet build API.sln` sin errores.

- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Core/Reportes/Definiciones/VentasPorDocumentoReporte.cs API.Service.WebApi.Tests/Domain/VentasPorDocumentoReporteTests.cs
git commit -m "feat(reporteria): reporte Ventas por documento

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Reporte «Existencias y valor por almacén»

**Files:**
- Create: `API.Domain.Core/Reportes/Definiciones/ExistenciasValorReporte.cs`
- Test: `API.Service.WebApi.Tests/Domain/ExistenciasValorReporteTests.cs`

**Interfaces:**
- Consumes: `IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>`; entidades `ExistenciaArticulo` (`CodArticulo`, `CodAlmacen`, `Disponible`, `Comprometido`, `Pedido`, navegación `CodArticuloNavigation`), `Articulo` (`Nombre`, `MetodoValuacion` `'P'`/`'E'`, `CostoPromedio`, `CostoEstandar`, navegación `CodigoGrupoNavigation`), `GrupoArticulo` (`Nombre`).
- Produces: `ExistenciasValorReporte : IReporte`, `public const string CodigoReporte = "existencias-valor"`, módulo `ReportesInventario`. Filas con claves `almacen, articulo, nombre, grupo, enStock, comprometido, pedido, costo, valor` (`valor = enStock × costo`, redondeado a 6 decimales; el costo es el promedio o el estándar según `MetodoValuacion`).

- [ ] **Step 1: Pruebas que fallan** `API.Service.WebApi.Tests/Domain/ExistenciasValorReporteTests.cs`:

```csharp
using API.Domain.Core.Reportes.Definiciones;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ExistenciasValorReporteTests
    {
        private readonly Mock<IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)>> _repo = new();

        private ExistenciasValorReporte Crear(params ExistenciaArticulo[] filas)
        {
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());
            return new ExistenciasValorReporte(_repo.Object);
        }

        private static ExistenciaArticulo Ex(string art, string alm, decimal disponible, string metodo = "P", decimal promedio = 10m,
            decimal estandar = 8m, string? grupo = "Lubricantes", decimal comprometido = 0m, decimal pedido = 0m) =>
            new()
            {
                CodArticulo = art, CodAlmacen = alm, Disponible = disponible, Comprometido = comprometido, Pedido = pedido,
                CodArticuloNavigation = new Articulo
                {
                    Codigo = art, Nombre = "Artículo " + art, MetodoValuacion = metodo, CostoPromedio = promedio, CostoEstandar = estandar,
                    CodigoGrupoNavigation = grupo is null ? null : new GrupoArticulo { Codigo = 100, Nombre = grupo }
                }
            };

        private static FiltrosReporte Filtros(Action<Dictionary<string, FiltroValor>>? ajustar = null)
        {
            var d = new Dictionary<string, FiltroValor>();
            ajustar?.Invoke(d);
            return new FiltrosReporte(d);
        }

        [Fact]
        public void Definicion_EsDeInventarioConSusFiltrosYColumnas()
        {
            var d = Crear().Definicion;

            Assert.Equal("existencias-valor", d.Codigo);
            Assert.Equal("ReportesInventario", d.Modulo);
            Assert.Equal("Inventario", d.Familia);
            Assert.Equal(new[] { "almacen", "articulo", "grupo", "soloConExistencias" }, d.Filtros.Select(f => f.Campo).ToArray());
            Assert.Equal(TipoFiltro.Almacen, d.Filtros.Single(f => f.Campo == "almacen").Tipo);
            Assert.Equal("N", d.Filtros.Single(f => f.Campo == "soloConExistencias").ValorPorDefecto);
            Assert.Equal(Totalizacion.Suma, d.Columnas.Single(c => c.Campo == "valor").Totaliza);
            Assert.True(d.Columnas.Single(c => c.Campo == "almacen").Agrupable);
        }

        [Fact]
        public async Task Ejecutar_ValorEsEnStockPorCostoPromedio()
        {
            var r = Crear(Ex("A1", "01", 4m, promedio: 12.5m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            var f = Assert.Single(filas);
            Assert.Equal("01", f["almacen"]);
            Assert.Equal("A1", f["articulo"]);
            Assert.Equal("Artículo A1", f["nombre"]);
            Assert.Equal("Lubricantes", f["grupo"]);
            Assert.Equal(4m, f["enStock"]);
            Assert.Equal(12.5m, f["costo"]);
            Assert.Equal(50m, f["valor"]);
        }

        [Fact]
        public async Task Ejecutar_ConMetodoEstandar_UsaElCostoEstandar()
        {
            var r = Crear(Ex("A1", "01", 3m, metodo: "E", promedio: 10m, estandar: 7m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Equal(7m, filas[0]["costo"]);
            Assert.Equal(21m, filas[0]["valor"]);
        }

        [Fact]
        public async Task Ejecutar_LlevaComprometidoYPedidoYTolerarGrupoNulo()
        {
            var r = Crear(Ex("A1", "01", 1m, grupo: null, comprometido: 2m, pedido: 5m));

            var filas = await r.EjecutarAsync(Filtros(), 100);

            Assert.Null(filas[0]["grupo"]);
            Assert.Equal(2m, filas[0]["comprometido"]);
            Assert.Equal(5m, filas[0]["pedido"]);
        }

        [Fact]
        public async Task Ejecutar_FiltrosAlmacenArticuloYGrupo()
        {
            var r = Crear(Ex("A1", "01", 1m), Ex("A1", "02", 1m), Ex("A2", "01", 1m, grupo: "Filtros"));

            var porAlmacen = await r.EjecutarAsync(Filtros(d => d["almacen"] = new FiltroValor { Valor = "02" }), 100);
            var porArticulo = await r.EjecutarAsync(Filtros(d => d["articulo"] = new FiltroValor { Valor = "A2" }), 100);
            var porGrupo = await r.EjecutarAsync(Filtros(d => d["grupo"] = new FiltroValor { Valor = "Filtro" }), 100);

            Assert.Equal(new[] { "A1" }, porAlmacen.Select(f => (string)f["articulo"]!).ToArray());
            Assert.Equal(new[] { "A2" }, porArticulo.Select(f => (string)f["articulo"]!).ToArray());
            Assert.Equal(new[] { "A2" }, porGrupo.Select(f => (string)f["articulo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_SoloConExistencias_ExcluyeLasFilasEnCero()
        {
            var r = Crear(Ex("A1", "01", 0m), Ex("A2", "01", 2m));

            var todas = await r.EjecutarAsync(Filtros(d => d["soloConExistencias"] = new FiltroValor { Valor = "N" }), 100);
            var soloCon = await r.EjecutarAsync(Filtros(d => d["soloConExistencias"] = new FiltroValor { Valor = "S" }), 100);

            Assert.Equal(2, todas.Count);
            Assert.Equal(new[] { "A2" }, soloCon.Select(f => (string)f["articulo"]!).ToArray());
        }

        [Fact]
        public async Task Ejecutar_OrdenNaturalPorAlmacenYArticuloYAplicaElLimite()
        {
            var r = Crear(Ex("B", "02", 1m), Ex("A", "02", 1m), Ex("B", "01", 1m));

            var filas = await r.EjecutarAsync(Filtros(), 2);

            Assert.Equal(new[] { "01|B", "02|A" }, filas.Select(f => $"{f["almacen"]}|{f["articulo"]}").ToArray());
        }
    }
}
```

- [ ] **Step 2: Ver que fallan** (no compila): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~ExistenciasValorReporteTests"`.

- [ ] **Step 3: Implementar** `API.Domain.Core/Reportes/Definiciones/ExistenciasValorReporte.cs`:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes.Definiciones
{
    /// <summary>Existencias y valor de inventario por almacén. Valor = en stock × costo (promedio o estándar según el método de valuación).</summary>
    public sealed class ExistenciasValorReporte : IReporte
    {
        public const string CodigoReporte = "existencias-valor";

        private readonly IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> _existencias;

        public ExistenciasValorReporte(IRepositorioGenerico<ExistenciaArticulo, (string CodArticulo, string CodAlmacen)> existencias)
        {
            _existencias = existencias;
        }

        public ReporteDefinicion Definicion { get; } = new(
            Codigo: CodigoReporte,
            Nombre: "Existencias y valor por almacén",
            Descripcion: "Cantidad en stock, comprometida y pedida por artículo y almacén, con el valor del inventario.",
            Familia: "Inventario",
            Modulo: "ReportesInventario",
            Filtros: new[]
            {
                new FiltroReporte("almacen", "Almacén", TipoFiltro.Almacen),
                new FiltroReporte("articulo", "Artículo", TipoFiltro.Articulo),
                new FiltroReporte("grupo", "Grupo de artículos", TipoFiltro.Texto),
                new FiltroReporte("soloConExistencias", "Solo con existencias", TipoFiltro.Lista, Opciones: new[]
                {
                    new OpcionFiltro("N", "No (todas las filas)"),
                    new OpcionFiltro("S", "Sí (solo con stock)")
                }, ValorPorDefecto: "N")
            },
            Columnas: new[]
            {
                new ColumnaReporte("almacen", "Almacén", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("articulo", "Artículo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("nombre", "Nombre", TipoColumna.Texto),
                new ColumnaReporte("grupo", "Grupo", TipoColumna.Texto, Agrupable: true),
                new ColumnaReporte("enStock", "En stock", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma),
                new ColumnaReporte("comprometido", "Comprometido", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma, VisiblePorDefecto: false),
                new ColumnaReporte("pedido", "Pedido", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma, VisiblePorDefecto: false),
                new ColumnaReporte("costo", "Costo unitario", TipoColumna.Dinero),
                new ColumnaReporte("valor", "Valor", TipoColumna.Dinero, Totaliza: Totalizacion.Suma)
            });

        public async Task<IReadOnlyList<FilaReporte>> EjecutarAsync(FiltrosReporte filtros, int limite)
        {
            var almacen = filtros.Texto("almacen");
            var articulo = filtros.Texto("articulo");
            var grupo = filtros.Texto("grupo");
            var soloConExistencias = filtros.Texto("soloConExistencias") == "S";

            var q = await _existencias.ObtenerTodoAsync();
            if (almacen is not null) q = q.Where(e => e.CodAlmacen == almacen);
            if (articulo is not null) q = q.Where(e => e.CodArticulo == articulo);
            if (grupo is not null)
                q = q.Where(e => e.CodArticuloNavigation.CodigoGrupoNavigation != null
                              && e.CodArticuloNavigation.CodigoGrupoNavigation.Nombre!.Contains(grupo));
            if (soloConExistencias) q = q.Where(e => e.Disponible > 0);

            var filas = await q.OrderBy(e => e.CodAlmacen).ThenBy(e => e.CodArticulo).Take(limite)
                .Select(e => new
                {
                    e.CodAlmacen,
                    e.CodArticulo,
                    Nombre = e.CodArticuloNavigation.Nombre,
                    Grupo = e.CodArticuloNavigation.CodigoGrupoNavigation == null ? null : e.CodArticuloNavigation.CodigoGrupoNavigation.Nombre,
                    e.Disponible,
                    e.Comprometido,
                    e.Pedido,
                    Costo = e.CodArticuloNavigation.MetodoValuacion == "E"
                        ? e.CodArticuloNavigation.CostoEstandar
                        : e.CodArticuloNavigation.CostoPromedio
                })
                .ToListAsync();

            return filas.Select(x => new FilaReporte
            {
                ["almacen"] = x.CodAlmacen,
                ["articulo"] = x.CodArticulo,
                ["nombre"] = x.Nombre,
                ["grupo"] = x.Grupo,
                ["enStock"] = x.Disponible,
                ["comprometido"] = x.Comprometido,
                ["pedido"] = x.Pedido,
                ["costo"] = x.Costo,
                ["valor"] = Math.Round(x.Disponible * x.Costo, 6)
            }).ToList();
        }
    }
}
```

- [ ] **Step 4: Ver que pasan.** `--filter` en verde; suite completa en verde; build sin errores.

- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Core/Reportes/Definiciones/ExistenciasValorReporte.cs API.Service.WebApi.Tests/Domain/ExistenciasValorReporteTests.cs
git commit -m "feat(reporteria): reporte Existencias y valor por almacen

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Catálogo, `ReporteApplication` y `ReporteController` (catálogo, definición y ejecutar)

**Files:**
- Create: `API.Domain.Core/Reportes/CatalogoReportes.cs`
- Create: `API.Application.DTO/reporte/ReporteResumenDTO.cs`
- Create: `API.Application.Interface/IReporteApplication.cs`
- Modify: `API.Application.Interface/API.Application.Interface.csproj` (referencia a `API.Domain.Interface`)
- Create: `API.Application.Main/ReporteApplication.cs`
- Create: `API.Service.WebApi/Controllers/ReporteController.cs`
- Modify: `API.Service.WebApi/Startup.cs` (DI)
- Test: `API.Service.WebApi.Tests/Domain/CatalogoReportesTests.cs`, `API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs`, `API.Service.WebApi.Tests/Controllers/ReportePermisosTests.cs`

**Interfaces:**
- Consumes: `IReporte`, `ICatalogoReportes`, `IMotorReportes`, `MotorReportes`, `ReporteDefinicion`, `SolicitudReporte`, `ResultadoReporte`, `ReporteInvalidoException`, `ReporteNoExisteException`; `Respuesta<T>` (`API.Application.DTO`); `PermisoUsuario` y `Accion`.
- Produces:
  - `CatalogoReportes : ICatalogoReportes` (ctor `IEnumerable<IReporte>`; ordena por familia y nombre; `Buscar` sin distinguir mayúsculas; lanza `InvalidOperationException("Código de reporte duplicado: x")` si hay códigos repetidos).
  - `ReporteResumenDTO { string Codigo; string Nombre; string Descripcion; string Familia }` en `API.Application.DTO.reporte`.
  - `IReporteApplication` (en `API.Application.Interface`), con en esta tarea: `Respuesta<List<ReporteResumenDTO>> ObtenerCatalogo(Func<string, bool> puedeVerModulo)`, `Respuesta<ReporteDefinicion> ObtenerDefinicion(string codigo)`, `Task<Respuesta<ResultadoReporte>> EjecutarAsync(string codigo, SolicitudReporte solicitud)`. Las tareas 8 y 11 agregan más miembros.
  - `ReporteApplication(ICatalogoReportes catalogo, IMotorReportes motor)` (las tareas 8 y 11 amplían el constructor).
  - `ReporteController` (`[Route("api/Reporte")]`): `GET api/Reporte`, `GET api/Reporte/{codigo}`, `POST api/Reporte/{codigo}/ejecutar`. Constructor `(IReporteApplication app)` (la Tarea 8 agrega `IConfiguration`).

- [ ] **Step 1: Pruebas que fallan.**

`API.Service.WebApi.Tests/Domain/CatalogoReportesTests.cs`:

```csharp
using API.Domain.Core.Reportes;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class CatalogoReportesTests
    {
        private static ReporteFalso R(string codigo, string nombre, string familia) =>
            new(new ReporteDefinicion(codigo, nombre, "d", familia, "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>()),
                new List<FilaReporte>());

        [Fact]
        public void Todos_SeOrdenaPorFamiliaYNombre()
        {
            var c = new CatalogoReportes(new[] { R("c", "Zeta", "Ventas"), R("a", "Alfa", "Ventas"), R("b", "Beta", "Compras") });

            Assert.Equal(new[] { "b", "a", "c" }, c.Todos.Select(r => r.Definicion.Codigo).ToArray());
        }

        [Fact]
        public void Buscar_NoDistingueMayusculasYDevuelveNuloSiNoExiste()
        {
            var c = new CatalogoReportes(new[] { R("ventas-por-documento", "V", "Ventas") });

            Assert.NotNull(c.Buscar("VENTAS-POR-DOCUMENTO"));
            Assert.Null(c.Buscar("zzz"));
        }

        [Fact]
        public void CodigosDuplicados_LanzanAlConstruir()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new CatalogoReportes(new[] { R("x", "A", "F"), R("X", "B", "F") }));

            Assert.Equal("Código de reporte duplicado: x", ex.Message);
        }
    }
}
```

`API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs`:

```csharp
using API.Application.Main;
using API.Domain.Interface.Reportes;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class ReporteApplicationTests
    {
        private readonly Mock<ICatalogoReportes> _catalogo = new();
        private readonly Mock<IMotorReportes> _motor = new();
        private readonly Mock<IReporte> _ventas = new();
        private readonly Mock<IReporte> _stock = new();
        private readonly ReporteApplication _app;

        private static ReporteDefinicion Def(string codigo, string modulo) =>
            new(codigo, "Nombre " + codigo, "Desc " + codigo, "Familia", modulo, Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());

        public ReporteApplicationTests()
        {
            _ventas.SetupGet(r => r.Definicion).Returns(Def("ventas", "ReportesVentas"));
            _stock.SetupGet(r => r.Definicion).Returns(Def("stock", "ReportesInventario"));
            _catalogo.SetupGet(c => c.Todos).Returns(new[] { _ventas.Object, _stock.Object });
            _catalogo.Setup(c => c.Buscar("ventas")).Returns(_ventas.Object);
            _catalogo.Setup(c => c.Buscar("nada")).Returns((IReporte?)null);
            _app = new ReporteApplication(_catalogo.Object, _motor.Object);
        }

        [Fact]
        public void ObtenerCatalogo_SoloIncluyeLosReportesDeModulosPermitidos()
        {
            var r = _app.ObtenerCatalogo(modulo => modulo == "ReportesVentas");

            Assert.True(r.Resultado);
            var item = Assert.Single(r.Dato);
            Assert.Equal("ventas", item.Codigo);
            Assert.Equal("Nombre ventas", item.Nombre);
            Assert.Equal("Desc ventas", item.Descripcion);
            Assert.Equal("Familia", item.Familia);
        }

        [Fact]
        public void ObtenerDefinicion_Existente_LaDevuelve()
        {
            var r = _app.ObtenerDefinicion("ventas");

            Assert.True(r.Resultado);
            Assert.Equal("ventas", r.Dato.Codigo);
        }

        [Fact]
        public void ObtenerDefinicion_Inexistente_DiceQueNoExiste()
        {
            var r = _app.ObtenerDefinicion("nada");

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
        }

        [Fact]
        public async Task Ejecutar_PasaLaSolicitudAlMotorYDevuelveElResultado()
        {
            var solicitud = new SolicitudReporte();
            var resultado = new ResultadoReporte { TotalFilas = 3 };
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, solicitud)).ReturnsAsync(resultado);

            var r = await _app.EjecutarAsync("ventas", solicitud);

            Assert.True(r.Resultado);
            Assert.Same(resultado, r.Dato);
        }

        [Fact]
        public async Task Ejecutar_ReporteInexistente_DiceQueNoExiste()
        {
            var r = await _app.EjecutarAsync("nada", new SolicitudReporte());

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
            _motor.Verify(m => m.EjecutarAsync(It.IsAny<IReporte>(), It.IsAny<SolicitudReporte>()), Times.Never);
        }

        [Fact]
        public async Task Ejecutar_SolicitudInvalida_DevuelveElMensajeDelMotor()
        {
            _motor.Setup(m => m.EjecutarAsync(It.IsAny<IReporte>(), It.IsAny<SolicitudReporte>()))
                  .ThrowsAsync(new ReporteInvalidoException("El filtro 'Cliente' es obligatorio."));

            var r = await _app.EjecutarAsync("ventas", new SolicitudReporte());

            Assert.False(r.Resultado);
            Assert.Equal("El filtro 'Cliente' es obligatorio.", r.Mensaje);
        }
    }
}
```

`API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs`:

```csharp
using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class ReporteControllerTests
    {
        private readonly Mock<IReporteApplication> _app = new();

        internal static ClaimsPrincipal Usuario(string? perm = null, bool superusuario = false, string id = "5")
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Name, "tester") };
            if (superusuario) claims.Add(new Claim("SuperUsuario", "S"));
            if (perm is not null) claims.Add(new Claim("perm", perm));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        private ReporteController Crear(ClaimsPrincipal? usuario = null) =>
            new(_app.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario ?? Usuario() } }
            };

        private static Respuesta<ReporteDefinicion> DefinicionOk(string modulo = "ReportesVentas") => new()
        {
            Resultado = true,
            Dato = new ReporteDefinicion("ventas", "V", "d", "Ventas", modulo, Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>())
        };

        private static void Es403(IActionResult resultado)
        {
            var o = Assert.IsType<ObjectResult>(resultado);
            Assert.Equal(403, o.StatusCode);
            Assert.Equal(PermisoUsuario.MensajeDenegado, ((Respuesta<object>)o.Value!).Mensaje);
        }

        // ---------- catálogo ----------

        [Fact]
        public void Catalogo_FiltraPorElPermisoVerDeCadaModulo()
        {
            Func<string, bool>? predicado = null;
            var buena = new Respuesta<List<ReporteResumenDTO>> { Resultado = true, Dato = new() };
            _app.Setup(a => a.ObtenerCatalogo(It.IsAny<Func<string, bool>>()))
                .Callback<Func<string, bool>>(f => predicado = f)
                .Returns(buena);

            var r = Crear(Usuario("ReportesVentas:V")).Catalogo();

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
            Assert.True(predicado!("ReportesVentas"));
            Assert.False(predicado("ReportesCompras"));
        }

        [Fact]
        public void Catalogo_ElSuperusuarioVeTodo()
        {
            Func<string, bool>? predicado = null;
            _app.Setup(a => a.ObtenerCatalogo(It.IsAny<Func<string, bool>>()))
                .Callback<Func<string, bool>>(f => predicado = f)
                .Returns(new Respuesta<List<ReporteResumenDTO>> { Resultado = true, Dato = new() });

            Crear(Usuario(superusuario: true)).Catalogo();

            Assert.True(predicado!("ReportesCobrosPagos"));
        }

        [Fact]
        public void Catalogo_ConError_DevuelveBadRequest()
        {
            var mala = new Respuesta<List<ReporteResumenDTO>> { Resultado = false, Mensaje = "falla" };
            _app.Setup(a => a.ObtenerCatalogo(It.IsAny<Func<string, bool>>())).Returns(mala);

            var r = Crear().Catalogo();

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        // ---------- definición ----------

        [Fact]
        public void Definicion_ConPermiso_DevuelveOk()
        {
            var buena = DefinicionOk();
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(buena);

            var r = Crear(Usuario("ReportesVentas:V")).Definicion("ventas");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public void Definicion_SinPermisoDelModulo_Devuelve403()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            Es403(Crear(Usuario("ReportesCompras:V")).Definicion("ventas").Result!);
        }

        [Fact]
        public void Definicion_Inexistente_DevuelveNotFound()
        {
            var mala = new Respuesta<ReporteDefinicion> { Resultado = false, Mensaje = "El reporte 'x' no existe." };
            _app.Setup(a => a.ObtenerDefinicion("x")).Returns(mala);

            var r = Crear().Definicion("x");

            Assert.Same(mala, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }

        // ---------- ejecutar ----------

        [Fact]
        public async Task Ejecutar_ConPermiso_EjecutaYDevuelveOk()
        {
            var solicitud = new SolicitudReporte();
            var buena = new Respuesta<ResultadoReporte> { Resultado = true, Dato = new ResultadoReporte() };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EjecutarAsync("ventas", solicitud)).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V")).Ejecutar("ventas", solicitud);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Ejecutar_SinPermiso_Devuelve403YNoEjecuta()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(Usuario("ReportesVentas:A")).Ejecutar("ventas", new SolicitudReporte());

            Es403(r.Result!);
            _app.Verify(a => a.EjecutarAsync(It.IsAny<string>(), It.IsAny<SolicitudReporte>()), Times.Never);
        }

        [Fact]
        public async Task Ejecutar_ReporteInexistente_DevuelveNotFound()
        {
            _app.Setup(a => a.ObtenerDefinicion("x")).Returns(new Respuesta<ReporteDefinicion> { Resultado = false, Mensaje = "El reporte 'x' no existe." });

            var r = await Crear().Ejecutar("x", new SolicitudReporte());

            Assert.IsType<NotFoundObjectResult>(r.Result);
        }

        [Fact]
        public async Task Ejecutar_SolicitudNula_DevuelveBadRequest()
        {
            var r = await Crear().Ejecutar("ventas", null!);

            var mala = Assert.IsType<BadRequestObjectResult>(r.Result);
            Assert.Equal("La solicitud del reporte es obligatoria.", ((Respuesta<ResultadoReporte>)mala.Value!).Mensaje);
        }

        [Fact]
        public async Task Ejecutar_ErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<ResultadoReporte> { Resultado = false, Mensaje = "El filtro 'Cliente' es obligatorio." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EjecutarAsync("ventas", It.IsAny<SolicitudReporte>())).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).Ejecutar("ventas", new SolicitudReporte());

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }
    }
}
```

`API.Service.WebApi.Tests/Controllers/ReportePermisosTests.cs`:

```csharp
using System.Reflection;
using API.Service.WebApi.Authorization;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    /// <summary>Fija por reflexión la forma del controller: el permiso depende del reporte, así que se evalúa dentro de cada acción.</summary>
    public class ReportePermisosTests
    {
        private static IEnumerable<MethodInfo> MetodosPublicos() =>
            typeof(ReporteController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        [Fact]
        public void ElControllerExigeAutenticacionYCuelgaDeApiReporte()
        {
            Assert.NotNull(typeof(ReporteController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.NotNull(typeof(ReporteController).GetCustomAttribute<ApiControllerAttribute>());
            Assert.Equal("api/Reporte", typeof(ReporteController).GetCustomAttribute<RouteAttribute>()!.Template);
        }

        [Fact]
        public void NingunaAccionUsaElAtributoDePermisoPorModuloFijo()
        {
            Assert.Null(typeof(ReporteController).GetCustomAttribute<RequierePermisoAttribute>());
            Assert.All(MetodosPublicos(), m => Assert.Null(m.GetCustomAttribute<RequierePermisoAttribute>()));
        }

        [Theory]
        [InlineData("Catalogo", "GET", "")]
        [InlineData("Definicion", "GET", "{codigo}")]
        [InlineData("Ejecutar", "POST", "{codigo}/ejecutar")]
        public void LasRutasYElVerboSonLosEsperados(string accion, string verbo, string ruta)
        {
            var metodo = typeof(ReporteController).GetMethod(accion)!;
            var http = metodo.GetCustomAttributes().OfType<HttpMethodAttribute>().Single();

            Assert.Equal(verbo, http.HttpMethods.Single());
            Assert.Equal(ruta, http.Template ?? "");
        }
    }
}
```

- [ ] **Step 2: Ver que fallan** (no compilan: faltan las clases): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~CatalogoReportesTests|FullyQualifiedName~ReporteApplicationTests|FullyQualifiedName~ReporteControllerTests|FullyQualifiedName~ReportePermisosTests"`.

- [ ] **Step 3: Implementar.**

`API.Domain.Core/Reportes/CatalogoReportes.cs`:

```csharp
using API.Domain.Interface.Reportes;

namespace API.Domain.Core.Reportes
{
    /// <summary>Reúne todas las definiciones registradas por DI (una por cada <see cref="IReporte"/>).</summary>
    public sealed class CatalogoReportes : ICatalogoReportes
    {
        private readonly IReadOnlyList<IReporte> _reportes;

        public CatalogoReportes(IEnumerable<IReporte> reportes)
        {
            var lista = reportes
                .OrderBy(r => r.Definicion.Familia, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(r => r.Definicion.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var repetido = lista.GroupBy(r => r.Definicion.Codigo, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (repetido is not null)
                throw new InvalidOperationException($"Código de reporte duplicado: {repetido.Key}");

            _reportes = lista;
        }

        public IReadOnlyList<IReporte> Todos => _reportes;

        public IReporte? Buscar(string codigo) =>
            _reportes.FirstOrDefault(r => string.Equals(r.Definicion.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
    }
}
```

`API.Application.DTO/reporte/ReporteResumenDTO.cs`:

```csharp
namespace API.Application.DTO.reporte
{
    public class ReporteResumenDTO
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Familia { get; set; } = "";
    }
}
```

`API.Application.Interface/API.Application.Interface.csproj`: dentro del `<ItemGroup>` de `ProjectReference` agrega `<ProjectReference Include="..\API.Domain.Interface\API.Domain.Interface.csproj" />`.

`API.Application.Interface/IReporteApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Domain.Interface.Reportes;

namespace API.Application.Interface
{
    public interface IReporteApplication
    {
        /// <summary>Reportes cuyo módulo de permiso cumple el predicado.</summary>
        Respuesta<List<ReporteResumenDTO>> ObtenerCatalogo(Func<string, bool> puedeVerModulo);

        /// <summary>Definición del reporte; Resultado=false con «El reporte 'x' no existe.» si no existe.</summary>
        Respuesta<ReporteDefinicion> ObtenerDefinicion(string codigo);

        Task<Respuesta<ResultadoReporte>> EjecutarAsync(string codigo, SolicitudReporte solicitud);
    }
}
```

`API.Application.Main/ReporteApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface.Reportes;

namespace API.Application.Main
{
    public class ReporteApplication : IReporteApplication
    {
        private readonly ICatalogoReportes _catalogo;
        private readonly IMotorReportes _motor;

        public ReporteApplication(ICatalogoReportes catalogo, IMotorReportes motor)
        {
            _catalogo = catalogo;
            _motor = motor;
        }

        public Respuesta<List<ReporteResumenDTO>> ObtenerCatalogo(Func<string, bool> puedeVerModulo)
        {
            var respuesta = new Respuesta<List<ReporteResumenDTO>>();
            try
            {
                respuesta.Dato = _catalogo.Todos
                    .Where(r => puedeVerModulo(r.Definicion.Modulo))
                    .Select(r => new ReporteResumenDTO
                    {
                        Codigo = r.Definicion.Codigo,
                        Nombre = r.Definicion.Nombre,
                        Descripcion = r.Definicion.Descripcion,
                        Familia = r.Definicion.Familia
                    })
                    .ToList();
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public Respuesta<ReporteDefinicion> ObtenerDefinicion(string codigo)
        {
            var respuesta = new Respuesta<ReporteDefinicion>();
            var reporte = _catalogo.Buscar(codigo);
            if (reporte is null)
            {
                respuesta.Mensaje = new ReporteNoExisteException(codigo).Message;
                return respuesta;
            }

            respuesta.Dato = reporte.Definicion;
            respuesta.Resultado = true;
            respuesta.Mensaje = "Consulta realizada correctamente.";
            return respuesta;
        }

        public async Task<Respuesta<ResultadoReporte>> EjecutarAsync(string codigo, SolicitudReporte solicitud)
        {
            var respuesta = new Respuesta<ResultadoReporte>();
            try
            {
                var reporte = _catalogo.Buscar(codigo) ?? throw new ReporteNoExisteException(codigo);
                respuesta.Dato = await _motor.EjecutarAsync(reporte, solicitud);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
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

`API.Service.WebApi/Controllers/ReporteController.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.reporte;
using API.Application.Interface;
using API.Domain.Interface;
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    /// <summary>
    /// Catálogo y ejecución de reportes. El permiso depende del reporte (cada familia tiene su módulo), así que no se usa
    /// [RequierePermiso]: cada acción evalúa Accion.Ver sobre el módulo de la definición ANTES de ejecutar nada.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Reporte")]
    public class ReporteController : ControllerBase
    {
        private readonly IReporteApplication _app;

        public ReporteController(IReporteApplication app)
        {
            _app = app;
        }

        [HttpGet]
        public ActionResult<Respuesta<List<ReporteResumenDTO>>> Catalogo()
        {
            var respuesta = _app.ObtenerCatalogo(modulo => PermisoUsuario.Tiene(User, modulo, Accion.Ver));
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpGet("{codigo}")]
        public ActionResult<Respuesta<ReporteDefinicion>> Definicion([FromRoute] string codigo)
        {
            var definicion = _app.ObtenerDefinicion(codigo);
            var error = Verificar(definicion);
            return error ?? Ok(definicion);
        }

        [HttpPost("{codigo}/ejecutar")]
        public async Task<ActionResult<Respuesta<ResultadoReporte>>> Ejecutar([FromRoute] string codigo, [FromBody] SolicitudReporte solicitud)
        {
            if (solicitud is null)
                return BadRequest(new Respuesta<ResultadoReporte> { Mensaje = "La solicitud del reporte es obligatoria." });

            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;

            var respuesta = await _app.EjecutarAsync(codigo, solicitud);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        /// <summary>null si el reporte existe y el usuario puede verlo; si no, el 404 o el 403 que corresponde.</summary>
        private ActionResult? Verificar(Respuesta<ReporteDefinicion> definicion)
        {
            if (!definicion.Resultado) return NotFound(definicion);
            if (!PermisoUsuario.Tiene(User, definicion.Dato.Modulo, Accion.Ver)) return PermisoUsuario.Denegado();
            return null;
        }
    }
}
```

`API.Service.WebApi/Startup.cs`: agrega los `using API.Domain.Core.Reportes;`, `using API.Domain.Core.Reportes.Definiciones;` y `using API.Domain.Interface.Reportes;` al inicio (si no están) y, justo después de la línea `services.AddTransient<IRepositorioGenerico<ReporteVista, int>, ReporteVistaRepositorio>();`:

```csharp
            services.AddTransient<IMotorReportes, MotorReportes>();
            services.AddTransient<IReporte, VentasPorDocumentoReporte>();
            services.AddTransient<IReporte, ExistenciasValorReporte>();
            services.AddTransient<ICatalogoReportes, CatalogoReportes>();
            services.AddTransient<IReporteApplication, ReporteApplication>();
```

- [ ] **Step 4: Ver que pasan.** El `--filter` del Step 2 en verde (≈ 30 pruebas); suite completa en verde; `dotnet build API.sln` sin errores (compila también `API.Service.WebApi` con el DI nuevo).

- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Core/Reportes/CatalogoReportes.cs API.Application.DTO/reporte/ReporteResumenDTO.cs API.Application.Interface/IReporteApplication.cs API.Application.Interface/API.Application.Interface.csproj API.Application.Main/ReporteApplication.cs API.Service.WebApi/Controllers/ReporteController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/CatalogoReportesTests.cs API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs API.Service.WebApi.Tests/Controllers/ReportePermisosTests.cs
git commit -m "feat(reporteria): catalogo, aplicacion y controlador de reportes con permiso por modulo

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 2 — Exportación a CSV, Excel y PDF (API)

### Task 8: Proyecto de exportación, CSV y endpoint `exportar`

**Files:**
- Create: `API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj`, `API.Infraestructure.Exportacion/CsvExportador.cs`
- Modify: `API.sln` (agregar el proyecto), `API.Service.WebApi/API.Service.WebApi.csproj` y `API.Service.WebApi.Tests/API.Service.WebApi.Tests.csproj` (referencia al proyecto nuevo)
- Create: `API.Application.DTO/reporte/ArchivoReporteDTO.cs`
- Modify: `API.Application.Interface/IReporteApplication.cs`, `API.Application.Main/ReporteApplication.cs`, `API.Service.WebApi/Controllers/ReporteController.cs`, `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Infraestructura/CsvExportadorTests.cs`; modificar `API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs` y `API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs`, `ReportePermisosTests.cs`

**Interfaces:**
- Consumes: `IExportadorReporte`, `ContextoExportacion`, `ResultadoReporte`, `ExportarReporteRequest`, `ReporteInvalidoException`, `ReporteNoExisteException`.
- Produces:
  - `API.Infraestructure.Exportacion.CsvExportador : IExportadorReporte` (`Formato "csv"`, `ContentType "text/csv"`, `Extension "csv"`; ver Rulings 7).
  - `ArchivoReporteDTO { byte[] Contenido; string ContentType; string NombreArchivo }` en `API.Application.DTO.reporte`.
  - `IReporteApplication.ExportarAsync(string codigo, ExportarReporteRequest request, string formato, string empresa, string usuario) → Task<Respuesta<ArchivoReporteDTO>>`.
  - `ReporteApplication(ICatalogoReportes catalogo, IMotorReportes motor, IEnumerable<IExportadorReporte> exportadores)` (constructor ampliado).
  - `ReporteController(IReporteApplication app, IConfiguration config)` y la acción `Exportar` (`POST api/Reporte/{codigo}/exportar?formato=`), que devuelve el archivo (`File(...)`); la empresa sale de la clave de configuración `Reportes:Empresa` (por defecto «Sistema de Gestión»).
  - Mensajes: «El formato 'x' no es válido. Use xlsx, pdf o csv.», «El gráfico es demasiado grande (máximo 2 MB).», «El gráfico no es una imagen PNG válida.», «La solicitud del reporte es obligatoria.».
  - Texto de filtros del encabezado (`ContextoExportacion.FiltrosTexto`): `«Fechas: 2026-01-01 a 2026-01-31; Cliente: C1»`, `desde X` / `hasta Y` si falta un extremo, el título de la opción en las listas, y `«Sin filtros»` si no hay ninguno.

- [ ] **Step 1: Proyecto nuevo.** Crea `API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\API.Domain.Interface\API.Domain.Interface.csproj" />
  </ItemGroup>

</Project>
```

y regístralo (desde la carpeta del repo API):

```powershell
dotnet sln API.sln add API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj --solution-folder Infraestructure
dotnet add API.Service.WebApi/API.Service.WebApi.csproj reference API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj
dotnet add API.Service.WebApi.Tests/API.Service.WebApi.Tests.csproj reference API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj
git diff --stat -- API.sln
```

Revisa que el diff de `API.sln` solo agregue el proyecto (sin reformatear lo demás) y que quede dentro de la carpeta de solución `Infraestructure`.

- [ ] **Step 2: Pruebas que fallan.**

`API.Service.WebApi.Tests/Infraestructura/CsvExportadorTests.cs`:

```csharp
using System.Text;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Exportacion;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructura
{
    public class CsvExportadorTests
    {
        private static readonly ReporteDefinicion Def = new("t", "Prueba", "d", "F", "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());
        private static readonly ContextoExportacion Ctx = new("Empresa", "tester", new DateTime(2026, 11, 8, 10, 30, 0), "Sin filtros", null);

        private static readonly ColumnaReporte CFecha = new("fecha", "Fecha", TipoColumna.Fecha);
        private static readonly ColumnaReporte CTexto = new("cliente", "Cliente", TipoColumna.Texto);
        private static readonly ColumnaReporte CCant = new("cantidad", "Cantidad", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma);
        private static readonly ColumnaReporte CDinero = new("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma);

        private static (byte[] Bytes, string[] Lineas) Generar(ResultadoReporte r)
        {
            var bytes = new CsvExportador().Exportar(r, Def, Ctx);
            var texto = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            return (bytes, texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
        }

        private static FilaReporte Fila(DateTime fecha, string cliente, decimal cantidad, decimal importe) =>
            ReporteFalso.Fila(("fecha", fecha), ("cliente", cliente), ("cantidad", cantidad), ("importe", importe));

        [Fact]
        public void LosMetadatosSonLosDelCsv()
        {
            var e = new CsvExportador();

            Assert.Equal("csv", e.Formato);
            Assert.Equal("text/csv", e.ContentType);
            Assert.Equal("csv", e.Extension);
        }

        [Fact]
        public void EmpiezaConBomUtf8YLlevaLosTitulosComoEncabezado()
        {
            var r = new ResultadoReporte { Columnas = { CFecha, CTexto, CCant, CDinero } };

            var (bytes, lineas) = Generar(r);

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Equal("Fecha,Cliente,Cantidad,Importe", lineas[0]);
        }

        [Fact]
        public void FormateaFechasYNumerosConPuntoDecimalSinTotales()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CFecha, CTexto, CCant, CDinero },
                Filas = { Fila(new DateTime(2026, 1, 5), "C1", 2.5m, 1234.5m), Fila(new DateTime(2026, 1, 6), "C2", 3m, 0.123456m) },
                Totales = { ["cantidad"] = 5.5m, ["importe"] = 1234.623456m }
            };

            var (_, lineas) = Generar(r);

            Assert.Equal(3, lineas.Length);   // encabezado + 2 filas: sin fila de total
            Assert.Equal("2026-01-05,C1,2.5,1234.50", lineas[1]);
            Assert.Equal("2026-01-06,C2,3,0.123456", lineas[2]);
        }

        [Fact]
        public void EscapaComasComillasYSaltosDeLinea()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CTexto },
                Filas = { ReporteFalso.Fila(("cliente", "Pérez, \"Juan\"\nJr.")) }
            };

            var bytes = new CsvExportador().Exportar(r, Def, Ctx);
            var texto = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);

            Assert.Contains("\"Pérez, \"\"Juan\"\"\nJr.\"", texto);
        }

        [Fact]
        public void NeutralizaLasFormulasEnLasColumnasDeTexto()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CTexto },
                Filas = { ReporteFalso.Fila(("cliente", "=HYPERLINK(\"x\")")), ReporteFalso.Fila(("cliente", "@cmd")), ReporteFalso.Fila(("cliente", "-5 cajas")) }
            };

            var (_, lineas) = Generar(r);

            Assert.Equal("\"'=HYPERLINK(\"\"x\"\")\"", lineas[1]);
            Assert.Equal("'@cmd", lineas[2]);
            Assert.Equal("'-5 cajas", lineas[3]);
        }

        [Fact]
        public void LosNumerosNegativosNoSeTocan()
        {
            var r = new ResultadoReporte { Columnas = { CDinero }, Filas = { ReporteFalso.Fila(("importe", -56m)) } };

            var (_, lineas) = Generar(r);

            Assert.Equal("-56.00", lineas[1]);
        }

        [Fact]
        public void ConAgrupacion_AgregaColumnasGrupoYUnaFilaPorDetalle()
        {
            var hoja1 = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2, Filas = { Fila(new DateTime(2026, 1, 5), "A", 1m, 10m), Fila(new DateTime(2026, 1, 6), "A", 2m, 20m) } };
            var mes1 = new GrupoReporte { Campo = "fecha", Titulo = "Fecha", Etiqueta = "2026-01", Clave = "2026-01", Conteo = 2, Filas = hoja1.Filas };
            var raiz = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2, Hijos = { mes1 } };
            var r = new ResultadoReporte { Columnas = { CFecha, CCant, CDinero }, Grupos = { raiz } };

            var (_, lineas) = Generar(r);

            Assert.Equal("Grupo 1,Grupo 2,Fecha,Cantidad,Importe", lineas[0]);
            Assert.Equal("A,2026-01,2026-01-05,1,10.00", lineas[1]);
            Assert.Equal("A,2026-01,2026-01-06,2,20.00", lineas[2]);
            Assert.Equal(3, lineas.Length);
        }

        [Fact]
        public void ConAgrupacionYSoloResumen_UnaFilaPorGrupoHojaConSusSubtotales()
        {
            var hoja = new GrupoReporte
            {
                Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2,
                Subtotales = { ["cantidad"] = 3m, ["importe"] = 30m }
            };
            var r = new ResultadoReporte { Columnas = { CFecha, CCant, CDinero }, Grupos = { hoja } };

            var (_, lineas) = Generar(r);

            Assert.Equal("Grupo 1,Fecha,Cantidad,Importe", lineas[0]);
            Assert.Equal("A,,3,30.00", lineas[1]);
        }
    }
}
```

`API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs` — (a) actualiza el constructor: agrega a los campos `private readonly Mock<IExportadorReporte> _csv = new();` y crea `_app` con `new ReporteApplication(_catalogo.Object, _motor.Object, new[] { _csv.Object })`, con `_csv.SetupGet(e => e.Formato).Returns("csv"); _csv.SetupGet(e => e.ContentType).Returns("text/csv"); _csv.SetupGet(e => e.Extension).Returns("csv");` en el constructor de la clase de prueba; (b) agrega al final de la clase:

```csharp
        // ---------- exportar ----------

        private static ExportarReporteRequest Pedido(string? grafico = null, Action<SolicitudReporte>? ajustar = null)
        {
            var req = new ExportarReporteRequest { GraficoPng = grafico };
            ajustar?.Invoke(req.Solicitud);
            return req;
        }

        // PNG mínimo válido de 1x1.
        private const string PngDe1x1 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        [Fact]
        public async Task Exportar_GeneraElArchivoConElExportadorDelFormatoYSuContexto()
        {
            var resultado = new ResultadoReporte();
            var solicitud = new SolicitudReporte();
            solicitud.Filtros["fechas"] = new FiltroValor { Desde = "2026-01-01", Hasta = "2026-01-31" };
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, solicitud)).ReturnsAsync(resultado);
            ContextoExportacion? contexto = null;
            _csv.Setup(e => e.Exportar(resultado, It.IsAny<ReporteDefinicion>(), It.IsAny<ContextoExportacion>()))
                .Callback<ResultadoReporte, ReporteDefinicion, ContextoExportacion>((_, _, c) => contexto = c)
                .Returns(new byte[] { 1, 2, 3 });

            var r = await _app.ExportarAsync("ventas", new ExportarReporteRequest { Solicitud = solicitud, GraficoPng = PngDe1x1 }, "CSV", "Mi Empresa", "ana");

            Assert.True(r.Resultado);
            Assert.Equal(new byte[] { 1, 2, 3 }, r.Dato.Contenido);
            Assert.Equal("text/csv", r.Dato.ContentType);
            Assert.Matches(@"^ventas-\d{8}-\d{4}\.csv$", r.Dato.NombreArchivo);
            Assert.Equal("Mi Empresa", contexto!.Empresa);
            Assert.Equal("ana", contexto.Usuario);
            Assert.Equal("Fechas: 2026-01-01 a 2026-01-31", contexto.FiltrosTexto);
            Assert.NotNull(contexto.GraficoPng);
        }

        [Fact]
        public async Task Exportar_SinFiltros_DiceSinFiltros()
        {
            ContextoExportacion? contexto = null;
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, It.IsAny<SolicitudReporte>())).ReturnsAsync(new ResultadoReporte());
            _csv.Setup(e => e.Exportar(It.IsAny<ResultadoReporte>(), It.IsAny<ReporteDefinicion>(), It.IsAny<ContextoExportacion>()))
                .Callback<ResultadoReporte, ReporteDefinicion, ContextoExportacion>((_, _, c) => contexto = c)
                .Returns(new byte[] { 1 });

            await _app.ExportarAsync("ventas", Pedido(), "csv", "E", "u");

            Assert.Equal("Sin filtros", contexto!.FiltrosTexto);
            Assert.Null(contexto.GraficoPng);
        }

        [Fact]
        public async Task Exportar_FormatoInvalido_NoEjecutaNada()
        {
            var r = await _app.ExportarAsync("ventas", Pedido(), "docx", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("El formato 'docx' no es válido. Use xlsx, pdf o csv.", r.Mensaje);
            _motor.Verify(m => m.EjecutarAsync(It.IsAny<IReporte>(), It.IsAny<SolicitudReporte>()), Times.Never);
        }

        [Fact]
        public async Task Exportar_ReporteInexistente_DiceQueNoExiste()
        {
            var r = await _app.ExportarAsync("nada", Pedido(), "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
        }

        [Fact]
        public async Task Exportar_SolicitudNula_Lanza400DeNegocio()
        {
            var r = await _app.ExportarAsync("ventas", null!, "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("La solicitud del reporte es obligatoria.", r.Mensaje);
        }

        [Theory]
        [InlineData("no-es-base64!!", "El gráfico no es una imagen PNG válida.")]
        [InlineData("aGVsbG8gbXVuZG8=", "El gráfico no es una imagen PNG válida.")]
        public async Task Exportar_GraficoInvalido_Rechaza(string grafico, string mensaje)
        {
            var r = await _app.ExportarAsync("ventas", Pedido(grafico), "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal(mensaje, r.Mensaje);
        }

        [Fact]
        public async Task Exportar_GraficoMayorA2Mb_Rechaza()
        {
            var grande = Convert.ToBase64String(new byte[2 * 1024 * 1024 + 10]);

            var r = await _app.ExportarAsync("ventas", Pedido(grande), "csv", "E", "u");

            Assert.False(r.Resultado);
            Assert.Equal("El gráfico es demasiado grande (máximo 2 MB).", r.Mensaje);
        }

        [Fact]
        public async Task Exportar_AceptaElPrefijoDataUri()
        {
            _motor.Setup(m => m.EjecutarAsync(_ventas.Object, It.IsAny<SolicitudReporte>())).ReturnsAsync(new ResultadoReporte());
            _csv.Setup(e => e.Exportar(It.IsAny<ResultadoReporte>(), It.IsAny<ReporteDefinicion>(), It.IsAny<ContextoExportacion>())).Returns(new byte[] { 1 });

            var r = await _app.ExportarAsync("ventas", Pedido("data:image/png;base64," + PngDe1x1), "csv", "E", "u");

            Assert.True(r.Resultado);
        }
```

(c) agrega al principio del archivo `using API.Domain.Interface.Reportes;` si no está.

`API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs` — (a) cambia `Crear` para el constructor nuevo:

```csharp
        private ReporteController Crear(ClaimsPrincipal? usuario = null, IConfiguration? config = null) =>
            new(_app.Object, config ?? new ConfigurationBuilder().Build())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario ?? Usuario() } }
            };
```

(con `using Microsoft.Extensions.Configuration;`); (b) agrega al final de la clase:

```csharp
        // ---------- exportar ----------

        private static ArchivoReporteDTO Archivo() => new() { Contenido = new byte[] { 9, 8 }, ContentType = "text/csv", NombreArchivo = "ventas-20261108-1030.csv" };

        [Fact]
        public async Task Exportar_ConPermiso_DevuelveElArchivoYUsaLaEmpresaConfigurada()
        {
            var req = new ExportarReporteRequest();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Reportes:Empresa"] = "Mi Empresa" }).Build();
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ExportarAsync("ventas", req, "csv", "Mi Empresa", "tester"))
                .ReturnsAsync(new Respuesta<ArchivoReporteDTO> { Resultado = true, Dato = Archivo() });

            var r = await Crear(Usuario("ReportesVentas:V"), config).Exportar("ventas", "csv", req);

            var archivo = Assert.IsType<FileContentResult>(r);
            Assert.Equal("text/csv", archivo.ContentType);
            Assert.Equal("ventas-20261108-1030.csv", archivo.FileDownloadName);
            Assert.Equal(new byte[] { 9, 8 }, archivo.FileContents);
        }

        [Fact]
        public async Task Exportar_SinEmpresaConfigurada_UsaSistemaDeGestion()
        {
            var req = new ExportarReporteRequest();
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ExportarAsync("ventas", req, "csv", "Sistema de Gestión", "tester"))
                .ReturnsAsync(new Respuesta<ArchivoReporteDTO> { Resultado = true, Dato = Archivo() });

            var r = await Crear(Usuario(superusuario: true)).Exportar("ventas", "csv", req);

            Assert.IsType<FileContentResult>(r);
        }

        [Fact]
        public async Task Exportar_SinPermiso_Devuelve403YNoGeneraNada()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(Usuario("ReportesCompras:V")).Exportar("ventas", "csv", new ExportarReporteRequest());

            Es403(r);
            _app.Verify(a => a.ExportarAsync(It.IsAny<string>(), It.IsAny<ExportarReporteRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Exportar_ErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<ArchivoReporteDTO> { Resultado = false, Mensaje = "El formato 'x' no es válido. Use xlsx, pdf o csv." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ExportarAsync("ventas", It.IsAny<ExportarReporteRequest>(), "x", It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).Exportar("ventas", "x", new ExportarReporteRequest());

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r).Value);
        }

        [Fact]
        public async Task Exportar_ReporteInexistente_DevuelveNotFound()
        {
            _app.Setup(a => a.ObtenerDefinicion("x")).Returns(new Respuesta<ReporteDefinicion> { Resultado = false, Mensaje = "El reporte 'x' no existe." });

            var r = await Crear().Exportar("x", "csv", new ExportarReporteRequest());

            Assert.IsType<NotFoundObjectResult>(r);
        }

        [Fact]
        public async Task Exportar_CuerpoNulo_DevuelveBadRequest()
        {
            var r = await Crear().Exportar("ventas", "csv", null!);

            Assert.IsType<BadRequestObjectResult>(r);
        }
```

`API.Service.WebApi.Tests/Controllers/ReportePermisosTests.cs` — agrega la fila `[InlineData("Exportar", "POST", "{codigo}/exportar")]` a la `Theory` de rutas.

- [ ] **Step 3: Ver que fallan** (no compilan): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~CsvExportadorTests|FullyQualifiedName~ReporteApplicationTests|FullyQualifiedName~ReporteControllerTests|FullyQualifiedName~ReportePermisosTests"`.

- [ ] **Step 4: Implementar.**

`API.Infraestructure.Exportacion/CsvExportador.cs`:

```csharp
using System.Globalization;
using System.Text;
using API.Domain.Interface.Reportes;

namespace API.Infraestructure.Exportacion
{
    /// <summary>
    /// CSV para importar en otros sistemas (UTF-8 con BOM para que Excel lo abra bien). Solo datos: sin filas de total.
    /// Con agrupación agrega las columnas "Grupo 1" y "Grupo 2" y una fila por detalle (o por grupo hoja con sus subtotales si SoloResumen).
    /// </summary>
    public sealed class CsvExportador : IExportadorReporte
    {
        public string Formato => "csv";
        public string ContentType => "text/csv";
        public string Extension => "csv";

        public byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto)
        {
            var sb = new StringBuilder();
            var niveles = Profundidad(resultado.Grupos);

            var encabezados = new List<string>();
            for (var i = 1; i <= niveles; i++) encabezados.Add($"Grupo {i}");
            encabezados.AddRange(resultado.Columnas.Select(c => c.Titulo));
            Linea(sb, encabezados);

            if (resultado.Grupos.Count == 0)
            {
                foreach (var fila in resultado.Filas)
                    Linea(sb, resultado.Columnas.Select(c => Celda(c, fila.TryGetValue(c.Campo, out var v) ? v : null)));
            }
            else
            {
                Recorrer(resultado.Grupos, new List<string>(), niveles, resultado.Columnas, sb);
            }

            var preambulo = new UTF8Encoding(true).GetPreamble();
            var cuerpo = new UTF8Encoding(false).GetBytes(sb.ToString());
            return preambulo.Concat(cuerpo).ToArray();
        }

        private static void Recorrer(List<GrupoReporte> grupos, List<string> ruta, int niveles, IReadOnlyList<ColumnaReporte> columnas, StringBuilder sb)
        {
            foreach (var g in grupos)
            {
                var actual = new List<string>(ruta) { g.Etiqueta };
                if (g.Hijos.Count > 0)
                {
                    Recorrer(g.Hijos, actual, niveles, columnas, sb);
                    continue;
                }

                var prefijo = Enumerable.Range(0, niveles).Select(i => i < actual.Count ? actual[i] : "").ToList();
                if (g.Filas.Count > 0)
                {
                    foreach (var fila in g.Filas)
                        Linea(sb, prefijo.Concat(columnas.Select(c => Celda(c, fila.TryGetValue(c.Campo, out var v) ? v : null))));
                }
                else
                {
                    // SoloResumen: una fila por grupo hoja con sus subtotales (las columnas sin subtotal van vacías).
                    Linea(sb, prefijo.Concat(columnas.Select(c =>
                        c.Totaliza != Totalizacion.Ninguna && g.Subtotales.TryGetValue(c.Campo, out var s) ? Celda(c, s) : "")));
                }
            }
        }

        private static int Profundidad(List<GrupoReporte> grupos) =>
            grupos.Count == 0 ? 0 : 1 + grupos.Max(g => Profundidad(g.Hijos));

        private static string Celda(ColumnaReporte columna, object? valor)
        {
            if (valor is null) return "";
            if (valor is DateTime fecha) return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (ComparadorNumerico.EsNumero(valor))
            {
                var n = Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
                return columna.Tipo switch
                {
                    TipoColumna.Dinero => n.ToString("0.00####", CultureInfo.InvariantCulture),
                    TipoColumna.Entero => n.ToString("0", CultureInfo.InvariantCulture),
                    _ => n.ToString("0.######", CultureInfo.InvariantCulture)
                };
            }

            var texto = valor.ToString() ?? "";
            // Neutraliza fórmulas (=, +, -, @) en las columnas de texto: Excel las ejecutaría al abrir el archivo.
            if (columna.Tipo == TipoColumna.Texto && texto.Length > 0 && "=+-@".Contains(texto[0]))
                texto = "'" + texto;
            return texto;
        }

        private static void Linea(StringBuilder sb, IEnumerable<string> celdas)
        {
            sb.Append(string.Join(",", celdas.Select(Escapar)));
            sb.Append("\r\n");
        }

        private static string Escapar(string texto) =>
            texto.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + texto.Replace("\"", "\"\"") + "\"" : texto;
    }

    internal static class ComparadorNumerico
    {
        public static bool EsNumero(object valor) =>
            valor is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}
```

`API.Application.DTO/reporte/ArchivoReporteDTO.cs`:

```csharp
namespace API.Application.DTO.reporte
{
    public class ArchivoReporteDTO
    {
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public string NombreArchivo { get; set; } = "reporte";
    }
}
```

`API.Application.Interface/IReporteApplication.cs`: agrega el miembro

```csharp
        /// <summary>Ejecuta el reporte con la solicitud y lo genera en el formato pedido (xlsx, pdf o csv).</summary>
        Task<Respuesta<ArchivoReporteDTO>> ExportarAsync(string codigo, ExportarReporteRequest request, string formato, string empresa, string usuario);
```

`API.Application.Main/ReporteApplication.cs`: cambia el constructor y agrega la implementación.

```csharp
        private readonly IEnumerable<IExportadorReporte> _exportadores;

        public ReporteApplication(ICatalogoReportes catalogo, IMotorReportes motor, IEnumerable<IExportadorReporte> exportadores)
        {
            _catalogo = catalogo;
            _motor = motor;
            _exportadores = exportadores;
        }
```

```csharp
        private const int MaxGraficoBytes = 2 * 1024 * 1024;

        public async Task<Respuesta<ArchivoReporteDTO>> ExportarAsync(string codigo, ExportarReporteRequest request, string formato, string empresa, string usuario)
        {
            var respuesta = new Respuesta<ArchivoReporteDTO>();
            try
            {
                if (request?.Solicitud is null)
                    throw new ReporteInvalidoException("La solicitud del reporte es obligatoria.");

                var reporte = _catalogo.Buscar(codigo) ?? throw new ReporteNoExisteException(codigo);
                var exportador = _exportadores.FirstOrDefault(e => string.Equals(e.Formato, formato, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ReporteInvalidoException($"El formato '{formato}' no es válido. Use xlsx, pdf o csv.");
                var grafico = DecodificarGrafico(request.GraficoPng);

                var resultado = await _motor.EjecutarAsync(reporte, request.Solicitud);

                var ahora = DateTime.Now;
                var contexto = new ContextoExportacion(empresa, usuario, ahora, TextoFiltros(reporte.Definicion, request.Solicitud), grafico);
                respuesta.Dato = new ArchivoReporteDTO
                {
                    Contenido = exportador.Exportar(resultado, reporte.Definicion, contexto),
                    ContentType = exportador.ContentType,
                    NombreArchivo = $"{reporte.Definicion.Codigo}-{ahora:yyyyMMdd-HHmm}.{exportador.Extension}"
                };
                respuesta.Resultado = true;
                respuesta.Mensaje = "Archivo generado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        private static byte[]? DecodificarGrafico(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;

            var texto = base64.Trim();
            var coma = texto.IndexOf(',');
            if (texto.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma >= 0) texto = texto[(coma + 1)..];

            var demasiadoGrande = new ReporteInvalidoException("El gráfico es demasiado grande (máximo 2 MB).");
            if (texto.Length > MaxGraficoBytes / 3 * 4 + 8) throw demasiadoGrande;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(texto); }
            catch (FormatException) { throw new ReporteInvalidoException("El gráfico no es una imagen PNG válida."); }

            if (bytes.Length > MaxGraficoBytes) throw demasiadoGrande;
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
                throw new ReporteInvalidoException("El gráfico no es una imagen PNG válida.");
            return bytes;
        }

        private static string TextoFiltros(ReporteDefinicion definicion, SolicitudReporte solicitud)
        {
            var partes = new List<string>();
            foreach (var filtro in definicion.Filtros)
            {
                if (solicitud.Filtros is null || !solicitud.Filtros.TryGetValue(filtro.Campo, out var v) || v is null) continue;

                if (filtro.Tipo == TipoFiltro.RangoFechas)
                {
                    var desde = v.Desde?.Trim();
                    var hasta = v.Hasta?.Trim();
                    if (!string.IsNullOrEmpty(desde) && !string.IsNullOrEmpty(hasta)) partes.Add($"{filtro.Titulo}: {desde} a {hasta}");
                    else if (!string.IsNullOrEmpty(desde)) partes.Add($"{filtro.Titulo}: desde {desde}");
                    else if (!string.IsNullOrEmpty(hasta)) partes.Add($"{filtro.Titulo}: hasta {hasta}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(v.Valor)) continue;
                var texto = filtro.Tipo == TipoFiltro.Lista
                    ? filtro.Opciones?.FirstOrDefault(o => o.Valor == v.Valor)?.Titulo ?? v.Valor
                    : v.Valor.Trim();
                partes.Add($"{filtro.Titulo}: {texto}");
            }
            return partes.Count == 0 ? "Sin filtros" : string.Join("; ", partes);
        }
```

En `ReporteApplicationTests` el filtro `fechas` debe existir en la definición del reporte falso: en `Def(...)` de esa clase de prueba cambia `Array.Empty<FiltroReporte>()` por `new[] { new FiltroReporte("fechas", "Fechas", TipoFiltro.RangoFechas) }` (en la prueba `Exportar_GeneraElArchivo...`).

`API.Service.WebApi/Controllers/ReporteController.cs`: agrega `using Microsoft.Extensions.Configuration;`, el campo y el constructor nuevo y la acción:

```csharp
        private readonly IConfiguration _config;

        public ReporteController(IReporteApplication app, IConfiguration config)
        {
            _app = app;
            _config = config;
        }
```

```csharp
        [HttpPost("{codigo}/exportar")]
        public async Task<IActionResult> Exportar([FromRoute] string codigo, [FromQuery] string formato, [FromBody] ExportarReporteRequest request)
        {
            if (request is null)
                return BadRequest(new Respuesta<ArchivoReporteDTO> { Mensaje = "La solicitud del reporte es obligatoria." });

            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;

            var empresa = _config["Reportes:Empresa"] ?? "Sistema de Gestión";
            var respuesta = await _app.ExportarAsync(codigo, request, formato, empresa, User.Identity?.Name ?? "");
            if (!respuesta.Resultado) return BadRequest(respuesta);

            return File(respuesta.Dato.Contenido, respuesta.Dato.ContentType, respuesta.Dato.NombreArchivo);
        }
```

`API.Service.WebApi/Startup.cs`: agrega `using API.Infraestructure.Exportacion;` y, tras `services.AddTransient<IReporteApplication, ReporteApplication>();`:

```csharp
            services.AddTransient<IExportadorReporte, CsvExportador>();
```

- [ ] **Step 5: Ver que pasan.** El `--filter` del Step 3 en verde; suite completa en verde; `dotnet build API.sln` sin errores.

- [ ] **Step 6: Commit.**

```bash
git add API.sln API.Infraestructure.Exportacion API.Service.WebApi/API.Service.WebApi.csproj API.Service.WebApi.Tests/API.Service.WebApi.Tests.csproj API.Application.DTO/reporte/ArchivoReporteDTO.cs API.Application.Interface/IReporteApplication.cs API.Application.Main/ReporteApplication.cs API.Service.WebApi/Controllers/ReporteController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Infraestructura/CsvExportadorTests.cs API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs API.Service.WebApi.Tests/Controllers/ReportePermisosTests.cs
git commit -m "feat(reporteria): exportacion a CSV y endpoint exportar

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Exportador a Excel (ClosedXML) y aplanador del resultado

**Files:**
- Create: `API.Domain.Interface/Reportes/AplanadorResultado.cs`
- Modify: `API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj` (paquete ClosedXML)
- Create: `API.Infraestructure.Exportacion/FormatoValores.cs`, `API.Infraestructure.Exportacion/ExcelExportador.cs`
- Modify: `API.Service.WebApi/Startup.cs` (DI)
- Test: `API.Service.WebApi.Tests/Domain/AplanadorResultadoTests.cs`, `API.Service.WebApi.Tests/Infraestructura/ExcelExportadorTests.cs`

**Interfaces:**
- Consumes: `ResultadoReporte`, `GrupoReporte`, `ColumnaReporte`, `ContextoExportacion`.
- Produces:
  - `enum TipoLinea { GrupoEncabezado, Detalle, Subtotal, Total }`; `record LineaExportacion(TipoLinea Tipo, int Nivel, string Etiqueta, IReadOnlyList<object?> Celdas)`; `static class AplanadorResultado { static List<LineaExportacion> Aplanar(ResultadoReporte resultado) }` en `API.Domain.Interface.Reportes`. `Celdas` va alineada con `resultado.Columnas` (vacía en `GrupoEncabezado`).
  - `ExcelExportador : IExportadorReporte` (`Formato "xlsx"`, `ContentType "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"`, `Extension "xlsx"`); `internal static class FormatoValores` con `Texto(ColumnaReporte, object?)` (formato de pantalla: fechas `yyyy-MM-dd`, dinero `#,##0.00`, cantidades hasta 6 decimales, enteros sin decimales).
  - Reglas de las líneas aplanadas: sin agrupación, cada fila es un `Detalle` y, si hay totales, una línea `Total` final con etiqueta «Total general»; con agrupación, cada grupo con filas o hijos genera `GrupoEncabezado` («{Título}: {Etiqueta} ({Conteo})», `Nivel` = profundidad desde 0), sus líneas (detalle con `Nivel` + 1) y un `Subtotal` («Subtotal {Etiqueta}»); un grupo hoja sin filas (SoloResumen) genera una sola línea `Subtotal` con la etiqueta del encabezado; al final el `Total` general.

- [ ] **Step 1: Pruebas que fallan.**

`API.Service.WebApi.Tests/Domain/AplanadorResultadoTests.cs`:

```csharp
using API.Domain.Interface.Reportes;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class AplanadorResultadoTests
    {
        private static readonly ColumnaReporte CTexto = new("cliente", "Cliente", TipoColumna.Texto);
        private static readonly ColumnaReporte CImporte = new("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma);

        private static FilaReporte Fila(string cliente, decimal importe) => ReporteFalso.Fila(("cliente", cliente), ("importe", importe));

        [Fact]
        public void SinAgrupacion_DetalleYTotalGeneral()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CTexto, CImporte },
                Filas = { Fila("A", 10m), Fila("B", 5m) },
                Totales = { ["importe"] = 15m }
            };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Equal(new[] { TipoLinea.Detalle, TipoLinea.Detalle, TipoLinea.Total }, l.Select(x => x.Tipo).ToArray());
            Assert.Equal(new object?[] { "A", 10m }, l[0].Celdas.ToArray());
            Assert.Equal("Total general", l[2].Etiqueta);
            Assert.Equal(new object?[] { null, 15m }, l[2].Celdas.ToArray());
        }

        [Fact]
        public void SinTotales_NoHayLineaDeTotal()
        {
            var r = new ResultadoReporte { Columnas = { CTexto }, Filas = { ReporteFalso.Fila(("cliente", "A")) } };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Single(l);
            Assert.Equal(TipoLinea.Detalle, l[0].Tipo);
        }

        [Fact]
        public void ConAgrupacion_EncabezadoDetalleSubtotalYTotal()
        {
            var grupo = new GrupoReporte
            {
                Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2,
                Subtotales = { ["importe"] = 15m },
                Filas = { Fila("A", 10m), Fila("A", 5m) }
            };
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Grupos = { grupo }, Totales = { ["importe"] = 15m } };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Equal(new[] { TipoLinea.GrupoEncabezado, TipoLinea.Detalle, TipoLinea.Detalle, TipoLinea.Subtotal, TipoLinea.Total }, l.Select(x => x.Tipo).ToArray());
            Assert.Equal("Cliente: A (2)", l[0].Etiqueta);
            Assert.Equal(0, l[0].Nivel);
            Assert.Equal(1, l[1].Nivel);
            Assert.Equal("Subtotal A", l[3].Etiqueta);
            Assert.Equal(15m, l[3].Celdas[1]);
        }

        [Fact]
        public void DosNiveles_AnidaLosNivelesYLosSubtotales()
        {
            var hoja = new GrupoReporte { Campo = "fecha", Titulo = "Fecha", Etiqueta = "2026-01", Clave = "2026-01", Conteo = 1, Subtotales = { ["importe"] = 10m }, Filas = { Fila("A", 10m) } };
            var raiz = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 1, Subtotales = { ["importe"] = 10m }, Hijos = { hoja } };
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Grupos = { raiz } };

            var l = AplanadorResultado.Aplanar(r);

            Assert.Equal(new[] { TipoLinea.GrupoEncabezado, TipoLinea.GrupoEncabezado, TipoLinea.Detalle, TipoLinea.Subtotal, TipoLinea.Subtotal }, l.Select(x => x.Tipo).ToArray());
            Assert.Equal(new[] { 0, 1, 2, 1, 0 }, l.Select(x => x.Nivel).ToArray());
        }

        [Fact]
        public void SoloResumen_UnaSolaLineaDeSubtotalPorGrupoHoja()
        {
            var hoja = new GrupoReporte { Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2, Subtotales = { ["importe"] = 15m } };
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Grupos = { hoja } };

            var l = AplanadorResultado.Aplanar(r);

            var unica = Assert.Single(l);
            Assert.Equal(TipoLinea.Subtotal, unica.Tipo);
            Assert.Equal("Cliente: A (2)", unica.Etiqueta);
            Assert.Equal(15m, unica.Celdas[1]);
        }
    }
}
```

`API.Service.WebApi.Tests/Infraestructura/ExcelExportadorTests.cs`:

```csharp
using API.Domain.Interface.Reportes;
using API.Infraestructure.Exportacion;
using API.Service.WebApi.Tests.TestHelpers;
using ClosedXML.Excel;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructura
{
    public class ExcelExportadorTests
    {
        private static readonly ReporteDefinicion Def = new("t", "Ventas por documento", "d", "Ventas", "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());
        private static readonly ContextoExportacion Ctx = new("Mi Empresa", "ana", new DateTime(2026, 11, 8, 10, 30, 0), "Fechas: 2026-01-01 a 2026-01-31", null);

        private static readonly ColumnaReporte CFecha = new("fecha", "Fecha", TipoColumna.Fecha);
        private static readonly ColumnaReporte CTexto = new("cliente", "Cliente", TipoColumna.Texto);
        private static readonly ColumnaReporte CCant = new("cantidad", "Cantidad", TipoColumna.Cantidad, Totaliza: Totalizacion.Suma);
        private static readonly ColumnaReporte CImporte = new("importe", "Importe", TipoColumna.Dinero, Totaliza: Totalizacion.Suma);

        private static FilaReporte Fila(DateTime fecha, string cliente, decimal cant, decimal importe) =>
            ReporteFalso.Fila(("fecha", fecha), ("cliente", cliente), ("cantidad", cant), ("importe", importe));

        private static XLWorkbook Abrir(ResultadoReporte r) =>
            new(new MemoryStream(new ExcelExportador().Exportar(r, Def, Ctx)));

        [Fact]
        public void LosMetadatosSonLosDelXlsx()
        {
            var e = new ExcelExportador();

            Assert.Equal("xlsx", e.Formato);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", e.ContentType);
            Assert.Equal("xlsx", e.Extension);
        }

        [Fact]
        public void SinAgrupacion_EncabezadoDeEmpresaFiltrosTitulosYFormatos()
        {
            var r = new ResultadoReporte
            {
                Columnas = { CFecha, CTexto, CCant, CImporte },
                Filas = { Fila(new DateTime(2026, 1, 5), "C1", 2.5m, 1234.5m), Fila(new DateTime(2026, 1, 6), "C2", 3m, 10m) },
                Totales = { ["cantidad"] = 5.5m, ["importe"] = 1244.5m }
            };

            using var libro = Abrir(r);
            var h = libro.Worksheet(1);

            Assert.Equal("Mi Empresa", h.Cell(1, 1).GetString());
            Assert.True(h.Cell(1, 1).Style.Font.Bold);
            Assert.Equal("Ventas por documento", h.Cell(2, 1).GetString());
            Assert.Equal("Fechas: 2026-01-01 a 2026-01-31", h.Cell(3, 1).GetString());
            Assert.Equal("Generado por ana el 2026-11-08 10:30", h.Cell(4, 1).GetString());

            // Con totales hay una primera columna de etiquetas ("Total general"); los datos empiezan en la columna 2.
            Assert.Equal("Fecha", h.Cell(6, 2).GetString());
            Assert.Equal("Importe", h.Cell(6, 5).GetString());
            Assert.True(h.Cell(6, 2).Style.Font.Bold);

            Assert.Equal(new DateTime(2026, 1, 5), h.Cell(7, 2).GetDateTime());
            Assert.Equal("yyyy-mm-dd", h.Cell(7, 2).Style.NumberFormat.Format);
            Assert.Equal("C1", h.Cell(7, 3).GetString());
            Assert.Equal(1234.5, h.Cell(7, 5).GetDouble());
            Assert.Equal("#,##0.00", h.Cell(7, 5).Style.NumberFormat.Format);
            Assert.Equal("#,##0.######", h.Cell(7, 4).Style.NumberFormat.Format);

            Assert.Equal("Total general", h.Cell(9, 1).GetString());
            Assert.True(h.Cell(9, 1).Style.Font.Bold);
            Assert.Equal(1244.5, h.Cell(9, 5).GetDouble());
        }

        [Fact]
        public void SinAgrupacion_TieneAutofiltroYEncabezadoFijo()
        {
            var r = new ResultadoReporte { Columnas = { CTexto, CImporte }, Filas = { ReporteFalso.Fila(("cliente", "A"), ("importe", 1m)) } };

            using var libro = Abrir(r);
            var h = libro.Worksheet(1);

            Assert.True(h.AutoFilter.IsEnabled);
            Assert.Equal(6, h.SheetView.SplitRow);
        }

        [Fact]
        public void ConAgrupacion_EncabezadoDeGrupoSubtotalYTotalEnNegrita()
        {
            var grupo = new GrupoReporte
            {
                Campo = "cliente", Titulo = "Cliente", Etiqueta = "A", Clave = "A", Conteo = 2,
                Subtotales = { ["cantidad"] = 3m, ["importe"] = 30m },
                Filas = { Fila(new DateTime(2026, 1, 5), "A", 1m, 10m), Fila(new DateTime(2026, 1, 6), "A", 2m, 20m) }
            };
            var r = new ResultadoReporte
            {
                Columnas = { CFecha, CCant, CImporte },
                Grupos = { grupo },
                Totales = { ["cantidad"] = 3m, ["importe"] = 30m }
            };

            using var libro = Abrir(r);
            var h = libro.Worksheet(1);

            Assert.False(h.AutoFilter.IsEnabled);
            Assert.Equal("Cliente: A (2)", h.Cell(7, 1).GetString());
            Assert.True(h.Cell(7, 1).Style.Font.Bold);
            Assert.Equal(new DateTime(2026, 1, 5), h.Cell(8, 2).GetDateTime());
            Assert.Equal("Subtotal A", h.Cell(10, 1).GetString());
            Assert.True(h.Cell(10, 1).Style.Font.Bold);
            Assert.Equal(30.0, h.Cell(10, 4).GetDouble());
            Assert.Equal("Total general", h.Cell(11, 1).GetString());
            Assert.Equal(30.0, h.Cell(11, 4).GetDouble());
        }

        [Fact]
        public void ElNombreDeLaHojaSeSaneaYSeRecortaA31()
        {
            var def = Def with { Nombre = "Ventas: [por] documento / año con un nombre muy largo para Excel" };
            var bytes = new ExcelExportador().Exportar(new ResultadoReporte { Columnas = { CTexto } }, def, Ctx);

            using var libro = new XLWorkbook(new MemoryStream(bytes));
            var nombre = libro.Worksheet(1).Name;

            Assert.True(nombre.Length <= 31);
            Assert.DoesNotContain(':', nombre);
            Assert.DoesNotContain('[', nombre);
            Assert.DoesNotContain('/', nombre);
        }

        [Fact]
        public void ElResultadoVacioGeneraUnLibroValido()
        {
            using var libro = Abrir(new ResultadoReporte { Columnas = { CTexto, CImporte } });

            Assert.Equal("Cliente", libro.Worksheet(1).Cell(6, 1).GetString());
        }
    }
}
```

- [ ] **Step 2: Ver que fallan** (no compilan): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~AplanadorResultadoTests|FullyQualifiedName~ExcelExportadorTests"`.

- [ ] **Step 3: Implementar.**

`API.Domain.Interface/Reportes/AplanadorResultado.cs`:

```csharp
namespace API.Domain.Interface.Reportes
{
    public enum TipoLinea { GrupoEncabezado, Detalle, Subtotal, Total }

    /// <summary>Una línea de la tabla de salida. Celdas está alineada con ResultadoReporte.Columnas (vacía en los encabezados de grupo).</summary>
    public sealed record LineaExportacion(TipoLinea Tipo, int Nivel, string Etiqueta, IReadOnlyList<object?> Celdas);

    /// <summary>Convierte un resultado (plano o agrupado) en una lista de líneas lista para pintar en Excel o PDF.</summary>
    public static class AplanadorResultado
    {
        public static List<LineaExportacion> Aplanar(ResultadoReporte resultado)
        {
            var lineas = new List<LineaExportacion>();

            if (resultado.Grupos.Count == 0)
            {
                foreach (var fila in resultado.Filas)
                    lineas.Add(Detalle(resultado.Columnas, fila, 0));
            }
            else
            {
                Recorrer(resultado.Columnas, resultado.Grupos, 0, lineas);
            }

            if (resultado.Totales.Count > 0)
                lineas.Add(new LineaExportacion(TipoLinea.Total, 0, "Total general", Valores(resultado.Columnas, resultado.Totales)));

            return lineas;
        }

        private static void Recorrer(IReadOnlyList<ColumnaReporte> columnas, List<GrupoReporte> grupos, int nivel, List<LineaExportacion> lineas)
        {
            foreach (var g in grupos)
            {
                var encabezado = $"{g.Titulo}: {g.Etiqueta} ({g.Conteo})";

                if (g.Hijos.Count == 0 && g.Filas.Count == 0)
                {
                    // SoloResumen: una sola línea por grupo hoja.
                    lineas.Add(new LineaExportacion(TipoLinea.Subtotal, nivel, encabezado, Valores(columnas, g.Subtotales)));
                    continue;
                }

                lineas.Add(new LineaExportacion(TipoLinea.GrupoEncabezado, nivel, encabezado, Array.Empty<object?>()));
                if (g.Hijos.Count > 0)
                    Recorrer(columnas, g.Hijos, nivel + 1, lineas);
                else
                    foreach (var fila in g.Filas)
                        lineas.Add(Detalle(columnas, fila, nivel + 1));
                lineas.Add(new LineaExportacion(TipoLinea.Subtotal, nivel, $"Subtotal {g.Etiqueta}", Valores(columnas, g.Subtotales)));
            }
        }

        private static LineaExportacion Detalle(IReadOnlyList<ColumnaReporte> columnas, FilaReporte fila, int nivel) =>
            new(TipoLinea.Detalle, nivel, "", columnas.Select(c => fila.TryGetValue(c.Campo, out var v) ? v : null).ToList());

        private static IReadOnlyList<object?> Valores(IReadOnlyList<ColumnaReporte> columnas, Dictionary<string, decimal?> valores) =>
            columnas.Select(c => valores.TryGetValue(c.Campo, out var v) ? (object?)v : null).ToList();
    }
}
```

`API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj`: dentro de un `<ItemGroup>` agrega `<PackageReference Include="ClosedXML" Version="0.102.3" />` y restaura (`dotnet restore API.sln`). Si la versión no restaura, usa la 0.102.x más cercana disponible y anótalo en el ledger.

`API.Infraestructure.Exportacion/FormatoValores.cs`:

```csharp
using System.Globalization;
using API.Domain.Interface.Reportes;

namespace API.Infraestructure.Exportacion
{
    /// <summary>Formato de pantalla de los valores (PDF): fechas yyyy-MM-dd, dinero #,##0.00, cantidades hasta 6 decimales, enteros sin decimales.</summary>
    internal static class FormatoValores
    {
        public static string Texto(ColumnaReporte columna, object? valor)
        {
            if (valor is null) return "";
            if (valor is DateTime fecha) return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (ComparadorNumerico.EsNumero(valor))
            {
                var n = Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
                return columna.Tipo switch
                {
                    TipoColumna.Dinero => n.ToString("#,##0.00", CultureInfo.InvariantCulture),
                    TipoColumna.Entero => n.ToString("0", CultureInfo.InvariantCulture),
                    _ => n.ToString("#,##0.######", CultureInfo.InvariantCulture)
                };
            }
            return valor.ToString() ?? "";
        }
    }
}
```

`API.Infraestructure.Exportacion/ExcelExportador.cs`:

```csharp
using System.Globalization;
using API.Domain.Interface.Reportes;
using ClosedXML.Excel;

namespace API.Infraestructure.Exportacion
{
    /// <summary>
    /// Excel con encabezado (empresa, reporte, filtros, usuario y fecha), formato por tipo de columna, grupos con subtotales en
    /// negrita, fila de total, encabezado fijo y autofiltro cuando no hay agrupación.
    /// </summary>
    public sealed class ExcelExportador : IExportadorReporte
    {
        private const int FilaEncabezado = 6;

        public string Formato => "xlsx";
        public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public string Extension => "xlsx";

        public byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto)
        {
            using var libro = new XLWorkbook();
            var hoja = libro.Worksheets.Add(NombreHoja(definicion.Nombre));

            var lineas = AplanadorResultado.Aplanar(resultado);
            // Una primera columna de etiquetas solo cuando hace falta (grupos, subtotales o total general).
            var conEtiqueta = resultado.Grupos.Count > 0 || resultado.Totales.Count > 0;
            var desplazamiento = conEtiqueta ? 1 : 0;
            var totalColumnas = Math.Max(1, resultado.Columnas.Count + desplazamiento);

            hoja.Cell(1, 1).Value = contexto.Empresa;
            hoja.Cell(1, 1).Style.Font.Bold = true;
            hoja.Cell(1, 1).Style.Font.FontSize = 14;
            hoja.Cell(2, 1).Value = definicion.Nombre;
            hoja.Cell(2, 1).Style.Font.Bold = true;
            hoja.Cell(3, 1).Value = contexto.FiltrosTexto;
            hoja.Cell(4, 1).Value = $"Generado por {contexto.Usuario} el {contexto.Fecha:yyyy-MM-dd HH:mm}";

            for (var i = 0; i < resultado.Columnas.Count; i++)
            {
                var c = hoja.Cell(FilaEncabezado, i + 1 + desplazamiento);
                c.Value = resultado.Columnas[i].Titulo;
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.LightGray;
                c.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                if (EsNumerica(resultado.Columnas[i])) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }
            if (conEtiqueta)
            {
                var c = hoja.Cell(FilaEncabezado, 1);
                c.Style.Fill.BackgroundColor = XLColor.LightGray;
                c.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            var fila = FilaEncabezado + 1;
            var ultimaDetalle = FilaEncabezado;
            foreach (var l in lineas)
            {
                switch (l.Tipo)
                {
                    case TipoLinea.GrupoEncabezado:
                        var g = hoja.Cell(fila, 1);
                        g.Value = l.Etiqueta;
                        g.Style.Font.Bold = true;
                        g.Style.Alignment.Indent = l.Nivel;
                        break;

                    case TipoLinea.Detalle:
                        EscribirCeldas(hoja, fila, desplazamiento, resultado.Columnas, l.Celdas);
                        ultimaDetalle = fila;
                        break;

                    case TipoLinea.Subtotal:
                    case TipoLinea.Total:
                        var etiqueta = hoja.Cell(fila, 1);
                        etiqueta.Value = l.Etiqueta;
                        etiqueta.Style.Alignment.Indent = l.Nivel;
                        EscribirCeldas(hoja, fila, desplazamiento, resultado.Columnas, l.Celdas);
                        var rango = hoja.Range(fila, 1, fila, totalColumnas);
                        rango.Style.Font.Bold = true;
                        rango.Style.Fill.BackgroundColor = l.Tipo == TipoLinea.Total ? XLColor.LightGray : XLColor.WhiteSmoke;
                        if (l.Tipo == TipoLinea.Total) rango.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                        break;
                }
                fila++;
            }

            hoja.SheetView.FreezeRows(FilaEncabezado);
            if (resultado.Grupos.Count == 0 && ultimaDetalle > FilaEncabezado)
                hoja.Range(FilaEncabezado, 1, ultimaDetalle, totalColumnas).SetAutoFilter();

            hoja.Columns().AdjustToContents(FilaEncabezado, Math.Max(fila, FilaEncabezado + 1));

            using var salida = new MemoryStream();
            libro.SaveAs(salida);
            return salida.ToArray();
        }

        private static void EscribirCeldas(IXLWorksheet hoja, int fila, int desplazamiento, IReadOnlyList<ColumnaReporte> columnas, IReadOnlyList<object?> celdas)
        {
            for (var i = 0; i < columnas.Count && i < celdas.Count; i++)
            {
                var celda = hoja.Cell(fila, i + 1 + desplazamiento);
                celda.Value = ACelda(celdas[i]);
                celda.Style.NumberFormat.Format = columnas[i].Tipo switch
                {
                    TipoColumna.Fecha => "yyyy-mm-dd",
                    TipoColumna.Dinero => "#,##0.00",
                    TipoColumna.Cantidad => "#,##0.######",
                    TipoColumna.Entero => "0",
                    _ => "General"
                };
            }
        }

        private static XLCellValue ACelda(object? valor)
        {
            if (valor is null) return Blank.Value;
            if (valor is DateTime d) return d;
            if (valor is string s) return s;
            if (valor is decimal m) return m;
            if (valor is int i) return i;
            if (valor is long l) return l;
            if (valor is double db) return db;
            return Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
        }

        private static bool EsNumerica(ColumnaReporte c) => c.Tipo is TipoColumna.Dinero or TipoColumna.Cantidad or TipoColumna.Entero;

        private static string NombreHoja(string nombre)
        {
            var limpio = new string(nombre.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()).Trim();
            if (limpio.Length == 0) limpio = "Reporte";
            return limpio.Length > 31 ? limpio[..31] : limpio;
        }
    }
}
```

`API.Service.WebApi/Startup.cs`: tras `services.AddTransient<IExportadorReporte, CsvExportador>();` agrega `services.AddTransient<IExportadorReporte, ExcelExportador>();`.

- [ ] **Step 4: Ver que pasan.** El `--filter` del Step 2 en verde. Si alguna aserción de formato de ClosedXML difiere (por ejemplo el valor exacto de `SheetView.SplitRow` o la lectura de `GetDateTime`), ajusta **la prueba** al comportamiento real de la librería sin debilitar lo que verifica (encabezado fijo en la fila 6, autofiltro solo sin agrupación, negrita en subtotales y total, formato por tipo). Suite completa en verde; `dotnet build API.sln` sin errores.

- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Interface/Reportes/AplanadorResultado.cs API.Infraestructure.Exportacion API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/AplanadorResultadoTests.cs API.Service.WebApi.Tests/Infraestructura/ExcelExportadorTests.cs
git commit -m "feat(reporteria): exportacion a Excel con ClosedXML y aplanador del resultado

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Exportador a PDF (QuestPDF)

**Files:**
- Modify: `API.Infraestructure.Exportacion/API.Infraestructure.Exportacion.csproj` (paquete QuestPDF)
- Create: `API.Infraestructure.Exportacion/PdfExportador.cs`
- Modify: `API.Service.WebApi/Startup.cs` (DI)
- Test: `API.Service.WebApi.Tests/Infraestructura/PdfExportadorTests.cs`

**Interfaces:**
- Consumes: `AplanadorResultado`, `LineaExportacion`, `TipoLinea`, `FormatoValores`, `ContextoExportacion` (con `GraficoPng`).
- Produces: `PdfExportador : IExportadorReporte` (`Formato "pdf"`, `ContentType "application/pdf"`, `Extension "pdf"`). Orientación horizontal si el resultado tiene **más de 7 columnas**; encabezado de página con empresa, reporte, filtros, usuario y fecha; pie con «Página X de Y»; el gráfico (si `GraficoPng` trae bytes) va encima de la tabla; los avisos del resultado y el aviso de truncado salen antes de la tabla. Declara la licencia `QuestPDF.Settings.License = LicenseType.Community` en un constructor estático.

- [ ] **Step 1: Pruebas que fallan** `API.Service.WebApi.Tests/Infraestructura/PdfExportadorTests.cs`:

```csharp
using System.Text;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Exportacion;
using API.Service.WebApi.Tests.TestHelpers;
using Xunit;

namespace API.Service.WebApi.Tests.Infraestructura
{
    public class PdfExportadorTests
    {
        private static readonly ReporteDefinicion Def = new("t", "Ventas por documento", "d", "Ventas", "ReportesVentas", Array.Empty<FiltroReporte>(), Array.Empty<ColumnaReporte>());

        // PNG mínimo válido de 1x1.
        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        private static ContextoExportacion Ctx(byte[]? grafico = null) =>
            new("Mi Empresa", "ana", new DateTime(2026, 11, 8, 10, 30, 0), "Sin filtros", grafico);

        private static ColumnaReporte Col(int i) => new($"c{i}", $"Columna {i}", i % 2 == 0 ? TipoColumna.Dinero : TipoColumna.Texto, Totaliza: i % 2 == 0 ? Totalizacion.Suma : Totalizacion.Ninguna);

        private static ResultadoReporte Plano(int columnas, int filas)
        {
            var r = new ResultadoReporte();
            for (var i = 0; i < columnas; i++) r.Columnas.Add(Col(i));
            for (var f = 0; f < filas; f++)
            {
                var fila = new FilaReporte();
                for (var i = 0; i < columnas; i++) fila[$"c{i}"] = i % 2 == 0 ? (object)(f + 0.5m) : $"Texto {f}";
                r.Filas.Add(fila);
            }
            r.Totales["c0"] = filas;
            return r;
        }

        private static bool EsPdf(byte[] bytes) => Encoding.ASCII.GetString(bytes, 0, 4) == "%PDF";

        [Fact]
        public void LosMetadatosSonLosDelPdf()
        {
            var e = new PdfExportador();

            Assert.Equal("pdf", e.Formato);
            Assert.Equal("application/pdf", e.ContentType);
            Assert.Equal("pdf", e.Extension);
        }

        [Fact]
        public void GeneraUnPdfValido()
        {
            var bytes = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx());

            Assert.True(EsPdf(bytes));
            Assert.True(bytes.Length > 1000);
        }

        [Fact]
        public void ConGraficoGeneraUnPdfMasGrandeQueSinEl()
        {
            var sin = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx());
            var con = new PdfExportador().Exportar(Plano(4, 5), Def, Ctx(Png));

            Assert.True(EsPdf(con));
            Assert.True(con.Length > sin.Length);
        }

        [Fact]
        public void MasDeSieteColumnas_GeneraElPdfEnHorizontal()
        {
            var bytes = new PdfExportador().Exportar(Plano(9, 3), Def, Ctx());

            Assert.True(EsPdf(bytes));
        }

        [Fact]
        public void ConAgrupacionAvisosYTruncado_GeneraUnPdfValido()
        {
            var grupo = new GrupoReporte
            {
                Campo = "c1", Titulo = "Columna 1", Etiqueta = "Texto 0", Clave = "Texto 0", Conteo = 1,
                Subtotales = { ["c0"] = 0.5m },
                Filas = { ReporteFalso.Fila(("c0", 0.5m), ("c1", "Texto 0")) }
            };
            var r = new ResultadoReporte { Columnas = { Col(0), Col(1) }, Grupos = { grupo }, Totales = { ["c0"] = 0.5m }, Truncado = true };
            r.Avisos.Add("El resultado incluye más de una moneda: los importes no se suman.");

            var bytes = new PdfExportador().Exportar(r, Def, Ctx(Png));

            Assert.True(EsPdf(bytes));
        }

        [Fact]
        public void ConMuchasFilas_PaginaSinFallar()
        {
            var bytes = new PdfExportador().Exportar(Plano(5, 300), Def, Ctx());

            Assert.True(EsPdf(bytes));
        }

        [Fact]
        public void UnResultadoVacioGeneraUnPdfValido()
        {
            var bytes = new PdfExportador().Exportar(new ResultadoReporte { Columnas = { Col(0) } }, Def, Ctx());

            Assert.True(EsPdf(bytes));
        }
    }
}
```

- [ ] **Step 2: Ver que fallan** (no compila): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~PdfExportadorTests"`.

- [ ] **Step 3: Implementar.** En `API.Infraestructure.Exportacion.csproj` agrega `<PackageReference Include="QuestPDF" Version="2024.3.10" />` y restaura (`dotnet restore API.sln`; si esa versión no restaura, usa la 2024.x más cercana y anótalo en el ledger). `API.Infraestructure.Exportacion/PdfExportador.cs`:

```csharp
using API.Domain.Interface.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace API.Infraestructure.Exportacion
{
    /// <summary>
    /// PDF con encabezado (empresa, reporte, filtros, usuario y fecha), pie «Página X de Y», gráfico opcional sobre la tabla y la
    /// tabla del resultado (horizontal si hay más de 7 columnas). Licencia QuestPDF Community (gratuita para ingresos anuales menores a US$1 millón).
    /// </summary>
    public sealed class PdfExportador : IExportadorReporte
    {
        private const int MaxColumnasVertical = 7;

        static PdfExportador()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string Formato => "pdf";
        public string ContentType => "application/pdf";
        public string Extension => "pdf";

        public byte[] Exportar(ResultadoReporte resultado, ReporteDefinicion definicion, ContextoExportacion contexto)
        {
            var lineas = AplanadorResultado.Aplanar(resultado);
            var columnas = resultado.Columnas;
            var conEtiqueta = resultado.Grupos.Count > 0 || resultado.Totales.Count > 0;
            var totalColumnas = Math.Max(1, columnas.Count + (conEtiqueta ? 1 : 0));
            var horizontal = columnas.Count > MaxColumnasVertical;

            var documento = Document.Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(horizontal ? PageSizes.A4.Landscape() : PageSizes.A4);
                    pagina.Margin(24);
                    pagina.DefaultTextStyle(x => x.FontSize(8));

                    pagina.Header().Column(col =>
                    {
                        col.Item().Text(contexto.Empresa).FontSize(14).Bold();
                        col.Item().Text(definicion.Nombre).FontSize(11).SemiBold();
                        col.Item().Text(contexto.FiltrosTexto).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingBottom(6).Text($"Generado por {contexto.Usuario} el {contexto.Fecha:yyyy-MM-dd HH:mm}")
                            .FontColor(Colors.Grey.Darken2);
                    });

                    pagina.Content().Column(col =>
                    {
                        if (contexto.GraficoPng is { Length: > 0 })
                            col.Item().PaddingBottom(8).Height(220).Image(contexto.GraficoPng).FitArea();

                        foreach (var aviso in resultado.Avisos)
                            col.Item().PaddingBottom(2).Text(aviso).FontColor(Colors.Orange.Darken3);
                        if (resultado.Truncado)
                            col.Item().PaddingBottom(2).Text("El resultado se truncó al máximo de filas: refine los filtros para ver el resto.")
                                .FontColor(Colors.Orange.Darken3);

                        col.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(c =>
                            {
                                if (conEtiqueta) c.RelativeColumn(2);
                                foreach (var _ in columnas) c.RelativeColumn();
                            });

                            tabla.Header(h =>
                            {
                                if (conEtiqueta) h.Cell().Element(Cabecera).Text("");
                                foreach (var c in columnas)
                                {
                                    var celda = h.Cell().Element(Cabecera);
                                    (EsNumerica(c) ? celda.AlignRight() : celda).Text(c.Titulo);
                                }
                            });

                            foreach (var l in lineas)
                            {
                                if (l.Tipo == TipoLinea.GrupoEncabezado)
                                {
                                    tabla.Cell().ColumnSpan((uint)totalColumnas).Element(EstiloGrupo)
                                        .PaddingLeft(l.Nivel * 8).Text(l.Etiqueta).Bold();
                                    continue;
                                }

                                var resaltada = l.Tipo != TipoLinea.Detalle;
                                if (conEtiqueta)
                                {
                                    var etiqueta = tabla.Cell().Element(c => Celda(c, resaltada)).PaddingLeft(l.Nivel * 8);
                                    var texto = etiqueta.Text(l.Etiqueta);
                                    if (resaltada) texto.Bold();
                                }

                                for (var i = 0; i < columnas.Count; i++)
                                {
                                    var valor = i < l.Celdas.Count ? l.Celdas[i] : null;
                                    var celda = tabla.Cell().Element(c => Celda(c, resaltada));
                                    var texto = (EsNumerica(columnas[i]) ? celda.AlignRight() : celda).Text(FormatoValores.Texto(columnas[i], valor));
                                    if (resaltada) texto.Bold();
                                }
                            }
                        });
                    });

                    pagina.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            });

            return documento.GeneratePdf();
        }

        private static bool EsNumerica(ColumnaReporte c) => c.Tipo is TipoColumna.Dinero or TipoColumna.Cantidad or TipoColumna.Entero;

        private static IContainer Cabecera(IContainer c) =>
            c.Background(Colors.Grey.Lighten3).BorderBottom(0.5f).PaddingVertical(3).PaddingHorizontal(3).DefaultTextStyle(x => x.SemiBold());

        private static IContainer EstiloGrupo(IContainer c) =>
            c.Background(Colors.Grey.Lighten4).PaddingVertical(2).PaddingHorizontal(3);

        private static IContainer Celda(IContainer c, bool resaltada) =>
            (resaltada ? c.Background(Colors.Grey.Lighten4) : c)
                .BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(3);
    }
}
```

Si algún nombre de la API fluida de QuestPDF 2024.3.10 difiere (por ejemplo `FitArea`, `ColumnSpan`, `DefaultTextStyle`), ajusta el código a la API real **manteniendo el comportamiento descrito**; el contrato lo fijan las pruebas.

`API.Service.WebApi/Startup.cs`: tras `services.AddTransient<IExportadorReporte, ExcelExportador>();` agrega `services.AddTransient<IExportadorReporte, PdfExportador>();`.

- [ ] **Step 4: Ver que pasan.** El `--filter` en verde (genera PDF reales: tarda unos segundos). Suite completa en verde; `dotnet build API.sln` sin errores. Inspección visual opcional: guarda el resultado de una de las pruebas en un PDF temporal y ábrelo para revisar el aspecto (no se commitea).

- [ ] **Step 5: Commit.**

```bash
git add API.Infraestructure.Exportacion API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Infraestructura/PdfExportadorTests.cs
git commit -m "feat(reporteria): exportacion a PDF con QuestPDF

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 3 — Vistas guardadas (API)

### Task 11: Vistas guardadas por usuario

**Files:**
- Create: `API.Domain.Core/Reportes/ReporteVistaDomain.cs`
- Create: `API.Application.DTO/reporte/ReporteVistaDTO.cs`, `API.Application.DTO/reporte/GuardarVistaDTO.cs`
- Modify: `API.Application.Interface/IReporteApplication.cs`, `API.Application.Main/ReporteApplication.cs`, `API.Service.WebApi/Controllers/ReporteController.cs`, `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Domain/ReporteVistaDomainTests.cs`; modificar `API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs`, `ReportePermisosTests.cs`

**Interfaces:**
- Consumes: `IReporteVistaDomain`, `ReporteVistaDato`, entidad `ReporteVista`, `IRepositorioGenerico<ReporteVista, int>`.
- Produces:
  - `ReporteVistaDomain : IReporteVistaDomain` con constantes `MaxVistasPorUsuario = 50`, `MaxLongitudNombre = 100`, `MaxTamanoConfiguracion = 20000`; constructor `(IRepositorioGenerico<ReporteVista, int> repo)`. Mensajes: «El nombre de la vista es obligatorio.», «El nombre de la vista no puede superar 100 caracteres.», «La configuración de la vista no es válida.», «La configuración de la vista es demasiado grande.», «Ya tienes 50 vistas guardadas. Borra alguna para crear otra.».
  - `ReporteVistaDTO { int Entry; string Nombre; JsonElement Configuracion; DateTime FechaActualizacion }`, `GuardarVistaDTO { string Nombre; JsonElement Configuracion }` (`API.Application.DTO.reporte`).
  - `IReporteApplication`: `Task<Respuesta<List<ReporteVistaDTO>>> ObtenerVistasAsync(int usuarioId, string codigo)`, `Task<Respuesta<ReporteVistaDTO>> GuardarVistaAsync(int usuarioId, string codigo, GuardarVistaDTO vista)`, `Task<Respuesta<bool>> EliminarVistaAsync(int usuarioId, string codigo, int entry)` (mensaje «La vista no existe.» si no existe o no es del usuario). `ReporteApplication(ICatalogoReportes, IMotorReportes, IEnumerable<IExportadorReporte>, IReporteVistaDomain)`.
  - Controller: `GET api/Reporte/{codigo}/vistas`, `POST api/Reporte/{codigo}/vistas`, `DELETE api/Reporte/{codigo}/vistas/{entry:int}` (acciones `Vistas`, `GuardarVista`, `EliminarVista`); el usuario sale del claim `NameIdentifier` (sin él: 401 con «No se pudo identificar al usuario.»); un mensaje que termina en «no existe.» produce 404, lo demás 400.

- [ ] **Step 1: Pruebas que fallan.**

`API.Service.WebApi.Tests/Domain/ReporteVistaDomainTests.cs`:

```csharp
using API.Domain.Core.Reportes;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class ReporteVistaDomainTests
    {
        private readonly Mock<IRepositorioGenerico<ReporteVista, int>> _repo = new();
        private readonly List<ReporteVista> _datos = new();
        private readonly ReporteVistaDomain _domain;

        public ReporteVistaDomainTests()
        {
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(() => _datos.AsAsyncQueryable());
            _repo.Setup(r => r.InsertarAsync(It.IsAny<ReporteVista>()))
                 .ReturnsAsync((ReporteVista v) => { v.Entry = _datos.Count + 100; _datos.Add(v); return v; });
            _repo.Setup(r => r.ActualizarAsync(It.IsAny<int>(), It.IsAny<ReporteVista>())).ReturnsAsync(true);
            _repo.Setup(r => r.EliminarAsync(It.IsAny<int>())).ReturnsAsync(true);
            _domain = new ReporteVistaDomain(_repo.Object);
        }

        private static ReporteVista V(int entry, int usuario, string reporte, string nombre, string config = "{}") =>
            new() { Entry = entry, UsuarioId = usuario, CodigoReporte = reporte, Nombre = nombre, Configuracion = config, FechaCreacion = DateTime.Today, FechaActualizacion = DateTime.Today };

        private static async Task<string> MensajeAsync(Task tarea) =>
            (await Assert.ThrowsAsync<ReporteInvalidoException>(() => tarea)).Message;

        [Fact]
        public async Task Obtener_SoloLasVistasDelUsuarioYDelReporteOrdenadasPorNombre()
        {
            _datos.AddRange(new[] { V(1, 5, "r1", "Zeta"), V(2, 5, "r1", "Alfa"), V(3, 6, "r1", "Ajena"), V(4, 5, "r2", "Otro reporte") });

            var vistas = await _domain.ObtenerAsync(5, "r1");

            Assert.Equal(new[] { "Alfa", "Zeta" }, vistas.Select(v => v.Nombre).ToArray());
        }

        [Fact]
        public async Task Guardar_Nueva_InsertaConElNombreRecortado()
        {
            var dato = await _domain.GuardarAsync(5, "r1", "  Mi vista  ", "{\"columnas\":[\"a\"]}");

            Assert.Equal("Mi vista", dato.Nombre);
            Assert.Equal("{\"columnas\":[\"a\"]}", dato.Configuracion);
            var guardada = Assert.Single(_datos);
            Assert.Equal(5, guardada.UsuarioId);
            Assert.Equal("r1", guardada.CodigoReporte);
            _repo.Verify(r => r.InsertarAsync(It.IsAny<ReporteVista>()), Times.Once);
        }

        [Fact]
        public async Task Guardar_ConUnNombreExistenteSinDistinguirMayusculas_LaReemplaza()
        {
            _datos.Add(V(1, 5, "r1", "Mi Vista", "{\"a\":1}"));

            var dato = await _domain.GuardarAsync(5, "r1", "mi vista", "{\"a\":2}");

            Assert.Equal(1, dato.Entry);
            Assert.Equal("{\"a\":2}", _datos[0].Configuracion);
            _repo.Verify(r => r.ActualizarAsync(1, It.IsAny<ReporteVista>()), Times.Once);
            _repo.Verify(r => r.InsertarAsync(It.IsAny<ReporteVista>()), Times.Never);
        }

        [Fact]
        public async Task Guardar_OtroUsuarioOReporteConElMismoNombre_CreaUnaVistaNueva()
        {
            _datos.Add(V(1, 6, "r1", "Mi vista"));
            _datos.Add(V(2, 5, "r2", "Mi vista"));

            await _domain.GuardarAsync(5, "r1", "Mi vista", "{}");

            Assert.Equal(3, _datos.Count);
        }

        [Theory]
        [InlineData("", "El nombre de la vista es obligatorio.")]
        [InlineData("   ", "El nombre de la vista es obligatorio.")]
        public async Task Guardar_SinNombre_Lanza(string nombre, string mensaje)
        {
            Assert.Equal(mensaje, await MensajeAsync(_domain.GuardarAsync(5, "r1", nombre, "{}")));
            _repo.Verify(r => r.InsertarAsync(It.IsAny<ReporteVista>()), Times.Never);
        }

        [Fact]
        public async Task Guardar_NombreMuyLargo_Lanza()
        {
            Assert.Equal("El nombre de la vista no puede superar 100 caracteres.",
                await MensajeAsync(_domain.GuardarAsync(5, "r1", new string('x', 101), "{}")));
        }

        [Theory]
        [InlineData("")]
        [InlineData("no es json")]
        [InlineData("[1,2]")]
        [InlineData("\"texto\"")]
        public async Task Guardar_ConfiguracionQueNoEsUnObjetoJson_Lanza(string config)
        {
            Assert.Equal("La configuración de la vista no es válida.", await MensajeAsync(_domain.GuardarAsync(5, "r1", "V", config)));
        }

        [Fact]
        public async Task Guardar_ConfiguracionMuyGrande_Lanza()
        {
            var grande = "{\"x\":\"" + new string('a', ReporteVistaDomain.MaxTamanoConfiguracion) + "\"}";

            Assert.Equal("La configuración de la vista es demasiado grande.", await MensajeAsync(_domain.GuardarAsync(5, "r1", "V", grande)));
        }

        [Fact]
        public async Task Guardar_ConCincuentaVistasDelUsuario_NoDejaCrearOtraPeroSiReemplazar()
        {
            for (var i = 0; i < ReporteVistaDomain.MaxVistasPorUsuario; i++) _datos.Add(V(i + 1, 5, i % 2 == 0 ? "r1" : "r2", "Vista " + i));

            Assert.Equal("Ya tienes 50 vistas guardadas. Borra alguna para crear otra.",
                await MensajeAsync(_domain.GuardarAsync(5, "r1", "Nueva", "{}")));

            var reemplazo = await _domain.GuardarAsync(5, "r1", "Vista 0", "{\"x\":1}");
            Assert.Equal(1, reemplazo.Entry);
        }

        [Fact]
        public async Task Guardar_ElLimiteEsPorUsuario_NoAfectaAOtros()
        {
            for (var i = 0; i < ReporteVistaDomain.MaxVistasPorUsuario; i++) _datos.Add(V(i + 1, 6, "r1", "Vista " + i));

            var dato = await _domain.GuardarAsync(5, "r1", "Nueva", "{}");

            Assert.Equal("Nueva", dato.Nombre);
        }

        [Fact]
        public async Task Eliminar_VistaPropia_LaBorra()
        {
            _datos.Add(V(1, 5, "r1", "Mi vista"));

            Assert.True(await _domain.EliminarAsync(5, "r1", 1));
            _repo.Verify(r => r.EliminarAsync(1), Times.Once);
        }

        [Fact]
        public async Task Eliminar_VistaAjenaOInexistenteOdeOtroReporte_DevuelveFalseYNoBorra()
        {
            _datos.Add(V(1, 6, "r1", "Ajena"));
            _datos.Add(V(2, 5, "r2", "Otro reporte"));

            Assert.False(await _domain.EliminarAsync(5, "r1", 1));
            Assert.False(await _domain.EliminarAsync(5, "r1", 2));
            Assert.False(await _domain.EliminarAsync(5, "r1", 99));
            _repo.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }
    }
}
```

`ReporteApplicationTests.cs` — (a) agrega el campo `private readonly Mock<IReporteVistaDomain> _vistas = new();` y crea `_app` con `new ReporteApplication(_catalogo.Object, _motor.Object, new[] { _csv.Object }, _vistas.Object)`; (b) agrega `using System.Text.Json;` y `using API.Application.DTO.reporte;`; (c) agrega al final de la clase:

```csharp
        // ---------- vistas guardadas ----------

        private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement.Clone();

        [Fact]
        public async Task ObtenerVistas_DevuelveLaConfiguracionComoObjetoJson()
        {
            _vistas.Setup(v => v.ObtenerAsync(5, "ventas")).ReturnsAsync(new[]
            {
                new ReporteVistaDato(1, "Mi vista", "{\"columnas\":[\"a\",\"b\"]}", new DateTime(2026, 11, 8))
            });

            var r = await _app.ObtenerVistasAsync(5, "ventas");

            Assert.True(r.Resultado);
            var vista = Assert.Single(r.Dato);
            Assert.Equal("Mi vista", vista.Nombre);
            Assert.Equal(2, vista.Configuracion.GetProperty("columnas").GetArrayLength());
        }

        [Fact]
        public async Task ObtenerVistas_ReporteInexistente_DiceQueNoExiste()
        {
            var r = await _app.ObtenerVistasAsync(5, "nada");

            Assert.False(r.Resultado);
            Assert.Equal("El reporte 'nada' no existe.", r.Mensaje);
        }

        [Fact]
        public async Task GuardarVista_PasaElJsonCrudoAlDominio()
        {
            _vistas.Setup(v => v.GuardarAsync(5, "ventas", "Mi vista", "{\"a\":1}"))
                   .ReturnsAsync(new ReporteVistaDato(7, "Mi vista", "{\"a\":1}", new DateTime(2026, 11, 8)));

            var r = await _app.GuardarVistaAsync(5, "ventas", new GuardarVistaDTO { Nombre = "Mi vista", Configuracion = Json("{\"a\":1}") });

            Assert.True(r.Resultado);
            Assert.Equal(7, r.Dato.Entry);
        }

        [Fact]
        public async Task GuardarVista_SinConfiguracion_Rechaza()
        {
            var r = await _app.GuardarVistaAsync(5, "ventas", new GuardarVistaDTO { Nombre = "V" });

            Assert.False(r.Resultado);
            Assert.Equal("La configuración de la vista no es válida.", r.Mensaje);
        }

        [Fact]
        public async Task GuardarVista_ErrorDelDominio_DevuelveElMensaje()
        {
            _vistas.Setup(v => v.GuardarAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                   .ThrowsAsync(new ReporteInvalidoException("El nombre de la vista es obligatorio."));

            var r = await _app.GuardarVistaAsync(5, "ventas", new GuardarVistaDTO { Nombre = "", Configuracion = Json("{}") });

            Assert.False(r.Resultado);
            Assert.Equal("El nombre de la vista es obligatorio.", r.Mensaje);
        }

        [Fact]
        public async Task EliminarVista_Existente_DevuelveTrue()
        {
            _vistas.Setup(v => v.EliminarAsync(5, "ventas", 7)).ReturnsAsync(true);

            var r = await _app.EliminarVistaAsync(5, "ventas", 7);

            Assert.True(r.Resultado);
            Assert.True(r.Dato);
        }

        [Fact]
        public async Task EliminarVista_Inexistente_DiceQueNoExiste()
        {
            _vistas.Setup(v => v.EliminarAsync(5, "ventas", 7)).ReturnsAsync(false);

            var r = await _app.EliminarVistaAsync(5, "ventas", 7);

            Assert.False(r.Resultado);
            Assert.Equal("La vista no existe.", r.Mensaje);
        }
```

`ReporteControllerTests.cs` — agrega `using System.Text.Json;` si no está y al final de la clase:

```csharp
        // ---------- vistas guardadas ----------

        private static ClaimsPrincipal SinId() =>
            new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tester"), new Claim("SuperUsuario", "S") }, "Test"));

        [Fact]
        public async Task Vistas_ConPermiso_ListaLasDelUsuarioDelToken()
        {
            var buena = new Respuesta<List<ReporteVistaDTO>> { Resultado = true, Dato = new() };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.ObtenerVistasAsync(5, "ventas")).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V", id: "5")).Vistas("ventas");

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task Vistas_SinPermiso_Devuelve403()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(Usuario("ReportesCompras:V")).Vistas("ventas");

            Es403(r.Result!);
        }

        [Fact]
        public async Task Vistas_SinIdentificadorDeUsuario_Devuelve401()
        {
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());

            var r = await Crear(SinId()).Vistas("ventas");

            var no = Assert.IsType<UnauthorizedObjectResult>(r.Result);
            Assert.Equal("No se pudo identificar al usuario.", ((Respuesta<List<ReporteVistaDTO>>)no.Value!).Mensaje);
        }

        [Fact]
        public async Task GuardarVista_Ok_DevuelveOk()
        {
            var vista = new GuardarVistaDTO { Nombre = "V", Configuracion = JsonDocument.Parse("{}").RootElement.Clone() };
            var buena = new Respuesta<ReporteVistaDTO> { Resultado = true, Dato = new ReporteVistaDTO { Entry = 3 } };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.GuardarVistaAsync(5, "ventas", vista)).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V", id: "5")).GuardarVista("ventas", vista);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task GuardarVista_ErrorDeNegocio_DevuelveBadRequest()
        {
            var mala = new Respuesta<ReporteVistaDTO> { Resultado = false, Mensaje = "El nombre de la vista es obligatorio." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.GuardarVistaAsync(It.IsAny<int>(), "ventas", It.IsAny<GuardarVistaDTO>())).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).GuardarVista("ventas", new GuardarVistaDTO());

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task GuardarVista_CuerpoNulo_DevuelveBadRequest()
        {
            var r = await Crear().GuardarVista("ventas", null!);

            Assert.IsType<BadRequestObjectResult>(r.Result);
        }

        [Fact]
        public async Task EliminarVista_Ok_DevuelveOk()
        {
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EliminarVistaAsync(5, "ventas", 9)).ReturnsAsync(buena);

            var r = await Crear(Usuario("ReportesVentas:V", id: "5")).EliminarVista("ventas", 9);

            Assert.Same(buena, Assert.IsType<OkObjectResult>(r.Result).Value);
        }

        [Fact]
        public async Task EliminarVista_Inexistente_DevuelveNotFound()
        {
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "La vista no existe." };
            _app.Setup(a => a.ObtenerDefinicion("ventas")).Returns(DefinicionOk());
            _app.Setup(a => a.EliminarVistaAsync(It.IsAny<int>(), "ventas", 9)).ReturnsAsync(mala);

            var r = await Crear(Usuario(superusuario: true)).EliminarVista("ventas", 9);

            Assert.Same(mala, Assert.IsType<NotFoundObjectResult>(r.Result).Value);
        }
```

`ReportePermisosTests.cs` — agrega a la `Theory` de rutas: `[InlineData("Vistas", "GET", "{codigo}/vistas")]`, `[InlineData("GuardarVista", "POST", "{codigo}/vistas")]`, `[InlineData("EliminarVista", "DELETE", "{codigo}/vistas/{entry:int}")]`.

- [ ] **Step 2: Ver que fallan** (no compilan): `dotnet test API.Service.WebApi.Tests --nologo -v q --filter "FullyQualifiedName~ReporteVistaDomainTests|FullyQualifiedName~ReporteApplicationTests|FullyQualifiedName~ReporteControllerTests|FullyQualifiedName~ReportePermisosTests"`.

- [ ] **Step 3: Implementar.**

`API.Domain.Core/Reportes/ReporteVistaDomain.cs`:

```csharp
using System.Text.Json;
using API.Domain.Entity.Models;
using API.Domain.Interface.Reportes;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core.Reportes
{
    /// <summary>Vistas guardadas de reportes: configuración JSON por usuario y reporte, con nombre único (sin distinguir mayúsculas).</summary>
    public sealed class ReporteVistaDomain : IReporteVistaDomain
    {
        public const int MaxVistasPorUsuario = 50;
        public const int MaxLongitudNombre = 100;
        public const int MaxTamanoConfiguracion = 20000;

        private readonly IRepositorioGenerico<ReporteVista, int> _repo;

        public ReporteVistaDomain(IRepositorioGenerico<ReporteVista, int> repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyList<ReporteVistaDato>> ObtenerAsync(int usuarioId, string codigoReporte)
        {
            var q = await _repo.ObtenerTodoAsync();
            var vistas = await q.Where(v => v.UsuarioId == usuarioId && v.CodigoReporte == codigoReporte)
                                .OrderBy(v => v.Nombre)
                                .ToListAsync();
            return vistas.Select(Dato).ToList();
        }

        public async Task<ReporteVistaDato> GuardarAsync(int usuarioId, string codigoReporte, string nombre, string configuracionJson)
        {
            nombre = (nombre ?? "").Trim();
            if (nombre.Length == 0)
                throw new ReporteInvalidoException("El nombre de la vista es obligatorio.");
            if (nombre.Length > MaxLongitudNombre)
                throw new ReporteInvalidoException($"El nombre de la vista no puede superar {MaxLongitudNombre} caracteres.");
            ValidarConfiguracion(configuracionJson);

            var q = await _repo.ObtenerTodoAsync();
            var minuscula = nombre.ToLower();
            var existente = await q.Where(v => v.UsuarioId == usuarioId && v.CodigoReporte == codigoReporte && v.Nombre.ToLower() == minuscula)
                                   .FirstOrDefaultAsync();
            var ahora = DateTime.Now;

            if (existente is not null)
            {
                existente.Nombre = nombre;
                existente.Configuracion = configuracionJson;
                existente.FechaActualizacion = ahora;
                await _repo.ActualizarAsync(existente.Entry, existente);
                return Dato(existente);
            }

            var total = await q.CountAsync(v => v.UsuarioId == usuarioId);
            if (total >= MaxVistasPorUsuario)
                throw new ReporteInvalidoException($"Ya tienes {MaxVistasPorUsuario} vistas guardadas. Borra alguna para crear otra.");

            var creada = await _repo.InsertarAsync(new ReporteVista
            {
                UsuarioId = usuarioId,
                CodigoReporte = codigoReporte,
                Nombre = nombre,
                Configuracion = configuracionJson,
                FechaCreacion = ahora,
                FechaActualizacion = ahora
            });
            return Dato(creada);
        }

        public async Task<bool> EliminarAsync(int usuarioId, string codigoReporte, int entry)
        {
            var q = await _repo.ObtenerTodoAsync();
            var vista = await q.Where(v => v.Entry == entry && v.UsuarioId == usuarioId && v.CodigoReporte == codigoReporte)
                               .FirstOrDefaultAsync();
            if (vista is null) return false;
            return await _repo.EliminarAsync(entry);
        }

        private static void ValidarConfiguracion(string? json)
        {
            if (json is not null && json.Length > MaxTamanoConfiguracion)
                throw new ReporteInvalidoException("La configuración de la vista es demasiado grande.");
            if (string.IsNullOrWhiteSpace(json))
                throw new ReporteInvalidoException("La configuración de la vista no es válida.");

            try
            {
                using var documento = JsonDocument.Parse(json);
                if (documento.RootElement.ValueKind != JsonValueKind.Object)
                    throw new ReporteInvalidoException("La configuración de la vista no es válida.");
            }
            catch (JsonException)
            {
                throw new ReporteInvalidoException("La configuración de la vista no es válida.");
            }
        }

        private static ReporteVistaDato Dato(ReporteVista v) => new(v.Entry, v.Nombre, v.Configuracion, v.FechaActualizacion);
    }
}
```

`API.Application.DTO/reporte/ReporteVistaDTO.cs`:

```csharp
using System.Text.Json;

namespace API.Application.DTO.reporte
{
    public class ReporteVistaDTO
    {
        public int Entry { get; set; }
        public string Nombre { get; set; } = "";
        /// <summary>Configuración guardada (filtros, columnas, orden, agrupación y gráfico) como objeto JSON.</summary>
        public JsonElement Configuracion { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }
}
```

`API.Application.DTO/reporte/GuardarVistaDTO.cs`:

```csharp
using System.Text.Json;

namespace API.Application.DTO.reporte
{
    public class GuardarVistaDTO
    {
        public string Nombre { get; set; } = "";
        public JsonElement Configuracion { get; set; }
    }
}
```

`API.Application.Interface/IReporteApplication.cs`: agrega

```csharp
        Task<Respuesta<List<ReporteVistaDTO>>> ObtenerVistasAsync(int usuarioId, string codigo);

        /// <summary>Crea la vista o reemplaza la del mismo nombre.</summary>
        Task<Respuesta<ReporteVistaDTO>> GuardarVistaAsync(int usuarioId, string codigo, GuardarVistaDTO vista);

        /// <summary>«La vista no existe.» si no existe o no es del usuario.</summary>
        Task<Respuesta<bool>> EliminarVistaAsync(int usuarioId, string codigo, int entry);
```

`API.Application.Main/ReporteApplication.cs`: agrega `using System.Text.Json;`, el campo `private readonly IReporteVistaDomain _vistas;`, amplía el constructor a `(ICatalogoReportes catalogo, IMotorReportes motor, IEnumerable<IExportadorReporte> exportadores, IReporteVistaDomain vistas)` (asignando `_vistas = vistas;`) y agrega:

```csharp
        public async Task<Respuesta<List<ReporteVistaDTO>>> ObtenerVistasAsync(int usuarioId, string codigo)
        {
            var respuesta = new Respuesta<List<ReporteVistaDTO>>();
            try
            {
                ExigirReporte(codigo);
                var vistas = await _vistas.ObtenerAsync(usuarioId, codigo);
                respuesta.Dato = vistas.Select(AVistaDto).ToList();
                respuesta.Resultado = true;
                respuesta.Mensaje = "Consulta realizada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<ReporteVistaDTO>> GuardarVistaAsync(int usuarioId, string codigo, GuardarVistaDTO vista)
        {
            var respuesta = new Respuesta<ReporteVistaDTO>();
            try
            {
                ExigirReporte(codigo);
                if (vista is null || vista.Configuracion.ValueKind != JsonValueKind.Object)
                    throw new ReporteInvalidoException("La configuración de la vista no es válida.");

                var guardada = await _vistas.GuardarAsync(usuarioId, codigo, vista.Nombre, vista.Configuracion.GetRawText());
                respuesta.Dato = AVistaDto(guardada);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Vista guardada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarVistaAsync(int usuarioId, string codigo, int entry)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                ExigirReporte(codigo);
                if (!await _vistas.EliminarAsync(usuarioId, codigo, entry))
                {
                    respuesta.Mensaje = "La vista no existe.";
                    return respuesta;
                }
                respuesta.Dato = true;
                respuesta.Resultado = true;
                respuesta.Mensaje = "Vista eliminada correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        private void ExigirReporte(string codigo)
        {
            if (_catalogo.Buscar(codigo) is null) throw new ReporteNoExisteException(codigo);
        }

        private static ReporteVistaDTO AVistaDto(ReporteVistaDato dato) => new()
        {
            Entry = dato.Entry,
            Nombre = dato.Nombre,
            Configuracion = JsonDocument.Parse(dato.Configuracion).RootElement.Clone(),
            FechaActualizacion = dato.FechaActualizacion
        };
```

`API.Service.WebApi/Controllers/ReporteController.cs`: agrega `using System.Security.Claims;` y las acciones:

```csharp
        [HttpGet("{codigo}/vistas")]
        public async Task<ActionResult<Respuesta<List<ReporteVistaDTO>>>> Vistas([FromRoute] string codigo)
        {
            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;
            if (!TryUsuarioId(out var usuarioId)) return SinUsuario<List<ReporteVistaDTO>>();

            var respuesta = await _app.ObtenerVistasAsync(usuarioId, codigo);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpPost("{codigo}/vistas")]
        public async Task<ActionResult<Respuesta<ReporteVistaDTO>>> GuardarVista([FromRoute] string codigo, [FromBody] GuardarVistaDTO vista)
        {
            if (vista is null)
                return BadRequest(new Respuesta<ReporteVistaDTO> { Mensaje = "La vista es obligatoria." });

            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;
            if (!TryUsuarioId(out var usuarioId)) return SinUsuario<ReporteVistaDTO>();

            var respuesta = await _app.GuardarVistaAsync(usuarioId, codigo, vista);
            return respuesta.Resultado ? Ok(respuesta) : BadRequest(respuesta);
        }

        [HttpDelete("{codigo}/vistas/{entry:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarVista([FromRoute] string codigo, [FromRoute] int entry)
        {
            var error = Verificar(_app.ObtenerDefinicion(codigo));
            if (error is not null) return error;
            if (!TryUsuarioId(out var usuarioId)) return SinUsuario<bool>();

            var respuesta = await _app.EliminarVistaAsync(usuarioId, codigo, entry);
            if (respuesta.Resultado) return Ok(respuesta);
            return respuesta.Mensaje.EndsWith("no existe.") ? NotFound(respuesta) : BadRequest(respuesta);
        }

        private bool TryUsuarioId(out int usuarioId) =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out usuarioId);

        private ActionResult SinUsuario<T>() =>
            Unauthorized(new Respuesta<T> { Mensaje = "No se pudo identificar al usuario." });
```

`API.Service.WebApi/Startup.cs`: tras `services.AddTransient<IReporteApplication, ReporteApplication>();` agrega `services.AddTransient<IReporteVistaDomain, ReporteVistaDomain>();`.

- [ ] **Step 4: Ver que pasan.** El `--filter` del Step 2 en verde; suite completa en verde; `dotnet build API.sln` sin errores.

- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Core/Reportes/ReporteVistaDomain.cs API.Application.DTO/reporte API.Application.Interface/IReporteApplication.cs API.Application.Main/ReporteApplication.cs API.Service.WebApi/Controllers/ReporteController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ReporteVistaDomainTests.cs API.Service.WebApi.Tests/Application/ReporteApplicationTests.cs API.Service.WebApi.Tests/Controllers/ReporteControllerTests.cs API.Service.WebApi.Tests/Controllers/ReportePermisosTests.cs
git commit -m "feat(reporteria): vistas guardadas por usuario (dominio, aplicacion y endpoints)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Prueba de humo de la API contra SQL real (sin código de producción)

**Files:** ninguno en el repo (los scripts van al scratchpad de la sesión; no se commitean). Si la prueba descubre un defecto, se corrige en la tarea que lo originó (o se agrega una tarea de corrección) con su prueba xUnit, y se vuelve a correr la humo.

**Interfaces:**
- Consumes: todo lo de las Tareas 1–11 (API completa) y `evid-up.ps1` / `evid-down.ps1` de la Tarea 1.
- Produces: evidencia en `<scratchpad>\evid-reporteria-api-<fecha>.txt` con contadores OK/FALLO. Esta tarea **solo** cierra cuando todos los chequeos pasan.

**Por qué:** las pruebas xUnit usan datos en memoria; una consulta LINQ que EF no sabe traducir a SQL solo falla contra SQL Server (spec §6). Esta tarea es obligatoria.

- [ ] **Step 1: Entorno.** `& '<scratchpad>\evid-up.ps1' API_DB_EVID2`. Levanta la API contra esa base **sin tocar `appsettings.json`** (variables de entorno de proceso; la contraseña se arma dentro del script y nunca se imprime):

```powershell
$cs = (Get-Content 'C:\Users\migue\source\repos\angelm0508\API\API.Service.WebApi\appsettings.json' -Raw | ConvertFrom-Json).ConnectionStrings.API_DB
$pw = [regex]::Match($cs, 'Password=([^;]*)').Groups[1].Value
$env:ConnectionStrings__API_DB = "Server=localhost; Database=API_DB_EVID2; User=sa; Password=$pw; TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5291'
Start-Process dotnet -ArgumentList 'run','--project','C:\Users\migue\source\repos\angelm0508\API\API.Service.WebApi','--no-launch-profile','-p:BaseOutputPath=<carpeta temporal>\' -PassThru
```

Espera a que `http://localhost:5291/swagger/index.html` responda 200. El usuario `admin` de la base nueva entra con `POST /api/Auth/login` `{ "usuario": "admin", "contrasena": "Admin123!" }` (token en `dato.token`, se envía como `Authorization: Bearer`).

- [ ] **Step 2: Datos de prueba** (SQL directo sobre `API_DB_EVID2` o por la API; lee las columnas `NOT NULL` y las llaves foráneas en `sql/crear-api-db-test-completo.sql`; moneda `QTZ`, series de documento y grupo de artículos `100` ya existen en la base nueva):
  - Clientes `C1` «Cliente Uno» y `C2` «Cliente Dos».
  - `Factura` (`DocSubType '--'`): **F1** C1, 2026-01-10, bruto 100, imp 12, total 112; **F2** C1, 2026-01-31 14:00, total 224; **F3** C2, 2026-02-01, total 56; **F4** C1, 2026-01-15, total 999 con `Cancelado = 'S'`.
  - `Factura` con `DocSubType 'ND'`: C1, 2026-01-20, total 56.
  - `NotaCredito`: C1, 2026-01-25, total 28.
  - Artículos `A1` y `A2` (grupo 100) y almacenes `01` y `02`; `ExistenciaArticulo`: (A1,01) disponible 10; (A1,02) 2; (A2,01) 4; (A2,02) 0. `Articulo.CostoPromedio`: A1 = 5, A2 = 20 (método `P`).

- [ ] **Step 3: Chequeos** (cada uno es un par esperado/obtenido en el script; imprime `OK`/`FALLO` y suma):

  *Catálogo y definición*
  1. `GET api/Reporte` con `admin` devuelve exactamente los reportes `existencias-valor` y `ventas-por-documento`.
  2. `GET api/Reporte/ventas-por-documento` devuelve 4 filtros y 11 columnas; `GET api/Reporte/zzz` → 404 con «El reporte 'zzz' no existe.».

  *Ventas por documento (`POST api/Reporte/ventas-por-documento/ejecutar`)*
  3. Sin filtros: 5 filas y `totales.total` = **420** (112 + 224 + 56 + 56 − 28); F4 (cancelada) no aparece.
  4. `fechas` 2026-01-01 a 2026-01-31: 4 filas y total **364** (F3 queda fuera; F2 del 31 a las 14:00 entra).
  5. `cliente = C2`: 1 fila, total 56. `tipo = NC`: 1 fila, total −28. `tipo = ND`: 1 fila, total 56. `tipo = FAC`: 3 filas, total 392.
  6. Orden `total` descendente: la primera fila es F2 (224).
  7. Agrupar por `tipo`: 3 grupos («Factura» 3 filas/392, «Nota de crédito» 1/−28, «Nota de débito» 1/56) y `totales.total` 420.
  8. Agrupar por `fecha` parte `Mes`: grupos «2026-01» (364) y «2026-02» (56). Dos niveles `tipo` > `fecha` Mes: la jerarquía suma igual.
  9. `soloResumen = true`: los grupos hoja no traen filas y conservan sus subtotales.
  10. Columnas `["nombreSn","total"]`: las filas solo traen esas dos claves.
  11. Errores: filtro desconocido → 400 «El filtro 'x' no es válido para este reporte.»; `fechas` mal formada → 400; 3 niveles de agrupación → 400 «Solo se permiten hasta 2 niveles de agrupación.»; agrupar por `bruto` → 400 «No se puede agrupar por 'Bruto': la columna no es agrupable.».

  *Existencias y valor (`existencias-valor`)*
  12. Sin filtros: 4 filas; `totales.enStock` = 16; `totales.valor` = **140** (50 + 10 + 80 + 0).
  13. `soloConExistencias = S`: 3 filas. `almacen = 01`: 2 filas, valor 130. `articulo = A1`: 2 filas, valor 60. `grupo` con parte del nombre del grupo 100: 4 filas.
  14. Agrupar por `almacen`: «01» valor 130 y «02» valor 10.

  *Exportación (`POST api/Reporte/ventas-por-documento/exportar?formato=`, cuerpo `{ "solicitud": {...}, "graficoPng": null }`, con agrupación por `tipo`)*
  15. `csv`: 200, `Content-Type: text/csv`, empieza con los bytes `EF BB BF`, encabezado `Grupo 1,...`.
  16. `xlsx`: 200, tipo `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, empieza con `PK`. Guarda el archivo y ábrelo para una revisión visual (encabezado, subtotales en negrita, total).
  17. `pdf`: 200, `application/pdf`, empieza con `%PDF`; repite con un PNG real de gráfico en `graficoPng` y confirma que sale más grande. Guarda el PDF y ábrelo.
  18. `formato=docx` → 400 «El formato 'docx' no es válido. Use xlsx, pdf o csv.»; `graficoPng` basura → 400.

  *Permisos*
  19. Crea un usuario sin permisos (por la API de usuarios o por SQL, ver `sql/2026-10-01-permisos-usuario.sql`) y entra con él: `GET api/Reporte` → lista vacía; `GET api/Reporte/ventas-por-documento`, `.../ejecutar` y `.../exportar` → **403**.
  20. Dale solo `ReportesInventario:V`: el catálogo muestra solo `existencias-valor`; ejecutar `ventas-por-documento` → 403; ejecutar `existencias-valor` → 200.

  *Vistas guardadas*
  21. `POST .../vistas` `{ "nombre": "Mi vista", "configuracion": { "columnas": ["fecha","total"] } }` → 200; `GET .../vistas` la lista con `configuracion` como **objeto**; guardar de nuevo `mi VISTA` la **reemplaza** (sigue habiendo una). `POST` con nombre vacío → 400 «El nombre de la vista es obligatorio.».
  22. Con otro usuario: `GET .../vistas` no ve la vista del primero y `DELETE .../vistas/{entry}` de la ajena → **404** «La vista no existe.». El propietario la borra → 200.
  23. Crea 50 vistas y confirma que la 51.ª devuelve 400 «Ya tienes 50 vistas guardadas. Borra alguna para crear otra.»; reemplazar una existente sigue funcionando.

  *Tope de filas*
  24. Inserta 60,000 facturas más por SQL (`INSERT ... SELECT TOP 60000 ...` sobre una tabla de números) y ejecuta `ventas-por-documento` sin filtros: `truncado = true`, `totalFilas = 50000`, y responde en menos de 30 s. Borra esas filas al terminar el chequeo.

- [ ] **Step 4: Limpieza.** Detén el proceso de la API; `& '<scratchpad>\evid-down.ps1' API_DB_EVID2`; confirma sin bases `API_DB_EVID*` (`SELECT name FROM sys.databases WHERE name LIKE 'API_DB_EVID%'`) y sin el puerto 5291 abierto. Guarda la evidencia en `<scratchpad>\evid-reporteria-api-<fecha>.txt`.

- [ ] **Step 5:** El resumen debe decir «N de N chequeos OK». Cualquier FALLO se corrige (con prueba xUnit que lo reproduzca, en el commit de la tarea que lo causó o en uno nuevo `fix(reporteria): ...`) y la humo se repite completa. No hay commit propio de esta tarea.

---

## Web (fases 1 a 3)

### Task 13: Cliente de la API, controlador, menú y pantalla genérica (filtros, columnas, orden, tabla, CSV/Excel/PDF e impresión)

**Files (repo Web, rama `main`, carpeta `C:\Users\migue\source\repos\angelm0508\Web`):**
- Create: `Web.ApiClient/Dtos/Reporte/ReporteResumenDTO.cs`, `Web.ApiClient/Clientes/IReporteApiClient.cs`, `Web.ApiClient/Clientes/ReporteApiClient.cs`
- Modify: `Web.UI/Program.cs` (registro del cliente)
- Create: `Web.UI/Controllers/ReportesController.cs`, `Web.UI/Views/Reportes/Index.cshtml`, `Web.UI/Views/Reportes/Ver.cshtml`, `Web.UI/wwwroot/js/reportes.js`
- Modify: `Web.UI/Views/Shared/_Layout.cshtml` (menú), `Web.UI/wwwroot/css/site.css` (estilos e impresión)

**Interfaces:**
- Consumes (API, ya probada en la Tarea 12): `GET api/Reporte`, `GET api/Reporte/{codigo}`, `POST api/Reporte/{codigo}/ejecutar`, `POST api/Reporte/{codigo}/exportar?formato=`. Respuestas camelCase: definición `{ codigo, nombre, descripcion, familia, modulo, filtros:[{campo,titulo,tipo,obligatorio,opciones:[{valor,titulo}],valorPorDefecto,subTipo}], columnas:[{campo,titulo,tipo,agrupable,totaliza,visiblePorDefecto}] }` (los enums como texto: `RangoFechas`, `Dinero`, `Suma`…); resultado `{ columnas, filas, grupos, totales, truncado, totalFilas, avisos }`.
- Produces: rutas Web `GET /Reportes`, `GET /Reportes/Ver?codigo=`, `GET /Reportes/Definicion?codigo=`, `POST /Reportes/Ejecutar?codigo=` (cuerpo = `SolicitudReporte` JSON), `POST /Reportes/Exportar?codigo=&formato=` (cuerpo `{ solicitud, graficoPng }`; devuelve el archivo), `GET /Reportes/BuscarSocios?texto=&tipo=`, `BuscarArticulos?texto=`, `BuscarAlmacenes?texto=`. Las Tareas 14 y 15 amplían `Ver.cshtml` y `reportes.js`; en este módulo JS las funciones se llaman `construirSolicitud`, `pintarResultado`, `leerFiltros`, `leerColumnas`, `leerOrden`, `exportar`, `ejecutar` (las siguientes tareas las reemplazan por nombre).

- [ ] **Step 1: DTO y cliente.**

`Web.ApiClient/Dtos/Reporte/ReporteResumenDTO.cs`:

```csharp
namespace Web.ApiClient.Dtos.Reporte
{
    public class ReporteResumenDTO
    {
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Familia { get; set; } = "";
    }
}
```

`Web.ApiClient/Clientes/IReporteApiClient.cs`:

```csharp
using System.Text.Json;
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Reporte;

namespace Web.ApiClient.Clientes
{
    /// <summary>Archivo generado por la API (o el motivo por el que no se pudo generar).</summary>
    public sealed class ArchivoDescargado
    {
        public byte[]? Contenido { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
        public string NombreArchivo { get; set; } = "reporte";
        public string? Error { get; set; }
    }

    public interface IReporteApiClient
    {
        Task<Respuesta<List<ReporteResumenDTO>>> ObtenerCatalogoAsync();

        /// <summary>La definición y los resultados son dinámicos: pasan tal cual (JSON) desde la API hasta el navegador.</summary>
        Task<Respuesta<JsonElement>> ObtenerDefinicionAsync(string codigo);
        Task<Respuesta<JsonElement>> EjecutarAsync(string codigo, JsonElement solicitud);
        Task<ArchivoDescargado> ExportarAsync(string codigo, string formato, JsonElement cuerpo);
    }
}
```

`Web.ApiClient/Clientes/ReporteApiClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.Reporte;

namespace Web.ApiClient.Clientes
{
    public class ReporteApiClient : ApiClientBase, IReporteApiClient
    {
        private const string Recurso = "api/Reporte";
        private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

        public ReporteApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<List<ReporteResumenDTO>>> ObtenerCatalogoAsync() =>
            GetAsync<List<ReporteResumenDTO>>(Recurso);

        public Task<Respuesta<JsonElement>> ObtenerDefinicionAsync(string codigo) =>
            GetAsync<JsonElement>($"{Recurso}/{Uri.EscapeDataString(codigo)}");

        public Task<Respuesta<JsonElement>> EjecutarAsync(string codigo, JsonElement solicitud) =>
            PostAsync<JsonElement>($"{Recurso}/{Uri.EscapeDataString(codigo)}/ejecutar", solicitud);

        public async Task<ArchivoDescargado> ExportarAsync(string codigo, string formato, JsonElement cuerpo)
        {
            try
            {
                using var response = await Http.PostAsJsonAsync(
                    $"{Recurso}/{Uri.EscapeDataString(codigo)}/exportar?formato={Uri.EscapeDataString(formato)}", cuerpo, OpcionesJson);

                if (response.IsSuccessStatusCode)
                {
                    var disposicion = response.Content.Headers.ContentDisposition;
                    return new ArchivoDescargado
                    {
                        Contenido = await response.Content.ReadAsByteArrayAsync(),
                        ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
                        NombreArchivo = disposicion?.FileNameStar ?? disposicion?.FileName?.Trim('"') ?? $"reporte.{formato}"
                    };
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return new ArchivoDescargado { Error = "Tu sesión expiró o no tienes permisos. Por favor inicia sesión nuevamente." };

                return new ArchivoDescargado { Error = ExtraerMensaje(await response.Content.ReadAsStringAsync()) };
            }
            catch (Exception ex)
            {
                return new ArchivoDescargado { Error = $"No se pudo conectar con la API: {ex.Message}" };
            }
        }

        private static string ExtraerMensaje(string contenido)
        {
            try
            {
                using var documento = JsonDocument.Parse(contenido);
                if (documento.RootElement.TryGetProperty("mensaje", out var m) && m.ValueKind == JsonValueKind.String)
                    return m.GetString() ?? "No se pudo generar el archivo.";
            }
            catch (JsonException) { }
            return "No se pudo generar el archivo.";
        }
    }
}
```

`Web.UI/Program.cs`: debajo del registro de `IAntiguedadApiClient` agrega

```csharp
builder.Services.AddHttpClient<IReporteApiClient, ReporteApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

(y, si `Program.cs` no tiene ya `using Web.ApiClient.Dtos.Reporte;`, no hace falta: el cliente se registra por su espacio `Web.ApiClient.Clientes`, ya importado).

- [ ] **Step 2: Controlador** `Web.UI/Controllers/ReportesController.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.Reporte;

namespace Web.UI.Controllers
{
    // Sin permiso a nivel de clase ni por acción: cada reporte pertenece a un módulo (ReportesVentas, ReportesCompras, ...)
    // y es la API la que lo evalúa en cada llamada (catálogo, definición, ejecución y exportación). Aquí solo se exige sesión.
    [Authorize]
    public class ReportesController : Controller
    {
        private readonly IReporteApiClient _reportes;
        private readonly ISocioNegocioApiClient _socios;
        private readonly IArticuloApiClient _articulos;
        private readonly IAlmacenApiClient _almacenes;

        public ReportesController(IReporteApiClient reportes, ISocioNegocioApiClient socios, IArticuloApiClient articulos, IAlmacenApiClient almacenes)
        {
            _reportes = reportes;
            _socios = socios;
            _articulos = articulos;
            _almacenes = almacenes;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var catalogo = await _reportes.ObtenerCatalogoAsync();
            ViewBag.Error = catalogo.Resultado ? null : catalogo.Mensaje;
            return View(catalogo.Dato ?? new List<ReporteResumenDTO>());
        }

        [HttpGet]
        public IActionResult Ver(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return RedirectToAction(nameof(Index));
            ViewBag.Codigo = codigo;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Definicion(string codigo) => Json(await _reportes.ObtenerDefinicionAsync(codigo));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ejecutar(string codigo, [FromBody] JsonElement solicitud) =>
            Json(await _reportes.EjecutarAsync(codigo, solicitud));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Exportar(string codigo, string formato, [FromBody] JsonElement cuerpo)
        {
            var archivo = await _reportes.ExportarAsync(codigo, formato, cuerpo);
            if (archivo.Contenido is null)
                return BadRequest(new { resultado = false, mensaje = archivo.Error ?? "No se pudo generar el archivo." });
            return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
        }

        // Buscadores de los filtros de socio, artículo y almacén (los usa App.autocompletar).
        [HttpGet]
        public async Task<IActionResult> BuscarSocios(string? texto, string? tipo)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync(tipo)
                : await _socios.ObtenerContenganNombreAsync(texto, tipo);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarArticulos(string? texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarAlmacenes(string? texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }
    }
}
```

- [ ] **Step 3: Vistas.**

`Web.UI/Views/Reportes/Index.cshtml`:

```razor
@model List<Web.ApiClient.Dtos.Reporte.ReporteResumenDTO>
@{
    ViewData["Title"] = "Reportes";
    var error = ViewBag.Error as string;
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Reportes</h3>
</div>

@if (!string.IsNullOrEmpty(error))
{
    <div class="alert alert-danger">@error</div>
}
else if (Model.Count == 0)
{
    <div class="alert alert-info">No tienes reportes habilitados. Pide al administrador que te asigne los permisos de reportes.</div>
}

@foreach (var familia in Model.GroupBy(r => r.Familia))
{
    <h5 class="mt-4 mb-3">@familia.Key</h5>
    <div class="row g-3">
        @foreach (var reporte in familia)
        {
            <div class="col-md-6 col-xl-4">
                <a class="card card-modulo h-100 text-decoration-none" asp-controller="Reportes" asp-action="Ver" asp-route-codigo="@reporte.Codigo">
                    <div class="card-body">
                        <h6 class="card-title mb-1"><i class="fa-solid fa-chart-column me-2"></i>@reporte.Nombre</h6>
                        <p class="card-text small text-muted mb-0">@reporte.Descripcion</p>
                    </div>
                </a>
            </div>
        }
    </div>
}
```

`Web.UI/Views/Reportes/Ver.cshtml`:

```razor
@{
    ViewData["Title"] = "Reporte";
}

<div id="reporte" data-codigo="@ViewBag.Codigo"
     data-url-definicion="/Reportes/Definicion"
     data-url-ejecutar="/Reportes/Ejecutar"
     data-url-exportar="/Reportes/Exportar">

    <div class="d-flex justify-content-between align-items-start mb-3 no-print">
        <div>
            <h3 class="mb-0" id="repTitulo">Reporte</h3>
            <p class="text-muted small mb-0" id="repDescripcion"></p>
        </div>
        <a class="btn btn-outline-secondary btn-sm" asp-controller="Reportes" asp-action="Index"><i class="fa-solid fa-arrow-left me-1"></i>Reportes</a>
    </div>

    <div class="card card-modulo mb-3 no-print">
        <div class="card-body">
            <h6 class="mb-3">Filtros</h6>
            <div class="row g-3" id="repFiltros"></div>

            <hr class="my-3" />

            <div class="row g-4">
                <div class="col-lg-6">
                    <h6>Columnas</h6>
                    <p class="text-muted small mb-2">Marca las que quieres ver y exportar; con las flechas cambias su orden.</p>
                    <div id="repColumnas"></div>
                </div>
                <div class="col-lg-6">
                    <h6>Orden</h6>
                    <p class="text-muted small mb-2">Hasta 3 criterios.</p>
                    <div id="repOrden"></div>
                </div>
            </div>

            <div class="d-flex flex-wrap align-items-center gap-2 mt-4">
                <button type="button" class="btn btn-primary" id="btnEjecutar"><i class="fa-solid fa-play me-1"></i>Ver reporte</button>
                <div class="btn-group" role="group" aria-label="Exportar">
                    <button type="button" class="btn btn-outline-secondary btn-exportar" data-formato="xlsx"><i class="fa-solid fa-file-excel me-1"></i>Excel</button>
                    <button type="button" class="btn btn-outline-secondary btn-exportar" data-formato="pdf"><i class="fa-solid fa-file-pdf me-1"></i>PDF</button>
                    <button type="button" class="btn btn-outline-secondary btn-exportar" data-formato="csv"><i class="fa-solid fa-file-csv me-1"></i>CSV</button>
                </div>
                <button type="button" class="btn btn-outline-secondary" id="btnImprimir"><i class="fa-solid fa-print me-1"></i>Imprimir</button>
            </div>
        </div>
    </div>

    <div class="print-only mb-3" id="repEncabezadoImpresion"></div>
    <div id="repAvisos"></div>

    <div class="card card-modulo">
        <div class="card-body">
            <div id="repResultado" class="table-responsive">
                <p class="text-muted mb-0">Elige los filtros y pulsa «Ver reporte».</p>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <script src="~/js/reportes.js" asp-append-version="true"></script>
}
```

- [ ] **Step 4: Menú.** En `Web.UI/Views/Shared/_Layout.cshtml`:
  1. En el bloque `@{ ... }` del inicio, junto a `bool MuestraAdministracion = Puede("Usuarios");`, agrega `bool MuestraReportes = new[] { "ReportesVentas", "ReportesCompras", "ReportesInventario", "ReportesCobrosPagos" }.Any(Puede);`.
  2. Justo **antes** de la línea `@if (MuestraAdministracion)` del `<nav>` agrega:

```razor
                    @if (MuestraReportes)
                    {
                        <a class="nav-link @(EsActivo("Reportes") ? "active" : "")" asp-controller="Reportes" asp-action="Index">
                            <i class="fa-solid fa-chart-column"></i><span>Reportes</span>
                        </a>
                    }
```

- [ ] **Step 5: Estilos.** Al final de `Web.UI/wwwroot/css/site.css` agrega (conserva el fin de línea CRLF del archivo):

```css

/* ---------- Reportes ---------- */
.rep-columna { padding: 2px 0; }
.rep-columna .btn-link { color: var(--bs-secondary-color, #6c757d); }
.rep-grupo td { background: var(--bs-tertiary-bg, #f1f3f5); font-weight: 600; }
.rep-subtotal td { font-weight: 600; background: var(--bs-tertiary-bg, #f8f9fa); }
.rep-total td { font-weight: 700; border-top: 2px solid var(--bs-border-color, #dee2e6); }
.rep-grafico { position: relative; height: 320px; }
.print-only { display: none; }

@media print {
  aside, .topbar, .no-print, .dataTables_filter, .dataTables_length, .dataTables_paginate, .dataTables_info { display: none !important; }
  .print-only { display: block !important; }
  .app-content, .content-area { margin: 0 !important; padding: 0 !important; width: 100% !important; }
  .card-modulo { border: 0 !important; box-shadow: none !important; }
}
```

- [ ] **Step 6: Lógica de la pantalla** `Web.UI/wwwroot/js/reportes.js`:

```javascript
// Pantalla genérica de reportes: se arma desde la definición que devuelve la API (filtros, columnas, orden) y trabaja siempre
// con la misma solicitud para ver, exportar e imprimir. Todo texto dinámico se escapa antes de insertarlo en HTML.
$(function () {
    const $raiz = $('#reporte');
    if ($raiz.length === 0) return;

    const codigo = $raiz.data('codigo');
    const url = {
        definicion: $raiz.data('url-definicion'),
        ejecutar: $raiz.data('url-ejecutar'),
        exportar: $raiz.data('url-exportar')
    };

    const escapar = v => $('<div>').text(v ?? '').html().replace(/"/g, '&quot;');
    const esNumerica = c => ['Dinero', 'Cantidad', 'Entero'].includes(c.tipo);
    const MAX_ORDEN = 3;

    let definicion = null;
    let columnasUI = [];          // [{ campo, titulo, tipo, visible }] en el orden que ve el usuario
    let tabla = null;             // DataTable del resultado plano

    // ---------- formato ----------

    function formatoValor(col, v) {
        if (v === null || v === undefined || v === '') return '';
        if (col.tipo === 'Fecha') return String(v).slice(0, 10);
        if (col.tipo === 'Dinero') return Number(v).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        if (col.tipo === 'Cantidad') return Number(v).toLocaleString('en-US', { maximumFractionDigits: 6 });
        if (col.tipo === 'Entero') return Number(v).toLocaleString('en-US', { maximumFractionDigits: 0 });
        return String(v);
    }

    // ---------- filtros ----------

    const BUSCADORES = {
        Socio: { endpoint: '/Reportes/BuscarSocios', etiqueta: s => `${s.codigo} - ${s.nombre ?? ''}` },
        Articulo: { endpoint: '/Reportes/BuscarArticulos', etiqueta: a => `${a.codigo} - ${a.nombre ?? ''}` },
        Almacen: { endpoint: '/Reportes/BuscarAlmacenes', etiqueta: a => `${a.codigo} - ${a.nombre ?? ''}` }
    };

    function pintarFiltros() {
        const html = definicion.filtros.map(f => {
            const id = `f_${f.campo}`;
            const etiqueta = `<label class="form-label" for="${id}">${escapar(f.titulo)}${f.obligatorio ? ' <span class="text-danger">*</span>' : ''}</label>`;
            switch (f.tipo) {
                case 'RangoFechas':
                    return `<div class="col-md-6 col-lg-4">${etiqueta}
                        <div class="input-group">
                            <input type="date" class="form-control" id="${id}_desde" aria-label="${escapar(f.titulo)} desde">
                            <span class="input-group-text">a</span>
                            <input type="date" class="form-control" id="${id}_hasta" aria-label="${escapar(f.titulo)} hasta">
                        </div></div>`;
                case 'Fecha':
                    return `<div class="col-md-4 col-lg-3">${etiqueta}<input type="date" class="form-control" id="${id}"></div>`;
                case 'Numero':
                    return `<div class="col-md-4 col-lg-3">${etiqueta}<input type="number" step="any" class="form-control" id="${id}"></div>`;
                case 'Lista': {
                    const opciones = ['<option value="">(Todos)</option>']
                        .concat((f.opciones ?? []).map(o => `<option value="${escapar(o.valor)}">${escapar(o.titulo)}</option>`));
                    return `<div class="col-md-4 col-lg-3">${etiqueta}<select class="form-select" id="${id}">${opciones.join('')}</select></div>`;
                }
                case 'Socio':
                case 'Articulo':
                case 'Almacen':
                    return `<div class="col-md-6 col-lg-4 position-relative">${etiqueta}
                        <input type="text" class="form-control" id="${id}_texto" placeholder="Buscar por código o nombre..." autocomplete="off">
                        <input type="hidden" id="${id}">
                        <div class="invalid-feedback d-none" id="${id}_error">Selecciona una opción de la lista o borra el texto.</div>
                        <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="${id}_lista"></ul>
                    </div>`;
                default:
                    return `<div class="col-md-4 col-lg-3">${etiqueta}<input type="text" class="form-control" id="${id}" maxlength="100"></div>`;
            }
        }).join('');
        $('#repFiltros').html(html);

        definicion.filtros.forEach(f => {
            const id = `f_${f.campo}`;
            if (f.valorPorDefecto && ['Lista', 'Texto', 'Numero', 'Fecha'].includes(f.tipo)) $(`#${id}`).val(f.valorPorDefecto);

            const b = BUSCADORES[f.tipo];
            if (!b) return;
            App.autocompletar({
                texto: $(`#${id}_texto`), oculto: $(`#${id}`), lista: $(`#${id}_lista`), error: $(`#${id}_error`),
                endpoint: b.endpoint,
                obtenerCodigo: x => x.codigo ?? x.Codigo,
                obtenerEtiqueta: b.etiqueta,
                minCaracteres: 0,
                requerido: false,
                parametros: () => (f.tipo === 'Socio' && f.subTipo ? { tipo: f.subTipo } : {})
            });
        });
    }

    // Devuelve los filtros con valor, en la forma de la API. En los buscadores se agrega "texto" (la etiqueta visible), que la API
    // ignora; sirve para encabezados de impresión y vistas guardadas.
    function leerFiltros() {
        const filtros = {};
        definicion.filtros.forEach(f => {
            const id = `#f_${f.campo}`;
            if (f.tipo === 'RangoFechas') {
                const desde = $(`${id}_desde`).val();
                const hasta = $(`${id}_hasta`).val();
                if (desde || hasta) filtros[f.campo] = { desde: desde || null, hasta: hasta || null };
                return;
            }
            const valor = String($(id).val() ?? '').trim();
            if (!valor) return;
            filtros[f.campo] = { valor };
            if (BUSCADORES[f.tipo]) filtros[f.campo].texto = $(`${id}_texto`).val();
        });
        return filtros;
    }

    function textoFiltros() {
        const filtros = leerFiltros();
        const partes = definicion.filtros.filter(f => filtros[f.campo]).map(f => {
            const v = filtros[f.campo];
            if (f.tipo === 'RangoFechas') return `${f.titulo}: ${v.desde ?? '…'} a ${v.hasta ?? '…'}`;
            if (f.tipo === 'Lista') return `${f.titulo}: ${(f.opciones ?? []).find(o => o.valor === v.valor)?.titulo ?? v.valor}`;
            return `${f.titulo}: ${v.texto || v.valor}`;
        });
        return partes.length ? partes.join('; ') : 'Sin filtros';
    }

    // ---------- columnas y orden ----------

    function pintarColumnas() {
        $('#repColumnas').html(columnasUI.map((c, i) => `
            <div class="d-flex align-items-center gap-2 rep-columna">
                <input class="form-check-input rep-col-visible" type="checkbox" id="col_${i}" data-i="${i}" ${c.visible ? 'checked' : ''}>
                <label class="form-check-label flex-grow-1" for="col_${i}">${escapar(c.titulo)}</label>
                <button type="button" class="btn btn-sm btn-link p-0 rep-col-subir" data-i="${i}" title="Subir" ${i === 0 ? 'disabled' : ''}><i class="fa-solid fa-arrow-up"></i></button>
                <button type="button" class="btn btn-sm btn-link p-0 rep-col-bajar" data-i="${i}" title="Bajar" ${i === columnasUI.length - 1 ? 'disabled' : ''}><i class="fa-solid fa-arrow-down"></i></button>
            </div>`).join(''));
    }

    $(document).on('change', '.rep-col-visible', function () {
        columnasUI[Number(this.dataset.i)].visible = this.checked;
    });

    function mover(i, delta) {
        const j = i + delta;
        if (j < 0 || j >= columnasUI.length) return;
        [columnasUI[i], columnasUI[j]] = [columnasUI[j], columnasUI[i]];
        pintarColumnas();
    }
    $(document).on('click', '.rep-col-subir', function () { mover(Number(this.dataset.i), -1); });
    $(document).on('click', '.rep-col-bajar', function () { mover(Number(this.dataset.i), 1); });

    function leerColumnas() {
        return columnasUI.filter(c => c.visible).map(c => c.campo);
    }

    function pintarOrden() {
        const opciones = ['<option value="">(sin orden)</option>']
            .concat(definicion.columnas.map(c => `<option value="${escapar(c.campo)}">${escapar(c.titulo)}</option>`)).join('');
        let html = '';
        for (let i = 0; i < MAX_ORDEN; i++) {
            html += `<div class="input-group input-group-sm mb-2">
                <span class="input-group-text">${i + 1}</span>
                <select class="form-select rep-orden-campo" data-i="${i}" aria-label="Orden ${i + 1}">${opciones}</select>
                <select class="form-select rep-orden-dir" data-i="${i}" aria-label="Sentido ${i + 1}">
                    <option value="asc">Ascendente</option><option value="desc">Descendente</option>
                </select></div>`;
        }
        $('#repOrden').html(html);
    }

    function leerOrden() {
        const orden = [];
        $('.rep-orden-campo').each(function () {
            if (!this.value) return;
            const dir = $(`.rep-orden-dir[data-i="${this.dataset.i}"]`).val();
            orden.push({ campo: this.value, descendente: dir === 'desc' });
        });
        return orden;
    }

    // ---------- solicitud ----------

    function construirSolicitud() {
        return { filtros: leerFiltros(), columnas: leerColumnas(), orden: leerOrden(), agrupar: [], soloResumen: false };
    }

    function validarSolicitud(solicitud) {
        if (solicitud.columnas.length === 0) {
            App.mostrarError('Selecciona al menos una columna.');
            return false;
        }
        const faltante = definicion.filtros.find(f => f.obligatorio && !solicitud.filtros[f.campo]);
        if (faltante) {
            App.mostrarError(`El filtro '${faltante.titulo}' es obligatorio.`);
            return false;
        }
        return true;
    }

    // ---------- resultado ----------

    function celdaTotal(col, i, totales) {
        const v = totales[col.campo];
        if (v !== undefined && v !== null) return formatoValor(col, v);
        return i === 0 ? 'Total' : '';
    }

    function pintarAvisos(res) {
        const avisos = [...(res.avisos ?? [])];
        if (res.truncado) avisos.push('El resultado se truncó al máximo de filas: refina los filtros para ver el resto.');
        $('#repAvisos').html(avisos.map(a => `<div class="alert alert-warning py-2">${escapar(a)}</div>`).join(''));
    }

    function pintarResultado(res) {
        pintarAvisos(res);
        if (tabla) { tabla.destroy(); tabla = null; }

        const cols = res.columnas;
        const thead = `<tr>${cols.map(c => `<th class="${esNumerica(c) ? 'text-end' : ''}">${escapar(c.titulo)}</th>`).join('')}</tr>`;
        const hayTotales = Object.keys(res.totales ?? {}).length > 0;
        const tfoot = hayTotales
            ? `<tfoot><tr class="fw-semibold">${cols.map((c, i) => `<th class="${esNumerica(c) ? 'text-end' : ''}">${escapar(celdaTotal(c, i, res.totales))}</th>`).join('')}</tr></tfoot>`
            : '';

        $('#repResultado').html(`<table id="tblReporte" class="table table-hover table-sm align-middle w-100"><thead>${thead}</thead><tbody></tbody>${tfoot}</table>`);

        tabla = $('#tblReporte').DataTable({
            data: res.filas.map(f => cols.map(c => f[c.campo] ?? null)),
            columns: cols.map(c => ({
                className: esNumerica(c) ? 'text-end' : '',
                render: (d, tipo) => tipo === 'display' ? escapar(formatoValor(c, d)) : (d ?? '')
            })),
            ordering: false,
            pageLength: 25,
            language: App.datatableEsEs
        });
    }

    async function ejecutar() {
        const solicitud = construirSolicitud();
        if (!validarSolicitud(solicitud)) return;

        $('#btnEjecutar').prop('disabled', true);
        const respuesta = await App.enviarJson(`${url.ejecutar}?codigo=${encodeURIComponent(codigo)}`, 'POST', solicitud);
        $('#btnEjecutar').prop('disabled', false);

        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        pintarResultado(respuesta.dato);
    }

    // ---------- exportar e imprimir ----------

    function nombreDeArchivo(disposicion, porDefecto) {
        if (!disposicion) return porDefecto;
        const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(disposicion);
        if (utf8) return decodeURIComponent(utf8[1]);
        const simple = /filename="?([^";]+)"?/i.exec(disposicion);
        return simple ? simple[1] : porDefecto;
    }

    function descargar(blob, nombre) {
        const enlace = document.createElement('a');
        const objeto = URL.createObjectURL(blob);
        enlace.href = objeto;
        enlace.download = nombre;
        document.body.appendChild(enlace);
        enlace.click();
        enlace.remove();
        setTimeout(() => URL.revokeObjectURL(objeto), 1000);
    }

    async function exportar(formato) {
        const solicitud = construirSolicitud();
        if (!validarSolicitud(solicitud)) return;

        const respuesta = await fetch(`${url.exportar}?codigo=${encodeURIComponent(codigo)}&formato=${encodeURIComponent(formato)}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': App.csrfToken() },
            body: JSON.stringify({ solicitud, graficoPng: null })
        });

        if (!respuesta.ok) {
            let mensaje = 'No se pudo generar el archivo.';
            try { mensaje = (await respuesta.json()).mensaje ?? mensaje; } catch { /* se deja el mensaje por defecto */ }
            App.mostrarError(mensaje);
            return;
        }
        descargar(await respuesta.blob(), nombreDeArchivo(respuesta.headers.get('Content-Disposition'), `reporte.${formato}`));
    }

    $(document).on('click', '.btn-exportar', function () { exportar(this.dataset.formato); });

    $('#btnImprimir').on('click', function () {
        $('#repEncabezadoImpresion').html(
            `<h4 class="mb-1">${escapar(definicion.nombre)}</h4><div class="small">${escapar(textoFiltros())}</div>
             <div class="small text-muted">Impreso el ${escapar(new Date().toLocaleString('es-GT'))}</div>`);
        // Para imprimir se muestran todas las filas (no solo la página actual de la tabla).
        const paginaAnterior = tabla ? tabla.page.len() : null;
        if (tabla) tabla.page.len(-1).draw();
        window.addEventListener('afterprint', function restaurar() {
            window.removeEventListener('afterprint', restaurar);
            if (tabla && paginaAnterior !== null) tabla.page.len(paginaAnterior).draw();
        });
        window.print();
    });

    $('#btnEjecutar').on('click', ejecutar);

    // ---------- arranque ----------

    async function iniciar() {
        const respuesta = await $.get(url.definicion, { codigo });
        if (!respuesta.resultado) {
            $('#repResultado').html(`<div class="alert alert-danger mb-0">${escapar(respuesta.mensaje)}</div>`);
            $('#btnEjecutar, .btn-exportar, #btnImprimir').prop('disabled', true);
            return;
        }
        definicion = respuesta.dato;
        $('#repTitulo').text(definicion.nombre);
        $('#repDescripcion').text(definicion.descripcion);
        document.title = `${definicion.nombre} - Reportes`;

        columnasUI = definicion.columnas.map(c => ({ campo: c.campo, titulo: c.titulo, tipo: c.tipo, visible: c.visiblePorDefecto }));
        pintarFiltros();
        pintarColumnas();
        pintarOrden();
    }

    iniciar();
});
```

- [ ] **Step 7: Verificar.** `dotnet build Web.slnx` (desde la carpeta Web; si hay una Web en ejecución que bloquea `bin`, usa `-p:BaseOutputPath="<carpeta temporal>\"`) → 0 errores (el único aviso aceptable es el CS8602 previo de `ArticulosController`). No hay pruebas automáticas de la Web: la verificación en navegador es la Tarea 16, pero ahora haz una comprobación mínima con una API real:
  1. Levanta `evid-up.ps1 API_DB_EVID3`, la API (puerto 5291, como en la Tarea 12) con datos de la Tarea 12, y la Web con `ApiSettings__BaseUrl=http://localhost:5291` (`dotnet run --project Web.UI --no-launch-profile --urls http://localhost:5295`).
  2. Entra como `admin`/`Admin123!`; el menú muestra «Reportes»; el catálogo lista los 2 reportes; «Ventas por documento» muestra los filtros, las columnas y el orden; «Ver reporte» pinta la tabla con total; Excel, PDF y CSV descargan; «Imprimir» muestra la vista de impresión.
  3. La consola del navegador no muestra errores. Apaga todo y elimina `API_DB_EVID3` al terminar.

- [ ] **Step 8: Commit** (repo Web):

```bash
git add Web.ApiClient/Dtos/Reporte Web.ApiClient/Clientes/IReporteApiClient.cs Web.ApiClient/Clientes/ReporteApiClient.cs Web.UI/Program.cs Web.UI/Controllers/ReportesController.cs Web.UI/Views/Reportes Web.UI/wwwroot/js/reportes.js Web.UI/Views/Shared/_Layout.cshtml Web.UI/wwwroot/css/site.css
git commit -m "feat(reportes): pantalla generica de reportes con filtros, columnas, orden, exportacion e impresion

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Agrupación y subtotales en la pantalla

**Files:**
- Modify: `Web.UI/Views/Reportes/Ver.cshtml`, `Web.UI/wwwroot/js/reportes.js`

**Interfaces:**
- Consumes: la API con `agrupar` y `soloResumen` (Tareas 4 y 12); `resultado.grupos` (árbol: `{ campo, titulo, clave, etiqueta, conteo, subtotales, hijos, filas }`), `resultado.totales`.
- Produces: bloque «Agrupar» (hasta 2 niveles, con parte de fecha cuando la columna es de tipo `Fecha`, y casilla «Solo resumen»); `construirSolicitud` incluye `agrupar` y `soloResumen`; `pintarResultado` pinta el árbol de grupos con subtotales y el total general cuando hay agrupación.

- [ ] **Step 1: Markup.** En `Ver.cshtml`, reemplaza el bloque `<div class="row g-4"> ... </div>` de columnas y orden por esta versión de tres columnas:

```razor
            <div class="row g-4">
                <div class="col-lg-4">
                    <h6>Columnas</h6>
                    <p class="text-muted small mb-2">Marca las que quieres ver y exportar; con las flechas cambias su orden.</p>
                    <div id="repColumnas"></div>
                </div>
                <div class="col-lg-4">
                    <h6>Orden</h6>
                    <p class="text-muted small mb-2">Hasta 3 criterios.</p>
                    <div id="repOrden"></div>
                </div>
                <div class="col-lg-4">
                    <h6>Agrupar</h6>
                    <p class="text-muted small mb-2">Hasta 2 niveles, con subtotales y total general.</p>
                    <div id="repAgrupar"></div>
                    <div class="form-check mt-2">
                        <input class="form-check-input" type="checkbox" id="repSoloResumen">
                        <label class="form-check-label" for="repSoloResumen">Solo resumen (sin detalle)</label>
                    </div>
                </div>
            </div>
```

- [ ] **Step 2: Lógica.** En `reportes.js`:

  1. Agrega estas constantes y funciones **después de `leerOrden`**:

```javascript
    const MAX_NIVELES = 2;

    function pintarAgrupacion() {
        const agrupables = definicion.columnas.filter(c => c.agrupable);
        const opciones = ['<option value="">(sin agrupar)</option>']
            .concat(agrupables.map(c => `<option value="${escapar(c.campo)}" data-tipo="${escapar(c.tipo)}">${escapar(c.titulo)}</option>`)).join('');
        let html = '';
        for (let i = 0; i < MAX_NIVELES; i++) {
            html += `<div class="input-group input-group-sm mb-2">
                <span class="input-group-text">${i + 1}</span>
                <select class="form-select rep-agrupar-campo" data-i="${i}" aria-label="Agrupar nivel ${i + 1}">${opciones}</select>
                <select class="form-select rep-agrupar-parte" data-i="${i}" aria-label="Parte de fecha nivel ${i + 1}" disabled>
                    <option value="Dia">Día</option><option value="Mes">Mes</option><option value="Anio">Año</option>
                </select></div>`;
        }
        $('#repAgrupar').html(html);
    }

    // La parte de fecha (día, mes, año) solo se habilita cuando la columna elegida es de tipo Fecha.
    $(document).on('change', '.rep-agrupar-campo', function () {
        const esFecha = $(this).find('option:selected').data('tipo') === 'Fecha';
        $(`.rep-agrupar-parte[data-i="${this.dataset.i}"]`).prop('disabled', !esFecha);
    });

    function leerAgrupacion() {
        const agrupar = [];
        $('.rep-agrupar-campo').each(function () {
            if (!this.value) return;
            const $parte = $(`.rep-agrupar-parte[data-i="${this.dataset.i}"]`);
            const nivel = { campo: this.value };
            if (!$parte.prop('disabled')) nivel.parte = $parte.val();
            agrupar.push(nivel);
        });
        return agrupar;
    }
```

  2. **Reemplaza** la función `construirSolicitud` por:

```javascript
    function construirSolicitud() {
        return {
            filtros: leerFiltros(),
            columnas: leerColumnas(),
            orden: leerOrden(),
            agrupar: leerAgrupacion(),
            soloResumen: $('#repSoloResumen').is(':checked')
        };
    }
```

  3. **Reemplaza** la función `pintarResultado` por esta versión, que delega en `pintarPlano` (el cuerpo anterior) o en `pintarGrupos`:

```javascript
    function pintarResultado(res) {
        pintarAvisos(res);
        if (tabla) { tabla.destroy(); tabla = null; }
        if ((res.grupos ?? []).length > 0) pintarGrupos(res);
        else pintarPlano(res);
    }

    function pintarPlano(res) {
        const cols = res.columnas;
        const thead = `<tr>${cols.map(c => `<th class="${esNumerica(c) ? 'text-end' : ''}">${escapar(c.titulo)}</th>`).join('')}</tr>`;
        const hayTotales = Object.keys(res.totales ?? {}).length > 0;
        const tfoot = hayTotales
            ? `<tfoot><tr class="fw-semibold">${cols.map((c, i) => `<th class="${esNumerica(c) ? 'text-end' : ''}">${escapar(celdaTotal(c, i, res.totales))}</th>`).join('')}</tr></tfoot>`
            : '';

        $('#repResultado').html(`<table id="tblReporte" class="table table-hover table-sm align-middle w-100"><thead>${thead}</thead><tbody></tbody>${tfoot}</table>`);

        tabla = $('#tblReporte').DataTable({
            data: res.filas.map(f => cols.map(c => f[c.campo] ?? null)),
            columns: cols.map(c => ({
                className: esNumerica(c) ? 'text-end' : '',
                render: (d, tipo) => tipo === 'display' ? escapar(formatoValor(c, d)) : (d ?? '')
            })),
            ordering: false,
            pageLength: 25,
            language: App.datatableEsEs
        });
    }

    // Resultado agrupado: tabla simple con una fila de encabezado por grupo, sus filas, y una fila de subtotal; al final el total general.
    function pintarGrupos(res) {
        const cols = res.columnas;
        const filaValores = (valores, clase) =>
            `<tr class="${clase}">${cols.map(c => {
                const v = valores[c.campo];
                const texto = v === undefined || v === null ? '' : formatoValor(c, v);
                return `<td class="${esNumerica(c) ? 'text-end' : ''}">${escapar(texto)}</td>`;
            }).join('')}</tr>`;

        const filasHtml = [];
        const recorrer = (grupos, nivel) => {
            grupos.forEach(g => {
                const sangria = `style="padding-left: ${0.5 + nivel * 1.25}rem"`;
                filasHtml.push(`<tr class="rep-grupo"><td colspan="${cols.length}" ${sangria}>${escapar(g.titulo)}: ${escapar(g.etiqueta)} (${escapar(g.conteo)})</td></tr>`);
                if ((g.hijos ?? []).length > 0) recorrer(g.hijos, nivel + 1);
                else (g.filas ?? []).forEach(f => filasHtml.push(
                    `<tr>${cols.map(c => `<td class="${esNumerica(c) ? 'text-end' : ''}">${escapar(formatoValor(c, f[c.campo]))}</td>`).join('')}</tr>`));
                filasHtml.push(filaValores(g.subtotales ?? {}, 'rep-subtotal'));
            });
        };
        recorrer(res.grupos, 0);
        if (Object.keys(res.totales ?? {}).length > 0) filasHtml.push(filaValores(res.totales, 'rep-total'));

        const thead = `<tr>${cols.map(c => `<th class="${esNumerica(c) ? 'text-end' : ''}">${escapar(c.titulo)}</th>`).join('')}</tr>`;
        $('#repResultado').html(`<table id="tblReporte" class="table table-sm align-middle w-100"><thead>${thead}</thead><tbody>${filasHtml.join('')}</tbody></table>`);
    }
```

  4. En `iniciar()`, después de `pintarOrden();` agrega `pintarAgrupacion();`.

- [ ] **Step 3: Verificar.** `dotnet build Web.slnx` sin errores. Con el entorno de la Tarea 13 (API + Web contra una base temporal con los datos de la Tarea 12): «Ventas por documento» agrupado por Tipo → 3 grupos con subtotales y total 420; por Fecha (Mes) → «2026-01» 364 y «2026-02» 56; dos niveles Tipo > Fecha (Mes); «Solo resumen» deja solo encabezados y subtotales; la parte de fecha se habilita solo al elegir una columna de fecha; Excel y PDF del resultado agrupado traen los grupos; sin errores en consola. Limpia el entorno.

- [ ] **Step 4: Commit.**

```bash
git add Web.UI/Views/Reportes/Ver.cshtml Web.UI/wwwroot/js/reportes.js
git commit -m "feat(reportes): agrupacion con subtotales y total general en la pantalla de reportes

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Gráfico (Chart.js) y vistas guardadas en la pantalla

**Files:**
- Create: `Web.UI/wwwroot/lib/chartjs/chart.umd.min.js` (descarga, ver Step 1)
- Modify: `Web.ApiClient/Clientes/IReporteApiClient.cs`, `Web.ApiClient/Clientes/ReporteApiClient.cs`, `Web.UI/Controllers/ReportesController.cs`, `Web.UI/Views/Reportes/Ver.cshtml`, `Web.UI/wwwroot/js/reportes.js`

**Interfaces:**
- Consumes: API de vistas (`GET/POST api/Reporte/{codigo}/vistas`, `DELETE .../vistas/{entry}`; `configuracion` es un objeto JSON) y `graficoPng` del endpoint de exportación (máximo 2 MB; la API acepta el prefijo `data:image/png;base64,`).
- Produces: rutas Web `GET /Reportes/Vistas?codigo=`, `POST /Reportes/GuardarVista?codigo=` (cuerpo `{ nombre, configuracion }`), `POST /Reportes/EliminarVista?codigo=&entry=`; gráfico (barras, líneas, pastel) en la pantalla y su imagen en el PDF; selector de vistas guardadas con Guardar y Borrar.

- [ ] **Step 1: Descargar Chart.js (confirmación del usuario).** **Antes de descargar nada, pregúntale al usuario en el chat** (nombre, origen y tamaño): «Voy a descargar `chart.umd.min.js` de Chart.js 4.4.1 (MIT, ≈200 KB) desde `https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js` y guardarlo en `Web.UI/wwwroot/lib/chartjs/`. ¿Procedo?». Solo con un sí explícito:

```bash
mkdir -p Web.UI/wwwroot/lib/chartjs
curl -fsSL -o Web.UI/wwwroot/lib/chartjs/chart.umd.min.js https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js
head -c 200 Web.UI/wwwroot/lib/chartjs/chart.umd.min.js; echo; wc -c Web.UI/wwwroot/lib/chartjs/chart.umd.min.js
```

Verifica que empieza con el comentario `/*! * Chart.js v4.4.1` y que pesa unos 200 KB. Si el usuario no autoriza la descarga o falla, **detente y pregunta** (no hay alternativa por CDN: el spec la prohíbe).

- [ ] **Step 2: Cliente y controlador de vistas.** En `IReporteApiClient.cs` agrega:

```csharp
        Task<Respuesta<JsonElement>> ObtenerVistasAsync(string codigo);
        Task<Respuesta<JsonElement>> GuardarVistaAsync(string codigo, JsonElement vista);
        Task<Respuesta<bool>> EliminarVistaAsync(string codigo, int entry);
```

En `ReporteApiClient.cs`:

```csharp
        public Task<Respuesta<JsonElement>> ObtenerVistasAsync(string codigo) =>
            GetAsync<JsonElement>($"{Recurso}/{Uri.EscapeDataString(codigo)}/vistas");

        public Task<Respuesta<JsonElement>> GuardarVistaAsync(string codigo, JsonElement vista) =>
            PostAsync<JsonElement>($"{Recurso}/{Uri.EscapeDataString(codigo)}/vistas", vista);

        public Task<Respuesta<bool>> EliminarVistaAsync(string codigo, int entry) =>
            DeleteAsync<bool>($"{Recurso}/{Uri.EscapeDataString(codigo)}/vistas/{entry}");
```

En `ReportesController.cs` (antes de los buscadores):

```csharp
        [HttpGet]
        public async Task<IActionResult> Vistas(string codigo) => Json(await _reportes.ObtenerVistasAsync(codigo));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarVista(string codigo, [FromBody] JsonElement vista) =>
            Json(await _reportes.GuardarVistaAsync(codigo, vista));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarVista(string codigo, int entry) =>
            Json(await _reportes.EliminarVistaAsync(codigo, entry));
```

- [ ] **Step 3: Markup.** En `Ver.cshtml`:
  1. Dentro del `div.d-flex` de botones (el que tiene `btnEjecutar`, el grupo de exportación e `btnImprimir`), **agrega al final** el bloque de vistas:

```razor
                <div class="ms-lg-auto d-flex flex-wrap align-items-center gap-2">
                    <select class="form-select form-select-sm" id="repVistas" style="width: auto; min-width: 12rem;" aria-label="Vistas guardadas"></select>
                    <button type="button" class="btn btn-outline-secondary btn-sm" id="btnGuardarVista" title="Guardar la configuración actual como vista"><i class="fa-solid fa-floppy-disk me-1"></i>Guardar vista</button>
                    <button type="button" class="btn btn-outline-danger btn-sm" id="btnBorrarVista" title="Borrar la vista elegida" disabled><i class="fa-solid fa-trash"></i></button>
                </div>
```

  2. **Entre** `<div id="repAvisos"></div>` y el `div.card` del resultado agrega la tarjeta del gráfico:

```razor
    <div class="card card-modulo mb-3" id="repGraficoCard">
        <div class="card-body">
            <div class="d-flex flex-wrap align-items-end gap-2 mb-2 no-print">
                <div>
                    <label class="form-label small mb-1" for="repGrafTipo">Gráfico</label>
                    <select class="form-select form-select-sm" id="repGrafTipo">
                        <option value="">(sin gráfico)</option>
                        <option value="bar">Barras</option>
                        <option value="line">Líneas</option>
                        <option value="pie">Pastel</option>
                    </select>
                </div>
                <div>
                    <label class="form-label small mb-1" for="repGrafCategoria">Categorías</label>
                    <select class="form-select form-select-sm" id="repGrafCategoria"></select>
                </div>
                <div>
                    <label class="form-label small mb-1" for="repGrafValor">Valores</label>
                    <select class="form-select form-select-sm" id="repGrafValor"></select>
                </div>
            </div>
            <div class="rep-grafico d-none" id="repGraficoContenedor"><canvas id="repGrafico"></canvas></div>
        </div>
    </div>
```

  3. En `@section Scripts`, **antes** de `reportes.js` agrega `<script src="~/lib/chartjs/chart.umd.min.js"></script>`.

- [ ] **Step 4: Lógica.** En `reportes.js`:

  1. Agrega estas variables junto a `let tabla = null;`:

```javascript
    let ultimoResultado = null;   // último resultado pintado (de él se arma el gráfico)
    let grafico = null;           // instancia de Chart.js
    let vistas = [];              // vistas guardadas del usuario para este reporte
    const PALETA = ['#0f766e', '#2563eb', '#d97706', '#dc2626', '#7c3aed', '#059669', '#db2777', '#0891b2', '#65a30d', '#9333ea'];
    const MAX_CATEGORIAS = 30;
    const MAX_PNG_CHARS = 2700000;   // ≈ 2 MB en base64: por encima la API lo rechaza
```

  2. En `pintarResultado`, **antes** de `pintarAvisos(res);` agrega `ultimoResultado = res;` y **al final** de la función (después del `if/else`) agrega `prepararGrafico(res);`.

  3. Agrega estas funciones antes de `// ---------- exportar e imprimir ----------`:

```javascript
    // ---------- gráfico ----------

    const FONDO_BLANCO = {
        id: 'fondoBlanco',
        beforeDraw: chart => {
            const ctx = chart.ctx;
            ctx.save();
            ctx.globalCompositeOperation = 'destination-over';
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, chart.width, chart.height);
            ctx.restore();
        }
    };

    function destruirGrafico() {
        if (grafico) { grafico.destroy(); grafico = null; }
        $('#repGraficoContenedor').addClass('d-none');
    }

    // Con agrupación las categorías son los grupos del primer nivel; sin ella, una columna de texto o fecha del resultado.
    function prepararGrafico(res) {
        destruirGrafico();
        const agrupado = (res.grupos ?? []).length > 0;
        const columnasValor = res.columnas.filter(c => esNumerica(c));
        const columnasCategoria = agrupado ? [] : res.columnas.filter(c => !esNumerica(c));

        $('#repGrafValor').html(columnasValor.map(c => `<option value="${escapar(c.campo)}">${escapar(c.titulo)}</option>`).join(''));
        $('#repGrafCategoria').html(agrupado
            ? `<option value="">${escapar(res.grupos[0].titulo)} (primer nivel de agrupación)</option>`
            : columnasCategoria.map(c => `<option value="${escapar(c.campo)}">${escapar(c.titulo)}</option>`).join(''))
            .prop('disabled', agrupado);

        if ($('#repGrafTipo').val()) dibujarGrafico();
    }

    function datosGrafico(res, campoCategoria, campoValor) {
        const agregados = new Map();
        if ((res.grupos ?? []).length > 0) {
            res.grupos.forEach(g => agregados.set(g.etiqueta, Number(g.subtotales?.[campoValor] ?? 0)));
        } else {
            const cols = res.columnas;
            const colCat = cols.find(c => c.campo === campoCategoria);
            res.filas.forEach(f => {
                const etiqueta = formatoValor(colCat, f[campoCategoria]) || '(Sin valor)';
                agregados.set(etiqueta, (agregados.get(etiqueta) ?? 0) + Number(f[campoValor] ?? 0));
            });
        }
        let pares = [...agregados.entries()];
        if (pares.length > MAX_CATEGORIAS) {
            const resto = pares.slice(MAX_CATEGORIAS - 1).reduce((s, [, v]) => s + v, 0);
            pares = pares.slice(0, MAX_CATEGORIAS - 1).concat([['Otros', resto]]);
        }
        return { etiquetas: pares.map(p => p[0]), valores: pares.map(p => p[1]) };
    }

    function dibujarGrafico() {
        destruirGrafico();
        const tipo = $('#repGrafTipo').val();
        if (!tipo || !ultimoResultado) return;

        const campoValor = $('#repGrafValor').val();
        if (!campoValor) return;
        const { etiquetas, valores } = datosGrafico(ultimoResultado, $('#repGrafCategoria').val(), campoValor);
        if (etiquetas.length === 0) return;

        const colValor = ultimoResultado.columnas.find(c => c.campo === campoValor);
        const colores = etiquetas.map((_, i) => PALETA[i % PALETA.length]);
        $('#repGraficoContenedor').removeClass('d-none');
        grafico = new Chart(document.getElementById('repGrafico'), {
            type: tipo,
            data: {
                labels: etiquetas,
                datasets: [{
                    label: colValor.titulo,
                    data: valores,
                    backgroundColor: tipo === 'line' ? PALETA[0] : colores,
                    borderColor: tipo === 'line' ? PALETA[0] : colores,
                    fill: false
                }]
            },
            options: { responsive: true, maintainAspectRatio: false, animation: false, plugins: { legend: { display: tipo === 'pie' } } },
            plugins: [FONDO_BLANCO]
        });
    }

    $('#repGrafTipo, #repGrafCategoria, #repGrafValor').on('change', dibujarGrafico);

    // PNG del gráfico para el PDF; null si no hay gráfico o pesa demasiado.
    function graficoPng() {
        if (!grafico) return null;
        const png = grafico.toBase64Image('image/png', 1);
        if (png.length > MAX_PNG_CHARS) {
            App.mostrarError('El gráfico es demasiado grande para incluirlo en el PDF; el archivo se genera sin él.');
            return null;
        }
        return png;
    }
```

  4. En `exportar`, cambia `body: JSON.stringify({ solicitud, graficoPng: null })` por `body: JSON.stringify({ solicitud, graficoPng: formato === 'pdf' ? graficoPng() : null })`.

  5. Agrega las vistas guardadas, antes de `// ---------- arranque ----------`:

```javascript
    // ---------- vistas guardadas ----------

    function configuracionActual() {
        return {
            filtros: leerFiltros(),
            columnas: columnasUI.map(c => ({ campo: c.campo, visible: c.visible })),
            orden: leerOrden(),
            agrupar: leerAgrupacion(),
            soloResumen: $('#repSoloResumen').is(':checked'),
            grafico: { tipo: $('#repGrafTipo').val() || '', categoria: $('#repGrafCategoria').val() || '', valor: $('#repGrafValor').val() || '' }
        };
    }

    // Aplica una configuración guardada ignorando lo que ya no existe en la definición.
    function aplicarConfiguracion(config) {
        // filtros
        definicion.filtros.forEach(f => {
            const id = `#f_${f.campo}`;
            const v = config.filtros?.[f.campo];
            if (f.tipo === 'RangoFechas') {
                $(`${id}_desde`).val(v?.desde ?? '');
                $(`${id}_hasta`).val(v?.hasta ?? '');
                return;
            }
            $(id).val(v?.valor ?? (f.valorPorDefecto ?? ''));
            if (BUSCADORES[f.tipo]) $(`${id}_texto`).val(v?.texto ?? '');
        });

        // columnas: las guardadas primero (en su orden), luego las que la definición tiene y la vista no conocía
        const guardadas = (config.columnas ?? []).filter(g => definicion.columnas.some(c => c.campo === g.campo));
        const nuevas = definicion.columnas.filter(c => !guardadas.some(g => g.campo === c.campo));
        columnasUI = guardadas.map(g => {
            const c = definicion.columnas.find(x => x.campo === g.campo);
            return { campo: c.campo, titulo: c.titulo, tipo: c.tipo, visible: !!g.visible };
        }).concat(nuevas.map(c => ({ campo: c.campo, titulo: c.titulo, tipo: c.tipo, visible: c.visiblePorDefecto })));
        pintarColumnas();

        // orden y agrupación
        $('.rep-orden-campo').each(function () {
            const o = (config.orden ?? [])[Number(this.dataset.i)];
            const existe = o && definicion.columnas.some(c => c.campo === o.campo);
            $(this).val(existe ? o.campo : '');
            $(`.rep-orden-dir[data-i="${this.dataset.i}"]`).val(existe && o.descendente ? 'desc' : 'asc');
        });
        $('.rep-agrupar-campo').each(function () {
            const a = (config.agrupar ?? [])[Number(this.dataset.i)];
            const existe = a && definicion.columnas.some(c => c.campo === a.campo && c.agrupable);
            $(this).val(existe ? a.campo : '').trigger('change');
            if (existe && a.parte) $(`.rep-agrupar-parte[data-i="${this.dataset.i}"]`).val(a.parte);
        });
        $('#repSoloResumen').prop('checked', !!config.soloResumen);

        // gráfico: se guarda la elección y se aplica al volver a ver el reporte
        $('#repGrafTipo').val(config.grafico?.tipo ?? '');
        grafoPendiente = config.grafico ?? null;
    }
    let grafoPendiente = null;

    function pintarSelectorVistas(seleccionada) {
        $('#repVistas').html(['<option value="">(Vistas guardadas)</option>']
            .concat(vistas.map(v => `<option value="${escapar(v.entry)}">${escapar(v.nombre)}</option>`)).join(''));
        $('#repVistas').val(seleccionada ? String(seleccionada) : '');
        $('#btnBorrarVista').prop('disabled', !$('#repVistas').val());
    }

    async function cargarVistas(seleccionada) {
        const respuesta = await $.get('/Reportes/Vistas', { codigo });
        vistas = respuesta.resultado && respuesta.dato ? respuesta.dato : [];
        pintarSelectorVistas(seleccionada);
    }

    $('#repVistas').on('change', async function () {
        $('#btnBorrarVista').prop('disabled', !this.value);
        const vista = vistas.find(v => String(v.entry) === this.value);
        if (!vista) return;
        aplicarConfiguracion(vista.configuracion);
        await ejecutar();
        // la categoría y el valor del gráfico se pueden elegir recién cuando hay resultado
        if (grafoPendiente) {
            if (grafoPendiente.categoria && !$('#repGrafCategoria').prop('disabled')) $('#repGrafCategoria').val(grafoPendiente.categoria);
            if (grafoPendiente.valor) $('#repGrafValor').val(grafoPendiente.valor);
            dibujarGrafico();
            grafoPendiente = null;
        }
    });

    $('#btnGuardarVista').on('click', async function () {
        const pedido = await Swal.fire({
            title: 'Guardar vista',
            input: 'text',
            inputLabel: 'Nombre de la vista',
            inputAttributes: { maxlength: 100 },
            showCancelButton: true,
            confirmButtonText: 'Guardar',
            cancelButtonText: 'Cancelar',
            inputValidator: v => (!v || !v.trim()) ? 'Escribe un nombre.' : null
        });
        if (!pedido.isConfirmed) return;

        const respuesta = await App.enviarJson(`/Reportes/GuardarVista?codigo=${encodeURIComponent(codigo)}`, 'POST',
            { nombre: pedido.value.trim(), configuracion: configuracionActual() });
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        await cargarVistas(respuesta.dato.entry);
        App.mostrarExito('Vista guardada correctamente.');
    });

    $('#btnBorrarVista').on('click', async function () {
        const entry = $('#repVistas').val();
        if (!entry) return;
        if (!await App.confirmarEliminar('La vista se borrará y no se puede deshacer.')) return;

        const respuesta = await App.enviarJson(`/Reportes/EliminarVista?codigo=${encodeURIComponent(codigo)}&entry=${encodeURIComponent(entry)}`, 'POST', {});
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        await cargarVistas(null);
        App.mostrarExito('Vista eliminada correctamente.');
    });
```

  6. En `iniciar()`, después de `pintarAgrupacion();` agrega `await cargarVistas(null);`.

- [ ] **Step 5: Verificar.** `dotnet build Web.slnx` sin errores. Con el entorno de la Tarea 13 y datos de la Tarea 12:
  1. Ventas por documento agrupado por Tipo + gráfico de barras con «Total»: se dibuja con 3 barras; con «Pastel» también; sin agrupación, categoría «Cliente» y valor «Total» suma por cliente.
  2. «PDF» con el gráfico visible: el PDF descargado incluye el gráfico (ábrelo); sin gráfico, sale sin él.
  3. «Guardar vista» con un nombre → aparece en el selector; recargar la página y elegirla restaura filtros, columnas, orden, agrupación y gráfico y ejecuta el reporte; guardar con el mismo nombre (otra capitalización) la reemplaza; «Borrar» la elimina (con confirmación); con otro usuario no se ven las vistas del primero.
  4. El nombre de vista con `<script>alert(1)</script>` se muestra como texto y no se ejecuta; sin errores en consola. Limpia el entorno.

- [ ] **Step 6: Commit** (repo Web; `chart.umd.min.js` se agrega solo si el usuario autorizó la descarga y el archivo es el esperado):

```bash
git add Web.UI/wwwroot/lib/chartjs Web.ApiClient/Clientes/IReporteApiClient.cs Web.ApiClient/Clientes/ReporteApiClient.cs Web.UI/Controllers/ReportesController.cs Web.UI/Views/Reportes/Ver.cshtml Web.UI/wwwroot/js/reportes.js
git commit -m "feat(reportes): grafico con Chart.js (tambien en el PDF) y vistas guardadas por usuario

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 16: Recorrido final, documentación y cierre

**Files:**
- Modify: `API/AGENTS.md`, `Web/AGENTS.md`, `API/docs/traspaso-2026-11-07.md`
- Create (scratchpad, no se commitea): capturas y evidencia del recorrido

**Interfaces:**
- Consumes: todo lo anterior. Produce: documentación para quien continúe y la evidencia de que las fases 1–3 funcionan de punta a punta.

- [ ] **Step 1: Recorrido en navegador** con el entorno de la Tarea 13 (base temporal `API_DB_EVID4` con los datos de la Tarea 12 más un cliente con nombre con acentos y comillas para probar el escape), por usuario:
  - **`admin`:** menú «Reportes» → catálogo con los 2 reportes; para cada uno: filtros, columnas (ocultar/mostrar/mover), orden de 3 criterios, agrupación de 1 y 2 niveles (con parte de fecha), «Solo resumen», total y subtotales correctos (los mismos números de la Tarea 12), Excel/PDF/CSV/Imprimir (revisa los archivos abiertos), gráfico en barras/líneas/pastel con y sin agrupación, y una vista guardada (guardar, reabrir tras recargar, reemplazar, borrar).
  - **Usuario sin permisos de reportes:** el menú no muestra «Reportes»; abrir `/Reportes` directo muestra el aviso «No tienes reportes habilitados…»; abrir `/Reportes/Ver?codigo=ventas-por-documento` muestra el error de permisos sin romper la página; los exports directos devuelven el mensaje de permisos.
  - **Usuario con solo `ReportesInventario:V`:** ve solo «Existencias y valor por almacén».
  - Consola del navegador sin errores del producto en todo el recorrido; nombres con acentos, comillas y `<` se ven como texto.
  - Responsivo: la pantalla de un reporte se usa a ancho de tablet (768 px) sin desbordes graves.
  Guarda capturas y un resumen en `<scratchpad>\evid-reporteria-web-<fecha>.txt`. Apaga todo y elimina `API_DB_EVID4`.

- [ ] **Step 2: Documentación.**
  1. `API/AGENTS.md`: en «Reglas de negocio clave» agrega el punto

```markdown
- **Reportería dinámica:** catálogo de reportes con un motor genérico (`Domain.Core/Reportes/MotorReportes`). Un reporte nuevo es una clase `IReporte` en `Domain.Core/Reportes/Definiciones/` (definición de filtros y columnas + consulta EF/LINQ sin SQL libre) registrada en `Startup` con `services.AddTransient<IReporte, MiReporte>()`; cada reporte pertenece a un módulo de permiso (`ReportesVentas`, `ReportesCompras`, `ReportesInventario`, `ReportesCobrosPagos`) que se evalúa dentro de la acción de `ReporteController`. Tope de 50,000 filas, 2 niveles de agrupación, 3 criterios de orden, 50 vistas por usuario. Exportadores (CSV, Excel con ClosedXML, PDF con QuestPDF Community) en `API.Infraestructure.Exportacion`. El nombre de empresa de los encabezados sale de la clave `Reportes:Empresa` (por defecto «Sistema de Gestión»). Una consulta nueva se prueba con xUnit **y** con SQL real (puede no traducirse a SQL).
```

  2. `Web/AGENTS.md`: en «Convenciones» agrega

```markdown
- **Reportes:** una sola pantalla genérica (`Views/Reportes/Ver.cshtml` + `wwwroot/js/reportes.js`) que se arma desde la definición que devuelve la API; los reportes nuevos no llevan vista propia. El permiso lo evalúa la API por reporte (no hay `[RequierePermiso]` en `ReportesController`). Chart.js está empaquetado en `wwwroot/lib/chartjs` (sin CDN).
```

  3. `API/docs/traspaso-2026-11-07.md`: en «Qué se construyó» agrega al inicio el punto «0. **Reportería dinámica (fases 1–3)** (spec `2026-11-08-reporteria-dinamica-design.md`, plan `2026-11-08-reporteria-dinamica-implementation.md`): motor genérico, 2 reportes de muestra, exportación a CSV/Excel/PDF, agrupación con subtotales, gráfico y vistas guardadas. **Falta la fase 4** (los otros 16 reportes del catálogo): plan aparte»; en «Despliegue» agrega «Base existente: correr `sql/2026-11-08-reporteria.sql` y asignar a las plantillas los módulos `Reportes*` (sin eso solo `admin` ve los reportes); la API necesita restaurar los paquetes ClosedXML y QuestPDF; opcional: clave `Reportes:Empresa` en la configuración»; y ajusta la línea base de pruebas con el nuevo conteo.

- [ ] **Step 3: Verificación final.**

```powershell
# API (carpeta C:\Users\migue\source\repos\angelm0508\API)
dotnet build API.sln
dotnet test API.Service.WebApi.Tests --nologo -v q
git status --short
git log --oneline -14
# Web (carpeta C:\Users\migue\source\repos\angelm0508\Web)
dotnet build Web.slnx
git status --short
```

Esperado: builds sin errores (aviso CS8602 previo de la Web), suite en verde con el número de la línea base + las pruebas nuevas, y `git status` solo con los archivos sin seguimiento conocidos (`appsettings.json`, `sql/crear-api-db-completo*.sql`, el script temporal de limpieza). No debe quedar ninguna base `API_DB_EVID*` ni proceso `dotnet` de pruebas.

- [ ] **Step 4: Commits de documentación** (uno por repo; sin push):

```bash
# API
git add AGENTS.md docs/traspaso-2026-11-07.md
git commit -m "docs: reporteria dinamica en AGENTS.md y documento de traspaso

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
# Web
git add AGENTS.md
git commit -m "docs: reportes en AGENTS.md

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 5: Cierre.** Informa al usuario: qué quedó funcionando (con los números de la humo y del recorrido), los commits de cada repo, lo que **no** se hizo (fase 4 y demás fuera de alcance del spec) y que no se hizo push. Pregunta si desea el plan de la fase 4 (catálogo completo de 16 reportes).

---
