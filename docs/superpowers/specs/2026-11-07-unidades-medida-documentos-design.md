# Unidades de medida en las líneas de los documentos - Diseño

**Fecha:** 2026-11-07
**Repos:** `angelm0508/API` (.NET 7, rama `desarrollo`) y `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Referencia:** SAP B1, campos «Unidad de medida» (`UomCode`/`UomEntry`), «Cantidad en UM» (`UnitMsr`, `NumPerMsr`) y «Cantidad en unidad de inventario» (`InvQty`) de las líneas de documentos de marketing.
**Estado:** borrador pendiente de aprobación.

## Objetivo

Que cada línea de un documento use una unidad de medida del grupo del artículo. La unidad predeterminada es la **unidad base** del grupo; el usuario puede cambiarla por otra unidad del mismo grupo. El inventario (existencias, kardex y costo promedio) **siempre se maneja en unidad base**: el asiento convierte la cantidad de la línea.

### Decisiones tomadas

1. **Alcance:** todos los documentos con líneas de artículo: cotización, pedido, entrega, factura y nota de crédito/débito de venta; pedido, entrega, entrada, factura y nota de crédito/débito de compra; entrada y salida de mercancías; orden de servicio.
2. **Precio:** al cambiar de unidad, el precio de la línea se recalcula como `precio por unidad base × factor` y el usuario puede editarlo.
3. **Documentos existentes:** una migración asigna a cada línea la unidad base de su artículo con factor 1; no cambian cantidades ni stock.
4. **Guardado:** cada línea guarda la unidad elegida, el factor y la cantidad en unidad base. Stock, costo y kardex solo en unidad base.

### Fuera de alcance

Precios por unidad en la lista de precios (SAP `UoM Price List`); unidades distintas por almacén; «Manual» (grupo -1) como grupo con conversión libre; reportes en una unidad distinta de la base; códigos de barras por unidad.

## Contexto verificado

- Ya existen `UnidadMedidaArticulo`, `GrupoUnidadMedidaArticulo` (con `BaseMedida`) y `GrupoUnidadMedidaDetArticulo(GrpMedidaEntry, MedidaEntry, CantAlternativa, CantBase, NumLinea, Activo)`. Los CRUD de estas tablas ya están en API y Web.
- `Articulo.CodigoGrpUnidadMedida` apunta al grupo. El grupo `-1 Manual` es un registro semilla bloqueado.
- Ninguna tabla `*Detalle` tiene unidad: solo `Cantidad`, `Precio`, etc.
- `MovimientoRequest(TipoDoc, DocEntry, DocLinea, CodArticulo, CodAlmacen, Cantidad, PrecioUnitario, Fecha)` es el único punto de entrada del inventario. Hoy se asume que `Cantidad` y `PrecioUnitario` están en la unidad de inventario.

## Modelo de conversión (como SAP B1)

En cada fila del grupo, `CantAlternativa` unidades alternativas equivalen a `CantBase` unidades base. Para una unidad `u`:

```
factor(u) = CantBase / CantAlternativa      (unidades base por 1 unidad u)
CantidadBase = Cantidad × factor
PrecioBase   = Precio ÷ factor
```

La unidad base del grupo tiene factor 1. Ejemplo: grupo «Caja 12»: 1 Caja = 12 Unidad. Vender 3 Cajas a Q120 → `CantidadBase` 36 y `PrecioBase` Q10.

## 1 - Datos

Script nuevo `sql/2026-11-07-unidades-medida-documentos.sql` (idempotente) y el mismo bloque al final de `crear-api-db-test-completo.sql` y `crear-api-db-completo.sql`.

A cada tabla de detalle de los 14 documentos se agregan:

| Columna | Tipo | Regla |
|---|---|---|
| `UomEntry` | `INT NULL` | FK a `UnidadMedidaArticulo`. Nulo solo en líneas sin artículo (p. ej. servicio sin grupo). |
| `FactorUom` | `DECIMAL(19,6) NOT NULL DEFAULT 1` | Unidades base por unidad de la línea. `CHECK (FactorUom > 0)` con `EXEC('...')`. |
| `CantidadBase` | `DECIMAL(19,6) NULL` | `Cantidad × FactorUom`; la fija el servidor. |

Migración: `UomEntry` = `BaseMedida` del grupo del artículo, `FactorUom` = 1, `CantidadBase` = `Cantidad`.

## 2 - Dominio

- Nuevo servicio puro `UnidadMedidaService` en `Domain.Core` (interfaz en `Domain.Interface`, sin parámetros opcionales):
  - `ResolverAsync(codArticulo, uomEntry?)` devuelve `UnidadResuelta(UomEntry, Codigo, Factor)`. Sin `uomEntry` usa la base del grupo.
  - Rechaza una unidad que no pertenezca al grupo del artículo, o que esté inactiva en el grupo, con mensajes literales en español («La unidad X no pertenece al grupo de medida del artículo Y»).
  - Artículo sin grupo o con grupo Manual: unidad nula y factor 1 (comportamiento actual).
- Cada dominio de documento, antes de numerar y de asentar, resuelve la unidad de cada línea y fija `FactorUom` y `CantidadBase` en el servidor; ignora los valores que mande el cliente.
- `InventarioAsientoService` no cambia: los dominios le pasan `CantidadBase` y `PrecioBase`. Así valuación, kardex y bloqueo por almacén siguen igual.
- **Reversas** (cancelar, quitar línea): ya usan los movimientos originales en base, por lo que no necesitan conversión.
- **Documentos base** (`DocumentoBaseService`, «Copiar a»): la línea nueva hereda unidad y factor de la base; al copiar parcialmente, la cantidad pendiente se calcula en `CantidadBase` y se muestra en la unidad de la línea.
- Totales, impuestos y descuentos siguen calculándose con `Cantidad × Precio` de la línea (la unidad solo cambia la escala de los dos).
- Cambiar la unidad de una línea en un documento ya asentado revierte y reasienta como cualquier cambio de cantidad.

## 3 - API

- DTOs de línea de los 14 documentos: `UomEntry` (opcional al crear/actualizar), y `UomCodigo`, `FactorUom`, `CantidadBase` de solo lectura en la respuesta.
- Nuevo `GET api/Articulo/{codigo}/Unidades`: unidad base, y lista de unidades activas del grupo con `UomEntry`, código, nombre y factor. Permiso `Articulos` / Ver.
- Mapeos en `PerfilMapeo.cs`: `UomCodigo` se obtiene por navegación; `FactorUom` y `CantidadBase` se ignoran al mapear desde el DTO de entrada.
- Errores de unidad: 400 con el mensaje literal.

## 4 - Web

- Cada formulario de líneas (`_Form` y `Crear`) agrega la columna «UM», un select que se llena con `GET Articulo/{codigo}/Unidades` al elegir el artículo y queda en la base por defecto.
- Al cambiar la unidad: el precio se recalcula (`precioBase × factor`) y la línea muestra «Cant. base» informativa. El usuario puede editar el precio.
- Listados y detalles de documentos muestran la UM y, si no es la base, la cantidad base.
- Helper común en `site.js` (`App.unidadesArticulo`) para no repetirlo en cada módulo.
- Escapar el texto dinámico al armar los selects.

## 5 - Pruebas

- Unitarias de `UnidadMedidaService`: base por defecto, unidad ajena al grupo, unidad inactiva, grupo Manual, factor fraccionario.
- Por documento: la factura con unidad alternativa descuenta `CantidadBase`; la entrada de compra aumenta en base; el costo promedio se calcula con `PrecioBase`; reversa; edición de unidad; «Copiar a» con cantidad parcial.
- Regresión: la suite existente (2713) debe seguir en verde sin cambios, porque la unidad nula equivale a la base.
- Prueba de humo en base temporal `API_DB_EVID<n>` y recorrido en navegador.

## 6 - Riesgos y puntos abiertos

- Redondeos: `CantAlternativa` y `CantBase` son `DECIMAL(19)` (sin decimales) en la tabla actual; los factores fraccionarios (p. ej. 1 Docena = 12) funcionan, pero 1 Libra = 0.4536 Kg no cabe. Propongo ampliar ambas columnas a `DECIMAL(21,6)` en este mismo script.
- Cambiar el grupo de un artículo con movimientos: SAP lo bloquea. Propongo rechazarlo si el artículo tiene líneas o movimientos.
- Alcance grande (14 documentos): se implementa por fases, comenzando por el servicio y la factura de venta y luego el resto, con commit por documento.
