# Documentos de compra (Pedido/Entrega/Factura de compra) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir a la API y a la Web los módulos `PedidoCompra`, `EntregaCompra` y `FacturaCompra` (CRUD completo con líneas de detalle y buscador con autocompletado), como clon estructural de los módulos de venta `Pedido`/`Entrega`/`Factura` ya existentes, y hacer que el buscador de Socio de Negocio filtre por tipo (compra→proveedores, venta→clientes).

**Architecture:** API N-capas (.NET 7): Entity → Infraestructure.Repository (repositorio genérico) → Domain.Core → Application.Main → Service.WebApi (controllers), respuestas envueltas en `Respuesta<T>`, mapeo con AutoMapper (`PerfilMapeo`), DI en `Startup.cs`. Web (.NET 8 MVC): `Web.ApiClient` (HttpClients tipados) consumido por controllers de `Web.UI` que renderizan vistas Razor + JS por módulo. Cada módulo de compra es una **transformación mecánica de strings** sobre los archivos del módulo de venta homónimo, con una única adición estructural (FK de impuesto en el detalle) y constantes propias de `TipoObjeto`/`CodigoObj`.

**Tech Stack:** C# / .NET 7 (API) y .NET 8 (Web), Entity Framework Core (SQL Server), AutoMapper, xUnit + Moq (pruebas API), jQuery + DataTables + Bootstrap (Web).

**Spec:** `API/docs/superpowers/specs/2026-08-30-documentos-compra-design.md`

## Global Constraints

- **Repos y ramas:** API en `C:\Users\migue\source\repos\angelm0508\API` (rama `desarrollo`); Web en `C:\Users\migue\source\repos\angelm0508\Web` (rama `main`). Identidad de git ya configurada localmente (`panchoman08`).
- **Build sin chocar con Visual Studio:** compilar a carpeta externa con `-p:BaseOutputPath=` cuando VS pueda estar abierto. Rutas de salida sugeridas: `C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apibuild\` y `...\webbuild\`.
- **No hay .NET 7 SDK instalado**; el SDK 9/10 compila `net7.0` sin problema. No añadir `global.json`.
- **`dotnet test` de la suite completa de la API en verde** antes de dar por terminada cualquier tarea que toque la API. Baseline actual: **506 pruebas, 0 fallos**.
- **Nombres exactos** (verificados contra la base de datos real `API_DB_TEST`):
  - `TipoObjeto` (constraint CHECK, encabezado y detalle): `PedidoCompra`=`"11"`, `EntregaCompra`=`"12"`, `FacturaCompra`=`"13"`.
  - `CodigoObj` en `NumeracionDocumento`/`NumeracionDocumentoDet`: `"11"` / `"12"` / `"13"`.
  - PK: `pk_pedido_compra` / `pk_pedido_compra_det`, `pk_entrega_compra` / `pk_entrega_compra_det`, `pk_factura_compra` / `pk_factura_compra_det`.
  - FK por módulo (`{t}` ∈ `pedido`/`entrega`/`factura`): `fk_{t}_compra_sn`, `fk_{t}_compra_moneda`, `fk_{t}_compra_serie`, `fk_{t}_compra_det_almacen`, `fk_{t}_compra_det_cod_art`, `fk_{t}_compra_det_cod_impuesto`.
  - Columnas `CodigoSN` / `NombreSN` en la tabla (las entidades mantienen las propiedades `CodigoSn` / `NombreSn` con `.HasColumnName("CodigoSN")` / `("NombreSN")`, **igual que en ventas**).
- **Diferencia estructural respecto a ventas:** las tablas `*CompraDetalle` **sí** tienen FK de impuesto (`fk_{t}_compra_det_cod_impuesto` → `Impuesto.Codigo`). Las de venta no. Es la única adición sobre el patrón de ventas.
- **Fuera de alcance:** encadenamiento de documentos (`BaseTipo`/`BaseEntry` quedan inertes, igual que en ventas), efecto en inventario, módulo "Oferta Compra" (`CodigoObj` 10).
- **Numeración ya configurada** por el usuario: `NumeracionDocumento` y `NumeracionDocumentoDet` ya tienen filas para `CodigoObj` 11/12/13 (serie Manual + serie Primaria con `SigNumero=1`). No se siembra nada.

---

## Procedimiento de referencia A — Clonar un módulo de la API

Tasks 4, 6 y 8 aplican **este mismo procedimiento**, cambiando solo la fila de sustituciones del módulo. `{Venta}` es el módulo de venta plantilla; `{Compra}` el módulo de compra a crear; `{t}` el prefijo en minúscula de los nombres de constraint; `{TO}` el valor de `TipoObjeto`.

| Módulo | `{Venta}` | `{Compra}` | `{t}` | `{TO}` = `TipoObjeto` = `CodigoObj` | namespace DTO venta | namespace DTO compra |
|---|---|---|---|---|---|---|
| Task 4 | `Pedido` | `PedidoCompra` | `pedido` | `"11"` | `API.Application.DTO.pedido` | `API.Application.DTO.pedidoCompra` |
| Task 6 | `Entrega` | `EntregaCompra` | `entrega` | `"12"` | `API.Application.DTO.entrega` | `API.Application.DTO.entregaCompra` |
| Task 8 | `Factura` | `FacturaCompra` | `factura` | `"13"` | `API.Application.DTO.factura` | `API.Application.DTO.facturaCompra` |

### A.1 — Archivos a crear (copiar del archivo de venta homónimo y transformar)

| Origen (venta) | Destino (compra) |
|---|---|
| `API.Domain.Entity/Models/{Venta}.cs` | `API.Domain.Entity/Models/{Compra}.cs` |
| `API.Domain.Entity/Models/{Venta}Detalle.cs` | `API.Domain.Entity/Models/{Compra}Detalle.cs` |
| `API.Application.DTO/{venta}/{Venta}DTO.cs` | `API.Application.DTO/{compra}/{Compra}DTO.cs` |
| `API.Application.DTO/{venta}/{Venta}CrearDTO.cs` | `API.Application.DTO/{compra}/{Compra}CrearDTO.cs` |
| `API.Application.DTO/{venta}/{Venta}ActualizarDTO.cs` | `API.Application.DTO/{compra}/{Compra}ActualizarDTO.cs` |
| `API.Application.DTO/{venta}/{Venta}DetalleDTO.cs` | `API.Application.DTO/{compra}/{Compra}DetalleDTO.cs` |
| `API.Application.DTO/{venta}/{Venta}DetalleCrearDTO.cs` | `API.Application.DTO/{compra}/{Compra}DetalleCrearDTO.cs` |
| `API.Application.DTO/{venta}/{Venta}DetalleActualizarDTO.cs` | `API.Application.DTO/{compra}/{Compra}DetalleActualizarDTO.cs` |
| `API.Domain.Interface/I{Venta}Domain.cs` | `API.Domain.Interface/I{Compra}Domain.cs` |
| `API.Domain.Interface/I{Venta}DetalleDomain.cs` | `API.Domain.Interface/I{Compra}DetalleDomain.cs` |
| `API.Domain.Core/{Venta}Domain.cs` | `API.Domain.Core/{Compra}Domain.cs` |
| `API.Domain.Core/{Venta}DetalleDomain.cs` | `API.Domain.Core/{Compra}DetalleDomain.cs` |
| `API.Infraestructure.Repository/{Venta}Repositorio.cs` | `API.Infraestructure.Repository/{Compra}Repositorio.cs` |
| `API.Infraestructure.Repository/{Venta}DetalleRepositorio.cs` | `API.Infraestructure.Repository/{Compra}DetalleRepositorio.cs` |
| `API.Application.Interface/I{Venta}Application.cs` | `API.Application.Interface/I{Compra}Application.cs` |
| `API.Application.Interface/I{Venta}DetalleApplication.cs` | `API.Application.Interface/I{Compra}DetalleApplication.cs` |
| `API.Application.Main/{Venta}Application.cs` | `API.Application.Main/{Compra}Application.cs` |
| `API.Application.Main/{Venta}DetalleApplication.cs` | `API.Application.Main/{Compra}DetalleApplication.cs` |
| `API.Service.WebApi/Controllers/{Venta}Controller.cs` | `API.Service.WebApi/Controllers/{Compra}Controller.cs` |
| `API.Service.WebApi/Controllers/{Venta}DetalleController.cs` | `API.Service.WebApi/Controllers/{Compra}DetalleController.cs` |

### A.2 — Tabla de sustitución de strings (aplicar a TODOS los archivos copiados)

Aplicar en este orden, respetando mayúsculas/minúsculas, en todo el contenido del archivo:

1. `{Venta}Detalle` → `{Compra}Detalle`  (p. ej. `EntregaDetalle` → `EntregaCompraDetalle`)
2. `{Venta}` → `{Compra}`  (p. ej. `Entrega` → `EntregaCompra`) — como el paso 1 ya renombró `...Detalle`, este no lo vuelve a tocar
3. `I{Venta}` → `I{Compra}` (queda cubierto por los pasos 1-2, pero verificar nombres de interfaz)
4. namespace DTO: `namespace API.Application.DTO.{venta}` → `namespace API.Application.DTO.{compra}`; y todos los `using API.Application.DTO.{venta};` → `using API.Application.DTO.{compra};`
5. Rutas de controller: `[Route("api/{Venta}")]` → `[Route("api/{Compra}")]`; `[Route("api/{Venta}Detalle")]` → `[Route("api/{Compra}Detalle")]`
6. Rutas de sub-recurso del detalle: `Por{Venta}` → `Por{Compra}` (atributo `[HttpGet("Por{Venta}/{entry:int}")]` y el nombre del método `ObtenerPor{Venta}` / `ObtenerPor{Venta}Async`)
7. Nombres de método de dominio del detalle: `ObtenerPor{Venta}Async` → `ObtenerPor{Compra}Async` (en `I{Compra}DetalleDomain`, `{Compra}DetalleDomain`, `I{Compra}DetalleApplication`, `{Compra}DetalleApplication`)
8. `DbSet<{Compra}>` / `DbSet<{Compra}Detalle>` — se agregan en el contexto (paso A.4), no vienen de un archivo copiado
9. Constante de `TipoObjeto` en el dominio: en `{Compra}Domain.cs` la constante privada (`private const string TipoObjeto{Venta} = "5";` o análoga) pasa a `private const string TipoObjeto{Compra} = "{TO}";` y todas sus referencias.
10. Mensajes de error orientados al usuario: sustituir la palabra del documento en textos como `"El código de la {venta} no se encontró."`, `"...registrar {venta}s."`, `"La serie está bloqueada y no se puede usar para registrar {venta}s."` por el término de compra correspondiente (`pedido de compra` / `entrega de compra` / `factura de compra`). Es cosmético; si hay duda, dejar el término genérico "documento".

### A.3 — Adición estructural: navegación de impuesto en el detalle

En `{Compra}Detalle.cs`, además de lo que trae el clon, **añadir** la propiedad de navegación (las de venta no la tienen):

```csharp
    public virtual Impuesto? CodigoImpuestoNavigation { get; set; }
