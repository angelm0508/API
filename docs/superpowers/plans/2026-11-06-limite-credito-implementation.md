# Límite de crédito de clientes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que la API rechace pedidos, entregas y facturas de venta que harían que la exposición de un cliente (saldo abierto + comprometido en pedidos y entregas) supere su límite de crédito, salvo autorización de un usuario con el permiso `AutorizarExcesoCredito`, que queda registrada (Fase C de cuentas por cobrar / por pagar).

**Architecture:** Dos campos nuevos en el socio (`LimiteCredito`, `BloquearSiVencido`) y una tabla `AutorizacionCredito`. Un servicio de dominio `ICreditoClienteService` calcula la posición del cliente (saldo y vencido con `ISaldoSocioService` de la Fase B, más el valor de las líneas abiertas de pedidos y entregas) y verifica cada documento nuevo; `PedidoDomain`, `EntregaDomain` y `FacturaDomain` lo llaman antes de numerar. Un contexto de autorización por petición (`IAutorizacionCreditoContexto`) lo llena el controlador tras comprobar el permiso; el dominio guarda la autorización en la misma transacción que el documento. El rechazo viaja como HTTP 409 con las cifras; la Web muestra el diálogo «Autorizar y guardar».

**Tech Stack:** .NET 7 API (N-capas, EF Core 7 DB-first, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server (solo bases temporales), PowerShell/node para verificación.

**Spec:** `docs/superpowers/specs/2026-11-06-limite-credito-design.md` (repo API, commit `7055d02` más el apartado «Ajustes del plan» agregado con este plan).

**Cómo usar este plan (importante):** hay módulos construidos y verificados que son la **plantilla autoritativa** (Fase B para servicios de saldo, controladores y pantallas; Fase A para pagos). Cada tarea nombra su plantilla, la lista exacta de archivos, el código nuevo clave y las reglas. Quien implementa abre la plantilla real del repositorio y la adapta; no reescribe de memoria. Los mensajes indicados se verifican literalmente en pruebas.

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. `git log --oneline -1` muestra el commit del plan o posterior; `git status --short` solo con `API.Service.WebApi/appsettings.json` modificado. Línea base de pruebas: **2350** pasando.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `51b944e` (o posterior). `git status --short` muestra **9 archivos ajenos** sin commit que NO se tocan ni se stagean: `Web.ApiClient/Dtos/Almacen/AlmacenActualizarDTO.cs`, `Web.ApiClient/Dtos/Almacen/AlmacenCrearDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloActualizarDTO.cs`, `Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioActualizarDTO.cs`, `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioCrearDTO.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`. **Atención:** dos de ellos son DTOs de socio que esta fase también debe ampliar y `Respuesta.cs` tiene cambios ajenos: se usa el **procedimiento seguro** de la Tarea 8 (copias de seguridad, solo el hunk propio con `git apply --cached --unidiff-zero`, comprobar con `git diff --cached`). No se agrega nada a `Respuesta.cs`.
- Build/test (ejecutar desde PowerShell; la carpeta scratchpad de la sesión actual es `C:\Users\migue\AppData\Local\Temp\claude\9f8881082ead4f04\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad` y puede cambiar: si cambia, usar la que indique el entorno):
  - API (suite): `dotnet build API.Service.WebApi.Tests\API.Service.WebApi.Tests.csproj` y `dotnet test API.Service.WebApi.Tests --nologo -v q` (sirve el build normal; `--filter "FullyQualifiedName~Clase"` para una clase).
  - Web: `dotnet build Web.slnx` (si `bin` está bloqueado por un Web.UI en ejecución, `-p:BaseOutputPath="<scratchpad>\webbuild\"`).
- SQL de pruebas: SOLO bases temporales (`API_DB_EVID<n>` vía `evid-up.ps1` / `evid-down.ps1` del scratchpad); **jamás** `API_DB_TEST`. La contraseña de `sa` se extrae de `API.Service.WebApi\appsettings.json` a una variable de proceso, sin imprimirla ni escribirla. Las bases temporales se eliminan al terminar. Scripts de verificación existentes en el scratchpad: `evid-up.ps1`, `evid-down.ps1`, `smoke-pagos-final.ps1` (54), `smoke-pagos-efectuados.ps1` (51), `smoke-vencimientos.ps1` (122); puppeteer en `<scratchpad>\pupmod`.
- Tipos de objeto de la cadena de ventas (`TipoDocumentoVenta`): Cotización 3, Pedido 4, Entrega 5, Factura 6, Orden de servicio 70.

## Decisiones del plan que ajustan el spec (verificadas en el código)

1. **El pago al facturar no es parte de la creación de la factura.** La Web crea la factura y luego envía el pago a `/PagosRecibidos/Crear`; la API no lo ve al crear. Por eso `FacturaCrearDTO` gana `PagoInmediato` (decimal `>= 0`, por defecto 0): el monto que el usuario va a cobrar en el acto. Se resta del monto nuevo. Solo se acepta si el usuario tiene `PagosRecibidos` / Agregar (si no, 403 «No tenés permiso para registrar pagos.») y no puede exceder `TotalDoc` («El pago inmediato no puede exceder el total del documento.»). Riesgo aceptado: la API no puede garantizar que ese pago se registre después; la deuda resultante queda visible como saldo.
2. **Contexto de autorización por petición.** En vez de cambiar las firmas de `InsertarAsync`, un servicio `IAutorizacionCreditoContexto` (registrado *scoped*) con `bool Autorizada` y `string? Usuario` lo llena el controlador. Los dominios lo leen.
3. **Cuerpo del 409.** Es un `Respuesta` normal más una propiedad `credito`: `{ "resultado": false, "mensaje": "...", "dato": null, "credito": { "codigo": "LIMITE_CREDITO_EXCEDIDO" | "CLIENTE_CON_VENCIDO", "limite": 5000.00, "saldo": 3000.00, "comprometido": 1500.00, "vencido": 0.00, "monto": 800.00, "exceso": 300.00, "disponible": 500.00 } }`. En la API es `RespuestaCredito<T> : Respuesta<T>` (clase nueva) que la capa de aplicación devuelve y el controlador responde con `Conflict(...)`.
4. **Valor del comprometido.** Para cada línea abierta: `CantidadAbierta / Cantidad × TotalLinea × factor`, con `factor = TotalDoc / Σ TotalLinea` del documento origen (1 si `Σ TotalLinea = 0`). Así el descuento y el impuesto de la cabecera quedan incluidos sin depender de la semántica de `Impuesto`. Redondeo por documento a 2 decimales `AwayFromZero`. Una línea con `Cantidad = 0` no cuenta.
5. **Reabrir y cambio de socio: solo el pedido.** Entrega y Factura no tienen reapertura en el dominio (verificado). `PedidoDomain.ActualizarAsync` sí reabre (`seReabre`) y permite cambiar de socio cuando no hay líneas copiadas: ambos casos vuelven a verificar al cliente destino con el valor abierto del pedido.
6. **Fuera de control (riesgos documentados):** el alta de pedido sin líneas (`InsertarAsync(Pedido obj)`: sin líneas no compromete nada) y los endpoints sueltos de líneas (`PedidoDetalle`/`EntregaDetalle`/`FacturaDetalle`), que la Web no usa para crear documentos. La verificación se hace en la creación atómica cabecera + líneas.
7. **El límite solo aplica a clientes:** `SocioNegocioDomain` rechaza `LimiteCredito` en un socio que no es cliente (`TipoSn <> 'C'`) con «El límite de crédito solo aplica a clientes.».

## Global Constraints

Cada tarea las incluye implícitamente.

- **Datos:** `SocioNegocio.LimiteCredito decimal(19,2) NULL` con `CHECK (LimiteCredito IS NULL OR LimiteCredito >= 0)`; `SocioNegocio.BloquearSiVencido nvarchar(1) NOT NULL DEFAULT 'N'` con `CHECK IN ('S','N')`; tabla `AutorizacionCredito(Id int identity PK, TipoDocumento int, EntryDocumento int, CodigoSn nvarchar(15) FK SocioNegocio, Usuario nvarchar(50), Motivo nvarchar(20) CHECK IN ('LIMITE','VENCIDO'), Limite decimal(19,2) NULL, Saldo, Comprometido, Vencido, Monto, Exceso decimal(19,2) NOT NULL, Fecha datetime NOT NULL DEFAULT GETDATE())`, índices por `(TipoDocumento, EntryDocumento)` y `CodigoSn`, sin borrado. Módulo de permisos `AutorizarExcesoCredito` (nombre «Autorizar exceso de crédito», grupo `Ventas`), acceso denegado por defecto. Ninguna fila existente se modifica (todos sin límite).
- **Valor del límite:** `NULL` = sin límite; `0` = solo contado; `> 0` = límite.
- **Exposición** = `Saldo` (de `ISaldoSocioService`) + `Comprometido`. `Disponible = Limite − Exposicion` (puede ser negativo) o `NULL` sin límite.
- **Comprometido** = valor de las líneas abiertas de pedidos y entregas **activos** (`Cancelado != 'S'` y `EstadoDoc != 'C'`) del cliente; una línea copiada a un documento destino activo (`BaseTipo`/`BaseEntry`/`BaseLinea`) deja de contar en el origen. Cotizaciones y órdenes de servicio no cuentan. Cargas agrupadas (una consulta por tabla), no una por documento.
- **Monto nuevo** = `max(0, TotalDoc − PagoInmediato − Liberado)`; `Liberado` = Σ por línea con `BaseTipo` 4 o 5 de `Cantidad × valor unitario neto de la línea origen` (mismo valor que se contó como comprometido). Líneas con base 3 o 70 no liberan.
- **Verificación (orden):** (1) socio inexistente o `TipoSn <> 'C'` → permitido; (2) `Monto = 0` → permitido; (3) `BloquearSiVencido = 'S'` y `Vencido > 0` → no permitido, motivo `VENCIDO` (`Exceso = Monto`); (4) límite no vacío y `Exposicion + Monto > Limite` → no permitido, motivo `LIMITE`, `Exceso = Exposicion + Monto − Limite`; (5) otro caso → permitido.
- **Mensajes (verificados literalmente en pruebas):** `LIMITE_CREDITO_EXCEDIDO` → «El cliente {CodigoSn} excede su límite de crédito: límite {Limite:N2}, exposición {Exposicion:N2}, documento {Monto:N2}, exceso {Exceso:N2}.»; `CLIENTE_CON_VENCIDO` → «El cliente {CodigoSn} tiene saldo vencido de {Vencido:N2} y está bloqueado para nuevos documentos.» (cultura invariante). Además: «El límite de crédito solo aplica a clientes.», «El límite de crédito no puede ser negativo.», «El límite de crédito admite como máximo 2 decimales.», «El bloqueo por vencido debe ser S o N.», «El pago inmediato no puede exceder el total del documento.», «No tenés permiso para registrar pagos.», «No tenés permiso para autorizar excesos de crédito.».
- **Orden en los dominios:** la verificación va **después** de validar las líneas con base y **antes** de numerar; un rechazo no consume consecutivo.
- **Autorización:** si la verificación falla y el contexto está autorizado, el documento se crea y se inserta una fila `AutorizacionCredito` (motivo, límite, saldo, comprometido, vencido, monto, exceso, usuario, fecha) **en la misma transacción**; si no falla, no se registra nada aunque la bandera venga en `true`.
- **Permisos:** `GET api/CreditoCliente/{codigoSn}` y `POST api/CreditoCliente/Posiciones` exigen `EstadoCuenta` / Ver. Pedir autorizar exige `AutorizarExcesoCredito` / Ver evaluado con `PermisoUsuario.Tiene(User, ...)` (403 si falta, sin tocar el dominio).
- **Compatibilidad:** los documentos sin la bandera y los clientes sin límite se comportan exactamente como hoy; ninguna prueba existente cambia de resultado.
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` NUNCA se commitea. Los 9 archivos ajenos de la Web NUNCA se stagean ni se modifican (salvo el hunk propio de los dos DTO de socio con el procedimiento seguro). **Sin `git push`.**
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (`git ls-files --eol`), archivos nuevos en LF, sin churn. Comentarios y mensajes en español, estilo del repo.
- **Lección de fases anteriores:** AutoMapper no debe poblar colecciones o navegaciones que el dominio inserta explícitamente (aquí `AutorizacionCredito`): se ignora en los mapas y se prueba con SQL real al final de la fase API.
- **Verificación al cerrar cada tarea de la API:** `dotnet build` sin errores y pruebas de la tarea en verde; suite completa donde se indique.

## File Structure

**API:** `sql/2026-11-06-limite-credito.sql`; entidad `AutorizacionCredito` (+ campos en `SocioNegocio`) y `ApiDbTestContext`; repositorio de `AutorizacionCredito`; dominio `ICreditoClienteService` / `CreditoClienteService`, `CreditoExcedidoException`, `IAutorizacionCreditoContexto` / `AutorizacionCreditoContexto` (interfaces y tipos en `API.Domain.Interface`); cambios en `PedidoDomain`, `EntregaDomain`, `FacturaDomain`, `SocioNegocioDomain`; DTOs de socio, `PedidoCrearDTO`/`PedidoActualizarDTO`/`EntregaCrearDTO`/`FacturaCrearDTO`, `RespuestaCredito<T>`, `PosicionCreditoDTO`; aplicación `CreditoClienteApplication` y cambios en `PedidoApplication`, `EntregaApplication`, `FacturaApplication`; controladores `CreditoClienteController` y cambios en `PedidoController`, `EntregaController`, `FacturaController`; `PerfilMapeo` y `Startup`.

**Web:** `Web.ApiClient` (DTOs de crédito, `ICreditoClienteApiClient`, `RespuestaCredito<T>`, soporte en `ApiClientBase`, campos nuevos en DTOs de socio y de documentos); socios (`_Form`, `Crear`, `Index`, `sociosNegocio.js`, controlador); `credito-documento.js` compartido y los formularios de pedido, entrega y factura (controladores, vistas, JS).

---

## Fase 1 — API

### Task 1: Script SQL, entidades y repositorio

**Plantilla:** `sql/2026-10-26-condiciones-pago-vencimientos.sql` (estructura idempotente, MERGE de módulos con `WHEN MATCHED ... UPDATE` y sin `NOT MATCHED BY SOURCE`, bloque duplicado al final de `sql/crear-api-db-test-completo.sql`, guarda `THROW` de la fase previa solo en el script individual), commit `a50b97d`/`43da9e1` (entidad, contexto, repositorio, DI, prueba de modelo).

**Precondición:** `git log` contiene los commits de la Fase B; `sql/2026-10-26-condiciones-pago-vencimientos.sql` existe.

**Files:** Create `sql/2026-11-06-limite-credito.sql`, `API.Domain.Entity/Models/AutorizacionCredito.cs`, `API.Infraestructure.Repository/AutorizacionCreditoRepositorio.cs`, `API.Service.WebApi.Tests/Domain/ModeloLimiteCreditoTests.cs`; Modify `API.Domain.Entity/Models/SocioNegocio.cs`, `API.Domain.Entity/Models/ApiDbTestContext.cs`, `sql/crear-api-db-test-completo.sql` (mismo bloque al final), `API.Service.WebApi/Startup.cs` (una línea de DI del repositorio genérico, como los otros).

**Interfaces — Produces:** `SocioNegocio.LimiteCredito` (`decimal?`), `SocioNegocio.BloquearSiVencido` (`string`, por defecto `"N"`); `AutorizacionCredito { int Id; int TipoDocumento; int EntryDocumento; string CodigoSn; string Usuario; string Motivo; decimal? Limite; decimal Saldo; decimal Comprometido; decimal Vencido; decimal Monto; decimal Exceso; DateTime Fecha }` con `IRepositorioGenerico<AutorizacionCredito, int>` registrado.

- [ ] **Step 1: Prueba de modelo (falla).** `ModeloLimiteCreditoTests` (patrón `ModeloCondicionesPagoTests`): `SocioNegocio` con `LimiteCredito` decimal(19,2) nulable y `BloquearSiVencido` con default `'N'`; tabla `AutorizacionCredito` con clave `Id`, longitudes (`Usuario` 50, `Motivo` 20, `CodigoSn` 15), FK a `SocioNegocio` e índices por `(TipoDocumento, EntryDocumento)` y `CodigoSn`.
- [ ] **Step 2: Script SQL idempotente.** (a) `ALTER TABLE ... ADD` de las dos columnas protegidos con `COL_LENGTH(...) IS NULL`; **los `CHECK` y `DEFAULT` que referencian columnas recién agregadas se crean con `EXEC('...')`** (el lote no compila si la columna no existe al analizarlo: lección de la Fase B); (b) tabla `AutorizacionCredito` con `IF OBJECT_ID(...) IS NULL`, constraints nombrados e índices con `IF NOT EXISTS`; (c) módulo `AutorizarExcesoCredito` con el mismo `MERGE` que el script de la Fase B (nombre «Autorizar exceso de crédito», grupo `'Ventas'`, verifica en `sql/2026-10-01-permisos-usuario.sql` que la clave del grupo es exactamente la sembrada). El script individual lleva la guarda `THROW` si falta la tabla `CondicionPago` (Fase B).
- [ ] **Step 3: Entidades, contexto, repositorio y DI** como el commit `43da9e1`; `AutorizacionCredito` sin colecciones inversas obligatorias.
- [ ] **Step 4: Verificar.** Build y `ModeloLimiteCreditoTests` en verde; tests de modelo previos sin regresión. En una base temporal con `crear-api-db-test-completo.sql`: ejecutar el script individual **dos veces**, comprobar columnas, constraints, tabla, índices, módulo (y que el `SocioNegocio` existente conserva `LimiteCredito IS NULL` y `BloquearSiVencido='N'`); eliminar la base.
- [ ] **Step 5: Commit** `feat(limite-credito): script SQL, entidades y repositorio de limite de credito y autorizaciones`.

### Task 2: Límite y bloqueo en el socio de negocio (API)

**Plantilla:** cómo la Fase B agregó `CondicionPago` a `SocioNegocioDomain`, los DTOs de socio y `PerfilMapeo` (commits de la Task 4 de la Fase B).

**Precondición:** Task 1 hecha.

**Files:** Modify `API.Domain.Core/SocioNegocioDomain.cs`, `API.Application.DTO/socioNegocio/{SocioNegocioDTO,SocioNegocioCrearDTO,SocioNegocioActualizarDTO}.cs`, `API.Transversal.Mapper/PerfilMapeo.cs` (solo si el mapeo lo requiere), `API.Service.WebApi.Tests/Domain/SocioNegocioDomainTests.cs` (o el archivo de pruebas de ese dominio).

**Interfaces — Produces:** `SocioNegocioDTO/CrearDTO/ActualizarDTO` con `decimal? LimiteCredito` y `string? BloquearSiVencido` (el de lectura devuelve siempre `"S"`/`"N"`).

- [ ] **Step 1: Pruebas (fallan).** (a) crear cliente con `LimiteCredito = 5000.50` y `BloquearSiVencido = "S"` los guarda; (b) `LimiteCredito = -1` → «El límite de crédito no puede ser negativo.»; (c) `LimiteCredito = 10.123` → «El límite de crédito admite como máximo 2 decimales.»; (d) `LimiteCredito = 0` es válido (solo contado); (e) proveedor (`TipoSn = "P"`) con límite → «El límite de crédito solo aplica a clientes.»; proveedor sin límite pasa; (f) `BloquearSiVencido = "X"` → «El bloqueo por vencido debe ser S o N.»; vacío/nulo → `"N"`; (g) la actualización reemplaza ambos valores (enviar `null` limpia el límite); (h) el mapeo de lectura devuelve los dos campos.
- [ ] **Step 2: Implementar** las validaciones en `SocioNegocioDomain` (insertar y actualizar), los campos en los DTOs y el mapeo.
- [ ] **Step 3: Pruebas en verde y suite completa.**
- [ ] **Step 4: Commit** `feat(limite-credito): limite de credito y bloqueo por vencido en el socio de negocio`.

### Task 3: `CreditoClienteService` — posición y comprometido

**Plantilla:** `SaldoSocioService` y `AntiguedadService` (carga agrupada, anónimos proyectados antes de `ToListAsync`, `Redondear` 2 decimales `AwayFromZero`, `OrdinalIgnoreCase` en códigos), y la lógica de consumo de `DocumentoBaseService.ConsumosAsync` (lo que consume una línea origen: líneas de destinos activos con `BaseTipo`/`BaseEntry`/`BaseLinea`).

**Precondición:** Tasks 1–2 hechas. **Verificación previa (leer antes de codificar, anotar en el reporte):** nombres reales de campos en `PedidoDetalle`, `EntregaDetalle`, `FacturaDetalle` (`Cantidad`, `TotalLinea`, `BaseTipo`, `BaseEntry`, `BaseLinea`) y de las cabeceras (`Cancelado`, `EstadoDoc`, `TotalDoc`, `CodigoSn`); qué destinos pueden consumir pedidos (Entrega y Factura con `BaseTipo` 4) y entregas (Factura con `BaseTipo` 5), siguiendo `DocumentoBaseService.ConsumosAsync`.

**Files:** Create `API.Domain.Interface/ICreditoClienteService.cs`, `API.Domain.Core/CreditoClienteService.cs`, `API.Service.WebApi.Tests/Domain/CreditoClienteServiceTests.cs`; Modify `API.Service.WebApi/Startup.cs` (una línea de DI).

**Interfaces — Produces:**

```csharp
public sealed record PosicionCredito(string CodigoSn, decimal? Limite, bool BloquearSiVencido,
    decimal Saldo, decimal Vencido, decimal Comprometido, decimal Exposicion, decimal? Disponible);

public interface ICreditoClienteService
{
    /// <summary>Posición de crédito del cliente. Socio inexistente: devuelve null.</summary>
    Task<PosicionCredito?> ObtenerPosicionAsync(string codigoSn);
    /// <summary>Posiciones de varios clientes con carga agrupada; los códigos que no son cliente no aparecen.</summary>
    Task<IReadOnlyDictionary<string, PosicionCredito>> ObtenerPosicionesAsync(IEnumerable<string> codigosSn);
    /// <summary>Valor abierto (comprometido) de un pedido o entrega, ignorando si el propio documento está cancelado.
    /// tipo = 4 (Pedido) o 5 (Entrega). Documento inexistente: 0.</summary>
    Task<decimal> ValorAbiertoDocumentoAsync(int tipo, int entry);
    // VerificarAsync y CalcularMontoAsync se agregan en la Task 4.
}
```

**Reglas:** `Saldo` y `Vencido` de **una** llamada a `ISaldoSocioService.ObtenerAsync` con todos los códigos. `Comprometido`: (1) pedidos activos y entregas activas de los clientes (una consulta por tabla); (2) sus líneas (una consulta por tabla, filtradas por `Entry in (...)`); (3) consumos: líneas de `EntregaDetalle` con `BaseTipo=4`, de `FacturaDetalle` con `BaseTipo` 4 o 5, cuyo `BaseEntry` está en los orígenes y cuya cabecera destino está activa (se cargan primero los `Entry` activos de entregas y facturas relacionadas y se filtran las líneas por esos `Entry`); (4) por línea: `abierta = Cantidad − Σ consumos de esa (tipo, entry, noLinea)`; si `abierta <= 0` no cuenta; valor = `abierta / Cantidad × TotalLinea × factor`; `factor = TotalDoc / Σ TotalLinea` del documento origen (1 si la suma es 0); suma por documento redondeada a 2 decimales y total redondeado. `Exposicion = Saldo + Comprometido`. `Limite = null` → `Disponible = null`. `BloquearSiVencido = (socio.BloquearSiVencido == "S")`. Un socio que no es cliente (`TipoSn <> 'C'`) no aparece en `ObtenerPosicionesAsync` y `ObtenerPosicionAsync` devuelve `null`. Entradas vacías devuelven vacío **sin consultar**.

- [ ] **Step 1: Pruebas (fallan)** con mocks de repositorios y `ISaldoSocioService` (helper `AsAsyncQueryable` de las pruebas existentes). Casos con cifras exactas:
  1. Cliente sin documentos y con saldo 300 / vencido 50 → `Comprometido 0`, `Exposicion 300`, `Disponible = Limite − 300`.
  2. Pedido P1 `TotalDoc 1100`, líneas L1 (cantidad 10, `TotalLinea 600`) y L2 (cantidad 5, `TotalLinea 400`) → factor 1.1, `Comprometido 1100`.
  3. Entrega E1 activa copia 4 unidades de L1 (E1: `TotalDoc 264`, una línea cantidad 4, `TotalLinea 240`, `BaseTipo 4`) → P1 abierto = 6/10×600×1.1 + 440 = 836, E1 = 264 → **`Comprometido 1100` (copiar parcial no cambia el total)**.
  4. E1 cancelada (`Cancelado = 'S'`) → vuelve a contar P1 completo: `Comprometido 1100`, E1 no cuenta.
  5. Factura F1 activa copia el resto de E1 (`BaseTipo 5`) → E1 abierto 0; el valor ya no está en comprometido (pasó a saldo).
  6. Pedido cancelado y entrega con `EstadoDoc = 'C'` no cuentan; cotización y orden de servicio no cuentan (ni se consultan).
  7. Línea con `Cantidad 0` no cuenta ni divide entre cero; `Σ TotalLinea = 0` → factor 1.
  8. Redondeo: valores que dan 0.005 se redondean `AwayFromZero`.
  9. Límite `null` → `Disponible null`; límite 0; `BloquearSiVencido "S"` → `true`.
  10. Socio proveedor / inexistente → `null` en posición y ausente en posiciones.
  11. `ObtenerPosicionesAsync` con 3 clientes: **una** llamada a `ISaldoSocioService` y una consulta por tabla (`Times.Once`), códigos duplicados o con distinta capitalización se unifican; entrada vacía no consulta nada.
  12. `ValorAbiertoDocumentoAsync(4, entryCancelado)` devuelve el valor completo del pedido aunque esté cancelado; para un entry inexistente 0; con una entrega activa que consume parte, descuenta lo consumido.
- [ ] **Step 2: Implementar** `CreditoClienteService` (y la línea de DI). Código estructural esperado: una función privada que carga `OrigenValor` por `(tipo, entry)` con `Factor`, líneas y consumos, reutilizada por `ObtenerPosicionesAsync` y `ValorAbiertoDocumentoAsync` y, en la Task 4, por `CalcularMontoAsync`.
- [ ] **Step 3: Pruebas en verde y suite completa** (anotar el total).
- [ ] **Step 4: Commit** `feat(limite-credito): servicio de posicion de credito y comprometido por cliente`.

### Task 4: Verificación del documento nuevo, excepción y contexto de autorización

**Plantilla:** Task 3 (mismo servicio), `ResolutorVencimiento` (servicio de dominio llamado antes de numerar) y `GuardaPagos` (excepciones de negocio con mensaje de usuario).

**Precondición:** Task 3 hecha.

**Files:** Modify `API.Domain.Interface/ICreditoClienteService.cs`, `API.Domain.Core/CreditoClienteService.cs`, `API.Service.WebApi.Tests/Domain/CreditoClienteServiceTests.cs`, `API.Service.WebApi/Startup.cs`; Create `API.Domain.Interface/CreditoExcedidoException.cs`, `API.Domain.Interface/IAutorizacionCreditoContexto.cs`, `API.Domain.Core/AutorizacionCreditoContexto.cs`, `API.Domain.Core/AutorizacionCreditoRegistro.cs` (ayuda para construir la fila), `API.Service.WebApi.Tests/Domain/AutorizacionCreditoTests.cs`.

**Interfaces — Produces:**

```csharp
public sealed record ResultadoCredito(bool Permitido, string? Motivo /* "LIMITE" | "VENCIDO" | null */,
    PosicionCredito? Posicion, decimal Monto, decimal Exceso);

// se agrega a ICreditoClienteService
Task<decimal> CalcularMontoAsync(int tipoDestino, decimal totalDoc, decimal pagoInmediato, IReadOnlyList<LineaBaseRequest> lineasBase);
Task<ResultadoCredito> VerificarAsync(string? codigoSn, decimal monto);

public sealed class CreditoExcedidoException : Exception
{
    public ResultadoCredito Resultado { get; }
    public string Codigo => Resultado.Motivo == "VENCIDO" ? "CLIENTE_CON_VENCIDO" : "LIMITE_CREDITO_EXCEDIDO";
    public CreditoExcedidoException(ResultadoCredito resultado, string mensaje) : base(mensaje) { Resultado = resultado; }
}

public interface IAutorizacionCreditoContexto
{
    bool Autorizada { get; set; }
    string? Usuario { get; set; }
}

public static class AutorizacionCreditoRegistro
{
    /// <summary>Fila de AutorizacionCredito para el documento ya numerado (tipo 4/5/6 y entry).</summary>
    public static AutorizacionCredito Crear(int tipo, int entry, string codigoSn, string usuario, ResultadoCredito r);
}
```

`AutorizacionCreditoContexto` es la implementación trivial (propiedades) registrada **scoped**. Los mensajes de `CreditoExcedidoException` son los de Global Constraints; un método estático auxiliar `CreditoExcedidoException.Para(ResultadoCredito r, string codigoSn)` los arma.

**Reglas:** `CalcularMontoAsync` = `max(0, round2(totalDoc − pagoInmediato − liberado))` con `liberado` = Σ por línea base con `BaseTipo` 4 o 5 de `Cantidad × (TotalLinea / Cantidad origen) × factor del origen`, usando la misma carga de `OrigenValor` (una consulta por tabla; sin líneas base no consulta). `VerificarAsync` aplica el orden de Global Constraints usando `ObtenerPosicionAsync(codigoSn)`; si `codigoSn` es nulo/vacío o `ObtenerPosicionAsync` devuelve `null` → `Permitido = true` con `Posicion = null` y no consulta saldos. `Monto = 0` → permitido sin consultar saldos. `Exceso` según el caso (`VENCIDO`: `Monto`; `LIMITE`: `Exposicion + Monto − Limite`).

- [ ] **Step 1: Pruebas (fallan).** Verificación: (a) sin límite → permitido; (b) límite 1000, saldo 300, comprometido 500, monto 200 → exposición final 1000 → **permitido (no es `>`)**; monto 201 → `LIMITE`, `Exceso 1`; (c) límite 0 y monto 0.01 → `LIMITE`; límite 0 y monto 0 → permitido; (d) `BloquearSiVencido` con vencido 10 → `VENCIDO`, `Exceso = monto`; con vencido 0 → no bloquea; con `monto 0` y vencido → permitido; interruptor apagado y vencido alto → no bloquea (solo importa el límite); (e) vencido tiene prioridad sobre límite cuando ambos fallan; (f) socio inexistente/proveedor → permitido sin consultar saldos (`Times.Never` en `ISaldoSocioService`). Monto: (g) factura sin base `TotalDoc 500`, `pagoInmediato 0` → 500; con `pagoInmediato 500` → 0; (h) factura con una línea base de entrega E (valor unitario neto 66 por unidad, cantidad 4) y `TotalDoc 264` → `liberado 264`, monto 0; (i) entrega desde pedido: `TotalDoc 264`, liberado 264 → 0; con precio mayor en la entrega (`TotalDoc 300`) → monto 36; (j) base 3 (cotización) y 70 no liberan; (k) `max(0, …)`: documento menor que lo liberado → 0. Excepción y contexto: (l) `CreditoExcedidoException.Codigo` y los mensajes literales de Global Constraints; (m) `AutorizacionCreditoRegistro.Crear` copia cifras, usuario, motivo y tipo/entry; (n) el contexto es scoped (dos resoluciones en el mismo scope son la misma instancia, en scopes distintos no).
- [ ] **Step 2: Implementar** lo anterior y las líneas de DI (`AddScoped<IAutorizacionCreditoContexto, AutorizacionCreditoContexto>()`).
- [ ] **Step 3: Pruebas en verde y suite completa.**
- [ ] **Step 4: Commit** `feat(limite-credito): verificacion de limite, monto neto del documento y contexto de autorizacion`.

### Task 5: Integración en el pedido (alta con líneas, reapertura y cambio de socio)

**Plantilla:** cómo `FacturaDomain.InsertarAsync` llama al `ResolutorVencimiento` antes de numerar (Fase B) y `PedidoDomain.ActualizarAsync` (`seReabre`, `cambiaSocio`).

**Precondición:** Tasks 3–4 hechas. **Verificación previa:** relee `PedidoDomain.InsertarAsync(Pedido, IEnumerable<PedidoDetalle>)` y `ActualizarAsync`; confirma el uso de `_tx.EjecutarAsync` (Save #1 asigna `Entry`, Save #2 guarda líneas) y cómo agregar una fila al mismo contexto con `AgregarSinGuardarAsync` del repositorio genérico.

**Files:** Modify `API.Domain.Core/PedidoDomain.cs`, `API.Application.DTO/pedido/{PedidoCrearDTO,PedidoActualizarDTO}.cs` (campo `bool AutorizarExcesoCredito`), `API.Transversal.Mapper/PerfilMapeo.cs` (ignorar `AutorizarExcesoCredito`), `API.Service.WebApi.Tests/Domain/PedidoDomainTests.cs` (o el archivo existente de ese dominio); Create `API.Service.WebApi.Tests/Domain/PedidoDomainCreditoTests.cs`.

**Interfaces — Consumes:** `ICreditoClienteService` (Task 4), `IAutorizacionCreditoContexto`, `IRepositorioGenerico<AutorizacionCredito,int>`, `CreditoExcedidoException`, `AutorizacionCreditoRegistro`. **Produces:** `PedidoDomain` con las dependencias nuevas en el constructor (actualizar los constructores de las pruebas existentes: solo cambia la firma).

**Reglas:**
- `InsertarAsync(Pedido, IEnumerable<PedidoDetalle>)`: después de `ValidarLineasAsync` y antes de `AsignarNumeroAsync`, calcular `monto = CalcularMontoAsync(4, obj.TotalDoc ?? 0, 0, basadas)` y `VerificarAsync(obj.CodigoSn, monto)`. Si no es permitido: sin autorización → lanzar `CreditoExcedidoException`; con `contexto.Autorizada` y `Usuario` no vacío → continuar y, **dentro de la transacción**, tras el Save #1 (con `Entry` asignado) agregar `AutorizacionCreditoRegistro.Crear(4, creado.Entry, ...)` con `AgregarSinGuardarAsync`.
- `InsertarAsync(Pedido obj)` (sin líneas): **no se verifica** (no compromete nada; decisión 6).
- `ActualizarAsync`: cuando `seReabre` o `cambiaSocio` y el pedido tiene líneas, tras las validaciones existentes: `monto = ValorAbiertoDocumentoAsync(4, id)` y `VerificarAsync(obj.CodigoSn, monto)`; mismo tratamiento de autorización; con autorización, la fila se guarda junto con la actualización en la misma transacción (usar `_tx.EjecutarAsync` alrededor de la actualización y el alta de la fila). Cancelar o actualizar sin reabrir ni cambiar de socio **no** verifica.

- [ ] **Step 1: Pruebas (fallan).** (a) crear con líneas y verificación permitida → crea y no registra autorización; (b) rechazo con `LIMITE` → lanza `CreditoExcedidoException`, **`SigNumero` sin cambio y ningún insert** (`Times.Never` en el repositorio de pedidos); (c) rechazo con contexto autorizado → crea y registra **una** fila con tipo 4, el `Entry` nuevo, usuario, motivo y cifras; (d) contexto autorizado pero sin exceso → no registra; (e) contexto con `Autorizada = true` y `Usuario` vacío → se trata como no autorizado y lanza; (f) alta sin líneas no llama al servicio; (g) pedido copiado de cotización (base 3) usa `lineasBase` en `CalcularMontoAsync`; (h) reabrir un pedido cancelado con líneas verifica con `ValorAbiertoDocumentoAsync`; rechazo → lanza y el estado no cambia; con autorización → reabre y registra; (i) cambiar de socio sin líneas copiadas verifica al nuevo socio; (j) cancelar no verifica; (k) actualizar solo el comentario no verifica.
- [ ] **Step 2: Implementar** y actualizar mapeos/constructores existentes.
- [ ] **Step 3: Pruebas en verde y suite completa.**
- [ ] **Step 4: Commit** `feat(limite-credito): control de credito al crear, reabrir o cambiar de socio un pedido`.

### Task 6: Integración en entrega y factura (incluye `PagoInmediato`)

**Plantilla:** Task 5 (mismo patrón), `EntregaDomain.InsertarAsync` y `FacturaDomain.InsertarAsync`.

**Precondición:** Task 5 hecha.

**Files:** Modify `API.Domain.Core/{EntregaDomain,FacturaDomain}.cs`, `API.Application.DTO/entrega/EntregaCrearDTO.cs` (`bool AutorizarExcesoCredito`), `API.Application.DTO/factura/FacturaCrearDTO.cs` (`bool AutorizarExcesoCredito`, `decimal? PagoInmediato`), `API.Transversal.Mapper/PerfilMapeo.cs` (ignorar `AutorizarExcesoCredito` y `PagoInmediato` al mapear a la entidad), pruebas de ambos dominios; Create `API.Service.WebApi.Tests/Domain/EntregaDomainCreditoTests.cs`, `API.Service.WebApi.Tests/Domain/FacturaDomainCreditoTests.cs`.

**Interfaces — Consumes:** igual que la Task 5. **Produces:** firma `FacturaDomain.InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas, decimal pagoInmediato = 0m)` **o** una propiedad no mapeada que lleve el valor (elige la opción con menos cambios en las pruebas existentes y documéntala en el reporte; los llamadores existentes siguen compilando).

**Reglas:** `EntregaDomain.InsertarAsync`: con `tipoDestino = 5`, `monto = CalcularMontoAsync(5, TotalDoc, 0, basadas)` y `VerificarAsync(CodigoSn, monto)`, después de validar las líneas base y antes de numerar; autorización registrada con tipo 5. `FacturaDomain.InsertarAsync` (solo `DocSubType '--'`, las notas de débito `ND` no se controlan y nunca llaman al servicio): después de validar líneas base y de resolver el vencimiento y **antes de numerar**, `monto = CalcularMontoAsync(6, TotalDoc, pagoInmediato, basadas)`; `pagoInmediato > TotalDoc` → «El pago inmediato no puede exceder el total del documento.» (antes de verificar crédito); autorización registrada con tipo 6. Un rechazo no consume consecutivo en ninguno de los dos. `NotaDebitoDomain` no cambia.

- [ ] **Step 1: Pruebas (fallan)** equivalentes a (a)–(e) de la Task 5 para entrega y factura (tipo 5 y 6 en el registro), más: (f) **factura de contado pagada completa (`PagoInmediato = TotalDoc`) con límite 0 → permitida** (monto 0) y `VerificarAsync` no consulta saldos; (g) factura con límite 0 y `PagoInmediato = 0` → rechazada; (h) pago parcial: `TotalDoc 500`, `PagoInmediato 200`, monto 300 → verifica con 300; (i) `PagoInmediato > TotalDoc` → excepción de negocio sin consumir consecutivo; (j) factura copiada de entrega (base 5): `liberado` evita el doble conteo (monto 0 → permitida aunque el cliente esté en su límite); (k) factura de pedido directo (base 4) igual; (l) nota de débito `ND` no verifica (`Times.Never`); (m) factura creada con autorización registra una sola fila con tipo 6 y el `Entry` de la factura.
- [ ] **Step 2: Implementar.**
- [ ] **Step 3: Pruebas en verde y suite completa.**
- [ ] **Step 4: Commit** `feat(limite-credito): control de credito al crear entregas y facturas de venta`.

### Task 7: Aplicación y controladores (409, 403, posición del cliente)

**Plantilla:** Tarea 8 de la Fase B (`EstadoCuentaApplication`, `EstadoCuentaController`, DTOs, pruebas de aplicación, controlador y permisos por reflexión) y `PermisoUsuario.Tiene` (ya usado dentro de acciones, p. ej. cancelar exige Eliminar).

**Precondición:** Tasks 5–6 hechas.

**Files:** Create `API.Application.DTO/creditoCliente/{PosicionCreditoDTO,CreditoExcedidoDTO}.cs`, `API.Application.DTO/RespuestaCredito.cs`, `API.Application.Interface/ICreditoClienteApplication.cs`, `API.Application.Main/CreditoClienteApplication.cs`, `API.Service.WebApi/Controllers/CreditoClienteController.cs`, `API.Service.WebApi.Tests/Application/CreditoClienteApplicationTests.cs`, `API.Service.WebApi.Tests/Controllers/{CreditoClienteControllerTests,CreditoClientePermisosTests,AutorizacionCreditoControllersTests}.cs`; Modify `API.Application.Main/{PedidoApplication,EntregaApplication,FacturaApplication}.cs`, `API.Service.WebApi/Controllers/{PedidoController,EntregaController,FacturaController}.cs`, `API.Service.WebApi/Startup.cs`.

**Interfaces — Produces:**

```csharp
public class RespuestaCredito<T> : Respuesta<T> { public CreditoExcedidoDTO? Credito { get; set; } }
public class CreditoExcedidoDTO { public string Codigo; public decimal? Limite; public decimal Saldo; public decimal Comprometido;
    public decimal Vencido; public decimal Monto; public decimal Exceso; public decimal? Disponible; }
