# Órdenes de servicio del taller (motocicletas) — Diseño

**Fecha:** 2026-10-22
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

El sistema lo usará una distribuidora de repuestos y accesorios para motocicletas que tiene además un taller
mecánico. Se agrega un módulo **Taller** para registrar las **órdenes de servicio**: una moto de un cliente
ingresa al taller, se le aplican servicios (mano de obra) y se consumen **insumos tomados del inventario del
negocio**. El sistema hace los movimientos de inventario de esos insumos y la orden se factura después con
**"Copiar a Factura"**, sin volver a descontar lo que ya salió.

El módulo incluye, además de las órdenes, un catálogo de **motocicletas** por cliente y un catálogo de
**mecánicos**.

### Decisiones tomadas (brainstorming, 2026-10-22)

1. **Momento del inventario:** el insumo sale del inventario **al agregarlo a la orden**. La factura al cliente
   se genera después con "Copiar a Factura" y no vuelve a descontar las líneas que vienen de la orden. Si la
   línea se quita o la orden se cancela, el stock se devuelve.
2. **Alcance de la primera versión:** registro de motocicletas, mano de obra / servicios cobrables, mecánico
   asignado, y estados de la orden con datos de recepción y diagnóstico (texto libre, sin fotos).
3. **Mecánico:** catálogo propio (`Mecanico`), no usuarios del sistema ni texto libre.
4. **Enfoque:** la orden es un **documento propio** (cabecera + líneas) con inventario por línea y facturación
   mediante la cadena "Copiar a" ya existente (spec `2026-10-18-copiar-a-documentos-venta-design`), con la
   orden como un origen más. Se descartaron: generar Salidas de Mercancía por tanda (inmutables, ensucian su
   listado) y usar una Factura abierta como borrador (la Factura es inmutable y descuenta al nacer).

## Contexto verificado

- `IInventarioAsientoService.AsentarAsync(IEnumerable<MovimientoRequest>, bool permitirNegativo)` y
  `RevertirAsync(string tipoDoc, int docEntry)` existen; **ignoran** los artículos con
  `ArticuloInventario != 'S'`, de modo que una línea de mano de obra no mueve stock sin código adicional.
  `RevertirAsync` revierte un documento completo, no una línea. `MovimientoRequest(TipoDoc, DocEntry, DocLinea,
  CodArticulo, CodAlmacen, Cantidad, PrecioUnitario, Fecha)`; `Cantidad < 0` es salida. Una reversa nunca se
  bloquea por stock negativo.
- `StockInsuficienteException`, `ArticuloNoExisteException` y `AlmacenNoExisteException` ya existen en
  `API.Domain.Core.Inventario`.
- `IEjecutorTransaccion.EjecutarAsync` abre la transacción, guarda y confirma; los dominios no llaman
  `SaveChangesAsync` directo.
- Códigos de objeto ocupados: 1, 2, 3, 4, 5, 6, 10, 11, 12, 13, 59, 60 (y los de notas de débito/crédito, que
  usan `SubTipoDoc`). **70 está libre** y se usa para la orden de servicio.
- En la cadena "Copiar a" (spec 2026-10-18), `TipoDocumentoVenta` define Cotización 3, Pedido 4, Entrega 5,
  Factura 6; `FacturaDetalle.BaseTipo` tiene un `CHECK` que hoy admite {-1, 3, 4, 5}; `FacturaDomain` descuenta
  inventario solo de las líneas con `BaseTipo != 5`.
- `Articulo` tiene `ArticuloInventario`, `ArticuloVenta`, `AlmacenDefecto`, `Activo`. `SocioNegocio` tiene
  `TipoSn` y `NumLstPrecio` (lista de precios del cliente).
- Los módulos de permisos se siembran en `dbo.Modulo` con `MERGE` (ver `sql/2026-10-05-notas-credito.sql`).

## 1. Modelo de datos