```

Colocarla junto a las otras navegaciones (`CodAlmacenNavigation`, `CodArticuloNavigation`).

### A.4 — `ApiDbTestContext.cs`

Archivo: `API.Domain.Entity/Models/ApiDbTestContext.cs`.

**a) DbSets.** Junto a los `DbSet` de venta ya existentes (`public virtual DbSet<Entrega> Entregas { get; set; }` etc.), añadir:

```csharp
    public virtual DbSet<PedidoCompra> PedidoCompras { get; set; }

    public virtual DbSet<PedidoCompraDetalle> PedidoCompraDetalles { get; set; }

    public virtual DbSet<EntregaCompra> EntregaCompras { get; set; }

    public virtual DbSet<EntregaCompraDetalle> EntregaCompraDetalles { get; set; }

    public virtual DbSet<FacturaCompra> FacturaCompras { get; set; }

    public virtual DbSet<FacturaCompraDetalle> FacturaCompraDetalles { get; set; }
```

(En cada Task solo se usa el par del módulo correspondiente; los tres pares pueden añadirse en Task 4 o cada uno en su Task — indistinto, mientras el módulo compile.)

**b) Bloque `OnModelCreating`.** Copiar el bloque `modelBuilder.Entity<{Venta}>(entity => { ... });` y `modelBuilder.Entity<{Venta}Detalle>(entity => { ... });` completos, aplicar la tabla A.2, y además:

- `.HasName("pk_{venta}")` → `.HasName("pk_{t}_compra")`; `.HasName("pk_{venta}_det")` → `.HasName("pk_{t}_compra_det")`.
- `.ToTable("{Venta}")` → `.ToTable("{Compra}")`; `.ToTable("{Venta}Detalle")` → `.ToTable("{Compra}Detalle")`.
- Nombres de constraint FK: `fk_{venta}_sn` → `fk_{t}_compra_sn`, `fk_{venta}_moneda` → `fk_{t}_compra_moneda`, `fk_{venta}_serie` → `fk_{t}_compra_serie`, `fk_{venta}_det_almacen` → `fk_{t}_compra_det_almacen`, `fk_{venta}_det_cod_art` → `fk_{t}_compra_det_cod_art`.
- `TipoObjeto` del encabezado: `.HasDefaultValueSql("('5')")` → `.HasDefaultValueSql("('{TO}')")`.
- `TipoObjeto` del detalle: dejar `.HasDefaultValueSql("('{TO}')")` (el clon de venta puede traer `("((3))")` por copy-paste; corregirlo al valor real `('{TO}')`).
- **Añadir** al final del bloque `modelBuilder.Entity<{Compra}Detalle>(...)`, después de las dos `HasOne` copiadas:

```csharp
            entity.HasOne(d => d.CodigoImpuestoNavigation).WithMany(p => p.{Compra}Detalles)
                .HasForeignKey(d => d.CodigoImpuesto)
                .HasConstraintName("fk_{t}_compra_det_cod_impuesto");
```

### A.5 — Colecciones inversas en entidades relacionadas

Añadir una `ICollection<{Compra}>` / `ICollection<{Compra}Detalle>` junto a las de venta ya existentes:

- `API.Domain.Entity/Models/SocioNegocio.cs`: `public virtual ICollection<{Compra}> {Compra}s { get; set; } = new List<{Compra}>();`
- `API.Domain.Entity/Models/Monedum.cs`: `public virtual ICollection<{Compra}> {Compra}s { get; set; } = new List<{Compra}>();`
- `API.Domain.Entity/Models/NumeracionDocumentoDet.cs`: `public virtual ICollection<{Compra}> {Compra}s { get; set; } = new List<{Compra}>();`
- `API.Domain.Entity/Models/Almacen.cs`: `public virtual ICollection<{Compra}Detalle> {Compra}Detalles { get; set; } = new List<{Compra}Detalle>();`
- `API.Domain.Entity/Models/Articulo.cs`: `public virtual ICollection<{Compra}Detalle> {Compra}Detalles { get; set; } = new List<{Compra}Detalle>();`
- `API.Domain.Entity/Models/Impuesto.cs` (**nuevo** — hoy no tiene colecciones): `public virtual ICollection<{Compra}Detalle> {Compra}Detalles { get; set; } = new List<{Compra}Detalle>();`

`Impuesto.cs` actual es una clase de 3 propiedades sin `using System.Collections.Generic;`. Al añadir la primera colección, dejar el archivo así:

```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class Impuesto
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public decimal? Tasa { get; set; }

    public virtual ICollection<PedidoCompraDetalle> PedidoCompraDetalles { get; set; } = new List<PedidoCompraDetalle>();

    public virtual ICollection<EntregaCompraDetalle> EntregaCompraDetalles { get; set; } = new List<EntregaCompraDetalle>();

    public virtual ICollection<FacturaCompraDetalle> FacturaCompraDetalles { get; set; } = new List<FacturaCompraDetalle>();
}
```

(Las tres colecciones de `Impuesto` pueden agregarse todas en Task 4 o una por Task; mientras compile.)

### A.6 — DI en `Startup.cs`

Archivo: `API.Service.WebApi/Startup.cs`. Junto al bloque de registro de `{Venta}` (busca `IRepositorioGenerico<{Venta}, int>`), añadir:

```csharp
            services.AddTransient<IRepositorioGenerico<{Compra}, int>, {Compra}Repositorio>();
            services.AddTransient<I{Compra}Domain, {Compra}Domain>();
            services.AddTransient<I{Compra}Application, {Compra}Application>();

            services.AddTransient<IRepositorioGenerico<{Compra}Detalle, (int Entry, int NoLinea)>, {Compra}DetalleRepositorio>();
            services.AddTransient<I{Compra}DetalleDomain, {Compra}DetalleDomain>();
            services.AddTransient<I{Compra}DetalleApplication, {Compra}DetalleApplication>();
