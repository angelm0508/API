# Copiar a… en la cadena de compras (Pedido de compra → Entrega de compra → Factura de compra) — Diseño

**Fecha:** 2026-11-21
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`), `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Estado:** diseño aprobado por el usuario en chat (enfoque B); pendiente de revisión del documento.

## Objetivo

Hacer del lado de compras lo mismo que ya existe en ventas (`2026-10-18-copiar-a-documentos-venta-design.md`): crear una
**Entrega de compra** o una **Factura de compra** a partir de un documento origen de la cadena de compras, copiando sus
líneas abiertas, como el "Copy To" de SAP Business One.

| Origen | Destinos permitidos |
|---|---|
| Pedido de compra (11) | Entrega de compra (12), Factura de compra (13) |
| Entrega de compra (12) | Factura de compra (13) |

El punto de entrada es un botón **"Copiar a"** en el listado del documento origen. Abre el formulario del destino
**ya cargado** con las líneas abiertas del origen, sin guardar; el usuario revisa y edita (baja cantidades, quita
líneas, agrega líneas libres) y recién al guardar se crea el documento.

Fuera de alcance de la cadena: la Oferta de compra (`10`, no existe el módulo) y el Pedido de compra como destino (el
`CHECK` de `PedidoCompraDetalle` solo admite `BaseTipo` `10` y `-1`).

### Decisiones tomadas (brainstorming, 2026-11-21)

1. **Punto de entrada:** "Copiar a" desde el listado del origen, igual que ventas. El **"Copiar desde"** con varios
   orígenes en el destino (diseño pausado `2026-09-13-documentos-base-compras-design.md`) queda fuera de este alcance.
2. **Enfoque B:** un servicio **paralelo** de compras (`DocumentoBaseCompraService`) con la misma forma que el de ventas,
   su propio endpoint y los registros compartidos. **No se toca** el `DocumentoBaseService` de ventas (más de 3,900
   pruebas lo respaldan): no puede romperse por esto.
3. **Integridad del origen:** igual que ventas. Un origen con destinos activos no se puede cancelar, eliminar ni cambiar
   de proveedor, y sus líneas ya copiadas no se pueden modificar ni quitar.

### Relación con el diseño de septiembre

`2026-09-13-documentos-base-compras-*` quedó con solo su Tarea 1 hecha: `PedidoCompraDomain.ObtenerAbiertosAsync` y los
tipos `DocumentoAbierto`/`LineaAbierta`. Ese método sirve al buscador "Copiar desde" y **este diseño no lo usa**: se deja
tal cual (no se retira en este trabajo; si "Copiar desde" no se retoma, queda como deuda a evaluar). Las Tareas 2 a 10 de ese
plan quedan **sustituidas** por este diseño para la parte "Copiar a".

## Contexto verificado

### Esquema (sin cambios; no se ejecuta DDL)

| Tabla detalle | `BaseTipo` permitido por `CHECK` (según el spec de septiembre, confirmado entonces contra la base) |
|---|---|
| `PedidoCompraDetalle` | `10`, `-1` |
| `EntregaCompraDetalle` | `11`, `10`, `-1` |
| `FacturaCompraDetalle` | `12`, `11`, `10`, `-1` |

Las tres tablas de detalle y las tres cabeceras tienen las mismas columnas relevantes que las de ventas (`BaseTipo`,
`BaseEntry`, `BaseLinea`, `CodArticulo`, `Descripcion`, `Cantidad`, `Precio`, `PrecioBruto`, `PrctjeDesc`,
`CodigoImpuesto`, `Impuesto`, `CodAlmacen`, `UomEntry`, `FactorUom`, `CantidadBase`; cabeceras con `CodigoSn`,
`NombreSn`, `Direccion`, `Nit`, `MonedaDoc`, `PrctjeDesc`, `PrctjeImpuesto`, `Comentario`, `Cancelado`, `EstadoDoc`).
`FacturaCompra` comparte tabla con las notas de débito de proveedor (`DocSubType = '--'` son las facturas; las notas de
débito **no** participan). Las cabeceras tienen columnas `BaseTipo`/`BaseEntry`: el plan verifica su `CHECK` y, si exige
`-1`, la cabecera del destino se fija siempre en `-1` (la referencia vive solo en las líneas, como en ventas).
`TipoDocDestino`/`DocDestinoEntry` no se usan (con copia parcial una línea origen puede tener varios consumidores).

