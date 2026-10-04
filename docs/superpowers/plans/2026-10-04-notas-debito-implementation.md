# Notas de débito (cliente y proveedor) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar los documentos Nota de Débito Cliente (objeto 7) y Nota de Débito Proveedor (objeto 14), con tablas, numeración, API, permisos y pantallas Web, sin efecto en inventario.

**Architecture:** Dos pilas de documento independientes clonadas del patrón Factura/FacturaCompra (entidad EF → repo genérico → dominio → aplicación → controller, y en la Web cliente de API → controller MVC → vistas/JS) sin inventario ni líneas de artículo: las líneas son conceptos libres y quedan fijas al crear el documento. La referencia a la factura base es solo informativa y vive en el encabezado.

**Tech Stack:** .NET 7 API (EF Core 7, AutoMapper, xUnit + Moq), .NET 8 MVC Web (jQuery, DataTables, Bootstrap, SweetAlert2), SQL Server.

**Spec:** `docs/superpowers/specs/2026-10-04-notas-debito-design.md` (repo API). Las diferencias que el plan introduce respecto del spec están en la sección "Ajustes respecto del spec" y el spec se enmienda en el Task 0.

## Rutas y estado de partida

- API: `C:\Users\migue\source\repos\angelm0508\API`, rama `desarrollo`. Línea base de pruebas: **967** pasando (`dotnet test API.sln`).
- Web: `C:\Users\migue\source\repos\angelm0508\Web`, rama `main`.
- Build/test (siempre con `BaseOutputPath` en el scratchpad para no ensuciar el repo):
  - API: `dotnet test API.sln -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apitest\"`
  - Web: `dotnet build Web.slnx -p:BaseOutputPath="C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\webbuild\"`

## Global Constraints

Cada tarea las incluye implícitamente.

