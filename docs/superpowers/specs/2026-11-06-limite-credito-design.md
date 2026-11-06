# Límite de crédito de clientes — Diseño (Fase C de cuentas por cobrar / por pagar)

**Fecha:** 2026-11-06
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Documentos hermanos:** `2026-10-25-pagos-efectuados-design.md` (Fase A) y `2026-10-26-vencimientos-estado-cuenta-antiguedad-design.md` (Fase B, ya construida: `ISaldoSocioService` entrega saldo y vencido por socio). Este diseño es la **Fase C**, la última de la hoja de ruta.

## Objetivo

Que el sistema **impida vender a crédito a un cliente más allá de lo que se le autorizó**, como el *Credit Limit* de SAP B1: cada cliente tiene un límite; al crear un pedido, una entrega o una factura de venta se compara su exposición (lo que debe más lo que ya se le comprometió) contra ese límite; si se excede, el documento se rechaza salvo que un usuario con permiso especial lo autorice, y la autorización queda registrada.

### Decisiones tomadas (brainstorming)

1. **Bloquea, salvo autorización.** Si el documento nuevo haría que la exposición supere el límite, la API lo rechaza con un error identificable. Un usuario con el permiso `AutorizarExcesoCredito` puede reenviarlo con la bandera de autorización; la API verifica ese permiso en el servidor y guarda un registro de la autorización. Quien no tiene el permiso no puede saltarlo ni manipulando la pantalla.
2. **Exposición = saldo abierto + comprometido.** *Saldo abierto* es el de la Fase B (`ISaldoSocioService`: facturas y notas de débito con saldo > 0). *Comprometido* es el valor de las **líneas aún abiertas de pedidos y entregas activos** (no cancelados) que todavía no se han copiado a otro documento. Una línea copiada a un documento destino activo deja de contar en el origen; así nada se cuenta dos veces. Las cotizaciones y las órdenes de servicio no cuentan.
3. **Valor del límite.** Vacío (`NULL`) = sin límite (todos los clientes existentes siguen igual al desplegar); `0` = solo contado (no se le fía nada); `> 0` = límite en la moneda de los documentos.
4. **Saldo vencido.** Cada socio tiene un interruptor `BloquearSiVencido` (apagado por defecto). Encendido, un cliente con **cualquier saldo vencido** no puede recibir pedidos, entregas ni facturas nuevos aunque no haya llegado a su límite; se puede autorizar con el mismo permiso. No depende de que el socio tenga límite.
5. **Alcance de documentos:** pedido, entrega y factura de venta (`Factura` con `DocSubType '--'`). No se controlan cotizaciones, notas de débito ni de crédito, órdenes de servicio ni ningún documento de compra. **Solo clientes**; el límite en proveedores queda fuera.
6. **Una venta de contado totalmente pagada al facturar no se bloquea** nunca (suma 0 a la exposición), ni siquiera con límite 0.

### Fuera de alcance

Límite para proveedores; moneda distinta de la de los documentos (se compara en la moneda del documento, como el resto del sistema); aprobación por un segundo usuario; notificaciones o historial gráfico; control sobre cotizaciones, notas de débito y órdenes de servicio; bloqueo duro de la **edición** (los documentos solo permiten cambiar el comentario o cancelarse, así que editar no aumenta la exposición); control concurrente estricto (ver Riesgos).

## Contexto verificado

- `SocioNegocio` no tiene campos de crédito. El saldo y el vencido por socio los entrega `ISaldoSocioService.ObtenerAsync` (Fase B, registrado en DI).
- `Pedido`, `Entrega` y `Factura` tienen `CodigoSn`, `TotalDoc`, `Cancelado` y `EstadoDoc`; las líneas llevan `BaseTipo/BaseEntry/BaseLinea`. `TipoDocumentoVenta` define los tipos de objeto: Cotización 3, Pedido 4, Entrega 5, Factura 6, Orden de servicio 70.
- `IDocumentoBaseService` / `DocumentoBaseService` ya calcula la cantidad **abierta** de cada línea de un origen restando lo consumido por documentos destino activos (`Consumido`), y valida líneas con base antes de numerar. Un documento es activo si `Cancelado != 'S'` y `EstadoDoc != 'C'`.
- `FacturaDomain.InsertarAsync` fuerza serie, `DocSubType` y numeración, valida las líneas base y resuelve el vencimiento (Fase B) **antes de numerar**. `PedidoDomain` tiene lógica de cancelar y reabrir (`seCancela`/`seReabre`). La factura admite pago al facturar (Fase A): el pago se registra junto con la factura.
- Los permisos de la API son atributos `RequierePermiso(modulo, acción)` sobre las acciones de los controladores; los módulos viven en la tabla `Modulo(Codigo, Nombre, Grupo)` y se asignan a plantillas de permisos. Los grupos en uso incluyen `SociosNegocio`, `Ventas` y `Compras` (la clave sembrada del grupo de socios es `SociosNegocio`).
- Los controladores de creación de pedido, entrega y factura ya existen con su `RequierePermiso(…, Agregar)`.

