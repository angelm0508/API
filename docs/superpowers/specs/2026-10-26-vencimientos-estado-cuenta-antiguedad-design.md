# Condiciones de pago, vencimientos, estado de cuenta y antigüedad de saldos — Diseño (Fase B de cuentas por cobrar / por pagar)

**Fecha:** 2026-10-26
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Documentos hermanos:** `2026-10-23-pagos-recibidos-design.md` y `2026-10-25-pagos-efectuados-design.md` (Fase A: pagos y saldos por documento, ya construidos). Este diseño es la **Fase B** de la hoja de ruta del segundo documento; la Fase C (límite de crédito) queda al final.

## Objetivo

Saber **cuánto debe cada cliente y cuánto se le debe a cada proveedor, y desde cuándo**. Hoy el sistema calcula el saldo de cada factura (total − notas de crédito − pagos), pero no sabe cuándo vence ni agrupa por socio. La Fase B agrega:

1. **Condiciones de pago y fecha de vencimiento** en las facturas (de venta y de compra) y sus notas de débito.
2. **Saldo del socio y estado de cuenta** (movimientos con saldo acumulado) para clientes y proveedores.
3. **Antigüedad de saldos** por cobrar y por pagar.

Referencia de comportamiento: **SAP Business One** — *Payment Terms* (`OCTG`) asignados al socio y heredados por el documento, `DocDueDate`, *Business Partner Account Balance*, *Customer/Vendor Receivables/Payables Aging* (informes de antigüedad).

### Decisiones tomadas (brainstorming)

1. **Vencimiento por catálogo de condiciones de pago.** Tabla `CondicionPago` (código, nombre, días). El socio tiene una condición por defecto; el documento la hereda al elegir el socio y se puede cambiar **en ese documento**; el servidor calcula `FechaVencimiento = FechaDoc + días`. **No hay fecha de vencimiento manual** (evita descuadres entre condición y fecha).
2. **La condición y la fecha se guardan en el documento** al crearlo (como SAP: cambiar después la condición del socio no altera documentos ya emitidos). Los saldos y reportes se calculan al leer, con los servicios de saldo existentes; no se materializan tablas de saldos.
3. **Documentos históricos vencen en la fecha del documento** (condición Contado, 0 días): una migración del script SQL les asigna `CondicionPago = 'CON'` y `FechaVencimiento = FechaDoc`. Su saldo pendiente figura vencido desde la fecha de la factura; no se oculta deuda atrasada. Incluye las facturas de compra reales cargadas desde fotografías.
4. **Solo facturas y notas de débito llevan vencimiento** (`Factura` con `DocSubType` `--`/`ND`; `FacturaCompra` con `--`/`DN`). Cotización, pedido, entrega, orden de servicio y notas de crédito no.
5. **Antigüedad al día de hoy**, sobre los saldos actuales; sin corte retroactivo (reconstruir saldos a una fecha pasada queda fuera de alcance).
6. **Cortes de antigüedad:** Vigente (no vencido), 1-30, 31-60, 61-90 y más de 90 días vencidos.
7. **Estado de cuenta consistente con los saldos:** su saldo final es la suma de los saldos de los documentos abiertos. Las notas de crédito **sin factura base** no afectan ningún saldo (decisión de la Fase A) y se listan solo como informativas.
8. **Permisos nuevos** (acceso denegado por defecto): `CondicionesPago` y `EstadoCuenta` en el grupo Socios de negocio; `AntiguedadCobrar` en Ventas; `AntiguedadPagar` en Compras.

### Fuera de alcance

Recordatorios o cobranza, intereses por mora, descuento por pronto pago, pagos en cuotas o parcialidades programadas, vencimiento manual por documento, antigüedad con corte retroactivo, exportación a PDF o Excel (sigue como recomendación de reportes), notificaciones, **límite de crédito** (Fase C) y mostrar los vencidos en el tablero (hoy es texto de ejemplo).

## Contexto verificado

