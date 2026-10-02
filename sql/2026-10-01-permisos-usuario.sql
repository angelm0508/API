IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Modulo')
BEGIN
    CREATE TABLE Modulo (
        Codigo nvarchar(40) NOT NULL PRIMARY KEY,
        Nombre nvarchar(100) NOT NULL,
        Grupo nvarchar(40) NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UsuarioPermiso')
BEGIN
    CREATE TABLE UsuarioPermiso (
        UsuarioId int NOT NULL,
        CodigoModulo nvarchar(40) NOT NULL,
        Ver bit NOT NULL DEFAULT 0,
        Agregar bit NOT NULL DEFAULT 0,
        Actualizar bit NOT NULL DEFAULT 0,
        Eliminar bit NOT NULL DEFAULT 0,
        CONSTRAINT pk_usuario_permiso PRIMARY KEY (UsuarioId, CodigoModulo),
        CONSTRAINT fk_usuario_permiso_usuario FOREIGN KEY (UsuarioId) REFERENCES Usuario(ID),
        CONSTRAINT fk_usuario_permiso_modulo FOREIGN KEY (CodigoModulo) REFERENCES Modulo(Codigo)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Plantilla')
BEGIN
    CREATE TABLE Plantilla (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Nombre nvarchar(100) NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PlantillaPermisoDetalle')
BEGIN
    CREATE TABLE PlantillaPermisoDetalle (
        PlantillaId int NOT NULL,
        CodigoModulo nvarchar(40) NOT NULL,
        Ver bit NOT NULL DEFAULT 0,
        Agregar bit NOT NULL DEFAULT 0,
        Actualizar bit NOT NULL DEFAULT 0,
        Eliminar bit NOT NULL DEFAULT 0,
        CONSTRAINT pk_plantilla_permiso_detalle PRIMARY KEY (PlantillaId, CodigoModulo),
        CONSTRAINT fk_plantilla_permiso_plantilla FOREIGN KEY (PlantillaId) REFERENCES Plantilla(Id),
        CONSTRAINT fk_plantilla_permiso_modulo FOREIGN KEY (CodigoModulo) REFERENCES Modulo(Codigo)
    );
END

MERGE Modulo AS target
USING (VALUES
    ('Articulos', 'Artículos', 'Inventario'),
    ('Fabricantes', 'Fabricantes', 'Inventario'),
    ('GruposArticulo', 'Grupos de artículo', 'Inventario'),
    ('UnidadesMedida', 'Unidades de medida', 'Inventario'),
    ('GruposUnidadMedida', 'Grupo unidades de medida', 'Inventario'),
    ('Almacenes', 'Almacenes', 'Inventario'),
    ('Existencias', 'Existencias', 'Inventario'),
    ('EntradasMercancia', 'Entradas de mercancía', 'Inventario'),
    ('SalidasMercancia', 'Salidas de mercancía', 'Inventario'),
    ('SociosNegocio', 'Socios de Negocio', 'SociosNegocio'),
    ('GruposSn', 'Grupos SN', 'SociosNegocio'),
    ('ListadosPrecio', 'Listado de Precios', 'SociosNegocio'),
    ('NumeracionDocumento', 'Numeración de documentos', 'Documentos'),
    ('Cotizaciones', 'Cotizaciones', 'Ventas'),
    ('Pedidos', 'Pedidos', 'Ventas'),
    ('Entregas', 'Entregas', 'Ventas'),
    ('Facturas', 'Facturas', 'Ventas'),
    ('PedidosCompra', 'Pedidos de compra', 'Compras'),
    ('EntregasCompra', 'Entregas de compra', 'Compras'),
    ('FacturasCompra', 'Facturas de compra', 'Compras'),
    ('Usuarios', 'Usuarios', 'Administracion')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT Codigo, Nombre, Grupo FROM Modulo ORDER BY Grupo, Nombre;
