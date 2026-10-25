# Pagos efectuados (pagos a proveedores) — Diseño (Fase A de cuentas por cobrar / por pagar)

**Fecha:** 2026-10-25
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Documento hermano:** `2026-10-23-pagos-recibidos-design.md` (este diseño es su espejo del lado de compras).

## Contexto: tres fases

El pedido es llevar el control de **cuentas por cobrar y por pagar** como SAP Business One, y controlar el **límite
de crédito** de los socios de negocio. Se divide en tres fases, cada una con su diseño, plan y commits:

| Fase | Contenido | Estado |
|---|---|---|
| **A** | **Pagos efectuados** + saldo y estado de pago de facturas de compra y notas de débito de proveedor (este documento) | este diseño |
| B | Condiciones de pago y fechas de vencimiento; saldo y estado de cuenta del socio; antigüedad de saldos (cobrar y pagar) | por diseñar |
| C | Límite de crédito del socio y su control en los documentos de venta | por diseñar |

La Fase A no depende de B ni de C. B y C se diseñarán después de cerrar A, porque requieren decisiones de negocio
(ver "Hoja de ruta de las fases B y C" al final).

## Objetivo

Registrar los pagos que la empresa hace a sus proveedores sobre sus **facturas de compra** y **notas de débito de
proveedor**, y mostrar para cada una cuánto se ha pagado y cuánto falta.

Referencia de comportamiento: **SAP Business One** (*Outgoing Payment*, `OVPM`/`VPM2`, objeto 46): se elige el
proveedor, el sistema lista sus documentos abiertos, se indica cuánto se paga a cada uno y con qué medios; el pago
es un documento propio con número, que solo se puede cancelar.

### Decisiones tomadas (brainstorming)

1. **Alcance de la Fase A = espejo de Pagos recibidos.** Se conservan todas sus decisiones: formas de pago =
   catálogo + referencia, varias formas por pago, sin cajas ni bancos con saldo, sin asientos contables; todo pago
   se aplica a documentos (sin anticipos, sin saldo a favor, sin sobrepago); saldo calculado y no guardado; el
   pago es inmutable y solo se cancela; moneda única (QTZ).
2. **Enfoque: tablas propias + helpers compartidos.** `PagoEfectuado`, `PagoEfectuadoFactura` y
   `PagoEfectuadoMedio` son tablas separadas de las de cobros (como `ORCT` y `OVPM` en SAP). Del dominio de cobros
   se extrae a un helper común **solo** la validación de los medios de pago y el cuadre; el comportamiento de
   Pagos recibidos no cambia y sus pruebas actuales siguen pasando sin modificarse.
3. **Catálogo `FormaPago` compartido** con Pagos recibidos (mismo módulo `FormasPago`, mismas semillas EFE, CHQ,
   TRF, TJT).
4. **Tipo de objeto 46** (el mismo número que usa SAP B1 para Pagos salientes; libre en `NumeracionDocumento`).
5. **Notas de crédito de proveedor con factura base descuentan el saldo** de esa factura (`NotaCreditoCompra`,
   `BaseTipo=13`, `Cancelado<>'S'`). Las notas sin factura base no afectan ningún saldo.
6. **Permisos:** módulo nuevo `PagosEfectuados` en el grupo Compras, acceso denegado por defecto (solo
   `SuperUsuario` se salta la matriz).

### Fuera de alcance de la Fase A

Anticipos / pagos a cuenta / saldo a favor, devolución de dinero del proveedor, aplicar notas de crédito dentro del
pago (compensación estilo SAP), cajas y bancos con saldo, cheques como documento propio (impresión, numeración de
chequera), asientos contables, **registrar el pago al crear la factura de compra** (se puede añadir después, como
se hizo en ventas), fechas de vencimiento, estado de cuenta, antigüedad de saldos (Fase B) y límite de crédito
(Fase C).

## Contexto verificado

