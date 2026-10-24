-- Pagos recibidos (objeto 24): cobros a clientes sobre facturas y notas de debito de cliente.
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

-- ===== FormaPago (catalogo) =====
IF OBJECT_ID('dbo.FormaPago', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FormaPago (
        Codigo   nvarchar(10) NOT NULL,
        Nombre   nvarchar(50) NOT NULL,
        TipoDato nvarchar(1)  NOT NULL CONSTRAINT DF_FormaPago_TipoDato DEFAULT ('N') CONSTRAINT CK_FormaPago_TipoDato CHECK (TipoDato IN ('N','C','T','J')), -- N sin dato, C cheque, T transferencia, J tarjeta
        Activo   nvarchar(1)  NOT NULL CONSTRAINT DF_FormaPago_Activo DEFAULT ('S') CONSTRAINT CK_FormaPago_Activo CHECK (Activo IN ('S','N')),
        CONSTRAINT pk_forma_pago PRIMARY KEY (Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'EFE') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('EFE', 'Efectivo', 'N');
IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'CHQ') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('CHQ', 'Cheque', 'C');
IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'TRF') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('TRF', 'Transferencia', 'T');
IF NOT EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'TJT') INSERT INTO dbo.FormaPago (Codigo, Nombre, TipoDato) VALUES ('TJT', 'Tarjeta', 'J');

-- ===== PagoRecibido (ObjType 24) =====
IF OBJECT_ID('dbo.PagoRecibido', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoRecibido (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_PagoRecibido_Cancelado DEFAULT ('N') CONSTRAINT CK_PagoRecibido_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_PagoRecibido_NumManual DEFAULT ('N') CONSTRAINT CK_PagoRecibido_NumManual CHECK (NumManual IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_PagoRecibido_TipoObjeto DEFAULT ('24') CONSTRAINT CK_PagoRecibido_TipoObjeto CHECK (TipoObjeto IN ('24')),
        FechaDoc       datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        TotalPago      decimal(19,6) NOT NULL CONSTRAINT CK_PagoRecibido_Total CHECK (TotalPago > 0),
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_pago_recibido PRIMARY KEY (Entry),
        CONSTRAINT fk_pago_recibido_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_pago_recibido_sn    FOREIGN KEY (CodigoSN) REFERENCES dbo.SocioNegocio(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PagoRecibido_CodigoSN' AND object_id = OBJECT_ID('dbo.PagoRecibido'))
    CREATE NONCLUSTERED INDEX IX_PagoRecibido_CodigoSN ON dbo.PagoRecibido (CodigoSN) INCLUDE (Cancelado);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_PagoRecibido_Serie_NumDoc' AND object_id = OBJECT_ID('dbo.PagoRecibido'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_PagoRecibido_Serie_NumDoc ON dbo.PagoRecibido (Serie, NumDoc);

-- ===== PagoRecibidoFactura (a que documentos se aplica; FK a Factura cubre facturas y notas de debito) =====
IF OBJECT_ID('dbo.PagoRecibidoFactura', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoRecibidoFactura (
        Entry         int           NOT NULL,
        NoLinea       int           NOT NULL,
        FacturaEntry  int           NOT NULL,
        MontoAplicado decimal(19,6) NOT NULL CONSTRAINT CK_PagoRecibidoFactura_Monto CHECK (MontoAplicado > 0),
        CONSTRAINT pk_pago_recibido_factura PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT uq_pago_recibido_factura_doc UNIQUE (Entry, FacturaEntry),
        CONSTRAINT fk_pago_recibido_factura_pago    FOREIGN KEY (Entry)        REFERENCES dbo.PagoRecibido(Entry),
        CONSTRAINT fk_pago_recibido_factura_factura FOREIGN KEY (FacturaEntry) REFERENCES dbo.Factura(Entry)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PagoRecibidoFactura_Factura' AND object_id = OBJECT_ID('dbo.PagoRecibidoFactura'))
    CREATE NONCLUSTERED INDEX IX_PagoRecibidoFactura_Factura ON dbo.PagoRecibidoFactura (FacturaEntry) INCLUDE (MontoAplicado);

-- ===== PagoRecibidoMedio (con que se pago) =====
IF OBJECT_ID('dbo.PagoRecibidoMedio', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoRecibidoMedio (
        Entry        int           NOT NULL,
        NoLinea      int           NOT NULL,
        CodFormaPago nvarchar(10)  NOT NULL,
        Monto        decimal(19,6) NOT NULL CONSTRAINT CK_PagoRecibidoMedio_Monto CHECK (Monto > 0),
        Referencia   nvarchar(50)  NULL,
        Banco        nvarchar(60)  NULL,
        FechaCheque  datetime      NULL,
        CONSTRAINT pk_pago_recibido_medio PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_pago_recibido_medio_pago  FOREIGN KEY (Entry)        REFERENCES dbo.PagoRecibido(Entry),
        CONSTRAINT fk_pago_recibido_medio_forma FOREIGN KEY (CodFormaPago) REFERENCES dbo.FormaPago(Codigo)
    );
END

-- ===== Numeracion (serie "Primario" y fila padre para la pantalla "Numeracion de documentos") =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '24')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('24', (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet), 'Primario', 'N', '--', 'D', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '24' AND SubTipoDoc = '--')
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SubTipoDoc, DocAlias, SerieDfct)
    SELECT '24', '--', 'Pago recibido', MIN(Serie) FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '24';

-- ===== Catalogo de permisos (grupo Ventas) =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('PagosRecibidos', 'Pagos recibidos', 'Ventas'),
    ('FormasPago', 'Formas de pago', 'Ventas')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Pagos recibidos DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('FormaPago','PagoRecibido','PagoRecibidoFactura','PagoRecibidoMedio') ORDER BY name;
SELECT Codigo, TipoDato FROM dbo.FormaPago ORDER BY Codigo;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '24';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('PagosRecibidos','FormasPago');
