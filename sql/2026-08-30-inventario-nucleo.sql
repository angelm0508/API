-- INV-1: nucleo de inventario multi-almacen.
-- Idempotente: cada objeto se crea solo si no existe.
SET NOCOUNT ON;

-- ===== ExistenciaArticulo: cantidad por (articulo, almacen) =====
IF OBJECT_ID('dbo.ExistenciaArticulo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExistenciaArticulo (
        CodArticulo        nvarchar(15)  NOT NULL,
        CodAlmacen         nvarchar(8)   NOT NULL,
        Disponible         decimal(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Disponible   DEFAULT (0),
        Comprometido       decimal(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Comprometido DEFAULT (0),
        Pedido             decimal(19,6) NOT NULL CONSTRAINT DF_ExistenciaArticulo_Pedido       DEFAULT (0),
        FechaActualizacion datetime      NOT NULL CONSTRAINT DF_ExistenciaArticulo_Fecha        DEFAULT (getdate()),
        RowVersion         rowversion    NOT NULL,
        CONSTRAINT pk_existencia_articulo PRIMARY KEY (CodArticulo, CodAlmacen),
        CONSTRAINT fk_existencia_articulo FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_existencia_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo)
    );
END

-- ===== MovimientoInventario: kardex append-only =====
IF OBJECT_ID('dbo.MovimientoInventario', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MovimientoInventario (
        Entry              int identity(1,1) NOT NULL,
        TipoDoc            nvarchar(20)  NOT NULL,
        DocEntry           int           NOT NULL,
        DocLinea           int           NOT NULL,
        CodArticulo        nvarchar(15)  NOT NULL,
        CodAlmacen         nvarchar(8)   NOT NULL,
        Fecha              datetime      NOT NULL,
        CantidadEntra      decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_CantEntra DEFAULT (0),
        CantidadSale       decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_CantSale  DEFAULT (0),
        PrecioUnitario     decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Precio    DEFAULT (0),
        CostoUnitario      decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Costo     DEFAULT (0),
        ValorMovimiento    decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Valor     DEFAULT (0),
        VariacionPrecio    decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_Variacion DEFAULT (0),
        SaldoCantidad      decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoCant DEFAULT (0),
        SaldoCostoPromedio decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoCP   DEFAULT (0),
        SaldoValor         decimal(19,6) NOT NULL CONSTRAINT DF_MovInv_SaldoVal  DEFAULT (0),
        MovReversaDe       int           NULL,
        CONSTRAINT pk_movimiento_inventario PRIMARY KEY (Entry),
        CONSTRAINT fk_movimiento_articulo FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo),
        CONSTRAINT fk_movimiento_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo),
        CONSTRAINT fk_movimiento_reversa  FOREIGN KEY (MovReversaDe) REFERENCES dbo.MovimientoInventario(Entry)
    );
    CREATE INDEX ix_movimiento_articulo_fecha ON dbo.MovimientoInventario (CodArticulo, Fecha, Entry);
    CREATE INDEX ix_movimiento_origen         ON dbo.MovimientoInventario (TipoDoc, DocEntry);
END

-- ===== Columnas nuevas en Articulo =====
IF COL_LENGTH('dbo.Articulo', 'MetodoValuacion') IS NULL
    ALTER TABLE dbo.Articulo ADD MetodoValuacion nvarchar(1) NOT NULL
        CONSTRAINT DF_Articulo_MetodoValuacion DEFAULT ('P')
        CONSTRAINT CK_Articulo_MetodoValuacion CHECK (MetodoValuacion IN ('P','E'));
IF COL_LENGTH('dbo.Articulo', 'CostoPromedio') IS NULL
    ALTER TABLE dbo.Articulo ADD CostoPromedio decimal(19,6) NOT NULL CONSTRAINT DF_Articulo_CostoPromedio DEFAULT (0);
IF COL_LENGTH('dbo.Articulo', 'CostoEstandar') IS NULL
    ALTER TABLE dbo.Articulo ADD CostoEstandar decimal(19,6) NOT NULL CONSTRAINT DF_Articulo_CostoEstandar DEFAULT (0);
IF COL_LENGTH('dbo.Articulo', 'ValorInventario') IS NULL
    ALTER TABLE dbo.Articulo ADD ValorInventario decimal(19,6) NOT NULL CONSTRAINT DF_Articulo_ValorInventario DEFAULT (0);

PRINT 'INV-1 DDL aplicado.';
SELECT name FROM sys.tables WHERE name IN ('ExistenciaArticulo','MovimientoInventario') ORDER BY name;
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
 WHERE TABLE_NAME='Articulo' AND COLUMN_NAME IN ('MetodoValuacion','CostoPromedio','CostoEstandar','ValorInventario')
 ORDER BY COLUMN_NAME;
