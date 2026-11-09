# Reportería dinámica — Diseño

**Fecha:** 2026-11-08
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`) y `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Referencia:** reportes con filtros y exportación de los sistemas de gestión (SAP B1: informes con selección de criterios, agrupación y exportación a Excel/PDF).
**Documentos relacionados:** `2026-10-26-vencimientos-estado-cuenta-antiguedad-design.md` (estado de cuenta y antigüedad de saldos, que el catálogo reutiliza), `2026-08-30-inventario-nucleo-design.md` (existencias y kardex), `2026-11-06-stock-por-almacen-design.md` (mínimo, máximo y bloqueo por almacén).

## Objetivo

Que el sistema tenga un **catálogo de reportes** con filtros dinámicos, donde el usuario elige filtros, columnas visibles y su orden, agrupación con subtotales y un gráfico, guarda sus combinaciones favoritas y saca el resultado a pantalla, Excel, PDF, CSV o impresión. Hoy el sistema solo tiene pantallas fijas (antigüedad por cobrar y por pagar, estado de cuenta, existencias con kardex y el tablero del Home), sin exportación ni gráficos.

### Decisiones tomadas (brainstorming)

1. **Alcance: catálogo de reportes con filtros dinámicos** (no un constructor ad hoc ni solo tableros). Los reportes están definidos de antemano; lo dinámico es lo que el usuario puede variar sobre cada uno.
2. **Cuatro familias en la primera versión:** ventas, compras, inventario, cobros y pagos (18 reportes, ver §4).
3. **Salidas:** pantalla (tabla con totales), Excel (.xlsx), PDF, CSV e impresión desde el navegador.
4. **Personalización sobre el resultado:** elegir columnas visibles y su orden, ordenar, agrupar con subtotales, gráfico del resultado y vistas guardadas por usuario.
5. **Enfoque A:** catálogo de definiciones en código C# con **un motor genérico** (no vistas SQL por reporte ni SQL guardado como dato). Es seguro (sin SQL libre), probable con xUnit y respeta las capas.

### Fuera de alcance

Reportes programados o enviados por correo; diseñador visual de reportes o constructor ad hoc; cruces de datos entre familias; conversión entre monedas; rotación de inventario; reportes por vendedor (el sistema no guarda vendedor); reemplazar las pantallas actuales de estado de cuenta, antigüedad y existencias (siguen como están; solo se agregan sus versiones del catálogo); vistas compartidas entre usuarios; tableros de indicadores.

## Contexto verificado

- La API es N-capas: `Service.WebApi` → `Application.Main` → `Domain.Core` → `Infraestructure.Repository` (EF Core 7). `Application.*` depende solo de `Domain.Interface`; los tipos y las interfaces compartidas van en `Domain.Interface`. Respuestas siempre `Respuesta<T>`; permisos con `[RequierePermiso("Modulo", Accion.X)]`.
- Ya existen servicios de dominio reutilizables: `IAntiguedadService`, `IEstadoCuentaService`, `ISaldoFacturaService`, `ISaldoFacturaCompraService`, `ISaldoSocioService`, y `StockAlmacenCalculadora` (estado de stock por almacén) en `Domain.Interface`.
- Las notas de débito comparten tabla con la factura (`DocSubType` `'ND'` en `Factura`, `'DN'` en `FacturaCompra`); las notas de crédito tienen tablas propias (`NotaCredito*`, `NotaCreditoCompra*`) y sus líneas no llevan artículo.
- En las líneas, `Precio` es **neto de IVA**; `Impuesto` es el monto del IVA de la línea y `TotalLinea` = neto con descuento + impuesto.
- Todo movimiento de stock está en `MovimientoInventario` (`TipoDoc`, `DocEntry`, `DocLinea`, `CodArticulo`, `CodAlmacen`, `CantidadEntra`, `CantidadSale`, `CostoUnitario`, `ValorMovimiento`); las existencias, en `ExistenciaArticulo`.
- El usuario autenticado llega en el claim `NameIdentifier` (valor = `Usuario.ID`).
- La Web trae DataTables (sin su plugin de botones), jQuery, Bootstrap 5, SweetAlert2 y Font Awesome; **no hay librería de gráficos**. Hoy hay moneda única (QTZ).

## §1 — Arquitectura y componentes

### API

**`API.Domain.Interface/Reportes`** (tipos compartidos, sin dependencias de EF ni de librerías de archivos):