- `Factura` (ventas) y `FacturaCompra` (compras) tienen `FechaDoc`, `FechaEmision`, `TotalDoc`, `Cancelado`, `DocSubType`; **no** tienen condición ni vencimiento. `SocioNegocio` no tiene condición de pago.
- Inserción: `FacturaDomain.InsertarAsync`, `FacturaCompraDomain.InsertarAsync`, `NotaDebitoDomain.InsertarAsync`, `NotaDebitoCompraDomain.InsertarAsync` (cada una fuerza `DocSubType`, serie y numeración antes de guardar).
- Saldos por documento: `ISaldoFacturaService` (ventas) e `ISaldoFacturaCompraService` (compras) devuelven `Total, Acreditado, Pagado, Saldo, Estado` con tres consultas agrupadas.
- Pagos: `PagoRecibido`/`PagoRecibidoFactura` y `PagoEfectuado`/`PagoEfectuadoFactura` (con `FechaDoc`, `Cancelado`).
- Notas de crédito con factura base: `NotaCredito` (`BaseTipo=6`) y `NotaCreditoCompra` (`BaseTipo=13`), `TotalDoc`, `Cancelado`, `FechaDoc`.
- Los catálogos simples (Mecánicos, Formas de pago) siguen el patrón dominio/aplicación/controlador + pantalla en modal, con baja lógica.
- Módulos de permisos: tabla `Modulo(Codigo, Nombre, Grupo)`; grupos en uso: Inventario, Socios de negocio, Documentos, Ventas, Compras, Administración.

## §1 — Datos (B1)

Script nuevo `sql/2026-10-26-condiciones-pago-vencimientos.sql` (idempotente) y el mismo bloque al final de `sql/crear-api-db-test-completo.sql`.

```
CondicionPago
  Codigo   nvarchar(10)  PK
  Nombre   nvarchar(50)  NOT NULL
  Dias     int           NOT NULL CHECK (Dias BETWEEN 0 AND 365)
  Activo   nvarchar(1)   NOT NULL DEFAULT 'S' CHECK IN ('S','N')
  Semillas: CON Contado (0), C15 Crédito 15 días (15), C30 Crédito 30 días (30),
            C45 Crédito 45 días (45), C60 Crédito 60 días (60)

SocioNegocio  + CondicionPago nvarchar(10) NULL  FK -> CondicionPago(Codigo)

Factura       + CondicionPago nvarchar(10) NULL  FK -> CondicionPago(Codigo)
              + FechaVencimiento datetime NULL
FacturaCompra + CondicionPago nvarchar(10) NULL  FK -> CondicionPago(Codigo)
              + FechaVencimiento datetime NULL
```

- **Migración de históricos:** `UPDATE Factura SET CondicionPago='CON', FechaVencimiento=FechaDoc WHERE FechaVencimiento IS NULL` y lo mismo en `FacturaCompra`. Las columnas quedan **nulables** para no romper `INSERT` existentes; el dominio las llena siempre y la lectura usa `COALESCE(FechaVencimiento, FechaDoc)` por seguridad.
- Índices `IX_Factura_Vencimiento (FechaVencimiento) INCLUDE (CodigoSN, TotalDoc)` e `IX_FacturaCompra_Vencimiento` análogo, para la antigüedad.
- Módulos: `('CondicionesPago','Condiciones de pago','Socios de negocio')`, `('EstadoCuenta','Estado de cuenta','Socios de negocio')`, `('AntiguedadCobrar','Antigüedad de saldos por cobrar','Ventas')`, `('AntiguedadPagar','Antigüedad de saldos por pagar','Compras')`; `MERGE` sin `WHEN NOT MATCHED BY SOURCE`.

## §2 — Reglas y dominio (API)

### Condición de pago — `CondicionPagoDomain`
Catálogo con baja lógica (`DELETE` pone `Activo='N'`). `Codigo` inmutable. `Dias` editable: solo afecta documentos futuros (los existentes guardan su fecha). La condición `CON` **no se puede dar de baja** (es el valor de respaldo del sistema). Al desactivar una condición, el socio o el documento que ya la usa la conserva; no se puede **asignar** una condición inactiva a un socio ni a un documento nuevo.

### Resolución de la condición al crear un documento — `ResolutorVencimiento`
Helper compartido por los cuatro dominios de inserción:

