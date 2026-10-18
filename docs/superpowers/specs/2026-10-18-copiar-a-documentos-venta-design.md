# Copiar a… en la cadena de ventas (Cotización → Pedido → Entrega → Factura) — Diseño

**Fecha:** 2026-10-18
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)

## Objetivo

Permitir crear un **Pedido**, una **Entrega** o una **Factura de venta** a partir de un documento origen de la
cadena de ventas, copiando sus líneas abiertas, igual que el "Copy To" de SAP Business One:

| Origen | Destinos permitidos |
|---|---|
| Cotización | Pedido, Entrega, Factura |
| Pedido | Entrega, Factura |
| Entrega | Factura |

El punto de entrada es un botón **"Copiar a"** en el listado del documento origen. Abre el formulario del
destino **ya cargado** con las líneas abiertas del origen, sin guardar; el usuario revisa y edita (baja
cantidades, quita líneas, agrega líneas libres) y recién al guardar se crea el documento.

Referencia de comportamiento: SAP B1, `QUT1` → `RDR1` / `DLN1` / `INV1` con `BaseType`/`BaseEntry`/`BaseLine`,
cantidad abierta por línea, copia parcial y origen que no se puede cancelar mientras tenga destinos activos.

### Decisiones tomadas (brainstorming, 2026-10-18)

1. **Punto de entrada:** "Copiar a…" desde el documento origen. "Copiar desde…" con varios orígenes en el destino
   queda fuera de este alcance (es el diseño del módulo de compras, `2026-09-13-documentos-base-compras`).
2. **Alcance:** toda la cadena de ventas de la tabla anterior, no solo desde la cotización.
3. **Cantidades:** copia **parcial con cantidad abierta**. Se puede generar más de un destino contra el mismo
   origen hasta agotar la cantidad.
4. **Integridad:** un origen con destinos activos **no se puede cancelar, eliminar ni cambiar de socio**, y sus
   líneas ya copiadas **no se pueden modificar ni quitar**. Primero se cancela el destino.

## Contexto verificado

### Esquema (sin cambios; no se ejecuta DDL)

Tipos de objeto: Cotización `3`, Pedido `4`, Entrega `5`, Factura `6` (solo filas con `DocSubType = '--'`; las
notas de débito son otra cosa y no participan).

| Tabla detalle | `BaseTipo` permitido por `CHECK` |
|---|---|
| `CotizacionDetalle` | `-1` |
| `PedidoDetalle` | `-1`, `3` |
| `EntregaDetalle` | `-1`, `3`, `4` |
| `FacturaDetalle` | `-1`, `3`, `4`, `5` |

Las **cabeceras** (`Pedido`, `Entrega`, `Factura`) solo admiten `BaseTipo = -1`: la referencia al documento base se
guarda **solo en las líneas**. `TipoDocDestino`/`DocDestinoEntry` (puntero hacia adelante de un único valor) no se
usan: con copia parcial una línea origen puede tener varios consumidores.

### Dominios existentes

- `EntregaDomain.InsertarAsync(Entrega, IEnumerable<EntregaDetalle>)` y `FacturaDomain.InsertarAsync(Factura,
  IEnumerable<FacturaDetalle>)` ya guardan encabezado, líneas y asiento de inventario en **una transacción**
  (`IEjecutorTransaccion`). `FacturaDomain` solo asienta inventario de las líneas con `BaseEntry == null`
  (supone que toda línea con base viene de una Entrega; hoy nada llena `BaseEntry`).
- `CotizacionDomain.InsertarAsync(Cotizacion)` y `PedidoDomain.InsertarAsync(Pedido)` guardan **solo la cabecera**; las
  líneas se agregan una por una después (`…DetalleController` / `…DetalleDomain.InsertarAsync`), **sin transacción**.
- Cotización y Pedido **no se cancelan**: la Web los edita (incluido `EstadoDoc` `'A'`/`'C'`) y los **elimina
  físicamente** junto con sus líneas. Entrega y Factura se cancelan con `Cancelado = 'S'` (revierte el inventario).
- Los módulos de permisos son `Cotizaciones`, `Pedidos`, `Entregas` y `Facturas`.

## 1. Reglas de negocio

### Documento activo y cantidad abierta

Un documento está **activo** si `Cancelado != 'S'` **y** `EstadoDoc != 'C'`.

```
abierta(líneaOrigen) = líneaOrigen.Cantidad
                     − Σ línea.Cantidad
                         de las líneas de cualquier documento destino ACTIVO
                         con BaseTipo = tipoOrigen, BaseEntry = entryOrigen, BaseLinea = noLinea
```

Se calcula **siempre en vivo**, nunca se guarda un contador. Cancelar o eliminar un destino libera su cantidad
sin tocar el origen. Un origen "cerrado" es simplemente uno sin cantidad abierta > 0.

### Validación de una línea con base (al guardar el destino)

Para cada línea con `BaseTipo` distinto de `-1` (la validación corre **antes de numerar** el documento y antes de abrir la transacción de guardado):