## §1 — Datos

Script nuevo `sql/2026-11-06-limite-credito.sql` (idempotente) y el mismo bloque al final de `sql/crear-api-db-test-completo.sql`. Se ejecuta después de la Fase B.

- `SocioNegocio.LimiteCredito decimal(19,2) NULL` con `CHECK (LimiteCredito IS NULL OR LimiteCredito >= 0)`.
- `SocioNegocio.BloquearSiVencido nvarchar(1) NOT NULL DEFAULT 'N'` con `CHECK IN ('S','N')`.
- Tabla `AutorizacionCredito`:
  `Id int identity PK`, `TipoDocumento int NOT NULL` (4, 5 o 6), `EntryDocumento int NOT NULL`, `CodigoSn nvarchar(15) NOT NULL` (FK a `SocioNegocio`), `Usuario nvarchar(50) NOT NULL`, `Motivo nvarchar(20) NOT NULL` (`LIMITE` o `VENCIDO`), `Limite decimal(19,2) NULL`, `Saldo`, `Comprometido`, `Vencido`, `Monto`, `Exceso` (todos `decimal(19,2) NOT NULL`), `Fecha datetime NOT NULL DEFAULT GETDATE()`. Índice por `(TipoDocumento, EntryDocumento)` y por `CodigoSn`. Sin borrado.
- Módulo de permisos nuevo `AutorizarExcesoCredito` (nombre «Autorizar exceso de crédito», grupo `Ventas`), insertado con `MERGE` que actualiza nombre y grupo en filas existentes, sin `NOT MATCHED BY SOURCE`. Acceso denegado por defecto; el administrador lo asigna a las plantillas.
- Ninguna fila existente se modifica: todos los clientes quedan sin límite y con `BloquearSiVencido = 'N'`.

## §2 — Reglas de cálculo (dominio)

Servicio `ICreditoClienteService` (`API.Domain.Interface`) con implementación en `API.Domain.Core`, registrado en DI.

```csharp
public sealed record PosicionCredito(string CodigoSn, decimal? Limite, bool BloquearSiVencido,
    decimal Saldo, decimal Vencido, decimal Comprometido, decimal Exposicion, decimal? Disponible);

public sealed record ResultadoCredito(bool Permitido, string? Motivo /* LIMITE | VENCIDO */, PosicionCredito Posicion,
    decimal Monto, decimal Exceso);

public interface ICreditoClienteService
{
    Task<PosicionCredito> ObtenerPosicionAsync(string codigoSn);
    Task<ResultadoCredito> VerificarAsync(string codigoSn, decimal montoNuevo);
}
```

**Posición.**
- `Saldo` y `Vencido` salen de `ISaldoSocioService` (una llamada).
- `Comprometido` = Σ sobre pedidos y entregas **activos** del cliente de `CantidadAbierta × valor unitario neto` por línea, donde el valor unitario neto es el precio de la línea con el descuento y el impuesto de la cabecera del origen, redondeado a 2 decimales (`AwayFromZero`), y `CantidadAbierta = Cantidad − consumido por documentos destino activos`, calculada con la misma lógica de consumo de `DocumentoBaseService` (carga agrupada: una consulta de líneas por tipo y una de consumos; no una por documento). Líneas con `CantidadAbierta <= 0` no cuentan.
- `Exposicion = Saldo + Comprometido`. `Disponible = Limite − Exposicion` si hay límite (puede ser negativo), `NULL` si no.

**Monto nuevo de un documento** (lo calcula cada dominio y lo pasa al servicio): el **aumento neto** de exposición que causa.
- `Monto = TotalDoc − Pagado al crear − Liberado`.
- *Pagado al crear*: el total del pago al facturar (solo factura). Una factura de contado pagada completa da 0.
- *Liberado*: el valor que el documento nuevo **deja de comprometer** en sus orígenes: Σ por línea con `BaseTipo` Pedido (4) o Entrega (5) de `Cantidad × valor unitario neto de la línea origen` (mismo valor que se había contado como comprometido), limitado por la cantidad que realmente consume. Así copiar un pedido a una entrega, o una entrega a una factura, no se cuenta dos veces. Líneas con base Cotización (3) u Orden de servicio (70) no liberan nada porque nunca estuvieron comprometidas.
- `Monto` nunca es menor que 0 para esta verificación.