```
(string condicion, DateTime vencimiento) Resolver(string? condicionPedida, SocioNegocio socio, DateTime fechaDoc, ...)
  1. condicion = condicionPedida ?? socio.CondicionPago ?? "CON"
  2. la condición debe existir y estar activa  → "La condición de pago {c} no existe." / "... está inactiva."
  3. vencimiento = fechaDoc.Date + condición.Dias días
```

`FacturaDomain`, `FacturaCompraDomain`, `NotaDebitoDomain` y `NotaDebitoCompraDomain` lo invocan en `InsertarAsync` (antes de numerar, para que un error no consuma el consecutivo) y asignan `obj.CondicionPago` y `obj.FechaVencimiento`. El cliente **no** puede enviar `FechaVencimiento` (se ignora). Actualizar un documento (`ActualizarAsync`) no recalcula nada: el documento solo admite cambiar el comentario o cancelarse.

### Socio de negocio
`SocioNegocioDomain` valida que `CondicionPago`, si viene, exista y esté activa (`InsertarAsync`/`ActualizarAsync`). Un socio sin condición usa `CON`.

### Saldo del socio — `ISaldoSocioService`
```
SaldoSocio(string CodigoSn, string Tipo, decimal Saldo, decimal Vencido, int DocumentosAbiertos)
Task<IReadOnlyDictionary<string, SaldoSocio>> ObtenerAsync(IEnumerable<string> codigosSn)
```
`Saldo` = suma de `Saldo` de los documentos abiertos del socio, usando `ISaldoFacturaService` (clientes: facturas y notas de débito no canceladas) o `ISaldoFacturaCompraService` (proveedores); `Vencido` = suma de los saldos con `COALESCE(FechaVencimiento, FechaDoc) < hoy`. Cargas agrupadas (una consulta de documentos por tipo y una de saldos), no una por socio.

### Estado de cuenta — `IEstadoCuentaService.ObtenerAsync(codigoSn, desde, hasta)`
```
MovimientoCuenta(DateTime Fecha, string Tipo, int NumDoc, string Descripcion, decimal Cargo, decimal Abono, decimal SaldoAcumulado, bool Informativo)
EstadoCuenta(SocioNegocio Socio, DateTime Desde, DateTime Hasta, decimal SaldoInicial, IReadOnlyList<MovimientoCuenta> Movimientos, decimal SaldoFinal, decimal SaldoActual, decimal Vencido)
```
- **Cliente:** *cargos* = facturas y notas de débito (`FechaDoc`), *abonos* = notas de crédito **con factura base** (`FechaDoc`) y pagos recibidos (`FechaDoc`). **Proveedor:** el mismo esquema con `FacturaCompra`/`NotaCreditoCompra`/`PagoEfectuado` (el saldo es lo que se debe al proveedor).
- Solo documentos y pagos **no cancelados**. Las notas de crédito sin factura base aparecen con `Informativo=true`, sin monto en cargo/abono y sin afectar el acumulado.
- `SaldoInicial` = Σ cargos − Σ abonos de todos los movimientos con fecha **anterior a `desde`**; `SaldoAcumulado` corre movimiento a movimiento ordenado por fecha y luego por tipo y número; `SaldoFinal` = saldo al `hasta`; `SaldoActual` y `Vencido` salen de `ISaldoSocioService` (hoy). Si el rango abarca toda la historia hasta hoy, `SaldoFinal == SaldoActual` (invariante verificado por prueba: Σ(total − acreditado − pagado) de los documentos abiertos).
- Rango por defecto: inicio del año en curso hasta hoy; `desde > hasta` se rechaza con mensaje.