```

### A.7 — AutoMapper en `PerfilMapeo.cs`

Archivo: `API.Transversal.Mapper/PerfilMapeo.cs`. Añadir el `using API.Application.DTO.{compra};` arriba y, junto a los `CreateMap` de venta:

```csharp
            // {Compra}
            CreateMap<{Compra}, {Compra}DTO>();
            CreateMap<{Compra}CrearDTO, {Compra}>();
            CreateMap<{Compra}ActualizarDTO, {Compra}>();

            // {Compra}Detalle
            CreateMap<{Compra}Detalle, {Compra}DetalleDTO>();
            CreateMap<{Compra}DetalleCrearDTO, {Compra}Detalle>();
            CreateMap<{Compra}DetalleActualizarDTO, {Compra}Detalle>();
```

### A.8 — Pruebas de la API (clonar de las de venta)

| Origen | Destino |
|---|---|
| `API.Service.WebApi.Tests/Controllers/{Venta}ControllerTests.cs` | `.../Controllers/{Compra}ControllerTests.cs` |
| `API.Service.WebApi.Tests/Controllers/{Venta}DetalleControllerTests.cs` | `.../Controllers/{Compra}DetalleControllerTests.cs` |
| `API.Service.WebApi.Tests/Domain/{Venta}DomainTests.cs` | `.../Domain/{Compra}DomainTests.cs` |

Aplicar la tabla A.2. Además, en `{Compra}DomainTests.cs`:
- El helper `SerieAutogenerada(...)` fija `CodigoObj = "5"` → `CodigoObj = "{TO}"`.
- Las aserciones `Assert.Equal("5", obj.TipoObjeto)` → `Assert.Equal("{TO}", obj.TipoObjeto)`.
- El nombre del método `ActualizarAsync_FuerzaTipoObjetoACinco` → `ActualizarAsync_FuerzaTipoObjeto` (o el número que corresponda: `...AOnce` / `...ADoce` / `...ATrece`).

Métodos de prueba esperados (mismos que en venta), todos deben quedar y pasar:

`{Compra}DomainTests`: `InsertarAsync_SerieAutogenerada_AsignaSigNumeroYLoIncrementa`, `InsertarAsync_SerieManual_RespetaNumDocDelCliente`, `InsertarAsync_SerieManualSinNumDoc_Lanza`, `InsertarAsync_SerieBloqueada_Lanza`, `InsertarAsync_SerieAgotada_Lanza`, `InsertarAsync_SerieInexistente_Lanza`, `ActualizarAsync_FuerzaTipoObjeto`.

`{Compra}ControllerTests`: `Obtener_DevuelveBadRequest_CuandoResultadoEsFalso`, `Obtener_DevuelveNotFound_CuandoDatoEsNulo`, `Obtener_DevuelveOk_CuandoExiste`, `ObtenerTodoAsync_DevuelveBadRequest_CuandoResultadoEsFalso`, `ObtenerTodoAsync_DevuelveOk_CuandoResultadoEsExitoso`, `InsertarAsync_DevuelveBadRequest_CuandoResultadoEsFalso`, `InsertarAsync_DevuelveOk_CuandoResultadoEsExitoso`, `ActualizarAsync_DevuelveNotFound_CuandoNoExiste`, `ActualizarAsync_DevuelveBadRequest_CuandoActualizarFalla`, `ActualizarAsync_DevuelveOk_CuandoActualizaCorrectamente`, `EliminarAsync_DevuelveNotFound_CuandoNoExiste`, `EliminarAsync_DevuelveBadRequest_CuandoEliminarFalla`, `EliminarAsync_DevuelveOk_CuandoEliminaCorrectamente`.

`{Compra}DetalleControllerTests`: los 15 métodos análogos (incluye `ObtenerPor{Compra}_DevuelveBadRequest_CuandoResultadoEsFalso` y `ObtenerPor{Compra}_DevuelveOk_CuandoResultadoEsExitoso`).

**Conteo por módulo:** 7 (dominio) + 13 (controller) + 15 (detalle controller) = **35 pruebas nuevas**. Los totales de suite en los "Expected" de abajo son orientativos (baseline 506 → +5 en Task 2 → +35 por cada módulo); el gate duro es **0 fallos** y que las pruebas nuevas del módulo aparezcan y pasen.

---

## Procedimiento de referencia B — Clonar un módulo de la Web

Tasks 5, 7 y 9. `{Ventas}` es el controlador/carpeta de venta en plural (`Pedidos`/`Entregas`/`Facturas`); `{ComprasWeb}` el de compra (`PedidosCompra`/`EntregasCompra`/`FacturasCompra`); `{Compra}` la entidad API (`PedidoCompra`/`EntregaCompra`/`FacturaCompra`); `{compraweb}` el nombre del archivo JS en minúscula (`pedidoscompra`/`entregascompra`/`facturascompra`); `{CO}` el `CodigoObj` (`"11"`/`"12"`/`"13"`).

| Módulo | `{Venta}` (entidad API) | `{Ventas}` | `{Compra}` | `{ComprasWeb}` | `{compraweb}` | `{CO}` |
|---|---|---|---|---|---|---|
| Task 5 | `Pedido` | `Pedidos` | `PedidoCompra` | `PedidosCompra` | `pedidoscompra` | `"11"` |
| Task 7 | `Entrega` | `Entregas` | `EntregaCompra` | `EntregasCompra` | `entregascompra` | `"12"` |
| Task 9 | `Factura` | `Facturas` | `FacturaCompra` | `FacturasCompra` | `facturascompra` | `"13"` |

### B.1 — Archivos a crear

| Origen (venta) | Destino (compra) |
|---|---|
| `Web.ApiClient/Dtos/{Venta}/{Venta}DTO.cs` | `Web.ApiClient/Dtos/{Compra}/{Compra}DTO.cs` |
| `Web.ApiClient/Dtos/{Venta}/{Venta}CrearDTO.cs` | `Web.ApiClient/Dtos/{Compra}/{Compra}CrearDTO.cs` |
| `Web.ApiClient/Dtos/{Venta}/{Venta}ActualizarDTO.cs` | `Web.ApiClient/Dtos/{Compra}/{Compra}ActualizarDTO.cs` |
| `Web.ApiClient/Dtos/{Venta}Detalle/{Venta}DetalleDTO.cs` | `Web.ApiClient/Dtos/{Compra}Detalle/{Compra}DetalleDTO.cs` |
| `Web.ApiClient/Dtos/{Venta}Detalle/{Venta}DetalleCrearDTO.cs` | `Web.ApiClient/Dtos/{Compra}Detalle/{Compra}DetalleCrearDTO.cs` |
| `Web.ApiClient/Dtos/{Venta}Detalle/{Venta}DetalleActualizarDTO.cs` | `Web.ApiClient/Dtos/{Compra}Detalle/{Compra}DetalleActualizarDTO.cs` |
| `Web.ApiClient/Clientes/I{Venta}ApiClient.cs` | `Web.ApiClient/Clientes/I{Compra}ApiClient.cs` |
| `Web.ApiClient/Clientes/{Venta}ApiClient.cs` | `Web.ApiClient/Clientes/{Compra}ApiClient.cs` |
| `Web.ApiClient/Clientes/I{Venta}DetalleApiClient.cs` | `Web.ApiClient/Clientes/I{Compra}DetalleApiClient.cs` |
| `Web.ApiClient/Clientes/{Venta}DetalleApiClient.cs` | `Web.ApiClient/Clientes/{Compra}DetalleApiClient.cs` |
| `Web.UI/Controllers/{Ventas}Controller.cs` | `Web.UI/Controllers/{ComprasWeb}Controller.cs` |
| `Web.UI/Views/{Ventas}/Index.cshtml` | `Web.UI/Views/{ComprasWeb}/Index.cshtml` |
| `Web.UI/Views/{Ventas}/_Form.cshtml` | `Web.UI/Views/{ComprasWeb}/_Form.cshtml` |
| `Web.UI/wwwroot/js/{ventas}.js` | `Web.UI/wwwroot/js/{compraweb}.js` |

### B.2 — Tabla de sustitución de strings (Web)

1. `{Venta}Detalle` → `{Compra}Detalle` (namespaces `Web.ApiClient.Dtos.{Venta}Detalle` → `...{Compra}Detalle`, tipos, `private const string Recurso = "api/{Venta}Detalle"` → `"api/{Compra}Detalle"`)
2. `{Venta}` → `{Compra}` (namespaces `Web.ApiClient.Dtos.{Venta}` → `...{Compra}`, tipos DTO, interfaces `I{Venta}ApiClient` → `I{Compra}ApiClient`, `Recurso = "api/{Venta}"` → `"api/{Compra}"`)
3. En el controller Web: `class {Ventas}Controller` → `class {ComprasWeb}Controller`; `using Web.ApiClient.Dtos.{Venta};` → `...{Compra};`, `using Web.ApiClient.Dtos.{Venta}Detalle;` → `...{Compra}Detalle;`
4. Constante del controller: `private const string CodigoObj{Venta} = "5";` → `private const string CodigoObj{Compra} = "{CO}";` (y `SubTipoDoc{Venta}` → `SubTipoDoc{Compra}`, valor `"--"` sin cambio) y todas sus referencias
5. `TipoObjeto = "5"` en `FormularioCrear` (`new {Venta}CrearDTO { EstadoDoc = "A", TipoObjeto = "5" }`) → `TipoObjeto = "{CO}"`
6. Vistas: `@model Web.ApiClient.Dtos.{Venta}.{Venta}CrearDTO` → `...{Compra}.{Compra}CrearDTO`; `<script src="~/js/{ventas}.js" ...>` → `~/js/{compraweb}.js`; ids/selectores que contengan `{Venta}` (`#tbl{Ventas}`, `#selectSerie{Venta}`, `#datosSeries{Venta}`, `#tblDetalle{Venta}`, `#form{Venta}`, `#btnGuardar{Venta}`, `ViewBag.Series{Venta}`, `#datosSeries{Venta}`) → equivalente con `{Compra}`. Textos visibles ("Nueva entrega" → "Nueva entrega de compra", encabezados de tabla, `ViewData["Title"]`) al término de compra.
7. JS: todas las URLs `'/{Ventas}/...'` → `'/{ComprasWeb}/...'`; selectores `#tbl{Ventas}`, `#selectSerie{Venta}`, `#datosSeries{Venta}`, `#tblDetalle{Venta}`, `#form{Venta}`, `#btnGuardar{Venta}`, funciones `inicializarSerie{Venta}` / `esSerieManual{Venta}` → equivalentes con `{Compra}`; textos de `App.mostrarExito(...)` / `App.confirmarEliminar(...)` al término de compra.