- `FacturaCompra` guarda facturas de compra (`DocSubType='--'`) y notas de débito de proveedor (`'ND'`) en las
  mismas tablas (`FacturaCompra`/`FacturaCompraDetalle`). Cada una tiene su API: `FacturaCompraDomain` y
  `NotaDebitoCompraDomain`. Una clave foránea a `FacturaCompra(Entry)` cubre ambas.
- Cancelar es `ActualizarAsync` con `obj.Cancelado == "S"` (revierte inventario dentro de `IEjecutorTransaccion`)
  en `FacturaCompraDomain` y en `NotaDebitoCompraDomain`; ambos tienen también `EliminarAsync`.
- `NotaCreditoCompra` (obj 15) es un documento aparte con `BaseTipo` (13 = factura de compra), `BaseEntry`,
  `TotalDoc` y `Cancelado`; su límite acreditable se valida en
  `NotaCreditoCompraDomain.ValidarLimiteAcreditableAsync` usando `ObtenerAcreditadoAsync`.
- Pagos recibidos ya aporta el patrón completo: `ISaldoFacturaService`/`SaldoFacturaService`, `GuardaPagos`,
  `PagoRecibidoDomain`/`Application`/`Controller`, DTOs, repositorios, `FormaPago*`, y en Web `PagosRecibidos*`
  (`pagosrecibidos.js`, `pagosrecibidos-crear.js`).
- Los objetos de numeración en uso son 1-6, 8, 10-13, 15, 24, 59, 60 y 70; el 46 está libre.

## §1 — Datos

Script nuevo `sql/2026-10-25-pagos-efectuados.sql` (idempotente) y el mismo bloque al final de
`sql/crear-api-db-test-completo.sql`.

```
PagoEfectuado                       (obj 46)
  Entry          int IDENTITY PK
  NumDoc         int NOT NULL
  Serie          int NOT NULL       FK -> NumeracionDocumentoDet(Serie)
  Cancelado      nvarchar(1) NOT NULL DEFAULT 'N' CHECK IN ('S','N')
  NumManual      nvarchar(1) NOT NULL DEFAULT 'N' CHECK IN ('S','N')
  TipoObjeto     nvarchar(11) NOT NULL DEFAULT '46' CHECK IN ('46')
  FechaDoc       datetime NOT NULL
  FechaCancelado datetime NULL
  CodigoSN       nvarchar(15) NOT NULL  FK -> SocioNegocio(Codigo)
  NombreSN       nvarchar(200) NULL
  TotalPago      decimal(19,6) NOT NULL CHECK (TotalPago > 0)
  Comentario     nvarchar(254) NULL
  UNIQUE (Serie, NumDoc)

PagoEfectuadoFactura                (a qué documentos se aplica)
  Entry               int NOT NULL       FK -> PagoEfectuado(Entry)
  NoLinea             int NOT NULL
  FacturaCompraEntry  int NOT NULL       FK -> FacturaCompra(Entry)   -- factura de compra o nota de débito
  MontoAplicado       decimal(19,6) NOT NULL CHECK (MontoAplicado > 0)
  PK (Entry, NoLinea); UNIQUE (Entry, FacturaCompraEntry)
  Índice IX_PagoEfectuadoFactura_Factura (FacturaCompraEntry) INCLUDE (MontoAplicado)

PagoEfectuadoMedio                  (con qué se pagó)
  Entry          int NOT NULL       FK -> PagoEfectuado(Entry)
  NoLinea        int NOT NULL
  CodFormaPago   nvarchar(10) NOT NULL FK -> FormaPago(Codigo)
  Monto          decimal(19,6) NOT NULL CHECK (Monto > 0)
  Referencia     nvarchar(50) NULL  -- nº de cheque emitido, referencia de transferencia, autorización o últimos 4 de tarjeta
  Banco          nvarchar(60) NULL
  FechaCheque    datetime NULL
  PK (Entry, NoLinea)
```

