# Notas de débito dentro de Factura/FacturaCompra (DocSubType) — diseño

Fecha: 2026-10-17 · Repos: API (rama `desarrollo`) y Web (rama `main`)

## 1. Contexto y objetivo

Hoy las notas de débito viven en cuatro tablas propias (`NotaDebito`, `NotaDebitoDetalle`,
`NotaDebitoCompra`, `NotaDebitoCompraDetalle`; objetos 7 y 14). En SAP B1 la nota de débito
(deudores/acreedores) se guarda en la misma tabla que la factura (`OINV`/`OPCH`) y se distingue por
`DocSubType`. Decisión del usuario: seguir el modelo de SAP.

**Objetivo:** guardar las notas de débito de cliente en `Factura`/`FacturaDetalle` y las de proveedor
en `FacturaCompra`/`FacturaCompraDetalle`, diferenciadas por un campo `DocSubType`, conservando el
comportamiento, los endpoints, los permisos y las pantallas de notas de débito que ya existen.

Las notas de crédito siguen en tablas propias (como `ORIN`/`ORPC` en SAP).

## 2. Decisiones tomadas (brainstorming)

1. **Alcance: cliente y proveedor.** `Factura` (subtipo `ND`) y `FacturaCompra` (subtipo `DN`). Se
   eliminan las cuatro tablas `NotaDebito*`, sus entidades EF y sus repositorios.
2. **Mismo objeto + `DocSubType`.** `TipoObjeto` de una nota sigue siendo `'6'` (venta) / `'13'`
   (compra); la nota se distingue solo por `DocSubType`. Numeración propia con el `SubTipoDoc` que ya
   existe en `NumeracionDocumento`: filas `(6,'ND')` y `(13,'DN')`. Los objetos 7 y 14 quedan libres.
3. **Superficie sin cambios.** Se mantienen `api/NotaDebito` y `api/NotaDebitoCompra`, los DTOs, los
   módulos de permiso `NotasDebito`/`NotasDebitoCompra` y las pantallas Web; solo cambia dónde se
   guardan los datos.
4. **Enfoque A:** `NotaDebitoDomain`/`NotaDebitoCompraDomain` conservan sus reglas (líneas de
   concepto, sin inventario, factura base opcional, serie de nota) pero usan los repositorios de
   `Factura`/`FacturaDetalle` (y de compra), fijando siempre el subtipo. `FacturaDomain` y
   `FacturaCompraDomain` solo ganan el filtro de subtipo `'--'`. (Se descartó meter las reglas de
   nota dentro de `FacturaDomain` con ramas por subtipo: mezcla inventario y conceptos sin ganancia
   para el usuario.)
5. **Las líneas de la nota siguen siendo conceptos** (sin artículo ni almacén) y la nota **nunca**
   mueve inventario (igual que hoy).

## 3. Datos (SQL)

### 3.1 Cambios en `Factura` y `FacturaCompra`

- Columna nueva `DocSubType nvarchar(2) NOT NULL` con default `'--'`:
  - `Factura`: `CHECK (DocSubType IN ('--','ND'))`
  - `FacturaCompra`: `CHECK (DocSubType IN ('--','DN'))`
  - `'--'` = factura normal. Mismo vocabulario que `NumeracionDocumento.SubTipoDoc`.
- `BaseTipo` del encabezado: la restricción actual `CHECK (BaseTipo IN (-1))` se reemplaza por una
  restricción con nombre que depende del subtipo: factura normal `BaseTipo = -1`; nota de débito
  `BaseTipo IN (-1, 6)` (compra: `-1, 13`). `BaseEntry` sigue sin FK (igual que en las notas actuales).
- Detalle (`FacturaDetalle`, `FacturaCompraDetalle`): `CodArticulo` y `CodAlmacen` pasan a
  `NULL` permitido (las FK a `Articulo`/`Almacen` se conservan, con valor nulo no se validan). La
  obligatoriedad para facturas normales ya la exige `FacturaDomain` (ver §5), no el esquema.
- `Descripcion` ya es opcional en el detalle de factura; para las notas la exige el dominio (como
  hoy: `Descripcion` obligatoria en el DTO de línea).
- Índice de apoyo para los listados por subtipo: `IX_Factura_DocSubType` sobre `DocSubType`
  (idempotente), y el equivalente en compra.
- Las restricciones antiguas se crearon **sin nombre**; el script las localiza por su definición en
  `sys.check_constraints` (columna + `IN (-1)`/`IN ('6')`) antes de eliminarlas, y es idempotente.

### 3.2 `TipoObjeto` de las notas

`Factura.TipoObjeto` (`CHECK IN ('6')`) y `FacturaCompra.TipoObjeto` (`'13'`) no cambian: la nota guarda
`'6'`/`'13'`. Tampoco cambia el `CHECK` del detalle. Esto es lo que SAP hace con `ObjType 13` +
`DocSubType`.