### B.3 — Filtro de proveedor en el buscador de socios (Web)

En `{ComprasWeb}Controller`, la acción `BuscarSocios` (heredada del clon) debe pedir **solo proveedores**. El endpoint del `ISocioNegocioApiClient` ya acepta un `tipo` opcional (Task 3). Dejar la acción así:

```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync("P")
                : await _socios.ObtenerContenganNombreAsync(texto, "P");
            return Json(respuesta);
        }
```

(El resto de acciones `Buscar*` quedan igual que en el clon.)

### B.4 — Registro de HttpClients en `Program.cs`

Archivo: `Web.UI/Program.cs`. Junto a los `AddHttpClient<I{Venta}ApiClient, ...>` de venta, añadir:

```csharp
builder.Services.AddHttpClient<I{Compra}ApiClient, {Compra}ApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<I{Compra}DetalleApiClient, {Compra}DetalleApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

### B.5 — Menú

Archivo: `Web.UI/Views/Shared/_Layout.cshtml`. El submenú "Compras" se crea en Task 5 (primer módulo Web) y en Tasks 7 y 9 solo se añade el enlace nuevo. Ver Task 5 Step 6 para el bloque completo.

---

## Task 1: Corregir `NumeracionDocumento.SerieDfct` de Factura Compra

**Files:**
- Create: `API/sql/2026-08-30-fix-seriedfct-factura-compra.sql`

**Interfaces:**
- Consumes: nada.
- Produces: nada de código. Deja `NumeracionDocumento` con `SerieDfct = 27` para `CodigoObj = '13'` en `API_DB_TEST`.

**Contexto:** `NumeracionDocumento` para `CodigoObj = '13'` (Factura Compra) tiene hoy `SerieDfct = 11`, que es una serie de Factura de **venta** (`CodigoObj = '6'`). Las series reales de `CodigoObj = '13'` son 26 (Manual) y 27 (Primaria). El formulario Web preselecciona `SerieDfct`, así que sin corregir arrancaría con una serie ajena.

- [ ] **Step 1: Crear el script SQL**

Crear `API/sql/2026-08-30-fix-seriedfct-factura-compra.sql` con:

```sql
-- Corrige el SerieDfct de "Factura Compra" (CodigoObj 13): apuntaba a la serie 11,
-- que pertenece a Factura de venta (CodigoObj 6). La serie primaria real de
-- CodigoObj 13 es la 27 (la 26 es la serie Manual).
-- Idempotente: solo actualiza si sigue mal.
UPDATE NumeracionDocumento
SET SerieDfct = 27
WHERE CodigoObj = '13' AND (SerieDfct IS NULL OR SerieDfct <> 27);

SELECT CodigoObj, SerieDfct, DocAlias FROM NumeracionDocumento WHERE CodigoObj = '13';
```

- [ ] **Step 2: Aplicar el script a `API_DB_TEST`**

Run:
```bash
sqlcmd -S localhost -U sa -P '#Integra1' -d API_DB_TEST -C -i "C:/Users/migue/source/repos/angelm0508/API/sql/2026-08-30-fix-seriedfct-factura-compra.sql"
```
Expected: la fila final muestra `13 | 27 | Factura Compra`.

- [ ] **Step 3: Verificar que las series de CodigoObj 11/12/13 siguen configuradas**

Run:
```bash
sqlcmd -S localhost -U sa -P '#Integra1' -d API_DB_TEST -C -W -Q "SELECT CodigoObj, Serie, NombreSerie, SigNumero, Manual, Bloqueado FROM NumeracionDocumentoDet WHERE CodigoObj IN ('11','12','13') ORDER BY CodigoObj, Serie;"
```
Expected: cada `CodigoObj` tiene una fila `Manual='S'` y una `Manual='N'` con `SigNumero=1`.

- [ ] **Step 4: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add sql/2026-08-30-fix-seriedfct-factura-compra.sql
git commit -m "fix(db): corregir SerieDfct de Factura Compra (CodigoObj 13) a la serie 27"
```

