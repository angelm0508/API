-- Notas de debito dentro de Factura (DocSubType 'ND') y FacturaCompra (DocSubType 'DN'), como SAP B1:
-- la nota de debito comparte tabla con la factura y se distingue por DocSubType. DocType elige el modo
-- ('I' = por articulos, mueve inventario; 'S' = por concepto, sin inventario).
-- Idempotente. Reemplaza a 2026-10-04-notas-debito.sql (que ya no debe ejecutarse).
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0) Precondicion: esta migracion no copia datos de las tablas NotaDebito*. Si tienen filas, no cambia nada.
-- (conteo con SQL dinamico: si una tabla ya no existe, una consulta estatica a ella no compilaria)
DECLARE @filas int = 0, @cnt int;
IF OBJECT_ID('dbo.NotaDebito', 'U') IS NOT NULL
BEGIN
    EXEC sp_executesql N'SELECT @c = COUNT(*) FROM dbo.NotaDebito', N'@c int OUTPUT', @c = @cnt OUTPUT;
    SET @filas = @filas + @cnt;
END
IF OBJECT_ID('dbo.NotaDebitoDetalle', 'U') IS NOT NULL
BEGIN
    EXEC sp_executesql N'SELECT @c = COUNT(*) FROM dbo.NotaDebitoDetalle', N'@c int OUTPUT', @c = @cnt OUTPUT;
    SET @filas = @filas + @cnt;
END
IF OBJECT_ID('dbo.NotaDebitoCompra', 'U') IS NOT NULL
BEGIN
    EXEC sp_executesql N'SELECT @c = COUNT(*) FROM dbo.NotaDebitoCompra', N'@c int OUTPUT', @c = @cnt OUTPUT;
    SET @filas = @filas + @cnt;
END
IF OBJECT_ID('dbo.NotaDebitoCompraDetalle', 'U') IS NOT NULL
BEGIN
    EXEC sp_executesql N'SELECT @c = COUNT(*) FROM dbo.NotaDebitoCompraDetalle', N'@c int OUTPUT', @c = @cnt OUTPUT;
    SET @filas = @filas + @cnt;
END
IF @filas > 0
    THROW 50001, N'Las tablas NotaDebito* tienen filas: esta migracion no copia datos. No se cambio nada.', 1;

DECLARE @ck sysname, @sql nvarchar(max);

-- 1) Factura (cliente): DocSubType 'ND', BaseTipo 6
IF COL_LENGTH('dbo.Factura', 'DocSubType') IS NULL
    ALTER TABLE dbo.Factura ADD DocSubType nvarchar(2) NOT NULL CONSTRAINT DF_Factura_DocSubType DEFAULT ('--');
IF COL_LENGTH('dbo.Factura', 'DocType') IS NULL
    ALTER TABLE dbo.Factura ADD DocType nvarchar(1) NOT NULL CONSTRAINT DF_Factura_DocType DEFAULT ('I');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_DocSubType' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_DocSubType CHECK (DocSubType IN (''--'', ''ND''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_DocType' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_DocType CHECK (DocType IN (''I'', ''S''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_DocType_Subtipo' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_DocType_Subtipo CHECK (DocSubType <> ''--'' OR DocType = ''I'')');

-- La restriccion original de BaseTipo (IN (-1)) se creo sin nombre: se busca por columna y se reemplaza.
SET @ck = NULL;
SELECT @ck = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('dbo.Factura') AND c.name = 'BaseTipo';
IF @ck IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.Factura DROP CONSTRAINT ' + QUOTENAME(@ck);
    EXEC(@sql);
END
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_Base' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_Base CHECK ((DocSubType = ''--'' AND BaseTipo = -1) OR (DocSubType = ''ND'' AND BaseTipo IN (-1, 6)))');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Factura_DocSubType' AND object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'CREATE NONCLUSTERED INDEX IX_Factura_DocSubType ON dbo.Factura (DocSubType, CodigoSN)');

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaDetalle') AND name = 'CodArticulo' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaDetalle ALTER COLUMN CodArticulo nvarchar(15) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaDetalle') AND name = 'CodAlmacen' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaDetalle ALTER COLUMN CodAlmacen nvarchar(8) NULL;

-- 2) FacturaCompra (proveedor): DocSubType 'DN', BaseTipo 13
IF COL_LENGTH('dbo.FacturaCompra', 'DocSubType') IS NULL
    ALTER TABLE dbo.FacturaCompra ADD DocSubType nvarchar(2) NOT NULL CONSTRAINT DF_FacturaCompra_DocSubType DEFAULT ('--');
