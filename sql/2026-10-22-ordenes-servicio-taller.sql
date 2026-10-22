-- Taller: ordenes de servicio de motocicletas (objeto 70), mecanicos y motocicletas.
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

-- ===== Mecanico =====
IF OBJECT_ID('dbo.Mecanico', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Mecanico (
        Codigo   nvarchar(10)  NOT NULL,
        Nombre   nvarchar(100) NOT NULL,
        Telefono nvarchar(30)  NULL,
        Activo   nvarchar(1)   NOT NULL CONSTRAINT DF_Mecanico_Activo DEFAULT ('S') CONSTRAINT CK_Mecanico_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_mecanico PRIMARY KEY (Codigo)
    );
END

-- ===== Motocicleta =====
IF OBJECT_ID('dbo.Motocicleta', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Motocicleta (
        Entry      int identity(1,1) NOT NULL,
        CodigoSN   nvarchar(15)  NOT NULL,
        Placa      nvarchar(15)  NOT NULL,
        Marca      nvarchar(50)  NULL,
        Modelo     nvarchar(50)  NULL,
        Anio       smallint      NULL,
        Color      nvarchar(30)  NULL,
        Vin        nvarchar(30)  NULL,
        Comentario nvarchar(254) NULL,
        Activo     nvarchar(1)   NOT NULL CONSTRAINT DF_Motocicleta_Activo DEFAULT ('S') CONSTRAINT CK_Motocicleta_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_motocicleta PRIMARY KEY (Entry),
        CONSTRAINT uq_motocicleta_placa UNIQUE (Placa),
        CONSTRAINT fk_motocicleta_sn FOREIGN KEY (CodigoSN) REFERENCES dbo.SocioNegocio(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Motocicleta_CodigoSN' AND object_id = OBJECT_ID('dbo.Motocicleta'))
    CREATE NONCLUSTERED INDEX IX_Motocicleta_CodigoSN ON dbo.Motocicleta (CodigoSN);

-- ===== OrdenServicio (ObjType 70) =====
IF OBJECT_ID('dbo.OrdenServicio', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdenServicio (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_Cancelado DEFAULT ('N') CONSTRAINT CK_OrdenServicio_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_NumManual DEFAULT ('N') CONSTRAINT CK_OrdenServicio_NumManual CHECK (NumManual IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_OrdenServicio_TipoObjeto DEFAULT ('70') CONSTRAINT CK_OrdenServicio_TipoObjeto CHECK (TipoObjeto IN ('70')),
        FechaDoc       datetime      NOT NULL,
        FechaIngreso   datetime      NULL,
        FechaPromesa   datetime      NULL,
        FechaEntrega   datetime      NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        MotoEntry      int           NOT NULL,
        CodMecanico    nvarchar(10)  NULL,
        KmIngreso      int           NULL,
        FallaReportada nvarchar(500) NULL,
        Diagnostico    nvarchar(500) NULL,
        Observaciones  nvarchar(500) NULL,
        EstadoServicio nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicio_Estado DEFAULT ('R') CONSTRAINT CK_OrdenServicio_Estado CHECK (EstadoServicio IN ('R','P','T','E')),
        TotalBruto     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_orden_servicio PRIMARY KEY (Entry),
        CONSTRAINT fk_orden_servicio_serie    FOREIGN KEY (Serie)       REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_orden_servicio_sn       FOREIGN KEY (CodigoSN)    REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_orden_servicio_moneda   FOREIGN KEY (MonedaDoc)   REFERENCES dbo.Moneda(Codigo),
        CONSTRAINT fk_orden_servicio_moto     FOREIGN KEY (MotoEntry)   REFERENCES dbo.Motocicleta(Entry),
        CONSTRAINT fk_orden_servicio_mecanico FOREIGN KEY (CodMecanico) REFERENCES dbo.Mecanico(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrdenServicio_Moto' AND object_id = OBJECT_ID('dbo.OrdenServicio'))
    CREATE NONCLUSTERED INDEX IX_OrdenServicio_Moto ON dbo.OrdenServicio (MotoEntry) INCLUDE (Cancelado, EstadoServicio);

-- ===== OrdenServicioDetalle =====
IF OBJECT_ID('dbo.OrdenServicioDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdenServicioDetalle (
        Entry          int           NOT NULL,
        NoLinea        int           NOT NULL,
        CodArticulo    nvarchar(15)  NOT NULL,
        Descripcion    nvarchar(200) NULL,
        CodAlmacen     nvarchar(8)   NULL,
        EsInsumo       nvarchar(1)   NOT NULL CONSTRAINT DF_OrdenServicioDet_EsInsumo DEFAULT ('N') CONSTRAINT CK_OrdenServicioDet_EsInsumo CHECK (EsInsumo IN ('S','N')),
        Cantidad       decimal(19,6) NULL,
        Precio         decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        CodigoImpuesto nvarchar(8)   NOT NULL,
        Impuesto       decimal(19,6) NULL,
        TotalLinea     decimal(19,6) NULL,
        CodMecanico    nvarchar(10)  NULL,
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_OrdenServicioDet_TipoObjeto DEFAULT ('70') CONSTRAINT CK_OrdenServicioDet_TipoObjeto CHECK (TipoObjeto IN ('70')),
        CONSTRAINT pk_orden_servicio_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_orden_servicio_det_orden    FOREIGN KEY (Entry)          REFERENCES dbo.OrdenServicio(Entry),
        CONSTRAINT fk_orden_servicio_det_cod_art  FOREIGN KEY (CodArticulo)    REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_orden_servicio_det_almacen  FOREIGN KEY (CodAlmacen)     REFERENCES dbo.Almacen(Codigo),
        CONSTRAINT fk_orden_servicio_det_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo),
        CONSTRAINT fk_orden_servicio_det_mecanico FOREIGN KEY (CodMecanico)    REFERENCES dbo.Mecanico(Codigo)
    );
END

-- ===== FacturaDetalle.BaseTipo admite 70 (orden de servicio como documento base) =====
-- El CHECK original es de columna y tiene nombre generado por SQL Server: se busca por definicion.
DECLARE @ck sysname;
WHILE 1 = 1
BEGIN
    SET @ck = NULL;
    SELECT TOP 1 @ck = cc.name
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID('dbo.FacturaDetalle')
      AND cc.definition LIKE '%BaseTipo%'
      AND cc.definition NOT LIKE '%70%';
    IF @ck IS NULL BREAK;
    EXEC (N'ALTER TABLE dbo.FacturaDetalle DROP CONSTRAINT ' + QUOTENAME(@ck));
END

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FacturaDetalle') AND name = 'CK_FacturaDetalle_BaseTipo')
    ALTER TABLE dbo.FacturaDetalle ADD CONSTRAINT CK_FacturaDetalle_BaseTipo CHECK (BaseTipo IN (-1, 3, 4, 5, 70));

-- ===== Numeracion (serie "Primario" y fila padre para la pantalla "Numeracion de documentos") =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('70', (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet), 'Primario', 'N', '--', 'D', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '70' AND SubTipoDoc = '--')
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SubTipoDoc, DocAlias, SerieDfct)
    SELECT '70', '--', 'Orden de servicio', MIN(Serie) FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70';

-- ===== Catalogo de permisos (grupo Taller) =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('Mecanicos', 'Mecánicos', 'Taller'),
    ('Motocicletas', 'Motocicletas', 'Taller'),
    ('OrdenesServicio', 'Órdenes de servicio', 'Taller')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Taller DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('Mecanico','Motocicleta','OrdenServicio','OrdenServicioDetalle') ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '70';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Grupo = 'Taller';
SELECT name, definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.FacturaDetalle') AND definition LIKE '%BaseTipo%';