---

## Task 2: Filtro `tipo` (C/P) en la búsqueda de Socio de Negocio — API

**Files:**
- Modify: `API/API.Domain.Interface/ISocioNegocioDomain.cs`
- Modify: `API/API.Domain.Core/SocioNegocioDomain.cs`
- Modify: `API/API.Application.Interface/ISocioNegocioApplication.cs`
- Modify: `API/API.Application.Main/SocioNegocioApplication.cs`
- Modify: `API/API.Service.WebApi/Controllers/SocioNegocioController.cs`
- Modify: `API/API.Service.WebApi.Tests/Domain/SocioNegocioDomainTests.cs` (crear si no existe)

**Interfaces:**
- Produces:
  - `ISocioNegocioDomain.ObtenerTodoAsync(string? tipo = null)` → `Task<IQueryable<SocioNegocio>>`
  - `ISocioNegocioDomain.ObtenerContengaNombreAsync(string nombre, string? tipo = null)` → `Task<IEnumerable<SocioNegocio>>`
  - `ISocioNegocioApplication.ObtenerAsync(string? tipo = null)` → `Task<Respuesta<IEnumerable<SocioNegocioDTO>>>`
  - `ISocioNegocioApplication.ObtenerContengaNombreAsync(string nombre, string? tipo = null)` → `Task<Respuesta<IEnumerable<SocioNegocioDTO>>>`
  - `GET api/SocioNegocio?tipo=P` y `GET api/SocioNegocio/ContengaNombre/{nombre}?tipo=P`
- Semántica: si `tipo` es `"C"` o `"P"`, filtra por `SocioNegocio.TipoSn == tipo`; con `null`/vacío/cualquier otro valor, sin filtro (comportamiento actual).

- [ ] **Step 1: Escribir las pruebas de dominio (fallan)**

Crear/editar `API/API.Service.WebApi.Tests/Domain/SocioNegocioDomainTests.cs`. Si el archivo no existe, crearlo completo:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class SocioNegocioDomainTests
    {
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSnMock;
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracionMock;
        private readonly SocioNegocioDomain _domain;

        public SocioNegocioDomainTests()
        {
            _repoSnMock = new Mock<IRepositorioGenerico<SocioNegocio, string>>();
            _repoNumeracionMock = new Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>>();
            _domain = new SocioNegocioDomain(_repoSnMock.Object, _repoNumeracionMock.Object);
        }

        private void SeedSocios() => _repoSnMock
            .Setup(r => r.ObtenerTodoAsync())
            .ReturnsAsync(new[]
            {
                new SocioNegocio { Codigo = "C001", Nombre = "Cliente Uno", TipoSn = "C" },
                new SocioNegocio { Codigo = "C002", Nombre = "Cliente Dos Nube", TipoSn = "C" },
                new SocioNegocio { Codigo = "P001", Nombre = "Proveedor Uno", TipoSn = "P" },
                new SocioNegocio { Codigo = "P002", Nombre = "Proveedor Dos Nube", TipoSn = "P" },
            }.AsAsyncQueryable());

        [Fact]
        public async Task ObtenerContengaNombreAsync_SinTipo_NoFiltra()
        {
            SeedSocios();
            var r = await _domain.ObtenerContengaNombreAsync("Nube");
            Assert.Equal(2, r.Count());
        }

        [Fact]
        public async Task ObtenerContengaNombreAsync_TipoP_SoloProveedores()
        {
            SeedSocios();
            var r = await _domain.ObtenerContengaNombreAsync("Nube", "P");
            Assert.Single(r);
            Assert.Equal("P002", r.First().Codigo);
        }

        [Fact]
        public async Task ObtenerContengaNombreAsync_TipoC_SoloClientes()
        {
            SeedSocios();
            var r = await _domain.ObtenerContengaNombreAsync("Uno", "C");
            Assert.Single(r);
            Assert.Equal("C001", r.First().Codigo);
        }

        [Fact]
        public async Task ObtenerTodoAsync_TipoP_SoloProveedores()
        {
            SeedSocios();
            var q = await _domain.ObtenerTodoAsync("P");
            Assert.Equal(2, q.Count());
            Assert.All(q, s => Assert.Equal("P", s.TipoSn));
        }

        [Fact]
        public async Task ObtenerTodoAsync_SinTipo_DevuelveTodos()
        {
            SeedSocios();
            var q = await _domain.ObtenerTodoAsync();
            Assert.Equal(4, q.Count());
        }
    }
}
```

- [ ] **Step 2: Correr las pruebas y verificar que fallan a compilar**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~SocioNegocioDomainTests" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: error de compilación — `ObtenerContengaNombreAsync` / `ObtenerTodoAsync` no aceptan 2 / 1 argumentos.

- [ ] **Step 3: Modificar `ISocioNegocioDomain.cs`**

Cambiar las dos firmas:

```csharp
        Task<IQueryable<SocioNegocio>> ObtenerTodoAsync(string? tipo = null);
        Task<IEnumerable<SocioNegocio>> ObtenerContengaNombreAsync(string nombre, string? tipo = null);
```

- [ ] **Step 4: Modificar `SocioNegocioDomain.cs`**

Reemplazar los dos métodos (y notar que `InsertarAsync` llama `ObtenerPorCodigoAsync`, no estos, así que no se afecta):

```csharp
        public async Task<IQueryable<SocioNegocio>> ObtenerTodoAsync(string? tipo = null)
        {
            var queryable = await _repoSocioNegocio.ObtenerTodoAsync();
            if (tipo is "C" or "P")
                queryable = queryable.Where(x => x.TipoSn == tipo);
            return queryable;
        }

        public async Task<IEnumerable<SocioNegocio>> ObtenerContengaNombreAsync(string nombre, string? tipo = null)
        {
            var sociosNegocios = await _repoSocioNegocio.ObtenerTodoAsync();
            var filtrado = sociosNegocios.Where(x => x.Nombre.Contains(nombre));
            if (tipo is "C" or "P")
                filtrado = filtrado.Where(x => x.TipoSn == tipo);
            return await filtrado.ToListAsync();
        }
```

- [ ] **Step 5: Modificar `ISocioNegocioApplication.cs`**

```csharp
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerAsync(string? tipo = null);
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContengaNombreAsync(string nombre, string? tipo = null);
```

- [ ] **Step 6: Modificar `SocioNegocioApplication.cs`**

En `ObtenerAsync`: firma `public async Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerAsync(string? tipo = null)` y la llamada `await _socioNegocioDomain.ObtenerTodoAsync(tipo);`.
En `ObtenerContengaNombreAsync`: firma `public async Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContengaNombreAsync(string nombre, string? tipo = null)` y la llamada `await _socioNegocioDomain.ObtenerContengaNombreAsync(nombre, tipo);`.

- [ ] **Step 7: Modificar `SocioNegocioController.cs`**

`ObtenerContengaNombre` y `ObtenerTodo` ganan `[FromQuery] string? tipo`:

```csharp
        [HttpGet("ContengaNombre/{nombre}")]
        public async Task<ActionResult<Respuesta<IEnumerable<SocioNegocioDTO>>>> ObtenerContengaNombre([FromRoute] string nombre, [FromQuery] string? tipo)
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerContengaNombreAsync(nombre, tipo);

            if (!sociosNegocios.Resultado)
                return BadRequest(sociosNegocios);

            return Ok(sociosNegocios);
        }
```

```csharp
        [HttpGet()]
        public async Task<ActionResult<Respuesta<IEnumerable<SocioNegocioDTO>>>> ObtenerTodo([FromQuery] string? tipo)
        {
            var sociosNegocios = await _socioNegocioApplication.ObtenerAsync(tipo);

            if (!sociosNegocios.Resultado)
                return BadRequest(sociosNegocios);

            return Ok(sociosNegocios);
        }
