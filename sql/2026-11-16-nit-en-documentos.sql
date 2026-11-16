-- NIT en los documentos de compra y venta (campo Nit copiado del socio de negocio al crear).
-- Requiere las tablas de documentos (sql/2026-10-05-notas-credito.sql incluida). Idempotente: cada columna se agrega solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001.
SET NOCOUNT ON;

-- Nueve tablas: Cotizacion, Pedido, Entrega, Factura (incluye notas de debito de venta, DocSubType 'ND'), NotaCredito,
-- PedidoCompra, EntregaCompra, FacturaCompra (incluye notas de debito de compra, 'DN') y NotaCreditoCompra.
-- Columna Nit nvarchar(15) NULL (igual que SocioNegocio.Nit). El backfill (NIT actual del socio en los documentos
-- existentes) se ejecuta una sola vez, justo cuando se agrega la columna: volver a correr el script no rellena documentos
-- que se crearon sin NIT a proposito.
IF COL_LENGTH('dbo.Cotizacion', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.Cotizacion ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.Cotizacion d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.Pedido', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.Pedido ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.Pedido d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.Entrega', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.Entrega ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.Entrega d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.Factura', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.Factura ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.Factura d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.NotaCredito', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.NotaCredito ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.NotaCredito d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.PedidoCompra', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.PedidoCompra ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.PedidoCompra d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.EntregaCompra', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.EntregaCompra ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.EntregaCompra d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.FacturaCompra', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.FacturaCompra ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.FacturaCompra d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

IF COL_LENGTH('dbo.NotaCreditoCompra', 'Nit') IS NULL
BEGIN
    ALTER TABLE dbo.NotaCreditoCompra ADD Nit nvarchar(15) NULL;
    -- EXEC: la columna se agrega en este mismo lote, el UPDATE no compilaria antes de existir
    EXEC('UPDATE d SET d.Nit = s.Nit FROM dbo.NotaCreditoCompra d JOIN dbo.SocioNegocio s ON s.Codigo = d.CodigoSN WHERE d.Nit IS NULL AND s.Nit IS NOT NULL');
END

PRINT 'NIT en documentos aplicado.';
SELECT t.name AS Tabla, c.name AS Columna, c.max_length / 2 AS Longitud, c.is_nullable AS Nulable FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id WHERE c.name = 'Nit' AND t.name IN ('Cotizacion','Pedido','Entrega','Factura','NotaCredito','PedidoCompra','EntregaCompra','FacturaCompra','NotaCreditoCompra') ORDER BY t.name;