| Tipo | Contenido |
|---|---|
| `IReporte` | `ReporteDefinicion Definicion { get; }` y `Task<IReadOnlyList<FilaReporte>> EjecutarAsync(IReadOnlyDictionary<string, FiltroValor> filtros)` |
| `ReporteDefinicion` | `Codigo`, `Nombre`, `Descripcion`, `Familia`, `Modulo` (módulo de permiso), `Filtros`, `Columnas` |
| `ColumnaReporte` | `Campo`, `Titulo`, `Tipo` (`Texto`, `Entero`, `Cantidad`, `Dinero`, `Fecha`), `Agrupable`, `Totaliza` (`Ninguna`, `Suma`, `Promedio`, `Conteo`), `VisiblePorDefecto` |
| `FiltroReporte` | `Campo`, `Titulo`, `Tipo` (`Fecha`, `RangoFechas`, `Texto`, `Numero`, `Lista` con opciones fijas, `Socio`, `Articulo`, `Almacen`), `Obligatorio`, `ValorPorDefecto` |
| `SolicitudReporte` | `Filtros`, `Columnas` (campos en el orden pedido), `Orden` (lista de `{Campo, Descendente}`), `Agrupar` (hasta 2 `{Campo, Parte}`; `Parte` = `Dia`, `Mes` o `Anio` solo en fechas), `SoloResumen` (ocultar el detalle y dejar subtotales) |
| `ResultadoReporte` | `Columnas` usadas, `Filas` o `Grupos` (árbol con `Clave`, `Etiqueta`, `Subtotales`, `Filas`/`Hijos`), `Totales`, `Truncado`, `TotalFilasEncontradas` |
| `IExportadorReporte` | `Formato` (`xlsx`, `pdf`, `csv`) y `byte[] Exportar(ResultadoReporte, ReporteDefinicion, ContextoExportacion)`; `ContextoExportacion` lleva empresa, usuario, fecha, filtros en texto y la imagen opcional del gráfico |

`FilaReporte` es un diccionario `campo → valor` (las filas de un reporte dinámico no tienen forma fija).

**`API.Domain.Core/Reportes`**
- `MotorReportes`: único para todos los reportes. Valida la solicitud contra la definición, ejecuta, ordena, agrupa y calcula subtotales y totales (ver §2).
- `Definiciones/*.cs`: una clase por reporte (`IReporte`), con su consulta EF/LINQ o delegando en un servicio existente. Sin SQL libre.
- `CatalogoReportes`: reúne las definiciones registradas por DI.

**Exportadores.** Las implementaciones usan **ClosedXML** (Excel, MIT) y **QuestPDF** (PDF, licencia *Community*, gratuita para empresas con ingresos anuales menores a US$1 millón; se declara en el código). Van en un proyecto nuevo `API.Infraestructure.Exportacion` que referencia solo `Domain.Interface` y se registra por DI; así `Application.Main` no depende de ninguna librería. El CSV se escribe a mano.

**`API.Application.Main`**: `ReporteApplication` (catálogo según los permisos del usuario, ejecutar, exportar, vistas guardadas) con sus DTO.

**`API.Service.WebApi/Controllers/ReporteController`** (`[Route("api/Reporte")]`):

| Método y ruta | Resultado |
|---|---|
| `GET api/Reporte` | Catálogo `[ {Codigo, Nombre, Descripcion, Familia} ]`, solo lo que el usuario puede ver |
| `GET api/Reporte/{codigo}` | Definición completa (filtros y columnas) |
| `POST api/Reporte/{codigo}/ejecutar` | `ResultadoReporte` para la `SolicitudReporte` del cuerpo |
| `POST api/Reporte/{codigo}/exportar?formato=xlsx\|pdf\|csv` | Archivo; el cuerpo es la misma solicitud más `graficoPng` (base64, opcional) |
| `GET api/Reporte/{codigo}/vistas` | Vistas guardadas del usuario para ese reporte |
| `POST api/Reporte/{codigo}/vistas` | Crea o reemplaza (por nombre) una vista |
| `DELETE api/Reporte/{codigo}/vistas/{entry}` | Borra una vista propia |

### Base de datos

Script nuevo `sql/2026-11-08-reporteria.sql` (idempotente), el mismo bloque al final de `sql/crear-api-db-test-completo.sql` y de `sql/crear-api-db-completo.sql`:

- Tabla `ReporteVista`: `Entry` int identity (PK), `UsuarioId` int NOT NULL (FK a `Usuario(ID)` con `ON DELETE CASCADE`), `CodigoReporte` nvarchar(50) NOT NULL, `Nombre` nvarchar(100) NOT NULL, `Configuracion` nvarchar(max) NOT NULL con `CHECK (ISJSON(Configuracion) = 1)`, `FechaCreacion` y `FechaActualizacion` datetime. Única por `(UsuarioId, CodigoReporte, Nombre)`.
- Módulos de permiso en `Modulo` (grupo `Reportes`): `ReportesVentas`, `ReportesCompras`, `ReportesInventario`, `ReportesCobrosPagos`.