Módulos de permisos: `PedidosCompra`, `EntregasCompra`, `FacturasCompra`.

### Dominios existentes (leídos)

- `EntregaCompraDomain.InsertarAsync(EntregaCompra, IEnumerable<EntregaCompraDetalle>)` y
  `FacturaCompraDomain.InsertarAsync(FacturaCompra, IEnumerable<FacturaCompraDetalle>)` ya guardan cabecera, líneas e
  inventario en **una transacción** (`IEjecutorTransaccion`) y numeran **antes** con el candado de serie. Hoy
  `FacturaCompraDomain` asienta inventario de **todas** las líneas con cantidad > 0.
- `PedidoCompraDomain.InsertarAsync(PedidoCompra)` guarda solo la cabecera (las líneas llegan por
  `PedidoCompraDetalleDomain`); el pedido de compra no se edita por el camino "con líneas".
- Las líneas de `EntregaCompra` y `FacturaCompra` **no se editan ni se borran** una vez creado el documento
  (`EntregaCompraDetalleDomain`/`FacturaCompraDetalleDomain` lo rechazan); solo las líneas del `PedidoCompra` siguen
  editables (`PedidoCompraDetalleDomain`).
- Ya existen los avisos de «Comprometido y Pedido» (`IExistenciaCompromisoService`): crear una entrega o factura de
  compra con líneas `BaseTipo = 11` recalcula `Pedido` automáticamente. No hay que tocarlos.

## 1. Reglas de negocio

### Documento activo y cantidad abierta

- **Origen activo:** `Cancelado != 'S'` **y** `EstadoDoc != 'C'`.
- **Destino que consume:** `Cancelado != 'S'` (el mismo criterio que usan `PedidoCompraDomain.ObtenerAbiertosAsync` y el
  recálculo de «Pedido»; no se mira `EstadoDoc` del destino).

```
abierta(líneaOrigen) = líneaOrigen.Cantidad
                     − Σ línea.Cantidad
                         de las líneas de cualquier documento destino ACTIVO
                         con BaseTipo = tipoOrigen, BaseEntry = entryOrigen, BaseLinea = noLinea
```

Se calcula **siempre en vivo**, nunca se guarda un contador. Cancelar o eliminar un destino libera su cantidad sin tocar
el origen. La cantidad se copia **en la unidad de la línea origen** (`UomEntry`, `FactorUom`; la cantidad base se
recalcula con `UnidadesLinea` como en ventas).

### Validación de una línea con base (al guardar el destino)

Para cada línea con `BaseTipo` distinto de `-1`, **antes de numerar** el documento y antes de abrir la transacción (un
rechazo no consume consecutivo):

1. La combinación (`BaseTipo` → tipo del destino) está en la tabla de destinos permitidos.
2. El documento origen existe y está activo.
3. El **proveedor** del destino es el del origen.
4. La línea origen existe y `CodArticulo` es el mismo (el artículo de una línea copiada no se puede cambiar).
5. `Cantidad > 0` y `Cantidad ≤ abierta`; varias líneas del payload contra la misma línea origen se suman.

`Precio`, `PrctjeDesc`, `CodigoImpuesto` y `CodAlmacen` quedan editables; se copian del origen sin volver a resolver nada. Una
línea sin base (`BaseTipo = -1`) sigue siendo válida en el mismo documento. Un error en cualquier línea rechaza el
documento completo. Los mensajes son los de ventas con «proveedor» donde ventas dice «socio»
(p. ej. `El proveedor del documento debe ser el del documento base Pedido de compra 5 (P001).`).

### Inventario

| Destino | Qué asienta |
|---|---|
| Entrega de compra | todas las líneas con cantidad > 0 (sin cambio) |
| Factura de compra | las líneas con cantidad > 0 y `BaseTipo != 12` (es decir, **no** las que vienen de una Entrega de compra, que ya sumó esa mercancía). Las líneas con base Pedido de compra o sin base sí suman, porque nadie recibió todavía. |

Reemplaza la regla actual (asentar todas). El filtro de inventario por artículo (`ArticuloInventario`) que ya existe en
la factura de compra no cambia.

