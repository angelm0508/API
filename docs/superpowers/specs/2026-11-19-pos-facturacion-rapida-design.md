# POS: facturación rápida de mostrador (diseño)

Fecha: 2026-11-19. Repos: `API` (rama `desarrollo`) y `Web` (rama `main`). Estado: diseño aprobado por el usuario en chat (partes 1 a 3); pendiente de revisión del documento.

## 1. Objetivo y alcance

Un módulo **POS** para quien factura en mostrador: una pantalla pensada para teclado, lector de códigos de barras y pantalla táctil, que registra en un solo paso una **factura de contado** y su **cobro**, y entrega un **ticket** imprimible. La venta POS **es una factura normal** (`Factura`, `DocSubType = '--'`) con una serie propia, y el cobro es un **pago recibido** (`PagoRecibido`) aplicado a esa factura. Por eso inventario, costo, reportes (utilidad, kardex, cobros y pagos) y cuentas por cobrar siguen funcionando sin cambios.

Decisiones del usuario:

- Alcance de la primera versión: venta rápida + ticket (sin caja ni turnos).
- Siempre contado; cliente por defecto «Consumidor final» (CF), cambiable para poner otro NIT.
- Buscador por código o nombre, **campo código de barras** en el artículo y **cuadrícula de artículos rápidos**.
- Enfoque B: endpoint atómico `POST api/Pos/Venta` (factura y cobro en una sola transacción).

**Fuera de esta versión** (se hace con los módulos existentes): ventas a crédito, descuentos manuales, cambio de precio, venta en espera, presentaciones/unidades de medida distintas de la base, anulación y devoluciones (notas de crédito), caja y turnos, clave de idempotencia.

## 2. Pantalla y flujo (Web)

Menú nuevo «POS» (visible con `Pos.Ver`). Pantalla completa `Pos/Index`:

- **Buscador** (foco permanente) por código, código de barras o nombre. Enter con coincidencia **exacta** de código de barras o de código de artículo agrega 1 unidad (si ya está en la lista suma 1); si no, muestra sugerencias (hasta 20) para elegir con Enter o clic. Muestra existencias del almacén POS.
- **Lista de la venta:** `+`/`-` cantidad, `x` quitar. El precio sale de la lista de precios del cliente y no se edita.
- **Artículos rápidos:** botones grandes que agregan 1 unidad con un toque.
- **Cliente:** por defecto el cliente de la configuración (CF). Botón «Cambiar» (buscador de clientes). El NIT del documento se muestra y se puede ajustar antes de cobrar.
- **Totales:** subtotal, IVA, total.
- **Cobrar (F9):** ventana con las formas de pago activas (efectivo, tarjeta, transferencia, cheque…), con el total ya puesto en efectivo; reparto entre varias formas, botones rápidos de billetes y cambio calculado. Referencia opcional por forma.
- **Confirmar:** crea la venta y muestra el **ticket** (80 mm) con «Imprimir» y «Nueva venta» (Enter). «Ventas del día» lista las ventas POS de hoy y permite reimprimir.
- **Atajos:** F2 buscar, F4 cliente, F9 cobrar, Esc nueva venta/cancelar.
- El botón Cobrar se deshabilita mientras se envía. La venta en curso se conserva ante un error para reintentar.
- Si falta algo de la configuración, el POS no deja vender y dice qué completar.

## 3. Datos

Script nuevo `sql/2026-11-19-pos.sql` (idempotente), el mismo bloque al final de `sql/crear-api-db-test-completo.sql` (y del local sin seguimiento `crear-api-db-completo.sql`). Cambios:

**Tabla `PosConfiguracion`** (una fila, `Id = 1` con `CHECK (Id = 1)`):

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | int PK | siempre 1 |
| `SerieFactura` | int NULL | FK a `NumeracionDocumentoDet(Serie)`: serie de facturas (`CodigoObj = '6'`, `SubTipoDoc = '--'`), no manual ni bloqueada |
| `SeriePago` | int NULL | FK a `NumeracionDocumentoDet(Serie)`: serie de pagos recibidos (`CodigoObj = '24'`), no manual ni bloqueada. *(Añadida al leer `PagoRecibidoDomain`, que exige una serie de pagos.)* |
| `CodAlmacen` | nvarchar NULL | FK a `Almacen` |
| `CodigoImpuesto` | nvarchar NULL | FK a `Impuesto` (IVA por defecto de todas las líneas) |
| `ClienteDefecto` | nvarchar NULL | FK a `SocioNegocio`, de tipo cliente (`TipoSn = 'C'`) |
| `FormaPagoEfectivo` | nvarchar NULL | FK a `FormaPago`; por defecto `EFE`; solo con esta forma se acepta cambio |
| `MonedaDoc` | nvarchar NULL | FK a `Moneda` |