### 3.3 Numeración

- `NumeracionDocumento`: filas nuevas `(6,'ND', DocAlias 'Nota Debito Cliente')` y
  `(13,'DN', 'Nota Debito Prov.')`; se eliminan las filas `(7,'--')` y `(14,'--')` del complete script y
  de la migración (nunca se aplicaron a ninguna base).
- `NumeracionDocumentoDet`: las series de notas de débito (hoy 32-35: Manual/Primario de cada una)
  pasan a `CodigoObj` `'6'`/`'13'` con `SubTipoDoc` `'ND'`/`'DN'`. `SerieDfct` de cada fila nueva apunta a
  su serie Primaria. Los números de serie no cambian.
- Las series de notas de crédito (8/15, series 36-39) no cambian.

### 3.4 Módulos de permiso

`NotasDebito` (grupo Ventas) y `NotasDebitoCompra` (grupo Compras) se conservan tal cual.

## 4. Migración y scripts

- Se **elimina** `sql/2026-10-04-notas-debito.sql` (nunca se aplicó a una base con datos; ya no debe
  ejecutarse) y se **crea** `sql/2026-10-17-notas-debito-en-factura.sql`, UTF-8, idempotente, que:
  1. agrega `DocSubType` y reemplaza las restricciones de §3.1 en las cuatro tablas;
  2. crea la numeración de §3.3 (si las series 32-35 aún no existen, las crea; si existen con
     `CodigoObj` 7/14, las reasigna);
  3. asegura los módulos de §3.4;
  4. si existen las tablas `NotaDebito*`: si están **vacías** las elimina; si tienen **filas**
     aborta con un mensaje explícito y sin cambiar nada (la copia de datos no se implementa: no hay
     datos conocidos; si algún día hacen falta se agrega como cambio aparte).
- `sql/crear-api-db-test-completo.sql` se actualiza al esquema final (sin `NotaDebito*`; con
  `DocSubType`, restricciones, numeración y series nuevas). `sql/2026-10-05-notas-credito.sql` no se
  toca.
- Prueba en base temporal (nunca `API_DB_TEST`): (a) base nueva desde el complete script; (b) base con
  el esquema anterior (script viejo aplicado + tablas vacías) y migración; (c) misma base con una fila de
  prueba → la migración aborta sin cambios.

## 5. API