### Integridad del origen

Un origen (Pedido de compra o Entrega de compra) con **al menos una línea con destinos activos**:

- **no se puede eliminar** (Pedido de compra y Entrega de compra, esta última ya exige cancelarla antes) ni **cancelar**
  (Entrega de compra `Cancelado = 'S'`; Pedido de compra `Cancelado = 'S'` o `EstadoDoc = 'C'`);
- el **Pedido de compra** no puede **cambiar de proveedor** (`CodigoSn`);
- sus **líneas ya consumidas no se pueden modificar ni eliminar** (`PedidoCompraDetalleDomain`; las de Entrega de compra
  ya son inmutables);
- agregar líneas nuevas al Pedido de compra sigue permitido.

El mensaje nombra los documentos que lo usan: `No se puede cancelar el documento: lo usan Entrega de compra 3 y Factura de
compra 2. Cancele primero esos documentos.` Esto corrige además el hueco equivalente de compras: hoy se puede cancelar una
Entrega de compra ya facturada y el inventario queda descuadrado.

## 2. API

### Servicio de documento base de compras (`API.Domain.Core`)

`IDocumentoBaseCompraService` (interfaz en `API.Domain.Interface`) con su implementación `DocumentoBaseCompraService` en
`API.Domain.Core`, **con la misma forma** que `IDocumentoBaseService`:

- `ObtenerBorradorAsync(int tipoOrigen, int entry, int tipoDestino)`: valida la combinación y devuelve cabecera y líneas
  con cantidad abierta > 0. Rechaza si el origen no existe (`DocumentoBaseMensajes.OrigenNoExiste`), está inactivo
  (`El documento origen está cancelado.`) o no tiene líneas abiertas (`El documento no tiene líneas abiertas.`).
- `ValidarLineasAsync(int tipoDestino, string? codigoSnDestino, IReadOnlyList<LineaBaseRequest> lineas)`.
- `ObtenerDerivadosAsync(int tipoOrigen, int entry, int? noLinea = null)`.
- `ValidarOrigenModificableAsync(int tipoOrigen, int entry, string accion)`.
- `ValidarLineaModificableAsync(int tipoOrigen, int entry, int noLinea, string accion)`.

Un conjunto de consultas cubre las dos tablas destino (`EntregaCompraDetalle`, `FacturaCompraDetalle`) filtradas por
`BaseTipo`, `BaseEntry` y cabecera no cancelada (las facturas solo con `DocSubType = '--'`). Las consultas deben
traducirse a SQL (sin evaluación en cliente): se verifican con SQL real.

### Tipos compartidos (`API.Domain.Interface`)

- **Nuevo:** `TipoDocumentoCompra` (constantes `PedidoCompra = 11`, `EntregaCompra = 12`, `FacturaCompra = 13`;
  `Nombre(tipo)` → «Pedido de compra», «Entrega de compra», «Factura de compra»; `DestinosDe(origen)`;
  `EsCombinacionValida`).
- **Reutilizados sin cambio de forma:** `LineaBaseRequest`, `LineaAbiertaVenta` (las líneas de compra tienen las mismas
  columnas), `CabeceraBorrador`, `BorradorDocumentoBase`, `LineasBase.Extraer`, `DocumentoBaseMensajes`.
- **Cambio mínimo en un tipo compartido:** `DocumentoDerivado.Etiqueta` hoy resuelve el nombre con
  `TipoDocumentoVenta.Nombre`; se hace que resuelva **ventas o compras** (los códigos no se solapan: ventas 3, 4, 5, 6,
  70; compras 11, 12, 13) mediante un ayudante `NombreDocumento.De(int tipo)`. Con prueba de que las etiquetas de ventas
  no cambian.

### Endpoint

`GET api/DocumentoBaseCompra/{tipoOrigen:int}/{entry:int}/Abierto?destino={tipoDestino:int}` →
`Respuesta<DocumentoBaseBorradorDTO>` (el mismo DTO de ventas). Controlador `DocumentoBaseCompraController` y casos de
uso `IDocumentoBaseCompraApplication`/`DocumentoBaseCompraApplication` (Application depende solo de `Domain.Interface`).

