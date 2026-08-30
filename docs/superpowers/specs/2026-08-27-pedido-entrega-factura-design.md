# Diseño: módulos Pedido, Entrega y Factura

## Contexto

El proceso comercial de ventas del sistema sigue la cadena:

**Cotización → Pedido → Entrega → Factura**

- **Pedido**: representa un compromiso de compra del producto o servicio por parte del cliente, según el precio y las cantidades acordadas.
- **Entrega**: indica que se llevó a cabo el envío de las mercancías.
- **Factura**: el único documento obligatorio del proceso; puede por sí sola registrar el pedido, iniciar la entrega, contabilizar la salida de mercancías y registrar lo que el cliente debe.

Cotización ya está completamente implementada (API + Web), incluida la corrección reciente de numeración: el consecutivo de un documento solo avanza cuando el documento se registra de verdad, no al solo previsualizar el número.

El usuario recreó la base de datos y agregó tres tablas nuevas -- `Pedido`/`PedidoDetalle`, `Entrega`/`EntregaDetalle`, `Factura`/`FacturaDetalle` -- cada una con **exactamente la misma estructura de columnas, tipos, nulabilidad y foreign keys** que `Cotizacion`/`CotizacionDetalle` (verificado columna por columna y FK por FK contra la base de datos real). Ninguna tabla existente tiene columnas nuevas.

Cada tabla de encabezado exige un `TipoObjeto` fijo distinto, ya reflejado en el esquema real (constraint CHECK + default, ambos coinciden):

| Documento | `TipoObjeto` | `CodigoObj` (NumeracionDocumento) |
|---|---|---|
| Cotizacion | `'3'` | `3` |
| Pedido | `'4'` | `4` |
| Entrega | `'5'` | `5` |
| Factura | `'6'` | `6` |

Ninguno de los tres `CodigoObj` nuevos (4, 5, 6) tiene todavía filas en `NumeracionDocumento`/`NumeracionDocumentoDet` -- el usuario deberá agregar al menos una serie para cada uno desde la pantalla existente "Numeración de documentos" antes de poder crear registros, igual que hizo para Cotización.

## Decisiones ya confirmadas con el usuario

- Alcance por módulo: **API completa + CRUD completo en la Web**, al mismo nivel que Cotizaciones hoy (listado, crear con líneas de detalle embebidas, editar).
- **Fuera de alcance**: lógica de "crear un documento a partir del anterior" (copiar cabecera/líneas usando `BaseTipo`/`BaseEntry` para encadenar Cotización→Pedido→Entrega→Factura). Los campos quedan en el modelo, sin funcionalidad todavía -- es una tarea futura independiente.
- Orden de construcción: los tres módulos en esta misma sesión, uno tras otro, cada uno verificado (build + pruebas) antes de pasar al siguiente.
- Configuración de numeración (`NumeracionDocumento`/`NumeracionDocumentoDet` para CodigoObj 4/5/6): queda a cargo del usuario vía la pantalla ya existente, no se siembra por script.

## Enfoque

Cada uno de los tres documentos es una réplica estructural exacta de Cotización/CotizacionDetalle. El diseño consiste en replicar el stack completo ya construido y probado para Cotizaciones, cambiando únicamente:

1. El nombre de la entidad/DTO/clase en cada capa (`Cotizacion` → `Pedido` / `Entrega` / `Factura`).
2. La constante de `TipoObjeto` forzada en el servidor (`"4"` / `"5"` / `"6"`).
3. El `CodigoObj` usado para buscar series de numeración (`"4"` / `"5"` / `"6"`).
4. Las rutas API (`api/Pedido`, `api/Entrega`, `api/Factura`) y Web (`/Pedidos`, `/Entregas`, `/Facturas`).

No hay decisiones de arquitectura nuevas que tomar por tipo de documento -- el patrón ya está validado en producción (Cotizaciones) y las tres tablas nuevas son clones estructurales verificados.

## Componentes por módulo (API)

Para cada documento `{Tipo}` (Pedido, Entrega, Factura):