public class PosicionCreditoDTO { public string CodigoSn; public decimal? Limite; public bool BloquearSiVencido; public decimal Saldo;
    public decimal Vencido; public decimal Comprometido; public decimal Exposicion; public decimal? Disponible; public decimal? PorcentajeUso; }
```

(`PorcentajeUso = Exposicion / Limite × 100` redondeado a 1 decimal, `null` si no hay límite o el límite es 0.) JSON en camelCase como el resto de la API.

**Rutas y permisos:** `GET api/CreditoCliente/{codigoSn}` (`RequierePermiso("EstadoCuenta", Accion.Ver)`) → `Respuesta<PosicionCreditoDTO>`, socio inexistente o que no es cliente → 404 con el mensaje «El cliente no existe.»; `POST api/CreditoCliente/Posiciones` (mismo permiso) con cuerpo `["C001","C002"]` → lista de `PosicionCreditoDTO` ordenada por `CodigoSn`; nulo o vacío → lista vacía; más de 500 códigos → 400 «Demasiados socios en la consulta.»; blancos ignorados, trimeados y duplicados colapsados (igual que `EstadoCuentaApplication.ObtenerSaldosAsync`).

**Reglas de las acciones de creación** (`PedidoController` alta con líneas y `PUT` que reabre, `EntregaController` alta, `FacturaController` alta): antes de llamar a la aplicación, si `AutorizarExcesoCredito` es `true`: `PermisoUsuario.Tiene(User, "AutorizarExcesoCredito", Accion.Ver)` → si no, `StatusCode(403)` con `Respuesta<object>{ Mensaje = "No tenés permiso para autorizar excesos de crédito." }` **sin llamar a la aplicación**; si sí, `contexto.Autorizada = true` y `contexto.Usuario = User` (nombre de usuario del claim que ya usan otros controladores para auditoría; confirmar cuál en el código). En `FacturaController`: si `PagoInmediato > 0` y no `PermisoUsuario.Tiene(User, "PagosRecibidos", Accion.Agregar)` → 403 «No tenés permiso para registrar pagos.». `FacturaApplication.InsertarAsync` pasa `obj.PagoInmediato ?? 0m` al dominio (Task 6) y lo valida `>= 0` («El pago inmediato no puede ser negativo.»). Las aplicaciones capturan `CreditoExcedidoException` **antes** del `catch (Exception)` genérico y devuelven `RespuestaCredito<int>` (o `<bool>` en la actualización) con `Resultado = false`, `Mensaje = ex.Message`, `Dato` por defecto y `Credito` armado desde `ex.Resultado`/`ex.Codigo`; los controladores, si el resultado es `RespuestaCredito<>` con `Credito != null`, responden `Conflict(respuesta)` (409) en lugar de `BadRequest`. Todo otro error sigue siendo 400.

- [ ] **Step 1: Pruebas (fallan).** Aplicación: mapeo de posición y porcentaje (`3000 / 5000 → 60.0`, límite 0 → `null`), 404 de cliente inexistente, lista de posiciones (límite 500, depuración, orden), `CreditoExcedidoException` → `RespuestaCredito` con código y cifras, otro error → `Respuesta` normal. Controlador: 200/404 de la posición, 409 con cuerpo (`credito.codigo`, cifras), 400 en otros errores, 403 al pedir autorizar sin el permiso (**la aplicación no se invoca**), 403 de `PagoInmediato` sin permiso, con permiso el contexto queda `Autorizada = true` y `Usuario` correcto (verificar con una instancia real de `AutorizacionCreditoContexto`), `AutorizarExcesoCredito = false` no toca el contexto. Permisos por reflexión: las dos acciones de `CreditoClienteController` con `EstadoCuenta`/Ver y rutas absolutas (`~/api/CreditoCliente/{codigoSn}` y `~/api/CreditoCliente/Posiciones`), sin permiso a nivel de clase.
- [ ] **Step 2: Implementar** y registrar `ICreditoClienteApplication` (una línea en `Startup.cs`).
- [ ] **Step 3: Pruebas en verde y suite completa** (anotar el total).
- [ ] **Step 4: Commit** `feat(limite-credito): API de posicion de credito, respuesta 409 y autorizacion de excesos`.

---

## Fase 2 — Web

### Task 8: DTOs, clientes tipados y `RespuestaCredito`

**Plantilla:** Web commit `f7564c1` (DTOs y clientes de la Fase B) y `ApiClientBase`.

**Precondición:** API Tareas 1–7 hechas; Web `git status` solo con los 9 ajenos.

**Files:** Create `Web.ApiClient/Dtos/CreditoCliente/{PosicionCreditoDTO,CreditoExcedidoDTO}.cs`, `Web.ApiClient/Dtos/RespuestaCredito.cs`, `Web.ApiClient/Clientes/{ICreditoClienteApiClient,CreditoClienteApiClient}.cs`; Modify `Web.ApiClient/ApiClientBase.cs` (solo **agregar** un método), `Web.ApiClient/Dtos/SocioNegocio/SocioNegocioDTO.cs`, `Web.ApiClient/Dtos/SocioNegocio/{SocioNegocioCrearDTO,SocioNegocioActualizarDTO}.cs` (procedimiento seguro), `Web.ApiClient/Dtos/{Pedido/PedidoCrearDTO,Pedido/PedidoActualizarDTO,Entrega/EntregaCrearDTO,Factura/FacturaCrearDTO}.cs`, los clientes `Pedido`, `Entrega` y `Factura` (el método de alta/actualización devuelve `RespuestaCredito<int>` / `<bool>`), `Web.UI/Program.cs` (registro de `ICreditoClienteApiClient`).

**Interfaces — Produces:** `RespuestaCredito<T> : Respuesta<T> { CreditoExcedidoDTO? Credito }` (clase nueva en archivo nuevo; **no** se edita `Respuesta.cs`); `ApiClientBase.PostConCreditoAsync<T>(string url, object body)` y `PutConCreditoAsync<T>` que parsean además la propiedad `credito` del JSON cuando existe (la respuesta 409 no es excepción: `EnviarAsync` ya lee el cuerpo de cualquier estado excepto 401/403); `ICreditoClienteApiClient { Task<Respuesta<PosicionCreditoDTO>> ObtenerAsync(string codigoSn); Task<Respuesta<List<PosicionCreditoDTO>>> ObtenerPosicionesAsync(IEnumerable<string> codigosSn); }`; campos nuevos en DTOs: socio (`decimal? LimiteCredito`, `string? BloquearSiVencido` en lectura/creación/actualización), `bool AutorizarExcesoCredito` en `PedidoCrearDTO`, `PedidoActualizarDTO`, `EntregaCrearDTO`, `FacturaCrearDTO`, y `decimal? PagoInmediato` en `FacturaCrearDTO`.

**Reglas:** los DTOs de socio de la Web con cambios ajenos se amplían con el **procedimiento seguro**: (1) copias de seguridad de ambos archivos y `git diff` guardado en el scratchpad; (2) editar agregando solo las propiedades nuevas con su comentario; (3) en el commit, stagear **solo el hunk propio** con `git diff -U0 <archivo>` filtrado y `git apply --cached --unidiff-zero`, comprobar con `git diff --cached -- <archivo>` que solo aparecen las propiedades nuevas y que `git diff -- <archivo>` todavía muestra los hunks ajenos; (4) si no se puede separar, no commitear esos dos archivos y reportarlo como concern (los campos del formulario de socio de la Task 9 quedan entonces pendientes). `ApiClientBase.cs` se edita solo agregando métodos (sin cambiar los existentes). `CreditoClienteApiClient.ObtenerPosicionesAsync` parte en bloques de 500 si hace falta y une las listas.

- [ ] **Step 1:** crear DTOs y clientes, ampliar DTOs existentes, registrar el cliente.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores (0 errores; los 4 avisos de línea base están en archivos ajenos).
- [ ] **Step 3:** build del HEAD commiteado en un worktree temporal bajo el scratchpad, y retirarlo (`git worktree remove`).
- [ ] **Step 4: Commit** `feat(limite-credito): DTOs y clientes tipados de credito, autorizacion y respuesta 409` con rutas explícitas; `git status --short` termina con exactamente los 9 ajenos.

### Task 9: Socios de negocio — límite, bloqueo, ficha y listado

**Plantilla:** cómo la Fase B agregó la condición de pago y el saldo/vencido a `Views/SociosNegocio/{_Form,Crear,Index}.cshtml`, `SociosNegocioController.cs` y `sociosNegocio.js` (Web commit `99d68fd` y `51b944e`: permiso del lado servidor, una sola llamada con bloques de 500, degradación silenciosa, mapa de saldos `OrdinalIgnoreCase`).

**Precondición:** Task 8 hecha.

**Files:** Modify `Web.UI/Controllers/SociosNegocioController.cs`, `Web.UI/Views/SociosNegocio/{_Form,Crear,Index}.cshtml`, `Web.UI/wwwroot/js/sociosNegocio.js`.

**Reglas:** (1) pestaña/sección Comercial del alta y de la edición: campo numérico «Límite de crédito» (`min 0`, `step 0.01`, vacío = sin límite, leyenda «Vacío = sin límite; 0 = solo contado») y casilla/selector «Bloquear si tiene saldo vencido» (`S`/`N`, por defecto `N`), visibles solo para clientes (`TipoSn = 'C'`; ocultar y enviar vacío/`N` para proveedores); la edición **siempre** envía ambos valores actuales; (2) ficha: bloque «Crédito» (límite o «Sin límite», saldo, comprometido, vencido, disponible, barra de uso: verde < 80 %, ámbar 80–100 %, roja al exceder) que se carga con `GET api/CreditoCliente/{codigo}` **solo con `Ver` de `EstadoCuenta` verificado en el servidor**; sin permiso no se pide ni se muestra; (3) listado: columnas «Límite» y una insignia roja «Excedido» cuando `Exposicion > Limite`, con **una sola llamada** a `POST api/CreditoCliente/Posiciones` (bloques de 500) para el conjunto mostrado, sin llamar si no hay permiso, ocultando columnas si falla o no hay datos (mismo mecanismo que Saldo/Vencido; conservar índices de columnas y la fila de acciones); (4) texto escapado; importes con 2 decimales y separador de miles como las demás columnas.

- [ ] **Step 1:** implementar siguiendo las plantillas.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores y `node --check` de `sociosNegocio.js`; build del HEAD en un worktree temporal.
- [ ] **Step 3: Commit** `feat(limite-credito): limite de credito, bloqueo por vencido y posicion de credito en socios de negocio` (los dos DTOs de socio ya se commitearon en la Task 8).

### Task 10: Formularios de factura, entrega y pedido — disponible, aviso y autorización

**Plantilla:** el módulo `condicionpago-documento.js` de la Fase B (JS compartido por los cuatro documentos, `inicializar`/`onSeleccion`), la sección de pago al facturar de `facturas-pago.js`/`FacturasController` (suma de formas de pago) y el flujo de errores de `App.enviarJson`.

**Precondición:** Tasks 8–9 hechas.

**Files:** Create `Web.UI/wwwroot/js/credito-documento.js`; Modify `Web.UI/Controllers/{FacturasController,EntregasController,PedidosController}.cs`, `Web.UI/Views/{Facturas,Entregas,Pedidos}/{_Form,Index}.cshtml` (referencia del script y el contenedor «Disponible»), `Web.UI/wwwroot/js/{facturas,entregas,pedidos,facturas-pago}.js`.

**Reglas:**
- `credito-documento.js` expone `CreditoDoc.inicializar({ tipo })`, `CreditoDoc.alElegirSocio(codigoSn)`, `CreditoDoc.avisarTotal(total)` y `CreditoDoc.manejar409(respuesta, reintentar)`. Al elegir el cliente (hook `onSeleccion` existente) pide `GET /…/CreditoCliente/{codigo}` a través de una acción del controlador de la Web **protegida con `EstadoCuenta` Ver** (nueva acción `PosicionCredito` en cada controlador de documento o en un controlador común: elegir la opción con menos duplicación) y muestra «Disponible: X» o «Sin límite»; si el total del documento supera el disponible o el cliente tiene `BloquearSiVencido` con vencido > 0, muestra un aviso **informativo** (el control real es el de la API); sin permiso no se consulta ni se muestra nada.
- **Al guardar:** si la respuesta es un 409 (`respuesta.credito` presente), muestra un diálogo SweetAlert2 con las cifras (límite, saldo, comprometido, monto, exceso; o «tiene saldo vencido de X») y los textos del servidor escapados. Si el usuario tiene `AutorizarExcesoCredito` (bandera calculada en el servidor de la Web con `User.TienePermiso("AutorizarExcesoCredito", Accion.Ver)` y entregada en `ViewBag`/JSON de datos del formulario), el botón «Autorizar y guardar» reenvía **el mismo documento** con `AutorizarExcesoCredito = true`; si no, solo el mensaje. Nunca se envía `AutorizarExcesoCredito = true` sin pasar por ese diálogo.
- **Facturas:** la acción de alta de la Web envía `PagoInmediato` = Σ de los montos de formas de pago capturados en la sección de pago al facturar (0 si no hay), antes de crear la factura; después sigue el flujo existente que registra el pago. Si el 409 se autoriza, se reenvía con el mismo `PagoInmediato`. El diálogo explica «El pago inmediato se descontó del monto a controlar».
- El backend de la Web reenvía el cuerpo del 409 al JS (la acción de alta de cada controlador devuelve el `RespuestaCredito` serializado con su `credito`). Los estados 401/403 que `ApiClientBase` convierte en «Tu sesión expiró o no tienes permisos» se muestran tal cual.
- Pedidos y entregas creados mediante «Copiar a» ya traen su base; el mismo flujo de 409/autorización aplica.

- [ ] **Step 1:** implementar el helper y la integración en los tres documentos con el mismo patrón.
- [ ] **Step 2:** `dotnet build Web.slnx` sin errores, `node --check` de los JS editados y del nuevo; build del HEAD en un worktree temporal.
- [ ] **Step 3: Commit** `feat(limite-credito): disponible, aviso y autorizacion de exceso de credito en los formularios de venta`.

---

## Fase 3 — Verificación integral

### Task 11: Prueba de humo contra SQL Server real

**Plantilla:** `smoke-vencimientos.ps1` (helpers, estilo `OK`/`FALLA`, fixtures por API, usuarios con plantilla de permisos), `evid-up.ps1`/`evid-down.ps1`.

**Precondición:** Tareas 1–10 hechas y aprobadas.

**Files:** (scratchpad, no se commitea) `smoke-limite-credito.ps1`.

- [ ] **Step 1:** `evid-up.ps1 -NombreDb API_DB_EVID<n>` (n libre; listar antes las bases `API_DB_EVID%` existentes y no tocar las ajenas).
- [ ] **Step 2: Guion `smoke-limite-credito.ps1`** (`OK`/`FALLA`, termina con «Todas las verificaciones pasaron»), mínimo **40 comprobaciones**: login y 401; socio cliente con `LimiteCredito` 1000 y otro sin límite; validaciones de socio (negativo, 3 decimales, proveedor con límite, `BloquearSiVencido` inválido); `GET api/CreditoCliente/{c}` con cifras exactas tras una factura abierta (saldo, comprometido 0, disponible); factura que cabe (exposición final = límite exacto, permitida) y una que excede por 1 centavo (409 con `credito.codigo = LIMITE_CREDITO_EXCEDIDO`, cifras exactas y **SigNumero sin cambio**); cliente sin límite nunca bloquea; **cliente con límite 0**: factura de crédito rechazada y factura de contado con `PagoInmediato = TotalDoc` permitida; `PagoInmediato > TotalDoc` → 400; pedido P1 que cabe → `comprometido = valor del pedido`; entrega parcial copiada desde P1 → **comprometido total sin cambio**; factura desde la entrega → el valor pasa de comprometido a saldo (exposición total sin cambio); cancelar la entrega devuelve el comprometido al pedido; pedido que excede → 409; reabrir un pedido cancelado que excede → 409; cambio de socio de un pedido libre hacia un cliente sin espacio → 409; **bloqueo por vencido**: cliente con `BloquearSiVencido = 'S'` y una factura vencida (documento con `FechaDoc` pasada y condición `CON`) → 409 `CLIENTE_CON_VENCIDO`; con el interruptor apagado pasa; autorización: usuario **sin** `AutorizarExcesoCredito` que envía `AutorizarExcesoCredito = true` → 403 y ningún documento creado; usuario **con** el permiso → documento creado y **una** fila en `AutorizacionCredito` con motivo, cifras, tipo y `Entry` correctos (consulta SQL); autorización sin exceso no registra; usuario sin `PagosRecibidos` Agregar que envía `PagoInmediato > 0` → 403; `POST api/CreditoCliente/Posiciones` (varios clientes, orden, lista vacía, >500 → 400); permisos: usuario sin `EstadoCuenta` Ver → 403 en las dos rutas de `CreditoCliente`; entregas y facturas copiadas desde su base no se bloquean aunque el cliente esté en su límite (monto neto 0); nota de débito de cliente no se controla; un `POST` de factura sin la bandera y sin límite sigue funcionando como antes (compatibilidad). Usuarios de prueba creados como en `smoke-vencimientos.ps1` (plantilla con y sin `AutorizarExcesoCredito`).
- [ ] **Step 3:** correr el guion y guardar la salida en un archivo nuevo `evid-limite-credito-<timestamp>.txt` (todas OK). Luego correr `smoke-pagos-final.ps1` (54), `smoke-pagos-efectuados.ps1` (51) y `smoke-vencimientos.ps1` (122) sobre un entorno limpio y guardar cada salida en archivos nuevos. Si una comprobación falla: causa raíz (no parche); si es defecto del producto, se detiene y se reporta con detalle (nombre, petición/respuesta, esperado/real, archivo sospechoso).
- [ ] **Step 4:** dejar el entorno corriendo para la Task 12 y reportar base, puertos, PIDs y usuarios.

### Task 12: Recorrido en navegador y limpieza

**Plantilla:** Task 17b de la Fase B (puppeteer-core en `<scratchpad>\pupmod`, helper de login propio, capturas reales vistas con Read).

**Precondición:** Task 11 hecha con el entorno corriendo.

**Files:** (scratchpad, no se commitea) capturas en `<scratchpad>\docfigs\fig-limite-credito-NN-*.png`.

- [ ] **Step 1: Recorrido** (admin salvo indicación): 01 alta/edición de socio con límite y bloqueo (leyendas visibles; proveedor sin esos campos); 02 ficha con bloque «Crédito» y barra de uso; 03 listado con columnas «Límite» y la insignia «Excedido»; 04 formulario de factura: elegir el cliente muestra «Disponible»; 05 aviso al superar el disponible; 06 guardar → diálogo 409 con cifras; 07 «Autorizar y guardar» (admin) y el documento creado; 08 lo mismo para un pedido y para una entrega; 09 cliente con límite 0: factura de contado con pago al facturar pasa; 10 cliente con bloqueo por vencido; 11 usuario sin `AutorizarExcesoCredito`: el diálogo solo muestra el mensaje, sin botón; 12 usuario sin `EstadoCuenta`: no ve «Disponible», bloque de crédito ni columnas. Consola sin errores (capturar `error`, `pageerror`, `requestfailed` y HTTP ≥ 400 distintos de los 409 esperados). Cada captura se **abre y se verifica**; si muestra error, página en blanco o datos incorrectos, se investiga y se repite o se reporta.
- [ ] **Step 2: Defectos:** no se corrige código de producto en esta tarea; se anotan (página, pasos, esperado/real, archivo sospechoso) y se continúa.
- [ ] **Step 3: Limpieza:** `evid-down.ps1 -NombreDb <base> -Todo` para cada base creada; puertos 5199/5200 libres; ninguna base `API_DB_EVID%` restante (consulta a `sys.databases`); `git status --short` del API solo con `API.Service.WebApi/appsettings.json` y del Web con exactamente los 9 ajenos; **sin push** y nada commiteado.

---

## Notas de despliegue (fuera de las tareas)

- Ejecutar `sql/2026-11-06-limite-credito.sql` en `API_DB_TEST` (idempotente; requiere la Fase B) y asignar el módulo `AutorizarExcesoCredito` a las plantillas de permisos que deban poder autorizar excesos. Sin esa asignación nadie (salvo SuperUsuario) puede autorizar.
- Revisar `SELECT DISTINCT Grupo FROM dbo.Modulo` antes de ejecutar (el módulo nuevo usa el grupo `Ventas`).
- Todos los clientes quedan **sin límite** al desplegar; el control empieza socio por socio al asignar un límite o activar «Bloquear si tiene saldo vencido».
- Para ver «Disponible» y el bloque de crédito, el usuario necesita `EstadoCuenta` / Ver; para usar pago al facturar con control de crédito, `PagosRecibidos` / Agregar.
- Riesgos aceptados: concurrencia (dos documentos casi simultáneos del mismo cliente pueden pasar ambos la verificación); `PagoInmediato` es una declaración del cliente de la API; el alta de pedido sin líneas y los endpoints sueltos de líneas no están controlados; la estimación del comprometido puede diferir en centavos de `TotalDoc`.
