GO
-- Unidades de medida en las lineas de los documentos (UomEntry, FactorUom, CantidadBase)
IF OBJECT_ID('dbo.Articulo', 'U') IS NULL OR OBJECT_ID('dbo.GrupoUnidadMedidaDetArticulo', 'U') IS NULL
    THROW 50000, 'Faltan las tablas Articulo y GrupoUnidadMedidaDetArticulo: cree primero la base con el script completo.', 1;

-- Factores fraccionarios (p. ej. 1 Lb = 0.4536 Kg): las cantidades del grupo dejan de ser enteras.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GrupoUnidadMedidaDetArticulo') AND name = 'CantAlternativa' AND scale = 0)
    ALTER TABLE dbo.GrupoUnidadMedidaDetArticulo ALTER COLUMN CantAlternativa DECIMAL(21,6) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GrupoUnidadMedidaDetArticulo') AND name = 'CantBase' AND scale = 0)
    ALTER TABLE dbo.GrupoUnidadMedidaDetArticulo ALTER COLUMN CantBase DECIMAL(21,6) NULL;

DECLARE @tablas TABLE (Nombre sysname);
INSERT INTO @tablas VALUES
    ('CotizacionDetalle'), ('PedidoDetalle'), ('EntregaDetalle'), ('FacturaDetalle'),
    ('PedidoCompraDetalle'), ('EntregaCompraDetalle'), ('EntradaMercanciaDetalle'), ('FacturaCompraDetalle'),
    ('SalidaMercanciaDetalle'), ('OrdenServicioDetalle');

DECLARE @t sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Nombre FROM @tablas;
OPEN c;
FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID('dbo.' + @t, 'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH('dbo.' + @t, 'UomEntry') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' ADD UomEntry INT NULL, '
                     + N'FactorUom DECIMAL(19,6) NOT NULL CONSTRAINT ' + QUOTENAME('DF_' + @t + '_FactorUom') + N' DEFAULT (1), '
                     + N'CantidadBase DECIMAL(19,6) NULL;';
            EXEC (@sql);
        END

        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_' + @t + '_uom')
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' ADD CONSTRAINT ' + QUOTENAME('fk_' + @t + '_uom')
                     + N' FOREIGN KEY (UomEntry) REFERENCES dbo.UnidadMedidaArticulo(Entry);';
            EXEC (@sql);
        END

        IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_' + @t + '_FactorUom')
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' ADD CONSTRAINT ' + QUOTENAME('CK_' + @t + '_FactorUom')
                     + N' CHECK (FactorUom > 0);';
            EXEC (@sql);
        END

        -- Migracion: lineas con articulo y grupo real -> unidad base del grupo (factor 1). Grupo Manual (-1) queda sin unidad.
        SET @sql = N'UPDATE d SET d.UomEntry = g.BaseMedida FROM dbo.' + QUOTENAME(@t) + N' d '
                 + N'JOIN dbo.Articulo a ON a.Codigo = d.CodArticulo '
                 + N'JOIN dbo.GrupoUnidadMedidaArticulo g ON g.Entry = a.CodigoGrpUnidadMedida '
                 + N'WHERE d.UomEntry IS NULL AND g.Entry <> -1;';
        EXEC (@sql);

        SET @sql = N'UPDATE dbo.' + QUOTENAME(@t) + N' SET CantidadBase = Cantidad WHERE CantidadBase IS NULL AND Cantidad IS NOT NULL;';
        EXEC (@sql);
    END
    FETCH NEXT FROM c INTO @t;
END
CLOSE c;
DEALLOCATE c;
GO
PRINT 'Unidades de medida en documentos aplicado.';
SELECT t.name AS Tabla FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'UomEntry' ORDER BY t.name;