Permisos: **Ver** en el módulo del origen y **Agregar** en el del destino, evaluados con `PermisoUsuario.Tiene` como en
ventas. Errores: `400` combinación inválida / sin líneas abiertas / origen cancelado, `404` origen inexistente, `403` sin
permiso.

### Dominios destino

- `EntregaCompraDomain.InsertarAsync` y `FacturaCompraDomain.InsertarAsync`: llaman a `ValidarLineasAsync` **antes de
  numerar**, junto a las validaciones que ya tienen. `FacturaCompraDomain` cambia la condición de inventario a
  `BaseTipo != 12` y fuerza la cabecera `BaseTipo = -1` si el `CHECK` lo exige.
- Los DTO de línea de `EntregaCompra` y `FacturaCompra` ya mapean `BaseTipo`/`BaseEntry`/`BaseLinea` (a confirmar en el
  plan, tarea 1); donde falten se agregan.
- Se conserva el aviso de «Comprometido y Pedido» después de confirmar (sin cambios): se ejecuta ya con líneas de base.

### Dominios origen

`PedidoCompraDomain` (eliminar; cancelar/cerrar; cambiar proveedor, en `ActualizarAsync`), `EntregaCompraDomain`
(cancelar y eliminar) y `PedidoCompraDetalleDomain` (actualizar/eliminar línea) invocan
`ValidarOrigenModificableAsync`/`ValidarLineaModificableAsync`. **Orden con los avisos de «Comprometido y Pedido»:** la
validación va **antes** de cualquier lectura previa (`ObtenerParesAsync`) y de cualquier cambio, y los avisos siguen siendo
el último paso tras confirmar.

## 3. Web

- **Listados** de Pedidos de compra y Entregas de compra: botón desplegable **"Copiar a"** en cada fila no cancelada, con solo
  los destinos válidos del origen y para los que el usuario tiene permiso de Agregar (los permisos de los módulos destino
  se agregan al JSON de permisos que ya emite cada `Index`). Igual que en `Views/Pedidos/Index.cshtml` (`PuedeCopiar`).
- **Apertura del destino:** `…/FormularioCrear?baseTipo=&baseEntry=` en los controladores de Entregas de compra y Facturas
  de compra. El controlador pide el borrador a la API (cliente nuevo `IDocumentoBaseCompraApiClient` en `Web.ApiClient`, con
  el mismo DTO de borrador) y lo entrega al formulario como JSON.
- **Formulario del destino con base** (`entregascompra.js`, `facturascompra.js`): mismo comportamiento que ventas — carga
  las líneas con sus referencias base; proveedor, dirección y moneda quedan de solo lectura; la cantidad es editable con tope
  en `CantidadAbierta` (columna informativa "Abierto: N" y "Base: Pedido de compra 12"); se puede quitar una línea (solo de
  la lista local) y agregar líneas libres; si la línea origen no trae almacén se elige en el formulario; no corre el
  recálculo por proveedor (no cambia); al guardar se envía todo en **una** petición con las referencias base en cada línea.
  Se reutilizan los ayudantes de «Copiar a» de `site.js` (borrador, líneas con base, botón) en lo que sean genéricos; el
  plan indica qué se generaliza y qué se duplica.
- Tras guardar: mensaje `"Entrega de compra N creada desde Pedido de compra M"` y recarga del listado. Los errores del
  servidor (cantidad excedida, origen cancelado, proveedor distinto) se muestran con los mensajes del dominio.

## 4. Pruebas

- **Dominio** (xUnit + Moq, convención de `API.Service.WebApi.Tests`): cantidad abierta (parcial, agotada, destino cancelado
  libera, origen con `EstadoDoc = 'C'` inactivo), validación de cada combinación válida e inválida, artículo distinto,
  cantidad excedida, proveedor distinto, líneas del mismo origen que se suman, unidad de medida (la cantidad se copia en la
  unidad de la línea), regla de inventario de la Factura de compra (base 11 y sin base suman, base 12 no, mezcla de líneas),
  consecutivo no consumido ante error, y todas las reglas de integridad del origen (cancelar, eliminar, cambiar proveedor,
  editar/quitar línea copiada, agregar línea nueva permitido). Las etiquetas de `DocumentoDerivado` de ventas no cambian.
