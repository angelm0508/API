-- POS de facturacion rapida: tabla de configuracion, codigo de barras y marca POS del articulo, modulos de permiso.
-- Requiere las tablas Almacen, Impuesto, SocioNegocio, FormaPago, Moneda, NumeracionDocumentoDet, Articulo y Modulo.
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001.
SET NOCOUNT ON;

-- ===== POS de facturacion rapida: configuracion unica, codigo de barras y marca POS del articulo, modulos de permiso =====
-- PosConfiguracion es una tabla de una sola fila (Id = 1): series de factura y de pago, almacen, impuesto, cliente por defecto,
-- forma de pago en efectivo y moneda. Los tipos y longitudes de las columnas coinciden con las tablas que referencian
-- (Almacen.Codigo nvarchar(8), Impuesto.Codigo nvarchar(8), SocioNegocio.Codigo nvarchar(15), FormaPago.Codigo nvarchar(10),
-- Moneda.Codigo nvarchar(3), NumeracionDocumentoDet.Serie int).
IF OBJECT_ID('dbo.PosConfiguracion', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PosConfiguracion (
        Id                int           NOT NULL CONSTRAINT PK_PosConfiguracion PRIMARY KEY,
        SeriePago         int           NULL,
        SerieFactura      int           NULL,
        CodAlmacen        nvarchar(8)   NULL,
        CodigoImpuesto    nvarchar(8)   NULL,
        ClienteDefecto    nvarchar(15)  NULL,
        FormaPagoEfectivo nvarchar(10)  NULL,
        MonedaDoc         nvarchar(3)   NULL,
        CONSTRAINT CK_PosConfiguracion_Id CHECK (Id = 1)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_pos_configuracion_serie_pago')
    ALTER TABLE dbo.PosConfiguracion ADD CONSTRAINT fk_pos_configuracion_serie_pago FOREIGN KEY (SeriePago) REFERENCES dbo.NumeracionDocumentoDet (Serie);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_pos_configuracion_serie_factura')
    ALTER TABLE dbo.PosConfiguracion ADD CONSTRAINT fk_pos_configuracion_serie_factura FOREIGN KEY (SerieFactura) REFERENCES dbo.NumeracionDocumentoDet (Serie);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_pos_configuracion_almacen')
    ALTER TABLE dbo.PosConfiguracion ADD CONSTRAINT fk_pos_configuracion_almacen FOREIGN KEY (CodAlmacen) REFERENCES dbo.Almacen (Codigo);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_pos_configuracion_impuesto')
    ALTER TABLE dbo.PosConfiguracion ADD CONSTRAINT fk_pos_configuracion_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto (Codigo);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_pos_configuracion_cliente')
    ALTER TABLE dbo.PosConfiguracion ADD CONSTRAINT fk_pos_configuracion_cliente FOREIGN KEY (ClienteDefecto) REFERENCES dbo.SocioNegocio (Codigo);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_pos_configuracion_forma_pago')
    ALTER TABLE dbo.PosConfiguracion ADD CONSTRAINT fk_pos_configuracion_forma_pago FOREIGN KEY (FormaPagoEfectivo) REFERENCES dbo.FormaPago (Codigo);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_pos_configuracion_moneda')
    ALTER TABLE dbo.PosConfiguracion ADD CONSTRAINT fk_pos_configuracion_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda (Codigo);

-- Fila inicial: solo la forma de pago en efectivo, y unicamente si existe el codigo EFE; el resto lo configura el usuario
IF NOT EXISTS (SELECT 1 FROM dbo.PosConfiguracion)
    INSERT INTO dbo.PosConfiguracion (Id, FormaPagoEfectivo)
    VALUES (1, CASE WHEN EXISTS (SELECT 1 FROM dbo.FormaPago WHERE Codigo = 'EFE') THEN 'EFE' ELSE NULL END);

-- Articulo: codigo de barras (unico si existe) y marca para mostrarlo en los accesos rapidos del POS
IF COL_LENGTH('dbo.Articulo', 'CodigoBarras') IS NULL
    ALTER TABLE dbo.Articulo ADD CodigoBarras NVARCHAR(50) NULL;

IF COL_LENGTH('dbo.Articulo', 'MostrarEnPos') IS NULL
    ALTER TABLE dbo.Articulo ADD MostrarEnPos CHAR(1) NOT NULL CONSTRAINT DF_Articulo_MostrarEnPos DEFAULT 'N';

-- EXEC: las columnas se agregan en este mismo lote, el CHECK y el indice no compilarian antes de existir.
-- El indice filtrado exige QUOTED_IDENTIFIER ON (sqlcmd lo trae en OFF salvo con -I).
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Articulo_MostrarEnPos' AND parent_object_id = OBJECT_ID('dbo.Articulo'))
    EXEC('ALTER TABLE dbo.Articulo ADD CONSTRAINT CK_Articulo_MostrarEnPos CHECK (MostrarEnPos IN (''S'', ''N''))');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Articulo_CodigoBarras' AND object_id = OBJECT_ID('dbo.Articulo'))
    EXEC('SET QUOTED_IDENTIFIER ON; CREATE UNIQUE INDEX UX_Articulo_CodigoBarras ON dbo.Articulo (CodigoBarras) WHERE CodigoBarras IS NOT NULL');

-- Catalogo de permisos (grupo POS). Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los acentos se guarden bien.
MERGE dbo.Modulo AS target
USING (VALUES
    (N'Pos',              N'Punto de venta',          N'POS'),
    (N'PosConfiguracion', N'Configuración del POS',   N'POS')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

PRINT 'POS aplicado.';
SELECT Id, SeriePago, SerieFactura, CodAlmacen, CodigoImpuesto, ClienteDefecto, FormaPagoEfectivo, MonedaDoc FROM dbo.PosConfiguracion;
SELECT c.name AS Columna, t.name AS Tipo, c.max_length / 2 AS Longitud, c.is_nullable AS Nulable FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID('dbo.Articulo') AND c.name IN ('CodigoBarras', 'MostrarEnPos') ORDER BY c.name;
SELECT name, filter_definition FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Articulo') AND name = 'UX_Articulo_CodigoBarras';
SELECT Codigo, Nombre, Grupo FROM dbo.Modulo WHERE Grupo = 'POS' ORDER BY Codigo;