- **Sin inventario:** una nota de débito nunca llama a `IInventarioAsientoService` ni toca existencias; sus líneas no tienen `CodArticulo` ni `CodAlmacen`.
- **Tipos de objeto:** Nota de Débito Cliente = `7`, Nota de Débito Proveedor = `14`; `NumeracionDocumento.SubTipoDoc = '--'`.
- **Tablas:** `NotaDebito`, `NotaDebitoDetalle`, `NotaDebitoCompra`, `NotaDebitoCompraDetalle`.
- **Módulos de permisos:** `NotasDebito` (grupo `Ventas`, nombre `Notas de débito`) y `NotasDebitoCompra` (grupo `Compras`, nombre `Notas de débito de compra`). Sin filas en `UsuarioPermiso` un usuario no tiene acceso; `SuperUsuario` se salta la matriz.
- **Factura base:** `BaseTipo` ∈ {`-1`, `6` (cliente) / `13` (proveedor)}; solo referencia, no modifica ni cierra la factura; debe existir, no estar cancelada y ser del mismo socio.
- **Cancelar un documento** (`PUT` con `Cancelado == "S"`) exige además `Accion.Eliminar` (mismo chequeo que Factura vía `PermisoUsuario.Tiene` en API y `User.TienePermiso` en Web).
- **Eliminar** exige haber cancelado antes; borra líneas y encabezado en una transacción.
- **Mensajes de error en español**, tal como aparecen en los bloques de código del plan (los tests los verifican literalmente).
- **Git:** NUNCA `git add -A` ni `git add .`; siempre rutas explícitas. `API.Service.WebApi/appsettings.json` puede aparecer modificado localmente con un connection string real: NUNCA commitearlo. La Web tiene trabajo ajeno sin commitear (`Web.ApiClient/Dtos/Articulo/ArticuloCrearDTO.cs`, `Web.ApiClient/Dtos/Respuesta.cs`, `Web.UI/Controllers/ArticulosController.cs`, `Web.UI/wwwroot/js/articulos.js`): jamás se stagean, commitean ni modifican. Sin push hasta aprobación final del usuario.
- **Trailer de commit exacto:** `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- **Estilo:** mismas convenciones que el código vecino; se conserva el fin de línea (CRLF/LF) de cada archivo existente que se edita; los archivos nuevos en LF.

## Ajustes respecto del spec

Descubiertos al leer el código real mientras se escribía el plan; el Task 0 enmienda el spec.

1. **Las líneas son inmutables tras crear el documento** (igual que Factura: `FacturaDetalleDomain` rechaza insertar/modificar/eliminar líneas de un documento existente). Por eso **no hay** `NotaDebitoDetalleDomain/Application/Controller`, ni endpoints ni acciones Web de crear/editar/eliminar línea sobre un documento guardado. Las líneas se leen con `GET api/NotaDebito/{id}/Lineas`. El detalle `NotaDebitoDetalle` solo necesita su entidad y su repo.
2. **Sin FK de `Entry` al encabezado** en las tablas de detalle (igual que `FacturaDetalle`); el dominio borra las líneas antes del encabezado. Sí hay FK a `Impuesto`.
3. **`DocAlias` ≤ 20 caracteres** (límite de la columna): `Nota Debito Cliente` y `Nota Debito Prov.`.
4. **Series:** el script de migración toma las siguientes series libres de `NumeracionDocumentoDet` (en una base con series 1-31 resultan 32-35); el script de creación desde cero usa 32-35 fijas.
5. **Edición:** el documento base se muestra como "Factura (entrada N)" porque resolver el número de la factura exigiría permiso `Ver` sobre Facturas.
6. **El DTO de línea no lleva `Entry`** (lo asigna el servidor) y la descripción es obligatoria.

## File Structure

**API (repo `API`):**
- Crear `sql/2026-10-04-notas-debito.sql` (migración idempotente); modificar `sql/crear-api-db-test-completo.sql`.
- Crear entidades en `API.Domain.Entity/Models/`: `NotaDebito.cs`, `NotaDebitoDetalle.cs`, `NotaDebitoCompra.cs`, `NotaDebitoCompraDetalle.cs`; modificar `ApiDbTestContext.cs`, `SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs`, `Impuesto.cs`.
- Crear repos en `API.Infraestructure.Repository/`: `NotaDebitoRepositorio.cs`, `NotaDebitoDetalleRepositorio.cs`, `NotaDebitoCompraRepositorio.cs`, `NotaDebitoCompraDetalleRepositorio.cs`.
- Crear DTOs en `API.Application.DTO/notaDebito/` y `API.Application.DTO/notaDebitoCompra/`.
- Crear interfaces: `API.Domain.Interface/{INotaDebitoDomain,INotaDebitoCompraDomain,FacturaBase}.cs`, `API.Application.Interface/{INotaDebitoApplication,INotaDebitoCompraApplication}.cs`.
- Crear `API.Domain.Core/{NotaDebitoDomain,NotaDebitoCompraDomain}.cs`; `API.Application.Main/{NotaDebitoApplication,NotaDebitoCompraApplication}.cs`; `API.Service.WebApi/Controllers/{NotaDebitoController,NotaDebitoCompraController}.cs`.
- Modificar `API.Transversal.Mapper/PerfilMapeo.cs` y `API.Service.WebApi/Startup.cs`.
- Pruebas en `API.Service.WebApi.Tests/{Domain,Application,Controllers}/`.

**Web (repo `Web`):**
- Crear `Web.ApiClient/Dtos/NotaDebito/*.cs`, `Web.ApiClient/Dtos/NotaDebitoCompra/*.cs`, `Web.ApiClient/Clientes/{INotaDebitoApiClient,NotaDebitoApiClient,INotaDebitoCompraApiClient,NotaDebitoCompraApiClient}.cs`; modificar `Web.UI/Program.cs`.
- Crear `Web.UI/Controllers/{NotasDebitoController,NotasDebitoCompraController}.cs`, `Web.UI/Views/NotasDebito/{Index,_Form}.cshtml`, `Web.UI/Views/NotasDebitoCompra/{Index,_Form}.cshtml`, `Web.UI/wwwroot/js/{notasdebito,notasdebitocompra}.js`; modificar `Web.UI/Views/Shared/_Layout.cshtml`.

---

### Task 0: Enmendar el spec con los ajustes del plan

**Files:**
- Modify: `docs/superpowers/specs/2026-10-04-notas-debito-design.md`

**Precondición verificable:** `git -C API log --oneline -1 -- docs/superpowers/specs/2026-10-04-notas-debito-design.md` muestra el commit del spec y `git -C API status --porcelain` solo lista `appsettings.json` (y nada del spec).

- [ ] **Step 1: Editar el spec**

Aplicar estos cambios exactos (el resto del spec no se toca):

1. En "Decisiones tomadas", agregar el punto 6: `6. **Líneas inmutables tras crear el documento** (igual que Factura): no hay endpoints ni acciones para crear/editar/eliminar líneas de un documento guardado; las líneas se leen con `GET api/NotaDebito/{id}/Lineas`.`
2. En §1 "Tablas", reemplazar la frase "FK al encabezado por `Entry` (sin cascade; el dominio borra las líneas)." por "Sin FK de `Entry` al encabezado (igual que `FacturaDetalle`); el dominio borra las líneas antes del encabezado."
3. En §1 "Numeración (seed)": cambiar `DocAlias` a `Nota Debito Cliente` y `Nota Debito Prov.`; y debajo de las dos tablas agregar: "Los números de serie 32-35 son los de una base nueva; el script de migración toma las siguientes series libres de `NumeracionDocumentoDet`."
4. En §2 "Dominio": eliminar la viñeta de `...DetalleDomain` y reemplazar la mención "`NotaDebitoDomain` / `NotaDebitoCompraDomain` + `...DetalleDomain`" por "`NotaDebitoDomain` / `NotaDebitoCompraDomain`"; agregar la operación `ObtenerLineasAsync(id)`.
5. En §2 "Aplicación" y "Controllers": quitar `...DetalleApplication`, `NotaDebitoDetalleController` y `NotaDebitoCompraDetalleController`; agregar la ruta `GET {id:int}/Lineas`.
6. En §3 "Web": quitar `...DetalleApiClient` y las acciones `CrearLinea/EditarLinea/EliminarLinea`; en el formulario de edición el documento base se muestra como "Factura (entrada N)".

- [ ] **Step 2: Commit**

```bash
git -C C:/Users/migue/source/repos/angelm0508/API add docs/superpowers/specs/2026-10-04-notas-debito-design.md
git -C C:/Users/migue/source/repos/angelm0508/API commit -m "docs(spec): ajusta notas de debito a lineas inmutables y numeracion dinamica

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 1 — Datos y entidades (API)

### Task 1: Script SQL y catálogo de módulos

**Files:**
- Create: `sql/2026-10-04-notas-debito.sql`
- Modify: `sql/crear-api-db-test-completo.sql`

**Interfaces:**
- Produces: tablas `NotaDebito`, `NotaDebitoDetalle`, `NotaDebitoCompra`, `NotaDebitoCompraDetalle`; filas de numeración para `CodigoObj` `7` y `14`; módulos `NotasDebito` y `NotasDebitoCompra` en `Modulo`. Las constraints se llaman exactamente: `pk_nota_debito`, `fk_nota_debito_serie`, `fk_nota_debito_sn`, `fk_nota_debito_moneda`, `pk_nota_debito_det`, `fk_nota_debito_det_cod_impuesto`, y los equivalentes `pk_nota_debito_compra`, `fk_nota_debito_compra_serie`, `fk_nota_debito_compra_sn`, `fk_nota_debito_compra_moneda`, `pk_nota_debito_compra_det`, `fk_nota_debito_compra_det_cod_impuesto` (el mapeo EF del Task 2 las referencia).

**Precondición verificable:** `sqlcmd` conecta a `localhost` con el usuario `sa` cuya clave está en `API.Service.WebApi/appsettings.json` (campo `Password=` de la cadena `API_DB`); la base `API_DB_TEST` existe y NO se modifica en este task (la verificación usa una base temporal).

- [ ] **Step 1: Crear `sql/2026-10-04-notas-debito.sql`**

```sql
-- Notas de debito de cliente (objeto 7) y de proveedor (objeto 14). Sin inventario.
-- Idempotente: cada objeto se crea solo si no existe. Las series nuevas toman los siguientes
-- numeros libres de NumeracionDocumentoDet (en una base con series 1-31: 32-35).
SET NOCOUNT ON;

-- ===== NotaDebito (cliente, ObjType 7) =====
IF OBJECT_ID('dbo.NotaDebito', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebito (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaDebito_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_NumManual DEFAULT ('N') CONSTRAINT CK_NotaDebito_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaDebito_Imprimido CHECK (Imprimido IN ('S','N')),
        EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaDebito_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaDebito_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaDebito_TipoObjeto DEFAULT ('7') CONSTRAINT CK_NotaDebito_TipoObjeto CHECK (TipoObjeto IN ('7')),
        FechaDoc       datetime      NOT NULL,
        FechaEmision   datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        BaseTipo       int           NULL CONSTRAINT DF_NotaDebito_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaDebito_BaseTipo CHECK (BaseTipo IN (-1, 6)),
        BaseEntry      int           NULL,
        PrctjeImpuesto decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalBruto     decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_nota_debito PRIMARY KEY (Entry),
        CONSTRAINT fk_nota_debito_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_nota_debito_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_nota_debito_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
    );
END

IF OBJECT_ID('dbo.NotaDebitoDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebitoDetalle (
        Entry          int           NOT NULL,
        NoLinea        int           NOT NULL,
        Descripcion    nvarchar(200) NOT NULL,
        Cantidad       decimal(19,6) NULL,
        Precio         decimal(19,6) NULL,
        PrecioBruto    decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        CodigoImpuesto nvarchar(8)   NOT NULL,
        Impuesto       decimal(19,6) NULL,
        TotalLinea     decimal(19,6) NULL,
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaDebitoDet_TipoObjeto DEFAULT ('7') CONSTRAINT CK_NotaDebitoDet_TipoObjeto CHECK (TipoObjeto IN ('7')),
        CONSTRAINT pk_nota_debito_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_nota_debito_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
    );
END

-- ===== NotaDebitoCompra (proveedor, ObjType 14) =====
IF OBJECT_ID('dbo.NotaDebitoCompra', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebitoCompra (
        Entry          int identity(1,1) NOT NULL,
        NumDoc         int           NOT NULL,
        Serie          int           NOT NULL,
        Cancelado      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_Cancelado DEFAULT ('N') CONSTRAINT CK_NotaDebitoCompra_Cancelado CHECK (Cancelado IN ('S','N')),
        NumManual      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_NumManual DEFAULT ('N') CONSTRAINT CK_NotaDebitoCompra_NumManual CHECK (NumManual IN ('S','N')),
        Imprimido      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_Imprimido DEFAULT ('N') CONSTRAINT CK_NotaDebitoCompra_Imprimido CHECK (Imprimido IN ('S','N')),
        EstadoDoc      nvarchar(1)   NULL CONSTRAINT DF_NotaDebitoCompra_EstadoDoc DEFAULT ('A') CONSTRAINT CK_NotaDebitoCompra_EstadoDoc CHECK (EstadoDoc IN ('A','C')),
        TipoObjeto     nvarchar(11)  NULL CONSTRAINT DF_NotaDebitoCompra_TipoObjeto DEFAULT ('14') CONSTRAINT CK_NotaDebitoCompra_TipoObjeto CHECK (TipoObjeto IN ('14')),
        FechaDoc       datetime      NOT NULL,
        FechaEmision   datetime      NOT NULL,
        FechaCancelado datetime      NULL,
        CodigoSN       nvarchar(15)  NOT NULL,
        NombreSN       nvarchar(200) NULL,
        Direccion      nvarchar(254) NULL,
        MonedaDoc      nvarchar(3)   NOT NULL,
        BaseTipo       int           NULL CONSTRAINT DF_NotaDebitoCompra_BaseTipo DEFAULT (-1) CONSTRAINT CK_NotaDebitoCompra_BaseTipo CHECK (BaseTipo IN (-1, 13)),
        BaseEntry      int           NULL,
        PrctjeImpuesto decimal(19,6) NULL,
        TotalImp       decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        TotalDesc      decimal(19,6) NULL,
        TotalBruto     decimal(19,6) NULL,
        TotalDoc       decimal(19,6) NULL,
        Comentario     nvarchar(254) NULL,
        CONSTRAINT pk_nota_debito_compra PRIMARY KEY (Entry),
        CONSTRAINT fk_nota_debito_compra_serie  FOREIGN KEY (Serie)     REFERENCES dbo.NumeracionDocumentoDet(Serie),
        CONSTRAINT fk_nota_debito_compra_sn     FOREIGN KEY (CodigoSN)  REFERENCES dbo.SocioNegocio(Codigo),
        CONSTRAINT fk_nota_debito_compra_moneda FOREIGN KEY (MonedaDoc) REFERENCES dbo.Moneda(Codigo)
    );
END

IF OBJECT_ID('dbo.NotaDebitoCompraDetalle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.NotaDebitoCompraDetalle (
        Entry          int           NOT NULL,
        NoLinea        int           NOT NULL,
        Descripcion    nvarchar(200) NOT NULL,
        Cantidad       decimal(19,6) NULL,
        Precio         decimal(19,6) NULL,
        PrecioBruto    decimal(19,6) NULL,
        PrctjeDesc     decimal(19,6) NULL,
        CodigoImpuesto nvarchar(8)   NOT NULL,
        Impuesto       decimal(19,6) NULL,
        TotalLinea     decimal(19,6) NULL,
        TipoObjeto     nvarchar(20)  NULL CONSTRAINT DF_NotaDebitoCompraDet_TipoObjeto DEFAULT ('14') CONSTRAINT CK_NotaDebitoCompraDet_TipoObjeto CHECK (TipoObjeto IN ('14')),
        CONSTRAINT pk_nota_debito_compra_det PRIMARY KEY (Entry, NoLinea),
        CONSTRAINT fk_nota_debito_compra_det_cod_impuesto FOREIGN KEY (CodigoImpuesto) REFERENCES dbo.Impuesto(Codigo)
    );
END

-- ===== Numeracion: series Manual y Primario por documento =====
DECLARE @manualND int, @primariaND int, @manualDN int, @primariaDN int;

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '7')
BEGIN
    SET @manualND = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @primariaND = @manualND + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('7', @manualND, 'Manual', 'N', '--', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('7', @primariaND, 'Primario', 'N', '--', 'D', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '7' AND SubTipoDoc = '--')
        INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
        VALUES ('7', @primariaND, 'Nota Debito Cliente', '--');
END

IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumentoDet WHERE CodigoObj = '14')
BEGIN
    SET @manualDN = (SELECT ISNULL(MAX(Serie), 0) + 1 FROM dbo.NumeracionDocumentoDet);
    SET @primariaDN = @manualDN + 1;
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
    VALUES ('14', @manualDN, 'Manual', 'N', '--', 'D', 'S');
    INSERT INTO dbo.NumeracionDocumentoDet (CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
    VALUES ('14', @primariaDN, 'Primario', 'N', '--', 'D', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.NumeracionDocumento WHERE CodigoObj = '14' AND SubTipoDoc = '--')
        INSERT INTO dbo.NumeracionDocumento (CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
        VALUES ('14', @primariaDN, 'Nota Debito Prov.', '--');
END

-- ===== Catalogo de permisos =====
MERGE dbo.Modulo AS target
USING (VALUES
    ('NotasDebito', 'Notas de débito', 'Ventas'),
    ('NotasDebitoCompra', 'Notas de débito de compra', 'Compras')
) AS source (Codigo, Nombre, Grupo)
ON target.Codigo = source.Codigo
WHEN NOT MATCHED THEN
    INSERT (Codigo, Nombre, Grupo) VALUES (source.Codigo, source.Nombre, source.Grupo);

SELECT name FROM sys.tables WHERE name IN ('NotaDebito','NotaDebitoDetalle','NotaDebitoCompra','NotaDebitoCompraDetalle') ORDER BY name;
SELECT CodigoObj, Serie, NombreSerie FROM dbo.NumeracionDocumentoDet WHERE CodigoObj IN ('7','14') ORDER BY Serie;
SELECT CodigoObj, SerieDfct, DocAlias FROM dbo.NumeracionDocumento WHERE CodigoObj IN ('7','14');
SELECT Codigo, Grupo FROM dbo.Modulo WHERE Codigo IN ('NotasDebito','NotasDebitoCompra');
```

- [ ] **Step 2: Agregar lo mismo al script de creación desde cero**

En `sql/crear-api-db-test-completo.sql`, después de las tablas `FacturaCompra`/`FacturaCompraDetalle` y antes del bloque "INV-4" (busca el comentario `-- INV-4: Entrada y Salida de Mercancias`), insertar las 4 sentencias `CREATE TABLE` del Step 1 **sin** los envoltorios `IF OBJECT_ID(...) IS NULL BEGIN ... END` (el script completo corre sobre una base vacía, como el resto de sus tablas). Para los seeds de numeración, en la sección de `NumeracionDocumento` (después del insert de `CodigoObj 60`) agregar:

```sql
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(7, 33, 'Nota Debito Cliente', '--');
INSERT INTO NumeracionDocumento(CodigoObj, SerieDfct, DocAlias, SubTipoDoc)
VALUES(14, 35, 'Nota Debito Prov.', '--');
```

y en la sección de `NumeracionDocumentoDet` (después del insert de `Serie 31`):

```sql
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(7, 32,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(7, 33,'Primario', 'N', '--', 'D', 1, 1);
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, Manual)
VALUES(14, 34,'Manual', 'N', '--', 'D', 'S');
INSERT INTO NumeracionDocumentoDet(CodigoObj, Serie, NombreSerie, Bloqueado, SubTipoDoc, TipoSerie, IniNumero, SigNumero)
VALUES(14, 35,'Primario', 'N', '--', 'D', 1, 1);
```

Importante: los inserts de `NumeracionDocumentoDet` del documento van **antes** de las `CREATE TABLE NotaDebito*` (las FK de `Serie` los necesitan, y la tabla `NumeracionDocumentoDet` ya existe en ese punto del script). Y al final, en el `MERGE Modulo`, agregar a la lista `VALUES` las dos filas `('NotasDebito', 'Notas de débito', 'Ventas')` y `('NotasDebitoCompra', 'Notas de débito de compra', 'Compras')` (con la coma que corresponda).

- [ ] **Step 3: Verificar en una base temporal (NO tocar `API_DB_TEST`)**

PowerShell:

```powershell
cd C:\Users\migue\source\repos\angelm0508\API
$pw = ([regex]::Match((Get-Content API.Service.WebApi\appsettings.json -Raw), 'Password=([^;]*)')).Groups[1].Value
$scratch = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
# (a) script completo en base nueva
(Get-Content sql\crear-api-db-test-completo.sql -Raw) -replace 'API_DB_TEST','API_DB_PLANCHECK' | Set-Content "$scratch\plancheck.sql" -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$scratch\plancheck.sql"
sqlcmd -S localhost -U sa -P $pw -C -d API_DB_PLANCHECK -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'NotaDebito%'; SELECT Serie FROM NumeracionDocumentoDet WHERE CodigoObj IN ('7','14') ORDER BY Serie; SELECT COUNT(*) FROM Modulo WHERE Codigo LIKE 'NotasDebito%'"
# (b) migracion sobre una base que NO tiene las tablas: base de la parte (a) sin ellas
sqlcmd -S localhost -U sa -P $pw -C -b -d API_DB_PLANCHECK -Q "DROP TABLE NotaDebitoDetalle, NotaDebito, NotaDebitoCompraDetalle, NotaDebitoCompra; DELETE FROM NumeracionDocumento WHERE CodigoObj IN ('7','14'); DELETE FROM NumeracionDocumentoDet WHERE CodigoObj IN ('7','14'); DELETE FROM Modulo WHERE Codigo LIKE 'NotasDebito%'"
sqlcmd -S localhost -U sa -P $pw -C -b -d API_DB_PLANCHECK -i sql\2026-10-04-notas-debito.sql
# (c) idempotencia: correr de nuevo no debe fallar ni duplicar
sqlcmd -S localhost -U sa -P $pw -C -b -d API_DB_PLANCHECK -i sql\2026-10-04-notas-debito.sql
sqlcmd -S localhost -U sa -P $pw -C -d API_DB_PLANCHECK -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM NumeracionDocumentoDet WHERE CodigoObj IN ('7','14')"
# (d) limpieza
sqlcmd -S localhost -U sa -P $pw -C -b -Q "ALTER DATABASE API_DB_PLANCHECK SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE API_DB_PLANCHECK"
```

Esperado: (a) 4 tablas, series `32,33,34,35`, 2 módulos nuevos, exit 0; (b) tras reaplicar la migración las series nuevas son `32..35` (siguientes libres tras 31); (c) segunda corrida sin errores y el conteo de series sigue en 4. Si algo difiere, corregir el script antes de seguir. Confirmar al final que `API_DB_PLANCHECK` ya no existe y que `API_DB_TEST` no cambió (`SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'NotaDebito%'` en `API_DB_TEST` devuelve 0).

- [ ] **Step 4: Commit**

```bash
git add sql/2026-10-04-notas-debito.sql sql/crear-api-db-test-completo.sql
git commit -m "feat(db): tablas, numeracion y modulos de notas de debito (cliente y proveedor)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Entidades EF, mapeo del contexto y repos

**Files:**
- Create: `API.Domain.Entity/Models/NotaDebito.cs`, `NotaDebitoDetalle.cs`, `NotaDebitoCompra.cs`, `NotaDebitoCompraDetalle.cs`
- Modify: `API.Domain.Entity/Models/ApiDbTestContext.cs`, `SocioNegocio.cs`, `Monedum.cs`, `NumeracionDocumentoDet.cs`, `Impuesto.cs`
- Create: `API.Infraestructure.Repository/NotaDebitoRepositorio.cs`, `NotaDebitoDetalleRepositorio.cs`, `NotaDebitoCompraRepositorio.cs`, `NotaDebitoCompraDetalleRepositorio.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaDebitoModeloTests.cs`

**Interfaces:**
- Consumes: constraints del Task 1.
- Produces: entidades `NotaDebito`/`NotaDebitoCompra` (clave `Entry`) y `NotaDebitoDetalle`/`NotaDebitoCompraDetalle` (clave `(Entry, NoLinea)`), DbSets `NotaDebitos`, `NotaDebitoDetalles`, `NotaDebitoCompras`, `NotaDebitoCompraDetalles`; repos `NotaDebitoRepositorio : RepositorioGenericoEfCore<NotaDebito,int>`, `NotaDebitoDetalleRepositorio : RepositorioGenericoEfCore<NotaDebitoDetalle,(int Entry,int NoLinea)>` y los de compra.

**Precondición verificable:** `git status --porcelain` sin cambios en `API.Domain.Entity`; `dotnet build API.sln` pasa (0 errores) antes de empezar.

- [ ] **Step 1: Escribir el test del modelo (falla: las entidades no existen)**

Crear `API.Service.WebApi.Tests/Domain/NotaDebitoModeloTests.cs`:

```csharp
using API.Domain.Entity.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace API.Service.WebApi.Tests.Domain
{
    /// <summary>
    /// Verifica el mapeo EF de las notas de debito sin base de datos: claves, tablas y
    /// longitudes tienen que coincidir con sql/2026-10-04-notas-debito.sql.
    /// </summary>
    public class NotaDebitoModeloTests
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
        [InlineData(typeof(NotaDebito), "NotaDebito", "Entry", "7")]
        [InlineData(typeof(NotaDebitoCompra), "NotaDebitoCompra", "Entry", "14")]
        public void Encabezado_MapeaTablaClaveYDefaultDeTipoObjeto(Type tipo, string tabla, string clave, string tipoObjeto)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            Assert.Equal(tabla, entidad.GetTableName());
            Assert.Equal(new[] { clave }, entidad.FindPrimaryKey()!.Properties.Select(p => p.Name));
            Assert.Equal($"('{tipoObjeto}')", entidad.FindProperty("TipoObjeto")!.GetDefaultValueSql());
            Assert.Equal("CodigoSN", entidad.FindProperty("CodigoSn")!.GetColumnName());
            Assert.Equal("NombreSN", entidad.FindProperty("NombreSn")!.GetColumnName());
            Assert.Equal(200, entidad.FindProperty("NombreSn")!.GetMaxLength());
            Assert.DoesNotContain(entidad.GetProperties(), p => p.Name == "EstadoInv");
        }

        [Theory]
        [InlineData(typeof(NotaDebitoDetalle), "NotaDebitoDetalle", "7")]
        [InlineData(typeof(NotaDebitoCompraDetalle), "NotaDebitoCompraDetalle", "14")]
        public void Detalle_MapeaClaveCompuestaSinArticuloNiAlmacen(Type tipo, string tabla, string tipoObjeto)
        {
            var entidad = Modelo().FindEntityType(tipo)!;

            Assert.Equal(tabla, entidad.GetTableName());
            Assert.Equal(new[] { "Entry", "NoLinea" }, entidad.FindPrimaryKey()!.Properties.Select(p => p.Name));
            Assert.Equal(200, entidad.FindProperty("Descripcion")!.GetMaxLength());
            Assert.False(entidad.FindProperty("Descripcion")!.IsNullable);
            Assert.False(entidad.FindProperty("CodigoImpuesto")!.IsNullable);
            Assert.Equal($"('{tipoObjeto}')", entidad.FindProperty("TipoObjeto")!.GetDefaultValueSql());
            Assert.DoesNotContain(entidad.GetProperties(), p => p.Name is "CodArticulo" or "CodAlmacen");
        }
    }
}
```

Nota: el test no abre conexión (solo construye el modelo). Si el constructor de `ApiDbTestContext` no acepta `DbContextOptions<ApiDbTestContext>`, usar el constructor que tenga el contexto real (ver el inicio de `ApiDbTestContext.cs`).

- [ ] **Step 2: Ejecutar y ver el fallo**

`dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoModeloTests"` → error de compilación (tipos no definidos).

- [ ] **Step 3: Crear las 4 entidades**

`API.Domain.Entity/Models/NotaDebito.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace API.Domain.Entity.Models;

public partial class NotaDebito
{
    public int Entry { get; set; }

    public int NumDoc { get; set; }

    public int Serie { get; set; }

    public string? Cancelado { get; set; }

    public string? NumManual { get; set; }

    public string? Imprimido { get; set; }

    public string? EstadoDoc { get; set; }

    public string? TipoObjeto { get; set; }

    public DateTime? FechaDoc { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaCancelado { get; set; }

    public string? CodigoSn { get; set; }

    public string? NombreSn { get; set; }

    public string? Direccion { get; set; }

    public string? MonedaDoc { get; set; }

    public int? BaseTipo { get; set; }

    public int? BaseEntry { get; set; }

    public decimal? PrctjeImpuesto { get; set; }

    public decimal? TotalImp { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public decimal? TotalDesc { get; set; }

    public decimal? TotalBruto { get; set; }

    public decimal? TotalDoc { get; set; }

    public string? Comentario { get; set; }

    public virtual SocioNegocio? CodigoSnNavigation { get; set; }

    public virtual Monedum? MonedaDocNavigation { get; set; }

    public virtual NumeracionDocumentoDet SerieNavigation { get; set; } = null!;
}
```

`API.Domain.Entity/Models/NotaDebitoDetalle.cs`:

```csharp
namespace API.Domain.Entity.Models;

public partial class NotaDebitoDetalle
{
    public int Entry { get; set; }

    public int NoLinea { get; set; }

    public string Descripcion { get; set; } = null!;

    public decimal? Cantidad { get; set; }

    public decimal? Precio { get; set; }

    public decimal? PrecioBruto { get; set; }

    public decimal? PrctjeDesc { get; set; }

    public string CodigoImpuesto { get; set; } = null!;

    public decimal? Impuesto { get; set; }

    public decimal? TotalLinea { get; set; }

    public string? TipoObjeto { get; set; }

    public virtual Impuesto CodigoImpuestoNavigation { get; set; } = null!;
}
```

`NotaDebitoCompra.cs` y `NotaDebitoCompraDetalle.cs`: idénticos a los dos anteriores cambiando únicamente el nombre de la clase (`NotaDebitoCompra`, `NotaDebitoCompraDetalle`). Las propiedades de navegación mantienen sus tipos.

- [ ] **Step 4: Colecciones de navegación en las entidades existentes**

Agregar una línea al final de la lista de colecciones `ICollection` de cada archivo (junto a las de `Facturas`/`FacturaCompras`), respetando su fin de línea:

- `SocioNegocio.cs`: `public virtual ICollection<NotaDebito> NotaDebitos { get; set; } = new List<NotaDebito>();` y `public virtual ICollection<NotaDebitoCompra> NotaDebitoCompras { get; set; } = new List<NotaDebitoCompra>();`
- `Monedum.cs`: las mismas dos líneas.
- `NumeracionDocumentoDet.cs`: las mismas dos líneas.
- `Impuesto.cs`: `public virtual ICollection<NotaDebitoDetalle> NotaDebitoDetalles { get; set; } = new List<NotaDebitoDetalle>();` y `public virtual ICollection<NotaDebitoCompraDetalle> NotaDebitoCompraDetalles { get; set; } = new List<NotaDebitoCompraDetalle>();`

- [ ] **Step 5: DbSets y mapeo en `ApiDbTestContext.cs`**

Después de la propiedad `DbSet<FacturaCompraDetalle> FacturaCompraDetalles` agregar:

```csharp
    public virtual DbSet<NotaDebito> NotaDebitos { get; set; }

    public virtual DbSet<NotaDebitoDetalle> NotaDebitoDetalles { get; set; }

    public virtual DbSet<NotaDebitoCompra> NotaDebitoCompras { get; set; }

    public virtual DbSet<NotaDebitoCompraDetalle> NotaDebitoCompraDetalles { get; set; }
```

Y dentro de `OnModelCreating`, inmediatamente después del bloque `modelBuilder.Entity<FacturaCompraDetalle>(...)`, agregar los cuatro bloques:

```csharp
        modelBuilder.Entity<NotaDebito>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_nota_debito");

            entity.ToTable("NotaDebito");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Direccion).HasMaxLength(254);
            entity.Property(e => e.EstadoDoc)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.Imprimido)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.MonedaDoc).HasMaxLength(3);
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeImpuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('7')");
            entity.Property(e => e.TotalBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalImp).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany(p => p.NotaDebitos)
                .HasForeignKey(d => d.CodigoSn)
                .HasConstraintName("fk_nota_debito_sn");

            entity.HasOne(d => d.MonedaDocNavigation).WithMany(p => p.NotaDebitos)
                .HasForeignKey(d => d.MonedaDoc)
                .HasConstraintName("fk_nota_debito_moneda");

            entity.HasOne(d => d.SerieNavigation).WithMany(p => p.NotaDebitos)
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_nota_debito_serie");
        });

        modelBuilder.Entity<NotaDebitoDetalle>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_nota_debito_det");

            entity.ToTable("NotaDebitoDetalle");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CodigoImpuesto).HasMaxLength(8);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Impuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Precio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrecioBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(20)
                .HasDefaultValueSql("('7')");
            entity.Property(e => e.TotalLinea).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoImpuestoNavigation).WithMany(p => p.NotaDebitoDetalles)
                .HasForeignKey(d => d.CodigoImpuesto)
                .HasConstraintName("fk_nota_debito_det_cod_impuesto");
        });

        modelBuilder.Entity<NotaDebitoCompra>(entity =>
        {
            entity.HasKey(e => e.Entry).HasName("pk_nota_debito_compra");

            entity.ToTable("NotaDebitoCompra");

            entity.Property(e => e.BaseTipo).HasDefaultValueSql("((-1))");
            entity.Property(e => e.Cancelado)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.CodigoSn)
                .HasMaxLength(15)
                .HasColumnName("CodigoSN");
            entity.Property(e => e.Comentario).HasMaxLength(254);
            entity.Property(e => e.Direccion).HasMaxLength(254);
            entity.Property(e => e.EstadoDoc)
                .HasMaxLength(1)
                .HasDefaultValueSql("('A')");
            entity.Property(e => e.FechaCancelado).HasColumnType("datetime");
            entity.Property(e => e.FechaDoc).HasColumnType("datetime");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.Imprimido)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.MonedaDoc).HasMaxLength(3);
            entity.Property(e => e.NombreSn)
                .HasMaxLength(200)
                .HasColumnName("NombreSN");
            entity.Property(e => e.NumManual)
                .HasMaxLength(1)
                .HasDefaultValueSql("('N')");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeImpuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(11)
                .HasDefaultValueSql("('14')");
            entity.Property(e => e.TotalBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalDoc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TotalImp).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoSnNavigation).WithMany(p => p.NotaDebitoCompras)
                .HasForeignKey(d => d.CodigoSn)
                .HasConstraintName("fk_nota_debito_compra_sn");

            entity.HasOne(d => d.MonedaDocNavigation).WithMany(p => p.NotaDebitoCompras)
                .HasForeignKey(d => d.MonedaDoc)
                .HasConstraintName("fk_nota_debito_compra_moneda");

            entity.HasOne(d => d.SerieNavigation).WithMany(p => p.NotaDebitoCompras)
                .HasForeignKey(d => d.Serie)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_nota_debito_compra_serie");
        });

        modelBuilder.Entity<NotaDebitoCompraDetalle>(entity =>
        {
            entity.HasKey(e => new { e.Entry, e.NoLinea }).HasName("pk_nota_debito_compra_det");

            entity.ToTable("NotaDebitoCompraDetalle");

            entity.Property(e => e.Cantidad).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.CodigoImpuesto).HasMaxLength(8);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Impuesto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrctjeDesc).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Precio).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.PrecioBruto).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TipoObjeto)
                .HasMaxLength(20)
                .HasDefaultValueSql("('14')");
            entity.Property(e => e.TotalLinea).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.CodigoImpuestoNavigation).WithMany(p => p.NotaDebitoCompraDetalles)
                .HasForeignKey(d => d.CodigoImpuesto)
                .HasConstraintName("fk_nota_debito_compra_det_cod_impuesto");
        });
```

- [ ] **Step 6: Repos**

`API.Infraestructure.Repository/NotaDebitoRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaDebitoRepositorio : RepositorioGenericoEfCore<NotaDebito, int>
    {
        public NotaDebitoRepositorio(ApiDbTestContext contexto) : base(contexto) { }
    }
}
```

`NotaDebitoDetalleRepositorio.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Infraestructure.Repository
{
    public class NotaDebitoDetalleRepositorio : RepositorioGenericoEfCore<NotaDebitoDetalle, (int Entry, int NoLinea)>
    {
        public NotaDebitoDetalleRepositorio(ApiDbTestContext contexto) : base(contexto) { }

        // Clave primaria compuesta real (Entry + NoLinea): FindAsync necesita ambas partes, en el
        // mismo orden en que se declaro HasKey en ApiDbTestContext.OnModelCreating.
        public override async Task<NotaDebitoDetalle?> ObtenerAsync((int Entry, int NoLinea) id)
        {
            return await DbSet.FindAsync(id.Entry, id.NoLinea);
        }
    }
}
```

`NotaDebitoCompraRepositorio.cs` y `NotaDebitoCompraDetalleRepositorio.cs`: idénticos cambiando `NotaDebito` por `NotaDebitoCompra` en el nombre de clase, constructor y tipo genérico.

- [ ] **Step 7: Verificar y commit**

`dotnet test API.sln` → todo en verde (967 + los 4 casos nuevos de `NotaDebitoModeloTests` = 971; si el conteo difiere, reportar el número real).

```bash
git add API.Domain.Entity/Models/NotaDebito.cs API.Domain.Entity/Models/NotaDebitoDetalle.cs API.Domain.Entity/Models/NotaDebitoCompra.cs API.Domain.Entity/Models/NotaDebitoCompraDetalle.cs API.Domain.Entity/Models/ApiDbTestContext.cs API.Domain.Entity/Models/SocioNegocio.cs API.Domain.Entity/Models/Monedum.cs API.Domain.Entity/Models/NumeracionDocumentoDet.cs API.Domain.Entity/Models/Impuesto.cs API.Infraestructure.Repository/NotaDebitoRepositorio.cs API.Infraestructure.Repository/NotaDebitoDetalleRepositorio.cs API.Infraestructure.Repository/NotaDebitoCompraRepositorio.cs API.Infraestructure.Repository/NotaDebitoCompraDetalleRepositorio.cs API.Service.WebApi.Tests/Domain/NotaDebitoModeloTests.cs
git commit -m "feat(notas-debito): entidades EF, mapeo y repos de nota de debito cliente y proveedor

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```


---

## Fase 2 — API: contratos, dominio, aplicación y controllers

### Task 3: DTOs, interfaces de dominio/aplicación y mapeos

**Files:**
- Create (cliente): `API.Application.DTO/notaDebito/{NotaDebitoDTO,NotaDebitoCrearDTO,NotaDebitoActualizarDTO,NotaDebitoDetalleDTO,NotaDebitoDetalleCrearDTO,NotaDebitoFacturaBaseResumenDTO,NotaDebitoFacturaBaseDTO}.cs`
- Create (proveedor): `API.Application.DTO/notaDebitoCompra/{NotaDebitoCompraDTO,NotaDebitoCompraCrearDTO,NotaDebitoCompraActualizarDTO,NotaDebitoCompraDetalleDTO,NotaDebitoCompraDetalleCrearDTO,NotaDebitoCompraFacturaBaseResumenDTO,NotaDebitoCompraFacturaBaseDTO}.cs`
- Create: `API.Domain.Interface/FacturaBase.cs`, `API.Domain.Interface/INotaDebitoDomain.cs`, `API.Domain.Interface/INotaDebitoCompraDomain.cs`, `API.Application.Interface/INotaDebitoApplication.cs`, `API.Application.Interface/INotaDebitoCompraApplication.cs`
- Modify: `API.Transversal.Mapper/PerfilMapeo.cs`
- Test: `API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs`

**Interfaces:**
- Consumes: entidades del Task 2.
- Produces: los contratos exactos que usan los tasks siguientes (copiarlos tal cual).

**Precondición verificable:** `dotnet test API.sln` verde (971 con el Task 2) y `git status --porcelain` limpio salvo `appsettings.json`.

- [ ] **Step 1: Test de mapeo (falla: no existen los tipos)**

Crear `API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs`:

```csharp
using API.Application.DTO.notaDebito;
using API.Application.DTO.notaDebitoCompra;
using API.Domain.Entity.Models;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class NotaDebitoMapeoTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();

        [Fact]
        public void Cliente_CrearDtoAEntidad_MapeaEncabezadoYIgnoraLineas()
        {
            var dto = new NotaDebitoCrearDTO
            {
                NumDoc = 7, Serie = 33, CodigoSn = "C001", NombreSn = "Cliente", MonedaDoc = "QTZ",
                BaseTipo = 6, BaseEntry = 12, TotalDoc = 112m, Comentario = "ajuste",
                Lineas = { new NotaDebitoDetalleCrearDTO { Descripcion = "Flete", Cantidad = 1, Precio = 100m, CodigoImpuesto = "IVA" } }
            };

            var entidad = _mapper.Map<NotaDebito>(dto);

            Assert.Equal(33, entidad.Serie);
            Assert.Equal("C001", entidad.CodigoSn);
            Assert.Equal(6, entidad.BaseTipo);
            Assert.Equal(12, entidad.BaseEntry);
            Assert.Equal(112m, entidad.TotalDoc);
        }

        [Fact]
        public void Cliente_DetalleCrearDtoAEntidad_MapeaConceptos()
        {
            var dto = new NotaDebitoDetalleCrearDTO { Descripcion = "Intereses", Cantidad = 2, Precio = 50m, PrctjeDesc = 5m, CodigoImpuesto = "IVA", Impuesto = 11.4m, TotalLinea = 106.4m };

            var entidad = _mapper.Map<NotaDebitoDetalle>(dto);

            Assert.Equal("Intereses", entidad.Descripcion);
            Assert.Equal(2m, entidad.Cantidad);
            Assert.Equal("IVA", entidad.CodigoImpuesto);
            Assert.Equal(106.4m, entidad.TotalLinea);
        }

        [Fact]
        public void Cliente_EntidadADto_MapeaEncabezadoYDetalle()
        {
            var nota = _mapper.Map<NotaDebitoDTO>(new NotaDebito { Entry = 9, NumDoc = 3, Serie = 33, CodigoSn = "C001", TotalDoc = 10m, Cancelado = "N" });
            var linea = _mapper.Map<NotaDebitoDetalleDTO>(new NotaDebitoDetalle { Entry = 9, NoLinea = 2, Descripcion = "X", CodigoImpuesto = "IVA" });

            Assert.Equal(9, nota.Entry);
            Assert.Equal("C001", nota.CodigoSn);
            Assert.Equal(2, linea.NoLinea);
            Assert.Equal("X", linea.Descripcion);
        }

        [Fact]
        public void Proveedor_Mapeos_EquivalentesAlCliente()
        {
            var entidad = _mapper.Map<NotaDebitoCompra>(new NotaDebitoCompraCrearDTO { Serie = 35, CodigoSn = "P001", BaseTipo = 13, BaseEntry = 4 });
            var detalle = _mapper.Map<NotaDebitoCompraDetalle>(new NotaDebitoCompraDetalleCrearDTO { Descripcion = "Diferencia de precio", CodigoImpuesto = "EXE" });
            var dto = _mapper.Map<NotaDebitoCompraDTO>(new NotaDebitoCompra { Entry = 1, CodigoSn = "P001" });
            var lineaDto = _mapper.Map<NotaDebitoCompraDetalleDTO>(new NotaDebitoCompraDetalle { Entry = 1, NoLinea = 1, Descripcion = "Z", CodigoImpuesto = "EXE" });

            Assert.Equal(13, entidad.BaseTipo);
            Assert.Equal("Diferencia de precio", detalle.Descripcion);
            Assert.Equal("P001", dto.CodigoSn);
            Assert.Equal("Z", lineaDto.Descripcion);
        }
    }
}
```

- [ ] **Step 2: Ver el fallo** — `dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoMapeoTests"` no compila.

- [ ] **Step 3: DTOs del cliente** (namespace `API.Application.DTO.notaDebito`)

`NotaDebitoDTO.cs`:

```csharp
namespace API.Application.DTO.notaDebito
{
    public class NotaDebitoDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public int Serie { get; set; }
        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }
    }
}
```

`NotaDebitoCrearDTO.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace API.Application.DTO.notaDebito
{
    public class NotaDebitoCrearDTO
    {
        // Requerido solo cuando la serie elegida es "Manual": para series autogeneradas el
        // servidor calcula el siguiente numero (ver NotaDebitoDomain.InsertarAsync).
        public int? NumDoc { get; set; }

        [Required(ErrorMessage = "{0} campo no debe de estar vacio.")]
        public int Serie { get; set; }

        public string? Cancelado { get; set; }
        public string? NumManual { get; set; }
        public string? Imprimido { get; set; }
        public string? EstadoDoc { get; set; }
        public string? TipoObjeto { get; set; }
        public DateTime? FechaDoc { get; set; }
        public DateTime? FechaEmision { get; set; }
        public DateTime? FechaCancelado { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? Direccion { get; set; }
        public string? MonedaDoc { get; set; }
        public int? BaseTipo { get; set; }
        public int? BaseEntry { get; set; }
        public decimal? PrctjeImpuesto { get; set; }
        public decimal? TotalImp { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public decimal? TotalDesc { get; set; }
        public decimal? TotalBruto { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? Comentario { get; set; }

        /// <summary>Lineas (conceptos) del documento: se registran junto con el encabezado y no se
        /// pueden modificar despues.</summary>
        public List<NotaDebitoDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
```

`NotaDebitoActualizarDTO.cs`: las mismas propiedades que `NotaDebitoCrearDTO` **sin** `Lineas`, con `public int NumDoc { get; set; }` (no nullable) y `public int Serie { get; set; }` sin `[Required]` (mismo contrato que `FacturaActualizarDTO`).

`NotaDebitoDetalleDTO.cs`:

```csharp
namespace API.Application.DTO.notaDebito
{
    public class NotaDebitoDetalleDTO
    {
        public int Entry { get; set; }
        public int NoLinea { get; set; }
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
        public string? TipoObjeto { get; set; }
    }
}
```

`NotaDebitoDetalleCrearDTO.cs`:

```csharp
namespace API.Application.DTO.notaDebito
{
    public class NotaDebitoDetalleCrearDTO
    {
        // Entry y NoLinea los asigna el servidor al registrar el documento.
        public string? Descripcion { get; set; }
        public decimal? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal? PrecioBruto { get; set; }
        public decimal? PrctjeDesc { get; set; }
        public string? CodigoImpuesto { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? TotalLinea { get; set; }
    }
}
```

`NotaDebitoFacturaBaseResumenDTO.cs`:

```csharp
namespace API.Application.DTO.notaDebito
{
    /// <summary>Resumen de una factura candidata a base de una nota de debito.</summary>
    public class NotaDebitoFacturaBaseResumenDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public DateTime? FechaDoc { get; set; }
        public decimal? TotalDoc { get; set; }
        public string? MonedaDoc { get; set; }
    }
}
```

`NotaDebitoFacturaBaseDTO.cs`:

```csharp
namespace API.Application.DTO.notaDebito
{
    /// <summary>Factura base con sus lineas convertidas en conceptos listos para precargar la nota.</summary>
    public class NotaDebitoFacturaBaseDTO
    {
        public int Entry { get; set; }
        public int NumDoc { get; set; }
        public string? CodigoSn { get; set; }
        public string? NombreSn { get; set; }
        public string? MonedaDoc { get; set; }
        public string? Direccion { get; set; }
        public List<NotaDebitoDetalleCrearDTO> Lineas { get; set; } = new();
    }
}
```

- [ ] **Step 4: DTOs del proveedor** (namespace `API.Application.DTO.notaDebitoCompra`)

Generarlos desde los del cliente con este script (bash, desde la raíz del repo `API`), que renombra tipos, namespace y archivos:

```bash
set -e
src=API.Application.DTO/notaDebito
dst=API.Application.DTO/notaDebitoCompra
mkdir -p "$dst"
for f in "$src"/*.cs; do
  base=$(basename "$f")
  nuevo="${base/NotaDebito/NotaDebitoCompra}"
  sed -e 's/NotaDebito/NotaDebitoCompra/g' -e 's/namespace API.Application.DTO.notaDebitoCompra/&/' \
      -e 's/API\.Application\.DTO\.notaDebito\b/API.Application.DTO.notaDebitoCompra/g' \
      -e 's/factura candidata/factura de compra candidata/; s/Factura base/Factura de compra base/; s/factura base/factura de compra base/' "$f" > "$dst/$nuevo"
done
grep -rn "notaDebito;" "$dst" && echo "FALLO: quedo namespace del cliente" || echo OK
```

Después abrir `NotaDebitoCompraCrearDTO.cs` y confirmar a mano que (a) el namespace es `API.Application.DTO.notaDebitoCompra`, (b) la lista es `List<NotaDebitoCompraDetalleCrearDTO> Lineas`, (c) el comentario de `Lineas` y el de `NotaDebitoCompraDomain.InsertarAsync` hablan del proveedor. Los nombres de archivo resultantes son exactamente los listados en **Files**.

- [ ] **Step 5: `FacturaBase.cs` e interfaces**

`API.Domain.Interface/FacturaBase.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    /// <summary>Factura de venta con sus lineas: base de una nota de debito de cliente.</summary>
    public record FacturaBase(Factura Documento, IReadOnlyList<FacturaDetalle> Lineas);

    /// <summary>Factura de compra con sus lineas: base de una nota de debito de proveedor.</summary>
    public record FacturaCompraBase(FacturaCompra Documento, IReadOnlyList<FacturaCompraDetalle> Lineas);
}
```

`API.Domain.Interface/INotaDebitoDomain.cs`:

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaDebitoDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaDebito obj, IEnumerable<NotaDebitoDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaDebito obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaDebito> ObtenerAsync(int id);
        Task<IQueryable<NotaDebito>> ObtenerTodoAsync();
        Task<IEnumerable<NotaDebitoDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<FacturaBase> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
```

`INotaDebitoCompraDomain.cs` (escribirlo completo, no derivar):

```csharp
using API.Domain.Entity.Models;

namespace API.Domain.Interface
{
    public interface INotaDebitoCompraDomain
    {
        #region async methods
        Task<int> InsertarAsync(NotaDebitoCompra obj, IEnumerable<NotaDebitoCompraDetalle> lineas);
        Task<bool> ActualizarAsync(int id, NotaDebitoCompra obj);
        Task<bool> EliminarAsync(int id);
        Task<NotaDebitoCompra> ObtenerAsync(int id);
        Task<IQueryable<NotaDebitoCompra>> ObtenerTodoAsync();
        Task<IEnumerable<NotaDebitoCompraDetalle>> ObtenerLineasAsync(int id);
        Task<IEnumerable<FacturaCompra>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<FacturaCompraBase> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
```

`API.Application.Interface/INotaDebitoApplication.cs`:

```csharp
using API.Application.DTO;
using API.Application.DTO.notaDebito;

namespace API.Application.Interface
{
    public interface INotaDebitoApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(NotaDebitoCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, NotaDebitoActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<NotaDebitoDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>> ObtenerLineasAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<Respuesta<NotaDebitoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry);
        #endregion
    }
}
```

`INotaDebitoCompraApplication.cs` (completo):

```csharp
using API.Application.DTO;
using API.Application.DTO.notaDebitoCompra;

namespace API.Application.Interface
{
    public interface INotaDebitoCompraApplication
    {
        #region async methods
        Task<Respuesta<int>> InsertarAsync(NotaDebitoCompraCrearDTO obj);
        Task<Respuesta<bool>> ActualizarAsync(int id, NotaDebitoCompraActualizarDTO obj);
        Task<Respuesta<bool>> EliminarAsync(int id);
        Task<Respuesta<NotaDebitoCompraDTO>> ObtenerAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoCompraDTO>>> ObtenerTodoAsync();
        Task<Respuesta<IEnumerable<NotaDebitoCompraDetalleDTO>>> ObtenerLineasAsync(int id);
        Task<Respuesta<IEnumerable<NotaDebitoCompraFacturaBaseResumenDTO>>> ObtenerFacturasCompraBaseAsync(string codigoSn);
        Task<Respuesta<NotaDebitoCompraFacturaBaseDTO>> ObtenerFacturaCompraBaseAsync(int entry);
        #endregion
    }
}
```

- [ ] **Step 6: Mapeos en `PerfilMapeo.cs`**

Agregar los `using API.Application.DTO.notaDebito;` y `using API.Application.DTO.notaDebitoCompra;` al inicio (junto a los de `factura`), y después del bloque `// FacturaDetalle` (que termina con `CreateMap<FacturaDetalleActualizarDTO, FacturaDetalle>();`):

```csharp
            // NotaDebito
            CreateMap<NotaDebito, NotaDebitoDTO>();
            CreateMap<NotaDebitoCrearDTO, NotaDebito>();
            CreateMap<NotaDebitoActualizarDTO, NotaDebito>();

            // NotaDebitoDetalle
            CreateMap<NotaDebitoDetalle, NotaDebitoDetalleDTO>();
            CreateMap<NotaDebitoDetalleCrearDTO, NotaDebitoDetalle>();

            // NotaDebitoCompra
            CreateMap<NotaDebitoCompra, NotaDebitoCompraDTO>();
            CreateMap<NotaDebitoCompraCrearDTO, NotaDebitoCompra>();
            CreateMap<NotaDebitoCompraActualizarDTO, NotaDebitoCompra>();

            // NotaDebitoCompraDetalle
            CreateMap<NotaDebitoCompraDetalle, NotaDebitoCompraDetalleDTO>();
            CreateMap<NotaDebitoCompraDetalleCrearDTO, NotaDebitoCompraDetalle>();
```

- [ ] **Step 7: Verificar y commit**

`dotnet build API.sln` 0 errores no es posible aún del todo (las interfaces no tienen implementación todavía, pero eso compila). `dotnet test API.sln` verde (971 + 4 = 975).

```bash
git add API.Application.DTO/notaDebito API.Application.DTO/notaDebitoCompra API.Domain.Interface/FacturaBase.cs API.Domain.Interface/INotaDebitoDomain.cs API.Domain.Interface/INotaDebitoCompraDomain.cs API.Application.Interface/INotaDebitoApplication.cs API.Application.Interface/INotaDebitoCompraApplication.cs API.Transversal.Mapper/PerfilMapeo.cs API.Service.WebApi.Tests/Application/NotaDebitoMapeoTests.cs
git commit -m "feat(notas-debito): DTOs, contratos de dominio/aplicacion y mapeos

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `NotaDebitoDomain` (cliente) con pruebas

**Files:**
- Create: `API.Domain.Core/NotaDebitoDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs`

**Interfaces:**
- Consumes: `INotaDebitoDomain`, `FacturaBase` (Task 3); entidades y repos (Task 2); `IRepositorioGenerico<T,K>`, `IEjecutorTransaccion` (existentes: `EjecutarAsync<T>(Func<Task<T>>)`).
- Produces: `NotaDebitoDomain : INotaDebitoDomain` con constructor `(IRepositorioGenerico<NotaDebito,int> repoNota, IRepositorioGenerico<NotaDebitoDetalle,(int Entry,int NoLinea)> repoDetalle, IRepositorioGenerico<NumeracionDocumentoDet,int> repoNumeracion, IRepositorioGenerico<SocioNegocio,string> repoSocio, IRepositorioGenerico<Impuesto,string> repoImpuesto, IRepositorioGenerico<Monedum,string> repoMoneda, IRepositorioGenerico<Factura,int> repoFactura, IRepositorioGenerico<FacturaDetalle,(int Entry,int NoLinea)> repoFacturaDetalle, IEjecutorTransaccion tx)` — en ESE orden (los tests y el Task 6 lo usan).

**Precondición verificable:** Task 3 commiteado; `grep -n "TipoSn\|Activo" API.Domain.Entity/Models/SocioNegocio.cs` muestra `TipoSn` y `Activo`; `grep -n "Codigo" API.Domain.Entity/Models/Impuesto.cs API.Domain.Entity/Models/Monedum.cs` muestra `Codigo` en ambos.

- [ ] **Step 1: Escribir las pruebas (fallan: el dominio no existe)**

Crear `API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs`:

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
        private const string CodigoObj = "7";
        private const string CodigoObjOtro = "6";   // CodigoObj de otro documento (Factura)
        private const string TipoSocio = "C";
        private const string TipoSocioOpuesto = "P";
        private const int TipoBase = 6;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de débito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private readonly Mock<IRepositorioGenerico<NotaDebito, int>> _repoNota = new();
        private readonly Mock<IRepositorioGenerico<NotaDebitoDetalle, (int Entry, int NoLinea)>> _repoDetalle = new();
        private readonly Mock<IRepositorioGenerico<NumeracionDocumentoDet, int>> _repoNumeracion = new();
        private readonly Mock<IRepositorioGenerico<SocioNegocio, string>> _repoSocio = new();
        private readonly Mock<IRepositorioGenerico<Impuesto, string>> _repoImpuesto = new();
        private readonly Mock<IRepositorioGenerico<Monedum, string>> _repoMoneda = new();
        private readonly Mock<IRepositorioGenerico<Factura, int>> _repoFactura = new();
        private readonly Mock<IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)>> _repoFacturaDetalle = new();
        private readonly Mock<IEjecutorTransaccion> _tx = new();
        private readonly NotaDebitoDomain _domain;

        public NotaDebitoDomainTests()
        {
            _domain = new NotaDebitoDomain(
                _repoNota.Object, _repoDetalle.Object, _repoNumeracion.Object, _repoSocio.Object,
                _repoImpuesto.Object, _repoMoneda.Object, _repoFactura.Object, _repoFacturaDetalle.Object, _tx.Object);

            // El doble del ejecutor corre el Func directo (sin transaccion ni save).
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>())).Returns<Func<Task<int>>>(f => f());
            _tx.Setup(t => t.EjecutarAsync(It.IsAny<Func<Task<bool>>>())).Returns<Func<Task<bool>>>(f => f());
            _repoNota.Setup(r => r.InsertarAsync(It.IsAny<NotaDebito>()))
                .ReturnsAsync((NotaDebito n) => { n.Entry = 99; return n; });
            _repoDetalle.Setup(r => r.AgregarSinGuardarAsync(It.IsAny<NotaDebitoDetalle>())).Returns(Task.CompletedTask);

            // Valores por defecto validos: cada test rompe solo lo que quiere probar.
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(CrearSerie());
            _repoSocio.Setup(r => r.ObtenerAsync("SN1")).ReturnsAsync(new SocioNegocio { Codigo = "SN1", TipoSn = TipoSocio, Activo = "S" });
            _repoMoneda.Setup(r => r.ObtenerAsync("QTZ")).ReturnsAsync(new Monedum { Codigo = "QTZ" });
            _repoImpuesto.Setup(r => r.ObtenerAsync("IVA")).ReturnsAsync(new Impuesto { Codigo = "IVA" });
        }

        private static NumeracionDocumentoDet CrearSerie(int? sig = 5, int? fin = null, string bloqueado = "N", string manual = "N", string? codigoObj = null) => new()
        {
            CodigoObj = codigoObj ?? CodigoObj, Serie = 4, NombreSerie = "Primario",
            SigNumero = sig, FinNumero = fin, Bloqueado = bloqueado, Manual = manual,
            SubTipoDoc = "--", TipoSerie = "D"
        };

        private static NotaDebito Nota(Action<NotaDebito>? ajustar = null)
        {
            var n = new NotaDebito
            {
                Serie = 4, CodigoSn = "SN1", MonedaDoc = "QTZ",
                FechaDoc = new DateTime(2026, 10, 4), FechaEmision = new DateTime(2026, 10, 4)
            };
            ajustar?.Invoke(n);
            return n;
        }

        private static NotaDebitoDetalle Linea(Action<NotaDebitoDetalle>? ajustar = null)
        {
            var l = new NotaDebitoDetalle
            {
                Descripcion = "Cargo por flete", Cantidad = 1m, Precio = 100m, PrctjeDesc = 0m,
                CodigoImpuesto = "IVA", Impuesto = 12m, TotalLinea = 112m
            };
            ajustar?.Invoke(l);
            return l;
        }

        private async Task<string> MensajeAlInsertar(NotaDebito nota, params NotaDebitoDetalle[] lineas)
        {
            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(nota, lineas));
            return ex.Message;
        }

        // ---------------- InsertarAsync: camino feliz ----------------

        [Fact]
        public async Task InsertarAsync_Valida_NumeraGuardaEncabezadoYLineas()
        {
            var serie = CrearSerie(sig: 5);
            _repoNumeracion.Setup(r => r.ObtenerAsync(4)).ReturnsAsync(serie);
            var nota = Nota();

            var entry = await _domain.InsertarAsync(nota, new[] { Linea(), Linea(l => l.Descripcion = "Intereses") });

            Assert.Equal(99, entry);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
            Assert.Equal(5, nota.NumDoc);
            Assert.Equal(6, serie.SigNumero);          // arrancaba en 5: avanzo
            Assert.Equal("N", nota.NumManual);
            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<NotaDebitoDetalle>(l => l.Entry == 99 && l.NoLinea == 1 && l.TipoObjeto == CodigoObj)), Times.Once);
            _repoDetalle.Verify(r => r.AgregarSinGuardarAsync(It.Is<NotaDebitoDetalle>(l => l.Entry == 99 && l.NoLinea == 2 && l.Descripcion == "Intereses")), Times.Once);
        }

        [Fact]
        public async Task InsertarAsync_FuerzaLosCamposDeSistema()
        {
            var nota = Nota(n => { n.Cancelado = "S"; n.EstadoDoc = "C"; n.FechaCancelado = DateTime.Now; n.TipoObjeto = "6"; });

            await _domain.InsertarAsync(nota, new[] { Linea() });

            Assert.Equal("N", nota.Cancelado);
            Assert.Equal("A", nota.EstadoDoc);
            Assert.Null(nota.FechaCancelado);
            Assert.Equal(CodigoObj, nota.TipoObjeto);
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
        public async Task InsertarAsync_LineaSinDescripcion_Lanza(string descripcion, string esperado)
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
        public async Task InsertarAsync_ConErrorDeValidacion_NoGuardaNada()
        {
            await Assert.ThrowsAsync<Exception>(() => _domain.InsertarAsync(Nota(), Array.Empty<NotaDebitoDetalle>()));

            _tx.Verify(t => t.EjecutarAsync(It.IsAny<Func<Task<int>>>()), Times.Never);
            _repoNota.Verify(r => r.InsertarAsync(It.IsAny<NotaDebito>()), Times.Never);
        }

        // ---------------- InsertarAsync: factura base ----------------

        private void PrepararFactura(int entry = 12, string socio = "SN1", string cancelado = "N") =>
            _repoFactura.Setup(r => r.ObtenerAsync(entry)).ReturnsAsync(new Factura { Entry = entry, CodigoSn = socio, Cancelado = cancelado });

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

        [Fact]
        public async Task ActualizarAsync_Inexistente_DevuelveFalse()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NotaDebito?)null);

            Assert.False(await _domain.ActualizarAsync(5, new NotaDebito()));
        }

        [Fact]
        public async Task ActualizarAsync_Cancelar_MarcaCanceladoYFechaSinTocarElComentario()
        {
            var existente = new NotaDebito { Entry = 5, Cancelado = "N", Comentario = "original" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            var ok = await _domain.ActualizarAsync(5, new NotaDebito { Cancelado = "S" });

            Assert.True(ok);
            Assert.Equal("S", existente.Cancelado);
            Assert.NotNull(existente.FechaCancelado);
            Assert.Equal("original", existente.Comentario);
            Assert.Equal("A", existente.EstadoDoc ?? "A");   // EstadoDoc no cambia al cancelar
        }

        [Fact]
        public async Task ActualizarAsync_CancelarConComentario_ActualizaElComentario()
        {
            var existente = new NotaDebito { Entry = 5, Cancelado = "N", Comentario = "original" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(5, new NotaDebito { Cancelado = "S", Comentario = "motivo" });

            Assert.Equal("motivo", existente.Comentario);
        }

        [Fact]
        public async Task ActualizarAsync_SoloComentario_NoCancelaNiCambiaOtrosCampos()
        {
            var existente = new NotaDebito { Entry = 5, Cancelado = "N", Comentario = "viejo", TotalDoc = 50m, CodigoSn = "SN1" };
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(existente);

            await _domain.ActualizarAsync(5, new NotaDebito { Cancelado = "N", Comentario = "nuevo", TotalDoc = 999m, CodigoSn = "OTRO" });

            Assert.Equal("nuevo", existente.Comentario);
            Assert.Equal("N", existente.Cancelado);
            Assert.Equal(50m, existente.TotalDoc);
            Assert.Equal("SN1", existente.CodigoSn);
        }

        [Fact]
        public async Task ActualizarAsync_DocumentoCancelado_NoSePuedeModificar()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaDebito { Entry = 5, Cancelado = "S" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ActualizarAsync(5, new NotaDebito { Comentario = "x" }));

            Assert.Equal("El documento está cancelado y no se puede modificar.", ex.Message);
        }

        // ---------------- EliminarAsync ----------------

        [Fact]
        public async Task EliminarAsync_Inexistente_DevuelveFalse()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync((NotaDebito?)null);

            Assert.False(await _domain.EliminarAsync(5));
        }

        [Fact]
        public async Task EliminarAsync_SinCancelar_Lanza()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaDebito { Entry = 5, Cancelado = "N" });

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.EliminarAsync(5));

            Assert.Equal("Cancele el documento (Cancelado='S') antes de eliminarlo.", ex.Message);
            _repoNota.Verify(r => r.EliminarAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task EliminarAsync_Cancelado_BorraLasLineasYElEncabezado()
        {
            _repoNota.Setup(r => r.ObtenerAsync(5)).ReturnsAsync(new NotaDebito { Entry = 5, Cancelado = "S" });
            var lineas = new List<NotaDebitoDetalle>
            {
                new() { Entry = 5, NoLinea = 1 }, new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());
            _repoDetalle.Setup(r => r.EliminarAsync(It.IsAny<(int Entry, int NoLinea)>())).ReturnsAsync(true);
            _repoNota.Setup(r => r.EliminarAsync(5)).ReturnsAsync(true);

            var ok = await _domain.EliminarAsync(5);

            Assert.True(ok);
            _repoDetalle.Verify(r => r.EliminarAsync((5, 1)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync((5, 2)), Times.Once);
            _repoDetalle.Verify(r => r.EliminarAsync((6, 1)), Times.Never);
            _repoNota.Verify(r => r.EliminarAsync(5), Times.Once);
        }

        // ---------------- Consultas ----------------

        [Fact]
        public async Task ObtenerLineasAsync_DevuelveSoloLasDelDocumentoEnOrden()
        {
            var lineas = new List<NotaDebitoDetalle>
            {
                new() { Entry = 5, NoLinea = 2 }, new() { Entry = 6, NoLinea = 1 }, new() { Entry = 5, NoLinea = 1 }
            };
            _repoDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

            var resultado = (await _domain.ObtenerLineasAsync(5)).ToList();

            Assert.Equal(new[] { 1, 2 }, resultado.Select(l => l.NoLinea));
        }

        [Fact]
        public async Task ObtenerFacturasBaseAsync_DevuelveLasNoCanceladasDelSocioDeLaMasNuevaALaMasVieja()
        {
            var facturas = new List<Factura>
            {
                new() { Entry = 1, CodigoSn = "SN1", Cancelado = "N" },
                new() { Entry = 2, CodigoSn = "SN1", Cancelado = "S" },
                new() { Entry = 3, CodigoSn = "OTRO", Cancelado = "N" },
                new() { Entry = 4, CodigoSn = "SN1", Cancelado = "N" }
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
            _repoFacturaDetalle.Setup(r => r.ObtenerTodoAsync()).ReturnsAsync(lineas.AsAsyncQueryable());

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
        public async Task ObtenerFacturaBaseAsync_Cancelada_Lanza()
        {
            PrepararFactura(entry: 12, cancelado: "S");

            var ex = await Assert.ThrowsAsync<Exception>(() => _domain.ObtenerFacturaBaseAsync(12));

            Assert.Equal(MsgFacturaCancelada, ex.Message);
        }
    }
}
```

- [ ] **Step 2: Ver el fallo** — `dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoDomainTests"` no compila (`NotaDebitoDomain` no existe).

- [ ] **Step 3: Implementar `API.Domain.Core/NotaDebitoDomain.cs`**

```csharp
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Infraestructure.Interface;
using Microsoft.EntityFrameworkCore;

namespace API.Domain.Core
{
    public class NotaDebitoDomain : INotaDebitoDomain
    {
        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        // CHECK constraint de la tabla: TipoObjeto='7'. Se fuerza siempre en el servidor.
        private const string TipoObjetoNotaDebito = "7";
        private const string TipoSocio = "C";
        private const int TipoDocBase = 6;
        private const string MsgSerieOtroObjeto = "La serie no corresponde a notas de débito de cliente.";
        private const string MsgSocioTipo = "El socio de negocio debe ser un cliente.";
        private const string MsgBaseRequerida = "Indique la factura base.";
        private const string MsgBaseNoExiste = "La factura base no existe.";
        private const string MsgBaseCancelada = "La factura base está cancelada.";
        private const string MsgBaseOtroSocio = "La factura base pertenece a otro socio de negocio.";
        private const string MsgFacturaNoExiste = "La factura no existe.";
        private const string MsgFacturaCancelada = "La factura está cancelada.";
        // ---- fin de constantes ----

        private const int SinBase = -1;
        private const int LongitudMaximaDescripcion = 200;

        private readonly IRepositorioGenerico<NotaDebito, int> _repoNota;
        private readonly IRepositorioGenerico<NotaDebitoDetalle, (int Entry, int NoLinea)> _repoDetalle;
        private readonly IRepositorioGenerico<NumeracionDocumentoDet, int> _repoNumeracion;
        private readonly IRepositorioGenerico<SocioNegocio, string> _repoSocio;
        private readonly IRepositorioGenerico<Impuesto, string> _repoImpuesto;
        private readonly IRepositorioGenerico<Monedum, string> _repoMoneda;
        private readonly IRepositorioGenerico<Factura, int> _repoFactura;
        private readonly IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> _repoFacturaDetalle;
        private readonly IEjecutorTransaccion _tx;

        public NotaDebitoDomain(
            IRepositorioGenerico<NotaDebito, int> repoNota,
            IRepositorioGenerico<NotaDebitoDetalle, (int Entry, int NoLinea)> repoDetalle,
            IRepositorioGenerico<NumeracionDocumentoDet, int> repoNumeracion,
            IRepositorioGenerico<SocioNegocio, string> repoSocio,
            IRepositorioGenerico<Impuesto, string> repoImpuesto,
            IRepositorioGenerico<Monedum, string> repoMoneda,
            IRepositorioGenerico<Factura, int> repoFactura,
            IRepositorioGenerico<FacturaDetalle, (int Entry, int NoLinea)> repoFacturaDetalle,
            IEjecutorTransaccion tx)
        {
            _repoNota = repoNota;
            _repoDetalle = repoDetalle;
            _repoNumeracion = repoNumeracion;
            _repoSocio = repoSocio;
            _repoImpuesto = repoImpuesto;
            _repoMoneda = repoMoneda;
            _repoFactura = repoFactura;
            _repoFacturaDetalle = repoFacturaDetalle;
            _tx = tx;
        }

        #region async methods
        public async Task<int> InsertarAsync(NotaDebito obj, IEnumerable<NotaDebitoDetalle> lineas)
        {
            var lineasList = lineas?.ToList() ?? new List<NotaDebitoDetalle>();

            obj.TipoObjeto = TipoObjetoNotaDebito;
            obj.Cancelado = "N";                  // un documento recien registrado nunca nace cancelado
            obj.EstadoDoc = "A";
            obj.FechaCancelado = null;
            obj.FechaDoc ??= DateTime.Now;
            obj.FechaEmision ??= obj.FechaDoc;

            var serie = await ValidarSerieAsync(obj);
            await ValidarSocioYMonedaAsync(obj);
            await ValidarLineasAsync(lineasList);
            await ValidarBaseAsync(obj);

            AsignarNumero(obj, serie);

            return await _tx.EjecutarAsync(async () =>
            {
                await _repoNota.InsertarAsync(obj); // Save #1: asigna obj.Entry

                var noLinea = 1;
                foreach (var linea in lineasList)
                {
                    linea.Entry = obj.Entry;
                    linea.NoLinea = noLinea++;
                    linea.TipoObjeto = TipoObjetoNotaDebito;
                    await _repoDetalle.AgregarSinGuardarAsync(linea);
                }

                return obj.Entry;
            });
            // EjecutarAsync: Save #2 (lineas) + Commit. No hay asiento de inventario: la nota es solo monetaria.
        }

        public async Task<bool> ActualizarAsync(int id, NotaDebito obj)
        {
            var existente = await _repoNota.ObtenerAsync(id);
            if (existente is null)
                return false;

            if (existente.Cancelado == "S")
                throw new Exception("El documento está cancelado y no se puede modificar.");

            // Cancelacion: Cancelado pasa a 'S' (EstadoDoc no cambia, igual que en Factura).
            if (obj.Cancelado == "S")
            {
                return await _tx.EjecutarAsync(async () =>
                {
                    await Task.CompletedTask;
                    existente.Cancelado = "S";
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
            var existente = await _repoNota.ObtenerAsync(id);
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

                return await _repoNota.EliminarAsync(id);
            });
        }

        public async Task<NotaDebito> ObtenerAsync(int id)
        {
            return await _repoNota.ObtenerAsync(id);
        }

        public async Task<IQueryable<NotaDebito>> ObtenerTodoAsync()
        {
            return await _repoNota.ObtenerTodoAsync();
        }

        public async Task<IEnumerable<NotaDebitoDetalle>> ObtenerLineasAsync(int id)
        {
            var queryable = await _repoDetalle.ObtenerTodoAsync();
            return await queryable.Where(d => d.Entry == id).OrderBy(d => d.NoLinea).ToListAsync();
        }

        public async Task<IEnumerable<Factura>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var queryable = await _repoFactura.ObtenerTodoAsync();
            return await queryable
                .Where(f => f.CodigoSn == codigoSn && f.Cancelado != "S")
                .OrderByDescending(f => f.Entry)
                .ToListAsync();
        }

        public async Task<FacturaBase> ObtenerFacturaBaseAsync(int entry)
        {
            var factura = await _repoFactura.ObtenerAsync(entry)
                ?? throw new Exception(MsgFacturaNoExiste);

            if (factura.Cancelado == "S")
                throw new Exception(MsgFacturaCancelada);

            var queryable = await _repoFacturaDetalle.ObtenerTodoAsync();
            var lineas = await queryable.Where(l => l.Entry == entry).OrderBy(l => l.NoLinea).ToListAsync();
            return new FacturaBase(factura, lineas);
        }
        #endregion

        #region validaciones
        private async Task<NumeracionDocumentoDet> ValidarSerieAsync(NotaDebito obj)
        {
            var serie = await _repoNumeracion.ObtenerAsync(obj.Serie)
                ?? throw new Exception("La serie no existe.");

            if (serie.CodigoObj != TipoObjetoNotaDebito)
                throw new Exception(MsgSerieOtroObjeto);

            if (serie.Bloqueado == "S")
                throw new Exception("La serie está bloqueada y no se puede usar para registrar notas de débito.");

            if (serie.Manual == "S" && obj.NumDoc <= 0)
                throw new Exception("El número de documento es requerido para series manuales.");

            return serie;
        }

        // La numeracion automatica se asigna al final (despues de validar todo) para no avanzar el
        // consecutivo de una serie tracked si el documento resulta invalido.
        private static void AsignarNumero(NotaDebito obj, NumeracionDocumentoDet serie)
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

        private async Task ValidarSocioYMonedaAsync(NotaDebito obj)
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

        private async Task ValidarLineasAsync(IList<NotaDebitoDetalle> lineas)
        {
            if (lineas.Count == 0)
                throw new Exception("El documento debe tener al menos una línea.");

            var impuestosVerificados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lineas.Count; i++)
            {
                var n = i + 1;
                var linea = lineas[i];

                var descripcion = (linea.Descripcion ?? string.Empty).Trim();
                if (descripcion.Length == 0)
                    throw new Exception($"Línea {n}: la descripción es requerida.");
                if (descripcion.Length > LongitudMaximaDescripcion)
                    throw new Exception($"Línea {n}: la descripción no puede superar {LongitudMaximaDescripcion} caracteres.");
                linea.Descripcion = descripcion;

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

        private async Task ValidarBaseAsync(NotaDebito obj)
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

            if (factura.Cancelado == "S")
                throw new Exception(MsgBaseCancelada);

            if (!string.Equals(factura.CodigoSn, obj.CodigoSn, StringComparison.OrdinalIgnoreCase))
                throw new Exception(MsgBaseOtroSocio);
        }
        #endregion
    }
}
```

- [ ] **Step 4: Ejecutar las pruebas del dominio**

`dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoDomainTests"` → todas en verde. Si un test falla por un detalle de Moq (p. ej. `ReturnsAsync((NotaDebito?)null)` en una versión que no admite el cast), ajustar el test manteniendo la aserción; no cambiar los mensajes del dominio.

- [ ] **Step 5: Suite completa y commit**

`dotnet test API.sln` → verde (975 + el número de casos del archivo de pruebas; reportar el total real).

```bash
git add API.Domain.Core/NotaDebitoDomain.cs API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs
git commit -m "feat(notas-debito): dominio de nota de debito de cliente con validaciones y factura base

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `NotaDebitoCompraDomain` (proveedor) derivado del cliente

**Files:**
- Create: `API.Domain.Core/NotaDebitoCompraDomain.cs`
- Test: `API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs`

**Interfaces:**
- Consumes: `NotaDebitoDomain` y `NotaDebitoDomainTests` del Task 4; `INotaDebitoCompraDomain`, `FacturaCompraBase` (Task 3).
- Produces: `NotaDebitoCompraDomain : INotaDebitoCompraDomain` con el mismo orden de constructor que el cliente pero con `NotaDebitoCompra`, `NotaDebitoCompraDetalle`, `FacturaCompra`, `FacturaCompraDetalle` en lugar de `NotaDebito`, `NotaDebitoDetalle`, `Factura`, `FacturaDetalle`; métodos de base `ObtenerFacturasCompraBaseAsync(string)` y `ObtenerFacturaCompraBaseAsync(int)`.

**Precondición verificable:** Task 4 commiteado y verde; `grep -n "Descripcion\|CodigoImpuesto\|TotalLinea\|CodigoSn\|Cancelado" API.Domain.Entity/Models/FacturaCompra.cs API.Domain.Entity/Models/FacturaCompraDetalle.cs` muestra esas propiedades (la derivación asume los mismos nombres que `Factura`).

Esta derivación es **mecánica** y determinista (el proveedor es el espejo exacto del cliente). El resultado se genera con el script de abajo y luego se verifica con las pruebas.

- [ ] **Step 1: Generar dominio y pruebas del proveedor**

Desde la raíz del repo `API` (bash). El orden de los `sed` importa: primero los valores propios del documento, después los renombres estructurales.

```bash
set -e
mirror() {   # $1 = origen  $2 = destino
  sed -E \
    -e 's|private const string TipoObjetoNotaDebito = "7";|private const string TipoObjetoNotaDebito = "14";|' \
    -e 's|private const string TipoSocio = "C";|private const string TipoSocio = "P";|' \
    -e 's|private const int TipoDocBase = 6;|private const int TipoDocBase = 13;|' \
    -e 's|private const string CodigoObj = "7";|private const string CodigoObj = "14";|' \
    -e 's|private const string CodigoObjOtro = "6";|private const string CodigoObjOtro = "13";|' \
    -e 's|private const string TipoSocioOpuesto = "P";|private const string TipoSocioOpuesto = "C";|' \
    -e 's|private const int TipoBase = 6;|private const int TipoBase = 13;|' \
    -e 's|notas de débito de cliente\.|notas de débito de proveedor.|' \
    -e 's|debe ser un cliente\.|debe ser un proveedor.|' \
    -e 's|"Indique la factura base\."|"Indique la factura de compra base."|' \
    -e 's|"La factura base no existe\."|"La factura de compra base no existe."|' \
    -e 's|"La factura base está cancelada\."|"La factura de compra base está cancelada."|' \
    -e 's|"La factura base pertenece a otro socio de negocio\."|"La factura de compra base pertenece a otro socio de negocio."|' \
    -e 's|"La factura no existe\."|"La factura de compra no existe."|' \
    -e 's|"La factura está cancelada\."|"La factura de compra está cancelada."|' \
    -e 's/ObtenerFacturasBaseAsync/ObtenerFacturasCompraBaseAsync/g' \
    -e 's/ObtenerFacturaBaseAsync/ObtenerFacturaCompraBaseAsync/g' \
    -e 's/NotaDebito/NotaDebitoCompra/g' \
    -e 's/FacturaBase/FacturaCompraBase/g' \
    -e 's/FacturaDetalle/FacturaCompraDetalle/g' \
    -e 's/_repoFactura\b/_repoFacturaCompra/g' \
    -e 's/\bFactura\b/FacturaCompra/g' \
    "$1" > "$2"
}
mirror API.Domain.Core/NotaDebitoDomain.cs API.Domain.Core/NotaDebitoCompraDomain.cs
mirror API.Service.WebApi.Tests/Domain/NotaDebitoDomainTests.cs API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs
```

- [ ] **Step 2: Revisar lo generado (diff contra el cliente)**

```bash
diff API.Domain.Core/NotaDebitoDomain.cs API.Domain.Core/NotaDebitoCompraDomain.cs | grep '^>' | head -80
```

Deben aparecer solo: nombres de clase/interfaz/entidad con `Compra`, las constantes `"14"`/`"P"`/`13` y los mensajes de proveedor, `ObtenerFacturasCompraBaseAsync/ObtenerFacturaCompraBaseAsync`, `_repoFacturaCompra`, `FacturaCompraBase`, y las referencias `factura.CodigoSn`/`factura.Cancelado` sin cambios. Comprobar a mano tres cosas que el `sed` no puede garantizar: (a) la propiedad del record sigue llamándose `Documento` en `FacturaCompraBase`; (b) el comentario de `TipoObjetoNotaDebitoCompra` dice `TipoObjeto='14'` (editar el texto del comentario si quedó "7"); (c) en el archivo de pruebas, `grep -n '"7"|= 6;' API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs` no debe devolver líneas (todos los literales propios del documento viven en la región de constantes y ya valen `"14"`, `"13"` y `13`).

- [ ] **Step 3: Pruebas**

`dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoCompraDomainTests"` → verde. Un fallo indica un literal que el `sed` no cubrió: corregirlo en el archivo derivado (no en el cliente) y volver a ejecutar.

- [ ] **Step 4: Suite completa y commit**

`dotnet test API.sln` verde (reportar total real).

```bash
git add API.Domain.Core/NotaDebitoCompraDomain.cs API.Service.WebApi.Tests/Domain/NotaDebitoCompraDomainTests.cs
git commit -m "feat(notas-debito): dominio de nota de debito de proveedor con validaciones y factura de compra base

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```


---

### Task 6: Aplicación, controllers y DI (cliente y proveedor) con pruebas

**Files:**
- Create: `API.Application.Main/NotaDebitoApplication.cs`, `API.Application.Main/NotaDebitoCompraApplication.cs`
- Create: `API.Service.WebApi/Controllers/NotaDebitoController.cs`, `API.Service.WebApi/Controllers/NotaDebitoCompraController.cs`
- Modify: `API.Service.WebApi/Startup.cs`
- Test: `API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs`, `NotaDebitoCompraApplicationTests.cs`; `API.Service.WebApi.Tests/Controllers/NotaDebitoControllerTests.cs`, `NotaDebitoCompraControllerTests.cs`

**Interfaces:**
- Consumes: `INotaDebitoDomain`/`INotaDebitoCompraDomain` (Task 3/4/5), DTOs y `INotaDebitoApplication`/`INotaDebitoCompraApplication` (Task 3), `PermisoUsuario.Tiene`/`PermisoUsuario.Denegado` y `[RequierePermiso]` (existentes en `API.Service.WebApi/Authorization`), `Respuesta<T>`.
- Produces: rutas REST (todas con `[Authorize]`):
  - `GET api/NotaDebito/{id}` · `GET api/NotaDebito` · `GET api/NotaDebito/{id}/Lineas` · `GET api/NotaDebito/FacturasBase/{codigoSn}` · `GET api/NotaDebito/FacturaBase/{entry}` (permiso `NotasDebito`/`Ver`); `POST api/NotaDebito` (`Agregar`); `PUT api/NotaDebito/{id}` (`Actualizar`, y `Eliminar` si `Cancelado == "S"`); `DELETE api/NotaDebito/{id}` (`Eliminar`).
  - Proveedor: igual con `api/NotaDebitoCompra`, módulo `NotasDebitoCompra`, y los endpoints de base `FacturasCompraBase/{codigoSn}` y `FacturaCompraBase/{entry}`.

**Precondición verificable:** Tasks 4 y 5 commiteados; `dotnet test API.sln` verde.

- [ ] **Step 1: Pruebas de la aplicación del cliente (fallan: no existe `NotaDebitoApplication`)**

Crear `API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs`:

```csharp
using API.Application.DTO.notaDebito;
using API.Application.Main;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using API.Service.WebApi.Tests.TestHelpers;
using API.Transversal.Mapper;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Application
{
    public class NotaDebitoApplicationTests
    {
        private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<PerfilMapeo>(), NullLoggerFactory.Instance).CreateMapper();
        private readonly Mock<INotaDebitoDomain> _domain = new();
        private readonly NotaDebitoApplication _app;

        public NotaDebitoApplicationTests()
        {
            _app = new NotaDebitoApplication(_domain.Object, _mapper);
        }

        [Fact]
        public async Task Insertar_MapeaEncabezadoYLineas_YDevuelveElEntry()
        {
            NotaDebito? recibida = null;
            List<NotaDebitoDetalle>? lineasRecibidas = null;
            _domain.Setup(d => d.InsertarAsync(It.IsAny<NotaDebito>(), It.IsAny<IEnumerable<NotaDebitoDetalle>>()))
                .Callback<NotaDebito, IEnumerable<NotaDebitoDetalle>>((n, l) => { recibida = n; lineasRecibidas = l.ToList(); })
                .ReturnsAsync(42);
            var dto = new NotaDebitoCrearDTO
            {
                Serie = 33, CodigoSn = "SN1", MonedaDoc = "QTZ", Comentario = "c",
                Lineas = { new NotaDebitoDetalleCrearDTO { Descripcion = "Flete", Cantidad = 2, Precio = 10m, CodigoImpuesto = "IVA" } }
            };

            var respuesta = await _app.InsertarAsync(dto);

            Assert.True(respuesta.Resultado);
            Assert.Equal(42, respuesta.Dato);
            Assert.Equal("Registro agregado correctamente.", respuesta.Mensaje);
            Assert.Equal("SN1", recibida!.CodigoSn);
            var linea = Assert.Single(lineasRecibidas!);
            Assert.Equal("Flete", linea.Descripcion);
            Assert.Equal(2m, linea.Cantidad);
        }

        [Fact]
        public async Task Insertar_ErrorDelDominio_DevuelveResultadoFalseConElMensaje()
        {
            _domain.Setup(d => d.InsertarAsync(It.IsAny<NotaDebito>(), It.IsAny<IEnumerable<NotaDebitoDetalle>>()))
                .ThrowsAsync(new Exception("La serie no existe."));

            var respuesta = await _app.InsertarAsync(new NotaDebitoCrearDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("La serie no existe.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_DominioDevuelveTrue_ResultadoTrue()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<NotaDebito>())).ReturnsAsync(true);

            var respuesta = await _app.ActualizarAsync(5, new NotaDebitoActualizarDTO { Cancelado = "S" });

            Assert.True(respuesta.Resultado);
            Assert.True(respuesta.Dato);
            Assert.Equal("Registro actualizado correctamente.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Actualizar_DominioDevuelveFalse_ResultadoFalse()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<NotaDebito>())).ReturnsAsync(false);

            var respuesta = await _app.ActualizarAsync(5, new NotaDebitoActualizarDTO());

            Assert.False(respuesta.Resultado);
        }

        [Fact]
        public async Task Actualizar_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ActualizarAsync(5, It.IsAny<NotaDebito>()))
                .ThrowsAsync(new Exception("El documento está cancelado y no se puede modificar."));

            var respuesta = await _app.ActualizarAsync(5, new NotaDebitoActualizarDTO());

            Assert.False(respuesta.Resultado);
            Assert.Equal("El documento está cancelado y no se puede modificar.", respuesta.Mensaje);
        }

        [Fact]
        public async Task Eliminar_Ok_Y_ErrorDelDominio()
        {
            _domain.Setup(d => d.EliminarAsync(5)).ReturnsAsync(true);
            _domain.Setup(d => d.EliminarAsync(6)).ThrowsAsync(new Exception("Cancele el documento (Cancelado='S') antes de eliminarlo."));

            var ok = await _app.EliminarAsync(5);
            var error = await _app.EliminarAsync(6);

            Assert.True(ok.Resultado);
            Assert.Equal("Registro eliminado correctamente.", ok.Mensaje);
            Assert.False(error.Resultado);
            Assert.Equal("Cancele el documento (Cancelado='S') antes de eliminarlo.", error.Mensaje);
        }

        [Fact]
        public async Task Obtener_MapeaElEncabezado()
        {
            _domain.Setup(d => d.ObtenerAsync(5)).ReturnsAsync(new NotaDebito { Entry = 5, NumDoc = 9, CodigoSn = "SN1" });

            var respuesta = await _app.ObtenerAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal(9, respuesta.Dato.NumDoc);
        }

        [Fact]
        public async Task ObtenerTodo_MapeaTodos()
        {
            var lista = new List<NotaDebito> { new() { Entry = 1 }, new() { Entry = 2 } };
            _domain.Setup(d => d.ObtenerTodoAsync()).ReturnsAsync(lista.AsAsyncQueryable());

            var respuesta = await _app.ObtenerTodoAsync();

            Assert.True(respuesta.Resultado);
            Assert.Equal(new[] { 1, 2 }, respuesta.Dato.Select(n => n.Entry));
        }

        [Fact]
        public async Task ObtenerLineas_MapeaLasLineas()
        {
            _domain.Setup(d => d.ObtenerLineasAsync(5)).ReturnsAsync(new List<NotaDebitoDetalle>
            {
                new() { Entry = 5, NoLinea = 1, Descripcion = "A", CodigoImpuesto = "IVA" }
            });

            var respuesta = await _app.ObtenerLineasAsync(5);

            Assert.True(respuesta.Resultado);
            Assert.Equal("A", Assert.Single(respuesta.Dato).Descripcion);
        }

        [Fact]
        public async Task ObtenerFacturasBase_MapeaElResumen()
        {
            _domain.Setup(d => d.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(new List<Factura>
            {
                new() { Entry = 12, NumDoc = 100, FechaDoc = new DateTime(2026, 10, 1), TotalDoc = 112m, MonedaDoc = "QTZ", CodigoSn = "SN1" }
            });

            var respuesta = await _app.ObtenerFacturasBaseAsync("SN1");

            var f = Assert.Single(respuesta.Dato);
            Assert.Equal(12, f.Entry);
            Assert.Equal(100, f.NumDoc);
            Assert.Equal(112m, f.TotalDoc);
            Assert.Equal("QTZ", f.MonedaDoc);
        }

        [Fact]
        public async Task ObtenerFacturaBase_ConvierteLasLineasEnConceptos()
        {
            var factura = new Factura { Entry = 12, NumDoc = 100, CodigoSn = "SN1", NombreSn = "Cliente", MonedaDoc = "QTZ", Direccion = "Zona 1" };
            var lineas = new List<FacturaDetalle>
            {
                new() { Entry = 12, NoLinea = 1, CodArticulo = "ART1", Descripcion = "Tornillo", Cantidad = 3m, Precio = 5m, PrecioBruto = 15m, PrctjeDesc = 0m, CodigoImpuesto = "IVA", Impuesto = 1.8m, TotalLinea = 16.8m },
                new() { Entry = 12, NoLinea = 2, CodArticulo = "ART2", Descripcion = "  ", Cantidad = 1m, Precio = 10m, CodigoImpuesto = "IVA" }
            };
            _domain.Setup(d => d.ObtenerFacturaBaseAsync(12)).ReturnsAsync(new FacturaBase(factura, lineas));

            var respuesta = await _app.ObtenerFacturaBaseAsync(12);

            Assert.True(respuesta.Resultado);
            var dato = respuesta.Dato;
            Assert.Equal((12, 100, "SN1", "Cliente", "QTZ", "Zona 1"), (dato.Entry, dato.NumDoc, dato.CodigoSn, dato.NombreSn, dato.MonedaDoc, dato.Direccion));
            Assert.Equal(2, dato.Lineas.Count);
            Assert.Equal("Tornillo", dato.Lineas[0].Descripcion);
            Assert.Equal(16.8m, dato.Lineas[0].TotalLinea);
            Assert.Equal("ART2", dato.Lineas[1].Descripcion);   // sin descripcion: usa el codigo del articulo
        }

        [Fact]
        public async Task ObtenerFacturaBase_ErrorDelDominio_DevuelveElMensaje()
        {
            _domain.Setup(d => d.ObtenerFacturaBaseAsync(777)).ThrowsAsync(new Exception("La factura no existe."));

            var respuesta = await _app.ObtenerFacturaBaseAsync(777);

            Assert.False(respuesta.Resultado);
            Assert.Equal("La factura no existe.", respuesta.Mensaje);
        }
    }
}
```

- [ ] **Step 2: Pruebas del controller del cliente**

Crear `API.Service.WebApi.Tests/Controllers/NotaDebitoControllerTests.cs`:

```csharp
using System.Security.Claims;
using API.Application.DTO;
using API.Application.DTO.notaDebito;
using API.Application.Interface;
using API.Service.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace API.Service.WebApi.Tests.Controllers
{
    public class NotaDebitoControllerTests
    {
        private readonly Mock<INotaDebitoApplication> _app = new();

        private NotaDebitoController Crear(params Claim[] claims) => new(_app.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };

        private static void AssertForbidden(ObjectResult? resultado)
        {
            Assert.NotNull(resultado);
            Assert.Equal(StatusCodes.Status403Forbidden, resultado!.StatusCode);
            var cuerpo = Assert.IsType<Respuesta<object>>(resultado.Value);
            Assert.Equal("No tenés permiso para realizar esta acción.", cuerpo.Mensaje);
        }

        [Fact]
        public async Task Obtener_BadRequest_CuandoResultadoEsFalso()
        {
            var respuesta = new Respuesta<NotaDebitoDTO> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await Crear().Obtener(1);

            Assert.Same(respuesta, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Obtener_NotFound_CuandoDatoEsNulo()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().Obtener(1);

            var notFound = Assert.IsType<NotFoundObjectResult>(resultado.Result);
            var valor = Assert.IsType<Respuesta<NotaDebitoDTO>>(notFound.Value);
            Assert.False(valor.Resultado);
            Assert.Equal("El código de la nota de débito no se encontró.", valor.Mensaje);
        }

        [Fact]
        public async Task Obtener_Ok_CuandoExiste()
        {
            var respuesta = new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = new NotaDebitoDTO { Entry = 1 } };
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(respuesta);

            var resultado = await Crear().Obtener(1);

            Assert.Same(respuesta, Assert.IsType<OkObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task ObtenerTodo_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaDebitoDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaDebitoDTO>> { Resultado = true, Dato = new[] { new NotaDebitoDTO() } };
            _app.SetupSequence(a => a.ObtenerTodoAsync()).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerTodoAsync()).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerTodoAsync()).Result).Value);
        }

        [Fact]
        public async Task ObtenerLineas_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaDebitoDetalleDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaDebitoDetalleDTO>> { Resultado = true, Dato = new[] { new NotaDebitoDetalleDTO() } };
            _app.SetupSequence(a => a.ObtenerLineasAsync(5)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerLineas(5)).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturasBase_BadRequestYOk()
        {
            var mala = new Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>> { Resultado = true, Dato = new[] { new NotaDebitoFacturaBaseResumenDTO() } };
            _app.SetupSequence(a => a.ObtenerFacturasBaseAsync("SN1")).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturasBase("SN1")).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturasBase("SN1")).Result).Value);
        }

        [Fact]
        public async Task ObtenerFacturaBase_BadRequestYOk()
        {
            var mala = new Respuesta<NotaDebitoFacturaBaseDTO> { Resultado = false, Mensaje = "La factura no existe." };
            var buena = new Respuesta<NotaDebitoFacturaBaseDTO> { Resultado = true, Dato = new NotaDebitoFacturaBaseDTO { Entry = 12 } };
            _app.SetupSequence(a => a.ObtenerFacturaBaseAsync(12)).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().ObtenerFacturaBase(12)).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().ObtenerFacturaBase(12)).Result).Value);
        }

        [Fact]
        public async Task Insertar_BadRequestYOk()
        {
            var mala = new Respuesta<int> { Resultado = false, Mensaje = "error" };
            var buena = new Respuesta<int> { Resultado = true, Dato = 7 };
            _app.SetupSequence(a => a.InsertarAsync(It.IsAny<NotaDebitoCrearDTO>())).ReturnsAsync(mala).ReturnsAsync(buena);

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().InsertarAsync(new NotaDebitoCrearDTO())).Result).Value);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().InsertarAsync(new NotaDebitoCrearDTO())).Result).Value);
        }

        private void NotaExistente(int id = 1) =>
            _app.Setup(a => a.ObtenerAsync(id)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = new NotaDebitoDTO { Entry = id } });

        [Fact]
        public async Task Actualizar_NotFound_CuandoNoExiste()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = null! });

            var resultado = await Crear().ActualizarAsync(1, new NotaDebitoActualizarDTO());

            Assert.IsType<NotFoundObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Actualizar_BadRequest_CuandoLaAplicacionFalla()
        {
            NotaExistente();
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "error" };
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(mala);

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAU")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Comentario = "x" });

            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        }

        [Fact]
        public async Task Actualizar_CancelarSinEliminar_Devuelve403YNoActualiza()
        {
            NotaExistente();

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAU")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "S" });

            AssertForbidden(resultado.Result as ObjectResult);
            _app.Verify(a => a.ActualizarAsync(It.IsAny<int>(), It.IsAny<NotaDebitoActualizarDTO>()), Times.Never);
        }

        [Fact]
        public async Task Actualizar_CancelarConEliminar_Actualiza()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAUE")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
            _app.Verify(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>()), Times.Once);
        }

        [Fact]
        public async Task Actualizar_CancelarComoSuperUsuario_Actualiza()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "S")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "S" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Actualizar_SinCancelar_NoExigeEliminar()
        {
            NotaExistente();
            _app.Setup(a => a.ActualizarAsync(1, It.IsAny<NotaDebitoActualizarDTO>())).ReturnsAsync(new Respuesta<bool> { Resultado = true, Dato = true });

            var resultado = await Crear(new Claim("SuperUsuario", "N"), new Claim("perm", "NotasDebito:VAU")).ActualizarAsync(1, new NotaDebitoActualizarDTO { Cancelado = "N", Comentario = "x" });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        [Fact]
        public async Task Eliminar_NotFound_BadRequest_Ok()
        {
            _app.Setup(a => a.ObtenerAsync(1)).ReturnsAsync(new Respuesta<NotaDebitoDTO> { Resultado = true, Dato = null! });
            Assert.IsType<NotFoundObjectResult>((await Crear().EliminarAsync(1)).Result);

            NotaExistente(2);
            var mala = new Respuesta<bool> { Resultado = false, Mensaje = "Cancele el documento (Cancelado='S') antes de eliminarlo." };
            _app.Setup(a => a.EliminarAsync(2)).ReturnsAsync(mala);
            Assert.Same(mala, Assert.IsType<BadRequestObjectResult>((await Crear().EliminarAsync(2)).Result).Value);

            NotaExistente(3);
            var buena = new Respuesta<bool> { Resultado = true, Dato = true };
            _app.Setup(a => a.EliminarAsync(3)).ReturnsAsync(buena);
            Assert.Same(buena, Assert.IsType<OkObjectResult>((await Crear().EliminarAsync(3)).Result).Value);
        }
    }
}
```

- [ ] **Step 3: Ver el fallo** — `dotnet test API.sln --filter "FullyQualifiedName~NotaDebitoApplicationTests|FullyQualifiedName~NotaDebitoControllerTests"` no compila.

- [ ] **Step 4: Implementar `API.Application.Main/NotaDebitoApplication.cs`**

```csharp
using API.Application.DTO;
using API.Application.DTO.notaDebito;
using API.Application.Interface;
using API.Domain.Entity.Models;
using API.Domain.Interface;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Application.Main
{
    public class NotaDebitoApplication : INotaDebitoApplication
    {
        private readonly INotaDebitoDomain _domain;
        private readonly IMapper _mapper;

        public NotaDebitoApplication(INotaDebitoDomain domain, IMapper mapper)
        {
            _domain = domain;
            _mapper = mapper;
        }

        #region async methods
        public async Task<Respuesta<int>> InsertarAsync(NotaDebitoCrearDTO obj)
        {
            var respuesta = new Respuesta<int>();
            try
            {
                var nota = _mapper.Map<NotaDebito>(obj);
                var lineas = _mapper.Map<IEnumerable<NotaDebitoDetalle>>(obj.Lineas);
                respuesta.Dato = await _domain.InsertarAsync(nota, lineas);
                respuesta.Resultado = true;
                respuesta.Mensaje = "Registro agregado correctamente.";
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> ActualizarAsync(int id, NotaDebitoActualizarDTO obj)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                var nota = _mapper.Map<NotaDebito>(obj);
                respuesta.Dato = await _domain.ActualizarAsync(id, nota);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro actualizado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<bool>> EliminarAsync(int id)
        {
            var respuesta = new Respuesta<bool>();
            try
            {
                respuesta.Dato = await _domain.EliminarAsync(id);
                if (respuesta.Dato)
                {
                    respuesta.Resultado = true;
                    respuesta.Mensaje = "Registro eliminado correctamente.";
                }
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<NotaDebitoDTO>> ObtenerAsync(int id)
        {
            var respuesta = new Respuesta<NotaDebitoDTO>();
            try
            {
                var nota = await _domain.ObtenerAsync(id);
                respuesta.Dato = _mapper.Map<NotaDebitoDTO>(nota);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoDTO>>> ObtenerTodoAsync()
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoDTO>>();
            try
            {
                var queryable = await _domain.ObtenerTodoAsync();
                var notas = await queryable.ToListAsync();
                respuesta.Dato = _mapper.Map<IEnumerable<NotaDebitoDTO>>(notas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>> ObtenerLineasAsync(int id)
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoDetalleDTO>>();
            try
            {
                var lineas = await _domain.ObtenerLineasAsync(id);
                respuesta.Dato = _mapper.Map<IEnumerable<NotaDebitoDetalleDTO>>(lineas);
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn)
        {
            var respuesta = new Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>();
            try
            {
                var facturas = await _domain.ObtenerFacturasBaseAsync(codigoSn);
                respuesta.Dato = facturas.Select(f => new NotaDebitoFacturaBaseResumenDTO
                {
                    Entry = f.Entry,
                    NumDoc = f.NumDoc,
                    FechaDoc = f.FechaDoc,
                    TotalDoc = f.TotalDoc,
                    MonedaDoc = f.MonedaDoc
                }).ToList();
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }

        public async Task<Respuesta<NotaDebitoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry)
        {
            var respuesta = new Respuesta<NotaDebitoFacturaBaseDTO>();
            try
            {
                var baseDoc = await _domain.ObtenerFacturaBaseAsync(entry);
                respuesta.Dato = new NotaDebitoFacturaBaseDTO
                {
                    Entry = baseDoc.Documento.Entry,
                    NumDoc = baseDoc.Documento.NumDoc,
                    CodigoSn = baseDoc.Documento.CodigoSn,
                    NombreSn = baseDoc.Documento.NombreSn,
                    MonedaDoc = baseDoc.Documento.MonedaDoc,
                    Direccion = baseDoc.Documento.Direccion,
                    // Las lineas de la factura se convierten en conceptos: sin articulo ni almacen.
                    Lineas = baseDoc.Lineas.Select(l => new NotaDebitoDetalleCrearDTO
                    {
                        Descripcion = string.IsNullOrWhiteSpace(l.Descripcion) ? (l.CodArticulo ?? string.Empty) : l.Descripcion,
                        Cantidad = l.Cantidad,
                        Precio = l.Precio,
                        PrecioBruto = l.PrecioBruto,
                        PrctjeDesc = l.PrctjeDesc,
                        CodigoImpuesto = l.CodigoImpuesto,
                        Impuesto = l.Impuesto,
                        TotalLinea = l.TotalLinea
                    }).ToList()
                };
                respuesta.Resultado = true;
            }
            catch (Exception ex)
            {
                respuesta.Mensaje = ex.Message;
            }
            return respuesta;
        }
        #endregion
    }
}
```

- [ ] **Step 5: Implementar `API.Service.WebApi/Controllers/NotaDebitoController.cs`**

```csharp
using API.Application.DTO;
using API.Application.DTO.notaDebito;
using API.Application.Interface;
using API.Domain.Interface;
using API.Service.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/NotaDebito")]
    public class NotaDebitoController : ControllerBase
    {
        private readonly INotaDebitoApplication _application;

        public NotaDebitoController(INotaDebitoApplication application)
        {
            _application = application;
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Respuesta<NotaDebitoDTO>>> Obtener([FromRoute] int id)
        {
            var nota = await _application.ObtenerAsync(id);

            if (!nota.Resultado)
            {
                return BadRequest(nota);
            }

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de débito no se encontró.";
                return NotFound(nota);
            }

            return Ok(nota);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaDebitoDTO>>>> ObtenerTodoAsync()
        {
            var notas = await _application.ObtenerTodoAsync();

            if (!notas.Resultado)
            {
                return BadRequest(notas);
            }

            return Ok(notas);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("{id:int}/Lineas")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>>> ObtenerLineas([FromRoute] int id)
        {
            var lineas = await _application.ObtenerLineasAsync(id);

            if (!lineas.Resultado)
            {
                return BadRequest(lineas);
            }

            return Ok(lineas);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("FacturasBase/{codigoSn}")]
        public async Task<ActionResult<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>>> ObtenerFacturasBase([FromRoute] string codigoSn)
        {
            var facturas = await _application.ObtenerFacturasBaseAsync(codigoSn);

            if (!facturas.Resultado)
            {
                return BadRequest(facturas);
            }

            return Ok(facturas);
        }

        [RequierePermiso("NotasDebito", Accion.Ver)]
        [HttpGet("FacturaBase/{entry:int}")]
        public async Task<ActionResult<Respuesta<NotaDebitoFacturaBaseDTO>>> ObtenerFacturaBase([FromRoute] int entry)
        {
            var factura = await _application.ObtenerFacturaBaseAsync(entry);

            if (!factura.Resultado)
            {
                return BadRequest(factura);
            }

            return Ok(factura);
        }

        [RequierePermiso("NotasDebito", Accion.Agregar)]
        [HttpPost]
        public async Task<ActionResult<Respuesta<int>>> InsertarAsync([FromBody] NotaDebitoCrearDTO obj)
        {
            var insert = await _application.InsertarAsync(obj);

            if (!insert.Resultado)
            {
                return BadRequest(insert);
            }

            return Ok(insert);
        }

        [RequierePermiso("NotasDebito", Accion.Actualizar)]
        [HttpPut("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> ActualizarAsync([FromRoute] int id, [FromBody] NotaDebitoActualizarDTO obj)
        {
            // Cancelar un documento (Cancelado = 'S') equivale a eliminarlo: exige Accion.Eliminar
            // ademas de Actualizar (spec de permisos: 'cancelar documento -> Eliminar').
            if (obj.Cancelado == "S" && !PermisoUsuario.Tiene(User, "NotasDebito", Accion.Eliminar))
                return PermisoUsuario.Denegado();

            var nota = await _application.ObtenerAsync(id);

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de débito no se encontró.";
                return NotFound(nota);
            }

            var update = await _application.ActualizarAsync(id, obj);

            if (!update.Resultado)
            {
                return BadRequest(update);
            }

            return Ok(update);
        }

        [RequierePermiso("NotasDebito", Accion.Eliminar)]
        [HttpDelete("{id:int}")]
        public async Task<ActionResult<Respuesta<bool>>> EliminarAsync([FromRoute] int id)
        {
            var nota = await _application.ObtenerAsync(id);

            if (nota.Dato == null)
            {
                nota.Resultado = false;
                nota.Mensaje = "El código de la nota de débito no se encontró.";
                return NotFound(nota);
            }

            var delete = await _application.EliminarAsync(id);

            if (!delete.Resultado)
            {
                return BadRequest(delete);
            }

            return Ok(delete);
        }
    }
}
```

- [ ] **Step 6: Registrar en `Startup.cs`**

Después de las líneas de `IFacturaDetalleApplication` (la última de las registraciones de Factura) agregar:

```csharp
            services.AddTransient<IRepositorioGenerico<NotaDebito, int>, NotaDebitoRepositorio>();
            services.AddTransient<IRepositorioGenerico<NotaDebitoDetalle, (int Entry, int NoLinea)>, NotaDebitoDetalleRepositorio>();
            services.AddTransient<INotaDebitoDomain, NotaDebitoDomain>();
            services.AddTransient<INotaDebitoApplication, NotaDebitoApplication>();

            services.AddTransient<IRepositorioGenerico<NotaDebitoCompra, int>, NotaDebitoCompraRepositorio>();
            services.AddTransient<IRepositorioGenerico<NotaDebitoCompraDetalle, (int Entry, int NoLinea)>, NotaDebitoCompraDetalleRepositorio>();
            services.AddTransient<INotaDebitoCompraDomain, NotaDebitoCompraDomain>();
            services.AddTransient<INotaDebitoCompraApplication, NotaDebitoCompraApplication>();
```

(Los `using` de `API.Domain.Core`, `API.Infraestructure.Repository`, `API.Application.Main`, `API.Application.Interface`, `API.Domain.Interface` y `API.Domain.Entity.Models` ya existen en el archivo porque las demás registraciones los usan.)

- [ ] **Step 7: Derivar aplicación, controller y pruebas del proveedor**

Desde la raíz del repo `API` (bash). Las reglas para el proveedor: renombrar tipos, nombres de método de base, rutas de base, módulo y namespace de DTO. Atención: `NotaDebitoFacturaBaseResumenDTO`/`NotaDebitoFacturaBaseDTO` contienen la palabra `FacturaBase`, por eso `FacturaBase` se renombra solo como palabra completa.

```bash
set -e
mirror2() {   # $1 = origen  $2 = destino
  sed -E \
    -e 's/ObtenerFacturasBase/ObtenerFacturasCompraBase/g' \
    -e 's/ObtenerFacturaBase/ObtenerFacturaCompraBase/g' \
    -e 's|"FacturasBase/|"FacturasCompraBase/|g' \
    -e 's|"FacturaBase/|"FacturaCompraBase/|g' \
    -e 's/NotaDebito/NotaDebitoCompra/g' \
    -e 's/NotasDebito/NotasDebitoCompra/g' \
    -e 's/\bFacturaBase\b/FacturaCompraBase/g' \
    -e 's/FacturaDetalle/FacturaCompraDetalle/g' \
    -e 's/\bFactura\b/FacturaCompra/g' \
    -e 's/API\.Application\.DTO\.notaDebito\b/API.Application.DTO.notaDebitoCompra/g' \
    -e 's/La factura no existe\./La factura de compra no existe./g' \
    "$1" > "$2"
}
mirror2 API.Application.Main/NotaDebitoApplication.cs API.Application.Main/NotaDebitoCompraApplication.cs
mirror2 API.Service.WebApi/Controllers/NotaDebitoController.cs API.Service.WebApi/Controllers/NotaDebitoCompraController.cs
mirror2 API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaDebitoCompraApplicationTests.cs
mirror2 API.Service.WebApi.Tests/Controllers/NotaDebitoControllerTests.cs API.Service.WebApi.Tests/Controllers/NotaDebitoCompraControllerTests.cs
```

Verificar a mano lo que el `sed` no garantiza:
1. En los 4 archivos generados no queda ningún `NotaDebito` sin `Compra` (`grep -n "NotaDebito[^C]" <archivo>` solo puede coincidir con el nombre `NotaDebitoCompra...` ya correcto; buscar específicamente `grep -nE "NotaDebito([^C]|$)"`: vacío) ni `notaDebito;` del namespace del cliente.
2. En `NotaDebitoCompraController.cs`: rutas `[HttpGet("FacturasCompraBase/{codigoSn}")]` y `[HttpGet("FacturaCompraBase/{entry:int}")]`, `[Route("api/NotaDebitoCompra")]`, módulo `"NotasDebitoCompra"` en todos los `[RequierePermiso]` y en `PermisoUsuario.Tiene`.
3. En `NotaDebitoCompraApplication.cs`, el método `ObtenerFacturaCompraBaseAsync` lee `baseDoc.Documento` y `baseDoc.Lineas` (propiedades del record `FacturaCompraBase`).
4. En `NotaDebitoCompraApplicationTests.cs`: `new FacturaCompra { ... }`, `new List<FacturaCompraDetalle>`, `new FacturaCompraBase(factura, lineas)`; el mensaje esperado `"La factura de compra no existe."`.

Registrar `NotaDebitoCompraApplication` en `Startup.cs` ya está hecho en el Step 6.

- [ ] **Step 8: Ejecutar y commit**

`dotnet test API.sln` → todo verde (reportar el total real). Si una prueba derivada falla por un literal que el `sed` no cubrió, corregirlo en el archivo derivado.

```bash
git add API.Application.Main/NotaDebitoApplication.cs API.Application.Main/NotaDebitoCompraApplication.cs API.Service.WebApi/Controllers/NotaDebitoController.cs API.Service.WebApi/Controllers/NotaDebitoCompraController.cs API.Service.WebApi/Startup.cs API.Service.WebApi.Tests/Application/NotaDebitoApplicationTests.cs API.Service.WebApi.Tests/Application/NotaDebitoCompraApplicationTests.cs API.Service.WebApi.Tests/Controllers/NotaDebitoControllerTests.cs API.Service.WebApi.Tests/Controllers/NotaDebitoCompraControllerTests.cs
git commit -m "feat(notas-debito): aplicacion, controllers y registro de DI de nota de debito cliente y proveedor

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 3 — Web

### Task 7: Clientes de API y DTOs de la Web

**Files:**
- Create: `Web.ApiClient/Dtos/NotaDebito/*.cs` (7 archivos), `Web.ApiClient/Dtos/NotaDebitoCompra/*.cs` (7 archivos)
- Create: `Web.ApiClient/Clientes/INotaDebitoApiClient.cs`, `NotaDebitoApiClient.cs`, `INotaDebitoCompraApiClient.cs`, `NotaDebitoCompraApiClient.cs`
- Modify: `Web.UI/Program.cs`

**Interfaces:**
- Consumes: las rutas REST del Task 6 y los DTOs de la API (mismos nombres de propiedad).
- Produces: `INotaDebitoApiClient` con `ObtenerTodoAsync()`, `ObtenerAsync(int entry)`, `InsertarAsync(NotaDebitoCrearDTO)`, `ActualizarAsync(int entry, NotaDebitoActualizarDTO)`, `EliminarAsync(int entry)`, `ObtenerLineasAsync(int entry)`, `ObtenerFacturasBaseAsync(string codigoSn)`, `ObtenerFacturaBaseAsync(int entry)`; y `INotaDebitoCompraApiClient` equivalente con `ObtenerFacturasCompraBaseAsync`/`ObtenerFacturaCompraBaseAsync`. DTOs en `Web.ApiClient.Dtos.NotaDebito` y `Web.ApiClient.Dtos.NotaDebitoCompra`.

**Precondición verificable:** en el repo Web, `git status --porcelain` lista únicamente los 4 archivos ajenos (`ArticuloCrearDTO.cs`, `Respuesta.cs`, `ArticulosController.cs`, `articulos.js`); `dotnet build Web.slnx` pasa; los DTOs de la API del Task 3 existen en `..\API\API.Application.DTO\notaDebito`.

- [ ] **Step 1: Generar los DTOs de la Web desde los de la API**

Desde la raíz del repo `Web` (bash). Cambia solo el namespace (el contenido es idéntico a los DTOs de la API; no se necesita `System.ComponentModel.DataAnnotations` adicional porque `NotaDebitoCrearDTO` ya lo importa):

```bash
set -e
api=../API/API.Application.DTO
for par in "notaDebito:NotaDebito" "notaDebitoCompra:NotaDebitoCompra"; do
  carpetaApi="${par%%:*}"; nombre="${par##*:}"
  destino="Web.ApiClient/Dtos/$nombre"
  mkdir -p "$destino"
  for f in "$api/$carpetaApi"/*.cs; do
    sed "s/namespace API\.Application\.DTO\.$carpetaApi/namespace Web.ApiClient.Dtos.$nombre/" "$f" > "$destino/$(basename "$f")"
  done
done
grep -rn "API.Application" Web.ApiClient/Dtos/NotaDebito Web.ApiClient/Dtos/NotaDebitoCompra && echo "FALLO" || echo OK
ls Web.ApiClient/Dtos/NotaDebito Web.ApiClient/Dtos/NotaDebitoCompra
```

Esperado: `OK` y 7 archivos por carpeta (`NotaDebitoDTO`, `NotaDebitoCrearDTO`, `NotaDebitoActualizarDTO`, `NotaDebitoDetalleDTO`, `NotaDebitoDetalleCrearDTO`, `NotaDebitoFacturaBaseResumenDTO`, `NotaDebitoFacturaBaseDTO` y sus equivalentes `NotaDebitoCompra...`).

- [ ] **Step 2: Clientes**

`Web.ApiClient/Clientes/INotaDebitoApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.NotaDebito;

namespace Web.ApiClient.Clientes
{
    public interface INotaDebitoApiClient
    {
        Task<Respuesta<IEnumerable<NotaDebitoDTO>>> ObtenerTodoAsync();
        Task<Respuesta<NotaDebitoDTO>> ObtenerAsync(int entry);
        Task<Respuesta<int>> InsertarAsync(NotaDebitoCrearDTO dto);
        Task<Respuesta<bool>> ActualizarAsync(int entry, NotaDebitoActualizarDTO dto);
        Task<Respuesta<bool>> EliminarAsync(int entry);
        Task<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>> ObtenerLineasAsync(int entry);
        Task<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn);
        Task<Respuesta<NotaDebitoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry);
    }
}
```

`Web.ApiClient/Clientes/NotaDebitoApiClient.cs`:

```csharp
using Web.ApiClient.Dtos;
using Web.ApiClient.Dtos.NotaDebito;

namespace Web.ApiClient.Clientes
{
    public class NotaDebitoApiClient : ApiClientBase, INotaDebitoApiClient
    {
        private const string Recurso = "api/NotaDebito";

        public NotaDebitoApiClient(HttpClient http) : base(http) { }

        public Task<Respuesta<IEnumerable<NotaDebitoDTO>>> ObtenerTodoAsync() =>
            GetAsync<IEnumerable<NotaDebitoDTO>>(Recurso);

        public Task<Respuesta<NotaDebitoDTO>> ObtenerAsync(int entry) =>
            GetAsync<NotaDebitoDTO>($"{Recurso}/{entry}");

        public Task<Respuesta<int>> InsertarAsync(NotaDebitoCrearDTO dto) =>
            PostAsync<int>(Recurso, dto);

        public Task<Respuesta<bool>> ActualizarAsync(int entry, NotaDebitoActualizarDTO dto) =>
            PutAsync<bool>($"{Recurso}/{entry}", dto);

        public Task<Respuesta<bool>> EliminarAsync(int entry) =>
            DeleteAsync<bool>($"{Recurso}/{entry}");

        public Task<Respuesta<IEnumerable<NotaDebitoDetalleDTO>>> ObtenerLineasAsync(int entry) =>
            GetAsync<IEnumerable<NotaDebitoDetalleDTO>>($"{Recurso}/{entry}/Lineas");

        public Task<Respuesta<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>> ObtenerFacturasBaseAsync(string codigoSn) =>
            GetAsync<IEnumerable<NotaDebitoFacturaBaseResumenDTO>>($"{Recurso}/FacturasBase/{Uri.EscapeDataString(codigoSn)}");

        public Task<Respuesta<NotaDebitoFacturaBaseDTO>> ObtenerFacturaBaseAsync(int entry) =>
            GetAsync<NotaDebitoFacturaBaseDTO>($"{Recurso}/FacturaBase/{entry}");
    }
}
```

Proveedor: generarlos con este sed desde la raíz de `Web`:

```bash
set -e
for f in INotaDebitoApiClient NotaDebitoApiClient; do
  destino="Web.ApiClient/Clientes/${f/NotaDebito/NotaDebitoCompra}.cs"
  sed -E \
    -e 's/ObtenerFacturasBaseAsync/ObtenerFacturasCompraBaseAsync/g' \
    -e 's/ObtenerFacturaBaseAsync/ObtenerFacturaCompraBaseAsync/g' \
    -e 's|/FacturasBase/|/FacturasCompraBase/|g' \
    -e 's|/FacturaBase/|/FacturaCompraBase/|g' \
    -e 's/NotaDebito/NotaDebitoCompra/g' \
    "Web.ApiClient/Clientes/$f.cs" > "$destino"
done
grep -n "Recurso =\|FacturasCompraBase\|FacturaCompraBase" Web.ApiClient/Clientes/NotaDebitoCompraApiClient.cs
```

Esperado: `api/NotaDebitoCompra`, `.../FacturasCompraBase/{...}` y `.../FacturaCompraBase/{entry}`; la interfaz `INotaDebitoCompraApiClient` en el otro archivo.

- [ ] **Step 3: Registro en `Program.cs`**

Después del registro de `IFacturaCompraDetalleApiClient` (y su `.AddHttpMessageHandler<JwtAuthorizationHandler>();`) agregar:

```csharp
builder.Services.AddHttpClient<INotaDebitoApiClient, NotaDebitoApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
builder.Services.AddHttpClient<INotaDebitoCompraApiClient, NotaDebitoCompraApiClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthorizationHandler>();
```

(Conservar el fin de línea del archivo; `Program.cs` es CRLF.)

- [ ] **Step 4: Build y commit**

`dotnet build Web.slnx` con el `BaseOutputPath` de la cabecera → 0 errores (las 4 advertencias de los archivos ajenos son esperadas).

```bash
git add Web.ApiClient/Dtos/NotaDebito Web.ApiClient/Dtos/NotaDebitoCompra Web.ApiClient/Clientes/INotaDebitoApiClient.cs Web.ApiClient/Clientes/NotaDebitoApiClient.cs Web.ApiClient/Clientes/INotaDebitoCompraApiClient.cs Web.ApiClient/Clientes/NotaDebitoCompraApiClient.cs Web.UI/Program.cs
git commit -m "feat(notas-debito): clientes de API y DTOs de nota de debito en la Web

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```


### Task 8: Pantalla Web de Notas de débito de cliente

**Files:**
- Create: `Web.UI/Controllers/NotasDebitoController.cs`, `Web.UI/Views/NotasDebito/Index.cshtml`, `Web.UI/Views/NotasDebito/_Form.cshtml`, `Web.UI/wwwroot/js/notasdebito.js`

**Interfaces:**
- Consumes: `INotaDebitoApiClient` y DTOs (Task 7); `ISocioNegocioApiClient` (`ObtenerTodoAsync(string tipo)`, `ObtenerContenganNombreAsync(string texto, string tipo)`), `IMonedaApiClient.ObtenerTodoAsync()`, `IImpuestoApiClient` (`ObtenerTodoAsync()`, `ObtenerContenganNombreAsync(string)`, `ObtenerAsync(string)`), `INumeracionDocumentoDetApiClient` (`ObtenerPorDocumentoAsync(string)`, `ObtenerAsync(int)`), `INumeracionDocumentoApiClient.ObtenerTodoAsync()` (existentes, los usa `FacturasController`); `App.*` de `wwwroot/js/site.js` (`autocompletar`, `recolectarFormulario`, `enviarJson`, `eliminar`, `confirmarEliminar`, `mostrarError`, `mostrarExito`, `dataSrcTabla`, `datatableEsEs`); `Accion`/`RequierePermiso`/`User.TienePermiso` de `Web.UI.Authorization`.
- Produces: rutas MVC `NotasDebito/{Index, ObtenerTodos, FormularioCrear, FormularioEditar, Crear, Editar, Eliminar, ObtenerDetalle, BuscarSocios, BuscarImpuestos, ObtenerImpuestoPorCodigo, BuscarFacturasBase, ObtenerFacturaBase}` con los atributos de permiso indicados abajo. Constantes propias del documento en el controller y en el JS (la nota de proveedor las cambia en el Task 9).

**Precondición verificable:** Task 7 commiteado; `grep -n "ObtenerContenganNombreAsync\|ObtenerTodoAsync" Web.ApiClient/Clientes/ISocioNegocioApiClient.cs Web.ApiClient/Clientes/IImpuestoApiClient.cs` confirma las firmas de arriba; `git status --porcelain` lista solo los 4 archivos ajenos.

- [ ] **Step 1: Controller**

`Web.UI/Controllers/NotasDebitoController.cs`:

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
        private readonly IImpuestoApiClient _impuestos;
        private readonly INumeracionDocumentoDetApiClient _series;
        private readonly INumeracionDocumentoApiClient _numeracion;

        // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
        // CodigoObj de NumeracionDocumento que identifica a "Notas de debito de cliente" como tipo de objeto.
        private const string CodigoObjNotaDebito = "7";
        private const string SubTipoDocNotaDebito = "--";
        private const string TipoSocio = "C";
        // ---- fin de constantes ----

        public NotasDebitoController(
            INotaDebitoApiClient notas,
            ISocioNegocioApiClient socios,
            IMonedaApiClient monedas,
            IImpuestoApiClient impuestos,
            INumeracionDocumentoDetApiClient series,
            INumeracionDocumentoApiClient numeracion)
        {
            _notas = notas;
            _socios = socios;
            _monedas = monedas;
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
                EstadoDoc = respuesta.Dato.EstadoDoc,
                TipoObjeto = respuesta.Dato.TipoObjeto,
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
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> BuscarFacturasBase(string codigoSn)
        {
            var respuesta = await _notas.ObtenerFacturasBaseAsync(codigoSn ?? string.Empty);
            return Json(respuesta);
        }

        [HttpGet]
        [RequierePermiso("NotasDebito", Accion.Ver)]
        public async Task<IActionResult> ObtenerFacturaBase(int entry)
        {
            var respuesta = await _notas.ObtenerFacturaBaseAsync(entry);
            return Json(respuesta);
        }

        private async Task CargarDropdownsAsync()
        {
            // Socio de negocio e Impuesto no se cargan como lista completa: el buscador con
            // autocompletado los consulta bajo demanda. Moneda es un <select> normal.
            var monedas = await _monedas.ObtenerTodoAsync();
            ViewBag.Monedas = new SelectList(monedas.Dato ?? [], "Codigo", "Nombre");
        }
    }
}
```

- [ ] **Step 2: `Views/NotasDebito/Index.cshtml`**

```html
@{
    ViewData["Title"] = "Notas de débito";
}

<div class="d-flex justify-content-between align-items-center mb-3">
    <h3 class="mb-0">Notas de débito</h3>
    @if (User.TienePermiso("NotasDebito", Accion.Agregar))
    {
        <button type="button" class="btn btn-primary" id="btnNuevo">
            <i class="fa-solid fa-plus me-1"></i>Nuevo
        </button>
    }
</div>

<div class="card card-modulo">
    <div class="card-body">
        <div class="table-responsive">
            <table id="tblNotasDebito" class="table table-hover align-middle w-100">
                <thead>
                    <tr>
                        <th>No. Documento</th>
                        <th>Socio de negocio</th>
                        <th>Fecha</th>
                        <th>Estado</th>
                        <th>Total</th>
                        <th class="text-end">Acciones</th>
                    </tr>
                </thead>
                <tbody></tbody>
            </table>
        </div>
    </div>
</div>

<div class="modal fade" id="modalFormulario" tabindex="-1" aria-hidden="true">
    <div class="modal-dialog modal-xl modal-dialog-scrollable">
        <div class="modal-content" id="contenidoModal">
            <!-- se carga por AJAX -->
        </div>
    </div>
</div>

<script id="datosPermisosNotasDebito" type="application/json">
    { "actualizar": @(User.TienePermiso("NotasDebito", Accion.Actualizar) ? "true" : "false"),
      "eliminar": @(User.TienePermiso("NotasDebito", Accion.Eliminar) ? "true" : "false") }
</script>

@section Scripts {
    <script src="~/js/notasdebito.js" asp-append-version="true"></script>
}
```

- [ ] **Step 3: `Views/NotasDebito/_Form.cshtml`**

```html
@using System.Text.Json
@model Web.ApiClient.Dtos.NotaDebito.NotaDebitoCrearDTO
@{
    bool esEdicion = ViewBag.EsEdicion ?? false;
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
                <input asp-for="PrctjeDesc" type="number" step="0.01" class="form-control" readonly="@esEdicion" />
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
                <textarea asp-for="Comentario" class="form-control" rows="2"></textarea>
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

    <div class="table-responsive">
        <table id="tblDetalleNotaDebito" class="table table-sm table-hover align-middle w-100" data-entry="@ViewBag.EntryActual" data-es-edicion="@esEdicion.ToString().ToLower()">
            <thead>
                <tr>
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
                <div class="col-md-8">
                    <label class="form-label">Descripción del concepto</label>
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
    @{
        var puedeGuardar = esEdicion ? User.TienePermiso("NotasDebito", Accion.Actualizar) : User.TienePermiso("NotasDebito", Accion.Agregar);
    }
    @if ((Model.Cancelado ?? "N") != "S" && puedeGuardar)
    {
        <button type="button" class="btn btn-primary" id="btnGuardarNotaDebito" data-edicion="@esEdicion.ToString().ToLower()" data-entry="@ViewBag.EntryActual">
            <i class="fa-solid fa-floppy-disk me-1"></i>Guardar
        </button>
    }
</div>
```

- [ ] **Step 4: `wwwroot/js/notasdebito.js`**

```javascript
$(function () {
    // ---- Constantes propias del documento (en la nota de debito de proveedor cambian estas lineas) ----
    const TIPO_BASE = 6;   // BaseTipo de la factura base: 6 = Factura de venta
    // ---- fin de constantes ----

    const esc = v => $('<div>').text(v ?? '').html();

    const datosPermisosEl = document.getElementById('datosPermisosNotasDebito');
    const permisos = datosPermisosEl ? JSON.parse(datosPermisosEl.textContent) : { actualizar: true, eliminar: true };

    const tabla = $('#tblNotasDebito').DataTable({
        ajax: { url: '/NotasDebito/ObtenerTodos', dataSrc: App.dataSrcTabla },
        columns: [
            { data: 'numDoc' },
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
        cargarFacturasBase();
    }

    function cerrarModal() {
        bootstrap.Modal.getOrCreateInstance(document.getElementById('modalFormulario')).hide();
    }

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
        const confirmado = await App.confirmarEliminar('Se cancelará este documento. Esta acción no se puede deshacer.');
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

        // El número de documento no se pide para series no manuales: el servidor lo calcula al registrar.
        const datos = App.recolectarFormulario('#formNotaDebito');
        datos.Serie = $('#selectSerieNotaDebito').val();

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

        const respuesta = await $.get('/NotasDebito/BuscarFacturasBase', { codigoSn });
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

        lineasLocales = (factura.lineas || []).map(l => ({
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
        pintarDetalle();
    });

    $(document).on('click', '#btnQuitarFacturaBase', quitarFacturaBase);

    // --- Detalle: en creación se administra localmente; en edición es de solo lectura ---

    let lineasLocales = [];
    let lineasRemotas = [];
    let proximoIdLocal = 1;
    let lineaEnEdicionId = null;
    let tasaImpuestoSeleccionado = 0;
    let buscadorImpuesto;

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

        if ($('#tblDetalleNotaDebito').length === 0) return;

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

        const totales = calcularTotalesDesdeLineas(lista);
        $('#TotalBruto').val(totales.totalBruto);
        $('#TotalImp').val(totales.totalImp);
        $('#TotalDoc').val(totales.totalDoc);

        if (lista.length === 0) {
            $tbody.html('<tr><td colspan="7" class="text-center text-muted">Sin líneas de detalle</td></tr>');
            return;
        }

        $tbody.html(lista.map(linea => {
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
            return `
                <tr>
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

    function limpiarPanelLinea() {
        buscadorImpuesto.establecer(null);
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

        const codigoImpuesto = linea.CodigoImpuesto ?? linea.codigoImpuesto ?? '';
        if (codigoImpuesto) {
            const respuestaImpuesto = await $.get('/NotasDebito/ObtenerImpuestoPorCodigo', { codigo: codigoImpuesto });
            buscadorImpuesto.establecer(respuestaImpuesto.resultado && respuestaImpuesto.dato
                ? respuestaImpuesto.dato
                : { codigo: codigoImpuesto, nombre: codigoImpuesto, tasa: 0 });
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
        pintarDetalle();
    });

    $(document).on('click', '#btnGuardarLinea', function () {
        const descripcion = ($('#detDescripcion').val() || '').trim();
        const codigoImpuesto = $('#detCodigoImpuesto').val() || '';
        const cantidad = Number($('#detCantidad').val());
        const precioTexto = $('#detPrecio').val();
        const prctjeDesc = Number($('#detPrctjeDesc').val()) || 0;

        if (!descripcion) {
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
            Descripcion: descripcion,
            Cantidad: cantidad,
            Precio: precio,
            PrecioBruto: Number((cantidad * precio).toFixed(2)),
            PrctjeDesc: prctjeDesc,
            CodigoImpuesto: codigoImpuesto,
            Impuesto: Number($('#detImpuestoMonto').val()) || 0,
            TotalLinea: Number($('#detTotalLinea').val()) || 0
        };

        if (lineaEnEdicionId !== null) {
            lineasLocales = lineasLocales.map(l => l._id === lineaEnEdicionId ? { ...linea, _id: l._id } : l);
        } else {
            lineasLocales.push({ ...linea, _id: proximoIdLocal++ });
        }

        $('#panelLineaDetalle').addClass('d-none');
        pintarDetalle();
    });
});
```

- [ ] **Step 5: Verificación**

1. `node --check Web.UI/wwwroot/js/notasdebito.js` sin errores.
2. `dotnet build Web.slnx` con el `BaseOutputPath` de la cabecera → 0 errores (Razor se compila en este SDK; los `_Form.cshtml` e `Index.cshtml` con error de sintaxis rompen el build).
3. Comprobar que el árbol **commiteado** compila sin el trabajo ajeno (después del commit del Step 6): `git worktree add --detach <scratchpad>\wt-check HEAD`, compilar allí con otro `BaseOutputPath` y `git worktree remove --force <scratchpad>\wt-check`.

- [ ] **Step 6: Commit**

```bash
git add Web.UI/Controllers/NotasDebitoController.cs Web.UI/Views/NotasDebito/Index.cshtml Web.UI/Views/NotasDebito/_Form.cshtml Web.UI/wwwroot/js/notasdebito.js
git commit -m "feat(notas-debito): pantalla de notas de debito de cliente (lista, formulario, factura base)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Pantalla Web de Notas de débito de proveedor (derivada)

**Files:**
- Create: `Web.UI/Controllers/NotasDebitoCompraController.cs`, `Web.UI/Views/NotasDebitoCompra/Index.cshtml`, `Web.UI/Views/NotasDebitoCompra/_Form.cshtml`, `Web.UI/wwwroot/js/notasdebitocompra.js`

**Interfaces:**
- Consumes: archivos del Task 8; `INotaDebitoCompraApiClient` y DTOs `Web.ApiClient.Dtos.NotaDebitoCompra` (Task 7: métodos `ObtenerFacturasCompraBaseAsync`/`ObtenerFacturaCompraBaseAsync`).
- Produces: rutas MVC `NotasDebitoCompra/*` equivalentes (incluyen `BuscarFacturasBase`/`ObtenerFacturaBase`, que en el controller de proveedor llaman a los métodos `...Compra...` del cliente), módulo de permisos `NotasDebitoCompra`.

**Precondición verificable:** Task 8 commiteado; `grep -c "NotasDebito" Web.UI/Views/NotasDebito/_Form.cshtml` > 0.

- [ ] **Step 1: Derivar los 4 archivos**

Desde la raíz del repo `Web` (bash). Orden: primero los valores propios del documento, luego los renombres estructurales.

```bash
set -e
mkdir -p Web.UI/Views/NotasDebitoCompra
mirror3() {   # $1 = origen  $2 = destino
  sed -E \
    -e 's|private const string CodigoObjNotaDebito = "7";|private const string CodigoObjNotaDebito = "14";|' \
    -e 's|private const string TipoSocio = "C";|private const string TipoSocio = "P";|' \
    -e 's|const TIPO_BASE = 6;   // BaseTipo de la factura base: 6 = Factura de venta|const TIPO_BASE = 13;   // BaseTipo de la factura base: 13 = Factura de compra|' \
    -e 's/Model\.BaseTipo == 6/Model.BaseTipo == 13/g' \
    -e 's/Notas de débito/Notas de débito de proveedor/g' \
    -e 's/Nueva nota de débito/Nueva nota de débito de proveedor/g' \
    -e 's/Editar nota de débito/Editar nota de débito de proveedor/g' \
    -e 's/Nota de débito (creada|actualizada|eliminada)/Nota de débito de proveedor \1/g' \
    -e 's/la nota de débito #/la nota de débito de proveedor #/g' \
    -e 's/Basado en factura \(opcional\)/Basado en factura de compra (opcional)/g' \
    -e 's/Selecciona un socio para ver sus facturas/Selecciona un proveedor para ver sus facturas de compra/g' \
    -e 's/Sin factura base: puedes crear la nota libremente\./Sin factura de compra base: puedes crear la nota libremente./g' \
    -e 's/Basada en la factura #/Basada en la factura de compra #/g' \
    -e 's/Las líneas actuales se reemplazarán por las de la factura seleccionada\./Las líneas actuales se reemplazarán por las de la factura de compra seleccionada./g' \
    -e 's/Factura \(entrada /Factura de compra (entrada /g' \
    -e 's/Selecciona un socio de negocio\./Selecciona un proveedor./g' \
    -e 's/Sin factura base --/Sin factura de compra base --/g' \
    -e 's/ObtenerFacturasBaseAsync/ObtenerFacturasCompraBaseAsync/g' \
    -e 's/ObtenerFacturaBaseAsync/ObtenerFacturaCompraBaseAsync/g' \
    -e 's/NotaDebito/NotaDebitoCompra/g' \
    -e 's/NotasDebito/NotasDebitoCompra/g' \
    -e 's/notasdebito\.js/notasdebitocompra.js/g' \
    "$1" > "$2"
}
mirror3 Web.UI/Controllers/NotasDebitoController.cs Web.UI/Controllers/NotasDebitoCompraController.cs
mirror3 Web.UI/Views/NotasDebito/Index.cshtml Web.UI/Views/NotasDebitoCompra/Index.cshtml
mirror3 Web.UI/Views/NotasDebito/_Form.cshtml Web.UI/Views/NotasDebitoCompra/_Form.cshtml
mirror3 Web.UI/wwwroot/js/notasdebito.js Web.UI/wwwroot/js/notasdebitocompra.js
```

- [ ] **Step 2: Revisar lo generado**

Comprobar con estos greps (todos deben cumplir lo indicado):

```bash
grep -n 'CodigoObjNotaDebitoCompra = \|TipoSocio = ' Web.UI/Controllers/NotasDebitoCompraController.cs     # "14" y "P"
grep -n 'TIPO_BASE =' Web.UI/wwwroot/js/notasdebitocompra.js                                                # 13
grep -n 'Model.BaseTipo ==' Web.UI/Views/NotasDebitoCompra/_Form.cshtml                                      # 13
grep -n 'ObtenerFacturas\|ObtenerFactura' Web.UI/Controllers/NotasDebitoCompraController.cs                 # ...CompraBaseAsync del cliente
grep -rn 'NotasDebito"\|NotasDebito/' Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Views/NotasDebitoCompra Web.UI/wwwroot/js/notasdebitocompra.js   # vacío (solo NotasDebitoCompra)
grep -rnE 'NotaDebito([^C]|$)' Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Views/NotasDebitoCompra Web.UI/wwwroot/js/notasdebitocompra.js        # vacío
```

Casos que revisar a mano: (a) en el controller de proveedor, las acciones `BuscarFacturasBase` y `ObtenerFacturaBase` conservan esos nombres (las llama el JS con `/NotasDebitoCompra/BuscarFacturasBase` y `/NotasDebitoCompra/ObtenerFacturaBase`) pero invocan `_notas.ObtenerFacturasCompraBaseAsync(...)` y `_notas.ObtenerFacturaCompraBaseAsync(...)`; (b) la región de constantes del JS y del controller quedó con los valores del proveedor; (c) los ids del DOM del formulario (`formNotaDebitoCompra`, `selectSerieNotaDebitoCompra`, `tblDetalleNotaDebitoCompra`, `btnGuardarNotaDebitoCompra`, `btnCancelarDocNotaDebitoCompra`, `datosSeriesNotaDebitoCompra`, `datosPermisosNotasDebitoCompra`, `tblNotasDebitoCompra`) coinciden entre vista y JS (`grep -o "[A-Za-z]*NotaDebitoCompra[A-Za-z]*\|[A-Za-z]*NotasDebitoCompra[A-Za-z]*" <archivo> | sort -u` en cada uno).

- [ ] **Step 3: Verificación y commit**

`node --check Web.UI/wwwroot/js/notasdebitocompra.js`; `dotnet build Web.slnx` → 0 errores.

```bash
git add Web.UI/Controllers/NotasDebitoCompraController.cs Web.UI/Views/NotasDebitoCompra/Index.cshtml Web.UI/Views/NotasDebitoCompra/_Form.cshtml Web.UI/wwwroot/js/notasdebitocompra.js
git commit -m "feat(notas-debito): pantalla de notas de debito de proveedor (lista, formulario, factura de compra base)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Menú lateral

**Files:**
- Modify: `Web.UI/Views/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: los módulos `NotasDebito` y `NotasDebitoCompra` y las rutas `NotasDebito/Index`, `NotasDebitoCompra/Index` (Tasks 8-9); `Puede(string)`, `EsActivo(string)` y las listas `EsActivoVentas`/`EsActivoCompras`/`MuestraVentas`/`MuestraCompras` ya definidas en el `@{ }` del layout.

**Precondición verificable:** `grep -n 'EsActivoVentas = \|EsActivoCompras = \|MuestraVentas = \|MuestraCompras = ' Web.UI/Views/Shared/_Layout.cshtml` muestra las cuatro líneas con `"Facturas"` / `"FacturasCompra"` como último elemento.

- [ ] **Step 1: Ampliar las listas**

En el bloque `@{ }` del layout, agregar los controllers nuevos a las cuatro listas (conservar el resto idéntico):

```razor
    bool EsActivoVentas = new[] { "Cotizaciones", "Pedidos", "Entregas", "Facturas", "NotasDebito" }.Any(EsActivo);
    bool EsActivoCompras = new[] { "PedidosCompra", "EntregasCompra", "FacturasCompra", "NotasDebitoCompra" }.Any(EsActivo);
    ...
    bool MuestraVentas = new[] { "Cotizaciones", "Pedidos", "Entregas", "Facturas", "NotasDebito" }.Any(Puede);
    bool MuestraCompras = new[] { "PedidosCompra", "EntregasCompra", "FacturasCompra", "NotasDebitoCompra" }.Any(Puede);
```

- [ ] **Step 2: Agregar los dos enlaces**

Inmediatamente después del bloque `@if (Puede("Facturas")) { ... }` (dentro de `#submenuVentas`) insertar:

```razor
                            @if (Puede("NotasDebito"))
                            {
                                <a class="nav-link nav-sublink @(EsActivo("NotasDebito") ? "active" : "")" asp-controller="NotasDebito" asp-action="Index">
                                    <i class="fa-solid fa-file-circle-plus"></i><span>Notas de débito</span>
                                </a>
                            }
```

e inmediatamente después del bloque `@if (Puede("FacturasCompra")) { ... }` (dentro de `#submenuCompras`):

```razor
                            @if (Puede("NotasDebitoCompra"))
                            {
                                <a class="nav-link nav-sublink @(EsActivo("NotasDebitoCompra") ? "active" : "")" asp-controller="NotasDebitoCompra" asp-action="Index">
                                    <i class="fa-solid fa-file-circle-plus"></i><span>Notas de débito de compra</span>
                                </a>
                            }
```

Respetar la indentación y el fin de línea del archivo.

- [ ] **Step 3: Build y commit**

`dotnet build Web.slnx` → 0 errores.

```bash
git add Web.UI/Views/Shared/_Layout.cshtml
git commit -m "feat(notas-debito): enlaces de notas de debito en el menu de Ventas y Compras

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Fase 4 — Verificación

### Task 11: Verificación conjunta (sin commit)

**Files:** ninguno (task de verificación). Las correcciones que hagan falta se commitean como `fix(notas-debito): ...` con el trailer estándar.

**Precondición verificable:** Tasks 0-10 commiteados en ambos repos.

- [ ] **Step 1: API — build y suite completa**

`dotnet test API.sln` (con el `BaseOutputPath` de la cabecera) → 0 errores, todas las pruebas pasando; reportar el total (línea base 967 + las nuevas).

- [ ] **Step 2: Web — árbol commiteado limpio**

En el repo Web: `git worktree add --detach <scratchpad>\wt-check HEAD`; compilar allí (`dotnet build Web.slnx` con un `BaseOutputPath` distinto, p. ej. `...\scratchpad\wtbuild\`) → 0 errores y 0 advertencias; `git worktree remove --force <scratchpad>\wt-check`. Confirmar `node --check` de `notasdebito.js`, `notasdebitocompra.js`.

- [ ] **Step 3: Humo de la API contra una base temporal (no toca `API_DB_TEST`)**

PowerShell, desde `C:\Users\migue\source\repos\angelm0508\API`:

```powershell
$pw = ([regex]::Match((Get-Content API.Service.WebApi\appsettings.json -Raw), 'Password=([^;]*)')).Groups[1].Value
$scratch = 'C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad'
# 1) base nueva desde el script completo
(Get-Content sql\crear-api-db-test-completo.sql -Raw) -replace 'API_DB_TEST','API_DB_SMOKE' | Set-Content "$scratch\smoke.sql" -Encoding UTF8
sqlcmd -S localhost -U sa -P $pw -C -b -f 65001 -i "$scratch\smoke.sql"
# 2) datos minimos: un cliente, un proveedor, un articulo y una factura de venta con una linea (base de la nota)
sqlcmd -S localhost -U sa -P $pw -C -b -d API_DB_SMOKE -Q @"
INSERT INTO SocioNegocio(Codigo,Nombre,TipoSN,Serie,GrupoSN,Activo) VALUES('C0001','Cliente prueba','C',1,101,'S');
INSERT INTO SocioNegocio(Codigo,Nombre,TipoSN,Serie,GrupoSN,Activo) VALUES('P0001','Proveedor prueba','P',2,102,'S');
INSERT INTO Articulo(Codigo,Nombre,CodigoGrupo,CodigoGrpUnidadMedida,FabricanteEntry,Serie) VALUES('A1','Articulo',100,-1,-1,3);
INSERT INTO Factura(NumDoc,Serie,FechaDoc,FechaEmision,CodigoSN,NombreSN,MonedaDoc,TotalDoc) VALUES(1,10,GETDATE(),GETDATE(),'C0001','Cliente prueba','QTZ',16.8);
INSERT INTO FacturaDetalle(Entry,NoLinea,CodArticulo,Descripcion,Cantidad,Precio,PrctjeDesc,CodigoImpuesto,Impuesto,TotalLinea,CodAlmacen) VALUES(1,1,'A1','Tornillo',3,5,0,'IVA',1.8,16.8,'01');
"@
# 3) levantar la API apuntando a esa base (otro puerto)
$env:ConnectionStrings__API_DB = "Server=localhost; Database=API_DB_SMOKE; User=sa; Password=$pw; TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$api = Start-Process dotnet -ArgumentList 'run','--project','API.Service.WebApi','--no-launch-profile','--urls','http://localhost:5199','-p:BaseOutputPath=C:\Users\migue\AppData\Local\Temp\claude\C--Users-migue-source-repos-angelm0508\949e6caf-87d5-4938-88c7-39af8f6d4340\scratchpad\apismoke\' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 25
```

Luego (mismo PowerShell, con la API arriba) ejecutar y registrar las respuestas:

```powershell
$base = 'http://localhost:5199'
$login = Invoke-RestMethod "$base/api/Auth/login" -Method Post -ContentType 'application/json' -Body '{"usuario":"admin","contrasena":"Admin123!"}'
$h = @{ Authorization = "Bearer $($login.dato.token)" }
# facturas base del cliente y detalle de la factura 1 como conceptos
Invoke-RestMethod "$base/api/NotaDebito/FacturasBase/C0001" -Headers $h | ConvertTo-Json -Depth 5
Invoke-RestMethod "$base/api/NotaDebito/FacturaBase/1" -Headers $h | ConvertTo-Json -Depth 6
# crear una nota de debito de cliente en base a la factura 1 (serie 33 = Primario)
$nota = @{ serie=33; codigoSn='C0001'; nombreSn='Cliente prueba'; monedaDoc='QTZ'; baseTipo=6; baseEntry=1; totalDoc=16.8
           lineas=@(@{ descripcion='Tornillo'; cantidad=3; precio=5; prctjeDesc=0; codigoImpuesto='IVA'; impuesto=1.8; totalLinea=16.8 }) } | ConvertTo-Json -Depth 5
$creada = Invoke-RestMethod "$base/api/NotaDebito" -Method Post -Headers $h -ContentType 'application/json' -Body $nota
$creada | ConvertTo-Json
Invoke-RestMethod "$base/api/NotaDebito/$($creada.dato)" -Headers $h | ConvertTo-Json
Invoke-RestMethod "$base/api/NotaDebito/$($creada.dato)/Lineas" -Headers $h | ConvertTo-Json -Depth 4
# eliminar sin cancelar debe fallar (400); cancelar y eliminar debe funcionar
try { Invoke-RestMethod "$base/api/NotaDebito/$($creada.dato)" -Method Delete -Headers $h } catch { $_.ErrorDetails.Message }
Invoke-RestMethod "$base/api/NotaDebito/$($creada.dato)" -Method Put -Headers $h -ContentType 'application/json' -Body '{"cancelado":"S"}' | ConvertTo-Json
Invoke-RestMethod "$base/api/NotaDebito/$($creada.dato)" -Method Delete -Headers $h | ConvertTo-Json
# una nota de cliente con un proveedor debe rechazarse (400)
try { Invoke-RestMethod "$base/api/NotaDebito" -Method Post -Headers $h -ContentType 'application/json' -Body ($nota -replace 'C0001','P0001' -replace '"baseTipo": 6','"baseTipo": -1' -replace '"baseEntry": 1','"baseEntry": null') } catch { $_.ErrorDetails.Message }
# proveedor: crear sin base (serie 35), verificar lineas
$notaP = @{ serie=35; codigoSn='P0001'; monedaDoc='QTZ'; baseTipo=-1; totalDoc=11.2
            lineas=@(@{ descripcion='Diferencia de precio'; cantidad=1; precio=10; prctjeDesc=0; codigoImpuesto='IVA'; impuesto=1.2; totalLinea=11.2 }) } | ConvertTo-Json -Depth 5
$creadaP = Invoke-RestMethod "$base/api/NotaDebitoCompra" -Method Post -Headers $h -ContentType 'application/json' -Body $notaP
$creadaP | ConvertTo-Json
Invoke-RestMethod "$base/api/NotaDebitoCompra/$($creadaP.dato)/Lineas" -Headers $h | ConvertTo-Json -Depth 4
# sin token: 401
try { Invoke-RestMethod "$base/api/NotaDebito" } catch { $_.Exception.Response.StatusCode.value__ }
```

Esperado: facturas base devuelve la factura 1; `FacturaBase/1` devuelve 1 línea convertida en concepto; la creación devuelve `resultado=true` y un `dato` (Entry); `NumDoc` autogenerado = 1 (serie Primario) con `tipoObjeto="7"`/`baseTipo=6`; las líneas se leen; eliminar sin cancelar da 400 con "Cancele el documento (Cancelado='S') antes de eliminarlo."; cancelar y eliminar dan 200; la nota de cliente con proveedor da 400 con "El socio de negocio debe ser un cliente."; la de proveedor se crea con `tipoObjeto="14"`; sin token 401.

Cierre obligatorio del humo (aunque falle algo):

```powershell
Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
Remove-Item Env:\ConnectionStrings__API_DB -ErrorAction SilentlyContinue
sqlcmd -S localhost -U sa -P $pw -C -b -Q "ALTER DATABASE API_DB_SMOKE SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE API_DB_SMOKE"
```

y confirmar que `API_DB_SMOKE` ya no existe y que `API_DB_TEST` sigue sin las tablas nuevas.

- [ ] **Step 4: Estado de git**

`git -C API status --porcelain` → solo ` M API.Service.WebApi/appsettings.json`; `git -C Web status --porcelain` → solo los 4 archivos ajenos. Listar los commits nuevos de ambos repos (`git log --oneline` desde el commit previo a este plan: API `f60718f`, Web `0cd572d`/`3f88c08` según el estado al iniciar).

- [ ] **Step 5: Checklist manual para el usuario (no automatizable aquí)**

Entregar al usuario esta lista (el humo de arriba no cubre la Web):

1. Ejecutar `sql/2026-10-04-notas-debito.sql` en `API_DB_TEST` y reiniciar la depuración de la API y de la Web.
2. Como `admin` (SuperUsuario): ver "Notas de débito" en Ventas y "Notas de débito de compra" en Compras.
3. Crear una nota de cliente libre y otra en base a una factura (el selector se llena al elegir el cliente; "Cargar" precarga moneda, dirección y líneas; "Quitar" las conserva pero quita la referencia).
4. Crear una nota de proveedor con numeración automática y otra con la serie Manual.
5. Cancelar una nota (aparece "Cancelado") y luego eliminarla; intentar eliminar una sin cancelar muestra el mensaje de la API.
6. Con un usuario sin permisos: no ve los módulos; con `Ver` solamente en `NotasDebito` (más `Ver` en `SociosNegocio`, `NumeracionDocumento`): ve la lista sin botones de Nuevo/Eliminar y puede abrir el detalle en solo lectura.
7. Un cliente no aparece en el buscador de la nota de proveedor y viceversa.

- [ ] **Step 6: Integración**

No hay commit en este task. Usar `superpowers:finishing-a-development-branch` para presentar las opciones de integración al usuario una vez completadas las tareas anteriores. Sin push hasta aprobación.

---

## Riesgos y notas de cierre

- **Derivación mecánica del proveedor:** los Tasks 3 (DTOs), 5, 6, 7 y 9 derivan el proveedor del cliente con `sed`; las pruebas de los Tasks 5 y 6 detectan la mayoría de las diferencias, pero la Web no tiene pruebas automáticas: por eso el Task 9 exige los `grep` de revisión y el Task 11 el humo de la API y el checklist manual.
- **Dependencia entre módulos (conocida):** un rol que solo tenga `NotasDebito` necesita además `Ver` sobre `SociosNegocio` y `NumeracionDocumento` (series) para que funcionen los buscadores del formulario; es la misma dependencia que ya tienen los demás documentos.
- **Totales:** se aceptan tal como llegan del formulario (paridad con Factura); el servidor valida cada línea pero no recalcula totales.
- **Concurrencia de numeración:** igual que Factura (lectura de `SigNumero` y actualización en la misma transacción, sin bloqueo adicional).
- **`DocAlias`:** `Nota Debito Prov.` (17 caracteres) respeta el límite de 20 de la columna.
- **Despliegue:** ejecutar `sql/2026-10-04-notas-debito.sql` en cada base antes de arrancar la API; los módulos nuevos quedan sin asignar a ningún usuario (acceso denegado por defecto) salvo `SuperUsuario`.