- **Entidades**: `API.Domain.Entity.Models/{Tipo}.cs` y `{Tipo}Detalle.cs` (clave compuesta `Entry`+`NoLinea` en el detalle), con sus navegaciones (`CodigoSnNavigation`, `MonedaDocNavigation`, `SerieNavigation` en el encabezado; `CodArticuloNavigation`, `CodAlmacenNavigation` en el detalle) y las colecciones inversas correspondientes agregadas a `SocioNegocio`, `Monedum`, `NumeracionDocumentoDet`, `Articulo` y `Almacen`.
- **`ApiDbTestContext.cs`**: `DbSet<{Tipo}>`/`DbSet<{Tipo}Detalle>` + bloques `OnModelCreating` mapeando exactamente las constraints reales (`fk_{tipo}_sn`, `fk_{tipo}_moneda`, `fk_{tipo}_serie`, `fk_{tipo}_det_cod_art`, `fk_{tipo}_det_almacen`, defaults y longitudes ya verificados contra la base de datos).
- **DTOs**: `API.Application.DTO/{tipo}/{Tipo}DTO.cs`, `{Tipo}CrearDTO.cs` (con `NumDoc` opcional, igual que se corrigió para Cotización), `{Tipo}ActualizarDTO.cs`, y los tres equivalentes para `{Tipo}Detalle`.
- **Dominio**: `I{Tipo}Domain`/`{Tipo}Domain` -- `InsertarAsync` fuerza `TipoObjeto` y calcula/avanza el consecutivo de la serie exactamente como `CotizacionDomain.InsertarAsync` (serie Manual respeta el número del cliente; serie autogenerada asigna `SigNumero` actual y lo incrementa en la misma transacción implícita del `SaveChangesAsync` del insert); `EliminarAsync` borra primero las líneas de detalle (no hay FK/cascada en la base de datos, igual que Cotización). `I{Tipo}DetalleDomain`/`{Tipo}DetalleDomain` calcula `NoLinea` como `max + 1` por `Entry`, igual que `CotizacionDetalleDomain`.
- **Repositorios**: `{Tipo}Repositorio` (genérico `int`), `{Tipo}DetalleRepositorio` (genérico `(int Entry, int NoLinea)` con `override ObtenerAsync` haciendo `FindAsync(id.Entry, id.NoLinea)`).
- **Aplicación**: `I{Tipo}Application`/`{Tipo}Application`, `I{Tipo}DetalleApplication`/`{Tipo}DetalleApplication`.
- **Controladores**: `{Tipo}Controller` (`api/{Tipo}`), `{Tipo}DetalleController` (`api/{Tipo}Detalle`, rutas `{entry:int}/{noLinea:int}` y `Por{Tipo}/{entry:int}`).
- **DI**: registros en `Startup.cs` para los repos genéricos, dominios y aplicaciones de ambas entidades.
- **Mapper**: entradas `CreateMap` en `PerfilMapeo.cs`.

## Componentes por módulo (Web)

- **`Web.ApiClient`**: `Dtos/{Tipo}/*.cs`, `Dtos/{Tipo}Detalle/*.cs`, `I{Tipo}ApiClient`/`{Tipo}ApiClient`, `I{Tipo}DetalleApiClient`/`{Tipo}DetalleApiClient`; registro de los HttpClients tipados en `Program.cs`.
- **Controlador Web**: `{Tipo}sController` -- mismas acciones que `CotizacionesController` (`Index`, `ObtenerTodos`, `FormularioCrear`, `FormularioEditar`, `Crear` con la mejora de devolver el `NumDoc` real recién asignado, `Editar`, `Eliminar`, `ObtenerDetalle`, `CrearLinea`, `EditarLinea`, `EliminarLinea`, `CargarDropdownsAsync`), con sus propias constantes `CodigoObj{Tipo}` y `SubTipoDoc{Tipo}`.
- **Vistas**: `Views/{Tipo}s/Index.cshtml`, `_Form.cshtml` -- mismo patrón modal de Cotizaciones (encabezado + detalle embebido, líneas locales al crear / remotas al editar).
- **JS**: `wwwroot/js/{tipo}s.js`, espejo de `cotizaciones.js` (incluida la corrección de no generar el número antes de guardar, y de mostrar el `NumDoc` real tras crear).
- **Menú**: `_Layout.cshtml` -- se agregan los tres enlaces dentro del submenú "Ventas" existente (hoy solo tiene "Cotizaciones").

## Pruebas

Por cada uno de los tres módulos, en `API.Service.WebApi.Tests`:
- `{Tipo}ControllerTests.cs` y `{Tipo}DetalleControllerTests.cs` (mismo patrón que los de Cotizacion/CotizacionDetalle, mockeando la capa de aplicación).
- `Domain/{Tipo}DomainTests.cs` (mismo patrón que `CotizacionDomainTests.cs`: serie autogenerada, serie manual con/sin número, serie bloqueada, serie agotada, serie inexistente, `TipoObjeto` forzado).

Verificación final: `dotnet build` de ambas soluciones (API y Web) sin errores, y `dotnet test` de la suite completa de la API en verde.

## Fuera de alcance (explícito)

- Lógica de "crear documento desde el anterior" en la cadena de ventas (copiar cabecera/líneas vía `BaseTipo`/`BaseEntry`).
- Sembrar filas de `NumeracionDocumento`/`NumeracionDocumentoDet` para los nuevos `CodigoObj` -- el usuario las configura desde la pantalla ya existente.
- Cualquier cambio a tablas existentes -- se verificó que ninguna tiene columnas nuevas.
