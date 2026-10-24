# Pagos recibidos (cobros a clientes) — Diseño

**Fecha:** 2026-10-23
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Documentos hermanos:** `2026-10-04-notas-credito-design.md`, `2026-10-17-notas-debito-en-factura-design.md`
(ambos dejaron "cobros y saldos de socios" como fuera de alcance), `2026-10-22-ordenes-servicio-taller-design.md`.

## Objetivo

Registrar los cobros que los clientes hacen sobre sus **facturas de venta** y **notas de débito de cliente**, y
mostrar para cada una cuánto se ha pagado y cuánto falta.

Referencia de comportamiento: **SAP Business One** (*Incoming Payment*, `ORCT`/`RCT2`): se elige el cliente, el
sistema lista sus documentos abiertos, se indica cuánto se paga a cada uno y con qué medios (efectivo, cheque,
transferencia, tarjeta); el pago es un documento propio con número, que solo se puede cancelar.

### Decisiones tomadas (brainstorming)

1. **Solo ventas.** "Pagos recibidos" (clientes). "Pagos efectuados" (proveedores sobre `FacturaCompra`) será una
   fase aparte que clone este diseño.
2. **Formas de pago = catálogo + referencia.** Un pago puede combinar varias formas (parte efectivo, parte
   tarjeta). Cada forma guarda su referencia (cheque, transferencia, tarjeta). **Sin cajas ni bancos con saldo, sin
   asientos contables, sin conciliación.**
3. **Todo pago se aplica a facturas.** No hay anticipos ni saldo a favor ni sobrepago: la suma de lo aplicado a
   facturas es igual al total del pago, y cada monto aplicado es como máximo el saldo de su factura.
4. **Las notas de crédito de cliente con factura base descuentan el saldo** de esa factura. Las notas sin factura
   base no afectan ningún saldo (siguen sin vínculo, como hoy).
5. **Saldo calculado, no guardado.** El saldo y el estado de pago se derivan de los documentos; `Factura` no gana
   columnas y `EstadoDoc` no cambia (hoy `EstadoDoc='C'` significa "documento no activo" para "Copiar a" y las
   validaciones de factura base, y no debe confundirse con "pagada").
6. **El pago es inmutable y solo se cancela** (permiso Eliminar, igual que las órdenes de servicio).
7. **Tipo de objeto 24** (el mismo número que usa SAP B1 para Pagos recibidos; libre en `NumeracionDocumento`).
8. **Permisos:** módulos nuevos `PagosRecibidos` y `FormasPago`, ambos en el grupo Ventas, acceso denegado por
   defecto (solo `SuperUsuario` se salta la matriz).
9. **Moneda única.** El sistema solo maneja QTZ y no hay tipo de cambio: el pago no lleva moneda.

### Fuera de alcance

Cajas y bancos con saldo, cuadre de caja, anticipos / pagos a cuenta / saldo a favor, devolución de dinero al
cliente, aplicar notas de crédito dentro del pago (compensación estilo SAP), estado de cuenta del cliente e informe de
antigüedad de saldos, fechas de vencimiento y mora, descuento por pronto pago, impresión del recibo, asientos
contables, pagos a proveedores (`Pagos efectuados`), y cobro de documentos que no sean factura o nota de débito.

## Contexto verificado

- `Factura` guarda facturas (`DocSubType='--'`) y notas de débito de cliente (`'ND'`) en las mismas tablas
  (`Factura`/`FacturaDetalle`). Cada una tiene su API: `FacturaDomain` filtra `'--'`, `NotaDebitoDomain` filtra
  `'ND'`. Una clave foránea a `Factura(Entry)` cubre ambas.
- Cancelar una factura es `FacturaDomain.ActualizarAsync` con `obj.Cancelado == "S"` (revierte inventario dentro de
  `IEjecutorTransaccion`); `NotaDebitoDomain` tiene su equivalente (`ActualizarAsync`, cancelación). Ambos dominios
  también tienen `EliminarAsync`.