1. La combinación (`BaseTipo` → tipo del destino) está en la tabla de destinos permitidos.
2. El documento origen existe y está activo.
3. La línea origen existe y `CodArticulo` es el mismo (el artículo de una línea copiada no se puede cambiar).
4. `Cantidad > 0` y `Cantidad ≤ abierta`; varias líneas del payload contra la misma línea origen se suman.
5. El socio del destino es el socio del origen.

`Precio`, `PrctjeDesc`, `CodigoImpuesto` y `CodAlmacen` quedan editables (como en SAP); se copian del origen sin
volver a resolver la lista de precios. Una línea sin base (`BaseTipo = -1`) sigue siendo válida en el mismo
documento. Un error en cualquier línea rechaza el documento completo y **no consume el consecutivo** (la validación corre antes
de numerar, igual que `FacturaDomain.ValidarLineas`).

El `BaseTipo` de la **cabecera** se fija siempre en `-1`.

### Inventario

| Destino | Qué asienta |
|---|---|
| Cotización, Pedido | nunca mueven inventario |
| Entrega | todas las líneas con cantidad > 0 (sin cambio) |
| Factura | las líneas con cantidad > 0 y `BaseTipo != 5` (es decir, **no** las que vienen de una Entrega, que ya movió esa mercancía). Las líneas con base Cotización o Pedido sí descuentan, porque nadie entregó todavía. |

Reemplaza la condición actual `BaseEntry == null`. Si no hay existencia suficiente, el destino completo se rechaza
con el mensaje actual de stock insuficiente.

### Integridad del origen

Un origen (Cotización, Pedido o Entrega) con **al menos una línea con destinos activos**:

- **no se puede eliminar** (Cotización, Pedido) ni **cancelar** (Entrega `Cancelado='S'`; Cotización y Pedido
  `EstadoDoc='C'`);
- **no puede cambiar de socio** (`CodigoSn`);
- sus **líneas ya consumidas no se pueden modificar ni eliminar** (`CotizacionDetalleDomain`,
  `PedidoDetalleDomain`, `EntregaDetalleDomain`, métodos `ActualizarAsync`/`EliminarAsync`);
- agregar líneas nuevas al origen sigue permitido.

El mensaje nombra los documentos que lo usan, por ejemplo: `No se puede cancelar: lo usan Pedido 3 y Factura 2. Cancele
primero esos documentos.` Esto corrige además un hallazgo de la demostración: hoy se puede cancelar una Entrega ya
facturada y el inventario queda descuadrado.

## 2. API

### Servicio de documento base (`API.Domain.Core`)

`IDocumentoBaseService` (interfaz en `API.Domain.Interface`), con su implementación en `API.Domain.Core`:

- `ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)`: valida la combinación y devuelve cabecera y
  líneas con `CantidadAbierta > 0`. Rechaza si el origen está inactivo o no tiene líneas abiertas
  (`"El documento no tiene líneas abiertas."`).
- `ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas)`: reglas de la
  sección 1; lanza `Exception` con mensaje de usuario, como el resto de los dominios.
- `ObtenerConsumidoresAsync(int tipoOrigen, int entry, int? noLinea)`: documentos destino **activos** que usan el
  origen (o una línea), para las reglas de integridad y para los mensajes.
- `ValidarOrigenModificableAsync(...)`: usada por los dominios origen (eliminar, cancelar, cambiar socio) y por los
  dominios de detalle (actualizar/eliminar línea).

Un único conjunto de consultas cubre las tres tablas destino (`PedidoDetalle`, `EntregaDetalle`, `FacturaDetalle`)
filtradas por `BaseTipo` y por cabecera activa.

### Endpoint

`GET api/DocumentoBase/{tipoOrigen:int}/{entry:int}/Abierto?destino={tipoDestino:int}` →
`Respuesta<DocumentoBaseBorradorDTO>`:

```
DocumentoBaseBorradorDTO { OrigenTipo, OrigenEntry, OrigenNumDoc,
  Cabecera { CodigoSn, NombreSn, Direccion, MonedaDoc, PrctjeDesc, PrctjeImpuesto, Comentario },
  Lineas [ { BaseTipo, BaseEntry, BaseLinea, CodArticulo, Descripcion, CantidadOrigen, CantidadAbierta,
             Precio, PrecioBruto, PrctjeDesc, CodigoImpuesto, CodAlmacen } ] }
```

Permisos: **Ver** en el módulo del origen y **Agregar** en el del destino, evaluados con `PermisoUsuario.Tiene` porque
el módulo depende de los parámetros. Errores: `400` combinación inválida / sin líneas abiertas, `404` origen
inexistente, `403` sin permiso.

### Dominios destino

- `EntregaDomain.InsertarAsync` y `FacturaDomain.InsertarAsync`: llaman a `ValidarLineasAsync` antes de numerar (junto a
  `ValidarLineas`, así un error no avanza el consecutivo); `FacturaDomain` cambia la condición de inventario a `BaseTipo != 5` y fuerza la
  cabecera `BaseTipo = -1`.
