# Condiciones de pago, vencimientos, estado de cuenta y antigüedad de saldos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar condiciones de pago y fecha de vencimiento a las facturas (de venta y de compra) y sus notas de débito, el saldo y estado de cuenta de cada socio, y la antigüedad de saldos por cobrar y por pagar (Fase B de cuentas por cobrar / por pagar).

**Architecture:** Catálogo `CondicionPago`; el socio tiene una condición por defecto y los documentos guardan `CondicionPago` y `FechaVencimiento` (calculada en el servidor con un `ResolutorVencimiento`). Tres servicios de solo lectura (`SaldoSocioService`, `EstadoCuentaService`, `AntiguedadService`) calculan saldos y reportes al leer, apoyándose en los servicios de saldo de la Fase A. La Web agrega el catálogo, la condición en socios y documentos, columnas Vence/Vencida, la pantalla de estado de cuenta y las dos pantallas de antigüedad.

**Tech Stack:** .NET 7 API (N-capas, EF Core 7 DB-first, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server (solo bases temporales), PowerShell/node para verificación.

**Spec:** `docs/superpowers/specs/2026-10-26-vencimientos-estado-cuenta-antiguedad-design.md` (repo API, commit `97b9d9f`).

**Cómo usar este plan (importante):** hay módulos ya construidos y verificados que son la **plantilla autoritativa** (Formas de pago para el catálogo; Pagos recibidos/efectuados para servicios de saldo, controladores y pantallas). Cada tarea nombra su plantilla, la lista exacta de archivos, el código **nuevo** completo y las reglas. Quien implementa abre la plantilla real del repositorio y la adapta; no reescribe de memoria. Los mensajes indicados se verifican literalmente en pruebas.

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. Antes de empezar: `git log --oneline -1` muestra `97b9d9f` (spec) o posterior; `git status --short` solo con `API.Service.WebApi/appsettings.json` modificado. Línea base de pruebas: **2084** pasando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `f8eab6e` (o posterior). `git status --short` muestra **9 archivos ajenos** sin commit que NO se tocan ni se stagean: `Web.ApiClient/Dtos/Almacen/AlmacenActualizarDTO.cs`, `Web.ApiClient/Dtos/Almacen/AlmacenCrearDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloActualizarDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioActualizarDTO.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioCrearDTO.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`. **Atención:** dos de esos archivos ajenos son DTOs de socio de negocio; la Tarea 14 necesita agregar `CondicionPago` al DTO de socio de la Web: ver la nota de esa tarea para hacerlo sin stagear los cambios ajenos.
- Build/test con `BaseOutputPath` en el scratchpad (ejecutar **desde PowerShell**):
  - API (suite): `dotnet build API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"` y luego `dotnet test "C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\Debug\net7.0\API.Service.WebApi.Tests.dll"` (con `--filter "FullyQualifiedName~Clase"` para una clase).
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`
- SQL de pruebas: SOLO bases temporales (`API_DB_VENC_*` o `API_DB_EVID<n>` vía `evid-up.ps1`/`evid-down.ps1` del scratchpad); **jamás** `API_DB_TEST`. La contraseña de `sa` se extrae de `API.Service.WebApi\appsettings.json` a una variable de proceso, sin imprimirla ni escribirla. Las bases temporales se eliminan al terminar.
- Subtipos: nota de débito de **cliente** `ND` (tabla `Factura`), nota de débito de **proveedor** `DN` (tabla `FacturaCompra`).

## Global Constraints

Cada tarea las incluye implícitamente.

- **Condiciones de pago:** `CondicionPago(Codigo nvarchar(10) PK, Nombre nvarchar(50), Dias int CHECK 0..365, Activo 'S'/'N')`. Semillas `CON` Contado (0), `C15` Crédito 15 días (15), `C30` Crédito 30 días (30), `C45` Crédito 45 días (45), `C60` Crédito 60 días (60). `CON` no se puede dar de baja. `Codigo` inmutable. Baja lógica (`DELETE` → `Activo='N'`). No se puede **asignar** una condición inactiva a un socio ni a un documento nuevo.
- **Resolución al crear un documento:** `condicion = condicionPedida ?? socio.CondicionPago ?? "CON"`; debe existir (`"La condición de pago {c} no existe."`) y estar activa (`"La condición de pago {c} está inactiva."`); `FechaVencimiento = FechaDoc.Date + Dias días` (si `FechaDoc` viene vacío se usa la fecha de hoy solo para este cálculo). Se resuelve **antes de numerar**: un error no consume el consecutivo. El cliente no puede fijar `FechaVencimiento` (se ignora). Aplica a `Factura` (`--`, `ND`) y `FacturaCompra` (`--`, `DN`); **no** a cotización, pedido, entrega, orden de servicio ni notas de crédito. Actualizar un documento no recalcula nada.
- **Migración de históricos:** `CondicionPago='CON'` y `FechaVencimiento=FechaDoc` para toda fila existente sin vencimiento; columnas nulables; lectura con `COALESCE(FechaVencimiento, FechaDoc)`.
- **Saldo del socio:** suma de `Saldo` de sus documentos abiertos (no cancelados), con `ISaldoFacturaService` (cliente: facturas y notas de débito `ND`) o `ISaldoFacturaCompraService` (proveedor: facturas de compra y `DN`); `Vencido` = suma de saldos con `COALESCE(FechaVencimiento,FechaDoc) < hoy`. Consultas agrupadas, no una por socio.
- **Estado de cuenta:** cargos = facturas y notas de débito; abonos = notas de crédito **con factura base** (`BaseTipo` 6 en ventas, 13 en compras) y pagos (recibidos o efectuados); solo no cancelados; las notas de crédito sin factura base van `Informativo=true` y no afectan el acumulado; `SaldoInicial` = cargos − abonos con fecha anterior a `desde`; orden por fecha, luego tipo y número; `SaldoFinal` al `hasta`; `SaldoActual` y `Vencido` de `ISaldoSocioService`. **Invariante:** con `hasta` ≥ hoy y `desde` en el pasado remoto, `SaldoFinal == SaldoActual`. `desde > hasta` → `"La fecha inicial no puede ser posterior a la final."`. Rango por defecto: 1 de enero del año en curso a hoy.
- **Antigüedad:** al día de hoy; `DiasVencido = (hoy − vencimiento).Days`; `≤ 0` Vigente, `1-30`, `31-60`, `61-90`, `> 90`; solo documentos con `Saldo > 0` y no cancelados; socios sin saldo no aparecen. Tipo `"C"` (cobrar: `Factura`) y `"P"` (pagar: `FacturaCompra`).
- **Módulos de permisos** (acciones Ver/Agregar/Actualizar/Eliminar, acceso denegado por defecto): `CondicionesPago` y `EstadoCuenta` (grupo "Socios de negocio"), `AntiguedadCobrar` (grupo "Ventas"), `AntiguedadPagar` (grupo "Compras").
- **Mensajes en español exactamente como en este plan** (las pruebas los verifican).
- **Compatibilidad:** los alta de documentos sin `CondicionPago` siguen funcionando (usa la del socio o `CON`); `Importar-FacturasCompra.ps1` no cambia.
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` NUNCA se commitea. Los 9 archivos ajenos de la Web NUNCA se stagean ni se modifican. **Sin `git push`.**
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (`file`), archivos nuevos en LF, sin churn de EOL. Comentarios y mensajes en español, estilo del repo.
- **Verificación al cerrar cada tarea de la API:** `dotnet build` sin errores y las pruebas de la tarea en verde; la suite completa en las tareas que lo indican.

## File Structure

**API:** `sql/2026-10-26-condiciones-pago-vencimientos.sql`; entidades `CondicionPago` (+ columnas en `SocioNegocio`, `Factura`, `FacturaCompra`) y `ApiDbTestContext`; repositorio `CondicionPagoRepositorio`; dominio `CondicionPagoDomain`, `ResolutorVencimiento`, `SaldoSocioService`, `EstadoCuentaService`, `AntiguedadService` (+ interfaces en `API.Domain.Interface`); aplicación y DTOs `condicionPago/*`, `estadoCuenta/*`, `antiguedad/*`; controladores `CondicionPagoController`, `EstadoCuentaController`, `AntiguedadController`; cambios en `FacturaDomain`, `NotaDebitoDomain`, `FacturaCompraDomain`, `NotaDebitoCompraDomain`, `SocioNegocioDomain`, DTOs de factura/nota/socio, `PerfilMapeo`, `Startup`.

**Web:** `Web.ApiClient` (DTOs y clientes de condiciones, estado de cuenta y antigüedad; campos nuevos en DTOs de documentos y socio); `CondicionesPagoController` + vistas + `condicionespago.js`; `EstadoCuentaController` + vistas + `estadocuenta.js`; `AntiguedadController` + vistas + `antiguedad.js`; cambios en socios (`_Form`, `Crear`, `Index`, `sociosNegocio.js`), en los cuatro formularios y listados de documentos y en `_Layout.cshtml`.

---

## Fase 1 — B1: condiciones de pago y vencimientos (API)

### Task 1: Script SQL, entidades EF y repositorio

**Plantilla:** commit `a50b97d` (`git show a50b97d`) y `43da9e1` (pagos efectuados): estructura del script idempotente, entidades parciales, bloque de `ApiDbTestContext`, repositorio, línea de DI, prueba de modelo.

**Files:** Create `sql/2026-10-26-condiciones-pago-vencimientos.sql`, `API.Domain.Entity/Models/CondicionPago.cs`, `API.Infraestructure.Repository/CondicionPagoRepositorio.cs`, `API.Service.WebApi.Tests/Domain/ModeloCondicionesPagoTests.cs`; Modify `API.Domain.Entity/Models/{SocioNegocio,Factura,FacturaCompra,ApiDbTestContext}.cs`, `API.Service.WebApi/Startup.cs`.

**Interfaces — Produces:** `CondicionPago { string Codigo; string Nombre; int Dias; string Activo }`; propiedades nuevas `string? CondicionPago` en `SocioNegocio`, `Factura`, `FacturaCompra` y `DateTime? FechaVencimiento` en `Factura`, `FacturaCompra` (con navegación `CondicionPagoNavigation`); `IRepositorioGenerico<CondicionPago, string>` registrado.

- [ ] **Step 1: Prueba de modelo (falla).** `ModeloCondicionesPagoTests` (patrón `ModeloPagosEfectuadosTests`): tabla `CondicionPago` con clave `Codigo`, longitud de `Nombre` 50, `Dias` int, `Activo` con default `'S'`; las tres tablas con columna `CondicionPago`/`FechaVencimiento` y la FK a `CondicionPago`.
- [ ] **Step 2: Script SQL idempotente** con **estas partes exactas**: (a) tabla `CondicionPago` con `CHECK (Dias BETWEEN 0 AND 365)`, `CHECK (Activo IN ('S','N'))` y las 5 semillas (`IF NOT EXISTS` por código); (b) `ALTER TABLE ... ADD` de `CondicionPago nvarchar(10) NULL` con `FOREIGN KEY` en `SocioNegocio`, `Factura` y `FacturaCompra`, y `FechaVencimiento datetime NULL` en `Factura` y `FacturaCompra`, cada uno protegido con `COL_LENGTH(...) IS NULL`; (c) migración: `UPDATE dbo.Factura SET CondicionPago='CON', FechaVencimiento=FechaDoc WHERE FechaVencimiento IS NULL` y el equivalente en `FacturaCompra`; (d) índices `IX_Factura_Vencimiento (FechaVencimiento) INCLUDE (CodigoSN, TotalDoc)` e `IX_FacturaCompra_Vencimiento` análogo, con `IF NOT EXISTS` en `sys.indexes`; (e) cuatro módulos en `Modulo` (`CondicionesPago` "Condiciones de pago" y `EstadoCuenta` "Estado de cuenta" en `Socios de negocio`; `AntiguedadCobrar` "Antigüedad de saldos por cobrar" en `Ventas`; `AntiguedadPagar` "Antigüedad de saldos por pagar" en `Compras`) con `MERGE` **sin** `WHEN NOT MATCHED BY SOURCE`. Debe poder correrse dos veces sin error y con resultados idénticos. Un encabezado `IF OBJECT_ID('dbo.PagoEfectuado') IS NULL THROW 50000, 'Falta la Fase A: ejecute antes sql/2026-10-25-pagos-efectuados.sql', 1;` (los índices de antigüedad no lo requieren, pero el orden de scripts sí).
- [ ] **Step 3: Entidades, contexto, repositorio y DI** (mismo patrón que el commit `43da9e1`; las columnas nuevas en las entidades parciales existentes con la navegación `CondicionPagoNavigation`, configuradas en `ApiDbTestContext` con nombres de FK iguales a los del SQL).
- [ ] **Step 4: Verificar.** Build y `ModeloCondicionesPagoTests` en verde; correr los tests de modelo previos (`ModeloPagos*`) para descartar regresión. Crear una base temporal con `crear-api-db-test-completo.sql` (reemplazando `API_DB_TEST`), ejecutar el script nuevo **dos veces** y comprobar: 5 semillas, columnas en las tres tablas, **todas las facturas existentes con `CondicionPago='CON'` y `FechaVencimiento=FechaDoc`** (insertar antes de la migración una factura mínima si la base está vacía para comprobarlo), 4 módulos. Eliminar la base.
- [ ] **Step 5: Commit.**

```bash
git add sql/2026-10-26-condiciones-pago-vencimientos.sql API.Domain.Entity/Models/CondicionPago.cs API.Domain.Entity/Models/SocioNegocio.cs API.Domain.Entity/Models/Factura.cs API.Domain.Entity/Models/FacturaCompra.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Infraestructure.Repository/CondicionPagoRepositorio.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Domain/ModeloCondicionesPagoTests.cs
git commit -m "feat(vencimientos): script SQL, entidades y repositorio de condiciones de pago y vencimientos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 2: Catálogo de condiciones de pago (dominio, aplicación, controlador, DTOs)

**Plantilla:** commit `1b8b065` y los archivos actuales `FormaPagoDomain`, `IFormaPagoDomain`, `FormaPagoApplication`, `IFormaPagoApplication`, `FormaPagoController`, DTOs `formaPago/*`, pruebas `FormaPagoDomainTests`, `FormaPagoControllerTests`, `PagosDtoLongitudesTests` (parte de formas), entradas de `PerfilMapeo` y DI.

**Files:** Create `API.Domain.Interface/ICondicionPagoDomain.cs`, `API.Domain.Core/CondicionPagoDomain.cs`, `API.Application.DTO/condicionPago/{CondicionPagoDTO,CondicionPagoCrearDTO,CondicionPagoActualizarDTO}.cs`, `API.Application.Interface/ICondicionPagoApplication.cs`, `API.Application.Main/CondicionPagoApplication.cs`, `API.Service.WebApi/Controllers/CondicionPagoController.cs`; Modify `API.Transversal.Mapper/PerfilMapeo.cs`, `Startup.cs`; Tests `Domain/CondicionPagoDomainTests.cs`, `Controllers/CondicionPagoControllerTests.cs`, `Application/CondicionPagoDtoLongitudesTests.cs`.

**Reglas (además de copiar el patrón):** `Codigo` ≤ 10 y `Nombre` ≤ 50 con los mismos `[StringLength]`/`[Required]` de formas de pago; `Dias` entre 0 y 365 (`"Los días de crédito deben estar entre 0 y 365."`); código duplicado → mensaje del patrón; `DELETE` = baja lógica; **la condición `CON` no se puede dar de baja** (`"La condición CON es la predeterminada del sistema y no se puede dar de baja."`); editar `Dias` no recalcula documentos. Rutas y permisos: `GET api/CondicionPago` (Ver `CondicionesPago`), `GET {codigo}` (Ver), `POST` (Agregar), `PUT {codigo}` (Actualizar), `DELETE {codigo}` (Eliminar). **Interfaces — Produces:** `ICondicionPagoDomain` con `ObtenerAsync(string)`, `ObtenerTodoAsync()`, `InsertarAsync`, `ActualizarAsync`, `EliminarAsync` (según la plantilla) y `Task<CondicionPago?> ObtenerActivaAsync(string codigo)` que lanza `"La condición de pago {c} no existe."` / `"La condición de pago {c} está inactiva."` (usado por la Tarea 3).

- [ ] **Step 1:** copiar/traducir las tres clases de pruebas y agregar los casos propios (`Dias` 0, 365, −1, 366; baja de `CON` rechazada; `ObtenerActivaAsync` feliz/inexistente/inactiva). Fallan por compilación.
- [ ] **Step 2:** implementar el catálogo (DTOs, dominio, aplicación, controlador, mapeo, DI).
- [ ] **Step 3:** pruebas de la tarea en verde.
- [ ] **Step 4: Commit** `feat(vencimientos): catalogo de condiciones de pago (dominio, aplicacion, controlador y pruebas)`.

### Task 3: `ResolutorVencimiento` y vencimiento en facturas y notas de débito de cliente

**Plantilla:** commit `722823f` (cómo se inyecta un servicio como último parámetro del constructor de `FacturaDomain`/`NotaDebitoDomain` y se actualizan las pruebas).

**Precondición:** Tareas 1–2 hechas.

**Files:** Create `API.Domain.Core/ResolutorVencimiento.cs`, `API.Service.WebApi.Tests/Domain/ResolutorVencimientoTests.cs`; Modify `API.Domain.Core/{FacturaDomain,NotaDebitoDomain}.cs`, DTOs `API.Application.DTO/factura/{FacturaCrearDTO,FacturaDTO}.cs` y `notaDebito/{NotaDebitoCrearDTO,NotaDebitoDTO}.cs`, `Startup.cs` (DI si hace falta), `API.Service.WebApi.Tests/Domain/{FacturaDomainTests,NotaDebitoDomainTests}.cs`.

**Código nuevo:**

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;

namespace API.Domain.Core
{
    /// <summary>
    /// Resuelve la condición de pago y la fecha de vencimiento de una factura o nota de débito al crearla:
    /// condición pedida, o la del socio, o CON (contado). Compartido por los cuatro dominios de inserción.
    /// </summary>
    public sealed class ResolutorVencimiento : IResolutorVencimiento
    {
        public const string CondicionPorDefecto = "CON";
        private readonly ICondicionPagoDomain _condiciones;

        public ResolutorVencimiento(ICondicionPagoDomain condiciones) => _condiciones = condiciones;

        public async Task<(string Condicion, DateTime Vencimiento)> ResolverAsync(string? condicionPedida, string? condicionDelSocio, DateTime? fechaDoc)
        {
            var codigo = FirstNonEmpty(condicionPedida, condicionDelSocio) ?? CondicionPorDefecto;
            var condicion = await _condiciones.ObtenerActivaAsync(codigo);   // lanza "no existe" / "está inactiva"
            var baseFecha = (fechaDoc ?? DateTime.Today).Date;
            return (condicion!.Codigo, baseFecha.AddDays(condicion.Dias));
        }

        private static string? FirstNonEmpty(params string?[] valores) =>
            valores.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
    }
}
```

`IResolutorVencimiento` (en `API.Domain.Interface`) expone `Task<(string Condicion, DateTime Vencimiento)> ResolverAsync(string?, string?, DateTime?)`; se registra en `Startup`. En `FacturaDomain.InsertarAsync` y `NotaDebitoDomain.InsertarAsync`: leer el socio (los dominios ya validan o leen el socio en nota de débito; en `FacturaDomain` agregar `IRepositorioGenerico<SocioNegocio,string>` al constructor **como último parámetro junto con `IResolutorVencimiento`** si no lo tiene), llamar a `ResolverAsync(obj.CondicionPago, socio?.CondicionPago, obj.FechaDoc)` **antes de numerar** (después de validar serie y líneas, antes de `if (serie.Manual == "S")`) y asignar `obj.CondicionPago` y `obj.FechaVencimiento`. Un socio inexistente no cambia el comportamiento previo (la FK lo rechazaría): si no se encuentra, `condicionDelSocio = null`. `FacturaCrearDTO`/`NotaDebitoCrearDTO` ganan `string? CondicionPago`; `FacturaDTO`/`NotaDebitoDTO` ganan `string? CondicionPago` y `DateTime? FechaVencimiento` (el mapeo AutoMapper por nombre ya cubre ambos sentidos; `FechaVencimiento` **no** se agrega a los DTOs de creación y, si el mapeo de creación la llenara, se ignora con `.ForMember(d => d.FechaVencimiento, o => o.Ignore())`).

- [ ] **Step 1: Pruebas (fallan).** `ResolutorVencimientoTests`: pedida gana a socio; socio gana a `CON`; sin ninguna → `CON`; condición inexistente / inactiva propaga el mensaje; vencimiento = `FechaDoc.Date + Dias` (0 → mismo día; 30 desde el 31 de enero → 2 de marzo/1 de marzo según año no bisiesto: usar fechas fijas, p. ej. 31/01/2026 + 30 = 02/03/2026); `FechaDoc` nula usa hoy (inyectando la fecha del día con tolerancia ±1 día); texto con espacios se recorta. En `FacturaDomainTests`/`NotaDebitoDomainTests`: asigna condición y vencimiento; usa la del socio; condición inválida lanza y **no avanza `SigNumero`**; un `FechaVencimiento` enviado por el cliente se sobrescribe.
- [ ] **Step 2:** implementar el resolutor, ajustar los dos dominios, DTOs, mapeo y DI; actualizar los `new FacturaDomain(...)`/`new NotaDebitoDomain(...)` de las pruebas existentes con mocks por defecto (`IResolutorVencimiento` devolviendo `("CON", FechaDoc)` y repo de socio sin resultados).
- [ ] **Step 3:** pruebas de la tarea y de ambos dominios en verde; suite completa **antes de commitear** (esperado: 2084 + nuevas, 0 con error).
- [ ] **Step 4: Commit** `feat(vencimientos): condicion de pago y fecha de vencimiento en facturas y notas de debito de cliente`.

### Task 4: Vencimiento en facturas de compra y notas de débito de proveedor; condición en el socio

**Plantilla:** Tarea 3 (resolutor ya existente) y el patrón de actualización de `SocioNegocioDomain`.

**Precondición:** Tarea 3 hecha.

**Files:** Modify `API.Domain.Core/{FacturaCompraDomain,NotaDebitoCompraDomain,SocioNegocioDomain}.cs`, DTOs `facturaCompra/{FacturaCompraCrearDTO,FacturaCompraDTO}.cs`, `notaDebitoCompra/{NotaDebitoCompraCrearDTO,NotaDebitoCompraDTO}.cs`, `socioNegocio/{SocioNegocioCrearDTO,SocioNegocioActualizarDTO,SocioNegocioDTO}.cs`; Tests `Domain/{FacturaCompraDomainTests,NotaDebitoCompraDomainTests,SocioNegocioDomainTests}.cs`.

**Reglas:** `FacturaCompraDomain.InsertarAsync` y `NotaDebitoCompraDomain.InsertarAsync` resuelven condición y vencimiento exactamente como la Tarea 3 (antes de numerar; condición del **proveedor**); los DTOs de lectura ganan `CondicionPago` y `FechaVencimiento`, los de creación solo `CondicionPago`. `SocioNegocioDomain` (alta y actualización) valida que `CondicionPago`, si viene, exista y esté activa (`ICondicionPagoDomain.ObtenerActivaAsync`) y la guarda; vacío/nulo se guarda como `null` (el sistema usará `CON`). Los DTOs de socio ganan `string? CondicionPago`. Actualizar un socio **no** toca documentos ya emitidos.

- [ ] **Step 1:** pruebas (fallan): ambos dominios de compras asignan/heredan/rechazan como en la Tarea 3 (incluyendo "no consume consecutivo"); socio: alta y actualización con condición válida, inexistente, inactiva, vacía.
- [ ] **Step 2:** implementar; actualizar los `new` de las pruebas existentes.
- [ ] **Step 3:** pruebas en verde y suite completa.
- [ ] **Step 4: Commit** `feat(vencimientos): condicion de pago y vencimiento en facturas de compra y notas de debito de proveedor; condicion por defecto del socio`.

### Task 5: Script completo y verificación de B1 con SQL real

**Plantilla:** commit `d644509`.

**Files:** Modify `sql/crear-api-db-test-completo.sql`.

- [ ] **Step 1:** agregar al final del script completo el bloque de `sql/2026-10-26-condiciones-pago-vencimientos.sql` **sin** el `THROW` de la Fase A (las tablas ya existen antes en el script), con el mismo estilo de encabezado que los bloques anteriores; el bloque debe ser idéntico al del script individual salvo el encabezado y el `THROW` (comprobar con `diff`).
- [ ] **Step 2:** base temporal desde el script completo; comprobar tabla y 5 semillas, columnas, 4 módulos; reaplicar el script individual encima sin error ni cambios. Eliminar la base.
- [ ] **Step 3:** suite completa (esperado: 2084 + las pruebas de B1, 0 con error); anotar el total exacto.
- [ ] **Step 4: Commit** `docs(db): script completo de creacion de API_DB_TEST incluye condiciones de pago y vencimientos`.

---

## Fase 2 — B2: saldo del socio y estado de cuenta (API)

### Task 6: `SaldoSocioService`

**Plantilla:** `SaldoFacturaService`/`SaldoFacturaCompraService` y sus pruebas (mocks de repos con `AsAsyncQueryable`).

**Files:** Create `API.Domain.Interface/ISaldoSocioService.cs`, `API.Domain.Core/SaldoSocioService.cs`, `API.Service.WebApi.Tests/Domain/SaldoSocioServiceTests.cs`; Modify `Startup.cs`.

**Interfaces — Produces:**

```csharp
public sealed record SaldoSocio(string CodigoSn, string Tipo, decimal Saldo, decimal Vencido, int DocumentosAbiertos);

public interface ISaldoSocioService
{
    /// <summary>Saldo y vencido de cada socio pedido (solo aparecen los que existen); el tipo sale de SocioNegocio.TipoSn.</summary>
    Task<IReadOnlyDictionary<string, SaldoSocio>> ObtenerAsync(IEnumerable<string> codigosSn);
}
```

**Reglas:** carga los socios pedidos (una consulta); para los de tipo `C`, los documentos `Factura` de esos socios con `Cancelado != 'S'` (una consulta) y sus saldos con `ISaldoFacturaService.ObtenerAsync(entries)` (una llamada); para `P`, `FacturaCompra` y `ISaldoFacturaCompraService`. `Saldo` = Σ `Saldo` de los documentos con saldo > 0; `DocumentosAbiertos` = cuántos; `Vencido` = Σ saldo de los que cumplen `COALESCE(FechaVencimiento, FechaDoc).Date < hoy`. Socio sin documentos abiertos aparece con ceros. Redondeo a 2 decimales (`AwayFromZero`).

- [ ] **Step 1:** pruebas (fallan): cliente y proveedor en la misma llamada; varios socios; vencido solo con saldo > 0; documento cancelado fuera; vencimiento nulo usa `FechaDoc`; socio inexistente no aparece; entradas vacías no consultan; nota de débito (`ND`/`DN`) cuenta como documento.
- [ ] **Step 2:** implementar.
- [ ] **Step 3:** pruebas en verde.
- [ ] **Step 4: Commit** `feat(estado-cuenta): servicio de saldo y vencido por socio`.

### Task 7: `EstadoCuentaService`

**Plantilla:** `SaldoFacturaService` (estilo de consultas), `NotaCreditoDomain` (lectura de notas con base), `PagoRecibidoDomain` (fechas de pago).

**Precondición:** Task 6 hecha.

**Files:** Create `API.Domain.Interface/IEstadoCuentaService.cs`, `API.Domain.Core/EstadoCuentaService.cs`, `API.Service.WebApi.Tests/Domain/EstadoCuentaServiceTests.cs`; Modify `Startup.cs`.

**Interfaces — Produces:**

```csharp
public sealed record MovimientoCuenta(DateTime Fecha, string Tipo, int NumDoc, string Descripcion, decimal Cargo, decimal Abono, decimal SaldoAcumulado, bool Informativo);
public sealed record EstadoCuenta(SocioNegocio Socio, DateTime Desde, DateTime Hasta, decimal SaldoInicial, IReadOnlyList<MovimientoCuenta> Movimientos, decimal SaldoFinal, decimal SaldoActual, decimal Vencido);

public interface IEstadoCuentaService
{
    /// <summary>desde/hasta nulos = 1 de enero del año en curso / hoy. Lanza "El socio de negocio no existe." o "La fecha inicial no puede ser posterior a la final.".</summary>
    Task<EstadoCuenta> ObtenerAsync(string codigoSn, DateTime? desde, DateTime? hasta);
}
```

**Reglas:** `Tipo` textual: "Factura", "Nota de débito", "Nota de crédito", "Pago" (cliente) o "Factura de compra", "Nota de débito de proveedor", "Nota de crédito de proveedor", "Pago efectuado"; `Descripcion` = serie de texto breve (comentario o nombre del documento) sin datos sensibles. Cliente: cargos = `Factura` (`--` y `ND`) no canceladas; abonos = `NotaCredito` con `BaseTipo=6` y `BaseEntry` no nulo, no canceladas, **y** pagos `PagoRecibido` no cancelados (monto = `TotalPago`); `NotaCredito` sin base → `Informativo=true`, `Cargo=Abono=0`. Proveedor: espejo con `FacturaCompra`/`NotaCreditoCompra` (`BaseTipo=13`)/`PagoEfectuado`. `SaldoInicial` = Σ(cargo − abono) de lo anterior a `desde` (fecha sin hora); movimientos con `desde ≤ fecha ≤ hasta` ordenados por fecha, luego tipo y número, con `SaldoAcumulado` desde `SaldoInicial`; `SaldoFinal` = último acumulado (o `SaldoInicial` sin movimientos). `SaldoActual` y `Vencido` de `ISaldoSocioService`.

- [ ] **Step 1:** pruebas (fallan): cliente con factura, nota de débito, nota de crédito con base, pago parcial y pago cancelado (cifra por cifra del acumulado); saldo inicial por movimientos anteriores al rango; orden por fecha y desempate; nota de crédito sin base informativa y sin efecto; documentos cancelados fuera; proveedor equivalente; rango por defecto; `desde > hasta` y socio inexistente; **invariante:** con todo el histórico, `SaldoFinal == SaldoActual` para un cliente y para un proveedor con un caso mixto.
- [ ] **Step 2:** implementar (consultas agrupadas por socio; el cómputo del acumulado en memoria).
- [ ] **Step 3:** pruebas en verde.
- [ ] **Step 4: Commit** `feat(estado-cuenta): estado de cuenta de clientes y proveedores con saldo acumulado`.

### Task 8: API de estado de cuenta (DTOs, aplicación, controlador, permisos)

**Plantilla:** Tarea 8 de la Fase A (`SaldoFacturaCompraController`, aplicación y DTO de saldos) y `PagoEfectuadoController` para el estilo.

**Precondición:** Tasks 6–7 hechas.

**Files:** Create `API.Application.DTO/estadoCuenta/{EstadoCuentaDTO,MovimientoCuentaDTO,SaldoSocioDTO}.cs`, `API.Application.Interface/IEstadoCuentaApplication.cs`, `API.Application.Main/EstadoCuentaApplication.cs`, `API.Service.WebApi/Controllers/EstadoCuentaController.cs`; Modify `PerfilMapeo.cs` (si el patrón lo requiere), `Startup.cs`; Tests `Application/EstadoCuentaApplicationTests.cs`, `Controllers/EstadoCuentaControllerTests.cs`, `Controllers/EstadoCuentaPermisosTests.cs`.

**Rutas y permisos:** `GET api/EstadoCuenta/{codigoSn}?desde=&hasta=` (Ver `EstadoCuenta`) → `EstadoCuentaDTO { CodigoSn, NombreSn, TipoSn, Desde, Hasta, SaldoInicial, SaldoFinal, SaldoActual, Vencido, Movimientos[] }`; `POST api/EstadoCuenta/Saldos` (Ver `EstadoCuenta`) con cuerpo `["C001","P003"]` → lista de `SaldoSocioDTO { CodigoSn, Tipo, Saldo, Vencido, DocumentosAbiertos }`. Errores del dominio → 400 con el mensaje; socio inexistente → 404 en el `GET`.

- [ ] **Step 1:** pruebas (fallan): aplicación (mapeo y errores), controlador (200/400/404/403) y permisos por reflexión (módulo `EstadoCuenta`, acción Ver, rutas absolutas).
- [ ] **Step 2:** implementar.
- [ ] **Step 3:** pruebas en verde y suite completa (esperado: acumulado de tareas anteriores + nuevas, 0 con error).
- [ ] **Step 4: Commit** `feat(estado-cuenta): API de estado de cuenta y saldos por socio`.

---

## Fase 3 — B3: antigüedad de saldos (API)

### Task 9: `AntiguedadService`

**Plantilla:** `SaldoSocioService` (Tarea 6).

**Files:** Create `API.Domain.Interface/IAntiguedadService.cs`, `API.Domain.Core/AntiguedadService.cs`, `API.Service.WebApi.Tests/Domain/AntiguedadServiceTests.cs`; Modify `Startup.cs`.

**Interfaces — Produces:**

```csharp
public sealed record FilaAntiguedad(string CodigoSn, string NombreSn, decimal Vigente, decimal De1a30, decimal De31a60, decimal De61a90, decimal Mas90, decimal Total);
public sealed record DocumentoAntiguedad(int Entry, string Tipo, int NumDoc, DateTime FechaDoc, DateTime Vencimiento, int DiasVencido, decimal Saldo);

public interface IAntiguedadService
{
    /// <summary>tipo "C" (por cobrar) o "P" (por pagar); otro valor lanza "El tipo debe ser C o P.". Al día de hoy.</summary>
    Task<IReadOnlyList<FilaAntiguedad>> ObtenerAsync(string tipo);
    Task<IReadOnlyList<DocumentoAntiguedad>> ObtenerDetalleAsync(string tipo, string codigoSn);
}
```

**Reglas:** documentos de `Factura` (`C`) o `FacturaCompra` (`P`) con `Cancelado != 'S'` (`--` y nota de débito); sus saldos con el servicio de saldo correspondiente (una llamada); solo `Saldo > 0`; `Vencimiento = (FechaVencimiento ?? FechaDoc).Date`; `DiasVencido = (hoy − Vencimiento).Days`; `≤ 0` → Vigente; `1..30`, `31..60`, `61..90`, `> 90`; el saldo de cada documento va a **un solo** tramo; `Total` de la fila = suma de los cinco; filas ordenadas por `Total` descendente y luego por nombre; el detalle ordena por `Vencimiento` ascendente. `NombreSn` del socio; `Tipo` del documento: "Factura"/"Nota de débito" (o sus equivalentes de proveedor).

- [ ] **Step 1:** pruebas (fallan): cortes en los límites exactos (días 0, 1, 30, 31, 60, 61, 90, 91); documento sin saldo y cancelado fuera; un socio con documentos en varios tramos suma bien; socio sin saldo no aparece; tipo inválido; detalle ordenado; nota de débito incluida; vencimiento nulo usa `FechaDoc`; pagos parciales reducen el saldo del tramo.
- [ ] **Step 2:** implementar (usar `hoy` inyectable —un `Func<DateTime>` o `TimeProvider`— solo si el patrón del repo ya lo hace; de lo contrario, las pruebas fijan fechas relativas a `DateTime.Today`).
- [ ] **Step 3:** pruebas en verde.
- [ ] **Step 4: Commit** `feat(antiguedad): servicio de antiguedad de saldos por cobrar y por pagar`.

### Task 10: API de antigüedad (DTOs, aplicación, controlador, permisos)

**Plantilla:** Tarea 8 (estado de cuenta).

**Precondición:** Task 9 hecha.

**Files:** Create `API.Application.DTO/antiguedad/{FilaAntiguedadDTO,DocumentoAntiguedadDTO}.cs`, `API.Application.Interface/IAntiguedadApplication.cs`, `API.Application.Main/AntiguedadApplication.cs`, `API.Service.WebApi/Controllers/AntiguedadController.cs`; Modify `Startup.cs`; Tests `Application/AntiguedadApplicationTests.cs`, `Controllers/AntiguedadControllerTests.cs`, `Controllers/AntiguedadPermisosTests.cs`.

**Rutas y permisos:** `GET api/Antiguedad/Cobrar` y `GET api/Antiguedad/Cobrar/{codigoSn}` (Ver `AntiguedadCobrar`); `GET api/Antiguedad/Pagar` y `GET api/Antiguedad/Pagar/{codigoSn}` (Ver `AntiguedadPagar`). Cuatro acciones, cada una con su `RequierePermiso` (un usuario con solo `AntiguedadCobrar` no puede leer lo de pagar y viceversa).

- [ ] **Step 1:** pruebas (fallan): aplicación y controlador (200/400/403), permisos por reflexión **por acción**.
- [ ] **Step 2:** implementar.
- [ ] **Step 3:** pruebas en verde y suite completa; anotar el total.
- [ ] **Step 4: Commit** `feat(antiguedad): API de antiguedad de saldos por cobrar y por pagar`.

---

## Fase 4 — Web

### Task 11: DTOs y clientes tipados de la Web

**Plantilla:** Web commits `c8a3db5` y `7ddd6dc`.

**Precondición:** API Tareas 1–10 hechas; Web `git status` solo con los 9 ajenos.

**Files:** Create `Web.ApiClient/Dtos/CondicionPago/*`, `Web.ApiClient/Dtos/EstadoCuenta/*`, `Web.ApiClient/Dtos/Antiguedad/*`, `Web.ApiClient/Clientes/{ICondicionPagoApiClient,CondicionPagoApiClient,IEstadoCuentaApiClient,EstadoCuentaApiClient,IAntiguedadApiClient,AntiguedadApiClient}.cs`; Modify `Web.ApiClient/Dtos/{Factura/FacturaDTO,Factura/FacturaCrearDTO,NotaDebito/NotaDebitoDTO,NotaDebito/NotaDebitoCrearDTO,FacturaCompra/FacturaCompraDTO,FacturaCompra/FacturaCompraCrearDTO,NotaDebitoCompra/NotaDebitoCompraDTO,NotaDebitoCompra/NotaDebitoCompraCrearDTO}.cs` (lectura: `CondicionPago`, `FechaVencimiento`; creación: `CondicionPago`), `Web.UI/Program.cs` (registro de los tres clientes).

**Nota sobre los DTOs de socio de la Web:** `SocioNegocioCrearDTO.cs`, `SocioNegocioActualizarDTO.cs` y `Respuesta.cs` tienen cambios locales ajenos sin commit. Esta tarea **no** los toca. La Tarea 13 agrega `CondicionPago` a los DTOs de socio de la Web sobre **copias de seguridad de esos dos archivos** (ver allí).

- [ ] **Step 1:** crear DTOs y clientes (rutas `api/CondicionPago`, `api/EstadoCuenta/{codigoSn}?desde=&hasta=`, `POST api/EstadoCuenta/Saldos`, `api/Antiguedad/Cobrar|Pagar[/{codigoSn}]`); campos nulables nuevos en los DTOs de documentos.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores (los 9 ajenos intactos).
- [ ] **Step 3: Commit** `feat(vencimientos): DTOs y clientes tipados de condiciones de pago, estado de cuenta y antiguedad`.

### Task 12: Pantalla de condiciones de pago y entradas de menú

**Plantilla:** Web commit `5154ad5` (Formas de pago: `FormasPagoController`, `Views/FormasPago/{Index,_Form}.cshtml`, `formaspago.js`, entrada del menú).

**Files:** Create `Web.UI/Controllers/CondicionesPagoController.cs`, `Web.UI/Views/CondicionesPago/{Index,_Form}.cshtml`, `Web.UI/wwwroot/js/condicionespago.js`; Modify `Web.UI/Views/Shared/_Layout.cshtml` (entradas del menú: "Condiciones de pago" y "Estado de cuenta" en Socios de negocio; "Antigüedad por cobrar" en Ventas; "Antigüedad por pagar" en Compras; cada una visible con Ver en su módulo; actualizar los booleanos de grupo activo/visible; las entradas de estado de cuenta y antigüedad apuntan a rutas que se crean en las Tareas 15–16: los enlaces pueden quedar vacíos hasta entonces).

**Reglas de la pantalla:** listado (código, nombre, días, estado) y alta/edición en modal; `CON` sin botón de baja; baja con confirmación; texto escapado; `[RequierePermiso("CondicionesPago", Accion.X)]` y `[ValidateAntiForgeryToken]` en POST.

- [ ] **Step 1:** copiar/traducir la plantilla.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** `feat(vencimientos): pantalla de condiciones de pago y entradas de menu`.

### Task 13: Condición de pago, saldo y vencido en socios de negocio

**Plantilla:** cómo el formulario de socios maneja `NumLstPrecio` (lista de precios) en `Views/SociosNegocio/{_Form,Crear,Index}.cshtml` y `wwwroot/js/sociosNegocio.js`; Web commit `20ff48d` (columnas de saldo fusionadas en un listado).

**Precondición:** Tarea 11 hecha.

**Files:** Modify `Web.UI/Controllers/SociosNegocioController.cs`, `Web.UI/Views/SociosNegocio/{_Form,Crear,Index}.cshtml`, `Web.UI/wwwroot/js/sociosNegocio.js`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioDTO.cs` (lectura) y los DTOs de creación/actualización (`SocioNegocioCrearDTO.cs`, `SocioNegocioActualizarDTO.cs`).

**Procedimiento seguro para los DTOs ajenos:** `SocioNegocioCrearDTO.cs` y `SocioNegocioActualizarDTO.cs` tienen cambios locales sin commit que no son del plan. (1) Guardar copias de ambos archivos tal cual están; (2) editarlos agregando **solo** `public string? CondicionPago { get; set; }` con su comentario; (3) en el commit, stagear el archivo **solo con el hunk propio** usando `git add -p` (o construir el parche con `git diff` filtrado y `git apply --cached`), de modo que el cambio ajeno quede sin stagear; (4) comprobar con `git diff --cached` que solo aparece la propiedad nueva; (5) si no es posible separar los hunks, NO commitear esos dos archivos: reportarlo como concern y dejar la propiedad solo en `SocioNegocioDTO.cs` (lectura) —la condición del socio se guardará entonces solo por la API— y el campo del formulario queda para cuando el usuario resuelva sus cambios locales.

**Reglas:** el campo "Condición de pago" (lista de condiciones activas; opción vacía = usar Contado) va en la pestaña Comercial del alta y de la edición; la ficha muestra **Saldo** y **Vencido** del socio (de `POST api/EstadoCuenta/Saldos`) y un enlace "Ver estado de cuenta" (`/EstadoCuenta?codigoSn=…`); el listado de socios agrega columnas Saldo y Vencido pidiendo los saldos del conjunto en **una sola llamada**, con degradación (sin columnas) si falla. Los usuarios sin Ver en `EstadoCuenta` no ven ni piden saldos. Texto escapado al pintar.

- [ ] **Step 1:** implementar siguiendo las plantillas y el procedimiento anterior.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** (rutas explícitas; ver procedimiento) `feat(vencimientos): condicion de pago por defecto, saldo y vencido en socios de negocio`.

### Task 14: Condición y vencimiento en los formularios y listados de documentos

**Plantilla:** Web commits `20ff48d` (columnas en los listados), `fbfa61b` (datos adicionales en `ViewBag` y JS del formulario de factura) y el hook `onSeleccion` del buscador de socio de `facturas.js` (que ya sincroniza la lista de precio).

**Precondición:** Tarea 11 hecha (y 13 si se quiere el campo del socio disponible en el buscador).

**Files:** Modify `Web.UI/Controllers/{FacturasController,NotasDebitoController,FacturasCompraController,NotasDebitoCompraController}.cs`, `Web.UI/Views/{Facturas,NotasDebito,FacturasCompra,NotasDebitoCompra}/{_Form,Index}.cshtml`, `Web.UI/wwwroot/js/{facturas,notasdebito,facturascompra,notasdebitocompra}.js`.

**Reglas:** (1) cada `_Form` agrega una lista "Condición de pago" (opciones: condiciones activas obtenidas del cliente tipado, entregadas por el controlador igual que las series) y un campo de solo lectura "Vencimiento"; (2) al elegir el socio (`onSeleccion`), la lista se precarga con `s.condicionPago ?? 'CON'`; al cambiar la condición o la fecha del documento, el campo Vencimiento se recalcula **solo para mostrarlo** (`fechaDoc + dias`; los días van en el atributo `data-dias` de cada `<option>`); (3) el cuerpo del alta envía `CondicionPago` (el servidor vuelve a calcular); (4) Copiar a precarga la condición del socio como en el alta normal; (5) cada listado agrega la columna **Vence** (fecha) y la insignia **Vencida** (rojo) cuando hay saldo > 0 y la fecha ya pasó (vacío para cancelados y para filas sin saldo), sin romper los índices de columnas existentes; (6) el buscador de socios de cada controlador debe devolver `CondicionPago` (viene en `SocioNegocioDTO`).

- [ ] **Step 1:** implementar en los cuatro documentos con el mismo patrón.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** `feat(vencimientos): condicion de pago y vencimiento en los formularios y listados de facturas y notas de debito`.

### Task 15: Pantalla de estado de cuenta

**Plantilla:** Web commits `a5a9845` (listado/detalle) y `e4e59a2` (página con buscador de socio por `App.autocompletar`).

**Precondición:** Tareas 11 y 12 hechas.

**Files:** Create `Web.UI/Controllers/EstadoCuentaController.cs`, `Web.UI/Views/EstadoCuenta/Index.cshtml`, `Web.UI/wwwroot/js/estadocuenta.js`.

**Reglas:** `Index` acepta `?codigoSn=&desde=&hasta=`; buscador de socio (clientes y proveedores; `BuscarSocios` sin filtro de tipo) y rango de fechas (por defecto 1 de enero del año y hoy); tabla de movimientos (fecha, tipo y número, descripción, cargo, abono, saldo) con filas de **Saldo inicial** y **Saldo final**, las notas informativas atenuadas con la etiqueta "sin aplicar", y un resumen "Saldo actual" y "Vencido"; mensajes de error de la API visibles; `[RequierePermiso("EstadoCuenta", Accion.Ver)]`; texto escapado; enlaces desde la entrada de menú y (en las Tareas 13/14) desde la ficha del socio.

- [ ] **Step 1:** implementar siguiendo las plantillas.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** `feat(estado-cuenta): pantalla de estado de cuenta de socios`.

### Task 16: Pantallas de antigüedad de saldos (por cobrar y por pagar)

**Plantilla:** Web commit `a5a9845` (DataTables y modal de detalle) y la Tarea 15.

**Precondición:** Tareas 11 y 12 hechas.

**Files:** Create `Web.UI/Controllers/AntiguedadController.cs`, `Web.UI/Views/Antiguedad/{Cobrar,Pagar,_Detalle}.cshtml`, `Web.UI/wwwroot/js/antiguedad.js`.

**Reglas:** dos acciones (`Cobrar` con `[RequierePermiso("AntiguedadCobrar", Accion.Ver)]`, `Pagar` con `AntiguedadPagar`) que comparten la vista parcial y el JS; tabla por socio (Socio, Vigente, 1-30, 31-60, 61-90, Más de 90, Total) ordenable, con fila de totales y las columnas vencidas resaltadas; clic en una fila abre un modal con los documentos (tipo y número, fecha, vencimiento, días de atraso, saldo) y el enlace "Ver estado de cuenta"; importes con 2 decimales; texto escapado.

- [ ] **Step 1:** implementar.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores.
- [ ] **Step 3: Commit** `feat(antiguedad): pantallas de antiguedad de saldos por cobrar y por pagar`.

---

## Fase 5 — Verificación integral

### Task 17: Prueba de humo contra SQL Server real y recorrido en navegador

**Plantilla:** `scratchpad\smoke-pagos-efectuados.ps1` y `smoke-pagos.ps1` (helpers y estilo), `evid-up.ps1`/`evid-down.ps1`, `scratchpad\shots\common.js` (puppeteer).

**Precondición:** Tareas 1–16 hechas y aprobadas.

**Files:** (scratchpad, no se commitea) `smoke-vencimientos.ps1`.

- [ ] **Step 1:** `evid-up.ps1 -NombreDb API_DB_EVID<n>`.
- [ ] **Step 2: Guion `smoke-vencimientos.ps1`** (`OK`/`FALLA`, termina con "Todas las verificaciones pasaron"), mínimo **40 comprobaciones**: login y 401; catálogo (5 semillas; alta; duplicado; días fuera de rango; baja de `CON` rechazada; baja de otra; condición inactiva no asignable a un socio ni a un documento); socio con condición `C30` y otro sin condición; factura de venta con condición explícita `C15` (vencimiento = fecha + 15), con la del socio (+30), sin ninguna (+0, `CON`), con condición inexistente (400 y **SigNumero sin cambio**), con `FechaVencimiento` enviada por el cliente (se ignora); lo mismo con nota de débito de cliente, factura de compra y nota de débito de proveedor; migración: crear una factura con la API vieja sin condición ⇒ `CON`; estado de cuenta de un cliente tras factura + nota de débito + nota de crédito con base + pago parcial + pago cancelado (cifras exactas de saldo inicial, acumulado y final) y de un proveedor equivalente; nota de crédito sin base informativa; **invariante** `SaldoFinal == SaldoActual` y `SaldoActual == suma de Saldos` (`POST api/Factura/Saldos`) para el cliente y para el proveedor; `POST api/EstadoCuenta/Saldos`; antigüedad: documentos con `FechaDoc` retrocedida (por la API con fecha pasada) en cada tramo (vigente, 1-30, 31-60, 61-90, +90) para cobrar y para pagar, totales por fila y detalle ordenado; documento pagado por completo no aparece; permisos: un usuario con plantilla que solo tiene `AntiguedadCobrar` obtiene 403 en `Pagar` y viceversa.
- [ ] **Step 3:** correr el guion y guardar la salida en `scratchpad\evid-vencimientos.txt`: todas OK. Luego correr `smoke-pagos.ps1` (54) y `smoke-pagos-efectuados.ps1` (51) sobre el **mismo entorno** (o uno nuevo) para comprobar que no hay regresión.
- [ ] **Step 4: Recorrido en navegador** (puppeteer): catálogo de condiciones; condición en un socio y saldo/vencido en su ficha y en el listado; alta de factura de venta y de compra con condición y vencimiento visibles; listados con "Vence/Vencida"; estado de cuenta de un cliente y de un proveedor; ambas antigüedades con el detalle; menús con las cuatro entradas; consola sin errores. Capturas reales en `scratchpad\docfigs\fig-vencimientos-*.png`, cada una vista y comprobada.
- [ ] **Step 5:** limpiar: `evid-down.ps1 -NombreDb … -Todo` para cada base creada; puertos 5199/5200 libres; ninguna base `API_DB_EVID%` restante; `git status` solo con los archivos esperados; **sin push**. Guardar los guiones y salidas finales en un archivo nuevo por corrida (no sobrescribir evidencias anteriores).

## Notas de despliegue (fuera de las tareas)

- Ejecutar `sql/2026-10-26-condiciones-pago-vencimientos.sql` en `API_DB_TEST` (idempotente; requiere la Fase A) y asignar a las plantillas de permisos los módulos `CondicionesPago`, `EstadoCuenta`, `AntiguedadCobrar` y `AntiguedadPagar`.
- La migración marca **todas** las facturas existentes como de contado y vencidas en su fecha; para reflejar el crédito real hay que asignar la condición correcta por SQL a las facturas abiertas de proveedores con crédito.
- La Fase C (límite de crédito) usará `SaldoSocio` (Tarea 6) para comparar contra el límite.