### Web

- Entrada nueva **Reportes** en el menú lateral, con el catálogo agrupado por familia (solo los reportes que el usuario puede ver).
- **Una pantalla genérica** `Reportes/Ver?codigo=` que se arma desde la definición que devuelve la API: formulario de filtros (los de socio, artículo y almacén reutilizan el autocompletado de la Web), selector de columnas con orden, orden por columna, agrupación, tabla (DataTables), gráfico (Chart.js), botones Excel, PDF, CSV e Imprimir, y selector de vistas guardadas.
- Controlador `ReportesController` y clientes tipados en `Web.ApiClient`, como el resto de módulos; `wwwroot/js/reportes.js`.
- Chart.js se agrega a `wwwroot/lib` con versión fija (MIT); no se carga desde una CDN.

## §2 — Cómo funciona el motor

1. **Solicitud única.** La pantalla, la exportación y las vistas guardadas usan exactamente la misma `SolicitudReporte`; lo exportado coincide con lo que se ve.
2. **Validación.** Solo se aceptan filtros, columnas y campos de orden o agrupación que la definición declara. Un campo desconocido, un filtro obligatorio ausente, un valor de filtro con tipo incorrecto, una agrupación sobre una columna no agrupable o más de 2 niveles se rechazan con 400 y un mensaje en español. `Parte` solo se acepta en columnas de tipo `Fecha`.
3. **Ejecución.** La definición devuelve las filas ya filtradas. El motor materializa a lo sumo **50,000** filas; si hay más, devuelve las primeras, `Truncado = true` y `TotalFilasEncontradas`. La pantalla avisa que se refinen los filtros. Para decidir el truncado el motor pide 50,001 filas, sin contar toda la tabla.
4. **Orden.** Por cualquier columna del reporte, ascendente o descendente, con hasta 3 criterios. Sin orden pedido se usa el orden natural de la definición.
5. **Agrupación y subtotales.** Hasta 2 niveles. Las fechas se agrupan por día, mes o año. Cada columna numérica se totaliza con la regla de su definición (`Suma`, `Promedio`, `Conteo`) en cada grupo y en el total general. Se calcula en memoria sobre las filas ya limitadas. Con `SoloResumen = true` el resultado trae solo los subtotales.
6. **Columnas.** Solo las pedidas, en el orden pedido; sin columnas pedidas se usan las `VisiblePorDefecto`. Los totales y subtotales solo se calculan para columnas visibles.
7. **Gráfico.** El usuario elige el campo de categorías, el de valores y el tipo (barras, líneas, pastel). Se dibuja en la Web con Chart.js a partir del resultado agrupado (o, sin agrupación, de las filas). Para el PDF la Web envía la imagen del canvas como `graficoPng` (base64, máximo 2 MB; mayor → 400).
8. **Salidas.**
   - **Excel:** encabezado con empresa, reporte, filtros usados, usuario y fecha y hora; formato según el tipo de columna; subtotales en negrita; fila de total; encabezado fijo; autofiltro cuando no hay agrupación.
   - **PDF:** horizontal si hay más de 7 columnas; encabezado, número de página y el gráfico si llegó.
   - **CSV:** UTF-8 con BOM (para que Excel lo abra bien), separador coma, fechas `yyyy-MM-dd`, importes con punto decimal y las filas tal como se ven.
   - **Imprimir:** vista limpia con CSS de impresión, sin menú ni botones.
9. **Vistas guardadas.** La configuración completa (filtros, columnas, orden, agrupación, gráfico) se guarda como JSON por usuario y reporte, con nombre único y un máximo de **50 por usuario**. Al abrir una vista, lo que ya no existe en la definición se ignora sin fallar. Un usuario solo lista, reemplaza o borra las suyas; borrar la de otro devuelve 404.
10. **Moneda.** Los montos salen en la moneda del documento. Si en una consulta aparece más de una moneda, el motor agrega la columna `Moneda` y no suma importes de monedas distintas.
11. **Fechas.** Los filtros de fecha son inclusivos en ambos extremos (hasta las 23:59:59 del último día).

## §3 — Permisos y errores