### Antigüedad de saldos — `IAntiguedadService`
```
FilaAntiguedad(string CodigoSn, string NombreSn, decimal Vigente, decimal De1a30, decimal De31a60, decimal De61a90, decimal Mas90, decimal Total)
DocumentoAntiguedad(int Entry, string Tipo, int NumDoc, DateTime FechaDoc, DateTime Vencimiento, int DiasVencido, decimal Saldo)

Task<IReadOnlyList<FilaAntiguedad>> ObtenerAsync(string tipo /* "C" | "P" */)
Task<IReadOnlyList<DocumentoAntiguedad>> ObtenerDetalleAsync(string tipo, string codigoSn)
```
`DiasVencido = (hoy − Vencimiento).Days`; `≤ 0` → Vigente; `1-30`, `31-60`, `61-90`, `> 90`. Solo documentos con `Saldo > 0` y no cancelados; los socios sin saldo no aparecen; la fila `Total` general se arma en la pantalla. Usa los servicios de saldo existentes (consultas agrupadas) y las nuevas columnas de vencimiento.

## §3 — API

- `CondicionPagoController` (`api/CondicionPago`): `GET` (Ver `CondicionesPago`), `GET {codigo}`, `POST` (Agregar), `PUT {codigo}` (Actualizar), `DELETE {codigo}` (Eliminar = baja lógica).
- `SocioNegocio` y sus DTOs ganan `CondicionPago`.
- `Factura*`, `NotaDebito*`, `FacturaCompra*`, `NotaDebitoCompra*` (DTOs de **lectura**) ganan `CondicionPago` y `FechaVencimiento`; los DTOs de **creación** ganan solo `CondicionPago` (opcional).
- `EstadoCuentaController` (`api/EstadoCuenta`): `GET {codigoSn}?desde=&hasta=` (Ver `EstadoCuenta`) y `POST api/EstadoCuenta/Saldos` (Ver `EstadoCuenta`) con el cuerpo `["C001","P003"]`, que devuelve el `SaldoSocio` de cada socio para la ficha y el listado de socios (POST por la misma razón que los saldos de facturas: un listado grande no cabe en la URL).
- `AntiguedadController`: `GET api/Antiguedad/Cobrar` y `GET api/Antiguedad/Pagar` (Ver `AntiguedadCobrar`/`AntiguedadPagar`); `GET api/Antiguedad/Cobrar/{codigoSn}` y `.../Pagar/{codigoSn}` con el detalle de documentos.
- Mensajes de error en español, estilo existente; validaciones de formato con `[Required]`/`[StringLength]`.
- **Compatibilidad:** los clientes existentes (incluido `Importar-FacturasCompra.ps1`) siguen funcionando sin enviar condición: el servidor usa la del socio o `CON`.

## §4 — Web

**Menú:** "Condiciones de pago" y "Estado de cuenta" en Socios de negocio; "Antigüedad de saldos por cobrar" en Ventas; "Antigüedad de saldos por pagar" en Compras (cada una visible con Ver en su módulo).

**Condiciones de pago** (`CondicionesPagoController`, `condicionespago.js`): listado y alta/edición en modal como Formas de pago (código, nombre, días, activo); baja lógica con confirmación; `CON` sin botón de baja.

**Socio de negocio:** campo "Condición de pago" (lista de condiciones activas) en la pestaña Comercial del alta y la edición; en la ficha se muestra el **saldo** y lo **vencido** del socio con un enlace "Ver estado de cuenta"; en el listado de socios, columnas Saldo y Vencido (se piden en una sola llamada para el conjunto listado).

**Documentos** (Facturas, Notas de débito, Facturas de compra, Notas de débito de proveedor): al elegir el socio se precarga su condición en una lista "Condición de pago" (editable) y se muestra el **vencimiento** calculado (solo lectura, la regla vive en el servidor; el formulario lo recalcula al cambiar fecha o condición solo para mostrarlo); los listados agregan la columna **Vence** y la insignia **Vencida** cuando hay saldo y la fecha ya pasó. Copiar a precarga la condición del socio.

**Estado de cuenta** (`EstadoCuentaController`, `estadocuenta.js`): página con buscador de socio (cliente o proveedor), rango de fechas, tabla de movimientos (fecha, tipo y número, descripción, cargo, abono, saldo) con saldo inicial y final, y resumen "Saldo actual / Vencido"; las notas sin base se muestran atenuadas con la etiqueta "sin aplicar". Se abre desde la ficha del socio (`?codigoSn=`) y desde los listados de facturas.