Semillas: serie "Primario" del objeto `46` en `NumeracionDocumentoDet` (`TipoSerie='D'`, `IniNumero=1`) y la fila
`NumeracionDocumento ('46','--','Pago efectuado')`; módulo `PagosEfectuados` ("Pagos efectuados") en `Modulo`,
grupo `Compras`, con `MERGE` sin `WHEN NOT MATCHED BY SOURCE` (no puede borrar módulos). `FormaPago` ya existe; no
se duplica. No hay datos históricos que migrar: todas las facturas de compra existentes quedan con saldo = total.

Las filas de `PagoEfectuadoFactura` / `PagoEfectuadoMedio` no se borran nunca; cancelar solo marca
`PagoEfectuado.Cancelado='S'`.

## §2 — Reglas y dominio (API)

### Helper compartido — `ValidadorMediosPago`

Se extrae de `PagoRecibidoDomain` a una clase estática en `API.Domain.Core` la lógica que no depende del lado:

- `ValidarMedios(IEnumerable<IMedioPago> medios, IReadOnlyDictionary<string, FormaPago> formas)`: al menos un
  medio; `Monto > 0` y a lo sumo 2 decimales; forma existente y activa; referencia/banco según `TipoDato`
  (`N`: ninguna; `C`: referencia y banco; `T`: referencia; `J`: referencia).
- `ValidarCuadre(decimal aplicado, decimal medios)`: `Σ aplicado == Σ medios`.

`PagoRecibidoMedio` y `PagoEfectuadoMedio` implementan una interfaz mínima `IMedioPago`
(`CodFormaPago`, `Monto`, `Referencia`, `Banco`, `FechaCheque`). Los mensajes de error se conservan **idénticos**
a los actuales, para que las pruebas existentes de `PagoRecibidoDomain` no cambien. Es el único cambio a código de
cobros.

### Saldo y estado de pago — `ISaldoFacturaCompraService`

`SaldoFacturaCompraService` (Domain.Core, interfaz en Domain.Interface), sin estado:

```
SaldoFacturaCompra(int FacturaCompraEntry, decimal Total, decimal Acreditado, decimal Pagado, decimal Saldo, string Estado)

Task<IReadOnlyDictionary<int, SaldoFacturaCompra>> ObtenerAsync(IEnumerable<int> facturaCompraEntries)
Task<IReadOnlyList<PagoAplicado>> ObtenerPagosActivosAsync(int facturaCompraEntry)   // (Entry, NumDoc, Monto) de pagos no cancelados
Task<bool> TienePagosRegistradosAsync(int facturaCompraEntry)                         // incluye pagos cancelados
```

- `Total` = `FacturaCompra.TotalDoc`; `Acreditado` = suma de `NotaCreditoCompra.TotalDoc` con `BaseTipo=13`,
  `BaseEntry=entry`, `Cancelado<>'S'`; `Pagado` = suma de `PagoEfectuadoFactura.MontoAplicado` cuyo
  `PagoEfectuado.Cancelado<>'S'`.
- `Saldo = Redondear(Total − Acreditado − Pagado)`; si por datos externos saliera negativo se informa 0.
- `Estado`: `"Pagada"` si `Saldo == 0`; `"Parcial"` si `Pagado > 0` y `Saldo > 0`; `"Pendiente"` en otro caso.
- Resuelve todas las facturas pedidas con **tres consultas agrupadas** (facturas, notas de crédito, pagos).

### `PagoEfectuadoDomain.InsertarAsync(PagoEfectuado cab, IEnumerable<PagoEfectuadoFactura> facturas, IEnumerable<PagoEfectuadoMedio> medios)`

Orden de validación (todas lanzan `Exception` con mensaje de usuario, como el resto de dominios):

1. Fuerza `TipoObjeto='46'`, `Cancelado='N'`, `FechaCancelado=null`, `FechaDoc ??= Now`, `NombreSN` desde el socio.
2. **Serie:** existe, es del objeto 46, no bloqueada.
3. **Proveedor:** existe y es de tipo proveedor (`TipoSn='P'`). **No se exige que esté activo**: un proveedor dado
   de baja con deuda debe poder cobrarse.
