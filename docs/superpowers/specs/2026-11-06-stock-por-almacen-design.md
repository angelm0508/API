# Stock mínimo, máximo y bloqueo por artículo y almacén — Diseño

**Fecha:** 2026-11-06
**Repos:** `angelm0508/API` (.NET 7, N-capas, rama `desarrollo`) y `angelm0508/Web` (.NET 8 MVC, rama `main`)
**Referencia:** pestaña «Datos de inventario» de la ficha de artículos de SAP B1 (tabla por almacén con Bloqueado, En stock, Comprometido, Pedido, Disponible, Stock mínimo, Stock máximo y Nivel de stock necesario).
**Documentos relacionados:** `2026-08-30-inventario-nucleo-design.md` (existencias por artículo y almacén, kardex y asiento de inventario).

## Objetivo

Que cada artículo pueda tener, **por almacén**, un stock mínimo, un stock máximo y una marca de **bloqueo**, y que la ficha del artículo muestre la tabla por almacén como SAP B1. El bloqueo se hace cumplir en el servidor: un artículo bloqueado en un almacén no puede entrar ni salir de él.

### Decisiones tomadas (brainstorming)

1. **El bloqueo rechaza entradas y salidas.** Un artículo marcado como bloqueado en un almacén no puede usarse en ese almacén en ningún documento que mueva stock (entregas y facturas de venta y de compra, entrada y salida de mercancías). Las **reversas** por cancelación o por quitar una línea nunca se bloquean, para que ningún documento quede imposible de cancelar.
2. **Mínimo y máximo generales se conservan.** `Articulo.Minimo` y `Articulo.Maximo` (total del artículo; los usan el aviso del Home y el de la ficha) no cambian. Los de cada almacén son adicionales.
3. **Mínimo y máximo solo avisan.** Nunca bloquean una entrada o salida. Marcan la fila, alimentan el aviso de stock bajo del Home y calculan el «Nivel de stock necesario».
4. **La tabla lista todos los almacenes activos**, tenga o no existencias el artículo, más los almacenes inactivos que tengan configuración o existencias del artículo.

### Fuera de alcance

Que el `Almacen.Bloqueado` global impida movimientos (hoy solo impide editar el almacén; no se toca); sugerencias automáticas de compra; notificaciones; carga masiva; mantener `Comprometido` y `Pedido` por almacén (ver Contexto); bloquear documentos que no mueven stock (cotización, pedido, orden de servicio).

## Contexto verificado

- `ExistenciaArticulo(CodArticulo, CodAlmacen, Disponible, Comprometido, Pedido, FechaActualizacion, RowVersion)` ya guarda existencias por artículo y almacén. **`Disponible` es la cantidad en stock** (en SAP, «En stock»). Solo `InventarioAsientoService` la escribe y usa `RowVersion` para concurrencia.
- **`Comprometido` y `Pedido` de `ExistenciaArticulo` no los actualiza ningún documento hoy** (verificado: no hay escrituras en el dominio). La tabla de la ficha los mostrará, pero valdrán 0 hasta que una fase futura los mantenga. No se cambia aquí.
- `Articulo` ya tiene `Minimo`, `Maximo` y `GestPorAlmacen` (generales, sin uso por almacén); el Home y la ficha avisan cuando `CantDisponible < Minimo`.
- `Almacen` tiene `Bloqueado` global; `AlmacenDomain` solo lo usa para impedir editar el almacén.
- `InventarioAsientoService.AplicarMovimientoAsync` es el único punto por el que pasan todos los movimientos de stock (alta de documento y reversa). Las reversas entran por `RevertirInternoAsync` con `movReversaDe != null`.
- Los permisos de la API son atributos `RequierePermiso(módulo, acción)`; `Articulos` ya tiene Ver y Actualizar.
- La Web arma la ficha en `Views/Articulos/_Form.cshtml` (edición) y `Crear.cshtml` (alta), con `ArticulosController` y `wwwroot/js/articulos.js`; los selects ya no tienen opción «-- Seleccione --».

## §1 — Datos

Script nuevo `sql/2026-11-06-stock-por-almacen.sql` (idempotente) y el mismo bloque en `sql/crear-api-db-test-completo.sql` y `sql/crear-api-db-completo.sql`.

Tabla `ArticuloAlmacen`:

| Columna | Tipo | Regla |
|---|---|---|
| `CodArticulo` | `nvarchar(15)` NOT NULL | FK a `Articulo(Codigo)` |
| `CodAlmacen` | `nvarchar(8)` NOT NULL | FK a `Almacen(Codigo)` |
| `Minimo` | `decimal(19,6)` NULL | `>= 0` |
| `Maximo` | `decimal(19,6)` NULL | `>= 0` |
| `Bloqueado` | `nvarchar(1)` NOT NULL | `DEFAULT 'N'`, `CHECK IN ('S','N')` |

Clave primaria `(CodArticulo, CodAlmacen)`. `CHECK (Minimo IS NULL OR Maximo IS NULL OR Maximo >= Minimo)`.

- Solo se guarda una fila si hay algo que configurar. Una fila sin mínimo, sin máximo y sin bloqueo se elimina.
- No se migra nada: los artículos existentes quedan sin configuración por almacén. `Articulo.Minimo`/`Maximo` no cambian.
- La tabla es independiente de `ExistenciaArticulo` a propósito: los movimientos escriben `ExistenciaArticulo` con control de concurrencia, y editar la configuración no debe chocar con ellos ni obligar a crear filas de existencias vacías.

## §2 — API

Entidad `ArticuloAlmacen` (`API.Domain.Entity`), repositorio genérico con clave `(CodArticulo, CodAlmacen)`, `IArticuloAlmacenDomain` en `API.Domain.Core`, aplicación en `API.Application.Main`, DTOs en `API.Application.DTO`, endpoints en el controlador de artículos.

**`GET api/Articulo/{codigo}/Almacenes`** (permiso `Articulos` / Ver). Devuelve una fila por almacén activo, más los inactivos con configuración o existencias de ese artículo. Cada fila:

| Campo | Cálculo |
|---|---|
| `codAlmacen`, `nombreAlmacen` | del almacén |
| `bloqueado` | `ArticuloAlmacen.Bloqueado == 'S'` |
| `enStock` | `ExistenciaArticulo.Disponible` (0 si no hay fila) |
| `comprometido`, `pedido` | de `ExistenciaArticulo` (0 si no hay fila) |
| `disponible` | `enStock − comprometido + pedido` (fórmula de SAP B1) |
| `minimo`, `maximo` | de `ArticuloAlmacen` (nulos si no hay fila) |
| `nivelNecesario` | si hay máximo: `max(0, maximo − disponible)`; si no, nulo |
| `estado` | `BAJO_MINIMO` si hay mínimo > 0 y `enStock < minimo`; `SOBRE_MAXIMO` si hay máximo y `enStock > maximo`; si no, `OK` |

Artículo inexistente → 404. Las filas salen ordenadas por código de almacén.

**`PUT api/Articulo/{codigo}/Almacenes`** (permiso `Articulos` / Actualizar). Recibe `[{ codAlmacen, minimo, maximo, bloqueado }]` y **reemplaza** la configuración del artículo en una sola operación de guardado: crea o actualiza las filas con algo configurado y elimina las que quedan sin nada. Validaciones (todas devuelven 400 con mensaje claro y no guardan nada):
- almacén inexistente o repetido en la lista;
- `minimo` o `maximo` negativos o con más de 6 decimales;
- `maximo < minimo` cuando ambos existen;
- `bloqueado` distinto de `S`/`N` (vacío = `N`);
- artículo inexistente → 404.

Una fila para un almacén inactivo es válida (permite desbloquear o limpiar). Los servicios (`ArticuloInventario = 'N'`) aceptan la configuración, pero no se aplica porque nunca mueven stock.

No se agregan módulos de permisos ni cambia el PUT del artículo.

## §3 — Bloqueo (dominio)

`InventarioAsientoService.AplicarMovimientoAsync` consulta `ArticuloAlmacen` por `(codArticulo, codAlmacen)` después de comprobar que el artículo es de inventario y que el almacén existe. Si `Bloqueado == 'S'` **y `movReversaDe == null`** (es un movimiento nuevo, no una reversa), lanza `ArticuloAlmacenBloqueadoException(codArticulo, codAlmacen)`, con el mensaje «El artículo {codArticulo} está bloqueado en el almacén {codAlmacen}.».

- La excepción se traduce a **400** (mismo manejo que `StockInsuficienteException`). Como el asiento corre antes de persistir el documento en cada dominio, un documento rechazado **no consume consecutivo** (igual que el stock insuficiente hoy).
- Las reversas (`RevertirAsync`, `RevertirLineaAsync`) pasan siempre.
- Se evalúa por movimiento; sin configuración o con `Bloqueado = 'N'` no cambia ningún comportamiento actual.
- Costo: una lectura por movimiento por clave primaria.