DDL idempotente en `API/sql/2026-10-22-ordenes-servicio-taller.sql`, **aplicado por el usuario** en
`API_DB_TEST` (igual que fases previas), y reflejado después en `crear-api-db-test-completo.sql`. Más entidades
EF (DB-first), repositorios genéricos en DI y mapeos AutoMapper.

| Tabla | Columnas principales |
|---|---|
| `Mecanico` | `Codigo` (PK, nvarchar), `Nombre`, `Telefono`, `Activo` ('S'/'N') |
| `Motocicleta` | `Entry` (PK identity), `CodigoSn` (FK `SocioNegocio`), `Placa` (única), `Marca`, `Modelo`, `Anio`, `Color`, `Vin`, `Comentario`, `Activo` |
| `OrdenServicio` | `Entry` (PK identity), `NumDoc`, `Serie` (FK numeración, `CodigoObj='70'`), `TipoObjeto='70'`, `FechaDoc`, `FechaIngreso`, `FechaPromesa`, `FechaEntrega`, `CodigoSn`, `NombreSn`, `MotoEntry` (FK `Motocicleta`), `CodMecanico` (FK `Mecanico`, opcional), `KmIngreso`, `FallaReportada`, `Diagnostico`, `Observaciones`, `EstadoServicio` ('R','P','T','E'), `Cancelado` ('S'/'N'), `FechaCancelado`, `MonedaDoc`, `Subtotal`, `TotalImpuesto`, `Total`, `Comentario`, `BaseTipo`/`BaseEntry` (siempre -1/null, como las demás cabeceras) |
| `OrdenServicioDetalle` | `Entry`, `NoLinea` (PK compuesta), `CodArticulo`, `Descripcion`, `CodAlmacen` (obligatorio solo si el artículo es de inventario), `Cantidad`, `Precio`, `PrctjeDesc`, `CodigoImpuesto`, `Impuesto`, `TotalLinea`, `CodMecanico` (opcional por línea), `TipoObjeto='70'`; la línea **no** guarda un campo "tipo": es insumo si `Articulo.ArticuloInventario = 'S'`, servicio si no |

La orden **no tiene descuento global**: el descuento es por línea. La cabecera guarda los totales calculados
por el servidor.

Se agrega además: la fila de numeración `NumeracionDocumento` (`CodigoObj='70'`, `SubTipoDoc='--'`,
`DocAlias='Orden de servicio'`) y una serie "Primario" sembrada (`SigNumero=1`, `Manual='N'`,
`Bloqueado='N'`) como se hizo con 59 y 60; los módulos `Mecanicos`, `Motocicletas`, `OrdenesServicio` en el
grupo **Taller** (`MERGE` en `dbo.Modulo`); y el `CHECK` de `FacturaDetalle.BaseTipo` ampliado a
{-1, 3, 4, 5, 70}.

## 2. Reglas de negocio

### Estados

`R` Recibida → `P` En proceso → `T` Terminada → `E` Entregada. Se puede pasar a cualquier estado posterior
pero **nunca a uno anterior**. `E` es final. La cancelación es independiente (`Cancelado = 'S'`) y también
final.

| Estado | Líneas (agregar/editar/quitar) | Cabecera | Facturable |
|---|---|---|---|
| R, P | sí | sí | no |
| T | sí | sí | sí |
| E | no | solo `Comentario` | sí |
| Cancelada | no | no | no |

Una orden puede pasar a **Entregada sin facturarse** (garantía, consumo del taller): el stock ya salió al
agregar los insumos y no se genera ninguna venta. Una orden **activa** es la que tiene `Cancelado != 'S'`.

### Validaciones de la orden