4. **Documentos:** al menos uno; sin `FacturaCompraEntry` repetido; cada uno existe, es del proveedor, no está
   cancelado (factura de compra o nota de débito, `DocSubType` `--` o `ND`) y `MontoAplicado > 0` con a lo sumo
   2 decimales.
5. **Medios:** `ValidadorMediosPago.ValidarMedios`.
6. **Cuadre:** `ValidadorMediosPago.ValidarCuadre`; ese valor es `TotalPago` (lo calcula el servidor, no se acepta
   del cliente).
7. **Transacción `Serializable`** (`IEjecutorTransaccion.EjecutarAsync(..., IsolationLevel.Serializable)`):
   a. se vuelven a leer los saldos con `SaldoFacturaCompraService.ObtenerAsync` y se exige
      `MontoAplicado <= Saldo` por documento (mensaje con el número del documento y el saldo disponible);
   b. se asigna número (`AsignarNumero` por serie), se inserta la cabecera (Save #1), y se agregan documentos y
      medios con `NoLinea` consecutivos.

Dos pagos simultáneos sobre el mismo documento no pueden exceder juntos el saldo.

### Cancelar un pago — `CancelarAsync(int entry)`

Permiso Eliminar. Rechaza si el pago no existe o ya está cancelado. Marca `Cancelado='S'` y `FechaCancelado=Now`
dentro de `IEjecutorTransaccion`. No toca documentos: su saldo se recupera solo porque deja de contar el pago.

### Cambios a código existente (acotados)

1. **Cancelar o eliminar una factura de compra / nota de débito de proveedor con pagos activos** —
   `FacturaCompraDomain.ActualizarAsync` (rama `Cancelado=='S'`), `FacturaCompraDomain.EliminarAsync` y los
   equivalentes en `NotaDebitoCompraDomain`: antes de actuar validan con un helper `GuardaPagosEfectuados` (espejo
   de `GuardaPagos`): `"El documento tiene pagos aplicados (Pago N, Pago M). Cancele primero el pago."`, y en la
   eliminación `"El documento tiene pagos registrados (cancelados) y no se puede eliminar; consérvelo cancelado."`.
   Ambos dominios reciben `ISaldoFacturaCompraService` por constructor (se actualizan sus pruebas y la DI).
2. **Límite de la nota de crédito de proveedor con factura base** —
   `NotaCreditoCompraDomain.ValidarLimiteAcreditableAsync` pasa de `acreditado + nota <= total` a
   `acreditado + nota <= total − pagado`, con `pagado` de `SaldoFacturaCompraService`. Mensaje nuevo cuando hay
   pagos: `"Las notas de crédito (X) exceden lo que queda por acreditar de la factura de compra (Y = total menos
   pagos aplicados). Cancele primero los pagos si necesita acreditarla por completo."`; sin pagos se conserva el
   mensaje actual. El selector "Basado en factura" de notas de crédito de proveedor muestra como saldo acreditable
   `total − acreditado − pagado`.

## §3 — API

`PagoEfectuadoController` (`api/PagoEfectuado`):

| Método | Ruta | Permiso | Resultado |
|---|---|---|---|
| GET | `api/PagoEfectuado` | Ver `PagosEfectuados` | cabeceras (sin líneas) |
| GET | `api/PagoEfectuado/{entry}` | Ver | cabecera |
| GET | `api/PagoEfectuado/{entry}/Facturas` | Ver | documentos aplicados (con `NumDoc`, tipo y monto) |
| GET | `api/PagoEfectuado/{entry}/Medios` | Ver | medios de pago |
| POST | `api/PagoEfectuado` | Agregar | crea; devuelve `Entry` |
| POST | `api/PagoEfectuado/{entry}/Cancelar` | Eliminar | cancela |
| GET | `api/PagoEfectuado/FacturasAbiertas/{codigoSn}` | Agregar `PagosEfectuados` | facturas de compra y notas de débito de proveedor del proveedor con saldo > 0, con `Total`, `Acreditado`, `Pagado`, `Saldo` |

`POST api/FacturaCompra/Saldos` (Ver `FacturasCompra`) y `POST api/NotaDebitoCompra/Saldos` (Ver
`NotasDebitoCompra`), con el cuerpo JSON `[1,2,3]`, devuelven `SaldoFacturaCompra` por documento para los listados
(POST y no GET porque un listado grande no cabe en la URL).

**DTOs:** `PagoEfectuadoCrearDTO { Serie, FechaDoc, CodigoSn, Comentario, Facturas[{FacturaCompraEntry,
MontoAplicado}], Medios[{CodFormaPago, Monto, Referencia, Banco, FechaCheque}] }`, `PagoEfectuadoDTO`,
`PagoEfectuadoFacturaDTO`, `PagoEfectuadoMedioDTO`, `SaldoFacturaCompraDTO`, `FacturaCompraAbiertaDTO`. Los totales
nunca se aceptan del cliente. `PerfilMapeo` (AutoMapper) y la DI en `Startup` siguen el patrón de cobros.
`FormaPago` no cambia de API.

**Mensajes de error en español**, en el estilo existente; las validaciones de formato con `[Required]` /
`[StringLength]` en los DTOs de creación.

## §4 — Web

**Menú Compras:** nueva entrada "Pagos efectuados" (visible con Ver en `PagosEfectuados`); `site.js` agrega la
etiqueta de objeto 46 "Pago efectuado". "Formas de pago" sigue en el menú Ventas y es el mismo catálogo.

**Pagos efectuados** (`PagosEfectuadosController`, `pagosefectuados.js`, `pagosefectuados-crear.js`; clon de
Pagos recibidos con "proveedor" en lugar de "cliente"):
- `Index`: listado con número, fecha, proveedor, total y estado (Vigente / Cancelado), filtro por estado y
  búsqueda. Clic en una fila abre un modal de solo lectura con las dos tablas (documentos aplicados y medios) y el
  botón "Cancelar pago" (con Eliminar), con confirmación.
- `Crear` (página completa; acepta `?facturaCompraEntry=` para precargar el proveedor y el documento):
  cabecera (serie del objeto 46, fecha, proveedor con `App.autocompletar` filtrado a tipo P, comentario);
  documentos del proveedor con saldo (documento, fecha, total, notas de crédito, pagado, saldo, casilla "Pagar" y
  "Monto a pagar" editable limitado a `0.00…saldo`; botón "Pagar todo"); medios de pago (forma, monto, referencia,
  banco, fecha de cheque, según el `TipoDato`); barra de cuadre (Guardar solo con total > 0 y diferencia 0).
- Todas las acciones POST del MVC llevan `[ValidateAntiForgeryToken]` y `RequierePermiso`; el JS manda el token
  `X-CSRF-TOKEN`. Texto de usuario escapado al pintar tablas.

**Listados de Facturas de compra y Notas de débito de proveedor:** `FacturasCompraController.ObtenerTodos` y
`NotasDebitoCompraController.ObtenerTodos` piden los saldos del conjunto y los fusionan en la fila; se agregan
columnas **Pagado** y **Saldo** y la insignia **Pendiente / Parcial / Pagada**. Los usuarios con Agregar en
`PagosEfectuados` ven en cada fila con saldo > 0 el botón "Registrar pago" (`PagosEfectuados/Crear?facturaCompraEntry=X`).

**Dependencias de permisos para armar plantillas:** el pago exige Agregar `PagosEfectuados`, Ver `FacturasCompra`
y Ver `SociosNegocio`, Ver `FormasPago` para poblar las formas de pago y Ver `NumeracionDocumento` para la lista de
series; sin ellos la página degrada con avisos, como en Pagos recibidos.

## §5 — Pruebas

- **Dominio (xUnit + Moq):**
  - `ValidadorMediosPago`: se mueven/duplican los casos de medios de `PagoRecibidoDomainTests`; los de cobros
    siguen pasando sin cambios (prueba de regresión del refactor).
  - `SaldoFacturaCompraService`: sin pagos, pago parcial, pago total, nota de crédito, pago cancelado no cuenta,
    nota cancelada no cuenta, redondeo, varios documentos en una consulta.
  - `PagoEfectuadoDomain`: pago feliz; proveedor inexistente / no proveedor / inactivo (permitido); documento de
    otro proveedor, cancelado, repetido; monto cero, negativo, con más de 2 decimales, mayor que el saldo; sin
    medios; descuadre; serie inexistente/de otro objeto/bloqueada; numeración; cancelar (feliz, inexistente, ya
    cancelado).
  - Cambios existentes: cancelar y eliminar factura de compra y nota de débito de proveedor con y sin pagos;
    límite de nota de crédito de proveedor con pagos (excede, justo en el límite, sin pagos conserva el mensaje).
  - Controladores: 403 sin permiso, 400 en dominio, 404 inexistente.
- **Smoke contra SQL Server real** (`smoke-pagos-efectuados.ps1`, base temporal creada con el script completo):
  pagar parcial y luego el resto; pago mixto; cancelar un pago y comprobar que el saldo vuelve; **dos pagos
  concurrentes al mismo documento** (el segundo rechazado por exceso); nota de crédito de proveedor sobre factura
  pagada rechazada y aceptada tras cancelar el pago; cancelar factura de compra con pago rechazado; nota de débito
  de proveedor pagable; script SQL corrido dos veces.
- **Recorrido en navegador:** nuevo pago con medio mixto, atajo desde el listado de facturas de compra,
  cancelación, columnas de saldo e insignia; el protocolo de 12 tareas de Pagos recibidos sigue pasando.

## §6 — Despliegue y riesgos

- Ejecutar `sql/2026-10-25-pagos-efectuados.sql` en `API_DB_TEST` (idempotente) y asignar el módulo
  `PagosEfectuados` a las plantillas de permisos.
- No hay migración de datos: las facturas de compra existentes quedan con saldo = total (incluye las facturas
  reales cargadas desde fotografías).
- El saldo se calcula con tres consultas agrupadas por listado; adecuado para el volumen esperado.
- **Riesgo del refactor:** extraer el validador de medios toca `PagoRecibidoDomain`. Se mitiga conservando los
  mensajes idénticos y exigiendo que toda la suite actual pase antes y después del cambio.
- **Riesgo aceptado:** un pago puede cancelarse sin restricción por fecha; sin anticipos, un pago de más al
  proveedor no tiene dónde registrarse (fase futura).

## Hoja de ruta de las fases B y C (no forma parte de esta entrega)

**Fase B — vencimientos, estado de cuenta y antigüedad.** Se parte de lo que SAP B1 llama *Payment Terms* y
*Business Partner Balance*: una tabla de **condiciones de pago** (contado, crédito a N días) asignable al socio y
heredada por sus documentos; **fecha de vencimiento** en facturas de venta y de compra (`FechaDoc + días`);
**saldo del socio** (Σ saldos de sus documentos abiertos) en su ficha; **estado de cuenta** por socio
(facturas, notas, pagos con saldo acumulado); **informe de antigüedad de saldos** (vigente, 1-30, 31-60, 61-90, +90
días) para cobrar y para pagar. Decisiones por tomar: si el vencimiento es editable por documento, si las facturas
históricas sin condición se consideran contado, y qué cortes de antigüedad usar.

**Fase C — límite de crédito.** Campo `LimiteCredito` en el socio de negocio (como *Credit Limit* de SAP B1) y
control al crear **facturas, entregas y pedidos de venta** comparando el límite contra el **saldo del cliente**
(Fase A/B) más lo comprometido. Decisiones por tomar: si el límite **bloquea** o solo **advierte** (en SAP, advierte
y pide autorización); qué cuenta como comprometido (pedidos y entregas aún no facturados); si lo puede saltar un
permiso especial; si 0 significa "sin límite".
