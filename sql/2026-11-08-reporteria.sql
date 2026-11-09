-- Reporteria dinamica: vistas guardadas por usuario y modulos de permiso de los reportes.
-- Idempotente: cada objeto se crea solo si no existe.
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;

IF OBJECT_ID('dbo.ReporteVista', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReporteVista (
        Entry              int identity(1,1) NOT NULL,
        UsuarioId          int           NOT NULL,
        CodigoReporte      nvarchar(50)  NOT NULL,
        Nombre             nvarchar(100) NOT NULL,
        Configuracion      nvarchar(max) NOT NULL,
        FechaCreacion      datetime      NOT NULL CONSTRAINT DF_ReporteVista_FechaCreacion DEFAULT (GETDATE()),
        FechaActualizacion datetime      NOT NULL CONSTRAINT DF_ReporteVista_FechaActualizacion DEFAULT (GETDATE()),
        CONSTRAINT pk_reporte_vista PRIMARY KEY (Entry),
        CONSTRAINT fk_reporte_vista_usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario(ID) ON DELETE CASCADE,
        CONSTRAINT CK_ReporteVista_Configuracion CHECK (ISJSON(Configuracion) = 1),
        CONSTRAINT uq_reporte_vista UNIQUE (UsuarioId, CodigoReporte, Nombre)
    );
END

-- Catalogo de permisos: un modulo por familia de reportes
MERGE dbo.Modulo AS target
USING (VALUES
    ('ReportesVentas',      'Reportes de ventas',         'Reportes'),
    ('ReportesCompras',     'Reportes de compras',        'Reportes'),
    ('ReportesInventario',  'Reportes de inventario',     'Reportes'),
    ('ReportesCobrosPagos', 'Reportes de cobros y pagos', 'Reportes')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT name FROM sys.tables WHERE name = 'ReporteVista';
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Grupo = 'Reportes' ORDER BY Codigo;