**Antigüedad** (`AntiguedadController`, dos páginas): tabla por socio con Vigente, 1-30, 31-60, 61-90, Más de 90 y Total, fila de totales, ordenable; clic en un socio abre un modal con sus documentos (fecha, vencimiento, días de atraso, saldo) y un enlace a su estado de cuenta. Las columnas vencidas se resaltan.

Todas las acciones MVC llevan `RequierePermiso` y `[ValidateAntiForgeryToken]` en POST; el texto de usuario se escapa al pintar tablas.

## §5 — Pruebas

- **Dominio (xUnit + Moq):** `CondicionPagoDomain` (alta, duplicado, baja, `CON` protegida, edición de días); `ResolutorVencimiento` (pedida > socio > `CON`; condición inexistente o inactiva; día inicial y fin de mes; 0 días); los cuatro dominios de inserción asignan condición y vencimiento y **no consumen consecutivo si la condición es inválida**; `SocioNegocioDomain` valida la condición; `SaldoSocioService` (varios socios, vencido solo con saldo > 0, cancelados fuera); `EstadoCuentaService` (saldo inicial, orden, acumulado, notas sin base informativas, pagos cancelados fuera, **invariante saldo final = suma de saldos abiertos**, cliente y proveedor); `AntiguedadService` (cortes en los límites 0, 1, 30, 31, 60, 61, 90, 91 días, documentos sin saldo fuera).
- **Controladores:** 403 sin permiso, 400 de dominio, 404; permisos fijados por reflexión como en Pagos.
- **Smoke contra SQL Server real** (`smoke-vencimientos.ps1`): migración de históricos (facturas previas quedan `CON` y vencen en `FechaDoc`); alta con condición explícita, con la del socio y sin ninguna; vencimiento correcto; condición inválida rechazada sin consumir número; estado de cuenta de un cliente y de un proveedor tras factura, nota de débito, nota de crédito con base, pago parcial y pago cancelado (saldos esperados cifra por cifra); invariante contra `Saldos`; antigüedad con documentos en cada tramo (creados con `FechaDoc` retrocedida); la suite de pagos recibidos y efectuados sigue pasando (54 y 51).
- **Recorrido en navegador:** catálogo de condiciones, condición del socio, alta de factura con condición y vencimiento visible, listados con "Vence/Vencida", estado de cuenta de un cliente y de un proveedor, ambas antigüedades con el detalle.

## §6 — Despliegue y riesgos

- Ejecutar `sql/2026-10-26-condiciones-pago-vencimientos.sql` en `API_DB_TEST` (idempotente; requiere las tablas de la Fase A) y asignar los módulos nuevos a las plantillas de permisos.
- **Efecto visible de la migración:** todas las facturas existentes con saldo aparecerán **vencidas** desde su fecha. Es lo acordado; para reflejar el crédito real de un proveedor hay que asignar la condición correcta a sus facturas abiertas (no hay edición masiva en esta fase; la corrección es por SQL o en una fase posterior).
- **Rendimiento:** estado de cuenta y antigüedad recorren documentos y pagos del socio o del conjunto; para el volumen esperado es adecuado. Los índices por vencimiento cubren la antigüedad.
- **Riesgo aceptado:** sin corte retroactivo, la antigüedad de ayer no se puede reproducir; si se necesita, se guardará una foto periódica en una fase futura.
- **Riesgo aceptado:** el estado de cuenta ignora las notas de crédito sin factura base (no hay vínculo para aplicarlas); la fase futura "aplicar notas de crédito dentro del pago" (estilo SAP) las resolvería.

## Hoja de ruta de la Fase C — límite de crédito (no forma parte de esta entrega)

Campo `LimiteCredito` en el socio de negocio (como *Credit Limit* de SAP B1) y control al crear **facturas, entregas y pedidos de venta**, comparando el límite contra el **saldo del cliente** de esta fase más lo comprometido. Decisiones por tomar al diseñarla: si bloquea o solo advierte (en SAP advierte y pide autorización); qué cuenta como comprometido (pedidos y entregas aún no facturados); permiso especial para saltarlo; si 0 significa "sin límite"; y si el saldo vencido bloquea aunque no se haya llegado al límite.
