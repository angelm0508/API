# Notas de crédito (cliente y proveedor) — Diseño

**Fecha:** 2026-10-04
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Documento hermano:** `2026-10-04-notas-debito-design.md` (ya implementado). Este diseño **clona esa pila** y solo describe lo que cambia o se agrega.

## Objetivo

Agregar dos documentos nuevos con su tipo de objeto, numeración, tablas, API y pantallas Web:

- **Nota de Crédito Cliente** (ventas): reduce lo que un cliente le debe a la empresa.
- **Nota de Crédito Proveedor** (compras): reduce lo que la empresa le debe a un proveedor.

Referencia de comportamiento: **SAP Business One** (A/R Credit Memo `ORIN` y A/P Credit Memo `ORPC`):
reduce el saldo del socio; sirve para descuentos, correcciones de precio y ajustes; se crea libre o
copiando desde una factura, y al copiar de una factura no puede acreditarse más de lo facturado.
Como en la nota de débito, se crean tablas y tipos de objeto propios (no se reutilizan las tablas de
factura con un subtipo).

### Decisiones tomadas (brainstorming)

1. **Solo monetaria, sin inventario.** Líneas de concepto libre (descripción, cantidad, precio,
   descuento, impuesto), sin artículo ni almacén. Una nota de crédito **nunca** mueve inventario. Las
   devoluciones de mercadería quedan para un documento "Devolución" futuro (en SAP la devolución va
   antes de la nota de crédito).
2. **Documento base opcional con límite.** Se puede crear libre o "en base a" una factura (Factura de
   venta para la de cliente, Factura de compra para la de proveedor). Con factura base, la suma de las
   notas de crédito **no canceladas** sobre esa factura no puede exceder el total de la factura. Sin
   factura base no hay límite. La nota no modifica ni cierra la factura.
3. **Pila clonada de la nota de débito** (dos pares de tablas independientes).
4. **Tipos de objeto nuevos:** Nota de Crédito Cliente = `8`, Nota de Crédito Proveedor = `15`
   (continúan las secuencias de ventas 3-7 y compras 10-14). `SubTipoDoc='--'`.
5. **Permisos:** módulos nuevos `NotasCredito` (grupo Ventas) y `NotasCreditoCompra` (grupo Compras),
   acceso denegado por defecto (solo `SuperUsuario` se salta la matriz).
6. **Líneas inmutables tras crear el documento** (igual que Factura y Nota de Débito).
7. **Totales calculados por el servidor** (a diferencia de la nota de débito, que los acepta del
   cliente): el límite sobre la factura base solo es confiable si el total de cada nota es el real.

### Fuera de alcance

Saldos de socios, cobros y pagos, asientos contables, documento "Devolución", movimiento de
inventario, copia de líneas parciales/por cantidad abierta, un permiso especial para exceder el
límite, y el botón "Copiar a nota de crédito" desde el listado de facturas.

## Contexto verificado

- La nota de débito (commits desde `a8aa3cb` en API y `3f88c08` en Web) ya implementa: entidades
  `NotaDebito*`, repos, DTOs, `NotaDebitoDomain`/`NotaDebitoCompraDomain` (serie, socio C/P, moneda,
  líneas, factura base, cancelar/eliminar, `Imprimido='N'` forzado), aplicación, controllers
  (`api/NotaDebito`, `api/NotaDebitoCompra`), DI, clientes y pantallas Web, menú y migración
  `sql/2026-10-04-notas-debito.sql`. Los endpoints de factura base exigen `Agregar` (solo los usa el
  formulario de creación).
- `IEjecutorTransaccion` tiene la sobrecarga `EjecutarAsync<T>(Func<Task<T>>, IsolationLevel)` (creada
  para usuarios): se usa `Serializable` para el chequeo del límite.
- Series de numeración en uso tras las notas de débito: 1-11, 20-35.
- Longitud máxima de `NumeracionDocumento.DocAlias`: 20 caracteres (`Nota Credito Cliente` ocupa
  exactamente 20).

## §1 — Datos

### Tablas (clon de las de débito)

`NotaCredito`, `NotaCreditoDetalle`, `NotaCreditoCompra`, `NotaCreditoCompraDetalle`: mismas columnas,
tipos, `CHECK` y `DEFAULT` que sus equivalentes `NotaDebito*`, con estas diferencias:

- `TipoObjeto` `CHECK ... IN ('8')` / `DEFAULT '8'` en cliente y `IN ('15')` / `DEFAULT '15'` en proveedor
  (también en el detalle).
