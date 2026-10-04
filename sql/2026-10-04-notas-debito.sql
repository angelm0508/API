-- Notas de debito de cliente (objeto 7) y de proveedor (objeto 14). Sin inventario.
-- Idempotente: cada objeto se crea solo si no existe. Las series nuevas toman los siguientes
-- numeros libres de NumeracionDocumentoDet (en una base con series 1-31: 32-35).
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

-- ===== NotaDebito (cliente, ObjType 7) =====
IF OBJECT_ID('dbo.NotaDebito', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebito (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaDebito_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_NumManual DEFAULT ('N') CONSTRAINT CK_NotaDebito_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaDebito_Imprimido CHECK (Imprimido IN ('S','N')),
        EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaDebito_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaDebito_TipoObjeto DEFAULT ('7') CONSTRAINT CK_NotaDebito_TipoObjeto CHECK (TipoObjeto IN ('7')),
        FechaDoc       datetime      NOT NULL,
        FechaEmision   datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        BaseTipo       int           NULL CONSTRAINT DF_NotaDebito_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaDebito_BaseTipo CHECK (BaseTipo IN (-1, 6)),
        BaseEntry      int           NULL,
        PrctjeImpuesto decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalBruto     decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_nota_debito PRIMARY KEY (Entry),
        CONSTRAINT fk_nota_debito_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_nota_debito_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_nota_debito_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
    );
END

IF OBJECT_ID('dbo.NotaDebitoDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebitoDetalle (
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
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaDebitoDet_TipoObjeto DEFAULT ('7') CONSTRAINT CK_NotaDebitoDet_TipoObjeto CHECK (TipoObjeto IN ('7')),
        CONSTRAINT pk_nota_debito_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_nota_debito_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
    );
END

-- ===== NotaDebitoCompra (proveedor, ObjType 14) =====
IF OBJECT_ID('dbo.NotaDebitoCompra', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebitoCompra (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaDebitoCompra_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_NumManual DEFAULT ('N') CONSTRAINT CK_NotaDebitoCompra_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaDebitoCompra_Imprimido CHECK (Imprimido IN ('S','N')),
        EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaDebitoCompra_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaDebitoCompra_TipoObjeto DEFAULT ('14') CONSTRAINT CK_NotaDebitoCompra_TipoObjeto CHECK (TipoObjeto IN ('14')),
        FechaDoc       datetime      NOT NULL,
        FechaEmision   datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        BaseTipo       int           NULL CONSTRAINT DF_NotaDebitoCompra_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaDebitoCompra_BaseTipo CHECK (BaseTipo IN (-1, 13)),
        BaseEntry      int           NULL,
        PrctjeImpuesto decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalBruto     decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_nota_debito_compra PRIMARY KEY (Entry),
        CONSTRAINT fk_nota_debito_compra_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_nota_debito_compra_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_nota_debito_compra_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
    );
END

IF OBJECT_ID('dbo.NotaDebitoCompraDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebitoCompraDetalle (
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
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaDebitoCompraDet_TipoObjeto DEFAULT ('14') CONSTRAINT CK_NotaDebitoCompraDet_TipoObjeto CHECK (TipoObjeto IN ('14')),
        CONSTRAINT pk_nota_debito_compra_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_nota_debito_compra_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
    );
END

-- ===== Numeracion: series Manual y Primario por documento =====
DECLARE @manualND int, @primariaND int, @manualDN int, @primariaDN int;

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '7')
BEGIN
    SET @manualND = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @primariaND = @manualND + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('7', @manualND, 'Manual', 'N', '--', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('7', @primariaND, 'Primario', 'N', '--', 'D', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '7' AND SubTipoDoc = '--')
        INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
        VALUES ('7', @primariaND, 'Nota Debito Cliente', '--');
END

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '14')
BEGIN
    SET @manualDN = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @primariaDN = @manualDN + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('14', @manualDN, 'Manual', 'N', '--', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('14', @primariaDN, 'Primario', 'N', '--', 'D', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '14' AND SubTipoDoc = '--')
        INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
        VALUES ('14', @primariaDN, 'Nota Debito Prov.', '--');
END

-- ===== Catalogo de permisos =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('NotasDebito', 'Notas de débito', 'Ventas'),
    ('NotasDebitoCompra', 'Notas de débito de compra', 'Compras')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT name FROM sys.tables WHERE name IN ('NotaDebito','NotaDebitoDetalle','NotaDebitoCompra','NotaDebitoCompraDetalle') ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj IN ('7','14') ORDER BY Serie;
SELECT CodigoObj, SerieDfct, DocAlias FROM dbo.NumeracionDocumento WHERE CodigoObj IN ('7','14');
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('NotasDebito','NotasDebitoCompra');
