# Pagos efectuados (pagos a proveedores) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Registrar los pagos a proveedores (objeto 46) sobre facturas de compra y notas de débito de proveedor, con saldo y estado de pago por documento calculados, y botón "Registrar pago" en los listados de Facturas de compra y Notas de débito de proveedor (Fase A de cuentas por cobrar / por pagar).

**Architecture:** Espejo exacto del módulo ya construido y verificado **Pagos recibidos** (commits `a50b97d` … `b61b789` en la API y `c8a3db5` … `fbfa61b` en la Web), con tablas propias (`PagoEfectuado`, `PagoEfectuadoFactura`, `PagoEfectuadoMedio`), un `SaldoFacturaCompraService` sin estado, `PagoEfectuadoDomain` con transacción `Serializable`, y guardas en `FacturaCompraDomain`/`NotaDebitoCompraDomain`/`NotaCreditoCompraDomain`. El catálogo `FormaPago` se **comparte**. Lo único que se refactoriza de cobros es la validación de los medios de pago, que pasa a un helper común (`ValidadorMediosPago`) con mensajes idénticos.

**Tech Stack:** .NET 7 API (N-capas, EF Core 7 DB-first, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server (solo bases temporales para las pruebas reales), PowerShell/node para parches y verificación.

**Spec:** `docs/superpowers/specs/2026-10-25-pagos-efectuados-design.md` (repo API, commit `29da294`).

**Cómo usar este plan (importante):** el módulo de cobros ya existe en el repositorio y es la **plantilla autoritativa**. Para cada tarea, el plan nombra los archivos plantilla (`Plantilla:`), la **tabla de traducción** de abajo que se aplica de forma mecánica, y el código **nuevo o distinto** completo. Quien implementa abre la plantilla, la copia al archivo destino y aplica la tabla; las pruebas de la plantilla se copian y traducen igual. No se reescribe de memoria: se parte del archivo real. Los mensajes traducidos se verifican literalmente en pruebas.

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. Antes de empezar: `git log --oneline -1` muestra el commit del plan o posterior; `git status --short` solo con `API.Service.WebApi/appsettings.json` modificado. Línea base de pruebas: **1946** pasando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `fbfa61b` (o posterior). `git status --short` muestra **9 archivos ajenos** sin commit que NO se tocan ni se stagean: `Web.ApiClient/Dtos/Almacen/AlmacenActualizarDTO.cs`, `Web.ApiClient/Dtos/Almacen/AlmacenCrearDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloActualizarDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioActualizarDTO.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioCrearDTO.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`. Línea base: `dotnet build Web.slnx` con 0 errores.
- Build/test con `BaseOutputPath` en el scratchpad (el API de desarrollo del usuario puede estar corriendo y bloquear `bin\`; ejecutar **desde PowerShell**):
  - API (suite): `dotnet build API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"` y luego `dotnet test "C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\Debug\net7.0\API.Service.WebApi.Tests.dll"` (con `--filter "FullyQualifiedName~Clase"` para una clase).
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`
- SQL de pruebas: SOLO bases temporales (`API_DB_PAGOS_EF1`, `API_DB_PAGOS_EF2`); **jamás** `API_DB_TEST`. La contraseña de `sa` se extrae de `API.Service.WebApi\appsettings.json` (`[regex]::Match($cfg,'Password=([^;"]*)')`) a una variable de proceso y no se imprime ni se escribe en archivos. Las bases temporales se eliminan al terminar. Los scripts `evid-up.ps1`/`evid-down.ps1` del scratchpad (`-NombreDb API_DB_EVID<n>`) crean y destruyen un entorno completo (API :5199 + Web :5200) desde `crear-api-db-test-completo.sql`.

## Global Constraints

Cada tarea las incluye implícitamente.

- **Código de objeto:** pago efectuado = `46` (`TipoObjeto='46'`, `NumeracionDocumento.CodigoObj='46'`, `SubTipoDoc='--'`, `DocAlias='Pago efectuado'`). Ocupados (no se tocan): 1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 13, 15, 24, 59, 60, 70.
- **Módulo de permisos (grupo `Compras`):** `PagosEfectuados`. Acciones `Ver`/`Agregar`/`Actualizar`/`Eliminar` (claims `Modulo:VAUE`). Cancelar un pago = `Eliminar` sobre `PagosEfectuados`. `FormasPago` ya existe y no cambia.
- **Documentos pagables:** filas de `FacturaCompra` con `DocSubType` `'--'` (factura de compra) o `'ND'` (nota de débito de proveedor), `Cancelado != 'S'`, del proveedor del pago. `FacturaCompra.EstadoDoc` **no** se toca nunca.
- **Saldo:** `Saldo = Redondear(Total − Acreditado − Pagado)` (nunca negativo), con `Acreditado` = suma de `NotaCreditoCompra.TotalDoc` con `BaseTipo=13`, `BaseEntry=entry`, `Cancelado<>'S'` y `Pagado` = suma de `PagoEfectuadoFactura.MontoAplicado` de pagos con `Cancelado<>'S'`. Estado: `"Pagada"` si `Saldo == 0`; `"Parcial"` si `Pagado > 0` y `Saldo > 0`; `"Pendiente"` en otro caso. `Redondear(x) = Math.Round(x, 2, MidpointRounding.AwayFromZero)`.
- **Pago:** al menos un documento y una forma de pago; `Σ MontoAplicado == Σ Monto` y ese valor es `TotalPago` (lo calcula el servidor); montos > 0 con a lo más 2 decimales; `MontoAplicado <= Saldo` vigente validado **dentro** de la transacción `Serializable`; sin documentos repetidos; el socio debe existir y ser de tipo `P` pero **no** se exige que esté activo.
- **Formas de pago:** reglas de `FormaPago.TipoDato` idénticas a cobros (`N` sin dato; `C` cheque: referencia = nº de cheque y banco obligatorios; `T` transferencia: referencia; `J` tarjeta: referencia). Se descartan referencia/banco/fecha para `N`; `FechaCheque` solo se conserva en `C`.
- **Cancelar un pago:** solo marca `Cancelado='S'` y `FechaCancelado`; no se cancela dos veces; los pagos no se editan ni eliminan.
- **Guardas:** cancelar o eliminar una factura de compra / nota de débito de proveedor con pagos activos se rechaza con `"El documento tiene pagos aplicados (Pago 3, Pago 5). Cancele primero el pago."`; eliminar con pagos cancelados registrados: `"El documento tiene pagos registrados (cancelados) y no se puede eliminar; consérvelo cancelado."`; el límite de una nota de crédito de proveedor con factura base pasa a `acreditado + nota <= total − pagado`.
- **Mensajes en español exactamente como aparecen** en este plan y en la tabla de traducción (las pruebas los verifican literalmente).
- **Cobros no cambia de comportamiento:** toda prueba existente de `PagoRecibido*`, `SaldoFactura*`, `FormaPago*` debe seguir pasando sin modificarse (salvo agregar el helper nuevo, Tarea 2).
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` puede aparecer modificado: NUNCA commitearlo. Los 9 archivos ajenos de la Web NUNCA se stagean ni se modifican. **Sin `git push`.**
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (comprobar con `file`); archivos nuevos en LF; sin churn de EOL. Comentarios y mensajes en español, estilo del repo.
- **Verificación al cerrar cada tarea de la API:** `dotnet build` sin errores y las pruebas de la tarea en verde; la suite completa (1946 + las nuevas) se corre en la Tarea 9.

## Tabla de traducción (cobros → pagos efectuados)

Se aplica de forma mecánica al copiar plantillas (código, comentarios, nombres de pruebas y mensajes). Respetar mayúsculas/minúsculas y género.

| Cobros | Pagos efectuados |
|---|---|
| `PagoRecibido` / `pagoRecibido` / `PagosRecibidos` | `PagoEfectuado` / `pagoEfectuado` / `PagosEfectuados` |
| `Pagos recibidos` / `Pago recibido` / `pagos recibidos` | `Pagos efectuados` / `Pago efectuado` / `pagos efectuados` |
| `SaldoFactura` (clase/servicio/DTO/record/interfaz) | `SaldoFacturaCompra` |
| `ISaldoFacturaService` / `SaldoFacturaService` | `ISaldoFacturaCompraService` / `SaldoFacturaCompraService` |
| entidad `Factura` (repositorio, consultas) | `FacturaCompra` |
| `FacturaEntry` (campo, DTO, parámetros) | `FacturaCompraEntry` |
| tabla `PagoRecibidoFactura` / `PagoRecibidoMedio` | `PagoEfectuadoFactura` / `PagoEfectuadoMedio` |
| `NotaCredito` con `TipoBaseFactura = 6` | `NotaCreditoCompra` con `TipoBaseFactura = 13` |
| `TipoObjetoPago = "24"` | `TipoObjetoPago = "46"` |
| `TipoSocioCliente = "C"` | `TipoSocioProveedor = "P"` |
| `ValidarClienteAsync` | `ValidarProveedorAsync` |
| `"El socio de negocio debe ser un cliente."` | `"El socio de negocio debe ser un proveedor."` |
| `"La serie no corresponde a pagos recibidos."` | `"La serie no corresponde a pagos efectuados."` |
| `"La serie está bloqueada y no se puede usar para registrar pagos recibidos."` | `"La serie está bloqueada y no se puede usar para registrar pagos efectuados."` |
| cliente / clientes (texto de usuario y comentarios) | proveedor / proveedores |
| `"El documento {n} no existe."` y demás con "documento" | sin cambio (ya es genérico) |
| etiqueta "Factura N" / "Nota de débito N" | "Factura de compra N" / "Nota de débito de proveedor N" |
| `"Pago {NumDoc}"` en guardas | sin cambio |
| módulo `FacturasCompra` ← `Facturas`, `NotasDebitoCompra` ← `NotasDebito` (permisos de Saldos) | ver Tarea 8 |
| Web: controlador `FacturasController` / `NotasDebitoController` | `FacturasCompraController` / `NotasDebitoCompraController` |
| Web: parámetro de URL `facturaEntry` / `codigoSn` | `facturaCompraEntry` / `codigoSn` |
| Web: menú Ventas | menú Compras |

## File Structure

**API — entidades y datos**
- Crear `sql/2026-10-25-pagos-efectuados.sql`.
- Crear `API.Domain.Entity/Models/{PagoEfectuado,PagoEfectuadoFactura,PagoEfectuadoMedio}.cs`; modificar `API.Domain.Entity/Models/ApiDbTestContext.cs`.
- Crear `API.Infraestructure.Repository/{PagoEfectuado,PagoEfectuadoFactura,PagoEfectuadoMedio}Repositorio.cs`.

**API — dominio**
- Crear `API.Domain.Interface/{ISaldoFacturaCompraService,IPagoEfectuadoDomain}.cs` y `API.Domain.Entity/Models/IMedioPago.cs`.
- Crear `API.Domain.Core/{ValidadorMediosPago,SaldoFacturaCompraService,GuardaPagosEfectuados,PagoEfectuadoDomain}.cs`.
- Modificar `API.Domain.Core/{PagoRecibidoDomain,FacturaCompraDomain,NotaDebitoCompraDomain,NotaCreditoCompraDomain}.cs`, `API.Domain.Interface/FacturaBaseCredito.cs`, `API.Domain.Entity/Models/PagoRecibidoMedio` (archivo parcial nuevo `PagoRecibidoMedio.IMedioPago.cs`).

**API — aplicación y servicio**
- Crear `API.Application.DTO/pagoEfectuado/*` (espejo de `pagoRecibido/*`), `API.Application.Interface/{IPagoEfectuadoApplication,ISaldoFacturaCompraApplication}.cs`, `API.Application.Main/{PagoEfectuadoApplication,SaldoFacturaCompraApplication}.cs`.
- Crear `API.Service.WebApi/Controllers/{PagoEfectuadoController,SaldoFacturaCompraController}.cs`; modificar `API.Service.WebApi/Startup.cs`, `API.Transversal.Mapper/PerfilMapeo.cs`.
- Modificar `API.Application.DTO/notaCreditoCompra/*FacturaBase*` y `API.Application.Main/NotaCreditoCompraApplication.cs` (disponible descuenta pagos).

**Pruebas (`API.Service.WebApi.Tests`)**
- Crear `Domain/{ValidadorMediosPagoTests,SaldoFacturaCompraServiceTests,PagoEfectuadoDomainTests,GuardaPagosEfectuadosTests,ModeloPagosEfectuadosTests}.cs`, `Application/{PagoEfectuadoApplicationTests,PagosEfectuadosDtoLongitudesTests}.cs`, `Controllers/{PagoEfectuadoControllerTests,PagosEfectuadosPermisosTests,SaldoFacturaCompraControllerTests}.cs`.
- Modificar `Domain/{FacturaCompraDomainTests,NotaDebitoCompraDomainTests,NotaCreditoCompraDomainTests}.cs`, `Application/NotaCreditoCompraApplicationTests.cs`.

**Web**
- Crear `Web.ApiClient/Dtos/{PagoEfectuado,SaldoFacturaCompra}/*`, `Web.ApiClient/Clientes/{IPagoEfectuadoApiClient,PagoEfectuadoApiClient,ISaldoFacturaCompraApiClient,SaldoFacturaCompraApiClient}.cs`; modificar `Web.ApiClient/Dtos/{FacturaCompra/FacturaCompraDTO,NotaDebitoCompra/NotaDebitoCompraDTO}.cs`, `Web.UI/Program.cs`.
- Crear `Web.UI/Controllers/PagosEfectuadosController.cs`, `Web.UI/Views/PagosEfectuados/{Index,_Detalle,Crear}.cshtml`, `Web.UI/wwwroot/js/{pagosefectuados,pagosefectuados-crear}.js`; modificar `Web.UI/Views/Shared/_Layout.cshtml`, `Web.UI/wwwroot/js/site.js`.
- Modificar `Web.UI/Controllers/{FacturasCompraController,NotasDebitoCompraController}.cs`, `Web.UI/Views/{FacturasCompra,NotasDebitoCompra}/Index.cshtml`, `Web.UI/wwwroot/js/{facturascompra,notasdebitocompra}.js`.

---

## Fase 1 — Base de datos, entidades, helper de medios y saldo (API)

### Task 1: Script SQL, entidades EF, mapeo y repositorios de pagos efectuados

**Plantilla:** commit `a50b97d` (`git show a50b97d`): `sql/2026-10-23-pagos-recibidos.sql`, `API.Domain.Entity/Models/PagoRecibido*.cs`, el bloque de `ApiDbTestContext.cs`, `API.Infraestructure.Repository/PagoRecibido*Repositorio.cs`, líneas de DI en `Startup.cs`, `Domain/ModeloPagosTests.cs`.

**Precondición:** `git log --oneline -1` ≥ commit del plan; `Select-String -Path sql\*.sql -Pattern "'46'"` no encuentra nada (objeto 46 libre).

**Files:**
- Create: `sql/2026-10-25-pagos-efectuados.sql`
- Create: `API.Domain.Entity/Models/PagoEfectuado.cs`, `PagoEfectuadoFactura.cs`, `PagoEfectuadoMedio.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs`, `API.Service.WebApi/Startup.cs`
- Create: `API.Infraestructure.Repository/PagoEfectuadoRepositorio.cs`, `PagoEfectuadoFacturaRepositorio.cs`, `PagoEfectuadoMedioRepositorio.cs`
- Test: `API.Service.WebApi.Tests/Domain/ModeloPagosEfectuadosTests.cs`

**Interfaces — Produces:** entidades `PagoEfectuado` (`Entry, NumDoc, Serie, Cancelado, NumManual, TipoObjeto, FechaDoc (DateTime?), FechaCancelado, CodigoSn, NombreSn, TotalPago (decimal), Comentario`; navegación a `SerieNavigation`, `CodigoSnNavigation`, colecciones `Facturas`/`Medios`), `PagoEfectuadoFactura` (`Entry, NoLinea, FacturaCompraEntry, MontoAplicado`), `PagoEfectuadoMedio` (`Entry, NoLinea, CodFormaPago, Monto, Referencia, Banco, FechaCheque`); repositorios `IRepositorioGenerico<PagoEfectuado,int>`, `IRepositorioGenerico<PagoEfectuadoFactura,(int Entry,int NoLinea)>`, `IRepositorioGenerico<PagoEfectuadoMedio,(int Entry,int NoLinea)>` registrados en `Startup`.

- [ ] **Step 1: Prueba de modelo (falla).** Copiar `Domain/ModeloPagosTests.cs` a `ModeloPagosEfectuadosTests.cs` y traducir (entidades, `FacturaCompraEntry`, `TipoObjeto == "46"`, claves `(Entry, NoLinea)`). Debe fallar por compilación (tipos inexistentes).
- [ ] **Step 2: Script SQL.** Crear `sql/2026-10-25-pagos-efectuados.sql` copiando el de cobros y traduciendo, con **estas diferencias exactas**:
  - Sin la tabla `FormaPago` ni sus semillas (ya existe; el script empieza con `IF OBJECT_ID('dbo.FormaPago') IS NULL THROW 50000, 'Falta la tabla FormaPago: ejecute antes sql/2026-10-23-pagos-recibidos.sql', 1;`).
  - `PagoEfectuado`: `TipoObjeto nvarchar(11) NOT NULL DEFAULT '46' CHECK (TipoObjeto IN ('46'))`; `CodigoSN` FK a `SocioNegocio(Codigo)`; `UNIQUE (Serie, NumDoc)`; `TotalPago decimal(19,6) NOT NULL CHECK (TotalPago > 0)`.
  - `PagoEfectuadoFactura (Entry, NoLinea, FacturaCompraEntry FK → FacturaCompra(Entry), MontoAplicado CHECK > 0, PK (Entry, NoLinea), UNIQUE (Entry, FacturaCompraEntry), índice IX_PagoEfectuadoFactura_Factura (FacturaCompraEntry) INCLUDE (MontoAplicado))`.
  - `PagoEfectuadoMedio` idéntica a cobros (FK a `FormaPago(Codigo)`).
  - Semillas: serie "Primario" del objeto `46` (`TipoSerie='D'`, `IniNumero=1`, `SigNumero=1`) en `NumeracionDocumentoDet` si no existe; `NumeracionDocumento ('46','--','Pago efectuado', SerieDfct = MIN(Serie) del objeto 46)`; módulo `('PagosEfectuados','Pagos efectuados','Compras')` en `Modulo` con `MERGE` **sin** `WHEN NOT MATCHED BY SOURCE`.
  - Todo idempotente (`IF NOT EXISTS`/`IF OBJECT_ID ... IS NULL`).
- [ ] **Step 3: Entidades.** Crear las tres entidades (parciales, mismo estilo que `PagoRecibido*.cs`) y el bloque en `ApiDbTestContext.cs` (`DbSet`s + `OnModelCreating` con claves, longitudes, `HasDefaultValue`, relaciones y `HasIndex` como en cobros).
- [ ] **Step 4: Repositorios y DI.** Copiar `PagoRecibido*Repositorio.cs` traduciendo; en `Startup.cs`, junto a las líneas de `PagoRecibido*`, agregar `services.AddTransient<IRepositorioGenerico<PagoEfectuado, int>, PagoEfectuadoRepositorio>();` y las dos equivalentes.
- [ ] **Step 5: Verificar.** Build sin errores; `ModeloPagosEfectuadosTests` en verde.
- [ ] **Step 6: SQL real.** Crear una base temporal `API_DB_PAGOS_EF1` con `crear-api-db-test-completo.sql` (reemplazando `API_DB_TEST`), ejecutar el script nuevo **dos veces** (idempotencia) y comprobar: 3 tablas creadas, `NumeracionDocumento` con `46`, módulo `PagosEfectuados`, y que `INSERT` con `TotalPago = 0` o `TipoObjeto = '47'` falla por CHECK. Eliminar la base.
- [ ] **Step 7: Commit.**

```bash
git add sql/2026-10-25-pagos-efectuados.sql API.Domain.Entity/Models/PagoEfectuado.cs API.Domain.Entity/Models/PagoEfectuadoFactura.cs API.Domain.Entity/Models/PagoEfectuadoMedio.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Infraestructure.Repository/PagoEfectuadoRepositorio.cs API.Infraestructure.Repository/PagoEfectuadoFacturaRepositorio.cs API.Infraestructure.Repository/PagoEfectuadoMedioRepositorio.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ModeloPagosEfectuadosTests.cs
git commit -m "feat(pagos-efectuados): script SQL, entidades EF, mapeo y repositorios de pagos efectuados

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 2: Helper compartido `ValidadorMediosPago` (refactor sin cambio de comportamiento)

**Precondición:** la suite actual pasa (1946 + las del Task 1); `git status` limpio salvo `appsettings.json`.

**Files:**
- Create: `API.Domain.Entity/Models/IMedioPago.cs`, `API.Domain.Core/ValidadorMediosPago.cs`
- Create: `API.Domain.Entity/Models/PagoRecibidoMedio.IMedioPago.cs`, `API.Domain.Entity/Models/PagoEfectuadoMedio.IMedioPago.cs` (clases parciales que implementan la interfaz)
- Modify: `API.Domain.Core/PagoRecibidoDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/ValidadorMediosPagoTests.cs`; las pruebas de `PagoRecibidoDomainTests` **no se modifican**.

**Interfaces — Produces:**

> Nota de ubicación: `API.Domain.Interface` referencia a `API.Domain.Entity`, no al revés. Por eso `IMedioPago` se declara en **`API.Domain.Entity/Models/IMedioPago.cs`** (namespace `API.Domain.Entity.Models`), junto con las clases parciales que la implementan; `ValidadorMediosPago` (Domain.Core) la consume.

```csharp
namespace API.Domain.Entity.Models;

public interface IMedioPago
{
    string CodFormaPago { get; set; }
    decimal Monto { get; set; }
    string? Referencia { get; set; }
    string? Banco { get; set; }
    DateTime? FechaCheque { get; set; }
}
```

```csharp
namespace API.Domain.Entity.Models;
public partial class PagoRecibidoMedio : IMedioPago { }
public partial class PagoEfectuadoMedio : IMedioPago { }
```

```csharp
// API.Domain.Core/ValidadorMediosPago.cs
using API.Domain.Entity.Models;

namespace API.Domain.Core
{
    /// <summary>
    /// Validación de las formas de pago de un pago (cobro o pago a proveedor): compartida para que ambos módulos
    /// apliquen exactamente las mismas reglas y mensajes. Normaliza referencia, banco y fecha de cheque según el TipoDato.
    /// </summary>
    public static class ValidadorMediosPago
    {
        public static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        public static async Task ValidarAsync<T>(IReadOnlyList<T> medios, Func<string, Task<FormaPago?>> buscarForma) where T : IMedioPago
        {
            if (medios.Count == 0)
                throw new Exception("Indique al menos una forma de pago.");

            foreach (var medio in medios)
            {
                medio.CodFormaPago = (medio.CodFormaPago ?? string.Empty).Trim();
                var forma = await buscarForma(medio.CodFormaPago)
                    ?? throw new Exception($"La forma de pago {medio.CodFormaPago} no existe.");

                if (forma.Activo != "S")
                    throw new Exception($"La forma de pago {forma.Nombre} está inactiva.");

                if (medio.Monto <= 0m)
                    throw new Exception($"El monto de la forma de pago {forma.Nombre} debe ser mayor que 0.");

                if (Redondear(medio.Monto) != medio.Monto)
                    throw new Exception($"El monto de la forma de pago {forma.Nombre} no puede tener más de 2 decimales.");

                medio.Referencia = Limpiar(medio.Referencia);
                medio.Banco = Limpiar(medio.Banco);

                switch (forma.TipoDato)
                {
                    case "C":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere el número de cheque.");
                        if (medio.Banco is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere el banco.");
                        break;
                    case "T":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere la referencia de la transferencia.");
                        break;
                    case "J":
                        if (medio.Referencia is null)
                            throw new Exception($"La forma de pago {forma.Nombre} requiere la autorización o los últimos 4 dígitos de la tarjeta.");
                        break;
                    default:        // 'N': sin dato
                        medio.Referencia = null;
                        medio.Banco = null;
                        break;
                }

                if (forma.TipoDato != "C")
                    medio.FechaCheque = null;
            }
        }

        private static string? Limpiar(string? texto)
        {
            var t = texto?.Trim();
            return string.IsNullOrEmpty(t) ? null : t;
        }
    }
}
```

- [ ] **Step 1: Pruebas del helper (fallan).** `ValidadorMediosPagoTests`: copiar de `PagoRecibidoDomainTests` los casos de formas de pago (sin medios; forma inexistente; inactiva; monto 0 y negativo; más de 2 decimales; cheque sin referencia / sin banco; transferencia sin referencia; tarjeta sin referencia; forma `N` descarta referencia/banco/fecha; `FechaCheque` solo se conserva en `C`) y escribirlos contra `ValidadorMediosPago.ValidarAsync` con una clase de prueba interna `MedioFalso : IMedioPago` y un diccionario de formas. Mensajes literales del bloque de arriba.
- [ ] **Step 2: Interfaz y parciales; helper.** Crear los cuatro archivos de arriba (`IMedioPago.cs` en `API.Domain.Entity/Models`, las dos parciales —mismo namespace que el scaffold— y `ValidadorMediosPago.cs`). Build + las pruebas del helper en verde.
- [ ] **Step 3: Refactor de `PagoRecibidoDomain`.** Reemplazar el cuerpo de `ValidarFormasDePagoAsync` por `await ValidadorMediosPago.ValidarAsync(medios, codigo => _repoForma.ObtenerAsync(codigo));` y eliminar `Limpiar` si queda sin uso (el resto del archivo no cambia; su propio `Redondear` puede quedar). **No** tocar `PagoRecibidoDomainTests`.
- [ ] **Step 4: Regresión.** Correr `PagoRecibidoDomainTests`, `FormaPagoDomainTests`, `PagosPermisosTests` y la suite completa: **todo en verde y el mismo número que antes + las nuevas del helper**.
- [ ] **Step 5: Commit.**

```bash
git add API.Domain.Entity/Models/IMedioPago.cs API.Domain.Entity/Models/PagoRecibidoMedio.IMedioPago.cs API.Domain.Entity/Models/PagoEfectuadoMedio.IMedioPago.cs API.Domain.Core/ValidadorMediosPago.cs API.Domain.Core/PagoRecibidoDomain.cs API.Service.WebApi.Tests/Domain/ValidadorMediosPagoTests.cs
git commit -m "refactor(pagos): la validacion de las formas de pago pasa a un helper comun (ValidadorMediosPago)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 3: `SaldoFacturaCompraService`

**Plantilla:** `API.Domain.Core/SaldoFacturaService.cs`, `API.Domain.Interface/ISaldoFacturaService.cs` y `Domain/SaldoFacturaServiceTests.cs` (commit `899d3bf` + endurecimientos de `d3873b5`).

**Precondición:** Tarea 1 hecha (entidades `PagoEfectuado*`).

**Files:** Create `API.Domain.Interface/ISaldoFacturaCompraService.cs`, `API.Domain.Core/SaldoFacturaCompraService.cs`; Modify `API.Service.WebApi/Startup.cs` (`AddTransient<ISaldoFacturaCompraService, SaldoFacturaCompraService>()`); Test `Domain/SaldoFacturaCompraServiceTests.cs`.

**Interfaces — Produces:**

```csharp
namespace API.Domain.Interface
{
    /// <summary>Saldo de una factura de compra o nota de débito de proveedor: total − notas de crédito activas − pagos activos.</summary>
    public sealed record SaldoFacturaCompra(int FacturaCompraEntry, decimal Total, decimal Acreditado, decimal Pagado, decimal Saldo, string Estado);

    public interface ISaldoFacturaCompraService
    {
        Task<IReadOnlyDictionary<int, SaldoFacturaCompra>> ObtenerAsync(IEnumerable<int> facturaCompraEntries);
        Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaCompraEntry);   // PagoAplicado ya existe en ISaldoFacturaService.cs
        Task<bool> TienePagosRegistradosAsync(int facturaCompraEntry);
    }
}
```

Se **reutilizan** `PagoAplicado(PagoEntry, PagoNumDoc, Monto)` y las constantes `EstadoPagoFactura.{Pendiente,Parcial,Pagada}` ya definidas en `ISaldoFacturaService.cs`; no se redefinen.

- [ ] **Step 1:** copiar `SaldoFacturaServiceTests` a `SaldoFacturaCompraServiceTests` aplicando la tabla (`NotaCreditoCompra`, `BaseTipo = 13`, `PagoEfectuado*`, `FacturaCompra`); casos: sin pagos; pago parcial; pago total; nota de crédito; pago cancelado no cuenta; nota cancelada no cuenta; redondeo; varios documentos en una consulta; `ObtenerPagosActivosAsync` (excluye cancelados, ordena por número); `TienePagosRegistradosAsync` (incluye cancelados); entradas vacías. Deben fallar por compilación.
- [ ] **Step 2:** implementar el servicio copiando `SaldoFacturaService` con la tabla; **tres consultas agrupadas** y mismas reglas de redondeo/estado.
- [ ] **Step 3:** build + pruebas del servicio en verde.
- [ ] **Step 4: Commit** `feat(pagos-efectuados): servicio de saldo y estado de pago de facturas de compra`.

---

## Fase 2 — Documento de pago y aplicación (API)

### Task 4: `PagoEfectuadoDomain` (alta, cancelación y consultas)

**Plantilla:** `API.Domain.Core/PagoRecibidoDomain.cs` + `API.Domain.Interface/IPagoRecibidoDomain.cs` + `Domain/PagoRecibidoDomainTests.cs` (commits `00c6792` y `d3873b5`).

**Precondición:** Tareas 1–3 hechas.

**Files:** Create `API.Domain.Interface/IPagoEfectuadoDomain.cs`, `API.Domain.Core/PagoEfectuadoDomain.cs`; Modify `Startup.cs` (`AddTransient<IPagoEfectuadoDomain, PagoEfectuadoDomain>()`); Test `Domain/PagoEfectuadoDomainTests.cs`.

**Interfaces — Consumes:** `ValidadorMediosPago.ValidarAsync` (Tarea 2), `ISaldoFacturaCompraService` (Tarea 3), repos de la Tarea 1, `IRepositorioGenerico<FormaPago,string>`, `IRepositorioGenerico<FacturaCompra,int>`, `IRepositorioGenerico<SocioNegocio,string>`, `IRepositorioGenerico<NumeracionDocumentoDet,int>`, `IEjecutorTransaccion`.
**Produces:** `IPagoEfectuadoDomain` con las mismas firmas que `IPagoRecibidoDomain` traducidas: `InsertarAsync(PagoEfectuado, IEnumerable<PagoEfectuadoFactura>, IEnumerable<PagoEfectuadoMedio>) → Task<int>`, `CancelarAsync(int) → Task<bool>`, `ObtenerAsync(int)`, `ObtenerTodosAsync()`, `ObtenerFacturasAsync(int)`, `ObtenerMediosAsync(int)`, `ObtenerAbiertosAsync(string codigoSn)` (documentos con saldo > 0 del proveedor; devuelve el tipo `DocumentoPorPagar` análogo a `DocumentoPorCobrar`).

- [ ] **Step 1: Pruebas (fallan).** Copiar `PagoRecibidoDomainTests` → `PagoEfectuadoDomainTests` aplicando la tabla. **No** copiar las pruebas de formas de pago ya cubiertas por `ValidadorMediosPagoTests`, salvo una prueba por cada rama que demuestre que el dominio **invoca** al helper (medios vacíos → `"Indique al menos una forma de pago."`; cheque sin banco → `requiere el banco`). Casos: pago feliz (cabecera con `TipoObjeto "46"`, `TotalPago`, numeración); proveedor inexistente / no proveedor (`"El socio de negocio debe ser un proveedor."`) / inactivo permitido; documento de otro proveedor (`pertenece a otro socio de negocio.`), cancelado (`está cancelada.`), repetido, inexistente; monto cero, negativo, más de 2 decimales, mayor que el saldo (`excede su saldo`); descuadre; serie inexistente / de otro objeto / bloqueada / sin siguiente número; documento cancelado dentro de la transacción; nota de débito de proveedor pagable; `CancelarAsync` (feliz, inexistente → false, ya cancelado → `"El pago ya está cancelado."`); `ObtenerAbiertosAsync` (solo saldo > 0, excluye cancelados y de otros proveedores).
- [ ] **Step 2: Implementar** copiando `PagoRecibidoDomain` con la tabla; reemplazar el cuerpo de la validación de formas por la llamada a `ValidadorMediosPago`; **conservar** el orden de validación y la transacción `Serializable` (`_tx.EjecutarAsync(..., IsolationLevel.Serializable)`), relectura de estados dentro de la transacción, `AsignarNumero`, Save #1 de cabecera y `NoLinea` consecutivos.
- [ ] **Step 3:** build + pruebas del dominio en verde.
- [ ] **Step 4: Commit** `feat(pagos-efectuados): dominio de pagos efectuados con saldo validado en transaccion serializable`.

### Task 5: Aplicación, DTOs, controlador, mapeo y permisos de pagos efectuados

**Plantilla:** commit `549223f` (DTOs `pagoRecibido/*`, `IPagoRecibidoApplication`, `PagoRecibidoApplication`, `PagoRecibidoController`, `PerfilMapeo`, pruebas `PagoRecibidoApplicationTests`, `PagoRecibidoControllerTests`, `PagosPermisosTests`) + longitudes de `d3873b5` (`PagosDtoLongitudesTests`).

**Precondición:** Tarea 4 hecha.

**Files:** Create `API.Application.DTO/pagoEfectuado/{DocumentoPorPagarDTO,PagoEfectuadoCrearDTO,PagoEfectuadoDTO,PagoEfectuadoFacturaCrearDTO,PagoEfectuadoFacturaDTO,PagoEfectuadoMedioCrearDTO,PagoEfectuadoMedioDTO}.cs`, `API.Application.Interface/IPagoEfectuadoApplication.cs`, `API.Application.Main/PagoEfectuadoApplication.cs`, `API.Service.WebApi/Controllers/PagoEfectuadoController.cs`; Modify `API.Transversal.Mapper/PerfilMapeo.cs`, `Startup.cs`; Tests `Application/PagoEfectuadoApplicationTests.cs`, `Application/PagosEfectuadosDtoLongitudesTests.cs`, `Controllers/PagoEfectuadoControllerTests.cs`, `Controllers/PagosEfectuadosPermisosTests.cs`.

**Rutas y permisos (spec §3):** `GET api/PagoEfectuado` (Ver `PagosEfectuados`), `GET {entry}`, `GET {entry}/Facturas`, `GET {entry}/Medios` (Ver), `POST` (Agregar), `POST {entry}/Cancelar` (Eliminar), `GET FacturasAbiertas/{codigoSn}` (Agregar). Decorar con `[RequierePermiso("PagosEfectuados", Accion.X)]`. DTO de creación: `PagoEfectuadoCrearDTO { Serie, FechaDoc, CodigoSn, Comentario, Facturas[{FacturaCompraEntry, MontoAplicado}], Medios[{CodFormaPago, Monto, Referencia, Banco, FechaCheque}] }` con los mismos `[Required]`/`[StringLength]` que cobros (las pruebas de longitudes se copian y traducen).

- [ ] **Step 1:** copiar y traducir las cuatro clases de pruebas (fallan por compilación).
- [ ] **Step 2:** crear DTOs, aplicación, controlador y entradas de `PerfilMapeo` (mismo patrón; `PerfilMapeo` agrega los mapas `PagoEfectuado↔PagoEfectuadoDTO`, `PagoEfectuadoFactura↔PagoEfectuadoFacturaDTO`, `PagoEfectuadoMedio↔PagoEfectuadoMedioDTO`, crear→entidad) y DI de `IPagoEfectuadoApplication`.
- [ ] **Step 3:** pruebas de la tarea en verde (aplicación 200/400, controladores 403/400/404/200, longitudes, permisos: cada acción exige su permiso y el módulo correcto).
- [ ] **Step 4: Commit** `feat(pagos-efectuados): aplicacion, DTOs, controlador y permisos de pagos efectuados`.

---

## Fase 3 — Guardas sobre documentos existentes, saldos y script completo (API)

### Task 6: Guarda de pagos al cancelar o eliminar facturas de compra y notas de débito de proveedor

**Plantilla:** commit `722823f` (`GuardaPagos`, cambios en `FacturaDomain`/`NotaDebitoDomain`, `GuardaPagosTests`) y `d3873b5` para `ValidarSinHistorialAsync`.

**Precondición:** Tarea 3 hecha.

**Files:** Create `API.Domain.Core/GuardaPagosEfectuados.cs`; Modify `API.Domain.Core/FacturaCompraDomain.cs`, `API.Domain.Core/NotaDebitoCompraDomain.cs`; Tests `Domain/GuardaPagosEfectuadosTests.cs`, `Domain/FacturaCompraDomainTests.cs`, `Domain/NotaDebitoCompraDomainTests.cs`.

**Código nuevo:**

```csharp
using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>
    /// Regla compartida por factura de compra y nota de débito de proveedor: un documento con pagos activos no se
    /// cancela ni se elimina; primero hay que cancelar el pago.
    /// </summary>
    public static class GuardaPagosEfectuados
    {
        public static async Task ValidarSinPagosAsync(ISaldoFacturaCompraService saldos, int facturaCompraEntry)
        {
            var pagos = await saldos.ObtenerPagosActivosAsync(facturaCompraEntry);
            if (pagos.Count == 0)
                return;

            var etiquetas = string.Join(", ", pagos.Select(p => $"Pago {p.PagoNumDoc}"));
            throw new Exception($"El documento tiene pagos aplicados ({etiquetas}). Cancele primero el pago.");
        }

        /// <summary>Un documento que figura en algún pago (aunque esté cancelado) conserva su historial: no se elimina.</summary>
        public static async Task ValidarSinHistorialAsync(ISaldoFacturaCompraService saldos, int facturaCompraEntry)
        {
            if (await saldos.TienePagosRegistradosAsync(facturaCompraEntry))
                throw new Exception("El documento tiene pagos registrados (cancelados) y no se puede eliminar; consérvelo cancelado.");
        }
    }
}
```

**Cambios (mismo patrón que `722823f`):** `FacturaCompraDomain` y `NotaDebitoCompraDomain` reciben `ISaldoFacturaCompraService saldos` como **último** parámetro del constructor; en `ActualizarAsync`, dentro de `if (obj.Cancelado == "S")` y **antes** de `_tx.EjecutarAsync`, `await GuardaPagosEfectuados.ValidarSinPagosAsync(_saldos, id);`; en `EliminarAsync`, tras comprobar que el documento existe, `ValidarSinPagosAsync` y luego `ValidarSinHistorialAsync`. Actualizar todos los `new FacturaCompraDomain(...)`/`new NotaDebitoCompraDomain(...)` de las pruebas existentes (agregando un `Mock<ISaldoFacturaCompraService>` por defecto sin pagos) y la DI no requiere cambio (resuelve por constructor).

- [ ] **Step 1:** pruebas (fallan): `GuardaPagosEfectuadosTests` (sin pagos pasa; con pagos lanza el mensaje con `Pago 3, Pago 5`; historial lanza el mensaje de cancelados); en `FacturaCompraDomainTests` y `NotaDebitoCompraDomainTests`: cancelar con pagos → excepción y **sin** `_asiento.RevertirAsync` ni cambio; cancelar sin pagos → igual que hoy; eliminar con pagos activos / con historial → excepción; eliminar sin pagos → igual que hoy.
- [ ] **Step 2:** implementar y ajustar constructores y pruebas existentes. Build + pruebas de ambos dominios y la guarda en verde.
- [ ] **Step 3: Commit** `feat(pagos-efectuados): una factura de compra o nota de debito de proveedor con pagos activos no se cancela ni se elimina`.

### Task 7: El límite de la nota de crédito de proveedor descuenta los pagos aplicados

**Plantilla:** commit `fbc2057` (`NotaCreditoDomain`, `FacturaBaseCredito`, DTOs de factura base, `NotaCreditoApplication`).

**Precondición:** Tarea 3 hecha.

**Files:** Modify `API.Domain.Interface/FacturaBaseCredito.cs`, `API.Domain.Core/NotaCreditoCompraDomain.cs`, `API.Application.Main/NotaCreditoCompraApplication.cs`, `API.Application.DTO/notaCreditoCompra/` (los dos DTOs de factura base y su resumen); Tests `Domain/NotaCreditoCompraDomainTests.cs`, `Application/NotaCreditoCompraApplicationTests.cs`.

**Código:**
- `FacturaBaseCredito.cs`: `FacturaCompraAcreditable(FacturaCompra Documento, decimal TotalAcreditado, decimal TotalPagado = 0m)` y `FacturaCompraBaseCredito(FacturaCompra Documento, IReadOnlyList<FacturaCompraDetalle> Lineas, decimal TotalAcreditado, decimal TotalPagado = 0m)`.
- `NotaCreditoCompraDomain`: constructor + `ISaldoFacturaCompraService saldos` (último parámetro); constante `MsgExcesoPagada = "Las notas de crédito ({0}) exceden lo que queda por acreditar de la factura de compra ({1} = total menos pagos aplicados). Cancele primero los pagos si necesita acreditarla por completo."`; `ObtenerPagadoAsync(int facturaEntry)` = `saldos.ObtenerAsync(new[]{facturaEntry})` → `Pagado` o 0; en `ObtenerFacturasBaseAsync` poblar `TotalPagado` con un solo `saldos.ObtenerAsync(entries)`; en `ObtenerFacturaBaseAsync` pasar `pagado`; en `ValidarLimiteAcreditableAsync`, **antes** de la comparación actual: si `pagado > 0` → `limite = Redondear(totalFactura − pagado)`, y si `totalConEstaNota > limite` lanzar `MsgExcesoPagada` (con `"0.00"` invariante) y `return` en otro caso; sin pagos se conserva el mensaje actual `MsgExceso`.
- `NotaCreditoCompraApplication` y DTOs: `TotalPagado` y `Disponible = max(0, total − acreditado − pagado)` igual que en `fbc2057`.

- [ ] **Step 1:** pruebas (fallan): exceder con pagos (mensaje nuevo literal); justo en el límite con pagos pasa; sin pagos conserva `MsgExceso`; `ObtenerFacturasBaseAsync` trae `TotalPagado`; `Disponible` descuenta pagos (aplicación).
- [ ] **Step 2:** implementar; actualizar `new NotaCreditoCompraDomain(...)` en las pruebas existentes con un mock por defecto.
- [ ] **Step 3:** build + pruebas en verde.
- [ ] **Step 4: Commit** `feat(pagos-efectuados): el limite acreditable de una nota de credito de proveedor descuenta los pagos aplicados`.

### Task 8: Endpoints de saldos de facturas de compra y notas de débito de proveedor

**Plantilla:** commit `9e0ca4d` (`SaldoFacturaDTO`, `ISaldoFacturaApplication`, `SaldoFacturaApplication`, `SaldoFacturaController`, `SaldoFacturaControllerTests`).

**Precondición:** Tarea 3 hecha.

**Files:** Create `API.Application.DTO/pagoEfectuado/SaldoFacturaCompraDTO.cs`, `API.Application.Interface/ISaldoFacturaCompraApplication.cs`, `API.Application.Main/SaldoFacturaCompraApplication.cs`, `API.Service.WebApi/Controllers/SaldoFacturaCompraController.cs`; Modify `Startup.cs`, `PerfilMapeo.cs` (si el patrón lo requiere); Test `Controllers/SaldoFacturaCompraControllerTests.cs`.

**Rutas:** `POST api/FacturaCompra/Saldos` (Ver `FacturasCompra`) y `POST api/NotaDebitoCompra/Saldos` (Ver `NotasDebitoCompra`), cuerpo `[1,2,3]`, respuesta `Respuesta<List<SaldoFacturaCompraDTO>>` con `FacturaCompraEntry, Total, Acreditado, Pagado, Saldo, Estado`. Controlador con rutas absolutas por acción (`[HttpPost("~/api/FacturaCompra/Saldos")]`), igual que `SaldoFacturaController`.

- [ ] **Step 1:** copiar/traducir `SaldoFacturaControllerTests` (403 sin permiso por cada ruta; 200 con lista; cuerpo vacío → lista vacía).
- [ ] **Step 2:** implementar.
- [ ] **Step 3:** pruebas en verde.
- [ ] **Step 4: Commit** `feat(pagos-efectuados): endpoints de saldo y estado de pago para facturas de compra y notas de debito de proveedor`.

### Task 9: Script completo, suite de la API y verificación de SQL real

**Precondición:** Tareas 1–8 hechas.

**Files:** Modify `sql/crear-api-db-test-completo.sql`.

- [ ] **Step 1:** agregar al final del script completo el bloque de `sql/2026-10-25-pagos-efectuados.sql` (sin la comprobación `THROW` de `FormaPago`, que ya está creada antes en el script completo), con el mismo comentario de cabecera de sección que usó el bloque de cobros.
- [ ] **Step 2:** crear una base temporal `API_DB_PAGOS_EF2` solo con `crear-api-db-test-completo.sql` (reemplazando `API_DB_TEST`) y comprobar: existen las tres tablas, objeto `46`, módulo `PagosEfectuados`, `FormaPago` con 4 semillas; correr el script individual encima (idempotencia) sin error. Eliminar la base.
- [ ] **Step 3:** suite completa. **Esperado: 1946 + las pruebas nuevas, todas en verde, 0 con error** (anotar el total exacto).
- [ ] **Step 4: Commit** `docs(db): script completo de creacion de API_DB_TEST incluye pagos efectuados`.

---

## Fase 4 — Web: clientes tipados, pagos efectuados y saldos en los listados

### Task 10: DTOs y clientes tipados de la Web

**Plantilla:** commit `c8a3db5` (parte de `PagoRecibido*`, `SaldoFactura*`, `Program.cs`, y los campos agregados a `FacturaDTO`/`NotaDebitoDTO`).

**Precondición:** API Tareas 1–9 hechas; en la Web, `git status --short` solo con los 9 ajenos.

**Files:** Create `Web.ApiClient/Dtos/PagoEfectuado/*`, `Web.ApiClient/Dtos/SaldoFacturaCompra/SaldoFacturaCompraDTO.cs`, `Web.ApiClient/Clientes/{IPagoEfectuadoApiClient,PagoEfectuadoApiClient,ISaldoFacturaCompraApiClient,SaldoFacturaCompraApiClient}.cs`; Modify `Web.ApiClient/Dtos/FacturaCompra/FacturaCompraDTO.cs` y `Web.ApiClient/Dtos/NotaDebitoCompra/NotaDebitoCompraDTO.cs` (agregar `Pagado`, `Saldo`, `EstadoPago` nulables como en `FacturaDTO`), `Web.UI/Program.cs` (registro de los clientes).

- [ ] **Step 1:** copiar/traducir los DTOs y clientes (rutas `api/PagoEfectuado`, `api/FacturaCompra/Saldos`, `api/NotaDebitoCompra/Saldos`).
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores (los 9 archivos ajenos intactos).
- [ ] **Step 3: Commit** (rutas explícitas) `feat(pagos-efectuados): DTOs y clientes tipados de pagos efectuados y saldos de compras`.

### Task 11: Listado de pagos efectuados, detalle, cancelación y menú

**Plantilla:** commit `a5a9845` (`PagosRecibidosController` Index/Obtener/Cancelar, `Views/PagosRecibidos/{Index,_Detalle}.cshtml`, `pagosrecibidos.js`, entrada del `_Layout`) y la parte de `site.js` con la etiqueta del objeto 24.

**Files:** Create `Web.UI/Controllers/PagosEfectuadosController.cs`, `Web.UI/Views/PagosEfectuados/{Index,_Detalle}.cshtml`, `Web.UI/wwwroot/js/pagosefectuados.js`; Modify `Web.UI/Views/Shared/_Layout.cshtml` (entrada "Pagos efectuados" en el grupo Compras, visible con Ver en `PagosEfectuados`, `EsActivoCompras` y `MuestraCompras` incluyen el controlador) y `Web.UI/wwwroot/js/site.js` (etiqueta de objeto `46`: "Pago efectuado").

- [ ] **Step 1:** copiar/traducir; atributos `[RequierePermiso("PagosEfectuados", Accion.X)]`, `[ValidateAntiForgeryToken]` en POST, texto de usuario escapado al pintar tablas.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** `feat(pagos-efectuados): listado, detalle y cancelacion de pagos efectuados`.

### Task 12: Alta de un pago efectuado (página estilo SAP)

**Plantilla:** commits `e4e59a2` y `42199b9` (`PagosRecibidosController.Crear`, `Views/PagosRecibidos/Crear.cshtml`, `pagosrecibidos-crear.js` con la limpieza de estado al cambiar de cliente y el bloqueo del doble envío).

**Files:** Modify `Web.UI/Controllers/PagosEfectuadosController.cs`; Create `Web.UI/Views/PagosEfectuados/Crear.cshtml`, `Web.UI/wwwroot/js/pagosefectuados-crear.js`.

- [ ] **Step 1:** copiar/traducir: proveedor (buscador filtrado a tipo `P`), documentos con saldo, medios, barra de cuadre, `?facturaCompraEntry=&codigoSn=` para precarga, avisos de degradación sin series/formas.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** `feat(pagos-efectuados): pagina de alta de pagos efectuados con documentos, formas de pago y cuadre`.

### Task 13: Saldo, estado de pago y botón "Registrar pago" en Facturas de compra y Notas de débito de proveedor

**Plantilla:** commits `20ff48d` (columnas Pagado/Saldo/Pago + botón en `FacturasController`/`NotasDebitoController`, vistas `Index` y `facturas.js`/`notasdebito.js`) y `240a2a8` (escape del nombre en el listado).

**Files:** Modify `Web.UI/Controllers/FacturasCompraController.cs`, `Web.UI/Controllers/NotasDebitoCompraController.cs`, `Web.UI/Views/FacturasCompra/Index.cshtml`, `Web.UI/Views/NotasDebitoCompra/Index.cshtml`, `Web.UI/wwwroot/js/facturascompra.js`, `Web.UI/wwwroot/js/notasdebitocompra.js`.

- [ ] **Step 1:** `ObtenerTodos` pide `Saldos` del conjunto y los fusiona en la fila; columnas **Pagado**, **Saldo** y la insignia **Pendiente/Parcial/Pagada**; botón "Registrar pago" (clase `btn-outline-success`, `title="Registrar pago"`, enlace a `/PagosEfectuados/Crear?codigoSn=…&facturaCompraEntry=…`) para quien tenga Agregar en `PagosEfectuados` y saldo > 0. **Escapar** el nombre del socio al pintar (`escapar` solo en `display`), como en `240a2a8`.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** `feat(pagos-efectuados): saldo, estado de pago y boton Registrar pago en facturas de compra y notas de debito de proveedor`.

---

## Fase 5 — Verificación integral

### Task 14: Prueba de humo contra SQL Server real y recorrido en navegador

**Plantilla:** `scratchpad\smoke-pagos.ps1` (54 comprobaciones de cobros) y `evid-up.ps1`/`evid-down.ps1`.

**Precondición:** Tareas 1–13 hechas y confirmadas; Web y API compilan.

**Files:** Create (scratchpad, no se commitea al repo) `smoke-pagos-efectuados.ps1`; copiar el guion a la carpeta de evidencia si existe.

- [ ] **Step 1:** levantar `evid-up.ps1 -NombreDb API_DB_EVID6` (API :5199 y Web :5200).
- [ ] **Step 2: Guion `smoke-pagos-efectuados.ps1`** (patrón `Esperar`), con las comprobaciones del spec §5: login y 401 sin token; proveedor y facturas de compra de prueba (compra con inventario previo, nota de débito de proveedor, nota de crédito); saldos iniciales (`Pendiente`, saldo = total); pago parcial → `Parcial`; pago mixto (efectivo + cheque con referencia/banco) → `Pagada`; sobrepago rechazado `excede su saldo`; cheque sin referencia / sin banco, descuadre, documento repetido, de otro proveedor, socio que no es proveedor → 400 con el mensaje; ningún rechazo consume el consecutivo de la serie 46; cancelar factura de compra con pago → 400; nota de crédito de proveedor sobre factura pagada → 400 y aceptada tras cancelar el pago; cancelar el pago restituye el saldo; nota de débito de proveedor pagable; proveedor inactivo aún puede cobrarse; **dos pagos simultáneos** al mismo documento (exactamente uno 200 y otro 400 `excede su saldo`) y tres rondas de cuatro pagos simultáneos (exactamente 1 OK); pago cancelado no se vuelve a cancelar.
- [ ] **Step 3:** correr el guion: **todas las comprobaciones OK, 0 FALLA**; guardar la salida.
- [ ] **Step 4: Recorrido en navegador** (puppeteer del scratchpad, `shots\common.js`): Facturas de compra con columnas Pagado/Saldo/Pago y botón "Registrar pago"; Pagos efectuados (listado con un pago vigente y uno cancelado); alta con precarga desde el botón y medio mixto; detalle y cancelación. Verificar además que el menú Compras muestra "Pagos efectuados" y que **sin consola de errores**. Capturas reales a `scratchpad\docfigs\fig-pagos-efectuados-*.png`.
- [ ] **Step 5:** regresión de cobros: correr `smoke-pagos.ps1` contra el mismo entorno (54 OK) para comprobar que el refactor del helper no rompió Pagos recibidos.
- [ ] **Step 6:** limpiar: `evid-down.ps1 -NombreDb API_DB_EVID6 -Todo`; `git status` en ambos repos solo con los archivos esperados; **sin push**.

## Notas de despliegue (para quien integra, fuera de las tareas)

- Ejecutar `sql/2026-10-25-pagos-efectuados.sql` en `API_DB_TEST` (idempotente; requiere que ya exista `FormaPago`) y asignar el módulo `PagosEfectuados` a las plantillas de permisos; quien registre pagos necesita además Ver en `FacturasCompra`, `SociosNegocio`, `FormasPago` y `NumeracionDocumento`.
- Sin migración de datos: las facturas de compra existentes quedan con saldo = total.
- La Fase B (vencimientos, estado de cuenta, antigüedad) y la Fase C (límite de crédito) se diseñan después de cerrar esta fase.
