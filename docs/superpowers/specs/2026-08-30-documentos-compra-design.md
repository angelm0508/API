# Diseño: módulos Pedido, Entrega y Factura de compra (sub-proyecto A)

## Contexto

El sistema ya tiene el proceso de **ventas** completo (Cotización → Pedido → Entrega →
Factura), API + Web, incluida la corrección de numeración "peek-only" (el consecutivo de
una serie solo avanza cuando el documento se registra de verdad) y el buscador con
autocompletado en el formulario de cada documento.

El usuario creó en `API_DB_TEST` seis tablas nuevas para el proceso de **compra**:

- `PedidoCompra` / `PedidoCompraDetalle`
- `EntregaCompra` / `EntregaCompraDetalle`
- `FacturaCompra` / `FacturaCompraDetalle`

Verificado contra la base de datos real: cada tabla es un **clon estructural exacto** de su
equivalente de venta (`Cotizacion`/`CotizacionDetalle`), con las mismas columnas, tipos,
nulabilidad, PK y llaves foráneas. Las únicas diferencias:

| Documento | `TipoObjeto` (CHECK, header y detalle) | `CodigoObj` en `NumeracionDocumento` |
|---|---|---|
| PedidoCompra | `'11'` | `11` |
| EntregaCompra | `'12'` | `12` |
| FacturaCompra | `'13'` | `13` |

Diferencia adicional respecto a ventas: las tablas `*CompraDetalle` **sí** tienen FK de
impuesto (`fk_{tipo}_compra_det_cod_impuesto` → `Impuesto.Codigo`); las de venta no la
tienen. El resto de FKs son análogas: `fk_{tipo}_compra_sn` → `SocioNegocio.Codigo`,
`_moneda` → `Moneda.Codigo`, `_serie` → `NumeracionDocumentoDet.Serie`, `_det_almacen` →
`Almacen.Codigo`, `_det_cod_art` → `Articulo.Codigo`.

**Numeración ya configurada** (a diferencia del spec de ventas): `NumeracionDocumento` ya
tiene las filas `11`/`12`/`13` y `NumeracionDocumentoDet` ya tiene, por cada una, una serie
Manual y una serie Primaria con `SigNumero = 1`. No se siembra nada por script.

Existe además una fila `CodigoObj = 10` "Oferta Compra" configurada en `NumeracionDocumento`
y series en `NumeracionDocumentoDet`. **Fuera de alcance** — el usuario no pidió ese módulo.

## Alcance de este sub-proyecto (A)

- **API completa + CRUD completo en la Web** para los tres documentos de compra, al mismo
  nivel que Entregas/Facturas de venta hoy: listado, crear con líneas de detalle embebidas,
  editar, eliminar, y el buscador con autocompletado (Socio de Negocio / Artículo / Almacén
  / Impuesto) en el formulario.
- **Sin efecto en inventario.** Registrar una EntregaCompra o FacturaCompra en este
  sub-proyecto no mueve stock — igual que Entrega/Factura de venta hoy. El asiento de
  inventario es el sub-proyecto B+C, que se diseñará por separado después.

### Fuera de alcance (explícito)

- Lógica de "crear documento desde el anterior" en la cadena de compra
  (PedidoCompra → EntregaCompra → FacturaCompra vía `BaseTipo`/`BaseEntry`). Los campos
  quedan en el modelo sin funcionalidad, igual que en ventas. Tarea futura independiente.
- Módulo "Oferta Compra" (`CodigoObj = 10`).
- Cualquier movimiento o tabla de inventario (sub-proyecto B+C).
- Cambios de esquema a tablas existentes. Ninguna tabla existente gana columnas.

## Decisiones confirmadas con el usuario

1. **Filtro por tipo de socio.** El buscador de Socio de Negocio filtra por `TipoSN`:
   los documentos de **compra** muestran solo proveedores (`TipoSN = 'P'`) y los de
   **venta** solo clientes (`TipoSN = 'C'`). Esto toca código de ventas ya entregado
   (ver "Cambios al código de ventas existente").
2. **Nombres y rutas.** Entidad y recurso API en singular: `PedidoCompra` /
   `EntregaCompra` / `FacturaCompra`; rutas `api/PedidoCompra`, `api/EntregaCompra`,
   `api/FacturaCompra` (+ `...Detalle`). Lado Web en plural sobre el primer término,
   igual que ventas ("Pedidos"/"Entregas"): controladores `PedidosCompraController` /
   `EntregasCompraController` / `FacturasCompraController`, vistas `Views/PedidosCompra/`,
   `Views/EntregasCompra/`, `Views/FacturasCompra/`, y JS `pedidoscompra.js` /
   `entregascompra.js` / `facturascompra.js`. Coincide con el nombre real de las tablas.
