-- Garantiza a nivel de base de datos que el Codigo de Usuario sea unico (el login busca por Codigo y
-- toma la primera coincidencia; dos filas con el mismo codigo serian una cuenta ambigua).
-- Los usuarios eliminados logicamente (Eliminado = 'S') siguen ocupando su codigo: no se reutiliza.
-- Idempotente: no hace nada si el indice ya existe. Aborta con un mensaje claro si ya hay duplicados.

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Usuario_Codigo' AND object_id = OBJECT_ID('dbo.Usuario'))
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Usuario GROUP BY Codigo HAVING COUNT(*) > 1)
    BEGIN
        SELECT Codigo, COUNT(*) AS Repeticiones FROM dbo.Usuario GROUP BY Codigo HAVING COUNT(*) > 1;
        THROW 50001, 'Hay codigos de usuario duplicados (ver resultado anterior). Corrijalos antes de crear UX_Usuario_Codigo.', 1;
    END

    CREATE UNIQUE INDEX UX_Usuario_Codigo ON dbo.Usuario (Codigo);
END

SELECT name, is_unique FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Usuario') AND name = 'UX_Usuario_Codigo';