- **Entidades EF:** `Factura` y `FacturaCompra` ganan `DocSubType` (string, default `'--'`) con su
  mapeo; el detalle gana `CodArticulo`/`CodAlmacen` nulos (ya lo son en C#). Se quitan `NotaDebito*`
  de `ApiDbTestContext` (DbSets, bloques de mapeo y las colecciones de navegación de `SocioNegocio`,
  `Monedum`, `NumeracionDocumentoDet`, `Impuesto`).
- **Repositorios:** se eliminan los de `NotaDebito*`; los dominios de nota usan los genéricos de
  `Factura`/`FacturaDetalle`/`FacturaCompra`/`FacturaCompraDetalle`.
- **`NotaDebitoDomain`/`NotaDebitoCompraDomain`:**
  - fuerzan `DocSubType = 'ND'`/`'DN'`, `TipoObjeto = '6'`/`'13'`, `EstadoInv = 'C'` (explícito, para que
    el default `'A'` de la base no deje la nota con inventario pendiente), `Cancelado='N'`,
    `EstadoDoc='A'`;
  - todas sus consultas (`ObtenerTodo`, `Obtener`, `Lineas`, actualizar/cancelar/eliminar) filtran por su
    subtipo: un `Entry` de factura normal responde "no existe" para la API de notas;
  - validan la serie con `CodigoObj` = `'6'`/`'13'` **y** `SubTipoDoc` = `'ND'`/`'DN'`;
  - la factura base opcional sigue siendo una factura normal (`DocSubType='--'`) no cancelada, del tipo
    de socio correcto; una nota de débito **no** puede basarse en otra nota de débito.
- **`FacturaDomain`/`FacturaCompraDomain`/`FacturaDetalleDomain`:** `ObtenerTodo`, `Obtener`,
  `Actualizar`, `Eliminar` y la lectura de líneas filtran `DocSubType = '--'`, de modo que las notas no
  aparecen en Facturas ni pueden modificarse por sus endpoints ni leerse con solo el permiso `Facturas`.
  `InsertarAsync` fija `DocSubType = '--'` y valida que la serie tenga `CodigoObj` `'6'`/`'13'` y
  `SubTipoDoc` `'--'` (hoy la factura no valida a qué objeto pertenece la serie; sin esta validación una
  serie de nota serviría para una factura).
- **AutoMapper:** `CreateMap<Factura, NotaDebitoDTO>` (y detalle, y los DTOs de base) en lugar de las
  entidades `NotaDebito*`; los DTOs públicos no cambian. Los mapas `Factura → FacturaDTO` existentes
  siguen intactos.
- **Numeración (API):** hoy `NumeracionDocumento` se identifica solo por `CodigoObj` (`GET/PUT/DELETE
  api/NumeracionDocumento/{codigo}` y `GET api/NumeracionDocumentoDet/PorDocumento/{codigoObj}`), lo cual
  ya es ambiguo con las dos filas del objeto 1 (`C` y `P`) y lo sería con `(6,'--')`/`(6,'ND')`. Se agrega
  un parámetro opcional `subTipo` a esos endpoints: si llega, filtra por `(CodigoObj, SubTipoDoc)` exacto;
  si no, conserva el comportamiento actual. La pantalla Web de numeración lo envía desde la fila.
- **Compatibilidad:** rutas, DTOs, códigos de permiso y mensajes de las notas de débito no cambian.

## 6. Notas de crédito

- La factura base de una nota de crédito puede ser una factura normal **o una nota de débito** (como en
  SAP). `BaseTipo` sigue siendo `6`/`13` y `Entry` ya no se repite entre ambas porque comparten tabla.
- `NotaCreditoDomain`/`NotaCreditoCompraDomain` dejan de filtrar por subtipo en la factura base; el
  límite acreditable (Σ notas de crédito no canceladas ≤ `TotalDoc` de la base) es igual.
- Las pantallas de nota de crédito listan como facturas base las facturas y las notas de débito del
  socio (el selector indica el tipo). Los endpoints `FacturasBase/{codigoSn}` y `FacturaBase/{entry}` de
  crédito incluyen ambos subtipos; los de débito solo `'--'`.
- Las líneas copiadas de una nota de débito base se leen igual que las de una factura: las notas de
  crédito no usan artículo ni almacén.

## 7. Web

- Sin cambios funcionales en las pantallas de notas de débito (el contrato de la API no cambia), salvo:
  - las constantes de CodigoObj de las notas de débito pasan de `7`/`14` a `6`/`13` con
    `SubTipoDoc` `ND`/`DN`, y el selector de series filtra por ese subtipo (el de Facturas ya filtra
    por `SubTipoDoc == "--"`);
  - la pantalla de numeración de documentos envía `subTipo` en editar/ver series;
  - el selector de factura base de notas de crédito muestra también las notas de débito.
- Las pantallas de Facturas no cambian (la API ya excluye las notas).

## 8. Pruebas

- Pruebas de dominio/controller/aplicación de notas de débito: se adaptan al cambio de entidad
  (`NotaDebito*` → `Factura*`) manteniendo cada aserción; las pruebas del modelo EF se reescriben para el
  nuevo mapeo.
- Regresión nueva: (a) `FacturaDomain` ignora filas `ND` en listar/obtener/actualizar/eliminar/líneas;
  (b) serie de nota rechazada en una factura y serie de factura rechazada en una nota; (c) la nota no
  puede basarse en otra nota; (d) nota de crédito sobre una nota de débito (límite y cálculo);
  (e) endpoints de numeración con `subTipo`.
- Humo de la API contra una base temporal: crear una nota de débito, verificar que no aparece en
  `api/Factura`, que `api/Factura/{entryDeNota}` responde "no existe", crear una nota de crédito sobre la
  nota de débito y rebasar el límite.
- Compilación de la Web en un worktree limpio y checklist manual (las pantallas de notas siguen igual).

## 9. Riesgos y notas

- **Núcleo de facturación:** se toca `Factura`/`FacturaCompra`; el riesgo es olvidar algún consumidor. El
  plan incluye una auditoría explícita de toda consulta que lea esas tablas (hoy: los dominios de
  factura, nota de débito/crédito y las asignaciones de `PerfilMapeo`).
- **Ambigüedad de `NumeracionDocumento` por `CodigoObj`:** preexistente (objeto 1: `C`/`P`); se corrige con
  el parámetro opcional de §5. Cualquier otro lector de numeración por `CodigoObj` queda como estaba.
- **Costo de la unificación:** los listados de facturas dependen del filtro de subtipo; una consulta nueva
  sobre `Factura` debe acordarse de filtrar (se documenta en el código del repositorio/dominio).
- **Saldos de socio / cobros:** siguen fuera de alcance; cuando existan, facturas y notas de débito
  (misma tabla) y notas de crédito (otra) se combinan sin cambiar este modelo.
- **Datos existentes:** se asume que no hay notas de débito guardadas en ninguna base (el script
  anterior no se aplicó). Si las hubiera, la migración aborta sin tocar nada.

## 10. Fuera de alcance

Notas de crédito dentro de `Factura`; contabilidad y saldos; artículos o inventario en notas de
débito/crédito; copia de datos desde las tablas `NotaDebito*`; cambios en otros documentos
(cotizaciones, pedidos, entregas).
