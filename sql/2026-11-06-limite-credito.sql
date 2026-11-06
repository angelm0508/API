-- Limite de credito de clientes (Fase C): limite y bloqueo por vencido en SocioNegocio, tabla AutorizacionCredito y permiso AutorizarExcesoCredito.
-- Requiere la Fase B: sql/2026-10-26-condiciones-pago-vencimientos.sql (vencimientos y saldos).
-- Idempotente: cada objeto se crea solo si no existe. Ninguna fila existente cambia de significado: todos los socios quedan sin limite (NULL) y sin bloqueo ('N').
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres con acentos se guarden bien.
SET NOCOUNT ON;

IF OBJECT_ID('dbo.CondicionPago', 'U') IS NULL THROW 50000, 'Falta la Fase B: ejecute antes sql/2026-10-26-condiciones-pago-vencimientos.sql', 1;

-- ===== Columnas nuevas en SocioNegocio (NULL = sin limite; 0 = solo contado; > 0 = limite) =====
IF COL_LENGTH('dbo.SocioNegocio', 'LimiteCredito') IS NULL
    ALTER TABLE dbo.SocioNegocio ADD LimiteCredito decimal(19,2) NULL;

IF COL_LENGTH('dbo.SocioNegocio', 'BloquearSiVencido') IS NULL
    ALTER TABLE dbo.SocioNegocio ADD BloquearSiVencido nvarchar(1) NOT NULL CONSTRAINT DF_SocioNegocio_BloquearSiVencido DEFAULT ('N');

-- Los CHECK referencian columnas agregadas en este mismo lote: se crean con EXEC para que el lote compile
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SocioNegocio_LimiteCredito' AND parent_object_id = OBJECT_ID('dbo.SocioNegocio'))
    EXEC('ALTER TABLE dbo.SocioNegocio ADD CONSTRAINT CK_SocioNegocio_LimiteCredito CHECK (LimiteCredito IS NULL OR LimiteCredito >= 0)');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SocioNegocio_BloquearSiVencido' AND parent_object_id = OBJECT_ID('dbo.SocioNegocio'))
    EXEC('ALTER TABLE dbo.SocioNegocio ADD CONSTRAINT CK_SocioNegocio_BloquearSiVencido CHECK (BloquearSiVencido IN (''S'',''N''))');

-- ===== AutorizacionCredito (registro sin borrado de documentos autorizados por encima del limite o con vencido) =====
IF OBJECT_ID('dbo.AutorizacionCredito', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AutorizacionCredito (
        Id             int           IDENTITY(1,1) NOT NULL,
        TipoDocumento  int           NOT NULL,
        EntryDocumento int           NOT NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        Usuario        nvarchar(50)  NOT NULL,
        Motivo         nvarchar(20)  NOT NULL CONSTRAINT CK_AutorizacionCredito_Motivo CHECK (Motivo IN ('LIMITE','VENCIDO')),
        Limite         decimal(19,2) NULL,
        Saldo          decimal(19,2) NOT NULL,
        Comprometido   decimal(19,2) NOT NULL,
        Vencido        decimal(19,2) NOT NULL,
        Monto          decimal(19,2) NOT NULL,
        Exceso         decimal(19,2) NOT NULL,
        Fecha          datetime      NOT NULL CONSTRAINT DF_AutorizacionCredito_Fecha DEFAULT (GETDATE()),
        CONSTRAINT pk_autorizacion_credito PRIMARY KEY (Id),
        CONSTRAINT fk_autorizacion_credito_sn FOREIGN KEY (CodigoSN) REFERENCES dbo.SocioNegocio(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AutorizacionCredito_Documento' AND object_id = OBJECT_ID('dbo.AutorizacionCredito'))
    CREATE NONCLUSTERED INDEX IX_AutorizacionCredito_Documento ON dbo.AutorizacionCredito (TipoDocumento, EntryDocumento);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AutorizacionCredito_CodigoSN' AND object_id = OBJECT_ID('dbo.AutorizacionCredito'))
    CREATE NONCLUSTERED INDEX IX_AutorizacionCredito_CodigoSN ON dbo.AutorizacionCredito (CodigoSN);

-- ===== Catalogo de permisos (grupo Ventas); acceso denegado por defecto: nadie lo recibe sin asignarlo =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('AutorizarExcesoCredito', N'Autorizar exceso de crédito', 'Ventas')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Limite de credito aplicado.';
SELECT c.name AS Columna, c.is_nullable AS Nulable, c.max_length AS Longitud FROM sys.columns c WHERE c.object_id = OBJECT_ID('dbo.SocioNegocio') AND c.name IN ('LimiteCredito','BloquearSiVencido') ORDER BY c.name;
SELECT name AS Tabla FROM sys.tables WHERE name = 'AutorizacionCredito';
SELECT Codigo, Nombre, Grupo FROM dbo.Modulo WHERE Codigo = 'AutorizarExcesoCredito';