- El cliente (`CodigoSn`) debe existir, estar activo y ser de tipo cliente (`TipoSn` de cliente).
- La moto debe existir, estar activa y **pertenecer a ese cliente**.
- El mecánico, si se indica, debe existir y estar activo.
- Los artículos de las líneas deben existir, estar activos y marcados para venta (`ArticuloVenta = 'S'`).
- Un insumo exige `CodAlmacen` (por defecto el del artículo) y existencia suficiente.
- `Cantidad > 0`. Una orden puede crearse sin líneas.
- Todos los totales (línea y cabecera) los calcula el servidor con la calculadora de redondeo del proyecto; lo
  que mande el cliente se ignora.
- Un cliente, una moto o un mecánico con órdenes **no se eliminan**: solo se desactivan. Una moto con
  órdenes no cambia de cliente.

### Inventario por línea

Todo dentro de `IEjecutorTransaccion` junto con el guardado de la línea y el recálculo de los totales de la
cabecera. `TipoDoc = "70"`, `DocEntry = Entry` de la orden, `DocLinea = NoLinea`.

| Operación sobre un insumo | Movimiento |
|---|---|
| Agregar línea | salida por `Cantidad` al costo vigente (`permitirNegativo: false`) |
| Cambiar cantidad | movimiento por la **diferencia** (más cantidad = salida adicional, menos = entrada; la salida bloquea por stock) |
| Cambiar almacén | devolución al almacén anterior + salida del nuevo |
| Cambiar de artículo | devolución del artículo anterior + salida del nuevo |
| Quitar línea | devolución de esa línea (`RevertirLineaAsync`) |
| Cancelar la orden | devolución de todas las líneas (`RevertirAsync("70", entry)`) y `Cancelado='S'` |

Las líneas de servicio no generan movimiento. Si el servicio cambia a insumo o al revés al cambiar de
artículo, rige la tabla anterior sobre el artículo viejo y el nuevo.

`IInventarioAsientoService` gana `RevertirLineaAsync(string tipoDoc, int docEntry, int docLinea)`: revierte
los movimientos de una línea que aún no tengan reversa, con la misma lógica y orden determinista que
`RevertirAsync` (que puede implementarse sobre la misma rutina). Idempotente: repetirla no hace nada.

### Facturación (orden como origen de la cadena "Copiar a")

La orden de servicio es un origen más de `DocumentoBaseService`, con **una sola combinación: 70 → 6**:

- `TipoDocumentoVenta` gana `OrdenServicio = 70`; `DestinosDe(70) = [Factura]`; `EsCombinacionValida(70, 6)`.
- El borrador y la validación usan las reglas del spec 2026-10-18: cantidad abierta en vivo
  (`Cantidad − Σ` de líneas de **Facturas activas** con `BaseTipo=70, BaseEntry, BaseLinea`), mismo cliente,
  mismo artículo, `Cantidad ≤ abierta`, validación antes de numerar.
- La orden se considera **origen utilizable** si está activa y en estado `T` o `E`. En otro estado el borrador
  responde con el mensaje `"La orden de servicio debe estar Terminada o Entregada para facturarse."`.
- El borrador copia todas las líneas abiertas (insumos y servicios). Cabecera: cliente, nombre, dirección
  (del socio), moneda; sin descuento global; el impuesto viaja **por línea** (`CodigoImpuesto`, `Impuesto`).
- **Regla de inventario de Factura:** solo asienta las líneas con `BaseTipo` distinto de 5 **y** distinto de 70.
  Las líneas sueltas agregadas en la factura descuentan como hoy.
- **Integridad del origen** (misma matriz que Cotización/Pedido/Entrega): una orden con Facturas activas no se
  puede cancelar ni cambiar de cliente, y sus líneas ya facturadas no se modifican ni se quitan. Se pueden
  agregar líneas nuevas (si la orden aún no está Entregada): quedan abiertas para una segunda factura.
- Cancelar la factura libera la cantidad; los insumos siguen descontados (esa factura nunca los movió) y se
  puede volver a facturar.
- Los mensajes nombran el origen como `Orden de servicio N` (p. ej. `No se puede cancelar: lo usan Factura 3.
  Cancele primero esos documentos.`).