```

- [ ] **Step 8: Correr las pruebas nuevas y que pasen**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~SocioNegocioDomainTests" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 5 passed.

- [ ] **Step 9: Correr TODA la suite de la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~511 passed (506 baseline + 5 nuevas), 0 fallos.

- [ ] **Step 10: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add API.Domain.Interface/ISocioNegocioDomain.cs API.Domain.Core/SocioNegocioDomain.cs API.Application.Interface/ISocioNegocioApplication.cs API.Application.Main/SocioNegocioApplication.cs API.Service.WebApi/Controllers/SocioNegocioController.cs API.Service.WebApi.Tests/Domain/SocioNegocioDomainTests.cs
git commit -m "feat(api): filtro opcional por tipo (C/P) en busqueda y listado de SocioNegocio"
```

---

## Task 3: Filtro `tipo` en `Web.ApiClient` + controladores de venta

**Files:**
- Modify: `Web/Web.ApiClient/Clientes/ISocioNegocioApiClient.cs`
- Modify: `Web/Web.ApiClient/Clientes/SocioNegocioApiClient.cs`
- Modify: `Web/Web.UI/Controllers/CotizacionesController.cs`
- Modify: `Web/Web.UI/Controllers/PedidosController.cs`
- Modify: `Web/Web.UI/Controllers/EntregasController.cs`
- Modify: `Web/Web.UI/Controllers/FacturasController.cs`

**Interfaces:**
- Consumes: endpoint `api/SocioNegocio?tipo=` (Task 2).
- Produces:
  - `ISocioNegocioApiClient.ObtenerTodoAsync(string? tipo = null)`
  - `ISocioNegocioApiClient.ObtenerContenganNombreAsync(string nombre, string? tipo = null)`

- [ ] **Step 1: Modificar `ISocioNegocioApiClient.cs`**

```csharp
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerTodoAsync(string? tipo = null);
        Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContenganNombreAsync(string nombre, string? tipo = null);
```

- [ ] **Step 2: Modificar `SocioNegocioApiClient.cs`**

```csharp
        public Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerTodoAsync(string? tipo = null) =>
            GetAsync<IEnumerable<SocioNegocioDTO>>(tipo is null ? Recurso : $"{Recurso}?tipo={Uri.EscapeDataString(tipo)}");

        public Task<Respuesta<SocioNegocioDTO>> ObtenerAsync(string codigo) =>
            GetAsync<SocioNegocioDTO>($"{Recurso}/{codigo}");

        // ... resto igual ...

        public Task<Respuesta<IEnumerable<SocioNegocioDTO>>> ObtenerContenganNombreAsync(string nombre, string? tipo = null)
        {
            var url = $"{Recurso}/ContengaNombre/{Uri.EscapeDataString(nombre)}";
            if (tipo is not null) url += $"?tipo={Uri.EscapeDataString(tipo)}";
            return GetAsync<IEnumerable<SocioNegocioDTO>>(url);
        }
```

- [ ] **Step 3: Actualizar `BuscarSocios` en los 4 controladores de venta**

En `CotizacionesController.cs`, `PedidosController.cs`, `EntregasController.cs`, `FacturasController.cs`, la acción `BuscarSocios` pasa a filtrar clientes:

```csharp
        [HttpGet]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync("C")
                : await _socios.ObtenerContenganNombreAsync(texto, "C");
            return Json(respuesta);
        }
```

- [ ] **Step 4: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 5: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add Web.ApiClient/Clientes/ISocioNegocioApiClient.cs Web.ApiClient/Clientes/SocioNegocioApiClient.cs Web.UI/Controllers/CotizacionesController.cs Web.UI/Controllers/PedidosController.cs Web.UI/Controllers/EntregasController.cs Web.UI/Controllers/FacturasController.cs
git commit -m "feat(web): documentos de venta filtran SocioNegocio a clientes (tipo C)"
```

---

## Task 4: API completa de `PedidoCompra`

**Files:** ver **Procedimiento de referencia A**, fila Task 4 (`{Venta}`=`Pedido`, `{Compra}`=`PedidoCompra`, `{t}`=`pedido`, `{TO}`=`"11"`).

**Interfaces:**
- Consumes: repositorio genérico `RepositorioGenericoEfCore<TEntity,TKey>`, `Respuesta<T>`, `NumeracionDocumentoDet`, patrón de `PedidoDomain`.
- Produces (para Task 5): `GET/POST/PUT/DELETE api/PedidoCompra`, `.../api/PedidoCompraDetalle` (mismas rutas y forma que `api/Pedido` / `api/PedidoDetalle`); DTOs `PedidoCompraDTO` / `PedidoCompraCrearDTO` (con `NumDoc` opcional) / `PedidoCompraActualizarDTO` y los tres de `PedidoCompraDetalle`, en namespace `API.Application.DTO.pedidoCompra`.

- [ ] **Step 1: Crear entidades + navegación de impuesto**

Aplicar A.1 (filas de entidades), A.2 y A.3 para `PedidoCompra.cs` y `PedidoCompraDetalle.cs`.

- [ ] **Step 2: Colecciones inversas**

Aplicar A.5 para `PedidoCompra` / `PedidoCompraDetalle` en `SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs`, `Almacen.cs`, `Articulo.cs`, `Impuesto.cs`. (En este Task se puede además dejar ya las 3 colecciones de `Impuesto.cs` como en A.5.)

- [ ] **Step 3: Mapear en `ApiDbTestContext.cs`**

Aplicar A.4 (DbSets del par `PedidoCompra`/`PedidoCompraDetalle` + bloque `OnModelCreating` clonado de `Pedido` con `pk_pedido_compra` / `pk_pedido_compra_det`, `fk_pedido_compra_*`, `TipoObjeto` default `('11')`, y la `HasOne(CodigoImpuestoNavigation)` nueva).

- [ ] **Step 4: DTOs**

Aplicar A.1 (filas DTO) + A.2. Verificar: `PedidoCompraCrearDTO.NumDoc` es `int?` (opcional); `PedidoCompraDetalleCrearDTO.Entry` es `[Required] int`.

- [ ] **Step 5: Dominio**

Aplicar A.1 (interfaces + `Domain`) + A.2 + paso A.2.9 (constante `TipoObjetoPedidoCompra = "11"`). `PedidoCompraDomain.InsertarAsync` conserva el chequeo de serie bloqueada (igual que `PedidoDomain`/`EntregaDomain`).

- [ ] **Step 6: Repositorios**

Aplicar A.1 + A.2. `PedidoCompraDetalleRepositorio` mantiene el `override ObtenerAsync` con `FindAsync(id.Entry, id.NoLinea)`.

- [ ] **Step 7: Aplicación**

Aplicar A.1 + A.2 (interfaces + `Application`).

- [ ] **Step 8: Controladores**

Aplicar A.1 + A.2 + pasos A.2.5/A.2.6 (rutas `api/PedidoCompra`, `api/PedidoCompraDetalle`, `PorPedidoCompra`).

- [ ] **Step 9: DI**

Aplicar A.6 para `PedidoCompra`.

- [ ] **Step 10: AutoMapper**

Aplicar A.7 para `PedidoCompra` (`using API.Application.DTO.pedidoCompra;` + 6 `CreateMap`).

- [ ] **Step 11: Compilar la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores` (warnings CS860x preexistentes OK).

- [ ] **Step 12: Clonar y adaptar las pruebas**

Aplicar A.8 para `PedidoCompra`: crear `PedidoCompraControllerTests.cs`, `PedidoCompraDetalleControllerTests.cs`, `PedidoCompraDomainTests.cs`. En `PedidoCompraDomainTests`: helper con `CodigoObj = "11"`, aserciones `Assert.Equal("11", obj.TipoObjeto)`, método `ActualizarAsync_FuerzaTipoObjeto`.

- [ ] **Step 13: Correr las pruebas del módulo**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~PedidoCompra" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 35 passed (7 dominio + 13 controller + 15 detalle controller), 0 fallos.

