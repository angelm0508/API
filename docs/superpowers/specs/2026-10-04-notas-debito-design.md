# Notas de débito (cliente y proveedor) — Diseño

**Fecha:** 2026-10-04
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

Agregar dos documentos nuevos, con su tipo de objeto, numeración, tablas, API y pantallas Web:

- **Nota de Débito Cliente** (ventas): aumenta lo que un cliente le debe a la empresa.
- **Nota de Débito Proveedor** (compras): aumenta lo que la empresa le debe a un proveedor.

Referencia de comportamiento: **SAP Business One**. En SAP la nota de débito no es un objeto aparte:
es una factura (A/R `OINV` objeto 13, A/P `OPCH` objeto 18) marcada con un subtipo de documento;
"funciona igual que una factura" y su efecto es subir el saldo del socio de negocio. Se puede
crear libre o copiando desde una factura. No se toma de SAP la identidad con la factura (mismas
tablas): se crean tablas y tipos de objeto propios, como pidió el usuario, y se conserva el
comportamiento.

### Decisiones tomadas (brainstorming)

1. **Líneas = conceptos, sin inventario.** Cada línea es un concepto libre (descripción, cantidad,
   precio, descuento, impuesto). Sin artículo ni almacén. Una nota de débito **nunca** mueve
   inventario.
2. **Documento base opcional, solo referencia.** Se puede crear sola o "en base a" una factura
   (Factura de venta para la nota de cliente, Factura de compra para la de proveedor). Al elegir
   la factura se precargan socio, moneda y líneas como conceptos editables. La nota guarda la
   referencia (`BaseTipo`/`BaseEntry` del encabezado) pero **no modifica ni cierra** la factura.
3. **Dos pares de tablas independientes**, clonados del patrón de Factura/FacturaCompra sin
   inventario (se descartó una tabla única con discriminador y reutilizar las tablas de factura
   con subtipo `ND`/`DN`).
4. **Tipos de objeto nuevos:** Nota de Débito Cliente = `7`, Nota de Débito Proveedor = `14`
   (continúan las secuencias 3-6 de ventas y 10-13 de compras). Numeración con `SubTipoDoc='--'`
   como el resto de los documentos (los subtipos `ND`/`DN` del `CHECK` de numeración quedan sin
   usar).
5. **Permisos:** módulos nuevos `NotasDebito` (grupo Ventas) y `NotasDebitoCompra` (grupo Compras),
   con el modelo vigente: acceso denegado por defecto, solo `SuperUsuario` se salta la matriz.

### Fuera de alcance

Saldos de socios, cobros y pagos, asientos contables, el botón "Copiar a nota de débito" desde el
listado de facturas, nota de crédito (no existe en el sistema), multi-factura base, recálculo de
totales en el servidor (se mantiene la paridad con Factura: los totales viajan en el DTO).

## Contexto verificado

- `Factura`/`FacturaCompra` (+`Detalle`): entidades EF scaffold-style con navegaciones, repos
  `FacturaRepositorio`/`FacturaDetalleRepositorio`, `FacturaDomain`/`FacturaDetalleDomain`,
  `FacturaApplication`/`FacturaDetalleApplication`, `FacturaController`/`FacturaDetalleController`
  (`[RequierePermiso("Facturas", ...)]`), DTOs en `API.Application.DTO/factura/`, registros en
  `Startup.cs` (`AddTransient`), mapeos en `PerfilMapeo.cs`.
- Numeración: `NumeracionDocumento (CodigoObj, SubTipoDoc, DocAlias, SerieDfct)` y
  `NumeracionDocumentoDet (Serie, CodigoObj, NombreSerie, SigNumero, Manual, Bloqueado ...)`. Series
  usadas hoy: 1-11, 20-31. El domain de cada documento toma `SigNumero` de la serie elegida
  (series Manual piden `NumDoc`).
- `FacturaDomain.ActualizarAsync` solo permite cancelar (`Cancelado='S'`) o editar el comentario;
  un documento cancelado no se puede modificar; `EliminarAsync` exige cancelar primero y borra
  líneas y encabezado en una transacción. Cancelar un documento exige el permiso `Eliminar` además
  de `Actualizar` (regla vigente para los documentos que cancelan, ver spec de permisos).
