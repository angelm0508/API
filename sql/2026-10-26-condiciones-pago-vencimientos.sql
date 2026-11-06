-- Condiciones de pago y vencimientos (Fase B): catalogo CondicionPago, condicion por socio, y condicion + fecha de vencimiento en Factura/FacturaCompra.
-- Requiere la Fase A: sql/2026-10-25-pagos-efectuados.sql (y antes sql/2026-10-23-pagos-recibidos.sql).
-- Idempotente: cada objeto se crea solo si no existe; la migracion de historicos solo toca filas sin vencimiento.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres con acentos se guarden bien.
SET NOCOUNT ON;

IF OBJECT_ID('dbo.PagoEfectuado') IS NULL THROW 50000, 'Falta la Fase A: ejecute antes sql/2026-10-25-pagos-efectuados.sql', 1;

-- ===== CondicionPago (catalogo) =====
IF OBJECT_ID('dbo.CondicionPago', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CondicionPago (
        Codigo nvarchar(10) NOT NULL,
        Nombre nvarchar(50) NOT NULL,
        Dias   int          NOT NULL CONSTRAINT CK_CondicionPago_Dias CHECK (Dias BETWEEN 0 AND 365),
        Activo nvarchar(1)  NOT NULL CONSTRAINT DF_CondicionPago_Activo DEFAULT ('S') CONSTRAINT CK_CondicionPago_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_condicion_pago PRIMARY KEY (Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.CondicionPago WHERE Codigo = 'CON') INSERT INTO dbo.CondicionPago (Codigo, Nombre, Dias) VALUES ('CON', N'Contado', 0);
IF NOT EXISTS (SELECT 1 FROM dbo.CondicionPago WHERE Codigo = 'C15') INSERT INTO dbo.CondicionPago (Codigo, Nombre, Dias) VALUES ('C15', N'Crédito 15 días', 15);
IF NOT EXISTS (SELECT 1 FROM dbo.CondicionPago WHERE Codigo = 'C30') INSERT INTO dbo.CondicionPago (Codigo, Nombre, Dias) VALUES ('C30', N'Crédito 30 días', 30);
IF NOT EXISTS (SELECT 1 FROM dbo.CondicionPago WHERE Codigo = 'C45') INSERT INTO dbo.CondicionPago (Codigo, Nombre, Dias) VALUES ('C45', N'Crédito 45 días', 45);
IF NOT EXISTS (SELECT 1 FROM dbo.CondicionPago WHERE Codigo = 'C60') INSERT INTO dbo.CondicionPago (Codigo, Nombre, Dias) VALUES ('C60', N'Crédito 60 días', 60);

-- ===== Columnas nuevas (nulables para no romper INSERT existentes; el dominio las llena siempre) =====
IF COL_LENGTH('dbo.SocioNegocio', 'CondicionPago') IS NULL
BEGIN
    ALTER TABLE dbo.SocioNegocio ADD CondicionPago nvarchar(10) NULL;
    ALTER TABLE dbo.SocioNegocio ADD CONSTRAINT fk_sn_condicion_pago FOREIGN KEY (CondicionPago) REFERENCES dbo.CondicionPago(Codigo);
END

IF COL_LENGTH('dbo.Factura', 'CondicionPago') IS NULL
BEGIN
    ALTER TABLE dbo.Factura ADD CondicionPago nvarchar(10) NULL;
    ALTER TABLE dbo.Factura ADD CONSTRAINT fk_factura_condicion_pago FOREIGN KEY (CondicionPago) REFERENCES dbo.CondicionPago(Codigo);
END

IF COL_LENGTH('dbo.Factura', 'FechaVencimiento') IS NULL
    ALTER TABLE dbo.Factura ADD FechaVencimiento datetime NULL;

IF COL_LENGTH('dbo.FacturaCompra', 'CondicionPago') IS NULL
BEGIN
    ALTER TABLE dbo.FacturaCompra ADD CondicionPago nvarchar(10) NULL;
    ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT fk_factura_compra_condicion_pago FOREIGN KEY (CondicionPago) REFERENCES dbo.CondicionPago(Codigo);
END

IF COL_LENGTH('dbo.FacturaCompra', 'FechaVencimiento') IS NULL
    ALTER TABLE dbo.FacturaCompra ADD FechaVencimiento datetime NULL;

-- ===== Migracion de historicos: contado, vence el mismo dia del documento (EXEC: las columnas se agregan en este mismo lote) =====
EXEC('UPDATE dbo.Factura SET CondicionPago = ''CON'', FechaVencimiento = FechaDoc WHERE FechaVencimiento IS NULL');
EXEC('UPDATE dbo.FacturaCompra SET CondicionPago = ''CON'', FechaVencimiento = FechaDoc WHERE FechaVencimiento IS NULL');

-- ===== Indices para la antiguedad de saldos =====
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Factura_Vencimiento' AND object_id = OBJECT_ID('dbo.Factura'))
    CREATE NONCLUSTERED INDEX IX_Factura_Vencimiento ON dbo.Factura (FechaVencimiento) INCLUDE (CodigoSN, TotalDoc);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacturaCompra_Vencimiento' AND object_id = OBJECT_ID('dbo.FacturaCompra'))
    CREATE NONCLUSTERED INDEX IX_FacturaCompra_Vencimiento ON dbo.FacturaCompra (FechaVencimiento) INCLUDE (CodigoSN, TotalDoc);

-- ===== Catalogo de permisos =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('CondicionesPago',  N'Condiciones de pago',                'SociosNegocio'),
    ('EstadoCuenta',     N'Estado de cuenta',                   'SociosNegocio'),
    ('AntiguedadCobrar', N'Antigüedad de saldos por cobrar',    'Ventas'),
    ('AntiguedadPagar',  N'Antigüedad de saldos por pagar',     'Compras')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Condiciones de pago y vencimientos aplicado.';
SELECT Codigo, Nombre, Dias, Activo FROM dbo.CondicionPago ORDER BY Dias;
SELECT t.name AS Tabla, c.name AS Columna FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id WHERE c.name IN ('CondicionPago','FechaVencimiento') AND t.name IN ('SocioNegocio','Factura','FacturaCompra') ORDER BY t.name, c.name;
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('CondicionesPago','EstadoCuenta','AntiguedadCobrar','AntiguedadPagar') ORDER BY Codigo;
