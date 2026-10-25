-- Pagos efectuados (objeto 46): pagos a proveedores sobre facturas de compra y notas de debito de proveedor.
-- Requiere sql/2026-10-23-pagos-recibidos.sql (tabla FormaPago y sus semillas).
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

IF OBJECT_ID('dbo.FormaPago') IS NULL THROW 50000, 'Falta la tabla FormaPago: ejecute antes sql/2026-10-23-pagos-recibidos.sql', 1;

-- ===== PagoEfectuado (ObjType 46) =====
IF OBJECT_ID('dbo.PagoEfectuado', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoEfectuado (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NOT NULL CONSTRAINT DF_PagoEfectuado_Cancelado DEFAULT ('N') CONSTRAINT CK_PagoEfectuado_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NOT NULL CONSTRAINT DF_PagoEfectuado_NumManual DEFAULT ('N') CONSTRAINT CK_PagoEfectuado_NumManual CHECK (NumManual IN ('S','N')),
        TipoObjeto     nvarchar(11)  NOT NULL CONSTRAINT DF_PagoEfectuado_TipoObjeto DEFAULT ('46') CONSTRAINT CK_PagoEfectuado_TipoObjeto CHECK (TipoObjeto IN ('46')),
        FechaDoc       datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        TotalPago      decimal(19,6) NOT NULL CONSTRAINT CK_PagoEfectuado_Total CHECK (TotalPago > 0),
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_pago_efectuado PRIMARY KEY (Entry),
        CONSTRAINT uq_pago_efectuado_serie_numdoc UNIQUE (Serie, NumDoc),
        CONSTRAINT fk_pago_efectuado_serie FOREIGN KEY (Serie) REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_pago_efectuado_sn    FOREIGN KEY (CodigoSN) REFERENCES dbo.SocioNegocio(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PagoEfectuado_CodigoSN' AND object_id = OBJECT_ID('dbo.PagoEfectuado'))
    CREATE NONCLUSTERED INDEX IX_PagoEfectuado_CodigoSN ON dbo.PagoEfectuado (CodigoSN) INCLUDE (Cancelado);

-- ===== PagoEfectuadoFactura (a que documentos se aplica; FK a FacturaCompra cubre facturas de compra y notas de debito de proveedor) =====
IF OBJECT_ID('dbo.PagoEfectuadoFactura', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoEfectuadoFactura (
        Entry              int           NOT NULL,
        NoLinea            int           NOT NULL,
        FacturaCompraEntry int           NOT NULL,
        MontoAplicado      decimal(19,6) NOT NULL CONSTRAINT CK_PagoEfectuadoFactura_Monto CHECK (MontoAplicado > 0),
        CONSTRAINT pk_pago_efectuado_factura PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT uq_pago_efectuado_factura_doc UNIQUE (Entry, FacturaCompraEntry),
        CONSTRAINT fk_pago_efectuado_factura_pago    FOREIGN KEY (Entry)              REFERENCES dbo.PagoEfectuado(Entry),
        CONSTRAINT fk_pago_efectuado_factura_factura FOREIGN KEY (FacturaCompraEntry) REFERENCES dbo.FacturaCompra(Entry)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PagoEfectuadoFactura_Factura' AND object_id = OBJECT_ID('dbo.PagoEfectuadoFactura'))
    CREATE NONCLUSTERED INDEX IX_PagoEfectuadoFactura_Factura ON dbo.PagoEfectuadoFactura (FacturaCompraEntry) INCLUDE (MontoAplicado);

-- ===== PagoEfectuadoMedio (con que se pago) =====
IF OBJECT_ID('dbo.PagoEfectuadoMedio', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PagoEfectuadoMedio (
        Entry        int           NOT NULL,
        NoLinea      int           NOT NULL,
        CodFormaPago nvarchar(10)  NOT NULL,
        Monto        decimal(19,6) NOT NULL CONSTRAINT CK_PagoEfectuadoMedio_Monto CHECK (Monto > 0),
        Referencia   nvarchar(50)  NULL,
        Banco        nvarchar(60)  NULL,
        FechaCheque  datetime      NULL,
        CONSTRAINT pk_pago_efectuado_medio PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_pago_efectuado_medio_pago  FOREIGN KEY (Entry)        REFERENCES dbo.PagoEfectuado(Entry),
        CONSTRAINT fk_pago_efectuado_medio_forma FOREIGN KEY (CodFormaPago) REFERENCES dbo.FormaPago(Codigo)
    );
END

-- ===== Numeracion (serie "Primario" y fila padre para la pantalla "Numeracion de documentos") =====
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '46')
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('46', (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet), 'Primario', 'N', '--', 'D', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '46' AND SubTipoDoc = '--')
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SubTipoDoc, DocAlias, SerieDfct)
    SELECT '46', '--', 'Pago efectuado', MIN(Serie) FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '46';

-- ===== Catalogo de permisos (grupo Compras); FormasPago ya existe y no cambia =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('PagosEfectuados', 'Pagos efectuados', 'Compras')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'Pagos efectuados DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('PagoEfectuado','PagoEfectuadoFactura','PagoEfectuadoMedio') ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '46';
SELECT CodigoObj, SubTipoDoc, DocAlias, SerieDfct FROM dbo.NumeracionDocumento WHERE CodigoObj = '46';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo = 'PagosEfectuados';