**Verificación.** `VerificarAsync` evalúa en este orden:
1. Socio inexistente o que no es cliente (`TipoSn != 'C'`) → permitido (el control solo aplica a clientes; la validación del socio ya la hace cada dominio).
2. `Monto = 0` → permitido: un documento que no aumenta la deuda (venta de contado pagada completa, o copia de un pedido a una entrega por el mismo valor) no se bloquea ni por límite ni por vencido.
3. Cliente con `BloquearSiVencido = 'S'` y `Vencido > 0` → no permitido, motivo `VENCIDO` (`Exceso = Monto`).
4. `Limite` no vacío y `Exposicion + Monto > Limite` → no permitido, motivo `LIMITE`, `Exceso = Exposicion + Monto − Limite`.
5. Cualquier otro caso → permitido.

Con límite `0` y `Monto > 0` se rechaza (solo contado).

## §3 — Integración con los documentos (dominio y controlador)

**Dominio.** `PedidoDomain.InsertarAsync`, `EntregaDomain.InsertarAsync` y `FacturaDomain.InsertarAsync` llaman a `ICreditoClienteService.VerificarAsync` **antes de numerar**, después de validar las líneas con base (que ya hace cada uno). Si el resultado no es permitido y no hay autorización, lanzan una excepción de negocio tipificada `CreditoExcedidoException` (mensaje de usuario más el resultado). Una verificación fallida **no consume consecutivo**. La reapertura de un pedido cancelado (`seReabre`) y, donde el dominio lo permita, de una entrega, vuelve a verificar con el valor del documento reabierto como `Monto`.

**Autorización.**
- Los `CrearDTO` de pedido, entrega y factura ganan `AutorizarExcesoCredito` (bool, por defecto `false`); lo mismo el DTO de actualización que reabre un pedido.
- El **controlador** (no el dominio) conoce al usuario: si `AutorizarExcesoCredito` es `true`, verifica con el servicio de permisos existente que el usuario tenga `AutorizarExcesoCredito` (acción Ver); si no, responde 403 sin tocar el dominio. Si lo tiene, pasa la autorización al dominio junto con el nombre de usuario.
- Con autorización válida el dominio crea el documento **y** una fila en `AutorizacionCredito` (motivo, límite, saldo, comprometido, vencido, monto y exceso, usuario y fecha) en la **misma operación** de guardado, de modo que no hay documento autorizado sin registro ni registro sin documento. Si el cliente no excede nada, la bandera se ignora y no se registra nada.

**Respuesta de error.** Cuando el dominio lanza `CreditoExcedidoException`, el controlador responde **HTTP 409** con un cuerpo estable:
`{ "codigo": "LIMITE_CREDITO_EXCEDIDO" | "CLIENTE_CON_VENCIDO", "mensaje": "...", "limite": 5000.00, "saldo": 3000.00, "comprometido": 1500.00, "vencido": 0.00, "monto": 800.00, "exceso": 300.00 }` para que la Web arme el diálogo. Los demás errores de negocio siguen siendo 400.

## §4 — API de consulta y socios

- `GET api/CreditoCliente/{codigoSn}` → `PosicionCreditoDTO` (límite, bloquear si vencido, saldo, vencido, comprometido, exposición, disponible, porcentaje de uso). Permiso: **Ver** de `EstadoCuenta`. Socio inexistente → 404.
- `POST api/CreditoCliente/Posiciones` con la lista de códigos (máx. 500, como `POST api/EstadoCuenta/Saldos`) → lista de posiciones, para el listado de socios.
- `SocioNegocioDTO`, `SocioNegocioCrearDTO` y `SocioNegocioActualizarDTO` ganan `LimiteCredito` (`decimal?`) y `BloquearSiVencido` (`string`, `S`/`N`). Validación: límite `>= 0` con máximo 2 decimales; `BloquearSiVencido` solo `S`/`N` (vacío = `N`). La actualización del socio reemplaza todas las columnas escalares, así que la Web debe enviar siempre los valores actuales. Cambiar el límite exige el permiso de actualizar socios, como cualquier campo del socio.
- Los DTO de los tres documentos y las vistas de lectura no cambian salvo `AutorizarExcesoCredito` en la creación.
- Errores del dominio → 400; 409 solo para el crédito excedido (§3).

## §5 — Web

