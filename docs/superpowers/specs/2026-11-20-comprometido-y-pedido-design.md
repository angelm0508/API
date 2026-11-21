# Inventario: «Comprometido» y «Pedido» (diseño)

Fecha: 2026-11-20. Repos: `API` (rama `desarrollo`) y `Web` (rama `main`). Estado: diseño aprobado por el usuario en chat (partes 1 y 2); pendiente de revisión del documento.

## 1. Objetivo y alcance

Como en SAP Business One, llevar en `ExistenciaArticulo` (por artículo y almacén):

- **Comprometido** = unidades de **pedidos de venta** aún no entregadas ni facturadas.
- **Pedido** = unidades de **pedidos de compra** aún no recibidas ni facturadas.
- **Disponible** (calculado, ya existe en `StockAlmacenCalculadora.Disponible`) = en stock − comprometido + pedido.

Hoy las columnas `Comprometido` y `Pedido` existen y se **muestran** (ficha del artículo, `Existencias`, reportes `existencias-valor` y `stock-bajo-minimo`), pero ningún documento las mantiene. Este trabajo hace que se mantengan solas.

Decisiones del usuario:

- **Solo informativo:** no bloquea ni avisa al vender; las validaciones de stock siguen usando solo lo que hay en stock.
- **Enfoque B:** un servicio **recalcula** estas cantidades desde los pedidos abiertos (fuente de verdad) y las guarda en las columnas existentes; se invoca tras cada operación relevante y hay un «recalcular todo» para el relleno inicial y la reparación.

**Fuera de alcance:** cotizaciones, órdenes de servicio, ventas POS y facturas directas (no pasan por pedido), avisos o bloqueos por disponibilidad, reservas por lote/serie.

## 2. Reglas de cálculo

**Comprometido (ventas).** Por cada línea de un **pedido de venta activo** (`Cancelado != "S"` y `EstadoDoc != "C"`; la misma definición de «activo» de `DocumentoBaseService`):

`abierto = máx(0, cantidad de la línea − consumido)`, donde `consumido` es la cantidad copiada a líneas de **entregas y facturas activas** cuya base es esa línea (`BaseTipo = 4`, `BaseEntry`, `BaseLinea`); es el cálculo que ya hace `DocumentoBaseService.ConsumosAsync`.

- Se expresa en **unidad base**: `abierto × FactorUom` (la línea guarda `CantidadBase` y `FactorUom`; ver §9).
- Se suma por **(artículo, almacén de la línea del pedido)**. Una entrega o factura que descuenta el stock de otro almacén libera igualmente lo comprometido en el almacén del pedido.
- Solo artículos de inventario (`Articulo.ArticuloInventario = "S"`) y líneas con artículo y almacén definidos; el resto no compromete nada.

**Pedido (compras).** Igual con los **pedidos de compra activos**: `abierto = máx(0, cantidad − consumido)` con `consumido` = cantidad copiada a líneas de **entregas de compra y facturas de compra activas** con `BaseTipo = 11` (el cálculo que ya hace `PedidoCompraDomain` para sus líneas); unidad base, por (artículo, almacén de la línea).

**Cuándo se libera/recupera (consecuencias de la regla, no código aparte):** se libera al entregar o facturar (parcial o total), al cancelar o cerrar el pedido, al borrarlo, al quitar o reducir líneas y al cancelar o borrar una entrega o factura que lo consumía (la cantidad vuelve a quedar abierta). Reabrir un pedido lo vuelve a comprometer.

## 3. Componente

Servicio en `Domain.Core` con su contrato en `API.Domain.Interface/Existencias/`:

```csharp
public sealed record ResultadoRecalculo(int Filas, int Corregidas);

public interface IExistenciaCompromisoService
{
    // Recalcula Comprometido y Pedido de esos (artículo, almacén). Idempotente.
    Task RecalcularAsync(IReadOnlyCollection<(string CodArticulo, string CodAlmacen)> pares);

    // Recalcula los pares que toca un documento: los de sus propias líneas y los de las líneas de los pedidos base.
    // tipoDoc: 4 pedido, 5 entrega, 6 factura (ventas); 11 pedido de compra, 12 entrega de compra, 13 factura de compra.
    Task RecalcularPorDocumentoAsync(int tipoDoc, int entry);

    // Recalcula todos los pares con pedidos abiertos y deja en 0 los que dejaron de tenerlos. Devuelve cuántas filas
    // revisó y cuántas cambió.
    Task<ResultadoRecalculo> RecalcularTodoAsync();
}
```

- Escribe `ExistenciaArticulo.Comprometido` / `Pedido` (si falta la fila `(artículo, almacén)` la crea con `Disponible = 0`). **No** toca `Disponible` (el stock) ni genera movimientos de inventario.
- La suma se hace en SQL (consultas agrupadas por artículo y almacén, sin cargar tablas completas ni N+1); el consumo por línea reutiliza la lógica existente (el plan decide si se extrae a una consulta compartida o se invoca `DocumentoBaseService` / el cálculo de `PedidoCompraDomain` línea a línea para los pedidos afectados).
- Los redondeos: las columnas son `decimal` con la precisión que ya tienen; sin redondeo propio salvo el del factor de unidad.

## 4. Cuándo se invoca

Después de **confirmar** cada una de estas operaciones se llama `RecalcularPorDocumentoAsync(tipoDoc, entry)` (o `RecalcularAsync` con los pares conocidos cuando el documento ya no existe, p. ej. al borrarlo: se recolectan los pares **antes** de borrar):

| Ventas | Compras |
|---|---|
| Pedido: alta (con líneas), actualización (cancelar, cerrar, reabrir), eliminación | Pedido de compra: alta, actualización (cancelar, cerrar, reabrir), eliminación |
| Línea de pedido: alta, edición, eliminación | Línea de pedido de compra: alta, edición, eliminación |
| Entrega: alta, cancelar/reabrir, eliminación | Entrega de compra: alta, cancelar/reabrir, eliminación |
| Factura: alta, cancelar/reabrir, eliminación | Factura de compra: alta, cancelar/reabrir, eliminación |

Reglas de la invocación:
- Corre **después de la transacción del documento** (necesita leer las líneas ya guardadas), en una operación pequeña aparte. Si el recálculo falla, el documento queda guardado, se registra el error en el log (`ILogger`) y «Recalcular todo» lo repara; **nunca** falla ni revierte la operación del usuario.
- Solo se invoca si el documento tiene líneas de artículos de inventario con almacén (o si es un pedido); el resto no paga el costo.
- En los flujos que anidan operaciones (p. ej. la venta POS no pasa por pedido) no hay llamada.
- Un documento **sin base de pedido** no recalcula comprometido (no consume nada); sí se recalcula cuando es una entrega/factura con `BaseTipo` 4/5 (factura sobre entrega: el recálculo remonta a los pedidos base de la entrega) o 11 en compras.

## 5. Relleno inicial y reparación

- `POST api/Existencia/RecalcularCompromisos` → `Respuesta<ResultadoRecalculo>`: ejecuta `RecalcularTodoAsync`. Permiso: actualizar en el módulo de existencias (el plan verifica el código del módulo en `ExistenciaController`). Idempotente.
- Se ejecuta **una vez al desplegar** para cargar los pedidos abiertos que ya existen (queda documentado en el traspaso; no hay script SQL porque el consumo por línea vive en el dominio).
- Web: botón «Recalcular comprometido y pedido» en la pantalla `Existencias` (visible con ese permiso), con confirmación y resultado («N filas revisadas, M corregidas»).

## 6. Límites aceptados

