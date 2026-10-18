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
4. **Enfoque A:** `NotaDebitoDomain`/`NotaDebitoCompraDomain` conservan sus reglas (modo artículos o concepto,
   inventario solo en artículos, factura base opcional, serie de nota) pero usan los repositorios de
   `Factura`/`FacturaDetalle` (y de compra), fijando siempre el subtipo. `FacturaDomain` y
   `FacturaCompraDomain` solo ganan el filtro de subtipo `'--'`. (Se descartó meter las reglas de
   nota dentro de `FacturaDomain` con ramas por subtipo: mezcla inventario y conceptos sin ganancia
   para el usuario.)
5. **Modo de la nota elegido por el usuario, por documento** (como el `DocType` de SAP): una nota de
   débito es **por artículos** (`DocType = 'I'`) o **por concepto** (`DocType = 'S'`); todas sus líneas
   son del mismo tipo. Se guarda en el encabezado. Las facturas normales son siempre `'I'`.
6. **Inventario igual que su factura.** La nota de débito **por artículos** mueve stock como su
   factura: la de cliente da **salida** (como `Factura`) y la de proveedor da **entrada** (como
   `FacturaCompra`); cancelarla revierte el movimiento, y una salida sin existencia suficiente se
   rechaza. La nota **por concepto** no mueve inventario (como hoy).

## 3. Datos (SQL)

### 3.1 Cambios en `Factura` y `FacturaCompra`

- Columna nueva `DocSubType nvarchar(2) NOT NULL` con default `'--'`:
  - `Factura`: `CHECK (DocSubType IN ('--','ND'))`
  - `FacturaCompra`: `CHECK (DocSubType IN ('--','DN'))`
  - `'--'` = factura normal. Mismo vocabulario que `NumeracionDocumento.SubTipoDoc`.
- `BaseTipo` del encabezado: la restricción actual `CHECK (BaseTipo IN (-1))` se reemplaza por una
  restricción con nombre que depende del subtipo: factura normal `BaseTipo = -1`; nota de débito
  `BaseTipo IN (-1, 6)` (compra: `-1, 13`). `BaseEntry` sigue sin FK (igual que en las notas actuales).
- Columna nueva `DocType nvarchar(1) NOT NULL` con default `'I'` y `CHECK (DocType IN ('I','S'))`
  (`I` = artículos, `S` = concepto). Restricción cruzada con nombre: `DocSubType = '--'` exige
  `DocType = 'I'` (una factura normal siempre es por artículos).
- Detalle (`FacturaDetalle`, `FacturaCompraDetalle`): `CodArticulo` y `CodAlmacen` pasan a
  `NULL` permitido (las FK a `Articulo`/`Almacen` se conservan, con valor nulo no se validan). La
  obligatoriedad para facturas normales y notas por artículos la exige el dominio (ver §5), no el
  esquema.
- `Descripcion` ya es opcional en el detalle de factura; en las notas por concepto la exige el dominio
  (como hoy); en las notas por artículos es opcional, igual
  que en la factura.
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
  - fuerzan `DocSubType = 'ND'`/`'DN'`, `TipoObjeto = '6'`/`'13'`, `Cancelado='N'`, `EstadoDoc='A'`;
    `DocType` lo elige el usuario (`'I'` o `'S'`; obligatorio, sin default silencioso) y es inmutable tras
    crear la nota; `EstadoInv` se fija **siempre de forma explícita** (para que el default `'A'` de la
    base no decida): `'A'` en notas por artículos (hay asiento de inventario), `'C'` en notas por concepto;
  - **nota por concepto (`'S'`)**: sin artículo ni almacén (se rechazan si llegan), `Descripcion`
    obligatoria, sin movimiento de inventario (comportamiento actual);
  - **nota por artículos (`'I'`)**: cada línea exige `CodArticulo` y `CodAlmacen` existentes y
    `Cantidad > 0`; se registra el movimiento con `IInventarioAsientoService.AsentarAsync`
    (`TipoDoc` = `'6'`/`'13'`, `DocEntry` = `Entry` de la nota, que no se repite con las facturas porque
    comparten tabla) con la misma regla de la factura: cantidad negativa en cliente, positiva en proveedor,
    `PrecioUnitario` = precio de la línea, solo artículos de inventario mueven stock y una salida sin
    existencia se rechaza (`StockInsuficienteException`, igual mensaje que la factura). Las líneas de la
    nota llevan `BaseEntry` nulo (la factura base es solo referencia del encabezado), así que siempre mueven;
  - **cancelar** una nota por artículos llama a `RevertirAsync('6'/'13', Entry)` dentro de la misma
    transacción y pone `Cancelado='S'`, `EstadoInv='C'`; por concepto solo cambia las banderas. Eliminar
    sigue exigiendo cancelar primero;
  - todas sus consultas (`ObtenerTodo`, `Obtener`, `Lineas`, actualizar/cancelar/eliminar) filtran por su
    subtipo: un `Entry` de factura normal responde "no existe" para la API de notas;
  - validan la serie con `CodigoObj` = `'6'`/`'13'` **y** `SubTipoDoc` = `'ND'`/`'DN'`;
  - la factura base opcional sigue siendo una factura normal (`DocSubType='--'`) no cancelada, del tipo
    de socio correcto; una nota de débito **no** puede basarse en otra nota de débito. Al cargar la base,
    el endpoint devuelve las líneas con `CodArticulo`/`CodAlmacen`: una nota por artículos las precarga
    como artículos editables; una nota por concepto las convierte a conceptos (como hoy).
