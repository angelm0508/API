# Notas de débito dentro de Factura/FacturaCompra (DocSubType) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guardar las notas de débito de cliente en `Factura`/`FacturaDetalle` y las de proveedor en `FacturaCompra`/`FacturaCompraDetalle`, diferenciadas por `DocSubType` (`'ND'`/`'DN'`), con modo por artículos (mueve inventario como su factura) o por concepto, conservando endpoints, permisos y pantallas de notas de débito.

**Architecture:** `NotaDebitoDomain`/`NotaDebitoCompraDomain` conservan sus reglas pero leen y escriben en los repositorios de `Factura*`, filtrando siempre por su subtipo; `FacturaDomain`/`FacturaCompraDomain`/`FacturaDetalleDomain` filtran `'--'` y validan que la serie sea de factura. `NumeracionDocumento` gana una clave compuesta opcional `"CodigoObj:SubTipoDoc"` (`"6:ND"`). Las tablas `NotaDebito*` y sus clases se eliminan. La pantalla Web de notas de débito suma un selector "Tipo de nota" (Artículos / Concepto).

**Tech Stack:** .NET 7 API (EF Core 7 DB-first, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server, bash/sed/node para derivaciones y parches.

**Spec:** `docs/superpowers/specs/2026-10-17-notas-debito-en-factura-design.md` (repo API, commit a90590e). Documentos relacionados ya implementados: notas de débito (`2026-10-04-notas-debito-design.md`) y notas de crédito (`2026-10-04-notas-credito-design.md`).

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. Línea base de pruebas: **1331** pasando. Verificar antes de empezar: `git log --oneline -3` debe mostrar el commit de la spec (`a90590e`) o uno posterior de este plan.
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`, HEAD `071bdc2` (o posterior). Línea base: `dotnet build Web.slnx` con 0 errores y 0 advertencias en un worktree limpio.
- Build/test con `BaseOutputPath` en el scratchpad (ejecutar los de la API **desde PowerShell**; en bash la barra final rompe el quoting):
  - API: `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`
- SQL de pruebas: SOLO bases temporales (`API_DB_ND_A`, `API_DB_ND_B`, `API_DB_ND_C`, `API_DB_ND_D`, `API_DB_SMOKE`); **jamás** `API_DB_TEST`. Contraseña de `sa` extraída de `API.Service.WebApi\appsettings.json` a una variable; nunca se imprime ni se escribe en archivos ni en reportes. Cada base temporal se elimina al terminar (aunque falle algo).

## Global Constraints

Cada tarea las incluye implícitamente.

- **Modelo de datos:** `Factura.DocSubType nvarchar(2) NOT NULL DEFAULT '--'` con `CHECK IN ('--','ND')`; `FacturaCompra.DocSubType` con `CHECK IN ('--','DN')`. `DocType nvarchar(1) NOT NULL DEFAULT 'I'` con `CHECK IN ('I','S')` (`I` = artículos, `S` = concepto) en ambas tablas; `DocSubType <> '--'` o `DocType = 'I'` (una factura normal siempre es por artículos). `Factura.BaseTipo`: factura normal `-1`; nota `IN (-1, 6)` (compra `-1, 13`). `FacturaDetalle`/`FacturaCompraDetalle`: `CodArticulo`/`CodAlmacen` aceptan NULL. Constraints con nombre: `CK_Factura_DocSubType`, `CK_Factura_DocType`, `CK_Factura_DocType_Subtipo`, `CK_Factura_Base` (y `CK_FacturaCompra_*`); índices `IX_Factura_DocSubType` y `IX_FacturaCompra_DocSubType` sobre `(DocSubType, CodigoSN)`.
- **Identidad de la nota:** `TipoObjeto` de la nota = `'6'` (venta) / `'13'` (compra), el mismo de la factura. Sin objetos 7/14. `DocSubType` fija el tipo: `'ND'` (cliente) / `'DN'` (proveedor). El `Entry` es único entre facturas y notas (misma tabla).
- **Numeración:** filas `NumeracionDocumento(6,'ND','Nota Debito Cliente')` y `(13,'DN','Nota Debito Prov.')`; sus series (Manual y Primario) tienen `CodigoObj` `'6'`/`'13'` y `SubTipoDoc` `'ND'`/`'DN'`. Una serie de factura (`SubTipoDoc '--'`) no sirve para una nota y viceversa; `FacturaDomain` también valida `CodigoObj`+`SubTipoDoc`. Clave compuesta en la API de numeración: `"CodigoObj"` (comportamiento actual) o `"CodigoObj:SubTipoDoc"`.
- **Modo de la nota (`DocType`):** obligatorio al crear (`'I'` o `'S'`), inmutable. **Concepto (`'S'`)**: sin artículo ni almacén (se rechazan si llegan), `Descripcion` obligatoria, sin asiento de inventario, `EstadoInv = 'C'`. **Artículos (`'I'`)**: `CodArticulo` y `CodAlmacen` existentes por línea, `EstadoInv = 'A'`, asiento con `IInventarioAsientoService.AsentarAsync` (`TipoDoc` `'6'`/`'13'`, `DocEntry` = `Entry` de la nota, `DocLinea` = `NoLinea`, `PrecioUnitario` = `Precio`), cantidad **negativa** en la nota de cliente (salida) y **positiva** en la de proveedor (entrada); las líneas de la nota llevan `BaseEntry` nulo. Cancelar una nota `'I'` llama `RevertirAsync('6'/'13', Entry)` dentro de la misma transacción; en ambos modos pone `Cancelado='S'`, `EstadoInv='C'`, `FechaCancelado`. `EstadoInv` se fija SIEMPRE de forma explícita (el default `'A'` de la base no decide).
- **Reglas que se conservan de la nota de débito actual:** serie existente/no bloqueada, numeración manual o automática asignada al final, socio tipo `C`/`P` activo, moneda existente, ≥ 1 línea, cantidad > 0, precio ≥ 0, descuento 0–100, impuesto existente, factura base opcional (no cancelada, mismo socio, solo referencia), líneas inmutables, cancelar exige además `Accion.Eliminar`, eliminar exige cancelar antes. Los totales de la nota de débito siguen viniendo del cliente.
- **Aislamiento:** la API de facturas no ve notas (`DocSubType <> '--'`) y la de notas no ve facturas: un `Entry` de la otra clase responde "no existe" / `false` / lista vacía. La factura base de una nota de débito es una factura normal (`'--'`); una nota de débito **no** puede basarse en otra.
- **Notas de crédito:** su factura base puede ser una factura o una nota de débito (misma tabla, `BaseTipo` 6/13). Siguen siendo monetarias y no tocan inventario.
- **Mensajes en español exactamente como aparecen** en los bloques de código del plan (los tests los verifican literalmente).
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` puede aparecer modificado: NUNCA commitearlo. La Web tiene trabajo ajeno sin commitear (`Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`): jamás se stagean, commitean ni modifican. Sin push hasta aprobación final del usuario. Cuando una derivación (`sed`/`node`) sobrescribe un archivo ya commiteado, la tarea lo indica y verifica antes que el archivo no tenga cambios sin commitear (`git diff --quiet -- <archivo>`).
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo/EOL:** se conserva el fin de línea de cada archivo existente que se edita (CRLF/LF); los archivos nuevos en LF; sin churn de EOL (verificar con `git diff --stat`: una edición pequeña no puede mostrar cientos de líneas).

## File Structure

**Datos (API repo, carpeta `sql/`)**
- Crear `sql/2026-10-17-notas-debito-en-factura.sql` (migración idempotente); borrar `sql/2026-10-04-notas-debito.sql`; modificar `sql/crear-api-db-test-completo.sql` (esquema final).

**API — entidades y contexto**
- Modificar `API.Domain.Entity/Models/Factura.cs`, `FacturaCompra.cs` (propiedades `DocSubType`, `DocType`), `ApiDbTestContext.cs` (mapeo de las dos propiedades; al final se eliminan los bloques `NotaDebito*`), `SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs`, `Impuesto.cs` (se quitan las colecciones `NotaDebito*`).
- Eliminar `API.Domain.Entity/Models/NotaDebito.cs`, `NotaDebitoDetalle.cs`, `NotaDebitoCompra.cs`, `NotaDebitoCompraDetalle.cs`; `API.Infraestructure.Repository/NotaDebito*Repositorio.cs` (4); `API.Service.WebApi.Tests/Domain/NotaDebitoModeloTests.cs`.
- Crear `API.Domain.Entity/NumeracionClave.cs` (compone/separa `"CodigoObj:SubTipoDoc"`); modificar `API.Infraestructure.Repository/NumeracionDocumentoRepositorio.cs`, `API.Domain.Core/NumeracionDocumentoDomain.cs`.

**API — dominios y aplicación**
- Modificar `API.Domain.Core/FacturaDomain.cs`, `FacturaCompraDomain.cs`, `FacturaDetalleDomain.cs`, `FacturaCompraDetalleDomain.cs`.
- Reescribir `API.Domain.Core/NotaDebitoDomain.cs`, `NotaDebitoCompraDomain.cs`; `API.Domain.Interface/INotaDebitoDomain.cs`, `INotaDebitoCompraDomain.cs`; `API.Application.Main/NotaDebitoApplication.cs`, `NotaDebitoCompraApplication.cs`; DTOs `API.Application.DTO/notaDebito*/…` (se agregan `DocType`, `CodArticulo`, `CodAlmacen`); `API.Transversal.Mapper/PerfilMapeo.cs`; `API.Service.WebApi/Startup.cs` (se quitan los repos `NotaDebito*`).
- Modificar `API.Application.DTO/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs` y `notaCreditoCompra/…` (campo `DocSubType`), `API.Application.Main/NotaCreditoApplication.cs`, `NotaCreditoCompraApplication.cs`.
- Pruebas: reescribir `Domain/NotaDebitoDomainTests.cs`, `Domain/NotaDebitoCompraDomainTests.cs`; adaptar `Application/NotaDebito*ApplicationTests.cs`, `Application/NotaDebitoMapeoTests.cs`; ampliar `Domain/FacturaDomainTests.cs`, `FacturaCompraDomainTests.cs`, `FacturaDetalleDomainTests.cs`, `FacturaCompraDetalleDomainTests.cs`; crear `Domain/FacturaModeloTests.cs`, `Domain/NumeracionClaveTests.cs`, ampliar pruebas de numeración y notas de crédito.

**Web**
- Modificar `Web.ApiClient/Dtos/NotaDebito/*`, `NotaDebitoCompra/*`, `NotaCredito/NotaCreditoFacturaBaseResumenDTO.cs`, `NotaCreditoCompra/…`; `Web.UI/Controllers/NotasDebitoController.cs`, `NotasDebitoCompraController.cs`, `NumeracionDocumentoController.cs`; `Web.UI/Views/NotasDebito/_Form.cshtml`, `NotasDebitoCompra/_Form.cshtml`, `Views/NumeracionDocumento/_Form.cshtml`; `Web.UI/wwwroot/js/notasdebito.js`, `notasdebitocompra.js`, `numeracionDocumento.js`, `notascredito.js`, `notascreditocompra.js`.

## Fase 1 — Datos

### Task 1: Migración SQL y esquema final

**Files:**
- Create: `sql/2026-10-17-notas-debito-en-factura.sql`
- Delete: `sql/2026-10-04-notas-debito.sql` (con `git rm`)
- Modify: `sql/crear-api-db-test-completo.sql`

**Interfaces:**
- Consumes: esquema actual de `Factura`, `FacturaDetalle`, `FacturaCompra`, `FacturaCompraDetalle`, `NumeracionDocumento`, `NumeracionDocumentoDet`, `Modulo` (ver complete script).
- Produces: columnas `Factura.DocSubType`/`DocType` y `FacturaCompra.DocSubType`/`DocType` con las constraints e índices de las Global Constraints; series de notas de débito bajo `(6,'ND')` y `(13,'DN')`; módulos `NotasDebito`/`NotasDebitoCompra`; sin tablas `NotaDebito*`.

**Precondición verificable:** `git status --porcelain` en el repo API solo muestra ` M API.Service.WebApi/appsettings.json`; `sql/2026-10-04-notas-debito.sql` y `sql/2026-10-05-notas-credito.sql` existen; `grep -n "CREATE TABLE NotaDebito" sql/crear-api-db-test-completo.sql` muestra 4 tablas.

- [ ] **Step 1: Crear la migración**

Crear `sql/2026-10-17-notas-debito-en-factura.sql` (UTF-8 sin BOM, fin de línea LF) con exactamente este contenido:

```sql
-- Notas de debito dentro de Factura (DocSubType 'ND') y FacturaCompra (DocSubType 'DN'), como SAP B1:
-- la nota de debito comparte tabla con la factura y se distingue por DocSubType. DocType elige el modo
-- ('I' = por articulos, mueve inventario; 'S' = por concepto, sin inventario).
-- Idempotente. Reemplaza a 2026-10-04-notas-debito.sql (que ya no debe ejecutarse).
-- Archivo UTF-8: ejecutar con sqlcmd -f 65001 para que los nombres de modulo con acentos se guarden bien.
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 0) Precondicion: esta migracion no copia datos de las tablas NotaDebito*. Si tienen filas, no cambia nada.
IF (OBJECT_ID('dbo.NotaDebito', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.NotaDebito))
   OR (OBJECT_ID('dbo.NotaDebitoDetalle', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.NotaDebitoDetalle))
   OR (OBJECT_ID('dbo.NotaDebitoCompra', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.NotaDebitoCompra))
   OR (OBJECT_ID('dbo.NotaDebitoCompraDetalle', 'U') IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.NotaDebitoCompraDetalle))
    THROW 50001, N'Las tablas NotaDebito* tienen filas: esta migracion no copia datos. No se cambio nada.', 1;

DECLARE @ck sysname;

-- 1) Factura (cliente): DocSubType 'ND', BaseTipo 6
IF COL_LENGTH('dbo.Factura', 'DocSubType') IS NULL
    ALTER TABLE dbo.Factura ADD DocSubType nvarchar(2) NOT NULL CONSTRAINT DF_Factura_DocSubType DEFAULT ('--');
IF COL_LENGTH('dbo.Factura', 'DocType') IS NULL
    ALTER TABLE dbo.Factura ADD DocType nvarchar(1) NOT NULL CONSTRAINT DF_Factura_DocType DEFAULT ('I');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_DocSubType' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_DocSubType CHECK (DocSubType IN (''--'', ''ND''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_DocType' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_DocType CHECK (DocType IN (''I'', ''S''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_DocType_Subtipo' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_DocType_Subtipo CHECK (DocSubType <> ''--'' OR DocType = ''I'')');

-- La restriccion original de BaseTipo (IN (-1)) se creo sin nombre: se busca por columna y se reemplaza.
SET @ck = NULL;
SELECT @ck = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('dbo.Factura') AND c.name = 'BaseTipo';
IF @ck IS NOT NULL EXEC(N'ALTER TABLE dbo.Factura DROP CONSTRAINT ' + QUOTENAME(@ck));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Factura_Base' AND parent_object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'ALTER TABLE dbo.Factura ADD CONSTRAINT CK_Factura_Base CHECK ((DocSubType = ''--'' AND BaseTipo = -1) OR (DocSubType = ''ND'' AND BaseTipo IN (-1, 6)))');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Factura_DocSubType' AND object_id = OBJECT_ID('dbo.Factura'))
    EXEC(N'CREATE NONCLUSTERED INDEX IX_Factura_DocSubType ON dbo.Factura (DocSubType, CodigoSN)');

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaDetalle') AND name = 'CodArticulo' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaDetalle ALTER COLUMN CodArticulo nvarchar(15) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaDetalle') AND name = 'CodAlmacen' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaDetalle ALTER COLUMN CodAlmacen nvarchar(8) NULL;

-- 2) FacturaCompra (proveedor): DocSubType 'DN', BaseTipo 13
IF COL_LENGTH('dbo.FacturaCompra', 'DocSubType') IS NULL
    ALTER TABLE dbo.FacturaCompra ADD DocSubType nvarchar(2) NOT NULL CONSTRAINT DF_FacturaCompra_DocSubType DEFAULT ('--');
IF COL_LENGTH('dbo.FacturaCompra', 'DocType') IS NULL
    ALTER TABLE dbo.FacturaCompra ADD DocType nvarchar(1) NOT NULL CONSTRAINT DF_FacturaCompra_DocType DEFAULT ('I');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_DocSubType' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_DocSubType CHECK (DocSubType IN (''--'', ''DN''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_DocType' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_DocType CHECK (DocType IN (''I'', ''S''))');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_DocType_Subtipo' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_DocType_Subtipo CHECK (DocSubType <> ''--'' OR DocType = ''I'')');

SET @ck = NULL;
SELECT @ck = cc.name
FROM sys.check_constraints cc
JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('dbo.FacturaCompra') AND c.name = 'BaseTipo';
IF @ck IS NOT NULL EXEC(N'ALTER TABLE dbo.FacturaCompra DROP CONSTRAINT ' + QUOTENAME(@ck));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_FacturaCompra_Base' AND parent_object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'ALTER TABLE dbo.FacturaCompra ADD CONSTRAINT CK_FacturaCompra_Base CHECK ((DocSubType = ''--'' AND BaseTipo = -1) OR (DocSubType = ''DN'' AND BaseTipo IN (-1, 13)))');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FacturaCompra_DocSubType' AND object_id = OBJECT_ID('dbo.FacturaCompra'))
    EXEC(N'CREATE NONCLUSTERED INDEX IX_FacturaCompra_DocSubType ON dbo.FacturaCompra (DocSubType, CodigoSN)');

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaCompraDetalle') AND name = 'CodArticulo' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaCompraDetalle ALTER COLUMN CodArticulo nvarchar(15) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.FacturaCompraDetalle') AND name = 'CodAlmacen' AND is_nullable = 0)
    ALTER TABLE dbo.FacturaCompraDetalle ALTER COLUMN CodAlmacen nvarchar(8) NULL;

-- 3) Numeracion: las series de las notas cuelgan del objeto de la factura con SubTipoDoc 'ND' / 'DN'.
--    El script anterior de notas de debito las dejaba bajo los objetos 7 y 14: se reasignan.
DECLARE @man int, @pri int;

UPDATE dbo.NumeracionDocumentoDet SET CodigoObj = '6',  SubTipoDoc = 'ND' WHERE CodigoObj = '7'  AND SubTipoDoc = '--';
UPDATE dbo.NumeracionDocumentoDet SET CodigoObj = '13', SubTipoDoc = 'DN' WHERE CodigoObj = '14' AND SubTipoDoc = '--';

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '6' AND SubTipoDoc = 'ND')
   AND EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '7' AND SubTipoDoc = '--')
    UPDATE dbo.NumeracionDocumento SET CodigoObj = '6', SubTipoDoc = 'ND' WHERE CodigoObj = '7' AND SubTipoDoc = '--';
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '13' AND SubTipoDoc = 'DN')
   AND EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '14' AND SubTipoDoc = '--')
    UPDATE dbo.NumeracionDocumento SET CodigoObj = '13', SubTipoDoc = 'DN' WHERE CodigoObj = '14' AND SubTipoDoc = '--';

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '6' AND SubTipoDoc = 'ND')
BEGIN
    SET @man = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @pri = @man + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('6', @man, 'Manual', 'N', 'ND', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('6', @pri, 'Primario', 'N', 'ND', 'D', 1, 1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '6' AND SubTipoDoc = 'ND')
BEGIN
    SET @pri = (SELECT TOP 1 Serie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '6' AND SubTipoDoc = 'ND' AND NombreSerie = 'Primario' ORDER BY Serie);
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
    VALUES ('6', @pri, 'Nota Debito Cliente', 'ND');
END

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '13' AND SubTipoDoc = 'DN')
BEGIN
    SET @man = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @pri = @man + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('13', @man, 'Manual', 'N', 'DN', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('13', @pri, 'Primario', 'N', 'DN', 'D', 1, 1);
END
IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '13' AND SubTipoDoc = 'DN')
BEGIN
    SET @pri = (SELECT TOP 1 Serie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '13' AND SubTipoDoc = 'DN' AND NombreSerie = 'Primario' ORDER BY Serie);
    INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
    VALUES ('13', @pri, 'Nota Debito Prov.', 'DN');
END

-- 4) Las tablas propias de notas de debito (vacias, verificado arriba) ya no se usan.
IF OBJECT_ID('dbo.NotaDebitoDetalle', 'U') IS NOT NULL DROP TABLE dbo.NotaDebitoDetalle;
IF OBJECT_ID('dbo.NotaDebito', 'U') IS NOT NULL DROP TABLE dbo.NotaDebito;
IF OBJECT_ID('dbo.NotaDebitoCompraDetalle', 'U') IS NOT NULL DROP TABLE dbo.NotaDebitoCompraDetalle;
IF OBJECT_ID('dbo.NotaDebitoCompra', 'U') IS NOT NULL DROP TABLE dbo.NotaDebitoCompra;

-- 5) Catalogo de permisos (los modulos no cambian)
MERGE dbo.Modulo AS target
USING (VALUES
    ('NotasDebito', 'Notas de débito', 'Ventas'),
    ('NotasDebitoCompra', 'Notas de débito de compra', 'Compras')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN MATCHED AND (target.Nombre <> source.Nombre OR target.Grupo <> source.Grupo) THEN
    UPDATE SET Nombre = source.Nombre, Grupo = source.Grupo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT name FROM sys.tables WHERE name LIKE 'NotaDebito%' ORDER BY name;   -- debe quedar vacio
SELECT CodigoObj, Serie, NombreSerie, SubTipoDoc FROM dbo.NumeracionDocumentoDet WHERE SubTipoDoc IN ('ND', 'DN') ORDER BY Serie;
SELECT CodigoObj, SerieDfct, DocAlias, SubTipoDoc FROM dbo.NumeracionDocumento WHERE SubTipoDoc IN ('ND', 'DN');
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('NotasDebito', 'NotasDebitoCompra');
```

- [ ] **Step 2: Actualizar el script completo (esquema final)**

Editar `sql/crear-api-db-test-completo.sql` con estos cambios (conservar su BOM/EOL: comprobar antes con `file sql/crear-api-db-test-completo.sql` y `head -c 3 sql/crear-api-db-test-completo.sql | xxd`; usar `Edit` o un script `node` que preserve el EOL; el diff final de `git diff --stat` debe mostrar solo decenas de líneas, no el archivo completo):

1. **`CREATE TABLE Factura(`** (≈ línea 1264): después de la línea `TipoObjeto NVARCHAR(11) CHECK (TipoObjeto IN ('6')) DEFAULT '6', -- 6 = Factura` agregar:
   ```sql
   	DocSubType NVARCHAR(2) NOT NULL CONSTRAINT DF_Factura_DocSubType DEFAULT ('--') CONSTRAINT CK_Factura_DocSubType CHECK (DocSubType IN ('--', 'ND')), -- -- = factura, ND = nota de debito de cliente
   	DocType NVARCHAR(1) NOT NULL CONSTRAINT DF_Factura_DocType DEFAULT ('I') CONSTRAINT CK_Factura_DocType CHECK (DocType IN ('I', 'S')), -- I = articulos (mueve inventario), S = concepto (la factura normal es siempre I)
   ```
   reemplazar `BaseTipo INT CHECK(BaseTipo IN (-1)) DEFAULT -1,` por `BaseTipo INT DEFAULT -1,` y, justo antes de `CONSTRAINT pk_factura PRIMARY KEY(Entry),`, agregar:
   ```sql
   	CONSTRAINT CK_Factura_DocType_Subtipo CHECK (DocSubType <> '--' OR DocType = 'I'),
   	CONSTRAINT CK_Factura_Base CHECK ((DocSubType = '--' AND BaseTipo = -1) OR (DocSubType = 'ND' AND BaseTipo IN (-1, 6))),
   ```
   Actualizar el comentario de cabecera `BaseTipo se agrega CHECK(BaseTipo IN (-1)` por `BaseTipo: CK_Factura_Base`. Después del `CREATE TABLE Factura(...)` (antes del siguiente comentario) agregar `CREATE NONCLUSTERED INDEX IX_Factura_DocSubType ON Factura (DocSubType, CodigoSN);`.
2. **`CREATE TABLE FacturaDetalle(`**: `CodArticulo NVARCHAR(15) NOT NULL, -- FK` → `CodArticulo NVARCHAR(15), -- FK (NULL en notas de debito por concepto)` y `CodAlmacen NVARCHAR(8) NOT NULL, -- FK` → `CodAlmacen NVARCHAR(8), -- FK (NULL en notas de debito por concepto)`; actualizar los comentarios `-- CodArticulo NOT NULL` / `-- CodAlmacen NOT NULL` que preceden a la tabla a `-- CodArticulo y CodAlmacen: NOT NULL en facturas (lo exige FacturaDomain); NULL solo en notas de debito por concepto`.
3. **`CREATE TABLE FacturaCompra(`** (≈ línea 981) y **`FacturaCompraDetalle`**: los mismos cambios con `DocSubType` `('--', 'DN')`, nombres `DF_/CK_FacturaCompra_*`, `CK_FacturaCompra_Base` con `(DocSubType = 'DN' AND BaseTipo IN (-1, 13))`, `pk_factura_compra`, e índice `IX_FacturaCompra_DocSubType ON FacturaCompra (DocSubType, CodigoSN)`.
4. **Numeración (≈ líneas 553-559)**: reemplazar `VALUES(7, 33, 'Nota Debito Cliente', '--');` por `VALUES(6, 33, 'Nota Debito Cliente', 'ND');` y `VALUES(14, 35, 'Nota Debito Prov.', '--');` por `VALUES(13, 35, 'Nota Debito Prov.', 'DN');`. En `NumeracionDocumentoDet` (≈ líneas 628-634) reemplazar las cuatro filas de las series 32-35 por: `VALUES(6, 32,'Manual', 'N', 'ND', 'D', 'S');` · `VALUES(6, 33,'Primario', 'N', 'ND', 'D', 1, 1);` · `VALUES(13, 34,'Manual', 'N', 'DN', 'D', 'S');` · `VALUES(13, 35,'Primario', 'N', 'DN', 'D', 1, 1);` (conservar los nombres de columna de cada `INSERT` existente).
5. **Eliminar** las cuatro tablas `NotaDebito`, `NotaDebitoDetalle`, `NotaDebitoCompra`, `NotaDebitoCompraDetalle` (desde el comentario `-- Notas de debito de cliente (objeto 7) y de proveedor (objeto 14). Sin inventario.` hasta el final del bloque de `NotaDebitoCompraDetalle`, ≈ líneas 1329-1430) y sus comentarios. No tocar las tablas `NotaCredito*` ni el `MERGE Modulo`.
6. Verificar: `grep -n "NotaDebito\b\|NotaDebitoCompra\|VALUES(7,\|VALUES(14," sql/crear-api-db-test-completo.sql` solo puede mostrar las filas del `MERGE Modulo` (`NotasDebito`, `NotasDebitoCompra`) y nada de `CREATE TABLE NotaDebito`.

- [ ] **Step 3: Archivo de verificación SQL (scratch, no se commitea)**

Crear en el scratchpad (`C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\verifica-nd.sql`) este script, que prepara datos mínimos y prueba las restricciones imprimiendo `OK`/`FALLA` por caso:

```sql
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM SocioNegocio WHERE Codigo = 'C0001')
    INSERT INTO SocioNegocio(Codigo,Nombre,TipoSN,Serie,GrupoSN,Activo) VALUES('C0001','Cliente prueba','C',1,101,'S');
IF NOT EXISTS (SELECT 1 FROM SocioNegocio WHERE Codigo = 'P0001')
    INSERT INTO SocioNegocio(Codigo,Nombre,TipoSN,Serie,GrupoSN,Activo) VALUES('P0001','Proveedor prueba','P',2,102,'S');

DECLARE @serND int = (SELECT TOP 1 Serie FROM NumeracionDocumentoDet WHERE CodigoObj = '6'  AND SubTipoDoc = 'ND' AND NombreSerie = 'Primario');
DECLARE @serDN int = (SELECT TOP 1 Serie FROM NumeracionDocumentoDet WHERE CodigoObj = '13' AND SubTipoDoc = 'DN' AND NombreSerie = 'Primario');
DECLARE @serF  int = (SELECT TOP 1 Serie FROM NumeracionDocumentoDet WHERE CodigoObj = '6'  AND SubTipoDoc = '--' AND NombreSerie = 'Primario');
DECLARE @serFC int = (SELECT TOP 1 Serie FROM NumeracionDocumentoDet WHERE CodigoObj = '13' AND SubTipoDoc = '--' AND NombreSerie = 'Primario');
PRINT 'series: ND=' + ISNULL(CAST(@serND AS varchar(10)),'NULL') + ' DN=' + ISNULL(CAST(@serDN AS varchar(10)),'NULL')
    + ' F=' + ISNULL(CAST(@serF AS varchar(10)),'NULL') + ' FC=' + ISNULL(CAST(@serFC AS varchar(10)),'NULL');

-- T1: nota de debito ND con base 6 y modo concepto -> debe entrar
BEGIN TRY
    INSERT INTO Factura(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc,DocSubType,DocType,BaseTipo,BaseEntry) VALUES(901,@serND,GETDATE(),GETDATE(),'C0001','QTZ','ND','S',6,1);
    PRINT 'T1 OK nota ND (S, base 6) aceptada';
END TRY BEGIN CATCH PRINT 'T1 FALLA ' + ERROR_MESSAGE(); END CATCH

-- T2: factura normal con BaseTipo 6 -> rechazada por CK_Factura_Base
BEGIN TRY
    INSERT INTO Factura(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc,DocSubType,DocType,BaseTipo) VALUES(902,@serF,GETDATE(),GETDATE(),'C0001','QTZ','--','I',6);
    PRINT 'T2 FALLA factura con BaseTipo 6 aceptada';
END TRY BEGIN CATCH PRINT 'T2 OK rechazada: ' + ERROR_MESSAGE(); END CATCH

-- T3: factura normal en modo concepto -> rechazada por CK_Factura_DocType_Subtipo
BEGIN TRY
    INSERT INTO Factura(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc,DocSubType,DocType) VALUES(903,@serF,GETDATE(),GETDATE(),'C0001','QTZ','--','S');
    PRINT 'T3 FALLA factura S aceptada';
END TRY BEGIN CATCH PRINT 'T3 OK rechazada: ' + ERROR_MESSAGE(); END CATCH

-- T4: subtipo invalido -> rechazado
BEGIN TRY
    INSERT INTO Factura(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc,DocSubType) VALUES(904,@serF,GETDATE(),GETDATE(),'C0001','QTZ','XX');
    PRINT 'T4 FALLA subtipo XX aceptado';
END TRY BEGIN CATCH PRINT 'T4 OK rechazado: ' + ERROR_MESSAGE(); END CATCH

-- T5: linea de concepto sin articulo ni almacen -> aceptada
BEGIN TRY
    DECLARE @e1 int = (SELECT Entry FROM Factura WHERE NumDoc = 901);
    INSERT INTO FacturaDetalle(Entry,NoLinea,Descripcion,Cantidad,Precio,CodigoImpuesto) VALUES(@e1,1,'Flete',1,100,'IVA');
    PRINT 'T5 OK linea sin articulo ni almacen aceptada';
END TRY BEGIN CATCH PRINT 'T5 FALLA ' + ERROR_MESSAGE(); END CATCH

-- T6..T9: lo mismo en compra (DN, base 13)
BEGIN TRY
    INSERT INTO FacturaCompra(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc,DocSubType,DocType,BaseTipo,BaseEntry) VALUES(911,@serDN,GETDATE(),GETDATE(),'P0001','QTZ','DN','S',13,1);
    PRINT 'T6 OK nota DN (S, base 13) aceptada';
END TRY BEGIN CATCH PRINT 'T6 FALLA ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY
    INSERT INTO FacturaCompra(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc,DocSubType,DocType,BaseTipo) VALUES(912,@serFC,GETDATE(),GETDATE(),'P0001','QTZ','--','I',13);
    PRINT 'T7 FALLA factura de compra con BaseTipo 13 aceptada';
END TRY BEGIN CATCH PRINT 'T7 OK rechazada: ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY
    INSERT INTO FacturaCompra(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc,DocSubType,DocType) VALUES(913,@serFC,GETDATE(),GETDATE(),'P0001','QTZ','ND','I');
    PRINT 'T8 FALLA subtipo ND en compra aceptado';
END TRY BEGIN CATCH PRINT 'T8 OK rechazado: ' + ERROR_MESSAGE(); END CATCH
BEGIN TRY
    DECLARE @e2 int = (SELECT Entry FROM FacturaCompra WHERE NumDoc = 911);
    INSERT INTO FacturaCompraDetalle(Entry,NoLinea,Descripcion,Cantidad,Precio,CodigoImpuesto) VALUES(@e2,1,'Flete',1,100,'IVA');
    PRINT 'T9 OK linea de compra sin articulo ni almacen aceptada';
END TRY BEGIN CATCH PRINT 'T9 FALLA ' + ERROR_MESSAGE(); END CATCH

-- Estado del esquema
SELECT 'columnas' AS que, t.name AS tabla, c.name AS columna, c.is_nullable FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
 WHERE (t.name IN ('Factura','FacturaCompra') AND c.name IN ('DocSubType','DocType')) OR (t.name IN ('FacturaDetalle','FacturaCompraDetalle') AND c.name IN ('CodArticulo','CodAlmacen')) ORDER BY t.name, c.name;
SELECT 'constraints' AS que, name FROM sys.check_constraints WHERE name LIKE 'CK_Factura%' ORDER BY name;
SELECT 'indices' AS que, name FROM sys.indexes WHERE name LIKE 'IX_Factura%DocSubType' ORDER BY name;
SELECT 'tablasND' AS que, name FROM sys.tables WHERE name LIKE 'NotaDebito%';
SELECT 'series' AS que, CodigoObj, Serie, NombreSerie, SubTipoDoc FROM NumeracionDocumentoDet WHERE SubTipoDoc IN ('ND','DN') ORDER BY Serie;
SELECT 'numeracion' AS que, CodigoObj, SerieDfct, DocAlias, SubTipoDoc FROM NumeracionDocumento WHERE SubTipoDoc IN ('ND','DN') OR CodigoObj IN ('7','14');
```

- [ ] **Step 4: Probar la migración en bases temporales (4 casos, todos se eliminan al final)**

PowerShell desde la raíz del repo API. Para cada caso se crea la base con `sqlcmd` reemplazando el nombre `API_DB_TEST` por el de la base temporal en una copia del script (`$scratch` = carpeta del scratchpad). Variables comunes:

```powershell
$pw = ([regex]::Match((Get-Content API.Service.WebApi\appsettings.json -Raw), 'Password=([^;]*)')).Groups[1].Value
$scratch = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
function Crear($db, $contenido) {
    ($contenido -replace 'API_DB_TEST', $db) | Set-Content "$scratch\$db.sql" -Encoding UTF8
    sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$scratch\$db.sql"
}
function Sql($db, $archivo) { sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -d $db -i $archivo }
function Borrar($db) { sqlcmd -S localhost -U sa -P $pw -C -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" }
$viejo = (git show HEAD:sql/crear-api-db-test-completo.sql) -join "`n"     # esquema anterior (aún sin commitear el nuevo)
$nuevo = Get-Content sql\crear-api-db-test-completo.sql -Raw
```

- **Caso A — base real del usuario (sin tablas de notas de débito):** `Crear 'API_DB_ND_A' $viejo`; luego simular que el script de notas de débito nunca se aplicó: `sqlcmd ... -d API_DB_ND_A -Q "DROP TABLE NotaDebitoDetalle, NotaDebito, NotaDebitoCompraDetalle, NotaDebitoCompra; DELETE FROM NumeracionDocumentoDet WHERE CodigoObj IN ('7','14'); DELETE FROM NumeracionDocumento WHERE CodigoObj IN ('7','14');"`; ejecutar `Sql 'API_DB_ND_A' sql\2026-10-17-notas-debito-en-factura.sql` → sin errores; las series nuevas salen de `MAX(Serie)+1` (con las series 36-39 de notas de crédito ya existentes: 40-43). Después `Sql 'API_DB_ND_A' "$scratch\verifica-nd.sql"` → T1..T9 `OK`, sin tablas `NotaDebito*`, 4 series (`6/ND` ×2, `13/DN` ×2) y 2 filas en `numeracion`. **Re-ejecutar la migración** una segunda vez → sin errores y sin duplicados (idempotencia).
- **Caso B — base con tablas vacías del script viejo:** `Crear 'API_DB_ND_B' $viejo`; ejecutar la migración → las series 32-35 pasan a `6/ND` y `13/DN` (mismos números), desaparecen `(7,'--')` y `(14,'--')` de `NumeracionDocumento`, las tablas `NotaDebito*` se eliminan; `verifica-nd.sql` → T1..T9 `OK`.
- **Caso C — base con una nota en `NotaDebito`:** `Crear 'API_DB_ND_C' $viejo`; insertar una fila mínima en `NotaDebito` (`INSERT INTO NotaDebito(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,MonedaDoc) VALUES(1,32,GETDATE(),GETDATE(),'C0001','QTZ')` tras crear el socio `C0001` como en `verifica-nd.sql`); ejecutar la migración → **debe fallar** con el mensaje `Las tablas NotaDebito* tienen filas: esta migracion no copia datos. No se cambio nada.`; comprobar que no existe `Factura.DocSubType` (`COL_LENGTH('dbo.Factura','DocSubType') IS NULL`) y que `NotaDebito` conserva su fila.
- **Caso D — base nueva con el script completo actualizado:** `Crear 'API_DB_ND_D' $nuevo`; `verifica-nd.sql` → T1..T9 `OK`; las series `ND`/`DN` son 32-35 con `CodigoObj` 6/6/13/13; `numeracion` muestra `(6,33,'Nota Debito Cliente','ND')` y `(13,35,'Nota Debito Prov.','DN')`; sin tablas `NotaDebito*`.

Cierre obligatorio (aunque algo falle): `Borrar` de las cuatro bases, y comprobar con `sqlcmd ... -Q "SELECT name FROM sys.databases WHERE name LIKE 'API_DB_ND_%'"` que no quedan; `API_DB_TEST` no se tocó (no aparece en ningún comando).

- [ ] **Step 5: Retirar el script anterior y commit**

```bash
git rm sql/2026-10-04-notas-debito.sql
git add sql/2026-10-17-notas-debito-en-factura.sql sql/crear-api-db-test-completo.sql
git commit -m "feat(db): notas de debito dentro de Factura/FacturaCompra con DocSubType y DocType

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

`git status --porcelain` debe quedar solo con ` M API.Service.WebApi/appsettings.json`.

---

## Fase 2 — API: entidades, numeración y facturas

### Task 2: Propiedades `DocSubType` y `DocType` en `Factura` y `FacturaCompra`

**Files:**
- Modify: `API.Domain.Entity/Models/Factura.cs`, `API.Domain.Entity/Models/FacturaCompra.cs`, `API.Domain.Entity/Models/ApiDbTestContext.cs`
- Create: `API.Service.WebApi.Tests/Domain/FacturaModeloTests.cs`

**Interfaces:**
- Consumes: constraints/columnas de la Task 1.
- Produces: `Factura.DocSubType` (string, default `"--"`), `Factura.DocType` (string, default `"I"`) y lo mismo en `FacturaCompra`; mapeo EF con `HasMaxLength` y `HasDefaultValueSql`.

**Precondición verificable:** Task 1 commiteada; `dotnet test API.sln` verde (1331).

- [ ] **Step 1: Escribir las pruebas del modelo (fallan: las propiedades no existen)**

Crear `API.Service.WebApi.Tests/Domain/FacturaModeloTests.cs` (LF):

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// Verifica sin base de datos el mapeo EF de DocSubType/DocType y que el detalle de factura
    /// acepta artículo y almacén nulos (notas de débito por concepto). Debe coincidir con
    /// sql/2026-10-17-notas-debito-en-factura.sql.
    /// </summary>
    public class FacturaModeloTests
    {
        private static Microsoft.EntityFrameworkCore.Metadata.IModel Modelo()
        {
            var opciones = new DbContextOptionsBuilder<ApiDbTestContext>()
                .UseSqlServer("Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;
            using var contexto = new ApiDbTestContext(opciones);
            return contexto.Model;
        }

        [Theory]
        [InlineData(typeof(Factura))]
        [InlineData(typeof(FacturaCompra))]
        public void Encabezado_MapeaDocSubTypeYDocType(Type tipo)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            var subTipo = entidad.FindProperty("DocSubType")!;
            Assert.Equal(2, subTipo.GetMaxLength());
            Assert.Equal("('--')", subTipo.GetDefaultValueSql());
            Assert.False(subTipo.IsNullable);

            var modo = entidad.FindProperty("DocType")!;
            Assert.Equal(1, modo.GetMaxLength());
            Assert.Equal("('I')", modo.GetDefaultValueSql());
            Assert.False(modo.IsNullable);
        }

        [Theory]
        [InlineData(typeof(FacturaDetalle))]
        [InlineData(typeof(FacturaCompraDetalle))]
        public void Detalle_AceptaArticuloYAlmacenNulos(Type tipo)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            Assert.True(entidad.FindProperty("CodArticulo")!.IsNullable);
            Assert.True(entidad.FindProperty("CodAlmacen")!.IsNullable);
        }

        [Fact]
        public void LasEntidadesNuevasNacenComoFacturaPorArticulos()
        {
            Assert.Equal("--", new Factura().DocSubType);
            Assert.Equal("I", new Factura().DocType);
            Assert.Equal("--", new FacturaCompra().DocSubType);
            Assert.Equal("I", new FacturaCompra().DocType);
        }
    }
}
```

- [ ] **Step 2: Verificar que fallan**

`dotnet test API.sln --filter "FullyQualifiedName~FacturaModeloTests"` → error de compilación (`DocSubType` no existe en `Factura`).

- [ ] **Step 3: Agregar las propiedades y el mapeo**

En `API.Domain.Entity/Models/Factura.cs`, después de `public string? TipoObjeto { get; set; }` (y su línea en blanco) insertar:

```csharp
    /// <summary>'--' = factura; 'ND' = nota de débito de cliente (modelo SAP B1: misma tabla, distinto subtipo).</summary>
    public string DocSubType { get; set; } = "--";

    /// <summary>'I' = por artículos (mueve inventario); 'S' = por concepto. La factura normal es siempre 'I'.</summary>
    public string DocType { get; set; } = "I";