- `NotaCredito` (obj 8) es un documento aparte con `BaseTipo`/`BaseEntry` (6 = Factura), `TotalDoc` y `Cancelado`;
  su límite acreditable se valida en `NotaCreditoDomain.ValidarLimiteAcreditableAsync` dentro de una transacción
  `IsolationLevel.Serializable`, usando `ObtenerAcreditadoAsync` (suma de notas no canceladas sobre la factura).
  `ObtenerFacturasBaseAsync` devuelve `FacturaAcreditable(factura, acreditado)` para el selector "Basado en
  factura".
- Los documentos nuevos siguen el patrón de las órdenes de servicio (`OrdenServicioDomain`/`Controller`, DTOs,
  `PerfilMapeo`, DI en `Startup`), con numeración vía `NumeracionDocumentoDet` (serie "Primario", `TipoSerie='D'`) y
  fila padre en `NumeracionDocumento`. Objetos en uso: 1-6, 8, 10-13, 15, 59, 60 y 70; el 24 está libre.
- Permisos: tabla `Modulo` (`Codigo`, `Nombre`, `Grupo`), claims "perm" `Modulo:VAUE`, atributo
  `RequierePermiso("<Modulo>", Accion.X)` en API y Web. `PermisoUsuario.Tiene(User, modulo, accion)` para casos
  dinámicos.
- Moneda: solo `QTZ`; no existe tipo de cambio.
- Redondeo de importes: `Math.Round(x, 2, MidpointRounding.AwayFromZero)` en el servidor (helper `Redondear` en los
  dominios de notas).

## §1 — Datos

Script nuevo `sql/2026-10-23-pagos-recibidos.sql` (idempotente) y el mismo bloque al final de
`sql/crear-api-db-test-completo.sql`.

```
FormaPago
  Codigo   nvarchar(10)  PK
  Nombre   nvarchar(50)  NOT NULL
  TipoDato nvarchar(1)   NOT NULL DEFAULT 'N' CHECK IN ('N','C','T','J')
                         -- N = sin dato, C = cheque, T = transferencia, J = tarjeta
  Activo   nvarchar(1)   NOT NULL DEFAULT 'S' CHECK IN ('S','N')
  Semilla: EFE Efectivo (N), CHQ Cheque (C), TRF Transferencia (T), TJT Tarjeta (J)

PagoRecibido                        (obj 24)
  Entry          int IDENTITY PK
  NumDoc         int NOT NULL
  Serie          int NOT NULL       FK -> NumeracionDocumentoDet(Serie)
  Cancelado      nvarchar(1) NOT NULL DEFAULT 'N' CHECK IN ('S','N')
  NumManual      nvarchar(1) NOT NULL DEFAULT 'N' CHECK IN ('S','N')
  TipoObjeto     nvarchar(11) NOT NULL DEFAULT '24' CHECK IN ('24')
  FechaDoc       datetime NOT NULL
  FechaCancelado datetime NULL
  CodigoSN       nvarchar(15) NOT NULL  FK -> SocioNegocio(Codigo)
  NombreSN       nvarchar(200) NULL
  TotalPago      decimal(19,6) NOT NULL CHECK (TotalPago > 0)
  Comentario     nvarchar(254) NULL

PagoRecibidoFactura                 (a qué documentos se aplica)
  Entry          int NOT NULL       FK -> PagoRecibido(Entry)
  NoLinea        int NOT NULL
  FacturaEntry   int NOT NULL       FK -> Factura(Entry)   -- factura o nota de débito
  MontoAplicado  decimal(19,6) NOT NULL CHECK (MontoAplicado > 0)
  PK (Entry, NoLinea); UNIQUE (Entry, FacturaEntry)
  Índice IX_PagoRecibidoFactura_Factura (FacturaEntry) INCLUDE (MontoAplicado)

PagoRecibidoMedio                   (con qué se pagó)
  Entry          int NOT NULL       FK -> PagoRecibido(Entry)
  NoLinea        int NOT NULL
  CodFormaPago   nvarchar(10) NOT NULL FK -> FormaPago(Codigo)
  Monto          decimal(19,6) NOT NULL CHECK (Monto > 0)
  Referencia     nvarchar(50) NULL  -- nº de cheque, referencia de transferencia, autorización o últimos 4 de tarjeta
  Banco          nvarchar(60) NULL
  FechaCheque    datetime NULL
  PK (Entry, NoLinea)
```