- **Socio (pestaña Comercial, alta y edición):** campos «Límite de crédito» (vacío = sin límite; leyenda «0 = solo contado») y «Bloquear si tiene saldo vencido». La edición envía siempre ambos valores actuales. En la **ficha**, un bloque «Crédito» (límite, saldo, comprometido, vencido, disponible y barra de uso; rojo si excede), visible solo con Ver en `EstadoCuenta`.
- **Listado de socios:** columna «Límite» y una insignia roja «Excedido» cuando la exposición supera el límite, con **una sola llamada** a `POST api/CreditoCliente/Posiciones` (en bloques de 500) y degradación silenciosa si falla o si no hay permiso, igual que Saldo y Vencido.
- **Formularios de pedido, entrega y factura:** al elegir el cliente se consulta `GET api/CreditoCliente/{codigoSn}` y se muestra «Disponible: X» (o «Sin límite»); si el total del documento supera el disponible se muestra un aviso antes de guardar (solo informativo: el control real es el de la API). Sin permiso `EstadoCuenta` Ver no se consulta ni se muestra.
- **Al guardar con 409:** un diálogo con las cifras (límite, saldo, comprometido, monto, exceso o «tiene saldo vencido»). Si el usuario tiene `AutorizarExcesoCredito`, el botón «Autorizar y guardar» reenvía el mismo documento con `AutorizarExcesoCredito = true`; si no, solo el mensaje. Todo texto escapado.
- **Menú:** sin entradas nuevas.
- Los clientes tipados `ISocioNegocioApiClient` y un nuevo `ICreditoClienteApiClient` siguen el patrón existente; `ApiClientBase` devuelve el 409 como una `Respuesta` fallida que conserva el cuerpo para que el controlador de la Web lo reenvíe al JS.

## §6 — Permisos

| Acción | Módulo / acción |
|---|---|
| Ver posición de crédito (API y Web) | `EstadoCuenta` / Ver |
| Autorizar un documento que excede (API) | `AutorizarExcesoCredito` / Ver |
| Cambiar límite y bloqueo en el socio | permisos de socios de negocio existentes |
| Crear pedido, entrega, factura | los ya existentes (`Agregar`) |

Despliegue: asignar `AutorizarExcesoCredito` a las plantillas que corresponda; sin esa asignación nadie puede autorizar excesos.

## §7 — Pruebas y verificación

- **Unitarias (xUnit + Moq).**
  - Servicio: límite vacío (permite), `0` (rechaza si monto > 0, pasa con 0), exacto, excedido, vencido con el interruptor, comprometido con líneas parcialmente consumidas, **sin doble conteo** al copiar pedido → entrega → factura, entrega cancelada no cuenta, cotización y orden de servicio no cuentan, factura de contado pagada completa (monto 0), pago parcial, redondeos, cliente sin documentos, socio que no es cliente, carga agrupada (una consulta por tabla).
  - Dominios: no consume consecutivo cuando se rechaza; con autorización guarda el documento y la `AutorizacionCredito`; la reapertura vuelve a verificar.
  - Controladores y permisos por reflexión: 409 con cuerpo y código correctos, 403 si pide autorizar sin el permiso, 404 de la posición.
- **Contra SQL Server real** (`smoke-limite-credito.ps1`, ≥ 40 comprobaciones): migración, límite vacío / 0 / exacto / excedido, comprometido con cifras exactas tras pedido → entrega → factura, bloqueo por vencido, 409 con cifras, 403 sin permiso, autorización con registro en `AutorizacionCredito`, factura de contado con límite 0, SigNumero sin cambio al rechazar, posiciones por lote. Se corre junto con las pruebas de regresión de pagos recibidos (54), pagos efectuados (51) y vencimientos (122).
- **Navegador (puppeteer):** socio con límite, ficha y listado con crédito, aviso en el formulario, diálogo de 409 y autorización, usuario sin permiso. Capturas reales revisadas, consola sin errores.
- Lección de fases anteriores: AutoMapper no debe poblar colecciones de navegación que el dominio inserta explícitamente (`AutorizacionCredito`); se prueba con SQL real desde el principio.

## Riesgos y notas

- **Concurrencia.** Dos documentos del mismo cliente creados casi al mismo tiempo pueden pasar ambos la verificación y juntos exceder el límite. Se acepta para esta entrega (volumen y uso humano); si se necesita, se cierra con un bloqueo por socio en la transacción.
- **Pedidos y entregas ya existentes** sin consumos coherentes: el comprometido se calcula con lo que hay; los documentos históricos abiertos empezarán a contar al activar un límite (lo esperado).
- **Impuestos y descuentos** del valor comprometido se toman de la cabecera del origen; si la línea ya viene neta, el redondeo por línea puede diferir de `TotalDoc` en centavos. Es una estimación conservadora, no un asiento contable.
- **Moneda:** se asume la moneda de los documentos; no hay conversión.
- **Reabrir documentos** solo aplica donde el dominio ya lo permite (hoy el pedido). Si una entrega o factura no se reabre, no hay nada que verificar ahí.
- **Plan:** el plan de implementación debe confirmar en el código los puntos marcados «donde el dominio lo permita» y la forma exacta de leer el permiso del usuario en los controladores antes de fijar las tareas.