- `PedidoDomain` incorpora `IEjecutorTransaccion` y `InsertarAsync(Pedido, IEnumerable<PedidoDetalle>)`; el
  `PedidoCrearDTO` recibe un `List<PedidoDetalleCrearDTO> Lineas` **opcional** (como Entrega y Factura). Sin líneas,
  el camino antiguo (solo cabecera + líneas por separado) sigue funcionando.
- Los DTOs de línea de Pedido, Entrega y Factura ya mapean `BaseTipo`/`BaseEntry`/`BaseLinea` (a confirmar en el plan,
  tarea 1); donde falten se agregan.

### Dominios origen

`CotizacionDomain`, `PedidoDomain`, `EntregaDomain` (eliminar, cancelar, cambiar socio) y `CotizacionDetalleDomain`,
`PedidoDetalleDomain`, `EntregaDetalleDomain` (actualizar/eliminar línea) invocan `ValidarOrigenModificableAsync`.

## 3. Web

- **Listados** de Cotizaciones, Pedidos y Entregas: botón desplegable **"Copiar a"** en cada fila no cancelada, con
  solo los destinos válidos del origen y para los que el usuario tiene permiso de agregar (los permisos de los
  módulos destino se agregan al JSON `permisos` que ya emite cada `Index`).
- **Apertura del destino:** `…/FormularioCrear?baseTipo=&baseEntry=` en los controladores de Pedidos, Entregas y
  Facturas. El controlador pide el borrador a la API (`IDocumentoBaseApiClient` nuevo en `Web.ApiClient`) y lo entrega
  al formulario como JSON.
- **Formulario del destino con base** (`pedidos.js`, `entregas.js`, `facturas.js`):
  - carga las líneas con sus referencias base; el socio, la dirección y la moneda quedan de solo lectura;
  - la cantidad es editable con tope en `CantidadAbierta` (columna informativa "Abierto: N" y "Base: Cotización 12");
  - se puede quitar una línea (solo de la lista local) y agregar líneas libres;
  - si la línea origen no trae almacén en Entrega o Factura, se elige en el formulario (la validación de almacén
    ya existe en esos formularios);
  - el recálculo por lista de precios al cambiar de socio **no** corre (el socio no cambia);
  - al guardar, envía todo en **una** petición con las referencias base en cada línea (Pedido pasa a usar el camino con
    `Lineas`).
- Tras guardar, mensaje `"Pedido N creado desde Cotización M"` y recarga del listado.
- Los errores del servidor (cantidad excedida, stock insuficiente, origen inactivo) se muestran con los mensajes
  del dominio.

## 4. Pruebas

- **Dominio** (xUnit + Moq, convención de `API.Service.WebApi.Tests`): cantidad abierta (parcial, agotada, destino
  cancelado libera), validación de cada combinación válida e inválida, artículo distinto, cantidad excedida, socio
  distinto, líneas del mismo origen que se suman, regla de inventario de Factura (base 3/4 descuenta, base 5 no, mezcla
  de líneas), consecutivo no consumido ante error, y todas las reglas de integridad del origen.
- **Controladores:** permisos del endpoint nuevo (403 sin ver en origen o sin agregar en destino), `400`/`404`, y
  `Pedido` con y sin `Lineas`.
- **Prueba de humo real** en una base temporal (nunca `API_DB_TEST`): Cotización → Pedido parcial → Entrega → Factura;
  Cotización → Factura directa (descuenta inventario); copia que excede lo abierto (rechazada sin consumir
  consecutivo); cancelar un destino y comprobar que la cantidad vuelve; intentar cancelar/eliminar/editar un origen
  con destinos (rechazado con el mensaje que nombra los documentos); acceso `401`/`403`.
- **Compilación** de API y Web con 0 errores y **suite completa en verde** (línea base 1,443 pruebas).

## Fuera de alcance

- "Copiar desde…" con varios orígenes en el destino (y todo el módulo de compras: spec propio, pausado).
- Referencia a documento base en la cabecera (el esquema la prohíbe).
- Los punteros `TipoDocDestino`/`DocDestinoEntry` y los estados persistidos de línea/documento.
- Columna de estado derivado ("Abierto/Parcial/Cerrado") en los listados.
- Notas de débito y de crédito (tienen su propio documento base).
- Atomicidad de la creación **normal** de Cotización y de Pedido sin líneas en la petición (se mantiene el camino
  antiguo).

## Riesgos conocidos

- **Concurrencia:** dos usuarios copiando a la vez la misma línea podrían pasar ambos la validación. Para el volumen
  previsto (uno a tres usuarios) se acepta, igual que el riesgo ya documentado de la numeración consecutiva; la
  validación corre justo antes de numerar y de guardar para reducir la ventana.
- **Datos existentes:** ningún documento actual tiene líneas con base, así que no hay migración de datos. Las líneas
  de Factura con `BaseEntry` nulo siguen descontando igual que antes.