Semillas: serie "Primario" del objeto `24` en `NumeracionDocumentoDet` (`TipoSerie='D'`, `IniNumero=1`) y la fila
`NumeracionDocumento ('24','--','Pago recibido')`; módulos `PagosRecibidos` ("Pagos recibidos") y `FormasPago`
("Formas de pago") en `Modulo`, grupo `Ventas`, con `MERGE` sin `WHEN NOT MATCHED BY SOURCE` (no puede borrar
módulos). No hay datos históricos que migrar: todas las facturas existentes quedan con saldo = total.

Las filas de `PagoRecibidoFactura` / `PagoRecibidoMedio` no se borran nunca; cancelar solo marca
`PagoRecibido.Cancelado='S'`.

## §2 — Reglas y dominio (API)

### Saldo y estado de pago — `ISaldoFacturaService`

`SaldoFacturaService` (Domain.Core, interfaz en Domain.Interface), sin estado, con los repositorios genéricos:

```
SaldoFactura(int FacturaEntry, decimal Total, decimal Acreditado, decimal Pagado, decimal Saldo, string Estado)

Task<IReadOnlyDictionary<int, SaldoFactura>> ObtenerAsync(IEnumerable<int> facturaEntries)
Task<IReadOnlyList<DocumentoPago>> ObtenerPagosActivosAsync(int facturaEntry)   // (Entry, NumDoc, Monto) de pagos no cancelados
```

- `Total` = `Factura.TotalDoc`; `Acreditado` = suma de `NotaCredito.TotalDoc` con `BaseTipo=6`, `BaseEntry=entry`,
  `Cancelado<>'S'`; `Pagado` = suma de `PagoRecibidoFactura.MontoAplicado` cuyo `PagoRecibido.Cancelado<>'S'`.
- `Saldo = Redondear(Total − Acreditado − Pagado)`, nunca negativo en operación normal (las validaciones lo
  impiden); si por datos externos saliera negativo se informa 0.
- `Estado`: `"Pagada"` si `Saldo == 0`; `"Parcial"` si `Pagado > 0` y `Saldo > 0`; `"Pendiente"` en otro caso. Una
  factura saldada solo con notas de crédito aparece como "Pagada" (su saldo es 0).
- Resuelve todas las facturas pedidas con **tres consultas agrupadas** (facturas, notas de crédito, pagos), no una
  por factura.

### `PagoRecibidoDomain.InsertarAsync(PagoRecibido cab, IEnumerable<PagoRecibidoFactura> facturas, IEnumerable<PagoRecibidoMedio> medios)`

Orden de validación (todas lanzan `Exception` con mensaje de usuario, como el resto de dominios):

1. Fuerza `TipoObjeto='24'`, `Cancelado='N'`, `FechaCancelado=null`, `FechaDoc ??= Now`, `NombreSN` desde el socio.
2. **Serie:** existe, es del objeto 24, no bloqueada.
3. **Cliente:** existe y es de tipo cliente (`TipoSn='C'`). **No se exige que esté activo**: un cliente dado de baja
   con deuda debe poder pagarla.
4. **Facturas:** al menos una; sin `FacturaEntry` repetido; cada una existe, es del cliente, no está cancelada
   (factura o nota de débito, `DocSubType` `--` o `ND`) y `MontoAplicado > 0`. Los montos se redondean a 2
   decimales; `MontoAplicado` con más decimales se rechaza.
