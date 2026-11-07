-- Stock minimo, maximo y bloqueo por articulo y almacen
IF OBJECT_ID('dbo.Articulo', 'U') IS NULL OR OBJECT_ID('dbo.Almacen', 'U') IS NULL
    THROW 50000, 'Faltan las tablas Articulo y Almacen: cree primero la base con el script completo.', 1;

IF OBJECT_ID('dbo.ArticuloAlmacen', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArticuloAlmacen (
        CodArticulo nvarchar(15)  NOT NULL,
        CodAlmacen  nvarchar(8)   NOT NULL,
        Minimo      decimal(19,6) NULL,
        Maximo      decimal(19,6) NULL,
        Bloqueado   nvarchar(1)   NOT NULL CONSTRAINT DF_ArticuloAlmacen_Bloqueado DEFAULT ('N'),
        CONSTRAINT pk_articulo_almacen PRIMARY KEY (CodArticulo, CodAlmacen),
        CONSTRAINT fk_articulo_almacen_articulo FOREIGN KEY (CodArticulo) REFERENCES dbo.Articulo(Codigo) ON DELETE CASCADE,
        CONSTRAINT fk_articulo_almacen_almacen  FOREIGN KEY (CodAlmacen)  REFERENCES dbo.Almacen(Codigo) ON DELETE CASCADE,
        CONSTRAINT CK_ArticuloAlmacen_Bloqueado CHECK (Bloqueado IN ('S','N')),
        CONSTRAINT CK_ArticuloAlmacen_Minimo CHECK (Minimo IS NULL OR Minimo >= 0),
        CONSTRAINT CK_ArticuloAlmacen_Maximo CHECK (Maximo IS NULL OR Maximo >= 0),
        CONSTRAINT CK_ArticuloAlmacen_MaxMin CHECK (Minimo IS NULL OR Maximo IS NULL OR Maximo >= Minimo)
    );
END
GO
PRINT 'Stock por almacen aplicado.';
SELECT name AS Tabla FROM sys.tables WHERE name = 'ArticuloAlmacen';