## 3. API

Mismo esquema de capas del proyecto: `Controller → Application → Domain → IRepositorioGenerico`, DTOs en
`API.Application.DTO`, mapeos en `API.Transversal.Mapper`, DI en `Startup.cs`.

| Recurso | Endpoints |
|---|---|
| `api/Mecanico` | `GET`, `GET {codigo}`, `POST`, `PUT {codigo}`, `DELETE {codigo}` (baja lógica `Activo='N'`) |
| `api/Motocicleta` | `GET` (filtro opcional `?codigoSn=`/`?placa=`), `GET {entry}`, `POST`, `PUT {entry}`, `DELETE {entry}` (baja lógica), `GET {entry}/Historial` (órdenes de la moto) |
| `api/OrdenServicio` | `GET` (filtro `?estado=`), `GET {entry}` (cabecera + líneas), `POST` (cabecera + líneas opcionales, una transacción, numera y asienta), `PUT {entry}` (cabecera), `POST {entry}/Estado` (body: nuevo estado), `POST {entry}/Cancelar` |
| `api/OrdenServicioDetalle` | `POST` (agregar línea), `PUT {entry}/{linea}`, `DELETE {entry}/{linea}` — cada uno con su movimiento de inventario y recálculo de totales |
| `api/DocumentoBase` | sin endpoint nuevo: `GET 70/{entry}/Abierto?destino=6` usa el existente |

Dominios nuevos: `MecanicoDomain`, `MotocicletaDomain`, `OrdenServicioDomain`, `OrdenServicioDetalleDomain`.
`OrdenServicioDomain.InsertarAsync` numera (`CodigoObj='70'`, `SubTipoDoc='--'`) **después** de validar todo,
igual que Entrega, y asienta los insumos en la misma transacción.

**Permisos** (`RequierePermiso`): módulos `Mecanicos`, `Motocicletas`, `OrdenesServicio` (grupo Taller) con
Ver/Agregar/Actualizar/Eliminar. Cancelar una orden exige **Eliminar** (convención vigente: cancelar un
documento exige Eliminar); cambiar el estado y editar exigen **Actualizar**. Para facturar desde la orden el
endpoint de documento base exige **Ver** en `OrdenesServicio` y **Agregar** en `Facturas`
(`PermisoUsuario.Tiene`, ampliando el mapa tipo→módulo con 70→`OrdenesServicio`). La asignación a plantillas la
hace el usuario en la pantalla de permisos.

## 4. Web

Submenú **Taller** con tres pantallas, mismo patrón de las existentes (controlador MVC + `Web.ApiClient` tipado +
vistas parciales + DataTables + botones ocultos según permiso):

- **Mecánicos:** listado y modal (código, nombre, teléfono, activo).
- **Motocicletas:** listado con búsqueda por placa/cliente; modal con cliente (autocompletado de socios
  existente), placa, marca, modelo, año, color, VIN; botón **Historial** (órdenes de la moto).
- **Órdenes de servicio:**
  - Listado: número, fecha, cliente, moto (placa y modelo), mecánico, estado con color, total; filtro por
    estado; acciones por fila: editar/ver, avanzar el estado, **Facturar** (`App.botonCopiarA(70, entry,
    [{nombre:'Factura de venta', url:'/Facturas'}])`, solo en `T`/`E`) y cancelar.
  - Formulario (modal ancho): cliente → motos de ese cliente (con enlace rápido para registrar una moto
    nueva), mecánico, fechas de ingreso/promesa, kilometraje de ingreso, falla reportada, diagnóstico,
    observaciones. Tabla única de líneas con insignia "Insumo"/"Servicio"; botones "Agregar insumo" y "Agregar
    servicio" filtran el buscador de artículos por inventariable o no; precio de la lista de precios del
    cliente (recálculo existente) editable; insumos exigen almacén (el del artículo por defecto).
  - Alta: cabecera y líneas en una sola petición. Edición de una orden guardada: cada línea agregada, cambiada o
    quitada se guarda en el acto con su movimiento; las líneas ya facturadas salen bloqueadas.
  - Errores del servidor (stock insuficiente, orden no editable, origen no facturable) se muestran con el
    mensaje del dominio.