## §4 — Avisos

- **Ficha:** fila en rojo si `estado = BAJO_MINIMO`, en amarillo si `estado = SOBRE_MAXIMO`; no bloquean nada.
- **Existencias:** el listado agrega las columnas Mínimo, Máximo y Bloqueado (de `ArticuloAlmacen` por artículo y almacén) y marca las filas bajo mínimo.
- **Home:** el aviso de stock bajo conserva los artículos que están bajo su mínimo general y suma los artículos que están bajo su mínimo en algún almacén, indicando el almacén. Un mismo artículo no se duplica en la lista general: la fila por almacén aparece aparte.

## §5 — Web

- **Ficha (edición) y alta:** sección «Datos de inventario» con la tabla de SAP B1: `#`, Código de almacén, Nombre del almacén, Bloqueado (casilla), En stock, Comprometido, Pedido, Disponible, Stock mínimo, Stock máximo y Nivel de stock necesario. Las columnas de stock y el nivel son solo lectura y de fondo gris; Bloqueado, Stock mínimo y Stock máximo son editables. Al editar mínimo o máximo, el nivel necesario y el color de la fila se recalculan en el navegador con la misma fórmula. En el alta las columnas de stock valen 0.
- **Guardado:** la Web guarda el artículo (como hoy) y a continuación envía `PUT api/Articulo/{codigo}/Almacenes` con todas las filas. En el alta usa el código devuelto al crear el artículo. Si el artículo se guarda y la configuración falla, el usuario ve el error de la configuración y puede corregirla desde la ficha sin perder el artículo.
- Sin permiso `Articulos` Actualizar la sección es de solo lectura; sin Ver no se muestra.
- Validación en el navegador: mínimo y máximo no negativos y máximo >= mínimo, con mensajes por fila; la API revalida.
- El cliente tipado `IArticuloApiClient` gana `ObtenerAlmacenesAsync(codigo)` y `GuardarAlmacenesAsync(codigo, filas)` siguiendo el patrón existente. Todo texto dinámico se escapa.

## §6 — Pruebas y verificación

- **Unitarias (xUnit + Moq):** `ArticuloAlmacenDomain` (reemplazo completo, eliminación de filas vacías, cada validación, almacén repetido, almacén inactivo); cálculo de `disponible`, `nivelNecesario` y `estado` (sin fila, sin máximo, exacto en el límite, sobre máximo, bajo mínimo); `InventarioAsientoService` (entrada bloqueada, salida bloqueada, sin configuración, `Bloqueado = 'N'`, reversa de un documento con el artículo ya bloqueado pasa); controlador (404, 400 y permisos por reflexión).
- **Contra SQL Server real** (`smoke-stock-almacen.ps1`, base temporal `API_DB_EVID<n>`): migración idempotente, guardado y lectura de la tabla, CHECKs, bloqueo de entrada y salida con documentos reales sin consumir consecutivo, cancelación con el artículo bloqueado, desbloqueo, aviso de bajo mínimo y sobre máximo, regresión de inventario.
- **Navegador (puppeteer):** ficha con la tabla, edición y recálculo del nivel, bloqueo y rechazo al facturar, usuario sin permiso. Capturas reales y consola sin errores.
- Lección de fases anteriores: AutoMapper no debe poblar colecciones de navegación que el dominio escribe explícitamente; se prueba con SQL real desde el principio.

## Riesgos y notas

- **Comprometido y Pedido en 0:** hasta que se mantengan por documento, «Disponible» de la tabla equivale a «En stock». No es un error de esta fase.
- **Bloqueo y documentos en curso:** un artículo bloqueado no impide que un pedido ya existente se copie a una entrega; el rechazo ocurre al intentar asentar el movimiento de la entrega.
- **Reemplazo completo:** el PUT sustituye toda la configuración del artículo; la Web debe enviar siempre todas las filas actuales. Concurrencia entre dos usuarios editando la misma ficha: gana el último guardado (igual que el resto de la ficha).
- **Plan:** el plan debe confirmar en el código el nombre exacto del controlador de artículos y de su aplicación, cómo se traduce `StockInsuficienteException` a 400 para reutilizarlo, y el punto del Home donde se arma el aviso de stock bajo, antes de fijar las tareas.