3. **Corrección de dato `SerieDfct`.** `NumeracionDocumento` para `CodigoObj = '13'`
   (Factura Compra) tiene `SerieDfct = 11`, que apunta a una serie de Factura de **venta**
   (la serie 11 pertenece a `CodigoObj = '6'`). Las series reales de Factura Compra son
   26 (Manual) y 27 (Primaria). Se corrige a `SerieDfct = 27` mediante script SQL
   versionado en el repo (`API/sql/2026-08-30-fix-seriedfct-factura-compra.sql`) que además
   se aplica a la base local.
4. **Encadenamiento de documentos**: fuera de alcance (ver arriba).

## Enfoque

Réplica mecánica del stack de ventas Pedido/Entrega/Factura, ya validado y probado. No hay
decisiones de arquitectura nuevas por tipo de documento. El diseño consiste en clonar cada
capa cambiando:

1. El nombre de la entidad/DTO/clase (`Entrega` → `PedidoCompra` / `EntregaCompra` /
   `FacturaCompra`).
2. La constante `TipoObjeto` forzada en el servidor (`"11"` / `"12"` / `"13"`).
3. El `CodigoObj` para buscar series de numeración (`"11"` / `"12"` / `"13"`).
4. Las rutas API y Web.
5. El mapeo EF gana la FK de impuesto en el detalle (`fk_{tipo}_compra_det_cod_impuesto`).

## Componentes por módulo (API)

Nomenclatura en esta sección: `{Tipo}` es el nombre de entidad/recurso API en singular
(`PedidoCompra` / `EntregaCompra` / `FacturaCompra`); `{TipoWeb}` es el nombre Web en plural
(`PedidosCompra` / `EntregasCompra` / `FacturasCompra`).

Para cada documento `{Tipo}` ∈ {PedidoCompra, EntregaCompra, FacturaCompra}:

- **Entidades**: `API.Domain.Entity.Models/{Tipo}.cs` y `{Tipo}Detalle.cs` (PK compuesta
  `Entry`+`NoLinea` en el detalle), con navegaciones (`CodigoSnNavigation`,
  `MonedaDocNavigation`, `SerieNavigation` en el encabezado; `CodArticuloNavigation`,
  `CodAlmacenNavigation`, `CodigoImpuestoNavigation` en el detalle) y las colecciones
  inversas correspondientes en `SocioNegocio`, `Monedum`, `NumeracionDocumentoDet`,
  `Articulo`, `Almacen` e `Impuesto`.
- **`ApiDbTestContext.cs`**: `DbSet<{Tipo}>` / `DbSet<{Tipo}Detalle>` + bloques
  `OnModelCreating` que mapean exactamente las constraints reales (nombres de FK
  `fk_{tipo}_compra_*`, defaults, longitudes, CHECK de `TipoObjeto`). Incluye la FK de
  impuesto del detalle, que el patrón de ventas no tiene.
- **DTOs**: `API.Application.DTO/{tipo}/{Tipo}DTO.cs`, `{Tipo}CrearDTO.cs` (con `NumDoc`
  opcional), `{Tipo}ActualizarDTO.cs`, y los tres equivalentes de `{Tipo}Detalle`.
- **Dominio**: `I{Tipo}Domain` / `{Tipo}Domain` — `InsertarAsync` fuerza `TipoObjeto` a la
  constante del tipo y calcula/avanza el consecutivo de la serie **exactamente** como
  `EntregaDomain.InsertarAsync` (serie inexistente → error; serie bloqueada → error; serie
  Manual → exige `NumDoc > 0`; serie autogenerada → `SigNumero` actual, valida
  `FinNumero`, incrementa en memoria y persiste junto con el INSERT en el mismo
  `SaveChangesAsync`). `EliminarAsync` borra primero las líneas de detalle (no hay
  FK/cascada en la base de datos). `I{Tipo}DetalleDomain` / `{Tipo}DetalleDomain` calcula
  `NoLinea` como `max + 1` por `Entry`, igual que `EntregaDetalleDomain`. El detalle **no**
  fuerza `TipoObjeto` (queda al default de la columna, igual que ventas).
- **Repositorios**: `{Tipo}Repositorio` (genérico `int`), `{Tipo}DetalleRepositorio`
  (genérico `(int Entry, int NoLinea)` con `override ObtenerAsync` →
  `FindAsync(id.Entry, id.NoLinea)`).
- **Aplicación**: `I{Tipo}Application` / `{Tipo}Application`, `I{Tipo}DetalleApplication` /
  `{Tipo}DetalleApplication`.
- **Controladores**: `{Tipo}Controller` (`api/{Tipo}`), `{Tipo}DetalleController`
  (`api/{Tipo}Detalle`, rutas `{entry:int}/{noLinea:int}` y `Por{Tipo}/{entry:int}`).