- Permisos: `RequierePermiso(<modulo>, Accion)` en API y Web; el catálogo vive en la tabla `Modulo`
  (21 módulos hoy); sin filas en `UsuarioPermiso` un usuario no tiene acceso.
- Los formularios de documentos obtienen socios (`TipoSN` `C` o `P`), impuestos y series de otros
  módulos; un rol restringido necesita `Ver` sobre `SociosNegocio` y `NumeracionDocumento`
  (dependencia conocida entre módulos, documentada en el spec de permisos).

## §1 — Datos

### Tablas (clonadas de `Factura`/`FacturaCompra`; sin `EstadoInv`)

`NotaDebito`: `Entry` int identity PK, `NumDoc` int NOT NULL, `Serie` int NOT NULL (FK
`NumeracionDocumentoDet`), `Cancelado`/`NumManual`/`Imprimido` nvarchar(1) CHECK `('S','N')`
DEFAULT `'N'`, `EstadoDoc` nvarchar(1) CHECK `('A','C')` DEFAULT `'A'`, `TipoObjeto` nvarchar(11)
CHECK `('7')` DEFAULT `'7'`, `FechaDoc`/`FechaEmision` datetime NOT NULL, `FechaCancelado`
datetime, `CodigoSN` nvarchar(15) NOT NULL (FK `SocioNegocio`), `NombreSN` nvarchar(200),
`Direccion` nvarchar(254), `MonedaDoc` nvarchar(3) NOT NULL (FK `Moneda`), `BaseTipo` int CHECK
`(-1, 6)` DEFAULT `-1`, `BaseEntry` int (referencia a `Factura.Entry`, sin FK, como el resto de
las referencias base), `PrctjeImpuesto`/`TotalImp`/`PrctjeDesc`/`TotalDesc`/`TotalBruto`/
`TotalDoc` decimal(19,6), `Comentario` nvarchar(254).

`NotaDebitoDetalle`: `Entry` int, `NoLinea` int (PK compuesta), `Descripcion` nvarchar(200) NOT
NULL, `Cantidad`/`Precio`/`PrecioBruto`/`PrctjeDesc`/`Impuesto`/`TotalLinea` decimal(19,6),
`CodigoImpuesto` nvarchar(8) NOT NULL (FK `Impuesto`), `TipoObjeto` nvarchar(20) CHECK `('7')`
DEFAULT `'7'`. FK al encabezado por `Entry` (sin cascade; el dominio borra las líneas).

`NotaDebitoCompra` / `NotaDebitoCompraDetalle`: idéntico, con `TipoObjeto` `'14'` y `BaseTipo` CHECK
`(-1, 13)` (13 = Factura de Compra). Sin columnas de artículo, almacén ni referencias por línea
(`TipoDocDestino`, `BaseRef`, `BaseLinea`): la referencia base es solo del encabezado.

### Numeración (seed)

| `NumeracionDocumento` | `CodigoObj` | `SerieDfct` | `DocAlias` | `SubTipoDoc` |
|---|---|---|---|---|
| Nota Débito Cliente | `7` | `33` | `Nota Debito Cliente` | `--` |
| Nota Débito Proveedor | `14` | `35` | `Nota Debito Proveedor` | `--` |

| `NumeracionDocumentoDet` | `CodigoObj` | `Serie` | `NombreSerie` | extra |
|---|---|---|---|---|
| Manual cliente | `7` | `32` | `Manual` | `Manual='S'`, `TipoSerie='D'` |
| Primario cliente | `7` | `33` | `Primario` | `IniNumero=1`, `SigNumero=1`, `TipoSerie='D'` |
| Manual proveedor | `14` | `34` | `Manual` | `Manual='S'`, `TipoSerie='D'` |
| Primario proveedor | `14` | `35` | `Primario` | `IniNumero=1`, `SigNumero=1`, `TipoSerie='D'` |

(Mismo formato que los documentos existentes; `SubTipoDoc='--'`, `Bloqueado='N'`.)

### Catálogo de permisos (seed `Modulo`)

`('NotasDebito', 'Notas de débito', 'Ventas')` y `('NotasDebitoCompra', 'Notas de débito de
compra', 'Compras')`, insertados con `MERGE` idempotente.

### Scripts SQL

- `sql/2026-10-04-notas-debito.sql`: migración idempotente (tablas con `IF OBJECT_ID(...) IS NULL`,
  seeds sin duplicar, `MERGE` de módulos). Misma convención que `2026-09-01-inventario-mercancias.sql`.