- Con operaciones simultáneas sobre el mismo (artículo, almacén), un recálculo puede escribir por instantes una suma desactualizada; la siguiente operación o «Recalcular todo» la corrige.
- Si el recálculo post-confirmación falla, las columnas quedan desactualizadas hasta el siguiente recálculo (el log lo registra).
- Líneas sin almacén o sin artículo, y artículos que no son de inventario, no comprometen ni piden.
- Los documentos antiguos con datos incompletos (p. ej. líneas con `CantidadBase` nula) se tratan con la cantidad × `FactorUom` (factor 1 por defecto).

## 6 bis. Interfaz

Sin cambios de diseño: la ficha del artículo (`articulo-almacenes.js`) ya muestra En stock, Comprometido, Pedido y Disponible por almacén, y `Existencias` ya las lista. Solo se agrega el botón de §5.

## 7. Pruebas

- **xUnit** con cantidades concretas (repos con LINQ a objetos): pedido de 10 → comprometido 10; entrega de 4 → 6; factura de lo restante → 0; cancelar la entrega → vuelve a 10; cancelar el pedido → 0; cerrar el pedido con saldo → 0; reabrir → vuelve; entrega desde otro almacén libera el comprometido del almacén del pedido; unidad de medida con factor (caja de 12); artículo no inventariable ignorado; línea sin almacén ignorada; pares distintos del mismo artículo; creación de la fila de existencias si falta; el mismo cálculo para compras (pedido 20, entrega de compra 5 → pedido 15). `RecalcularTodoAsync`: deja en 0 los pares sin pedidos abiertos y cuenta filas y corregidas.
- Una prueba por cada dominio integrado verificando que invoca el servicio **después** de confirmar y que un fallo del servicio **no** hace fallar la operación (el error se registra).
- **SQL real** (base temporal `API_DB_EVID<n>`, nunca `API_DB_TEST`): flujos completos pedido → entrega → factura (parcial y total) y pedido de compra → entrega de compra → factura de compra, con cancelaciones; relleno inicial con pedidos abiertos creados antes; «Recalcular todo» idempotente; que las consultas se traduzcan a SQL sin advertencias de EF; comprobar que `existencias-valor` y `stock-bajo-minimo` muestran los nuevos valores.
- **Navegador:** ficha del artículo (como la tabla de SAP B1: En stock, Comprometido, Pedido, Disponible) y el botón de recalcular.

## 8. Entrega por fases

1. **Servicio y recálculo total:** contrato, implementación (ventas y compras), endpoint `RecalcularCompromisos`, pruebas.
2. **Integración en ventas:** pedido, línea de pedido, entrega, factura.
3. **Integración en compras:** pedido de compra, línea, entrega de compra, factura de compra.
4. **Web, documentación y verificación:** botón en Existencias, `AGENTS.md`/traspaso, humo con SQL real y recorrido en navegador.

Cada fase cierra con build sin errores y suite completa en verde.

## 9. Puntos que el plan debe verificar al leer el código

- Dónde se calcula hoy el consumo de **compras** (`PedidoCompraDomain` ~líneas 130-170) y cómo extraerlo/reutilizarlo sin duplicar; y que las líneas de entrega/factura de compra guardan `BaseTipo = 11`, `BaseEntry` y `BaseLinea`.
- Cómo definir «activo» en compras (`Cancelado`/`EstadoDoc`) y que sea el mismo criterio que ventas.
- Campos de unidad en líneas de pedido de venta y de compra (`CantidadBase`, `FactorUom`) y si `Cantidad` del consumo está en la unidad de la línea del pedido (el consumo por línea del pedido se compara con la cantidad de la línea en la misma unidad).
- El código del módulo de permisos de Existencias y el controlador (`ExistenciaController`).
- Los puntos de cada dominio donde la transacción ya se confirmó (`_tx.EjecutarAsync`), para llamar al servicio justo después, y los flujos en que el documento se borra (recolectar pares antes).
- Cómo se cierra automáticamente un pedido totalmente consumido (`EstadoDoc`), por si el cierre ocurre en el mismo paso que la entrega/factura que lo consume.