- **DTOs de nota de débito (cambio compatible):** `NotaDebitoCrearDTO` gana `DocType` (`'I'`/`'S'`;
  si no llega se rechaza con mensaje claro) y `NotaDebitoDetalleCrearDTO` gana `CodArticulo`/`CodAlmacen`
  opcionales; los DTOs de lectura devuelven ambos campos. Las rutas, los permisos y los demás campos
  no cambian. (La Web se actualiza en el mismo cambio; no hay otros consumidores.)
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
  ya es ambiguo con las dos filas del objeto 1 (`C` y `P`) y lo sería con `(6,'--')`/`(6,'ND')`. En vez
  de agregar parámetros a cada endpoint, el `{codigo}` acepta una **clave compuesta** `"CodigoObj:SubTipoDoc"`
  (p. ej. `6:ND`, `1:P`; el `:` no aparece en ningún CodigoObj y es válido en un segmento de URL): con `:` se
  filtra por `(CodigoObj, SubTipoDoc)` exacto; sin `:` se conserva el comportamiento actual (primera fila del
  objeto). El duplicado al insertar se detecta por `(CodigoObj, SubTipoDoc)`. `PorDocumento/{codigoObj}` no
  cambia (los controllers Web de documentos ya filtran las series por `SubTipoDoc`). La pantalla Web de
  numeración usa la clave compuesta al editar una fila.
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
  crédito siguen siendo monetarias, sin artículo, almacén ni movimiento de inventario (decisión previa
  de notas de crédito); acreditar una nota de débito por artículos **no** devuelve stock.

## 7. Web

- Las pantallas de notas de débito (cliente y proveedor) cambian así:
  - el formulario ofrece **"Tipo de nota: Artículos / Concepto"** (un selector al crear; no editable
    después). En modo Artículos el editor de líneas es el de Facturas/Facturas de compra (selector de
    artículo, almacén, cantidad, precio, descuento, impuesto; muestra existencias en cliente); en modo
    Concepto queda el editor actual. Cambiar de modo con líneas ya cargadas pide confirmación y las
    limpia. Al cargar una factura base se precargan según el modo elegido;
  - la tabla y el detalle de la nota muestran artículo y almacén en las notas por artículos y una
    columna "Tipo";
  - un usuario necesita `Ver` sobre `Articulos` y `Almacenes` para crear notas por artículos (igual que
    para facturas); se agrega a las notas de despliegue;
  - las constantes de CodigoObj de las notas de débito pasan de `7`/`14` a `6`/`13` con
    `SubTipoDoc` `ND`/`DN`, y el selector de series filtra por ese subtipo (el de Facturas ya filtra
    por `SubTipoDoc == "--"`);
  - la pantalla de numeración de documentos edita cada fila por su clave compuesta (`6`, `6:ND`, `1:C`…);
  - el selector de factura base de notas de crédito muestra también las notas de débito.
- Las pantallas de Facturas no cambian (la API ya excluye las notas).

## 8. Pruebas

- Pruebas de dominio/controller/aplicación de notas de débito: se adaptan al cambio de entidad
  (`NotaDebito*` → `Factura*`) manteniendo cada aserción; las pruebas del modelo EF se reescriben para el
  nuevo mapeo.
- Pruebas nuevas de inventario (patrón de `FacturaDomainTests`/`FacturaCompraDomainTests`): nota por
  artículos de cliente → `AsentarAsync` con cantidades negativas, de proveedor → positivas; por concepto →
  sin asiento; artículo/almacén requeridos en `'I'` y rechazados en `'S'`; `DocType` obligatorio y
  válido; `StockInsuficiente` se propaga sin dejar documento; cancelar llama `RevertirAsync`; `EstadoInv`
  `'A'`/`'C'` según el modo.
- Regresión nueva: (a) `FacturaDomain` ignora filas `ND` en listar/obtener/actualizar/eliminar/líneas;
  (b) serie de nota rechazada en una factura y serie de factura rechazada en una nota; (c) la nota no
  puede basarse en otra nota; (d) nota de crédito sobre una nota de débito (límite y cálculo);
  (e) numeración por clave compuesta (`6:ND`).
- Humo de la API contra una base temporal (con inventario real): nota por artículos de cliente baja la
  existencia y cancelarla la restaura; nota por artículos de proveedor la sube; salida mayor a la
  existencia → 400; nota por concepto no toca el stock. Además: crear una nota de débito, verificar que no aparece en
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
- **Kardex:** el movimiento de una nota por artículos se registra con `TipoDoc` `'6'`/`'13'` (el
  mismo de la factura) y `DocEntry` = `Entry` de la nota (único porque comparten tabla); en el historial
  de movimientos aparecerá con el alias del documento "Factura". Distinguirlo exige un campo nuevo en el
  movimiento y queda fuera de alcance.
- **Costo de las notas de proveedor por artículos:** las entradas usan el precio de la línea como
  costo unitario, igual que `FacturaCompra`; no se prorratea nada.
- **Saldos de socio / cobros:** siguen fuera de alcance; cuando existan, facturas y notas de débito
  (misma tabla) y notas de crédito (otra) se combinan sin cambiar este modelo.
- **Datos existentes:** se asume que no hay notas de débito guardadas en ninguna base (el script
  anterior no se aplicó). Si las hubiera, la migración aborta sin tocar nada.

## 10. Fuera de alcance

Notas de crédito dentro de `Factura`; contabilidad y saldos; artículos o inventario en notas de
crédito; mezclar artículos y conceptos en una misma nota; distinguir la nota en el kardex; copia de datos desde las tablas `NotaDebito*`; cambios en otros documentos
(cotizaciones, pedidos, entregas).