- `sql/crear-api-db-test-completo.sql`: se agregan las 4 tablas, el seed de numeración y los
  módulos, para bases nuevas.

## §2 — API

Por cada documento se clona la pila completa de Factura/FacturaCompra (sin inventario):

- **Entidades EF** `NotaDebito`, `NotaDebitoDetalle`, `NotaDebitoCompra`, `NotaDebitoCompraDetalle`
  + mapeo en `ApiDbTestContext.OnModelCreating` (claves, defaults, longitudes, FKs) + navegaciones
  en `SocioNegocio`, `Monedum`, `NumeracionDocumentoDet` y `Impuesto` como las demás.
- **Repos** `NotaDebitoRepositorio`, `NotaDebitoDetalleRepositorio` y los de compra (genéricos EF).
- **DTOs** (carpetas `notaDebito/`, `notaDebitoCompra/`): `...DTO`, `...CrearDTO` (con `Lineas`),
  `...ActualizarDTO`, y los tres del detalle. `Descripcion` obligatoria en el DTO de línea.
- **Dominio** `NotaDebitoDomain` / `NotaDebitoCompraDomain` + `...DetalleDomain`:
  - `InsertarAsync(nota, lineas)`: fuerza `TipoObjeto` (`7`/`14`), `Cancelado='N'`, `EstadoDoc='A'`,
    `FechaCancelado=null`; valida la serie (existe, **pertenece a su `CodigoObj`**, no bloqueada;
    numeración manual/automática igual que Factura); valida el socio (existe, `TipoSN` `C` para la
    nota de cliente y `P` para la de proveedor, `Activo` distinto de `N`); exige moneda, al menos
    una línea y, por línea, `Descripcion` no vacía (máx. 200), `Cantidad` > 0, `Precio` ≥ 0,
    `PrctjeDesc` entre 0 y 100, `CodigoImpuesto` existente. Si `BaseTipo` es `6`/`13`: `BaseEntry`
    obligatorio, la factura existe, **no está cancelada** y es del **mismo socio**; si no hay base
    se fuerza `BaseTipo=-1`/`BaseEntry=null`. Todo en una transacción (encabezado → líneas). No
    llama a `IInventarioAsientoService`.
  - `ActualizarAsync`: igual que Factura — solo cancelar (`Cancelado='S'`: marca `Cancelado` y
    `FechaCancelado`; `EstadoDoc` no cambia, igual que en Factura) o editar el comentario; un documento cancelado no se
    modifica. Sin reversa de inventario (no existe).
  - `EliminarAsync`: exige haber cancelado antes; borra líneas y encabezado en una transacción.
  - `...DetalleDomain`: mismas reglas de líneas que `FacturaDetalleDomain` (NoLinea =
    max+1, no se tocan líneas de un documento cancelado), validando los campos de línea.
  - Consultas de base: `ObtenerFacturasBaseAsync(codigoSn)` (facturas no canceladas del socio,
    resumen: entry, numDoc, fecha, total, moneda) y `ObtenerFacturaBaseAsync(entry)` (encabezado +
    líneas convertidas a conceptos). Para la nota de proveedor, lo mismo sobre `FacturaCompra`.
- **Aplicación** `NotaDebitoApplication` / `...DetalleApplication` y de compra (patrón
  `Respuesta<T>`, errores de dominio → `Resultado=false`).
- **Controllers** `NotaDebitoController` (`api/NotaDebito`), `NotaDebitoDetalleController`
  (`api/NotaDebitoDetalle`) y los de compra (`api/NotaDebitoCompra`, `api/NotaDebitoCompraDetalle`)
  con las mismas rutas que Factura: `GET {id}`, `GET`, `POST`, `PUT {id}`, `DELETE {id}`, más
  `GET FacturasBase/{codigoSn}` y `GET FacturaBase/{entry}`. Permisos:
  `RequierePermiso("NotasDebito"|"NotasDebitoCompra", Ver/Agregar/Actualizar/Eliminar)`; los
  endpoints de base usan `Ver` del propio módulo (así no hace falta `Ver` sobre Facturas). El
  `PUT` con `Cancelado=="S"` exige además `Eliminar` (mismo chequeo que los demás documentos).