- `BaseTipo` `CHECK (-1, 6)` en cliente y `(-1, 13)` en proveedor (igual que débito).
- Constraints con prefijo `nota_credito` (`pk_nota_credito`, `fk_nota_credito_serie`, `fk_nota_credito_sn`,
  `fk_nota_credito_moneda`, `pk_nota_credito_det`, `fk_nota_credito_det_cod_impuesto` y los `_compra`).
- Sin FK de `Entry` al encabezado en el detalle (igual que Factura/Débito); sí FK a `Impuesto`.

### Numeración (seed)

`NumeracionDocumento`: `CodigoObj` `8`, `DocAlias` `Nota Credito Cliente`, y `CodigoObj` `15`,
`DocAlias` `Nota Credito Prov.`, `SubTipoDoc` `--`. `NumeracionDocumentoDet`: series **Manual** y
**Primario** por documento (`TipoSerie='D'`, Primario con `IniNumero=1`, `SigNumero=1`) y
`SerieDfct` apuntando a la Primaria. En una base nueva (script completo) las series son 36 (Manual
cliente), 37 (Primario cliente), 38 (Manual proveedor) y 39 (Primario proveedor); en la migración se
toman las **siguientes series libres** (`MAX(Serie)+1`).

### Catálogo de permisos (seed `Modulo`)

`('NotasCredito', 'Notas de crédito', 'Ventas')` y `('NotasCreditoCompra', 'Notas de crédito de
compra', 'Compras')`, con `MERGE` idempotente que también corrige `Nombre`/`Grupo` si ya existían
distintos.

### Scripts SQL

`sql/2026-10-05-notas-credito.sql` (migración idempotente, mismo formato que
`2026-10-04-notas-debito.sql`, ejecutarlo con `sqlcmd -f 65001`) y `sql/crear-api-db-test-completo.sql`
actualizado para bases nuevas.

## §2 — API

Se clona la pila de la nota de débito (entidad EF + mapeo en `ApiDbTestContext`, repos, DTOs en
`notaCredito/` y `notaCreditoCompra/`, `NotaCreditoDomain`/`NotaCreditoCompraDomain`,
`NotaCreditoApplication`/`NotaCreditoCompraApplication`, `NotaCreditoController`/
`NotaCreditoCompraController`, DI en `Startup.cs`, mapeos en `PerfilMapeo`). Rutas, permisos y mensajes
son los de la nota de débito sustituyendo `NotaDebito`→`NotaCredito`, `NotasDebito`→`NotasCredito`,
`débito`→`crédito`. En particular: `GET {id}`, `GET`, `GET {id}/Lineas` (`Ver`), `GET FacturasBase/{codigoSn}` y
`GET FacturaBase/{entry}` (`Agregar`; en proveedor `FacturasCompraBase` / `FacturaCompraBase`), `POST`
(`Agregar`), `PUT {id}` (`Actualizar`, y `Eliminar` si `Cancelado=="S"`), `DELETE {id}` (`Eliminar`).

### Reglas propias de la nota de crédito (además de las de la de débito)

1. **Totales calculados por el servidor.** Al insertar, por cada línea: `bruto = Cantidad × Precio`,
   `subtotal = bruto − bruto × PrctjeDesc/100`, `Impuesto = subtotal × Impuesto.Tasa/100`,
   `TotalLinea = subtotal + Impuesto`, `PrecioBruto = bruto`, redondeando cada monto a 2 decimales
   (`MidpointRounding.AwayFromZero`). El encabezado se recalcula: `TotalBruto = Σ bruto`,
   `TotalDesc = Σ descuento de línea`, `TotalImp = Σ Impuesto`, `TotalDoc = Σ TotalLinea`; los valores que
   mande el cliente se ignoran. (La tasa sale de la entidad `Impuesto` que ya se consulta para validar
   el código.)
2. **Misma moneda que la factura base:** si hay base, `MonedaDoc` de la nota debe ser igual a la de la
   factura (mensaje: "La moneda de la nota de crédito debe ser la misma de la factura base.").
3. **Límite acreditable:** si hay base, `acreditado = Σ TotalDoc de las notas de crédito no canceladas
   con ese BaseEntry`; debe cumplirse `acreditado + TotalDoc de la nota ≤ TotalDoc de la factura`
   (comparado a 2 decimales). Mensaje: "El total acreditado ({acreditado + nota}) excede el total de
   la factura base ({total factura})." El chequeo y el insert van **en la misma transacción
   `Serializable`** (`EjecutarAsync(op, IsolationLevel.Serializable)`), para que dos notas simultáneas
   no excedan el límite. Cancelar una nota la excluye del acumulado; eliminar exige haberla cancelado
   antes (por lo que no afecta el acumulado).
