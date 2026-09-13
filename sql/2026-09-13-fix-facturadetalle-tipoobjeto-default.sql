-- FacturaDetalle.TipoObjeto tenia su DEFAULT CONSTRAINT en '3' (el valor de
-- CotizacionDetalle, aparentemente copiado por error al crear la tabla) en vez de '6'
-- (el TipoObjeto real de Factura/FacturaDetalle, exigido por el CHECK
-- CK__FacturaDe__TipoO__19AACF41: [TipoObjeto]='6'). FacturaDomain.InsertarAsync nunca
-- asigna TipoObjeto en las lineas -- igual que el resto de los dominios de documento
-- (EntregaCompra, Pedido, FacturaCompra, etc.), que dejan esa columna al DEFAULT de su
-- tabla de detalle -- asi que el INSERT aplicaba '3' y el motor rechazaba la fila con
-- "An error occurred while saving the entity changes." Corrige el DEFAULT para que
-- coincida con el resto de pares documento/detalle:
--   EntregaCompra/EntregaCompraDetalle = 12   FacturaCompra/FacturaCompraDetalle = 13
--   Pedido/PedidoDetalle = 4                  Entrega/EntregaDetalle = 5
--   PedidoCompra/PedidoCompraDetalle = 11     Cotizacion/CotizacionDetalle = 3
--   Factura/FacturaDetalle = 6  (roto: estaba en 3)
-- Idempotente: solo actua si el DEFAULT actual sigue sin ser '6'.
DECLARE @constraint sysname;
SELECT @constraint = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.FacturaDetalle')
  AND c.name = 'TipoObjeto'
  AND dc.definition <> N'(''6'')';

IF @constraint IS NOT NULL
BEGIN
    DECLARE @sqlDrop nvarchar(400) = N'ALTER TABLE dbo.FacturaDetalle DROP CONSTRAINT ' + QUOTENAME(@constraint);
    EXEC(@sqlDrop);
    EXEC('ALTER TABLE dbo.FacturaDetalle ADD CONSTRAINT DF_FacturaDetalle_TipoObjeto DEFAULT (''6'') FOR TipoObjeto');
END

SELECT c.name AS Columna, dc.name AS DefaultName, dc.definition
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.FacturaDetalle') AND c.name = 'TipoObjeto';