- **Facturas:** el formulario precargado ya existente acepta `baseTipo=70` sin cambios de lógica; solo se amplía
  el texto de origen (`App.etiquetaOrigen`/`_baseTexto`) con "Orden de servicio".

## 5. Pruebas

- **Dominio** (xUnit + Moq, convención de `API.Service.WebApi.Tests`):
  - Orden: insumo mueve inventario con `TipoDoc "70"`; servicio no; falta de stock no guarda nada ni deja
    movimiento; cambio de cantidad (sube, baja, igual); cambio de almacén y de artículo; quitar línea devuelve
    solo esa línea; cancelar devuelve todo; transiciones de estado válidas e inválidas; bloqueo en `E` y
    cancelada; validaciones de cliente, moto, mecánico y artículo; totales del servidor; consecutivo no
    consumido ante error.
  - `InventarioAsientoService.RevertirLineaAsync`: revierte solo la línea indicada, es idempotente y no toca
    otras líneas del mismo documento.
  - `DocumentoBaseService`: origen 70 (borrador, cantidad abierta contra facturas activas, estado no facturable,
    cliente/artículo/cantidad, integridad de cancelar/cambiar cliente/líneas consumidas).
  - `FacturaDomain`: inventario con líneas de base 70, base 5 y sueltas mezcladas.
- **Controladores:** permisos (403) en los tres controladores nuevos y en el endpoint de facturar desde la
  orden; `400`/`404`.
- **Prueba de humo real** en una base temporal (nunca `API_DB_TEST`): orden con un insumo y un servicio (el
  stock baja) → pasar a Terminada → facturar (el stock **no** vuelve a bajar) → cancelar la factura (la
  cantidad abierta vuelve) → cancelar la orden con factura activa (rechazado) → cancelar la factura y luego la
  orden (el stock vuelve) → insumo sin stock (rechazado, sin movimiento) → accesos `401`/`403`.
- **Recorrido en el navegador** de las tres pantallas y de "Facturar" (Browser pane), con la consola sin
  errores de JavaScript.
- **Compilación** de API y Web con 0 errores y **suite completa en verde** (línea base 1573 pruebas).

## Fuera de alcance

- Impresión de la orden, fotos, citas y recordatorios de mantenimiento.
- Comisiones y reportes de productividad de mecánicos.
- Presupuesto previo (cotización → orden de servicio) y facturar insumos y servicios por separado.
- Mostrar la existencia del almacén en pantalla mientras se elige el insumo (solo si ya hubiera un endpoint
  reutilizable; no se crea uno).
- Volver a un estado anterior de la orden, o reabrir una orden Entregada o cancelada.
- Kilometraje actual de la moto (solo se guarda el kilometraje de ingreso por orden).

## Riesgos conocidos

- **Concurrencia:** dos usuarios tomando la última unidad a la vez. Se acepta para 1–3 usuarios, igual que el
  riesgo ya documentado de la numeración; la validación de stock corre dentro de la transacción de guardado.
- **Valuación al devolver:** la devolución de un insumo usa el costo con que salió (misma deuda de valuación
  conocida de las reversas de `InventarioAsientoService`); no se corrige aquí.
- **Datos existentes:** el módulo es nuevo; no hay migración de datos. El único cambio sobre tablas existentes
  es el `CHECK` de `FacturaDetalle.BaseTipo`, que solo amplía los valores permitidos.
- **Entregas viejas sin asiento (hallazgo abierto de la revisión de "Copiar a"):** no afecta a este módulo, que
  asienta sus propios movimientos, pero conviene correr antes la consulta indicada en esa revisión.