(El plan fija los tipos y longitudes exactos leyendo las tablas referenciadas.)

**Tabla `Articulo`** (2 columnas nuevas):

- `CodigoBarras NVARCHAR(50) NULL`, con índice único filtrado (`WHERE CodigoBarras IS NOT NULL`).
- `MostrarEnPos CHAR(1) NOT NULL DEFAULT 'N'` con `CHECK IN ('S','N')`.

**Módulos de permiso** (`Modulo`, grupo `POS`): `Pos` (Ver = usar el POS, ver ventas del día y reimprimir; Agregar = cobrar) y `PosConfiguracion` (Ver, Actualizar).

**Artículos rápidos:** los marcados `MostrarEnPos = 'S'` (hasta 24, por nombre); si no hay ninguno, los 12 más vendidos de los últimos 30 días. En ambos casos solo artículos activos, de venta y con precio > 0 en la lista del cliente por defecto.

**Artículo vendible en POS:** activo (`Activo = 'S'`), `ArticuloVenta = 'S'` y precio > 0 en la lista de precios del cliente. Se vende en la unidad base. Las reglas de stock existentes aplican al cobrar (stock insuficiente, artículo bloqueado en el almacén).

## 4. API (`api/Pos`)

Capas como el resto: controlador → `PosApplication` (casos de uso, DTO) → `PosDomain` (reglas; en `Domain.Core`) → repositorios genéricos. `Application` depende solo de `Domain.Interface`.

| Método y ruta | Permiso | Resultado |
|---|---|---|
| `GET api/Pos/Configuracion` | `PosConfiguracion.Ver` (y `Pos.Ver` para leer lo necesario en la pantalla) | Configuración y lista de faltantes |
| `PUT api/Pos/Configuracion` | `PosConfiguracion.Actualizar` | Guarda; valida cada referencia |
| `GET api/Pos/Articulos?texto=&cliente=` | `Pos.Ver` | Hasta 20 artículos vendibles (código, código de barras, nombre, precio, existencias del almacén POS) |
| `GET api/Pos/Articulos/porCodigo/{codigo}?cliente=` | `Pos.Ver` | Coincidencia exacta por código de barras o por código; 404 si no existe o no es vendible |
| `GET api/Pos/Rapidos` | `Pos.Ver` | Artículos rápidos con precio |
| `POST api/Pos/Venta` | `Pos.Agregar` | Crea factura + cobro; devuelve el ticket |
| `GET api/Pos/Ticket/{facturaEntry}` | `Pos.Ver` | Datos del ticket de una venta POS |
| `GET api/Pos/VentasDelDia` | `Pos.Ver` | Ventas POS de hoy (serie, número, hora, cliente, total) |

Los permisos se evalúan con `[RequierePermiso]`. Las respuestas son siempre `Respuesta<T>`; las excepciones de negocio las atrapa `PosApplication` (400; 404 si el mensaje lo indica).

## 5. La venta (`POST api/Pos/Venta`)

Cuerpo: `{ codigoCliente?, nit?, lineas: [{ codArticulo, cantidad }], pagos: [{ codFormaPago, monto, referencia? }] }`. Sin cliente se usa el de la configuración. **Todas las validaciones corren antes de numerar** (un rechazo no consume consecutivo).

1. **Configuración completa**; si falta algo: «El POS no está configurado: falta <campo>.»
2. **Cliente** existe, activo y de tipo cliente.
3. **Líneas:** al menos una; cantidad > 0; cada artículo vendible (ver §3); un mismo artículo repetido se suma.
4. **Cálculo en el servidor:** precio neto por unidad base de la lista de precios del cliente; **se ignora cualquier precio enviado**. Impuesto = el de la configuración. `TotalLinea = neto + impuesto`, con el mismo redondeo que el cálculo de líneas existente. Total del documento = suma.
5. **Pagos:** al menos uno; montos > 0; formas de pago activas. Suma de pagos ≥ total. Si sobra, **cambio = suma − total** y debe ser ≤ al monto pagado con la forma de efectivo; el cobro registrado es **exactamente el total** (el efectivo se reduce en el cambio).
6. **NIT:** el enviado (recortado, máx. 15; vacío = el NIT del cliente; si el cliente no tiene, `CF`).
7. **Crear la factura** con `FacturaDomain` (serie POS, `CondicionPago = 'CON'`, fecha de hoy, `PagoInmediato = total`, NIT, líneas con almacén POS). Se reutilizan sus reglas: numeración, inventario, stock por almacén, costo.
8. **Crear el pago recibido** con `PagoRecibidoDomain` (medios y la factura aplicada por el total).
9. **Una sola transacción** (todo o nada): ante cualquier fallo no queda factura, cobro, movimiento de inventario ni consecutivo gastado.
10. **Respuesta:** datos del ticket (§7).