- **DI**: registros en `Startup.cs` para repos genéricos, dominios y aplicaciones de ambas
  entidades.
- **Mapper**: entradas `CreateMap` en `PerfilMapeo.cs`.

### Filtro de proveedor/cliente en la búsqueda de Socio de Negocio (API)

- `SocioNegocioController`: `ContengaNombre/{nombre}` y `GET api/SocioNegocio` (ObtenerTodo)
  ganan un parámetro opcional `[FromQuery] string? tipo`. Cuando `tipo` es `"C"` o `"P"`,
  el resultado se filtra por `SocioNegocio.TipoSN == tipo`; sin `tipo` (o con cualquier
  otro valor), comportamiento actual: sin filtro.
- `ISocioNegocioApplication` / `SocioNegocioApplication` e `ISocioNegocioDomain` /
  `SocioNegocioDomain`: los métodos de listado/búsqueda aceptan el filtro opcional.
- Sin nuevos endpoints; solo parámetro opcional — no rompe llamadas existentes.

## Componentes por módulo (Web)

- **`Web.ApiClient`**: `Dtos/{Tipo}/*.cs`, `Dtos/{Tipo}Detalle/*.cs`,
  `I{Tipo}ApiClient` / `{Tipo}ApiClient`, `I{Tipo}DetalleApiClient` /
  `{Tipo}DetalleApiClient`; registro de los `HttpClient` tipados en `Program.cs`.
- **`SocioNegocioApiClient`**: `ObtenerTodoAsync` y `ObtenerContenganNombreAsync` ganan un
  parámetro opcional `tipo` que se pasa como query string.
- **Controlador Web**: `{TipoWeb}Controller` — mismas acciones que `EntregasController`
  (`Index`, `ObtenerTodos`, `FormularioCrear`, `FormularioEditar`, `Crear` devolviendo el
  `NumDoc` real recién asignado, `Editar`, `Eliminar`, `ObtenerDetalle`, `CrearLinea`,
  `EditarLinea`, `EliminarLinea`, `BuscarSocios`, `BuscarArticulos`, `BuscarAlmacenes`,
  `BuscarImpuestos`, `ObtenerAlmacenPorCodigo`, `ObtenerImpuestoPorCodigo`,
  `CargarDropdownsAsync`), con sus constantes `CodigoObj{Tipo}` (`"11"`/`"12"`/`"13"`) y
  `SubTipoDoc{Tipo}` (`"--"`). `BuscarSocios` pasa `tipo = "P"`.
- **Vistas**: `Views/{TipoWeb}/Index.cshtml`, `_Form.cshtml` — mismo patrón modal
  (encabezado + detalle embebido; líneas locales al crear, remotas al editar).
- **JS**: `wwwroot/js/{tipoweb}.js` (`pedidoscompra.js` / `entregascompra.js` /
  `facturascompra.js`), espejo de `entregas.js`.
- **Menú**: `_Layout.cshtml` — nuevo submenú **"Compras"** (`#submenuCompras`, con su
  `EsActivoCompras`) paralelo al de "Ventas", con enlaces a `PedidosCompra`,
  `EntregasCompra`, `FacturasCompra`.

## Cambios al código de ventas existente

Consecuencia de la decisión 1 (filtro por tipo de socio):

- Los cuatro controladores Web de venta (`Cotizaciones`, `Pedidos`, `Entregas`,
  `Facturas`): su acción `BuscarSocios` pasa `tipo = "C"`.
- `SocioNegocioApiClient` / `ISocioNegocioApiClient` y la cadena
  Application/Domain de `SocioNegocio` en la API: parámetro `tipo` opcional (arriba).
- Es un cambio aditivo y retrocompatible: sin `tipo` el comportamiento no cambia.

## Pruebas

Por cada uno de los tres módulos, en `API.Service.WebApi.Tests`:

- `{Tipo}ControllerTests.cs` y `{Tipo}DetalleControllerTests.cs` (mismo patrón que los de
  Entrega/EntregaDetalle, mockeando la capa de aplicación).
- `Domain/{Tipo}DomainTests.cs` (mismo patrón que `EntregaDomainTests.cs`: serie
  autogenerada, serie manual con/sin número, serie bloqueada, serie agotada, serie
  inexistente, `TipoObjeto` forzado).

Además:

- `SocioNegocioControllerTests` / pruebas de dominio: cubrir el filtro `tipo` (`"C"`,
  `"P"`, sin filtro) en búsqueda y listado.

Verificación final: `dotnet build` de ambas soluciones (API y Web) sin errores; `dotnet
test` de la suite completa de la API en verde; y verificación manual en el navegador de los
tres formularios de compra (crear con líneas, editar, eliminar, buscador con filtro de
proveedor) más una regresión rápida de un documento de venta (que el buscador siga
mostrando clientes).