```

En `API.Domain.Entity/Models/FacturaCompra.cs` lo mismo, con el comentario `'--' = factura de compra; 'DN' = nota de débito de proveedor`.

En `API.Domain.Entity/Models/ApiDbTestContext.cs`, dentro de `modelBuilder.Entity<Factura>(entity => { ... })` y dentro de `modelBuilder.Entity<FacturaCompra>(entity => { ... })`, después del bloque `entity.Property(e => e.Direccion).HasMaxLength(254);` (en orden alfabético del scaffold) insertar:

```csharp
            entity.Property(e => e.DocSubType)
                .HasMaxLength(2)
                .HasDefaultValueSql("('--')");
            entity.Property(e => e.DocType)
                .HasMaxLength(1)
                .HasDefaultValueSql("('I')");
```

No hay que tocar los bloques `FacturaDetalle`/`FacturaCompraDetalle` (sus `CodArticulo`/`CodAlmacen` ya son `string?` y no declaran `IsRequired`). Conservar el fin de línea (LF) de los archivos.

- [ ] **Step 4: Pruebas, suite completa y commit**

`dotnet test API.sln --filter "FullyQualifiedName~FacturaModeloTests"` → verde; luego `dotnet test API.sln` completo verde (esperado 1331 + 5 = **1336**; reportar el total real).

```bash
git add API.Domain.Entity/Models/Factura.cs API.Domain.Entity/Models/FacturaCompra.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Service.WebApi.Tests/Domain/FacturaModeloTests.cs
git commit -m "feat(notas-debito): DocSubType y DocType en Factura y FacturaCompra

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Clave compuesta `"CodigoObj:SubTipoDoc"` en la numeración (API)

**Files:**
- Create: `API.Domain.Entity/NumeracionClave.cs`
- Modify: `API.Infraestructure.Repository/NumeracionDocumentoRepositorio.cs`, `API.Domain.Core/NumeracionDocumentoDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/NumeracionClaveTests.cs`, `API.Service.WebApi.Tests/Domain/NumeracionDocumentoDomainTests.cs`

**Interfaces:**
- Consumes: `NumeracionDocumento` (clave EF `(CodigoObj, SubTipoDoc)`), `IRepositorioGenerico<NumeracionDocumento, string>`.
- Produces: `API.Domain.Entity.NumeracionClave.Componer(string codigoObj, string? subTipoDoc)` → `"6"` o `"6:ND"`; `Separar(string clave)` → `(CodigoObj, SubTipoDoc?)`; `NumeracionDocumentoRepositorio.ObtenerAsync(clave)` y `NumeracionDocumentoDomain.ObtenerPorCodigoAsync(clave)` aceptan ambas formas (sin `:` = comportamiento actual: primera fila del objeto); `InsertarAsync` detecta duplicados por `(CodigoObj, SubTipoDoc)`.

**Precondición verificable:** Task 2 commiteada; `grep -n "FirstOrDefaultAsync(x => x.CodigoObj" API.Domain.Core/NumeracionDocumentoDomain.cs API.Infraestructure.Repository/NumeracionDocumentoRepositorio.cs` muestra una coincidencia en cada archivo.

- [ ] **Step 1: Pruebas (fallan: no existen `NumeracionClave` ni el filtro por subtipo)**

Crear `API.Service.WebApi.Tests/Domain/NumeracionClaveTests.cs`:

```csharp
using API.Domain.Entity;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NumeracionClaveTests
    {
        [Theory]
        [InlineData("6", null, "6")]
        [InlineData("6", "", "6")]
        [InlineData("6", "ND", "6:ND")]
        [InlineData("13", "DN", "13:DN")]
        public void Componer_UsaSoloElCodigoSiNoHaySubtipo(string codigo, string? subTipo, string esperado)
        {
            Assert.Equal(esperado, NumeracionClave.Componer(codigo, subTipo));
        }

        [Theory]
        [InlineData("6", "6", null)]
        [InlineData("6:ND", "6", "ND")]
        [InlineData("13:DN", "13", "DN")]
        [InlineData("1:--", "1", "--")]
        [InlineData("6:", "6", null)]
        public void Separar_DevuelveCodigoYSubtipo(string clave, string codigo, string? subTipo)
        {
            Assert.Equal((codigo, subTipo), NumeracionClave.Separar(clave));
        }
    }
}
```

Crear `API.Service.WebApi.Tests/Domain/NumeracionDocumentoDomainTests.cs`:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NumeracionDocumentoDomainTests
    {
        private readonly Mock<IRepositorioGenerico<NumeracionDocumento, string>> _repo = new();
        private readonly NumeracionDocumentoDomain _domain;

        public NumeracionDocumentoDomainTests()
        {
            _domain = new NumeracionDocumentoDomain(_repo.Object);
            var filas = new List<NumeracionDocumento>
            {
                new() { CodigoObj = "6", SubTipoDoc = "--", DocAlias = "Factura", SerieDfct = 11 },
                new() { CodigoObj = "6", SubTipoDoc = "ND", DocAlias = "Nota Debito Cliente", SerieDfct = 33 },
                new() { CodigoObj = "1", SubTipoDoc = "C", DocAlias = "Socio de negocio", SerieDfct = 1 },
                new() { CodigoObj = "1", SubTipoDoc = "P", DocAlias = "Socio de negocio", SerieDfct = 2 }
            };
            _repo.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());
        }

        [Fact]
        public async Task ObtenerPorCodigo_SoloConCodigo_DevuelveLaPrimeraFilaDelObjeto()
        {
            var fila = await _domain.ObtenerPorCodigoAsync("6");

            Assert.Equal("--", fila.SubTipoDoc);
        }

        [Theory]
        [InlineData("6:ND", "Nota Debito Cliente")]
        [InlineData("6:--", "Factura")]
        [InlineData("1:P", "Socio de negocio")]
        public async Task ObtenerPorCodigo_ConSubtipo_DevuelveLaFilaExacta(string clave, string alias)
        {
            var fila = await _domain.ObtenerPorCodigoAsync(clave);

            Assert.Equal(alias, fila.DocAlias);
            Assert.Equal(clave.Split(':')[1], fila.SubTipoDoc);
        }

        [Fact]
        public async Task ObtenerPorCodigo_ConSubtipoInexistente_DevuelveNull()
        {
            Assert.Null(await _domain.ObtenerPorCodigoAsync("6:XX"));
        }

        [Fact]
        public async Task Insertar_MismoObjetoConOtroSubtipo_Permite()
        {
            var nueva = new NumeracionDocumento { CodigoObj = "13", SubTipoDoc = "DN", DocAlias = "Nota Debito Prov." };
            _repo.Setup(r => r.InsertarAsync(nueva)).ReturnsAsync(nueva);

            Assert.True(await _domain.InsertarAsync(nueva));

            _repo.Verify(r => r.InsertarAsync(nueva), Times.Once);
        }

        [Fact]
        public async Task Insertar_ObjetoYSubtipoRepetidos_LanzaConElSubtipo()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new NumeracionDocumento { CodigoObj = "6", SubTipoDoc = "ND" }));

            Assert.Equal("Ya existe un registro con el código: 6 (ND)", ex.Message);
            _repo.Verify(r => r.InsertarAsync(It.IsAny<NumeracionDocumento>()), Times.Never);
        }

        [Fact]
        public async Task Insertar_ObjetoRepetidoConSubtipoPorDefecto_LanzaSinSubtipo()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new NumeracionDocumento { CodigoObj = "6", SubTipoDoc = "--" }));

            Assert.Equal("Ya existe un registro con el código: 6", ex.Message);
        }

        [Fact]
        public async Task ActualizarYEliminar_PasanLaClaveCompletaAlRepositorio()
        {
            var datos = new NumeracionDocumento { SerieDfct = 34 };
            _repo.Setup(r => r.ActualizarAsync("6:ND", datos)).ReturnsAsync(true);
            _repo.Setup(r => r.EliminarAsync("6:ND")).ReturnsAsync(true);

            Assert.True(await _domain.ActualizarAsync("6:ND", datos));
            Assert.True(await _domain.EliminarAsync("6:ND"));
        }
    }
}
```

- [ ] **Step 2: Verificar que fallan**

`dotnet test API.sln --filter "FullyQualifiedName~NumeracionClaveTests|FullyQualifiedName~NumeracionDocumentoDomainTests"` → no compila (`NumeracionClave` no existe).

- [ ] **Step 3: Implementar**

Crear `API.Domain.Entity/NumeracionClave.cs` (LF):

```csharp
namespace API.Domain.Entity
{
    /// <summary>
    /// Clave de un encabezado de NumeracionDocumento: "CodigoObj" (sin subtipo: primera fila del
    /// objeto, comportamiento histórico) o "CodigoObj:SubTipoDoc" (fila exacta, p. ej. "6:ND").
    /// El ':' no aparece en ningún CodigoObj y es válido dentro de un segmento de URL.
    /// </summary>
    public static class NumeracionClave
    {
        public const char Separador = ':';

        public static string Componer(string codigoObj, string? subTipoDoc) =>
            string.IsNullOrEmpty(subTipoDoc) ? codigoObj : $"{codigoObj}{Separador}{subTipoDoc}";

        public static (string CodigoObj, string? SubTipoDoc) Separar(string clave)
        {
            var i = clave.IndexOf(Separador);
            if (i < 0)
                return (clave, null);

            var subTipo = clave[(i + 1)..];
            return (clave[..i], subTipo.Length == 0 ? null : subTipo);
        }
    }
}
```

Reemplazar el cuerpo de `API.Infraestructure.Repository/NumeracionDocumentoRepositorio.cs`:

```csharp
using API.Domain.Entity;
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Infraestructure.Repository
{
    public class NumeracionDocumentoRepositorio : RepositorioGenericoEfCore<NumeracionDocumento, string>
    {
        public NumeracionDocumentoRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // id = "CodigoObj" (primera fila del objeto) o "CodigoObj:SubTipoDoc" (fila exacta).
        public override async Task<NumeracionDocumento?> ObtenerAsync(string id)
        {
            var (codigo, subTipo) = NumeracionClave.Separar(id);
            return subTipo is null
                ? await DbSet.FirstOrDefaultAsync(x => x.CodigoObj == codigo)
                : await DbSet.FirstOrDefaultAsync(x => x.CodigoObj == codigo && x.SubTipoDoc == subTipo);
        }
    }
}
```

En `API.Domain.Core/NumeracionDocumentoDomain.cs` agregar `using API.Domain.Entity;` y cambiar:

```csharp
        public async Task<bool> InsertarAsync(NumeracionDocumento obj)
        {
            if (await ObtenerPorCodigoAsync(NumeracionClave.Componer(obj.CodigoObj, obj.SubTipoDoc)) != null)
            {
                var sufijo = string.IsNullOrEmpty(obj.SubTipoDoc) || obj.SubTipoDoc == "--" ? string.Empty : $" ({obj.SubTipoDoc})";
                throw new Exception($"Ya existe un registro con el código: {obj.CodigoObj}{sufijo}");
            }

            await _repoNumeracion.InsertarAsync(obj);
            return true;
        }
```

y

```csharp
        public async Task<NumeracionDocumento> ObtenerPorCodigoAsync(string codigo)
        {
            var (codigoObj, subTipo) = NumeracionClave.Separar(codigo);
            var queryable = await _repoNumeracion.ObtenerTodoAsync();
            var numeracion = subTipo is null
                ? await queryable.FirstOrDefaultAsync(x => x.CodigoObj == codigoObj)
                : await queryable.FirstOrDefaultAsync(x => x.CodigoObj == codigoObj && x.SubTipoDoc == subTipo);

            return numeracion;
        }
```

(`ActualizarAsync` y `EliminarAsync` ya delegan en el repositorio con la clave tal cual.)

- [ ] **Step 4: Pruebas, suite completa y commit**

Pruebas filtradas verdes; `dotnet test API.sln` completo verde (esperado 1336 + 18 = **1354**; reportar el total real: 9 casos de `NumeracionClaveTests` + 9 de `NumeracionDocumentoDomainTests`).

```bash
git add API.Domain.Entity/NumeracionClave.cs API.Infraestructure.Repository/NumeracionDocumentoRepositorio.cs API.Domain.Core/NumeracionDocumentoDomain.cs API.Service.WebApi.Tests/Domain/NumeracionClaveTests.cs API.Service.WebApi.Tests/Domain/NumeracionDocumentoDomainTests.cs
git commit -m "feat(numeracion): clave compuesta CodigoObj:SubTipoDoc para distinguir filas del mismo objeto

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Los dominios de factura ignoran las notas, fijan el modo y validan la serie

**Files:**
- Modify: `API.Domain.Core/FacturaDomain.cs`, `FacturaCompraDomain.cs`, `FacturaDetalleDomain.cs`, `FacturaCompraDetalleDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs`, `FacturaCompraDomainTests.cs`, `FacturaDetalleDomainTests.cs`, `FacturaCompraDetalleDomainTests.cs`

**Interfaces:**
- Consumes: `Factura.DocSubType`/`DocType` (Task 2).
- Produces: `FacturaDomain`/`FacturaCompraDomain`: `InsertarAsync` fija `DocSubType="--"`, `DocType="I"`, exige serie con `CodigoObj` `"6"`/`"13"` y `SubTipoDoc "--"` (mensaje `"La serie no corresponde a facturas."` / `"La serie no corresponde a facturas de compra."`) y `CodArticulo`/`CodAlmacen` en cada línea (`"Línea {n}: el artículo es requerido."`, `"Línea {n}: el almacén es requerido."`); `ObtenerAsync` (→ `null`), `ActualizarAsync` y `EliminarAsync` (→ `false`) tratan una fila con `DocSubType <> '--'` como inexistente; `ObtenerTodoAsync` la excluye. `FacturaDetalleDomain`/`FacturaCompraDetalleDomain`: `ObtenerPorFacturaAsync` (→ lista vacía), `ObtenerAsync` (→ `null`) y `ObtenerTodoAsync` (excluye) ocultan las líneas de documentos con `DocSubType <> '--'`.

**Precondición verificable:** Task 3 commiteada; `grep -n "private const string TipoObjetoFactura\b" API.Domain.Core/FacturaDomain.cs` muestra la constante y `grep -n "_repoFactura\b" API.Domain.Core/FacturaDomain.cs` la usa (si el campo se llama distinto, usar ese nombre en los pasos siguientes).

- [ ] **Step 1: Pruebas de `FacturaDomain` (fallan)**

Agregar al final de la clase `FacturaDomainTests` (antes de las llaves de cierre; usan los campos `_repoHeader`, `_repoDetalle`, `_repoNumeracion`, `_asiento` y los helpers `SerieAuto`/`Linea` existentes):

```csharp
        // ---------------- Aislamiento de las notas de débito (DocSubType) ----------------

        [Fact]
        public async Task InsertarAsync_ForzaDocSubTypeFacturaYModoArticulos()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new Factura { Serie = 4, DocSubType = "ND", DocType = "S" };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 1m, 10m) });

            Assert.Equal("--", obj.DocSubType);
            Assert.Equal("I", obj.DocType);
        }

        [Theory]
        [InlineData("6", "ND")]    // serie de nota de débito
        [InlineData("5", "--")]    // serie de otro documento (Entrega)
        public async Task InsertarAsync_SerieQueNoEsDeFactura_Lanza_YNoInserta(string codigoObj, string subTipo)
        {
            var serie = SerieAuto(sig: 5);
            serie.CodigoObj = codigoObj;
            serie.SubTipoDoc = subTipo;
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4 }, new[] { Linea("ART1", "01", 1m, 10m) }));

            Assert.Equal("La serie no corresponde a facturas.", ex.Message);
            Assert.Equal(5, serie.SigNumero);   // el consecutivo no avanza
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinArticulo_Lanza_YNoAvanzaLaSerie()
        {
            var serie = SerieAuto(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4 }, new[] { Linea("ART1", "01", 1m, 10m), Linea(" ", "01", 1m, 10m) }));

            Assert.Equal("Línea 2: el artículo es requerido.", ex.Message);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinAlmacen_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new Factura { Serie = 4 }, new[] { Linea("ART1", null!, 1m, 10m) }));

            Assert.Equal("Línea 1: el almacén es requerido.", ex.Message);
        }

        [Fact]
        public async Task ObtenerAsync_DeUnaNotaDeDebito_DevuelveNull()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "ND" });

            Assert.Null(await _domain.ObtenerAsync(7));
        }

        [Fact]
        public async Task ActualizarAsync_DeUnaNotaDeDebito_DevuelveFalse_SinRevertirInventario()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "ND", Cancelado = "N" });

            Assert.False(await _domain.ActualizarAsync(7, new Factura { Cancelado = "S" }));

            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_DeUnaNotaDeDebito_DevuelveFalse_SinBorrar()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "ND", Cancelado = "S" });

            Assert.False(await _domain.EliminarAsync(7));

            _repoHeader.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerTodoAsync_ExcluyeLasNotasDeDebito()
        {
            var filas = new List<Factura>
            {
                new() { Entry = 1, DocSubType = "--" }, new() { Entry = 2, DocSubType = "ND" }, new() { Entry = 3, DocSubType = "--" }
            };
            _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 1, 3 }, resultado.Select(f => f.Entry));
        }
```

Si `FacturaDomainTests` no importa `API.Service.WebApi.Tests.TestHelpers` para `AsAsyncQueryable`, ya lo hace (línea 6 del archivo actual); verificar.

- [ ] **Step 2: Pruebas de `FacturaCompraDomain` (fallan)**

Agregar al final de `FacturaCompraDomainTests` el equivalente (misma estructura, tipos de compra, `"13"`, mensajes `"La serie no corresponde a facturas de compra."`, subtipo de nota `"DN"`):