5. **Medios:** al menos uno; `Monto > 0`; forma existente y activa; la referencia según `TipoDato`:
   - `N`: sin requisitos (se ignoran referencia/banco).
   - `C`: **Referencia** (nº de cheque) y **Banco** obligatorios; `FechaCheque` opcional.
   - `T`: **Referencia** obligatoria; banco opcional.
   - `J`: **Referencia** (autorización o últimos 4 dígitos) obligatoria.
6. **Cuadre:** `Σ MontoAplicado == Σ Monto`; ese valor es `TotalPago` (lo calcula el servidor, no se acepta del
   cliente).
7. **Transacción `Serializable`** (`IEjecutorTransaccion.EjecutarAsync(..., IsolationLevel.Serializable)`), igual
   que el límite acreditable de las notas de crédito:
   a. se vuelven a leer los saldos con `SaldoFacturaService.ObtenerAsync` y se exige `MontoAplicado <= Saldo` por
      factura (mensaje con el número del documento y el saldo disponible);
   b. se asigna número (`AsignarNumero` por serie), se inserta la cabecera (Save #1), y se agregan facturas y
      medios con `NoLinea` consecutivos.

Dos pagos simultáneos sobre la misma factura no pueden exceder juntos el saldo.

### Cancelar un pago — `CancelarAsync(int entry)`

Permiso Eliminar. Rechaza si el pago no existe o ya está cancelado. Marca `Cancelado='S'` y `FechaCancelado=Now`
dentro de `IEjecutorTransaccion`. No toca facturas: su saldo se recupera solo porque deja de contar el pago.

### Cambios a código existente (acotados)

1. **Cancelar o eliminar una factura / nota de débito con pagos activos** — `FacturaDomain.ActualizarAsync`
   (rama `Cancelado=='S'`), `FacturaDomain.EliminarAsync`, y los equivalentes en `NotaDebitoDomain`: antes de
   actuar llaman a `ISaldoFacturaService.ObtenerPagosActivosAsync` y, si hay pagos, lanzan
   `"El documento tiene pagos aplicados (Pago N, Pago M). Cancele primero el pago."`. Ambos dominios reciben el
   servicio por constructor (se actualizan sus pruebas y la DI).
2. **Límite de la nota de crédito con factura base** — `NotaCreditoDomain.ValidarLimiteAcreditableAsync` pasa de
   `acreditado + nota <= total` a `acreditado + nota <= total − pagado`, donde `pagado` sale de
   `SaldoFacturaService`. Sin esto, una factura ya pagada podría acreditarse completa y quedar con saldo negativo
   (no hay anticipos ni devoluciones donde reflejarlo). Mensaje nuevo cuando hay pagos:
   `"Las notas de crédito (X) exceden lo que queda por acreditar de la factura (Y = total menos pagos aplicados).
   Cancele primero los pagos si necesita acreditarla por completo."`; sin pagos se conserva el mensaje actual.
   `FacturaAcreditable` y el selector "Basado en factura" muestran como saldo acreditable
   `total − acreditado − pagado`.

## §3 — API

`FormaPagoController` (`api/FormaPago`): `GET` listado (Ver `FormasPago`), `GET {codigo}`, `POST` (Agregar),
`PUT {codigo}` (Actualizar), `DELETE {codigo}` (Eliminar = baja lógica, `Activo='N'`; no se borra físicamente).
`Codigo` y `TipoDato` son inmutables una vez que existe un pago que usa la forma (se puede cambiar `Nombre` y
`Activo`).

`PagoRecibidoController` (`api/PagoRecibido`):

| Método | Ruta | Permiso | Resultado |
|---|---|---|---|
| GET | `api/PagoRecibido` | Ver `PagosRecibidos` | cabeceras (sin líneas) |
| GET | `api/PagoRecibido/{entry}` | Ver | cabecera |
| GET | `api/PagoRecibido/{entry}/Facturas` | Ver | documentos aplicados (con `NumDoc`, tipo y monto) |
| GET | `api/PagoRecibido/{entry}/Medios` | Ver | medios de pago |
| POST | `api/PagoRecibido` | Agregar | crea; devuelve `Entry` |
| POST | `api/PagoRecibido/{entry}/Cancelar` | Eliminar | cancela |
| GET | `api/PagoRecibido/FacturasAbiertas/{codigoSn}` | Agregar `PagosRecibidos` | facturas y notas de débito del cliente con saldo > 0, con `Total`, `Acreditado`, `Pagado`, `Saldo` |

`POST api/Factura/Saldos` (Ver `Facturas`) y `POST api/NotaDebito/Saldos` (Ver `NotasDebito`), con el cuerpo JSON `[1,2,3]`, devuelven `SaldoFactura` por documento para los listados (POST y no GET porque un listado grande no cabe en la URL). La pantalla de Facturas/Notas de débito pide los saldos del conjunto listado en una sola llamada.

**DTOs:** `PagoRecibidoCrearDTO { Serie, FechaDoc, CodigoSn, Comentario, Facturas[{FacturaEntry, MontoAplicado}],
Medios[{CodFormaPago, Monto, Referencia, Banco, FechaCheque}] }`, `PagoRecibidoDTO`, `PagoRecibidoFacturaDTO`,
`PagoRecibidoMedioDTO`, `FormaPagoDTO`/`Crear`/`Actualizar`, `SaldoFacturaDTO`, `FacturaAbiertaDTO`. Los totales
nunca se aceptan del cliente. `PerfilMapeo` (AutoMapper) y la DI en `Startup` siguen el patrón de las órdenes.

**Mensajes de error en español**, en el estilo existente; las validaciones de formato (campo requerido, longitudes)
con `[Required]`/`[StringLength]` en los DTOs de creación.

## §4 — Web

**Menú Ventas:** nuevas entradas "Pagos recibidos" y "Formas de pago" (visibles con Ver en su módulo); `site.js`
agrega la etiqueta de objeto 24 "Pago recibido".

**Formas de pago** (`FormasPagoController`, vista y `formaspago.js`): listado con DataTables y alta/edición en
modal, igual que Mecánicos (código, nombre, tipo de dato —lista: Sin dato / Cheque / Transferencia / Tarjeta—,
activo). Baja lógica con confirmación.

**Pagos recibidos** (`PagosRecibidosController`):
- `Index`: listado con número, fecha, cliente, total y estado (Vigente / Cancelado), filtro por estado y búsqueda.
  Clic en una fila abre un modal de solo lectura con las dos tablas (documentos aplicados y medios) y el botón
  "Cancelar pago" (con Eliminar), con confirmación.
- `Crear` (página completa, como Nuevo artículo; acepta `?facturaEntry=` para precargar el cliente y el documento):
  1. **Cabecera:** serie (lista de la numeración del objeto 24), fecha (hoy), cliente (buscador por nombre/código
     con `App.autocompletar`), comentario.
  2. **Documentos del cliente:** al elegir cliente se cargan sus facturas/notas de débito con saldo
     (`FacturasAbiertas`): documento, fecha, total, notas de crédito, pagado, saldo y la casilla "Pagar" más el campo
     "Monto a pagar". Marcar la casilla pone el saldo completo; el campo es editable y limitado a `0.00…saldo`.
     Botón "Pagar todo".
  3. **Medios de pago:** tabla de filas (forma, monto, referencia, banco, fecha de cheque); los campos de
     referencia/banco/fecha se muestran o se exigen según el `TipoDato` de la forma. Con una sola fila vacía, su
     monto se iguala automáticamente al total aplicado.
  4. **Barra de cuadre:** "Total aplicado", "Total medios" y "Diferencia"; **Guardar** se habilita solo cuando el
     total aplicado es mayor que 0 y la diferencia es 0.
  Tras guardar, redirige al listado con el aviso de éxito.
- Todas las acciones POST del MVC llevan `[ValidateAntiForgeryToken]` y `RequierePermiso`; el JS manda el token
  `X-CSRF-TOKEN`. Texto de usuario escapado al pintar tablas (helper `escapar` solo en `display`).

**Listados de Facturas y Notas de débito:** `FacturasController.ObtenerTodos` y `NotasDebitoController.ObtenerTodos`
piden los saldos del conjunto (`Saldos`) y los fusionan en la fila; se agregan columnas **Pagado** y **Saldo** y una
insignia **Pendiente / Parcial / Pagada**. Los usuarios con Agregar en `PagosRecibidos` ven en cada fila con
saldo > 0 el botón "Registrar pago", que abre `PagosRecibidos/Crear?facturaEntry=X`. Si el usuario no tiene Ver
en `Facturas`/`NotasDebito` no ve el listado de todos modos.

**Flujo del taller:** orden de servicio → "Copiar a Factura" → "Registrar pago" desde la factura.

**Dependencias de permisos para armar plantillas:** el pago exige Agregar `PagosRecibidos`, Ver `Facturas` y Ver
`SociosNegocio`; Formas de pago exige Ver `FormasPago` para poblar la lista del medio de pago.

## §5 — Pruebas

- **Dominio (xUnit + Moq):**
  - `SaldoFacturaService`: sin pagos, pago parcial, pago total, nota de crédito, pago cancelado no cuenta, nota
    cancelada no cuenta, redondeo, varias facturas en una consulta.
  - `PagoRecibidoDomain`: pago feliz; cliente inexistente / no cliente / inactivo (permitido); factura de otro
    cliente, cancelada, repetida; monto cero, negativo, con más de 2 decimales, mayor que el saldo; sin medios;
    forma inactiva; referencia/banco requeridos por cheque, transferencia y tarjeta; descuadre aplicado ≠ medios;
    serie inexistente/de otro objeto/bloqueada; numeración; cancelar (feliz, inexistente, ya cancelado).
  - Cambios existentes: cancelar y eliminar factura y nota de débito con y sin pagos; límite de nota de crédito con
    pagos (excede, justo en el límite, sin pagos conserva el mensaje anterior).
  - Controladores: 403 sin permiso, 400 en dominio, 404 inexistente (patrón `TallerPermisosTests`).
  - `FormaPagoDomain`: alta, duplicado, baja lógica, inmutabilidad de `TipoDato` con pagos.
- **Smoke contra SQL Server real** (base temporal creada con el script completo): pagar parcial y luego el resto;
  cancelar un pago y comprobar que el saldo vuelve; **dos pagos concurrentes a la misma factura** (el segundo
  rechazado por exceso); nota de crédito sobre factura pagada rechazada y aceptada tras cancelar el pago; cancelar
  factura con pago rechazado; nota de débito cobrable; script SQL corrido dos veces.
- **Recorrido en navegador:** formas de pago, nuevo pago con medio mixto (efectivo + cheque), atajo desde el listado
  de facturas, cancelación, columnas de saldo y la insignia.

## §6 — Despliegue y riesgos

- Ejecutar `sql/2026-10-23-pagos-recibidos.sql` en `API_DB_TEST` (idempotente) y asignar los módulos
  `PagosRecibidos` y `FormasPago` a las plantillas de permisos.
- No hay migración de datos: las facturas existentes quedan con saldo = total.
- El saldo se calcula con tres consultas agrupadas por listado; para el volumen esperado (listados que ya cargan
  todas las filas en el cliente) es adecuado. Si el listado llegara a ser muy grande, se puede materializar después
  sin cambiar la API.
- **Riesgo aceptado:** un pago puede cancelarse sin restricción por fecha. Cuando exista contabilidad habrá que
  revisar esa regla.
- **Riesgo aceptado:** sin anticipos, un cliente que pagó de más no tiene dónde registrarlo; en ese caso se paga lo
  adeudado y el exceso queda fuera del sistema (fase futura "anticipos / saldo a favor").