- Un módulo por familia. `Accion.Ver` permite ejecutar, exportar y gestionar las vistas guardadas propias; no hay otras acciones. El catálogo solo muestra los reportes cuyo módulo el usuario puede ver; `admin` ve todos. Los módulos se asignan con las plantillas y los permisos por usuario que ya existen.
- Ejecutar o exportar un reporte sin permiso: 403. Reporte inexistente: 404. Solicitud inválida: 400 con `Respuesta<T>`.
- Que el resultado se trunque a 50,000 filas no es un error: llega como aviso en el resultado.
- Una excepción de negocio (por ejemplo, socio inexistente en el filtro) la atrapa `ReporteApplication` y devuelve `Resultado = false` con su mensaje, como el resto de la API.

## §4 — Catálogo de la primera versión

Reglas comunes: se excluyen los documentos cancelados (`Cancelado = 'S'`); las notas de crédito restan (importes con signo negativo) y las de débito suman. Todos los reportes con fecha traen filtro de rango. Las listas de columnas son el objetivo de diseño; el plan fija los nombres definitivos al leer las entidades.

### Ventas (`ReportesVentas`)

| Reporte | Fuente | Columnas principales | Filtros |
|---|---|---|---|
| Ventas por documento | `Factura` (facturas y notas de débito) y `NotaCredito` | Fecha, Tipo, Serie, No., Cliente, Moneda, Bruto, Descuento, Impuesto, Total | Fechas, cliente, tipo, serie |
| Ventas por artículo | Líneas de `FacturaDetalle` (facturas y notas de débito por artículos) y su costo en `MovimientoInventario` | Fecha, Documento, Cliente, Artículo, Almacén, Cantidad, Precio neto, Descuento, Venta neta, Costo, Utilidad | Fechas, cliente, artículo, grupo de artículos, almacén |
| Documentos de venta por estado | `Cotizacion`, `Pedido`, `Entrega`, `Factura` | Tipo, Fecha, No., Cliente, Estado (abierto, cerrado, cancelado), Total | Fechas, cliente, tipo, estado |
| Notas de crédito y de débito emitidas | `NotaCredito`, `Factura` con `DocSubType = 'ND'` | Fecha, Tipo, No., Cliente, Factura base, Total | Fechas, cliente, tipo |

«Venta neta» = cantidad × precio neto × (1 − descuento); «Utilidad» = venta neta − costo. Las notas de crédito no llevan artículo, por lo que no entran en «Ventas por artículo» (se ven en «Ventas por documento»).

### Compras (`ReportesCompras`)

| Reporte | Fuente | Columnas principales | Filtros |
|---|---|---|---|
| Compras por documento | `FacturaCompra` (facturas y notas de débito `DN`) y `NotaCreditoCompra` | Fecha, Tipo, Serie, No., Proveedor, Moneda, Bruto, Descuento, Impuesto, Total | Fechas, proveedor, tipo |
| Compras por artículo | Líneas de `FacturaCompraDetalle` | Fecha, Documento, Proveedor, Artículo, Almacén, Cantidad, Precio neto, Descuento, Compra neta | Fechas, proveedor, artículo, grupo, almacén |
| Documentos de compra por estado | `PedidoCompra`, `EntregaCompra`, `FacturaCompra` | Tipo, Fecha, No., Proveedor, Estado, Total | Fechas, proveedor, tipo, estado |
| Historial de precios de compra por artículo | `FacturaCompraDetalle` | Fecha, Proveedor, Artículo, Cantidad, Precio neto, Descuento | Artículo (obligatorio), fechas, proveedor |

### Inventario (`ReportesInventario`)

| Reporte | Fuente | Columnas principales | Filtros |
|---|---|---|---|
| Existencias y valor por almacén | `ExistenciaArticulo` + `Articulo` | Almacén, Artículo, Nombre, Grupo, En stock, Comprometido, Pedido, Costo unitario, Valor | Almacén, artículo, grupo, solo con existencias |
| Kardex por artículo | `MovimientoInventario` | Fecha, Documento, Línea, Almacén, Entra, Sale, Saldo acumulado, Costo | Artículo (obligatorio), fechas, almacén |
| Movimientos por período | `MovimientoInventario` | Fecha, Tipo de documento, Documento, Artículo, Almacén, Entra, Sale, Valor | Fechas, artículo, almacén, tipo de documento |
| Stock bajo mínimo o sobre máximo | `ExistenciaArticulo`, `ArticuloAlmacen` (reutiliza `StockAlmacenCalculadora`) | Almacén, Artículo, En stock, Mínimo, Máximo, Estado, Nivel necesario | Almacén, grupo, estado |

«Valor» = en stock × costo según el método de valuación del artículo (promedio o estándar). «Saldo acumulado» lo calcula la definición ordenando por fecha y número de movimiento, y por eso el artículo es obligatorio.

### Cobros y pagos (`ReportesCobrosPagos`)