- **Controladores:** permisos del endpoint (403 sin Ver en origen o sin Agregar en destino), `400`/`404`.
- **Orden con «Comprometido y Pedido»:** una prueba por dominio origen de que la validación de integridad ocurre antes de
  `ObtenerParesAsync` y de que un rechazo no avisa.
- **Prueba de humo real** en una base temporal `API_DB_EVID<n>` (nunca `API_DB_TEST`): Pedido de compra → Entrega parcial →
  Factura sobre la entrega (el stock no se suma dos veces); Pedido → Factura directa (suma stock); copia que excede lo
  abierto (rechazada sin consumir consecutivo); cancelar un destino y comprobar que la cantidad vuelve; intentar
  cancelar/eliminar/editar un origen con destinos (rechazado con el mensaje que nombra los documentos); y que la columna
  **Pedido** de Existencias baja al recibir y al facturar contra el pedido; acceso `401`/`403`; consultas traducidas a SQL.
- **Navegador:** botón "Copiar a" en ambos listados, formulario precargado, tope de cantidad, guardado y mensaje; usuario con
  y sin permiso de Agregar en el destino.
- **Compilación** de API y Web con 0 errores y **suite completa en verde** (línea base 3,918 pruebas).

## 5. Entrega por fases

1. **Servicio de compras:** tipos, `DocumentoBaseCompraService` (borrador, validación, derivados), aplicación, controlador y
   endpoint, con pruebas.
2. **Dominios destino:** validación de líneas con base y regla de inventario `BaseTipo != 12`.
3. **Integridad del origen:** pedido, entrega y líneas de pedido de compra.
4. **Web:** cliente, listados con "Copiar a", formularios precargados.
5. **Verificación:** humo con SQL real, recorrido en navegador y documentación (`AGENTS.md`, traspaso).

Cada fase cierra con build sin errores y suite completa en verde.

## Fuera de alcance

- "Copiar desde…" con varios orígenes en el destino.
- Oferta de compra y el Pedido de compra como destino.
- Notas de débito y de crédito de proveedor (tienen su propio documento base).
- Referencia a documento base en la cabecera (el esquema la prohíbe o la deja fija en `-1`).
- Columna de estado derivado ("Abierto/Parcial/Cerrado") en los listados.
- Retirar `PedidoCompraDomain.ObtenerAbiertosAsync` (queda como código sin uso).

## Riesgos conocidos

- **Concurrencia:** dos usuarios copiando a la vez la misma línea podrían pasar ambos la validación; se acepta para el
  volumen previsto (uno a tres usuarios), igual que en ventas. La validación corre justo antes de numerar y guardar.
- **Datos existentes:** ningún documento de compra actual tiene líneas con base, así que no hay migración de datos. Las
  facturas de compra ya creadas descuentan/suman igual que antes (todas las líneas asentadas).
- **Código repetido:** el servicio de compras repite la estructura del de ventas (~290 líneas). Es el precio de no tocar lo
  ya probado; si más adelante conviene, se extrae un motor genérico con un spec propio.
- **Pedido de compra y `EstadoDoc`:** el criterio de «activo» del origen (`EstadoDoc != 'C'`) y el del destino que consume
  (solo `Cancelado`) difieren a propósito; documentado arriba y cubierto por pruebas.

## Puntos que el plan debe verificar al leer el código

- Que `PedidoCompraCrearDTO`, `EntregaCompraDetalleCrearDTO` y `FacturaCompraDetalleCrearDTO` mapean `BaseTipo`/`BaseEntry`/
  `BaseLinea` y que `PerfilMapeo` no los ignora.
- El `CHECK` de `BaseTipo`/`BaseEntry` de las cabeceras de compra y cómo se fuerza a `-1`.
- Cómo cancela hoy un Pedido de compra (`ActualizarAsync` con `Cancelado`/`EstadoDoc`) y cuál es la señal exacta de
  "cancelar" y de "cambiar proveedor" para colgar la validación, sin romper el `PUT` que reemplaza columnas y conserva `Nit`.
- Las rutas y formularios de `PedidosCompra`/`EntregasCompra`/`FacturasCompra` en la Web y qué helpers de `site.js` de
  «Copiar a» son genéricos.
- Que `FacturaCompraDomain` ya valida el artículo/almacén de las líneas y qué hace con líneas sin almacén (el borrador puede
  traer `CodAlmacen` nulo).
