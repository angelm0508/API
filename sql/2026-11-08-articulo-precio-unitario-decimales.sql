-- Articulo.PrecioUnitario: de DECIMAL(19) (sin decimales, redondeaba a quetzales enteros) a DECIMAL(19,6),
-- igual que los precios de las lineas de los documentos. Idempotente: no hace nada si ya tiene escala 6.
-- Conserva los valores existentes (pasan de entero a entero con 6 decimales).
SET NOCOUNT ON;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Articulo') AND name = 'PrecioUnitario'
      AND (precision <> 19 OR scale <> 6))
    ALTER TABLE dbo.Articulo ALTER COLUMN PrecioUnitario DECIMAL(19,6) NULL;

SELECT name, TYPE_NAME(user_type_id) AS tipo, precision, scale
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.Articulo') AND name = 'PrecioUnitario';
