-- Notas de credito de cliente (objeto 8) y de proveedor (objeto 15). Sin inventario.
-- Idempotente: cada objeto se crea solo si no existe. Las series nuevas toman los siguientes
-- numeros libres de NumeracionDocumentoDet (en una base con series 1-35: 36-39).
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

-- ===== NotaCredito (cliente, ObjType 8) =====
IF OBJECT_ID('dbo.NotaCredito', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaCredito (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaCredito_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_NumManual DEFAULT ('N') CONSTRAINT CK_NotaCredito_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaCredito_Imprimido CHECK (Imprimido IN ('S','N')),
        EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaCredito_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaCredito_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaCredito_TipoObjeto DEFAULT ('8') CONSTRAINT CK_NotaCredito_TipoObjeto CHECK (TipoObjeto IN ('8')),
        FechaDoc       datetime      NOT NULL,
        FechaEmision   datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        BaseTipo       int           NULL CONSTRAINT DF_NotaCredito_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaCredito_BaseTipo CHECK (BaseTipo IN (-1, 6)),
        BaseEntry      int           NULL,
        PrctjeImpuesto decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalBruto     decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_nota_credito PRIMARY KEY (Entry),
        CONSTRAINT fk_nota_credito_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_nota_credito_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_nota_credito_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
    );
END

IF OBJECT_ID('dbo.NotaCreditoDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaCreditoDetalle (
        Entry          int           NOT NULL,
        NoLinea        int           NOT NULL,
        Descripcion    nvarchar(200) NOT NULL,
        Cantidad       decimal(19,6) NULL,
        Precio         decimal(19,6) NULL,
        PrecioBruto    decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        CodigoImpuesto nvarchar(8)   NOT NULL,
        Impuesto       decimal(19,6) NULL,
        TotalLinea     decimal(19,6) NULL,
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaCreditoDet_TipoObjeto DEFAULT ('8') CONSTRAINT CK_NotaCreditoDet_TipoObjeto CHECK (TipoObjeto IN ('8')),
        CONSTRAINT pk_nota_credito_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_nota_credito_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
    );
END

-- ===== NotaCreditoCompra (proveedor, ObjType 15) =====
IF OBJECT_ID('dbo.NotaCreditoCompra', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaCreditoCompra (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaCreditoCompra_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_NumManual DEFAULT ('N') CONSTRAINT CK_NotaCreditoCompra_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaCreditoCompra_Imprimido CHECK (Imprimido IN ('S','N')),
        EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaCreditoCompra_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaCreditoCompra_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaCreditoCompra_TipoObjeto DEFAULT ('15') CONSTRAINT CK_NotaCreditoCompra_TipoObjeto CHECK (TipoObjeto IN ('15')),
        FechaDoc       datetime      NOT NULL,
        FechaEmision   datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        BaseTipo       int           NULL CONSTRAINT DF_NotaCreditoCompra_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaCreditoCompra_BaseTipo CHECK (BaseTipo IN (-1, 13)),
        BaseEntry      int           NULL,
        PrctjeImpuesto decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalBruto     decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_nota_credito_compra PRIMARY KEY (Entry),
        CONSTRAINT fk_nota_credito_compra_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_nota_credito_compra_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_nota_credito_compra_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
    );
END

IF OBJECT_ID('dbo.NotaCreditoCompraDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaCreditoCompraDetalle (
        Entry          int           NOT NULL,
        NoLinea        int           NOT NULL,
        Descripcion    nvarchar(200) NOT NULL,
        Cantidad       decimal(19,6) NULL,
        Precio         decimal(19,6) NULL,
        PrecioBruto    decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        CodigoImpuesto nvarchar(8)   NOT NULL,
        Impuesto       decimal(19,6) NULL,
        TotalLinea     decimal(19,6) NULL,
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaCreditoCompraDet_TipoObjeto DEFAULT ('15') CONSTRAINT CK_NotaCreditoCompraDet_TipoObjeto CHECK (TipoObjeto IN ('15')),
        CONSTRAINT pk_nota_credito_compra_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_nota_credito_compra_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
    );
END

-- ===== Numeracion: series Manual y Primario por documento =====
DECLARE @manualNC int, @primariaNC int, @manualNCP int, @primariaNCP int;

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '8')
BEGIN
    SET @manualNC = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @primariaNC = @manualNC + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('8', @manualNC, 'Manual', 'N', '--', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('8', @primariaNC, 'Primario', 'N', '--', 'D', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '8' AND SubTipoDoc = '--')
        INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
        VALUES ('8', @primariaNC, 'Nota Credito Cliente', '--');
END

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '15')
BEGIN
    SET @manualNCP = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @primariaNCP = @manualNCP + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('15', @manualNCP, 'Manual', 'N', '--', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('15', @primariaNCP, 'Primario', 'N', '--', 'D', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '15' AND SubTipoDoc = '--')
        INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
        VALUES ('15', @primariaNCP, 'Nota Credito Prov.', '--');
END

-- ===== Catalogo de permisos =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('NotasCredito', 'Notas de crédito', 'Ventas'),
    ('NotasCreditoCompra', 'Notas de crédito de compra', 'Compras')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT name FROM sys.tables WHERE name IN ('NotaCredito','NotaCreditoDetalle','NotaCreditoCompra','NotaCreditoCompraDetalle') ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj IN ('8','15') ORDER BY Serie;
SELECT CodigoObj, SerieDfct, DocAlias FROM dbo.NumeracionDocumento WHERE CodigoObj IN ('8','15');
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('NotasCredito','NotasCreditoCompra');