- [ ] **Step 14: Correr TODA la suite de la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~546 passed (511 tras Task 2 + 35), 0 fallos.

- [ ] **Step 15: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add -A
git commit -m "feat(api): modulo PedidoCompra (entidades, dominio, aplicacion, controllers, pruebas)"
```

---

## Task 5: Web de `PedidoCompra` (+ submenú "Compras")

**Files:** ver **Procedimiento de referencia B**, fila Task 5. Además `Web.UI/Views/Shared/_Layout.cshtml` (Step 6).

**Interfaces:**
- Consumes: `api/PedidoCompra`, `api/PedidoCompraDetalle` (Task 4); `ISocioNegocioApiClient.ObtenerTodoAsync(tipo)` / `ObtenerContenganNombreAsync(nombre, tipo)` (Task 3).
- Produces: rutas Web `/PedidosCompra` (`Index`, `ObtenerTodos`, `FormularioCrear`, `FormularioEditar`, `Crear`, `Editar`, `Eliminar`, `ObtenerDetalle`, `CrearLinea`, `EditarLinea`, `EliminarLinea`, `BuscarSocios`, `BuscarArticulos`, `BuscarAlmacenes`, `BuscarImpuestos`, `ObtenerAlmacenPorCodigo`, `ObtenerImpuestoPorCodigo`).

- [ ] **Step 1: DTOs de `PedidoCompra` en `Web.ApiClient`**

Aplicar B.1 (filas DTO) + B.2 (pasos 1-2). 6 archivos en `Web.ApiClient/Dtos/PedidoCompra/` y `Web.ApiClient/Dtos/PedidoCompraDetalle/`.

- [ ] **Step 2: Clientes HTTP**

Aplicar B.1 (filas clientes) + B.2. 4 archivos en `Web.ApiClient/Clientes/`.

- [ ] **Step 3: Registrar HttpClients**

Aplicar B.4 para `PedidoCompra` en `Web.UI/Program.cs`.

- [ ] **Step 4: Controlador Web**

Aplicar B.1 (fila controller) + B.2 (pasos 3-5) → `Web.UI/Controllers/PedidosCompraController.cs` con `CodigoObjPedidoCompra = "11"`, `SubTipoDocPedidoCompra = "--"`, `TipoObjeto = "11"` en `FormularioCrear`. Aplicar B.3 (BuscarSocios con `"P"`).

- [ ] **Step 5: Vistas + JS**

Aplicar B.1 (filas vistas/js) + B.2 (pasos 6-7): `Web.UI/Views/PedidosCompra/Index.cshtml`, `Web.UI/Views/PedidosCompra/_Form.cshtml`, `Web.UI/wwwroot/js/pedidoscompra.js`. Textos visibles → "pedido de compra".

- [ ] **Step 6: Submenú "Compras" en `_Layout.cshtml`**

En `Web.UI/Views/Shared/_Layout.cshtml`:

a) En el bloque `@{ ... }` de arriba, tras la línea `bool EsActivoVentas = ...`, añadir:

```csharp
    bool EsActivoCompras = new[] { "PedidosCompra", "EntregasCompra", "FacturasCompra" }.Any(EsActivo);
```

b) Justo **después** del `</div>` que cierra `id="submenuVentas"`, añadir:

```html
                    <a class="nav-link nav-link-toggle @(EsActivoCompras ? "active" : "")" data-bs-toggle="collapse" href="#submenuCompras" role="button" aria-expanded="@(EsActivoCompras ? "true" : "false")" aria-controls="submenuCompras">
                        <i class="fa-solid fa-cart-flatbed"></i><span>Compras</span>
                        <i class="fa-solid fa-chevron-down ms-auto submenu-caret"></i>
                    </a>
                    <div class="collapse @(EsActivoCompras ? "show" : "")" id="submenuCompras">
                        <a class="nav-link nav-sublink @(EsActivo("PedidosCompra") ? "active" : "")" asp-controller="PedidosCompra" asp-action="Index">
                            <i class="fa-solid fa-cart-arrow-down"></i><span>Pedidos de compra</span>
                        </a>
                        <a class="nav-link nav-sublink @(EsActivo("EntregasCompra") ? "active" : "")" asp-controller="EntregasCompra" asp-action="Index">
                            <i class="fa-solid fa-truck-ramp-box"></i><span>Entregas de compra</span>
                        </a>
                        <a class="nav-link nav-sublink @(EsActivo("FacturasCompra") ? "active" : "")" asp-controller="FacturasCompra" asp-action="Index">
                            <i class="fa-solid fa-file-invoice-dollar"></i><span>Facturas de compra</span>
                        </a>
                    </div>
```

(Los enlaces a `EntregasCompra` y `FacturasCompra` apuntan a controladores que aún no existen; ASP.NET los renderiza igual y darán 404 hasta Tasks 7 y 9. Aceptable.)

- [ ] **Step 7: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add -A
git commit -m "feat(web): pantalla PedidosCompra (CRUD + detalle + autocompletado) y submenu Compras"
```

---

## Task 6: API completa de `EntregaCompra`

Idéntico a **Task 4** aplicando **Procedimiento de referencia A**, fila Task 6 (`{Venta}`=`Entrega`, `{Compra}`=`EntregaCompra`, `{t}`=`entrega`, `{TO}`=`"12"`, namespace `API.Application.DTO.entregaCompra`).

- [ ] **Step 1:** Entidades + navegación de impuesto (A.1/A.2/A.3) → `EntregaCompra.cs`, `EntregaCompraDetalle.cs`.
- [ ] **Step 2:** Colecciones inversas (A.5) para `EntregaCompra`/`EntregaCompraDetalle`.
- [ ] **Step 3:** `ApiDbTestContext.cs` (A.4): DbSets + bloque de `Entrega` clonado con `pk_entrega_compra`/`pk_entrega_compra_det`, `fk_entrega_compra_*`, `TipoObjeto` default `('12')`, `HasOne(CodigoImpuestoNavigation)` → `fk_entrega_compra_det_cod_impuesto`.
- [ ] **Step 4:** DTOs (A.1/A.2) en `API.Application.DTO.entregaCompra`.
- [ ] **Step 5:** Dominio (A.1/A.2 + constante `TipoObjetoEntregaCompra = "12"`).
- [ ] **Step 6:** Repositorios (A.1/A.2).
- [ ] **Step 7:** Aplicación (A.1/A.2).
- [ ] **Step 8:** Controladores (A.1/A.2 + rutas `api/EntregaCompra`, `api/EntregaCompraDetalle`, `PorEntregaCompra`).
- [ ] **Step 9:** DI (A.6) para `EntregaCompra`.
- [ ] **Step 10:** AutoMapper (A.7) para `EntregaCompra`.
- [ ] **Step 11: Compilar la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 12:** Clonar pruebas (A.8) para `EntregaCompra` (`CodigoObj = "12"`, `Assert.Equal("12", ...)`).
- [ ] **Step 13: Pruebas del módulo**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~EntregaCompra" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 35 passed.

- [ ] **Step 14: Suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~581 passed (546 + 35), 0 fallos.