| Reporte | Fuente | Columnas principales | Filtros |
|---|---|---|---|
| Facturas por cobrar | `ISaldoFacturaService` | Fecha, No., Cliente, Vencimiento, Total, Pagado, Saldo, Estado, Días de atraso | Cliente, estado de pago, solo vencidas, fecha de corte |
| Facturas por pagar | `ISaldoFacturaCompraService` | Igual, con proveedor | Proveedor, estado de pago, solo vencidas, fecha de corte |
| Pagos recibidos | `PagoRecibido` y sus medios | Fecha, No., Cliente, Forma de pago, Monto, Facturas aplicadas | Fechas, cliente, forma de pago |
| Pagos efectuados | `PagoEfectuado` y sus medios | Fecha, No., Proveedor, Forma de pago, Monto, Facturas aplicadas | Fechas, proveedor, forma de pago |
| Estado de cuenta de un socio | `IEstadoCuentaService` | Fecha, Documento, Cargo, Abono, Saldo | Socio (obligatorio), fechas |
| Antigüedad de saldos | `IAntiguedadService` | Socio, Corriente, 1–30, 31–60, 61–90, Más de 90, Total | Tipo (cobrar o pagar), fecha de corte, socio |

Estado de cuenta y antigüedad **reutilizan los servicios existentes**: sus cifras no cambian y sus pantallas actuales siguen funcionando.

## §5 — Entrega por fases

| Fase | Contenido |
|---|---|
| **1. Núcleo** | Contratos y motor (columnas, orden, validación, truncado), `ReporteController`, módulos de permiso y script SQL, y la pantalla genérica de la Web con filtros, columnas, tabla, CSV e impresión. Entran 2 reportes: Ventas por documento y Existencias y valor por almacén. |
| **2. Agrupar y exportar** | Agrupación con subtotales y total general; Excel y PDF. |
| **3. Gráfico y vistas** | Gráfico en pantalla y en PDF; vistas guardadas por usuario (`ReporteVista`). |
| **4. Catálogo completo** | Los 16 reportes restantes, por familia. |

Cada fase se planifica y se cierra con build sin errores, suite completa en verde y recorrido en navegador.

## §6 — Pruebas

- **xUnit + Moq** (como el resto de la API): motor (validación, orden, agrupación en 1 y 2 niveles, partes de fecha, totales, promedio, conteo, truncado exacto en 50,000 y 50,001, columnas por defecto), cada definición contra datos en memoria (cancelados excluidos, signo de las notas de crédito, utilidad, saldo acumulado del kardex) y permisos del controlador (403, 404, catálogo filtrado, vistas de otro usuario).
- **Exportadores:** se genera cada archivo y se vuelve a leer (Excel con ClosedXML: encabezado, formatos, subtotales y total; CSV: BOM, separadores y formato numérico; PDF: bytes con cabecera `%PDF` y tamaño mayor que cero; con y sin gráfico).
- **SQL real:** pruebas de humo en una base temporal `API_DB_EVID<n>` creada con el script completo y eliminada al terminar, con datos que cubran cada familia. Es obligatorio porque algunas consultas LINQ solo fallan al traducirse a SQL. **Nunca** contra `API_DB_TEST` ni una base del usuario.
- **Web:** compilación sin errores y recorrido en navegador por fase (filtros, columnas, orden, agrupación, gráfico, descargas, impresión, vistas), con un usuario con permiso y otro sin él.

## §7 — Riesgos

- **Traducción de LINQ a SQL:** consultas con uniones y agrupaciones pueden fallar o ser lentas. Mitigación: pruebas con SQL real y consultas que filtren y proyecten en la base antes de materializar.
- **Memoria:** 50,000 filas por solicitud es razonable para esta empresa; el tope y el aviso de truncado lo limitan.
- **QuestPDF:** su licencia *Community* aplica por ingresos anuales; hay que revisarla si la empresa crece. Alternativa de reemplazo detrás de `IExportadorReporte`.
- **Gráfico en el PDF:** depende de que la Web envíe la imagen; sin ella, el PDF sale sin gráfico, no falla.
- **Permisos nuevos:** llegan con un script SQL y hay que asignarlos a las plantillas; sin eso, solo `admin` ve los reportes. API y Web se despliegan juntas.
- **Definiciones que reutilizan servicios existentes** (antigüedad, estado de cuenta): si esos servicios cambian, el catálogo cambia con ellos; las pruebas de ambos lo cubren.
- **XSS:** la Web debe escapar siempre el texto dinámico (títulos de columna, valores de filas, nombres de vistas) al pintar la tabla y los filtros; el catálogo de reportes no puede repetir la deuda conocida de los listados antiguos.