IF COL_LENGTH('dbo.FacturaCompra', 'DocType') IS NULL
    ALTER TABLE dbo.FacturaCompra ADD DocType nvarchar(1) NOT NULL CONSTRAINT DF_FacturaCompra_DocType DEFAULT ('I');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_DocSubType' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_DocSubType CHECK (DocSubType IN (''--'', ''DN''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_DocType' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_DocType CHECK (DocType IN (''I'', ''S''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_DocType_Subtipo' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_DocType_Subtipo CHECK (DocSubType <> ''--'' OR DocType = ''I'')');

SET @ck = NULL;
SELECT @ck = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('dbo.FacturaCompra') AND c.name = 'BaseTipo';
IF @ck IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.FacturaCompra DROP CONSTRAINT ' + QUOTENAME(@ck);
    EXEC(@sql);
END
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_Base' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_Base CHECK ((DocSubType = ''--'' AND BaseTipo = -1) OR (DocSubType = ''DN'' AND BaseTipo IN (-1, 13)))');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacturaCompra_DocSubType' AND object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'CREATE NONCLUSTERED INDEX IX_FacturaCompra_DocSubType ON dbo.FacturaCompra (DocSubType, CodigoSN)');

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaCompraDetalle') AND name = 'CodArticulo' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaCompraDetalle ALTER COLUMN CodArticulo nvarchar(15) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaCompraDetalle') AND name = 'CodAlmacen' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaCompraDetalle ALTER COLUMN CodAlmacen nvarchar(8) NULL;

-- 3) Numeracion: las series de las notas cuelgan del objeto de la factura con SubTipoDoc 'ND' / 'DN'.
--    El script anterior de notas de debito las dejaba bajo los objetos 7 y 14: se reasignan.
DECLARE @man int, @pri int;

UPDATE dbo.NumeracionDocumentoDet SET CodigoObj = '6',  SubTipoDoc = 'ND' WHERE CodigoObj = '7'  AND SubTipoDoc = '--';
UPDATE dbo.NumeracionDocumentoDet SET CodigoObj = '13', SubTipoDoc = 'DN' WHERE CodigoObj = '14' AND SubTipoDoc = '--';

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '6' AND SubTipoDoc = 'ND')
   AND EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '7' AND SubTipoDoc = '--')
    UPDATE dbo.NumeracionDocumento SET CodigoObj = '6', SubTipoDoc = 'ND' WHERE CodigoObj = '7' AND SubTipoDoc = '--';
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '13' AND SubTipoDoc = 'DN')
   AND EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '14' AND SubTipoDoc = '--')
    UPDATE dbo.NumeracionDocumento SET CodigoObj = '13', SubTipoDoc = 'DN' WHERE CodigoObj = '14' AND SubTipoDoc = '--';

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '6' AND SubTipoDoc = 'ND')
BEGIN
    SET @man = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @pri = @man + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('6', @man, 'Manual', 'N', 'ND', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('6', @pri, 'Primario', 'N', 'ND', 'D', 1, 1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '6' AND SubTipoDoc = 'ND')
BEGIN
    SET @pri = (SELECT TOP 1 Serie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '6' AND SubTipoDoc = 'ND' AND NombreSerie = 'Primario' ORDER BY Serie);
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
    VALUES ('6', @pri, 'Nota Debito Cliente', 'ND');
END

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '13' AND SubTipoDoc = 'DN')
BEGIN
    SET @man = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @pri = @man + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('13', @man, 'Manual', 'N', 'DN', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('13', @pri, 'Primario', 'N', 'DN', 'D', 1, 1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '13' AND SubTipoDoc = 'DN')
BEGIN
    SET @pri = (SELECT TOP 1 Serie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '13' AND SubTipoDoc = 'DN' AND NombreSerie = 'Primario' ORDER BY Serie);
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
    VALUES ('13', @pri, 'Nota Debito Prov.', 'DN');
END

-- 4) Las tablas propias de notas de debito (vacias, verificado arriba) ya no se usan.
IF OBJECT_ID('dbo.NotaDebitoDetalle', 'U') IS NOT NULL DROP TABLE dbo.NotaDebitoDetalle;
IF OBJECT_ID('dbo.NotaDebito', 'U') IS NOT NULL DROP TABLE dbo.NotaDebito;
IF OBJECT_ID('dbo.NotaDebitoCompraDetalle', 'U') IS NOT NULL DROP TABLE dbo.NotaDebitoCompraDetalle;
IF OBJECT_ID('dbo.NotaDebitoCompra', 'U') IS NOT NULL DROP TABLE dbo.NotaDebitoCompra;

-- 5) Catalogo de permisos (los modulos no cambian)
MERGE dbo.Modulo AS target
USING (VALUES
    ('NotasDebito', 'Notas de débito', 'Ventas'),
    ('NotasDebitoCompra', 'Notas de débito de compra', 'Compras')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT name FROM sys.tables WHERE name LIKE 'NotaDebito%' ORDER BY name;   -- debe quedar vacio
SELECT CodigoObj, Serie, NombreSerie, SubTipoDoc FROM dbo.NumeracionDocumentoDet WHERE SubTipoDoc IN ('ND', 'DN') ORDER BY Serie;
SELECT CodigoObj, SerieDfct, DocAlias, SubTipoDoc FROM dbo.NumeracionDocumento WHERE SubTipoDoc IN ('ND', 'DN');
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('NotasDebito', 'NotasDebitoCompra');