- [ ] **Step 15: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add -A
git commit -m "feat(api): modulo EntregaCompra (entidades, dominio, aplicacion, controllers, pruebas)"
```

---

## Task 7: Web de `EntregaCompra`

Idéntico a **Task 5** (sin re-crear el submenú) aplicando **Procedimiento de referencia B**, fila Task 7 (`{Venta}`=`Entrega`, `{Ventas}`=`Entregas`, `{Compra}`=`EntregaCompra`, `{ComprasWeb}`=`EntregasCompra`, `{compraweb}`=`entregascompra`, `{CO}`=`"12"`).

- [ ] **Step 1:** DTOs `EntregaCompra` / `EntregaCompraDetalle` en `Web.ApiClient` (B.1/B.2).
- [ ] **Step 2:** Clientes HTTP (B.1/B.2).
- [ ] **Step 3:** Registrar HttpClients (B.4) para `EntregaCompra`.
- [ ] **Step 4:** `EntregasCompraController.cs` (B.1/B.2 + `CodigoObjEntregaCompra = "12"` + B.3 BuscarSocios `"P"`).
- [ ] **Step 5:** `Views/EntregasCompra/Index.cshtml`, `_Form.cshtml`, `wwwroot/js/entregascompra.js` (B.1/B.2). Textos → "entrega de compra".
- [ ] **Step 6:** El submenú "Compras" ya existe (Task 5); el enlace a `EntregasCompra` ya está. Nada que hacer aquí salvo confirmar que existe.
- [ ] **Step 7: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add -A
git commit -m "feat(web): pantalla EntregasCompra (CRUD + detalle + autocompletado)"
```

---

## Task 8: API completa de `FacturaCompra`

Idéntico a **Task 4** aplicando **Procedimiento de referencia A**, fila Task 8 (`{Venta}`=`Factura`, `{Compra}`=`FacturaCompra`, `{t}`=`factura`, `{TO}`=`"13"`, namespace `API.Application.DTO.facturaCompra`).

- [ ] **Step 1:** Entidades + navegación de impuesto → `FacturaCompra.cs`, `FacturaCompraDetalle.cs`.
- [ ] **Step 2:** Colecciones inversas (A.5).
- [ ] **Step 3:** `ApiDbTestContext.cs` (A.4): `pk_factura_compra`/`pk_factura_compra_det`, `fk_factura_compra_*`, `TipoObjeto` default `('13')`, `HasOne(CodigoImpuestoNavigation)` → `fk_factura_compra_det_cod_impuesto`.
- [ ] **Step 4:** DTOs en `API.Application.DTO.facturaCompra`.
- [ ] **Step 5:** Dominio (constante `TipoObjetoFacturaCompra = "13"`).
- [ ] **Step 6:** Repositorios.
- [ ] **Step 7:** Aplicación.
- [ ] **Step 8:** Controladores (`api/FacturaCompra`, `api/FacturaCompraDetalle`, `PorFacturaCompra`).
- [ ] **Step 9:** DI (A.6).
- [ ] **Step 10:** AutoMapper (A.7).
- [ ] **Step 11: Compilar la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 12:** Clonar pruebas (A.8) para `FacturaCompra` (`CodigoObj = "13"`, `Assert.Equal("13", ...)`).
- [ ] **Step 13: Pruebas del módulo**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln --filter "FullyQualifiedName~FacturaCompra" -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: 35 passed.

- [ ] **Step 14: Suite completa**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~616 passed (581 + 35), 0 fallos.

- [ ] **Step 15: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add -A
git commit -m "feat(api): modulo FacturaCompra (entidades, dominio, aplicacion, controllers, pruebas)"
```

---

## Task 9: Web de `FacturaCompra`

Idéntico a **Task 7** aplicando **Procedimiento de referencia B**, fila Task 9 (`{Venta}`=`Factura`, `{Ventas}`=`Facturas`, `{Compra}`=`FacturaCompra`, `{ComprasWeb}`=`FacturasCompra`, `{compraweb}`=`facturascompra`, `{CO}`=`"13"`).

- [ ] **Step 1:** DTOs (B.1/B.2).
- [ ] **Step 2:** Clientes HTTP (B.1/B.2).
- [ ] **Step 3:** Registrar HttpClients (B.4).
- [ ] **Step 4:** `FacturasCompraController.cs` (`CodigoObjFacturaCompra = "13"` + BuscarSocios `"P"`).
- [ ] **Step 5:** `Views/FacturasCompra/Index.cshtml`, `_Form.cshtml`, `wwwroot/js/facturascompra.js`. Textos → "factura de compra".
- [ ] **Step 6:** Enlace a `FacturasCompra` ya presente en el submenú "Compras" (Task 5). Confirmar.
- [ ] **Step 7: Compilar Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 8: Commit**

```bash
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add -A
git commit -m "feat(web): pantalla FacturasCompra (CRUD + detalle + autocompletado)"
```

---

## Task 10: Verificación final conjunta

**Files:** ninguno nuevo (posible ajuste menor si algo falla).

- [ ] **Step 1: Build completo de la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet build API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apibuild/"
```
Expected: `0 Errores`.

- [ ] **Step 2: Suite completa de la API**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/API" && dotnet test API.sln -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/apitest/"
```
Expected: ~616 passed, 0 fallos (el número exacto no importa; 0 fallos sí).

- [ ] **Step 3: Build completo de la Web**

Run:
```bash
cd "C:/Users/migue/source/repos/angelm0508/Web" && dotnet build Web.slnx -p:BaseOutputPath="C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/webbuild/"
```
Expected: `0 Errores`.

- [ ] **Step 4: Prueba manual en el navegador**

Levantar API (`API.Service.WebApi`) y Web (`Web.UI`). Con la sesión iniciada:
1. Menú → "Compras" aparece con 3 enlaces.
2. **Pedidos de compra** → "Nuevo": la serie por defecto (Primaria de CodigoObj 11) viene preseleccionada; el buscador "Socio de negocio" al teclear muestra **solo proveedores** (`TipoSN='P'`); agregar 1-2 líneas (artículo/almacén/impuesto por autocompletado); Guardar → aparece con su No. Documento en la lista.
3. Editar ese pedido de compra: cambiar comentario, agregar y quitar una línea. Eliminar el pedido de compra.
4. Repetir 2-3 para **Entregas de compra** (CodigoObj 12) y **Facturas de compra** (CodigoObj 13).
5. **Regresión ventas:** abrir "Ventas" → "Cotizaciones" → "Nuevo": el buscador "Socio de negocio" muestra **solo clientes** (`TipoSN='C'`). Crear y borrar una cotización de prueba.

- [ ] **Step 5: Recordatorio para el usuario**

Imprimir para el usuario:
- Reiniciar las sesiones de depuración de Visual Studio (API y Web.UI) — cambió código.
- La numeración de los tres documentos de compra ya estaba configurada; no hace falta tocar la pantalla "Numeración de documentos".
- Sub-proyecto B+C (inventario multi-almacén + asiento en EntregaCompra/FacturaCompra) es la siguiente conversación.

- [ ] **Step 6: Commit final (si quedó algo suelto, p. ej. el plan con checkboxes)**

```bash
cd "C:/Users/migue/source/repos/angelm0508/API"
git add -A && git commit -m "chore: marcar plan de documentos de compra como completado" || echo "nada que commitear"
cd "C:/Users/migue/source/repos/angelm0508/Web"
git add -A && git commit -m "chore: cierre sub-proyecto documentos de compra" || echo "nada que commitear"
```

---

## Notas de auto-revisión (cobertura del spec)

- **API completa + Web CRUD de los 3 documentos** → Tasks 4-9.
- **`TipoObjeto` 11/12/13 forzado en servidor** → A.2.9 + `{Compra}DomainTests`.
- **FK de impuesto en el detalle (diferencia con ventas)** → A.3, A.4, A.5.
- **Filtro `TipoSN`: compra=P, venta=C** → Task 2 (API), Task 3 (Web.ApiClient + 4 controladores de venta), B.3 (controladores de compra).
- **Nombres `PedidoCompra`/`EntregaCompra`/`FacturaCompra`, rutas `api/...`, Web `PedidosCompra`/...** → tablas de referencia A y B.
- **Corrección `SerieDfct`** → Task 1.
- **Submenú "Compras"** → Task 5 Step 6.
- **Numeración ya configurada, no sembrar** → Global Constraints + Task 1 Step 3 (solo verificación).
- **Encadenamiento / inventario fuera de alcance** → Global Constraints; los campos `BaseTipo`/`BaseEntry` se copian tal cual del clon de venta, inertes.
- **Verificación: build ambas soluciones + test API verde + prueba manual + regresión ventas** → Task 10.