4. Una nota sin base no valida moneda ni límite.

### Resumen de factura base con saldo acreditable

`GET FacturasBase/{codigoSn}` devuelve por factura, además de `Entry`, `NumDoc`, `FechaDoc`, `TotalDoc`,
`MonedaDoc`: `TotalAcreditado` (suma de notas no canceladas sobre esa factura) y `Disponible =
max(0, TotalDoc − TotalAcreditado)`. Solo se listan facturas no canceladas del socio (como en débito).
`GET FacturaBase/{entry}` devuelve la factura con sus líneas como conceptos (como en débito) más
`TotalAcreditado` y `Disponible`.

## §3 — Web

Clon de las pantallas de nota de débito (`NotasCreditoController`/`NotasCreditoCompraController`,
`Views/NotasCredito*`, `notascredito.js`/`notascreditocompra.js`, clientes `INotaCreditoApiClient`/
`INotaCreditoCompraApiClient`, DTOs `Web.ApiClient.Dtos.NotaCredito*`, registro en `Program.cs`).
Textos: "Notas de crédito", "Nueva nota de crédito", "Basado en factura (opcional)", etc.

Diferencias respecto de la de débito:

- El selector "Basado en factura" muestra junto a cada factura el saldo disponible
  (`#{numDoc} - fecha - moneda total (disponible: X)`) y **no lista facturas con `Disponible = 0`**.
- Al cargar la factura base se precargan socio, moneda y líneas editables (como en débito) y se muestra un
  aviso con el disponible; si el total calculado en pantalla excede el disponible se muestra una
  advertencia (el servidor es quien rechaza).
- Los totales que se muestran se calculan en pantalla con la misma fórmula, pero el servidor los
  recalcula; no se envían como autoritativos.
- Menú: "Notas de crédito" en Ventas y "Notas de crédito de compra" en Compras (`Puede("NotasCredito")` /
  `Puede("NotasCreditoCompra")`).
- Permisos, botones y cancelación idénticos a los de la nota de débito (cancelar exige `Eliminar`;
  `FormularioEditar` con `Ver`; `BuscarFacturasBase`/`ObtenerFacturaBase` con `Agregar`).

## §4 — Pruebas

API (xUnit + Moq, convenciones existentes): las de la nota de débito adaptadas (serie de otro objeto,
socio C/P, moneda, líneas, base inexistente/cancelada/de otro socio, cancelar, editar comentario,
eliminar exige cancelar, `Imprimido='N'`, permisos por reflexión) más las propias:

- recálculo de líneas y totales (impuesto con tasa, descuento, redondeo, ignora lo que manda el
  cliente);
- límite: dentro del límite, justo en el límite (igual al total), que lo excede (mensaje), con una nota
  previa cancelada que libera monto, con varias notas previas, moneda distinta (mensaje), sin base (sin
  límite), y que el chequeo corre dentro de `EjecutarAsync(..., Serializable)` (se verifica la
  sobrecarga);
- resumen de factura base con `TotalAcreditado`/`Disponible` (incluye `Disponible` mínimo 0).

Web: build limpio del árbol commiteado y `node --check`. Humo de la API contra una base temporal
(creación libre, en base a una factura, exceder el límite → 400, cancelar y volver a acreditar, proveedor,
403 sin permisos) y checklist manual.

## Riesgos y notas

- Despliegue: ejecutar `sql/2026-10-05-notas-credito.sql` (con `-f 65001`) en cada base y reiniciar API y
  Web; los módulos nuevos quedan sin asignar (acceso denegado por defecto).
- Un rol que solo tenga `NotasCredito` necesita además `Ver` sobre `SociosNegocio` y
  `NumeracionDocumento` (dependencia ya documentada para los demás documentos).
- El límite se calcula con el `TotalDoc` guardado de la factura (que viene del cliente en las facturas
  existentes): no se recalcula la factura base.
- Sin saldos ni cobros: la nota solo registra el documento; cuando existan cuentas por cobrar/pagar habrá
  que decidir cómo la nota las reduce.
- Dos notas simultáneas sobre la misma factura se serializan por la transacción `Serializable` (puede
  producirse un reintento del usuario por interbloqueo, como en usuarios).