**Transacciones anidables.** Hoy `EjecutorTransaccion.EjecutarAsync` abre siempre una transacción propia, y EF no permite anidarlas. Cambio: si ya hay una transacción activa en el contexto (`Database.CurrentTransaction`), la operación se ejecuta dentro de esa (con su `SaveChanges`) sin confirmar ni revertir por su cuenta; confirmar y revertir lo hace solo el nivel exterior, que también limpia el `ChangeTracker` ante una excepción. Sin transacción activa, el comportamiento actual no cambia (todas las pruebas existentes deben seguir pasando).

**Control de crédito.** La venta es de contado y llega con `PagoInmediato = total`, por lo que el control de crédito existente no la bloquea. El plan verifica que clientes con límite 0 («solo contado») puedan comprar por el POS.

**Cajero y marca POS.** La factura no guarda quién cobra. La venta POS escribe en `Factura.Comentario` el texto `Venta POS - Cajero: <usuario>` (el usuario es `ClaimTypes.Name` del token, que el controlador pasa al dominio como parámetro). Esa marca es lo que identifica una factura como venta POS para el ticket, la reimpresión y «Ventas del día» (así siguen siendo localizables aunque la serie POS de la configuración cambie después).

## 6. Ticket

Datos: empresa (clave `Reportes:Empresa` de la API, por defecto «Sistema de Gestión»), serie y número, fecha y hora, cajero (usuario que cobra), cliente y NIT, líneas (artículo, cantidad, precio, total), subtotal, IVA, total, formas de pago, pagado y cambio. La Web lo muestra en una vista imprimible de 80 mm (`@media print`, sin menú ni botones).

## 7. Errores

Mensajes literales en español; las pruebas los verifican. La pantalla los muestra con SweetAlert, conserva la venta y mantiene el foco en el buscador. Riesgo conocido, sin mitigación en esta versión: si la red falla después de que la API confirmó, un reintento podría duplicar la venta; «Ventas del día» sirve para verificar antes de reintentar.

## 8. Pruebas

- **xUnit** (`API.Service.WebApi.Tests`): `PosDomain` (cálculo, redondeo, pagos y cambio, validaciones y mensajes, NIT, artículo repetido, rollback total si falla el cobro o el stock), ejecutor de transacciones anidado, configuración (validaciones y faltantes), controlador y permisos.
- **SQL real** (base temporal `API_DB_EVID<n>`, nunca `API_DB_TEST`): venta de varias líneas, pago mixto con cambio, stock insuficiente que no deja nada, serie POS, artículo con código de barras duplicado rechazado, y comprobar que los reportes de utilidad y de cobros siguen cuadrando con ventas POS.
- **Navegador:** lector simulado (texto + Enter), atajos, cobro mixto, ticket e impresión, «Ventas del día».

## 9. Entrega por fases

1. **Base de datos y API:** script SQL, entidades, ejecutor anidable, configuración, búsqueda, artículos rápidos, venta atómica, ticket y ventas del día.
2. **Web, configuración y artículo:** pantalla «Configuración POS» y los campos «Código de barras» y «Mostrar en el POS» en la ficha del artículo.
3. **Web, pantalla POS:** buscador, lista, artículos rápidos, cobro, ticket y ventas del día.

Cada fase se cierra con build sin errores, suite completa en verde y verificación en navegador.

## 10. Puntos que el plan debe verificar al leer el código

- Servicio/endpoint existente de **precio por lista** de un artículo para un cliente, y qué hace cuando no hay precio.
- Cómo se resuelve la **unidad base** de una línea de factura sin `UomEntry` (`UnidadesLinea.ResolverLineaAsync`).
- De dónde sale el **usuario cajero** (claims) en la API.
- Tipos y longitudes exactos de las claves referenciadas por `PosConfiguracion`.
- Que el PUT de artículo (que reemplaza columnas) **conserve** `CodigoBarras` y `MostrarEnPos`.
- Que las series POS no choquen con la validación de `Factura` (`DocSubType = '--'`) ni con listados existentes de facturas.
