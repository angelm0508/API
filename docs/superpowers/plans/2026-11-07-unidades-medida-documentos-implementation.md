# Unidades de medida en las líneas de los documentos - Plan de implementación

Spec: `docs/superpowers/specs/2026-11-07-unidades-medida-documentos-design.md`.
Corrección al spec: son **12** tablas de detalle (la nota de débito de venta usa `FacturaDetalle` y la de compra, `FacturaCompraDetalle`): `CotizacionDetalle`, `PedidoDetalle`, `EntregaDetalle`, `FacturaDetalle`, `NotaCreditoDetalle`, `PedidoCompraDetalle`, `EntregaCompraDetalle`, `EntradaMercanciaDetalle`, `FacturaCompraDetalle`, `NotaCreditoCompraDetalle`, `SalidaMercanciaDetalle`, `OrdenServicioDetalle`.

Decisión de fase 1 (copiar a): una línea copiada de un documento base **debe conservar la unidad de la línea base**; el cambio de unidad en líneas con base se habilita en una fase posterior, porque exige calcular la cantidad abierta en unidad base.

## Fase 1 - Núcleo y factura de venta
1. SQL `sql/2026-11-07-unidades-medida-documentos.sql`: columnas en las 12 tablas, ampliación de `CantAlternativa`/`CantBase` a `DECIMAL(21,6)`, migración de líneas; mismo bloque en los scripts completos.
2. Entidades: `UomEntry`, `FactorUom`, `CantidadBase` en `FacturaDetalle` + mapeo EF.
3. `IUnidadMedidaService` / `UnidadMedidaService` (TDD) + registro en `Startup.cs`.
4. `FacturaDomain.InsertarAsync`: resolver unidad por línea antes de numerar, fijar factor y cantidad base, asentar inventario en base (cantidad y precio).
5. DTO de factura (y nota de débito), mapeo (`FactorUom`/`CantidadBase` ignorados al entrar) y `GET api/Articulo/{codigo}/Unidades`.
6. Pruebas de la factura; suite completa en verde; commit.
7. Web: cliente, select «UM» y recálculo de precio en factura.

## Fase 2 - Resto de documentos de venta (cotización, pedido, entrega, notas de crédito/débito)
## Fase 3 - Compras (pedido, entrega, entrada, factura, notas) y mercancías (entrada/salida)
## Fase 4 - Orden de servicio y «Copiar a» con cambio de unidad y cantidad abierta en base

Cada fase cierra con build sin errores, suite completa en verde, prueba de humo en `API_DB_EVID<n>` y recorrido en navegador.
