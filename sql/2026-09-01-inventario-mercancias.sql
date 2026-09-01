-- INV-4: Entrada y Salida de Mercancias (ajustes de inventario sin socio de negocio).
-- Idempotente: cada objeto se crea solo si no existe.
-- Referencia: OIGN/IGN1 y OIGE/IGE1 de SAP B1, recortado a lo que el proyecto usa.
SET NOCOUNT ON;

-- ===== EntradaMercancia (ObjType 59) =====
IF OBJECT_ID('dbo.EntradaMercancia', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntradaMercancia (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL CONSTRAINT DF_EntradaMerc_NumDoc     DEFAULT (0),
        Serie          int           NOT NULL,
        NumManual      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_NumManual  DEFAULT ('N') CONSTRAINT CK_EntradaMerc_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_Imprimido  DEFAULT ('N'),
        EstadoDoc      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_EstadoDoc  DEFAULT ('A') CONSTRAINT CK_EntradaMerc_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        EstadoInv      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_EstadoInv  DEFAULT ('A') CONSTRAINT CK_EntradaMerc_EstadoInv CHECK (EstadoInv IN ('A','C')),
        Cancelado      char(1)       NOT NULL CONSTRAINT DF_EntradaMerc_Cancelado  DEFAULT ('N') CONSTRAINT CK_EntradaMerc_Cancelado CHECK (Cancelado IN ('S','N')),
        TipoObjeto     varchar(11)   NOT NULL CONSTRAINT DF_EntradaMerc_TipoObjeto DEFAULT ('59'),
        FechaDoc       datetime      NULL,
        FechaContab    datetime      NULL,
        FechaCancelado datetime      NULL,
        Referencia     nvarchar(100) NULL,
        Comentario     nvarchar(254) NULL,
        TotalDoc       decimal(19,6) NOT NULL CONSTRAINT DF_EntradaMerc_TotalDoc   DEFAULT (0),
        CONSTRAINT pk_entrada_mercancia PRIMARY KEY (Entry),
        CONSTRAINT fk_entrada_mercancia_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie)
    );
END

-- ===== EntradaMercanciaDetalle =====
IF OBJECT_ID('dbo.EntradaMercanciaDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntradaMercanciaDetalle (
        Entry         int           NOT NULL,
        NoLinea       int           NOT NULL,
        CodArticulo   varchar(20)   NULL,
        Descripcion   nvarchar(254) NULL,
        Cantidad      decimal(19,6) NULL,
        CostoUnitario decimal(19,6) NOT NULL CONSTRAINT DF_EntradaMercDet_Costo DEFAULT (0),
        TotalLinea    decimal(19,6) NULL,
        CodAlmacen    varchar(10)   NULL,
        CONSTRAINT pk_entrada_mercancia_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_entrada_mercancia_det_art FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_entrada_mercancia_det_alm FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo)
    );
END

-- ===== SalidaMercancia (ObjType 60) — misma estructura, default TipoObjeto '60' =====
IF OBJECT_ID('dbo.SalidaMercancia', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalidaMercancia (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL CONSTRAINT DF_SalidaMerc_NumDoc     DEFAULT (0),
        Serie          int           NOT NULL,
        NumManual      char(1)       NOT NULL CONSTRAINT DF_SalidaMerc_NumManual  DEFAULT ('N') CONSTRAINT CK_SalidaMerc_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      char(1)       NOT NULL CONSTRAINT DF_SalidaMerc_Imprimido  DEFAULT ('N'),
        EstadoDoc      char(1)       NOT NULL CONSTRAINT DF_SalidaMerc_EstadoDoc  DEFAULT ('A') CONSTRAINT CK_SalidaMerc_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        EstadoInv      char(1)       NOT NULL CONSTRAINT DF_SalidaMerc_EstadoInv  DEFAULT ('A') CONSTRAINT CK_SalidaMerc_EstadoInv CHECK (EstadoInv IN ('A','C')),
        Cancelado      char(1)       NOT NULL CONSTRAINT DF_SalidaMerc_Cancelado  DEFAULT ('N') CONSTRAINT CK_SalidaMerc_Cancelado CHECK (Cancelado IN ('S','N')),
        TipoObjeto     varchar(11)   NOT NULL CONSTRAINT DF_SalidaMerc_TipoObjeto DEFAULT ('60'),
        FechaDoc       datetime      NULL,
        FechaContab    datetime      NULL,
        FechaCancelado datetime      NULL,
        Referencia     nvarchar(100) NULL,
        Comentario     nvarchar(254) NULL,
        TotalDoc       decimal(19,6) NOT NULL CONSTRAINT DF_SalidaMerc_TotalDoc   DEFAULT (0),
        CONSTRAINT pk_salida_mercancia PRIMARY KEY (Entry),
        CONSTRAINT fk_salida_mercancia_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie)
    );
END

-- ===== SalidaMercanciaDetalle =====
IF OBJECT_ID('dbo.SalidaMercanciaDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalidaMercanciaDetalle (
        Entry         int           NOT NULL,
        NoLinea       int           NOT NULL,
        CodArticulo   varchar(20)   NULL,
        Descripcion   nvarchar(254) NULL,
        Cantidad      decimal(19,6) NULL,
        CostoUnitario decimal(19,6) NOT NULL CONSTRAINT DF_SalidaMercDet_Costo DEFAULT (0),
        TotalLinea    decimal(19,6) NULL,
        CodAlmacen    varchar(10)   NULL,
        CONSTRAINT pk_salida_mercancia_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_salida_mercancia_det_art FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_salida_mercancia_det_alm FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo)
    );
END

-- ===== Seed de numeracion (idempotente) =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '59')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, SigNumero, Manual, Bloqueado, SubTipoDoc, TipoSerie)
    VALUES ('59', (SELECT ISNULL(MAX(Serie),0)+1 FROM dbo.NumeracionDocumentoDet), 'Primario', 1, 'N', 'N', '--', 'N');
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '60')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, SigNumero, Manual, Bloqueado, SubTipoDoc, TipoSerie)
    VALUES ('60', (SELECT ISNULL(MAX(Serie),0)+1 FROM dbo.NumeracionDocumentoDet), 'Primario', 1, 'N', 'N', '--', 'N');

PRINT 'INV-4 DDL aplicado.';
SELECT name FROM sys.tables
 WHERE name IN ('EntradaMercancia','EntradaMercanciaDetalle','SalidaMercancia','SalidaMercanciaDetalle')
 ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj IN ('59','60') ORDER BY CodigoObj;