- **DI y mapeos:** registros `AddTransient` en `Startup.cs` y `CreateMap` en `PerfilMapeo.cs`.

## §3 — Web

Clon de `FacturasController`/`FacturasCompraController` y sus vistas, sin las partes de artículo y
almacén:

- **Clientes de API** `INotaDebitoApiClient`/`NotaDebitoApiClient`, `...DetalleApiClient`,
  y de compra; DTOs en `Web.ApiClient/Dtos/NotaDebito*`; registro en `Program.cs`.
- **Controllers** `NotasDebitoController` y `NotasDebitoCompraController` con las acciones de
  Factura (Index, ObtenerTodos, FormularioCrear/Editar, Crear, Editar, Eliminar, ObtenerDetalle,
  CrearLinea/EditarLinea/EliminarLinea, BuscarSocios (`C` o `P`), BuscarImpuestos,
  ObtenerImpuestoPorCodigo) más `BuscarFacturasBase(codigoSn)` y `ObtenerFacturaBase(entry)`
  (proxies de los endpoints de base). Atributos `[RequierePermiso("NotasDebito"...)]`,
  `[ValidateAntiForgeryToken]` en los POST, y el chequeo de `Eliminar` al cancelar.
- **Vistas** `Views/NotasDebito/{Index,_Form}.cshtml` y `Views/NotasDebitoCompra/...`; JS
  `notasdebito.js` y `notasdebitocompra.js`. Líneas con descripción, cantidad, precio, descuento e
  impuesto (sin artículo/almacén); totales calculados igual que en Factura. En el formulario de
  creación, selector opcional **"Basado en factura"**: al elegir el socio lista sus facturas no
  canceladas; al seleccionar una, precarga socio, moneda y líneas (conceptos editables) y fija
  `BaseTipo`/`BaseEntry`. Botones Nuevo/Editar/Eliminar/Cancelar documento ocultos según permiso
  (patrón `datosPermisos<Modulo>` + `puedeGuardar`; "Cancelar documento" por `Eliminar`).
- **Menú** (`_Layout.cshtml`): "Notas de débito" en Ventas y "Notas de débito" en Compras,
  visibles con `Ver` del módulo respectivo; las listas `EsActivo*`/`Muestra*` incluyen los
  controllers nuevos.

## §4 — Pruebas

- API (xUnit + Moq, convenciones existentes, `AsAsyncQueryable()` para `ToListAsync`): dominio de
  los 4 pares (inserción con numeración manual/automática, serie de otro objeto/bloqueada,
  socio de tipo equivocado, líneas inválidas, base: inexistente/cancelada/otro socio/válida,
  cancelar, editar comentario, documento cancelado inmutable, eliminar exige cancelar, **no se
  asienta inventario**), aplicación (mapeo/errores), controllers (rutas, 404/400, permisos
  por módulo, `Cancelado=="S"` exige `Eliminar`), consultas de base, y que `PerfilMapeo`
  configure los mapeos nuevos.
- Web: sin proyecto de pruebas; verificación por build (árbol limpio en worktree), `node --check`
  de los JS y la checklist manual del final.
- Checklist manual: crear nota de cliente libre y en base a una factura; crear nota de proveedor;
  numeración automática y manual; cancelar y eliminar; un usuario con `Ver` solamente no ve botones;
  el menú respeta los permisos; una nota de cliente rechaza un proveedor y viceversa.

## Riesgos y notas

- El esquema se despliega con `sql/2026-10-04-notas-debito.sql` en cada base antes de arrancar la
  API; los módulos nuevos no aparecen en ningún usuario hasta que un admin les asigne permisos
  (deny by default; `SuperUsuario` ve todo).
- Un rol que solo tenga `NotasDebito` necesita además `Ver` sobre `SociosNegocio` y
  `NumeracionDocumento` (y el impuesto se resuelve igual que en Factura). Es la misma dependencia
  entre módulos que ya tienen los demás documentos.
- Sin inventario ni contabilidad: la nota solo registra el documento y sus totales. Cuando
  exista saldo de socios habrá que decidir cómo la nota lo afecta (fuera de alcance aquí).
- Serie 32-35 y `CodigoObj` 7/14 son nuevos; no hay conflicto con los datos actuales
  (series 1-11, 20-31; objetos 1-6, 10-13, 59, 60).