```csharp
        // ---------------- Aislamiento de las notas de débito de proveedor (DocSubType) ----------------

        [Fact]
        public async Task InsertarAsync_ForzaDocSubTypeFacturaYModoArticulos()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));
            var obj = new FacturaCompra { Serie = 4, DocSubType = "DN", DocType = "S" };

            await _domain.InsertarAsync(obj, new[] { Linea("ART1", "01", 1m, 10m) });

            Assert.Equal("--", obj.DocSubType);
            Assert.Equal("I", obj.DocType);
        }

        [Theory]
        [InlineData("13", "DN")]   // serie de nota de débito de proveedor
        [InlineData("12", "--")]   // serie de otro documento (Entrega de compra)
        public async Task InsertarAsync_SerieQueNoEsDeFacturaDeCompra_Lanza_YNoInserta(string codigoObj, string subTipo)
        {
            var serie = SerieAuto(sig: 5);
            serie.CodigoObj = codigoObj;
            serie.SubTipoDoc = subTipo;
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new FacturaCompra { Serie = 4 }, new[] { Linea("ART1", "01", 1m, 10m) }));

            Assert.Equal("La serie no corresponde a facturas de compra.", ex.Message);
            Assert.Equal(5, serie.SigNumero);
            _repoHeader.Verify(r => r.InsertarAsync(It.IsAny<FacturaCompra>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinArticulo_Lanza_YNoAvanzaLaSerie()
        {
            var serie = SerieAuto(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new FacturaCompra { Serie = 4 }, new[] { Linea("ART1", "01", 1m, 10m), Linea(" ", "01", 1m, 10m) }));

            Assert.Equal("Línea 2: el artículo es requerido.", ex.Message);
            Assert.Equal(5, serie.SigNumero);
        }

        [Fact]
        public async Task InsertarAsync_LineaSinAlmacen_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(SerieAuto(sig: 5));

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _domain.InsertarAsync(new FacturaCompra { Serie = 4 }, new[] { Linea("ART1", null!, 1m, 10m) }));

            Assert.Equal("Línea 1: el almacén es requerido.", ex.Message);
        }

        [Fact]
        public async Task ObtenerAsync_DeUnaNotaDeDebito_DevuelveNull()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7, DocSubType = "DN" });

            Assert.Null(await _domain.ObtenerAsync(7));
        }

        [Fact]
        public async Task ActualizarAsync_DeUnaNotaDeDebito_DevuelveFalse_SinRevertirInventario()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7, DocSubType = "DN", Cancelado = "N" });

            Assert.False(await _domain.ActualizarAsync(7, new FacturaCompra { Cancelado = "S" }));

            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_DeUnaNotaDeDebito_DevuelveFalse_SinBorrar()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new FacturaCompra { Entry = 7, DocSubType = "DN", Cancelado = "S" });

            Assert.False(await _domain.EliminarAsync(7));

            _repoHeader.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ObtenerTodoAsync_ExcluyeLasNotasDeDebito()
        {
            var filas = new List<FacturaCompra>
            {
                new() { Entry = 1, DocSubType = "--" }, new() { Entry = 2, DocSubType = "DN" }, new() { Entry = 3, DocSubType = "--" }
            };
            _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 1, 3 }, resultado.Select(f => f.Entry));
        }
```

- [ ] **Step 3: Pruebas de los dominios de detalle (fallan)**

En `FacturaDetalleDomainTests` agregar (antes de las llaves finales; agregar `using API.Service.WebApi.Tests.TestHelpers;` al inicio):

```csharp
        // ---------------- Las líneas de notas de débito no se exponen por este recurso ----------------

        [Fact]
        public async Task ObtenerPorFacturaAsync_DeUnaNota_DevuelveVacio()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "ND" });
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle> { new() { Entry = 7, NoLinea = 1 } }.AsAsyncQueryable());

            Assert.Empty(await _domain.ObtenerPorFacturaAsync(7));
        }

        [Fact]
        public async Task ObtenerPorFacturaAsync_DeUnaFactura_DevuelveSusLineas()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "--" });
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle>
            {
                new() { Entry = 7, NoLinea = 1 }, new() { Entry = 8, NoLinea = 1 }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerPorFacturaAsync(7)).ToList();

            Assert.Equal(new[] { 1 }, lineas.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerAsync_DeLineaDeNota_DevuelveNull()
        {
            _repoHeader.Setup(r => r.ObtenerAsync(7)).ReturnsAsync(new Factura { Entry = 7, DocSubType = "ND" });
            _repoDet.Setup(r => r.ObtenerAsync((7, 1))).ReturnsAsync(new FacturaDetalle { Entry = 7, NoLinea = 1 });

            Assert.Null(await _domain.ObtenerAsync(7, 1));
        }

        [Fact]
        public async Task ObtenerTodoAsync_ExcluyeLineasDeNotas()
        {
            _repoHeader.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<Factura>
            {
                new() { Entry = 7, DocSubType = "--" }, new() { Entry = 8, DocSubType = "ND" }
            }.AsAsyncQueryable());
            _repoDet.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle>
            {
                new() { Entry = 7, NoLinea = 1 }, new() { Entry = 8, NoLinea = 1 }, new() { Entry = 8, NoLinea = 2 }
            }.AsAsyncQueryable());

            var lineas = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 7 }, lineas.Select(l => l.Entry));
        }
```

En `FacturaCompraDetalleDomainTests` el equivalente con `FacturaCompraDetalle`, `FacturaCompra`, `DocSubType = "DN"` (mismos nombres de prueba, mismos campos `_repoDet`/`_repoHeader`).

- [ ] **Step 4: Verificar que fallan**

`dotnet test API.sln --filter "FullyQualifiedName~FacturaDomainTests|FullyQualifiedName~FacturaCompraDomainTests|FullyQualifiedName~FacturaDetalleDomainTests|FullyQualifiedName~FacturaCompraDetalleDomainTests"` → las pruebas nuevas fallan (las anteriores siguen verdes).

- [ ] **Step 5: Implementar en `FacturaDomain`**

En `API.Domain.Core/FacturaDomain.cs`:

1. Junto a la constante `TipoObjetoFactura` agregar `private const string SubTipoFactura = "--";`.
2. En `InsertarAsync`, justo después de `obj.TipoObjeto = TipoObjetoFactura;` agregar `obj.DocSubType = SubTipoFactura;` y `obj.DocType = "I";`.
3. Tras la comprobación `"La serie no existe."` y **antes** de `if (serie.Bloqueado == "S")` insertar:

```csharp
            if (serie.CodigoObj != TipoObjetoFactura || serie.SubTipoDoc != SubTipoFactura)
                throw new Exception("La serie no corresponde a facturas.");
```

4. Mover la creación de `lineasList` (`var lineasList = lineas?.ToList() ?? new List<FacturaDetalle>();`) **antes** del bloque `if (serie.Manual == "S") {...} else {...}` de numeración, y justo después de crearla llamar `ValidarLineas(lineasList);` para que un error de línea no avance el consecutivo. Agregar el método privado (dentro de la clase, fuera de `#region async methods`, p. ej. en un `#region validaciones` al final):

```csharp
        private static void ValidarLineas(IList<FacturaDetalle> lineas)
        {
            for (var i = 0; i < lineas.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lineas[i].CodArticulo))
                    throw new Exception($"Línea {i + 1}: el artículo es requerido.");
                if (string.IsNullOrWhiteSpace(lineas[i].CodAlmacen))
                    throw new Exception($"Línea {i + 1}: el almacén es requerido.");
            }
        }
```

5. Agregar el helper y usarlo en actualizar/eliminar/obtener:

```csharp
        // Una fila de Factura con DocSubType distinto de '--' es una nota de débito: su API es otra.
        private async Task<Factura?> ObtenerFacturaAsync(int id)
        {
            var factura = await _repoFactura.ObtenerAsync(id);
            return factura is not null && factura.DocSubType == SubTipoFactura ? factura : null;
        }
```

   - `ActualizarAsync` y `EliminarAsync`: reemplazar `var existente = await _repoFactura.ObtenerAsync(id);` por `var existente = await ObtenerFacturaAsync(id);`.
   - `ObtenerAsync`: `return await ObtenerFacturaAsync(id);` (puede devolver `null`; mantener la firma `Task<Factura>`).
   - `ObtenerTodoAsync`:

```csharp
        public async Task<IQueryable<Factura>> ObtenerTodoAsync()
        {
            var facturas = await _repoFactura.ObtenerTodoAsync();
            return facturas.Where(f => f.DocSubType == SubTipoFactura);
        }
```

   (Si el campo no se llama `_repoFactura`, usar el nombre real.)

- [ ] **Step 6: Implementar en `FacturaCompraDomain`**

Igual que el Step 5 sobre `API.Domain.Core/FacturaCompraDomain.cs` con: constante `TipoObjetoFacturaCompra` (= `"13"`), mensaje `"La serie no corresponde a facturas de compra."`, campo `_repoFacturaCompra`, tipos `FacturaCompra`/`FacturaCompraDetalle`, helper `ObtenerFacturaCompraAsync`. `ValidarLineas` recibe `IList<FacturaCompraDetalle>`.

- [ ] **Step 7: Implementar en los dominios de detalle**

En `API.Domain.Core/FacturaDetalleDomain.cs` agregar `private const string SubTipoFactura = "--";` y reemplazar los tres métodos de lectura:

```csharp
        public async Task<FacturaDetalle> ObtenerAsync(int entry, int noLinea)
        {
            // Las líneas de una nota de débito no se exponen por este recurso.
            var encabezado = await _repoEncabezado.ObtenerAsync(entry);
            if (encabezado is not null && encabezado.DocSubType != SubTipoFactura)
                return null;
            return await _repoGenericoDet.ObtenerAsync((entry, noLinea));
        }

        public async Task<IQueryable<FacturaDetalle>> ObtenerTodoAsync()
        {
            var lineas = await _repoGenericoDet.ObtenerTodoAsync();
            var entradasDeNotas = (await _repoEncabezado.ObtenerTodoAsync())
                .Where(h => h.DocSubType != SubTipoFactura)
                .Select(h => h.Entry);
            return lineas.Where(l => !entradasDeNotas.Contains(l.Entry));
        }

        public async Task<IEnumerable<FacturaDetalle>> ObtenerPorFacturaAsync(int entry)
        {
            var encabezado = await _repoEncabezado.ObtenerAsync(entry);
            if (encabezado is not null && encabezado.DocSubType != SubTipoFactura)
                return Array.Empty<FacturaDetalle>();

            var queryable = await _repoGenericoDet.ObtenerTodoAsync();
            return await queryable.Where(x => x.Entry == entry).ToListAsync();
        }
```

Lo mismo en `FacturaCompraDetalleDomain.cs` con los tipos de compra (la prueba existente de `ObtenerTodoAsync`, si la hay, debe sembrar también `_repoHeader.ObtenerTodoAsync()`; ajustar solo el setup, no la aserción).

- [ ] **Step 8: Pruebas, suite completa y commit**

`dotnet test API.sln --filter "FullyQualifiedName~Factura"` verde. Si una prueba existente falla porque su línea de fixture no trae `CodArticulo`/`CodAlmacen` (ahora son obligatorios) o su serie no trae `CodigoObj`/`SubTipoDoc`, corregir **el fixture** de esa prueba (nunca la validación) y anotarlo en el reporte. `dotnet test API.sln` completo verde (esperado 1354 + 26 = **1380**: 9 en `FacturaDomainTests`, 9 en `FacturaCompraDomainTests`, 4 + 4 en detalle; reportar el real).

```bash
git add API.Domain.Core/FacturaDomain.cs API.Domain.Core/FacturaCompraDomain.cs API.Domain.Core/FacturaDetalleDomain.cs API.Domain.Core/FacturaCompraDetalleDomain.cs API.Service.WebApi.Tests/Domain/FacturaDomainTests.cs API.Service.WebApi.Tests/Domain/FacturaCompraDomainTests.cs API.Service.WebApi.Tests/Domain/FacturaDetalleDomainTests.cs API.Service.WebApi.Tests/Domain/FacturaCompraDetalleDomainTests.cs
git commit -m "feat(notas-debito): los dominios de factura ignoran las notas, fijan el modo y validan la serie

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 3 — API: notas de débito sobre Factura

### Task 5: Nota de débito de cliente sobre `Factura` (dominio, contratos, aplicación, mapeo y pruebas)

**Files:**
- Overwrite: `API.Domain.Core/NotaDebitoDomain.cs`, `API.Domain.Interface/INotaDebitoDomain.cs`, `API.Application.Main/NotaDebitoApplication.cs`, `API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs`
- Modify: `API.Application.DTO/notaDebito/NotaDebitoCrearDTO.cs`, `NotaDebitoDTO.cs`, `NotaDebitoDetalleCrearDTO.cs`, `NotaDebitoDetalleDTO.cs`; `API.Transversal.Mapper/PerfilMapeo.cs`; `API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs`, `NotaDebitoMapeoTests.cs`

**Interfaces:**
- Consumes: `Factura`/`FacturaDetalle` con `DocSubType`/`DocType` (Task 2); series con `SubTipoDoc` `'ND'` (Task 1); `IInventarioAsientoService.AsentarAsync/RevertirAsync` y `MovimientoRequest(TipoDoc, DocEntry, DocLinea, CodArticulo, CodAlmacen, Cantidad, PrecioUnitario, Fecha)`; repos genéricos de `Articulo` (clave `string`) y `Almacen` (clave `string`); `FacturaBase` (record existente).
- Produces: `INotaDebitoDomain` con tipos `Factura`/`FacturaDetalle`:
  `Task<int> InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas)`, `Task<bool> ActualizarAsync(int id, Factura obj)`, `Task<bool> EliminarAsync(int id)`, `Task<Factura?> ObtenerAsync(int id)`, `Task<IQueryable<Factura>> ObtenerTodoAsync()`, `Task<IEnumerable<FacturaDetalle>> ObtenerLineasAsync(int id)`, `Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn)`, `Task<FacturaBase> ObtenerFacturaBaseAsync(int entry)`.
  `NotaDebitoDomain(IRepositorioGenerico<Factura,int> repoFactura, IRepositorioGenerico<FacturaDetalle,(int Entry,int NoLinea)> repoDetalle, IRepositorioGenerico<NumeracionDocumentoDet,int> repoNumeracion, IRepositorioGenerico<SocioNegocio,string> repoSocio, IRepositorioGenerico<Impuesto,string> repoImpuesto, IRepositorioGenerico<Monedum,string> repoMoneda, IRepositorioGenerico<Articulo,string> repoArticulo, IRepositorioGenerico<Almacen,string> repoAlmacen, IEjecutorTransaccion tx, IInventarioAsientoService asiento)`.
  DTOs: `NotaDebitoCrearDTO.DocType` y `NotaDebitoDTO.DocType` (`string?`); `NotaDebitoDetalleCrearDTO` y `NotaDebitoDetalleDTO` con `CodArticulo`/`CodAlmacen` (`string?`); `NotaDebitoFacturaBaseDTO.Lineas` devuelve esos dos campos de la factura. Rutas, permisos y demás campos sin cambios.

**Precondición verificable:** Task 4 commiteada; `git diff --quiet -- API.Domain.Core/NotaDebitoDomain.cs API.Domain.Interface/INotaDebitoDomain.cs API.Application.Main/NotaDebitoApplication.cs API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs` sin cambios pendientes (esos archivos se sobrescriben); `dotnet test API.sln` verde.

- [ ] **Step 1: Escribir las pruebas del dominio (fallan: el dominio todavía usa `NotaDebito`)**

Sobrescribir `API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs` (LF) con:

```csharp
using API.Domain.Core;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    public class NotaDebitoDomainTests
    {
        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        private const string CodigoObj = "6";
        private const string CodigoObjOtro = "5";   // CodigoObj de otro documento (Entrega)
        private const string SubTipoNota = "ND";
        private const string TipoSocio = "C";
        private const string TipoSocioOpuesto = "P";
        private const int TipoBase = 6;
        private const decimal SignoInventario = -1m;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de débito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseEsNota = "La factura base no puede ser una nota de débito.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private const string MsgModoInvalido = "Indique el tipo de nota: por artículos ('I') o por concepto ('S').";

        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<Articulo, string>> _repoArticulo = new();
        private readonly Mock<IRepositorioGenerico<Almacen, string>> _repoAlmacen = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly Mock<IInventarioAsientoService> _asiento = new();
        private readonly NotaDebitoDomain _domain;

        private readonly List<MovimientoRequest> _movimientos = new();

        public NotaDebitoDomainTests()
        {
            _domain = new NotaDebitoDomain(
                _repoFactura.Object, _repoDetalle.Object, _repoNumeracion.Object, _repoSocio.Object,
                _repoImpuesto.Object, _repoMoneda.Object, _repoArticulo.Object, _repoAlmacen.Object,
                _tx.Object, _asiento.Object);

            // El doble del ejecutor corre el Func directo (sin transaccion ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoFactura.Setup(r => r.InsertarAsync(It.IsAny<Factura>()))
                .ReturnsAsync((Factura n) => { n.Entry = 99; return n; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<FacturaDetalle>())).Returns(Task.CompletedTask);
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .Callback<IEnumerable<MovimientoRequest>, bool>((ms, _) => _movimientos.AddRange(ms))
                .Returns(Task.CompletedTask);
            _asiento.Setup(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);

            // Valores por defecto validos: cada test rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocio, Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA" });
            _repoArticulo.Setup(r => r.ObtenerAsync("ART1")).ReturnsAsync(new Articulo { Codigo = "ART1" });
            _repoAlmacen.Setup(r => r.ObtenerAsync("01")).ReturnsAsync(new Almacen { Codigo = "01" });
        }

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string? codigoObj = null, string? subTipo = null) => new()
        {
            CodigoObj = codigoObj ?? CodigoObj, Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = subTipo ?? SubTipoNota, TipoSerie = "D"
        };

        // Nota por concepto ('S') por defecto; NotaArticulos() la cambia a por articulos ('I').
        private static Factura Nota(Action<Factura>? ajustar = null)
        {
            var n = new Factura
            {
                Serie = 4, CodigoSn = "SN1", MonedaDoc = "QTZ", DocType = "S",
                FechaDoc = new DateTime(2026, 10, 4), FechaEmision = new DateTime(2026, 10, 4)
            };
            ajustar?.Invoke(n);
            return n;
        }

        private static Factura NotaArticulos(Action<Factura>? ajustar = null) =>
            Nota(n => { n.DocType = "I"; ajustar?.Invoke(n); });

        private static FacturaDetalle Linea(Action<FacturaDetalle>? ajustar = null)
        {
            var l = new FacturaDetalle
            {
                Descripcion = "Cargo por flete", Cantidad = 1m, Precio = 100m, PrctjeDesc = 0m,
                CodigoImpuesto = "IVA", Impuesto = 12m, TotalLinea = 112m
            };
            ajustar?.Invoke(l);
            return l;
        }

        private static FacturaDetalle LineaArticulo(Action<FacturaDetalle>? ajustar = null)
        {
            var l = new FacturaDetalle
            {
                CodArticulo = "ART1", CodAlmacen = "01", Descripcion = "Tornillo", Cantidad = 2m, Precio = 10m,
                PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = 2.4m, TotalLinea = 22.4m
            };
            ajustar?.Invoke(l);
            return l;
        }

        private async Task<string> MensajeAlInsertar(Factura nota, params FacturaDetalle[] lineas)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(nota, lineas));
            return ex.Message;
        }

        // ---------------- InsertarAsync: camino feliz por concepto ----------------

        [Fact]
        public async Task InsertarAsync_PorConcepto_NumeraGuardaEncabezadoYLineasSinInventario()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var nota = Nota();

            var entry = await _domain.InsertarAsync(nota, new[] { Linea(), Linea(l => l.Descripcion = "Intereses") });

            Assert.Equal(99, entry);
            Assert.Equal(SubTipoNota, nota.DocSubType);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
            Assert.Equal("S", nota.DocType);
            Assert.Equal("C", nota.EstadoInv);          // sin inventario: explicito, no el DEFAULT 'A' de la base
            Assert.Equal(5, nota.NumDoc);
            Assert.Equal(6, serie.SigNumero);           // arrancaba en 5: avanzo
            Assert.Equal("N", nota.NumManual);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.TipoObjeto == CodigoObj)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l => l.Entry == 99 && l.NoLinea == 2 && l.Descripcion == "Intereses")), Times.Once);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task InsertarAsync_FuerzaLosCamposDeSistema()
        {
            var nota = Nota(n =>
            {
                n.Cancelado = "S"; n.EstadoDoc = "C"; n.FechaCancelado = DateTime.Now;
                n.TipoObjeto = "99"; n.DocSubType = "--"; n.EstadoInv = "A";
            });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal("N", nota.Cancelado);
            Assert.Equal("A", nota.EstadoDoc);
            Assert.Null(nota.FechaCancelado);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
            Assert.Equal(SubTipoNota, nota.DocSubType);
            Assert.Equal("C", nota.EstadoInv);
        }

        [Fact]
        public async Task InsertarAsync_FuerzaImprimidoEnN()
        {
            var nota = Nota(n => n.Imprimido = "S");

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal("N", nota.Imprimido);
        }

        [Fact]
        public async Task InsertarAsync_SinFechas_UsaLaFechaActual()
        {
            var nota = Nota(n => { n.FechaDoc = null; n.FechaEmision = null; });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.NotNull(nota.FechaDoc);
            Assert.Equal(nota.FechaDoc, nota.FechaEmision);
        }

        [Fact]
        public async Task InsertarAsync_SerieManualConNumDoc_UsaElNumeroYMarcaNumManual()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null, manual: "S"));
            var nota = Nota(n => n.NumDoc = 321);

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal(321, nota.NumDoc);
            Assert.Equal("S", nota.NumManual);
        }

        // ---------------- InsertarAsync: modo por articulos (inventario) ----------------

        [Fact]
        public async Task InsertarAsync_PorArticulos_AsientaElMovimientoYMarcaEstadoInvA()
        {
            var nota = NotaArticulos(n => n.FechaDoc = new DateTime(2026, 10, 4));

            var entry = await _domain.InsertarAsync(nota, new[]
            {
                LineaArticulo(),
                LineaArticulo(l => { l.Cantidad = 1m; l.Precio = 5m; })
            });

            Assert.Equal(99, entry);
            Assert.Equal("I", nota.DocType);
            Assert.Equal("A", nota.EstadoInv);
            Assert.Equal(2, _movimientos.Count);
            Assert.Equal((CodigoObj, 99, 1, "ART1", "01", SignoInventario * 2m, 10m), (
                _movimientos[0].TipoDoc, _movimientos[0].DocEntry, _movimientos[0].DocLinea,
                _movimientos[0].CodArticulo, _movimientos[0].CodAlmacen, _movimientos[0].Cantidad, _movimientos[0].PrecioUnitario));
            Assert.Equal((2, SignoInventario * 1m, 5m), (_movimientos[1].DocLinea, _movimientos[1].Cantidad, _movimientos[1].PrecioUnitario));
            Assert.Equal(new DateTime(2026, 10, 4), _movimientos[0].Fecha);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_GuardaElArticuloYAlmacenDeLaLinea()
        {
            await _domain.InsertarAsync(NotaArticulos(), new[] { LineaArticulo() });

            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<FacturaDetalle>(l =>
                l.Entry == 99 && l.NoLinea == 1 && l.CodArticulo == "ART1" && l.CodAlmacen == "01" && l.TipoObjeto == CodigoObj)), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_LaDescripcionEsOpcionalYSeRecorta()
        {
            var sinDescripcion = LineaArticulo(l => l.Descripcion = null);
            var conEspacios = LineaArticulo(l => l.Descripcion = "  Tornillo  ");

            await _domain.InsertarAsync(NotaArticulos(), new[] { sinDescripcion, conEspacios });

            Assert.Null(sinDescripcion.Descripcion);
            Assert.Equal("Tornillo", conEspacios.Descripcion);
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_SinArticulo_Lanza()
        {
            Assert.Equal("Línea 1: el artículo es requerido.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(l => l.CodArticulo = " ")));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_SinAlmacen_Lanza()
        {
            Assert.Equal("Línea 1: el almacén es requerido.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(l => l.CodAlmacen = null)));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_ArticuloInexistente_Lanza()
        {
            Assert.Equal("Línea 2: el artículo 'ZZZ' no existe.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(), LineaArticulo(l => l.CodArticulo = "ZZZ")));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_AlmacenInexistente_Lanza()
        {
            Assert.Equal("Línea 1: el almacén 'XX' no existe.",
                await MensajeAlInsertar(NotaArticulos(), LineaArticulo(l => l.CodAlmacen = "XX")));
        }

        [Fact]
        public async Task InsertarAsync_PorArticulos_SiElAsientoFalla_PropagaElError()
        {
            _asiento.Setup(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("fallo de inventario"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _domain.InsertarAsync(NotaArticulos(), new[] { LineaArticulo() }));

            Assert.Equal("fallo de inventario", ex.Message);
        }

        // ---------------- InsertarAsync: modo ----------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("X")]
        public async Task InsertarAsync_ModoInvalido_Lanza(string? modo)
        {
            Assert.Equal(MsgModoInvalido, await MensajeAlInsertar(Nota(n => n.DocType = modo!), Linea()));
        }

        [Theory]
        [InlineData("ART1", null)]
        [InlineData(null, "01")]
        public async Task InsertarAsync_PorConcepto_ConArticuloOAlmacen_Lanza(string? articulo, string? almacen)
        {
            Assert.Equal("Línea 1: una nota por concepto no lleva artículo ni almacén.",
                await MensajeAlInsertar(Nota(), Linea(l => { l.CodArticulo = articulo; l.CodAlmacen = almacen; })));
        }

        // ---------------- InsertarAsync: serie ----------------

        [Fact]
        public async Task InsertarAsync_SerieInexistente_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync((NumeracionDocumentoDet?)null);

            Assert.Equal("La serie no existe.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieDeOtroObjeto_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(codigoObj: CodigoObjOtro));

            Assert.Equal(MsgSerieOtroObjeto, await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieDeFacturaNormal_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(subTipo: "--"));

            Assert.Equal(MsgSerieOtroObjeto, await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieBloqueada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(bloqueado: "S"));

            Assert.Equal("La serie está bloqueada y no se puede usar para registrar notas de débito.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieManualSinNumDoc_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null, manual: "S"));

            Assert.Equal("El número de documento es requerido para series manuales.", await MensajeAlInsertar(Nota(n => n.NumDoc = 0), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieSinSiguienteNumero_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: null));

            Assert.Equal("La serie no tiene configurado el número siguiente.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SerieAgotada_Lanza()
        {
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie(sig: 11, fin: 10));

            Assert.Equal("Se agotó la numeración disponible en esta serie.", await MensajeAlInsertar(Nota(), Linea()));
        }

        // ---------------- InsertarAsync: socio y moneda ----------------

        [Fact]
        public async Task InsertarAsync_SinSocio_Lanza()
        {
            Assert.Equal("El socio de negocio es requerido.", await MensajeAlInsertar(Nota(n => n.CodigoSn = ""), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SocioInexistente_Lanza()
        {
            Assert.Equal("El socio de negocio no existe.", await MensajeAlInsertar(Nota(n => n.CodigoSn = "NOEXISTE"), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SocioDeTipoEquivocado_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocioOpuesto, Activo = "S" });

            Assert.Equal(MsgSocioTipo, await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SocioInactivo_Lanza()
        {
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocio, Activo = "N" });

            Assert.Equal("El socio de negocio está inactivo.", await MensajeAlInsertar(Nota(), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_SinMoneda_Lanza()
        {
            Assert.Equal("La moneda es requerida.", await MensajeAlInsertar(Nota(n => n.MonedaDoc = null), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_MonedaInexistente_Lanza()
        {
            Assert.Equal("La moneda no existe.", await MensajeAlInsertar(Nota(n => n.MonedaDoc = "XXX"), Linea()));
        }

        // ---------------- InsertarAsync: lineas ----------------

        [Fact]
        public async Task InsertarAsync_SinLineas_Lanza()
        {
            Assert.Equal("El documento debe tener al menos una línea.", await MensajeAlInsertar(Nota()));
        }

        [Theory]
        [InlineData("", "Línea 1: la descripción es requerida.")]
        [InlineData("   ", "Línea 1: la descripción es requerida.")]
        public async Task InsertarAsync_PorConcepto_LineaSinDescripcion_Lanza(string descripcion, string esperado)
        {
            Assert.Equal(esperado, await MensajeAlInsertar(Nota(), Linea(l => l.Descripcion = descripcion)));
        }

        [Fact]
        public async Task InsertarAsync_LineaConDescripcionMuyLarga_Lanza()
        {
            Assert.Equal("Línea 1: la descripción no puede superar 200 caracteres.",
                await MensajeAlInsertar(Nota(), Linea(l => l.Descripcion = new string('x', 201))));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public async Task InsertarAsync_LineaConCantidadNoPositiva_Lanza(int cantidad)
        {
            Assert.Equal("Línea 1: la cantidad debe ser mayor que 0.", await MensajeAlInsertar(Nota(), Linea(l => l.Cantidad = cantidad)));
        }

        [Fact]
        public async Task InsertarAsync_LineaSinPrecio_Lanza()
        {
            Assert.Equal("Línea 1: el precio es requerido.", await MensajeAlInsertar(Nota(), Linea(l => l.Precio = null)));
        }

        [Fact]
        public async Task InsertarAsync_LineaConPrecioNegativo_Lanza()
        {
            Assert.Equal("Línea 1: el precio no puede ser negativo.", await MensajeAlInsertar(Nota(), Linea(l => l.Precio = -1m)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        public async Task InsertarAsync_LineaConDescuentoFueraDeRango_Lanza(int descuento)
        {
            Assert.Equal("Línea 1: el descuento debe estar entre 0 y 100.", await MensajeAlInsertar(Nota(), Linea(l => l.PrctjeDesc = descuento)));
        }

        [Fact]
        public async Task InsertarAsync_LineaSinImpuesto_Lanza()
        {
            Assert.Equal("Línea 1: el impuesto es requerido.", await MensajeAlInsertar(Nota(), Linea(l => l.CodigoImpuesto = " ")));
        }

        [Fact]
        public async Task InsertarAsync_LineaConImpuestoInexistente_Lanza()
        {
            Assert.Equal("Línea 2: el impuesto 'ZZZ' no existe.",
                await MensajeAlInsertar(Nota(), Linea(), Linea(l => l.CodigoImpuesto = "ZZZ")));
        }

        [Fact]
        public async Task InsertarAsync_RecortaLaDescripcionYNormalizaDescuentoNulo()
        {
            var linea = Linea(l => { l.Descripcion = "  Flete  "; l.PrctjeDesc = null; });

            await _domain.InsertarAsync(Nota(), new[] { linea });

            Assert.Equal("Flete", linea.Descripcion);
            Assert.Equal(0m, linea.PrctjeDesc);
        }

        [Fact]
        public async Task InsertarAsync_ConErrorDeValidacion_NoGuardaNadaNiAvanzaLaSerie()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);

            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nota(), Array.Empty<FacturaDetalle>()));

            Assert.Equal(5, serie.SigNumero);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoFactura.Verify(r => r.InsertarAsync(It.IsAny<Factura>()), Times.Never);
            _asiento.Verify(a => a.AsentarAsync(It.IsAny<IEnumerable<MovimientoRequest>>(), It.IsAny<bool>()), Times.Never);
        }

        // ---------------- InsertarAsync: factura base ----------------

        private void PrepararFactura(int entry = 12, string socio = "SN1", string cancelado = "N", string subTipo = "--") =>
            _repoFactura.Setup(r => r.ObtenerAsync(entry)).ReturnsAsync(new Factura { Entry = entry, CodigoSn = socio, Cancelado = cancelado, DocSubType = subTipo });

        [Fact]
        public async Task InsertarAsync_ConBaseValida_ConservaLaReferencia()
        {
            PrepararFactura();
            var nota = Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal(TipoBase, nota.BaseTipo);
            Assert.Equal(12, nota.BaseEntry);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(-1)]
        public async Task InsertarAsync_SinBase_NormalizaBaseTipoMenosUnoYBaseEntryNulo(int? baseTipo)
        {
            var nota = Nota(n => { n.BaseTipo = baseTipo; n.BaseEntry = 55; });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal(-1, nota.BaseTipo);
            Assert.Null(nota.BaseEntry);
        }

        [Fact]
        public async Task InsertarAsync_ConTipoBaseInvalido_Lanza()
        {
            Assert.Equal("El tipo de documento base no es válido.", await MensajeAlInsertar(Nota(n => n.BaseTipo = 99), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseSinEntry_Lanza()
        {
            Assert.Equal(MsgBaseRequerida, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = null; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseInexistente_Lanza()
        {
            Assert.Equal(MsgBaseNoExiste, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 777; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseQueEsUnaNotaDeDebito_Lanza()
        {
            PrepararFactura(subTipo: SubTipoNota);

            Assert.Equal(MsgBaseEsNota, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseCancelada_Lanza()
        {
            PrepararFactura(cancelado: "S");

            Assert.Equal(MsgBaseCancelada, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; }), Linea()));
        }

        [Fact]
        public async Task InsertarAsync_ConBaseDeOtroSocio_Lanza()
        {
            PrepararFactura(socio: "OTRO");

            Assert.Equal(MsgBaseOtroSocio, await MensajeAlInsertar(Nota(n => { n.BaseTipo = TipoBase; n.BaseEntry = 12; }), Linea()));
        }

        // ---------------- ActualizarAsync ----------------

        private void PrepararNota(Factura nota) => _repoFactura.Setup(r => r.ObtenerAsync(nota.Entry)).ReturnsAsync(nota);

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((Factura?)null);

            Assert.False(await _domain.ActualizarAsync(5, new Factura()));
        }

        [Fact]
        public async Task ActualizarAsync_DeUnaFacturaNormal_DevuelveFalse()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--", Cancelado = "N" });

            Assert.False(await _domain.ActualizarAsync(5, new Factura { Cancelado = "S" }));

            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CancelarPorConcepto_MarcaCanceladoSinTocarInventarioNiComentario()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "S", Cancelado = "N", EstadoDoc = "A", EstadoInv = "C", Comentario = "original" };
            PrepararNota(existente);

            var ok = await _domain.ActualizarAsync(5, new Factura { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("S", existente.Cancelado);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("original", existente.Comentario);
            Assert.Equal("A", existente.EstadoDoc);   // EstadoDoc no cambia al cancelar
            Assert.Equal("C", existente.EstadoInv);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_CancelarPorArticulos_RevierteElInventarioYMarcaEstadoInvC()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "I", Cancelado = "N", EstadoInv = "A", EstadoDoc = "A" };
            PrepararNota(existente);

            var ok = await _domain.ActualizarAsync(5, new Factura { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("S", existente.Cancelado);
            Assert.Equal("C", existente.EstadoInv);
            Assert.NotNull(existente.FechaCancelado);
            _asiento.Verify(a => a.RevertirAsync(CodigoObj, 5), Times.Once);
        }

        [Fact]
        public async Task ActualizarAsync_CancelarConComentario_ActualizaElComentario()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "S", Cancelado = "N", Comentario = "original" };
            PrepararNota(existente);

            await _domain.ActualizarAsync(5, new Factura { Cancelado = "S", Comentario = "motivo" });

            Assert.Equal("motivo", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_SoloComentario_NoCancelaNiCambiaOtrosCampos()
        {
            var existente = new Factura { Entry = 5, DocSubType = SubTipoNota, DocType = "I", Cancelado = "N", Comentario = "viejo", TotalDoc = 50m, CodigoSn = "SN1" };
            PrepararNota(existente);

            await _domain.ActualizarAsync(5, new Factura { Cancelado = "N", Comentario = "nuevo", TotalDoc = 999m, CodigoSn = "OTRO" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("N", existente.Cancelado);
            Assert.Equal(50m, existente.TotalDoc);
            Assert.Equal("SN1", existente.CodigoSn);
            _asiento.Verify(a => a.RevertirAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoCancelado_NoSePuedeModificar()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, Cancelado = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, new Factura { Comentario = "x" }));

            Assert.Equal("El documento está cancelado y no se puede modificar.", ex.Message);
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            _repoFactura.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((Factura?)null);

            Assert.False(await _domain.EliminarAsync(5));
        }

        [Fact]
        public async Task EliminarAsync_DeUnaFacturaNormal_DevuelveFalse_SinBorrar()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--", Cancelado = "S" });

            Assert.False(await _domain.EliminarAsync(5));

            _repoFactura.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_SinCancelar_Lanza()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, Cancelado = "N" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5));

            Assert.Equal("Cancele el documento (Cancelado='S') antes de eliminarlo.", ex.Message);
            _repoFactura.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLasLineasYElEncabezado()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, Cancelado = "S" });
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 5, NoLinea = 1 }, new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoFactura.Setup(r => r.EliminarAsync(5)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(5);

            Assert.True(ok);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 1)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 5 && k.NoLinea == 2)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync(It.Is<(int Entry, int NoLinea)>(k => k.Entry == 6 && k.NoLinea == 1)), Times.Never);
            _repoFactura.Verify(r => r.EliminarAsync(5), Times.Once);
        }

        // ---------------- Consultas ----------------

        [Fact]
        public async Task ObtenerAsync_DeUnaNota_LaDevuelve()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota, NumDoc = 9 });

            Assert.Equal(9, (await _domain.ObtenerAsync(5))!.NumDoc);
        }

        [Fact]
        public async Task ObtenerAsync_DeUnaFacturaNormal_DevuelveNull()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--" });

            Assert.Null(await _domain.ObtenerAsync(5));
        }

        [Fact]
        public async Task ObtenerTodoAsync_DevuelveSoloLasNotas()
        {
            var filas = new List<Factura>
            {
                new() { Entry = 1, DocSubType = "--" }, new() { Entry = 2, DocSubType = SubTipoNota }, new() { Entry = 3, DocSubType = SubTipoNota }
            };
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerTodoAsync()).ToList();

            Assert.Equal(new[] { 2, 3 }, resultado.Select(n => n.Entry));
        }

        [Fact]
        public async Task ObtenerLineasAsync_DevuelveSoloLasDeLaNotaEnOrden()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = SubTipoNota });
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }, new() { Entry = 5, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerLineasAsync(5)).ToList();

            Assert.Equal(new[] { 1, 2 }, resultado.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerLineasAsync_DeUnaFacturaNormal_DevuelveVacio()
        {
            PrepararNota(new Factura { Entry = 5, DocSubType = "--" });
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(new List<FacturaDetalle> { new() { Entry = 5, NoLinea = 1 } }.AsAsyncQueryable());

            Assert.Empty(await _domain.ObtenerLineasAsync(5));
        }

        [Fact]
        public async Task ObtenerFacturasBaseAsync_DevuelveLasFacturasNormalesNoCanceladasDelSocioDeLaMasNuevaALaMasVieja()
        {
            var facturas = new List<Factura>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N" },
                new() { Entry = 2, CodigoSn = "SN1", Cancelado = "S" },
                new() { Entry = 3, CodigoSn = "OTRO", Cancelado = "N" },
                new() { Entry = 4, CodigoSn = "SN1", Cancelado = "N" },
                new() { Entry = 5, CodigoSn = "SN1", Cancelado = "N", DocSubType = SubTipoNota }   // una nota no es factura base
            };
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(facturas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerFacturasBaseAsync("SN1")).ToList();

            Assert.Equal(new[] { 4, 1 }, resultado.Select(f => f.Entry));
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_DevuelveEncabezadoYLineasOrdenadas()
        {
            PrepararFactura(entry: 12);
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 12, NoLinea = 2 }, new() { Entry = 13, NoLinea = 1 }, new() { Entry = 12, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

            var resultado = await _domain.ObtenerFacturaBaseAsync(12);

            Assert.Equal(12, resultado.Documento.Entry);
            Assert.Equal(new[] { 1, 2 }, resultado.Lineas.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_Inexistente_Lanza()
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaBaseAsync(777));

            Assert.Equal(MsgFacturaNoExiste, ex.Message);
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_DeUnaNota_LanzaNoExiste()
        {
            PrepararFactura(entry: 12, subTipo: SubTipoNota);

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaBaseAsync(12));

            Assert.Equal(MsgFacturaNoExiste, ex.Message);
        }

        [Fact]
        public async Task ObtenerFacturaBaseAsync_Cancelada_Lanza()
        {
            PrepararFactura(entry: 12, cancelado: "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaBaseAsync(12));

            Assert.Equal(MsgFacturaCancelada, ex.Message);
        }
    }
}
```

Si `Articulo` o `Almacen` declaran miembros `required`/no anulables sin inicializar que impidan `new Articulo { Codigo = "ART1" }` / `new Almacen { Codigo = "01" }`, completar solo lo mínimo para que compile y anotarlo en el reporte.

- [ ] **Step 2: Verificar que fallan**

`dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoDomainTests"` → no compila (`NotaDebitoDomain` todavía recibe `NotaDebito`).

- [ ] **Step 3: Interfaz del dominio**

Sobrescribir `API.Domain.Interface/INotaDebitoDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaDebitoDomain
    {
        #region async methods
        Task<int> InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas);
        Task<bool> ActualizarAsync(int id, Factura obj);
        Task<bool> EliminarAsync(int id);
        Task<Factura?> ObtenerAsync(int id);
        Task<IQueryable<Factura>> ObtenerTodoAsync();
        Task<IEnumerable<FacturaDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<FacturaBase> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
```

- [ ] **Step 4: Dominio**

Sobrescribir `API.Domain.Core/NotaDebitoDomain.cs` (el fin de línea que tenga el archivo actual; verificar con `file` y conservarlo) con:

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    /// <summary>
    /// Nota de débito de cliente. Se guarda en Factura/FacturaDetalle con DocSubType = 'ND' (modelo SAP B1).
    /// Por artículos (DocType 'I') mueve inventario como la factura; por concepto (DocType 'S') no.
    /// </summary>
    public class NotaDebitoDomain : INotaDebitoDomain
    {
        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        // Factura.TipoObjeto / FacturaDetalle.TipoObjeto y TipoDoc del asiento de inventario.
        private const string TipoObjetoDocumento = "6";
        private const string SubTipoNota = "ND";                // Factura.DocSubType de la nota y SubTipoDoc de su serie
        private const string TipoSocio = "C";
        private const int TipoDocBase = 6;
        private const decimal SignoInventario = -1m;            // cliente: salida de stock (la de proveedor: +1, entrada)
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de débito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseEsNota = "La factura base no puede ser una nota de débito.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private const string SubTipoFactura = "--";
        private const string ModoArticulos = "I";
        private const string ModoConcepto = "S";
        private const string MsgModoInvalido = "Indique el tipo de nota: por artículos ('I') o por concepto ('S').";
        private const int SinBase = -1;
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<Articulo, string> _repoArticulo;
        private readonly IRepositorioGenerico<Almacen, string> _repoAlmacen;
        private readonly IEjecutorTransaccion _tx;
        private readonly IInventarioAsientoService _asiento;

        public NotaDebitoDomain(
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<Monedum, string> repoMoneda,
            IRepositorioGenerico<Articulo, string> repoArticulo,
            IRepositorioGenerico<Almacen, string> repoAlmacen,
            IEjecutorTransaccion tx,
            IInventarioAsientoService asiento)
        {
            _repoFactura = repoFactura;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoImpuesto = repoImpuesto;
            _repoMoneda = repoMoneda;
            _repoArticulo = repoArticulo;
            _repoAlmacen = repoAlmacen;
            _tx = tx;
            _asiento = asiento;
        }

        #region async methods
        public async Task<int> InsertarAsync(Factura obj, IEnumerable<FacturaDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<FacturaDetalle>();

            obj.DocSubType = SubTipoNota;
            obj.TipoObjeto = TipoObjetoDocumento;
            obj.Cancelado = "N";                  // un documento recien registrado nunca nace cancelado
            obj.EstadoDoc = "A";
            obj.Imprimido = "N";                  // nunca nace impreso, ignora lo que mande el cliente
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaEmision ??= obj.FechaDoc;

            if (obj.DocType != ModoArticulos && obj.DocType != ModoConcepto)
                throw new Exception(MsgModoInvalido);

            var serie = await ValidarSerieAsync(obj);
            await ValidarSocioYMonedaAsync(obj);
            await ValidarLineasAsync(obj, lineasList);
            await ValidarBaseAsync(obj);

            // EstadoInv siempre explicito: el DEFAULT 'A' de la base no debe decidir si la nota tiene asiento.
            obj.EstadoInv = obj.DocType == ModoArticulos ? "A" : "C";

            AsignarNumero(obj, serie);

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoFactura.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    linea.TipoObjeto = TipoObjetoDocumento;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                if (obj.DocType == ModoArticulos)
                {
                    var movimientos = lineasList
                        .Select(l => new MovimientoRequest(
                            TipoDoc: TipoObjetoDocumento,
                            DocEntry: obj.Entry,
                            DocLinea: l.NoLinea,
                            CodArticulo: l.CodArticulo!,
                            CodAlmacen: l.CodAlmacen!,
                            Cantidad: SignoInventario * l.Cantidad!.Value,
                            PrecioUnitario: l.Precio ?? 0m,
                            Fecha: obj.FechaDoc ?? DateTime.Now))
                        .ToList();

                    await _asiento.AsentarAsync(movimientos);
                }

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (lineas + inventario) + Commit.
        }

        public async Task<bool> ActualizarAsync(int id, Factura obj)
        {
            var existente = await ObtenerNotaAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelacion: Cancelado pasa a 'S' (EstadoDoc no cambia, igual que en Factura).
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    if (existente.DocType == ModoArticulos)
                        await _asiento.RevertirAsync(TipoObjetoDocumento, id);

                    existente.Cancelado = "S";
                    existente.EstadoInv = "C";
                    existente.FechaCancelado = DateTime.Now;
                    // Solo se toca el comentario si el cliente lo envio: el boton "Cancelar documento"
                    // del Web manda unicamente { Cancelado: 'S' } y no debe borrar la nota existente.
                    if (obj.Comentario != null)
                        existente.Comentario = obj.Comentario;
                    return true;
                });
            }

            // Edicion inocua: solo el comentario (replace-semantics: enviar vacio lo borra).
            return await _tx.EjecutarAsync(async () =>
            {
                await Task.CompletedTask;
                existente.Comentario = obj.Comentario;
                return true;
            });
        }

        public async Task<bool> EliminarAsync(int id)
        {
            var existente = await ObtenerNotaAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado != "S")
                throw new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo.");

            return await _tx.EjecutarAsync(async () =>
            {
                var detalles = await _repoDetalle.ObtenerTodoAsync();
                var lineas = await detalles.Where(d => d.Entry == id).ToListAsync();
                foreach (var linea in lineas)
                    await _repoDetalle.EliminarAsync((linea.Entry, linea.NoLinea));

                return await _repoFactura.EliminarAsync(id);
            });
        }

        public async Task<Factura?> ObtenerAsync(int id)
        {
            return await ObtenerNotaAsync(id);
        }

        public async Task<IQueryable<Factura>> ObtenerTodoAsync()
        {
            var filas = await _repoFactura.ObtenerTodoAsync();
            return filas.Where(f => f.DocSubType == SubTipoNota);
        }

        public async Task<IEnumerable<FacturaDetalle>> ObtenerLineasAsync(int id)
        {
            if (await ObtenerNotaAsync(id) is null)
                return Array.Empty<FacturaDetalle>();

            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var queryable = await _repoFactura.ObtenerTodoAsync();
            return await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S" && f.DocSubType == SubTipoFactura)
                .OrderByDescending(f => f.Entry)
                .ToListAsync();
        }

        public async Task<FacturaBase> ObtenerFacturaBaseAsync(int entry)
        {
            var factura = await _repoFactura.ObtenerAsync(entry);
            if (factura is null || factura.DocSubType != SubTipoFactura)
                throw new Exception(MsgFacturaNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgFacturaCancelada);

            var queryable = await _repoDetalle.ObtenerTodoAsync();
            var lineas = await queryable.Where(l => l.Entry == entry).OrderBy(l => l.NoLinea).ToListAsync();
            return new FacturaBase(factura, lineas);
        }
        #endregion

        #region validaciones
        // Una fila de Factura con otro DocSubType es una factura normal: su API es otra.
        private async Task<Factura?> ObtenerNotaAsync(int id)
        {
            var fila = await _repoFactura.ObtenerAsync(id);
            return fila is not null && fila.DocSubType == SubTipoNota ? fila : null;
        }

        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(Factura obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoDocumento || serie.SubTipoDoc != SubTipoNota)
                throw new Exception(MsgSerieOtroObjeto);

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar notas de débito.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeracion automatica se asigna al final (despues de validar todo) para no avanzar el
        // consecutivo de una serie tracked si el documento resulta invalido.
        private static void AsignarNumero(Factura obj, NumeracionDocumentoDet serie)
        {
            if (serie.Manual == "S")
            {
                obj.NumManual = "S";
                return;
            }

            if (serie.SigNumero == null)
                throw new Exception("La serie no tiene configurado el número siguiente.");
            if (serie.FinNumero.HasValue && serie.SigNumero.Value > serie.FinNumero.Value)
                throw new Exception("Se agotó la numeración disponible en esta serie.");

            obj.NumManual = "N";
            obj.NumDoc = serie.SigNumero.Value;
            serie.SigNumero = serie.SigNumero.Value + 1; // rastreada por el mismo contexto; se persiste en el Save de la transaccion
        }

        private async Task ValidarSocioYMonedaAsync(Factura obj)
        {
            if (string.IsNullOrWhiteSpace(obj.CodigoSn))
                throw new Exception("El socio de negocio es requerido.");

            var socio = await _repoSocio.ObtenerAsync(obj.CodigoSn)
                ?? throw new Exception("El socio de negocio no existe.");

            if (socio.TipoSn != TipoSocio)
                throw new Exception(MsgSocioTipo);

            if (socio.Activo == "N")
                throw new Exception("El socio de negocio está inactivo.");

            if (string.IsNullOrWhiteSpace(obj.MonedaDoc))
                throw new Exception("La moneda es requerida.");

            if (await _repoMoneda.ObtenerAsync(obj.MonedaDoc) is null)
                throw new Exception("La moneda no existe.");
        }

        private async Task ValidarLineasAsync(Factura obj, IList<FacturaDetalle> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("El documento debe tener al menos una línea.");

            var porArticulos = obj.DocType == ModoArticulos;
            var impuestosVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var articulosVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var almacenesVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lineas.Count; i++)
            {
                var n = i + 1;
                var linea = lineas[i];

                var descripcion = (linea.Descripcion ?? string.Empty).Trim();
                if (!porArticulos && descripcion.Length == 0)
                    throw new Exception($"Línea {n}: la descripción es requerida.");
                if (descripcion.Length > LongitudMaximaDescripcion)
                    throw new Exception($"Línea {n}: la descripción no puede superar {LongitudMaximaDescripcion} caracteres.");
                linea.Descripcion = descripcion.Length == 0 ? null : descripcion;

                if (porArticulos)
                {
                    if (string.IsNullOrWhiteSpace(linea.CodArticulo))
                        throw new Exception($"Línea {n}: el artículo es requerido.");
                    if (string.IsNullOrWhiteSpace(linea.CodAlmacen))
                        throw new Exception($"Línea {n}: el almacén es requerido.");

                    if (articulosVerificados.Add(linea.CodArticulo) && await _repoArticulo.ObtenerAsync(linea.CodArticulo) is null)
                        throw new Exception($"Línea {n}: el artículo '{linea.CodArticulo}' no existe.");
                    if (almacenesVerificados.Add(linea.CodAlmacen) && await _repoAlmacen.ObtenerAsync(linea.CodAlmacen) is null)
                        throw new Exception($"Línea {n}: el almacén '{linea.CodAlmacen}' no existe.");
                }
                else if (!string.IsNullOrWhiteSpace(linea.CodArticulo) || !string.IsNullOrWhiteSpace(linea.CodAlmacen))
                {
                    throw new Exception($"Línea {n}: una nota por concepto no lleva artículo ni almacén.");
                }

                if ((linea.Cantidad ?? 0m) <= 0m)
                    throw new Exception($"Línea {n}: la cantidad debe ser mayor que 0.");

                if (linea.Precio is null)
                    throw new Exception($"Línea {n}: el precio es requerido.");
                if (linea.Precio < 0m)
                    throw new Exception($"Línea {n}: el precio no puede ser negativo.");

                var descuento = linea.PrctjeDesc ?? 0m;
                if (descuento < 0m || descuento > 100m)
                    throw new Exception($"Línea {n}: el descuento debe estar entre 0 y 100.");
                linea.PrctjeDesc = descuento;

                if (string.IsNullOrWhiteSpace(linea.CodigoImpuesto))
                    throw new Exception($"Línea {n}: el impuesto es requerido.");

                if (impuestosVerificados.Add(linea.CodigoImpuesto))
                {
                    if (await _repoImpuesto.ObtenerAsync(linea.CodigoImpuesto) is null)
                        throw new Exception($"Línea {n}: el impuesto '{linea.CodigoImpuesto}' no existe.");
                }
            }
        }

        private async Task ValidarBaseAsync(Factura obj)
        {
            var tipo = obj.BaseTipo ?? SinBase;

            if (tipo == SinBase)
            {
                obj.BaseTipo = SinBase;
                obj.BaseEntry = null;
                return;
            }

            if (tipo != TipoDocBase)
                throw new Exception("El tipo de documento base no es válido.");

            if (obj.BaseEntry is null)
                throw new Exception(MsgBaseRequerida);

            var factura = await _repoFactura.ObtenerAsync(obj.BaseEntry.Value)
                ?? throw new Exception(MsgBaseNoExiste);

            if (factura.DocSubType != SubTipoFactura)
                throw new Exception(MsgBaseEsNota);

            if (factura.Cancelado == "S")
                throw new Exception(MsgBaseCancelada);

            if (!string.Equals(factura.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                throw new Exception(MsgBaseOtroSocio);
        }
        #endregion
    }
}
```

- [ ] **Step 5: Pruebas del dominio verdes**

`dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoDomainTests"` → verde. Un fallo en una prueba de este archivo se resuelve revisando primero la constante/mensaje contra la Global Constraints; no se debilita ninguna aserción.

- [ ] **Step 6: DTOs**

Con `Edit` (conservando el fin de línea de cada archivo):

1. `API.Application.DTO/notaDebito/NotaDebitoCrearDTO.cs`: después de `public string? TipoObjeto { get; set; }` insertar
```csharp
        /// <summary>Tipo de nota: "I" = por artículos (mueve inventario), "S" = por concepto. Obligatorio al crear.</summary>
        public string? DocType { get; set; }
```
   y cambiar el comentario de `Lineas` a `/// <summary>Lineas del documento (artículos o conceptos según DocType): se registran junto con el encabezado y no se pueden modificar despues.</summary>`.
2. `NotaDebitoDTO.cs`: después de `public string? TipoObjeto { get; set; }` insertar `public string? DocType { get; set; }`.
3. `NotaDebitoDetalleCrearDTO.cs`: después del comentario `// Entry y NoLinea los asigna el servidor al registrar el documento.` insertar
```csharp
        // Solo en notas por artículos (DocType "I"); en notas por concepto deben ir vacíos.
        public string? CodArticulo { get; set; }
        public string? CodAlmacen { get; set; }
```
4. `NotaDebitoDetalleDTO.cs`: después de `public int NoLinea { get; set; }` insertar `public string? CodArticulo { get; set; }` y `public string? CodAlmacen { get; set; }`.

- [ ] **Step 7: Mapeo y aplicación**

En `API.Transversal.Mapper/PerfilMapeo.cs`, bloque `// NotaDebito` y `// NotaDebitoDetalle` (≈ líneas 202-209), reemplazar exactamente estas cinco líneas (cada una aparece una sola vez; comprobar con `grep -c`):

| antes | después |
|---|---|
| `CreateMap<NotaDebito, NotaDebitoDTO>();` | `CreateMap<Factura, NotaDebitoDTO>();` |
| `CreateMap<NotaDebitoCrearDTO, NotaDebito>();` | `CreateMap<NotaDebitoCrearDTO, Factura>();` |
| `CreateMap<NotaDebitoActualizarDTO, NotaDebito>();` | `CreateMap<NotaDebitoActualizarDTO, Factura>();` |
| `CreateMap<NotaDebitoDetalle, NotaDebitoDetalleDTO>();` | `CreateMap<FacturaDetalle, NotaDebitoDetalleDTO>();` |
| `CreateMap<NotaDebitoDetalleCrearDTO, NotaDebitoDetalle>();` | `CreateMap<NotaDebitoDetalleCrearDTO, FacturaDetalle>();` |

(Las líneas de `NotaDebitoCompra*` no se tocan aquí: son de la Task 6.)

En `API.Application.Main/NotaDebitoApplication.cs` aplicar (bash, desde la raíz del repo API; el archivo no tiene cambios pendientes):

```bash
sed -i -E 's/\bNotaDebitoDetalle\b/FacturaDetalle/g; s/\bNotaDebito\b/Factura/g' API.Application.Main/NotaDebitoApplication.cs
grep -n "Factura\b\|FacturaDetalle\b" API.Application.Main/NotaDebitoApplication.cs      # Map<Factura> x2, Map<IEnumerable<FacturaDetalle>> x1
```

Y en `ObtenerFacturaBaseAsync` reemplazar el comentario y el objeto de la línea, para devolver también artículo y almacén:

```csharp
                    // Las lineas de la factura se devuelven con su articulo y almacen: la nota por articulos
                    // las usa tal cual y la nota por concepto (Web) descarta esos dos campos.
                    Lineas = baseDoc.Lineas.Select(l => new NotaDebitoDetalleCrearDTO
                    {
                        CodArticulo = l.CodArticulo,
                        CodAlmacen = l.CodAlmacen,
                        Descripcion = string.IsNullOrWhiteSpace(l.Descripcion) ? (l.CodArticulo ?? string.Empty) : l.Descripcion,
                        Cantidad = l.Cantidad,
                        Precio = l.Precio,
                        PrecioBruto = l.PrecioBruto,
                        PrctjeDesc = l.PrctjeDesc,
                        CodigoImpuesto = l.CodigoImpuesto,
                        Impuesto = l.Impuesto,
                        TotalLinea = l.TotalLinea
                    }).ToList()
```

- [ ] **Step 8: Pruebas de aplicación y de mapeo**

```bash
sed -i -E 's/\bNotaDebitoDetalle\b/FacturaDetalle/g; s/\bNotaDebito\b/Factura/g' API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs
```

En `NotaDebitoApplicationTests.cs` reemplazar el método `ObtenerFacturaBase_ConvierteLasLineasEnConceptos` completo por:

```csharp
        [Fact]
        public async Task ObtenerFacturaBase_DevuelveLasLineasConArticuloYAlmacen()
        {
            var factura = new Factura { Entry = 12, NumDoc = 100, CodigoSn = "SN1", NombreSn = "Cliente", MonedaDoc = "QTZ", Direccion = "Zona 1" };
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 12, NoLinea = 1, CodArticulo = "ART1", CodAlmacen = "01", Descripcion = "Tornillo", Cantidad = 3m, Precio = 5m, PrecioBruto = 15m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = 1.8m, TotalLinea = 16.8m },
                new() { Entry = 12, NoLinea = 2, CodArticulo = "ART2", CodAlmacen = "02", Descripcion = "  ", Cantidad = 1m, Precio = 10m, CodigoImpuesto = "IVA" }
            };
            _domain.Setup(d => d.ObtenerFacturaBaseAsync(12)).ReturnsAsync(new FacturaBase(factura, lineas));

            var respuesta = await _app.ObtenerFacturaBaseAsync(12);

            Assert.True(respuesta.Resultado);
            var dato = respuesta.Dato;
            Assert.Equal((12, 100, "SN1", "Cliente", "QTZ", "Zona 1"), (dato.Entry, dato.NumDoc, dato.CodigoSn, dato.NombreSn, dato.MonedaDoc, dato.Direccion));
            Assert.Equal(2, dato.Lineas.Count);
            Assert.Equal(("ART1", "01", "Tornillo", 16.8m), (dato.Lineas[0].CodArticulo, dato.Lineas[0].CodAlmacen, dato.Lineas[0].Descripcion, dato.Lineas[0].TotalLinea));
            Assert.Equal(("ART2", "02", "ART2"), (dato.Lineas[1].CodArticulo, dato.Lineas[1].CodAlmacen, dato.Lineas[1].Descripcion));   // sin descripcion: usa el codigo del articulo
        }
```

Agregar al final de `NotaDebitoApplicationTests`:

```csharp
        [Fact]
        public async Task Insertar_MapeaElModoYElArticuloDeLasLineas()
        {
            Factura? recibida = null;
            List<FacturaDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<Factura>(), It.IsAny<IEnumerable<FacturaDetalle>>()))
                .Callback<Factura, IEnumerable<FacturaDetalle>>((n, l) => { recibida = n; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(7);
            var dto = new NotaDebitoCrearDTO
            {
                Serie = 33, CodigoSn = "SN1", MonedaDoc = "QTZ", DocType = "I",
                Lineas = { new NotaDebitoDetalleCrearDTO { CodArticulo = "ART1", CodAlmacen = "01", Cantidad = 2, Precio = 10m, CodigoImpuesto = "IVA" } }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal("I", recibida!.DocType);
            var linea = Assert.Single(lineasRecibidas!);
            Assert.Equal(("ART1", "01"), (linea.CodArticulo, linea.CodAlmacen));
        }
```

En `NotaDebitoMapeoTests.cs` agregar a la clase:

```csharp
        [Fact]
        public void Cliente_MapeaElModoYElArticuloEnAmbosSentidos()
        {
            var entidad = _mapper.Map<Factura>(new NotaDebitoCrearDTO { DocType = "I", Serie = 33 });
            var linea = _mapper.Map<FacturaDetalle>(new NotaDebitoDetalleCrearDTO { CodArticulo = "ART1", CodAlmacen = "01", CodigoImpuesto = "IVA" });
            var dto = _mapper.Map<NotaDebitoDTO>(new Factura { Entry = 4, DocType = "S", DocSubType = "ND" });
            var lineaDto = _mapper.Map<NotaDebitoDetalleDTO>(new FacturaDetalle { Entry = 4, NoLinea = 1, CodArticulo = "ART1", CodAlmacen = "01" });

            Assert.Equal("I", entidad.DocType);
            Assert.Equal(("ART1", "01"), (linea.CodArticulo, linea.CodAlmacen));
            Assert.Equal("S", dto.DocType);
            Assert.Equal(("ART1", "01"), (lineaDto.CodArticulo, lineaDto.CodAlmacen));
        }
```

- [ ] **Step 9: Suite completa y commit**

`dotnet test API.sln` completo verde (cero fallos; reportar el total real: sube respecto a 1380 por las pruebas nuevas del dominio de notas). Revisar `git diff --stat`: los archivos sobrescritos muestran diffs amplios (esperado) pero `PerfilMapeo.cs` solo 5 líneas y los DTO unas pocas.

```bash
git add API.Domain.Core/NotaDebitoDomain.cs API.Domain.Interface/INotaDebitoDomain.cs API.Application.Main/NotaDebitoApplication.cs API.Application.DTO/notaDebito/NotaDebitoCrearDTO.cs API.Application.DTO/notaDebito/NotaDebitoDTO.cs API.Application.DTO/notaDebito/NotaDebitoDetalleCrearDTO.cs API.Application.DTO/notaDebito/NotaDebitoDetalleDTO.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs
git commit -m "feat(notas-debito): nota de debito de cliente sobre Factura con modo por articulos o por concepto

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Nota de débito de proveedor sobre `FacturaCompra` (derivada del cliente)

**Files:**
- Overwrite (derivados con `sed`): `API.Domain.Core/NotaDebitoCompraDomain.cs`, `API.Domain.Interface/INotaDebitoCompraDomain.cs`, `API.Application.Main/NotaDebitoCompraApplication.cs`, `API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs`
- Modify: `API.Application.DTO/notaDebitoCompra/NotaDebitoCompraCrearDTO.cs`, `NotaDebitoCompraDTO.cs`, `NotaDebitoCompraDetalleCrearDTO.cs`, `NotaDebitoCompraDetalleDTO.cs`; `API.Transversal.Mapper/PerfilMapeo.cs`; `API.Service.WebApi.Tests/Application/NotaDebitoCompraApplicationTests.cs`, `NotaDebitoMapeoTests.cs`

**Interfaces:**
- Consumes: `NotaDebitoDomain`, `INotaDebitoDomain`, `NotaDebitoApplication` y sus pruebas (Task 5); `FacturaCompra`/`FacturaCompraDetalle` (Task 2); `FacturaCompraBase` (record existente).
- Produces: `NotaDebitoCompraDomain : INotaDebitoCompraDomain` con el mismo orden de constructor que el cliente pero sobre `FacturaCompra`/`FacturaCompraDetalle` (`TipoObjetoDocumento "13"`, `SubTipoNota "DN"`, `TipoSocio "P"`, `TipoDocBase 13`, `SignoInventario 1m` → entrada de stock); métodos `ObtenerFacturasCompraBaseAsync(string)` y `ObtenerFacturaCompraBaseAsync(int)`; DTOs de compra con `DocType`, `CodArticulo`, `CodAlmacen`.

**Precondición verificable:** Task 5 commiteada y verde; `git diff --quiet` sobre los archivos de la lista "Overwrite" y "Modify" (sin cambios pendientes).

- [ ] **Step 1: Generar dominio, interfaz, aplicación y pruebas del proveedor**

Desde la raíz del repo `API` (bash). Los archivos destino ya existen y están commiteados: el script los sobrescribe (el chequeo de `git diff --quiet` de la precondición ya lo garantiza). El orden de los `sed` importa: valores propios del documento primero, luego renombres estructurales.

```bash
set -e
mirror() {   # $1 = origen (cliente)  $2 = destino (proveedor, se sobrescribe)
  sed -E \
    -e 's|private const string TipoObjetoDocumento = "6";|private const string TipoObjetoDocumento = "13";|' \
    -e 's|private const string SubTipoNota = "ND";|private const string SubTipoNota = "DN";|' \
    -e 's|private const string TipoSocio = "C";|private const string TipoSocio = "P";|' \
    -e 's|private const int TipoDocBase = 6;|private const int TipoDocBase = 13;|' \
    -e 's|private const decimal SignoInventario = -1m;( *)// cliente: salida de stock \(la de proveedor: \+1, entrada\)|private const decimal SignoInventario = 1m;\1// proveedor: entrada de stock (la de cliente: -1, salida)|' \
    -e 's|private const decimal SignoInventario = -1m;|private const decimal SignoInventario = 1m;|' \
    -e 's|private const string CodigoObj = "6";|private const string CodigoObj = "13";|' \
    -e 's|private const string CodigoObjOtro = "5";|private const string CodigoObjOtro = "12";|' \
    -e 's|private const string TipoSocioOpuesto = "P";|private const string TipoSocioOpuesto = "C";|' \
    -e 's|private const int TipoBase = 6;|private const int TipoBase = 13;|' \
    -e 's|/// Nota de débito de cliente\.|/// Nota de débito de proveedor.|' \
    -e "s|DocSubType = 'ND'|DocSubType = 'DN'|" \
    -e 's|notas de débito de cliente\.|notas de débito de proveedor.|' \
    -e 's|debe ser un cliente\.|debe ser un proveedor.|' \
    -e 's|"Indique la factura base\."|"Indique la factura de compra base."|' \
    -e 's|"La factura base no existe\."|"La factura de compra base no existe."|' \
    -e 's|"La factura base no puede ser una nota de débito\."|"La factura de compra base no puede ser una nota de débito."|' \
    -e 's|"La factura base está cancelada\."|"La factura de compra base está cancelada."|' \
    -e 's|"La factura base pertenece a otro socio de negocio\."|"La factura de compra base pertenece a otro socio de negocio."|' \
    -e 's|"La factura no existe\."|"La factura de compra no existe."|' \
    -e 's|"La factura está cancelada\."|"La factura de compra está cancelada."|' \
    -e 's/ObtenerFacturasBaseAsync/ObtenerFacturasCompraBaseAsync/g' \
    -e 's/ObtenerFacturaBaseAsync/ObtenerFacturaCompraBaseAsync/g' \
    -e 's/notaDebito/notaDebitoCompra/g' \
    -e 's/NotaDebito/NotaDebitoCompra/g' \
    -e 's/FacturaBase\b/FacturaCompraBase/g' \
    -e 's/FacturaDetalle/FacturaCompraDetalle/g' \
    -e 's/_repoFactura\b/_repoFacturaCompra/g' \
    -e 's/\brepoFactura\b/repoFacturaCompra/g' \
    -e 's/\bFactura\b/FacturaCompra/g' \
    "$1" > "$2.tmp" && mv "$2.tmp" "$2"
}
mirror API.Domain.Core/NotaDebitoDomain.cs API.Domain.Core/NotaDebitoCompraDomain.cs
mirror API.Domain.Interface/INotaDebitoDomain.cs API.Domain.Interface/INotaDebitoCompraDomain.cs
mirror API.Application.Main/NotaDebitoApplication.cs API.Application.Main/NotaDebitoCompraApplication.cs
mirror API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs
```

(El derivado `sed` de Application e Interface reproduce byte a byte los archivos de proveedor ya commiteados cuando se aplica al estado anterior del cliente; se verificó antes de escribir este plan.)

- [ ] **Step 2: Revisar lo generado**

```bash
git diff -U0 -- API.Domain.Core/NotaDebitoCompraDomain.cs | grep '^[+-]' | grep -v '^+++\|^---' | head -150
```

El cambio sobre el archivo previo de proveedor debe ser exactamente el del Task 5 (entidades `FacturaCompra*`, constructor con `repoArticulo`/`repoAlmacen`/`asiento`, modos, inventario, filtros por `DocSubType`) con las constantes `"13"`/`"DN"`/`"P"`/`13`/`1m` y los mensajes de proveedor. Verificar a mano, porque el `sed` no puede garantizarlo:

(a) `grep -n 'TipoObjetoDocumento = \|SubTipoNota = \|TipoSocio = \|TipoDocBase = \|SignoInventario = ' API.Domain.Core/NotaDebitoCompraDomain.cs` → `"13"`, `"DN"`, `"P"`, `13`, `1m` (con el comentario "proveedor: entrada de stock").
(b) `grep -n '"6"\|"ND"\|-1m\| cliente\|de cliente' API.Domain.Core/NotaDebitoCompraDomain.cs` → sin coincidencias (salvo comentarios que expliquen la diferencia con el cliente).
(c) La propiedad `Documento` del record sigue usándose: `grep -n "FacturaCompraBase" API.Domain.Core/NotaDebitoCompraDomain.cs API.Domain.Interface/INotaDebitoCompraDomain.cs`.
(d) En las pruebas: `grep -n 'CodigoObj = \|CodigoObjOtro = \|SubTipoNota = \|TipoSocio = \|TipoSocioOpuesto = \|TipoBase = \|SignoInventario = ' API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs` → `"13"`, `"12"`, `"DN"`, `"P"`, `"C"`, `13`, `1m`; y `grep -n 'DocSubType = "ND"\|"ND"' API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs` → sin coincidencias.
(e) `git diff -U0 -- API.Application.Main/NotaDebitoCompraApplication.cs` solo cambia los tipos de entidad, el mapeo de `Lineas` con `CodArticulo`/`CodAlmacen` y nada más.

- [ ] **Step 3: DTOs, mapeo y pruebas de aplicación del proveedor**

DTOs (mismas inserciones del Task 5 Step 6 en los cuatro archivos de `API.Application.DTO/notaDebitoCompra/`: `NotaDebitoCompraCrearDTO` y `NotaDebitoCompraDTO` ganan `DocType`; `NotaDebitoCompraDetalleCrearDTO` y `NotaDebitoCompraDetalleDTO` ganan `CodArticulo`/`CodAlmacen`). Usar las mismas anclas (`public string? TipoObjeto { get; set; }`, el comentario `// Entry y NoLinea...`, `public int NoLinea { get; set; }`) y los mismos textos.

Mapeo en `PerfilMapeo.cs`, bloque `// NotaDebitoCompra` y `// NotaDebitoCompraDetalle`:

| antes | después |
|---|---|
| `CreateMap<NotaDebitoCompra, NotaDebitoCompraDTO>();` | `CreateMap<FacturaCompra, NotaDebitoCompraDTO>();` |
| `CreateMap<NotaDebitoCompraCrearDTO, NotaDebitoCompra>();` | `CreateMap<NotaDebitoCompraCrearDTO, FacturaCompra>();` |
| `CreateMap<NotaDebitoCompraActualizarDTO, NotaDebitoCompra>();` | `CreateMap<NotaDebitoCompraActualizarDTO, FacturaCompra>();` |
| `CreateMap<NotaDebitoCompraDetalle, NotaDebitoCompraDetalleDTO>();` | `CreateMap<FacturaCompraDetalle, NotaDebitoCompraDetalleDTO>();` |
| `CreateMap<NotaDebitoCompraDetalleCrearDTO, NotaDebitoCompraDetalle>();` | `CreateMap<NotaDebitoCompraDetalleCrearDTO, FacturaCompraDetalle>();` |

Pruebas de aplicación y de mapeo (bash):

```bash
sed -i -E 's/\bNotaDebitoCompraDetalle\b/FacturaCompraDetalle/g; s/\bNotaDebitoCompra\b/FacturaCompra/g' API.Service.WebApi.Tests/Application/NotaDebitoCompraApplicationTests.cs API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs
```

(El segundo archivo ya fue editado en el Task 5 para el cliente; aquí solo cambia las referencias del proveedor `_mapper.Map<NotaDebitoCompra>` y `NotaDebitoCompraDetalle`/`new NotaDebitoCompra {`.) Reemplazar en `NotaDebitoCompraApplicationTests.cs` el método equivalente a `ObtenerFacturaBase_ConvierteLasLineasEnConceptos` por la versión del Task 5 Step 8 con `FacturaCompra`/`FacturaCompraDetalle`, `FacturaCompraBase`, `ObtenerFacturaCompraBaseAsync`, nombre `ObtenerFacturaBase_DevuelveLasLineasConArticuloYAlmacen` y los mismos datos y aserciones; agregar `Insertar_MapeaElModoYElArticuloDeLasLineas` del mismo Step (con `NotaDebitoCompraCrearDTO`, `NotaDebitoCompraDetalleCrearDTO`, `FacturaCompra`, `FacturaCompraDetalle`) y, en `NotaDebitoMapeoTests`, la prueba `Proveedor_MapeaElModoYElArticuloEnAmbosSentidos` equivalente a la del cliente. Los métodos de la clase de pruebas de aplicación del proveedor que llamen `ObtenerFacturasBaseAsync`/`ObtenerFacturaBaseAsync` ya usan los nombres `...Compra...`.

- [ ] **Step 4: Pruebas, suite completa y commit**

`dotnet test API.sln --filter "FullyQualifiedName~NotaDebito"` verde (un fallo indica un literal que el `sed` no cubrió: corregirlo en el archivo derivado de proveedor, no en el del cliente; anotar cada corrección manual en el reporte). Luego `dotnet test API.sln` completo verde (reportar el total real).

```bash
git add API.Domain.Core/NotaDebitoCompraDomain.cs API.Domain.Interface/INotaDebitoCompraDomain.cs API.Application.Main/NotaDebitoCompraApplication.cs API.Application.DTO/notaDebitoCompra/NotaDebitoCompraCrearDTO.cs API.Application.DTO/notaDebitoCompra/NotaDebitoCompraDTO.cs API.Application.DTO/notaDebitoCompra/NotaDebitoCompraDetalleCrearDTO.cs API.Application.DTO/notaDebitoCompra/NotaDebitoCompraDetalleDTO.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs API.Service.WebApi.Tests/Application/NotaDebitoCompraApplicationTests.cs API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs
git commit -m "feat(notas-debito): nota de debito de proveedor sobre FacturaCompra con modo por articulos o por concepto

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Eliminar las entidades y repositorios `NotaDebito*`

**Files:**
- Delete: `API.Domain.Entity/Models/NotaDebito.cs`, `NotaDebitoDetalle.cs`, `NotaDebitoCompra.cs`, `NotaDebitoCompraDetalle.cs`; `API.Infraestructure.Repository/NotaDebitoRepositorio.cs`, `NotaDebitoDetalleRepositorio.cs`, `NotaDebitoCompraRepositorio.cs`, `NotaDebitoCompraDetalleRepositorio.cs`; `API.Service.WebApi.Tests/Domain/NotaDebitoModeloTests.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs`, `SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs`, `Impuesto.cs`; `API.Service.WebApi/Startup.cs`

**Interfaces:**
- Consumes: Tasks 5 y 6 (ningún código vivo usa ya `NotaDebito`, `NotaDebitoDetalle`, `NotaDebitoCompra`, `NotaDebitoCompraDetalle`).
- Produces: un modelo EF sin las cuatro tablas; el registro de DI de `INotaDebitoDomain`/`INotaDebitoApplication` (y de compra) se conserva.

**Precondición verificable:** Tasks 5 y 6 commiteadas y verdes. `grep -rnE "\b(NotaDebito|NotaDebitoDetalle|NotaDebitoCompra|NotaDebitoCompraDetalle)\b" --include=*.cs . | grep -v "/bin/\|/obj/"` solo debe mostrar: los 4 archivos de entidad, los 4 repositorios, `ApiDbTestContext.cs`, las colecciones de navegación (`SocioNegocio`, `Monedum`, `NumeracionDocumentoDet`, `Impuesto`), `Startup.cs` y `NotaDebitoModeloTests.cs`. Cualquier otra coincidencia (p. ej. en un dominio, aplicación o prueba) es un resto de la Task 5/6: corregirlo ahí antes de continuar.

- [ ] **Step 1: Borrar archivos**

```bash
git rm API.Domain.Entity/Models/NotaDebito.cs API.Domain.Entity/Models/NotaDebitoDetalle.cs API.Domain.Entity/Models/NotaDebitoCompra.cs API.Domain.Entity/Models/NotaDebitoCompraDetalle.cs
git rm API.Infraestructure.Repository/NotaDebitoRepositorio.cs API.Infraestructure.Repository/NotaDebitoDetalleRepositorio.cs API.Infraestructure.Repository/NotaDebitoCompraRepositorio.cs API.Infraestructure.Repository/NotaDebitoCompraDetalleRepositorio.cs
git rm API.Service.WebApi.Tests/Domain/NotaDebitoModeloTests.cs
```

(El mapeo de `DocSubType`/`DocType` ya lo cubre `FacturaModeloTests` de la Task 2.)

- [ ] **Step 2: Limpiar el contexto y las colecciones de navegación**

Con `node` (preserva el EOL; aborta si algo no coincide el número esperado de veces). Desde la raíz del repo API:

```bash
node - <<'EOF'
const fs = require('fs');
function editar(ruta, fn) {
  let s = fs.readFileSync(ruta, 'utf8');
  const crlf = s.includes('\r\n');
  s = s.replace(/\r\n/g, '\n');
  s = fn(s);
  if (crlf) s = s.replace(/\n/g, '\r\n');
  fs.writeFileSync(ruta, s);
}
function quitar(s, regex, esperado, nombre) {
  const m = s.match(regex);
  const n = m ? m.length : 0;
  if (n !== esperado) throw new Error(`${nombre}: se esperaban ${esperado} coincidencias y hay ${n}`);
  return s.replace(regex, '');
}

// 1) DbSets y bloques de mapeo del contexto
editar('API.Domain.Entity/Models/ApiDbTestContext.cs', s => {
  s = quitar(s, /^    public virtual DbSet<NotaDebito(?:Detalle|Compra|CompraDetalle)?> \w+ \{ get; set; \}\n\n/gm, 4, 'DbSets');
  for (const nombre of ['NotaDebito', 'NotaDebitoDetalle', 'NotaDebitoCompra', 'NotaDebitoCompraDetalle']) {
    const ini = s.indexOf(`        modelBuilder.Entity<${nombre}>(entity =>`);
    if (ini < 0 || s.indexOf(`        modelBuilder.Entity<${nombre}>(entity =>`, ini + 1) >= 0) throw new Error('bloque ' + nombre);
    const finTxt = '\n        });\n';
    const fin = s.indexOf(finTxt, ini);
    if (fin < 0) throw new Error('fin de bloque ' + nombre);
    let hasta = fin + finTxt.length;
    if (s[hasta] === '\n') hasta++;            // la linea en blanco que separa bloques
    s = s.slice(0, ini) + s.slice(hasta);
  }
  return s;
});

// 2) Colecciones de navegacion en las entidades relacionadas
const navegacion = /^    public virtual ICollection<NotaDebito(?:Detalle|Compra|CompraDetalle)?> \w+ \{ get; set; \} = new List<NotaDebito(?:Detalle|Compra|CompraDetalle)?>\(\);\n(?:\n)?/gm;
editar('API.Domain.Entity/Models/SocioNegocio.cs', s => quitar(s, navegacion, 2, 'SocioNegocio'));
editar('API.Domain.Entity/Models/Monedum.cs', s => quitar(s, navegacion, 2, 'Monedum'));
editar('API.Domain.Entity/Models/NumeracionDocumentoDet.cs', s => quitar(s, navegacion, 2, 'NumeracionDocumentoDet'));
editar('API.Domain.Entity/Models/Impuesto.cs', s => quitar(s, navegacion, 2, 'Impuesto'));

// 3) Registros de repositorios en Startup
editar('API.Service.WebApi/Startup.cs', s => quitar(s,
  /^ *services\.AddTransient<IRepositorioGenerico<NotaDebito(?:Detalle|Compra|CompraDetalle)?, [^>]+>>?, NotaDebito\w*Repositorio>\(\);\n/gm, 4, 'Startup'));
console.log('ok');
EOF
```

Revisar `git diff --stat` (solo borrados en esos archivos, sin churn de EOL) y que `Startup.cs` conserve los registros `INotaDebitoDomain`/`INotaDebitoApplication`/`INotaDebitoCompraDomain`/`INotaDebitoCompraApplication`:

```bash
grep -n "NotaDebito" API.Service.WebApi/Startup.cs      # 4 lineas: Domain y Application de cliente y de compra
grep -rnE "\b(NotaDebito|NotaDebitoDetalle|NotaDebitoCompra|NotaDebitoCompraDetalle)\b" --include=*.cs . | grep -v "/bin/\|/obj/"      # vacio
```

(Si el patrón de Startup no coincide 4 veces porque los genéricos de los registros tienen otra forma, el script aborta con el conteo: ajustar solo esa expresión regular a la forma real de las cuatro líneas `AddTransient<IRepositorioGenerico<NotaDebito...>, NotaDebito...Repositorio>`.)

- [ ] **Step 3: Compilar, pruebas y commit**

`dotnet test API.sln` verde (el total baja por las pruebas del modelo `NotaDebito*` eliminadas — 4 casos — y por nada más; reportar el total real).

```bash
git add API.Domain.Entity/Models/ApiDbTestContext.cs API.Domain.Entity/Models/SocioNegocio.cs API.Domain.Entity/Models/Monedum.cs API.Domain.Entity/Models/NumeracionDocumentoDet.cs API.Domain.Entity/Models/Impuesto.cs API.Service.WebApi/Startup.cs
git commit -m "refactor(notas-debito): eliminar las entidades y repositorios NotaDebito* (ahora viven en Factura*)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

(Los `git rm` del Step 1 ya dejaron los borrados en el índice y entran en este commit.)

---

### Task 8: La factura base de una nota de crédito puede ser una nota de débito

**Files:**
- Modify: `API.Application.DTO/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs`, `API.Application.DTO/notaCreditoCompra/NotaCreditoCompraFacturaBaseResumenDTO.cs`, `API.Application.Main/NotaCreditoApplication.cs`, `API.Application.Main/NotaCreditoCompraApplication.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs`, `NotaCreditoCompraDomainTests.cs`, `API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs`, `NotaCreditoCompraApplicationTests.cs`

**Interfaces:**
- Consumes: `Factura.DocSubType`/`FacturaCompra.DocSubType` (Task 2); `NotaCreditoDomain.ObtenerFacturasBaseAsync`, que ya devuelve toda fila de `Factura` no cancelada del socio (facturas y notas de débito comparten tabla); `FacturaAcreditable(Factura Documento, decimal TotalAcreditado)`.
- Produces: `NotaCreditoFacturaBaseResumenDTO.DocSubType` (`string?`, `"--"` factura / `"ND"` nota de débito; compra `"--"` / `"DN"`); el dominio de crédito no cambia (la nota de débito ya es una base válida porque `BaseTipo` 6/13 + `Entry` identifica una sola fila).

**Precondición verificable:** Task 7 commiteada y verde; `grep -n "DocSubType" API.Domain.Core/NotaCreditoDomain.cs API.Domain.Core/NotaCreditoCompraDomain.cs` sin coincidencias (el dominio de crédito no filtra por subtipo).

- [ ] **Step 1: Pruebas (el dominio ya las cumple; la del resumen falla)**

En `NotaCreditoDomainTests.cs`: cambiar el helper `PrepararFactura` para aceptar el subtipo (parámetro opcional al final, sin romper las llamadas existentes):

```csharp
        private void PrepararFactura(int entry = 12, string socio = "SN1", string cancelado = "N", string moneda = "QTZ", decimal total = 1000m, string subTipo = "--") =>
            _repoFactura.Setup(r => r.ObtenerAsync(entry)).ReturnsAsync(new Factura
            {
                Entry = entry, CodigoSn = socio, Cancelado = cancelado, MonedaDoc = moneda, TotalDoc = total, DocSubType = subTipo
            });
```

y agregar (junto a las otras pruebas de límite):

```csharp
        [Fact]
        public async Task InsertarAsync_ConBaseUnaNotaDeDebito_LaAceptaYAplicaElLimiteSobreSuTotal()
        {
            PrepararFactura(total: 112m, subTipo: "ND");

            var entry = await _domain.InsertarAsync(NotaConBase(), new[] { Linea() });   // la nota suma exactamente 112

            Assert.Equal(99, entry);
        }

        [Fact]
        public async Task InsertarAsync_ConBaseUnaNotaDeDebito_QueSeExcede_Lanza()
        {
            PrepararFactura(total: 100m, subTipo: "ND");

            Assert.Equal(Exceso("112.00", "100.00"), await MensajeAlInsertar(NotaConBase(), Linea()));
        }

        [Fact]
        public async Task ObtenerFacturasBaseAsync_IncluyeLasNotasDeDebitoDelSocio()
        {
            var filas = new List<Factura>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 112m, DocSubType = "--" },
                new() { Entry = 2, CodigoSn = "SN1", Cancelado = "N", TotalDoc = 50m, DocSubType = "ND" }
            };
            _repoFactura.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(filas.AsAsyncQueryable());
            NotasPrevias(Previa(2, 20m));

            var resultado = (await _domain.ObtenerFacturasBaseAsync("SN1")).ToList();

            Assert.Equal(new[] { 2, 1 }, resultado.Select(f => f.Documento.Entry));
            Assert.Equal("ND", resultado[0].Documento.DocSubType);
            Assert.Equal(20m, resultado[0].TotalAcreditado);
        }
```

En `NotaCreditoCompraDomainTests.cs` lo equivalente (el helper `PrepararFactura` de ese archivo crea `FacturaCompra`; agregar el mismo parámetro `subTipo` y `DocSubType = subTipo`), con `"DN"` en lugar de `"ND"`, `FacturaCompra` en las listas y `NotasPrevias(Previa(2, 20m))` igual.

En `NotaCreditoApplicationTests.cs` agregar:

```csharp
        [Fact]
        public async Task ObtenerFacturasBase_MapeaElSubtipoDelDocumento()
        {
            _domain.Setup(d => d.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(new List<FacturaAcreditable>
            {
                new(new Factura { Entry = 12, NumDoc = 100, TotalDoc = 112m, MonedaDoc = "QTZ", CodigoSn = "SN1", DocSubType = "--" }, 0m),
                new(new Factura { Entry = 13, NumDoc = 7, TotalDoc = 50m, MonedaDoc = "QTZ", CodigoSn = "SN1", DocSubType = "ND" }, 0m)
            });

            var lista = (await _app.ObtenerFacturasBaseAsync("SN1")).Dato.ToList();

            Assert.Equal(new[] { "--", "ND" }, lista.Select(f => f.DocSubType));
        }
```

y en `NotaCreditoCompraApplicationTests.cs` el equivalente con `FacturaCompraAcreditable`, `FacturaCompra`, `"DN"`.

- [ ] **Step 2: Verificar**

`dotnet test API.sln --filter "FullyQualifiedName~NotaCredito"` → falla solo la prueba del resumen (por compilación: falta `DocSubType` en el DTO); las de dominio sobre notas de débito pasan sin tocar el dominio.

- [ ] **Step 3: DTO y aplicación**

En `NotaCreditoFacturaBaseResumenDTO.cs` y `NotaCreditoCompraFacturaBaseResumenDTO.cs` agregar después de `MonedaDoc`:

```csharp
        /// <summary>"--" = factura; "ND" (cliente) / "DN" (proveedor) = nota de débito usada como base.</summary>
        public string? DocSubType { get; set; }
```

En `NotaCreditoApplication.cs` y `NotaCreditoCompraApplication.cs`, dentro de `ObtenerFacturasBaseAsync`, agregar al inicializador del resumen (después de `MonedaDoc = f.Documento.MonedaDoc,`):

```csharp
                    DocSubType = f.Documento.DocSubType,
```

- [ ] **Step 4: Pruebas, suite completa y commit**

`dotnet test API.sln` verde (reportar el total real).

```bash
git add API.Application.DTO/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs API.Application.DTO/notaCreditoCompra/NotaCreditoCompraFacturaBaseResumenDTO.cs API.Application.Main/NotaCreditoApplication.cs API.Application.Main/NotaCreditoCompraApplication.cs API.Service.WebApi.Tests/Domain/NotaCreditoDomainTests.cs API.Service.WebApi.Tests/Domain/NotaCreditoCompraDomainTests.cs API.Service.WebApi.Tests/Application/NotaCreditoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaCreditoCompraApplicationTests.cs
git commit -m "feat(notas-credito): una nota de debito puede ser la factura base de una nota de credito

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Humo de la API contra una base temporal (sin commit)

**Files:** ninguno. Los defectos se **reportan** con la salida que falla; no se corrigen aquí.

**Interfaces:**
- Consumes: Tasks 1-8 commiteadas; script completo `sql/crear-api-db-test-completo.sql` (esquema final) para crear `API_DB_SMOKE`.
- Produces: evidencia de que la API se comporta como la spec sobre una base real: aislamiento factura/nota, inventario por artículos (cliente y proveedor), cancelación, numeración con clave compuesta, notas de crédito sobre notas de débito.

**Precondición verificable:** `dotnet test API.sln` verde en HEAD; `git status --porcelain` solo ` M API.Service.WebApi/appsettings.json`.

- [ ] **Step 1: Crear la base temporal y los datos mínimos**

PowerShell desde la raíz del repo API (nunca `API_DB_TEST`):

```powershell
$pw = ([regex]::Match((Get-Content API.Service.WebApi\appsettings.json -Raw), 'Password=([^;]*)')).Groups[1].Value
$scratch = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
(Get-Content sql\crear-api-db-test-completo.sql -Raw) -replace 'API_DB_TEST','API_DB_SMOKE' | Set-Content "$scratch\smoke.sql" -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$scratch\smoke.sql"
```

Datos mínimos (adaptar los `INSERT` a las columnas reales si alguno falla; comprobar con `sys.columns`; anotar en el reporte cualquier cambio): socio cliente `C0001` y proveedor `P0001` (mismos `INSERT` de `verifica-nd.sql`), almacén `01` (si no existe), artículo `A1` de inventario (`ArticuloInventario = 'S'`, con grupo/unidad/fabricante válidos como en el humo de notas de crédito: `Articulo(Codigo,Nombre,CodigoGrupo,CodigoGrpUnidadMedida,FabricanteEntry,Serie)` con `Serie` = la serie de artículos), existencia inicial de **100** unidades en `ExistenciaArticulo(CodArticulo='A1', CodAlmacen='01', Disponible=100)` con `Articulo.CantDisponible = 100` y `CostoPromedio = 5`; una factura de venta (`Factura`, `DocSubType='--'`, `Serie` = la Primaria de `(6,'--')`, `CodigoSN='C0001'`, `MonedaDoc='QTZ'`, `TotalDoc=16.8`) con una línea (`FacturaDetalle`: `A1`, almacén `01`, cantidad 3, precio 5, `IVA`, impuesto 1.8, total 16.8); lo mismo en `FacturaCompra` (`P0001`, serie Primaria de `(13,'--')`); y el usuario `sinperm` (como en el humo de notas de crédito: `INSERT INTO Usuario(ID,Password,Codigo,Nombre,SuperUsuario,Bloqueado,Eliminado) SELECT 2, Password, 'sinperm', 'Sin permisos', 'N', 'N', 'N' FROM Usuario WHERE ID = 1`).

- [ ] **Step 2: Levantar la API contra esa base (puerto 5199)**

```powershell
$env:ConnectionStrings__API_DB = "Server=localhost; Database=API_DB_SMOKE; User=sa; Password=$pw; TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$api = Start-Process dotnet -ArgumentList 'run','--project','API.Service.WebApi','--no-launch-profile','--urls','http://localhost:5199','-p:BaseOutputPath=C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apismoke\' -PassThru -WindowStyle Hidden
1..30 | ForEach-Object { try { Invoke-RestMethod http://localhost:5199/api/Auth/login -Method Post -ContentType 'application/json' -Body '{}' -ErrorAction Stop } catch { if ($_.Exception.Response) { break } }; Start-Sleep -Seconds 2 }
```

- [ ] **Step 3: Escenarios (registrar cada respuesta: código HTTP, mensaje y campos clave)**

Login como `admin` (contraseña del script de creación; si falla, buscar el usuario sembrado en el script) y usar el token en `Authorization: Bearer`. Definir las series: `$sND` = `Serie` de `NumeracionDocumentoDet WHERE CodigoObj='6' AND SubTipoDoc='ND' AND NombreSerie='Primario'`, `$sDN` la de `(13,'DN')`, `$sF`/`$sFC` las de factura y factura de compra, `$sNC` la Primaria de `CodigoObj='8'`. Función auxiliar: cuerpo JSON de nota con `serie`, `codigoSn`, `monedaDoc='QTZ'`, `docType`, `baseTipo`, `baseEntry` y `lineas`.

Cliente (`api/NotaDebito`):
1. **Por artículos:** `POST` con `docType='I'`, serie `$sND`, línea `A1`/`01` cantidad 3 precio 5 `IVA` impuesto 1.8 total 16.8 → 200. `GET api/NotaDebito/{id}` → `docType 'I'`, `tipoObjeto '6'`; SQL: `ExistenciaArticulo.Disponible = 97`, `Factura.DocSubType='ND'`, `EstadoInv='A'`, `MovimientoInventario` con `TipoDoc='6'`, `DocEntry={id}`, `CantidadSale=3`; las líneas (`GET api/NotaDebito/{id}/Lineas`) traen `codArticulo 'A1'`, `codAlmacen '01'`.
2. **Aislamiento:** `GET api/Factura` NO contiene `{id}`; `GET api/Factura/{id}` → 404; `GET api/FacturaDetalle/PorFactura/{id}` (usar la ruta real del controller) → vacío o 404; `GET api/NotaDebito` contiene `{id}` y no contiene la factura sembrada; `GET api/NotaDebito/{entryDeLaFactura}` → 404.
3. **Salida mayor a la existencia:** misma nota con cantidad 500 → 400 con el mensaje de stock insuficiente; sin fila nueva en `Factura` y existencia intacta (97).
4. **Cancelar:** `PUT api/NotaDebito/{id}` `{"cancelado":"S"}` → 200; existencia vuelve a 100; `Cancelado='S'`, `EstadoInv='C'`; `DELETE api/NotaDebito/{id}` → 200 (y antes, sin cancelar, otra nota → 400 `Cancele el documento (Cancelado='S') antes de eliminarlo.`).
5. **Por concepto:** `POST` con `docType='S'`, línea sin artículo (`descripcion 'Flete'`) → 200; existencia sin cambios; `EstadoInv='C'`; con base = la factura sembrada (`baseTipo 6`, `baseEntry`) → 200.
6. **Reglas:** `docType` ausente → 400 `Indique el tipo de nota: por artículos ('I') o por concepto ('S').`; concepto con `codArticulo` → 400 `Línea 1: una nota por concepto no lleva artículo ni almacén.`; artículos sin almacén → 400 `Línea 1: el almacén es requerido.`; serie de factura (`$sF`) en una nota → 400 `La serie no corresponde a notas de débito de cliente.`; serie de nota (`$sND`) en `POST api/Factura` → 400 `La serie no corresponde a facturas.`; nota con base = otra nota de débito → 400 `La factura base no puede ser una nota de débito.`
7. **Nota de crédito sobre nota de débito:** `GET api/NotaCredito/FacturasBase/C0001` lista la nota de débito con `docSubType 'ND'`; `POST api/NotaCredito` con `baseTipo 6`, `baseEntry` = la nota de débito, por un monto ≤ su total → 200; por un monto mayor → 400 con el mensaje `El total acreditado (...) excede el total de la factura base (...).`
8. **Numeración con clave compuesta:** `GET api/NumeracionDocumento/6` → fila `Factura`; `GET api/NumeracionDocumento/6:ND` → `Nota Debito Cliente`; `PUT api/NumeracionDocumento/6:ND` cambiando `serieDfct` a la Manual → 200 y `(6,'--')` no cambia; restaurar el valor.

Proveedor (`api/NotaDebitoCompra`, serie `$sDN`, socio `P0001`):
9. **Por artículos:** `POST` `docType='I'`, `A1`/`01` cantidad 3 precio 5 → 200; existencia **103** (entrada); `TipoDoc='13'`, `CantidadEntra=3`; `PUT ... cancelado 'S'` → 200 y existencia vuelve a 100.
10. **Aislamiento y reglas equivalentes** a los puntos 2 y 6 (`api/FacturaCompra` no lista la nota; serie `$sFC` en la nota → `La serie no corresponde a notas de débito de proveedor.`; serie `$sDN` en `POST api/FacturaCompra` → `La serie no corresponde a facturas de compra.`).
11. **Permisos:** sin token → 401; usuario `sinperm` → 403 en `api/NotaDebito`, `api/NotaDebitoCompra`.

Esperado general: todo lo anterior tal cual; cualquier diferencia se reporta con la salida exacta.

- [ ] **Step 4: Cierre obligatorio (aunque algo falle)**

```powershell
Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $api.Id } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
sqlcmd -S localhost -U sa -P $pw -C -b -Q "ALTER DATABASE API_DB_SMOKE SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE API_DB_SMOKE"
```

Confirmar que el puerto 5199 ya no tiene listener, que `API_DB_SMOKE` no existe y que `API_DB_TEST` no fue tocada (no tiene `Factura.DocSubType`).

- [ ] **Step 5: Estado**

`git status --porcelain` (API) → solo ` M API.Service.WebApi/appsettings.json`. Reportar el total de pruebas y, por escenario 1-11, PASS/FALLA con la respuesta.

---

## Fase 4 — Web

### Task 10: DTOs de la Web

**Files:**
- Overwrite (copias de los DTOs de la API con el namespace de la Web): `Web.ApiClient/Dtos/NotaDebito/*.cs` (7), `Web.ApiClient/Dtos/NotaDebitoCompra/*.cs` (7), `Web.ApiClient/Dtos/NotaCredito/NotaCreditoFacturaBaseResumenDTO.cs`, `Web.ApiClient/Dtos/NotaCreditoCompra/NotaCreditoCompraFacturaBaseResumenDTO.cs`

**Interfaces:**
- Consumes: DTOs de la API de las Tasks 5, 6 y 8 (`DocType`, `CodArticulo`, `CodAlmacen`, `DocSubType`).
- Produces: `NotaDebitoCrearDTO.DocType`/`NotaDebitoDTO.DocType` (y de compra), `NotaDebitoDetalleCrearDTO` y `NotaDebitoDetalleDTO` con `CodArticulo`/`CodAlmacen`, `NotaCreditoFacturaBaseResumenDTO.DocSubType` (y de compra). Los clientes `INotaDebitoApiClient`/`INotaDebitoCompraApiClient` no cambian (no hay endpoints nuevos).

**Precondición verificable:** repo Web: `git status --porcelain` lista únicamente los 4 archivos ajenos; repo API con las Tasks 1-9 commiteadas; `dotnet build Web.slnx` pasa.

- [ ] **Step 1: Regenerar los DTOs desde los de la API**

Desde la raíz del repo `Web` (bash):

```bash
set -e
api=../API/API.Application.DTO
for par in "notaDebito:NotaDebito" "notaDebitoCompra:NotaDebitoCompra"; do
  carpetaApi="${par%%:*}"; nombre="${par##*:}"
  for f in "$api/$carpetaApi"/*.cs; do
    sed "s/namespace API\.Application\.DTO\.$carpetaApi/namespace Web.ApiClient.Dtos.$nombre/" "$f" > "Web.ApiClient/Dtos/$nombre/$(basename "$f")"
  done
done
sed "s/namespace API\.Application\.DTO\.notaCredito\b/namespace Web.ApiClient.Dtos.NotaCredito/" "$api/notaCredito/NotaCreditoFacturaBaseResumenDTO.cs" > Web.ApiClient/Dtos/NotaCredito/NotaCreditoFacturaBaseResumenDTO.cs
sed "s/namespace API\.Application\.DTO\.notaCreditoCompra\b/namespace Web.ApiClient.Dtos.NotaCreditoCompra/" "$api/notaCreditoCompra/NotaCreditoCompraFacturaBaseResumenDTO.cs" > Web.ApiClient/Dtos/NotaCreditoCompra/NotaCreditoCompraFacturaBaseResumenDTO.cs
grep -rn "API.Application" Web.ApiClient/Dtos/NotaDebito Web.ApiClient/Dtos/NotaDebitoCompra Web.ApiClient/Dtos/NotaCredito Web.ApiClient/Dtos/NotaCreditoCompra && echo FALLO || echo OK
git diff --stat
```

`git diff --stat` debe mostrar solo cambios pequeños (unas pocas líneas por archivo: `DocType`, `CodArticulo`, `CodAlmacen`, `DocSubType` y el comentario de `Lineas`) en los 4 DTOs de cada nota de débito y los 2 resúmenes de crédito; los otros DTOs de notas de débito deben quedar **sin diferencias** (si aparece churn de fin de línea, restaurar con `git checkout -- <archivo>` y volver a copiar conservando el EOL original, porque los de la Web ya eran copias de los de la API).

- [ ] **Step 2: Compilar y commit**

`dotnet build Web.slnx -p:BaseOutputPath="..."` → 0 errores. Compilar también en un worktree limpio de HEAD tras el commit si el árbol principal tiene advertencias de los archivos ajenos.

```bash
git add Web.ApiClient/Dtos/NotaDebito Web.ApiClient/Dtos/NotaDebitoCompra Web.ApiClient/Dtos/NotaCredito/NotaCreditoFacturaBaseResumenDTO.cs Web.ApiClient/Dtos/NotaCreditoCompra/NotaCreditoCompraFacturaBaseResumenDTO.cs
git commit -m "feat(notas-debito): DTOs de la Web con tipo de nota, articulo y almacen

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

(`git add` de las dos carpetas completas solo toma los DTOs de notas de débito; los 4 archivos ajenos no están ahí. Verificar con `git status --porcelain` que siguen sin stagear.)

---

### Task 11: Pantalla de notas de débito de cliente con selector "Tipo de nota"

**Files:**
- Overwrite: `Web.UI/Controllers/NotasDebitoController.cs`, `Web.UI/Views/NotasDebito/_Form.cshtml`, `Web.UI/wwwroot/js/notasdebito.js`
- Modify: `Web.UI/Views/NotasDebito/Index.cshtml`

**Interfaces:**
- Consumes: `NotaDebitoCrearDTO.DocType`, `NotaDebitoDetalleCrearDTO.CodArticulo/CodAlmacen`, `NotaDebitoDTO.DocType`, `NotaDebitoDetalleDTO.CodArticulo/CodAlmacen` (Task 10); `IArticuloApiClient` (`ObtenerTodoAsync`, `ObtenerContenganNombreAsync(texto)`, `ObtenerContenganCodigoAsync(texto)`), `IAlmacenApiClient` (`ObtenerTodoAsync`, `ObtenerContenganNombreAsync`, `ObtenerAsync(codigo)`) (ya registrados, los usa `FacturasController`); `App.autocompletar`, `App.obtenerListaDeSocio`, `App.resolverPrecioLista` de `site.js`.
- Produces: rutas MVC nuevas `NotasDebito/BuscarArticulos`, `BuscarArticulosPorCodigo`, `BuscarAlmacenes`, `ObtenerAlmacenPorCodigo` (todas `NotasDebito`/`Ver`); el formulario envía `DocType` (`"S"`/`"I"`) y líneas con `CodArticulo`/`CodAlmacen` solo en modo `"I"`; en edición `DocType` es de solo lectura. Constantes del controller: `CodigoObjNotaDebito = "6"`, `SubTipoDocNotaDebito = "ND"`.

**Precondición verificable:** Task 10 commiteada; `git diff --quiet -- Web.UI/Controllers/NotasDebitoController.cs Web.UI/Views/NotasDebito Web.UI/wwwroot/js/notasdebito.js` (sin cambios pendientes: se sobrescriben); `dotnet build Web.slnx` pasa.

- [ ] **Step 1: Controller**

Sobrescribir `Web.UI/Controllers/NotasDebitoController.cs` (LF) con:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;
using Web.ApiClient.Clientes;
using Web.ApiClient.Dtos.NotaDebito;
using Web.UI.Authorization;

namespace Web.UI.Controllers
{
    [Authorize]
    public class NotasDebitoController : Controller
    {
        private readonly INotaDebitoApiClient _notas;
        private readonly ISocioNegocioApiClient _socios;
        private readonly IMonedaApiClient _monedas;
        private readonly IArticuloApiClient _articulos;
        private readonly IAlmacenApiClient _almacenes;
        private readonly IImpuestoApiClient _impuestos;
        private readonly INumeracionDocumentoDetApiClient _series;
        private readonly INumeracionDocumentoApiClient _numeracion;

        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        // CodigoObj y SubTipoDoc de NumeracionDocumento que identifican a "Notas de debito de cliente":
        // la nota comparte objeto con la factura (6) y se distingue por el subtipo ND.
        private const string CodigoObjNotaDebito = "6";
        private const string SubTipoDocNotaDebito = "ND";
        private const string TipoSocio = "C";
        // ---- fin de constantes ----

        public NotasDebitoController(
            INotaDebitoApiClient notas,
            ISocioNegocioApiClient socios,
            IMonedaApiClient monedas,
            IArticuloApiClient articulos,
            IAlmacenApiClient almacenes,
            IImpuestoApiClient impuestos,
            INumeracionDocumentoDetApiClient series,
            INumeracionDocumentoApiClient numeracion)
        {
            _notas = notas;
            _socios = socios;
            _monedas = monedas;
            _articulos = articulos;
            _almacenes = almacenes;
            _impuestos = impuestos;
            _series = series;
            _numeracion = numeracion;
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        public IActionResult Index() => View();

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> ObtenerTodos()
        {
            var respuesta = await _notas.ObtenerTodoAsync();
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Agregar)]
        public async Task<IActionResult> FormularioCrear()
        {
            await CargarDropdownsAsync();
            var series = await _series.ObtenerPorDocumentoAsync(CodigoObjNotaDebito);
            ViewBag.SeriesNotaDebito = (series.Dato ?? []).Where(s => s.SubTipoDoc == SubTipoDocNotaDebito);

            // Serie preseleccionada: la configurada como "por defecto" para este objeto en la pantalla
            // "Numeración de documentos" (NumeracionDocumento.SerieDfct).
            var numeraciones = await _numeracion.ObtenerTodoAsync();
            var numeracionActual = (numeraciones.Dato ?? []).FirstOrDefault(n => n.CodigoObj == CodigoObjNotaDebito && n.SubTipoDoc == SubTipoDocNotaDebito);
            ViewBag.SerieDefecto = numeracionActual?.SerieDfct;

            ViewBag.EsEdicion = false;
            var hoy = DateTime.Today;
            return PartialView("_Form", new NotaDebitoCrearDTO
            {
                EstadoDoc = "A",
                TipoObjeto = CodigoObjNotaDebito,
                DocType = "S",
                FechaDoc = hoy,
                FechaEmision = hoy,
                PrctjeDesc = 0,
                BaseTipo = -1
            });
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> FormularioEditar(int entry)
        {
            var respuesta = await _notas.ObtenerAsync(entry);
            if (!respuesta.Resultado || respuesta.Dato is null)
                return NotFound();

            await CargarDropdownsAsync();
            ViewBag.EsEdicion = true;
            ViewBag.EntryActual = entry;

            var serieInfo = await _series.ObtenerAsync(respuesta.Dato.Serie);
            ViewBag.NombreSerieActual = serieInfo.Resultado ? serieInfo.Dato?.NombreSerie : null;

            var dto = new NotaDebitoCrearDTO
            {
                NumDoc = respuesta.Dato.NumDoc,
                Serie = respuesta.Dato.Serie,
                Cancelado = respuesta.Dato.Cancelado,
                EstadoDoc = respuesta.Dato.Cancelado == "S" ? "C" : respuesta.Dato.EstadoDoc,
                TipoObjeto = respuesta.Dato.TipoObjeto,
                DocType = respuesta.Dato.DocType,
                FechaDoc = respuesta.Dato.FechaDoc,
                FechaEmision = respuesta.Dato.FechaEmision,
                CodigoSn = respuesta.Dato.CodigoSn,
                NombreSn = respuesta.Dato.NombreSn,
                Direccion = respuesta.Dato.Direccion,
                MonedaDoc = respuesta.Dato.MonedaDoc,
                BaseTipo = respuesta.Dato.BaseTipo,
                BaseEntry = respuesta.Dato.BaseEntry,
                PrctjeImpuesto = respuesta.Dato.PrctjeImpuesto,
                TotalImp = respuesta.Dato.TotalImp,
                PrctjeDesc = respuesta.Dato.PrctjeDesc,
                TotalDesc = respuesta.Dato.TotalDesc,
                TotalBruto = respuesta.Dato.TotalBruto,
                TotalDoc = respuesta.Dato.TotalDoc,
                Comentario = respuesta.Dato.Comentario
            };

            return PartialView("_Form", dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("NotasDebito", Accion.Agregar)]
        public async Task<IActionResult> Crear([FromBody] NotaDebitoCrearDTO dto)
        {
            var respuesta = await _notas.InsertarAsync(dto);
            if (!respuesta.Resultado)
                return Json(respuesta);

            var creado = await _notas.ObtenerAsync(respuesta.Dato);
            return Json(new { respuesta.Resultado, respuesta.Mensaje, dato = respuesta.Dato, numDoc = creado.Dato?.NumDoc });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("NotasDebito", Accion.Actualizar)]
        public async Task<IActionResult> Editar(int entry, [FromBody] NotaDebitoCrearDTO dto)
        {
            // Cancelar un documento es una actualización, pero requiere además el permiso Eliminar.
            if (dto.Cancelado == "S" && !User.TienePermiso("NotasDebito", Accion.Eliminar))
                return Json(new { resultado = false, mensaje = "No tienes permiso para cancelar documentos." });

            var actual = await _notas.ObtenerAsync(entry);
            if (!actual.Resultado || actual.Dato is null)
                return NotFound(actual);

            var actualizar = new NotaDebitoActualizarDTO
            {
                NumDoc = actual.Dato.NumDoc,
                Serie = actual.Dato.Serie,
                Cancelado = dto.Cancelado,
                Comentario = dto.Comentario
            };

            var respuesta = await _notas.ActualizarAsync(entry, actualizar);
            return Json(respuesta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequierePermiso("NotasDebito", Accion.Eliminar)]
        public async Task<IActionResult> Eliminar(int entry)
        {
            var respuesta = await _notas.EliminarAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> ObtenerDetalle(int entry)
        {
            var respuesta = await _notas.ObtenerLineasAsync(entry);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> BuscarSocios(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _socios.ObtenerTodoAsync(TipoSocio)
                : await _socios.ObtenerContenganNombreAsync(texto, TipoSocio);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> BuscarArticulos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> BuscarArticulosPorCodigo(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _articulos.ObtenerTodoAsync()
                : await _articulos.ObtenerContenganCodigoAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> BuscarAlmacenes(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _almacenes.ObtenerTodoAsync()
                : await _almacenes.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> ObtenerAlmacenPorCodigo(string codigo)
        {
            var respuesta = await _almacenes.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> BuscarImpuestos(string texto)
        {
            var respuesta = string.IsNullOrEmpty(texto)
                ? await _impuestos.ObtenerTodoAsync()
                : await _impuestos.ObtenerContenganNombreAsync(texto);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> ObtenerImpuestoPorCodigo(string codigo)
        {
            var respuesta = await _impuestos.ObtenerAsync(codigo);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Agregar)]
        public async Task<IActionResult> BuscarFacturasBase(string codigoSn)
        {
            var respuesta = await _notas.ObtenerFacturasBaseAsync(codigoSn ?? string.Empty);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Agregar)]
        public async Task<IActionResult> ObtenerFacturaBase(int entry)
        {
            var respuesta = await _notas.ObtenerFacturaBaseAsync(entry);
            return Json(respuesta);
        }

        private async Task CargarDropdownsAsync()
        {
            // Socio de negocio, Articulo, Almacen e Impuesto no se cargan como lista completa: el buscador
            // con autocompletado los consulta bajo demanda. Moneda es un <select> normal.
            var monedas = await _monedas.ObtenerTodoAsync();
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
        }
    }
}
```

(Verificar con `grep -n "ObtenerContenganCodigoAsync\|ObtenerContenganNombreAsync" Web.ApiClient/Clientes/IArticuloApiClient.cs Web.ApiClient/Clientes/IAlmacenApiClient.cs` que las firmas existen; son las mismas que usa `FacturasController`.)

- [ ] **Step 2: Vista del formulario**

Sobrescribir `Web.UI/Views/NotasDebito/_Form.cshtml` (LF) con:

```cshtml
@using System.Text.Json
@model Web.ApiClient.Dtos.NotaDebito.NotaDebitoCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
    var puedeGuardar = esEdicion ? User.TienePermiso("NotasDebito", Accion.Actualizar) : User.TienePermiso("NotasDebito", Accion.Agregar);
    var opcionesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
}

<div class="modal-header">
    <h5 class="modal-title">@(esEdicion ? "Editar nota de débito" : "Nueva nota de débito")</h5>
    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
</div>
<div class="modal-body">
    <form id="formNotaDebito" novalidate>
        <div asp-validation-summary="ModelOnly" class="alert alert-danger py-2 px-3 small"></div>

        <div class="row g-3">
            @if (!esEdicion)
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <select id="selectSerieNotaDebito" class="form-select" data-serie-defecto="@ViewBag.SerieDefecto"></select>
                    <span class="form-text">Si eliges una serie distinta de "Manual", el número se genera solo al guardar.</span>
                </div>
            }
            else
            {
                <div class="col-md-3">
                    <label class="form-label">Serie</label>
                    <input class="form-control" value="@ViewBag.NombreSerieActual" disabled />
                </div>
            }
            <div class="col-md-3">
                <label asp-for="NumDoc" class="form-label">No. documento</label>
                <input asp-for="NumDoc" type="number" class="form-control" readonly="@esEdicion" />
                <span asp-validation-for="NumDoc" class="text-danger small"></span>
            </div>
            <div class="col-md-3">
                <label asp-for="EstadoDoc" class="form-label">Estado</label>
                <select asp-for="EstadoDoc" class="form-select" disabled="@esEdicion">
                    <option value="A">Abierto</option>
                    <option value="C">Cancelado</option>
                </select>
            </div>
            <div class="col-md-3">
                <label asp-for="DocType" class="form-label">Tipo de nota</label>
                <select asp-for="DocType" class="form-select" disabled="@esEdicion">
                    <option value="S">Concepto</option>
                    <option value="I">Artículos</option>
                </select>
            </div>
            <div class="col-md-3" hidden>
                <label asp-for="TipoObjeto" class="form-label">Tipo</label>
                <input asp-for="TipoObjeto" class="form-control" readonly="@esEdicion" />
            </div>

            <div class="col-md-4 position-relative">
                <label asp-for="CodigoSn" class="form-label">Socio de negocio</label>
                @if (esEdicion)
                {
                    <input class="form-control" value="@(!string.IsNullOrEmpty(Model.NombreSn) ? $"{Model.CodigoSn} - {Model.NombreSn}" : Model.CodigoSn)" disabled />
                    <input type="hidden" asp-for="CodigoSn" />
                }
                else
                {
                    <input type="text" id="CodigoSnTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                    <input type="hidden" asp-for="CodigoSn" />
                    <div class="invalid-feedback" id="CodigoSnError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="CodigoSnResultados"></ul>
                }
            </div>
            <div class="col-md-4">
                <label asp-for="NombreSn" class="form-label">Nombre</label>
                <input asp-for="NombreSn" class="form-control" readonly="@esEdicion" />
            </div>
            <div class="col-md-4">
                <label asp-for="MonedaDoc" class="form-label">Moneda</label>
                <select asp-for="MonedaDoc" class="form-select" asp-items="ViewBag.Monedas" disabled="@esEdicion">
                    <option value="">-- Seleccione --</option>
                </select>
            </div>

            @if (!esEdicion)
            {
                <div class="col-12" id="bloqueFacturaBase">
                    <label class="form-label">Basado en factura (opcional)</label>
                    <div class="input-group">
                        <select id="selectFacturaBase" class="form-select" disabled>
                            <option value="">-- Selecciona un socio para ver sus facturas --</option>
                        </select>
                        <button type="button" class="btn btn-outline-primary" id="btnCargarFacturaBase" disabled>Cargar</button>
                        <button type="button" class="btn btn-outline-secondary d-none" id="btnQuitarFacturaBase">Quitar</button>
                    </div>
                    <span class="form-text" id="textoFacturaBase">Sin factura base: puedes crear la nota libremente.</span>
                    <input type="hidden" id="baseEntry" value="" />
                </div>
            }
            else if (Model.BaseTipo == 6 && Model.BaseEntry.HasValue)
            {
                <div class="col-12">
                    <label class="form-label">Documento base</label>
                    <input class="form-control" value="Factura (entrada @Model.BaseEntry)" disabled />
                </div>
            }

            <div class="col-md-4">
                <label asp-for="Direccion" class="form-label"></label>
                <input asp-for="Direccion" class="form-control" readonly="@esEdicion" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha documento</label>
                <input type="date" name="FechaDoc" id="FechaDoc" class="form-control" value="@Model.FechaDoc?.ToString("yyyy-MM-dd")" readonly="@esEdicion" />
            </div>
            <div class="col-md-4">
                <label class="form-label">Fecha emisión</label>
                <input type="date" name="FechaEmision" id="FechaEmision" class="form-control" value="@Model.FechaEmision?.ToString("yyyy-MM-dd")" readonly="@esEdicion" />
            </div>

            <div class="col-md-3">
                <label asp-for="PrctjeDesc" class="form-label">% Descuento</label>
                <input asp-for="PrctjeDesc" type="number" step="0.01" class="form-control" readonly="readonly" />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total bruto</label>
                <input id="TotalBruto" class="form-control" value="@Model.TotalBruto" disabled />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total impuesto</label>
                <input id="TotalImp" class="form-control" value="@Model.TotalImp" disabled />
            </div>
            <div class="col-md-3">
                <label class="form-label">Total documento</label>
                <input id="TotalDoc" class="form-control" value="@Model.TotalDoc" disabled />
            </div>

            <div class="col-12">
                <label asp-for="Comentario" class="form-label"></label>
                <textarea asp-for="Comentario" class="form-control" rows="2" readonly="@(((Model.Cancelado ?? "N") == "S") || !puedeGuardar)"></textarea>
            </div>
        </div>
    </form>

    <hr />
    <div class="d-flex justify-content-between align-items-center mb-2">
        <h6 class="mb-0">Detalle</h6>
        @if (!esEdicion && User.TienePermiso("NotasDebito", Accion.Agregar))
        {
            <button type="button" class="btn btn-sm btn-outline-primary" id="btnNuevaLinea">
                <i class="fa-solid fa-plus me-1"></i>Agregar línea
            </button>
        }
    </div>

    @if (!esEdicion)
    {
        <p class="text-muted small">Las líneas agregadas aquí se guardarán junto con la nota de débito. Después no se pueden modificar.</p>
    }
    else
    {
        <p class="text-muted small">El documento ya está registrado. Solo se puede modificar el comentario; las líneas son de solo lectura.</p>
    }
    <p id="avisoInventario" class="small text-info d-none">
        <i class="fa-solid fa-boxes-stacked me-1"></i>Nota por artículos: la cantidad de cada línea sale del inventario al guardar y vuelve a ingresar si se cancela la nota.
    </p>

    <div class="table-responsive">
        <table id="tblDetalleNotaDebito" class="table table-sm table-hover align-middle w-100" data-entry="@ViewBag.EntryActual" data-es-edicion="@esEdicion.ToString().ToLower()">
            <thead>
                <tr>
                    <th class="col-articulo d-none">Artículo</th>
                    <th>Descripción</th>
                    <th>Cantidad</th>
                    <th>Precio</th>
                    <th>% Desc.</th>
                    <th>Impuesto</th>
                    <th>Total línea</th>
                    <th class="text-end">Acciones</th>
                </tr>
            </thead>
            <tbody></tbody>
        </table>
    </div>

    <div id="panelLineaDetalle" class="border rounded p-3 mb-2 d-none">
        <form id="formLineaDetalle">
            <div class="row g-2">
                <div id="grupoArticulo" class="col-12 d-none">
                    <div class="row g-2">
                        <div class="col-md-4 position-relative">
                            <label class="form-label">Código de artículo</label>
                            <input type="text" id="detArtCodTexto" class="form-control" placeholder="Buscar por código..." autocomplete="off" />
                            <div class="invalid-feedback" id="detArtCodError">Selecciona una opción de la lista o borra el texto.</div>
                            <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detArtCodResultados"></ul>
                        </div>
                        <div class="col-md-4 position-relative">
                            <label class="form-label">Descripción de artículo</label>
                            <input type="text" id="detArtDescTexto" class="form-control" placeholder="Buscar por descripción..." autocomplete="off" />
                            <div class="invalid-feedback" id="detArtDescError">Selecciona una opción de la lista o borra el texto.</div>
                            <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detArtDescResultados"></ul>
                            <input type="hidden" id="detCodArticulo" />
                        </div>
                        <div class="col-md-4 position-relative">
                            <label class="form-label">Almacén</label>
                            <input type="text" id="detCodAlmacenTexto" class="form-control" placeholder="Buscar por código o nombre..." autocomplete="off" />
                            <input type="hidden" id="detCodAlmacen" />
                            <div class="invalid-feedback" id="detCodAlmacenError">Selecciona una opción de la lista o borra el texto.</div>
                            <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodAlmacenResultados"></ul>
                        </div>
                    </div>
                </div>

                <div class="col-md-8">
                    <label class="form-label" id="lblDetDescripcion">Descripción del concepto</label>
                    <input id="detDescripcion" class="form-control" maxlength="200" />
                </div>
                <div class="col-md-4 position-relative">
                    <label class="form-label">Impuesto</label>
                    <input type="text" id="detCodigoImpuestoTexto" class="form-control" placeholder="Buscar por nombre..." autocomplete="off" />
                    <input type="hidden" id="detCodigoImpuesto" />
                    <div class="invalid-feedback" id="detCodigoImpuestoError">Selecciona una opción de la lista o borra el texto.</div>
                    <ul class="list-group position-absolute w-100 shadow-sm d-none" style="z-index: 1055; max-height: 220px; overflow-y: auto;" id="detCodigoImpuestoResultados"></ul>
                </div>

                <div class="col-md-3">
                    <label class="form-label">Cantidad</label>
                    <input id="detCantidad" type="number" step="0.01" class="form-control" value="1" />
                </div>
                <div class="col-md-3">
                    <label class="form-label">Precio</label>
                    <input id="detPrecio" type="number" step="0.01" class="form-control" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">% Desc.</label>
                    <input id="detPrctjeDesc" type="number" step="0.01" class="form-control" value="0" />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Impuesto (Q)</label>
                    <input id="detImpuestoMonto" type="number" step="0.01" class="form-control" readonly />
                </div>
                <div class="col-md-2">
                    <label class="form-label">Total línea</label>
                    <input id="detTotalLinea" type="number" step="0.01" class="form-control" readonly />
                </div>
            </div>
            <div class="text-end mt-2">
                <button type="button" class="btn btn-sm btn-secondary" id="btnCancelarLinea">Cancelar</button>
                <button type="button" class="btn btn-sm btn-primary" id="btnGuardarLinea">Guardar línea</button>
            </div>
        </form>
    </div>

    @if (!esEdicion)
    {
        <script id="datosSeriesNotaDebito" type="application/json">
            @Html.Raw(JsonSerializer.Serialize(ViewBag.SeriesNotaDebito, opcionesJson))
        </script>
    }
</div>
<div class="modal-footer">
    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
    @if (esEdicion && (Model.Cancelado ?? "N") != "S" && User.TienePermiso("NotasDebito", Accion.Eliminar))
    {
        <button type="button" class="btn btn-outline-danger" id="btnCancelarDocNotaDebito" data-entry="@ViewBag.EntryActual">
            <i class="fa-solid fa-ban me-1"></i>Cancelar documento
        </button>
    }
    @if ((Model.Cancelado ?? "N") != "S" && puedeGuardar)
    {
        <button type="button" class="btn btn-primary" id="btnGuardarNotaDebito" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
            <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
        </button>
    }
</div>
```

- [ ] **Step 3: Lista (columna "Tipo")**

En `Web.UI/Views/NotasDebito/Index.cshtml`, después de `<th>No. Documento</th>` insertar (misma sangría) `<th>Tipo</th>`:

```bash
sed -i 's|^\( *\)<th>No. Documento</th>$|&\n\1<th>Tipo</th>|' Web.UI/Views/NotasDebito/Index.cshtml
grep -n "<th>" Web.UI/Views/NotasDebito/Index.cshtml     # No. Documento, Tipo, Socio de negocio, Fecha, Estado, Total, Acciones
```

- [ ] **Step 4: JavaScript**

Sobrescribir `Web.UI/wwwroot/js/notasdebito.js` (LF) con:

```javascript
$(function () {
    // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
    const TIPO_BASE = 6;   // BaseTipo de la factura base: 6 = Factura de venta
    // ---- fin de constantes ----

    const esc = v => $('<div>').text(v ?? '').html();

    const datosPermisosEl = document.getElementById('datosPermisosNotasDebito');
    const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true };

    // Estado compartido del detalle (declarado arriba: lo usan funciones de varias secciones).
    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let lineaEnEdicionId = null;
    let tasaImpuestoSeleccionado = 0;
    let buscadorImpuesto;
    let buscadorArticuloCod, buscadorArticuloDesc, buscadorAlmacen;
    let sincronizandoArticulo = false;
    let listaPrecioSocio = null;

    const tabla = $('#tblNotasDebito').DataTable({
        ajax: { url: '/NotasDebito/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'numDoc' },
            { data: 'docType', render: d => d === 'I' ? 'Artículos' : 'Concepto' },
            { data: 'nombreSn', render: (d, t, row) => esc(d || row.codigoSn || '') },
            { data: 'fechaDoc', render: d => d ? new Date(d).toLocaleDateString() : '' },
            {
                data: 'cancelado',
                render: d => d === 'S'
                    ? '<span class="badge text-bg-danger">Cancelado</span>'
                    : '<span class="badge text-bg-success">Abierto</span>'
            },
            { data: 'totalDoc', render: d => d != null ? Number(d).toFixed(2) : '' },
            {
                data: 'entry', orderable: false, className: 'text-end',
                render: entry => `
                    <button class="btn btn-sm btn-outline-primary btn-editar" data-entry="${entry}"${permisos.actualizar ? '' : ' title="Ver"'}><i class="fa-solid ${permisos.actualizar ? 'fa-pen' : 'fa-eye'}"></i></button>
                    ${permisos.eliminar ? `<button class="btn btn-sm btn-outline-danger btn-eliminar" data-entry="${entry}"><i class="fa-solid fa-trash"></i></button>` : ''}
                `
            }
        ],
        language: App.datatableEsEs
    });

    function recargarTabla() { tabla.ajax.reload(null, false); }

    function abrirModal(html) {
        $('#contenidoModal').html(html);
        $('#modalFormulario').off('.autocompletar'); // limpia los listeners de la apertura anterior antes de que los buscadores registren los suyos
        bootstrap.Modal.getOrCreateInstance(document.getElementById('modalFormulario')).show();
        inicializarSerie();
        inicializarBuscadorSocio();
        inicializarDetalle();
        aplicarModo();
        cargarFacturasBase();
    }

    function cerrarModal() {
        bootstrap.Modal.getOrCreateInstance(document.getElementById('modalFormulario')).hide();
    }

    // --- Tipo de nota: por artículos (I, mueve inventario) o por concepto (S) ---

    function porArticulos() {
        return $('#DocType').val() === 'I';
    }

    // Muestra u oculta lo propio de cada modo: columna y panel de artículo, aviso de inventario y etiqueta.
    function aplicarModo() {
        const art = porArticulos();
        $('.col-articulo').toggleClass('d-none', !art);
        $('#grupoArticulo').toggleClass('d-none', !art);
        $('#avisoInventario').toggleClass('d-none', !art);
        $('#lblDetDescripcion').text(art ? 'Descripción (opcional)' : 'Descripción del concepto');
    }

    $(document).on('change', '#DocType', async function () {
        if (esEdicionDetalle()) return;
        const nuevo = $('#DocType').val();

        if (lineasLocales.length > 0) {
            const ok = await Swal.fire({
                icon: 'question',
                title: '¿Cambiar el tipo de nota?',
                text: 'Se quitarán las líneas ya cargadas.',
                showCancelButton: true,
                confirmButtonText: 'Cambiar',
                cancelButtonText: 'Cancelar'
            }).then(r => r.isConfirmed);
            if (!ok) {
                $('#DocType').val(nuevo === 'I' ? 'S' : 'I');
                return;
            }
            lineasLocales = [];
            lineaEnEdicionId = null;
            $('#panelLineaDetalle').addClass('d-none');
        }

        aplicarModo();
        pintarDetalle();
    });

    // --- Socio de negocio ---

    function inicializarBuscadorSocio() {
        if ($('#CodigoSnTexto').length === 0) return;
        App.autocompletar({
            texto: $('#CodigoSnTexto'),
            oculto: $('#CodigoSn'),
            lista: $('#CodigoSnResultados'),
            error: $('#CodigoSnError'),
            endpoint: '/NotasDebito/BuscarSocios',
            obtenerCodigo: s => s.codigo ?? s.Codigo,
            obtenerEtiqueta: s => `${s.codigo ?? s.Codigo} - ${s.nombre ?? s.Nombre}`,
            onSeleccion: s => {
                $('#NombreSn').val(s ? (s.nombre ?? s.Nombre) : '');
                // La lista de precio del socio se usa al elegir artículos (modo por artículos).
                listaPrecioSocio = s ? (s.numLstPrecio ?? s.NumLstPrecio ?? null) : null;
                // La factura base pertenece al socio anterior: se descarta y se recarga la lista del nuevo.
                if (hayFacturaBase()) quitarFacturaBase();
                cargarFacturasBase();
            }
        });
    }

    // --- Botones de la lista ---

    $('#btnNuevo').on('click', async function () {
        const html = await $.get('/NotasDebito/FormularioCrear');
        abrirModal(html);
    });

    $('#tblNotasDebito').on('click', '.btn-editar', async function () {
        const entry = $(this).data('entry');
        const html = await $.get('/NotasDebito/FormularioEditar', { entry });
        abrirModal(html);
    });

    $('#tblNotasDebito').on('click', '.btn-eliminar', async function () {
        const entry = $(this).data('entry');
        const confirmado = await App.confirmarEliminar(`Se eliminará la nota de débito #${entry}. Solo se pueden eliminar notas ya canceladas.`);
        if (!confirmado) return;

        const respuesta = await App.eliminar(`/NotasDebito/Eliminar?entry=${entry}`);
        if (!respuesta.resultado) {
            App.mostrarError(respuesta.mensaje);
            return;
        }
        App.mostrarExito('Nota de débito eliminada correctamente.');
        recargarTabla();
    });

    $(document).on('click', '#btnCancelarDocNotaDebito', async function () {
        const entry = $(this).data('entry');
        const mensaje = porArticulos()
            ? 'Se cancelará este documento y el inventario se revertirá. Esta acción no se puede deshacer.'
            : 'Se cancelará este documento. Esta acción no se puede deshacer.';
        const confirmado = await App.confirmarEliminar(mensaje);
        if (!confirmado) return;

        const $btn = $(this).prop('disabled', true);
        try {
            const respuesta = await App.enviarJson(`/NotasDebito/Editar?entry=${entry}`, 'POST', { Cancelado: 'S' });
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }
            cerrarModal();
            App.mostrarExito('Documento cancelado.');
            recargarTabla();
        } finally {
            $btn.prop('disabled', false);
        }
    });

    // --- Serie de numeración (solo aplica al crear) ---

    function inicializarSerie() {
        const $sel = $('#selectSerieNotaDebito');
        if ($sel.length === 0) return;

        const datosEl = document.getElementById('datosSeriesNotaDebito');
        const series = datosEl ? (JSON.parse(datosEl.textContent) || []) : [];

        $sel.html('');
        series.forEach(s => {
            const serie = s.serie ?? s.Serie;
            const nombre = s.nombreSerie ?? s.NombreSerie;
            const manual = s.manual ?? s.Manual;
            const sigNumero = s.sigNumero ?? s.SigNumero;
            $sel.append($('<option>').val(serie).text(nombre).attr('data-manual', manual).attr('data-sig-numero', sigNumero ?? ''));
        });

        // Preselecciona la serie "por defecto" del objeto; si no esta entre las opciones, la primera.
        const serieDefecto = $sel.data('serie-defecto');
        const tieneSerieDefecto = serieDefecto !== undefined && serieDefecto !== '' &&
            $sel.find(`option[value="${serieDefecto}"]`).length > 0;
        if (tieneSerieDefecto) {
            $sel.val(String(serieDefecto));
        } else if ($sel.find('option').length > 0) {
            $sel.prop('selectedIndex', 0);
        }

        actualizarNumDocSegunSerie();
    }

    function esSerieManual() {
        const $sel = $('#selectSerieNotaDebito');
        if ($sel.length === 0 || !$sel.val()) return true;
        return $sel.find('option:selected').attr('data-manual') === 'S';
    }

    function actualizarNumDocSegunSerie() {
        const $numDoc = $('#NumDoc');
        if ($numDoc.length === 0) return;

        if (esSerieManual()) {
            $numDoc.val('').prop('disabled', false).attr('placeholder', '');
        } else {
            const sigNumero = $('#selectSerieNotaDebito').find('option:selected').attr('data-sig-numero');
            $numDoc.val(sigNumero ?? '').prop('disabled', true).attr('placeholder', 'Se generará al guardar');
        }
    }

    $(document).on('change', '#selectSerieNotaDebito', actualizarNumDocSegunSerie);

    // --- Guardar ---

    $(document).on('click', '#btnGuardarNotaDebito', async function () {
        const $boton = $(this);
        const esEdicion = $boton.data('edicion') === true || $boton.data('edicion') === 'true';
        const entry = $boton.data('entry');

        if (esEdicion) {
            // En edición solo se puede cambiar el comentario (las líneas y el encabezado quedan fijos).
            $boton.prop('disabled', true);
            try {
                const respuesta = await App.enviarJson(`/NotasDebito/Editar?entry=${entry}`, 'POST', {
                    Cancelado: 'N',
                    Comentario: $('#Comentario').val()
                });
                if (!respuesta.resultado) {
                    App.mostrarError(respuesta.mensaje);
                    return;
                }
                cerrarModal();
                App.mostrarExito('Nota de débito actualizada correctamente.');
                recargarTabla();
            } finally {
                $boton.prop('disabled', false);
            }
            return;
        }

        if (!$('#selectSerieNotaDebito').val()) {
            App.mostrarError('Debes seleccionar una serie.');
            return;
        }
        if (!$('#DocType').val()) {
            App.mostrarError('Selecciona el tipo de nota.');
            return;
        }
        if (!$('#CodigoSn').val()) {
            App.mostrarError('Selecciona un socio de negocio.');
            return;
        }
        if (!$('#MonedaDoc').val()) {
            App.mostrarError('Selecciona una moneda.');
            return;
        }
        if (lineasLocales.length === 0) {
            App.mostrarError('Agrega al menos una línea al documento.');
            return;
        }
        if (porArticulos() && lineasLocales.some(l => !(l.CodArticulo && l.CodAlmacen))) {
            App.mostrarError('Todas las líneas de una nota por artículos deben tener artículo y almacén.');
            return;
        }

        // El número de documento no se pide para series no manuales: el servidor lo calcula al registrar.
        const datos = App.recolectarFormulario('#formNotaDebito');
        datos.Serie = $('#selectSerieNotaDebito').val();
        datos.DocType = $('#DocType').val();

        const baseEntry = Number($('#baseEntry').val()) || null;
        datos.BaseTipo = baseEntry ? TIPO_BASE : -1;
        datos.BaseEntry = baseEntry;

        const totales = calcularTotalesDesdeLineas(lineasLocales);
        datos.TotalBruto = totales.totalBruto;
        datos.TotalDesc = totales.totalDesc;
        datos.TotalImp = totales.totalImp;
        datos.TotalDoc = totales.totalDoc;
        datos.Lineas = lineasLocales.map(({ _id, ...linea }) => linea);

        $boton.prop('disabled', true);
        try {
            const respuesta = await App.enviarJson('/NotasDebito/Crear', 'POST', datos);
            if (!respuesta.resultado) {
                App.mostrarError(respuesta.mensaje);
                return;
            }

            const sufijoNumDoc = respuesta.numDoc != null ? ` No. documento: ${respuesta.numDoc}.` : '';
            await App.mostrarExito(`Nota de débito creada correctamente.${sufijoNumDoc}`);
            cerrarModal();
            recargarTabla();
        } finally {
            $boton.prop('disabled', false);
        }
    });

    // --- Factura base (opcional, solo al crear) ---

    function hayFacturaBase() {
        return !!$('#baseEntry').val();
    }

    function quitarFacturaBase() {
        $('#baseEntry').val('');
        $('#textoFacturaBase').text('Sin factura base: puedes crear la nota libremente.');
        $('#btnQuitarFacturaBase').addClass('d-none');
        $('#selectFacturaBase').val('');
        $('#btnCargarFacturaBase').prop('disabled', true);
    }

    async function cargarFacturasBase() {
        const $sel = $('#selectFacturaBase');
        if ($sel.length === 0) return;

        const codigoSn = $('#CodigoSn').val();
        $('#btnCargarFacturaBase').prop('disabled', true);
        if (!codigoSn) {
            $sel.html('<option value="">-- Selecciona un socio para ver sus facturas --</option>').prop('disabled', true);
            return;
        }

        let respuesta;
        try {
            respuesta = await $.get('/NotasDebito/BuscarFacturasBase', { codigoSn });
        } catch (e) {
            // Si el socio cambio mientras tanto, otra carga manda sobre el select.
            if ($('#CodigoSn').val() !== codigoSn) return;
            $sel.html('<option value="">-- Sin factura base --</option>').prop('disabled', false);
            return;
        }
        // Respuesta vieja: el socio cambio mientras esperabamos.
        if ($('#CodigoSn').val() !== codigoSn) return;

        const lista = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        $sel.html('<option value="">-- Sin factura base --</option>').prop('disabled', false);
        lista.forEach(f => {
            const fecha = f.fechaDoc ? new Date(f.fechaDoc).toLocaleDateString() : '';
            const total = f.totalDoc != null ? Number(f.totalDoc).toFixed(2) : '';
            $sel.append($('<option>').val(f.entry).text(`#${f.numDoc} - ${fecha} - ${f.monedaDoc ?? ''} ${total}`));
        });
    }

    $(document).on('change', '#selectFacturaBase', function () {
        $('#btnCargarFacturaBase').prop('disabled', !$('#selectFacturaBase').val());
    });

    $(document).on('click', '#btnCargarFacturaBase', async function () {
        const entry = $('#selectFacturaBase').val();
        if (!entry) return;

        if (lineasLocales.length > 0) {
            const ok = await Swal.fire({
                icon: 'question',
                title: '¿Reemplazar las líneas?',
                text: 'Las líneas actuales se reemplazarán por las de la factura seleccionada.',
                showCancelButton: true,
                confirmButtonText: 'Reemplazar',
                cancelButtonText: 'Cancelar'
            }).then(r => r.isConfirmed);
            if (!ok) return;
        }

        const respuesta = await $.get('/NotasDebito/ObtenerFacturaBase', { entry });
        if (!respuesta.resultado || !respuesta.dato) {
            App.mostrarError(respuesta.mensaje || 'No se pudo cargar la factura.');
            return;
        }

        const factura = respuesta.dato;
        $('#baseEntry').val(factura.entry);
        $('#textoFacturaBase').text(`Basada en la factura #${factura.numDoc}. Puedes ajustar las líneas antes de guardar.`);
        $('#btnQuitarFacturaBase').removeClass('d-none');

        const monedaExiste = $('#MonedaDoc option').filter((_, o) => o.value === factura.monedaDoc).length > 0;
        if (factura.monedaDoc && monedaExiste) $('#MonedaDoc').val(factura.monedaDoc);
        if (factura.direccion) $('#Direccion').val(factura.direccion);

        // Por artículos: las líneas conservan artículo y almacén de la factura. Por concepto: solo el texto y los importes.
        const art = porArticulos();
        lineasLocales = (factura.lineas || [])
            .filter(l => !art || (l.codArticulo && l.codAlmacen))
            .map(l => ({
                ...(art ? { CodArticulo: l.codArticulo, CodAlmacen: l.codAlmacen } : {}),
                Descripcion: l.descripcion,
                Cantidad: l.cantidad,
                Precio: l.precio,
                PrecioBruto: l.precioBruto,
                PrctjeDesc: l.prctjeDesc ?? 0,
                CodigoImpuesto: l.codigoImpuesto,
                Impuesto: l.impuesto,
                TotalLinea: l.totalLinea,
                _id: proximoIdLocal++
            }));
        lineaEnEdicionId = null;
        $('#panelLineaDetalle').addClass('d-none');
        pintarDetalle();
    });

    $(document).on('click', '#btnQuitarFacturaBase', quitarFacturaBase);

    // --- Detalle: en creación se administra localmente; en edición es de solo lectura ---

    function esEdicionDetalle() {
        const v = $('#tblDetalleNotaDebito').data('es-edicion');
        return v === true || v === 'true';
    }

    function inicializarDetalle() {
        lineasLocales = [];
        lineasRemotas = [];
        proximoIdLocal = 1;
        lineaEnEdicionId = null;
        tasaImpuestoSeleccionado = 0;
        listaPrecioSocio = null;

        if ($('#tblDetalleNotaDebito').length === 0) return;

        const codSnActual = $('#CodigoSn').val();
        if (codSnActual) {
            App.obtenerListaDeSocio(codSnActual).then(n => { listaPrecioSocio = n; });
        }

        buscadorImpuesto = App.autocompletar({
            texto: $('#detCodigoImpuestoTexto'), oculto: $('#detCodigoImpuesto'),
            lista: $('#detCodigoImpuestoResultados'), error: $('#detCodigoImpuestoError'),
            endpoint: '/NotasDebito/BuscarImpuestos',
            obtenerCodigo: i => i.codigo ?? i.Codigo,
            obtenerEtiqueta: i => `${i.nombre ?? i.Nombre} (${i.tasa ?? i.Tasa ?? 0}%)`,
            minCaracteres: 0,
            onSeleccion: i => {
                tasaImpuestoSeleccionado = i ? Number(i.tasa ?? i.Tasa ?? 0) : 0;
                recalcularLinea();
            }
        });

        if ($('#detArtCodTexto').length > 0) {
            buscadorArticuloCod = App.autocompletar({
                texto: $('#detArtCodTexto'), oculto: $('#detCodArticulo'),
                lista: $('#detArtCodResultados'), error: $('#detArtCodError'),
                endpoint: '/NotasDebito/BuscarArticulosPorCodigo',
                obtenerCodigo: a => a.codigo ?? a.Codigo,
                obtenerEtiqueta: a => `${a.codigo ?? a.Codigo} - ${a.nombre ?? a.Nombre}`,
                onSeleccion: a => aplicarArticuloSeleccionado(a, 'cod')
            });
            buscadorArticuloDesc = App.autocompletar({
                texto: $('#detArtDescTexto'), oculto: $('#detCodArticulo'),
                lista: $('#detArtDescResultados'), error: $('#detArtDescError'),
                endpoint: '/NotasDebito/BuscarArticulos',
                obtenerCodigo: a => a.codigo ?? a.Codigo,
                obtenerEtiqueta: a => `${a.nombre ?? a.Nombre} (${a.codigo ?? a.Codigo})`,
                onSeleccion: a => aplicarArticuloSeleccionado(a, 'desc')
            });
            buscadorAlmacen = App.autocompletar({
                texto: $('#detCodAlmacenTexto'), oculto: $('#detCodAlmacen'),
                lista: $('#detCodAlmacenResultados'), error: $('#detCodAlmacenError'),
                endpoint: '/NotasDebito/BuscarAlmacenes',
                obtenerCodigo: al => al.codigo ?? al.Codigo,
                obtenerEtiqueta: al => `${al.codigo ?? al.Codigo} - ${al.nombre ?? al.Nombre}`
            });
        }

        if (esEdicionDetalle()) {
            cargarDetalleRemoto();
        } else {
            pintarDetalle();
        }
    }

    async function cargarDetalleRemoto() {
        const entry = $('#tblDetalleNotaDebito').data('entry');
        const respuesta = await $.get('/NotasDebito/ObtenerDetalle', { entry });
        lineasRemotas = (respuesta.resultado && respuesta.dato) ? respuesta.dato : [];
        pintarDetalle();
    }

    function calcularTotalesDesdeLineas(lista) {
        let totalBruto = 0, totalDesc = 0, totalImp = 0, totalDoc = 0;
        lista.forEach(l => {
            const cantidad = Number(l.cantidad ?? l.Cantidad ?? 0);
            const precio = Number(l.precio ?? l.Precio ?? 0);
            const prctjeDesc = Number(l.prctjeDesc ?? l.PrctjeDesc ?? 0);
            const impuesto = Number(l.impuesto ?? l.Impuesto ?? 0);
            const bruto = cantidad * precio;
            const desc = bruto * (prctjeDesc / 100);
            totalBruto += bruto;
            totalDesc += desc;
            totalImp += impuesto;
            totalDoc += (bruto - desc + impuesto);
        });
        return {
            totalBruto: totalBruto.toFixed(2),
            totalDesc: totalDesc.toFixed(2),
            totalImp: totalImp.toFixed(2),
            totalDoc: totalDoc.toFixed(2)
        };
    }

    function pintarDetalle() {
        const $tbody = $('#tblDetalleNotaDebito tbody');
        if ($tbody.length === 0) return;

        const lista = esEdicionDetalle() ? lineasRemotas : lineasLocales;
        const art = porArticulos();

        const totales = calcularTotalesDesdeLineas(lista);
        $('#TotalBruto').val(totales.totalBruto);
        $('#TotalImp').val(totales.totalImp);
        $('#TotalDoc').val(totales.totalDoc);

        if (lista.length === 0) {
            $tbody.html(`<tr><td colspan="${art ? 8 : 7}" class="text-center text-muted">Sin líneas de detalle</td></tr>`);
            return;
        }

        $tbody.html(lista.map(linea => {
            const codArticulo = linea.codArticulo ?? linea.CodArticulo;
            const codAlmacen = linea.codAlmacen ?? linea.CodAlmacen;
            const descripcion = linea.descripcion ?? linea.Descripcion;
            const cantidad = linea.cantidad ?? linea.Cantidad;
            const precio = linea.precio ?? linea.Precio;
            const prctjeDesc = linea.prctjeDesc ?? linea.PrctjeDesc;
            const impuesto = linea.impuesto ?? linea.Impuesto;
            const totalLinea = linea.totalLinea ?? linea.TotalLinea;
            // En edición las líneas son de solo lectura: no se pintan botones por fila.
            const acciones = esEdicionDetalle() ? '' : `
                        <button type="button" class="btn btn-sm btn-outline-primary btn-editar-linea" data-id="${linea._id}"><i class="fa-solid fa-pen"></i></button>
                        <button type="button" class="btn btn-sm btn-outline-danger btn-eliminar-linea" data-id="${linea._id}"><i class="fa-solid fa-trash"></i></button>`;
            const celdaArticulo = art
                ? `<td>${esc(codArticulo)}${codAlmacen ? ` <span class="text-muted small">(${esc(codAlmacen)})</span>` : ''}</td>`
                : '';
            return `
                <tr>
                    ${celdaArticulo}
                    <td>${esc(descripcion)}</td>
                    <td>${cantidad ?? ''}</td>
                    <td>${precio != null ? Number(precio).toFixed(2) : ''}</td>
                    <td>${prctjeDesc ?? 0}</td>
                    <td>${impuesto != null ? Number(impuesto).toFixed(2) : '0.00'}</td>
                    <td>${totalLinea != null ? Number(totalLinea).toFixed(2) : ''}</td>
                    <td class="text-end">${acciones}</td>
                </tr>
            `;
        }).join(''));
    }

    // --- Artículo y almacén (modo por artículos) ---

    // Sincroniza las dos cajas de artículo sin re-disparar onSeleccion (establecer() lo haría).
    function setArticuloEnAmbasCajas(item) {
        if (!buscadorArticuloCod) return;
        sincronizandoArticulo = true;
        try {
            buscadorArticuloCod.establecer(item || null);
            buscadorArticuloDesc.establecer(item || null);
            $('#detCodArticulo').val(item ? (item.codigo ?? item.Codigo) : '');
        } finally {
            sincronizandoArticulo = false;
        }
    }

    // Handler de selección del usuario en cualquiera de las dos cajas.
    function aplicarArticuloSeleccionado(a, origen) {
        if (sincronizandoArticulo) return; // corta la recursión de establecer()
        sincronizandoArticulo = true;
        try {
            $('#detCodArticulo').val(a ? (a.codigo ?? a.Codigo) : '');
            if (origen !== 'cod')  buscadorArticuloCod.establecer(a || null);
            if (origen !== 'desc') buscadorArticuloDesc.establecer(a || null);
        } finally {
            sincronizandoArticulo = false;
        }
        if (!a) { buscadorAlmacen.establecer(null); return; }
        $('#detDescripcion').val(a.nombre ?? a.Nombre ?? '');
        aplicarPrecioDeArticulo(a);
        aplicarAlmacenDefectoDeArticulo(a);
        recalcularLinea();
    }

    // Coloca en #detPrecio el precio de lista del socio, o el PrecioUnitario base si no hay lista.
    async function aplicarPrecioDeArticulo(a) {
        const base = a.precioUnitario ?? a.PrecioUnitario ?? 0;
        const codArt = a.codigo ?? a.Codigo;
        const pLista = await App.resolverPrecioLista(codArt, listaPrecioSocio);
        $('#detPrecio').val(pLista != null ? pLista : base);
        recalcularLinea();
    }

    async function aplicarAlmacenDefectoDeArticulo(a) {
        const cod = a.almacenDefecto ?? a.AlmacenDefecto ?? '';
        if (!cod) { buscadorAlmacen.establecer(null); return; }
        const r = await $.get('/NotasDebito/ObtenerAlmacenPorCodigo', { codigo: cod });
        buscadorAlmacen.establecer(r.resultado && r.dato ? r.dato : { codigo: cod, nombre: cod });
    }

    function limpiarPanelLinea() {
        buscadorImpuesto.establecer(null);
        if (buscadorArticuloCod) {
            setArticuloEnAmbasCajas(null);
            buscadorAlmacen.establecer(null);
        }
        tasaImpuestoSeleccionado = 0;
        $('#detDescripcion').val('');
        $('#detCantidad').val('1');
        $('#detPrecio').val('');
        $('#detPrctjeDesc').val('0');
        $('#detImpuestoMonto').val('');
        $('#detTotalLinea').val('');
        lineaEnEdicionId = null;
    }

    /** Recalcula el monto de impuesto y el total de la línea con base en los campos actuales del panel. */
    function recalcularLinea() {
        const cantidad = Number($('#detCantidad').val()) || 0;
        const precio = Number($('#detPrecio').val()) || 0;
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;
        const tasa = tasaImpuestoSeleccionado || 0;

        const bruto = cantidad * precio;
        const desc = bruto * (prctjeDesc / 100);
        const subtotal = bruto - desc;
        const impuesto = subtotal * (tasa / 100);
        const total = subtotal + impuesto;

        $('#detImpuestoMonto').val(impuesto.toFixed(2));
        $('#detTotalLinea').val(total.toFixed(2));
    }

    $(document).on('input change', '#detCantidad, #detPrecio, #detPrctjeDesc', recalcularLinea);

    $(document).on('click', '#btnNuevaLinea', function () {
        limpiarPanelLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '#btnCancelarLinea', function () {
        $('#panelLineaDetalle').addClass('d-none');
    });

    $(document).on('click', '.btn-editar-linea', async function () {
        const id = Number($(this).data('id'));
        const linea = lineasLocales.find(l => l._id === id);
        if (!linea) return;

        limpiarPanelLinea();
        lineaEnEdicionId = id;

        if (porArticulos()) {
            const codArticulo = linea.CodArticulo ?? linea.codArticulo ?? '';
            const codAlmacen = linea.CodAlmacen ?? linea.codAlmacen ?? '';
            setArticuloEnAmbasCajas(codArticulo ? { codigo: codArticulo, nombre: linea.Descripcion ?? linea.descripcion ?? '' } : null);
            if (codAlmacen) {
                const respuestaAlmacen = await $.get('/NotasDebito/ObtenerAlmacenPorCodigo', { codigo: codAlmacen });
                buscadorAlmacen.establecer(respuestaAlmacen.resultado && respuestaAlmacen.dato ? respuestaAlmacen.dato : { codigo: codAlmacen, nombre: codAlmacen });
            } else {
                buscadorAlmacen.establecer(null);
            }
        }

        const codigoImpuesto = linea.CodigoImpuesto ?? linea.codigoImpuesto ?? '';
        if (codigoImpuesto) {
            const respuestaImpuesto = await $.get('/NotasDebito/ObtenerImpuestoPorCodigo', { codigo: codigoImpuesto });
            if (!respuestaImpuesto.resultado || !respuestaImpuesto.dato) {
                App.mostrarError('No se pudo cargar el impuesto de la línea. Vuelve a intentarlo.');
                limpiarPanelLinea();
                return;
            }
            buscadorImpuesto.establecer(respuestaImpuesto.dato);
        }

        $('#detDescripcion').val(linea.Descripcion ?? linea.descripcion ?? '');
        $('#detCantidad').val(linea.Cantidad ?? linea.cantidad ?? 1);
        $('#detPrecio').val(linea.Precio ?? linea.precio ?? '');
        $('#detPrctjeDesc').val(linea.PrctjeDesc ?? linea.prctjeDesc ?? 0);

        recalcularLinea();
        $('#panelLineaDetalle').removeClass('d-none');
    });

    $(document).on('click', '.btn-eliminar-linea', async function () {
        const id = Number($(this).data('id'));

        const confirmado = await App.confirmarEliminar('Se eliminará la línea de detalle seleccionada.');
        if (!confirmado) return;

        lineasLocales = lineasLocales.filter(l => l._id !== id);
        lineaEnEdicionId = null;
        $('#panelLineaDetalle').addClass('d-none');
        pintarDetalle();
    });

    $(document).on('click', '#btnGuardarLinea', function () {
        const art = porArticulos();
        const descripcion = ($('#detDescripcion').val() || '').trim();
        const codArticulo = $('#detCodArticulo').val() || '';
        const codAlmacen = $('#detCodAlmacen').val() || '';
        const codigoImpuesto = $('#detCodigoImpuesto').val() || '';
        const cantidad = Number($('#detCantidad').val());
        const precioTexto = $('#detPrecio').val();
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;

        if (art) {
            if (!codArticulo) {
                App.mostrarError('Selecciona un artículo.');
                return;
            }
            if (!codAlmacen) {
                App.mostrarError('Selecciona un almacén.');
                return;
            }
        } else if (!descripcion) {
            App.mostrarError('Escribe la descripción del concepto.');
            return;
        }
        if (!(cantidad > 0)) {
            App.mostrarError('La cantidad debe ser mayor que 0.');
            return;
        }
        if (precioTexto === '' || Number(precioTexto) < 0) {
            App.mostrarError('Escribe un precio válido (0 o mayor).');
            return;
        }
        if (prctjeDesc < 0 || prctjeDesc > 100) {
            App.mostrarError('El descuento debe estar entre 0 y 100.');
            return;
        }
        if (!codigoImpuesto) {
            App.mostrarError('Selecciona un impuesto.');
            return;
        }

        recalcularLinea();
        const precio = Number(precioTexto);
        const linea = {
            ...(art ? { CodArticulo: codArticulo, CodAlmacen: codAlmacen } : {}),
            Descripcion: descripcion,
            Cantidad: cantidad,
            Precio: precio,
            PrecioBruto: Number((cantidad * precio).toFixed(2)),
            PrctjeDesc: prctjeDesc,
            CodigoImpuesto: codigoImpuesto,
            Impuesto: Number($('#detImpuestoMonto').val()) || 0,
            TotalLinea: Number($('#detTotalLinea').val()) || 0
        };

        const existe = lineasLocales.some(l => l._id === lineaEnEdicionId);
        if (lineaEnEdicionId !== null && existe) {
            lineasLocales = lineasLocales.map(l => l._id === lineaEnEdicionId ? { ...linea, _id: l._id } : l);
        } else {
            lineasLocales.push({ ...linea, _id: proximoIdLocal++ });
        }

        $('#panelLineaDetalle').addClass('d-none');
        pintarDetalle();
    });
});
```

- [ ] **Step 5: Verificación y commit**

```bash
node --check Web.UI/wwwroot/js/notasdebito.js
grep -o "#[A-Za-z]*" Web.UI/wwwroot/js/notasdebito.js | sort -u | tr -d '#' | while read id; do grep -q "id=\"$id\"\|asp-for=\"$id\"\|name=\"$id\"" Web.UI/Views/NotasDebito/_Form.cshtml Web.UI/Views/NotasDebito/Index.cshtml || echo "id sin vista: $id"; done
```

(El segundo comando lista ids usados por el JS sin definición en las vistas; solo debe mostrar ids de elementos que crea el propio JS o `modalFormulario`/`contenidoModal`/`btnNuevo`, que están en `Index.cshtml`. Revisar cada línea a mano.) `dotnet build Web.slnx` → 0 errores. Comprobar `git diff --stat`: archivos sobrescritos con cambios acotados (el `_Form` ≈ +60 líneas, el controller ≈ +60, el JS ≈ +200).

```bash
git add Web.UI/Controllers/NotasDebitoController.cs Web.UI/Views/NotasDebito/_Form.cshtml Web.UI/Views/NotasDebito/Index.cshtml Web.UI/wwwroot/js/notasdebito.js
git commit -m "feat(notas-debito): pantalla de notas de debito de cliente con tipo de nota (articulos o concepto)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Pantalla de notas de débito de proveedor (derivada del cliente)

**Files:**
- Overwrite (derivados con `sed`): `Web.UI/Controllers/NotasDebitoCompraController.cs`, `Web.UI/Views/NotasDebitoCompra/_Form.cshtml`, `Web.UI/wwwroot/js/notasdebitocompra.js`
- Modify: `Web.UI/Views/NotasDebitoCompra/Index.cshtml`

**Interfaces:**
- Consumes: archivos del cliente de la Task 11; `INotaDebitoCompraApiClient` (`ObtenerFacturasCompraBaseAsync`, `ObtenerFacturaCompraBaseAsync`) y DTOs `Web.ApiClient.Dtos.NotaDebitoCompra` (Task 10).
- Produces: pantalla `NotasDebitoCompra` con el mismo selector "Tipo de nota"; constantes `CodigoObjNotaDebitoCompra = "13"`, `SubTipoDocNotaDebitoCompra = "DN"`, `TipoSocio = "P"`, `TIPO_BASE = 13`; aviso de inventario "ingresa al inventario".

**Precondición verificable:** Task 11 commiteada; `git diff --quiet` sobre los cuatro archivos de la lista; `grep -c "NotasDebitoCompra" Web.UI/Views/NotasDebitoCompra/_Form.cshtml` > 0.

- [ ] **Step 1: Guardar el derivador y comprobarlo antes de aplicarlo**

El derivador `mirw` convierte los archivos del cliente en los del proveedor. Se guarda en un archivo para usarlo en los pasos siguientes (cada llamada de bash es una sesión nueva). Desde la raíz del repo `Web` (bash):

```bash
cat > "C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/mirw.sh" <<'EOF'
mirw() { sed -E \
 -e 's|const TIPO_BASE = 6;   // BaseTipo de la factura base: 6 = Factura de venta|const TIPO_BASE = 13;   // BaseTipo de la factura base: 13 = Factura de compra|' \
 -e 's|"Editar nota de débito" : "Nueva nota de débito"|"Editar nota de débito de proveedor" : "Nueva nota de débito de proveedor"|' \
 -e 's|Basado en factura \(opcional\)|Basado en factura de compra (opcional)|' \
 -e 's|Model\.BaseTipo == 6 |Model.BaseTipo == 13 |' \
 -e 's|value="Factura \(entrada|value="Factura de compra (entrada|' \
 -e 's|Nota por artículos: la cantidad de cada línea sale del inventario al guardar y vuelve a ingresar si se cancela la nota\.|Nota por artículos: la cantidad de cada línea ingresa al inventario al guardar y se descuenta de nuevo si se cancela la nota.|' \
 -e 's|la nota de débito #|la nota de débito de proveedor #|' \
 -e "s|'Nota de débito (eliminada\|actualizada) correctamente\.'|'Nota de débito de proveedor \1 correctamente.'|" \
 -e 's|Selecciona un socio de negocio\.|Selecciona un proveedor.|' \
 -e 's|`Nota de débito creada correctamente|`Nota de débito de proveedor creada correctamente|' \
 -e 's|Sin factura base: puedes|Sin factura de compra base: puedes|' \
 -e 's|Selecciona un socio para ver sus facturas|Selecciona un proveedor para ver sus facturas de compra|' \
 -e 's|Sin factura base --|Sin factura de compra base --|' \
 -e 's|Si el socio cambio|Si el proveedor cambio|' \
 -e 's|Respuesta vieja: el socio cambio|Respuesta vieja: el proveedor cambio|' \
 -e 's|de la factura seleccionada\.|de la factura de compra seleccionada.|' \
 -e 's|`Basada en la factura #|`Basada en la factura de compra #|' \
 -e 's|private const string CodigoObjNotaDebito = "6";|private const string CodigoObjNotaDebito = "13";|' \
 -e 's|private const string SubTipoDocNotaDebito = "ND";|private const string SubTipoDocNotaDebito = "DN";|' \
 -e 's|private const string TipoSocio = "C";|private const string TipoSocio = "P";|' \
 -e 's|"Notas de debito de cliente"|"Notas de debito de proveedor"|' \
 -e 's|la nota comparte objeto con la factura \(6\) y se distingue por el subtipo ND|la nota comparte objeto con la factura de compra (13) y se distingue por el subtipo DN|' \
 -e 's|_notas.ObtenerFacturasBaseAsync|_notas.ObtenerFacturasCompraBaseAsync|' \
 -e 's|_notas.ObtenerFacturaBaseAsync|_notas.ObtenerFacturaCompraBaseAsync|' \
 -e 's/notasdebito/notasdebitocompra/g' \
 -e 's/NotasDebito/NotasDebitoCompra/g' \
 -e 's/NotaDebito/NotaDebitoCompra/g' "$1"; }
EOF
source "C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/mirw.sh"
# HEAD es el commit de la Task 11; HEAD~1 conserva los archivos de cliente ANTERIORES, que deben derivar en los de proveedor actuales:
git show HEAD~1:Web.UI/wwwroot/js/notasdebito.js | mirw /dev/stdin | tr -d '\r' | diff - <(tr -d '\r' < Web.UI/wwwroot/js/notasdebitocompra.js) && echo JS_OK
git show HEAD~1:Web.UI/Views/NotasDebito/_Form.cshtml | mirw /dev/stdin | tr -d '\r' | diff - <(tr -d '\r' < Web.UI/Views/NotasDebitoCompra/_Form.cshtml) && echo FORM_OK
```

Debe imprimir `JS_OK` y `FORM_OK`. Si hay diferencias, corregir la regla de `mirw.sh` correspondiente antes de seguir (es solo un chequeo de la derivación; los archivos de proveedor de HEAD no se tocan todavía).

- [ ] **Step 2: Derivar**

```bash
set -e
source "C:/Users/migue/AppData/Local/Temp/claude/C--Users-migue-source-repos-angelm0508/949e6caf-87d5-4938-88c7-39af8f6d4340/scratchpad/mirw.sh"
mirw Web.UI/wwwroot/js/notasdebito.js > Web.UI/wwwroot/js/notasdebitocompra.js.tmp && mv Web.UI/wwwroot/js/notasdebitocompra.js.tmp Web.UI/wwwroot/js/notasdebitocompra.js
mirw Web.UI/Views/NotasDebito/_Form.cshtml > Web.UI/Views/NotasDebitoCompra/_Form.cshtml.tmp && mv Web.UI/Views/NotasDebitoCompra/_Form.cshtml.tmp Web.UI/Views/NotasDebitoCompra/_Form.cshtml
mirw Web.UI/Controllers/NotasDebitoController.cs > Web.UI/Controllers/NotasDebitoCompraController.cs.tmp && mv Web.UI/Controllers/NotasDebitoCompraController.cs.tmp Web.UI/Controllers/NotasDebitoCompraController.cs
sed -i 's|^\( *\)<th>No. Documento</th>$|&\n\1<th>Tipo</th>|' Web.UI/Views/NotasDebitoCompra/Index.cshtml
node --check Web.UI/wwwroot/js/notasdebitocompra.js
```

- [ ] **Step 3: Revisar lo generado**

```bash
grep -n 'CodigoObjNotaDebitoCompra =\|SubTipoDocNotaDebitoCompra =\|TipoSocio =' Web.UI/Controllers/NotasDebitoCompraController.cs     # "13", "DN", "P"
grep -n 'TIPO_BASE =' Web.UI/wwwroot/js/notasdebitocompra.js                                                                            # 13
grep -n 'Model.BaseTipo ==' Web.UI/Views/NotasDebitoCompra/_Form.cshtml                                                                # 13
grep -rn "Debito\b\|NotasDebito\b\|NotasDebito/" Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Views/NotasDebitoCompra Web.UI/wwwroot/js/notasdebitocompra.js   # vacio (solo ...Compra)
grep -rnE "NotaDebito([^C]|$)" Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Views/NotasDebitoCompra Web.UI/wwwroot/js/notasdebitocompra.js                  # vacio
grep -n "ObtenerFacturas\|ObtenerFactura" Web.UI/Controllers/NotasDebitoCompraController.cs                                            # ...CompraBaseAsync
grep -n "ingresa al inventario" Web.UI/Views/NotasDebitoCompra/_Form.cshtml                                                             # aviso de proveedor
grep -n "cliente" Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Views/NotasDebitoCompra/_Form.cshtml Web.UI/wwwroot/js/notasdebitocompra.js   # solo comentarios que expliquen la diferencia
```

Revisar a mano que los ids del DOM de la vista y del JS coinciden (`formNotaDebitoCompra`, `selectSerieNotaDebitoCompra`, `tblDetalleNotaDebitoCompra`, `btnGuardarNotaDebitoCompra`, `btnCancelarDocNotaDebitoCompra`, `datosSeriesNotaDebitoCompra`, `datosPermisosNotasDebitoCompra`, `tblNotasDebitoCompra`) y que `DocType`, `grupoArticulo`, `avisoInventario`, `lblDetDescripcion`, `col-articulo` están en ambos.

- [ ] **Step 4: Compilar y commit**

`dotnet build Web.slnx` → 0 errores; `node --check` ya ejecutado.

```bash
git add Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Views/NotasDebitoCompra/_Form.cshtml Web.UI/Views/NotasDebitoCompra/Index.cshtml Web.UI/wwwroot/js/notasdebitocompra.js
git commit -m "feat(notas-debito): pantalla de notas de debito de proveedor con tipo de nota (articulos o concepto)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Numeración con clave compuesta y notas de crédito sobre notas de débito (Web)

**Files:**
- Modify: `Web.UI/Controllers/NumeracionDocumentoController.cs`, `Web.UI/Views/NumeracionDocumento/_Form.cshtml`, `Web.UI/wwwroot/js/numeracionDocumento.js`, `Web.UI/wwwroot/js/notascredito.js`, `Web.UI/wwwroot/js/notascreditocompra.js`, `Web.UI/Views/NotasCredito/_Form.cshtml`, `Web.UI/Views/NotasCreditoCompra/_Form.cshtml`

**Interfaces:**
- Consumes: API con clave compuesta `"CodigoObj:SubTipoDoc"` (Task 3); `NotaCreditoFacturaBaseResumenDTO.DocSubType` (Tasks 8 y 10); `NumeracionDocumentoDTO.SubTipoDoc` (ya existe).
- Produces: la pantalla de numeración identifica cada fila por su clave (`6` para la factura, `6:ND` para su nota de débito, `1:C`/`1:P` para socios) al editar; los selectores de factura base de notas de crédito rotulan cada opción como "Factura" o "Nota de débito".

**Precondición verificable:** Tasks 10-12 commiteadas; `grep -n "codigoObj = c.CodigoObj" Web.UI/Controllers/NumeracionDocumentoController.cs` muestra el objeto anónimo de filas; `grep -n "disponible: " Web.UI/wwwroot/js/notascredito.js` muestra la opción del selector.

- [ ] **Step 1: Numeración (controller, vista, JS)**

En `NumeracionDocumentoController.ObtenerTodos`, agregar al objeto anónimo de cada fila (después de `codigoObj = c.CodigoObj,`):

```csharp
                    subTipoDoc = c.SubTipoDoc,
                    clave = string.IsNullOrEmpty(c.SubTipoDoc) || c.SubTipoDoc == "--" ? c.CodigoObj : $"{c.CodigoObj}:{c.SubTipoDoc}",
```

En `FormularioEditar(string codigo)`: el parámetro `codigo` pasa a ser la clave (puede ser `"6:ND"`). Cambiar la construcción del DTO y agregar la clave a la vista:

```csharp
            ViewBag.ClaveNumeracion = codigo;

            var dto = new NumeracionDocumentoCrearDTO
            {
                CodigoObj = respuesta.Dato.CodigoObj,
                SerieDfct = respuesta.Dato.SerieDfct,
                DocAlias = respuesta.Dato.DocAlias,
                SubTipoDoc = respuesta.Dato.SubTipoDoc
            };
```

(Es decir, `CodigoObj = codigo` pasa a `CodigoObj = respuesta.Dato.CodigoObj`; `Editar(string codigo, ...)` no cambia: reenvía `codigo` tal cual a `_numeraciones.ObtenerAsync/ActualizarAsync`, que ahora acepta la clave compuesta.)

En `Web.UI/Views/NumeracionDocumento/_Form.cshtml` (≈ línea 158) cambiar `data-codigo="@Model.CodigoObj"` del botón `btnGuardarNumeracionDocumento` por `data-codigo="@(ViewBag.ClaveNumeracion ?? Model.CodigoObj)"`. No tocar `data-codigo-obj="@Model.CodigoObj"` de la tabla de detalle (sigue siendo el código puro del objeto, que es lo que usa `ObtenerDetalle` y `CrearLinea`).

En `Web.UI/wwwroot/js/numeracionDocumento.js`, columna de acciones (líneas 12-17): reemplazar `data: 'codigoObj'` por `data: 'clave'` (el resto de la columna queda igual, su `render: codigo => ...` ahora recibe la clave y la pone en `data-codigo`).

- [ ] **Step 2: Notas de crédito: rotular la base**

En `Web.UI/wwwroot/js/notascredito.js` y `notascreditocompra.js`, dentro de `cargarFacturasBase`, reemplazar la línea que agrega la opción:

```javascript
            $sel.append($('<option>').val(f.entry).text(`#${f.numDoc} - ${fecha} - ${f.monedaDoc ?? ''} ${total} (disponible: ${disponible})`));
```

por:

```javascript
            const tipoDoc = f.docSubType && f.docSubType !== '--' ? 'Nota de débito' : 'Factura';
            $sel.append($('<option>').val(f.entry).text(`${tipoDoc} #${f.numDoc} - ${fecha} - ${f.monedaDoc ?? ''} ${total} (disponible: ${disponible})`));
```

(Misma línea en ambos archivos; verificar con `grep -c "disponible: \${disponible}" <archivo>` = 1.)

En `Views/NotasCredito/_Form.cshtml`: `Basado en factura (opcional)` → `Basado en factura o nota de débito (opcional)`; `value="Factura (entrada @Model.BaseEntry)"` → `value="Factura o nota de débito (entrada @Model.BaseEntry)"`. En `Views/NotasCreditoCompra/_Form.cshtml`: `Basado en factura de compra (opcional)` → `Basado en factura de compra o nota de débito (opcional)`; `value="Factura de compra (entrada @Model.BaseEntry)"` → `value="Factura de compra o nota de débito (entrada @Model.BaseEntry)"` (comprobar con `grep` el texto actual y usar el que aparezca).

- [ ] **Step 3: Verificación y commit**

```bash
node --check Web.UI/wwwroot/js/numeracionDocumento.js && node --check Web.UI/wwwroot/js/notascredito.js && node --check Web.UI/wwwroot/js/notascreditocompra.js
```

`dotnet build Web.slnx` → 0 errores; `git diff --stat` solo muestra líneas sueltas por archivo.

```bash
git add Web.UI/Controllers/NumeracionDocumentoController.cs Web.UI/Views/NumeracionDocumento/_Form.cshtml Web.UI/wwwroot/js/numeracionDocumento.js Web.UI/wwwroot/js/notascredito.js Web.UI/wwwroot/js/notascreditocompra.js Web.UI/Views/NotasCredito/_Form.cshtml Web.UI/Views/NotasCreditoCompra/_Form.cshtml
git commit -m "feat(notas-debito): numeracion por clave compuesta y notas de credito sobre notas de debito en la Web

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 5 — Verificación

### Task 14: Verificación conjunta (sin commit)

**Files:** ninguno. Los defectos se **reportan** con la salida que falla; no se corrigen aquí.

**Precondición verificable:** Tasks 1-13 commiteadas en ambos repos.

- [ ] **Step 1: API — build y suite completa**

`dotnet test API.sln` (PowerShell, con el `BaseOutputPath` de la cabecera) → 0 errores y todo verde; reportar el total real (línea base 1331 + las nuevas − las eliminadas).

- [ ] **Step 2: Web — árbol commiteado limpio**

En el repo Web: `git worktree add --detach <scratchpad>\wt-check HEAD`; compilar allí con un `BaseOutputPath` distinto → 0 errores y 0 advertencias; `git worktree remove --force <scratchpad>\wt-check`. `node --check` de `notasdebito.js`, `notasdebitocompra.js`, `numeracionDocumento.js`, `notascredito.js`, `notascreditocompra.js`.

- [ ] **Step 3: Auditoría de consumidores de Factura y de numeración**

Dos comprobaciones de que no quedó ninguna lectura sin filtrar:

```bash
cd ../API
grep -rnE "_repo(Factura|FacturaCompra|Header|Encabezado)\w*\.ObtenerTodoAsync|IRepositorioGenerico<Factura(Compra)?, int>" --include=*.cs API.Domain.Core API.Application.Main API.Service.WebApi | grep -v "/bin/\|/obj/"
```

Cada coincidencia debe estar en un dominio que ya filtra por `DocSubType` (facturas, detalles de factura, notas de débito) o que lo ignora a propósito (notas de crédito, que aceptan ambos subtipos). Cualquier otro consumidor (reportes, resúmenes, selectores) se reporta. Y para la numeración:

```bash
grep -rn "ObtenerPorDocumentoAsync\|ObtenerPorCodigoAsync" --include=*.cs ../Web/Web.UI API.Domain.Core API.Service.WebApi | grep -v "/bin/\|/obj/"
```

Los controllers Web de documentos filtran las series por `SubTipoDoc` (los de factura por `"--"`, los de nota por `"ND"`/`"DN"`); reportar cualquier consumidor que mezcle series de objetos con varias filas.

- [ ] **Step 4: Humo de la API**

Si la Task 9 no se ejecutó o su resultado es anterior a algún cambio posterior, repetirla completa.

- [ ] **Step 5: Estado de git**

`git -C API status --porcelain` → solo ` M API.Service.WebApi/appsettings.json`; `git -C Web status --porcelain` → solo los 4 archivos ajenos. Listar los commits nuevos de ambos repos (`git log --oneline` desde el commit previo a este plan: API `a90590e`, Web `071bdc2`).

- [ ] **Step 6: Checklist manual para el usuario (no automatizable aquí)**

Entregar al usuario:

1. Ejecutar `sql/2026-10-17-notas-debito-en-factura.sql` en `API_DB_TEST` con `sqlcmd -f 65001` (si antes ejecutó `sql/2026-10-05-notas-credito.sql`, está bien; el orden entre ambos no importa) y reiniciar la depuración de la API y de la Web. **No** ejecutar `sql/2026-10-04-notas-debito.sql` (fue reemplazado).
2. Como `admin`: en Ventas → Notas de débito, crear una nota **por concepto** (como antes) y otra **por artículos**: elegir "Tipo de nota: Artículos", agregar una línea con artículo (se llena almacén y precio de lista) y guardar; en Artículos → existencias, comprobar que la cantidad bajó; cancelar la nota y comprobar que volvió.
3. Probar una salida mayor a la existencia: debe rechazarse con el mensaje de stock insuficiente y no dejar nota.
4. Crear una nota en base a una factura: por artículos precarga artículo, almacén y cantidades; por concepto precarga solo los conceptos. Cambiar el tipo con líneas cargadas pide confirmación.
5. Compras → Notas de débito de compra: lo mismo, esta vez la nota por artículos **suma** al inventario.
6. Verificar que las notas **no** aparecen en Ventas → Facturas ni en Compras → Facturas de compra, y que sus series ("Manual"/"Primario" de la nota) no aparecen en el selector de series de las facturas.
7. Numeración de documentos: ahora hay filas separadas "Factura" y "Nota Debito Cliente" (y "Factura Compra" / "Nota Debito Prov."); editar la serie por defecto de la nota no cambia la de la factura.
8. Notas de crédito: en el selector de factura base aparecen facturas y notas de débito del socio, rotuladas; acreditar una nota de débito respeta el límite de su total.
9. Con un usuario con `Ver` solo en `NotasDebito`: ve la lista y puede abrir el detalle; para crear una nota por artículos necesita además `Ver` sobre `Articulos` y `Almacenes`.

- [ ] **Step 7: Integración**

No hay commit en este task. Usar `superpowers:finishing-a-development-branch` para presentar las opciones de integración al usuario una vez completadas las tareas anteriores. Sin push hasta aprobación.

---

## Riesgos y notas de cierre

- **Núcleo de facturación:** se toca `Factura`/`FacturaCompra` y sus dominios. El filtro por `DocSubType` es la barrera: cualquier consulta nueva sobre esas tablas debe acordarse de filtrar (la auditoría del Task 14 revisa las existentes).
- **Migración sin copia de datos:** `sql/2026-10-17-notas-debito-en-factura.sql` aborta si las tablas `NotaDebito*` tienen filas. Se asume que no hay notas de débito guardadas en ninguna base (el script anterior no se aplicó en `API_DB_TEST`). Si las hubiera, se agrega la copia como cambio aparte.
- **Facturas ahora validan artículo y almacén por línea en el dominio** (el esquema ya no lo exige porque las notas por concepto no los llevan). Un cliente de la API que enviara facturas sin artículo recibe un mensaje claro en vez de un error de base.
- **Kardex:** el movimiento de una nota por artículos usa `TipoDoc` `'6'`/`'13'` (el de la factura) y `DocEntry` = `Entry` de la nota (único porque comparten tabla); en el historial de movimientos aparece con el alias "Factura". Distinguirlo exige un campo nuevo en el movimiento y queda fuera de alcance.
- **Costo de las notas de proveedor por artículos:** las entradas usan el precio de la línea como costo unitario, igual que `FacturaCompra`; la reversa de una entrada se registra como salida al costo original (limitación de valuación ya conocida del asiento de inventario).
- **Notas de crédito sobre notas de débito por artículos:** la nota de crédito sigue siendo monetaria; no devuelve stock.
- **Totales de la nota de débito:** siguen viniendo del cliente (decisión previa); las de crédito se recalculan en el servidor.
- **Numeración:** la clave compuesta `"CodigoObj:SubTipoDoc"` corrige también la ambigüedad preexistente del objeto 1 (`C`/`P`) en la pantalla de numeración. Cualquier otro lector de `NumeracionDocumento` por `CodigoObj` solo conserva el comportamiento de "primera fila".
- **Despliegue:** ejecutar `sql/2026-10-17-notas-debito-en-factura.sql` (con `-f 65001`) en cada base antes de arrancar la API; los módulos `NotasDebito`/`NotasDebitoCompra` conservan sus permisos asignados.
